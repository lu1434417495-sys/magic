using Godot;

[GlobalClass]
public partial class UseChargePathAoeAction : EnemyAiAction
{
    [Export]
    public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int minimum_hit_count { get; set; } = 1;

    [Export]
    public int desired_min_distance { get; set; } = 1;

    [Export]
    public int desired_max_distance { get; set; } = 1;

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        AiTraceRecorder.Enter("decide:charge_path_aoe");
        BattleAiDecision decision =
            new BattleAiChargePathAoeActionEvaluator().Evaluate(this, context);
        AiTraceRecorder.Exit("decide:charge_path_aoe");
        return decision;
    }

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
        {
            errors.Add($"UseChargePathAoeAction {action_id} must declare at least one skill_id.");
        }
        if (target_selector == "")
        {
            errors.Add($"UseChargePathAoeAction {action_id} is missing target_selector.");
        }
        _append_enemy_focus_target_selector_errors(errors, "UseChargePathAoeAction", target_selector);
        if (minimum_hit_count <= 0)
        {
            errors.Add($"UseChargePathAoeAction {action_id} minimum_hit_count must be >= 1.");
        }
        if (desired_min_distance < 0)
        {
            errors.Add($"UseChargePathAoeAction {action_id} desired_min_distance must be >= 0.");
        }
        if (desired_max_distance < desired_min_distance)
        {
            errors.Add(
                $"UseChargePathAoeAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        }
        return errors;
    }
}
