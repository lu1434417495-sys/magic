using Godot;

[GlobalClass]
public partial class MoveToMultiUnitSkillPositionAction : UseMultiUnitSkillAction
{
    [Export]
    public int target_count_weight { get; set; } = 40;

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        AiTraceRecorder.Enter("decide:move_to_multi_unit_skill_position");
        BattleAiDecision decision =
            new BattleAiMoveToMultiUnitSkillPositionEvaluator().Evaluate(this, context);
        AiTraceRecorder.Exit("decide:move_to_multi_unit_skill_position");
        return decision;
    }

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        Godot.Collections.Array<string> errors = base.ValidateSchema();
        if (target_count_weight < 0)
        {
            errors.Add(
                $"MoveToMultiUnitSkillPositionAction {action_id} target_count_weight must be >= 0."
            );
        }
        return errors;
    }
}
