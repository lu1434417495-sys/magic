#nullable enable

// Tooling adapter that binds the content-layer gate contract to BattleSimRunner.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal sealed class SkillGenerationBattleSimGate
    : ISkillGenerationBattleSimulationGate
{
    private static readonly BattleSimProfileDefinition StandardProfile = new(
        "skill_generation_standard",
        "Skill generation standard profile",
        "Untuned baseline score profile used by the generated-skill strength gate.",
        BattleAiScoreProfileDefinition.Default,
        Array.Empty<BattleSimOverridePatchDefinition>()
    );

    private readonly SkillGenerationBattleSimOptions _options;

    internal SkillGenerationBattleSimGate(
        SkillGenerationBattleSimOptions? options = null
    )
    {
        _options = options ?? new SkillGenerationBattleSimOptions();
    }

    public SkillGenerationBattleSimulationGateResult Evaluate(
        IReadOnlyDictionary<StringName, SkillDefinition> candidateSkills,
        IReadOnlyDictionary<StringName, JsonContentEntryContext> candidateContexts,
        IReadOnlyDictionary<StringName, SkillDefinition> combinedSkills,
        ContentSnapshot processSnapshot
    )
    {
        ArgumentNullException.ThrowIfNull(candidateSkills);
        ArgumentNullException.ThrowIfNull(candidateContexts);
        ArgumentNullException.ThrowIfNull(combinedSkills);
        ArgumentNullException.ThrowIfNull(processSnapshot);
        SkillGenerationBattleSimFixtureDefinition fixture = RequireInfrastructure(
            combinedSkills,
            processSnapshot
        );

        var diagnostics = new List<ContentJsonDiagnostic>();
        var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["configured_seed_count"] = _options.Seeds.Count,
            ["minimum_completed_samples"] = _options.MinimumCompletedSamples,
            ["maximum_win_rate_delta_bp"] =
                _options.MaximumWinRateDeltaBasisPoints,
            ["minimum_win_rate_delta_bp"] =
                _options.MinimumWinRateDeltaBasisPoints,
            ["maximum_damage_ratio_bp"] =
                _options.MaximumDamageRatioBasisPoints,
            ["minimum_damage_ratio_bp"] =
                _options.MinimumDamageRatioBasisPoints,
        };
        int sampledSkillCount = 0;
        int completedSampleCount = 0;
        using var provider = new BattleSimContentProvider(
            processSnapshot,
            combinedSkills
        );
        var runner = new BattleSimRunner(provider);

        foreach (
            (StringName skillId, SkillDefinition candidate) in candidateSkills.OrderBy(
                pair => pair.Key.ToString(),
                StringComparer.Ordinal
            )
        )
        {
            JsonContentEntryContext context = RequireContext(
                skillId,
                candidateContexts
            );
            if (!SkillGenerationBattleSimScenarioFactory.IsSupportedCandidate(candidate))
            {
                diagnostics.Add(new ContentJsonDiagnostic(
                    SkillGenerationBattleSimRules.UnsupportedSurface,
                    "The standard BattleSim strength fixture only samples active enemy-targeted damage skills.",
                    context.SourceLabel,
                    context.JsonPointer + "/combat_profile",
                    "active enemy-targeted unit or ground skill with a damage/execute effect",
                    DescribeSurface(candidate)
                ));
                continue;
            }

            BattleSimScenarioReport baseline = runner.RunScenario(
                SkillGenerationBattleSimScenarioFactory.Create(
                    candidate,
                    fixture,
                    includeCandidate: false,
                    seeds: _options.Seeds
                ),
                new[] { StandardProfile }
            );
            BattleSimScenarioReport candidateReport = runner.RunScenario(
                SkillGenerationBattleSimScenarioFactory.Create(
                    candidate,
                    fixture,
                    includeCandidate: true,
                    seeds: _options.Seeds
                ),
                new[] { StandardProfile }
            );
            SkillGenerationBattleSimStrengthSample sample =
                SkillGenerationBattleSimStrengthProjector.BuildSample(
                    skillId,
                    baseline,
                    candidateReport
                );
            sampledSkillCount += 1;
            completedSampleCount += sample.CandidateCompletedSamples;
            AddMetrics(metrics, sample);
            diagnostics.AddRange(
                SkillGenerationBattleSimStrengthAnalyzer.Evaluate(
                    sample,
                    _options,
                    context
                )
            );
        }

        metrics["sampled_skill_count"] = sampledSkillCount;
        metrics["completed_candidate_sample_count"] = completedSampleCount;
        metrics["diagnostic_count"] = diagnostics.Count;
        return new SkillGenerationBattleSimulationGateResult(
            sampledSkillCount,
            diagnostics,
            metrics
        );
    }

    private static SkillGenerationBattleSimFixtureDefinition RequireInfrastructure(
        IReadOnlyDictionary<StringName, SkillDefinition> combinedSkills,
        ContentSnapshot processSnapshot
    )
    {
        SkillGenerationBattleSimFixtureDefinition fixture = processSnapshot
            .GameplayConfiguration?.SkillGenerationBattleSim
            ?? throw new InvalidOperationException(
                "Skill generation BattleSim fixture configuration is unavailable."
            );
        if (!combinedSkills.ContainsKey(fixture.BasicAttackSkillId))
        {
            throw new InvalidOperationException(
                $"Skill generation BattleSim requires basic attack skill {fixture.BasicAttackSkillId}."
            );
        }
        foreach (StringName benchmarkSkillId in new[]
        {
            fixture.MultiTargetBenchmarkSkillId,
            fixture.GroundBenchmarkSkillId,
            fixture.RangedUnitBenchmarkSkillId,
        })
        {
            if (!combinedSkills.ContainsKey(benchmarkSkillId))
            {
                throw new InvalidOperationException(
                    $"Skill generation BattleSim requires benchmark skill {benchmarkSkillId}."
                );
            }
        }
        foreach (StringName brainId in new[]
        {
            fixture.MeleeBrainId,
            fixture.MageBrainId,
            fixture.RangedBrainId,
        })
        {
            if (!processSnapshot.EnemyBrains.ContainsKey(brainId))
            {
                throw new InvalidOperationException(
                    $"Skill generation BattleSim requires enemy AI brain {brainId}."
                );
            }
        }
        return fixture;
    }

    private static JsonContentEntryContext RequireContext(
        StringName skillId,
        IReadOnlyDictionary<StringName, JsonContentEntryContext> contexts
    ) => contexts.TryGetValue(skillId, out JsonContentEntryContext? context)
        ? context
        : throw new InvalidOperationException(
            $"Skill generation BattleSim is missing source context for {skillId}."
        );

    private static string DescribeSurface(SkillDefinition? candidate)
    {
        CombatSkillDefinition? combat = candidate?.CombatProfile;
        return $"skill_type={candidate?.SkillType ?? "<null>"};"
            + $"target_mode={combat?.TargetMode.ToString() ?? "<none>"};"
            + $"target_filter={combat?.TargetTeamFilter.ToString() ?? "<none>"};"
            + $"effect_count={combat?.EffectDefinitions.Count ?? 0}";
    }

    private static void AddMetrics(
        IDictionary<string, object> metrics,
        SkillGenerationBattleSimStrengthSample sample
    )
    {
        string prefix = $"skill.{sample.SkillId}";
        metrics[$"{prefix}.baseline_completed_samples"] =
            sample.BaselineCompletedSamples;
        metrics[$"{prefix}.candidate_completed_samples"] =
            sample.CandidateCompletedSamples;
        metrics[$"{prefix}.candidate_attempt_count"] = sample.CandidateAttemptCount;
        metrics[$"{prefix}.candidate_success_count"] = sample.CandidateSuccessCount;
        metrics[$"{prefix}.baseline_win_rate_bp"] =
            sample.BaselineAllyWinRateBasisPoints;
        metrics[$"{prefix}.candidate_win_rate_bp"] =
            sample.CandidateAllyWinRateBasisPoints;
        metrics[$"{prefix}.win_rate_delta_bp"] = sample.WinRateDeltaBasisPoints;
        metrics[$"{prefix}.baseline_damage_per_run_bp"] =
            sample.BaselineDamagePerCompletedRunBasisPoints;
        metrics[$"{prefix}.candidate_damage_per_run_bp"] =
            sample.CandidateDamagePerCompletedRunBasisPoints;
        metrics[$"{prefix}.damage_ratio_bp"] = sample.DamageRatioBasisPoints;
        metrics[$"{prefix}.baseline_report"] = sample.BaselineReportPath;
        metrics[$"{prefix}.candidate_report"] = sample.CandidateReportPath;
    }
}
