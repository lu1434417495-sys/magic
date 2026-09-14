using System;
using System.Collections.Generic;
using Godot;
using IOException = System.IO.IOException;

public partial class run_battle_sim_analysis_output_contract_regression
    : LifecycleTestSceneTree
{
    private sealed class SelectiveFailureSink : IBattleSimOutputFileSink
    {
        private readonly GodotBattleSimOutputFileSink _inner = new();
        private readonly string _failingOwnerLabel;

        internal SelectiveFailureSink(string failingOwnerLabel)
        {
            _failingOwnerLabel = failingOwnerLabel ?? "";
        }

        internal List<string> AttemptedPaths { get; } = new();

        public void Write(
            string path,
            string ownerLabel,
            Action<Godot.FileAccess> writeAction
        )
        {
            AttemptedPaths.Add(path);
            if (
                string.Equals(
                    ownerLabel,
                    _failingOwnerLabel,
                    StringComparison.Ordinal
                )
            )
            {
                throw new IOException($"Injected output failure for {ownerLabel}.");
            }
            _inner.Write(path, ownerLabel, writeAction);
        }
    }

    private readonly TestHarness _test = new();
    private readonly string _tempRoot =
        $"user://battle_sim_analysis_output_contract_{Guid.NewGuid():N}";
    private readonly List<string> _createdFiles = new();

    public override void _Initialize()
    {
        try
        {
            TestExitCodePolicyUsesCompletionAndEveryRequiredArtifact();
            TestInjectedMainTraceAndProfileFailuresReachExitPolicy();
            TestCheckedWriterRejectsDirectoryCreationFailure();
            TestCheckedWriterPropagatesOpenFailure();
            TestAiProfileReportsPropagateEveryRequiredArtifactFailure();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unhandled battle simulation analysis output exception: {exception}"
            );
        }
        finally
        {
            CleanupGeneratedArtifacts();
        }

        RequestTestExit(
            _test.Finish("Battle simulation analysis output contract regression")
        );
    }

    private void TestExitCodePolicyUsesCompletionAndEveryRequiredArtifact()
    {
        BattleSimAnalysisArtifactStatus success =
            BattleSimAnalysisArtifactStatus.Success;
        BattleSimAnalysisArtifactStatus skipped =
            BattleSimAnalysisArtifactStatus.NotRequired;

        _test.Eq(
            BattleSimAnalysisExitCodePolicy.Resolve(
                new BattleSimAnalysisCompletionStatus(
                    true,
                    success,
                    success,
                    success
                )
            ),
            BattleSimAnalysisExitCodePolicy.Success,
            "A complete batch with every required artifact should return zero."
        );
        _test.Eq(
            BattleSimAnalysisExitCodePolicy.Resolve(
                new BattleSimAnalysisCompletionStatus(
                    false,
                    success,
                    success,
                    success
                )
            ),
            BattleSimAnalysisExitCodePolicy.IncompleteBatch,
            "An incomplete batch with successful writes should return two."
        );
        _test.Eq(
            BattleSimAnalysisExitCodePolicy.Resolve(
                new BattleSimAnalysisCompletionStatus(
                    true,
                    skipped,
                    skipped,
                    skipped
                )
            ),
            BattleSimAnalysisExitCodePolicy.Success,
            "Stdout-only analysis should not require file artifacts."
        );
    }

    private void TestInjectedMainTraceAndProfileFailuresReachExitPolicy()
    {
        AssertInjectedArtifactFailure(
            BattleSimAnalysisArtifactKind.Main,
            "analysis-contract-main"
        );
        AssertInjectedArtifactFailure(
            BattleSimAnalysisArtifactKind.Trace,
            "analysis-contract-trace"
        );
        AssertInjectedArtifactFailure(
            BattleSimAnalysisArtifactKind.Profile,
            "analysis-contract-profile"
        );
    }

    private void AssertInjectedArtifactFailure(
        BattleSimAnalysisArtifactKind failingKind,
        string failingOwnerLabel
    )
    {
        var sink = new SelectiveFailureSink(failingOwnerLabel);
        var writer = new BattleSimAnalysisArtifactFileWriter(sink);
        BattleSimAnalysisArtifactWriteResult main = WriteProbeArtifact(
            writer,
            BattleSimAnalysisArtifactKind.Main,
            "analysis-contract-main",
            failingKind
        );
        BattleSimAnalysisArtifactWriteResult trace = WriteProbeArtifact(
            writer,
            BattleSimAnalysisArtifactKind.Trace,
            "analysis-contract-trace",
            failingKind
        );
        BattleSimAnalysisArtifactWriteResult profile = WriteProbeArtifact(
            writer,
            BattleSimAnalysisArtifactKind.Profile,
            "analysis-contract-profile",
            failingKind
        );

        BattleSimAnalysisArtifactWriteResult failed = failingKind switch
        {
            BattleSimAnalysisArtifactKind.Main => main,
            BattleSimAnalysisArtifactKind.Trace => trace,
            _ => profile,
        };
        _test.False(
            failed.Status.Succeeded,
            $"Injected {failingKind} failure must be reported as a failed artifact."
        );
        _test.True(
            !string.IsNullOrEmpty(failed.ErrorMessage),
            $"Injected {failingKind} failure must preserve a diagnostic error."
        );

        int exitCode = BattleSimAnalysisExitCodePolicy.Resolve(
            new BattleSimAnalysisCompletionStatus(
                true,
                main.Status,
                trace.Status,
                profile.Status
            )
        );
        _test.Eq(
            exitCode,
            BattleSimAnalysisExitCodePolicy.OutputFailure,
            $"A complete batch with a failed {failingKind} artifact must return one."
        );
    }

    private BattleSimAnalysisArtifactWriteResult WriteProbeArtifact(
        BattleSimAnalysisArtifactFileWriter writer,
        BattleSimAnalysisArtifactKind kind,
        string ownerLabel,
        BattleSimAnalysisArtifactKind failingKind
    )
    {
        string path =
            $"{_tempRoot}/{failingKind.ToString().ToLowerInvariant()}/{kind.ToString().ToLowerInvariant()}.json";
        BattleSimAnalysisArtifactWriteResult result = writer.WriteText(
            kind,
            path,
            ownerLabel,
            $"{{\"artifact\":\"{kind}\"}}"
        );
        if (kind != failingKind)
        {
            _test.True(
                result.Status.Succeeded,
                $"The non-failing {kind} artifact should still succeed while {failingKind} is injected."
            );
        }
        if (result.Status.Succeeded)
        {
            _createdFiles.Add(path);
            _test.True(
                Godot.FileAccess.FileExists(path),
                $"Successful {kind} probe should create its artifact."
            );
        }
        return result;
    }

    private void TestCheckedWriterRejectsDirectoryCreationFailure()
    {
        string blockerPath = $"{_tempRoot}/directory_blocker";
        BattleSimAnalysisArtifactWriteResult blockerWrite =
            new BattleSimAnalysisArtifactFileWriter().WriteText(
                BattleSimAnalysisArtifactKind.Main,
                blockerPath,
                "analysis-contract-directory-blocker",
                "blocker"
            );
        _test.True(
            blockerWrite.Status.Succeeded,
            "Directory failure fixture should first create a blocking file."
        );
        if (blockerWrite.Status.Succeeded)
            _createdFiles.Add(blockerPath);

        string nestedPath = $"{blockerPath}/nested.json";
        BattleSimAnalysisArtifactWriteResult nestedWrite =
            new BattleSimAnalysisArtifactFileWriter().WriteText(
                BattleSimAnalysisArtifactKind.Main,
                nestedPath,
                "analysis-contract-directory-failure",
                "{}"
            );
        _test.False(
            nestedWrite.Status.Succeeded,
            "A file occupying the requested directory must make the write fail."
        );
        _test.False(
            Godot.FileAccess.FileExists(nestedPath),
            "A directory creation failure must not be reported as a created artifact."
        );
    }

    private void TestCheckedWriterPropagatesOpenFailure()
    {
        string directoryTarget = $"{_tempRoot}/unopenable_directory_target";
        Error createError = DirAccess.MakeDirRecursiveAbsolute(
            ProjectSettings.GlobalizePath(directoryTarget)
        );
        _test.Eq(
            createError,
            Error.Ok,
            "Open-failure fixture should create a directory target."
        );

        BattleSimAnalysisArtifactWriteResult result =
            new BattleSimAnalysisArtifactFileWriter().WriteText(
                BattleSimAnalysisArtifactKind.Trace,
                directoryTarget,
                "analysis-contract-open-failure",
                "{}"
            );
        _test.False(
            result.Status.Succeeded,
            "Opening a directory as an artifact file must produce a failed write result."
        );
        _test.True(
            !string.IsNullOrEmpty(result.ErrorMessage),
            "The checked writer should preserve the open-failure diagnostic."
        );
    }

    private void TestAiProfileReportsPropagateEveryRequiredArtifactFailure()
    {
        AssertAiProfileArtifactFailure(
            "ai-profile-hotspots-text",
            "hotspots",
            dumpTraceJson: false
        );
        AssertAiProfileArtifactFailure(
            "ai-profile-functions-csv",
            "functions_csv",
            dumpTraceJson: false
        );
        AssertAiProfileArtifactFailure(
            "ai-profile-trace-json",
            "trace",
            dumpTraceJson: true
        );
    }

    private void AssertAiProfileArtifactFailure(
        string failingOwnerLabel,
        string failingArtifact,
        bool dumpTraceJson
    )
    {
        var sink = new SelectiveFailureSink(failingOwnerLabel);
        var writer = new BattleSimAnalysisArtifactFileWriter(sink);
        using var capture = new AiProfileCapture(writer);
        capture.Setup(
            $"output_contract_{failingArtifact}",
            $"{_tempRoot}/profile/{failingArtifact}/",
            5,
            "self_usec",
            dumpTraceJson: dumpTraceJson
        );

        if (dumpTraceJson)
        {
            AiTraceRecorder recorder = capture.BeginRun();
            try
            {
                AiTraceRecorder.Enter("analysis_output_required_trace");
                AiTraceRecorder.Exit("analysis_output_required_trace");
            }
            finally
            {
                capture.EndRun(recorder, aiTurns: 1);
            }
        }

        AiProfileReportWriteResult result = capture.WriteReports();
        _test.False(
            result.ArtifactStatus.Succeeded,
            $"A failed required {failingArtifact} artifact must fail the aggregate profile status."
        );
        _test.Eq(
            BattleSimAnalysisExitCodePolicy.Resolve(
                new BattleSimAnalysisCompletionStatus(
                    true,
                    BattleSimAnalysisArtifactStatus.NotRequired,
                    BattleSimAnalysisArtifactStatus.NotRequired,
                    result.ArtifactStatus
                )
            ),
            BattleSimAnalysisExitCodePolicy.OutputFailure,
            $"A required {failingArtifact} failure must reach the analysis exit code."
        );

        bool hotspotsWritten = GetReportBool(result.Report, "wrote_hotspots");
        bool csvWritten = GetReportBool(result.Report, "wrote_functions_csv");
        bool traceWritten = GetReportBool(result.Report, "wrote_trace");
        _test.Eq(
            hotspotsWritten,
            failingArtifact != "hotspots",
            $"The hotspots result should reflect the actual {failingArtifact} failure branch."
        );
        _test.Eq(
            csvWritten,
            failingArtifact != "functions_csv",
            $"The functions CSV result should reflect the actual {failingArtifact} failure branch."
        );
        _test.Eq(
            traceWritten,
            dumpTraceJson && failingArtifact != "trace",
            $"The trace result should reflect whether trace output was required and whether {failingArtifact} failed."
        );
        _test.Eq(
            GetReportBool(result.Report, "trace_json"),
            dumpTraceJson,
            "The profile summary should preserve whether trace output was requested."
        );
        if (dumpTraceJson)
        {
            _test.True(
                result.Report.TryGetValue("trace_path", out object tracePathValue)
                    && tracePathValue is string tracePath
                    && !string.IsNullOrEmpty(tracePath),
                "A captured trace must make the trace artifact required."
            );
        }
        _test.True(
            result.Report.TryGetValue("write_errors", out object errorsValue)
                && errorsValue is IReadOnlyCollection<string> errors
                && errors.Count > 0,
            $"The failed {failingArtifact} artifact should publish a diagnostic error."
        );

        TrackReportPath(result.Report, "hotspots_path");
        TrackReportPath(result.Report, "functions_csv_path");
        TrackReportPath(result.Report, "trace_path");
    }

    private static bool GetReportBool(
        IReadOnlyDictionary<string, object> report,
        string key
    ) =>
        report.TryGetValue(key, out object value)
        && value is bool flag
        && flag;

    private void TrackReportPath(
        IReadOnlyDictionary<string, object> report,
        string key
    )
    {
        if (
            report.TryGetValue(key, out object value)
            && value is string path
            && !string.IsNullOrEmpty(path)
            && Godot.FileAccess.FileExists(path)
        )
        {
            _createdFiles.Add(path);
        }
    }

    private void CleanupGeneratedArtifacts()
    {
        foreach (string path in _createdFiles)
            RemoveFileIfPresent(path);

        string absoluteRoot = ProjectSettings.GlobalizePath(_tempRoot);
        if (!DirAccess.DirExistsAbsolute(absoluteRoot))
            return;
        RemoveEmptyDirectories(absoluteRoot);
    }

    private static void RemoveEmptyDirectories(string absoluteDirectory)
    {
        using DirAccess directory = DirAccess.Open(absoluteDirectory);
        if (directory == null)
            return;
        directory.ListDirBegin();
        string entry = directory.GetNext();
        var childDirectories = new List<string>();
        while (!string.IsNullOrEmpty(entry))
        {
            if (
                directory.CurrentIsDir()
                && entry != "."
                && entry != ".."
            )
            {
                childDirectories.Add(absoluteDirectory.PathJoin(entry));
            }
            entry = directory.GetNext();
        }
        directory.ListDirEnd();

        foreach (string childDirectory in childDirectories)
            RemoveEmptyDirectories(childDirectory);
        DirAccess.RemoveAbsolute(absoluteDirectory);
    }

    private static void RemoveFileIfPresent(string path)
    {
        if (!string.IsNullOrEmpty(path) && Godot.FileAccess.FileExists(path))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }
}
