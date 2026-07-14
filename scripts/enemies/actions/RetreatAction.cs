using Godot;

[GlobalClass]
public partial class RetreatAction : EnemyAiAction
{
    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int minimum_safe_distance { get; set; } = 3;

    [Export]
    public bool use_dynamic_threat_safe_distance { get; set; }

    [Export]
    public int safe_distance_margin { get; set; } = 1;

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (target_selector == "")
            errors.Add($"RetreatAction {action_id} is missing target_selector.");
        _append_enemy_focus_target_selector_errors(errors, "RetreatAction", target_selector);
        if (minimum_safe_distance <= 0)
            errors.Add($"RetreatAction {action_id} minimum_safe_distance must be >= 1.");
        if (safe_distance_margin < 0)
            errors.Add($"RetreatAction {action_id} safe_distance_margin must be >= 0.");
        return errors;
    }
}
