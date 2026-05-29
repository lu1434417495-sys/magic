using Godot;

[GlobalClass]
public partial class BattleAiDecisionCommitterTestAction : EnemyAiAction
{
    public void setup()
    {
        action_id = "commit_probe";
    }

    public override BattleAiDecision decide(BattleAiContext context)
    {
        if (context == null)
        {
            return null;
        }
        BattleCommand command = new()
        {
            command_type = BattleCommand.TYPE_WAIT(),
            unit_id = context.unit_state?.unit_id ?? "",
        };
        return new BattleAiDecision
        {
            command = command,
            action_id = action_id,
            reason_text = "commit probe",
        };
    }

    public override Godot.Collections.Array<string> validate_schema() => new();
}
