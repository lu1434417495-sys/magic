using System;
using System.Collections.Generic;
using Godot;

public partial class run_fate_attack_formula_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestCritGateDieSizeCases();
        TestFumbleLowEndCases();
        TestCombatLuckScoreAndCritThresholdCases();
        TestRollDieUsesInjectedRngWithoutDisadvantage();
        TestRollDieUsesInjectedRngWithDisadvantage();

        RequestTestExit(_test.Finish("Fate attack formula regression"));
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
            _test.Eq(actual, expected, $"{label} gate die mismatch");
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
            _test.Eq(actual, expected, $"{label} fumble range mismatch");
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
            _test.Eq(score, expectedScore, $"{label} combat luck score mismatch");
            _test.Eq(threshold, expectedThreshold, $"{label} crit threshold mismatch");
        }
    }

    private void TestRollDieUsesInjectedRngWithoutDisadvantage()
    {
        StubRollSource rng = new(new[] { 17, 4 });
        int actual = FateAttackFormula.RollDieWithDisadvantageRule(20, false, rng);
        _test.Eq(actual, 17, "normal roll should return the first injected result");
        _test.Eq(rng.CallCount, 1, "normal roll should consume exactly one injected RNG call");
        _test.Eq(rng.RemainingCount, 1, "normal roll should leave the second injected result unused");
    }

    private void TestRollDieUsesInjectedRngWithDisadvantage()
    {
        StubRollSource rng = new(new[] { 17, 4 });
        int actual = FateAttackFormula.RollDieWithDisadvantageRule(20, true, rng);
        _test.Eq(actual, 4, "disadvantage roll should choose the lower injected result");
        _test.Eq(rng.CallCount, 2, "disadvantage roll should consume exactly two injected RNG calls");
        _test.Eq(rng.RemainingCount, 0, "disadvantage roll should consume both injected results");
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
