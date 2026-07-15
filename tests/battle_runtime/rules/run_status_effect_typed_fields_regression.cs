using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_status_effect_typed_fields_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestCombatEffectDefExposesLockGuardTypedField();
        TestEffectSchemaRejectsLegacyLockGuardParam();
        TestStatusParamsNoLongerDriveTypedStatusSemantics();
        TestTypedFieldsDriveStatusSemantics();
        TestGuardLockBlocksGuardGrantingSkillsWithDistinctReason();
        TestStatusEffectLockGuardRoundTripUsesTopLevelField();
        TestRuntimeWrapperForwardsTypedFields();
        TestRuntimeWrapperForwardsLockGuardTypedField();
        TestRuntimeDebuffWrapperForwardsDebuffSemantics();
        TestRuntimeBodySizeOverrideWrapperForwardsTypedFields();
        TestRuntimeSourceWrapperForwardsCoreFields();

        RequestTestExit(_test.Finish("Status effect typed fields regression"));
    }



    private void TestCombatEffectDefExposesLockGuardTypedField()
    {
        using CombatEffectDef effect = new() { lock_guard = true };
        _test.True(
            effect.lock_guard,
            "CombatEffectDef.lock_guard should be a public bool typed field."
        );
    }

    private void TestEffectSchemaRejectsLegacyLockGuardParam()
    {
        using var registry = new SkillContentRegistry(
            new TestContentResourceLoader(),
            loadDefaultContent: false
        );
        var errors = new GStringArray();
        var effectDef = TestResourceOwnership.Own(
            new CombatEffectDef
            {
                effect_type = "status",
                status_id = "legacy_lock_guard_param",
                @params = new GDictionary { ["lock_guard"] = true },
            },
            "StatusEffectTypedFields.legacy-lock-guard-effect"
        );

        registry.AppendEffectValidationErrors(
            errors,
            "lock_guard_schema_contract",
            effectDef,
            "test_effect"
        );

        _test.True(
            ContainsFragment(errors, "params.lock_guard")
            && ContainsFragment(errors, "CombatEffectDef.lock_guard"),
            $"params.lock_guard should be rejected in favor of CombatEffectDef.lock_guard. errors={FormatErrors(errors)}"
        );
    }

    private void TestStatusParamsNoLongerDriveTypedStatusSemantics()
    {
        var resolver = new BattleRuntimeSkillTurnResolver();

        BattleUnitState counterLockUnit = BuildUnit("legacy_counter_lock");
        SetStatusParams(
            counterLockUnit,
            "legacy_counter_lock",
            new GDictionary { ["lock_counterattack"] = true }
        );
        _test.False(
            resolver.HasCounterattackLockStatus(counterLockUnit),
            "status params.lock_counterattack must not drive typed counterattack locks."
        );

        BattleUnitState guardLockUnit = BuildUnit("legacy_guard_lock");
        SetStatusParams(
            guardLockUnit,
            "legacy_guard_lock",
            new GDictionary { ["lock_guard"] = true }
        );
        _test.False(
            resolver.HasGuardLockStatus(guardLockUnit),
            "status params.lock_guard must not drive typed guard locks."
        );

        BattleUnitState critLockUnit = BuildUnit("legacy_crit_lock");
        SetStatusParams(
            critLockUnit,
            "legacy_crit_lock",
            new GDictionary { ["lock_crit"] = true }
        );
        _test.False(
            BattleFateAttackRules.IsAttackCritLocked(critLockUnit),
            "status params.lock_crit must not drive typed crit locks."
        );

        BattleUnitState mainSkillLockUnit = BuildUnit("legacy_main_skill_lock");
        SetStatusParams(
            mainSkillLockUnit,
            "legacy_main_skill_lock",
            new GDictionary { ["main_skill_lock_other_debuff_count"] = 2 }
        );
        _test.Eq(
            resolver.GetMainSkillLockOtherDebuffCount(mainSkillLockUnit),
            0,
            "status params.main_skill_lock_other_debuff_count must not drive typed main skill locks."
        );

        BattleUnitState customDebuffUnit = BuildUnit("legacy_custom_debuff");
        SetStatusParams(
            customDebuffUnit,
            "custom_debuff",
            new GDictionary { ["counts_as_debuff"] = true }
        );
        _test.Eq(
            resolver.CountDebuffStatuses(customDebuffUnit),
            0,
            "status params.counts_as_debuff=true must not mark custom statuses as debuffs."
        );

        BattleUnitState burningOverrideUnit = BuildUnit("legacy_burning_override");
        SetStatusParams(
            burningOverrideUnit,
            "burning",
            new GDictionary { ["counts_as_debuff"] = false }
        );
        _test.Eq(
            resolver.CountDebuffStatuses(burningOverrideUnit),
            1,
            "status params.counts_as_debuff=false must not override built-in debuff semantics."
        );
        DisposeUnits(
            counterLockUnit,
            guardLockUnit,
            critLockUnit,
            mainSkillLockUnit,
            customDebuffUnit,
            burningOverrideUnit
        );
    }

    private void TestTypedFieldsDriveStatusSemantics()
    {
        var resolver = new BattleRuntimeSkillTurnResolver();

        BattleUnitState counterLockUnit = BuildUnit("typed_counter_lock");
        SetTypedStatus(
            counterLockUnit,
            "typed_counter_lock",
            lockCounterattack: true
        );
        _test.True(
            resolver.HasCounterattackLockStatus(counterLockUnit),
            "typed lock_counterattack must drive counterattack locks."
        );

        BattleUnitState guardLockUnit = BuildUnit("typed_guard_lock");
        SetTypedStatus(
            guardLockUnit,
            "typed_guard_lock",
            lockGuard: true
        );
        _test.True(
            resolver.HasGuardLockStatus(guardLockUnit),
            "typed lock_guard must drive guard locks."
        );

        BattleUnitState mainSkillLockUnit = BuildUnit("typed_main_skill_lock");
        SetTypedStatus(
            mainSkillLockUnit,
            "typed_main_skill_lock",
            mainSkillLockOtherDebuffCount: 2
        );
        _test.Eq(
            resolver.GetMainSkillLockOtherDebuffCount(mainSkillLockUnit),
            2,
            "typed main_skill_lock_other_debuff_count must drive main skill locks."
        );

        BattleUnitState critLockUnit = BuildUnit("typed_crit_lock");
        SetTypedStatus(
            critLockUnit,
            "typed_crit_lock",
            lockCrit: true
        );
        _test.True(
            BattleFateAttackRules.IsAttackCritLocked(critLockUnit),
            "typed lock_crit must drive crit locks."
        );

        BattleUnitState customDebuffUnit = BuildUnit("typed_custom_debuff");
        SetTypedStatus(
            customDebuffUnit,
            "custom_debuff",
            countsAsDebuffOverride: true,
            countsAsDebuff: true
        );
        _test.Eq(
            resolver.CountDebuffStatuses(customDebuffUnit),
            1,
            "typed counts_as_debuff=true must mark custom statuses as debuffs."
        );

        BattleUnitState burningOverrideUnit = BuildUnit("typed_burning_override");
        SetTypedStatus(
            burningOverrideUnit,
            "burning",
            countsAsDebuffOverride: true,
            countsAsDebuff: false
        );
        _test.Eq(
            resolver.CountDebuffStatuses(burningOverrideUnit),
            0,
            "typed counts_as_debuff=false must override built-in debuff semantics."
        );
        DisposeUnits(
            counterLockUnit,
            guardLockUnit,
            mainSkillLockUnit,
            critLockUnit,
            customDebuffUnit,
            burningOverrideUnit
        );
    }

    private void TestStatusEffectLockGuardRoundTripUsesTopLevelField()
    {
        var effect = new BattleStatusEffectState
        {
            status_id = "typed_guard_lock_round_trip",
            source_unit_id = "source_unit",
            power = 1,
            stacks = 1,
            lock_guard = true,
            @params = new GDictionary { ["legacy_marker"] = true },
        };
        _test.True(
            effect.lock_guard,
            "BattleStatusEffectState.lock_guard should be settable as a typed field."
        );

        using GodotProjectionLease<GDictionary> payloadLease = effect.ToDictionaryLease();
        GDictionary payload = payloadLease.Value;
        _test.True(
            payload.ContainsKey("lock_guard"),
            "BattleStatusEffectState.ToDictionary() should project lock_guard as a top-level field."
        );
        using GDictionary paramsPayload = payload["params"].AsGodotDictionary();
        _test.True(
            payload.ContainsKey("params")
            && payload["params"].VariantType == Variant.Type.Dictionary
            && !paramsPayload.ContainsKey("lock_guard"),
            "BattleStatusEffectState.ToDictionary() should not project lock_guard through params."
        );

        BattleStatusEffectState restored = BattleStatusEffectState.FromDictionary(payload);
        _test.True(
            restored?.lock_guard == true,
            "BattleStatusEffectState.FromDictionary() should preserve top-level lock_guard."
        );
    }

    private void TestGuardLockBlocksGuardGrantingSkillsWithDistinctReason()
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();
        BattleUnitState unit = BuildUnit("guard_lock_cast_block_unit");
        unit.current_ap = 3;
        unit.current_mp = 3;
        unit.current_stamina = 3;
        unit.current_aura = 3;
        SetTypedStatus(unit, "typed_guard_lock_block", lockGuard: true);
        SkillDefinition guardDefinition = BuildGuardGrantingSkill();

        try
        {
            BattleSkillCastBlockReasonKind blockReason = runtime.GetSkillCastBlockReason(
                unit,
                guardDefinition
            );
            _test.Eq(
                blockReason,
                BattleSkillCastBlockReasonKind.GuardLockedByStatus,
                "typed lock_guard should block guard-granting skills with a non-black-star reason."
            );
            _test.Eq(
                BattleSkillCastBlockReasonKinds.ToTraceKey(blockReason),
                "guard_locked_by_status",
                "typed lock_guard block reason should project a stable trace key."
            );
            _test.True(
                runtime.GetSkillCastBlockMessage(unit, guardDefinition).Contains("格挡"),
                "typed lock_guard block reason should produce a guard-lock message."
            );

            unit.SetStatusEffect(
                new BattleStatusEffectState
                {
                    status_id = "black_star_brand_normal",
                    power = 1,
                    stacks = 1,
                }
            );
            _test.Eq(
                runtime.GetSkillCastBlockReason(unit, guardDefinition),
                BattleSkillCastBlockReasonKind.BlackStarGuardLock,
                "black-star guard lock should keep its existing distinct block reason."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(unit);
            runtime.Dispose();
        }
    }

    private void TestRuntimeWrapperForwardsTypedFields()
    {
        var runtime = new BattleRuntimeModule();
        BattleUnitState unit = BuildUnit("runtime_wrapper_unit");
        try
        {
            runtime._set_runtime_status_effect(
                unit,
                "runtime_wrapper_status",
                10,
                "source_unit",
                1,
                new GDictionary(),
                source_profile_id: "prismatic_sphere",
                source_layer_id: "green",
                source_skill_id: "runtime_source_skill",
                source_skill_level: 4,
                self_save_dc: 18,
                self_save_ability: "willpower",
                self_save_tag: "magic",
                self_save_roll_override: 17,
                save_bonus: 2,
                control_save_bonus: 1,
                range_bonus: 5,
                passive_reduction: 6,
                content_dr: 4,
                guard_block: 3,
                save_advantage_tags: new List<StringName> { "magic", "fear" },
                save_disadvantage_tags: new List<StringName> { "poison" },
                save_immunity_tags: new List<StringName> { "sleep" },
                save_tags: new List<StringName> { "magic", "mind" },
                heal_multiplier_percent: 50,
                shield_gain_multiplier_percent: 40,
                incoming_damage_multiplier: 1.25,
                outgoing_damage_multiplier: 0.75,
                damage_tag: "fire",
                damage_tags: new List<StringName> { "fire", "slash" },
                damage_category: "elemental",
                attack_roll_penalty: 6,
                undispellable: true,
                dispellable_harmful_magic: true,
                mitigation_tier: "half",
                dr_bypass_tag: "armor_pierce",
                counts_as_debuff_override: true,
                counts_as_debuff: true,
                forced_move_immune: true,
                lock_counterattack: true,
                lock_dodge_bonus: true,
                lock_crit: true,
                main_skill_lock_other_debuff_count: 3,
                stack_behavior: "add",
                stack_limit: 9
            );

            BattleStatusEffectState status = unit.GetStatusEffect("runtime_wrapper_status");
            _test.True(status != null, "runtime wrapper should create the status effect.");
            _test.True(
                status?.counts_as_debuff_override == true && status.counts_as_debuff,
                "runtime wrapper should forward typed debuff override fields."
            );
            _test.True(
                status?.forced_move_immune == true,
                "runtime wrapper should forward typed forced_move_immune field."
            );
            _test.Eq(
                status?.source_skill_id ?? new StringName(""),
                new StringName("runtime_source_skill"),
                "runtime wrapper should forward typed source_skill_id field."
            );
            _test.Eq(
                status?.source_profile_id ?? new StringName(""),
                new StringName("prismatic_sphere"),
                "runtime wrapper should forward typed source_profile_id field."
            );
            _test.Eq(
                status?.source_layer_id ?? new StringName(""),
                new StringName("green"),
                "runtime wrapper should forward typed source_layer_id field."
            );
            _test.Eq(
                status?.source_skill_level ?? -1,
                4,
                "runtime wrapper should forward typed source_skill_level field."
            );
            _test.Eq(
                status?.self_save_dc ?? -1,
                18,
                "runtime wrapper should forward typed self_save_dc field."
            );
            _test.Eq(
                status?.self_save_ability ?? new StringName(""),
                new StringName("willpower"),
                "runtime wrapper should forward typed self_save_ability field."
            );
            _test.Eq(
                status?.self_save_tag ?? new StringName(""),
                new StringName("magic"),
                "runtime wrapper should forward typed self_save_tag field."
            );
            _test.Eq(
                status?.self_save_roll_override ?? -1,
                17,
                "runtime wrapper should forward typed self_save_roll_override field."
            );
            _test.Eq(
                status?.save_bonus ?? -1,
                2,
                "runtime wrapper should forward typed save_bonus field."
            );
            _test.Eq(
                status?.control_save_bonus ?? -1,
                1,
                "runtime wrapper should forward typed control_save_bonus field."
            );
            _test.Eq(
                status?.range_bonus ?? -1,
                5,
                "runtime wrapper should forward typed range_bonus field."
            );
            _test.Eq(
                status?.passive_reduction ?? -1,
                6,
                "runtime wrapper should forward typed passive_reduction field."
            );
            _test.Eq(
                status?.content_dr ?? -1,
                4,
                "runtime wrapper should forward typed content_dr field."
            );
            _test.Eq(
                status?.guard_block ?? -1,
                3,
                "runtime wrapper should forward typed guard_block field."
            );
            _test.Eq(
                status?.save_advantage_tags?.Count ?? -1,
                2,
                "runtime wrapper should forward typed save_advantage_tags list."
            );
            _test.Eq(
                status?.save_advantage_tags?[0] ?? new StringName(""),
                new StringName("magic"),
                "runtime wrapper should preserve first save_advantage_tags entry."
            );
            _test.Eq(
                status?.save_advantage_tags?[1] ?? new StringName(""),
                new StringName("fear"),
                "runtime wrapper should preserve second save_advantage_tags entry."
            );
            _test.Eq(
                status?.save_disadvantage_tags?.Count ?? -1,
                1,
                "runtime wrapper should forward typed save_disadvantage_tags list."
            );
            _test.Eq(
                status?.save_disadvantage_tags?[0] ?? new StringName(""),
                new StringName("poison"),
                "runtime wrapper should preserve save_disadvantage_tags entry."
            );
            _test.Eq(
                status?.save_immunity_tags?.Count ?? -1,
                1,
                "runtime wrapper should forward typed save_immunity_tags list."
            );
            _test.Eq(
                status?.save_immunity_tags?[0] ?? new StringName(""),
                new StringName("sleep"),
                "runtime wrapper should preserve save_immunity_tags entry."
            );
            _test.Eq(
                status?.save_tags?.Count ?? -1,
                2,
                "runtime wrapper should forward typed save_tags list."
            );
            _test.Eq(
                status?.save_tags?[0] ?? new StringName(""),
                new StringName("magic"),
                "runtime wrapper should preserve first save_tags entry."
            );
            _test.Eq(
                status?.save_tags?[1] ?? new StringName(""),
                new StringName("mind"),
                "runtime wrapper should preserve second save_tags entry."
            );
            _test.Eq(
                status?.heal_multiplier_percent ?? -1,
                50,
                "runtime wrapper should forward typed heal_multiplier_percent field."
            );
            _test.Eq(
                status?.shield_gain_multiplier_percent ?? -1,
                40,
                "runtime wrapper should forward typed shield_gain_multiplier_percent field."
            );
            _test.Eq(
                status?.incoming_damage_multiplier ?? -1.0,
                1.25,
                "runtime wrapper should forward typed incoming_damage_multiplier field."
            );
            _test.Eq(
                status?.outgoing_damage_multiplier ?? -1.0,
                0.75,
                "runtime wrapper should forward typed outgoing_damage_multiplier field."
            );
            _test.Eq(
                status?.damage_tag ?? new StringName(""),
                new StringName("fire"),
                "runtime wrapper should forward typed damage_tag field."
            );
            _test.Eq(
                status?.damage_category ?? new StringName(""),
                new StringName("elemental"),
                "runtime wrapper should forward typed damage_category field."
            );
            _test.Eq(
                status?.damage_tags?.Count ?? -1,
                2,
                "runtime wrapper should forward typed damage_tags list."
            );
            _test.Eq(
                status?.damage_tags?[0] ?? new StringName(""),
                new StringName("fire"),
                "runtime wrapper should preserve first damage_tags entry."
            );
            _test.Eq(
                status?.damage_tags?[1] ?? new StringName(""),
                new StringName("slash"),
                "runtime wrapper should preserve second damage_tags entry."
            );
            _test.Eq(
                status?.attack_roll_penalty ?? -1,
                6,
                "runtime wrapper should forward typed attack_roll_penalty field."
            );
            _test.True(
                status?.undispellable == true,
                "runtime wrapper should forward typed undispellable field."
            );
            _test.True(
                status?.dispellable_harmful_magic == true,
                "runtime wrapper should forward typed dispellable_harmful_magic field."
            );
            _test.True(
                status?.dispellable_magic == false && status?.dispellable_beneficial_magic == false,
                "runtime wrapper should preserve false dispel flags."
            );
            _test.Eq(
                status?.mitigation_tier ?? new StringName(""),
                new StringName("half"),
                "runtime wrapper should forward typed mitigation_tier field."
            );
            _test.Eq(
                status?.dr_bypass_tag ?? new StringName(""),
                new StringName("armor_pierce"),
                "runtime wrapper should forward typed dr_bypass_tag field."
            );
            _test.True(
                status?.lock_counterattack == true,
                "runtime wrapper should forward typed counterattack lock field."
            );
            _test.True(
                status?.lock_dodge_bonus == true,
                "runtime wrapper should forward typed lock_dodge_bonus field."
            );
            _test.True(
                status?.lock_crit == true,
                "runtime wrapper should forward typed crit lock field."
            );
            _test.Eq(
                status?.main_skill_lock_other_debuff_count ?? -1,
                3,
                "runtime wrapper should forward typed main skill lock count."
            );
            _test.Eq(
                status?.stack_behavior ?? new StringName(""),
                new StringName("add"),
                "runtime wrapper should forward typed stack_behavior field."
            );
            _test.Eq(
                status?.stack_limit ?? -1,
                9,
                "runtime wrapper should forward typed stack_limit field."
            );
            _test.True(
                runtime.IsUnitCounterattackLocked(unit),
                "runtime counterattack lock query should use typed status fields."
            );
            _test.True(
                BattleFateAttackRules.IsAttackCritLocked(unit),
                "runtime crit lock query should use typed status fields."
            );
            _test.Eq(
                runtime._get_main_skill_lock_other_debuff_count(unit),
                3,
                "runtime main skill lock query should use typed status fields."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(unit);
            runtime.Dispose();
        }
    }

    private void TestRuntimeWrapperForwardsLockGuardTypedField()
    {
        var runtime = new BattleRuntimeModule();
        BattleUnitState unit = BuildUnit("runtime_lock_guard_wrapper_unit");
        try
        {
            runtime._set_runtime_status_effect(
                unit_state: unit,
                status_id: "runtime_lock_guard_status",
                duration_tu: 10,
                source_unit_id: "runtime_source_unit",
                power: 1,
                lock_guard: true
            );

            BattleStatusEffectState status = unit.GetStatusEffect("runtime_lock_guard_status");
            _test.True(status != null, "runtime wrapper should create the lock_guard status effect.");
            _test.True(
                status?.lock_guard == true,
                "runtime wrapper should forward typed lock_guard field."
            );
            _test.True(
                runtime.IsUnitGuardLocked(unit),
                "runtime guard lock query should use typed lock_guard status fields."
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleUnit(unit);
            runtime.Dispose();
        }
    }

    private void TestRuntimeDebuffWrapperForwardsDebuffSemantics()
    {
        var runtime = new BattleRuntimeModule();
        BattleUnitState unit = BuildUnit("runtime_debuff_wrapper_unit");

        runtime._set_runtime_debuff_status_effect(
            unit,
            "runtime_debuff_status",
            12,
            "source_unit",
            2
        );

        BattleStatusEffectState status = unit.GetStatusEffect("runtime_debuff_status");
        _test.True(status != null, "runtime debuff wrapper should create the status effect.");
        _test.True(
            status?.counts_as_debuff_override == true && status.counts_as_debuff,
            "runtime debuff wrapper should mark runtime status as a typed debuff."
        );
        _test.Eq(
            status?.power ?? -1,
            2,
            "runtime debuff wrapper should preserve power."
        );
        BattleTestFixture.DisposeBattleUnit(unit);
        runtime.Dispose();
    }

    private void TestRuntimeBodySizeOverrideWrapperForwardsTypedFields()
    {
        var runtime = new BattleRuntimeModule();
        BattleUnitState unit = BuildUnit("runtime_body_size_wrapper_unit");

        runtime._set_runtime_body_size_override_status_effect(
            unit,
            "runtime_body_size_status",
            20,
            "source_unit",
            1,
            new GDictionary(),
            BodySizeContentRules.ToStringName(BodySizeCategoryKind.Huge),
            BodySizeContentRules.ToStringName(BodySizeCategoryKind.Large)
        );

        BattleStatusEffectState status = unit.GetStatusEffect("runtime_body_size_status");
        _test.True(status != null, "runtime body-size wrapper should create the status effect.");
        _test.Eq(
            status?.body_size_category_override ?? new StringName(""),
            BodySizeContentRules.ToStringName(BodySizeCategoryKind.Huge),
            "runtime body-size wrapper should forward typed body_size_category_override field."
        );
        _test.Eq(
            status?.previous_body_size_category ?? new StringName(""),
            BodySizeContentRules.ToStringName(BodySizeCategoryKind.Large),
            "runtime body-size wrapper should forward typed previous_body_size_category field."
        );
        BattleTestFixture.DisposeBattleUnit(unit);
        runtime.Dispose();
    }

    private void TestRuntimeSourceWrapperForwardsCoreFields()
    {
        var runtime = new BattleRuntimeModule();
        BattleUnitState unit = BuildUnit("runtime_source_wrapper_unit");

        runtime._set_runtime_source_status_effect(
            unit,
            "runtime_source_status",
            14,
            "source_unit",
            3
        );

        BattleStatusEffectState status = unit.GetStatusEffect("runtime_source_status");
        _test.True(status != null, "runtime source wrapper should create the status effect.");
        _test.Eq(
            status?.source_unit_id ?? new StringName(""),
            new StringName("source_unit"),
            "runtime source wrapper should forward source_unit_id."
        );
        _test.Eq(
            status?.power ?? -1,
            3,
            "runtime source wrapper should preserve power."
        );
        BattleTestFixture.DisposeBattleUnit(unit);
        runtime.Dispose();
    }

    private static BattleUnitState BuildUnit(string unitId)
    {
        return new BattleUnitState
        {
            unit_id = new StringName(unitId),
            display_name = unitId,
        };
    }

    private static SkillDefinition BuildGuardGrantingSkill()
    {
        return TestSkillDefinitionProjection.BuildSkill(
            "test_guard_granting_skill",
            displayName: "Guard Grant",
            maxLevel: 1,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                "test_guard_granting_skill",
                effects: new[]
                {
                    TestSkillDefinitionProjection.BuildEffect(
                        "status",
                        statusId: "guarding",
                        power: 1,
                        durationTu: 10
                    ),
                },
                targetMode: "unit",
                targetTeamFilter: "self",
                targetSelectionMode: "self",
                apCost: 0,
                mpCost: 0,
                staminaCost: 0,
                auraCost: 0
            )
        );
    }

    private static void SetStatusParams(
        BattleUnitState unit,
        string statusId,
        GDictionary statusParams
    )
    {
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = new StringName(statusId),
                power = 1,
                stacks = 1,
                @params = statusParams.Duplicate(true),
            }
        );
    }

    private static void SetTypedStatus(
        BattleUnitState unit,
        string statusId,
        bool lockCounterattack = false,
        bool lockGuard = false,
        int mainSkillLockOtherDebuffCount = 0,
        bool lockCrit = false,
        bool countsAsDebuffOverride = false,
        bool countsAsDebuff = false
    )
    {
        unit.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = new StringName(statusId),
                power = 1,
                stacks = 1,
                lock_counterattack = lockCounterattack,
                lock_crit = lockCrit,
                main_skill_lock_other_debuff_count = mainSkillLockOtherDebuffCount,
                counts_as_debuff_override = countsAsDebuffOverride,
                counts_as_debuff = countsAsDebuff,
            }
        );
        BattleStatusEffectState statusEntry = unit.GetStatusEffect(statusId);
        if (statusEntry != null && lockGuard)
        {
            statusEntry.lock_guard = true;
            unit.SetStatusEffect(statusEntry);
        }
    }

    private static void DisposeUnits(params BattleUnitState[] units)
    {
        foreach (BattleUnitState unit in units)
        {
            BattleTestFixture.DisposeBattleUnit(unit);
        }
    }

    private static bool ContainsFragment(GStringArray values, string fragment)
    {
        foreach (string value in values)
        {
            if (value != null && value.Contains(fragment))
                return true;
        }
        return false;
    }

    private static string FormatErrors(GStringArray errors) =>
        errors == null ? "" : string.Join(" | ", errors);


}
