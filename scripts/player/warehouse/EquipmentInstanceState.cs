using Godot;
using System;
using System.Collections.Generic;

public sealed class EquipmentAbilityUsagePeriodState
{
    public string AbilityId { get; set; } = "";
    public string PeriodKind { get; set; } = "";
    public int PeriodIndex { get; set; }
    public int UsedCount { get; set; }

    public EquipmentAbilityUsagePeriodState DuplicateState() =>
        new()
        {
            AbilityId = AbilityId ?? "",
            PeriodKind = PeriodKind ?? "",
            PeriodIndex = PeriodIndex,
            UsedCount = UsedCount,
        };
}

public sealed class EquipmentAbilityPersistentCounterState
{
    public string CounterId { get; set; } = "";
    public long Value { get; set; }

    public EquipmentAbilityPersistentCounterState DuplicateState() =>
        new()
        {
            CounterId = CounterId ?? "",
            Value = Value,
        };
}

public class EquipmentInstanceState
{
    public enum RarityTier
    {
        COMMON,
        UNCOMMON,
        RARE,
        EPIC,
        LEGENDARY,
    }

    private const string SAVE_PAYLOAD_LABEL = "save equipment instance payload";

    private const string TRANSIENT_LOOT_PAYLOAD_LABEL = "transient loot equipment instance payload";

    private const string PeriodKindPerWorldDay = "per_world_day";

    private const string PeriodKindPerWorldMonth = "per_world_month";

    public StringName instance_id = "";

    public StringName item_id = "";

    public int rarity = (int)RarityTier.COMMON;

    public int current_durability = EquipmentDurabilityRules.GetDefaultCurrentDurability(
        (int)RarityTier.COMMON
    );

    public List<TraitInstanceState> trait_instances = new();

    public List<EquipmentAbilityUsagePeriodState> ability_usage_periods = new();

    public List<EquipmentAbilityPersistentCounterState> ability_persistent_counters = new();

    public static EquipmentInstanceState CreateInstance(StringName pItemId, StringName pInstanceId)
    {
        var inst = new EquipmentInstanceState();
        inst.instance_id = ProgressionDataUtils.to_string_name(pInstanceId);
        inst.item_id = ProgressionDataUtils.to_string_name(pItemId);
        inst.current_durability = EquipmentDurabilityRules.GetDefaultCurrentDurability(inst.rarity);
        return inst;
    }

    public static EquipmentInstanceState CreateTransientInstance(StringName pItemId)
    {
        return CreateInstance(pItemId, default);
    }

    public static StringName FormatInstanceId(int serial) =>
        new StringName($"eq_{Mathf.Max(serial, 1):D6}");

    public static StringName FormatPreviewInstanceId(int serial) =>
        new StringName($"__preview_eq_{Mathf.Max(serial, 1):D6}");

    public Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            { "instance_id", (string)instance_id },
            { "item_id", (string)item_id },
            { "rarity", rarity },
            { "current_durability", current_durability },
            { "trait_instances", TraitInstanceCollection.ToPayloadArray(trait_instances) },
            { "ability_usage_periods", _usage_periods_to_payload_array(ability_usage_periods) },
            { "ability_persistent_counters", _persistent_counters_to_payload_array(ability_persistent_counters) },
        };
    }

    public EquipmentInstanceState DuplicateState()
    {
        return new EquipmentInstanceState
        {
            instance_id = instance_id,
            item_id = item_id,
            rarity = rarity,
            current_durability = current_durability,
            trait_instances = TraitInstanceCollection.Duplicate(trait_instances),
            ability_usage_periods = _duplicate_usage_periods(ability_usage_periods),
            ability_persistent_counters = _duplicate_persistent_counters(ability_persistent_counters),
        };
    }

    public static EquipmentInstanceState FromDictionary(Godot.Collections.Dictionary data) =>
        _from_dict(data, false, SAVE_PAYLOAD_LABEL);

    public static EquipmentInstanceState FromTransientLootDictionary(Godot.Collections.Dictionary data) =>
        _from_dict(data, true, TRANSIENT_LOOT_PAYLOAD_LABEL);

    public static string GetPayloadValidationError(
        Godot.Collections.Dictionary data,
        bool allowEmptyInstanceId = false
    ) =>
        _get_payload_validation_error(data, allowEmptyInstanceId, SAVE_PAYLOAD_LABEL);

    private static EquipmentInstanceState _from_dict(
        Godot.Collections.Dictionary payload,
        bool allowEmptyInstanceId,
        string payloadLabel
    )
    {
        var validationError = _get_payload_validation_error(
            payload,
            allowEmptyInstanceId,
            payloadLabel
        );

        if (validationError.Length > 0)
        {
            GameLog.Error(validationError, "equipment.validation_failed", "equipment");
            return null;
        }

        var traitInstances = TraitInstanceCollection.FromPayloadArray(
            payload["trait_instances"],
            TraitSourceKind.EquipmentRoll
        );
        if (traitInstances == null)
        {
            GameLog.Error(
                $"Corrupt {payloadLabel}: trait_instances contains invalid equipment_roll entries.",
                "equipment.validation_failed",
                "equipment"
            );
            return null;
        }

        return new EquipmentInstanceState
        {
            instance_id = new StringName(payload["instance_id"].AsString().StripEdges()),
            item_id = new StringName(payload["item_id"].AsString().StripEdges()),
            rarity = payload["rarity"].AsInt32(),
            current_durability = payload["current_durability"].AsInt32(),
            trait_instances = traitInstances,
            ability_usage_periods = _usage_periods_from_payload_array(payload["ability_usage_periods"]),
            ability_persistent_counters = _persistent_counters_from_payload_array(
                payload["ability_persistent_counters"]
            ),
        };
    }

    private static string _get_payload_validation_error(
        Godot.Collections.Dictionary payload,
        bool allowEmptyInstanceId,
        string payloadLabel
    )
    {
        if (payload == null)
            return $"Corrupt {payloadLabel}: expected Dictionary.";

        var requiredFields = new[]
        {
            "instance_id",
            "item_id",
            "rarity",
            "current_durability",
            "trait_instances",
            "ability_usage_periods",
            "ability_persistent_counters",
        };

        foreach (string fn in requiredFields)
        {
            if (!payload.ContainsKey(fn))
                return $"Corrupt {payloadLabel}: missing required field '{fn}'.";
        }

        if (payload.Count != requiredFields.Length)
            return $"Corrupt {payloadLabel}: expected exactly current equipment instance fields.";

        foreach (var keyValue in payload.Keys)
        {
            if (
                keyValue.VariantType != Variant.Type.String
                || !((System.Collections.Generic.IList<string>)requiredFields).Contains(
                    keyValue.AsString()
                )
            )
                return $"Corrupt {payloadLabel}: unsupported field '{keyValue}'.";
        }

        var instanceIdVar = payload["instance_id"];

        if (instanceIdVar.VariantType != Variant.Type.String)
            return $"Corrupt {payloadLabel}: instance_id must be String.";

        var itemIdVar = payload["item_id"];

        if (itemIdVar.VariantType != Variant.Type.String)
            return $"Corrupt {payloadLabel}: item_id must be String.";

        if (payload["rarity"].VariantType != Variant.Type.Int)
            return $"Corrupt {payloadLabel}: rarity must be int.";

        if (payload["current_durability"].VariantType != Variant.Type.Int)
            return $"Corrupt {payloadLabel}: current_durability must be int.";

        if (payload["trait_instances"].VariantType != Variant.Type.Array)
            return $"Corrupt {payloadLabel}: trait_instances must be Array.";

        if (payload["ability_usage_periods"].VariantType != Variant.Type.Array)
            return $"Corrupt {payloadLabel}: ability_usage_periods must be Array.";

        if (payload["ability_persistent_counters"].VariantType != Variant.Type.Array)
            return $"Corrupt {payloadLabel}: ability_persistent_counters must be Array.";

        string instanceIdText = instanceIdVar.AsString().StripEdges();

        string itemIdText = itemIdVar.AsString().StripEdges();

        var instanceId =
            instanceIdText.Length > 0 ? new StringName(instanceIdText) : new StringName("");

        var itemId = itemIdText.Length > 0 ? new StringName(itemIdText) : new StringName("");

        int rarityValue = payload["rarity"].AsInt32();

        int currentDurability = payload["current_durability"].AsInt32();

        if (instanceId == "" && !allowEmptyInstanceId)
            return $"Corrupt {payloadLabel}: instance_id is required.";

        if (itemId == "")
            return $"Corrupt {payloadLabel}: item_id is required for instance '{instanceId}'.";

        if (!IsValidRarity(rarityValue))
            return $"Corrupt {payloadLabel}: invalid rarity {rarityValue} for instance '{instanceId}'.";

        if (!EquipmentDurabilityRules.IsValidCurrentDurability(currentDurability, rarityValue))
            return $"Corrupt {payloadLabel}: invalid current_durability {currentDurability} for rarity {rarityValue} on instance '{instanceId}'.";

        string usageValidationError = _get_usage_periods_payload_validation_error(
            payload["ability_usage_periods"],
            payloadLabel
        );
        if (usageValidationError.Length > 0)
            return usageValidationError;

        string counterValidationError = _get_persistent_counters_payload_validation_error(
            payload["ability_persistent_counters"],
            payloadLabel
        );
        if (counterValidationError.Length > 0)
            return counterValidationError;

        return "";
    }

    private static Godot.Collections.Array _usage_periods_to_payload_array(
        IEnumerable<EquipmentAbilityUsagePeriodState> usagePeriods
    )
    {
        var payload = new Godot.Collections.Array();
        if (usagePeriods == null)
            return payload;
        foreach (EquipmentAbilityUsagePeriodState usage in usagePeriods)
        {
            if (usage == null)
                continue;
            payload.Add(
                new Godot.Collections.Dictionary
                {
                    { "ability_id", usage.AbilityId ?? "" },
                    { "period_kind", usage.PeriodKind ?? "" },
                    { "period_index", usage.PeriodIndex },
                    { "used_count", usage.UsedCount },
                }
            );
        }
        return payload;
    }

    private static Godot.Collections.Array _persistent_counters_to_payload_array(
        IEnumerable<EquipmentAbilityPersistentCounterState> counters
    )
    {
        var payload = new Godot.Collections.Array();
        if (counters == null)
            return payload;
        foreach (EquipmentAbilityPersistentCounterState counter in counters)
        {
            if (counter == null)
                continue;
            payload.Add(
                new Godot.Collections.Dictionary
                {
                    { "counter_id", counter.CounterId ?? "" },
                    { "value", counter.Value },
                }
            );
        }
        return payload;
    }

    private static List<EquipmentAbilityUsagePeriodState> _usage_periods_from_payload_array(
        Variant payload
    )
    {
        List<EquipmentAbilityUsagePeriodState> result = new();
        foreach (Variant entryValue in payload.AsGodotArray())
        {
            var entry = entryValue.AsGodotDictionary();
            result.Add(
                new EquipmentAbilityUsagePeriodState
                {
                    AbilityId = entry["ability_id"].AsString().StripEdges(),
                    PeriodKind = entry["period_kind"].AsString().StripEdges(),
                    PeriodIndex = entry["period_index"].AsInt32(),
                    UsedCount = entry["used_count"].AsInt32(),
                }
            );
        }
        return result;
    }

    private static List<EquipmentAbilityPersistentCounterState> _persistent_counters_from_payload_array(
        Variant payload
    )
    {
        List<EquipmentAbilityPersistentCounterState> result = new();
        foreach (Variant entryValue in payload.AsGodotArray())
        {
            var entry = entryValue.AsGodotDictionary();
            result.Add(
                new EquipmentAbilityPersistentCounterState
                {
                    CounterId = entry["counter_id"].AsString().StripEdges(),
                    Value = entry["value"].AsInt64(),
                }
            );
        }
        return result;
    }

    private static List<EquipmentAbilityUsagePeriodState> _duplicate_usage_periods(
        IEnumerable<EquipmentAbilityUsagePeriodState> usagePeriods
    )
    {
        List<EquipmentAbilityUsagePeriodState> result = new();
        if (usagePeriods == null)
            return result;
        foreach (EquipmentAbilityUsagePeriodState usage in usagePeriods)
            if (usage != null)
                result.Add(usage.DuplicateState());
        return result;
    }

    private static List<EquipmentAbilityPersistentCounterState> _duplicate_persistent_counters(
        IEnumerable<EquipmentAbilityPersistentCounterState> counters
    )
    {
        List<EquipmentAbilityPersistentCounterState> result = new();
        if (counters == null)
            return result;
        foreach (EquipmentAbilityPersistentCounterState counter in counters)
            if (counter != null)
                result.Add(counter.DuplicateState());
        return result;
    }

    private static string _get_usage_periods_payload_validation_error(
        Variant payload,
        string payloadLabel
    )
    {
        int index = 0;
        foreach (Variant entryValue in payload.AsGodotArray())
        {
            string entryLabel = $"{payloadLabel} ability_usage_periods[{index}]";
            if (entryValue.VariantType != Variant.Type.Dictionary)
                return $"Corrupt {entryLabel}: expected Dictionary.";

            var entry = entryValue.AsGodotDictionary();
            string fieldError = _get_exact_payload_fields_validation_error(
                entry,
                new[] { "ability_id", "period_kind", "period_index", "used_count" },
                entryLabel
            );
            if (fieldError.Length > 0)
                return fieldError;

            if (entry["ability_id"].VariantType != Variant.Type.String)
                return $"Corrupt {entryLabel}: ability_id must be String.";
            if (entry["period_kind"].VariantType != Variant.Type.String)
                return $"Corrupt {entryLabel}: period_kind must be String.";
            if (entry["period_index"].VariantType != Variant.Type.Int)
                return $"Corrupt {entryLabel}: period_index must be int.";
            if (entry["used_count"].VariantType != Variant.Type.Int)
                return $"Corrupt {entryLabel}: used_count must be int.";

            string abilityId = entry["ability_id"].AsString().StripEdges();
            string periodKind = entry["period_kind"].AsString().StripEdges();
            long periodIndex = entry["period_index"].AsInt64();
            long usedCount = entry["used_count"].AsInt64();

            if (abilityId.Length == 0)
                return $"Corrupt {entryLabel}: ability_id is required.";
            if (!_is_valid_period_kind(periodKind))
                return $"Corrupt {entryLabel}: invalid period_kind '{periodKind}'.";
            if (periodIndex < 0 || periodIndex > int.MaxValue)
                return $"Corrupt {entryLabel}: period_index must be a non-negative int.";
            if (usedCount < 0 || usedCount > int.MaxValue)
                return $"Corrupt {entryLabel}: used_count must be a non-negative int.";

            index++;
        }
        return "";
    }

    private static string _get_persistent_counters_payload_validation_error(
        Variant payload,
        string payloadLabel
    )
    {
        int index = 0;
        foreach (Variant entryValue in payload.AsGodotArray())
        {
            string entryLabel = $"{payloadLabel} ability_persistent_counters[{index}]";
            if (entryValue.VariantType != Variant.Type.Dictionary)
                return $"Corrupt {entryLabel}: expected Dictionary.";

            var entry = entryValue.AsGodotDictionary();
            string fieldError = _get_exact_payload_fields_validation_error(
                entry,
                new[] { "counter_id", "value" },
                entryLabel
            );
            if (fieldError.Length > 0)
                return fieldError;

            if (entry["counter_id"].VariantType != Variant.Type.String)
                return $"Corrupt {entryLabel}: counter_id must be String.";
            if (entry["value"].VariantType != Variant.Type.Int)
                return $"Corrupt {entryLabel}: value must be int.";

            string counterId = entry["counter_id"].AsString().StripEdges();
            long value = entry["value"].AsInt64();

            if (counterId.Length == 0)
                return $"Corrupt {entryLabel}: counter_id is required.";
            if (value < 0)
                return $"Corrupt {entryLabel}: value must be non-negative.";

            index++;
        }
        return "";
    }

    private static string _get_exact_payload_fields_validation_error(
        Godot.Collections.Dictionary payload,
        string[] requiredFields,
        string payloadLabel
    )
    {
        foreach (string fieldName in requiredFields)
        {
            if (!payload.ContainsKey(fieldName))
                return $"Corrupt {payloadLabel}: missing required field '{fieldName}'.";
        }

        if (payload.Count != requiredFields.Length)
            return $"Corrupt {payloadLabel}: expected exactly current fields.";

        foreach (Variant keyValue in payload.Keys)
        {
            if (
                keyValue.VariantType != Variant.Type.String
                || !_contains_required_field(requiredFields, keyValue.AsString())
            )
                return $"Corrupt {payloadLabel}: unsupported field '{keyValue}'.";
        }

        return "";
    }

    private static bool _contains_required_field(string[] requiredFields, string fieldName)
    {
        foreach (string requiredField in requiredFields)
            if (requiredField == fieldName)
                return true;
        return false;
    }

    private static bool _is_valid_period_kind(string periodKind) =>
        periodKind == PeriodKindPerWorldDay || periodKind == PeriodKindPerWorldMonth;

    public static bool IsValidRarity(int value) =>
        value >= (int)RarityTier.COMMON && value <= (int)RarityTier.LEGENDARY;

}
