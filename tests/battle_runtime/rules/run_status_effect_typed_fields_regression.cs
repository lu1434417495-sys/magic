using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_status_effect_typed_fields_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestStatusParamsNoLongerDriveTypedStatusSemantics();
        TestTypedFieldsDriveStatusSemantics();
        TestRuntimeWrapperForwardsTypedFields();

        if (_failures.Count == 0)
        {
            GD.Print("Status effect typed fields regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Status effect typed fields regression: FAIL ({_failures.Count})");
        return 1;
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
        AssertFalse(
            resolver.has_counterattack_lock_status(counterLockUnit),
            "status params.lock_counterattack must not drive typed counterattack locks."
        );

        BattleUnitState mainSkillLockUnit = BuildUnit("legacy_main_skill_lock");
        SetStatusParams(
            mainSkillLockUnit,
            "legacy_main_skill_lock",
            new GDictionary { ["main_skill_lock_other_debuff_count"] = 2 }
        );
        AssertEq(
            resolver.get_main_skill_lock_other_debuff_count(mainSkillLockUnit),
            0,
            "status params.main_skill_lock_other_debuff_count must not drive typed main skill locks."
        );

        BattleUnitState customDebuffUnit = BuildUnit("legacy_custom_debuff");
        SetStatusParams(
            customDebuffUnit,
            "custom_debuff",
            new GDictionary { ["counts_as_debuff"] = true }
        );
        AssertEq(
            resolver.count_debuff_statuses(customDebuffUnit),
            0,
            "status params.counts_as_debuff=true must not mark custom statuses as debuffs."
        );

        BattleUnitState burningOverrideUnit = BuildUnit("legacy_burning_override");
        SetStatusParams(
            burningOverrideUnit,
            "burning",
            new GDictionary { ["counts_as_debuff"] = false }
        );
        AssertEq(
            resolver.count_debuff_statuses(burningOverrideUnit),
            1,
            "status params.counts_as_debuff=false must not override built-in debuff semantics."
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
        AssertTrue(
            resolver.has_counterattack_lock_status(counterLockUnit),
            "typed lock_counterattack must drive counterattack locks."
        );

        BattleUnitState mainSkillLockUnit = BuildUnit("typed_main_skill_lock");
        SetTypedStatus(
            mainSkillLockUnit,
            "typed_main_skill_lock",
            mainSkillLockOtherDebuffCount: 2
        );
        AssertEq(
            resolver.get_main_skill_lock_other_debuff_count(mainSkillLockUnit),
            2,
            "typed main_skill_lock_other_debuff_count must drive main skill locks."
        );

        BattleUnitState customDebuffUnit = BuildUnit("typed_custom_debuff");
        SetTypedStatus(
            customDebuffUnit,
            "custom_debuff",
            countsAsDebuffOverride: true,
            countsAsDebuff: true
        );
        AssertEq(
            resolver.count_debuff_statuses(customDebuffUnit),
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
        AssertEq(
            resolver.count_debuff_statuses(burningOverrideUnit),
            0,
            "typed counts_as_debuff=false must override built-in debuff semantics."
        );
    }

    private void TestRuntimeWrapperForwardsTypedFields()
    {
        var runtime = new BattleRuntimeModule();
        BattleUnitState unit = BuildUnit("runtime_wrapper_unit");

        runtime._set_runtime_status_effect(
            unit,
            "runtime_wrapper_status",
            10,
            "source_unit",
            1,
            new GDictionary(),
            counts_as_debuff_override: true,
            counts_as_debuff: true,
            lock_counterattack: true,
            main_skill_lock_other_debuff_count: 3
        );

        BattleStatusEffectState status = unit.get_status_effect("runtime_wrapper_status");
        AssertTrue(status != null, "runtime wrapper should create the status effect.");
        AssertTrue(
            status?.counts_as_debuff_override == true && status.counts_as_debuff,
            "runtime wrapper should forward typed debuff override fields."
        );
        AssertTrue(
            status?.lock_counterattack == true,
            "runtime wrapper should forward typed counterattack lock field."
        );
        AssertEq(
            status?.main_skill_lock_other_debuff_count ?? -1,
            3,
            "runtime wrapper should forward typed main skill lock count."
        );
        AssertTrue(
            runtime.is_unit_counterattack_locked(unit),
            "runtime counterattack lock query should use typed status fields."
        );
        AssertEq(
            runtime._get_main_skill_lock_other_debuff_count(unit),
            3,
            "runtime main skill lock query should use typed status fields."
        );
    }

    private static BattleUnitState BuildUnit(string unitId)
    {
        return new BattleUnitState
        {
            unit_id = new StringName(unitId),
            display_name = unitId,
        };
    }

    private static void SetStatusParams(
        BattleUnitState unit,
        string statusId,
        GDictionary statusParams
    )
    {
        unit.set_status_effect(
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
        int mainSkillLockOtherDebuffCount = 0,
        bool countsAsDebuffOverride = false,
        bool countsAsDebuff = false
    )
    {
        unit.set_status_effect(
            new BattleStatusEffectState
            {
                status_id = new StringName(statusId),
                power = 1,
                stacks = 1,
                lock_counterattack = lockCounterattack,
                main_skill_lock_other_debuff_count = mainSkillLockOtherDebuffCount,
                counts_as_debuff_override = countsAsDebuffOverride,
                counts_as_debuff = countsAsDebuff,
            }
        );
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }
}
