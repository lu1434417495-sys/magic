using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

// Partial slice of GameRuntimeFacade — static Godot-payload/dictionary/array helpers.
// Pure physical split: same class, no behavior change. See GameRuntimeFacade.cs.
public sealed partial class GameRuntimeFacade
{

    private static GArray DictArray(GDictionary dictionary, object key)
    {
        if (!TryGetDictionaryValue(dictionary, key, out Variant value))
            return new GArray();
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static GDictionary DictDictionary(GDictionary dictionary, object key)
    {
        if (!TryGetDictionaryValue(dictionary, key, out Variant value))
            return new GDictionary();
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static int DictInt(GDictionary dictionary, object key, int fallback = 0)
    {
        if (!TryGetDictionaryValue(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    private static string DictString(GDictionary dictionary, object key, string fallback = "")
    {
        if (!TryGetDictionaryValue(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Nil ? fallback : value.ToString();
    }

    private static StringName DictStringName(
        GDictionary dictionary,
        object key,
        StringName fallback = default
    )
    {
        if (!TryGetDictionaryValue(dictionary, key, out Variant value))
            return fallback ?? new StringName("");
        return ProgressionDataUtils.to_string_name(value);
    }

    private static IEnumerable<GDictionary> ReadDictionaryItems(GArray values)
    {
        if (values == null)
            yield break;
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.Dictionary)
                yield return value.AsGodotDictionary();
        }
    }

    private static bool TryGetDictionaryValue(
        GDictionary dictionary,
        object key,
        out Variant value
    )
    {
        if (dictionary == null)
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
        if (dictionary.ContainsKey(variantKey))
        {
            value = dictionary[variantKey];
            return true;
        }
        value = default;
        return false;
    }

    private static GDictionary CoordDict(Vector2I coord) =>
        new() { ["x"] = coord.X, ["y"] = coord.Y };

    private static Dictionary<string, object> CoordPlain(Vector2I coord) =>
        new(StringComparer.Ordinal) { ["x"] = coord.X, ["y"] = coord.Y };

    private static void ResizeArray(GArray values, int maxCount)
    {
        if (values.Count > maxCount)
            values.Resize(maxCount);
    }

    private static void SortDictionaryArray(GArray values, string numericKey, string stringTieKey)
    {
        var list = new System.Collections.Generic.List<GDictionary>();
        foreach (GDictionary value in ReadDictionaryItems(values))
            list.Add(value);
        list.Sort(
            (left, right) =>
            {
                int leftValue = DictInt(left, numericKey);
                int rightValue = DictInt(right, numericKey);
                if (leftValue != rightValue)
                    return leftValue.CompareTo(rightValue);
                return string.CompareOrdinal(
                    DictString(left, stringTieKey),
                    DictString(right, stringTieKey)
                );
            }
        );
        values.Clear();
        foreach (var entry in list)
            values.Add(entry);
    }

    private static GArray UntypedQuestArray(IEnumerable<QuestState> values)
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (var value in values)
            if (value != null)
                result.Add(value.ToDictionary());
        return result;
    }

    private static GArray ProjectEquipmentEntries(
        IEnumerable<PartyEquipmentService.EquipmentViewEntry> values
    )
    {
        var result = new GArray();
        if (values == null)
            return result;
        foreach (PartyEquipmentService.EquipmentViewEntry value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new GDictionary
                {
                    ["slot_id"] = value.SlotId.ToString(),
                    ["slot_label"] = value.SlotLabel,
                    ["item_id"] = value.ItemId.ToString(),
                    ["instance_id"] = value.InstanceId.ToString(),
                    ["equipment_type_id"] = value.EquipmentTypeId.ToString(),
                    ["display_name"] = value.DisplayName,
                    ["icon"] = value.Icon,
                    ["description"] = value.Description,
                }
            );
        }
        return result;
    }
}
