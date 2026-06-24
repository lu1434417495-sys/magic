using System;
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
        _serializer = serializer;
        _saveDirectory = saveDirectory ?? "";
        _compressionMode = compressionMode;
        _errorSink = errorSink;
        _shouldFailPayloadWrite = shouldFailPayloadWrite;
    }

    internal GDictionary ReadSavePayload(string savePath, bool emitErrors = true)
    {
        int recoveryError = FileIOCoordinator.RecoverReplaceTarget(
            savePath,
            _compressionMode,
            "session.save.read",
            "save",
            PushError
        );
        if (recoveryError != (int)Error.Ok && recoveryError != (int)Error.DoesNotExist)
            return new GDictionary { ["error"] = recoveryError };
        if (!FileAccess.FileExists(savePath))
        {
            if (emitErrors)
            {
                throw new InvalidOperationException(
                    $"GameSession could not find persisted save {savePath}."
                );
            }
            return new GDictionary { ["error"] = (int)Error.DoesNotExist };
        }

        FileAccess saveFile = FileAccess.OpenCompressed(
            savePath,
            FileAccess.ModeFlags.Read,
            (FileAccess.CompressionMode)_compressionMode
        );
        if (saveFile == null)
        {
            Error openError = FileAccess.GetOpenError();
            if (emitErrors)
            {
                throw new InvalidOperationException(
                    $"Failed to open persisted save {savePath}. Error: {(int)openError}"
                );
            }
            return new GDictionary { ["error"] = (int)openError };
        }

        try
        {
            int saveSize = (int)saveFile.GetLength();
            if (saveSize < 8)
            {
                saveFile.Close();
                return new GDictionary { ["error"] = (int)Error.InvalidData };
            }

            using Variant rawPayload = saveFile.GetVar(false);
            saveFile.Close();
            if (rawPayload.VariantType != Variant.Type.Dictionary)
                return new GDictionary { ["error"] = (int)Error.InvalidData };

            return new GDictionary
            {
                ["error"] = (int)Error.Ok,
                ["payload"] = rawPayload.AsGodotDictionary().Duplicate(true),
            };
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(saveFile);
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

    internal int WriteSavePayloadAtomically(string savePath, GDictionary payload)
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
        GDictionary payload,
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
