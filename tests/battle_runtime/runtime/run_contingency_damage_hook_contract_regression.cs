using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_contingency_damage_hook_contract_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<BattleRuntimeModule> _runtimeFixtures = new();

    public override void _Initialize()
    {
        try
        {
            TestIncomingDamagePercentTriggersBeforeShieldAndHpMutation();
            TestFatalDamageIncomingUsesProjectedFatalBeforeDeathPrevention();
            TestFatalBlinkOutsideCurrentDamageAreaCancelsCurrentDamage();
            TestHookCancelDoesNotStopLaterEffectsInSameSkill();
            TestExecuteDamagePreservesDamageApplicationHookContext();
            TestGradedSaveExecuteDamagePreservesDamageApplicationHookContext();
            TestHookReportEntriesReachBatchAndRuntimeReport();
            TestZeroDamageDoesNotTriggerContingencyOnHitStatusOrMastery();
        }
        catch (Exception ex)
        {
            _test.Fail($"Contingency damage hook contract regression crashed: {ex}");
        }

        CleanupFixtures();
        RequestTestExit(_test.Finish("Contingency damage hook contract regression"));
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
            EffectArray(DamageEffect(12)),
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
        AssertV1ReportEntry(
            FindReportEntry(batch, "contingency_triggered"),
            "triggered",
            "damage_hook_matched",
            "incoming_guard",
            "incoming_damage_percent",
            "incoming_damage_percent trigger report"
        );
        AssertV1ReportEntry(
            FindReportEntry(batch, "contingency_released"),
            "released",
            "ok",
            "incoming_guard",
            "incoming_damage_percent",
            "incoming_damage_percent release report"
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
            EffectArray(DamageEffect(25)),
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

    private void TestFatalBlinkOutsideCurrentDamageAreaCancelsCurrentDamage()
    {
        BattleRuntimeModule runtime = BuildRuntimeWithSetup(
            ChargedSetup(
                "fatal_blink",
                "fatal_damage_incoming",
                storedSkillId: "contingency_blink",
                targetResolver: EmptyCellResolver("safe_cell", 3)
            )
        );
        BattleUnitState hero = runtime.GetState().GetUnit("hero_unit");
        BattleUnitState enemy = runtime.GetState().GetUnit("enemy_unit");
        hero.current_hp = 10;
        Vector2I originalCoord = hero.coord;

        using BattleEventBatch batch = new();
        runtime.GetDamageResolver().ResolveEffects(
            enemy,
            hero,
            EffectArray(DamageEffect(25)),
            DamageResolutionContext
                .ForSkill("enemy_blink_finisher")
                .WithDamageApplicationHookContext(batch, BattleEffectOrigin.PlayerCommand())
        );

        _test.True(hero.is_alive, "fatal blink should keep the owner alive.");
        _test.Eq(
            hero.current_hp,
            10,
            "fatal blink outside the current damage area should cancel the triggering damage."
        );
        _test.Ne(hero.coord, originalCoord, "fatal blink should relocate the owner before HP mutation.");
        _test.True(
            runtime.GetContingencySystemTyped().IsSetupConsumedForMember("hero", "fatal_blink"),
            "fatal blink should consume the matching setup."
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
            EffectArray(
                DamageEffect(10),
                TestSkillDefinitionProjection.BuildEffect(
                    "status",
                    effectTargetTeamFilter: "enemy",
                    statusId: "burning",
                    power: 1,
                    durationTu: 30
                )
            ),
            DamageResolutionContext.ForSkill("cancel_then_status")
        );

        _test.Eq(target.current_hp, 20, "CancelDamage should cancel only the current damage effect.");
        _test.True(target.HasStatusEffect("burning"), "CancelDamage should not stop later effects in the same skill.");
    }

    private void TestExecuteDamagePreservesDamageApplicationHookContext()
    {
        BattleDamageResolver resolver = new();
        CaptureDamageHook hook = new();
        resolver.SetDamageApplicationHook(hook);
        BattleUnitState source = Unit("execute_source", "player", 100);
        BattleUnitState target = Unit("execute_target", "enemy", 30);
        AutoCastRequest request = HookContextAutoCastRequest();
        using BattleEventBatch batch = new();

        resolver.ResolveEffects(
            source,
            target,
            EffectArray(ExecuteEffect()),
            DamageResolutionContext
                .ForSkill("execute_context_probe")
                .WithDamageApplicationHookContext(batch, BattleEffectOrigin.AutoCast(request))
        );

        _test.Eq(hook.CallCount, 1, "execute fatal damage should invoke the damage hook once.");
        _test.True(
            ReferenceEquals(hook.LastBatch, batch),
            "execute fatal damage should preserve the hook batch."
        );
        _test.Eq(
            hook.LastOrigin?.OriginKind ?? "",
            new StringName("contingency_auto_cast"),
            "execute fatal damage should preserve the auto-cast origin."
        );
        _test.False(
            hook.LastOrigin?.CanTriggerContingencies ?? true,
            "execute fatal damage should preserve contingency suppression origin metadata."
        );
    }

    private void TestGradedSaveExecuteDamagePreservesDamageApplicationHookContext()
    {
        BattleDamageResolver resolver = new();
        CaptureDamageHook hook = new();
        resolver.SetDamageApplicationHook(hook);
        BattleUnitState source = Unit("graded_execute_source", "player", 100);
        BattleUnitState target = Unit("graded_execute_target", "enemy", 40);
        AutoCastRequest request = HookContextAutoCastRequest();
        using BattleEventBatch batch = new();

        resolver.ResolveEffects(
            source,
            target,
            EffectArray(GradedSaveExecuteEffect()),
            DamageResolutionContext
                .Create(
                    false,
                    false,
                    false,
                    skillId: "graded_execute_context_probe",
                    saveRollOverrides: new[] { 5 }
                )
                .WithDamageApplicationHookContext(batch, BattleEffectOrigin.AutoCast(request))
        );

        _test.Eq(hook.CallCount, 1, "graded-save execute fatal damage should invoke the damage hook once.");
        _test.True(
            ReferenceEquals(hook.LastBatch, batch),
            "graded-save execute fatal damage should preserve the hook batch."
        );
        _test.Eq(
            hook.LastOrigin?.OriginKind ?? "",
            new StringName("contingency_auto_cast"),
            "graded-save execute fatal damage should preserve the auto-cast origin."
        );
        _test.False(
            hook.LastOrigin?.CanTriggerContingencies ?? true,
            "graded-save execute fatal damage should preserve contingency suppression origin metadata."
        );
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
            EffectArray(DamageEffect(12)),
            DamageResolutionContext
                .ForSkill("enemy_report_bolt")
                .WithDamageApplicationHookContext(batch, BattleEffectOrigin.PlayerCommand())
        );
        runtime._append_batch_logs_to_state(batch);

        _test.True(
            FindReportEntry(batch, "contingency_triggered").Count > 0,
            "Hook trigger report entry should be visible in BattleEventBatch."
        );
        _test.True(
            HasRuntimeReportEntry(runtime.GetState(), "contingency_triggered"),
            "Hook trigger report entry should be visible in runtime report output after batch flush."
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
            EffectArray(
                DamageEffect(
                    0,
                    new GDictionary
                    {
                        ["grant_status_id"] = "on_hit_focus",
                        ["grant_status_power"] = 1,
                    }
                )
            ),
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
        SkillDefinition guardSkill = GuardSkill();
        SkillDefinition blinkSkill = BlinkSkill();
        runtime.setup(
            character_gateway: gateway,
            skill_definitions: new Dictionary<StringName, SkillDefinition>
            {
                [guardSkill.SkillId] = guardSkill,
                [blinkSkill.SkillId] = blinkSkill,
            }
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

    private static SkillDefinition GuardSkill()
    {
        return TestSkillDefinitionProjection.BuildSkill(
            "contingency_guard",
            displayName: "Contingency Guard",
            maxLevel: 5,
            nonCoreMaxLevel: 5,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "contingency_guard",
                effects: new[]
                {
                    TestSkillDefinitionProjection.BuildEffect(
                        "shield",
                        effectTargetTeamFilter: "ally",
                        power: 20,
                        durationTu: 30
                    ),
                },
                targetMode: "unit",
                targetTeamFilter: "ally",
                targetSelectionMode: "single_unit",
                rangeValue: 5
            )
        );
    }

    private static SkillDefinition BlinkSkill()
    {
        return TestSkillDefinitionProjection.BuildSkill(
            "contingency_blink",
            displayName: "Contingency Blink",
            maxLevel: 5,
            nonCoreMaxLevel: 5,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "contingency_blink",
                effects: new[]
                {
                    TestSkillDefinitionProjection.BuildEffect(
                        "forced_move",
                        forcedMoveMode: "blink",
                        forcedMoveDistance: 5
                    ),
                },
                targetMode: "ground",
                targetTeamFilter: "ally",
                targetSelectionMode: "single_coord",
                rangeValue: 5
            )
        );
    }

    private static CombatEffectDefinition DamageEffect(int power, GDictionary parameters = null) =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            damageTag: "physical_slash",
            power: power,
            parameters: BattleRuntimeEffectDefinitions.CopyVariantDictionary(parameters)
        );

    private static CombatEffectDefinition ExecuteEffect() =>
        TestSkillDefinitionProjection.BuildEffect(
            "execute",
            effectTargetTeamFilter: "enemy",
            damageTag: "physical_slash",
            thresholdBaseValue: 999,
            thresholdMaxHpRatioPercent: 100,
            thresholdCapMaxHpRatioPercent: 100,
            saveDcMode: "none"
        );

    private static CombatEffectDefinition GradedSaveExecuteEffect() =>
        TestSkillDefinitionProjection.BuildEffect(
            "graded_save_execute",
            effectTargetTeamFilter: "enemy",
            damageTag: "psychic",
            saveDcMode: "static",
            saveDc: 10,
            saveDcSourceAbility: "intelligence",
            saveAbility: "willpower",
            saveTag: "illusion",
            savePartialOnSuccess: false,
            parameters: new Dictionary<string, Variant>
            {
                ["profile_id"] = "phantasmal_kill",
                ["failure_execute_threshold_fixed"] = 50,
                ["failure_execute_threshold_max_hp_percent"] = 25,
                ["failure_damage_dice_count"] = 6,
                ["failure_damage_dice_sides"] = 6,
                ["failure_frightened_duration_tu"] = 60,
                ["failure_reaction_lock_duration_tu"] = 30,
                ["critical_failure_execute_threshold_max_hp_percent"] = 35,
                ["critical_failure_damage_dice_count"] = 10,
                ["critical_failure_damage_dice_sides"] = 6,
                ["critical_failure_frightened_duration_tu"] = 90,
                ["critical_failure_stunned_duration_tu"] = 30,
                ["success_aftershock_duration_tu"] = 30,
            }
        );

    private static IReadOnlyList<CombatEffectDefinition> EffectArray(
        params CombatEffectDefinition[] effects
    ) => effects ?? Array.Empty<CombatEffectDefinition>();

    private static ContingencyMatrixSetupState ChargedSetup(
        string setupId,
        StringName triggerType,
        int percent = 0,
        StringName storedSkillId = default,
        GDictionary targetResolver = null
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
                        ["stored_skill_id"] = storedSkillId == default || storedSkillId == ""
                            ? "contingency_guard"
                            : storedSkillId.ToString(),
                        ["cast_level"] = 1,
                        ["order"] = 1,
                        ["target_resolver"] = targetResolver ?? new GDictionary { ["type"] = "self" },
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

    private static GDictionary EmptyCellResolver(string preference, int maxDistance) =>
        new()
        {
            ["type"] = "empty_cell_near_owner",
            ["preference"] = preference,
            ["max_distance"] = maxDistance,
        };

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
        member.progression.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = "mage_chain_contingency",
                is_learned = true,
                skill_level = 5,
            }
        );
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
        unit.attribute_snapshot.SetValue("intelligence", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.UnlockCombatResource("mp");
        unit.UnlockCombatResource("aura");
        return unit;
    }

    private static AutoCastRequest HookContextAutoCastRequest()
    {
        ContingencyReleaseContext releaseContext = new()
        {
            InstanceId = "hero:context_probe",
            SetupId = "context_probe",
            OwnerMemberId = "hero",
            OwnerUnitId = "execute_source",
            CasterUnitId = "execute_source",
            TriggerType = "fatal_damage_incoming",
        };
        return new AutoCastRequest
        {
            CasterUnitId = "execute_source",
            OwnerMemberId = "hero",
            OwnerUnitId = "execute_source",
            SetupId = "context_probe",
            InstanceId = "hero:context_probe",
            StoredSkillId = "execute_context_probe",
            CastLevel = 1,
            TargetResolution = ContingencyTargetResolutionResult.UnitTarget(
                "execute_target",
                Vector2I.Zero
            ),
            ReleaseContext = releaseContext,
            FrozenFacts = ContingencyFrozenTriggerFacts.Empty,
        };
    }

    private void AssertV1ReportEntry(
        IReadOnlyDictionary<string, object> entry,
        string decision,
        string reasonId,
        string setupId,
        string triggerType,
        string message
    )
    {
        _test.True(entry.Count > 0, $"{message} should exist.");
        if (entry.Count == 0)
            return;
        _test.Eq(DictString(entry, "decision"), decision, $"{message} decision mismatch.");
        _test.Eq(DictString(entry, "reason_id"), reasonId, $"{message} reason mismatch.");
        _test.Eq(DictString(entry, "owner_member_id"), "hero", $"{message} owner member mismatch.");
        _test.Eq(DictString(entry, "owner_unit_id"), "hero_unit", $"{message} owner unit mismatch.");
        _test.Eq(DictString(entry, "setup_id"), setupId, $"{message} setup mismatch.");
        _test.True(DictString(entry, "source_event_id") != "", $"{message} should expose source_event_id.");
        _test.True(DictString(entry, "damage_event_id") != "", $"{message} should expose damage_event_id.");
        _test.Ne(
            DictString(entry, "source_event_id"),
            DictString(entry, "damage_event_id"),
            $"{message} source_event_id should identify the contingency source separately from damage_event_id."
        );
        _test.Eq(DictString(entry, "trigger_type"), triggerType, $"{message} trigger mismatch.");
        _test.Eq(DictString(entry, "release_mode"), "burst_release", $"{message} release mode mismatch.");
        _test.Eq(DictString(entry, "stored_skill_id"), "", $"{message} stored skill should be empty.");
        _test.Eq(DictString(entry, "target_resolver"), "", $"{message} target resolver should be empty.");
    }

    private static IReadOnlyDictionary<string, object> FindReportEntry(
        BattleEventBatch batch,
        string entryType
    )
    {
        foreach (
            IReadOnlyDictionary<string, object> entry in
            batch?.ReportEntriesTyped ?? Array.Empty<IReadOnlyDictionary<string, object>>()
        )
            if (DictString(entry, "entry_type") == entryType)
                return entry;
        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static bool HasRuntimeReportEntry(BattleState state, string entryType)
    {
        if (state == null)
            return false;
        foreach (IReadOnlyDictionary<string, object> entry in state.report_entries)
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

    private static string DictString(
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        return source != null && source.TryGetValue(key, out object value)
            ? value?.ToString() ?? ""
            : "";
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

    private sealed class CaptureDamageHook : IBattleDamageApplicationHook
    {
        internal int CallCount { get; private set; }
        internal BattleEventBatch LastBatch { get; private set; }
        internal BattleEffectOrigin LastOrigin { get; private set; }

        public BattleDamageApplicationHookResult BeforeDamageResolved(
            BattleDamageApplicationHookContext context
        )
        {
            CallCount += 1;
            LastBatch = context?.Batch;
            LastOrigin = context?.Origin;
            return BattleDamageApplicationHookResult.None;
        }
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

        public ContingencyConsumedCommitResult ValidateContingencyConsumedSetups(
            StringName member_id,
            IReadOnlyCollection<StringName> consumed_setup_ids
        ) =>
            ContingencyConsumedCommitResult.Success(
                member_id,
                consumed_setup_ids?.Count ?? 0
            );

        public ContingencyConsumedCommitResult CommitContingencyConsumedSetups(
            StringName member_id,
            IReadOnlyCollection<StringName> consumed_setup_ids
        ) =>
            ContingencyConsumedCommitResult.Success(
                member_id,
                consumed_setup_ids?.Count ?? 0
            );

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
