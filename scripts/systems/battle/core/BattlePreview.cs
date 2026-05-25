using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattlePreview : RefCounted
{
    public bool allowed { get; set; } = false;
    public GArray log_lines { get; set; } = new();
    public Godot.Collections.Array<StringName> target_unit_ids { get; set; } = new();
    public Godot.Collections.Array<Vector2I> target_coords { get; set; } = new();
    public Godot.Collections.Array<StringName> random_chain_candidate_unit_ids { get; set; } = new();
    public Vector2I resolved_anchor_coord { get; set; } = new Vector2I(-1, -1);
    public int move_cost { get; set; } = 0;
    public GDictionary hit_preview { get; set; } = new();
    public GDictionary damage_preview { get; set; } = new();
    public BattleSpecialProfileGateResult special_profile_gate_result { get; set; }
    public BattleSpecialProfilePreviewFacts special_profile_preview_facts { get; set; }
}
