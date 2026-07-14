using Godot;

[GlobalClass]
public sealed partial class ScheduleAreaEffectActionPayloadDef : Resource
{
    [Export] public StringName anchor_selector { get; set; } = "";
    [Export] public int delay_tu { get; set; }
    [Export] public StringName terrain_effect_id { get; set; } = "";
    [Export] public StringName area_pattern { get; set; } = "";
    [Export] public int area_value { get; set; }
    [Export] public StringName lifetime_policy { get; set; } = "timed";
    [Export] public StringName effect_type { get; set; } = "none";
    [Export] public StringName target_team_filter { get; set; } = "any";
    [Export] public StringName stack_behavior { get; set; } = "refresh";
    [Export] public string display_name { get; set; } = "";
    [Export] public StringName render_overlay_id { get; set; } = "";
    [Export] public int overlay_priority { get; set; }
    [Export] public StringName contact_status_id { get; set; } = "";
    [Export] public int contact_status_duration_tu { get; set; }
    [Export] public StringName contact_stack_behavior { get; set; } = "refresh";
    [Export] public int contact_stack_limit { get; set; }
    [Export] public string contact_status_display_label { get; set; } = "";
    [Export] public bool contact_counts_as_debuff_override { get; set; }
    [Export] public bool contact_counts_as_debuff { get; set; }
    [Export] public bool contact_undispellable { get; set; }
    [Export] public bool contact_dispellable_magic { get; set; }
    [Export] public bool contact_dispellable_harmful_magic { get; set; }
    [Export] public bool contact_dispellable_beneficial_magic { get; set; }
    [Export] public int contact_save_dc { get; set; }
    [Export] public StringName contact_save_ability { get; set; } = "";
    [Export] public StringName contact_save_tag { get; set; } = "";
    [Export] public bool contact_apply_on_save_failure { get; set; }
    [Export] public int contact_tick_interval_tu { get; set; }
    [Export] public int contact_timeline_damage_dice_count { get; set; }
    [Export] public int contact_timeline_damage_dice_sides { get; set; }
    [Export] public int contact_timeline_damage_flat_bonus { get; set; }
    [Export] public StringName contact_blocked_by_trait_id { get; set; } = "";
}
