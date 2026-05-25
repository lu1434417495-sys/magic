using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleBarrierInstanceState : RefCounted
{
    public StringName barrier_instance_id { get; set; } = "";
    public StringName profile_id { get; set; } = "";
    public string display_name { get; set; } = "";
    public StringName source_unit_id { get; set; } = "";
    public StringName source_skill_id { get; set; } = "";
    public StringName anchor_mode { get; set; } = "fixed";
    public Vector2I anchor_coord { get; set; } = Vector2I.Zero;
    public int radius_cells { get; set; }
    public StringName area_pattern { get; set; } = "diamond";
    public int remaining_tu { get; set; }
    public int created_tu { get; set; }
    public int save_dc { get; set; }
    public bool catch_all_projected_effects { get; set; }
    public GArray layers { get; set; } = new();

    public GDictionary to_runtime_dict()
    {
        return new GDictionary
        {
            ["barrier_instance_id"] = barrier_instance_id.ToString(),
            ["profile_id"] = profile_id.ToString(),
            ["display_name"] = display_name,
            ["source_unit_id"] = source_unit_id.ToString(),
            ["source_skill_id"] = source_skill_id.ToString(),
            ["anchor_mode"] = anchor_mode.ToString(),
            ["anchor_coord"] = anchor_coord,
            ["radius_cells"] = radius_cells,
            ["area_pattern"] = area_pattern.ToString(),
            ["remaining_tu"] = remaining_tu,
            ["created_tu"] = created_tu,
            ["save_dc"] = save_dc,
            ["catch_all_projected_effects"] = catch_all_projected_effects,
            ["layers"] = layers.Duplicate(true),
        };
    }
}
