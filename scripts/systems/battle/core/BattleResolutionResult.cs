using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleResolutionResult : RefCounted
{
    private static readonly string[] TopLevelFields =
    {
        "battle_id",
        "seed",
        "world_coord",
        "encounter_anchor_id",
        "terrain_profile_id",
        "winner_faction_id",
        "encounter_resolution",
        "loot_entries",
        "overflow_entries",
        "pending_character_rewards",
        "quest_progress_events",
        "world_mutations",
        "party_resource_commit",
    };

    private static readonly string[] RequiredStringFields =
    {
        "battle_id",
        "encounter_anchor_id",
        "terrain_profile_id",
        "winner_faction_id",
        "encounter_resolution",
    };

    private static readonly string[] RequiredArrayFields =
    {
        "loot_entries",
        "overflow_entries",
        "pending_character_rewards",
        "quest_progress_events",
        "world_mutations",
    };

    public StringName battle_id = "";
    public int seed;
    public Vector2I world_coord = Vector2I.Zero;
    public StringName encounter_anchor_id = "";
    public StringName terrain_profile_id = "default";
    public StringName winner_faction_id = "";
    public StringName encounter_resolution = "";
    public GArray loot_entries = new();
    public GArray overflow_entries = new();
    public GArray pending_character_rewards = new();
    public GArray quest_progress_events = new();
    public GArray world_mutations = new();
    public GDictionary party_resource_commit = new();

    public bool is_empty()
    {
        return battle_id == ""
            && winner_faction_id == ""
            && encounter_resolution == ""
            && loot_entries.Count == 0
            && overflow_entries.Count == 0
            && pending_character_rewards.Count == 0
            && quest_progress_events.Count == 0
            && world_mutations.Count == 0
            && party_resource_commit.Count == 0;
    }

    public bool has_pending_character_rewards()
    {
        return pending_character_rewards.Count > 0;
    }

    public GArray get_pending_character_rewards_copy()
    {
        return pending_character_rewards.Duplicate();
    }

    public void set_loot_entries(GArray loot_entry_options)
    {
        loot_entries = NormalizeDropEntryOptions(loot_entry_options);
    }

    public void set_overflow_entries(GArray overflow_entry_options)
    {
        overflow_entries = NormalizeDropEntryOptions(overflow_entry_options);
    }

    public void set_pending_character_rewards(GArray reward_options)
    {
        pending_character_rewards = NormalizeRewardOptions(reward_options);
    }

    public GDictionary to_dict()
    {
        return new GDictionary
        {
            ["battle_id"] = battle_id.ToString(),
            ["seed"] = seed,
            ["world_coord"] = world_coord,
            ["encounter_anchor_id"] = encounter_anchor_id.ToString(),
            ["terrain_profile_id"] = terrain_profile_id.ToString(),
            ["winner_faction_id"] = winner_faction_id.ToString(),
            ["encounter_resolution"] = encounter_resolution.ToString(),
            ["loot_entries"] = NormalizeDropEntryOptions(loot_entries),
            ["overflow_entries"] = NormalizeDropEntryOptions(overflow_entries),
            ["pending_character_rewards"] = RewardOptionsToDicts(pending_character_rewards),
            ["quest_progress_events"] = DuplicateVariantArray(quest_progress_events),
            ["world_mutations"] = DuplicateVariantArray(world_mutations),
            ["party_resource_commit"] = party_resource_commit.Duplicate(true),
        };
    }

    public static BattleResolutionResult from_dict(GDictionary payload)
    {
        if (payload == null)
            return null;
        if (!HasValidTopLevelPayload(payload))
        {
            return null;
        }

        BattleResolutionResult result = new()
        {
            battle_id = ProgressionDataUtils.to_string_name(payload["battle_id"]),
            seed = payload["seed"].AsInt32(),
            world_coord = payload["world_coord"].AsVector2I(),
            encounter_anchor_id = ProgressionDataUtils.to_string_name(
                payload["encounter_anchor_id"]
            ),
            terrain_profile_id = ProgressionDataUtils.to_string_name(payload["terrain_profile_id"]),
            winner_faction_id = ProgressionDataUtils.to_string_name(payload["winner_faction_id"]),
            encounter_resolution = ProgressionDataUtils.to_string_name(
                payload["encounter_resolution"]
            ),
        };

        GArray parsedLootEntries = DropEntryDictsFromPayload(payload["loot_entries"]);
        if (parsedLootEntries == null)
        {
            return null;
        }
        GArray parsedOverflowEntries = DropEntryDictsFromPayload(payload["overflow_entries"]);
        if (parsedOverflowEntries == null)
        {
            return null;
        }
        GArray parsedPendingCharacterRewards = RewardOptionsFromDicts(
            payload["pending_character_rewards"]
        );
        if (parsedPendingCharacterRewards == null)
        {
            return null;
        }
        GArray parsedQuestProgressEvents = DictionaryArrayFromPayload(
            payload["quest_progress_events"]
        );
        if (parsedQuestProgressEvents == null)
        {
            return null;
        }
        GArray parsedWorldMutations = DictionaryArrayFromPayload(payload["world_mutations"]);
        if (parsedWorldMutations == null)
        {
            return null;
        }

        result.loot_entries = parsedLootEntries;
        result.overflow_entries = parsedOverflowEntries;
        result.pending_character_rewards = parsedPendingCharacterRewards;
        result.quest_progress_events = parsedQuestProgressEvents;
        result.world_mutations = parsedWorldMutations;
        result.party_resource_commit = payload["party_resource_commit"]
            .AsGodotDictionary()
            .Duplicate(true);
        return result;
    }

    private static bool HasValidTopLevelPayload(GDictionary payload)
    {
        if (!HasExactFields(payload, TopLevelFields))
        {
            return false;
        }
        foreach (string fieldName in RequiredStringFields)
        {
            if (!payload.ContainsKey(fieldName) || !IsNonEmptyString(payload[fieldName]))
            {
                return false;
            }
        }
        if (!payload.ContainsKey("seed") || !TryAsInt(payload["seed"], out _))
        {
            return false;
        }
        if (
            !payload.ContainsKey("world_coord")
            || !TryAsVector2I(payload["world_coord"], out _)
        )
        {
            return false;
        }
        foreach (string fieldName in RequiredArrayFields)
        {
            if (
                !payload.ContainsKey(fieldName)
                || !TryRawArray(payload[fieldName], out _)
            )
            {
                return false;
            }
        }
        return payload.ContainsKey("party_resource_commit")
            && TryRawDictionary(payload["party_resource_commit"], out _);
    }

    private static bool HasExactFields(GDictionary payload, string[] expectedFields)
    {
        if (payload.Count != expectedFields.Length)
        {
            return false;
        }

        GDictionary expectedLookup = new();
        GDictionary seenLookup = new();
        foreach (string fieldName in expectedFields)
        {
            expectedLookup[fieldName] = true;
        }
        foreach (object keyValue in payload.Keys)
        {
            if (!TryAsString(keyValue, out string keyText))
            {
                return false;
            }
            if (!expectedLookup.ContainsKey(keyText) || seenLookup.ContainsKey(keyText))
            {
                return false;
            }
            seenLookup[keyText] = true;
        }
        return seenLookup.Count == expectedLookup.Count;
    }

    private static bool IsNonEmptyString(object rawValue)
    {
        return rawValue switch
        {
            Variant value
                => value.VariantType == Variant.Type.String
                    && !string.IsNullOrEmpty(value.AsString().StripEdges()),
            string value => !string.IsNullOrEmpty(value.StripEdges()),
            _ => false,
        };
    }

    private static bool IsNonEmptyStringNameValue(object rawValue)
    {
        if (rawValue is string text)
        {
            return !string.IsNullOrEmpty(text.StripEdges());
        }
        if (rawValue is StringName stringName)
        {
            return !string.IsNullOrEmpty(stringName.ToString().StripEdges());
        }
        if (rawValue is not Variant value)
        {
            return false;
        }
        if (
            value.VariantType != Variant.Type.String
            && value.VariantType != Variant.Type.StringName
        )
        {
            return false;
        }
        return !string.IsNullOrEmpty(value.AsString().StripEdges());
    }

    private static GArray DropEntryDictsFromPayload(object values)
    {
        if (!TryRawArray(values, out GArray rawValues))
        {
            return null;
        }
        GArray parsedEntries = new();
        foreach (object entryValue in rawValues)
        {
            if (!TryRawDictionary(entryValue, out GDictionary entryData))
            {
                return null;
            }
            GDictionary parsedEntry = DropEntryFromPayload(entryData);
            if (parsedEntry == null)
            {
                return null;
            }
            parsedEntries.Add(parsedEntry);
        }
        return parsedEntries;
    }

    private static GDictionary DropEntryFromPayload(GDictionary entryData)
    {
        string[] requiredFields =
        {
            "drop_type",
            "drop_source_kind",
            "drop_source_id",
            "drop_source_label",
            "drop_entry_id",
            "item_id",
            "quantity",
        };

        foreach (string fieldName in requiredFields)
        {
            if (!entryData.ContainsKey(fieldName))
            {
                return null;
            }
        }
        foreach (
            string fieldName in new[]
            {
                "drop_type",
                "drop_source_kind",
                "drop_source_id",
                "drop_source_label",
                "drop_entry_id",
                "item_id",
            }
        )
        {
            if (!IsNonEmptyStringNameValue(entryData[fieldName]))
            {
                return null;
            }
        }
        if (!TryAsInt(entryData["quantity"], out int quantity))
        {
            return null;
        }
        if (quantity <= 0)
        {
            return null;
        }

        StringName dropType = ProgressionDataUtils.to_string_name(entryData["drop_type"]);
        int allowedFieldCount = requiredFields.Length;
        if (dropType == BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE())
        {
            allowedFieldCount += 1;
            if (
                entryData.Count != allowedFieldCount
                || !entryData.ContainsKey("equipment_instance")
                || quantity != 1
            )
            {
                return null;
            }

            string equipmentError = EquipmentInstanceState.get_payload_validation_error(
                entryData["equipment_instance"].AsGodotDictionary(),
                false
            );
            if (!string.IsNullOrEmpty(equipmentError))
            {
                return null;
            }
            EquipmentInstanceState equipmentInstance = EquipmentInstanceState.from_dict(
                entryData["equipment_instance"].AsGodotDictionary()
            );
            if (equipmentInstance == null)
            {
                return null;
            }
            StringName entryItemId = ProgressionDataUtils.to_string_name(entryData["item_id"]);
            if (equipmentInstance.item_id != entryItemId)
            {
                return null;
            }

            GDictionary normalizedEquipmentEntry = DuplicateFormalDropEntry(entryData);
            normalizedEquipmentEntry["equipment_instance"] = equipmentInstance.to_dict();
            return normalizedEquipmentEntry;
        }

        if (entryData.Count != allowedFieldCount || entryData.ContainsKey("equipment_instance"))
        {
            return null;
        }
        return DuplicateFormalDropEntry(entryData);
    }

    private static GDictionary DuplicateFormalDropEntry(GDictionary entryData)
    {
        return new GDictionary
        {
            ["drop_type"] = ProgressionDataUtils.to_string_name(entryData["drop_type"]).ToString(),
            ["drop_source_kind"] = ProgressionDataUtils
                .to_string_name(entryData["drop_source_kind"])
                .ToString(),
            ["drop_source_id"] = ProgressionDataUtils
                .to_string_name(entryData["drop_source_id"])
                .ToString(),
            ["drop_source_label"] = entryData["drop_source_label"].AsString().StripEdges(),
            ["drop_entry_id"] = ProgressionDataUtils
                .to_string_name(entryData["drop_entry_id"])
                .ToString(),
            ["item_id"] = ProgressionDataUtils.to_string_name(entryData["item_id"]).ToString(),
            ["quantity"] = entryData["quantity"].AsInt32(),
        };
    }

    private static GArray NormalizeDropEntryOptions(object lootEntryOptions)
    {
        GArray normalizedEntries = new();
        if (!TryRawArray(lootEntryOptions, out GArray lootEntryValues))
        {
            return normalizedEntries;
        }

        foreach (object lootEntryValue in lootEntryValues)
        {
            if (!TryRawDictionary(lootEntryValue, out GDictionary lootEntryData))
            {
                continue;
            }
            StringName dropType = ProgressionDataUtils.to_string_name(
                lootEntryData.GetValueOrDefault("drop_type")
            );
            StringName dropSourceKind = ProgressionDataUtils.to_string_name(
                lootEntryData.GetValueOrDefault("drop_source_kind")
            );
            StringName dropSourceId = ProgressionDataUtils.to_string_name(
                lootEntryData.GetValueOrDefault("drop_source_id")
            );
            StringName dropEntryId = ProgressionDataUtils.to_string_name(
                lootEntryData.GetValueOrDefault("drop_entry_id")
            );
            StringName itemId = ProgressionDataUtils.to_string_name(
                lootEntryData.GetValueOrDefault("item_id")
            );
            int quantity = Mathf.Max(
                TryAsInt(lootEntryData.GetValueOrDefault("quantity"), out int rawQuantity)
                    ? rawQuantity
                    : 0,
                0
            );
            string dropSourceLabel = lootEntryData
                .GetValueOrDefault("drop_source_label")
                .AsString()
                .StripEdges();
            if (
                dropType == ""
                || dropSourceKind == ""
                || dropSourceId == ""
                || string.IsNullOrEmpty(dropSourceLabel)
                || dropEntryId == ""
                || itemId == ""
                || quantity <= 0
            )
            {
                continue;
            }

            GDictionary normalizedEntry = new()
            {
                ["drop_type"] = dropType.ToString(),
                ["drop_source_kind"] = dropSourceKind.ToString(),
                ["drop_source_id"] = dropSourceId.ToString(),
                ["drop_source_label"] = dropSourceLabel,
                ["drop_entry_id"] = dropEntryId.ToString(),
                ["item_id"] = itemId.ToString(),
                ["quantity"] = quantity,
            };
            if (dropType == BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE())
            {
                if (!lootEntryData.ContainsKey("equipment_instance"))
                {
                    continue;
                }
                GDictionary equipmentInstanceData = NormalizeEquipmentInstanceData(
                    lootEntryData["equipment_instance"]
                );
                if (equipmentInstanceData.Count == 0)
                {
                    continue;
                }
                normalizedEntry["equipment_instance"] = equipmentInstanceData;
                normalizedEntry["quantity"] = 1;
                normalizedEntry["item_id"] = ProgressionDataUtils
                    .to_string_name(equipmentInstanceData.GetValueOrDefault("item_id", itemId))
                    .ToString();
            }
            normalizedEntries.Add(normalizedEntry);
        }
        return normalizedEntries;
    }

    private static GArray NormalizeRewardOptions(object rewardOptions)
    {
        GArray normalizedRewards = new();
        if (!TryRawArray(rewardOptions, out GArray rewardValues))
        {
            return normalizedRewards;
        }

        foreach (object rewardValue in rewardValues)
        {
            if (IsNil(rewardValue))
            {
                continue;
            }
            if (TryAsObject(rewardValue, out PendingCharacterReward typedReward))
            {
                if (!typedReward.is_empty())
                {
                    normalizedRewards.Add(typedReward);
                }
                continue;
            }
            if (TryRawDictionary(rewardValue, out GDictionary rewardData))
            {
                PendingCharacterReward normalizedReward = PendingCharacterReward.from_dict(
                    rewardData
                );
                if (normalizedReward != null && !normalizedReward.is_empty())
                {
                    normalizedRewards.Add(normalizedReward);
                }
            }
        }
        return normalizedRewards;
    }

    private static GArray RewardOptionsToDicts(GArray rewardOptions)
    {
        GArray rewards = new();
        foreach (object rewardValue in rewardOptions)
        {
            if (IsNil(rewardValue))
            {
                continue;
            }
            if (TryAsObject(rewardValue, out GodotObject rewardObject))
            {
                if (rewardObject != null && rewardObject.HasMethod("to_dict"))
                {
                    rewards.Add(rewardObject.Call("to_dict"));
                    continue;
                }
            }
            if (TryRawDictionary(rewardValue, out GDictionary rewardData))
            {
                rewards.Add(rewardData.Duplicate(true));
            }
        }
        return rewards;
    }

    private static GArray RewardOptionsFromDicts(object values)
    {
        if (!TryRawArray(values, out GArray rawValues))
        {
            return null;
        }

        GArray rewards = new();
        foreach (object rewardValue in rawValues)
        {
            if (!TryRawDictionary(rewardValue, out GDictionary rewardData))
            {
                return null;
            }
            PendingCharacterReward rewardFromDict = PendingCharacterReward.from_dict(
                rewardData
            );
            if (rewardFromDict == null)
            {
                return null;
            }
            rewards.Add(rewardFromDict);
        }
        return rewards;
    }

    private static GArray DuplicateVariantArray(object values)
    {
        GArray result = new();
        if (!TryRawArray(values, out GArray rawValues))
        {
            return result;
        }
        foreach (var value in rawValues)
        {
            if (TryRawDictionary(value, out GDictionary dictionary))
            {
                result.Add(dictionary.Duplicate(true));
            }
            else if (TryRawArray(value, out GArray array))
            {
                result.Add(array.Duplicate(true));
            }
            else
            {
                result.Add(value);
            }
        }
        return result;
    }

    private static GArray DictionaryArrayFromPayload(object values)
    {
        if (!TryRawArray(values, out GArray rawValues))
        {
            return null;
        }
        GArray result = new();
        foreach (object value in rawValues)
        {
            if (!TryRawDictionary(value, out GDictionary dictionary))
            {
                return null;
            }
            result.Add(dictionary.Duplicate(true));
        }
        return result;
    }

    private static GDictionary NormalizeEquipmentInstanceData(object rawValue)
    {
        if (TryRawDictionary(rawValue, out GDictionary rawDictionary))
        {
            if (
                !string.IsNullOrEmpty(
                    EquipmentInstanceState.get_payload_validation_error(rawDictionary, false)
                )
            )
            {
                return new GDictionary();
            }
            EquipmentInstanceState instance = EquipmentInstanceState.from_dict(rawDictionary);
            if (instance == null || instance.item_id == "")
            {
                return new GDictionary();
            }
            return instance.to_dict();
        }
        if (TryAsObject(rawValue, out GodotObject rawObject) && rawObject.HasMethod("to_dict"))
        {
            return NormalizeEquipmentObjectData(rawObject);
        }
        if (IsNil(rawValue))
        {
            return new GDictionary();
        }
        return new GDictionary();
    }

    private static GDictionary NormalizeEquipmentObjectData(GodotObject obj)
    {
        object instanceDict = obj.Call("to_dict");
        return TryRawDictionary(instanceDict, out _)
            ? NormalizeEquipmentInstanceData(instanceDict)
            : new GDictionary();
    }

    private static bool TryRawArray(object rawValue, out GArray values)
    {
        if (rawValue is Variant value && value.VariantType == Variant.Type.Array)
        {
            values = value.AsGodotArray();
            return true;
        }
        if (rawValue is GArray array)
        {
            values = array;
            return true;
        }
        values = new GArray();
        return false;
    }

    private static bool TryRawDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryAsObject<T>(object rawValue, out T value)
        where T : GodotObject
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Object)
        {
            value = variant.AsGodotObject() as T;
            return value != null;
        }
        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryAsInt(object rawValue, out int value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Int)
        {
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsString(object rawValue, out string value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString();
            return true;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsVector2I(object rawValue, out Vector2I value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Vector2I)
        {
            value = variant.AsVector2I();
            return true;
        }
        if (rawValue is Vector2I vector)
        {
            value = vector;
            return true;
        }
        value = Vector2I.Zero;
        return false;
    }

    private static bool IsNil(object rawValue)
    {
        return rawValue == null
            || (rawValue is Variant variant && variant.VariantType == Variant.Type.Nil);
    }
}
