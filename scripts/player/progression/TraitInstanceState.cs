using System;
using System.Collections.Generic;
using Godot;

public class TraitInstanceState
{
    private const string SavePayloadLabel = "trait instance payload";

    private static readonly string[] RequiredFields =
    {
        "trait_instance_id",
        "trait_id",
        "source_type",
        "source_id",
        "rank",
        "stacks",
        "roll_values",
    };

    public StringName trait_instance_id = "";
    public StringName trait_id = "";
    public StringName source_type = "";
    public StringName source_id = "";
    public int rank = 1;
    public int stacks = 1;
    public List<TraitRollValueState> roll_values = new();

    internal TraitSourceKind SourceKind => TraitContentRules.ToSourceKind(source_type);

    internal static TraitInstanceState Create(
        StringName traitInstanceId,
        StringName traitId,
        TraitSourceKind sourceKind,
        StringName sourceId,
        int rank = 1,
        int stacks = 1,
        IEnumerable<TraitRollValueState> rollValues = null)
    {
        return new TraitInstanceState
        {
            trait_instance_id = ProgressionDataUtils.to_string_name(traitInstanceId),
            trait_id = ProgressionDataUtils.to_string_name(traitId),
            source_type = TraitContentRules.ToStringName(sourceKind),
            source_id = ProgressionDataUtils.to_string_name(sourceId),
            rank = Mathf.Max(rank, 1),
            stacks = Mathf.Max(stacks, 1),
            roll_values = NormalizeRollValues(rollValues),
        };
    }

    public static List<TraitRollValueState> NormalizeRollValues(
        IEnumerable<TraitRollValueState> source)
    {
        List<TraitRollValueState> normalized = new();
        if (source == null)
            return normalized;

        foreach (TraitRollValueState entry in source)
        {
            TraitRollValueState clean = NormalizeRollEntry(entry);
            if (clean == null)
                continue;
            UpsertRoll(normalized, clean);
        }
        return normalized;
    }

    internal static List<TraitRollValueState> RollValuesFromDictionary(
        Godot.Collections.Dictionary source)
    {
        List<TraitRollValueState> normalized = new();
        if (source == null)
            return normalized;

        foreach (Variant rawKey in source.Keys)
        {
            StringName key = ProgressionDataUtils.to_string_name(rawKey);
            TraitRollValueState entry = TraitRollValueState.FromVariant(key, source[rawKey]);
            if (entry == null)
                return null;
            UpsertRoll(normalized, entry);
        }
        return normalized;
    }

    internal static Godot.Collections.Dictionary RollValuesToDictionary(
        IEnumerable<TraitRollValueState> values)
    {
        Godot.Collections.Dictionary payload = new();
        foreach (TraitRollValueState entry in NormalizeRollValues(values))
            payload[entry.key] = entry.ToVariant();
        return payload;
    }

    public int GetIntRoll(StringName key, int fallback = 0)
    {
        if (!TryFindRoll(key, out TraitRollValueState entry))
            return fallback;
        return entry.ValueTypeKind == TraitRollValueType.Int ? entry.int_value : fallback;
    }

    public StringName GetStringNameRoll(StringName key, StringName fallback = default)
    {
        if (!TryFindRoll(key, out TraitRollValueState entry))
            return fallback;
        return entry.ValueTypeKind == TraitRollValueType.StringName
            ? entry.string_name_value
            : fallback;
    }

    public bool GetBoolRoll(StringName key, bool fallback = false)
    {
        if (!TryFindRoll(key, out TraitRollValueState entry))
            return fallback;
        return entry.ValueTypeKind == TraitRollValueType.Bool ? entry.bool_value : fallback;
    }

    public void SetIntRoll(StringName key, int value)
    {
        UpsertRoll(roll_values, TraitRollValueState.CreateInt(key, value));
    }

    public void SetStringNameRoll(StringName key, StringName value)
    {
        UpsertRoll(roll_values, TraitRollValueState.CreateStringName(key, value));
    }

    public void SetBoolRoll(StringName key, bool value)
    {
        UpsertRoll(roll_values, TraitRollValueState.CreateBool(key, value));
    }

    public void RemoveRoll(StringName key)
    {
        StringName normalizedKey = ProgressionDataUtils.to_string_name(key);
        for (int index = roll_values.Count - 1; index >= 0; index--)
        {
            if (roll_values[index]?.key == normalizedKey)
                roll_values.RemoveAt(index);
        }
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            { "trait_instance_id", (string)trait_instance_id },
            { "trait_id", (string)trait_id },
            { "source_type", (string)source_type },
            { "source_id", (string)source_id },
            { "rank", rank },
            { "stacks", stacks },
            { "roll_values", RollValuesToDictionary(roll_values) },
        };
    }

    public TraitInstanceState DuplicateState()
    {
        return new TraitInstanceState
        {
            trait_instance_id = trait_instance_id,
            trait_id = trait_id,
            source_type = source_type,
            source_id = source_id,
            rank = rank,
            stacks = stacks,
            roll_values = NormalizeRollValues(roll_values),
        };
    }

    public static TraitInstanceState FromDictionary(Godot.Collections.Dictionary data)
    {
        string error = GetPayloadValidationError(data);
        if (error.Length > 0)
        {
            GameLog.Error(error, "trait.validation_failed", "progression");
            return null;
        }

        List<TraitRollValueState> parsedRollValues =
            RollValuesFromDictionary(data["roll_values"].AsGodotDictionary());
        if (parsedRollValues == null)
        {
            GameLog.Error(
                $"Corrupt {SavePayloadLabel}: roll_values contains unsupported typed value.",
                "trait.validation_failed",
                "progression"
            );
            return null;
        }

        return new TraitInstanceState
        {
            trait_instance_id = new StringName(data["trait_instance_id"].AsString().StripEdges()),
            trait_id = new StringName(data["trait_id"].AsString().StripEdges()),
            source_type = new StringName(data["source_type"].AsString().StripEdges()),
            source_id = new StringName(data["source_id"].AsString().StripEdges()),
            rank = data["rank"].AsInt32(),
            stacks = data["stacks"].AsInt32(),
            roll_values = parsedRollValues,
        };
    }

    public string ValidateAgainstDef(TraitDefinition definition)
    {
        if (definition == null)
            return $"Trait instance {trait_instance_id}: missing trait definition for {trait_id}.";

        var normalized = NormalizeRollValues(roll_values);
        var expectedKeys = new List<StringName>();
        foreach (TraitRollValueSchemaEntryDefinition schema in definition.RollValueSchema)
        {
            if (schema == null || schema.Key == "")
                continue;

            expectedKeys.Add(schema.Key);
            if (!TryFindRoll(normalized, schema.Key, out TraitRollValueState value))
                return $"Trait instance {trait_instance_id}: missing roll key {schema.Key}.";

            switch (schema.ValueTypeKind)
            {
                case TraitRollValueType.Int:
                    if (value.ValueTypeKind != TraitRollValueType.Int)
                        return $"Trait instance {trait_instance_id}: roll {schema.Key} must be int.";
                    if (value.int_value < schema.MinValue || value.int_value > schema.MaxValue)
                        return $"Trait instance {trait_instance_id}: roll {schema.Key} out of range.";
                    break;
                case TraitRollValueType.StringName:
                    bool allowed = false;
                    foreach (StringName allowedValue in schema.AllowedValues)
                    {
                        if (allowedValue != value.string_name_value)
                            continue;
                        allowed = true;
                        break;
                    }
                    if (value.ValueTypeKind != TraitRollValueType.StringName || !allowed)
                        return $"Trait instance {trait_instance_id}: roll {schema.Key} value {value.string_name_value} is not allowed.";
                    break;
                case TraitRollValueType.Bool:
                    if (value.ValueTypeKind != TraitRollValueType.Bool)
                        return $"Trait instance {trait_instance_id}: roll {schema.Key} must be bool.";
                    break;
                default:
                    return $"Trait instance {trait_instance_id}: unsupported schema type for {schema.Key}.";
            }
        }

        foreach (TraitRollValueState value in normalized)
        {
            if (!ContainsStringName(expectedKeys, value.key))
                return $"Trait instance {trait_instance_id}: unexpected roll key {value.key}.";
        }

        roll_values = normalized;
        return "";
    }

    public static string GetPayloadValidationError(Godot.Collections.Dictionary data)
    {
        if (data == null)
            return $"Corrupt {SavePayloadLabel}: expected Dictionary.";
        foreach (string fieldName in RequiredFields)
        {
            if (!data.ContainsKey(fieldName))
                return $"Corrupt {SavePayloadLabel}: missing required field '{fieldName}'.";
        }
        if (data.Count != RequiredFields.Length)
            return $"Corrupt {SavePayloadLabel}: expected exactly current trait instance fields.";

        foreach (Variant key in data.Keys)
        {
            if (key.VariantType != Variant.Type.String || !Array.Exists(RequiredFields, f => f == key.AsString()))
                return $"Corrupt {SavePayloadLabel}: unsupported field '{key}'.";
        }

        if (data["trait_instance_id"].VariantType != Variant.Type.String)
            return $"Corrupt {SavePayloadLabel}: trait_instance_id must be String.";
        if (
            data["trait_id"].VariantType != Variant.Type.String
            || data["trait_id"].AsString().StripEdges().Length == 0
        )
            return $"Corrupt {SavePayloadLabel}: trait_id is required.";
        if (data["source_type"].VariantType != Variant.Type.String)
            return $"Corrupt {SavePayloadLabel}: source_type must be String.";
        StringName sourceType = new(data["source_type"].AsString().StripEdges());
        if (!TraitContentRules.IsValidSourceType(sourceType))
            return $"Corrupt {SavePayloadLabel}: invalid source_type.";
        if (data["source_id"].VariantType != Variant.Type.String)
            return $"Corrupt {SavePayloadLabel}: source_id must be String.";
        string sourceId = data["source_id"].AsString().StripEdges();
        if (data["rank"].VariantType != Variant.Type.Int || data["rank"].AsInt32() < 1)
            return $"Corrupt {SavePayloadLabel}: rank must be int >= 1.";
        if (data["stacks"].VariantType != Variant.Type.Int || data["stacks"].AsInt32() < 1)
            return $"Corrupt {SavePayloadLabel}: stacks must be int >= 1.";
        if (data["roll_values"].VariantType != Variant.Type.Dictionary)
            return $"Corrupt {SavePayloadLabel}: roll_values must be Dictionary.";

        TraitSourceKind sourceKind = TraitContentRules.ToSourceKind(sourceType);
        if (
            (sourceKind == TraitSourceKind.Character || sourceKind == TraitSourceKind.EquipmentRoll)
            && data["trait_instance_id"].AsString().StripEdges().Length == 0
        )
            return $"Corrupt {SavePayloadLabel}: trait_instance_id is required for {sourceKind}.";
        if (
            (sourceKind == TraitSourceKind.Character || sourceKind == TraitSourceKind.EquipmentRoll)
            && sourceId.Length == 0
        )
            return $"Corrupt {SavePayloadLabel}: source_id is required for {sourceKind}.";

        return "";
    }

    private bool TryFindRoll(StringName key, out TraitRollValueState value) =>
        TryFindRoll(roll_values, key, out value);

    private static bool TryFindRoll(
        IEnumerable<TraitRollValueState> values,
        StringName key,
        out TraitRollValueState value
    )
    {
        StringName normalizedKey = ProgressionDataUtils.to_string_name(key);
        if (normalizedKey == "" || values == null)
        {
            value = null;
            return false;
        }

        foreach (TraitRollValueState entry in values)
        {
            if (entry == null || entry.key != normalizedKey)
                continue;
            value = entry;
            return true;
        }

        value = null;
        return false;
    }

    private static TraitRollValueState NormalizeRollEntry(TraitRollValueState entry)
    {
        if (entry == null)
            return null;

        StringName key = ProgressionDataUtils.to_string_name(entry.key);
        if (key == "")
            return null;

        return entry.ValueTypeKind switch
        {
            TraitRollValueType.Int => TraitRollValueState.CreateInt(key, entry.int_value),
            TraitRollValueType.StringName
                => TraitRollValueState.CreateStringName(key, entry.string_name_value),
            TraitRollValueType.Bool => TraitRollValueState.CreateBool(key, entry.bool_value),
            _ => null,
        };
    }

    private static void UpsertRoll(
        List<TraitRollValueState> values,
        TraitRollValueState entry
    )
    {
        TraitRollValueState clean = NormalizeRollEntry(entry);
        if (values == null || clean == null)
            return;

        for (int index = 0; index < values.Count; index++)
        {
            if (values[index]?.key != clean.key)
                continue;
            values[index] = clean;
            return;
        }

        values.Add(clean);
    }

    private static bool ContainsStringName(List<StringName> values, StringName expected)
    {
        foreach (StringName value in values)
            if (value == expected)
                return true;
        return false;
    }
}
