using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

// Partial slice of GameSession — save directory/file atomic IO + save index/meta read/write/merge.
// Pure physical split: same class, no behavior change. See GameSession.cs.
public partial class GameSession
{

    public int EnsureSaveDirectory()
    {
        return EnsureSaveRepository().EnsureSaveDirectory();
    }

    public string BuildSaveFilePath(string save_id)
    {
        return EnsureSaveRepository().BuildSaveFilePath(save_id);
    }

    private int WriteSavePayloadAtomically(
        string save_path,
        GodotProjectionLease<GDictionary> payload
    )
    {
        return EnsureSaveRepository().WriteSavePayloadAtomically(save_path, payload);
    }

    public int ReplaceFileAtomically(
        string source_path,
        string target_path,
        string error_event_prefix,
        string label
    )
    {
        return EnsureSaveRepository().ReplaceFileAtomically(
            source_path,
            target_path,
            error_event_prefix,
            label
        );
    }

    public int RenameFile(string from_virtual_path, string to_virtual_path)
    {
        return EnsureSaveRepository().RenameFile(from_virtual_path, to_virtual_path);
    }

    public int RemoveFileIfExists(string virtual_path)
    {
        return EnsureSaveRepository().RemoveFileIfExists(virtual_path);
    }

    private List<Dictionary<string, object>> LoadSaveIndexEntriesPlain()
    {
        if (IsSaveIndexCacheCurrent())
            return CloneSaveIndexEntries(_saveIndexEntriesCache);

        bool shouldRewriteIndex = false;
        List<Dictionary<string, object>> entries = new();
        int indexRecoveryError = FileIOCoordinator.RecoverReplaceTarget(
            _persistenceOptions.SaveIndexPath,
            SaveFileCompressionMode,
            "session.save.index",
            "save index",
            PushSessionError
        );
        if (indexRecoveryError != (int)Error.Ok && indexRecoveryError != (int)Error.DoesNotExist)
            shouldRewriteIndex = true;
        if (!FileAccess.FileExists(_persistenceOptions.SaveIndexPath))
        {
            shouldRewriteIndex = true;
        }
        else
        {
            using NativeLeaseScope fileScope = new(
                "save-index-read",
                LifetimeDomain.Request
            );
            FileAccess openedIndexFile = FileAccess.OpenCompressed(
                _persistenceOptions.SaveIndexPath,
                FileAccess.ModeFlags.Read,
                (FileAccess.CompressionMode)SaveFileCompressionMode
            );
            if (openedIndexFile == null)
            {
                shouldRewriteIndex = true;
            }
            else
            {
                FileAccess indexFile = fileScope.Own(
                    openedIndexFile,
                    $"open:{_persistenceOptions.SaveIndexPath}"
                );
                try
                {
                    bool hasIndexPayload = TryReadSaveIndexPayload(
                        indexFile,
                        out Dictionary<string, object> plainPayload
                    );
                    indexFile.Close();
                    if (hasIndexPayload)
                    {
                        if (!TryReadPlainSaveIndexEntries(plainPayload, out entries))
                        {
                            shouldRewriteIndex = true;
                        }
                    }
                    else
                    {
                        shouldRewriteIndex = true;
                    }
                }
                finally
                {
                    indexFile.Close();
                }
            }
        }

        List<Dictionary<string, object>> rebuiltEntries =
            RebuildSaveIndexEntriesFromSaveFilesPlain();
        List<Dictionary<string, object>> mergedEntries = MergeSaveIndexEntriesPlain(
            entries,
            rebuiltEntries
        );
        if (shouldRewriteIndex || !SaveIndexEntriesMatch(entries, mergedEntries))
            WriteSaveIndexPlain(mergedEntries);
        else
            SetSaveIndexCache(mergedEntries);
        return CloneSaveIndexEntries(mergedEntries);
    }

    private List<Dictionary<string, object>> PeekSaveIndexEntriesPlain()
    {
        if (IsSaveIndexCacheCurrent())
            return CloneSaveIndexEntries(_saveIndexEntriesCache);
        if (!FileAccess.FileExists(_persistenceOptions.SaveIndexPath))
            return new List<Dictionary<string, object>>();

        using NativeLeaseScope fileScope = new(
            "save-index-peek",
            LifetimeDomain.Request
        );
        FileAccess openedIndexFile = FileAccess.OpenCompressed(
            _persistenceOptions.SaveIndexPath,
            FileAccess.ModeFlags.Read,
            (FileAccess.CompressionMode)SaveFileCompressionMode
        );
        if (openedIndexFile == null)
            return new List<Dictionary<string, object>>();
        FileAccess indexFile = fileScope.Own(
            openedIndexFile,
            $"open:{_persistenceOptions.SaveIndexPath}"
        );
        Dictionary<string, object> plainPayload;
        try
        {
            bool hasIndexPayload = TryReadSaveIndexPayload(indexFile, out plainPayload);
            indexFile.Close();
            if (!hasIndexPayload)
                return new List<Dictionary<string, object>>();
        }
        finally
        {
            indexFile.Close();
        }

        if (!TryReadPlainSaveIndexEntries(plainPayload, out List<Dictionary<string, object>> entries))
            return new List<Dictionary<string, object>>();
        SetSaveIndexCache(entries);
        return CloneSaveIndexEntries(entries);
    }

    private int WriteSaveIndexPlain(IReadOnlyList<Dictionary<string, object>> entries)
    {
        int ensureDirError = EnsureSaveDirectory();
        if (ensureDirError != (int)Error.Ok)
            return ensureDirError;

        List<Dictionary<string, object>> normalizedEntries =
            NormalizeSaveIndexEntriesPlain(entries);
        using GodotProjectionLease<GDictionary> payload =
            BuildSaveIndexPayloadLease(normalizedEntries);
        int writeError = EnsureSaveRepository().WriteCompressedVariantAtomically(
            _persistenceOptions.SaveIndexPath,
            payload,
            "session.save.index",
            "save index"
        );
        if (writeError != (int)Error.Ok)
            return writeError;
        SetSaveIndexCache(normalizedEntries);
        return (int)Error.Ok;
    }

    private bool TryReadSaveIndexPayload(
        FileAccess index_file,
        out Dictionary<string, object> payload
    )
    {
        return _save_serializer.TryReadSaveIndexPayloadPlain(index_file, out payload);
    }

    private bool TryReadPlainSaveIndexEntries(
        IReadOnlyDictionary<string, object> payload,
        out List<Dictionary<string, object>> entries
    )
    {
        entries = new List<Dictionary<string, object>>();
        if (
            payload == null
            || !payload.TryGetValue("version", out object versionValue)
            || versionValue is not long version
            || version != SaveIndexVersion
            || !payload.TryGetValue("saves", out object savesValue)
            || savesValue is not IReadOnlyList<object> rawEntries
        )
        {
            return false;
        }
        entries = NormalizeSaveIndexEntriesPlain(rawEntries);
        return true;
    }

    private bool IsSaveIndexCacheCurrent()
    {
        if (!_saveIndexCacheValid)
            return false;
        return _saveIndexCacheSignature.Matches(GetSaveIndexFileSignature());
    }

    private void SetSaveIndexCache(
        IReadOnlyList<Dictionary<string, object>> entries
    )
    {
        _saveIndexEntriesCache = CloneSaveIndexEntries(entries);
        _saveIndexCacheValid = true;
        _saveIndexCacheSignature = GetSaveIndexFileSignature();
    }

    private void InvalidateSaveIndexCache()
    {
        _saveIndexEntriesCache.Clear();
        _saveIndexCacheValid = false;
        _saveIndexCacheSignature = SaveIndexFileSignature.Missing;
    }

    private SaveIndexFileSignature GetSaveIndexFileSignature()
    {
        if (!FileAccess.FileExists(_persistenceOptions.SaveIndexPath))
            return SaveIndexFileSignature.Missing;

        int size = -1;
        string fingerprint = "";
        using NativeLeaseScope fileScope = new(
            "save-index-fingerprint",
            LifetimeDomain.Request
        );
        FileAccess openedIndexFile = FileAccess.Open(
            _persistenceOptions.SaveIndexPath,
            FileAccess.ModeFlags.Read
        );
        if (openedIndexFile != null)
        {
            FileAccess indexFile = fileScope.Own(
                openedIndexFile,
                $"open:{_persistenceOptions.SaveIndexPath}"
            );
            try
            {
                long fileLength = (long)indexFile.GetLength();
                size = (int)fileLength;
                if (fileLength > 0)
                    fingerprint = BuildFileFingerprint(indexFile.GetBuffer(fileLength));
                indexFile.Close();
            }
            finally
            {
                indexFile.Close();
            }
        }
        return new SaveIndexFileSignature(
            true,
            (int)FileAccess.GetModifiedTime(_persistenceOptions.SaveIndexPath),
            size,
            fingerprint
        );
    }

    private static string BuildFileFingerprint(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return "";
        unchecked
        {
            ulong hash = 14695981039346656037UL;
            foreach (byte value in bytes)
            {
                hash ^= value;
                hash *= 1099511628211UL;
            }
            return hash.ToString("x16");
        }
    }

    private static List<Dictionary<string, object>> CloneSaveIndexEntries(
        IEnumerable<Dictionary<string, object>> entries
    )
    {
        var result = new List<Dictionary<string, object>>();
        if (entries == null)
            return result;
        foreach (Dictionary<string, object> entry in entries)
        {
            if (entry != null)
                result.Add(RuntimePlainPayload.CloneDictionary(entry));
        }
        return result;
    }

    private bool SaveIndexEntriesMatch(
        IReadOnlyList<Dictionary<string, object>> left_entries,
        IReadOnlyList<Dictionary<string, object>> right_entries
    )
    {
        if (
            left_entries == null
            || right_entries == null
            || left_entries.Count != right_entries.Count
        )
            return false;
        for (int index = 0; index < left_entries.Count; index++)
        {
            if (!SaveIndexEntryMatches(left_entries[index], right_entries[index]))
                return false;
        }
        return true;
    }

    private bool SaveIndexEntryMatches(
        IReadOnlyDictionary<string, object> left_entry,
        IReadOnlyDictionary<string, object> right_entry
    )
    {
        string[] keys =
        {
            "save_id",
            "display_name",
            "world_preset_id",
            "world_preset_name",
            "generation_config_path",
            "world_size_cells",
            "created_at_unix_time",
            "updated_at_unix_time",
        };
        foreach (string key in keys)
        {
            object leftValue = left_entry != null && left_entry.TryGetValue(key, out object left)
                ? left
                : null;
            object rightValue = right_entry != null && right_entry.TryGetValue(key, out object right)
                ? right
                : null;
            if (!Equals(leftValue, rightValue))
                return false;
        }
        return true;
    }

    internal GodotProjectionLease<GDictionary> BuildSaveIndexPayloadLease(
        IReadOnlyList<Dictionary<string, object>> entries
    )
    {
        return _save_serializer.BuildSaveIndexPayloadLease(entries);
    }

    private List<Dictionary<string, object>> NormalizeSaveIndexEntriesPlain(
        IReadOnlyList<object> rawEntries
    )
    {
        var entries = new List<Dictionary<string, object>>();
        if (rawEntries == null)
            return entries;
        for (int index = 0; index < rawEntries.Count; index++)
        {
            if (
                rawEntries[index] is not IReadOnlyDictionary<string, object> rawEntry
                || !_save_serializer.TryNormalizeSaveMetaPlain(
                    rawEntry,
                    out Dictionary<string, object> entry
                )
                || !FileAccess.FileExists(
                    BuildSaveFilePath(ReadPlainString(entry, "save_id"))
                )
            )
            {
                continue;
            }
            entries.Add(entry);
        }
        SortSaveMetaNewestFirstPlain(entries);
        return entries;
    }

    private List<Dictionary<string, object>> NormalizeSaveIndexEntriesPlain(
        IReadOnlyList<Dictionary<string, object>> rawEntries
    )
    {
        var entries = new List<Dictionary<string, object>>();
        if (rawEntries == null)
            return entries;
        for (int index = 0; index < rawEntries.Count; index++)
        {
            if (
                !_save_serializer.TryNormalizeSaveMetaPlain(
                    rawEntries[index],
                    out Dictionary<string, object> entry
                )
                || !FileAccess.FileExists(
                    BuildSaveFilePath(ReadPlainString(entry, "save_id"))
                )
            )
            {
                continue;
            }
            entries.Add(entry);
        }
        SortSaveMetaNewestFirstPlain(entries);
        return entries;
    }

    private List<Dictionary<string, object>> RebuildSaveIndexEntriesFromSaveFilesPlain()
    {
        if (
            !DirAccess.DirExistsAbsolute(
                ProjectSettings.GlobalizePath(_persistenceOptions.SaveDirectory)
            )
        )
            return new List<Dictionary<string, object>>();

        using NativeLeaseScope directoryScope = new(
            "save-index-directory-scan",
            LifetimeDomain.Request
        );
        DirAccess openedSaveDir = DirAccess.Open(_persistenceOptions.SaveDirectory);
        if (openedSaveDir == null)
            return new List<Dictionary<string, object>>();
        DirAccess saveDir = directoryScope.Own(
            openedSaveDir,
            $"open:{_persistenceOptions.SaveDirectory}"
        );

        try
        {
            Dictionary<string, Dictionary<string, object>> rebuiltById =
                new(StringComparer.Ordinal);
            Error listError = saveDir.ListDirBegin();
            if (listError != Error.Ok)
            {
                throw new InvalidOperationException(
                    $"Failed to list save directory {_persistenceOptions.SaveDirectory} for index rebuild. Error: {(int)listError}"
                );
            }

            while (true)
            {
                string fileName = saveDir.GetNext();
                if (string.IsNullOrEmpty(fileName))
                    break;
                if (fileName == "." || fileName == ".." || saveDir.CurrentIsDir())
                    continue;
                if (!fileName.EndsWith(".dat") || fileName == "index.dat")
                    continue;
                string candidateSaveId = fileName[..^4];
                if (!_save_serializer.IsValidSaveIdToken(candidateSaveId))
                    continue;
                string savePath = $"{_persistenceOptions.SaveDirectory}/{fileName}";
                int readError = ReadSavePayload(
                    savePath,
                    out Dictionary<string, object> plainPayload,
                    false
                );
                if (readError != (int)Error.Ok)
                    continue;
                if (
                    !_save_serializer.TryExtractSaveMetaPlain(
                        plainPayload,
                        out Dictionary<string, object> saveMeta
                    )
                )
                    continue;
                string generationConfigPath = ReadPlainString(
                    saveMeta,
                    "generation_config_path"
                );
                if (
                    !_save_serializer.TryDecodePayload(
                        plainPayload,
                        generationConfigPath,
                        saveMeta,
                        out SaveDecodeResult decodeResult
                    )
                )
                    continue;
                try
                {
                    if (
                        ValidateDecodedPartyIdentityForSave(
                            decodeResult.PartyState,
                            ReadPlainString(saveMeta, "save_id"),
                            "index_rebuild"
                        ) != (int)Error.Ok
                    )
                    {
                        continue;
                    }
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                rebuiltById[ReadPlainString(saveMeta, "save_id")] =
                    RuntimePlainPayload.CloneDictionary(saveMeta);
            }

            var rebuiltEntries = new List<Dictionary<string, object>>();
            foreach (Dictionary<string, object> saveMeta in rebuiltById.Values)
                rebuiltEntries.Add(RuntimePlainPayload.CloneDictionary(saveMeta));
            SortSaveMetaNewestFirstPlain(rebuiltEntries);
            return rebuiltEntries;
        }
        finally
        {
            saveDir.ListDirEnd();
        }
    }

    private List<Dictionary<string, object>> MergeSaveIndexEntriesPlain(
        IReadOnlyList<Dictionary<string, object>> primaryEntries,
        IReadOnlyList<Dictionary<string, object>> fallbackEntries
    )
    {
        List<Dictionary<string, object>> merged = NormalizeSaveIndexEntriesPlain(
            primaryEntries
        );
        if (fallbackEntries != null)
        {
            foreach (Dictionary<string, object> fallbackEntry in fallbackEntries)
                merged = UpsertSaveMetaPlain(merged, fallbackEntry);
        }
        SortSaveMetaNewestFirstPlain(merged);
        return merged;
    }

    private int ValidateDecodedPartyIdentityForSave(
        PartyState party_state,
        string save_id,
        StringName context
    )
    {
        IReadOnlyList<string> identityErrors = IdentityPayloadValidator.ValidatePartyIdentityForContentSource(
            party_state,
            GetProgressionIdentityCatalogTyped()
        );
        if (identityErrors.Count == 0)
            return (int)Error.Ok;
        List<object> errorPayload = new();
        foreach (string identityError in identityErrors)
            errorPayload.Add(identityError);
        Dictionary<string, object> contextPayload = new(StringComparer.Ordinal)
        {
            ["save_id"] = save_id ?? "",
            ["context"] = context.ToString(),
            ["errors"] = errorPayload,
        };
        using GodotProjectionLease<GDictionary> contextLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                contextPayload,
                "save-identity-error-context",
                LifetimeDomain.Request,
                "GameSession.ValidateDecodedPartyIdentityForSave"
            );
        PushSessionError(
            "session.save.identity_invalid",
            $"Save slot {save_id} has invalid party identity payload.",
            Json.Stringify(contextLease.Value)
        );
        return (int)Error.InvalidData;
    }

    private List<Dictionary<string, object>> UpsertSaveMetaPlain(
        IReadOnlyList<Dictionary<string, object>> entries,
        IReadOnlyDictionary<string, object> saveMeta
    )
    {
        var updated = new List<Dictionary<string, object>>();
        if (
            !_save_serializer.TryNormalizeSaveMetaPlain(
                saveMeta,
                out Dictionary<string, object> normalizedMeta
            )
        )
        {
            if (entries != null)
                updated.AddRange(CloneSaveIndexEntries(entries));
            SortSaveMetaNewestFirstPlain(updated);
            return updated;
        }

        string saveId = ReadPlainString(normalizedMeta, "save_id");
        bool replaced = false;
        if (entries != null)
        {
            foreach (Dictionary<string, object> entry in entries)
            {
                if (
                    !_save_serializer.TryNormalizeSaveMetaPlain(
                        entry,
                        out Dictionary<string, object> normalizedExisting
                    )
                )
                {
                    continue;
                }
                if (
                    string.Equals(
                        ReadPlainString(normalizedExisting, "save_id"),
                        saveId,
                        StringComparison.Ordinal
                    )
                )
                {
                    updated.Add(RuntimePlainPayload.CloneDictionary(normalizedMeta));
                    replaced = true;
                }
                else
                {
                    updated.Add(normalizedExisting);
                }
            }
        }
        if (!replaced)
            updated.Add(RuntimePlainPayload.CloneDictionary(normalizedMeta));
        SortSaveMetaNewestFirstPlain(updated);
        return updated;
    }

    private Dictionary<string, object> GetSaveMetaByIdPlain(string saveId)
    {
        foreach (Dictionary<string, object> entry in LoadSaveIndexEntriesPlain())
        {
            if (
                string.Equals(
                    ReadPlainString(entry, "save_id"),
                    saveId,
                    StringComparison.Ordinal
                )
            )
                return RuntimePlainPayload.CloneDictionary(entry);
        }
        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static void SortSaveMetaNewestFirstPlain(
        List<Dictionary<string, object>> entries
    )
    {
        entries?.Sort(CompareSaveMetaNewestFirstPlain);
    }

    private static int CompareSaveMetaNewestFirstPlain(
        Dictionary<string, object> left,
        Dictionary<string, object> right
    )
    {
        int leftUpdated = ReadPlainInt(left, "updated_at_unix_time");
        int rightUpdated = ReadPlainInt(right, "updated_at_unix_time");
        if (leftUpdated != rightUpdated)
            return rightUpdated.CompareTo(leftUpdated);

        int leftCreated = ReadPlainInt(left, "created_at_unix_time");
        int rightCreated = ReadPlainInt(right, "created_at_unix_time");
        if (leftCreated != rightCreated)
            return rightCreated.CompareTo(leftCreated);

        return -string.CompareOrdinal(
            ReadPlainString(left, "save_id"),
            ReadPlainString(right, "save_id")
        );
    }

    private static string ReadPlainString(
        IReadOnlyDictionary<string, object> values,
        string key,
        string fallback = ""
    ) =>
        values != null
        && values.TryGetValue(key, out object value)
        && value is string stringValue
            ? stringValue
            : fallback ?? "";

    private static int ReadPlainInt(
        IReadOnlyDictionary<string, object> values,
        string key,
        int fallback = 0
    )
    {
        if (values == null || !values.TryGetValue(key, out object value))
            return fallback;
        return value switch
        {
            byte byteValue => byteValue,
            short shortValue => shortValue,
            int intValue => intValue,
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue =>
                (int)longValue,
            _ => fallback,
        };
    }

    public int RemoveDirectoryRecursive(string virtual_path)
    {
        return FileIOCoordinator.RemoveDirectoryRecursive(
            virtual_path,
            PushSessionError
        );
    }
}
