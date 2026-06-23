using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_effective_mp_reservation_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestAttributeSnapshotProjectsReservedMp();
        TestCharacterManagementUsesEffectiveMpForClampAndRelease();
        TestBattleUnitFactoryUsesEffectiveMp();
        TestBattleWritebackUsesCurrentEffectiveMpAfterSetupStateChanges();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Effective MP reservation regression"));
    }

    private void TestAttributeSnapshotProjectsReservedMp()
    {
        AttributeService service = new();
        service.SetupContext(
            new AttributeSourceContext
            {
                unit_progress = MakeProgress("snapshot_hero", 30),
            }
        );

        AttributeSnapshot rawSnapshot = service.GetSnapshot();
        _test.Eq(
            rawSnapshot.GetValue("mp_max"),
            30,
            "Unreserved member should expose raw mp_max as effective mp_max."
        );

        AttributeSourceContext reservedContext = new()
        {
            unit_progress = MakeProgress("snapshot_hero_reserved", 30),
        };
        SetReservedMpMax(reservedContext, 12);
        service.SetupContext(reservedContext);

        AttributeSnapshot reservedSnapshot = service.GetSnapshot();
        _test.Eq(
            reservedSnapshot.GetValue("mp_max_unreserved"),
            30,
            "Snapshot should preserve raw mp_max before contingency reservation."
        );
        _test.Eq(
            reservedSnapshot.GetValue("reserved_mp_max"),
            12,
            "Snapshot should expose normalized reserved_mp_max."
        );
        _test.Eq(
            reservedSnapshot.GetValue("mp_max"),
            18,
            "Snapshot should expose effective mp_max after reservation."
        );
    }

    private void TestCharacterManagementUsesEffectiveMpForClampAndRelease()
    {
        PartyState partyState = BuildPartyState(
            ChargedSetup("charged_clamp", 12),
            currentMp: 25
        );
        using CharacterManagementModule manager = BuildManager(partyState);

        AttributeSnapshot chargedSnapshot = manager.GetMemberAttributeSnapshot("hero");
        _test.Eq(
            chargedSnapshot.GetValue("mp_max_unreserved"),
            30,
            "Member snapshot should keep raw mp_max before reservation."
        );
        _test.Eq(
            chargedSnapshot.GetValue("reserved_mp_max"),
            12,
            "Member snapshot should read reservation from charged setup."
        );
        _test.Eq(
            chargedSnapshot.GetValue("mp_max"),
            18,
            "Member snapshot should use effective mp_max while charged."
        );

        manager.CommitBattleResources("hero", current_hp: 20, current_mp: 25, current_aura: 0);
        _test.Eq(
            partyState.GetMemberState("hero").current_mp,
            18,
            "Current MP should clamp from 25 to effective mp_max 18 when refreshed."
        );

        PartyMemberState releasedMember = partyState
            .GetMemberState("hero")
            .WithContingencySetupsForMutation(new[] { UnchargedSetup("charged_clamp") });
        partyState.SetMemberState(releasedMember);

        AttributeSnapshot releasedSnapshot = manager.GetMemberAttributeSnapshot("hero");
        _test.Eq(
            releasedSnapshot.GetValue("reserved_mp_max"),
            0,
            "Cleared setup should release reserved MP."
        );
        _test.Eq(
            releasedSnapshot.GetValue("mp_max"),
            30,
            "Clearing charge should raise effective mp_max to raw mp_max."
        );
        _test.Eq(
            partyState.GetMemberState("hero").current_mp,
            18,
            "Clearing charge should not increase current MP."
        );

        partyState.GetMemberState("hero").SetCurrentMp(10);
        RestoreMemberMpFromSnapshot(partyState.GetMemberState("hero"), releasedSnapshot);
        _test.Eq(
            partyState.GetMemberState("hero").current_mp,
            30,
            "Restoration should use effective mp_max after reservation is released."
        );

        PartyMemberState chargedAgain = partyState
            .GetMemberState("hero")
            .WithContingencySetupsForMutation(new[] { ChargedSetup("charged_clamp", 12) });
        chargedAgain.SetCurrentMp(10);
        partyState.SetMemberState(chargedAgain);
        RestoreMemberMpFromSnapshot(
            partyState.GetMemberState("hero"),
            manager.GetMemberAttributeSnapshot("hero")
        );
        _test.Eq(
            partyState.GetMemberState("hero").current_mp,
            18,
            "Restoration should use effective mp_max, not raw mp_max, while charged."
        );
    }

    private void TestBattleUnitFactoryUsesEffectiveMp()
    {
        PartyState partyState = BuildPartyState(
            ChargedSetup("battle_charge", 12),
            currentMp: 25
        );
        using CharacterManagementModule manager = BuildManager(partyState);
        using BattleRuntimeModule runtime = new();
        runtime.setup(character_gateway: manager);
        BattleUnitFactory factory = new();
        factory.Setup(runtime);

        IReadOnlyList<BattleUnitState> units = factory.BuildAllyUnits(
            partyState,
            new GDictionary()
        );
        _test.Eq(units.Count, 1, "Battle unit factory should build one active ally.");
        BattleUnitState unit = units.Count > 0 ? units[0] : null;
        _test.Eq(
            unit?.attribute_snapshot?.GetValue("mp_max") ?? -1,
            18,
            "Battle unit snapshot should inherit effective mp_max."
        );
        _test.Eq(
            unit?.current_mp ?? -1,
            18,
            "Battle unit current MP should clamp to effective mp_max."
        );

        factory.DisposeRuntime();
    }

    private void TestBattleWritebackUsesCurrentEffectiveMpAfterSetupStateChanges()
    {
        PartyState partyState = BuildPartyState(
            ChargedSetup("writeback_charge", 12),
            currentMp: 5
        );
        using CharacterManagementModule manager = BuildManager(partyState);

        manager.CommitBattleResources("hero", current_hp: 20, current_mp: 28, current_aura: 0);
        _test.Eq(
            partyState.GetMemberState("hero").current_mp,
            18,
            "Unchanged charged member should write back no more than reserved effective mp_max."
        );

        PartyMemberState releasedMember = partyState
            .GetMemberState("hero")
            .WithContingencySetupsForMutation(new[] { UnchargedSetup("writeback_charge") });
        partyState.SetMemberState(releasedMember);
        manager.CommitBattleResources("hero", current_hp: 20, current_mp: 28, current_aura: 0);
        _test.Eq(
            partyState.GetMemberState("hero").current_mp,
            28,
            "Released setup before writeback should clamp against released effective mp_max."
        );
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

    private static PartyState BuildPartyState(
        ContingencyMatrixSetupState setup,
        int currentMp
    )
    {
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
            progression = MakeProgress("hero", 30),
            current_hp = 20,
            current_mp = currentMp,
            current_aura = 0,
        };
        member = member.WithContingencySetupsForMutation(new[] { setup });

        PartyState partyState = new();
        partyState.SetMemberState(member);
        partyState.active_member_ids = new Godot.Collections.Array<StringName> { "hero" };
        partyState.leader_member_id = "hero";
        partyState.main_character_member_id = "hero";
        return partyState;
    }

    private static UnitProgress MakeProgress(StringName unitId, int mpMax)
    {
        UnitProgress progress = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString().Capitalize(),
        };
        progress.unit_base_attributes.SetAttributeValue(AttributeService.HP_MAX, 20);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.MP_MAX, mpMax);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.AURA_MAX, 0);
        return progress;
    }

    private static ContingencyMatrixSetupState ChargedSetup(string setupId, int reservedMpMax) =>
        ContingencyMatrixSetupState.FromDictionary(
            BuildSetupPayload(setupId, charged: true, reservedMpMax, ChargedMaterialCosts())
        );

    private static ContingencyMatrixSetupState UnchargedSetup(string setupId) =>
        ContingencyMatrixSetupState.FromDictionary(
            BuildSetupPayload(setupId, charged: false, reservedMpMax: 0, new GArray())
        );

    private static void RestoreMemberMpFromSnapshot(
        PartyMemberState member,
        AttributeSnapshot snapshot
    )
    {
        int mpMax = Mathf.Max(snapshot?.GetValue(AttributeService.MP_MAX) ?? 0, 0);
        member.SetCurrentMp(mpMax);
    }

    private static void SetReservedMpMax(AttributeSourceContext context, int reservedMpMax)
    {
        FieldInfo field = typeof(AttributeSourceContext).GetField(
            "reserved_mp_max",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        field?.SetValue(context, reservedMpMax);
    }

    private static GArray ChargedMaterialCosts() =>
        new()
        {
            new GDictionary
            {
                ["item_id"] = "special_contingency_gem",
                ["quantity"] = 1,
            },
        };

    private static GDictionary BuildSetupPayload(
        string setupId,
        bool charged,
        int reservedMpMax,
        GArray materialCosts
    )
    {
        return new GDictionary
        {
            ["setup_id"] = setupId,
            ["display_name"] = "Emergency Matrix",
            ["enabled"] = true,
            ["charged"] = charged,
            ["source_skill_id"] = "mage_chain_contingency",
            ["source_skill_level"] = 5,
            ["matrix_load"] = 3,
            ["reserved_mp_max"] = reservedMpMax,
            ["material_costs"] = materialCosts,
            ["trigger"] = new GDictionary
            {
                ["type"] = "hp_below_percent",
                ["subject"] = "owner",
                ["percent"] = 30,
                ["crossing_only"] = true,
                ["timing"] = "after_hp_changed",
            },
            ["release_mode"] = "burst_release",
            ["stored_spells"] = new GArray { BuildStoredSpellPayload("mage_mirror_image") },
        };
    }

    private static GDictionary BuildStoredSpellPayload(string skillId)
    {
        return new GDictionary
        {
            ["stored_skill_id"] = skillId,
            ["cast_level"] = 2,
            ["order"] = 1,
            ["target_resolver"] = new GDictionary { ["type"] = "self" },
            ["parameter_bindings"] = new GDictionary(),
            ["fallback_policy"] = "skip_if_invalid",
        };
    }
}
