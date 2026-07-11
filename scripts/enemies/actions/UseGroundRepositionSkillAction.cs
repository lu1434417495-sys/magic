using Godot;

[GlobalClass]
public partial class UseGroundRepositionSkillAction : EnemyAiAction
{
    [Export]
    public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int minimum_safe_distance { get; set; } = 3;

    [Export]
    public int safe_distance_margin { get; set; } = 1;

    [Export]
    public int desired_max_distance_bonus { get; set; } = 2;

    [Export]
    public int action_base_score { get; set; } = 1500;

    // Minimum survival-margin improvement an escape must buy when there is no lethal risk.
    [Export]
    public int min_survival_margin_gain_to_escape { get; set; } = 1;

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
        {
            errors.Add(
                $"UseGroundRepositionSkillAction {action_id} must declare at least one skill_id."
            );
        }
        if (target_selector == "")
            errors.Add($"UseGroundRepositionSkillAction {action_id} is missing target_selector.");
        _append_enemy_focus_target_selector_errors(
            errors,
            "UseGroundRepositionSkillAction",
            target_selector
        );
        if (minimum_safe_distance <= 0)
        {
            errors.Add(
                $"UseGroundRepositionSkillAction {action_id} minimum_safe_distance must be >= 1."
            );
        }
        if (safe_distance_margin < 0)
        {
            errors.Add(
                $"UseGroundRepositionSkillAction {action_id} safe_distance_margin must be >= 0."
            );
        }
        if (desired_max_distance_bonus < 0)
        {
            errors.Add(
                $"UseGroundRepositionSkillAction {action_id} desired_max_distance_bonus must be >= 0."
            );
        }
        return errors;
    }
}
