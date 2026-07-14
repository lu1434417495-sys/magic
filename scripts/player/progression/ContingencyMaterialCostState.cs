using Godot;
using GDictionary = Godot.Collections.Dictionary;

public class ContingencyMaterialCostState
{
    private static readonly string[] PayloadKeys = { "item_id", "quantity" };

    public StringName ItemId { get; private set; } = "";
    public int Quantity { get; private set; }

    public static ContingencyMaterialCostState Create(StringName itemId, int quantity)
    {
        if (itemId == "" || quantity <= 0)
            return null;
        return new ContingencyMaterialCostState
        {
            ItemId = itemId,
            Quantity = quantity,
        };
    }

    public ContingencyMaterialCostState DuplicateState() => Create(ItemId, Quantity);

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["item_id"] = ItemId.ToString(),
            ["quantity"] = Quantity,
        };
    }

    public static ContingencyMaterialCostState FromDictionary(GDictionary payload)
    {
        if (!ContingencySchemaUtils.HasExactKeys(payload, PayloadKeys))
            return null;
        if (!ContingencySchemaUtils.TryReadStringName(payload, "item_id", false, out StringName itemId))
            return null;
        if (!ContingencySchemaUtils.TryReadInt(payload, "quantity", out int quantity) || quantity <= 0)
            return null;
        return Create(itemId, quantity);
    }
}

internal static class ContingencySchemaUtils
{
    internal static bool HasExactKeys(GDictionary payload, string[] expectedKeys)
    {
        if (payload == null || payload.Count != expectedKeys.Length)
            return false;
        foreach (string key in expectedKeys)
            if (!payload.ContainsKey(key))
                return false;
        return true;
    }

    internal static bool TryReadBool(GDictionary payload, string key, out bool value)
    {
        value = false;
        if (!TryGetValue(payload, key, out Variant rawValue) || rawValue.VariantType != Variant.Type.Bool)
            return false;
        value = rawValue.AsBool();
        return true;
    }

    internal static bool TryReadInt(GDictionary payload, string key, out int value)
    {
        value = 0;
        if (!TryGetValue(payload, key, out Variant rawValue) || rawValue.VariantType != Variant.Type.Int)
            return false;
        value = rawValue.AsInt32();
        return true;
    }

    internal static bool TryReadStrictString(GDictionary payload, string key, out string value)
    {
        value = "";
        if (!TryGetValue(payload, key, out Variant rawValue) || rawValue.VariantType != Variant.Type.String)
            return false;
        value = rawValue.AsString();
        return true;
    }

    internal static bool TryReadStringName(
        GDictionary payload,
        string key,
        bool allowEmpty,
        out StringName value
    )
    {
        value = "";
        if (!TryGetValue(payload, key, out Variant rawValue))
            return false;
        if (!TryAsStringLike(rawValue, out string text))
            return false;
        value = new StringName(text);
        return allowEmpty || value != "";
    }

    internal static bool TryReadDictionary(GDictionary payload, string key, out GDictionary value)
    {
        value = new GDictionary();
        if (!TryGetValue(payload, key, out Variant rawValue) || rawValue.VariantType != Variant.Type.Dictionary)
            return false;
        value = rawValue.AsGodotDictionary();
        return true;
    }

    internal static bool TryReadArray(GDictionary payload, string key, out Godot.Collections.Array value)
    {
        value = new Godot.Collections.Array();
        if (!TryGetValue(payload, key, out Variant rawValue) || rawValue.VariantType != Variant.Type.Array)
            return false;
        value = rawValue.AsGodotArray();
        return true;
    }

    internal static bool TryAsStringLike(Variant rawValue, out string value)
    {
        if (rawValue.VariantType == Variant.Type.String)
        {
            value = rawValue.AsString();
            return true;
        }
        if (rawValue.VariantType == Variant.Type.StringName)
        {
            value = rawValue.AsStringName().ToString();
            return true;
        }
        value = "";
        return false;
    }

    internal static Godot.Collections.Array<string> ToStringArray(
        Godot.Collections.Array<StringName> values
    )
    {
        var result = new Godot.Collections.Array<string>();
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }

    private static bool TryGetValue(GDictionary payload, string key, out Variant value)
    {
        value = default;
        if (payload == null || !payload.ContainsKey(key))
            return false;
        value = payload[key];
        return true;
    }
}
