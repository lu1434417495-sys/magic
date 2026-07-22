using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_sim_report_output_regression : LifecycleTestSceneTree
{
    private sealed class FailingOutputFileSink : IBattleSimOutputFileSink
    {
        private readonly GodotBattleSimOutputFileSink _inner = new();
        private int _writeCount;

        internal InvalidOperationException ExpectedFailure { get; } =
            new("expected battle simulation output failure");
        internal List<string> AttemptedPaths { get; } = new();

        public void Write(string path, string ownerLabel, Action<FileAccess> writeAction)
        {
            AttemptedPaths.Add(path);
            _writeCount++;
            if (_writeCount == 1)
            {
                _inner.Write(path, ownerLabel, writeAction);
                return;
            }
            throw ExpectedFailure;
        }
    }

    private const long FixedTimestampSeconds = 1780000000;
    private const string ScenarioId = "report_output_contract";
    private const string FailureScenarioId = "report_output_failure";
    private const string UnopenableTargetName = "unopenable_file_target";

    private readonly TestHarness _test = new();
    private readonly string _tempRoot =
        $"user://battle_sim_report_output_regression_{Guid.NewGuid():N}";
    private readonly List<string> _createdFiles = new();

    public override void _Initialize()
    {
        try
        {
            TestSameSecondWritesRemainDistinctAndParseable();
            TestWriteFailureRestoresPreviousOutputFiles();
            TestGodotSinkRejectsUnopenablePath();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled battle simulation report output exception: {exception}");
        }
        finally
        {
            CleanupGeneratedArtifacts();
        }

        RequestTestExit(_test.Finish("Battle simulation report output regression"));
    }

    private void TestSameSecondWritesRemainDistinctAndParseable()
    {
        var writer = new BattleSimReportFileWriter(
            _tempRoot,
            static () => FixedTimestampSeconds,
            new GodotBattleSimOutputFileSink()
        );
        BattleSimScenarioDefinition scenario = BuildScenario(ScenarioId);
        BattleSimScenarioReport firstReport = BuildReport(scenario, "first_battle");
        BattleSimScenarioReport secondReport = BuildReport(scenario, "second_battle");

        BattleSimOutputFiles firstOutput = writer.Write(scenario, firstReport);
        Track(firstOutput);
        BattleSimOutputFiles secondOutput = writer.Write(scenario, secondReport);
        Track(secondOutput);

        _test.True(
            firstOutput.ReportJson.Contains($"_{FixedTimestampSeconds}_"),
            "First output should use the injected second-level timestamp."
        );
        _test.True(
            secondOutput.ReportJson.Contains($"_{FixedTimestampSeconds}_"),
            "Second output should use the same injected second-level timestamp."
        );
        _test.True(
            firstOutput.ReportJson != secondOutput.ReportJson,
            "Two report writes in the same second must use distinct report paths."
        );
        _test.True(
            firstOutput.TurnTraceJsonl != secondOutput.TurnTraceJsonl,
            "Two report writes in the same second must use distinct trace paths."
        );
        _test.True(
            firstOutput.TraceSummaryJson != secondOutput.TraceSummaryJson,
            "Two report writes in the same second must use distinct summary paths."
        );

        AssertCompleteArtifactSet(firstOutput, "first");
        AssertCompleteArtifactSet(secondOutput, "second");
    }

    private void TestWriteFailureRestoresPreviousOutputFiles()
    {
        var failingSink = new FailingOutputFileSink();
        var writer = new BattleSimReportFileWriter(
            _tempRoot,
            static () => FixedTimestampSeconds,
            failingSink
        );
        BattleSimScenarioDefinition scenario = BuildScenario(FailureScenarioId);
        BattleSimScenarioReport report = BuildReport(scenario, "failure_battle");
        var previousOutputFiles = new BattleSimOutputFiles
        {
            ReportJson = "previous_report.json",
            TurnTraceJsonl = "previous_traces.jsonl",
        };
        report.OutputFiles = previousOutputFiles;

        Exception observedFailure = null;
        try
        {
            writer.Write(scenario, report);
        }
        catch (Exception exception)
        {
            observedFailure = exception;
        }

        _test.True(
            ReferenceEquals(observedFailure, failingSink.ExpectedFailure),
            "Output writer should preserve the actual file-write failure."
        );
        _test.True(
            ReferenceEquals(report.OutputFiles, previousOutputFiles),
            "A failed write must restore the report's previous output-file contract."
        );
        _test.True(
            failingSink.AttemptedPaths.Count >= 2,
            "Failure fixture should fail only after one artifact was written."
        );
        if (failingSink.AttemptedPaths.Count > 0)
        {
            _test.False(
                FileAccess.FileExists(failingSink.AttemptedPaths[0]),
                "A later write failure should remove the artifact already written for this batch."
            );
        }
    }

    private void TestGodotSinkRejectsUnopenablePath()
    {
        string directoryPath = $"{_tempRoot}/{UnopenableTargetName}";
        Error createDirectoryError = DirAccess.MakeDirRecursiveAbsolute(
            ProjectSettings.GlobalizePath(directoryPath)
        );
        _test.Eq(
            createDirectoryError,
            Error.Ok,
            "Failure fixture should create a directory that cannot be opened as an output file."
        );

        Exception observedFailure = null;
        try
        {
            new GodotBattleSimOutputFileSink().Write(
                directoryPath,
                "battle-sim-unopenable-output-test",
                _ => { }
            );
        }
        catch (Exception exception)
        {
            observedFailure = exception;
        }

        _test.True(
            observedFailure is System.IO.IOException,
            "Godot output sink should propagate FileAccess.Open failure as IOException."
        );
    }

    private void AssertCompleteArtifactSet(BattleSimOutputFiles outputFiles, string label)
    {
        _test.True(
            FileAccess.FileExists(outputFiles.ReportJson),
            $"{label} report JSON should exist."
        );
        _test.True(
            FileAccess.FileExists(outputFiles.TurnTraceJsonl),
            $"{label} trace JSONL should exist."
        );
        _test.True(
            FileAccess.FileExists(outputFiles.TraceSummaryJson),
            $"{label} trace summary JSON should exist."
        );

        using GDictionary reportPayload = ReadJsonObject(
            outputFiles.ReportJson,
            $"{label} report"
        );
        if (reportPayload.ContainsKey("output_files"))
        {
            using GDictionary projectedOutputFiles =
                reportPayload["output_files"].AsGodotDictionary();
            _test.Eq(
                projectedOutputFiles.GetValueOrDefault("report_json", "").AsString(),
                outputFiles.ReportJson,
                $"{label} report should embed its committed report path."
            );
            _test.Eq(
                projectedOutputFiles.GetValueOrDefault("turn_trace_jsonl", "").AsString(),
                outputFiles.TurnTraceJsonl,
                $"{label} report should embed its committed trace path."
            );
        }
        else
        {
            _test.Fail($"{label} report should contain output_files.");
        }

        int traceLineCount = CountParseableJsonLines(
            outputFiles.TurnTraceJsonl,
            $"{label} trace"
        );
        _test.Eq(traceLineCount, 1, $"{label} trace JSONL should contain one trace row.");

        using GDictionary summaryPayload = ReadJsonObject(
            outputFiles.TraceSummaryJson,
            $"{label} trace summary"
        );
        _test.Eq(
            summaryPayload.GetValueOrDefault("source_report", "").AsString(),
            outputFiles.ReportJson,
            $"{label} trace summary should point at its committed report."
        );
    }

    private GDictionary ReadJsonObject(string path, string label)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            _test.Fail($"Could not open {label}: {FileAccess.GetOpenError()} path={path}");
            return new GDictionary();
        }

        Variant parsed = Json.ParseString(file.GetAsText());
        _test.Eq(
            parsed.VariantType,
            Variant.Type.Dictionary,
            $"{label} should contain a parseable JSON object."
        );
        return parsed.VariantType == Variant.Type.Dictionary
            ? parsed.AsGodotDictionary()
            : new GDictionary();
    }

    private int CountParseableJsonLines(string path, string label)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            _test.Fail($"Could not open {label}: {FileAccess.GetOpenError()} path={path}");
            return 0;
        }

        int lineCount = 0;
        while (file.GetPosition() < file.GetLength())
        {
            string line = file.GetLine();
            if (string.IsNullOrWhiteSpace(line))
                continue;
            Variant parsed = Json.ParseString(line);
            _test.Eq(
                parsed.VariantType,
                Variant.Type.Dictionary,
                $"{label} row {lineCount + 1} should be a JSON object."
            );
            lineCount++;
        }
        return lineCount;
    }

    private static BattleSimScenarioDefinition BuildScenario(string scenarioId)
    {
        return new BattleSimScenarioDefinition(
            new StringName(scenarioId),
            scenarioId,
            "",
            Vector2I.One,
            "",
            false,
            Vector2I.Zero,
            Array.Empty<BattleSimScenarioUnitEntry>(),
            Array.Empty<BattleSimScenarioUnitEntry>(),
            0,
            0,
            new Dictionary<Vector2I, IReadOnlyDictionary<string, object>>(),
            1,
            5,
            1,
            "wait",
            true,
            new[] { 101 }
        );
    }

    private static BattleSimScenarioReport BuildReport(
        BattleSimScenarioDefinition scenario,
        string battleId
    )
    {
        var report = new BattleSimScenarioReport
        {
            Scenario = scenario,
            GeneratedAtUnix = (int)FixedTimestampSeconds,
        };
        var profileEntry = new BattleSimProfileReportEntry();
        BattleSimRunReport run = new()
        {
            ScenarioId = scenario.ScenarioId.ToString(),
            ProfileId = "output_contract_profile",
            Seed = 101,
            BattleId = battleId,
            TerminationKind = BattleSimTerminationKind.BattleEnded,
            AiTurnTraces = new[]
            {
                new BattleAiTurnTraceProjection
                {
                    BattleId = battleId,
                    TurnStartedTu = 5,
                    UnitId = "output_contract_unit",
                    UnitName = "Output Contract Unit",
                    FactionId = "player",
                    BrainId = "output_contract_brain",
                    StateId = "output_contract_state",
                    ActionId = "wait",
                    ReasonText = "output contract trace",
                },
            },
        };
        run.SetFinalDecision(
            BattleObjectiveTestFactory.CreateEliminationDecision("player", decisionTu: 5)
        );
        profileEntry.Runs.Add(run);
        report.ProfileEntries.Add(profileEntry);
        return report;
    }

    private void Track(BattleSimOutputFiles outputFiles)
    {
        _createdFiles.Add(outputFiles.ReportJson);
        _createdFiles.Add(outputFiles.TurnTraceJsonl);
        _createdFiles.Add(outputFiles.TraceSummaryJson);
    }

    private void CleanupGeneratedArtifacts()
    {
        foreach (string path in _createdFiles)
            RemoveFileIfPresent(path);
        RemoveDirectoryIfPresent($"{_tempRoot}/{ScenarioId}");
        RemoveDirectoryIfPresent($"{_tempRoot}/{FailureScenarioId}");
        RemoveDirectoryIfPresent($"{_tempRoot}/{UnopenableTargetName}");
        RemoveDirectoryIfPresent(_tempRoot);
    }

    private static void RemoveFileIfPresent(string path)
    {
        if (string.IsNullOrEmpty(path) || !FileAccess.FileExists(path))
            return;
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }

    private static void RemoveDirectoryIfPresent(string path)
    {
        string absolutePath = ProjectSettings.GlobalizePath(path);
        if (DirAccess.DirExistsAbsolute(absolutePath))
            DirAccess.RemoveAbsolute(absolutePath);
    }
}
