using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public static class FileIOCoordinator
{
    public static int WriteCompressedVariantAtomically(
        string virtual_path,
        GDictionary payload,
        int compression_mode,
        string error_event_prefix,
        string label,
        Action<string, string, string> error_sink = null
    )
    {
        string tempPath = $"{virtual_path}.tmp";
        int cleanupTempError = RemoveFileIfExists(tempPath);
        if (cleanupTempError != (int)Error.Ok)
        {
            return cleanupTempError;
        }

        FileAccess file = FileAccess.OpenCompressed(
            tempPath,
            FileAccess.ModeFlags.Write,
            (FileAccess.CompressionMode)compression_mode
        );
        if (file == null)
        {
            Error openError = FileAccess.GetOpenError();
            PushError(
                error_sink,
                $"{error_event_prefix}.open_failed",
                $"Failed to open {label} file {tempPath}. Error: {(int)openError}",
                Json.Stringify(new GDictionary { ["path"] = tempPath, ["open_error"] = (int)openError })
            );
            return (int)openError;
        }

        try
        {
            file.StoreVar(payload, false);
            Error writeError = file.GetError();
            file.Close();
            if (writeError != Error.Ok)
            {
                RemoveFileIfExists(tempPath);
                PushError(
                    error_sink,
                    $"{error_event_prefix}.write_failed",
                    $"Failed to write {label} file {tempPath}. Error: {(int)writeError}",
                    Json.Stringify(new GDictionary { ["path"] = tempPath, ["write_error"] = (int)writeError })
                );
                return (int)writeError;
            }

            return ReplaceFileAtomically(
                tempPath,
                virtual_path,
                error_event_prefix,
                label,
                error_sink
            );
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(file);
        }
    }

    public static int ReplaceFileAtomically(
        string source_path,
        string target_path,
        string error_event_prefix,
        string label,
        Action<string, string, string> error_sink = null
    )
    {
        string backupPath = $"{target_path}.bak";
        int cleanupBackupError = RemoveFileIfExists(backupPath);
        if (cleanupBackupError != (int)Error.Ok)
        {
            RemoveFileIfExists(source_path);
            return cleanupBackupError;
        }

        bool hadExistingTarget = FileAccess.FileExists(target_path);
        if (hadExistingTarget)
        {
            int backupError = RenameFile(target_path, backupPath);
            if (backupError != (int)Error.Ok)
            {
                RemoveFileIfExists(source_path);
                PushError(
                    error_sink,
                    $"{error_event_prefix}.backup_failed",
                    $"Failed to prepare existing {label} file {target_path} for replacement. Error: {backupError}",
                    Json.Stringify(new GDictionary
                    {
                        ["target_path"] = target_path,
                        ["backup_path"] = backupPath,
                        ["backup_error"] = backupError,
                    })
                );
                return backupError;
            }
        }

        int replaceError = RenameFile(source_path, target_path);
        if (replaceError != (int)Error.Ok)
        {
            RemoveFileIfExists(source_path);
            if (hadExistingTarget)
            {
                RenameFile(backupPath, target_path);
            }
            PushError(
                error_sink,
                $"{error_event_prefix}.replace_failed",
                $"Failed to replace {label} file {target_path}. Error: {replaceError}",
                Json.Stringify(new GDictionary
                {
                    ["source_path"] = source_path,
                    ["target_path"] = target_path,
                    ["replace_error"] = replaceError,
                })
            );
            return replaceError;
        }

        if (hadExistingTarget)
        {
            int removeBackupError = RemoveFileIfExists(backupPath);
            if (removeBackupError != (int)Error.Ok)
            {
                GameLog.Warning(
                    $"FileIOCoordinator: replaced {label} file but failed to remove backup {backupPath}. Error: {removeBackupError}",
                    "io.backup_remove_failed",
                    "io"
                );
            }
        }
        return (int)Error.Ok;
    }

    public static int RecoverReplaceTarget(
        string target_path,
        int compression_mode,
        string error_event_prefix,
        string label,
        Action<string, string, string> error_sink = null
    )
    {
        string tempPath = $"{target_path}.tmp";
        RemoveFileIfExists(tempPath);

        string backupPath = $"{target_path}.bak";
        if (FileAccess.FileExists(target_path))
        {
            if (FileAccess.FileExists(backupPath))
            {
                int cleanupBackupError = RemoveFileIfExists(backupPath);
                if (cleanupBackupError != (int)Error.Ok)
                {
                    GameLog.Warning(
                        $"FileIOCoordinator: found valid {label} target but failed to remove stale backup {backupPath}. Error: {cleanupBackupError}",
                        "io.stale_backup_remove_failed",
                        "io"
                    );
                }
            }
            return (int)Error.Ok;
        }

        if (!FileAccess.FileExists(backupPath))
        {
            return (int)Error.DoesNotExist;
        }

        if (!IsCompressedVariantFileReadable(backupPath, compression_mode))
        {
            PushError(
                error_sink,
                $"{error_event_prefix}.backup_invalid",
                $"Failed to recover {label} file {target_path} because backup {backupPath} is invalid.",
                Json.Stringify(new GDictionary { ["target_path"] = target_path, ["backup_path"] = backupPath })
            );
            return (int)Error.InvalidData;
        }

        int restoreError = RenameFile(backupPath, target_path);
        if (restoreError != (int)Error.Ok)
        {
            PushError(
                error_sink,
                $"{error_event_prefix}.backup_restore_failed",
                $"Failed to restore {label} file {target_path} from backup {backupPath}. Error: {restoreError}",
                Json.Stringify(new GDictionary
                {
                    ["target_path"] = target_path,
                    ["backup_path"] = backupPath,
                    ["restore_error"] = restoreError,
                })
            );
            return restoreError;
        }
        return (int)Error.Ok;
    }

    public static bool IsCompressedVariantFileReadable(
        string virtual_path,
        int compression_mode
    )
    {
        if (!FileAccess.FileExists(virtual_path))
        {
            return false;
        }

        FileAccess file = FileAccess.OpenCompressed(
            virtual_path,
            FileAccess.ModeFlags.Read,
            (FileAccess.CompressionMode)compression_mode
        );
        if (file == null)
        {
            return false;
        }
        try
        {
            if ((long)file.GetLength() < 8)
            {
                file.Close();
                return false;
            }

            using Variant readValue = file.GetVar(false);
            Error readError = file.GetError();
            file.Close();
            return readError == Error.Ok;
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(file);
        }
    }

    public static int RenameFile(string from_virtual_path, string to_virtual_path)
    {
        return (int)
            DirAccess.RenameAbsolute(
                ProjectSettings.GlobalizePath(from_virtual_path),
                ProjectSettings.GlobalizePath(to_virtual_path)
            );
    }

    public static int RemoveFileIfExists(string virtual_path)
    {
        if (!FileAccess.FileExists(virtual_path))
        {
            return (int)Error.Ok;
        }
        return (int)DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(virtual_path));
    }

    public static int RemoveDirectoryRecursive(
        string virtual_path,
        Action<string, string, string> error_sink = null
    )
    {
        string absolutePath = ProjectSettings.GlobalizePath(virtual_path);
        if (!DirAccess.DirExistsAbsolute(absolutePath))
        {
            return (int)Error.Ok;
        }

        DirAccess dir = DirAccess.Open(virtual_path);
        if (dir == null)
        {
            Error openError = DirAccess.GetOpenError();
            PushError(
                error_sink,
                "session.cleanup.open_directory_failed",
                $"Failed to open directory {virtual_path} for cleanup. Error: {(int)openError}",
                Json.Stringify(new GDictionary { ["virtual_path"] = virtual_path, ["open_error"] = (int)openError })
            );
            return (int)openError;
        }

        bool listingStarted = false;
        try
        {
            Error listError = dir.ListDirBegin();
            if (listError != Error.Ok)
            {
                PushError(
                    error_sink,
                    "session.cleanup.list_directory_failed",
                    $"Failed to list directory {virtual_path} for cleanup. Error: {(int)listError}",
                    Json.Stringify(new GDictionary { ["virtual_path"] = virtual_path, ["list_error"] = (int)listError })
                );
                return (int)listError;
            }
            listingStarted = true;

            while (true)
            {
                string name = dir.GetNext();
                if (string.IsNullOrEmpty(name))
                {
                    break;
                }
                if (name == "." || name == "..")
                {
                    continue;
                }

                string childVirtualPath = $"{virtual_path}/{name}";
                if (dir.CurrentIsDir())
                {
                    int nestedError = RemoveDirectoryRecursive(childVirtualPath, error_sink);
                    if (nestedError != (int)Error.Ok)
                    {
                        return nestedError;
                    }
                    continue;
                }

                Error removeFileError = DirAccess.RemoveAbsolute(
                    ProjectSettings.GlobalizePath(childVirtualPath)
                );
                if (removeFileError != Error.Ok)
                {
                    return (int)removeFileError;
                }
            }

            return (int)DirAccess.RemoveAbsolute(absolutePath);
        }
        finally
        {
            if (listingStarted)
                dir.ListDirEnd();
            GodotObjectLifecycle.DisposeGodotObject(dir);
        }
    }

    private static void PushError(
        Action<string, string, string> errorSink,
        string eventId,
        string message,
        string context
    )
    {
        errorSink?.Invoke(eventId, message, context);
    }
}
