using Godot;
using Godot.Collections;

[GlobalClass]
public partial class BattleSpecialProfilePreviewFacts : RefCounted
{
    public StringName profile_id = "";
    public StringName skill_id = "";
    public StringName preview_fact_id = "";
    public string nominal_plan_signature = "";
    public string final_plan_signature = "";
    public Vector2I resolved_anchor_coord = new(-1, -1);
    public Array<StringName> target_unit_ids = new();
    public Array<Vector2I> target_coords = new();
    public Dictionary terrain_summary = new();
    public Array<Dictionary> friendly_fire_numeric_summary = new();
    public Array<Dictionary> attack_roll_modifier_breakdown = new();

    public virtual Dictionary ToDict()
    {
        return new Dictionary()
        {
            { "profile_id", (string)profile_id },
            { "skill_id", (string)skill_id },
            { "preview_fact_id", (string)preview_fact_id },
            { "nominal_plan_signature", nominal_plan_signature },
            { "final_plan_signature", final_plan_signature },
            { "resolved_anchor_coord", resolved_anchor_coord },
            { "target_unit_ids", target_unit_ids.Duplicate() },
            { "target_coords", target_coords.Duplicate() },
            { "terrain_summary", terrain_summary.Duplicate(true) },
            { "friendly_fire_numeric_summary", friendly_fire_numeric_summary.Duplicate(true) },
            { "attack_roll_modifier_breakdown", attack_roll_modifier_breakdown.Duplicate(true) },
        };
    }

    public Dictionary to_dict() => ToDict();

    public Array<Dictionary> GetFriendlyFireNumericSummary() =>
        friendly_fire_numeric_summary.Duplicate(true);

    public Array<Dictionary> get_friendly_fire_numeric_summary() => GetFriendlyFireNumericSummary();
}
