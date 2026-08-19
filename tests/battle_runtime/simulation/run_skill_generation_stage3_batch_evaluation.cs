#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_skill_generation_stage3_batch_evaluation
    : LifecycleTestSceneTree
{
    private const string FixtureRoot =
        "res://tests/fixtures/skill_generation/stage3_batch";

    private static readonly IReadOnlySet<StringName> PilotSkillIds =
        new HashSet<StringName>
        {
            "mage_generated_force_needle",
            "mage_generated_ember_orb",
            "mage_generated_reserve_comet",
        };

    private static readonly IReadOnlyList<BatchCase> Cases = Array.AsReadOnly(
        new[]
        {
            new BatchCase(
                "accepted_force_needle",
                ExpectedAccept: true,
                ExpectedRejectedStage:
                    SkillGenerationValidationStageKind.BattleSimulation,
                ExpectedRuleId:
                    SkillGenerationBattleSimRules.IncompleteSamples
            ),
            new BatchCase(
                "accepted_ember_orb",
                ExpectedAccept: true,
                ExpectedRejectedStage: null,
                ExpectedRuleId: ""
            ),
            new BatchCase(
                "expected_accept_reserve_comet",
                ExpectedAccept: true,
                ExpectedRejectedStage:
                    SkillGenerationValidationStageKind.BattleSimulation,
                ExpectedRuleId:
                    SkillGenerationBattleSimRules.IncompleteSamples
            ),
            new BatchCase(
                "reject_schema_mana_cost",
                ExpectedAccept: false,
                ExpectedRejectedStage: SkillGenerationValidationStageKind.Schema,
                ExpectedRuleId: SkillJsonImportRules.InvalidDto
            ),
            new BatchCase(
                "reject_domain_growth_total",
                ExpectedAccept: false,
                ExpectedRejectedStage: SkillGenerationValidationStageKind.Domain,
                ExpectedRuleId: "skill.validation.domain_rule"
            ),
            new BatchCase(
                "reject_cross_missing_skill",
                ExpectedAccept: false,
                ExpectedRejectedStage:
                    SkillGenerationValidationStageKind.CrossDomain,
                ExpectedRuleId:
                    SkillGenerationCrossDomainRules.MissingSkillReference
            ),
            new BatchCase(
                "reject_simulation_outlier",
                ExpectedAccept: false,
                ExpectedRejectedStage:
                    SkillGenerationValidationStageKind.BattleSimulation,
                ExpectedRuleId: ""
            ),
        }
    );

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunBatch);
    }

    private void RunBatch()
    {
        try
        {
            ContentSnapshot processSnapshot = BuildPreAdmissionSnapshot(
                GameSessionTestFactory.GetProcessSnapshot()
            );
            var results = new List<BatchResult>(Cases.Count);
            foreach (BatchCase batchCase in Cases)
                results.Add(EvaluateCase(batchCase, processSnapshot));

            BatchStatistics statistics = BatchStatistics.From(results);
            AssertInitialRunStatistics(statistics);
            Console.Out.WriteLine(statistics.ToJson());
            foreach (BatchResult result in results)
                Console.Out.Write(SkillGenerationValidationProtocol.FormatNdjson(result.Report));
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unexpected stage 3 generated-skill batch exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Stage 3 generated-skill batch evaluation"));
    }

    private BatchResult EvaluateCase(
        BatchCase batchCase,
        ContentSnapshot processSnapshot
    )
    {
        string sourceDirectory = $"{FixtureRoot}/{batchCase.CaseId}";
        SkillGenerationValidationReport report =
            new SkillGenerationValidationService(
                processSnapshot,
                new SkillGenerationBattleSimGate()
            ).Validate(sourceDirectory, new GodotContentJsonSourceReader());

        if (batchCase.ExpectedRejectedStage != null)
        {
            _test.True(
                report.RejectedStage == batchCase.ExpectedRejectedStage,
                $"{batchCase.CaseId} should stop at {batchCase.ExpectedRejectedStage}"
            );
            SkillGenerationValidationStageReport rejected = report.Stages[^1];
            _test.True(
                rejected.Diagnostics.Any(value =>
                    (
                        string.IsNullOrEmpty(batchCase.ExpectedRuleId)
                        || value.RuleId == batchCase.ExpectedRuleId
                    )
                    && !string.IsNullOrWhiteSpace(value.SourceLabel)
                    && !string.IsNullOrWhiteSpace(value.JsonPointer)
                    && !string.IsNullOrWhiteSpace(value.Expected)
                    && !string.IsNullOrWhiteSpace(value.Actual)
                ),
                $"{batchCase.CaseId} rejection should expose the expected machine-locatable rule"
            );
        }

        return new BatchResult(batchCase, report);
    }

    private void AssertInitialRunStatistics(BatchStatistics statistics)
    {
        _test.Eq(statistics.AttemptedCount, 7, "batch should contain seven labeled cases");
        _test.True(
            statistics.AcceptedCount is >= 0 and <= 1,
            "pre-fix repeated runs should accept zero or one case"
        );
        _test.True(
            statistics.RejectedCount is >= 6 and <= 7,
            "pre-fix repeated runs should reject six or seven cases"
        );
        _test.True(
            statistics.FalsePositiveCount is >= 2 and <= 3,
            "pre-fix repeated runs should expose two or three false positives"
        );
        _test.True(
            statistics.FalsePositiveRateBasisPoints is >= 6667 and <= 10000,
            "pre-fix false-positive rate should remain in the observed 66.67%-100% range"
        );
        _test.Eq(statistics.FalseNegativeCount, 0, "first run should have no false negatives");
        _test.Eq(
            statistics.RejectedByStage[SkillGenerationValidationStageKind.Schema],
            1,
            "schema gate should reject one case"
        );
        _test.Eq(
            statistics.RejectedByStage[SkillGenerationValidationStageKind.Domain],
            1,
            "domain gate should reject one case"
        );
        _test.Eq(
            statistics.RejectedByStage[SkillGenerationValidationStageKind.CrossDomain],
            1,
            "cross-domain gate should reject one case"
        );
        _test.True(
            statistics.RejectedByStage[
                SkillGenerationValidationStageKind.BattleSimulation
            ] is >= 3 and <= 4,
            "BattleSim should reject three or four cases before the fixture correction"
        );
    }

    private static ContentSnapshot BuildPreAdmissionSnapshot(ContentSnapshot source)
    {
        SyntheticContentSnapshotSeed seed =
            SyntheticContentSnapshotFactory.CreateSeed(source);
        seed.Skills = source.Skills
            .Where(pair => !PilotSkillIds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        return SyntheticContentSnapshotFactory.Create(seed);
    }

    private sealed record BatchCase(
        string CaseId,
        bool ExpectedAccept,
        SkillGenerationValidationStageKind? ExpectedRejectedStage,
        string ExpectedRuleId
    );

    private sealed record BatchResult(
        BatchCase Case,
        SkillGenerationValidationReport Report
    );

    private sealed record BatchStatistics(
        int AttemptedCount,
        int AcceptedCount,
        int RejectedCount,
        IReadOnlyDictionary<SkillGenerationValidationStageKind, int> RejectedByStage,
        int IntendedAcceptCount,
        int FalsePositiveCount,
        int FalsePositiveRateBasisPoints,
        int FalseNegativeCount
    )
    {
        internal static BatchStatistics From(IReadOnlyList<BatchResult> results)
        {
            var rejectedByStage = Enum
                .GetValues<SkillGenerationValidationStageKind>()
                .ToDictionary(stage => stage, _ => 0);
            int accepted = 0;
            int intendedAccept = 0;
            int falsePositive = 0;
            int falseNegative = 0;
            foreach (BatchResult result in results)
            {
                if (result.Report.Success)
                    accepted += 1;
                else if (result.Report.RejectedStage is { } rejectedStage)
                    rejectedByStage[rejectedStage] += 1;

                if (result.Case.ExpectedAccept)
                {
                    intendedAccept += 1;
                    if (!result.Report.Success)
                        falsePositive += 1;
                }
                else if (result.Report.Success)
                {
                    falseNegative += 1;
                }
            }

            int falsePositiveRate = intendedAccept == 0
                ? 0
                : (int)Math.Round(
                    falsePositive * 10000.0 / intendedAccept,
                    MidpointRounding.AwayFromZero
                );
            return new BatchStatistics(
                results.Count,
                accepted,
                results.Count - accepted,
                rejectedByStage,
                intendedAccept,
                falsePositive,
                falsePositiveRate,
                falseNegative
            );
        }

        internal string ToJson() =>
            "{\"type\":\"skill_generation_batch_summary\","
            + $"\"attempted\":{AttemptedCount},"
            + $"\"accepted\":{AcceptedCount},"
            + $"\"rejected\":{RejectedCount},"
            + "\"rejected_by_stage\":{"
            + $"\"schema\":{RejectedByStage[SkillGenerationValidationStageKind.Schema]},"
            + $"\"domain\":{RejectedByStage[SkillGenerationValidationStageKind.Domain]},"
            + $"\"cross_domain\":{RejectedByStage[SkillGenerationValidationStageKind.CrossDomain]},"
            + $"\"battle_simulation\":{RejectedByStage[SkillGenerationValidationStageKind.BattleSimulation]}"
            + "},"
            + $"\"intended_accept\":{IntendedAcceptCount},"
            + $"\"false_positive_count\":{FalsePositiveCount},"
            + $"\"false_positive_rate_bp\":{FalsePositiveRateBasisPoints},"
            + $"\"false_negative_count\":{FalseNegativeCount}}}";
    }
}
