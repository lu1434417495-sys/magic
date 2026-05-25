using Godot;

[GlobalClass]
public partial class UseGroundRepositionSkillAction : EnemyAiAction
{
    [Export] public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();
    [Export] public StringName target_selector { get; set; } = "nearest_enemy";
    [Export] public int minimum_safe_distance { get; set; } = 3;
    [Export] public int safe_distance_margin { get; set; } = 1;
    [Export] public int desired_max_distance_bonus { get; set; } = 2;
    [Export] public int action_base_score { get; set; } = 1500;

    public override BattleAiDecision decide(GodotObject context) { AiTraceRecorder.enter("decide:ground_reposition_skill"); var r = _decide_impl(context); AiTraceRecorder.exit("decide:ground_reposition_skill"); return r; }

    private BattleAiDecision _decide_impl(GodotObject context)
    {
        var actionTrace = _begin_action_trace(context, new Godot.Collections.Dictionary { {"action_kind","ground_reposition_skill"},{"target_selector",(string)target_selector},{"minimum_safe_distance",minimum_safe_distance},{"safe_distance_margin",safe_distance_margin},{"desired_max_distance_bonus",desired_max_distance_bonus},{"action_base_score",action_base_score} });
        if (context == null || context.Get("state").AsGodotObject() == null || context.Get("unit_state").AsGodotObject() == null || context.Get("grid_service").AsGodotObject() == null) { _trace_add_block_reason(actionTrace, "missing_context"); _finalize_action_trace(context, actionTrace); return null; }
        var targets = _sort_target_units(context, "enemy", target_selector);
        if (targets.Count == 0 || targets[0].AsGodotObject() as BattleUnitState == null) { _trace_add_block_reason(actionTrace, "no_valid_targets"); _finalize_action_trace(context, actionTrace); return null; }
        var focusTarget = targets[0].AsGodotObject() as BattleUnitState;
        var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState;
        int resolvedSafeDist = Mathf.Max(minimum_safe_distance + safe_distance_margin, 1);
        int currentDist = _distance_from_anchor_to_unit(context, ctxUnitState, ctxUnitState.coord, focusTarget);
        if (actionTrace.ContainsKey("metadata")) { var tm = actionTrace["metadata"].AsGodotDictionary(); tm["focus_target_unit_id"] = (string)focusTarget.unit_id; tm["current_distance"] = currentDist; tm["resolved_safe_distance"] = resolvedSafeDist; actionTrace["metadata"] = tm; }
        if (currentDist >= resolvedSafeDist) { _trace_add_block_reason(actionTrace, "already_safe"); _finalize_action_trace(context, actionTrace); return null; }
        BattleAiDecision bestDecision = null; GodotObject bestScoreInput = null;
        foreach (var sidV in _resolve_known_skill_ids(context, skill_ids)) { var sid = sidV;
            _trace_count_increment(actionTrace, "skill_considered_count", 1);
            var skillDef = _get_skill_def(context, sid);
            if (skillDef?.combat_profile == null || (skillDef.combat_profile as CombatSkillDef).target_mode != "ground") { _trace_add_block_reason(actionTrace, skillDef == null ? "missing_skill_def" : "non_ground_skill"); continue; }
            var blockReason = _get_skill_cast_block_reason(context, skillDef);
            if (blockReason.Length > 0) { _trace_add_block_reason(actionTrace, blockReason); continue; }
            int effectiveRange = BattleRangeService.get_effective_skill_range(ctxUnitState, skillDef);
            foreach (var cvV in _get_ground_variants(context, skillDef)) { var cv = cvV.AsGodotObject() as CombatCastVariantDef; if (cv == null || _is_charge_variant(cv)) continue;
                if (!_has_reposition_effect(Variant.From(cv.effect_defs).AsGodotArray())) { _trace_add_block_reason(actionTrace, "missing_reposition_effect"); continue; }
                foreach (var tcsV in _enumerate_ground_target_coord_sets(context, cv)) { var tcs = tcsV.AsGodotArray<Vector2I>(); if (tcs.Count != 1) continue;
                    var landingCoord = tcs[0];
                    int castDist = context.Get("grid_service").AsGodotObject().Call("get_distance_from_unit_to_coord", ctxUnitState, landingCoord).AsInt32();
                    if (effectiveRange >= 0 && castDist > effectiveRange) continue;
                    int landingDist = _distance_from_anchor_to_unit(context, ctxUnitState, landingCoord, focusTarget);
                    if (landingDist <= currentDist) { _trace_add_block_reason(actionTrace, "does_not_improve_safety"); continue; }
                    _trace_count_increment(actionTrace, "evaluation_count", 1);
                    var command = _build_ground_skill_command(context, sid, cv.variant_id, new Godot.Collections.Array { landingCoord });
                    var preview = context.Call("preview_command", command).AsGodotObject();
                    if (preview == null || !preview.Get("allowed").AsBool()) { _trace_count_increment(actionTrace, "preview_reject_count", 1); continue; }
                    var scoreInput = _build_skill_score_input(context, skillDef, command, preview, Variant.From(cv.effect_defs).AsGodotArray(), new Godot.Collections.Dictionary { {"action_label",_format_skill_variant_label(skillDef,cv)},{"action_base_score",action_base_score},{"position_target_unit",focusTarget},{"position_anchor_coord",landingCoord},{"position_current_distance",currentDist},{"position_safe_distance",resolvedSafeDist},{"desired_min_distance",resolvedSafeDist},{"desired_max_distance",resolvedSafeDist+Mathf.Max(desired_max_distance_bonus,0)},{"position_objective_kind","distance_band_progress"} });
                    _trace_offer_candidate(actionTrace, _build_candidate_summary($"{_format_skill_variant_label(skillDef,cv)}_to_{landingCoord.X}_{landingCoord.Y}", command, scoreInput, new Godot.Collections.Dictionary { {"skill_id",(string)sid},{"landing_distance",landingDist},{"resolved_safe_distance",resolvedSafeDist} }));
                    if (!_is_better_skill_score_input(scoreInput, bestScoreInput)) continue;
                    bestScoreInput = scoreInput; bestDecision = _create_scored_decision(command, scoreInput, $"{ctxUnitState.display_name} 准备用 {skillDef.display_name} 拉开到 {landingDist} 格（评分 {scoreInput.Call("total_score").AsInt32()}）。");
                }
            }
        }
        _finalize_action_trace(context, actionTrace, bestDecision); return bestDecision;
    }

    public override Godot.Collections.Array<string> validate_schema() { var e = _collect_base_validation_errors(); if (skill_ids.Count == 0) e.Add($"UseGroundRepositionSkillAction {action_id} must declare at least one skill_id."); if (target_selector == "") e.Add($"UseGroundRepositionSkillAction {action_id} is missing target_selector."); if (minimum_safe_distance <= 0) e.Add($"UseGroundRepositionSkillAction {action_id} minimum_safe_distance must be >= 1."); if (safe_distance_margin < 0) e.Add($"UseGroundRepositionSkillAction {action_id} safe_distance_margin must be >= 0."); if (desired_max_distance_bonus < 0) e.Add($"UseGroundRepositionSkillAction {action_id} desired_max_distance_bonus must be >= 0."); return e; }

    private static bool _has_reposition_effect(Godot.Collections.Array effectDefs) { foreach (var edV in effectDefs) { var ed = edV.AsGodotObject() as CombatEffectDef; if (ed != null && ed.effect_type == "forced_move" && (ed.forced_move_mode == "blink" || ed.forced_move_mode == "jump")) return true; } return false; }
}
