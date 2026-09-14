using System;
using Godot;
using IOException = System.IO.IOException;

internal enum BattleSimAnalysisArtifactKind
{
    Main,
    Trace,
    Profile,
}

internal readonly record struct BattleSimAnalysisArtifactStatus(
    bool Required,
    bool Succeeded
)
{
    internal static BattleSimAnalysisArtifactStatus NotRequired => new(false, true);

    internal static BattleSimAnalysisArtifactStatus Success => new(true, true);

    internal static BattleSimAnalysisArtifactStatus Failure => new(true, false);
}

internal readonly record struct BattleSimAnalysisArtifactWriteResult(
    BattleSimAnalysisArtifactKind Kind,
    BattleSimAnalysisArtifactStatus Status,
    string Path,
    string ErrorMessage
)
{
    internal static BattleSimAnalysisArtifactWriteResult Succeeded(
        BattleSimAnalysisArtifactKind kind,
        string path
    ) => new(kind, BattleSimAnalysisArtifactStatus.Success, path ?? "", "");

    internal static BattleSimAnalysisArtifactWriteResult Failed(
        BattleSimAnalysisArtifactKind kind,
        string path,
        string errorMessage
    ) =>
        new(
            kind,
            BattleSimAnalysisArtifactStatus.Failure,
            path ?? "",
            errorMessage ?? ""
        );
}

internal readonly record struct BattleSimAnalysisCompletionStatus(
    bool BatchIsComplete,
    BattleSimAnalysisArtifactStatus MainArtifact,
    BattleSimAnalysisArtifactStatus TraceArtifact,
    BattleSimAnalysisArtifactStatus ProfileArtifact
)
{
    internal bool AllRequiredArtifactsSucceeded =>
        IsSatisfied(MainArtifact)
        && IsSatisfied(TraceArtifact)
        && IsSatisfied(ProfileArtifact);

    private static bool IsSatisfied(BattleSimAnalysisArtifactStatus status) =>
        !status.Required || status.Succeeded;
}

internal static class BattleSimAnalysisExitCodePolicy
{
    internal const int Success = 0;
    internal const int OutputFailure = 1;
    internal const int IncompleteBatch = 2;

    internal static int Resolve(BattleSimAnalysisCompletionStatus status)
    {
        if (!status.AllRequiredArtifactsSucceeded)
            return OutputFailure;
        return status.BatchIsComplete ? Success : IncompleteBatch;
    }
}

internal sealed class BattleSimAnalysisArtifactFileWriter
{
    private readonly IBattleSimOutputFileSink _fileSink;

    internal BattleSimAnalysisArtifactFileWriter()
        : this(new GodotBattleSimOutputFileSink())
    {
    }

    internal BattleSimAnalysisArtifactFileWriter(IBattleSimOutputFileSink fileSink)
    {
        _fileSink = fileSink ?? throw new ArgumentNullException(nameof(fileSink));
    }

    internal BattleSimAnalysisArtifactWriteResult WriteText(
        BattleSimAnalysisArtifactKind kind,
        string path,
        string ownerLabel,
        string content
    )
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return BattleSimAnalysisArtifactWriteResult.Failed(
                kind,
                path,
                "Output path cannot be empty."
            );
        }

        string resolvedPath =
            path.StartsWith("res://", StringComparison.Ordinal)
            || path.StartsWith("user://", StringComparison.Ordinal)
                ? ProjectSettings.GlobalizePath(path)
                : path;

        try
        {
            EnsureDirectory(resolvedPath);
            _fileSink.Write(
                resolvedPath,
                ownerLabel,
                file =>
                {
                    if (!file.StoreString(content ?? ""))
                    {
                        throw new IOException(
                            $"Failed to store battle simulation analysis artifact '{path}': {file.GetError()}."
                        );
                    }
                }
            );
            if (!FileAccess.FileExists(resolvedPath))
            {
                throw new IOException(
                    $"Battle simulation analysis artifact was not created: '{path}'."
                );
            }
            return BattleSimAnalysisArtifactWriteResult.Succeeded(kind, path);
        }
        catch (Exception exception)
        {
            return BattleSimAnalysisArtifactWriteResult.Failed(
                kind,
                path,
                exception.Message
            );
        }
    }

    private static void EnsureDirectory(string resolvedPath)
    {
        string directory = resolvedPath.GetBaseDir();
        if (string.IsNullOrEmpty(directory))
            return;
        Error directoryError = DirAccess.MakeDirRecursiveAbsolute(directory);
        if (directoryError != Error.Ok)
        {
            throw new IOException(
                $"Failed to create battle simulation analysis output directory '{directory}': {directoryError}."
            );
        }
    }
}
