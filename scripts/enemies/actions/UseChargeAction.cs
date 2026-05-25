using Godot;

[GlobalClass]
public partial class UseChargeAction : EnemyAiAction
{
    [Export] public StringName skill_id { get; set; } = "charge";
    [Export] public StringName target_selector { get; set; } = "nearest_enemy";
    [Export] public int minimum_charge_move_distance { get; set; } = 3;

    public override BattleAiDecision decide(GodotObject context) { AiTraceRecorder.enter("decide:charge"); var r = _decide_impl(context); AiTraceRecorder.exit("decide:charge"); return r; }

    private BattleAiDecision _decide_impl(GodotObject context)
    {
        var actionTrace = _begin_action_trace(context, new Godot.Collections.Dictionary { {"action_kind","charge"},{"target_selector",(string)target_selector} });
        var skillDef = _get_skill_def(context, skill_id);
        if (skillDef?.combat_profile == null || (skillDef.combat_profile as CombatSkillDef).target_mode != "ground") { _trace_add_block_reason(actionTrace, "invalid_charge_skill"); _finalize_action_trace(context, actionTrace); return null; }
        var blockReason = _get_skill_cast_block_reason(context, skillDef);
        if (blockReason.Length > 0) { _trace_add_block_reason(actionTrace, blockReason); _finalize_action_trace(context, actionTrace); return null; }
        var targets = _sort_target_units(context, "enemy", target_selector);
        if (targets.Count == 0) { _trace_add_block_reason(actionTrace, "no_valid_targets"); _finalize_action_trace(context, actionTrace); return null; }
        var focusTarget = targets[0].AsGodotObject() as BattleUnitState;
        var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState;
        int focusTargetDist = _distance_between_units(context, ctxUnitState, focusTarget);
        if (actionTrace.ContainsKey("metadata")) { var tm = actionTrace["metadata"].AsGodotDictionary(); tm["focus_target_distance"] = focusTargetDist; tm["minimum_charge_move_distance"] = minimum_charge_move_distance; actionTrace["metadata"] = tm; }
        BattleAiDecision bestDecision = null;
        GodotObject bestScoreInput = null;
        int bestFallbackScore = -999999;
        var state = context.Get("state").AsGodotObject();
        foreach (var cvV in _get_ground_variants(context, skillDef)) { var cv = cvV.AsGodotObject() as CombatCastVariantDef; if (cv == null || !_is_charge_variant(cv)) continue;
        for (int y = 0; y < state.Get("map_size").AsVector2I().Y; y++) for (int x = 0; x < state.Get("map_size").AsVector2I().X; x++) {
            _trace_count_increment(actionTrace, "evaluation_count", 1);
            var targetCoord = new Vector2I(x, y);
            var chargeInfo = _resolve_charge_target_info(ctxUnitState, targetCoord);
            if (!chargeInfo.ContainsKey("valid") || !chargeInfo["valid"].AsBool()) continue;
            var command = _build_ground_skill_command(context, skill_id, cv.variant_id, new Godot.Collections.Array { targetCoord });
            var preview = context.Call("preview_command", command).AsGodotObject();
            if (preview == null || !preview.Get("allowed").AsBool()) { _trace_count_increment(actionTrace, "preview_reject_count", 1); continue; }
            var resolvedAnchor = preview.Get("resolved_anchor_coord").AsVector2I();
            if (resolvedAnchor == new Vector2I(-1, -1)) resolvedAnchor = ctxUnitState.coord;
            int resolvedDist = _distance_from_anchor_to_unit(context, ctxUnitState, resolvedAnchor, focusTarget);
            int resolvedMoveDist = context.Get("grid_service").AsGodotObject()?.Call("get_distance", ctxUnitState.coord, resolvedAnchor).AsInt32() ?? 0;
            var shortBlock = _resolve_short_charge_block_reason(context, resolvedAnchor, resolvedMoveDist, focusTargetDist);
            if (shortBlock.Length > 0) { _trace_add_block_reason(actionTrace, shortBlock); continue; }
            int chargeBaseScore = 20 + Mathf.Max(resolvedMoveDist - 1, 0) * 8;
            var scoreInput = _build_skill_score_input(context, skillDef, command, preview, Variant.From(cv.effect_defs).AsGodotArray(), new Godot.Collections.Dictionary { {"action_kind","move"},{"action_base_score",chargeBaseScore},{"position_target_unit",focusTarget},{"position_anchor_coord",resolvedAnchor},{"desired_min_distance",1},{"desired_max_distance",1},{"action_label",_format_skill_variant_label(skillDef, cv)} });
            _trace_offer_candidate(actionTrace, _build_candidate_summary($"{_format_skill_variant_label(skillDef, cv)}->{focusTarget.display_name}", command, scoreInput, new Godot.Collections.Dictionary { {"resolved_anchor_coord",resolvedAnchor},{"resolved_distance",resolvedDist},{"resolved_move_distance",resolvedMoveDist} }));
            if (scoreInput != null) { if (!_is_better_skill_score_input(scoreInput, bestScoreInput)) continue; bestScoreInput = scoreInput; bestDecision = _create_scored_decision(command, scoreInput, $"{ctxUnitState.display_name} 准备用冲锋逼近 {focusTarget.display_name}（评分 {scoreInput.Call("total_score").AsInt32()}）。"); continue; }
            int movedDist = context.Get("grid_service").AsGodotObject()?.Call("get_distance", ctxUnitState.coord, resolvedAnchor).AsInt32() ?? 0;
            int fallback = 1000 - resolvedDist * 100 + movedDist;
            if (fallback <= bestFallbackScore) continue;
            bestFallbackScore = fallback;
            bestDecision = _create_decision(command, $"{ctxUnitState.display_name} 准备用冲锋逼近 {focusTarget.display_name}。");
        }}
        _finalize_action_trace(context, actionTrace, bestDecision); return bestDecision;
    }

    private static Godot.Collections.Dictionary _resolve_charge_target_info(BattleUnitState us, Vector2I tc) { if (us == null) return new Godot.Collections.Dictionary { {"valid",false} }; us.refresh_footprint(); int minX = us.coord.X, maxX = us.coord.X + us.footprint_size.X - 1, minY = us.coord.Y, maxY = us.coord.Y + us.footprint_size.Y - 1; if (tc.Y >= minY && tc.Y <= maxY) { if (tc.X < minX) { int ld = minX - tc.X; return new Godot.Collections.Dictionary { {"valid",true},{"distance",ld},{"predicted_anchor",us.coord + Vector2I.Left * ld} }; } if (tc.X > maxX) { int rd = tc.X - maxX; return new Godot.Collections.Dictionary { {"valid",true},{"distance",rd},{"predicted_anchor",us.coord + Vector2I.Right * rd} }; } } if (tc.X >= minX && tc.X <= maxX) { if (tc.Y < minY) { int ud = minY - tc.Y; return new Godot.Collections.Dictionary { {"valid",true},{"distance",ud},{"predicted_anchor",us.coord + Vector2I.Up * ud} }; } if (tc.Y > maxY) { int dd = tc.Y - maxY; return new Godot.Collections.Dictionary { {"valid",true},{"distance",dd},{"predicted_anchor",us.coord + Vector2I.Down * dd} }; } } return new Godot.Collections.Dictionary { {"valid",false} }; }

    private string _resolve_short_charge_block_reason(GodotObject context, Vector2I ra, int rmd, int ftd) { if (minimum_charge_move_distance <= 1) return ""; if (ftd <= minimum_charge_move_distance) return "target_distance_below_minimum_charge"; if (rmd > minimum_charge_move_distance) return ""; var ctxUs = context?.Get("unit_state").AsGodotObject() as BattleUnitState; if (ctxUs == null || ra == ctxUs.coord) return ""; var mc = _build_move_command(context, ra); var mp = context.Call("preview_command", mc).AsGodotObject(); return mp != null && mp.Get("allowed").AsBool() ? "short_charge_regular_move_available" : ""; }

    public override Godot.Collections.Array<string> validate_schema() { var e = _collect_base_validation_errors(); if (skill_id == "") e.Add($"UseChargeAction {action_id} is missing skill_id."); if (target_selector == "") e.Add($"UseChargeAction {action_id} is missing target_selector."); if (minimum_charge_move_distance < 1) e.Add($"UseChargeAction {action_id} minimum_charge_move_distance must be >= 1."); return e; }
}
