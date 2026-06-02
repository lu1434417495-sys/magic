using System;
using System.Collections.Generic;
using Godot;

public partial class run_fate_attack_formula_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestFormulaNoLongerRequiresGodotRegistration();
        TestCritGateDieSizeCases();
        TestFumbleLowEndCases();
        TestCombatLuckScoreAndCritThresholdCases();
        TestRollDieUsesInjectedRngWithoutDisadvantage();
        TestRollDieUsesInjectedRngWithDisadvantage();

        if (_failures.Count == 0)
        {
            GD.Print("Fate attack formula regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Fate attack formula regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestFormulaNoLongerRequiresGodotRegistration()
    {
        Type formulaType = typeof(FateAttackFormula);
        AssertTrue(
            formulaType.IsAbstract && formulaType.IsSealed,
            "FateAttackFormula 应是 static C# helper。"
        );
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(formulaType),
            "FateAttackFormula 不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            formulaType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length
                > 0,
            "FateAttackFormula 不应继续注册为 Godot GlobalClass。"
        );
        AssertTrue(
            formulaType.GetMethod("calc_crit_gate_die_size") == null,
            "FateAttackFormula 不应保留 GDScript snake_case 暴击门 API。"
        );
        AssertTrue(
            formulaType.GetMethod("roll_die_with_disadvantage_rule") == null,
            "FateAttackFormula 不应保留 GDScript snake_case 掷骰 API。"
        );
    }

    private void TestCritGateDieSizeCases()
    {
        var cases = new[]
        {
            ("effective_luck >= -3 normal", 0, false, 20),
            ("effective_luck >= -3 disadvantage", -3, true, 20),
            ("effective_luck -4 normal", -4, false, 40),
            ("effective_luck -4 disadvantage", -4, true, 40),
            ("effective_luck -5 normal", -5, false, 80),
            ("effective_luck -5 disadvantage mercy", -5, true, 40),
            ("effective_luck -6 normal", -6, false, 160),
            ("effective_luck -6 disadvantage mercy", -6, true, 80),
        };
        foreach ((string label, int effectiveLuck, bool isDisadvantage, int expected) in cases)
        {
            int actual = FateAttackFormula.CalcCritGateDieSize(effectiveLuck, isDisadvantage);
            AssertEq(actual, expected, $"{label} gate die mismatch");
        }
    }

    private void TestFumbleLowEndCases()
    {
        var cases = new[]
        {
            ("effective_luck >= -4", 2, 1),
            ("effective_luck -4", -4, 1),
            ("effective_luck -5", -5, 2),
            ("effective_luck -6", -6, 3),
        };
        foreach ((string label, int effectiveLuck, int expected) in cases)
        {
            int actual = FateAttackFormula.CalcFumbleLowEnd(effectiveLuck);
            AssertEq(actual, expected, $"{label} fumble range mismatch");
        }
    }

    private void TestCombatLuckScoreAndCritThresholdCases()
    {
        var cases = new[]
        {
            ("default values", 0, 0, 0, 20),
            ("odd faith rounds down", 0, 1, 0, 20),
            ("high luck soft cap", 2, 5, 4, 16),
            ("negative faith ignored for score", 2, -3, 2, 18),
            ("combat luck score cap", 4, 4, 4, 16),
        };
        foreach (
            (
                string label,
                int hiddenLuck,
                int faithLuck,
                int expectedScore,
                int expectedThreshold
            ) in cases
        )
        {
            int score = FateAttackFormula.CalcCombatLuckScore(hiddenLuck, faithLuck);
            int threshold = FateAttackFormula.CalcCritThreshold(hiddenLuck, faithLuck);
            AssertEq(score, expectedScore, $"{label} combat luck score mismatch");
            AssertEq(threshold, expectedThreshold, $"{label} crit threshold mismatch");
        }
    }

    private void TestRollDieUsesInjectedRngWithoutDisadvantage()
    {
        StubRollSource rng = new(new[] { 17, 4 });
        int actual = FateAttackFormula.RollDieWithDisadvantageRule(20, false, rng);
        AssertEq(actual, 17, "normal roll should return the first injected result");
        AssertEq(rng.CallCount, 1, "normal roll should consume exactly one injected RNG call");
        AssertEq(rng.RemainingCount, 1, "normal roll should leave the second injected result unused");
    }

    private void TestRollDieUsesInjectedRngWithDisadvantage()
    {
        StubRollSource rng = new(new[] { 17, 4 });
        int actual = FateAttackFormula.RollDieWithDisadvantageRule(20, true, rng);
        AssertEq(actual, 4, "disadvantage roll should choose the lower injected result");
        AssertEq(rng.CallCount, 2, "disadvantage roll should consume exactly two injected RNG calls");
        AssertEq(rng.RemainingCount, 0, "disadvantage roll should consume both injected results");
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} expected={expected} actual={actual}");
    }

    private sealed class StubRollSource : FateAttackFormula.IRollSource
    {
        private readonly Queue<int> _rolls;

        public StubRollSource(IEnumerable<int> rolls)
        {
            _rolls = new Queue<int>(rolls ?? Array.Empty<int>());
        }

        public int CallCount { get; private set; }

        public int RemainingCount => _rolls.Count;

        public int RandiRange(int minValue, int maxValue)
        {
            int lower = Math.Min(minValue, maxValue);
            int upper = Math.Max(minValue, maxValue);
            CallCount += 1;
            if (_rolls.Count == 0)
                return lower;
            return Math.Clamp(_rolls.Dequeue(), lower, upper);
        }
    }
}
