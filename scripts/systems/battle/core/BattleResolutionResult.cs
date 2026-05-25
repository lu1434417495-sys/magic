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

    public void set_loot_entries(GArray loot_entry_variants)
    {
        loot_entries = NormalizeDropEntryVariants(loot_entry_variants);
    }

    public void set_overflow_entries(GArray overflow_entry_variants)
    {
        overflow_entries = NormalizeDropEntryVariants(overflow_entry_variants);
    }

    public void set_pending_character_rewards(GArray reward_variants)
    {
        pending_character_rewards = NormalizeRewardVariants(reward_variants);
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
            ["loot_entries"] = NormalizeDropEntryVariants(loot_entries),
            ["overflow_entries"] = NormalizeDropEntryVariants(overflow_entries),
            ["pending_character_rewards"] = RewardVariantsToDicts(pending_character_rewards),
            ["quest_progress_events"] = DuplicateVariantArray(quest_progress_events),
            ["world_mutations"] = DuplicateVariantArray(world_mutations),
            ["party_resource_commit"] = party_resource_commit.Duplicate(true),
        };
    }

    public static BattleResolutionResult from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }

        GDictionary payload = data.AsGodotDictionary();
        if (!HasValidTopLevelPayload(payload))
        {
            return null;
        }

        BattleResolutionResult result = new()
        {
            battle_id = ProgressionDataUtils.to_string_name(payload["battle_id"]),
            seed = payload["seed"].AsInt32(),
            world_coord = payload["world_coord"].AsVector2I(),
            encounter_anchor_id = ProgressionDataUtils.to_string_name(payload["encounter_anchor_id"]),
            terrain_profile_id = ProgressionDataUtils.to_string_name(payload["terrain_profile_id"]),
            winner_faction_id = ProgressionDataUtils.to_string_name(payload["winner_faction_id"]),
            encounter_resolution = ProgressionDataUtils.to_string_name(payload["encounter_resolution"]),
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
        GArray parsedPendingCharacterRewards = RewardVariantsFromDicts(payload["pending_character_rewards"]);
        if (parsedPendingCharacterRewards == null)
        {
            return null;
        }
        GArray parsedQuestProgressEvents = DictionaryArrayFromPayload(payload["quest_progress_events"]);
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
        result.party_resource_commit = payload["party_resource_commit"].AsGodotDictionary().Duplicate(true);
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
        if (!payload.ContainsKey("seed") || payload["seed"].VariantType != Variant.Type.Int)
        {
            return false;
        }
        if (!payload.ContainsKey("world_coord") || payload["world_coord"].VariantType != Variant.Type.Vector2I)
        {
            return false;
        }
        foreach (string fieldName in RequiredArrayFields)
        {
            if (!payload.ContainsKey(fieldName) || payload[fieldName].VariantType != Variant.Type.Array)
            {
                return false;
            }
        }
        return payload.ContainsKey("party_resource_commit")
            && payload["party_resource_commit"].VariantType == Variant.Type.Dictionary;
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
        foreach (Variant key in payload.Keys)
        {
            if (key.VariantType != Variant.Type.String)
            {
                return false;
            }
            string keyText = key.AsString();
            if (!expectedLookup.ContainsKey(keyText) || seenLookup.ContainsKey(keyText))
            {
                return false;
            }
            seenLookup[keyText] = true;
        }
        return seenLookup.Count == expectedLookup.Count;
    }

    private static bool IsNonEmptyString(Variant value)
    {
        return value.VariantType == Variant.Type.String && !string.IsNullOrEmpty(value.AsString().StripEdges());
    }

    private static bool IsNonEmptyStringNameValue(Variant value)
    {
        if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName)
        {
            return false;
        }
        return !string.IsNullOrEmpty(value.AsString().StripEdges());
    }

    private static GArray DropEntryDictsFromPayload(Variant values)
    {
        if (values.VariantType != Variant.Type.Array)
        {
            return null;
        }
        GArray parsedEntries = new();
        foreach (Variant entryVariant in values.AsGodotArray())
        {
            if (entryVariant.VariantType != Variant.Type.Dictionary)
            {
                return null;
            }
            GDictionary parsedEntry = DropEntryFromPayload(entryVariant.AsGodotDictionary());
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
        foreach (string fieldName in new[]
        {
            "drop_type",
            "drop_source_kind",
            "drop_source_id",
            "drop_source_label",
            "drop_entry_id",
            "item_id",
        })
        {
            if (!IsNonEmptyStringNameValue(entryData[fieldName]))
            {
                return null;
            }
        }
        if (entryData["quantity"].VariantType != Variant.Type.Int)
        {
            return null;
        }

        int quantity = entryData["quantity"].AsInt32();
        if (quantity <= 0)
        {
            return null;
        }

        StringName dropType = ProgressionDataUtils.to_string_name(entryData["drop_type"]);
        int allowedFieldCount = requiredFields.Length;
        if (dropType == BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE())
        {
            allowedFieldCount += 1;
            if (entryData.Count != allowedFieldCount || !entryData.ContainsKey("equipment_instance") || quantity != 1)
            {
                return null;
            }

            string equipmentError = EquipmentInstanceState.get_payload_validation_error(entryData["equipment_instance"], false);
            if (!string.IsNullOrEmpty(equipmentError))
            {
                return null;
            }
            EquipmentInstanceState equipmentInstance = EquipmentInstanceState.from_dict(entryData["equipment_instance"]);
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
            ["drop_source_kind"] = ProgressionDataUtils.to_string_name(entryData["drop_source_kind"]).ToString(),
            ["drop_source_id"] = ProgressionDataUtils.to_string_name(entryData["drop_source_id"]).ToString(),
            ["drop_source_label"] = entryData["drop_source_label"].AsString().StripEdges(),
            ["drop_entry_id"] = ProgressionDataUtils.to_string_name(entryData["drop_entry_id"]).ToString(),
            ["item_id"] = ProgressionDataUtils.to_string_name(entryData["item_id"]).ToString(),
            ["quantity"] = entryData["quantity"].AsInt32(),
        };
    }

    private static GArray NormalizeDropEntryVariants(Variant lootEntryVariants)
    {
        GArray normalizedEntries = new();
        if (lootEntryVariants.VariantType != Variant.Type.Array)
        {
            return normalizedEntries;
        }

        foreach (Variant lootEntryVariant in lootEntryVariants.AsGodotArray())
        {
            if (lootEntryVariant.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }

            GDictionary lootEntryData = lootEntryVariant.AsGodotDictionary();
            StringName dropType = ProgressionDataUtils.to_string_name(GetOrDefault(lootEntryData, "drop_type"));
            StringName dropSourceKind = ProgressionDataUtils.to_string_name(GetOrDefault(lootEntryData, "drop_source_kind"));
            StringName dropSourceId = ProgressionDataUtils.to_string_name(GetOrDefault(lootEntryData, "drop_source_id"));
            StringName dropEntryId = ProgressionDataUtils.to_string_name(GetOrDefault(lootEntryData, "drop_entry_id"));
            StringName itemId = ProgressionDataUtils.to_string_name(GetOrDefault(lootEntryData, "item_id"));
            int quantity = Mathf.Max(GetOrDefault(lootEntryData, "quantity").AsInt32(), 0);
            string dropSourceLabel = GetOrDefault(lootEntryData, "drop_source_label").AsString().StripEdges();
            if (dropType == ""
                || dropSourceKind == ""
                || dropSourceId == ""
                || string.IsNullOrEmpty(dropSourceLabel)
                || dropEntryId == ""
                || itemId == ""
                || quantity <= 0)
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
                GDictionary equipmentInstanceData = NormalizeEquipmentInstanceData(lootEntryData["equipment_instance"]);
                if (equipmentInstanceData.Count == 0)
                {
                    continue;
                }
                normalizedEntry["equipment_instance"] = equipmentInstanceData;
                normalizedEntry["quantity"] = 1;
                normalizedEntry["item_id"] = ProgressionDataUtils.to_string_name(GetOrDefault(equipmentInstanceData, "item_id", itemId)).ToString();
            }
            normalizedEntries.Add(normalizedEntry);
        }
        return normalizedEntries;
    }

    private static GArray NormalizeRewardVariants(Variant rewardVariants)
    {
        GArray normalizedRewards = new();
        if (rewardVariants.VariantType == Variant.Type.Nil)
        {
            return normalizedRewards;
        }
        if (rewardVariants.VariantType != Variant.Type.Array)
        {
            return normalizedRewards;
        }

        foreach (Variant rewardVariant in rewardVariants.AsGodotArray())
        {
            if (rewardVariant.VariantType == Variant.Type.Nil)
            {
                continue;
            }
            if (rewardVariant.VariantType == Variant.Type.Object
                && rewardVariant.AsGodotObject() is PendingCharacterReward typedReward)
            {
                if (!typedReward.is_empty())
                {
                    normalizedRewards.Add(typedReward);
                }
                continue;
            }
            if (rewardVariant.VariantType == Variant.Type.Dictionary)
            {
                PendingCharacterReward normalizedReward = PendingCharacterReward.from_variant(rewardVariant);
                if (normalizedReward != null && !normalizedReward.is_empty())
                {
                    normalizedRewards.Add(normalizedReward);
                }
            }
        }
        return normalizedRewards;
    }

    private static GArray RewardVariantsToDicts(GArray rewardVariants)
    {
        GArray rewards = new();
        foreach (Variant rewardVariant in rewardVariants)
        {
            if (rewardVariant.VariantType == Variant.Type.Nil)
            {
                continue;
            }
            if (rewardVariant.VariantType == Variant.Type.Object)
            {
                GodotObject rewardObject = rewardVariant.AsGodotObject();
                if (rewardObject != null && rewardObject.HasMethod("to_dict"))
                {
                    rewards.Add(rewardObject.Call("to_dict"));
                    continue;
                }
            }
            if (rewardVariant.VariantType == Variant.Type.Dictionary)
            {
                rewards.Add(rewardVariant.AsGodotDictionary().Duplicate(true));
            }
        }
        return rewards;
    }

    private static GArray RewardVariantsFromDicts(Variant values)
    {
        if (values.VariantType != Variant.Type.Array)
        {
            return null;
        }

        GArray rewards = new();
        foreach (Variant rewardVariant in values.AsGodotArray())
        {
            if (rewardVariant.VariantType != Variant.Type.Dictionary)
            {
                return null;
            }
            PendingCharacterReward rewardFromDict = PendingCharacterReward.from_variant(rewardVariant);
            if (rewardFromDict == null)
            {
                return null;
            }
            rewards.Add(rewardFromDict);
        }
        return rewards;
    }

    private static GArray DuplicateVariantArray(Variant values)
    {
        GArray result = new();
        if (values.VariantType != Variant.Type.Array)
        {
            return result;
        }
        foreach (Variant value in values.AsGodotArray())
        {
            if (value.VariantType == Variant.Type.Dictionary)
            {
                result.Add(value.AsGodotDictionary().Duplicate(true));
            }
            else if (value.VariantType == Variant.Type.Array)
            {
                result.Add(value.AsGodotArray().Duplicate(true));
            }
            else
            {
                result.Add(value);
            }
        }
        return result;
    }

    private static GArray DictionaryArrayFromPayload(Variant values)
    {
        if (values.VariantType != Variant.Type.Array)
        {
            return null;
        }
        GArray result = new();
        foreach (Variant value in values.AsGodotArray())
        {
            if (value.VariantType != Variant.Type.Dictionary)
            {
                return null;
            }
            result.Add(value.AsGodotDictionary().Duplicate(true));
        }
        return result;
    }

    private static GDictionary NormalizeEquipmentInstanceData(Variant value)
    {
        if (value.VariantType == Variant.Type.Nil)
        {
            return new GDictionary();
        }
        if (value.VariantType == Variant.Type.Dictionary)
        {
            if (!string.IsNullOrEmpty(EquipmentInstanceState.get_payload_validation_error(value, false)))
            {
                return new GDictionary();
            }
            EquipmentInstanceState instance = EquipmentInstanceState.from_dict(value);
            if (instance == null || instance.item_id == "")
            {
                return new GDictionary();
            }
            return instance.to_dict();
        }
        if (value.VariantType == Variant.Type.Object)
        {
            GodotObject obj = value.AsGodotObject();
            if (obj != null && obj.HasMethod("to_dict"))
            {
                Variant instanceDict = obj.Call("to_dict");
                if (instanceDict.VariantType == Variant.Type.Dictionary)
                {
                    return NormalizeEquipmentInstanceData(instanceDict);
                }
            }
        }
        return new GDictionary();
    }

    private static Variant GetOrDefault(GDictionary data, string key, Variant fallback = default)
    {
        return data.ContainsKey(key) ? data[key] : fallback;
    }
}
