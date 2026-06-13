using Godot;

internal enum AttributeModifierMode
{
    Unknown = 0,
    Flat,
    Percent,
}

[GlobalClass]
public partial class AttributeModifier : Resource
{
    private static readonly StringName ModeFlat = "flat";
    private static readonly StringName ModePercent = "percent";

    public static bool IsValidMode(StringName value)
    {
        return ToMode(value) != AttributeModifierMode.Unknown;
    }

    internal AttributeModifierMode ModeKind => ToMode(mode);

    internal static AttributeModifierMode ToMode(StringName value)
    {
        if (value == ModeFlat)
            return AttributeModifierMode.Flat;
        if (value == ModePercent)
            return AttributeModifierMode.Percent;
        return AttributeModifierMode.Unknown;
    }

    internal static StringName ToStringName(AttributeModifierMode mode)
    {
        return mode switch
        {
            AttributeModifierMode.Flat => ModeFlat,
            AttributeModifierMode.Percent => ModePercent,
            _ => "",
        };
    }

    [Export]
    public StringName attribute_id { get; set; } = "";

    [Export]
    public StringName mode { get; set; } = ModeFlat;

    [Export]
    public int value { get; set; }

    [Export]
    public int value_per_rank { get; set; }

    [Export]
    public StringName source_type { get; set; } = "";

    [Export]
    public StringName source_id { get; set; } = "";

    public int GetValueForRank(int rank)
    {
        int normalizedRank = Mathf.Max(rank, 1);
        return value + value_per_rank * (normalizedRank - 1);
    }

    public bool IsPercent()
    {
        return ModeKind == AttributeModifierMode.Percent;
    }

    public bool IsFlat()
    {
        return ModeKind == AttributeModifierMode.Flat;
    }
}
