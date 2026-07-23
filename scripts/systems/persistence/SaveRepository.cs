using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class SaveRepository
{
    private readonly SaveSerializer _serializer;
    private readonly string _saveDirectory;
    private readonly int _compressionMode;
    private readonly Action<string, string, string> _errorSink;
    private readonly Func<bool> _shouldFailPayloadWrite;

    internal SaveRepository(
        SaveSerializer serializer,
        string saveDirectory,
        int compressionMode,
        Action<string, string, string> errorSink,
        Func<bool> shouldFailPayloadWrite
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);

        _serializer = serializer;
        _saveDirectory = saveDirectory;
        _compressionMode = compressionMode;
        _errorSink = errorSink;
        _shouldFailPayloadWrite = shouldFailPayloadWrite;
    }

    internal int ReadSavePayload(
        string savePath,
        out Dictionary<string, object> payload,
        bool emitErrors = true
    )
    {
        payload = new Dictionary<string, object>(StringComparer.Ordinal);
        int recoveryError = FileIOCoordinator.RecoverReplaceTarget(
            savePath,
            _compressionMode,
            "session.save.read",
            "save",
            PushError
        );
        if (recoveryError != (int)Error.Ok && recoveryError != (int)Error.DoesNotExist)
            return recoveryError;
        if (!FileAccess.FileExists(savePath))
        {
            if (emitErrors)
                PushError(
                    "session.save.read.missing",
                    $"GameSession could not find persisted save {savePath}. Error: {(int)Error.DoesNotExist}"
                );
            return (int)Error.DoesNotExist;
        }

        using NativeLeaseScope requestScope = new(
            "save-repository-read",
            LifetimeDomain.Request
        );
        FileAccess openedFile = FileAccess.OpenCompressed(
            savePath,
            FileAccess.ModeFlags.Read,
            (FileAccess.CompressionMode)_compressionMode
        );
        if (openedFile == null)
        {
            Error openError = FileAccess.GetOpenError();
            Error readError =
                openError is Error.FileNotFound or Error.DoesNotExist
                || !FileAccess.FileExists(savePath)
                    ? Error.DoesNotExist
                    : openError == Error.Ok
                        ? Error.CantOpen
                        : openError;
            if (emitErrors)
                PushError(
                    "session.save.read.open_failed",
                    $"Failed to open persisted save {savePath}. Error: {(int)readError}"
                );
            return (int)readError;
        }
        FileAccess saveFile = requestScope.Own(openedFile, $"open:{savePath}");

        try
        {
            int saveSize = (int)saveFile.GetLength();
            if (saveSize < 8)
            {
                saveFile.Close();
                return (int)Error.InvalidData;
            }

            using Variant rawPayload = saveFile.GetVar(false);
            bool restored = RuntimePlainPayload.TryRestoreSaveVariantDictionary(
                rawPayload,
                $"SaveRepository:{savePath}",
                out Dictionary<string, object> restoredPayload
            );
            saveFile.Close();
            if (!restored)
                return (int)Error.InvalidData;

            payload = restoredPayload;
            return (int)Error.Ok;
        }
        finally
        {
            saveFile.Close();
        }
    }

    internal int EnsureSaveDirectory()
    {
        return (int)
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(_saveDirectory));
    }

    internal string BuildSaveFilePath(string saveId)
    {
        if (_serializer == null || !_serializer.IsValidSaveIdToken(saveId))
            return "";
        return $"{_saveDirectory}/{saveId}.dat";
    }

    internal int WriteSavePayloadAtomically(
        string savePath,
        GodotProjectionLease<GDictionary> payload
    )
    {
        if (_shouldFailPayloadWrite?.Invoke() ?? false)
            return (int)Error.CantCreate;
        return WriteCompressedVariantAtomically(
            savePath,
            payload,
            "session.save.persist",
            "save"
        );
    }

    internal int WriteCompressedVariantAtomically(
        string virtualPath,
        GodotProjectionLease<GDictionary> payload,
        string errorEventPrefix,
        string label
    )
    {
        return FileIOCoordinator.WriteCompressedVariantAtomically(
            virtualPath,
            payload,
            _compressionMode,
            errorEventPrefix,
            label,
            PushError
        );
    }

    internal int ReplaceFileAtomically(
        string sourcePath,
        string targetPath,
        string errorEventPrefix,
        string label
    )
    {
        return FileIOCoordinator.ReplaceFileAtomically(
            sourcePath,
            targetPath,
            errorEventPrefix,
            label,
            PushError
        );
    }

    internal int RenameFile(string fromVirtualPath, string toVirtualPath) =>
        FileIOCoordinator.RenameFile(fromVirtualPath, toVirtualPath);

    internal int RemoveFileIfExists(string virtualPath) =>
        FileIOCoordinator.RemoveFileIfExists(virtualPath);

    private void PushError(string eventId, string message) =>
        PushError(eventId, message, "");

    private void PushError(string eventId, string message, string context)
    {
        _errorSink?.Invoke(eventId, message, context ?? "");
    }
}
