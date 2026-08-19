#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_skill_generation_validation_pipeline_regression
    : LifecycleTestSceneTree
{
    private const string SourceDirectory = "res://tests/fixtures/generated_skills";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestSchemaRejectionStopsPipeline();
            TestDomainRejectionStopsPipeline();
            TestCrossDomainRejectionStopsPipeline();
            TestBattleSimulationRejectionUsesDedicatedExitCode();
            TestValidBatchPassesAllFourStages();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unexpected skill generation validation pipeline exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Skill generation validation pipeline regression"));
    }

    private void TestSchemaRejectionStopsPipeline()
    {
        var simulation = new FakeSimulationGate();
        SkillGenerationValidationReport report = Validate(
            Entry(
                "schema_bad",
                "\"skill_type\":\"passive\",\"max_level\":0,\"unexpected\":true"
            ),
            simulation
        );

        AssertRejected(
            report,
            SkillGenerationValidationStageKind.Schema,
            SkillGenerationValidationExitCodes.SchemaRejected,
            expectedStageCount: 1
        );
        _test.Eq(simulation.CallCount, 0, "schema rejection must not run BattleSim");
        _test.True(
            report.Stages[0].Diagnostics.Any(value =>
                value.SourceLabel == "pipeline.json#schema_bad"
                && value.JsonPointer == "/entries/0/unexpected"
            ),
            "schema rejection should retain file, entry ID, and exact pointer"
        );
    }

    private void TestDomainRejectionStopsPipeline()
    {
        var simulation = new FakeSimulationGate();
        SkillGenerationValidationReport report = Validate(
            Entry("domain_bad", "\"max_level\":0", displayName: ""),
            simulation
        );

        AssertRejected(
            report,
            SkillGenerationValidationStageKind.Domain,
            SkillGenerationValidationExitCodes.DomainRejected,
            expectedStageCount: 2
        );
        _test.Eq(simulation.CallCount, 0, "domain rejection must not run BattleSim");
        _test.True(
            report.Stages[1].Diagnostics.Any(value =>
                value.RuleId == "skill.validation.domain_rule"
                && value.SourceLabel == "pipeline.json#domain_bad"
            ),
            "domain rejection should use the canonical import validator"
        );
    }

    private void TestCrossDomainRejectionStopsPipeline()
    {
        var simulation = new FakeSimulationGate();
        SkillGenerationValidationReport report = Validate(
            Entry(
                "cross_bad",
                "\"skill_type\":\"passive\",\"max_level\":0,"
                    + "\"learn_requirements\":[\"missing_skill\"]"
            ),
            simulation
        );

        AssertRejected(
            report,
            SkillGenerationValidationStageKind.CrossDomain,
            SkillGenerationValidationExitCodes.CrossDomainRejected,
            expectedStageCount: 3
        );
        _test.Eq(simulation.CallCount, 0, "cross-domain rejection must not run BattleSim");
        _test.True(
            report.Stages[2].Diagnostics.Any(value =>
                value.RuleId
                    == SkillGenerationCrossDomainRules.MissingSkillReference
                && value.SourceLabel == "pipeline.json#cross_bad"
                && value.JsonPointer == "/entries/0/learn_requirements/0"
            ),
            "cross-domain rejection should locate the missing reference"
        );
    }

    private void TestBattleSimulationRejectionUsesDedicatedExitCode()
    {
        var simulation = new FakeSimulationGate(reject: true);
        SkillGenerationValidationReport report = Validate(
            Entry(
                "simulation_bad",
                "\"skill_type\":\"passive\",\"max_level\":0"
            ),
            simulation
        );

        AssertRejected(
            report,
            SkillGenerationValidationStageKind.BattleSimulation,
            SkillGenerationValidationExitCodes.BattleSimulationRejected,
            expectedStageCount: 4
        );
        _test.Eq(simulation.CallCount, 1, "BattleSim should run after three passing gates");
    }

    private void TestValidBatchPassesAllFourStages()
    {
        var simulation = new FakeSimulationGate();
        SkillGenerationValidationReport report = Validate(
            Entry(
                "pipeline_pass",
                "\"skill_type\":\"passive\",\"max_level\":0"
            ),
            simulation
        );

        _test.True(report.Success, "valid generated batch should pass all four stages");
        _test.Eq(
            report.ExitCode,
            SkillGenerationValidationExitCodes.Success,
            "passing pipeline should return exit code 0"
        );
        _test.Eq(report.Stages.Count, 4, "passing pipeline should publish four stages");
        _test.True(
            report.Stages.Select(value => value.Stage).SequenceEqual(new[]
            {
                SkillGenerationValidationStageKind.Schema,
                SkillGenerationValidationStageKind.Domain,
                SkillGenerationValidationStageKind.CrossDomain,
                SkillGenerationValidationStageKind.BattleSimulation,
            }),
            "validation stage order should be stable"
        );
        _test.Eq(simulation.CallCount, 1, "passing pipeline should invoke BattleSim once");
        _test.True(
            simulation.LastCombinedSkillCount == 1,
            "BattleSim should receive the generated definition in the combined catalog"
        );
    }

    private SkillGenerationValidationReport Validate(
        string entryJson,
        FakeSimulationGate simulation
    )
    {
        var reader = new FakeSourceReader(new ContentJsonSourceText(
            "res://tests/fixtures/generated_skills/pipeline.json",
            "{\"schema\":1,\"domain\":\"skills\",\"family\":\"pipeline\","
                + "\"templates\":{},\"entries\":[{" + entryJson + "}]}"
        ));
        return new SkillGenerationValidationService(
            SyntheticContentSnapshotFactory.CreateEmpty(),
            simulation
        ).Validate(SourceDirectory, reader);
    }

    private static string Entry(
        string skillId,
        string fields,
        string displayName = "Generated Test Skill"
    ) =>
        $"\"skill_id\":\"{skillId}\",\"display_name\":\"{displayName}\",{fields}";

    private void AssertRejected(
        SkillGenerationValidationReport report,
        SkillGenerationValidationStageKind stage,
        int exitCode,
        int expectedStageCount
    )
    {
        _test.False(report.Success, $"{stage} rejection should fail the pipeline");
        _test.True(report.RejectedStage == stage, $"{stage} should be reported as rejected");
        _test.Eq(report.ExitCode, exitCode, $"{stage} should use its dedicated exit code");
        _test.Eq(
            report.Stages.Count,
            expectedStageCount,
            $"{stage} rejection should short-circuit later stages"
        );
    }

    private sealed class FakeSourceReader : IContentJsonSourceReader
    {
        private readonly IReadOnlyList<ContentJsonSourceText> _sources;

        internal FakeSourceReader(params ContentJsonSourceText[] sources)
        {
            _sources = sources;
        }

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath) =>
            _sources;
    }

    private sealed class FakeSimulationGate : ISkillGenerationBattleSimulationGate
    {
        private readonly bool _reject;

        internal FakeSimulationGate(bool reject = false)
        {
            _reject = reject;
        }

        internal int CallCount { get; private set; }
        internal int LastCombinedSkillCount { get; private set; }

        public SkillGenerationBattleSimulationGateResult Evaluate(
            IReadOnlyDictionary<StringName, SkillDefinition> candidateSkills,
            IReadOnlyDictionary<StringName, SkillDefinition> combinedSkills,
            ContentSnapshot processSnapshot
        )
        {
            CallCount += 1;
            LastCombinedSkillCount = combinedSkills.Count;
            IReadOnlyList<ContentJsonDiagnostic> diagnostics = _reject
                ? new[]
                {
                    new ContentJsonDiagnostic(
                        "skill.generation.battle_simulation.fixture_rejection",
                        "Synthetic BattleSim gate rejected the fixture.",
                        "pipeline.json#simulation_bad",
                        "/entries/0"
                    ),
                }
                : Array.Empty<ContentJsonDiagnostic>();
            return new SkillGenerationBattleSimulationGateResult(
                candidateSkills.Count,
                diagnostics,
                new Dictionary<string, object>
                {
                    ["fixture_sample_count"] = candidateSkills.Count,
                }
            );
        }
    }
}
