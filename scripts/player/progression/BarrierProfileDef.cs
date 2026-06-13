using Godot;

internal enum BarrierAnchorMode
{
    Unknown = 0,
    Fixed,
}

[GlobalClass]
public partial class BarrierProfileDef : Resource
{
    private static readonly StringName AnchorModeFixed = "fixed";

    [Export]
    public StringName profile_id = "";

    [Export]
    public string display_name = "";

    [Export]
    public StringName anchor_mode = "fixed";
    internal BarrierAnchorMode AnchorModeKind
    {
        get => ToAnchorMode(anchor_mode);
        set => anchor_mode = ToStringName(value);
    }

    [Export]
    public StringName area_pattern = "diamond";
    internal BattleAreaPattern AreaPatternKind
    {
        get => BattleTypedNames.ToAreaPattern(area_pattern);
        set => area_pattern = BattleTypedNames.ToStringName(value);
    }

    [Export]
    public int radius_cells;

    [Export]
    public int duration_tu;

    [Export]
    public bool catch_all_projected_effects;

    [Export]
    public Godot.Collections.Array<BarrierLayerDef> layers = new();

    public Godot.Collections.Array<BarrierLayerDef> GetOrderedLayers()
    {
        var list = new System.Collections.Generic.List<BarrierLayerDef>();
        foreach (var layer in layers)
            if (layer != null)
                list.Add(layer);
        list.Sort((left, right) => left.order.CompareTo(right.order));
        var result = new Godot.Collections.Array<BarrierLayerDef>();
        foreach (var item in list)
            result.Add(item);
        return result;
    }

    internal static BarrierAnchorMode ToAnchorMode(StringName value)
    {
        if (value == AnchorModeFixed)
            return BarrierAnchorMode.Fixed;
        return BarrierAnchorMode.Unknown;
    }

    internal static StringName ToStringName(BarrierAnchorMode mode)
    {
        return mode switch
        {
            BarrierAnchorMode.Fixed => AnchorModeFixed,
            _ => "",
        };
    }
}
