using System;
using System.Collections.Generic;
using Godot;
using FileAccess = Godot.FileAccess;
using GDictionary = Godot.Collections.Dictionary;
using IOException = System.IO.IOException;

internal interface IBattleSimOutputFileSink
{
    void Write(string path, string ownerLabel, Action<FileAccess> writeAction);
}

internal sealed class GodotBattleSimOutputFileSink : IBattleSimOutputFileSink
{
    public void Write(string path, string ownerLabel, Action<FileAccess> writeAction)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(ownerLabel);
        ArgumentNullException.ThrowIfNull(writeAction);

        using NativeLeaseScope fileScope = new(ownerLabel, LifetimeDomain.Request);
        FileAccess openedFile = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (openedFile == null)
        {
            Error openError = FileAccess.GetOpenError();
            throw new IOException(
                $"Failed to open battle simulation output file '{path}': {openError}."
            );
        }

        try
        {
            FileAccess file = fileScope.Own(openedFile, $"open:{path}");
            writeAction(file);
            file.Flush();
            Error writeError = file.GetError();
            if (writeError != Error.Ok)
            {
                throw new IOException(
                    $"Failed to write battle simulation output file '{path}': {writeError}."
                );
            }
        }
        finally
        {
            openedFile.Close();
        }
    }
}

internal sealed class BattleSimReportFileWriter
{
    private const string DefaultReportDirectory = "user://simulation_reports";

    private readonly string _reportDirectory;
    private readonly Func<long> _timestampSecondsProvider;
    private readonly IBattleSimOutputFileSink _fileSink;
    private readonly BattleSimTraceSummaryBuilder _traceSummaryBuilder = new();

    internal BattleSimReportFileWriter()
        : this(
            DefaultReportDirectory,
            static () => (long)Time.GetUnixTimeFromSystem(),
            new GodotBattleSimOutputFileSink()
        )
    {
    }

    internal BattleSimReportFileWriter(
        string reportDirectory,
        Func<long> timestampSecondsProvider,
        IBattleSimOutputFileSink fileSink
    )
    {
        if (string.IsNullOrWhiteSpace(reportDirectory))
            throw new ArgumentException("Report directory cannot be empty.", nameof(reportDirectory));
        _reportDirectory = reportDirectory.TrimEnd('/', '\\');
        _timestampSecondsProvider = timestampSecondsProvider
            ?? throw new ArgumentNullException(nameof(timestampSecondsProvider));
        _fileSink = fileSink ?? throw new ArgumentNullException(nameof(fileSink));
    }

    internal BattleSimOutputFiles Write(
        BattleSimScenarioDefinition scenarioDefinition,
        BattleSimScenarioReport report
    )
    {
        ArgumentNullException.ThrowIfNull(scenarioDefinition);
        ArgumentNullException.ThrowIfNull(report);

        string scenarioKey =
            scenarioDefinition.ScenarioId != ""
                ? scenarioDefinition.ScenarioId.ToString()
                : "battle_sim";
        string reportDirectory = $"{_reportDirectory}/{scenarioKey}";
        Error ensureDirectoryError = DirAccess.MakeDirRecursiveAbsolute(
            ProjectSettings.GlobalizePath(reportDirectory)
        );
        if (ensureDirectoryError != Error.Ok)
        {
            throw new IOException(
                $"Failed to create battle simulation report directory '{reportDirectory}': {ensureDirectoryError}."
            );
        }

        bool hasTraces = _traceSummaryBuilder.HasTraces(report);
        BattleSimOutputFiles outputFiles = BuildUniqueOutputFiles(
            reportDirectory,
            scenarioKey,
            hasTraces
        );
        BattleSimOutputFiles previousOutputFiles = report.OutputFiles;
        report.OutputFiles = outputFiles;

        try
        {
            WriteTurnTraceJsonl(report, scenarioKey, outputFiles.TurnTraceJsonl);
            if (hasTraces)
                WriteTraceSummaryJson(report, outputFiles);
            WriteReportJson(report, outputFiles.ReportJson);
            return outputFiles;
        }
        catch
        {
            report.OutputFiles = previousOutputFiles ?? new BattleSimOutputFiles();
            CleanupIncompleteArtifacts(outputFiles);
            throw;
        }
    }

    private BattleSimOutputFiles BuildUniqueOutputFiles(
        string reportDirectory,
        string scenarioKey,
        bool hasTraces
    )
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            string batchId = $"{_timestampSecondsProvider()}_{Guid.NewGuid():N}";
            string stem = $"{reportDirectory}/{scenarioKey}_{batchId}";
            var outputFiles = new BattleSimOutputFiles
            {
                ReportJson = $"{stem}_report.json",
                TurnTraceJsonl = $"{stem}_turn_traces.jsonl",
                TraceSummaryJson = hasTraces ? $"{stem}_trace_summary.json" : "",
            };
            if (!AnyArtifactExists(outputFiles))
                return outputFiles;
        }

        throw new IOException(
            $"Could not reserve a unique battle simulation output name for scenario '{scenarioKey}'."
        );
    }

    private void WriteReportJson(BattleSimScenarioReport report, string reportPath)
    {
        _fileSink.Write(
            reportPath,
            "battle-sim-report-file",
            reportFile =>
            {
                using GodotProjectionLease<GDictionary> reportLease =
                    BattleSimFilePayloadProjection.BuildReportLease(report);
                if (!reportFile.StoreString(Json.Stringify(reportLease.Value, "\t")))
                {
                    throw new IOException(
                        $"Failed to write battle simulation report JSON '{reportPath}': {reportFile.GetError()}."
                    );
                }
            }
        );
        EnsureArtifactExists(reportPath);
    }

    private void WriteTurnTraceJsonl(
        BattleSimScenarioReport report,
        string scenarioKey,
        string tracePath
    )
    {
        _fileSink.Write(
            tracePath,
            "battle-sim-trace-file",
            traceFile =>
            {
                foreach (BattleSimProfileReportEntry profileEntry in report.ProfileEntries)
                {
                    if (profileEntry == null)
                        continue;
                    string profileId = profileEntry.Profile?.ProfileId.ToString() ?? "";
                    foreach (BattleSimRunReport runEntry in profileEntry.Runs)
                    {
                        if (runEntry?.AiTurnTraces == null)
                            continue;
                        foreach (BattleAiTurnTraceProjection traceEntry in runEntry.AiTurnTraces)
                        {
                            if (traceEntry == null)
                                continue;
                            using GodotProjectionLease<GDictionary> traceLease =
                                BattleSimFilePayloadProjection.BuildFlattenedTraceLease(
                                    traceEntry,
                                    scenarioKey,
                                    profileId,
                                    runEntry
                                );
                            if (!traceFile.StoreLine(Json.Stringify(traceLease.Value)))
                            {
                                throw new IOException(
                                    $"Failed to write battle simulation trace JSONL '{tracePath}': {traceFile.GetError()}."
                                );
                            }
                        }
                    }
                }
            }
        );
        EnsureArtifactExists(tracePath);
    }

    private void WriteTraceSummaryJson(
        BattleSimScenarioReport report,
        BattleSimOutputFiles outputFiles
    )
    {
        _fileSink.Write(
            outputFiles.TraceSummaryJson,
            "battle-sim-trace-summary-file",
            summaryFile =>
            {
                using GodotProjectionLease<GDictionary> traceSummaryLease =
                    _traceSummaryBuilder.BuildFileLease(report, outputFiles.ReportJson);
                if (!summaryFile.StoreString(Json.Stringify(traceSummaryLease.Value, "\t")))
                {
                    throw new IOException(
                        $"Failed to write battle simulation trace summary '{outputFiles.TraceSummaryJson}': {summaryFile.GetError()}."
                    );
                }
            }
        );
        EnsureArtifactExists(outputFiles.TraceSummaryJson);
    }

    private static bool AnyArtifactExists(BattleSimOutputFiles outputFiles)
    {
        return FileAccess.FileExists(outputFiles.ReportJson)
            || FileAccess.FileExists(outputFiles.TurnTraceJsonl)
            || (
                !string.IsNullOrEmpty(outputFiles.TraceSummaryJson)
                && FileAccess.FileExists(outputFiles.TraceSummaryJson)
            );
    }

    private static void EnsureArtifactExists(string path)
    {
        if (!FileAccess.FileExists(path))
            throw new IOException($"Battle simulation output file was not created: '{path}'.");
    }

    private static void CleanupIncompleteArtifacts(BattleSimOutputFiles outputFiles)
    {
        if (outputFiles == null)
            return;
        RemoveIncompleteArtifact(outputFiles.ReportJson);
        RemoveIncompleteArtifact(outputFiles.TurnTraceJsonl);
        RemoveIncompleteArtifact(outputFiles.TraceSummaryJson);
    }

    private static void RemoveIncompleteArtifact(string path)
    {
        if (string.IsNullOrEmpty(path) || !FileAccess.FileExists(path))
            return;
        Error removeError = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        if (removeError != Error.Ok)
        {
            GameLog.Warning(
                $"Failed to remove incomplete battle simulation output '{path}': {removeError}.",
                "battlesim.report.cleanup_failed",
                "battlesim"
            );
        }
    }
}
