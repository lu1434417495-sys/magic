using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class SaveDecodeResult
{
    internal SaveDecodeResult(int error)
    {
        Error = error;
        ActiveSaveMeta = new Dictionary<string, object>(StringComparer.Ordinal);
        WorldData = new Dictionary<string, object>(StringComparer.Ordinal);
        PartyState = new PartyState();
        ActiveSaveId = "";
        GenerationConfigPath = "";
        PlayerFactionId = "player";
    }

    internal SaveDecodeResult(
        Dictionary<string, object> activeSaveMeta,
        Dictionary<string, object> worldData,
        PartyState partyState,
        string activeSaveId,
        string generationConfigPath,
        Vector2I playerCoord,
        string playerFactionId
    )
    {
        Error = (int)Godot.Error.Ok;
        ActiveSaveMeta = activeSaveMeta
            ?? new Dictionary<string, object>(StringComparer.Ordinal);
        WorldData = worldData ?? new Dictionary<string, object>(StringComparer.Ordinal);
        PartyState = partyState ?? new PartyState();
        ActiveSaveId = activeSaveId ?? "";
        GenerationConfigPath = generationConfigPath ?? "";
        PlayerCoord = playerCoord;
        PlayerFactionId = playerFactionId ?? "player";
    }

    internal int Error { get; }
    internal Dictionary<string, object> ActiveSaveMeta { get; }
    internal Dictionary<string, object> WorldData { get; }
    internal PartyState PartyState { get; }
    internal string ActiveSaveId { get; }
    internal string GenerationConfigPath { get; }
    internal Vector2I PlayerCoord { get; }
    internal string PlayerFactionId { get; }
}

public sealed class SaveSerializer
{
    private const string WorldMapSeedKey = "map_seed";
    private const string WorldEquipmentInstanceSerialKey = "next_equipment_instance_serial";
    private const string SaveFormat = "multi_save_total_save";

    private int _save_version = SaveSchemaVersions.SaveVersion;
    private int _save_index_version = SaveSchemaVersions.SaveIndexVersion;
    private int _max_active_member_count = SaveSchemaVersions.MaxActiveMemberCount;

    public void Setup(int saveVersion, int saveIndexVersion, int maxActiveMemberCount)
    {
        _save_version = saveVersion;
        _save_index_version = saveIndexVersion;
        _max_active_member_count = maxActiveMemberCount;
    }

    internal GodotProjectionLease<GDictionary> BuildSavePayloadLease(
        string activeSaveId,
        string generationConfigPath,
        IReadOnlyDictionary<string, object> activeSaveMeta,
        IReadOnlyDictionary<string, object> worldData,
        Vector2I playerCoord,
        string playerFactionId,
        PartyState partyState,
        int savedAtUnixTime
    )
    {
        Dictionary<string, object> worldState = BuildWorldStatePlain(
            worldData,
            playerCoord,
            playerFactionId
        );
        return BuildSavePayloadFromWorldStateLease(
            activeSaveId,
            generationConfigPath,
            activeSaveMeta,
            worldState,
            partyState,
            savedAtUnixTime,
            "SaveSerializer.BuildSavePayloadLease"
        );
    }

    internal GodotProjectionLease<GDictionary> BuildTrustedSavePayloadLease(
        string activeSaveId,
        string generationConfigPath,
        IReadOnlyDictionary<string, object> activeSaveMeta,
        IReadOnlyDictionary<string, object> worldData,
        Vector2I playerCoord,
        string playerFactionId,
        PartyState partyState,
        int savedAtUnixTime
    )
    {
        if (worldData == null)
            throw new ArgumentNullException(nameof(worldData));
        Dictionary<string, object> worldState = new(StringComparer.Ordinal)
        {
            ["world_data"] = worldData,
            ["player_coord"] = playerCoord,
            ["player_faction_id"] = playerFactionId,
        };
        return BuildSavePayloadFromWorldStateLease(
            activeSaveId,
            generationConfigPath,
            activeSaveMeta,
            worldState,
            partyState,
            savedAtUnixTime,
            "SaveSerializer.BuildTrustedSavePayloadLease"
        );
    }

    private GodotProjectionLease<GDictionary> BuildSavePayloadFromWorldStateLease(
        string activeSaveId,
        string generationConfigPath,
        IReadOnlyDictionary<string, object> activeSaveMeta,
        Dictionary<string, object> worldState,
        PartyState partyState,
        int savedAtUnixTime,
        string reason
    )
    {
        Dictionary<string, object> payload = new(StringComparer.Ordinal)
        {
            ["version"] = _save_version,
            ["save_id"] = activeSaveId,
            ["generation_config_path"] = generationConfigPath,
            ["world_state"] = worldState,
            ["party_state"] = SerializePartyStatePlain(partyState),
            ["meta"] = BuildMetaPayloadPlain(savedAtUnixTime),
            ["save_slot_meta"] = RuntimePlainPayload.CloneDictionary(activeSaveMeta),
        };
        return RuntimePlainPayload.ProjectDictionaryLease(
            payload,
            "save-payload",
            LifetimeDomain.Request,
            reason,
            minimizeStrings: true
        );
    }

    internal GodotProjectionLease<GDictionary> BuildWorldStatePayloadLease(
        IReadOnlyDictionary<string, object> worldData,
        Vector2I playerCoord,
        string playerFactionId
    )
    {
        return RuntimePlainPayload.ProjectDictionaryLease(
            BuildWorldStatePlain(worldData, playerCoord, playerFactionId),
            "world-state-payload",
            LifetimeDomain.Request,
            "SaveSerializer.BuildWorldStatePayloadLease"
        );
    }

    internal bool TryDecodePayload(
        IReadOnlyDictionary<string, object> payload,
        string generationConfigPath,
        IReadOnlyDictionary<string, object> saveMeta,
        out SaveDecodeResult result
    )
    {
        result = new SaveDecodeResult((int)Error.InvalidData);
        if (
            payload == null
            || !TryNormalizeSaveMetaPlain(
                saveMeta,
                out Dictionary<string, object> normalizedRequestedMeta
            )
        )
        {
            return false;
        }
        string[] requiredPayloadKeys =
        {
            "version",
            "save_id",
            "generation_config_path",
            "world_state",
            "party_state",
            "meta",
            "save_slot_meta",
        };
        if (!HasExactPlainKeys(payload, requiredPayloadKeys))
            return false;
        if (
            !TryReadPlainInt(payload, "version", out int version)
            || version != _save_version
            || !TryReadPlainString(payload, "save_id", out string activeSaveId)
            || !TryReadPlainString(
                payload,
                "generation_config_path",
                out string payloadGenerationConfigPath
            )
        )
            return false;
        if (
            !string.Equals(
                payloadGenerationConfigPath,
                generationConfigPath,
                StringComparison.Ordinal
            )
            || !string.Equals(
                activeSaveId,
                ReadPlainString(normalizedRequestedMeta, "save_id"),
                StringComparison.Ordinal
            )
        )
            return false;
        if (
            !TryReadPlainDictionary(payload, "world_state", out var worldState)
            || !TryReadPlainDictionary(payload, "party_state", out var partyPayload)
            || !TryReadPlainDictionary(payload, "meta", out var payloadMeta)
            || !TryReadPlainDictionary(payload, "save_slot_meta", out var payloadSaveMeta)
        )
            return false;

        if (
            !HasExactPlainKeys(
                worldState,
                new[] { "world_data", "player_coord", "player_faction_id" }
            )
            || !TryReadPlainDictionary(worldState, "world_data", out var rawWorldData)
            || !worldState.TryGetValue("player_coord", out object playerCoordValue)
            || playerCoordValue is not Vector2I playerCoord
            || !TryReadPlainString(
                worldState,
                "player_faction_id",
                out string playerFactionId
            )
            || !TryNormalizeWorldDataPlain(rawWorldData, out Dictionary<string, object> worldData)
        )
            return false;
        playerFactionId = playerFactionId.Trim();
        if (string.IsNullOrEmpty(playerFactionId))
            return false;

        if (
            !HasExactPlainKeys(
                payloadMeta,
                new[] { "saved_at_unix_time", "save_format" }
            )
            || !TryReadPlainInt(payloadMeta, "saved_at_unix_time", out _)
            || !TryReadPlainString(payloadMeta, "save_format", out string saveFormat)
            || !string.Equals(saveFormat, SaveFormat, StringComparison.Ordinal)
        )
            return false;

        if (
            !TryNormalizeSaveMetaPlain(
                payloadSaveMeta,
                out Dictionary<string, object> normalizedMeta
            )
            || !string.Equals(
                ReadPlainString(normalizedMeta, "save_id"),
                activeSaveId,
                StringComparison.Ordinal
            )
            || !string.Equals(
                ReadPlainString(normalizedMeta, "save_id"),
                ReadPlainString(normalizedRequestedMeta, "save_id"),
                StringComparison.Ordinal
            )
            || !string.Equals(
                ReadPlainString(normalizedMeta, "generation_config_path"),
                generationConfigPath,
                StringComparison.Ordinal
            )
            || !string.Equals(
                ReadPlainString(normalizedRequestedMeta, "generation_config_path"),
                generationConfigPath,
                StringComparison.Ordinal
            )
        )
            return false;

        PartyState partyState;
        using (
            GodotProjectionLease<GDictionary> partyLease =
                RuntimePlainPayload.ProjectDictionaryLease(
                    partyPayload,
                    "save-party-decode",
                    LifetimeDomain.Request,
                    "SaveSerializer.TryDecodePayload.party_state"
                )
        )
        {
            partyState = PartyState.FromDictionary(partyLease.Value);
        }
        if (partyState == null)
            return false;
        partyState = NormalizeParsedPartyState(partyState);

        result = new SaveDecodeResult(
            normalizedMeta,
            worldData,
            partyState,
            activeSaveId,
            generationConfigPath,
            playerCoord,
            playerFactionId
        );
        return true;
    }

    internal Dictionary<string, object> BuildSaveMetaPlain(
        string saveId,
        string displayName,
        string generationConfigPath,
        StringName presetId,
        string presetName,
        Vector2I worldSizeCells,
        int createdAtUnixTime,
        int updatedAtUnixTime
    )
    {
        var raw = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["save_id"] = saveId ?? "",
            ["display_name"] = string.IsNullOrEmpty(displayName) ? saveId ?? "" : displayName,
            ["world_preset_id"] = presetId.ToString(),
            ["world_preset_name"] = presetName ?? "",
            ["generation_config_path"] = generationConfigPath ?? "",
            ["world_size_cells"] = worldSizeCells,
            ["created_at_unix_time"] = createdAtUnixTime,
            ["updated_at_unix_time"] = updatedAtUnixTime,
        };
        return TryNormalizeSaveMetaPlain(raw, out Dictionary<string, object> normalized)
            ? normalized
            : new Dictionary<string, object>(StringComparer.Ordinal);
    }

    internal bool TryNormalizeSaveMetaPlain(
        IReadOnlyDictionary<string, object> rawMeta,
        out Dictionary<string, object> normalized
    )
    {
        normalized = new Dictionary<string, object>(StringComparer.Ordinal);
        string[] required =
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
        if (rawMeta == null || rawMeta.Count != required.Length)
            return false;
        foreach (string key in required)
        {
            if (!rawMeta.ContainsKey(key))
                return false;
        }

        if (
            !TryReadPlainString(rawMeta, "save_id", out string saveId)
            || !IsValidSaveIdToken(saveId)
            || !TryReadPlainString(rawMeta, "display_name", out string displayName)
            || !TryReadPlainString(rawMeta, "world_preset_id", out string worldPresetId)
            || !TryReadPlainString(rawMeta, "world_preset_name", out string worldPresetName)
            || !TryReadPlainString(
                rawMeta,
                "generation_config_path",
                out string generationConfigPath
            )
            || !TryReadPlainInt(rawMeta, "created_at_unix_time", out int createdAt)
            || !TryReadPlainInt(rawMeta, "updated_at_unix_time", out int updatedAt)
            || !rawMeta.TryGetValue("world_size_cells", out object worldSizeValue)
            || worldSizeValue is not Vector2I worldSizeCells
        )
        {
            return false;
        }

        displayName = displayName.Trim();
        worldPresetName = worldPresetName.Trim();
        generationConfigPath = generationConfigPath.Trim();
        if (
            displayName.Length == 0
            || worldPresetName.Length == 0
            || generationConfigPath.Length == 0
            || worldSizeCells.X <= 0
            || worldSizeCells.Y <= 0
            || createdAt <= 0
            || updatedAt <= 0
        )
        {
            return false;
        }

        normalized = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["save_id"] = saveId,
            ["display_name"] = displayName,
            ["world_preset_id"] = worldPresetId,
            ["world_preset_name"] = worldPresetName,
            ["generation_config_path"] = generationConfigPath,
            ["world_size_cells"] = worldSizeCells,
            ["created_at_unix_time"] = createdAt,
            ["updated_at_unix_time"] = updatedAt,
        };
        return true;
    }

    internal bool TryExtractSaveMetaPlain(
        IReadOnlyDictionary<string, object> payload,
        out Dictionary<string, object> saveMeta
    )
    {
        saveMeta = new Dictionary<string, object>(StringComparer.Ordinal);
        string[] requiredPayloadKeys =
        {
            "version",
            "save_id",
            "generation_config_path",
            "world_state",
            "party_state",
            "meta",
            "save_slot_meta",
        };
        if (!HasExactPlainKeys(payload, requiredPayloadKeys))
            return false;
        if (
            !TryReadPlainInt(payload, "version", out int version)
            || version != _save_version
            || !TryReadPlainString(payload, "save_id", out string saveId)
            || !TryReadPlainString(
                payload,
                "generation_config_path",
                out string generationConfigPath
            )
            || !TryReadPlainDictionary(payload, "save_slot_meta", out var rawSaveMeta)
            || !TryNormalizeSaveMetaPlain(rawSaveMeta, out Dictionary<string, object> normalized)
        )
        {
            return false;
        }

        saveId = saveId.Trim();
        generationConfigPath = generationConfigPath.Trim();
        if (
            saveId.Length == 0
            || generationConfigPath.Length == 0
            || !string.Equals(
                ReadPlainString(normalized, "save_id"),
                saveId,
                StringComparison.Ordinal
            )
            || !string.Equals(
                ReadPlainString(normalized, "generation_config_path"),
                generationConfigPath,
                StringComparison.Ordinal
            )
        )
        {
            return false;
        }

        saveMeta = normalized;
        return true;
    }

    public string GetWorldDataValidationError(GDictionary worldData)
    {
        if (worldData == null)
            return "Corrupt save world_data: expected Dictionary.";
        string seedError = GetWorldDataSeedValidationError(worldData);
        if (!string.IsNullOrEmpty(seedError))
            return seedError;
        string worldStepError = GetWorldDataStepValidationError(worldData);
        if (!string.IsNullOrEmpty(worldStepError))
            return worldStepError;
        string equipmentSerialError = GetEquipmentInstanceSerialValidationError(worldData);
        if (!string.IsNullOrEmpty(equipmentSerialError))
            return equipmentSerialError;
        string schemaError = GetWorldDataSchemaValidationError(worldData);
        if (!string.IsNullOrEmpty(schemaError))
            return schemaError;
        string nestedSchemaError = GetWorldDataNestedSchemaValidationError(worldData);
        if (!string.IsNullOrEmpty(nestedSchemaError))
            return nestedSchemaError;
        using GDictionary mountedSubmaps = ReadDictionary(worldData, "mounted_submaps");
        return get_mounted_submaps_validation_error(mountedSubmaps);
    }

    public string GetWorldDataSchemaValidationError(GDictionary worldData)
    {
        if (worldData == null)
            return "Corrupt save world_data: expected Dictionary.";

        string[] required =
        {
            WorldMapSeedKey,
            "world_step",
            WorldEquipmentInstanceSerialKey,
            "active_submap_id",
            "submap_return_stack",
            "settlements",
            "world_events",
            "encounter_anchors",
            "resource_nodes",
            "mounted_submaps",
        };
        string[] optional =
        {
            "world_npcs",
            "player_start_coord",
            "player_start_settlement_id",
            "player_start_settlement_name",
            "fog_states",
        };
        if (!HasRequiredAndAllowedKeys(worldData, required, optional))
            return "Corrupt save world_data: fields must match current schema.";
        if (!IsStringValue(worldData["active_submap_id"]))
            return "Corrupt save world_data: active_submap_id must be a String.";
        foreach (
            string arrayField in new[]
            {
                "submap_return_stack",
                "settlements",
                "world_events",
                "encounter_anchors",
                "resource_nodes",
            }
        )
        {
            if (worldData[arrayField].VariantType != Variant.Type.Array)
                return $"Corrupt save world_data: {arrayField} must be an Array.";
        }
        if (
            worldData.ContainsKey("world_npcs")
            && worldData["world_npcs"].VariantType != Variant.Type.Array
        )
            return "Corrupt save world_data: world_npcs must be an Array.";
        if (worldData["mounted_submaps"].VariantType != Variant.Type.Dictionary)
            return "Corrupt save world_data: mounted_submaps must be a Dictionary.";
        if (
            worldData.ContainsKey("player_start_coord")
            && !IsNativeVector2I(worldData["player_start_coord"])
        )
            return "Corrupt save world_data: player_start_coord must be a native Vector2i.";
        foreach (
            string optionalStringField in new[]
            {
                "player_start_settlement_id",
                "player_start_settlement_name",
            }
        )
        {
            if (
                worldData.ContainsKey(optionalStringField)
                && !IsStringValue(worldData[optionalStringField])
            )
                return $"Corrupt save world_data: {optionalStringField} must be a String.";
        }
        if (worldData.ContainsKey("fog_states"))
        {
            if (worldData["fog_states"].VariantType != Variant.Type.Dictionary)
                return "Corrupt save world_data: fog_states must be a Dictionary.";
            using GDictionary fogStates = worldData["fog_states"].AsGodotDictionary();
            string fogStateError =
                WorldMapFogSystem.GetPersistentStateSchemaValidationError(fogStates);
            if (!string.IsNullOrEmpty(fogStateError))
                return $"Corrupt save world_data.fog_states: {fogStateError}";
        }
        return "";
    }

    public string GetWorldDataNestedSchemaValidationError(GDictionary worldData)
    {
        if (worldData == null)
            return "Corrupt save world_data: expected Dictionary.";
        using GArray returnStack = ReadArray(worldData, "submap_return_stack");
        string returnStackError = GetSubmapReturnStackValidationError(returnStack);
        if (!string.IsNullOrEmpty(returnStackError))
            return returnStackError;
        using GArray settlements = ReadArray(worldData, "settlements");
        string settlementError = GetSettlementsValidationError(settlements);
        if (!string.IsNullOrEmpty(settlementError))
            return settlementError;
        using GArray worldEvents = ReadArray(worldData, "world_events");
        string eventError = GetWorldEventsValidationError(worldEvents);
        if (!string.IsNullOrEmpty(eventError))
            return eventError;
        if (
            worldData.ContainsKey("encounter_anchors")
            && worldData["encounter_anchors"].VariantType != Variant.Type.Array
        )
            return "Corrupt save world_data.encounter_anchors: expected Array.";
        using GArray resourceNodes = ReadArray(worldData, "resource_nodes");
        string resourceNodeError = GetWorldResourceNodesValidationError(resourceNodes);
        if (!string.IsNullOrEmpty(resourceNodeError))
            return resourceNodeError;
        if (
            worldData.ContainsKey("world_npcs")
            && worldData["world_npcs"].VariantType != Variant.Type.Array
        )
            return "Corrupt save world_data.world_npcs: expected Array.";
        return "";
    }

    public string GetWorldDataSeedValidationError(GDictionary worldData)
    {
        if (worldData == null || !worldData.ContainsKey(WorldMapSeedKey))
            return $"Corrupt save world_data: missing required field '{WorldMapSeedKey}'.";
        if (worldData[WorldMapSeedKey].VariantType != Variant.Type.Int)
            return $"Corrupt save world_data: {WorldMapSeedKey} must be an int.";
        if ((long)worldData[WorldMapSeedKey] < 1)
            return $"Corrupt save world_data: {WorldMapSeedKey} must be >= 1.";
        return "";
    }

    public string GetWorldDataStepValidationError(GDictionary worldData)
    {
        if (worldData == null || !worldData.ContainsKey("world_step"))
            return "Corrupt save world_data: missing required field 'world_step'.";
        if (worldData["world_step"].VariantType != Variant.Type.Int)
            return "Corrupt save world_data: world_step must be an int.";
        if (worldData["world_step"].AsInt32() < 0)
            return "Corrupt save world_data: world_step must be >= 0.";
        return "";
    }

    public string GetEquipmentInstanceSerialValidationError(GDictionary worldData)
    {
        if (worldData == null || !worldData.ContainsKey(WorldEquipmentInstanceSerialKey))
            return $"Corrupt save world_data: missing required field '{WorldEquipmentInstanceSerialKey}'.";
        if (worldData[WorldEquipmentInstanceSerialKey].VariantType != Variant.Type.Int)
            return $"Corrupt save world_data: {WorldEquipmentInstanceSerialKey} must be an int.";
        if (worldData[WorldEquipmentInstanceSerialKey].AsInt32() < 1)
            return $"Corrupt save world_data: {WorldEquipmentInstanceSerialKey} must be >= 1.";
        return "";
    }

    public string GetMountedSubmapWorldDataValidationError(
        string submapId,
        bool isGenerated,
        GDictionary worldData
    )
    {
        if (worldData == null)
            return FormatMountedSubmapWorldDataError(submapId, "expected Dictionary.");

        if (!isGenerated)
        {
            return worldData.Count == 0
                ? ""
                : FormatMountedSubmapWorldDataError(
                    submapId,
                    "ungenerated submap requires empty world_data."
                );
        }
        if (worldData.Count == 0)
            return FormatMountedSubmapWorldDataError(
                submapId,
                "generated submap requires complete world_data."
            );

        string validationError = GetWorldDataValidationError(worldData);
        return string.IsNullOrEmpty(validationError)
            ? ""
            : FormatMountedSubmapWorldDataError(submapId, validationError);
    }

    private string get_mounted_submaps_validation_error(GDictionary submaps)
    {
        if (submaps == null)
            return "Corrupt save world_data: mounted_submaps must be a Dictionary.";
        foreach (KeyValuePair<Variant, Variant> submapEntry in submaps)
        {
            Variant submapKey = submapEntry.Key;
            Variant entryValue = submapEntry.Value;
            string keyText = submapKey.ToString();
            if (entryValue.VariantType != Variant.Type.Dictionary)
                return $"Corrupt save mounted_submaps[{keyText}]: expected Dictionary.";
            using GDictionary entry = entryValue.AsGodotDictionary();
            string[] required =
            {
                "submap_id",
                "display_name",
                "generation_config_path",
                "return_hint_text",
                "is_generated",
                "player_coord",
                "world_data",
            };
            if (!HasExactKeys(entry, required))
                return $"Corrupt save mounted_submaps[{keyText}]: fields must exactly match current schema.";
            string submapId = ReadString(entry, "submap_id", keyText);
            if (string.IsNullOrEmpty(submapId))
                return $"Corrupt save mounted_submaps[{keyText}]: submap_id is required.";
            foreach (
                string stringField in new[]
                {
                    "display_name",
                    "generation_config_path",
                    "return_hint_text",
                }
            )
            {
                if (!IsStringValue(entry[stringField]))
                    return $"Corrupt save mounted_submaps[{keyText}]: {stringField} must be a String.";
            }
            if (entry["is_generated"].VariantType != Variant.Type.Bool)
                return $"Corrupt save mounted_submaps[{keyText}]: is_generated must be a bool.";
            if (!IsNativeVector2I(entry["player_coord"]))
                return $"Corrupt save mounted_submaps[{keyText}]: player_coord must be a native Vector2i.";
            using GDictionary mountedWorldData =
                entry["world_data"].VariantType == Variant.Type.Dictionary
                    ? entry["world_data"].AsGodotDictionary()
                    : null;
            string worldDataError = GetMountedSubmapWorldDataValidationError(
                submapId,
                ReadBool(entry, "is_generated", false),
                mountedWorldData
            );
            if (!string.IsNullOrEmpty(worldDataError))
                return worldDataError;
        }
        return "";
    }

    public PartyState NormalizePartyState(PartyState partyState)
    {
        if (partyState == null)
            return new PartyState();

        using GodotProjectionLease<GDictionary> payloadLease =
            partyState.ToDictionaryLease("SaveSerializer.NormalizePartyState");
        GDictionary payload = payloadLease.Value;
        PartyState normalized =
            payload.Count > 0
                ? PartyState.FromDictionary(payload)
                : new PartyState();
        if (normalized == null)
            return new PartyState();

        return NormalizeParsedPartyState(normalized);
    }

    private PartyState NormalizeParsedPartyState(PartyState normalized)
    {
        if (normalized == null)
            return new PartyState();

        var livingMemberIds = new StringNameList();
        foreach (string key in normalized.member_states.GetSortedIdStrings())
        {
            StringName memberId = new(key);
            PartyMemberState memberState = normalized.GetMemberState(memberId);
            if (memberState == null || memberState.is_dead)
                continue;
            livingMemberIds.Add(memberId);
        }

        HashSet<string> seenIds = new();
        var activeMemberIds = new StringNameList();
        foreach (StringName memberId in normalized.active_member_ids)
        {
            if (
                !TryAddRosterMember(
                    normalized,
                    memberId,
                    seenIds,
                    activeMemberIds,
                    _max_active_member_count
                )
            )
                continue;
        }

        var reserveMemberIds = new StringNameList();
        foreach (StringName memberId in normalized.reserve_member_ids)
        {
            if (IsEmpty(memberId) || seenIds.Contains(memberId.ToString()))
                continue;
            PartyMemberState memberState = normalized.GetMemberState(memberId);
            if (memberState == null || memberState.is_dead)
                continue;
            seenIds.Add(memberId.ToString());
            reserveMemberIds.Add(memberId);
        }

        foreach (StringName memberId in livingMemberIds)
        {
            if (seenIds.Contains(memberId.ToString()))
                continue;
            if (activeMemberIds.Count < _max_active_member_count)
                activeMemberIds.Add(memberId);
            else
                reserveMemberIds.Add(memberId);
            seenIds.Add(memberId.ToString());
        }

        StringName mainCharacterMemberId = normalized.main_character_member_id;
        if (
            !IsEmpty(mainCharacterMemberId)
            && normalized.GetMemberState(mainCharacterMemberId) != null
        )
        {
            bool mainCharacterDead = normalized.IsMemberDead(mainCharacterMemberId);
            if (!mainCharacterDead)
            {
                reserveMemberIds.Remove(mainCharacterMemberId);
                if (!activeMemberIds.Contains(mainCharacterMemberId))
                {
                    if (
                        activeMemberIds.Count >= _max_active_member_count
                        && activeMemberIds.Count > 0
                    )
                    {
                        StringName demotedMemberId = activeMemberIds[activeMemberIds.Count - 1];
                        activeMemberIds.RemoveAt(activeMemberIds.Count - 1);
                        if (
                            !IsEmpty(demotedMemberId)
                            && demotedMemberId != mainCharacterMemberId
                            && !reserveMemberIds.Contains(demotedMemberId)
                        )
                        {
                            reserveMemberIds.Add(demotedMemberId);
                        }
                    }
                    activeMemberIds.Add(mainCharacterMemberId);
                }
            }
        }

        if (activeMemberIds.Count == 0 && livingMemberIds.Count > 0)
            activeMemberIds.Add(livingMemberIds[0]);
        if (
            IsEmpty(normalized.leader_member_id)
            || !activeMemberIds.Contains(normalized.leader_member_id)
        )
            normalized.leader_member_id =
                activeMemberIds.Count > 0 ? activeMemberIds[0] : new StringName("");

        normalized.active_member_ids = activeMemberIds;
        normalized.reserve_member_ids = reserveMemberIds;
        return normalized;
    }

    public bool IsValidSaveIdToken(string saveId)
    {
        if (string.IsNullOrEmpty(saveId))
            return false;
        if (saveId != saveId.StripEdges())
            return false;
        if (
            saveId == "."
            || saveId == ".."
            || saveId.Contains("..")
            || saveId.Contains("/")
            || saveId.Contains("\\")
        )
            return false;
        foreach (char ch in saveId)
        {
            bool ok = char.IsLetterOrDigit(ch) || ch == '_' || ch == '-';
            if (!ok)
                return false;
        }
        return true;
    }

    internal bool TryReadSaveIndexPayloadPlain(
        FileAccess indexFile,
        out Dictionary<string, object> payload
    )
    {
        payload = new Dictionary<string, object>(StringComparer.Ordinal);
        if (indexFile == null)
            return false;
        long fileLength = (long)indexFile.GetLength();
        if (fileLength <= 0)
            return true;

        byte[] rawBytes = indexFile.GetBuffer(fileLength);
        if (rawBytes.Length == 0)
            return true;
        if (rawBytes.Length < 8 || DetectTextSaveIndexBuffer(rawBytes))
            return false;

        indexFile.Seek(0);
        using Variant rawPayload = indexFile.GetVar(false);
        return RuntimePlainPayload.TryRestoreSaveVariantDictionary(
            rawPayload,
            "SaveSerializer.save_index",
            out payload
        );
    }

    internal GodotProjectionLease<GDictionary> BuildSaveIndexPayloadLease(
        IReadOnlyList<Dictionary<string, object>> entries
    )
    {
        List<object> serializedEntries = new();
        if (entries != null)
        {
            foreach (Dictionary<string, object> entry in entries)
            {
                if (
                    TryNormalizeSaveMetaPlain(
                        entry,
                        out Dictionary<string, object> normalizedEntry
                    )
                )
                {
                    serializedEntries.Add(normalizedEntry);
                }
            }
        }

        Dictionary<string, object> payload = new(StringComparer.Ordinal)
        {
            ["version"] = _save_index_version,
            ["saves"] = serializedEntries,
        };
        return RuntimePlainPayload.ProjectDictionaryLease(
            payload,
            "save-index-payload",
            LifetimeDomain.Request,
            "SaveSerializer.BuildSaveIndexPayloadLease",
            minimizeStrings: true
        );
    }

    public bool IsSaveIndexIntValue(int value)
    {
        return true;
    }

    public bool IsSaveIndexFloatValue(double value)
    {
        return false;
    }

    public bool IsSaveIndexStringValue(string value)
    {
        return false;
    }

    public bool IsSaveIndexBoolValue(bool value)
    {
        return false;
    }

    public bool IsTextSaveIndexBuffer(byte[] rawBytes)
    {
        return DetectTextSaveIndexBuffer(rawBytes ?? System.Array.Empty<byte>());
    }

    private Dictionary<string, object> BuildWorldStatePlain(
        IReadOnlyDictionary<string, object> worldData,
        Vector2I playerCoord,
        string playerFactionId
        )
    {
        WorldRuntimeData runtimeData;
        using GodotProjectionLease<GDictionary> validationLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                worldData,
                "save-world-data-validation",
                LifetimeDomain.Request,
                "SaveSerializer.BuildWorldStatePlain.validation"
            );
        GDictionary validationPayload = validationLease.Value;
        runtimeData = WorldRuntimeData.FromDictionary(validationPayload);
        if (runtimeData == null)
        {
            throw new InvalidOperationException(
                "Corrupt save world_data: typed world runtime data parse failed."
            );
        }
        string validationError = GetWorldDataValidationError(validationPayload);
        if (!string.IsNullOrEmpty(validationError))
            throw new InvalidOperationException(validationError);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["world_data"] = runtimeData.BuildSaveSnapshotPlain(),
            ["player_coord"] = playerCoord,
            ["player_faction_id"] = playerFactionId,
        };
    }

    internal bool TryNormalizeWorldDataPlain(
        IReadOnlyDictionary<string, object> worldData,
        out Dictionary<string, object> normalized
    )
    {
        normalized = new Dictionary<string, object>(StringComparer.Ordinal);
        if (worldData == null)
            return false;
        using GodotProjectionLease<GDictionary> validationLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                worldData,
                "save-world-data-decode",
                LifetimeDomain.Request,
                "SaveSerializer.TryNormalizeWorldDataPlain"
            );
        GDictionary validationPayload = validationLease.Value;
        if (!string.IsNullOrEmpty(GetWorldDataValidationError(validationPayload)))
            return false;
        WorldRuntimeData runtimeData = WorldRuntimeData.FromDictionary(validationPayload);
        if (runtimeData == null)
            return false;
        normalized = runtimeData.BuildSaveSnapshotPlain();
        return true;
    }

    internal bool TryNormalizeWorldDataPlain(
        GDictionary worldData,
        out Dictionary<string, object> normalized
    )
    {
        normalized = new Dictionary<string, object>(StringComparer.Ordinal);
        if (worldData == null)
            return false;

        Dictionary<string, object> plain;
        try
        {
            plain = RuntimePlainPayload.NormalizeDictionaryStrict(
                worldData,
                "SaveSerializer.TryNormalizeWorldDataPlain.input"
            );
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        return TryNormalizeWorldDataPlain(plain, out normalized);
    }

    internal Dictionary<string, object> SerializeWorldDataPlain(
        IReadOnlyDictionary<string, object> worldData
    )
    {
        if (TryNormalizeWorldDataPlain(worldData, out Dictionary<string, object> serialized))
            return serialized;
        throw new InvalidOperationException("Corrupt save world_data: validation failed.");
    }

    private static bool HasExactPlainKeys(
        IReadOnlyDictionary<string, object> values,
        IReadOnlyList<string> requiredKeys
    )
    {
        if (values == null || requiredKeys == null || values.Count != requiredKeys.Count)
            return false;
        for (int index = 0; index < requiredKeys.Count; index++)
        {
            if (!values.ContainsKey(requiredKeys[index]))
                return false;
        }
        return true;
    }

    private static bool TryReadPlainDictionary(
        IReadOnlyDictionary<string, object> values,
        string key,
        out IReadOnlyDictionary<string, object> dictionary
    )
    {
        dictionary = null;
        if (values == null || !values.TryGetValue(key, out object value))
            return false;
        dictionary = value as IReadOnlyDictionary<string, object>;
        return dictionary != null;
    }

    private static bool TryReadPlainString(
        IReadOnlyDictionary<string, object> values,
        string key,
        out string text
    )
    {
        text = "";
        if (
            values == null
            || !values.TryGetValue(key, out object value)
            || value is not string stringValue
        )
        {
            return false;
        }
        text = stringValue;
        return true;
    }

    private static bool TryReadPlainInt(
        IReadOnlyDictionary<string, object> values,
        string key,
        out int number
    )
    {
        number = 0;
        if (values == null || !values.TryGetValue(key, out object value))
            return false;
        long candidate = value switch
        {
            byte byteValue => byteValue,
            short shortValue => shortValue,
            int intValue => intValue,
            long longValue => longValue,
            _ => long.MinValue,
        };
        if (candidate < int.MinValue || candidate > int.MaxValue)
            return false;
        number = (int)candidate;
        return true;
    }

    private static string ReadPlainString(
        IReadOnlyDictionary<string, object> values,
        string key
    ) =>
        values != null && values.TryGetValue(key, out object value)
            ? value switch
            {
                string stringValue => stringValue,
                StringName stringNameValue => stringNameValue.ToString(),
                _ => value?.ToString() ?? "",
            }
            : "";

    private static Dictionary<string, object> SerializePartyStatePlain(PartyState partyState)
    {
        return partyState?.BuildSaveSnapshotPlain()
            ?? new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static Dictionary<string, object> BuildMetaPayloadPlain(int savedAtUnixTime) =>
        new(StringComparer.Ordinal)
        {
            ["saved_at_unix_time"] = savedAtUnixTime,
            ["save_format"] = SaveFormat,
        };

    private static bool TryAddRosterMember(
        PartyState partyState,
        StringName memberId,
        HashSet<string> seenIds,
        StringNameList target,
        int maxCount
    )
    {
        if (IsEmpty(memberId) || seenIds.Contains(memberId.ToString()))
            return false;
        PartyMemberState memberState = partyState.GetMemberState(memberId);
        if (memberState == null || memberState.is_dead)
            return false;
        if (target.Count >= maxCount)
            return false;
        seenIds.Add(memberId.ToString());
        target.Add(memberId);
        return true;
    }

    private static string GetSubmapReturnStackValidationError(GArray stackValues)
    {
        if (stackValues == null)
            return "Corrupt save world_data.submap_return_stack: expected Array.";
        int index = 0;
        foreach (var entryValue in stackValues)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                return $"Corrupt save world_data.submap_return_stack[{index}]: expected Dictionary.";
            using GDictionary entry = entryValue.AsGodotDictionary();
            if (!HasExactKeys(entry, new[] { "map_id", "coord" }))
                return $"Corrupt save world_data.submap_return_stack[{index}]: fields must exactly match current schema.";
            if (!IsStringValue(entry["map_id"]))
                return $"Corrupt save world_data.submap_return_stack[{index}]: map_id must be a String.";
            if (!IsNativeVector2I(entry["coord"]))
                return $"Corrupt save world_data.submap_return_stack[{index}]: coord must be a native Vector2i.";
            index++;
        }
        return "";
    }

    private static string GetWorldEventsValidationError(GArray eventValues)
    {
        if (eventValues == null)
            return "Corrupt save world_data.world_events: expected Array.";
        string[] required =
        {
            "event_id",
            "display_name",
            "world_coord",
            "event_type",
            "target_submap_id",
            "discovery_condition_id",
            "prompt_title",
            "prompt_text",
            "is_discovered",
        };
        int index = 0;
        foreach (var eventValue in eventValues)
        {
            if (eventValue.VariantType != Variant.Type.Dictionary)
                return $"Corrupt save world_data.world_events[{index}]: expected Dictionary.";
            using GDictionary eventData = eventValue.AsGodotDictionary();
            if (!HasExactKeys(eventData, required))
                return $"Corrupt save world_data.world_events[{index}]: fields must exactly match current schema.";
            foreach (
                string stringField in new[]
                {
                    "event_id",
                    "display_name",
                    "event_type",
                    "target_submap_id",
                    "discovery_condition_id",
                    "prompt_title",
                    "prompt_text",
                }
            )
            {
                if (!IsStringValue(eventData[stringField]))
                    return $"Corrupt save world_data.world_events[{index}]: {stringField} must be a String.";
            }
            if (!IsNativeVector2I(eventData["world_coord"]))
                return $"Corrupt save world_data.world_events[{index}]: world_coord must be a native Vector2i.";
            if (eventData["is_discovered"].VariantType != Variant.Type.Bool)
                return $"Corrupt save world_data.world_events[{index}]: is_discovered must be a bool.";
            index++;
        }
        return "";
    }

    private static string GetWorldResourceNodesValidationError(GArray resourceNodeValues)
    {
        if (resourceNodeValues == null)
            return "Corrupt save world_data.resource_nodes: expected Array.";
        string[] required =
        {
            "node_id",
            "node_kind",
            "display_name",
            "world_coord",
            "yield_item_id",
            "source_settlement_id",
            "max_charges",
            "remaining_charges",
        };
        int index = 0;
        foreach (var resourceNodeValue in resourceNodeValues)
        {
            if (resourceNodeValue.VariantType != Variant.Type.Dictionary)
                return $"Corrupt save world_data.resource_nodes[{index}]: expected Dictionary.";
            using GDictionary resourceNode = resourceNodeValue.AsGodotDictionary();
            if (!HasExactKeys(resourceNode, required))
                return $"Corrupt save world_data.resource_nodes[{index}]: fields must exactly match current schema.";
            foreach (
                string stringField in new[]
                {
                    "node_id",
                    "node_kind",
                    "display_name",
                    "yield_item_id",
                    "source_settlement_id",
                }
            )
            {
                if (!IsStringValue(resourceNode[stringField]))
                    return $"Corrupt save world_data.resource_nodes[{index}]: {stringField} must be a String.";
            }
            string nodeKind = resourceNode["node_kind"].AsString();
            if (!WorldMapResourceNodeData.IsKnownKind(nodeKind))
                return $"Corrupt save world_data.resource_nodes[{index}]: node_kind is unknown.";
            if (!IsNativeVector2I(resourceNode["world_coord"]))
                return $"Corrupt save world_data.resource_nodes[{index}]: world_coord must be a native Vector2i.";
            if (
                resourceNode["yield_item_id"].AsString()
                != WorldMapResourceNodeData.DefaultYieldItemId(nodeKind)
            )
                return $"Corrupt save world_data.resource_nodes[{index}]: yield_item_id does not match node_kind.";
            if (
                resourceNode["max_charges"].VariantType != Variant.Type.Int
                || resourceNode["max_charges"].AsInt32() <= 0
            )
                return $"Corrupt save world_data.resource_nodes[{index}]: max_charges must be a positive int.";
            if (
                resourceNode["remaining_charges"].VariantType != Variant.Type.Int
                || resourceNode["remaining_charges"].AsInt32() < 0
                || resourceNode["remaining_charges"].AsInt32()
                    > resourceNode["max_charges"].AsInt32()
            )
                return $"Corrupt save world_data.resource_nodes[{index}]: remaining_charges must be between 0 and max_charges.";
            index++;
        }
        return "";
    }

    private static string GetSettlementsValidationError(GArray settlementValues)
    {
        if (settlementValues == null)
            return "Corrupt save world_data.settlements: expected Array.";
        string[] required =
        {
            "entity_id",
            "template_id",
            "settlement_id",
            "display_name",
            "tier",
            "tier_name",
            "faction_id",
            "origin",
            "footprint_size",
            "facilities",
            "service_npcs",
            "available_services",
            "is_player_start",
            "settlement_state",
        };
        int index = 0;
        foreach (var settlementValue in settlementValues)
        {
            if (settlementValue.VariantType != Variant.Type.Dictionary)
                return $"Corrupt save world_data.settlements[{index}]: expected Dictionary.";
            using GDictionary settlementData = settlementValue.AsGodotDictionary();
            if (!HasExactKeys(settlementData, required))
                return $"Corrupt save world_data.settlements[{index}]: fields must exactly match current schema.";
            foreach (
                string stringField in new[]
                {
                    "entity_id",
                    "template_id",
                    "settlement_id",
                    "display_name",
                    "tier_name",
                    "faction_id",
                }
            )
            {
                if (!IsStringValue(settlementData[stringField]))
                    return $"Corrupt save world_data.settlements[{index}]: {stringField} must be a String.";
            }
            if (
                settlementData["tier"].VariantType != Variant.Type.Int
                || settlementData["tier"].AsInt32() < 0
            )
                return $"Corrupt save world_data.settlements[{index}]: tier must be a non-negative int.";
            foreach (string coordField in new[] { "origin", "footprint_size" })
            {
                if (!IsNativeVector2I(settlementData[coordField]))
                    return $"Corrupt save world_data.settlements[{index}]: {coordField} must be a native Vector2i.";
            }
            foreach (
                string arrayField in new[] { "facilities", "service_npcs", "available_services" }
            )
            {
                if (settlementData[arrayField].VariantType != Variant.Type.Array)
                    return $"Corrupt save world_data.settlements[{index}]: {arrayField} must be an Array.";
            }
            if (settlementData["is_player_start"].VariantType != Variant.Type.Bool)
                return $"Corrupt save world_data.settlements[{index}]: is_player_start must be a bool.";
            if (settlementData["settlement_state"].VariantType != Variant.Type.Dictionary)
                return $"Corrupt save world_data.settlements[{index}]: settlement_state must be a Dictionary.";
            using GDictionary settlementState =
                settlementData["settlement_state"].AsGodotDictionary();
            if (
                !WorldMapSettlementStateData.TryFromDictionary(
                    settlementState,
                    out _,
                    out string settlementStateError
                )
            )
            {
                return $"Corrupt save world_data.settlements[{index}]: {settlementStateError}";
            }
            index++;
        }
        return "";
    }

    private static bool DetectTextSaveIndexBuffer(byte[] rawBytes)
    {
        bool sawContent = false;
        bool allPrintableText = true;
        foreach (byte byteValue in rawBytes)
        {
            int byteInt = byteValue;
            if (byteInt == 9 || byteInt == 10 || byteInt == 13 || byteInt == 32)
                continue;
            if (!sawContent)
            {
                if (byteInt == 123 || byteInt == 91)
                    return true;
                sawContent = true;
            }
            if (byteInt < 32 || byteInt > 126)
                allPrintableText = false;
        }
        return sawContent && allPrintableText;
    }

    private static string FormatMountedSubmapWorldDataError(string submapId, string message)
    {
        string detail = message ?? "";
        string prefix = "Corrupt save world_data: ";
        if (detail.StartsWith(prefix))
            detail = detail[prefix.Length..];
        return $"Corrupt save mounted_submaps[{submapId}].world_data: {detail}";
    }

    private static bool IsStringValue(Variant value) => value.VariantType == Variant.Type.String;

    private static bool IsNativeVector2I(Variant value) =>
        value.VariantType == Variant.Type.Vector2I;

    private static bool HasExactKeys(GDictionary data, string[] requiredKeys)
    {
        if (data == null || data.Count != requiredKeys.Length)
            return false;
        foreach (string requiredKey in requiredKeys)
        {
            if (!data.ContainsKey(requiredKey))
                return false;
        }
        return true;
    }

    private static bool HasRequiredAndAllowedKeys(
        GDictionary data,
        string[] requiredKeys,
        string[] optionalKeys
    )
    {
        if (data == null)
            return false;
        HashSet<string> allowedKeys = new();
        foreach (string requiredKey in requiredKeys)
        {
            if (!data.ContainsKey(requiredKey))
                return false;
            allowedKeys.Add(requiredKey);
        }
        foreach (string optionalKey in optionalKeys)
            allowedKeys.Add(optionalKey);
        foreach (KeyValuePair<Variant, Variant> entry in data)
        {
            Variant rawKey = entry.Key;
            if (rawKey.VariantType != Variant.Type.String)
                return false;
            if (!allowedKeys.Contains(rawKey.AsString()))
                return false;
        }
        return true;
    }

    private static bool TryRead(GDictionary source, object key, out Variant value)
    {
        if (source == null || key == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return true;
        }
        value = default;
        return false;
    }

    private static GArray ReadArray(GDictionary source, object key)
    {
        if (!TryRead(source, key, out Variant value))
            return new GArray();
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static GDictionary ReadDictionary(GDictionary source, object key)
    {
        if (!TryRead(source, key, out Variant value))
            return new GDictionary();
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static string ReadString(GDictionary source, object key, string fallback = "")
    {
        if (!TryRead(source, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            _ => fallback,
        };
    }

    private static int ReadInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryRead(source, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool ReadBool(GDictionary source, object key, bool fallback = false)
    {
        if (!TryRead(source, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value.ToString().Length == 0;
    }

}
