using Godot;

[GlobalClass]
public partial class AttributeModifier : Resource
{
    private static readonly StringName ModeFlat = "flat";
    private static readonly StringName ModePercent = "percent";

    public static StringName MODE_FLAT() => ModeFlat;
    public static StringName MODE_PERCENT() => ModePercent;

    public static bool is_valid_mode(Variant value)
    {
        StringName normalized = ToStringName(value);
        return normalized == ModeFlat || normalized == ModePercent;
    }

    [Export] public StringName attribute_id { get; set; } = "";
    [Export] public StringName mode { get; set; } = ModeFlat;
    [Export] public int value { get; set; }
    [Export] public int value_per_rank { get; set; }
    [Export] public StringName source_type { get; set; } = "";
    [Export] public StringName source_id { get; set; } = "";

    public int get_value_for_rank(int rank)
    {
        int normalizedRank = Mathf.Max(rank, 1);
        return value + value_per_rank * (normalizedRank - 1);
    }

    public bool is_percent()
    {
        return mode == ModePercent;
    }

    public bool is_flat()
    {
        return !is_percent();
    }

    private static StringName ToStringName(Variant value)
    {
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName();
        if (value.VariantType == Variant.Type.String)
            return new StringName(value.AsString());
        return "";
    }
}
