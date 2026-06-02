using Godot;

[GlobalClass]
public partial class CombatCastVariantDef : Resource
{
    [Export]
    public StringName variant_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string description { get; set; } = "";

    [Export]
    public int min_skill_level { get; set; }

    [Export]
    public StringName target_mode { get; set; } = "ground";

    [Export]
    public StringName footprint_pattern { get; set; } = "single";

    [Export]
    public int required_coord_count { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<StringName> allowed_base_terrains { get; set; } = new();

    [Export]
    public Godot.Collections.Array<CombatEffectDef> effect_defs { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary @params { get; set; } = new();
}
