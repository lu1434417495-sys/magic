using Godot;

[GlobalClass]
public partial class BarrierProfileDef : Resource
{
    [Export]
    public StringName profile_id = "";

    [Export]
    public string display_name = "";

    [Export]
    public StringName anchor_mode = "fixed";

    [Export]
    public StringName area_pattern = "diamond";

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

    public Godot.Collections.Array<BarrierLayerDef> get_ordered_layers()
    {
        return GetOrderedLayers();
    }
}
