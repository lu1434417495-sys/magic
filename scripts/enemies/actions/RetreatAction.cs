using Godot;

[GlobalClass]
public partial class RetreatAction : EnemyAiAction
{
    [Export] public StringName target_selector { get; set; } = "nearest_enemy";
    [Export] public int minimum_safe_distance { get; set; } = 3;
    [Export] public bool use_dynamic_threat_safe_distance { get; set; }
    [Export] public int safe_distance_margin { get; set; } = 1;

    public override BattleAiDecision decide(GodotObject context)
    {
        AiTraceRecorder.enter("decide:retreat");
        var result = _decide_impl(context);
        AiTraceRecorder.exit("decide:retreat");
        return result;
    }

    private BattleAiDecision _decide_impl(GodotObject context)
    {
        var actionTrace = _begin_action_trace(context, new Godot.Collections.Dictionary { { "action_kind", "retreat" }, { "target_selector", (string)target_selector }, { "minimum_safe_distance", minimum_safe_distance }, { "use_dynamic_threat_safe_distance", use_dynamic_threat_safe_distance }, { "safe_distance_margin", safe_distance_margin } });
        var targets = _sort_target_units(context, "enemy", target_selector);
        if (targets.Count == 0) { _trace_add_block_reason(actionTrace, "no_valid_targets"); _finalize_action_trace(context, actionTrace); return null; }
        var focusTarget = _resolve_retreat_focus_target(context, targets);
        if (focusTarget == null) { _trace_add_block_reason(actionTrace, "no_valid_targets"); _finalize_action_trace(context, actionTrace); return null; }
        int resolvedSafeDistance = _resolve_retreat_safe_distance(context, focusTarget);
        int threatAttackRange = _resolve_unit_effective_threat_range(context, focusTarget);
        if (actionTrace.ContainsKey("metadata")) { var tm = actionTrace["metadata"].AsGodotDictionary(); tm["focus_target_unit_id"] = (string)focusTarget.unit_id; tm["threat_attack_range"] = threatAttackRange; tm["resolved_safe_distance"] = resolvedSafeDistance; actionTrace["metadata"] = tm; }
        var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState;
        var currentScoreInput = _build_action_score_input(context, "retreat", (string)action_id, null, null, new Godot.Collections.Dictionary { { "position_target_unit", focusTarget }, { "position_anchor_coord", ctxUnitState.coord }, { "desired_min_distance", resolvedSafeDistance }, { "desired_max_distance", resolvedSafeDistance }, { "position_objective_kind", "distance_band_progress" }, { "move_cost", 0 } });
        BattleAiDecision bestDecision = null;
        var bestScoreInput = currentScoreInput;
        var gridService = context.Get("grid_service").AsGodotObject();
        var state = context.Get("state").AsGodotObject();
        foreach (var neighborV in gridService.Call("get_neighbors_4", state, ctxUnitState.coord).AsGodotArray<Vector2I>())
        {
            var neighbor = neighborV;
            if (!gridService.Call("can_traverse", state, ctxUnitState.coord, neighbor, ctxUnitState).AsBool()) continue;
            _trace_count_increment(actionTrace, "evaluation_count", 1);
            var command = _build_move_command(context, neighbor);
            var preview = context.Call("preview_command", command).AsGodotObject();
            if (preview == null || !preview.Get("allowed").AsBool()) { _trace_count_increment(actionTrace, "preview_reject_count", 1); continue; }
            var scoreInput = _build_action_score_input(context, "retreat", (string)action_id, command, preview, new Godot.Collections.Dictionary { { "position_target_unit", focusTarget }, { "position_anchor_coord", neighbor }, { "desired_min_distance", resolvedSafeDistance }, { "desired_max_distance", resolvedSafeDistance }, { "position_objective_kind", "distance_band_progress" } });
            _trace_offer_candidate(actionTrace, _build_candidate_summary($"retreat_to_{neighbor.X}_{neighbor.Y}", command, scoreInput, new Godot.Collections.Dictionary { { "predicted_distance", scoreInput != null ? scoreInput.Call("distance_to_primary_coord").AsInt32() : -1 }, { "resolved_safe_distance", resolvedSafeDistance }, { "threat_attack_range", threatAttackRange } }));
            if (!_is_better_skill_score_input(scoreInput, bestScoreInput)) continue;
            bestScoreInput = scoreInput;
            bestDecision = _create_scored_decision(command, scoreInput, $"{ctxUnitState.display_name} 准备与 {focusTarget.display_name} 拉开到 {scoreInput.Call("distance_to_primary_coord").AsInt32()} 格（评分 {scoreInput.Call("total_score").AsInt32()}）。");
        }
        _finalize_action_trace(context, actionTrace, bestDecision);
        return bestDecision;
    }

    public override Godot.Collections.Array<string> validate_schema()
    {
        var errors = _collect_base_validation_errors();
        if (target_selector == "") errors.Add($"RetreatAction {action_id} is missing target_selector.");
        if (minimum_safe_distance <= 0) errors.Add($"RetreatAction {action_id} minimum_safe_distance must be >= 1.");
        if (safe_distance_margin < 0) errors.Add($"RetreatAction {action_id} safe_distance_margin must be >= 0.");
        return errors;
    }

    private int _resolve_retreat_safe_distance(GodotObject context, BattleUnitState focusTarget)
    {
        if (!use_dynamic_threat_safe_distance) return minimum_safe_distance;
        return _resolve_target_safe_distance(context, focusTarget, minimum_safe_distance, safe_distance_margin);
    }

    private BattleUnitState _resolve_retreat_focus_target(GodotObject context, Godot.Collections.Array targets)
    {
        if (targets.Count == 0) return null;
        if (!use_dynamic_threat_safe_distance) return targets[0].AsGodotObject() as BattleUnitState;
        var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState;
        return _select_most_unsafe_target(context, targets, ctxUnitState.coord, minimum_safe_distance, safe_distance_margin);
    }
}
