using Godot;

[GlobalClass]
public partial class BattleAiMutationGuardTestAction : EnemyAiAction
{
    public StringName mutation_kind { get; set; } = "none";

    public void setup(StringName kind)
    {
        mutation_kind = kind;
        action_id = new StringName($"test_mutation_{kind}");
    }

    public override BattleAiDecision decide(BattleAiContext context)
    {
        if (context == null)
        {
            return null;
        }

        switch (mutation_kind.ToString())
        {
            case "active_hp":
                context.unit_state.current_hp = 1;
                break;
            case "other_coord":
                if (
                    context.state.units.ContainsKey(new StringName("hero"))
                    && context.state.units[new StringName("hero")].As<BattleUnitState>()
                        is BattleUnitState target
                )
                {
                    target.set_anchor_coord(new Vector2I(4, 2));
                }
                break;
            case "blackboard":
                context.unit_state.ai_blackboard.set_text("rogue_key", "should_not_persist");
                break;
            case "cell_occupant":
                context.grid_service.set_occupant(
                    context.state,
                    new Vector2I(3, 1),
                    context.unit_state.unit_id
                );
                break;
            case "cell_height":
                context.grid_service.set_height_offset(context.state, new Vector2I(0, 0), 2);
                break;
        }

        BattleCommand command = new()
        {
            command_type = BattleCommand.TYPE_WAIT(),
            unit_id = context.unit_state.unit_id,
        };
        return new BattleAiDecision
        {
            command = command,
            action_id = action_id,
            reason_text = "test mutation action",
        };
    }

    public override Godot.Collections.Array<string> validate_schema() => new();
}
