#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;

internal sealed record ContentJsonOutputFile(string FileName, string Utf8Json);

internal enum ContentJsonDirectoryMovePhase
{
    ExistingTargetToBackup,
    StagingToTarget,
    RollbackBackupToTarget,
}

/// <summary>
/// Publishes a complete generated JSON directory through a same-volume sibling staging
/// directory. Replacing an existing target is recoverable on an ordinary move failure, but is
/// intentionally not described as crash-atomic on Windows.
/// </summary>
internal sealed class ContentJsonTransactionalDirectoryPublisher
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true
    );

    private readonly Action<ContentJsonDirectoryMovePhase, string, string>? _beforeMoveForTests;
    private readonly Action<string>? _beforeBackupDeleteForTests;
    private readonly Action<string>? _validateExistingTargetOwnership;

    internal ContentJsonTransactionalDirectoryPublisher(
        Action<ContentJsonDirectoryMovePhase, string, string>? beforeMoveForTests = null,
        Action<string>? beforeBackupDeleteForTests = null,
        Action<string>? validateExistingTargetOwnership = null
    )
    {
        _beforeMoveForTests = beforeMoveForTests;
        _beforeBackupDeleteForTests = beforeBackupDeleteForTests;
        _validateExistingTargetOwnership = validateExistingTargetOwnership;
    }

    internal void Publish(string targetDirectory, IReadOnlyList<ContentJsonOutputFile> files)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new ArgumentException("Content JSON target directory is required.", nameof(targetDirectory));
        ArgumentNullException.ThrowIfNull(files);

        string target = Path.GetFullPath(targetDirectory);
        string? parent = Path.GetDirectoryName(target);
        string targetName = Path.GetFileName(target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(targetName))
            throw new ArgumentException("Content JSON target must have a parent directory.", nameof(targetDirectory));
        IReadOnlyList<EncodedOutputFile> encodedFiles = ValidateAndEncode(files);
        Directory.CreateDirectory(parent);
        string publicationLockPath = Path.Combine(parent, $".{targetName}.publish.lock");
        FileStream publicationLock = AcquirePublicationLock(target, publicationLockPath);
        Exception? publicationFailure = null;
        try
        {
            PublishLocked(target, parent, targetName, encodedFiles);
        }
        catch (Exception exception)
        {
            publicationFailure = exception;
        }
        finally
        {
            publicationLock.Dispose();
        }

        Exception? lockCleanupFailure = null;
        try
        {
            File.Delete(publicationLockPath);
        }
        catch (Exception exception)
        {
            lockCleanupFailure = new IOException(
                $"Content JSON publication lock '{publicationLockPath}' could not be removed; "
                    + "review it before retrying.",
                exception
            );
        }

        if (publicationFailure != null && lockCleanupFailure != null)
        {
            throw new IOException(
                $"Content JSON publication for '{target}' failed and its publication lock "
                    + "could not be removed.",
                new AggregateException(publicationFailure, lockCleanupFailure)
            );
        }
        if (lockCleanupFailure != null)
            throw lockCleanupFailure;
        if (publicationFailure != null)
            ExceptionDispatchInfo.Capture(publicationFailure).Throw();
    }

    private void PublishLocked(
        string target,
        string parent,
        string targetName,
        IReadOnlyList<EncodedOutputFile> encodedFiles
    )
    {
        if (File.Exists(target))
            throw new IOException($"Content JSON target '{target}' is a file, not a directory.");
        EnsureNoStaleTransactionArtifacts(parent, targetName);
        if (Directory.Exists(target))
        {
            ValidateReplaceableTarget(target, encodedFiles);
            _validateExistingTargetOwnership?.Invoke(target);
        }

        string nonce = Guid.NewGuid().ToString("N");
        string staging = Path.Combine(parent, $".{targetName}.staging-{nonce}");
        string backup = Path.Combine(parent, $".{targetName}.backup-{nonce}");
        bool movedTargetToBackup = false;
        bool promotedStaging = false;

        try
        {
            WriteAndVerifyStaging(staging, encodedFiles);

            if (Directory.Exists(target))
            {
                MoveDirectory(
                    ContentJsonDirectoryMovePhase.ExistingTargetToBackup,
                    target,
                    backup
                );
                movedTargetToBackup = true;
            }

            MoveDirectory(ContentJsonDirectoryMovePhase.StagingToTarget, staging, target);
            promotedStaging = true;

            if (movedTargetToBackup)
                DeletePublishedBackupStrict(target, backup);
        }
        catch (Exception publicationFailure)
        {
            Exception? rollbackFailure = null;
            if (movedTargetToBackup && !promotedStaging)
            {
                try
                {
                    if (Directory.Exists(target))
                        Directory.Delete(target, recursive: true);
                    if (Directory.Exists(backup))
                    {
                        MoveDirectory(
                            ContentJsonDirectoryMovePhase.RollbackBackupToTarget,
                            backup,
                            target
                        );
                    }
                }
                catch (Exception exception)
                {
                    rollbackFailure = exception;
                }
            }

            TryDeleteDirectory(staging);
            if (rollbackFailure == null)
                throw;

            throw new IOException(
                $"Content JSON publication failed and rollback also failed. Publication: "
                    + $"{publicationFailure.Message} Rollback: {rollbackFailure.Message}",
                new AggregateException(publicationFailure, rollbackFailure)
            );
        }
        finally
        {
            TryDeleteDirectory(staging);
        }
    }

    private static FileStream AcquirePublicationLock(string target, string lockPath)
    {
        try
        {
            return new FileStream(
                lockPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.WriteThrough
            );
        }
        catch (IOException exception)
        {
            throw new IOException(
                $"Content JSON target '{target}' has active or stale publication lock "
                    + $"'{lockPath}'; review it before retrying.",
                exception
            );
        }
    }

    private static IReadOnlyList<EncodedOutputFile> ValidateAndEncode(
        IReadOnlyList<ContentJsonOutputFile> files
    )
    {
        if (files.Count == 0)
            throw new ArgumentException("Content JSON publication requires at least one file.", nameof(files));

        var encoded = new List<EncodedOutputFile>(files.Count);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (ContentJsonOutputFile file in files)
        {
            if (file == null)
                throw new ArgumentException("Content JSON output files cannot contain null.", nameof(files));
            string fileName = file.FileName ?? "";
            if (
                string.IsNullOrWhiteSpace(fileName)
                || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
                || fileName.IndexOfAny(new[] { '/', '\\' }) >= 0
                || !fileName.EndsWith(".json", StringComparison.Ordinal)
            )
            {
                throw new ArgumentException(
                    $"Content JSON output file '{fileName}' must be a canonical .json leaf name.",
                    nameof(files)
                );
            }
            if (!names.Add(fileName))
                throw new ArgumentException($"Duplicate content JSON output file '{fileName}'.", nameof(files));

            string normalized = NormalizeLf(file.Utf8Json ?? "");
            byte[] bytes = StrictUtf8.GetBytes(normalized);
            encoded.Add(new EncodedOutputFile(fileName, bytes));
        }

        return new ReadOnlyCollection<EncodedOutputFile>(encoded);
    }

    private static string NormalizeLf(string content)
    {
        string normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
    }

    private static void WriteAndVerifyStaging(
        string stagingDirectory,
        IReadOnlyList<EncodedOutputFile> files
    )
    {
        Directory.CreateDirectory(stagingDirectory);
        foreach (EncodedOutputFile file in files)
        {
            string path = Path.Combine(stagingDirectory, file.FileName);
            using (
                var stream = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.SequentialScan
                )
            )
            {
                stream.Write(file.Bytes, 0, file.Bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            byte[] persisted = File.ReadAllBytes(path);
            if (!persisted.AsSpan().SequenceEqual(file.Bytes))
                throw new IOException($"Content JSON staging verification failed for '{file.FileName}'.");
        }

        string[] actualNames = Directory.GetFiles(stagingDirectory)
            .Select(static path => Path.GetFileName(path)!)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expectedNames = files.Select(static file => file.FileName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actualNames.SequenceEqual(expectedNames, StringComparer.Ordinal))
            throw new IOException("Content JSON staging directory contains an unexpected file set.");
    }

    private static void EnsureNoStaleTransactionArtifacts(string parent, string targetName)
    {
        string[] stale = Directory
            .GetFileSystemEntries(parent, $".{targetName}.staging-*")
            .Concat(Directory.GetFileSystemEntries(parent, $".{targetName}.backup-*"))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        if (stale.Length == 0)
            return;

        throw new IOException(
            $"Content JSON target '{Path.Combine(parent, targetName)}' has stale transaction "
                + $"artifact '{stale[0]}'; review it before retrying."
        );
    }

    private static void ValidateReplaceableTarget(
        string target,
        IReadOnlyList<EncodedOutputFile> expectedFiles
    )
    {
        if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Content JSON target '{target}' cannot be a reparse point.");

        string[] directories = Directory.GetDirectories(target);
        if (directories.Length > 0)
        {
            throw new IOException(
                $"Content JSON target '{target}' contains unexpected subdirectory "
                    + $"'{Path.GetFileName(directories.OrderBy(static path => path, StringComparer.Ordinal).First())}'."
            );
        }

        string[] actualNames = Directory.GetFiles(target)
            .Select(static path => Path.GetFileName(path)!)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        string[] expectedNames = expectedFiles.Select(static file => file.FileName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actualNames.SequenceEqual(expectedNames, StringComparer.Ordinal))
        {
            throw new IOException(
                $"Content JSON target '{target}' is not the exact expected generated file set."
            );
        }

        foreach (string path in Directory.GetFiles(target))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Content JSON target file '{path}' cannot be a reparse point.");
        }
    }

    private void MoveDirectory(
        ContentJsonDirectoryMovePhase phase,
        string source,
        string target
    )
    {
        _beforeMoveForTests?.Invoke(phase, source, target);
        Directory.Move(source, target);
    }

    private void DeletePublishedBackupStrict(string target, string backup)
    {
        try
        {
            _beforeBackupDeleteForTests?.Invoke(backup);
            Directory.Delete(backup, recursive: true);
        }
        catch (Exception exception)
        {
            throw new IOException(
                $"Content JSON target '{target}' contains the new generation, but backup "
                    + $"'{backup}' could not be removed and requires manual review.",
                exception
            );
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Publication/rollback errors remain primary. A same-run orphan is uniquely named
            // and never mistaken for the published target.
        }
    }

    private sealed record EncodedOutputFile(string FileName, byte[] Bytes);
}
