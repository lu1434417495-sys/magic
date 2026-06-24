using Godot;

public class WarehouseStackState
{
    public static readonly string[] TO_DICT_FIELDS =
    {
        "item_id",
        "quantity",
    };

    public StringName item_id = "";
    public int quantity;

    public bool IsEmpty() => (string)item_id == "" || quantity <= 0;

    public WarehouseStackState DuplicateState()
    {
        return new WarehouseStackState { item_id = item_id, quantity = Mathf.Max(quantity, 0) };
    }

    public Godot.Collections.Dictionary ToDictionary() =>
        new() { { "item_id", (string)item_id }, { "quantity", Mathf.Max(quantity, 0) } };

    public static WarehouseStackState FromDictionary(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            return null;
        if (payload.Count != 2)
            return null;
        if (!payload.ContainsKey("item_id") || !payload.ContainsKey("quantity"))
            return null;
        Variant itemIdValue = payload["item_id"];
        if (itemIdValue.VariantType != Variant.Type.String)
            return null;
        if (payload["quantity"].VariantType != Variant.Type.Int)
            return null;
        string itemIdText = itemIdValue.AsString().StripEdges();
        int qv = payload["quantity"].AsInt32();
        if (itemIdText.Length == 0 || qv <= 0)
            return null;
        return new WarehouseStackState { item_id = new StringName(itemIdText), quantity = qv };
    }
}
