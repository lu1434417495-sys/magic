using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

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

    private int WriteSavePayloadAtomically(string save_path, GDictionary payload)
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

    public GDictionaryArray LoadSaveIndexEntries()
    {
        if (IsSaveIndexCacheCurrent())
            return ProjectSaveIndexEntriesCache(
                _saveIndexEntriesCache,
                "GameSession.LoadSaveIndexEntries.cache"
            );

        bool shouldRewriteIndex = false;
        GArray rawEntries = new();
        int indexRecoveryError = FileIOCoordinator.RecoverReplaceTarget(
            SaveIndexPath,
            SaveFileCompressionMode,
            "session.save.index",
            "save index",
            PushSessionError
        );
        if (indexRecoveryError != (int)Error.Ok && indexRecoveryError != (int)Error.DoesNotExist)
            shouldRewriteIndex = true;
        if (!FileAccess.FileExists(SaveIndexPath))
        {
            shouldRewriteIndex = true;
        }
        else
        {
            FileAccess indexFile = FileAccess.OpenCompressed(
                SaveIndexPath,
                FileAccess.ModeFlags.Read,
                (FileAccess.CompressionMode)SaveFileCompressionMode
            );
            if (indexFile == null)
            {
                shouldRewriteIndex = true;
            }
            else
            {
                try
                {
                    bool hasIndexPayload = TryReadSaveIndexPayload(indexFile, out GDictionary rawPayloadDict);
                    indexFile.Close();
                    if (hasIndexPayload)
                    {
                        TryRead(rawPayloadDict, "version", out var indexVersionValue);
                        TryRead(rawPayloadDict, "saves", out var savesValue);
                        if (
                            !_is_save_index_integer_value(indexVersionValue)
                            || indexVersionValue.AsInt32() != SaveIndexVersion
                            || savesValue.VariantType != Variant.Type.Array
                        )
                        {
                            shouldRewriteIndex = true;
                        }
                        else
                        {
                            rawEntries = savesValue.AsGodotArray();
                        }
                    }
                    else
                    {
                        shouldRewriteIndex = true;
                    }
                }
                finally
                {
                    GodotObjectLifecycle.DisposeGodotObject(indexFile);
                }
            }
        }

        GDictionaryArray entries = NormalizeSaveIndexEntries(rawEntries);
        GDictionaryArray rebuiltEntries = RebuildSaveIndexEntriesFromSaveFiles();
        GDictionaryArray mergedEntries = MergeSaveIndexEntries(entries, rebuiltEntries);
        if (shouldRewriteIndex || !SaveIndexEntriesMatch(entries, mergedEntries))
            WriteSaveIndex(mergedEntries);
        else
            SetSaveIndexCache(mergedEntries);
        return DuplicateSaveIndexEntries(mergedEntries);
    }

    public GDictionaryArray PeekSaveIndexEntriesReadOnly()
    {
        if (IsSaveIndexCacheCurrent())
            return ProjectSaveIndexEntriesCache(
                _saveIndexEntriesCache,
                "GameSession.PeekSaveIndexEntriesReadOnly.cache"
            );
        if (!FileAccess.FileExists(SaveIndexPath))
            return new GDictionaryArray();

        FileAccess indexFile = FileAccess.OpenCompressed(
            SaveIndexPath,
            FileAccess.ModeFlags.Read,
            (FileAccess.CompressionMode)SaveFileCompressionMode
        );
        if (indexFile == null)
            return new GDictionaryArray();
        GDictionary rawPayloadDict;
        try
        {
            bool hasIndexPayload = TryReadSaveIndexPayload(indexFile, out rawPayloadDict);
            indexFile.Close();
            if (!hasIndexPayload)
                return new GDictionaryArray();
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(indexFile);
        }

        TryRead(rawPayloadDict, "version", out var indexVersionValue);
        TryRead(rawPayloadDict, "saves", out var savesValue);
        if (
            !_is_save_index_integer_value(indexVersionValue)
            || indexVersionValue.AsInt32() != SaveIndexVersion
            || savesValue.VariantType != Variant.Type.Array
        )
        {
            return new GDictionaryArray();
        }

        GDictionaryArray entries = NormalizeSaveIndexEntries(savesValue.AsGodotArray());
        SetSaveIndexCache(entries);
        return DuplicateSaveIndexEntries(entries);
    }

    public int WriteSaveIndex(GDictionaryArray entries)
    {
        int ensureDirError = EnsureSaveDirectory();
        if (ensureDirError != (int)Error.Ok)
            return ensureDirError;

        GDictionaryArray normalizedEntries = NormalizeSaveIndexEntries(ToUntypedArray(entries));
        int writeError = EnsureSaveRepository().WriteCompressedVariantAtomically(
            SaveIndexPath,
            BuildSaveIndexPayload(normalizedEntries),
            "session.save.index",
            "save index"
        );
        if (writeError != (int)Error.Ok)
            return writeError;
        SetSaveIndexCache(normalizedEntries);
        return (int)Error.Ok;
    }

    private bool TryReadSaveIndexPayload(FileAccess index_file, out GDictionary payload)
    {
        GDictionary rawPayload = _save_serializer.ReadSaveIndexPayload(index_file);
        if (rawPayload != null)
        {
            payload = rawPayload;
            return true;
        }
        payload = new GDictionary();
        return false;
    }

    private bool IsSaveIndexCacheCurrent()
    {
        if (!_saveIndexCacheValid)
            return false;
        return _saveIndexCacheSignature.Matches(GetSaveIndexFileSignature());
    }

    private void SetSaveIndexCache(GDictionaryArray entries)
    {
        _saveIndexEntriesCache = NormalizeSaveIndexEntryCache(entries);
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
        if (!FileAccess.FileExists(SaveIndexPath))
            return SaveIndexFileSignature.Missing;

        int size = -1;
        string fingerprint = "";
        FileAccess indexFile = FileAccess.Open(SaveIndexPath, FileAccess.ModeFlags.Read);
        if (indexFile != null)
        {
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
                GodotObjectLifecycle.DisposeGodotObject(indexFile);
            }
        }
        return new SaveIndexFileSignature(
            true,
            (int)FileAccess.GetModifiedTime(SaveIndexPath),
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

    private static T MarkRuntimePayload<T>(T payload, string reason)
        where T : class
    {
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(payload, reason);
        return payload;
    }

    private GDictionaryArray DuplicateSaveIndexEntries(GDictionaryArray entries)
    {
        GDictionaryArray duplicatedEntries = new();
        if (entries == null)
        {
            RuntimeStateLifecycle.MarkValueGraphFinalizerless(
                duplicatedEntries,
                "GameSession.DuplicateSaveIndexEntries.empty"
            );
            return duplicatedEntries;
        }
        foreach (GDictionary entry in entries)
            duplicatedEntries.Add(
                RuntimePayloadCopy.Dictionary(
                    entry,
                    "GameSession.DuplicateSaveIndexEntries.entry"
                )
            );
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(
            duplicatedEntries,
            "GameSession.DuplicateSaveIndexEntries"
        );
        return duplicatedEntries;
    }

    private List<Dictionary<string, object>> NormalizeSaveIndexEntryCache(
        GDictionaryArray entries
    )
    {
        var result = new List<Dictionary<string, object>>();
        if (entries == null)
            return result;
        foreach (GDictionary entry in entries)
        {
            result.Add(
                RuntimePlainPayload.NormalizeDictionary(
                    entry,
                    "GameSession.SaveIndexCache.entry"
                )
            );
        }
        return result;
    }

    private GDictionaryArray ProjectSaveIndexEntriesCache(
        IEnumerable<Dictionary<string, object>> entries,
        string reason
    )
    {
        GDictionaryArray projectedEntries = new();
        if (entries != null)
        {
            int index = 0;
            foreach (Dictionary<string, object> entry in entries)
            {
                projectedEntries.Add(
                    RuntimePlainPayload.ProjectDictionary(entry, $"{reason}[{index}]")
                );
                index++;
            }
        }
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(projectedEntries, reason);
        return projectedEntries;
    }

    private bool SaveIndexEntriesMatch(
        GDictionaryArray left_entries,
        GDictionaryArray right_entries
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

    private bool SaveIndexEntryMatches(GDictionary left_entry, GDictionary right_entry)
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
            TryRead(left_entry, key, out var leftVal);
            TryRead(right_entry, key, out var rightVal);
            if (!VariantEquals(leftVal, rightVal))
                return false;
        }
        return true;
    }

    private GDictionaryArray NormalizeSaveIndexEntries(GArray raw_entries)
    {
        GDictionaryArray entries = new();
        if (raw_entries == null)
            return entries;
        foreach (GDictionary rawEntry in ReadDictionaryItems(raw_entries))
        {
            GDictionary entry = NormalizeSaveMeta(
                DeserializeSaveIndexEntry(rawEntry)
            );
            if (entry.Count == 0)
                continue;
            if (!FileAccess.FileExists(BuildSaveFilePath(GetString(entry, "save_id"))))
                continue;
            entries.Add(entry);
        }
        SortSaveMetaNewestFirst(entries);
        return entries;
    }

    public GDictionaryArray SerializeSaveIndexEntries(GDictionaryArray entries)
    {
        return _save_serializer.SerializeSaveIndexEntries(entries);
    }

    public GDictionary BuildSaveIndexPayload(GDictionaryArray entries)
    {
        return _save_serializer.BuildSaveIndexPayload(entries);
    }

    public GDictionary DeserializeSaveIndexEntry(GDictionary raw_entry)
    {
        return _save_serializer.DeserializeSaveIndexEntry(raw_entry);
    }

    private bool _is_save_index_integer_value(Variant value)
    {
        return value.VariantType == Variant.Type.Int
            && _save_serializer.IsSaveIndexIntegerValue(value.AsInt32());
    }

    private GDictionaryArray RebuildSaveIndexEntriesFromSaveFiles()
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(SaveDirectory)))
            return new GDictionaryArray();

        DirAccess saveDir = DirAccess.Open(SaveDirectory);
        if (saveDir == null)
            return new GDictionaryArray();

        try
        {
            GDictionary rebuiltById = new();
            Error listError = saveDir.ListDirBegin();
            if (listError != Error.Ok)
            {
                throw new InvalidOperationException(
                    $"Failed to list save directory {SaveDirectory} for index rebuild. Error: {(int)listError}"
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
                string savePath = $"{SaveDirectory}/{fileName}";
                GDictionary readResult = ReadSavePayload(savePath, false);
                if (GetInt(readResult, "error", (int)Error.InvalidData) != (int)Error.Ok)
                    continue;
                if (!TryRead(readResult, "payload", out var payloadValue)
                    || payloadValue.VariantType != Variant.Type.Dictionary)
                    continue;
                GDictionary payload = payloadValue.AsGodotDictionary();
                GDictionary saveMeta = ExtractSaveMetaFromPayload(payload);
                if (saveMeta.Count == 0)
                    continue;
                string generationConfigPath = GetString(saveMeta, "generation_config_path");
                WorldMapGenerationConfig generationConfig = LoadGenerationConfig(
                    generationConfigPath
                );
                if (generationConfig == null)
                    continue;
                GDictionary decodeResult = _save_serializer.DecodePayload(
                    payload,
                    generationConfigPath,
                    generationConfig,
                    saveMeta
                );
                if (GetInt(decodeResult, "error", (int)Error.InvalidData) != (int)Error.Ok)
                    continue;
                try
                {
                    if (
                        ValidateDecodedPartyIdentityForSave(
                            PartyState.TryReadPartyPayload(
                                decodeResult["party_state"],
                                out PartyState decodedPartyStateForMeta
                            )
                                ? decodedPartyStateForMeta
                                : null,
                            GetString(saveMeta, "save_id"),
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
                rebuiltById[GetString(saveMeta, "save_id")] = saveMeta;
            }

            GDictionaryArray rebuiltEntries = new();
            foreach (var saveMetaValue in rebuiltById.Values)
            {
                if (TryUnboxToDictionary(saveMetaValue, out GDictionary saveMeta))
                    rebuiltEntries.Add(saveMeta);
            }
            SortSaveMetaNewestFirst(rebuiltEntries);
            return rebuiltEntries;
        }
        finally
        {
            saveDir.ListDirEnd();
            GodotObjectLifecycle.DisposeGodotObject(saveDir);
        }
    }

    public GDictionaryArray MergeSaveIndexEntries(
        GDictionaryArray primary_entries,
        GDictionaryArray fallback_entries
    )
    {
        return _save_serializer.MergeSaveIndexEntries(primary_entries, fallback_entries);
    }

    public GDictionary ExtractSaveMetaFromPayload(GDictionary payload)
    {
        return _save_serializer.ExtractSaveMetaFromPayload(payload);
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
        GArray errorPayload = new();
        foreach (string identityError in identityErrors)
            errorPayload.Add(identityError);
        PushSessionError(
            "session.save.identity_invalid",
            $"Save slot {save_id} has invalid party identity payload.",
            Json.Stringify(
                new GDictionary
                {
                    ["save_id"] = save_id,
                    ["context"] = context,
                    ["errors"] = errorPayload,
                }
            )
        );
        return (int)Error.InvalidData;
    }

    public GDictionaryArray UpsertSaveMeta(GDictionaryArray entries, GDictionary save_meta)
    {
        return _save_serializer.UpsertSaveMeta(entries, save_meta);
    }

    private GDictionary GetSaveMetaById(string save_id)
    {
        foreach (GDictionary entry in LoadSaveIndexEntries())
        {
            if (GetString(entry, "save_id") == save_id)
                return entry;
        }
        return new GDictionary();
    }

    private GDictionary FindMostRecentSaveByConfig(string generation_config_path)
    {
        foreach (GDictionary entry in LoadSaveIndexEntries())
        {
            if (GetString(entry, "generation_config_path") == generation_config_path)
                return entry;
        }
        return new GDictionary();
    }

    public GDictionary NormalizeSaveMeta(GDictionary raw_meta)
    {
        return _save_serializer.NormalizeSaveMeta(raw_meta ?? new GDictionary());
    }

    private bool SortSaveMetaNewestFirst(GDictionary a, GDictionary b)
    {
        return _save_serializer.SortSaveMetaNewestFirst(a, b);
    }

    public int RemoveDirectoryRecursive(string virtual_path)
    {
        return FileIOCoordinator.RemoveDirectoryRecursive(
            virtual_path,
            PushSessionError
        );
    }
}
