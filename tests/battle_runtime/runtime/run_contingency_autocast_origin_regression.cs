using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_contingency_autocast_origin_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestBurstAutoCastBypassesTurnAndCostsButCommitsEffects();
        }
        catch (Exception ex)
        {
            _test.Fail($"Unhandled exception: {ex.GetType().Name}: {ex.Message}");
        }

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Contingency auto-cast origin regression"));
    }

    private void TestBurstAutoCastBypassesTurnAndCostsButCommitsEffects()
    {
        SkillDef storedSkill = StoredBoltSkill();
        PartyState partyState = BuildPartyState(
            ChargedSetup(
                "combat_burst",
                "combat_started",
                "burst_release",
                new GArray
                {
                    StoredSpell("contingency_bolt", 1, Resolver("nearest_enemy_to_owner")),
                }
            )
        );
        using TrackingBattleGateway gateway = new(partyState);
        BattleUnitState caster = Unit("caster", "player", Vector2I.Zero, "hero");
        BattleUnitState activeAlly = Unit("active_ally", "player", new Vector2I(0, 1), "ally");
        BattleUnitState target = Unit("enemy", "enemy", new Vector2I(2, 0), "");

        using BattleRuntimeModule runtime = new();
        runtime.setup(
            character_gateway: gateway,
            skill_defs: new Dictionary<StringName, SkillDef> { [storedSkill.skill_id] = storedSkill }
        );
        BattleState state = BattleTestFixture.BuildFlatState(
            "contingency_auto_cast_origin",
            new Vector2I(4, 3)
        );
        BattleTestFixture.InstallUnits(state, new[] { caster, activeAlly }, new[] { target });
        state.active_unit_id = activeAlly.unit_id;
        state.phase = "unit_acting";
        runtime.SetupStateForTests(state);
        BattleTestFixture.ConfigureDamageResolverForTests(
            runtime,
            new FixedSuccessOneDamageResolver()
        );

        int casterApBefore = caster.current_ap;
        int casterMpBefore = caster.current_mp;
        int casterStaminaBefore = caster.current_stamina;
        int casterAuraBefore = caster.current_aura;

        BattleContingencySystem sidecar = runtime.GetContingencySystemTyped();
        using BattleEventBatch batch = new();
        runtime.OnBattleConfirmed(batch);

        _test.Eq(
            sidecar.GetQueuedReleaseContextsTyped().Count,
            0,
            "combat_started burst release should execute immediately instead of leaving a queued context."
        );

        _test.True(target.current_hp < 30, "auto-cast should apply formal damage to the resolved target.");
        _test.True(
            target.HasStatusEffect("contingency_marked"),
            "auto-cast should commit ordinary status effects through the formal effect path."
        );
        _test.Eq(caster.current_ap, casterApBefore, "auto-cast should not consume caster AP.");
        _test.Eq(caster.current_mp, casterMpBefore, "auto-cast should not consume caster MP.");
        _test.Eq(caster.current_stamina, casterStaminaBefore, "auto-cast should not consume stamina.");
        _test.Eq(caster.current_aura, casterAuraBefore, "auto-cast should not consume aura.");
        _test.Eq(
            caster.GetCooldownTyped(storedSkill.skill_id),
            0,
            "auto-cast should not place the stored skill on cooldown."
        );
        _test.Eq(
            batch.ProgressionDeltasTyped.Count,
            0,
            "auto-cast should not grant battle mastery progression deltas."
        );
        _test.Eq(
            gateway.SkillUsedAchievementEvents,
            0,
            "auto-cast should not emit ordinary skill_used achievements."
        );
        _test.True(
            HasSuppressedOrigin(batch),
            "damage/status report facts from auto-cast should carry CanTriggerContingencies=false."
        );
        _test.Eq(
            sidecar.GetQueuedReleaseContextsTyped().Count,
            0,
            "contingency scanner should not enqueue nested releases from suppressed auto-cast facts."
        );

        runtime.SetupStateForTests(null);
        BattleTestFixture.DisposeBattleState(state);
        BattleTestFixture.DisposeSkill(storedSkill);
    }

    private static bool HasSuppressedOrigin(BattleEventBatch batch)
    {
        foreach (GDictionary reportEntry in batch?.ReportEntriesTyped ?? Array.Empty<GDictionary>())
        {
            if (!reportEntry.ContainsKey("effect_origin"))
                continue;
            GDictionary origin = reportEntry["effect_origin"].AsGodotDictionary();
            if (
                origin.ContainsKey("can_trigger_contingencies")
                && !origin["can_trigger_contingencies"].AsBool()
            )
                return true;
        }
        return false;
    }

    private static SkillDef StoredBoltSkill()
    {
        var skill = new SkillDef
        {
            skill_id = "contingency_bolt",
            display_name = "Contingency Bolt",
            max_level = 5,
            non_core_max_level = 5,
            combat_profile = new CombatSkillDef
            {
                skill_id = "contingency_bolt",
                target_mode = "unit",
                target_team_filter = "enemy",
                target_selection_mode = "single_unit",
                range_value = 5,
                ap_cost = 2,
                mp_cost = 7,
                stamina_cost = 3,
                aura_cost = 1,
                cooldown_tu = 40,
            },
        };
        skill.combat_profile.effect_defs.Add(
            new CombatEffectDef
            {
                effect_type = "damage",
                effect_target_team_filter = "enemy",
                damage_tag = "physical_slash",
                power = 1,
                dice_count = 1,
                dice_sides = 6,
            }
        );
        skill.combat_profile.effect_defs.Add(
            new CombatEffectDef
            {
                effect_type = "status",
                effect_target_team_filter = "enemy",
                status_id = "contingency_marked",
                power = 1,
                duration_tu = 30,
            }
        );
        return skill;
    }

    private static BattleUnitState Unit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        StringName sourceMemberId
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(unitId, factionId, coord, currentAp: 2);
        unit.source_member_id = sourceMemberId;
        unit.current_mp = 20;
        unit.current_stamina = 10;
        unit.current_aura = 5;
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 20);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 5);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 2);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 6);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, AttributeService.BASE_ARMOR_CLASS);
        unit.UnlockCombatResource("mp");
        unit.UnlockCombatResource("stamina");
        unit.UnlockCombatResource("aura");
        unit.AddKnownActiveSkill("contingency_bolt");
        unit.known_skill_level_map[new StringName("contingency_bolt")] = 3;
        return unit;
    }

    private static PartyState BuildPartyState(ContingencyMatrixSetupState setup)
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero", "ally" },
            reserve_member_ids = new GStringNameArray(),
            warehouse_state = new WarehouseState(),
        };
        partyState.SetMemberState(Member("hero", setup));
        partyState.SetMemberState(Member("ally", null));
        return partyState;
    }

    private static PartyMemberState Member(StringName memberId, ContingencyMatrixSetupState setup)
    {
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = memberId.ToString(),
            progression = new UnitProgress
            {
                unit_id = memberId,
                display_name = memberId.ToString(),
            },
            current_hp = 100,
            current_mp = 20,
            current_aura = 5,
        };
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.HP_MAX, 100);
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.MP_MAX, 20);
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.STAMINA_MAX, 10);
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.AURA_MAX, 5);
        return setup != null
            ? member.WithContingencySetupsForMutation(new[] { setup })
            : member;
    }

    private static ContingencyMatrixSetupState ChargedSetup(
        string setupId,
        StringName triggerType,
        StringName releaseMode,
        GArray storedSpells
    ) =>
        ContingencyMatrixSetupState.FromDictionary(
            new GDictionary
            {
                ["setup_id"] = setupId,
                ["display_name"] = setupId,
                ["enabled"] = true,
                ["charged"] = true,
                ["source_skill_id"] = "mage_chain_contingency",
                ["source_skill_level"] = 5,
                ["matrix_load"] = 3,
                ["reserved_mp_max"] = 5,
                ["material_costs"] = new GArray
                {
                    new GDictionary { ["item_id"] = "special_contingency_gem", ["quantity"] = 1 },
                },
                ["trigger"] = Trigger(triggerType),
                ["release_mode"] = releaseMode.ToString(),
                ["stored_spells"] = storedSpells,
            }
        );

    private static GDictionary Trigger(StringName triggerType)
    {
        string timing = triggerType.ToString() switch
        {
            "combat_started" => "after_battle_confirmed",
            "owner_turn_started" => "owner_turn_started",
            _ => "after_hp_changed",
        };
        return new GDictionary
        {
            ["type"] = triggerType.ToString(),
            ["subject"] = "owner",
            ["timing"] = timing,
        };
    }

    private static GDictionary StoredSpell(
        StringName storedSkillId,
        int order,
        ContingencyTargetResolverState resolver
    ) =>
        new()
        {
            ["stored_skill_id"] = storedSkillId.ToString(),
            ["cast_level"] = 3,
            ["order"] = order,
            ["target_resolver"] = resolver.ToDictionary(),
            ["parameter_bindings"] = new GDictionary(),
            ["fallback_policy"] = "skip_if_invalid",
        };

    private static ContingencyTargetResolverState Resolver(string type) =>
        ContingencyTargetResolverState.FromDictionary(new GDictionary { ["type"] = type });

    private sealed class TrackingBattleGateway : IBattleRuntimeCharacterGateway, IDisposable
    {
        private readonly PartyState _partyState;

        internal TrackingBattleGateway(PartyState partyState)
        {
            _partyState = partyState;
        }

        internal int SkillUsedAchievementEvents { get; private set; }

        public PartyState GetPartyState() => _partyState;

        public IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped() =>
            new Dictionary<StringName, ItemDef>();

        public bool HasItemDefCatalog() => false;

        public ItemDef GetItemDef(StringName item_id) => null;

        public PartyMemberState GetMemberState(StringName member_id) =>
            _partyState?.GetMemberState(member_id);

        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
            StringName member_id,
            EquipmentState equipment_view
        )
        {
            AttributeSnapshot snapshot = new();
            snapshot.SetValue(AttributeService.HP_MAX, 100);
            snapshot.SetValue(AttributeService.MP_MAX, 20);
            snapshot.SetValue(AttributeService.STAMINA_MAX, 10);
            snapshot.SetValue(AttributeService.AURA_MAX, 5);
            snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
            return snapshot;
        }

        public WeaponProjection GetMemberWeaponProjectionForEquipmentViewTyped(
            StringName member_id,
            EquipmentState equipment_view
        ) =>
            new();

        public BattleEffectiveTraitProjection BuildEffectiveTraitProjectionForEquipmentView(
            StringName member_id,
            EquipmentState equipment_view
        ) =>
            BattleEffectiveTraitProjection.Empty;

        public PassiveSourceContext BuildPassiveSourceContext(
            StringName member_id,
            UnitProgress progression_state
        ) =>
            null;

        public CharacterProgressionDelta PromoteProfession(
            StringName member_id,
            StringName profession_id,
            PromotionSelectionData selection
        ) =>
            new();

        public BattleResourceCommitResult CommitBattleResources(
            StringName member_id,
            int current_hp,
            int current_mp,
            int current_aura
        ) =>
            BattleResourceCommitResult.Success(member_id);

        public void CommitBattleDeath(StringName member_id) { }

        public int FlushAfterBattle() => (int)Error.Ok;

        public CharacterProgressionDelta GrantBattleMastery(
            StringName member_id,
            StringName skill_id,
            int amount
        ) =>
            new();

        public CharacterProgressionDelta GrantSkillMasteryFromSource(
            StringName member_id,
            StringName skill_id,
            int amount,
            StringName source_type,
            string source_label,
            string reason_text,
            bool emit_achievement_event
        ) =>
            new();

        public GStringNameArray RecordAchievementEvent(
            StringName member_id,
            StringName event_type,
            int amount
        ) =>
            new();

        public GStringNameArray RecordAchievementEvent(
            StringName member_id,
            StringName event_type,
            int amount,
            StringName subject_id,
            GDictionary meta
        )
        {
            if (event_type == "skill_used")
                SkillUsedAchievementEvents++;
            return new GStringNameArray();
        }

        public PendingCharacterReward BuildPendingSkillMasteryReward(
            StringName member_id,
            StringName source_type,
            string source_label,
            IEnumerable<PendingCharacterRewardEntry> entry_options,
            string summary_text
        ) =>
            null;

        public void Dispose() { }
    }
}
