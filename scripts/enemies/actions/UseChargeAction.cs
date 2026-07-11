using Godot;

[GlobalClass]
public partial class UseChargeAction : EnemyAiAction
{
    [Export]
    public StringName skill_id { get; set; } = "charge";

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int minimum_charge_move_distance { get; set; } = 3;

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (skill_id == "")
        {
            errors.Add($"UseChargeAction {action_id} is missing skill_id.");
        }
        if (target_selector == "")
        {
            errors.Add($"UseChargeAction {action_id} is missing target_selector.");
        }
        _append_enemy_focus_target_selector_errors(errors, "UseChargeAction", target_selector);
        if (minimum_charge_move_distance < 1)
        {
            errors.Add($"UseChargeAction {action_id} minimum_charge_move_distance must be >= 1.");
        }
        return errors;
    }
}
