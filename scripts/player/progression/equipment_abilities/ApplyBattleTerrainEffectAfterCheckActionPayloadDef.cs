using Godot;

[GlobalClass]
public sealed partial class ApplyBattleTerrainEffectAfterCheckActionPayloadDef : Resource
{
    [Export] public StringName anchor_selector { get; set; } = "";
    [Export] public StringName terrain_effect_id { get; set; } = "";
    [Export] public int move_cost_delta { get; set; }
    [Export] public StringName target_team_filter { get; set; } = "any";
    [Export] public StringName stack_behavior { get; set; } = "refresh";
    [Export] public string display_name { get; set; } = "";
    [Export] public StringName render_overlay_id { get; set; } = "";
    [Export] public int overlay_priority { get; set; }
    [Export] public StringName check_attribute_modifier_id { get; set; } = "";
    [Export] public StringName check_compare { get; set; } = "";
    [Export] public int check_threshold { get; set; }
    [Export] public bool natural_twenty_auto_success { get; set; }
    [Export] public bool natural_one_auto_failure { get; set; }
}
