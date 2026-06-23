using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_contingency_trigger_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestBattleLocalSidecarAndReleaseOverlay();
        }
        catch (Exception ex)
        {
            _test.Fail($"Unhandled exception: {ex.GetType().Name}: {ex.Message}");
        }

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Contingency trigger contract regression"));
    }

    private void TestBattleLocalSidecarAndReleaseOverlay()
    {
        PartyState partyState = BuildPartyState();
        PartyMemberState activeMember = partyState.GetMemberState("hero");
        PartyMemberState reserveMember = partyState.GetMemberState("reserve_mage");

        using CharacterManagementModule manager = BuildManager(partyState);
        using BattleRuntimeModule runtime = new();
        runtime.setup(character_gateway: manager);

        BattleUnitState heroUnit = BuildBattleUnit(
            "hero_unit",
            "hero",
            manager.GetMemberAttributeSnapshotForEquipmentView("hero", new EquipmentState())
        );
        BattleState battleState = BuildBattleState(heroUnit);
        runtime.SetupStateForTests(battleState);

        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();
        _test.True(sidecar != null, "BattleRuntimeModule should expose the battle-local contingency sidecar.");
        if (sidecar == null)
            return;

        IReadOnlyList<BattleContingencyInstance> instances = sidecar.GetInstancesTyped();
        _test.Eq(instances.Count, 1, "Only active charged members should create battle-local contingency instances.");
        BattleContingencyInstance instance = instances.Count > 0 ? instances[0] : null;
        _test.Eq(instance?.OwnerMemberId ?? new StringName(""), new StringName("hero"), "Instance should store owner member id.");
        _test.Eq(instance?.OwnerUnitId ?? new StringName(""), new StringName("hero_unit"), "Instance should store owner battle unit id.");
        _test.Eq(instance?.CasterUnitId ?? new StringName(""), new StringName("hero_unit"), "Initial caster unit id should equal owner unit id.");
        _test.False(
            sidecar.HasInstanceForSetup("reserve_mage", "reserve_setup"),
            "Reserve charged setups should remain party reservations, not battle-local instances."
        );
        _test.Eq(
            reserveMember.GetTotalReservedMpMax(),
            8,
            "Reserve member should keep persistent MP reservation while absent from the battle sidecar."
        );

        _test.Eq(
            heroUnit.attribute_snapshot.GetValue(AttributeService.MP_MAX),
            18,
            "Before release overlay, active owner MP max should include persistent contingency reservation."
        );

        ContingencyReleaseContext releaseContext = sidecar.EnterReleaseContext(instance?.InstanceId ?? "");
        _test.True(releaseContext.IsValid, "Entering release context should create a valid release context.");
        _test.Eq(releaseContext.OwnerMemberId, new StringName("hero"), "Release context should preserve owner member id.");
        _test.True(
            sidecar.IsSetupConsumedForMember("hero", "active_setup"),
            "Release context should mark setup consumed in the sidecar overlay."
        );
        _test.True(
            ContainsStringName(heroUnit.GetConsumedContingencySetupIdsTyped(), "active_setup"),
            "Release context should bridge consumed setup ids to BattleUnitState finalization state."
        );
        _test.Eq(
            heroUnit.attribute_snapshot.GetValue(AttributeService.RESERVED_MP_MAX),
            0,
            "Release overlay refresh should remove owner reservation from the battle-local snapshot."
        );
        _test.Eq(
            heroUnit.attribute_snapshot.GetValue(AttributeService.MP_MAX),
            30,
            "Release overlay refresh should restore owner effective MP max for the current battle."
        );

        GDictionary unitPayload = heroUnit.ToDictionary();
        _test.False(
            unitPayload.ContainsKey("contingency_instances"),
            "Battle-local contingency instances must not enter BattleUnitState.ToDictionary()."
        );
        _test.False(
            unitPayload.ContainsKey("consumed_contingency_setup_ids"),
            "Consumed contingency overlay must not enter BattleUnitState save payload."
        );

        runtime.Dispose();
        AssertSetupStillCharged(activeMember, "active_setup", 12, "Uncommitted battle disposal should not mutate active member setup.");
        AssertSetupStillCharged(reserveMember, "reserve_setup", 8, "Uncommitted battle disposal should not mutate reserve member setup.");
    }

    private static bool ContainsStringName(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }

    private void AssertSetupStillCharged(
        PartyMemberState member,
        StringName setupId,
        int reservedMpMax,
        string message
    )
    {
        _test.True(member.TryGetContingencySetupTyped(setupId, out ContingencyMatrixSetupState setup), $"{message} setup should exist.");
        if (setup == null)
            return;
        _test.True(setup.Charged, $"{message} setup should remain charged.");
        _test.Eq(setup.ReservedMpMax, reservedMpMax, $"{message} reserved MP should remain persistent.");
    }

    private static BattleState BuildBattleState(BattleUnitState heroUnit)
    {
        BattleState battleState = BattleTestFixture.BuildFlatState(
            "contingency_trigger_contract",
            new Vector2I(5, 5)
        );
        battleState.SetPartyBackpackView(new WarehouseState());
        BattleTestFixture.InstallUnits(
            battleState,
            new[] { heroUnit },
            new[] { BattleTestFixture.BuildUnit("enemy_unit", "enemy", new Vector2I(4, 4)) }
        );
        return battleState;
    }

    private static BattleUnitState BuildBattleUnit(
        StringName unitId,
        StringName memberId,
        AttributeSnapshot snapshot
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            source_member_id = memberId,
            display_name = "Hero",
            faction_id = "player",
            control_mode = "manual",
            current_hp = 20,
            current_mp = 18,
            current_stamina = 10,
            current_ap = 2,
            current_move_points = BattleUnitState.DefaultMovePointsPerTurn,
            is_alive = true,
            attribute_snapshot = snapshot ?? new AttributeSnapshot(),
        };
        unit.SetEquipmentView(new EquipmentState());
        unit.SetAnchorCoord(new Vector2I(0, 0));
        return unit;
    }

    private static CharacterManagementModule BuildManager(PartyState partyState)
    {
        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            new Dictionary<StringName, SkillDef>(),
            new Dictionary<StringName, ProfessionDef>(),
            new Dictionary<StringName, AchievementDef>(),
            new Dictionary<StringName, ItemDef>(),
            new Dictionary<StringName, QuestDef>(),
            new Dictionary<StringName, TraitDef>(),
            null,
            new ProgressionIdentityCatalogData()
        );
        return manager;
    }

    private static PartyState BuildPartyState()
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            reserve_member_ids = new GStringNameArray { "reserve_mage" },
            warehouse_state = new WarehouseState(),
        };
        partyState.SetMemberState(BuildMember("hero", "Hero", ChargedSetup("active_setup", reservedMpMax: 12)));
        partyState.SetMemberState(BuildMember("reserve_mage", "Reserve Mage", ChargedSetup("reserve_setup", reservedMpMax: 8)));
        return partyState;
    }

    private static PartyMemberState BuildMember(
        StringName memberId,
        string displayName,
        ContingencyMatrixSetupState setup
    )
    {
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = displayName,
            progression = MakeProgress(memberId),
            current_hp = 20,
            current_mp = 5,
            current_aura = 0,
        };
        return member.WithContingencySetupsForMutation(new[] { setup });
    }

    private static UnitProgress MakeProgress(StringName memberId)
    {
        UnitProgress progress = new()
        {
            unit_id = memberId,
            display_name = memberId.ToString(),
        };
        progress.unit_base_attributes.SetAttributeValue(AttributeService.HP_MAX, 20);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.MP_MAX, 30);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.AURA_MAX, 0);
        return progress;
    }

    private static ContingencyMatrixSetupState ChargedSetup(string setupId, int reservedMpMax) =>
        ContingencyMatrixSetupState.FromDictionary(
            new GDictionary
            {
                ["setup_id"] = setupId,
                ["display_name"] = "Emergency Matrix",
                ["enabled"] = true,
                ["charged"] = true,
                ["source_skill_id"] = "mage_chain_contingency",
                ["source_skill_level"] = 5,
                ["matrix_load"] = 3,
                ["reserved_mp_max"] = reservedMpMax,
                ["material_costs"] = new GArray
                {
                    new GDictionary
                    {
                        ["item_id"] = "special_contingency_gem",
                        ["quantity"] = 1,
                    },
                },
				["trigger"] = new GDictionary
				{
					["type"] = "hp_below_percent",
					["subject"] = "owner",
					["percent"] = 30,
					["crossing_only"] = true,
					["timing"] = "after_hp_changed",
				},
                ["release_mode"] = "burst_release",
                ["stored_spells"] = new GArray
                {
                    new GDictionary
                    {
                        ["stored_skill_id"] = "mage_mirror_image",
                        ["cast_level"] = 2,
                        ["order"] = 1,
                        ["target_resolver"] = new GDictionary { ["type"] = "self" },
                        ["parameter_bindings"] = new GDictionary(),
                        ["fallback_policy"] = "skip_if_invalid",
                    },
                },
            }
        );
}
