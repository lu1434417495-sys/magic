using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GPendingCharacterRewardArray = Godot.Collections.Array<PendingCharacterReward>;

internal partial class BattleResolutionResult : RefCounted
{
    private static readonly string[] ItemDropEntryFields =
    {
        "drop_type",
        "drop_source_kind",
        "drop_source_id",
        "drop_source_label",
        "drop_entry_id",
        "item_id",
        "quantity",
    };

    private static readonly string[] RandomEquipmentDropEntryFields =
    {
        "drop_type",
        "drop_source_kind",
        "drop_source_id",
        "drop_source_label",
        "drop_entry_id",
        "item_id",
        "quantity",
        "drop_luck",
    };

    private static readonly string[] EquipmentInstanceDropEntryFields =
    {
        "drop_type",
        "drop_source_kind",
        "drop_source_id",
        "drop_source_label",
        "drop_entry_id",
        "item_id",
        "quantity",
        "equipment_instance",
    };

    internal StringName battle_id = "";
    internal long seed;
    internal Vector2I world_coord = Vector2I.Zero;
    internal StringName encounter_anchor_id = "";
    internal StringName terrain_profile_id = "default";
    internal StringName winner_faction_id = "";
    internal StringName encounter_resolution = "";
    internal GArray loot_entries = new();
    internal GArray overflow_entries = new();
    internal GPendingCharacterRewardArray pending_character_rewards = new();
    internal GArray quest_progress_events = new();
    internal GArray world_mutations = new();
    internal GDictionary party_resource_commit = new();

    internal bool IsEmpty()
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

    internal int GetConvertedCalamityShards()
    {
        return ReadOptionalInt(party_resource_commit, "converted_calamity_shards");
    }

    internal void SetLootEntries(GArray loot_entry_options)
    {
        loot_entries = NormalizeDropEntryOptions(loot_entry_options);
    }

    internal void SetOverflowEntries(GArray overflow_entry_options)
    {
        overflow_entries = NormalizeDropEntryOptions(overflow_entry_options);
    }

    internal IReadOnlyList<PendingCharacterReward> PendingCharacterRewards =>
        DuplicatePendingCharacterRewards(pending_character_rewards);

    internal void SetPendingCharacterRewards(IEnumerable<PendingCharacterReward> rewards)
    {
        pending_character_rewards = NormalizePendingCharacterRewards(rewards);
    }

    internal GDictionary ToDictionary()
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
            ["pending_character_rewards"] = PendingRewardDictionaryArray(pending_character_rewards),
            ["quest_progress_events"] = DuplicateVariantArray(quest_progress_events),
            ["world_mutations"] = DuplicateVariantArray(world_mutations),
            ["party_resource_commit"] = party_resource_commit.Duplicate(true),
        };
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

    internal static GDictionary NormalizeFormalDropEntryPayload(GDictionary entryData)
    {
        if (entryData == null)
        {
            return null;
        }

        StringName dropType = ReadRequiredStringName(entryData, "drop_type");
        BattleLootDropKind dropKind = BattleLootIds.ToDropKind(dropType);
        StringName dropSourceKind = ReadRequiredStringName(entryData, "drop_source_kind");
        StringName dropSourceId = ReadRequiredStringName(entryData, "drop_source_id");
        string dropSourceLabel = ReadRequiredString(entryData, "drop_source_label");
        StringName dropEntryId = ReadRequiredStringName(entryData, "drop_entry_id");
        StringName itemId = ReadRequiredStringName(entryData, "item_id");
        if (
            dropType == ""
            || dropKind == BattleLootDropKind.Unknown
            || dropSourceKind == ""
            || dropSourceId == ""
            || string.IsNullOrEmpty(dropSourceLabel)
            || dropEntryId == ""
            || itemId == ""
        )
        {
            return null;
        }

        if (!TryReadRequiredInt(entryData, "quantity", out int quantity) || quantity <= 0)
        {
            return null;
        }

        if (dropKind == BattleLootDropKind.EquipmentInstance)
        {
            if (
                entryData.Count == EquipmentInstanceDropEntryFields.Length
                && !HasExactFields(entryData, EquipmentInstanceDropEntryFields)
            )
            {
                throw new KeyNotFoundException("equipment_instance");
            }
            if (!HasExactFields(entryData, EquipmentInstanceDropEntryFields) || quantity != 1)
            {
                return null;
            }
            if (!TryRawDictionary(entryData["equipment_instance"], out GDictionary equipmentPayload))
            {
                return null;
            }
            string equipmentError = EquipmentInstanceState.GetPayloadValidationError(
                equipmentPayload,
                false
            );
            if (!string.IsNullOrEmpty(equipmentError))
            {
                return null;
            }
            EquipmentInstanceState equipmentInstance = EquipmentInstanceState.FromDictionary(
                equipmentPayload
            );
            if (equipmentInstance == null || equipmentInstance.item_id != itemId)
            {
                return null;
            }

            GDictionary normalizedEquipmentEntry = CreateBaseFormalDropEntry(
                dropType,
                dropSourceKind,
                dropSourceId,
                dropSourceLabel,
                dropEntryId,
                itemId,
                1
            );
            normalizedEquipmentEntry["equipment_instance"] = equipmentInstance.ToDictionary();
            return normalizedEquipmentEntry;
        }

        if (dropKind == BattleLootDropKind.RandomEquipment)
        {
            if (!HasExactFields(entryData, RandomEquipmentDropEntryFields))
            {
                return null;
            }
            if (!TryReadRequiredInt(entryData, "drop_luck", out int dropLuck))
            {
                return null;
            }
            GDictionary normalizedRandomEquipmentEntry = CreateBaseFormalDropEntry(
                dropType,
                dropSourceKind,
                dropSourceId,
                dropSourceLabel,
                dropEntryId,
                itemId,
                quantity
            );
            normalizedRandomEquipmentEntry["drop_luck"] = Mathf.Clamp(dropLuck, -6, 5);
            return normalizedRandomEquipmentEntry;
        }

        if (dropKind != BattleLootDropKind.Item || !HasExactFields(entryData, ItemDropEntryFields))
        {
            return null;
        }
        return CreateBaseFormalDropEntry(
            dropType,
            dropSourceKind,
            dropSourceId,
            dropSourceLabel,
            dropEntryId,
            itemId,
            quantity
        );
    }

    private static GDictionary CreateBaseFormalDropEntry(
        StringName dropType,
        StringName dropSourceKind,
        StringName dropSourceId,
        string dropSourceLabel,
        StringName dropEntryId,
        StringName itemId,
        int quantity
    )
    {
        return new GDictionary
        {
            ["drop_type"] = dropType.ToString(),
            ["drop_source_kind"] = dropSourceKind.ToString(),
            ["drop_source_id"] = dropSourceId.ToString(),
            ["drop_source_label"] = dropSourceLabel,
            ["drop_entry_id"] = dropEntryId.ToString(),
            ["item_id"] = itemId.ToString(),
            ["quantity"] = quantity,
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
            GDictionary normalizedEntry = NormalizeFormalDropEntryPayload(lootEntryData);
            if (normalizedEntry == null)
            {
                continue;
            }
            normalizedEntries.Add(normalizedEntry);
        }
        return normalizedEntries;
    }

    private static string ReadRequiredString(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return "";
        }
        Variant value = source[key];
        return value.VariantType == Variant.Type.String ? value.AsString().StripEdges() : "";
    }

    private static StringName ReadRequiredStringName(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return "";
        }
        Variant value = source[key];
        return value.VariantType == Variant.Type.String ? new StringName(value.AsString()) : "";
    }

    private static bool TryReadRequiredInt(GDictionary source, string key, out int value)
    {
        value = 0;
        if (source == null || !source.ContainsKey(key))
        {
            return false;
        }
        return TryAsInt(source[key], out value);
    }

    private static GPendingCharacterRewardArray NormalizePendingCharacterRewards(
        IEnumerable<PendingCharacterReward> rewards
    )
    {
        GPendingCharacterRewardArray normalizedRewards = new();
        if (rewards == null)
            return normalizedRewards;

        foreach (PendingCharacterReward reward in rewards)
        {
            if (reward == null || reward.IsEmpty())
                continue;
            normalizedRewards.Add(reward.DuplicateState());
        }
        return normalizedRewards;
    }

    private static GPendingCharacterRewardArray DuplicatePendingCharacterRewards(
        IEnumerable<PendingCharacterReward> rewards
    ) => NormalizePendingCharacterRewards(rewards);

    private static GArray PendingRewardDictionaryArray(IEnumerable<PendingCharacterReward> rewards)
    {
        GArray result = new();
        if (rewards == null)
            return result;
        foreach (PendingCharacterReward reward in rewards)
            if (reward != null && !reward.IsEmpty())
                result.Add(reward.ToDictionary());
        return result;
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

    private static GDictionary NormalizeEquipmentInstanceData(object rawValue)
    {
        if (TryRawDictionary(rawValue, out GDictionary rawDictionary))
        {
            if (
                !string.IsNullOrEmpty(
                    EquipmentInstanceState.GetPayloadValidationError(rawDictionary, false)
                )
            )
            {
                return new GDictionary();
            }
            EquipmentInstanceState instance = EquipmentInstanceState.FromDictionary(rawDictionary);
            if (instance == null || instance.item_id == "")
            {
                return new GDictionary();
            }
            return instance.ToDictionary();
        }
        if (TryAsEquipmentInstance(rawValue, out EquipmentInstanceState instanceObject))
        {
            return NormalizeEquipmentObjectData(instanceObject);
        }
        if (IsNil(rawValue))
        {
            return new GDictionary();
        }
        return new GDictionary();
    }

    private static GDictionary NormalizeEquipmentObjectData(EquipmentInstanceState obj)
    {
        object instanceDict = (obj as EquipmentInstanceState)?.ToDictionary();
        return TryRawDictionary(instanceDict, out _)
            ? NormalizeEquipmentInstanceData(instanceDict)
            : new GDictionary();
    }

    private static bool TryRawArray(object rawValue, out GArray values)
    {
        try
        {
            dynamic dynamicValue = rawValue;
            values = dynamicValue.AsGodotArray();
            return true;
        }
        catch
        {
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
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsGodotDictionary();
            return true;
        }
        catch
        {
        }
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new GDictionary();
        return false;
    }

    private static bool TryAsEquipmentInstance(object rawValue, out EquipmentInstanceState value)
    {
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.As<EquipmentInstanceState>();
            return value != null;
        }
        catch
        {
        }
        if (rawValue is EquipmentInstanceState typedValue)
        {
            value = typedValue;
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryAsInt(object rawValue, out int value)
    {
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsInt32();
            return true;
        }
        catch
        {
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static int ReadOptionalInt(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return 0;
        return TryAsInt(source[key], out int value) ? value : 0;
    }

    private static bool TryAsString(object rawValue, out string value)
    {
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        if (rawValue is StringName stringNameValue)
        {
            value = stringNameValue.ToString();
            return true;
        }
        if (
            rawValue is Variant variant
            && variant.VariantType is Variant.Type.String or Variant.Type.StringName
        )
        {
            value = variant.AsString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsVector2I(object rawValue, out Vector2I value)
    {
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsVector2I();
            return true;
        }
        catch
        {
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
        return rawValue == null || rawValue.ToString() == "<null>";
    }
}
