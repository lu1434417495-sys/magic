using Godot;

[GlobalClass]
public partial class CombatEffectDef : Resource
{
    private const double MinJumpArcRatio = 0.15;

    public static double MIN_JUMP_ARC_RATIO() => MinJumpArcRatio;

    [Export] public StringName effect_type { get; set; } = "";
    [Export] public StringName tick_effect_type { get; set; } = "";
    [Export] public int power { get; set; }
    [Export] public int min_skill_level { get; set; }
    [Export] public int max_skill_level { get; set; } = -1;
    [Export] public int damage_ratio_percent { get; set; } = 100;
    [Export] public StringName damage_tag { get; set; } = "";
    [Export] public Godot.Collections.Array effect_categories { get; set; } = new();
    [Export] public StringName effect_target_team_filter { get; set; } = "";
    [Export] public StringName status_id { get; set; } = "";
    [Export] public StringName terrain_effect_id { get; set; } = "";
    [Export] public StringName terrain_replace_to { get; set; } = "";
    [Export] public int height_delta { get; set; }
    [Export] public StringName body_size_category { get; set; } = "";
    [Export] public StringName forced_move_mode { get; set; } = "";
    [Export] public int forced_move_distance { get; set; }
    [Export] public int jump_base_budget { get; set; }
    [Export] public double jump_str_scale { get; set; }
    [Export] public double jump_arc_ratio { get; set; }
    [Export] public int jump_range_multiplier { get; set; } = 1;
    [Export] public int duration_tu { get; set; }
    [Export] public int tick_interval_tu { get; set; }
    [Export] public StringName stack_behavior { get; set; } = "refresh";
    [Export] public int stack_limit { get; set; }
    [Export] public StringName bonus_condition { get; set; } = "";
    [Export] public StringName trigger_event { get; set; } = "";
    [Export] public StringName trigger_condition { get; set; } = "";
    [Export] public StringName trigger_status_id { get; set; } = "";
    [Export] public int save_dc { get; set; }
    [Export] public StringName save_dc_mode { get; set; } = "static";
    [Export] public StringName save_dc_source_ability { get; set; } = "";
    [Export] public StringName save_ability { get; set; } = "";
    [Export] public StringName save_failure_status_id { get; set; } = "";
    [Export] public bool save_partial_on_success { get; set; }
    [Export] public StringName save_tag { get; set; } = "";
    [Export] public StringName consumed_status_id { get; set; } = "";
    [Export] public int dice_per_consumed_stack { get; set; }
    [Export] public int dice_sides_per_stack { get; set; }
    [Export] public Godot.Collections.Dictionary @params { get; set; } = new();

    public CombatEffectDef duplicate_for_runtime()
    {
        var copy = (CombatEffectDef)Duplicate(true);
        if (copy == null) return null;
        copy.@params = copy.@params != null ? copy.@params.Duplicate(true) : new Godot.Collections.Dictionary();
        return copy;
    }
}
