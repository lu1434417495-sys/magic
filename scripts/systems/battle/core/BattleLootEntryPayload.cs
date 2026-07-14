using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleLootEntryPayload
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

    internal static GArray ProjectEntries(IEnumerable<BattleLootEntry> lootEntries)
    {
        GArray result = new();
        foreach (BattleLootEntry entry in NormalizeEntries(lootEntries))
            result.Add(ProjectEntry(entry));
        return result;
    }

    internal static GDictionary ProjectEntry(BattleLootEntry entry)
    {
        BattleLootEntry normalizedEntry = entry?.Duplicate();
        if (normalizedEntry == null || normalizedEntry.IsEmpty)
            return new GDictionary();

        GDictionary result = new()
        {
            ["drop_type"] = BattleLootIds.ToStringName(normalizedEntry.DropKind).ToString(),
            ["drop_source_kind"] = BattleLootIds
                .ToStringName(normalizedEntry.SourceKind)
                .ToString(),
            ["drop_source_id"] = normalizedEntry.SourceId.ToString(),
            ["drop_source_label"] = normalizedEntry.SourceLabel,
            ["drop_entry_id"] = normalizedEntry.DropEntryId.ToString(),
            ["item_id"] = normalizedEntry.ItemId.ToString(),
            ["quantity"] = normalizedEntry.Quantity,
        };
        if (normalizedEntry.DropKind == BattleLootDropKind.RandomEquipment)
            result["drop_luck"] = normalizedEntry.DropLuck;
        if (normalizedEntry.DropKind == BattleLootDropKind.EquipmentInstance)
            result["equipment_instance"] =
                normalizedEntry.EquipmentInstance?.ToDictionary() ?? new GDictionary();
        return result;
    }

    internal static List<BattleLootEntry> ParseEntries(GArray payloadEntries)
    {
        List<BattleLootEntry> result = new();
        if (payloadEntries == null)
            return result;

        foreach (Variant payloadEntryValue in payloadEntries)
        {
            if (payloadEntryValue.VariantType != Variant.Type.Dictionary)
                continue;
            using GDictionary payloadEntry = payloadEntryValue.AsGodotDictionary();
            BattleLootEntry entry = FormalDropEntryPayloadToTyped(payloadEntry);
            if (entry != null)
                result.Add(entry);
        }
        return result;
    }

    internal static List<BattleLootEntry> ParseEntriesPlain(
        IReadOnlyList<IReadOnlyDictionary<string, object>> payloadEntries
    )
    {
        var result = new List<BattleLootEntry>();
        if (payloadEntries == null)
            return result;

        foreach (IReadOnlyDictionary<string, object> payloadEntry in payloadEntries)
        {
            if (
                payloadEntry == null
                || !HasExactPlainFields(payloadEntry, ItemDropEntryFields)
                || ReadPlainRequiredString(payloadEntry, "drop_type") != "item"
            )
            {
                continue;
            }

            string sourceKindText = ReadPlainRequiredString(
                payloadEntry,
                "drop_source_kind"
            );
            BattleLootSourceKind sourceKind = sourceKindText switch
            {
                "enemy_template" or "encounter_roster" => BattleLootSourceKind.EnemyUnit,
                _ => BattleLootIds.ToSourceKind(new StringName(sourceKindText)),
            };
            StringName sourceId = ReadPlainRequiredString(payloadEntry, "drop_source_id");
            string sourceLabel = ReadPlainRequiredString(
                payloadEntry,
                "drop_source_label"
            );
            StringName dropEntryId = ReadPlainRequiredString(payloadEntry, "drop_entry_id");
            StringName itemId = ReadPlainRequiredString(payloadEntry, "item_id");
            if (
                sourceKind == BattleLootSourceKind.Unknown
                || sourceId == ""
                || string.IsNullOrEmpty(sourceLabel)
                || dropEntryId == ""
                || itemId == ""
                || !TryReadPlainPositiveInt(payloadEntry, "quantity", out int quantity)
            )
            {
                continue;
            }

            result.Add(
                BattleLootEntry.CreateItem(
                    sourceKind,
                    sourceId,
                    sourceLabel,
                    dropEntryId,
                    itemId,
                    quantity
                )
            );
        }
        return result;
    }

    internal static GDictionary NormalizeFormalDropEntryPayload(GDictionary entryData)
    {
        if (entryData == null)
            return null;

        StringName dropType = ReadRequiredStringName(entryData, "drop_type");
        BattleLootDropKind dropKind = BattleLootIds.ToDropKind(dropType);
        StringName dropSourceKind = ReadRequiredStringName(entryData, "drop_source_kind");
        BattleLootSourceKind sourceKind = BattleLootIds.ToSourceKind(dropSourceKind);
        StringName dropSourceId = ReadRequiredStringName(entryData, "drop_source_id");
        string dropSourceLabel = ReadRequiredString(entryData, "drop_source_label");
        StringName dropEntryId = ReadRequiredStringName(entryData, "drop_entry_id");
        StringName itemId = ReadRequiredStringName(entryData, "item_id");
        if (
            dropType == ""
            || dropKind == BattleLootDropKind.Unknown
            || dropSourceKind == ""
            || sourceKind == BattleLootSourceKind.Unknown
            || dropSourceId == ""
            || string.IsNullOrEmpty(dropSourceLabel)
            || dropEntryId == ""
            || itemId == ""
        )
        {
            return null;
        }

        if (!TryReadRequiredInt(entryData, "quantity", out int quantity) || quantity <= 0)
            return null;

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
                return null;
            if (!TryRawDictionary(entryData["equipment_instance"], out GDictionary equipmentPayload))
                return null;

            string equipmentError = EquipmentInstanceState.GetPayloadValidationError(
                equipmentPayload,
                false
            );
            if (!string.IsNullOrEmpty(equipmentError))
                return null;
            EquipmentInstanceState equipmentInstance = EquipmentInstanceState.FromDictionary(
                equipmentPayload
            );
            if (equipmentInstance == null || equipmentInstance.item_id != itemId)
                return null;

            GDictionary normalizedEquipmentEntry = CreateBaseFormalDropEntry(
                dropKind,
                sourceKind,
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
                return null;
            if (!TryReadRequiredInt(entryData, "drop_luck", out int dropLuck))
                return null;
            GDictionary normalizedRandomEquipmentEntry = CreateBaseFormalDropEntry(
                dropKind,
                sourceKind,
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
            return null;
        return CreateBaseFormalDropEntry(
            dropKind,
            sourceKind,
            dropSourceId,
            dropSourceLabel,
            dropEntryId,
            itemId,
            quantity
        );
    }

    internal static BattleLootEntry FormalDropEntryPayloadToTyped(GDictionary entryData)
    {
        GDictionary normalizedEntry = NormalizeFormalDropEntryPayload(entryData);
        if (normalizedEntry == null)
            return null;

        BattleLootDropKind dropKind = BattleLootIds.ToDropKind(
            ReadRequiredStringName(normalizedEntry, "drop_type")
        );
        BattleLootSourceKind sourceKind = BattleLootIds.ToSourceKind(
            ReadRequiredStringName(normalizedEntry, "drop_source_kind")
        );
        StringName sourceId = ReadRequiredStringName(normalizedEntry, "drop_source_id");
        string sourceLabel = ReadRequiredString(normalizedEntry, "drop_source_label");
        StringName dropEntryId = ReadRequiredStringName(normalizedEntry, "drop_entry_id");
        StringName itemId = ReadRequiredStringName(normalizedEntry, "item_id");
        int quantity = ReadRequiredInt(normalizedEntry, "quantity");
        if (dropKind == BattleLootDropKind.EquipmentInstance)
        {
            if (!TryRawDictionary(normalizedEntry["equipment_instance"], out GDictionary equipmentPayload))
                return null;
            return BattleLootEntry.CreateEquipmentInstance(
                sourceKind,
                sourceId,
                sourceLabel,
                dropEntryId,
                EquipmentInstanceState.FromDictionary(equipmentPayload)
            );
        }
        if (dropKind == BattleLootDropKind.RandomEquipment)
        {
            return BattleLootEntry.CreateRandomEquipment(
                sourceKind,
                sourceId,
                sourceLabel,
                dropEntryId,
                itemId,
                quantity,
                ReadRequiredInt(normalizedEntry, "drop_luck")
            );
        }
        return BattleLootEntry.CreateItem(
            sourceKind,
            sourceId,
            sourceLabel,
            dropEntryId,
            itemId,
            quantity
        );
    }

    private static List<BattleLootEntry> NormalizeEntries(IEnumerable<BattleLootEntry> lootEntries)
    {
        List<BattleLootEntry> result = new();
        if (lootEntries == null)
            return result;
        foreach (BattleLootEntry entry in lootEntries)
        {
            BattleLootEntry normalizedEntry = entry?.Duplicate();
            if (normalizedEntry != null && !normalizedEntry.IsEmpty)
                result.Add(normalizedEntry);
        }
        return result;
    }

    private static bool HasExactFields(GDictionary payload, string[] expectedFields)
    {
        if (payload.Count != expectedFields.Length)
            return false;

        GDictionary expectedLookup = new();
        GDictionary seenLookup = new();
        foreach (string fieldName in expectedFields)
            expectedLookup[fieldName] = true;
        foreach (object keyValue in payload.Keys)
        {
            if (!TryAsString(keyValue, out string keyText))
                return false;
            if (!expectedLookup.ContainsKey(keyText) || seenLookup.ContainsKey(keyText))
                return false;
            seenLookup[keyText] = true;
        }
        return seenLookup.Count == expectedLookup.Count;
    }

    private static bool HasExactPlainFields(
        IReadOnlyDictionary<string, object> payload,
        IReadOnlyList<string> expectedFields
    )
    {
        if (payload == null || payload.Count != expectedFields.Count)
            return false;
        foreach (string expectedField in expectedFields)
        {
            if (!payload.ContainsKey(expectedField))
                return false;
        }
        return true;
    }

    private static string ReadPlainRequiredString(
        IReadOnlyDictionary<string, object> payload,
        string key
    )
    {
        return payload != null
            && payload.TryGetValue(key, out object value)
            && value is string stringValue
            ? stringValue.Trim()
            : "";
    }

    private static bool TryReadPlainPositiveInt(
        IReadOnlyDictionary<string, object> payload,
        string key,
        out int value
    )
    {
        value = 0;
        if (payload == null || !payload.TryGetValue(key, out object rawValue))
            return false;
        long longValue = rawValue switch
        {
            int intValue => intValue,
            long value64 => value64,
            _ => 0,
        };
        if (longValue <= 0 || longValue > int.MaxValue)
            return false;
        value = (int)longValue;
        return true;
    }

    private static GDictionary CreateBaseFormalDropEntry(
        BattleLootDropKind dropKind,
        BattleLootSourceKind dropSourceKind,
        StringName dropSourceId,
        string dropSourceLabel,
        StringName dropEntryId,
        StringName itemId,
        int quantity
    )
    {
        return new GDictionary
        {
            ["drop_type"] = BattleLootIds.ToStringName(dropKind).ToString(),
            ["drop_source_kind"] = BattleLootIds.ToStringName(dropSourceKind).ToString(),
            ["drop_source_id"] = dropSourceId.ToString(),
            ["drop_source_label"] = dropSourceLabel,
            ["drop_entry_id"] = dropEntryId.ToString(),
            ["item_id"] = itemId.ToString(),
            ["quantity"] = quantity,
        };
    }

    private static string ReadRequiredString(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return "";
        Variant value = source[key];
        return value.VariantType == Variant.Type.String ? value.AsString().StripEdges() : "";
    }

    private static StringName ReadRequiredStringName(GDictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return "";
        Variant value = source[key];
        return value.VariantType == Variant.Type.String ? new StringName(value.AsString()) : "";
    }

    private static bool TryReadRequiredInt(GDictionary source, string key, out int value)
    {
        value = 0;
        if (source == null || !source.ContainsKey(key))
            return false;
        return TryAsInt(source[key], out value);
    }

    private static int ReadRequiredInt(GDictionary source, string key)
    {
        return TryReadRequiredInt(source, key, out int value) ? value : 0;
    }

    private static bool TryRawDictionary(object rawValue, out GDictionary value)
    {
        if (rawValue is GDictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        try
        {
            dynamic dynamicValue = rawValue;
            value = dynamicValue.AsGodotDictionary();
            return true;
        }
        catch
        {
        }
        value = new GDictionary();
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
}
