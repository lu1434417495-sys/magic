using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;

public sealed class SaveSerializer
{
    private const string WorldMapSeedKey = "map_seed";
    private const string WorldEquipmentInstanceSerialKey = "next_equipment_instance_serial";
    private const string SaveFormat = "multi_save_total_save";

    private int _save_version = 10;
    private int _save_index_version = 3;
    private int _max_active_member_count = 4;

    public void Setup(int saveVersion, int saveIndexVersion, int maxActiveMemberCount)
    {
        _save_version = saveVersion;
        _save_index_version = saveIndexVersion;
        _max_active_member_count = maxActiveMemberCount;
    }

    public GDictionary BuildSavePayload(
        string activeSaveId,
        string generationConfigPath,
        GDictionary activeSaveMeta,
        GDictionary worldData,
        Vector2I playerCoord,
        string playerFactionId,
        PartyState partyState,
        int savedAtUnixTime
    )
    {
        return MinimizeSavePayloadStrings(
            new GDictionary
            {
                ["version"] = _save_version,
                ["save_id"] = activeSaveId,
                ["generation_config_path"] = generationConfigPath,
                ["world_state"] = BuildWorldStatePayload(
                    worldData,
                    playerCoord,
                    playerFactionId
                ),
                ["party_state"] = SerializePartyState(partyState),
                ["meta"] = BuildMetaPayload(savedAtUnixTime),
                ["save_slot_meta"] = activeSaveMeta?.Duplicate(true) ?? new GDictionary(),
            }
        );
    }

    public GDictionary BuildWorldStatePayload(
        GDictionary worldData,
        Vector2I playerCoord,
        string playerFactionId
    )
    {
        return new GDictionary
        {
            ["world_data"] = SerializeWorldData(worldData ?? new GDictionary()),
            ["player_coord"] = playerCoord,
            ["player_faction_id"] = playerFactionId,
        };
    }

    internal GDictionary BuildWorldStatePayload(
        WorldRuntimeData worldData,
        Vector2I playerCoord,
        string playerFactionId
    )
    {
        return BuildWorldStatePayload(worldData?.ToDictionary() ?? new GDictionary(), playerCoord, playerFactionId);
    }

    public GDictionary BuildMetaPayload(int savedAtUnixTime)
    {
        return new GDictionary
        {
            ["saved_at_unix_time"] = savedAtUnixTime,
            ["save_format"] = SaveFormat,
        };
    }

    public GDictionary DecodePayload(
        GDictionary payload,
        string generationConfigPath,
        WorldMapGenerationConfig generationConfig,
        GDictionary saveMeta
    )
    {
        GDictionary payloadData = RestoreMinimizedSavePayloadStrings(
            payload ?? new GDictionary()
        );
        GDictionary normalizedRequestedMeta = NormalizeSaveMeta(saveMeta ?? new GDictionary());
        if (payloadData.Count == 0 || normalizedRequestedMeta.Count == 0)
            return ErrorResult();
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
        if (!HasExactKeys(payloadData, requiredPayloadKeys))
            return ErrorResult();
        if (
            !payloadData.ContainsKey("version")
            || payloadData["version"].VariantType != Variant.Type.Int
            || payloadData["version"].AsInt32() != _save_version
        )
            return ErrorResult();
        if (
            !payloadData.ContainsKey("save_id")
            || payloadData["save_id"].VariantType != Variant.Type.String
        )
            return ErrorResult();
        if (
            !payloadData.ContainsKey("generation_config_path")
            || payloadData["generation_config_path"].VariantType != Variant.Type.String
        )
            return ErrorResult();
        if (payloadData["generation_config_path"].AsString() != generationConfigPath)
            return ErrorResult();
        if (
            payloadData["save_id"].AsString()
            != ReadString(normalizedRequestedMeta, "save_id")
        )
            return ErrorResult();
        if (
            !payloadData.ContainsKey("world_state")
            || payloadData["world_state"].VariantType != Variant.Type.Dictionary
        )
            return ErrorResult();
        if (
            !payloadData.ContainsKey("party_state")
            || payloadData["party_state"].VariantType != Variant.Type.Dictionary
        )
            return ErrorResult();
        if (
            !payloadData.ContainsKey("meta")
            || payloadData["meta"].VariantType != Variant.Type.Dictionary
        )
            return ErrorResult();
        if (
            !payloadData.ContainsKey("save_slot_meta")
            || payloadData["save_slot_meta"].VariantType != Variant.Type.Dictionary
        )
            return ErrorResult();

        GDictionary worldState = payloadData["world_state"].AsGodotDictionary();
        if (!HasExactKeys(worldState, new[] { "world_data", "player_coord", "player_faction_id" }))
            return ErrorResult();
        if (worldState["world_data"].VariantType != Variant.Type.Dictionary)
            return ErrorResult();
        GDictionary rawWorldData = worldState["world_data"].AsGodotDictionary();
        if (!string.IsNullOrEmpty(GetWorldDataValidationError(rawWorldData)))
            return ErrorResult();
        WorldRuntimeData rawRuntimeData = WorldRuntimeData.FromDictionary(rawWorldData);
        if (rawRuntimeData == null)
            return ErrorResult();
        GDictionary worldData = NormalizeWorldData(rawRuntimeData);
        if (worldData.Count == 0)
            return ErrorResult();
        if (!IsSupportedVector2I(worldState["player_coord"]))
            return ErrorResult();
        if (!IsStringValue(worldState["player_faction_id"]))
            return ErrorResult();
        string playerFactionId = worldState["player_faction_id"].AsString().StripEdges();
        if (string.IsNullOrEmpty(playerFactionId))
            return ErrorResult();

        GDictionary payloadMeta = payloadData["meta"].AsGodotDictionary();
        if (!HasExactKeys(payloadMeta, new[] { "saved_at_unix_time", "save_format" }))
            return ErrorResult();
        if (
            payloadMeta["saved_at_unix_time"].VariantType != Variant.Type.Int
            || payloadMeta["save_format"].VariantType != Variant.Type.String
        )
            return ErrorResult();
        if (payloadMeta["save_format"].AsString() != SaveFormat)
            return ErrorResult();

        GDictionary normalizedMeta = NormalizeSaveMeta(
            payloadData["save_slot_meta"].AsGodotDictionary()
        );
        if (normalizedMeta.Count == 0)
            return ErrorResult();
        if (ReadString(normalizedMeta, "save_id") != payloadData["save_id"].AsString())
            return ErrorResult();
        if (
            ReadString(normalizedMeta, "save_id")
            != ReadString(normalizedRequestedMeta, "save_id")
        )
            return ErrorResult();
        if (ReadString(normalizedMeta, "generation_config_path") != generationConfigPath)
            return ErrorResult();
        if (
            ReadString(normalizedRequestedMeta, "generation_config_path")
            != generationConfigPath
        )
            return ErrorResult();

        PartyState partyState = PartyState.FromDictionary(
            payloadData["party_state"].AsGodotDictionary()
        );
        if (partyState == null)
            return ErrorResult();

        return new GDictionary
        {
            ["error"] = (int)Error.Ok,
            ["active_save_id"] = payloadData["save_id"].AsString(),
            ["active_save_meta"] = normalizedMeta,
            ["generation_config_path"] = generationConfigPath,
            ["generation_config"] = generationConfig,
            ["world_data"] = worldData,
            ["player_coord"] = read_vector2i(
                worldState.ContainsKey("player_coord")
                    ? worldState["player_coord"]
                    : Variant.From(Vector2I.Zero),
                Vector2I.Zero
            ),
            ["player_faction_id"] = playerFactionId,
            ["party_state"] = NormalizePartyState(partyState),
        };
    }

    public GDictionary BuildSaveMeta(
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
        return NormalizeSaveMeta(
            new GDictionary
            {
                ["save_id"] = saveId,
                ["display_name"] = string.IsNullOrEmpty(displayName) ? saveId : displayName,
                ["world_preset_id"] = presetId.ToString(),
                ["world_preset_name"] = presetName,
                ["generation_config_path"] = generationConfigPath,
                ["world_size_cells"] = worldSizeCells,
                ["created_at_unix_time"] = createdAtUnixTime,
                ["updated_at_unix_time"] = updatedAtUnixTime,
            }
        );
    }

    public GDictionary ExtractSaveMetaFromPayload(GDictionary payload)
    {
        GDictionary payloadData = RestoreMinimizedSavePayloadStrings(
            payload ?? new GDictionary()
        );
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
        if (!HasExactKeys(payloadData, requiredPayloadKeys))
            return new GDictionary();
        if (payloadData["save_id"].VariantType != Variant.Type.String)
            return new GDictionary();
        if (
            payloadData["version"].VariantType != Variant.Type.Int
            || payloadData["version"].AsInt32() != _save_version
        )
            return new GDictionary();
        if (payloadData["generation_config_path"].VariantType != Variant.Type.String)
            return new GDictionary();
        string saveId = payloadData["save_id"].AsString().StripEdges();
        string generationConfigPath = payloadData["generation_config_path"].AsString().StripEdges();
        if (string.IsNullOrEmpty(saveId) || string.IsNullOrEmpty(generationConfigPath))
            return new GDictionary();
        if (
            !payloadData.ContainsKey("save_slot_meta")
            || payloadData["save_slot_meta"].VariantType != Variant.Type.Dictionary
        )
            return new GDictionary();
        GDictionary normalizedMeta = NormalizeSaveMeta(
            payloadData["save_slot_meta"].AsGodotDictionary()
        );
        if (normalizedMeta.Count == 0)
            return new GDictionary();
        if (ReadString(normalizedMeta, "save_id").StripEdges() != saveId)
            return new GDictionary();
        if (
            ReadString(normalizedMeta, "generation_config_path").StripEdges()
            != generationConfigPath
        )
            return new GDictionary();
        return normalizedMeta;
    }

    public GDictionary NormalizeSaveMeta(GDictionary rawMeta)
    {
        if (rawMeta == null)
            return new GDictionary();
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
        foreach (string key in required)
        {
            if (!rawMeta.ContainsKey(key))
                return new GDictionary();
        }
        if (!HasExactKeys(rawMeta, required))
            return new GDictionary();
        string[] stringKeys =
        {
            "save_id",
            "display_name",
            "world_preset_id",
            "world_preset_name",
            "generation_config_path",
        };
        foreach (string key in stringKeys)
        {
            if (rawMeta[key].VariantType != Variant.Type.String)
                return new GDictionary();
        }

        if (
            rawMeta["created_at_unix_time"].VariantType != Variant.Type.Int
            || rawMeta["updated_at_unix_time"].VariantType != Variant.Type.Int
        )
            return new GDictionary();

        string saveId = rawMeta["save_id"].AsString();
        if (!IsValidSaveIdToken(saveId))
            return new GDictionary();
        string displayName = rawMeta["display_name"].AsString().StripEdges();
        if (displayName.Length == 0)
            return new GDictionary();
        string generationConfigPath = rawMeta["generation_config_path"].AsString().StripEdges();
        if (generationConfigPath.Length == 0)
            return new GDictionary();
        string worldPresetName = rawMeta["world_preset_name"].AsString().StripEdges();
        if (worldPresetName.Length == 0)
            return new GDictionary();

        if (!IsSupportedVector2I(rawMeta["world_size_cells"]))
            return new GDictionary();
        Vector2I worldSizeCells = read_vector2i(rawMeta["world_size_cells"], Vector2I.Zero);
        if (worldSizeCells.X <= 0 || worldSizeCells.Y <= 0)
            return new GDictionary();

        int createdAt = rawMeta["created_at_unix_time"].AsInt32();
        int updatedAt = rawMeta["updated_at_unix_time"].AsInt32();
        if (createdAt <= 0 || updatedAt <= 0)
            return new GDictionary();

        return new GDictionary
        {
            ["save_id"] = saveId,
            ["display_name"] = displayName,
            ["world_preset_id"] = rawMeta["world_preset_id"].AsString(),
            ["world_preset_name"] = worldPresetName,
            ["generation_config_path"] = generationConfigPath,
            ["world_size_cells"] = worldSizeCells,
            ["created_at_unix_time"] = createdAt,
            ["updated_at_unix_time"] = updatedAt,
        };
    }

    public GDictionary NormalizeWorldData(GDictionary worldData)
    {
        WorldRuntimeData runtimeData = WorldRuntimeData.FromDictionary(worldData);
        if (runtimeData == null)
        {
            return new GDictionary();
        }
        string validationError = GetWorldDataValidationError(worldData);
        if (!string.IsNullOrEmpty(validationError))
        {
            return new GDictionary();
        }
        GDictionary normalized = worldData.Duplicate(true);
        normalized[WorldMapSeedKey] = (long)worldData[WorldMapSeedKey];
        normalized["world_step"] = worldData["world_step"].AsInt32();
        normalized[WorldEquipmentInstanceSerialKey] = worldData[WorldEquipmentInstanceSerialKey]
            .AsInt32();
        normalized["active_submap_id"] = worldData["active_submap_id"].AsString();
        normalized["encounter_anchors"] = NormalizeEncounterAnchors(
            ReadArray(worldData, "encounter_anchors")
        );
        return normalized;
    }

    internal GDictionary NormalizeWorldData(WorldRuntimeData worldData)
    {
        return NormalizeWorldData(worldData?.ToDictionary() ?? new GDictionary());
    }

    public GDictionary SerializeWorldData(GDictionary worldData)
    {
        WorldRuntimeData runtimeData = WorldRuntimeData.FromDictionary(worldData);
        if (runtimeData == null)
        {
            throw new InvalidOperationException("Corrupt save world_data: typed world runtime data parse failed.");
        }
        string validationError = GetWorldDataValidationError(worldData);
        if (!string.IsNullOrEmpty(validationError))
        {
            throw new InvalidOperationException(validationError);
        }
        GDictionary serialized = worldData.Duplicate(true);
        serialized[WorldMapSeedKey] = (long)worldData[WorldMapSeedKey];
        serialized[WorldEquipmentInstanceSerialKey] = worldData[WorldEquipmentInstanceSerialKey]
            .AsInt32();
        serialized["active_submap_id"] = worldData["active_submap_id"].AsString();
        serialized["encounter_anchors"] = SerializeObjectOrDictionaryArray(
            ReadArray(worldData, "encounter_anchors")
        );
        return serialized;
    }

    internal GDictionary SerializeWorldData(WorldRuntimeData worldData)
    {
        return SerializeWorldData(worldData?.ToDictionary() ?? new GDictionary());
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
        return get_mounted_submaps_validation_error(
            ReadDictionary(worldData, "mounted_submaps")
        );
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
            && !IsSupportedVector2I(worldData["player_start_coord"])
        )
            return "Corrupt save world_data: player_start_coord must be a Vector2i payload.";
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
        if (
            worldData.ContainsKey("fog_states")
            && worldData["fog_states"].VariantType != Variant.Type.Dictionary
        )
            return "Corrupt save world_data: fog_states must be a Dictionary.";
        return "";
    }

    public string GetWorldDataNestedSchemaValidationError(GDictionary worldData)
    {
        if (worldData == null)
            return "Corrupt save world_data: expected Dictionary.";
        string returnStackError = GetSubmapReturnStackValidationError(
            ReadArray(worldData, "submap_return_stack")
        );
        if (!string.IsNullOrEmpty(returnStackError))
            return returnStackError;
        string settlementError = GetSettlementsValidationError(
            ReadArray(worldData, "settlements")
        );
        if (!string.IsNullOrEmpty(settlementError))
            return settlementError;
        string eventError = GetWorldEventsValidationError(
            ReadArray(worldData, "world_events")
        );
        if (!string.IsNullOrEmpty(eventError))
            return eventError;
        if (
            worldData.ContainsKey("encounter_anchors")
            && worldData["encounter_anchors"].VariantType != Variant.Type.Array
        )
            return "Corrupt save world_data.encounter_anchors: expected Array.";
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

    private string get_mounted_submaps_validation_error(Variant submapsValue)
    {
        if (!TryRawDictionary(submapsValue, out GDictionary submaps))
            return "Corrupt save world_data: mounted_submaps must be a Dictionary.";
        foreach (var submapKey in submaps.Keys)
        {
            var entryValue = submaps[submapKey];
            string keyText = submapKey.ToString();
            if (entryValue.VariantType != Variant.Type.Dictionary)
                return $"Corrupt save mounted_submaps[{keyText}]: expected Dictionary.";
            GDictionary entry = entryValue.AsGodotDictionary();
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
            if (!IsSupportedVector2I(entry["player_coord"]))
                return $"Corrupt save mounted_submaps[{keyText}]: player_coord must be a Vector2i payload.";
            GDictionary mountedWorldData =
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

        GDictionary payload = partyState.ToDictionary();
        PartyState normalized =
            payload.Count > 0
                ? PartyState.FromDictionary(payload)
                : new PartyState();
        if (normalized == null)
            return new PartyState();

        var livingMemberIds = new Godot.Collections.Array<StringName>();
        foreach (string key in normalized.member_states.GetSortedIdStrings())
        {
            StringName memberId = new(key);
            PartyMemberState memberState = normalized.GetMemberState(memberId);
            if (memberState == null || memberState.is_dead)
                continue;
            livingMemberIds.Add(memberId);
        }

        HashSet<string> seenIds = new();
        var activeMemberIds = new Godot.Collections.Array<StringName>();
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

        var reserveMemberIds = new Godot.Collections.Array<StringName>();
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

        normalized.active_member_ids = ProgressionDataUtils.to_string_name_array(
            Variant.From(activeMemberIds)
        );
        normalized.reserve_member_ids = ProgressionDataUtils.to_string_name_array(
            Variant.From(reserveMemberIds)
        );
        return normalized;
    }

    private Vector2I read_vector2i(Variant value)
    {
        return read_vector2i(value, Vector2I.Zero);
    }

    private Vector2I read_vector2i(Variant value, Vector2I fallback)
    {
        if (value.VariantType == Variant.Type.Vector2I)
            return value.AsVector2I();
        if (value.VariantType == Variant.Type.Dictionary)
        {
            return ReadVector2IDictionary(value.AsGodotDictionary(), fallback);
        }
        return fallback;
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

    internal GDictionary ReadSaveIndexPayload(FileAccess indexFile)
    {
        if (indexFile == null)
            return null;
        long fileLength = (long)indexFile.GetLength();
        if (fileLength <= 0)
            return new GDictionary();

        byte[] rawBytes = indexFile.GetBuffer(fileLength);
        if (rawBytes.Length == 0)
            return new GDictionary();
        if (rawBytes.Length < 8 || DetectTextSaveIndexBuffer(rawBytes))
            return null;

        indexFile.Seek(0);
        using Variant rawPayload = indexFile.GetVar(false);
        if (rawPayload.VariantType != Variant.Type.Dictionary)
            return null;
        return RestoreMinimizedSavePayloadStrings(rawPayload.AsGodotDictionary());
    }

    public GDictionaryArray SerializeSaveIndexEntries(GDictionaryArray entries)
    {
        GDictionaryArray serializedEntries = new();
        if (entries == null)
            return serializedEntries;
        foreach (GDictionary entry in entries)
        {
            GDictionary normalizedEntry = NormalizeSaveMeta(entry);
            if (normalizedEntry.Count == 0)
                continue;
            serializedEntries.Add(normalizedEntry.Duplicate(true));
        }
        return serializedEntries;
    }

    public GDictionary BuildSaveIndexPayload(GDictionaryArray entries)
    {
        return MinimizeSavePayloadStrings(
            new GDictionary
            {
                ["version"] = _save_index_version,
                ["saves"] = SerializeSaveIndexEntries(entries ?? new GDictionaryArray()),
            }
        );
    }

    public GDictionary DeserializeSaveIndexEntry(GDictionary rawEntry)
    {
        if (rawEntry == null || rawEntry.Count == 0)
            return new GDictionary();
        return NormalizeSaveMeta(rawEntry);
    }

    public bool IsSaveIndexIntValue(int value)
    {
        return IsSaveIndexIntegerValue(Variant.From(value));
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

    internal bool IsSaveIndexIntegerValue(Variant rawValue)
    {
        return rawValue.VariantType == Variant.Type.Int;
    }

    public bool IsTextSaveIndexBuffer(byte[] rawBytes)
    {
        return DetectTextSaveIndexBuffer(rawBytes ?? System.Array.Empty<byte>());
    }

    public GDictionaryArray MergeSaveIndexEntries(
        GDictionaryArray primaryEntries,
        GDictionaryArray fallbackEntries
    )
    {
        GDictionaryArray mergedEntries = DuplicateSaveMetaEntries(primaryEntries);
        if (fallbackEntries == null)
            return SortSaveMetaEntries(mergedEntries);
        foreach (GDictionary fallbackEntry in fallbackEntries)
            mergedEntries = UpsertSaveMeta(mergedEntries, fallbackEntry);
        return SortSaveMetaEntries(mergedEntries);
    }

    public GDictionaryArray UpsertSaveMeta(GDictionaryArray entries, GDictionary saveMeta)
    {
        GDictionary normalizedMeta = NormalizeSaveMeta(saveMeta);
        if (normalizedMeta.Count == 0)
            return SortSaveMetaEntries(entries ?? new GDictionaryArray());

        string normalizedSaveId = ReadString(normalizedMeta, "save_id");
        List<GDictionary> updatedEntries = new();
        bool replaced = false;
        if (entries != null)
        {
            foreach (GDictionary entry in entries)
            {
                GDictionary normalizedExistingEntry = NormalizeSaveMeta(entry);
                if (normalizedExistingEntry.Count == 0)
                    continue;
                if (ReadString(normalizedExistingEntry, "save_id") == normalizedSaveId)
                {
                    updatedEntries.Add(normalizedMeta);
                    replaced = true;
                }
                else
                {
                    updatedEntries.Add(normalizedExistingEntry);
                }
            }
        }
        if (!replaced)
            updatedEntries.Add(normalizedMeta);
        return SortSaveMetaEntries(updatedEntries);
    }

    public bool SortSaveMetaNewestFirst(GDictionary a, GDictionary b)
    {
        return CompareSaveMetaNewestFirst(a, b) < 0;
    }

    public GDictionary MinimizeSavePayloadStrings(GDictionary payload)
    {
        var minimized = MinimizeSavePayloadValue(Variant.From(payload ?? new GDictionary()));
        return minimized.VariantType == Variant.Type.Dictionary
            ? minimized.AsGodotDictionary()
            : new GDictionary();
    }

    public GDictionary RestoreMinimizedSavePayloadStrings(GDictionary payload)
    {
        var restored = RestoreMinimizedSavePayloadValue(
            Variant.From(payload ?? new GDictionary())
        );
        return restored.VariantType == Variant.Type.Dictionary
            ? restored.AsGodotDictionary()
            : new GDictionary();
    }

    public GDictionary Serialize(GDictionary worldData, PartyState partyState)
    {
        return new GDictionary
        {
            ["world_data"] = SerializeWorldData(worldData ?? new GDictionary()),
            ["party_state"] = SerializePartyState(partyState),
        };
    }

    private static GDictionary SerializePartyState(PartyState partyState)
    {
        if (partyState == null)
            return new GDictionary();
        return partyState.ToDictionary().Duplicate(true);
    }

    private static bool TryAddRosterMember(
        PartyState partyState,
        StringName memberId,
        HashSet<string> seenIds,
        Godot.Collections.Array<StringName> target,
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

    private static GArray NormalizeEncounterAnchors(Variant anchorsValue)
    {
        var result = new GArray();
        if (!TryRawArray(anchorsValue, out GArray anchorValues))
            return result;
        foreach (var anchorValue in anchorValues)
        {
            EncounterAnchorData parsedAnchor = anchorValue.VariantType == Variant.Type.Dictionary
                ? EncounterAnchorData.FromDictionary(anchorValue.AsGodotDictionary())
                : null;
            if (parsedAnchor != null)
                result.Add(WorldMapDataProjection.Project(parsedAnchor));
        }
        return result;
    }

    private static GArray SerializeObjectOrDictionaryArray(Variant arrayValue)
    {
        var result = new GArray();
        if (!TryRawArray(arrayValue, out GArray itemValues))
            return result;
        foreach (var itemValue in itemValues)
        {
            if (itemValue.VariantType == Variant.Type.Dictionary)
            {
                result.Add(itemValue.AsGodotDictionary().Duplicate(true));
                continue;
            }
        }
        return result;
    }

    private static Variant MinimizeSavePayloadValue(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Dictionary => Variant.From(
                MinimizeSavePayloadDictionary(value.AsGodotDictionary())
            ),
            Variant.Type.Array => Variant.From(MinimizeSavePayloadArray(value.AsGodotArray())),
            Variant.Type.String or Variant.Type.StringName
                => Variant.From(ProgressionDataUtils.to_string_name(value)),
            _ => value,
        };
    }

    private static GDictionary MinimizeSavePayloadDictionary(GDictionary values)
    {
        var result = new GDictionary();
        foreach (var rawKey in values.Keys)
        {
            var minimizedKey = IsMinimizableString(rawKey)
                ? Variant.From(ProgressionDataUtils.to_string_name(rawKey))
                : rawKey;
            result[minimizedKey] = MinimizeSavePayloadValue(values[rawKey]);
        }
        return result;
    }

    private static GArray MinimizeSavePayloadArray(GArray values)
    {
        var result = new GArray();
        foreach (var item in values)
            result.Add(MinimizeSavePayloadValue(item));
        return result;
    }

    private static Variant RestoreMinimizedSavePayloadValue(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Dictionary => Variant.From(
                RestoreMinimizedSavePayloadDictionary(value.AsGodotDictionary())
            ),
            Variant.Type.Array => Variant.From(
                RestoreMinimizedSavePayloadArray(value.AsGodotArray())
            ),
            Variant.Type.StringName => Variant.From(value.AsString()),
            _ => value,
        };
    }

    private static GDictionary RestoreMinimizedSavePayloadDictionary(GDictionary values)
    {
        var result = new GDictionary();
        foreach (var rawKey in values.Keys)
        {
            var restoredKey =
                rawKey.VariantType == Variant.Type.StringName
                    ? Variant.From(rawKey.AsString())
                    : rawKey;
            result[restoredKey] = RestoreMinimizedSavePayloadValue(values[rawKey]);
        }
        return result;
    }

    private static GArray RestoreMinimizedSavePayloadArray(GArray values)
    {
        var result = new GArray();
        foreach (var item in values)
            result.Add(RestoreMinimizedSavePayloadValue(item));
        return result;
    }

    private static string GetSubmapReturnStackValidationError(Variant stackValue)
    {
        if (!TryRawArray(stackValue, out GArray stackValues))
            return "Corrupt save world_data.submap_return_stack: expected Array.";
        int index = 0;
        foreach (var entryValue in stackValues)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                return $"Corrupt save world_data.submap_return_stack[{index}]: expected Dictionary.";
            GDictionary entry = entryValue.AsGodotDictionary();
            if (!HasExactKeys(entry, new[] { "map_id", "coord" }))
                return $"Corrupt save world_data.submap_return_stack[{index}]: fields must exactly match current schema.";
            if (!IsStringValue(entry["map_id"]))
                return $"Corrupt save world_data.submap_return_stack[{index}]: map_id must be a String.";
            if (!IsSupportedVector2I(entry["coord"]))
                return $"Corrupt save world_data.submap_return_stack[{index}]: coord must be a Vector2i payload.";
            index++;
        }
        return "";
    }

    private static string GetWorldEventsValidationError(Variant eventsValue)
    {
        if (!TryRawArray(eventsValue, out GArray eventValues))
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
            GDictionary eventData = eventValue.AsGodotDictionary();
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
            if (!IsSupportedVector2I(eventData["world_coord"]))
                return $"Corrupt save world_data.world_events[{index}]: world_coord must be a Vector2i payload.";
            if (eventData["is_discovered"].VariantType != Variant.Type.Bool)
                return $"Corrupt save world_data.world_events[{index}]: is_discovered must be a bool.";
            index++;
        }
        return "";
    }

    private static string GetSettlementsValidationError(Variant settlementsValue)
    {
        if (!TryRawArray(settlementsValue, out GArray settlementValues))
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
            GDictionary settlementData = settlementValue.AsGodotDictionary();
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
                if (!IsSupportedVector2I(settlementData[coordField]))
                    return $"Corrupt save world_data.settlements[{index}]: {coordField} must be a Vector2i payload.";
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
            index++;
        }
        return "";
    }

    private static GDictionary ErrorResult()
    {
        return new GDictionary { ["error"] = (int)Error.InvalidData };
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

    private static bool IsMinimizableString(Variant value) =>
        value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName;

    private static bool IsSupportedVector2I(Variant value)
    {
        if (value.VariantType == Variant.Type.Vector2I)
            return true;
        if (value.VariantType != Variant.Type.Dictionary)
            return false;
        return HasVector2IFields(value.AsGodotDictionary());
    }

    private static Vector2I ReadVector2IDictionary(GDictionary vectorData, Vector2I fallback)
    {
        return HasVector2IFields(vectorData)
            ? new Vector2I(vectorData["x"].AsInt32(), vectorData["y"].AsInt32())
            : fallback;
    }

    private static bool HasVector2IFields(GDictionary vectorData)
    {
        return vectorData != null
            && vectorData.ContainsKey("x")
            && vectorData.ContainsKey("y")
            && vectorData["x"].VariantType == Variant.Type.Int
            && vectorData["y"].VariantType == Variant.Type.Int;
    }

    private static bool TryRawArray(Variant value, out GArray values)
    {
        if (value.VariantType == Variant.Type.Array)
        {
            values = value.AsGodotArray();
            return true;
        }
        values = new GArray();
        return false;
    }

    private static bool TryRawDictionary(Variant value, out GDictionary values)
    {
        if (value.VariantType == Variant.Type.Dictionary)
        {
            values = value.AsGodotDictionary();
            return true;
        }
        values = new GDictionary();
        return false;
    }

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
        foreach (var rawKey in data.Keys)
        {
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

    private static GDictionaryArray DuplicateSaveMetaEntries(GDictionaryArray entries)
    {
        GDictionaryArray duplicatedEntries = new();
        if (entries == null)
            return duplicatedEntries;
        foreach (GDictionary entry in entries)
        {
            if (entry != null && entry.Count > 0)
                duplicatedEntries.Add(entry.Duplicate(true));
        }
        return duplicatedEntries;
    }

    private static GDictionaryArray SortSaveMetaEntries(GDictionaryArray entries)
    {
        List<GDictionary> normalizedEntries = new();
        if (entries != null)
        {
            foreach (GDictionary entry in entries)
            {
                if (entry != null && entry.Count > 0)
                    normalizedEntries.Add(entry.Duplicate(true));
            }
        }
        return SortSaveMetaEntries(normalizedEntries);
    }

    private static GDictionaryArray SortSaveMetaEntries(List<GDictionary> entries)
    {
        entries.Sort(CompareSaveMetaNewestFirst);
        GDictionaryArray result = new();
        foreach (GDictionary entry in entries)
            result.Add(entry);
        return result;
    }

    private static int CompareSaveMetaNewestFirst(GDictionary a, GDictionary b)
    {
        int updatedA = ReadInt(a, "updated_at_unix_time");
        int updatedB = ReadInt(b, "updated_at_unix_time");
        if (updatedA != updatedB)
            return updatedB.CompareTo(updatedA);

        int createdA = ReadInt(a, "created_at_unix_time");
        int createdB = ReadInt(b, "created_at_unix_time");
        if (createdA != createdB)
            return createdB.CompareTo(createdA);

        string saveIdA = ReadString(a, "save_id");
        string saveIdB = ReadString(b, "save_id");
        return -string.CompareOrdinal(saveIdA, saveIdB);
    }
}
