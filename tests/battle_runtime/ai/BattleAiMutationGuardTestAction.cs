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

    internal override BattleAiDecision Decide(BattleAiContext context)
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
                    context.state.ContainsUnit(new StringName("hero"))
                    && context.state.GetUnit(new StringName("hero"))
                        is BattleUnitState target
                )
                {
                    target.SetAnchorCoord(new Vector2I(4, 2));
                }
                break;
            case "blackboard":
                context.unit_state.ai_blackboard.SetText("rogue_key", "should_not_persist");
                break;
            case "cell_occupant":
                context.grid_service.SetOccupant(
                    context.state,
                    new Vector2I(3, 1),
                    context.unit_state.unit_id
                );
                break;
            case "cell_height":
                context.grid_service.SetHeightOffset(context.state, new Vector2I(0, 0), 2);
                break;
            case "effective_trait":
                if (context.unit_state.effective_trait_instances.Count > 0)
                {
                    context.unit_state.effective_trait_instances = TraitTestData.EffectiveTraits(
                        TraitTestData.EffectiveTrait(
                            "halfling_luck",
                            "halfling_luck",
                            "on_crit",
                            "per_turn",
                            "turn_start",
                            effectType: "savage_attacks",
                            sourceType: "character",
                            sourceId: "guard_actor"
                        )
                    );
                }
                break;
            case "effective_trait_ids":
                context.unit_state.effective_trait_ids.Add("rogue_trait");
                break;
        }

        BattleCommand command = new()
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Wait),
            unit_id = context.unit_state.unit_id,
        };
        return new BattleAiDecision
        {
            command = command,
            action_id = action_id,
            reason_text = "test mutation action",
        };
    }

    public override Godot.Collections.Array<string> ValidateSchema() => new();
}
