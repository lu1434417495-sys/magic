using Godot;

[GlobalClass]
public partial class WaitAction : EnemyAiAction
{
    [Export]
    public int active_rest_action_base_score { get; set; } = 10;

    [Export]
    public int active_rest_min_stamina_residue { get; set; } = 1;

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = _collect_base_validation_errors();
        if (active_rest_action_base_score < -1000)
        {
            errors.Add(
                $"WaitAction {action_id} active_rest_action_base_score is unexpectedly low."
            );
        }
        if (active_rest_min_stamina_residue < 0)
        {
            errors.Add(
                $"WaitAction {action_id} active_rest_min_stamina_residue must be >= 0."
            );
        }
        return errors;
    }
}
