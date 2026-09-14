using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Godot;

internal sealed class BattleAiDecisionBindingService
{
    private readonly Dictionary<StringName, BattleAiRuntimeActionPlan> _actionPlansByUnitId =
        new();

    private WeakReference<IBattleAiDecisionBindingRuntimePort> _runtimeRef;

    private IBattleAiDecisionBindingRuntimePort Runtime =>
        _runtimeRef != null
        && _runtimeRef.TryGetTarget(out IBattleAiDecisionBindingRuntimePort port)
            ? port
            : null;

    internal void Setup(IBattleAiDecisionBindingRuntimePort runtime)
    {
        _runtimeRef =
            runtime != null
                ? new WeakReference<IBattleAiDecisionBindingRuntimePort>(runtime)
                : null;
    }

    internal bool HasActionPlans => _actionPlansByUnitId.Count != 0;

    internal bool TryGetActionPlan(
        StringName unitId,
        out BattleAiRuntimeActionPlan actionPlan
    ) => _actionPlansByUnitId.TryGetValue(unitId, out actionPlan);

    internal void _build_ai_action_plans()
    {
        ClearAiActionPlans();
        IBattleAiDecisionBindingRuntimePort runtime = Runtime;
        BattleState state = runtime?.GetBattleState();
        if (state == null || runtime.IsActionAssemblerReady() != true)
            return;
        try
        {
            foreach (BattleUnitState unitState in state.GetUnitsTyped())
            {
                if (
                    unitState == null
                    || unitState.ControlModeKind == BattleUnitControlMode.Manual
                    || IsEmpty(unitState.ai_brain_id)
                )
                    continue;
                EnemyAiBrainDefinition brain = runtime.GetEnemyAiBrain(unitState.ai_brain_id);
                if (brain == null)
                    continue;
                BattleAiRuntimeActionPlan actionPlan = runtime.BuildUnitActionPlan(
                    unitState,
                    brain
                );
                if (actionPlan != null)
                    _actionPlansByUnitId[unitState.unit_id] = actionPlan;
            }
        }
        catch
        {
            Exception cleanupFailure = null;
            BattleTeardown.RunStep(ref cleanupFailure, ClearAiActionPlans);
            throw;
        }
    }

    internal void ClearAiActionPlans()
    {
        List<BattleAiRuntimeActionPlan> plans = new(_actionPlansByUnitId.Values);
        _actionPlansByUnitId.Clear();
        Exception accumulatedFailure = null;
        foreach (BattleAiRuntimeActionPlan plan in plans)
        {
            BattleTeardown.RunStep(ref accumulatedFailure, () => plan?.Dispose());
        }
        if (accumulatedFailure != null)
        {
            ExceptionDispatchInfo.Capture(accumulatedFailure).Throw();
        }
    }

    internal void _ensure_ai_action_plan_for_unit(BattleUnitState unit_state)
    {
        IBattleAiDecisionBindingRuntimePort runtime = Runtime;
        if (unit_state == null || runtime?.IsActionAssemblerReady() != true)
            return;
        if (
            unit_state.ControlModeKind == BattleUnitControlMode.Manual
            || IsEmpty(unit_state.ai_brain_id)
        )
            return;
        EnemyAiBrainDefinition brain = runtime.GetEnemyAiBrain(unit_state.ai_brain_id);
        if (brain == null)
            return;
        _actionPlansByUnitId.TryGetValue(
            unit_state.unit_id,
            out BattleAiRuntimeActionPlan previousPlan
        );
        if (
            previousPlan != null
            && !runtime.IsActionPlanStaleFor(previousPlan, unit_state, brain)
        )
        {
            return;
        }
        BattleAiRuntimeActionPlan actionPlan = runtime.BuildUnitActionPlan(unit_state, brain);
        if (actionPlan != null)
        {
            _actionPlansByUnitId[unit_state.unit_id] = actionPlan;
            if (!ReferenceEquals(previousPlan, actionPlan))
                previousPlan?.Dispose();
        }
    }

    internal void _bind_ai_helper_services_for_decision(
        BattleUnitState unit_state,
        BattleAiContext ai_context
    )
    {
        if (unit_state == null || ai_context == null)
            return;
        Runtime?.BindAiHelperServicesForDecision(unit_state, ai_context);
    }

    internal BattleAiContext _prepare_ai_context_for_decision(BattleUnitState activeUnit)
    {
        _actionPlansByUnitId.TryGetValue(
            activeUnit.unit_id,
            out BattleAiRuntimeActionPlan actionPlan
        );
        return Runtime?.PrepareAiContextForDecision(activeUnit, actionPlan);
    }

    internal BattleAiScoreInput BuildAiSkillScoreInput(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<string, object> metadata,
        BattleAiSkillCandidateScoreFacts? candidateScoreFacts
    )
    {
        return Runtime?.BuildSkillScoreInput(
            context,
            skillDefinition,
            command,
            preview,
            effectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            metadata,
            candidateScoreFacts
        );
    }

    internal BattleAiScoreInput BuildAiActionScoreInput(
        BattleAiContext context,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        return Runtime?.BuildActionScoreInput(
            context,
            actionKind,
            actionLabel,
            scoreBucketId,
            command,
            preview,
            metadata
        );
    }

    internal BattleAiScoreInput BuildAiQueryActionScoreInput(
        BattleAiQueryService service,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        return Runtime?.BuildQueryActionScoreInput(
            service,
            actionKind,
            actionLabel,
            scoreBucketId,
            command,
            preview,
            metadata
        );
    }

    internal bool IsAiMovementBlocked(StringName unitId)
    {
        IBattleAiDecisionBindingRuntimePort runtime = Runtime;
        BattleState state = runtime?.GetBattleState();
        if (state == null)
            return false;
        state.TryGetUnitTyped(unitId, out BattleUnitState candidate);
        return candidate != null && runtime.IsMovementBlocked(candidate);
    }

    internal int _get_ai_move_query_cost(StringName unit_id, Vector2I _from_coord, Vector2I to_coord)
    {
        IBattleAiDecisionBindingRuntimePort runtime = Runtime;
        BattleState state = runtime?.GetBattleState();
        if (state == null)
            return 1;
        state.TryGetUnitTyped(unit_id, out BattleUnitState unitState);
        return unitState == null ? 1 : runtime.GetMoveCostForUnitTarget(unitState, to_coord);
    }

    internal void _prepare_ai_turn(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return;
        IBattleAiDecisionBindingRuntimePort runtime = Runtime;
        BattleState state = runtime?.GetBattleState();
        unit_state.ai_blackboard.SetInt(
            "turn_started_tu",
            state?.timeline != null ? state.timeline.current_tu : 0
        );
        unit_state.ai_blackboard.SetInt("turn_decision_count", 0);
        EnemyAiBrainDefinition brain = runtime?.GetEnemyAiBrain(unit_state.ai_brain_id);
        if (brain != null && !brain.HasState(unit_state.ai_state_id))
            unit_state.ai_state_id = brain.DefaultStateId;
    }

    internal void _cleanup_ai_turn(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return;
        unit_state.ai_blackboard.Remove("turn_started_tu");
        unit_state.ai_blackboard.Remove("turn_decision_count");
        Runtime?.ClearTurnAiOverride(unit_state);
    }

    private static bool IsEmpty(StringName value) => value == default || value == (StringName)"";
}
