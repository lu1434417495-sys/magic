using Godot;

[GlobalClass]
public partial class UseRandomChainSkillAction : EnemyAiAction
{
    private static readonly StringName DISTANCE_REF_CANDIDATE_POOL = "candidate_pool", DISTANCE_REF_ENEMY_FRONTLINE = "enemy_frontline";

    [Export] public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();
    [Export] public StringName target_selector { get; set; } = "nearest_enemy";
    [Export] public int desired_min_distance { get; set; } = -1;
    [Export] public int desired_max_distance { get; set; } = -1;
    [Export] public StringName distance_reference { get; set; } = DISTANCE_REF_CANDIDATE_POOL;

    public override BattleAiDecision decide(GodotObject context)
    {
        if (!_has_explicit_distance_contract()) return null;
        var actionTrace = _begin_action_trace(context, new Godot.Collections.Dictionary { {"action_kind","random_chain_skill"},{"target_selection_mode","random_chain"},{"target_selector",(string)target_selector},{"distance_reference",(string)distance_reference},{"desired_min_distance",desired_min_distance},{"desired_max_distance",desired_max_distance},{"selection_policy","random_from_living_pool"},{"pool_refresh_policy","before_each_attempt"},{"score_estimate_policy","expected_value"} });
        BattleAiDecision bestDecision = null; GodotObject bestScoreInput = null; BattleAiDecision fallbackDecision = null;
        foreach (var sid in _resolve_known_skill_ids(context, skill_ids)) {
            _trace_count_increment(actionTrace, "skill_considered_count", 1);
            var skillDef = _get_skill_def(context, sid);
            if (skillDef?.combat_profile == null || !_is_random_chain_skill(skillDef)) { _trace_add_block_reason(actionTrace, skillDef == null ? "missing_skill_def" : "non_random_chain_skill"); continue; }
            var blockReason = _get_skill_cast_block_reason(context, skillDef);
            if (blockReason.Length > 0) { _trace_add_block_reason(actionTrace, blockReason); continue; }
            foreach (var cv in _get_random_chain_cast_variants(context, skillDef)) {
                _trace_count_increment(actionTrace, "evaluation_count", 1);
                var command = _build_random_chain_skill_command(context, sid, cv);
                var preview = context.Call("preview_command", command).AsGodotObject();
                if (preview == null || !preview.Get("allowed").AsBool()) { _trace_count_increment(actionTrace, "preview_reject_count", 1); continue; }
                var candidateUnits = _resolve_candidate_units(context, preview, skillDef);
                if (candidateUnits.Count == 0) { _trace_add_block_reason(actionTrace, "no_random_chain_candidates"); continue; }
                var candidateIds = _candidate_unit_ids(candidateUnits);
                var posMeta = _build_position_metadata(context, candidateUnits, skillDef);
                posMeta["action_kind"] = "random_chain_skill"; posMeta["target_selection_mode"] = "random_chain";
                posMeta["action_label"] = _format_skill_variant_label(skillDef, cv);
                posMeta["candidate_pool_unit_ids"] = new Godot.Collections.Array<StringName>(candidateIds);
                posMeta["candidate_pool_count"] = candidateIds.Count;
                int maxHits = Mathf.Max((skillDef.combat_profile as CombatSkillDef).max_hits_per_target, 1);
                posMeta["random_chain_max_hits_per_target"] = maxHits;
                posMeta["random_chain_max_attempt_count"] = Mathf.Max(candidateIds.Count * maxHits, 1);
                posMeta["random_chain_selection_policy"] = "random_from_living_pool";
                posMeta["random_chain_pool_refresh_policy"] = "before_each_attempt";
                posMeta["random_chain_score_estimate_policy"] = "expected_value";
                _update_trace_metadata(actionTrace, posMeta);
                var scoreInput = _build_skill_score_input(context, skillDef, command, preview, _collect_random_chain_effect_defs(skillDef, cv), posMeta);
                var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState;
                if (scoreInput == null) { if (fallbackDecision == null) fallbackDecision = _create_decision(command, $"{ctxUnitState.display_name} 准备发动 {skillDef.display_name}，候选池 {candidateIds.Count} 个单位。"); _trace_offer_candidate(actionTrace, _build_candidate_summary(_format_skill_variant_label(skillDef, cv), command, null, new Godot.Collections.Dictionary { {"skill_id",(string)sid},{"candidate_pool_count",candidateIds.Count},{"candidate_pool_unit_ids",_stringify_unit_ids(candidateIds)} })); continue; }
                _trace_offer_candidate(actionTrace, _build_candidate_summary(_format_skill_variant_label(skillDef, cv), command, scoreInput, new Godot.Collections.Dictionary { {"skill_id",(string)sid},{"candidate_pool_count",candidateIds.Count},{"candidate_pool_unit_ids",_stringify_unit_ids(candidateIds)} }));
                if (!_is_better_skill_score_input(scoreInput, bestScoreInput)) continue;
                bestScoreInput = scoreInput; bestDecision = _create_scored_decision(command, scoreInput, $"{ctxUnitState.display_name} 准备发动 {skillDef.display_name}，候选池 {candidateIds.Count} 个单位（评分 {scoreInput.Call("total_score").AsInt32()}）。");
            }
        }
        var resolved = bestDecision ?? fallbackDecision; _finalize_action_trace(context, actionTrace, resolved); return resolved;
    }

    private static bool _is_random_chain_skill(SkillDef sd) => sd?.combat_profile != null && (sd.combat_profile as CombatSkillDef).target_mode == "unit" && ProgressionDataUtils.to_string_name((sd.combat_profile as CombatSkillDef).target_selection_mode) == "random_chain";

    private Godot.Collections.Array<CombatCastVariantDef> _get_random_chain_cast_variants(GodotObject context, SkillDef sd) { var r = new Godot.Collections.Array<CombatCastVariantDef>(); if (sd?.combat_profile == null) return r; var cp = sd.combat_profile as CombatSkillDef; if (cp.cast_variants.Count == 0) { r.Add(null); return r; } int sl = context != null ? _get_skill_level(context.Get("unit_state").AsGodotObject() as BattleUnitState, sd.skill_id) : 0; foreach (var cv in cp.get_unlocked_cast_variants(sl)) if (cv != null) r.Add(cv); return r; }

    private static BattleCommand _build_random_chain_skill_command(GodotObject context, StringName sid, CombatCastVariantDef cv) { if (context?.Get("unit_state").AsGodotObject() == null) return null; var cmd = new BattleCommand { command_type = BattleCommand.TYPE_SKILL(), unit_id = (context.Get("unit_state").AsGodotObject() as BattleUnitState).unit_id, skill_id = sid, skill_variant_id = cv?.variant_id ?? new StringName("") }; return cmd; }

    private Godot.Collections.Array<BattleUnitState> _resolve_candidate_units(GodotObject context, GodotObject preview, SkillDef sd) { var cids = new Godot.Collections.Dictionary(); if (preview != null) foreach (var ru in preview.Get("random_chain_candidate_unit_ids").AsGodotArray()) { var uid = ProgressionDataUtils.to_string_name(ru); if (uid != "") cids[uid] = true; } if (cids.Count == 0) return new Godot.Collections.Array<BattleUnitState>(); var sorted = _sort_target_units(context, (sd.combat_profile as CombatSkillDef).target_team_filter, target_selector); var r = new Godot.Collections.Array<BattleUnitState>(); foreach (var sv in sorted) { var su = sv.AsGodotObject() as BattleUnitState; if (su != null && cids.ContainsKey(su.unit_id)) r.Add(su); } return r; }

    private static Godot.Collections.Array<StringName> _candidate_unit_ids(Godot.Collections.Array<BattleUnitState> candidates) { var r = new Godot.Collections.Array<StringName>(); foreach (var c in candidates) if (c != null) r.Add(c.unit_id); return r; }
    private static Godot.Collections.Array<string> _stringify_unit_ids(Godot.Collections.Array<StringName> ids) { var r = new Godot.Collections.Array<string>(); foreach (var id in ids) r.Add((string)id); return r; }

    private static Godot.Collections.Array _collect_random_chain_effect_defs(SkillDef sd, CombatCastVariantDef cv) { var r = new Godot.Collections.Array(); if (sd?.combat_profile != null) foreach (var ed in (sd.combat_profile as CombatSkillDef).effect_defs) if (ed != null) r.Add(ed); if (cv != null) foreach (var ed in cv.effect_defs) if (ed != null) r.Add(ed); return r; }

    private Godot.Collections.Dictionary _build_position_metadata(GodotObject context, Godot.Collections.Array<BattleUnitState> candidates, SkillDef sd) { var dc = _resolve_desired_distance_contract(context, sd); var m = dc; if (distance_reference == DISTANCE_REF_CANDIDATE_POOL) { var pc = candidates.Count > 0 ? candidates[0] : null; if (pc != null) m["position_target_unit"] = pc; else m["position_objective_kind"] = "none"; } else if (distance_reference == DISTANCE_REF_ENEMY_FRONTLINE) { var fl = _resolve_enemy_frontline_unit(context); if (fl != null) m["position_target_unit"] = fl; else m["position_objective_kind"] = "none"; } else m["position_objective_kind"] = "none"; return m; }

    private GodotObject _resolve_enemy_frontline_unit(GodotObject context) { var t = _sort_target_units(context, "enemy", "nearest_enemy"); return t.Count > 0 ? t[0].AsGodotObject() : null; }

    private static void _update_trace_metadata(Godot.Collections.Dictionary at, Godot.Collections.Dictionary sm) { if (at.Count == 0) return; var m = at.ContainsKey("metadata") ? at["metadata"].AsGodotDictionary() : new Godot.Collections.Dictionary(); m["candidate_pool_count"] = sm.ContainsKey("candidate_pool_count") ? sm["candidate_pool_count"].AsInt32() : 0; m["candidate_pool_unit_ids"] = _stringify_unit_ids(sm.ContainsKey("candidate_pool_unit_ids") ? ProgressionDataUtils.to_string_name_array(sm["candidate_pool_unit_ids"]) : new Godot.Collections.Array<StringName>()); m["max_hits_per_target"] = sm.ContainsKey("random_chain_max_hits_per_target") ? sm["random_chain_max_hits_per_target"].AsInt32() : 0; m["max_attempt_count"] = sm.ContainsKey("random_chain_max_attempt_count") ? sm["random_chain_max_attempt_count"].AsInt32() : 0; at["metadata"] = m; }

    private bool _has_explicit_distance_contract() => desired_min_distance >= 0 && desired_max_distance >= desired_min_distance && (distance_reference == DISTANCE_REF_CANDIDATE_POOL || distance_reference == DISTANCE_REF_ENEMY_FRONTLINE);

    public override Godot.Collections.Array<string> validate_schema() { var e = _collect_base_validation_errors(); if (skill_ids.Count == 0) e.Add($"UseRandomChainSkillAction {action_id} must declare at least one skill_id."); if (target_selector == "") e.Add($"UseRandomChainSkillAction {action_id} is missing target_selector."); if (desired_min_distance < 0) e.Add($"UseRandomChainSkillAction {action_id} desired_min_distance must be >= 0."); if (desired_max_distance < desired_min_distance) e.Add($"UseRandomChainSkillAction {action_id} desired_max_distance must be >= desired_min_distance."); if (distance_reference != DISTANCE_REF_CANDIDATE_POOL && distance_reference != DISTANCE_REF_ENEMY_FRONTLINE) e.Add($"UseRandomChainSkillAction {action_id} distance_reference must be candidate_pool or enemy_frontline."); return e; }
}
