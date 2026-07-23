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
            BattleObjectiveMode.Boss => EvaluateBoss(state),
            BattleObjectiveMode.Rescue => EvaluateRescue(state),
            BattleObjectiveMode.Escape => EvaluateEscape(state),
            BattleObjectiveMode.Escort => EvaluateEscort(state),
            BattleObjectiveMode.Defense => EvaluateDefense(state),
            BattleObjectiveMode.Intercept => EvaluateIntercept(state),
            BattleObjectiveMode.NodeOperation => EvaluateNodeOperation(state),
            _ => BattleObjectiveEvaluationResult.Invalid(),
        };
    }

    private static BattleObjectiveEvaluationResult EvaluateRescue(
        BattleState state
    )
    {
        if (
            state.ObjectiveRuntimeState
                is not BattleRescueObjectiveRuntimeState rescueObjective
        )
        {
            return BattleObjectiveEvaluationResult.Invalid();
        }
        BattleUnitState target = state.GetUnit(rescueObjective.TargetUnitId);
        if (target?.is_alive != true)
        {
            return Terminal(
                state,
                BattleObjectiveMode.Rescue,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.RescueTargetDefeated
            );
        }
        if (rescueObjective.TargetSecured)
        {
            return Terminal(
                state,
                BattleObjectiveMode.Rescue,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.RescueTargetSecured
            );
        }
        if (
            CountLivingUnits(state, rescueObjective.RequiredPartyUnitIds)
            <= 0
        )
        {
            return Terminal(
                state,
                BattleObjectiveMode.Rescue,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.RescuePartyDefeated
            );
        }
        return BattleObjectiveEvaluationResult.InProgress();
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

    private static BattleObjectiveEvaluationResult EvaluateEscort(
        BattleState state
    )
    {
        if (
            state.ObjectiveRuntimeState
                is not BattleEscortObjectiveRuntimeState escortObjective
        )
        {
            return BattleObjectiveEvaluationResult.Invalid();
        }
        BattleUnitState target = state.GetUnit(escortObjective.TargetUnitId);
        if (target?.is_alive != true)
        {
            return Terminal(
                state,
                BattleObjectiveMode.Escort,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.EscortTargetDefeated
            );
        }
        if (IsUnitFullyInsideExit(target, escortObjective))
        {
            return Terminal(
                state,
                BattleObjectiveMode.Escort,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.EscortTargetReachedExit
            );
        }
        if (
            CountLivingUnits(state, escortObjective.RequiredPartyUnitIds)
            <= 0
        )
        {
            return Terminal(
                state,
                BattleObjectiveMode.Escort,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.EscortPartyDefeated
            );
        }
        return BattleObjectiveEvaluationResult.InProgress();
    }

    private static BattleObjectiveEvaluationResult EvaluateBoss(BattleState state)
    {
        if (
            state.ObjectiveRuntimeState
                is not BattleBossObjectiveRuntimeState bossObjective
        )
        {
            return BattleObjectiveEvaluationResult.Invalid();
        }

        bool targetAlive = state.GetUnit(bossObjective.TargetUnitId)?.is_alive == true;
        int livingRequiredPartyUnits = CountLivingUnits(
            state,
            bossObjective.RequiredPartyUnitIds
        );
        if (targetAlive && livingRequiredPartyUnits > 0)
            return BattleObjectiveEvaluationResult.InProgress();

        BattleOutcomeKind outcome;
        BattleEndReasonKind reason;
        if (!targetAlive && livingRequiredPartyUnits <= 0)
        {
            outcome = BattleOutcomeKind.Draw;
            reason = BattleEndReasonKind.BossMutualDestruction;
        }
        else if (!targetAlive)
        {
            outcome = BattleOutcomeKind.PlayerSuccess;
            reason = BattleEndReasonKind.BossTargetDefeated;
        }
        else
        {
            outcome = BattleOutcomeKind.PlayerFailure;
            reason = BattleEndReasonKind.BossPartyDefeated;
        }

        return BattleObjectiveEvaluationResult.Terminal(
            new BattleFinalDecision(
                BattleObjectiveMode.Boss,
                outcome,
                reason,
                state.timeline?.current_tu ?? 0
            )
        );
    }

    private static BattleObjectiveEvaluationResult EvaluateIntercept(
        BattleState state
    )
    {
        if (
            state.ObjectiveRuntimeState
                is not BattleInterceptObjectiveRuntimeState interceptObjective
        )
        {
            return BattleObjectiveEvaluationResult.Invalid();
        }

        BattleUnitState target = state.GetUnit(interceptObjective.TargetUnitId);
        bool targetAlive = target?.is_alive == true;
        int livingRequiredPartyUnits = CountLivingUnits(
            state,
            interceptObjective.RequiredPartyUnitIds
        );
        if (!targetAlive && livingRequiredPartyUnits <= 0)
        {
            return Terminal(
                state,
                BattleObjectiveMode.Intercept,
                BattleOutcomeKind.Draw,
                BattleEndReasonKind.InterceptMutualDestruction
            );
        }
        if (!targetAlive)
        {
            return Terminal(
                state,
                BattleObjectiveMode.Intercept,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.InterceptTargetDefeated
            );
        }
        if (IsUnitFullyInsideExit(target, interceptObjective))
        {
            return Terminal(
                state,
                BattleObjectiveMode.Intercept,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.InterceptTargetEscaped
            );
        }
        if (livingRequiredPartyUnits <= 0)
        {
            return Terminal(
                state,
                BattleObjectiveMode.Intercept,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.InterceptPartyDefeated
            );
        }
        return BattleObjectiveEvaluationResult.InProgress();
    }

    private static BattleObjectiveEvaluationResult EvaluateDefense(
        BattleState state
    )
    {
        if (
            state.ObjectiveRuntimeState
                is not BattleDefenseObjectiveRuntimeState defenseObjective
            || state.timeline == null
            || state.timeline.current_tu < defenseObjective.StartTu
        )
        {
            return BattleObjectiveEvaluationResult.Invalid();
        }

        BattleUnitState target = state.GetUnit(defenseObjective.TargetUnitId);
        if (target?.is_alive != true)
        {
            return Terminal(
                state,
                BattleObjectiveMode.Defense,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.DefenseTargetDefeated
            );
        }
        if (state.timeline.current_tu >= defenseObjective.DeadlineTu)
        {
            return Terminal(
                state,
                BattleObjectiveMode.Defense,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.DefenseDeadlineReached
            );
        }
        if (
            CountLivingUnits(
                state,
                defenseObjective.RequiredPartyUnitIds
            ) <= 0
        )
        {
            return Terminal(
                state,
                BattleObjectiveMode.Defense,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.DefensePartyDefeated
            );
        }
        return BattleObjectiveEvaluationResult.InProgress();
    }

    private static BattleObjectiveEvaluationResult EvaluateNodeOperation(
        BattleState state
    )
    {
        if (
            state.ObjectiveRuntimeState
                is not BattleNodeOperationObjectiveRuntimeState objective
        )
        {
            return BattleObjectiveEvaluationResult.Invalid();
        }
        if (objective.AllNodesCompleted)
        {
            return Terminal(
                state,
                BattleObjectiveMode.NodeOperation,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.NodeOperationAllNodesCompleted
            );
        }
        if (CountLivingUnits(state, objective.RequiredPartyUnitIds) <= 0)
        {
            return Terminal(
                state,
                BattleObjectiveMode.NodeOperation,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.NodeOperationPartyDefeated
            );
        }
        return BattleObjectiveEvaluationResult.InProgress();
    }

    private static BattleObjectiveEvaluationResult EvaluateEscape(BattleState state)
    {
        if (
            state.ObjectiveRuntimeState
                is not BattleEscapeObjectiveRuntimeState escapeObjective
        )
        {
            return BattleObjectiveEvaluationResult.Invalid();
        }

        bool allRequiredUnitsInExit = true;
        foreach (StringName requiredUnitId in escapeObjective.RequiredUnitIds)
        {
            BattleUnitState unit = state.GetUnit(requiredUnitId);
            if (unit?.is_alive != true)
            {
                return BattleObjectiveEvaluationResult.Terminal(
                    new BattleFinalDecision(
                        BattleObjectiveMode.Escape,
                        BattleOutcomeKind.PlayerFailure,
                        BattleEndReasonKind.EscapeRequiredUnitDefeated,
                        state.timeline?.current_tu ?? 0
                    )
                );
            }
            if (!IsUnitFullyInsideExit(unit, escapeObjective))
                allRequiredUnitsInExit = false;
        }

        if (!allRequiredUnitsInExit)
            return BattleObjectiveEvaluationResult.InProgress();

        return BattleObjectiveEvaluationResult.Terminal(
            new BattleFinalDecision(
                BattleObjectiveMode.Escape,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.EscapeRequiredUnitsReachedExit,
                state.timeline?.current_tu ?? 0
            )
        );
    }

    internal static bool IsUnitFullyInsideExit(
        BattleUnitState unit,
        BattleEscapeObjectiveRuntimeState escapeObjective
    )
    {
        if (
            unit == null
            || escapeObjective == null
            || unit.occupied_coords == null
            || unit.occupied_coords.Count == 0
        )
        {
            return false;
        }
        foreach (Vector2I occupiedCoord in unit.occupied_coords)
        {
            if (!escapeObjective.ContainsExitCoord(occupiedCoord))
                return false;
        }
        return true;
    }

    internal static bool IsUnitFullyInsideExit(
        BattleUnitState unit,
        BattleEscortObjectiveRuntimeState escortObjective
    )
    {
        if (
            unit == null
            || escortObjective == null
            || unit.occupied_coords == null
            || unit.occupied_coords.Count == 0
        )
        {
            return false;
        }
        foreach (Vector2I occupiedCoord in unit.occupied_coords)
        {
            if (!escortObjective.ContainsExitCoord(occupiedCoord))
                return false;
        }
        return true;
    }

    internal static bool IsUnitFullyInsideExit(
        BattleUnitState unit,
        BattleInterceptObjectiveRuntimeState interceptObjective
    )
    {
        if (
            unit == null
            || interceptObjective == null
            || unit.occupied_coords == null
            || unit.occupied_coords.Count == 0
        )
        {
            return false;
        }
        foreach (Vector2I occupiedCoord in unit.occupied_coords)
        {
            if (!interceptObjective.ContainsExitCoord(occupiedCoord))
                return false;
        }
        return true;
    }

    private static BattleObjectiveEvaluationResult Terminal(
        BattleState state,
        BattleObjectiveMode mode,
        BattleOutcomeKind outcome,
        BattleEndReasonKind reason
    ) =>
        BattleObjectiveEvaluationResult.Terminal(
            new BattleFinalDecision(
                mode,
                outcome,
                reason,
                state?.timeline?.current_tu ?? 0
            )
        );

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
