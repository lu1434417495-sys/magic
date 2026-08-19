#nullable enable

using System;
using System.Collections.Generic;
using Godot;

public partial class run_skill_generation_battle_sim_gate_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        try
        {
            TestFormalSamplingContract();
            TestOutlierRulesUseStableThresholds();
            TestRealRunnerProducesComparableMetrics();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unexpected generated-skill BattleSim gate exception: {exception}"
            );
        }
        RequestTestExit(_test.Finish("Generated-skill BattleSim gate regression"));
    }

    private void TestFormalSamplingContract()
    {
        var options = new SkillGenerationBattleSimOptions();
        _test.Eq(options.Seeds.Count, 20, "formal generated-skill sampling should use 20 seeds");
        _test.Eq(
            options.MinimumCompletedSamples,
            20,
            "formal gate should require every configured seed to complete"
        );
        _test.Eq(
            options.MaximumWinRateDeltaBasisPoints,
            3500,
            "high win-rate outlier threshold should be explicit"
        );
        _test.Eq(
            options.MaximumDamageRatioBasisPoints,
            22500,
            "high damage-ratio outlier threshold should be explicit"
        );
    }

    private void TestOutlierRulesUseStableThresholds()
    {
        var options = new SkillGenerationBattleSimOptions();
        var context = new JsonContentEntryContext(
            "skills",
            "generated_probe",
            "probe.json#generated_probe",
            "/entries/0"
        );

        AssertSingleRule(
            Sample(completed: 19, attempts: 10, winDelta: 0, damageRatio: 10000),
            options,
            context,
            SkillGenerationBattleSimRules.IncompleteSamples,
            "incomplete sample set"
        );
        AssertSingleRule(
            Sample(completed: 20, attempts: 2, winDelta: 0, damageRatio: 10000),
            options,
            context,
            SkillGenerationBattleSimRules.CandidateNotExercised,
            "candidate not exercised"
        );
        AssertSingleRule(
            Sample(completed: 20, attempts: 10, winDelta: 3501, damageRatio: 10000),
            options,
            context,
            SkillGenerationBattleSimRules.HighStrengthOutlier,
            "high win-rate outlier"
        );
        AssertSingleRule(
            Sample(completed: 20, attempts: 10, winDelta: 0, damageRatio: 22501),
            options,
            context,
            SkillGenerationBattleSimRules.HighStrengthOutlier,
            "high damage outlier"
        );
        AssertSingleRule(
            Sample(completed: 20, attempts: 10, winDelta: -3501, damageRatio: 10000),
            options,
            context,
            SkillGenerationBattleSimRules.LowStrengthOutlier,
            "low win-rate outlier"
        );
        IReadOnlyList<ContentJsonDiagnostic> accepted =
            SkillGenerationBattleSimStrengthAnalyzer.Evaluate(
                Sample(completed: 20, attempts: 10, winDelta: 1000, damageRatio: 12500),
                options,
                context
            );
        _test.Eq(accepted.Count, 0, "inside-envelope sample should be accepted");
    }

    private void TestRealRunnerProducesComparableMetrics()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        if (!snapshot.Skills.TryGetValue("mage_fireball", out SkillDefinition? candidate))
        {
            _test.Fail("process snapshot is missing mage_fireball BattleSim probe");
            return;
        }
        _test.True(
            SkillGenerationBattleSimScenarioFactory.IsSupportedCandidate(candidate),
            "mage_fireball should fit the standard enemy-targeted damage surface"
        );
        var options = new SkillGenerationBattleSimOptions();
        var gate = new SkillGenerationBattleSimGate(options);
        var candidates = new Dictionary<StringName, SkillDefinition>
        {
            [candidate.SkillId] = candidate,
        };
        var contexts = new Dictionary<StringName, JsonContentEntryContext>
        {
            [candidate.SkillId] = new JsonContentEntryContext(
                "skills",
                candidate.SkillId.ToString(),
                $"probe.json#{candidate.SkillId}",
                "/entries/0"
            ),
        };
        SkillGenerationBattleSimulationGateResult result = gate.Evaluate(
            candidates,
            contexts,
            snapshot.Skills,
            snapshot
        );

        _test.Eq(result.SampledSkillCount, 1, "real gate should sample one candidate");
        string prefix = $"skill.{candidate.SkillId}";
        _test.True(
            ReadMetric(result.Metrics, $"{prefix}.baseline_completed_samples") >= 20,
            "baseline arm should produce the formal completed sample set"
        );
        _test.True(
            ReadMetric(result.Metrics, $"{prefix}.candidate_completed_samples") >= 20,
            "candidate arm should produce the formal completed sample set"
        );
        _test.True(
            ReadMetric(result.Metrics, $"{prefix}.candidate_attempt_count") >= 3,
            "real AI should exercise mage_fireball"
        );
        _test.True(
            result.Metrics.ContainsKey($"{prefix}.win_rate_delta_bp")
            && result.Metrics.ContainsKey($"{prefix}.damage_ratio_bp"),
            "real gate should publish comparable win-rate and damage metrics"
        );
        _test.True(
            result.Metrics.ContainsKey($"{prefix}.baseline_report")
            && result.Metrics.ContainsKey($"{prefix}.candidate_report"),
            "real runner report artifacts should remain traceable"
        );
        _test.Eq(
            result.Diagnostics.Count,
            0,
            "same-surface benchmark skill should remain inside its own formal envelope"
        );
    }

    private void AssertSingleRule(
        SkillGenerationBattleSimStrengthSample sample,
        SkillGenerationBattleSimOptions options,
        JsonContentEntryContext context,
        string expectedRule,
        string label
    )
    {
        IReadOnlyList<ContentJsonDiagnostic> diagnostics =
            SkillGenerationBattleSimStrengthAnalyzer.Evaluate(sample, options, context);
        _test.Eq(diagnostics.Count, 1, $"{label} should emit one diagnostic");
        if (diagnostics.Count != 1)
            return;
        _test.Eq(diagnostics[0].RuleId, expectedRule, $"{label} rule ID should be stable");
        _test.Eq(
            diagnostics[0].JsonPointer,
            "/entries/0/combat_profile",
            $"{label} should locate combat_profile"
        );
        _test.True(
            !string.IsNullOrEmpty(diagnostics[0].Expected)
            && !string.IsNullOrEmpty(diagnostics[0].Actual),
            $"{label} should include expected and actual values"
        );
    }

    private static SkillGenerationBattleSimStrengthSample Sample(
        int completed,
        int attempts,
        int winDelta,
        int damageRatio
    ) => new(
        "generated_probe",
        completed,
        completed,
        attempts,
        attempts,
        5000,
        5000 + winDelta,
        winDelta,
        100000,
        damageRatio * 10,
        damageRatio,
        "baseline.json",
        "candidate.json"
    );

    private static int ReadMetric(
        IReadOnlyDictionary<string, object> metrics,
        string key
    ) => metrics.TryGetValue(key, out object? value) && value is int number ? number : -1;
}
