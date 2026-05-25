using Godot;

[GlobalClass]
public partial class EquipmentInstanceState : RefCounted
{
    public enum RarityTier { COMMON, UNCOMMON, RARE, EPIC, LEGENDARY }

    public static int RARITY_TIER_COMMON() => (int)RarityTier.COMMON;
    public static int RARITY_TIER_UNCOMMON() => (int)RarityTier.UNCOMMON;
    public static int RARITY_TIER_RARE() => (int)RarityTier.RARE;
    public static int RARITY_TIER_EPIC() => (int)RarityTier.EPIC;
    public static int RARITY_TIER_LEGENDARY() => (int)RarityTier.LEGENDARY;

    private const string SAVE_PAYLOAD_LABEL = "save equipment instance payload";
    private const string TRANSIENT_LOOT_PAYLOAD_LABEL = "transient loot equipment instance payload";

    public StringName instance_id = "";
    public StringName item_id = "";
    public int rarity = (int)RarityTier.COMMON;
    public int current_durability = EquipmentDurabilityRules.get_default_current_durability((int)RarityTier.COMMON);

    public static EquipmentInstanceState create(StringName pItemId, StringName pInstanceId = default)
    {
        var inst = new EquipmentInstanceState();
        inst.instance_id = ProgressionDataUtils.to_string_name(pInstanceId);
        inst.item_id = ProgressionDataUtils.to_string_name(pItemId);
        inst.current_durability = EquipmentDurabilityRules.get_default_current_durability(inst.rarity);
        return inst;
    }

    public static StringName format_instance_id(int serial) => new StringName($"eq_{Mathf.Max(serial, 1):D6}");
    public static StringName format_preview_instance_id(int serial) => new StringName($"__preview_eq_{Mathf.Max(serial, 1):D6}");

    public Godot.Collections.Dictionary to_dict()
    {
        return new Godot.Collections.Dictionary { { "instance_id", (string)instance_id }, { "item_id", (string)item_id }, { "rarity", rarity }, { "current_durability", current_durability } };
    }

    public static EquipmentInstanceState from_dict(Variant data) => _from_dict(data, false, SAVE_PAYLOAD_LABEL);
    public static EquipmentInstanceState from_transient_loot_dict(Variant data) => _from_dict(data, true, TRANSIENT_LOOT_PAYLOAD_LABEL);
    public static string get_payload_validation_error(Variant data) => get_payload_validation_error(data, false);
    public static string get_payload_validation_error(Variant data, bool allowEmptyInstanceId) => _get_payload_validation_error(data, allowEmptyInstanceId, SAVE_PAYLOAD_LABEL);

    private static EquipmentInstanceState _from_dict(Variant data, bool allowEmptyInstanceId, string payloadLabel)
    {
        var validationError = _get_payload_validation_error(data, allowEmptyInstanceId, payloadLabel);
        if (validationError.Length > 0) { GD.PushError(validationError); return null; }
        var payload = data.AsGodotDictionary();
        return new EquipmentInstanceState { instance_id = ProgressionDataUtils.to_string_name(payload["instance_id"]), item_id = ProgressionDataUtils.to_string_name(payload["item_id"]), rarity = payload["rarity"].AsInt32(), current_durability = payload["current_durability"].AsInt32() };
    }

    private static string _get_payload_validation_error(Variant data, bool allowEmptyInstanceId, string payloadLabel)
    {
        if (data.VariantType != Variant.Type.Dictionary) return $"Corrupt {payloadLabel}: expected Dictionary, got {data.VariantType}.";
        var payload = data.AsGodotDictionary();
        var requiredFields = new[] { "instance_id", "item_id", "rarity", "current_durability" };
        foreach (string fn in requiredFields) { if (!payload.ContainsKey(fn)) return $"Corrupt {payloadLabel}: missing required field '{fn}'."; }
        if (payload.Count != requiredFields.Length) return $"Corrupt {payloadLabel}: expected exactly current equipment instance fields.";
        foreach (var keyVariant in payload.Keys) { if (keyVariant.VariantType != Variant.Type.String || !((System.Collections.Generic.IList<string>)requiredFields).Contains(keyVariant.AsString())) return $"Corrupt {payloadLabel}: unsupported field '{keyVariant}'."; }

        var instanceIdVar = payload["instance_id"];
        if (!_is_string_name_payload_value(instanceIdVar)) return $"Corrupt {payloadLabel}: instance_id must be String or StringName.";
        var itemIdVar = payload["item_id"];
        if (!_is_string_name_payload_value(itemIdVar)) return $"Corrupt {payloadLabel}: item_id must be String or StringName.";
        if (payload["rarity"].VariantType != Variant.Type.Int) return $"Corrupt {payloadLabel}: rarity must be int.";
        if (payload["current_durability"].VariantType != Variant.Type.Int) return $"Corrupt {payloadLabel}: current_durability must be int.";

        string instanceIdText = instanceIdVar.AsString().StripEdges();
        string itemIdText = itemIdVar.AsString().StripEdges();
        var instanceId = instanceIdText.Length > 0 ? new StringName(instanceIdText) : new StringName("");
        var itemId = itemIdText.Length > 0 ? new StringName(itemIdText) : new StringName("");
        int rarityValue = payload["rarity"].AsInt32();
        int currentDurability = payload["current_durability"].AsInt32();

        if (instanceId == "" && !allowEmptyInstanceId) return $"Corrupt {payloadLabel}: instance_id is required.";
        if (itemId == "") return $"Corrupt {payloadLabel}: item_id is required for instance '{instanceId}'.";
        if (!is_valid_rarity(rarityValue)) return $"Corrupt {payloadLabel}: invalid rarity {rarityValue} for instance '{instanceId}'.";
        if (!EquipmentDurabilityRules.is_valid_current_durability(currentDurability, rarityValue)) return $"Corrupt {payloadLabel}: invalid current_durability {currentDurability} for rarity {rarityValue} on instance '{instanceId}'.";
        return "";
    }

    public static bool is_valid_rarity(int value) => value >= (int)RarityTier.COMMON && value <= (int)RarityTier.LEGENDARY;

    private static bool _is_string_name_payload_value(Variant value) => value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName;
}
