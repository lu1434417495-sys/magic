using Godot;
using System;
using System.Collections.Generic;

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

    public StringName instance_id = "";

    public StringName item_id = "";

    public int rarity = (int)RarityTier.COMMON;

    public int current_durability = EquipmentDurabilityRules.GetDefaultCurrentDurability(
        (int)RarityTier.COMMON
    );

    public List<TraitInstanceState> trait_instances = new();

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
        return "";
    }

    public static bool IsValidRarity(int value) =>
        value >= (int)RarityTier.COMMON && value <= (int)RarityTier.LEGENDARY;

}
