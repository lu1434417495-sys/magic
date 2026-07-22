using System.Collections.Generic;
using Godot;

internal sealed class BattleObjectiveEvaluationService
{
    internal BattleObjectiveEvaluationResult Evaluate(BattleState state)
    {
        if (state?.ObjectiveRuntimeState == null)
            return BattleObjectiveEvaluationResult.Invalid();

        return state.ObjectiveRuntimeState.Mode switch
        {
            BattleObjectiveMode.Elimination => EvaluateElimination(state),
            _ => BattleObjectiveEvaluationResult.Invalid(),
        };
    }

    private static BattleObjectiveEvaluationResult EvaluateElimination(
        BattleState state
    )
    {
        int livingAllies = CountLivingUnits(state, state.GetAllyUnitIdsTyped());
        int livingEnemies = CountLivingUnits(state, state.GetEnemyUnitIdsTyped());
        if (livingAllies > 0 && livingEnemies > 0)
            return BattleObjectiveEvaluationResult.InProgress();

        BattleOutcomeKind outcome;
        BattleEndReasonKind reason;
        if (livingAllies <= 0 && livingEnemies <= 0)
        {
            outcome = BattleOutcomeKind.Draw;
            reason = BattleEndReasonKind.EliminationMutualDestruction;
        }
        else if (livingEnemies <= 0)
        {
            outcome = BattleOutcomeKind.PlayerSuccess;
            reason = BattleEndReasonKind.EliminationHostilesDefeated;
        }
        else
        {
            outcome = BattleOutcomeKind.PlayerFailure;
            reason = BattleEndReasonKind.EliminationAlliesDefeated;
        }

        return BattleObjectiveEvaluationResult.Terminal(
            new BattleFinalDecision(
                BattleObjectiveMode.Elimination,
                outcome,
                reason,
                state.timeline?.current_tu ?? 0
            )
        );
    }

    private static int CountLivingUnits(
        BattleState state,
        IEnumerable<StringName> unitIds
    )
    {
        if (state == null || unitIds == null)
            return 0;
        int count = 0;
        foreach (StringName unitId in unitIds)
        {
            BattleUnitState unit = state.GetUnit(unitId);
            if (unit?.is_alive == true)
                count++;
        }
        return count;
    }
}
