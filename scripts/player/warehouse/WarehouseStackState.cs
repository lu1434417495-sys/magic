using Godot;

[GlobalClass]
public partial class WarehouseStackState : RefCounted
{
    public static readonly Godot.Collections.Array<string> TO_DICT_FIELDS = new() { "item_id", "quantity" };

    public StringName item_id = "";
    public int quantity;

    public bool is_empty() => (string)item_id == "" || quantity <= 0;

    public WarehouseStackState duplicate_state() => from_dict(to_dict());

    public Godot.Collections.Dictionary to_dict() => new()
    {
        {"item_id", (string)item_id},
        {"quantity", Mathf.Max(quantity, 0)},
    };

    public static WarehouseStackState from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return null;
        var payload = data.AsGodotDictionary();
        if (payload.Count != 2) return null;
        if (!payload.ContainsKey("item_id") || !payload.ContainsKey("quantity")) return null;
        if (!_is_string_name_payload_value(payload["item_id"])) return null;
        if (payload["quantity"].VariantType != Variant.Type.Int) return null;
        var normalized = ProgressionDataUtils.to_string_name(payload["item_id"]);
        int qv = payload["quantity"].AsInt32();
        if ((string)normalized == "" || qv <= 0) return null;
        return new WarehouseStackState { item_id = normalized, quantity = qv };
    }

    private static bool _is_string_name_payload_value(Variant value)
    {
        var vt = (long)value.VariantType;
        return vt == (long)Variant.Type.String || vt == (long)Variant.Type.StringName;
    }
}
