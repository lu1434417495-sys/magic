using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_contingency_damage_hook_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<BattleRuntimeModule> _runtimeFixtures = new();

    public override void _Initialize()
    {
        try
        {
            TestIncomingDamagePercentTriggersBeforeShieldAndHpMutation();
            TestFatalDamageIncomingUsesProjectedFatalBeforeDeathPrevention();
            TestHookCancelDoesNotStopLaterEffectsInSameSkill();
            TestHookReportEntriesReachBatchAndRuntimeReport();
            TestZeroDamageDoesNotTriggerContingencyOnHitStatusOrMastery();
        }
        catch (Exception ex)
        {
            _test.Fail($"Contingency damage hook contract regression crashed: {ex}");
        }

        CleanupFixtures();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Contingency damage hook contract regression"));
    }

    private void TestIncomingDamagePercentTriggersBeforeShieldAndHpMutation()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSetup(
            ChargedSetup("incoming_guard", "incoming_damage_percent", percent: 50)
        );
        BattleUnitState hero = runtime.GetState().GetUnit("hero_unit");
        BattleUnitState enemy = runtime.GetState().GetUnit("enemy_unit");
        _test.Eq(
            runtime.GetContingencySystemTyped().GetInstancesTyped().Count,
            1,
            "incoming_damage_percent fixture should create one battle-local setup instance."
        );

        using BattleEventBatch batch = new();
        runtime.GetDamageResolver().ResolveEffects(
            enemy,
            hero,
            new GArray { DamageEffect(12) },
            DamageResolutionContext
                .ForSkill("enemy_bolt")
                .WithDamageApplicationHookContext(batch, BattleEffectOrigin.PlayerCommand())
        );

        _test.Eq(hero.current_hp, 20, "incoming_damage_percent auto-shield should resolve before HP mutation.");
        _test.Eq(hero.current_shield_hp, 8, "incoming_damage_percent auto-shield should absorb the triggering hit.");
        _test.True(
            runtime.GetContingencySystemTyped().IsSetupConsumedForMember("hero", "incoming_guard"),
            "incoming_damage_percent should consume the matching setup."
        );
        _test.True(
            HasReportEntry(batch, "contingency_damage_hook"),
            "incoming_damage_percent should append a hook report entry to the live batch."
        );
    }

    private void TestFatalDamageIncomingUsesProjectedFatalBeforeDeathPrevention()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSetup(
            ChargedSetup("fatal_guard", "fatal_damage_incoming")
        );
        BattleUnitState hero = runtime.GetState().GetUnit("hero_unit");
        BattleUnitState enemy = runtime.GetState().GetUnit("enemy_unit");
        hero.current_hp = 10;

        using BattleEventBatch batch = new();
        runtime.GetDamageResolver().ResolveEffects(
            enemy,
            hero,
            new GArray { DamageEffect(25) },
            DamageResolutionContext
                .ForSkill("enemy_finisher")
                .WithDamageApplicationHookContext(batch, BattleEffectOrigin.PlayerCommand())
        );

        _test.True(hero.is_alive, "fatal_damage_incoming should react before the fatal HP mutation.");
        _test.Eq(hero.current_hp, 5, "fatal_damage_incoming shield should leave projected nonfatal HP.");
        _test.True(
            runtime.GetContingencySystemTyped().IsSetupConsumedForMember("hero", "fatal_guard"),
            "fatal_damage_incoming should consume the matching setup."
        );
    }

    private void TestHookCancelDoesNotStopLaterEffectsInSameSkill()
    {
        BattleDamageResolver resolver = new();
        resolver.SetDamageApplicationHook(new CancelDamageHook());
        BattleUnitState source = Unit("cancel_source", "enemy", 20);
        BattleUnitState target = Unit("cancel_target", "player", 20);

        resolver.ResolveEffects(
            source,
            target,
            new GArray
            {
                DamageEffect(10),
                new CombatEffectDef
                {
                    effect_type = "status",
                    effect_target_team_filter = "enemy",
                    status_id = "burning",
                    power = 1,
                    duration_tu = 30,
                },
            },
            DamageResolutionContext.ForSkill("cancel_then_status")
        );

        _test.Eq(target.current_hp, 20, "CancelDamage should cancel only the current damage effect.");
        _test.True(target.HasStatusEffect("burning"), "CancelDamage should not stop later effects in the same skill.");
    }

    private void TestHookReportEntriesReachBatchAndRuntimeReport()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSetup(
            ChargedSetup("report_guard", "incoming_damage_percent", percent: 50)
        );
        BattleUnitState hero = runtime.GetState().GetUnit("hero_unit");
        BattleUnitState enemy = runtime.GetState().GetUnit("enemy_unit");

        using BattleEventBatch batch = new();
        runtime.GetDamageResolver().ResolveEffects(
            enemy,
            hero,
            new GArray { DamageEffect(12) },
            DamageResolutionContext
                .ForSkill("enemy_report_bolt")
                .WithDamageApplicationHookContext(batch, BattleEffectOrigin.PlayerCommand())
        );
        runtime._append_batch_logs_to_state(batch);

        _test.True(HasReportEntry(batch, "contingency_damage_hook"), "Hook report entry should be visible in BattleEventBatch.");
        _test.True(
            HasRuntimeReportEntry(runtime.GetState(), "contingency_damage_hook"),
            "Hook report entry should be visible in runtime report output after batch flush."
        );
    }

    private void TestZeroDamageDoesNotTriggerContingencyOnHitStatusOrMastery()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSetup(
            ChargedSetup("zero_guard", "incoming_damage_percent", percent: 1)
        );
        BattleUnitState hero = runtime.GetState().GetUnit("hero_unit");
        BattleUnitState enemy = runtime.GetState().GetUnit("enemy_unit");

        using BattleEventBatch batch = new();
        runtime.GetDamageResolver().ResolveEffects(
            enemy,
            hero,
            new GArray
            {
                DamageEffect(
                    0,
                    new GDictionary
                    {
                        ["grant_status_id"] = "on_hit_focus",
                        ["grant_status_power"] = 1,
                    }
                ),
            },
            DamageResolutionContext
                .ForSkill("zero_damage_probe")
                .WithDamageApplicationHookContext(batch, BattleEffectOrigin.PlayerCommand())
        );

        _test.False(
            runtime.GetContingencySystemTyped().IsSetupConsumedForMember("hero", "zero_guard"),
            "damage=0 should not trigger incoming damage contingency."
        );
        _test.False(enemy.HasStatusEffect("on_hit_focus"), "damage=0 should not grant on-hit source status.");
        _test.Eq(batch.ProgressionDeltasTyped.Count, 0, "damage=0 should not create mastery side effects.");
    }

    private BattleRuntimeModule BuildRuntimeWithSetup(ContingencyMatrixSetupState setup)
    {
        PartyState partyState = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new GStringNameArray { "hero" },
            reserve_member_ids = new GStringNameArray(),
            warehouse_state = new WarehouseState(),
        };
        partyState.SetMemberState(Member("hero", setup));
        TrackingBattleGateway gateway = new(partyState);
        BattleRuntimeModule runtime = Track(new BattleRuntimeModule());
        SkillDef guardSkill = GuardSkill();
        runtime.setup(
            character_gateway: gateway,
            skill_defs: new Dictionary<StringName, SkillDef> { [guardSkill.skill_id] = guardSkill }
        );
        BattleState state = BattleTestFixture.BuildFlatState(
            $"damage_hook_{setup?.SetupId}",
            new Vector2I(4, 3)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { Unit("hero_unit", "player", 20, "hero", Vector2I.Zero) },
            new[] { Unit("enemy_unit", "enemy", 20, "", new Vector2I(2, 0)) }
        );
        runtime.SetupStateForTests(state);
        return runtime;
    }

    private static SkillDef GuardSkill()
    {
        SkillDef skill = new()
        {
            skill_id = "contingency_guard",
            display_name = "Contingency Guard",
            max_level = 5,
            non_core_max_level = 5,
            combat_profile = new CombatSkillDef
            {
                skill_id = "contingency_guard",
                target_mode = "unit",
                target_team_filter = "ally",
                target_selection_mode = "single_unit",
                range_value = 5,
            },
        };
        skill.combat_profile.effect_defs.Add(
            new CombatEffectDef
            {
                effect_type = "shield",
                effect_target_team_filter = "ally",
                power = 20,
                duration_tu = 30,
            }
        );
        return skill;
    }

    private static CombatEffectDef DamageEffect(int power, GDictionary parameters = null) =>
        new()
        {
            effect_type = "damage",
            effect_target_team_filter = "enemy",
            damage_tag = "physical_slash",
            power = power,
            @params = parameters ?? new GDictionary(),
        };

    private static ContingencyMatrixSetupState ChargedSetup(
        string setupId,
        StringName triggerType,
        int percent = 0
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
                ["material_costs"] = new GArray(),
                ["trigger"] = Trigger(triggerType, percent),
                ["release_mode"] = "burst_release",
                ["stored_spells"] = new GArray
                {
                    new GDictionary
                    {
                        ["stored_skill_id"] = "contingency_guard",
                        ["cast_level"] = 1,
                        ["order"] = 1,
                        ["target_resolver"] = new GDictionary { ["type"] = "self" },
                        ["parameter_bindings"] = new GDictionary(),
                        ["fallback_policy"] = "skip_if_invalid",
                    },
                },
            }
        );

    private static GDictionary Trigger(StringName triggerType, int percent)
    {
        GDictionary trigger = new()
        {
            ["type"] = triggerType.ToString(),
            ["subject"] = "owner",
            ["timing"] = "before_damage_resolved",
        };
        if (triggerType == "incoming_damage_percent")
        {
            trigger["damage_percent"] = percent;
            trigger["damage_basis"] = "max_hp";
            trigger["damage_amount_mode"] = "projected_hp_damage_after_shield";
        }
        return trigger;
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
            current_hp = 20,
            current_mp = 200,
        };
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.HP_MAX, 20);
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.MP_MAX, 200);
        member.progression.unit_base_attributes.SetAttributeValue(AttributeService.ACTION_POINTS, 2);
        return setup != null
            ? member.WithContingencySetupsForMutation(new[] { setup })
            : member;
    }

    private static BattleUnitState Unit(
        StringName unitId,
        StringName factionId,
        int hp,
        StringName memberId = default,
        Vector2I? coord = null
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            factionId,
            coord ?? Vector2I.Zero,
            currentAp: 2
        );
        unit.source_member_id = memberId == default ? new StringName("") : memberId;
        unit.current_hp = hp;
        unit.current_mp = 200;
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, hp);
        unit.attribute_snapshot.SetValue(AttributeService.MP_MAX, 200);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        return unit;
    }

    private static bool HasReportEntry(BattleEventBatch batch, string entryType)
    {
        foreach (GDictionary entry in batch?.ReportEntriesTyped ?? Array.Empty<GDictionary>())
            if (DictString(entry, "entry_type") == entryType)
                return true;
        return false;
    }

    private static bool HasRuntimeReportEntry(BattleState state, string entryType)
    {
        if (state == null)
            return false;
        foreach (GDictionary entry in state.report_entries)
        {
            if (DictString(entry, "entry_type") == entryType)
                return true;
        }
        return false;
    }

    private static string DictString(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return "";
        return source[key].AsString();
    }

    private BattleRuntimeModule Track(BattleRuntimeModule runtime)
    {
        if (runtime != null)
            _runtimeFixtures.Add(runtime);
        return runtime;
    }

    private void CleanupFixtures()
    {
        foreach (BattleRuntimeModule runtime in _runtimeFixtures)
        {
            runtime?.SetupStateForTests(null);
            runtime?.Dispose();
        }
        _runtimeFixtures.Clear();
    }

    private sealed class CancelDamageHook : IBattleDamageApplicationHook
    {
        public BattleDamageApplicationHookResult BeforeDamageResolved(
            BattleDamageApplicationHookContext context
        ) =>
            BattleDamageApplicationHookResult.Cancel();
    }

    private sealed class TrackingBattleGateway : IBattleRuntimeCharacterGateway, IDisposable
    {
        private readonly PartyState _partyState;

        internal TrackingBattleGateway(PartyState partyState)
        {
            _partyState = partyState;
        }

        public PartyState GetPartyState() => _partyState;
        public IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped() => new Dictionary<StringName, ItemDef>();
        public bool HasItemDefCatalog() => false;
        public ItemDef GetItemDef(StringName item_id) => null;
        public PartyMemberState GetMemberState(StringName member_id) => _partyState?.GetMemberState(member_id);

        public AttributeSnapshot GetMemberAttributeSnapshotForEquipmentView(
            StringName member_id,
            EquipmentState equipment_view
        )
        {
            AttributeSnapshot snapshot = new();
            snapshot.SetValue(AttributeService.HP_MAX, 20);
            snapshot.SetValue(AttributeService.MP_MAX, 200);
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
        ) =>
            new();

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
