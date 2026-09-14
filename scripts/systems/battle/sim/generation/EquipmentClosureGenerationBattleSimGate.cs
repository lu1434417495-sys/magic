#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal sealed class EquipmentClosureGenerationBattleSimGate
    : IEquipmentClosureGenerationBattleSimulationGate
{
    private const string AllyFactionId = "player";

    private static readonly BattleSimProfileDefinition StandardProfile = new(
        "equipment_generation_standard",
        "Equipment generation standard profile",
        "Untuned baseline score profile used by the generated-equipment strength gate.",
        BattleAiScoreProfileDefinition.Default,
        Array.Empty<BattleSimOverridePatchDefinition>()
    );

    private readonly EquipmentClosureGenerationBattleSimOptions _options;

    internal EquipmentClosureGenerationBattleSimGate(
        EquipmentClosureGenerationBattleSimOptions? options = null
    )
    {
        _options = options ?? new EquipmentClosureGenerationBattleSimOptions();
    }

    public EquipmentClosureGenerationBattleSimulationGateResult Evaluate(
        EquipmentClosureGenerationProjectedContent content,
        ContentSnapshot processSnapshot
    )
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(processSnapshot);
        EquipmentGenerationBattleSimFixtureDefinition fixture = processSnapshot
            .GameplayConfiguration?.EquipmentGenerationBattleSim
            ?? throw new InvalidOperationException(
                "Equipment generation BattleSim fixture configuration is unavailable."
            );
        if (!processSnapshot.Skills.ContainsKey(fixture.BasicAttackSkillId))
            throw new InvalidOperationException(
                $"Equipment generation BattleSim requires basic attack skill {fixture.BasicAttackSkillId}."
            );
        if (!processSnapshot.EnemyBrains.ContainsKey(fixture.MeleeBrainId))
            throw new InvalidOperationException(
                $"Equipment generation BattleSim requires melee brain {fixture.MeleeBrainId}."
            );

        var candidates = content.CandidateItems.Values
            .Where(value => value?.IsEquipment() == true)
            .OrderBy(value => value.ItemId.ToString(), StringComparer.Ordinal)
            .Take(_options.MaximumSampledItems)
            .ToList();
        var diagnostics = new List<ContentJsonDiagnostic>();
        var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["candidate_equipment_item_count"] = content.CandidateItems.Values.Count(
                value => value?.IsEquipment() == true
            ),
            ["configured_seed_count"] = _options.Seeds.Count,
            ["minimum_completed_samples"] = _options.MinimumCompletedSamples,
            ["maximum_sampled_items"] = _options.MaximumSampledItems,
            ["maximum_win_rate_delta_bp"] =
                _options.MaximumWinRateDeltaBasisPoints,
            ["maximum_damage_ratio_bp"] =
                _options.MaximumDamageRatioBasisPoints,
        };
        if (candidates.Count == 0)
        {
            diagnostics.Add(new ContentJsonDiagnostic(
                EquipmentClosureGenerationBattleSimRules.NoEquipmentCandidate,
                "Generated equipment closure must contain at least one equipment item for BattleSim sampling.",
                "equipment_closure#<batch>",
                "",
                "at least one candidate item with item_category=equipment",
                "0"
            ));
            return new EquipmentClosureGenerationBattleSimulationGateResult(
                0,
                diagnostics,
                metrics
            );
        }

        using var provider = new BattleSimContentProvider(
            processSnapshot,
            processSnapshot.Skills,
            content.CombinedItems,
            content.CombinedTraits,
            content.CombinedEquipmentAbilityBindings
        );
        var runner = new BattleSimRunner(provider);
        int completedCandidateSamples = 0;
        int sampledItemCount = 0;
        foreach (ItemDefinition candidate in candidates)
        {
            JsonContentEntryContext context = content.ItemContexts.TryGetValue(
                candidate.ItemId,
                out JsonContentEntryContext? found
            )
                ? found
                : throw new InvalidOperationException(
                    $"Equipment generation BattleSim is missing source context for {candidate.ItemId}."
                );
            try
            {
                ItemDefinition? baselineItem =
                    EquipmentClosureGenerationBattleSimScenarioFactory.SelectBaselineItem(
                        candidate,
                        processSnapshot
                    );
                BattleUnitState baselineAlly =
                    EquipmentClosureGenerationBattleSimScenarioFactory.BuildProjectedAlly(
                        $"equipment_generation_{candidate.ItemId}_baseline_ally",
                        baselineItem,
                        content,
                        processSnapshot,
                        fixture
                    );
                BattleUnitState candidateAlly =
                    EquipmentClosureGenerationBattleSimScenarioFactory.BuildProjectedAlly(
                        $"equipment_generation_{candidate.ItemId}_candidate_ally",
                        candidate,
                        content,
                        processSnapshot,
                        fixture
                    );
                int sourceCount = candidateAlly
                    .GetEquipmentAbilitySourcesReadViewTyped()
                    .Count;
                BattleSimScenarioReport baselineReport = runner.RunScenario(
                    EquipmentClosureGenerationBattleSimScenarioFactory.Create(
                        candidate,
                        baselineItem,
                        baselineAlly,
                        candidateArm: false,
                        _options.Seeds,
                        fixture
                    ),
                    new[] { StandardProfile }
                );
                BattleSimScenarioReport candidateReport = runner.RunScenario(
                    EquipmentClosureGenerationBattleSimScenarioFactory.Create(
                        candidate,
                        candidate,
                        candidateAlly,
                        candidateArm: true,
                        _options.Seeds,
                        fixture
                    ),
                    new[] { StandardProfile }
                );
                EquipmentClosureGenerationBattleSimStrengthSample sample = BuildSample(
                    candidate,
                    baselineItem,
                    sourceCount,
                    baselineReport,
                    candidateReport
                );
                sampledItemCount += 1;
                completedCandidateSamples += sample.CandidateCompletedSamples;
                AddMetrics(metrics, sample);
                diagnostics.AddRange(
                    EquipmentClosureGenerationBattleSimStrengthAnalyzer.Evaluate(
                        sample,
                        _options,
                        context
                    )
                );
            }
            catch (Exception exception)
            {
                diagnostics.Add(new ContentJsonDiagnostic(
                    EquipmentClosureGenerationBattleSimRules.ProjectionFailed,
                    "Candidate equipment could not be projected into the canonical BattleSim fixture.",
                    context.SourceLabel,
                    context.JsonPointer,
                    "successful item -> character equipment -> battle unit projection",
                    $"{exception.GetType().Name}: {exception.Message}"
                ));
            }
        }
        metrics["sampled_item_count"] = sampledItemCount;
        metrics["completed_candidate_sample_count"] = completedCandidateSamples;
        metrics["diagnostic_count"] = diagnostics.Count;
        return new EquipmentClosureGenerationBattleSimulationGateResult(
            sampledItemCount,
            diagnostics,
            metrics
        );
    }

    private static EquipmentClosureGenerationBattleSimStrengthSample BuildSample(
        ItemDefinition candidate,
        ItemDefinition? baselineItem,
        int sourceCount,
        BattleSimScenarioReport baseline,
        BattleSimScenarioReport candidateReport
    )
    {
        BattleSimProfileSummary baselineSummary = RequireSingleSummary(baseline);
        BattleSimProfileSummary candidateSummary = RequireSingleSummary(candidateReport);
        int baselineWinRate = RateBasisPoints(
            baselineSummary.WinRateByFaction,
            AllyFactionId
        );
        int candidateWinRate = RateBasisPoints(
            candidateSummary.WinRateByFaction,
            AllyFactionId
        );
        int baselineDamage = DamagePerCompletedRunBasisPoints(baselineSummary);
        int candidateDamage = DamagePerCompletedRunBasisPoints(candidateSummary);
        return new EquipmentClosureGenerationBattleSimStrengthSample(
            candidate.ItemId.ToString(),
            baselineSummary.CompletedRunCount,
            candidateSummary.CompletedRunCount,
            baselineWinRate,
            candidateWinRate,
            candidateWinRate - baselineWinRate,
            baselineDamage,
            candidateDamage,
            RatioBasisPoints(candidateDamage, baselineDamage),
            sourceCount,
            baselineItem?.ItemId.ToString() ?? "",
            baseline.OutputFiles?.ReportJson ?? "",
            candidateReport.OutputFiles?.ReportJson ?? ""
        );
    }

    private static BattleSimProfileSummary RequireSingleSummary(
        BattleSimScenarioReport report
    )
    {
        if (report.ProfileEntries.Count != 1 || report.ProfileEntries[0]?.Summary == null)
            throw new InvalidOperationException(
                "Equipment generation BattleSim requires exactly one profile summary."
            );
        return report.ProfileEntries[0].Summary;
    }

    private static int RateBasisPoints(
        IReadOnlyDictionary<string, float> rates,
        string key
    ) => rates.TryGetValue(key, out float value)
        ? (int)Math.Round(value * 10000.0f, MidpointRounding.AwayFromZero)
        : 0;

    private static int DamagePerCompletedRunBasisPoints(
        BattleSimProfileSummary summary
    )
    {
        if (
            summary.CompletedRunCount <= 0
            || !summary.FactionMetricTotals.TryGetValue(
                AllyFactionId,
                out BattleSimFactionMetricSummary? metrics
            )
        )
        {
            return 0;
        }
        return (int)Math.Round(
            metrics.TotalDamageDone * 10000.0 / summary.CompletedRunCount,
            MidpointRounding.AwayFromZero
        );
    }

    private static int RatioBasisPoints(int candidate, int baseline)
    {
        if (baseline <= 0)
            return candidate <= 0 ? 10000 : 100000;
        return (int)Math.Clamp(
            Math.Round(candidate * 10000.0 / baseline, MidpointRounding.AwayFromZero),
            0,
            100000
        );
    }

    private static void AddMetrics(
        IDictionary<string, object> metrics,
        EquipmentClosureGenerationBattleSimStrengthSample sample
    )
    {
        string prefix = $"item.{sample.ItemId}";
        metrics[$"{prefix}.baseline_item_id"] = sample.BaselineItemId;
        metrics[$"{prefix}.baseline_completed_samples"] =
            sample.BaselineCompletedSamples;
        metrics[$"{prefix}.candidate_completed_samples"] =
            sample.CandidateCompletedSamples;
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
        metrics[$"{prefix}.equipment_ability_source_count"] =
            sample.EquipmentAbilitySourceCount;
        metrics[$"{prefix}.baseline_report"] = sample.BaselineReportPath;
        metrics[$"{prefix}.candidate_report"] = sample.CandidateReportPath;
    }
}
