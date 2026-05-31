using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_status_modifier_rules_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestHealAndShieldMultipliersUseTypedStatusState();
        TestMissingTypedUnitKeepsDefaultMultiplier();

        if (_failures.Count == 0)
        {
            GD.Print("Battle status modifier rules regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle status modifier rules regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestHealAndShieldMultipliersUseTypedStatusState()
    {
        var unit = new BattleUnitState();
        unit.set_status_effect(
            new BattleStatusEffectState
            {
                status_id = "healing_suppressed",
                @params = new GDictionary
                {
                    [BattleStatusModifierRules.PARAM_HEAL_MULTIPLIER_PERCENT()] = 50,
                    [BattleStatusModifierRules.PARAM_SHIELD_GAIN_MULTIPLIER_PERCENT()] = 25,
                },
            }
        );

        AssertEq(
            BattleStatusModifierRules.resolve_heal_multiplier_percent(unit),
            50,
            "typed status params 应决定治疗倍率。"
        );
        AssertEq(
            BattleStatusModifierRules.apply_heal_multiplier(unit, 11),
            6,
            "治疗倍率应用应四舍五入。"
        );
        AssertEq(
            BattleStatusModifierRules.resolve_shield_gain_multiplier_percent(unit),
            25,
            "typed status params 应决定护盾获取倍率。"
        );
        AssertEq(
            BattleStatusModifierRules.apply_shield_gain_multiplier(unit, 8),
            2,
            "护盾倍率应用应使用同一套 typed status params。"
        );
    }

    private void TestMissingTypedUnitKeepsDefaultMultiplier()
    {
        AssertEq(
            BattleStatusModifierRules.resolve_heal_multiplier_percent(null),
            BattleStatusModifierRules.DEFAULT_MULTIPLIER_PERCENT(),
            "空单位应使用默认倍率。"
        );
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} expected={expected} actual={actual}");
        }
    }
}
