using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_status_effect_typed_fields_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestEffectSchemaRejectsLegacyLockGuardParam();
        TestStatusParamsNoLongerDriveTypedStatusSemantics();
        TestTypedFieldsDriveStatusSemantics();
        TestGuardLockBlocksGuardGrantingSkillsWithDistinctReason();
        TestStatusEffectLockGuardRoundTripUsesTopLevelField();

        RequestTestExit(_test.Finish("Status effect typed fields regression"));
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
        unit.SetCurrentAp(3);
        unit.SetCurrentMp(3);
        unit.SetCurrentStamina(3);
        unit.SetCurrentAura(3);
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
