using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using Godot;
using GArray = Godot.Collections.Array;
using GBattleUnitArray = System.Collections.Generic.List<BattleUnitState>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal sealed class BattleAiDecisionBindingService : BattleRuntimeModuleBorrower
{
    private readonly Dictionary<StringName, BattleAiRuntimeActionPlan> _actionPlansByUnitId =
        new();

    internal bool HasActionPlans => _actionPlansByUnitId.Count != 0;

    internal bool TryGetActionPlan(
        StringName unitId,
        out BattleAiRuntimeActionPlan actionPlan
    ) => _actionPlansByUnitId.TryGetValue(unitId, out actionPlan);

    internal void _build_ai_action_plans()
    {
        ClearAiActionPlans();
        if (_runtime._state == null || _runtime._ai_action_assembler == null)
            return;
        try
        {
            foreach (BattleUnitState unitState in _runtime._state.GetUnitsTyped())
            {
                if (
                    unitState == null
                    || unitState.ControlModeKind == BattleUnitControlMode.Manual
                    || BattleRuntimeModule.IsEmpty(unitState.ai_brain_id)
                )
                    continue;
                EnemyAiBrainDefinition brain = _runtime.GetEnemyAiBrainTyped(unitState.ai_brain_id);
                if (brain == null)
                    continue;
                BattleAiRuntimeActionPlan actionPlan = _runtime._ai_action_assembler.BuildUnitActionPlan(
                    unitState,
                    brain,
                    _runtime.GetSkillDefinitionIndexTyped()
                );
                if (actionPlan != null)
                    _actionPlansByUnitId[unitState.unit_id] = actionPlan;
            }
        }
        catch
        {
            Exception cleanupFailure = null;
            BattleRuntimeModule.RunTeardownStep(ref cleanupFailure, ClearAiActionPlans);
            throw;
        }
    }

    internal void ClearAiActionPlans()
    {
        List<BattleAiRuntimeActionPlan> plans = new(_actionPlansByUnitId.Values);
        _actionPlansByUnitId.Clear();
        Exception firstFailure = null;
        foreach (BattleAiRuntimeActionPlan plan in plans)
        {
            BattleRuntimeModule.RunTeardownStep(ref firstFailure, () => plan?.Dispose());
        }
        if (firstFailure != null)
        {
            ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }
    }

    internal override void DisposeRuntime()
    {
        try
        {
            ClearAiActionPlans();
        }
        finally
        {
            base.DisposeRuntime();
        }
    }

    internal void _ensure_ai_action_plan_for_unit(BattleUnitState unit_state)
    {
        if (unit_state == null || _runtime._ai_action_assembler == null)
            return;
        if (_actionPlansByUnitId.ContainsKey(unit_state.unit_id))
            return;
        if (unit_state.ControlModeKind == BattleUnitControlMode.Manual || BattleRuntimeModule.IsEmpty(unit_state.ai_brain_id))
            return;
        EnemyAiBrainDefinition brain = _runtime.GetEnemyAiBrainTyped(unit_state.ai_brain_id);
        if (brain == null)
            return;
        BattleAiRuntimeActionPlan actionPlan = _runtime._ai_action_assembler.BuildUnitActionPlan(
            unit_state,
            brain,
            _runtime.GetSkillDefinitionIndexTyped()
        );
        if (actionPlan != null)
            _actionPlansByUnitId[unit_state.unit_id] = actionPlan;
    }

    internal void _bind_ai_helper_services_for_decision(
        BattleUnitState unit_state,
        BattleAiContext ai_context
    )
    {
        if (unit_state == null || ai_context == null || _runtime._state == null || _runtime._grid_service == null)
            return;
        _runtime._runtime_services.BindAiHelperServicesForDecision(
            new BattleAiHelperBindingContext(
                _runtime._state,
                _runtime._grid_service,
                unit_state,
                _runtime.GetSkillDefinitionIndexTyped(),
                _runtime.GetBarrierProfileIndexTyped(),
                _runtime._skillCatalog,
                _runtime._ai_service.GetScoreService(),
                _runtime._ai_move_query_cost_callback,
                _runtime._ai_query_action_score_input_callback,
                _runtime._ai_movement_blocked_callback,
                _runtime._ai_move_cost_callback,
                _runtime._ai_preview_command_callback,
                _runtime._ai_skill_score_input_callback,
                _runtime._ai_action_score_input_callback,
                _runtime._ai_skill_cast_block_reason_callback
            ),
            ai_context
        );
    }

    internal BattleAiContext _prepare_ai_context_for_decision(BattleUnitState activeUnit)
    {
        _actionPlansByUnitId.TryGetValue(
            activeUnit.unit_id,
            out BattleAiRuntimeActionPlan actionPlan
        );
        return _runtime._runtime_services.PrepareAiContextForDecision(
            new BattleAiDecisionContextSetup(
                _runtime._state,
                activeUnit,
                _runtime._grid_service,
                actionPlan,
                _runtime.GetSkillDefinitionIndexTyped(),
                _runtime.GetBarrierProfileIndexTyped(),
                _runtime._ai_trace_enabled,
                _runtime._skillCatalog,
                _runtime._ai_move_cost_callback,
                _runtime._ai_preview_command_callback,
                _runtime._ai_skill_score_input_callback,
                _runtime._ai_action_score_input_callback,
                _runtime._ai_skill_cast_block_reason_callback
            )
        );
    }

    internal BattleAiScoreInput BuildAiSkillScoreInput(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        return _runtime._ai_service.GetScoreService()
            .BuildSkillScoreInput(
                context,
                skillDefinition,
                command,
                preview,
                effectDefinitions ?? System.Array.Empty<CombatEffectDefinition>(),
                metadata
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
        return _runtime._ai_service.GetScoreService()
            .BuildActionScoreInput(
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
        return _runtime._runtime_services.BuildActionScoreInput(
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
        _runtime._state.TryGetUnitTyped(unitId, out BattleUnitState candidate);
        return candidate != null && _runtime._is_movement_blocked(candidate);
    }

    internal int _get_ai_move_query_cost(StringName unit_id, Vector2I _from_coord, Vector2I to_coord)
    {
        if (_runtime._state == null)
            return 1;
        _runtime._state.TryGetUnitTyped(unit_id, out BattleUnitState unitState);
        return unitState == null ? 1 : _runtime._movementCommandService._get_move_cost_for_unit_target(unitState, to_coord);
    }

    internal void _prepare_ai_turn(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return;
        unit_state.ai_blackboard.SetInt(
            "turn_started_tu",
            _runtime._state?.timeline != null ? _runtime._state.timeline.current_tu : 0
        );
        unit_state.ai_blackboard.SetInt("turn_decision_count", 0);
        EnemyAiBrainDefinition brain = _runtime.GetEnemyAiBrainTyped(unit_state.ai_brain_id);
        if (brain != null && !brain.HasState(unit_state.ai_state_id))
            unit_state.ai_state_id = brain.DefaultStateId;
    }

    internal void _cleanup_ai_turn(BattleUnitState unit_state)
    {
        if (unit_state == null)
            return;
        unit_state.ai_blackboard.Remove("turn_started_tu");
        unit_state.ai_blackboard.Remove("turn_decision_count");
        _runtime._skill_turn_resolver?.ClearTurnAiOverride(unit_state);
    }
}
