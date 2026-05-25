using Godot;

[GlobalClass]
public partial class MoveToMultiUnitSkillPositionAction : UseMultiUnitSkillAction
{
    [Export] public int target_count_weight { get; set; } = 40;

    public override BattleAiDecision decide(GodotObject context) { AiTraceRecorder.enter("decide:move_to_multi_unit_skill_position"); var r = _decide_impl(context); AiTraceRecorder.exit("decide:move_to_multi_unit_skill_position"); return r; }

    private BattleAiDecision _decide_impl(GodotObject context)
    {
        if (!_has_explicit_distance_contract()) return null;
        var at = _begin_action_trace(context, new Godot.Collections.Dictionary { {"action_kind","move_to_multi_unit_skill_position"},{"target_selector",(string)target_selector},{"distance_reference",(string)distance_reference},{"desired_min_distance",desired_min_distance},{"desired_max_distance",desired_max_distance},{"candidate_pool_limit",candidate_pool_limit},{"candidate_group_limit",candidate_group_limit},{"target_count_weight",target_count_weight} });
        BattleAiDecision bd = null; GodotObject bsi = null;
        var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState;
        foreach (var sid in _resolve_known_skill_ids(context, skill_ids)) {
            _trace_count_increment(at, "skill_considered_count", 1);
            var sd = _get_skill_def(context, sid);
            if (sd?.combat_profile == null || !_is_multi_unit_skill(sd)) { _trace_add_block_reason(at, sd == null ? "missing_skill_def" : "non_multi_unit_skill"); continue; }
            var br = _get_skill_cast_block_reason(context, sd); if (br.Length > 0) { _trace_add_block_reason(at, br); continue; }
            var st = _sort_target_units(context, (sd.combat_profile as CombatSkillDef).target_team_filter, target_selector);
            if (st.Count == 0) { _trace_add_block_reason(at, "no_valid_targets"); continue; }
            foreach (var cv in _get_multi_unit_cast_variants(context, sd)) {
                if (cv != null && _is_charge_variant(cv)) continue;
                var cg = _build_anchor_target_group(context, sd, cv, st, ctxUnitState.coord);
                int ctc = cg.Count;
                foreach (var dest in _collect_reachable_move_candidates(context)) {
                    _trace_count_increment(at, "evaluation_count", 1);
                    var tg = _build_anchor_target_group(context, sd, cv, st, dest);
                    int tc = tg.Count;
                    if (tc <= ctc) { _trace_add_block_reason(at, "does_not_improve_target_count"); continue; }
                    var cmd = _build_move_command(context, dest);
                    var pv = context.Call("preview_command", cmd).AsGodotObject();
                    if (pv == null || !pv.Get("allowed").AsBool()) { _trace_count_increment(at, "preview_reject_count", 1); continue; }
                    var pm = _build_position_metadata(context, tg, sd); pm["position_anchor_coord"] = dest;
                    var si = _build_action_score_input(context, "move", (string)action_id, cmd, pv, pm);
                    if (si == null) continue;
                    _apply_target_group_score(si, tg);
                    _trace_offer_candidate(at, _build_candidate_summary($"move_to_multi_{dest.X}_{dest.Y}", cmd, si, new Godot.Collections.Dictionary { {"skill_id",(string)sid},{"current_target_count",ctc},{"target_count",tc} }));
                    if (!_is_better_reposition_score_input(si, bsi)) continue;
                    bsi = si; bd = _create_scored_decision(cmd, si, $"{ctxUnitState.display_name} 准备移动到更适合 {sd.display_name} 的位置，可覆盖 {tc} 个目标（评分 {si.Call("total_score").AsInt32()}）。");
                }
            }
        }
        _finalize_action_trace(context, at, bd); return bd;
    }

    private Godot.Collections.Array<BattleUnitState> _build_anchor_target_group(GodotObject context, SkillDef sd, CombatCastVariantDef cv, Godot.Collections.Array st, Vector2I anchor)
    {
        var g = new Godot.Collections.Array<BattleUnitState>();
        if (context?.Get("unit_state").AsGodotObject() == null || sd?.combat_profile == null) return g;
        var cp = sd.combat_profile as CombatSkillDef;
        int sl = _get_skill_level(context.Get("unit_state").AsGodotObject() as BattleUnitState, sd.skill_id);
        int minC = Mathf.Max(cp.min_target_count, 1), maxC = Mathf.Max(cp.get_effective_max_target_count(sl), minC);
        foreach (var sv in st) { var tu = sv.AsGodotObject() as BattleUnitState; if (tu == null || g.Count >= maxC || g.Count >= candidate_pool_limit) break; if (!_can_anchor_target_unit(context, sd, anchor, tu)) continue; g.Add(tu); }
        return g.Count >= minC ? g : new Godot.Collections.Array<BattleUnitState>();
    }

    private bool _can_anchor_target_unit(GodotObject context, SkillDef sd, Vector2I anchor, BattleUnitState tu)
    {
        if (context?.Get("unit_state").AsGodotObject() == null || context.Get("grid_service").AsGodotObject() == null) return false;
        if (tu == null || !tu.is_alive) return false;
        if (!_matches_target_filter(context, tu, (sd.combat_profile as CombatSkillDef).target_team_filter)) return false;
        int er = BattleRangeService.get_effective_skill_range(context.Get("unit_state").AsGodotObject() as BattleUnitState, sd);
        return _distance_from_anchor_to_unit(context, context.Get("unit_state").AsGodotObject() as BattleUnitState, anchor, tu) <= er;
    }

    private Godot.Collections.Array<Vector2I> _collect_reachable_move_candidates(GodotObject context)
    {
        var c = new Godot.Collections.Array<Vector2I>();
        if (context?.Get("state").AsGodotObject() == null || context?.Get("unit_state").AsGodotObject() == null || context?.Get("grid_service").AsGodotObject() == null) return c;
        var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState;
        var origin = ctxUnitState.coord; int maxMp = Mathf.Max(ctxUnitState.current_move_points, 0);
        if (maxMp <= 0) return c;
        var seen = new Godot.Collections.Dictionary();
        var bestCosts = new Godot.Collections.Dictionary { { origin, 0 } };
        var frontier = new System.Collections.Generic.List<(Vector2I coord, int cost)> { (origin, 0) };
        var gs = context.Get("grid_service").AsGodotObject(); var state = context.Get("state").AsGodotObject();
        while (frontier.Count > 0) {
            var (cur, curCost) = frontier[0]; frontier.RemoveAt(0);
            if (!bestCosts.ContainsKey(cur) || bestCosts[cur].AsInt32() != curCost) continue;
            foreach (var nv in gs.Call("get_neighbors_4", state, cur).AsGodotArray<Vector2I>()) {
                var n = nv;
                if (!gs.Call("can_unit_step_between_anchors", state, ctxUnitState, cur, n).AsBool()) continue;
                int nc = curCost + context.Call("get_move_cost", ctxUnitState, n).AsInt32();
                if (nc > maxMp) continue;
                if (bestCosts.ContainsKey(n) && bestCosts[n].AsInt32() <= nc) continue;
                bestCosts[n] = nc; frontier.Add((n, nc));
                if (!seen.ContainsKey(n)) { seen[n] = true; c.Add(n); }
            }
        }
        var list = new System.Collections.Generic.List<Vector2I>(c); list.Sort((l, r) => { int ld = _distance_from_anchor_to_nearest_target(context, l), rd = _distance_from_anchor_to_nearest_target(context, r); if (ld == rd) { if (l.Y != r.Y) return l.Y.CompareTo(r.Y); return l.X.CompareTo(r.X); } return ld.CompareTo(rd); });
        var result = new Godot.Collections.Array<Vector2I>(); foreach (var v in list) result.Add(v); return result;
    }

    private int _distance_from_anchor_to_nearest_target(GodotObject context, Vector2I anchor) { var t = _sort_target_units(context, "enemy", target_selector); if (t.Count == 0) return 999999; return _distance_from_anchor_to_unit(context, context.Get("unit_state").AsGodotObject() as BattleUnitState, anchor, t[0].AsGodotObject() as BattleUnitState); }

    private void _apply_target_group_score(GodotObject si, Godot.Collections.Array<BattleUnitState> tg) { var ids = new Godot.Collections.Array<StringName>(); var coords = new Godot.Collections.Array<Vector2I>(); foreach (var tu in tg) { if (tu == null) continue; ids.Add(tu.unit_id); coords.Add(tu.coord); } si.Call("set", "target_unit_ids", ids); si.Call("set", "target_coords", coords); si.Call("set", "target_count", ids.Count); si.Call("set", "total_score", si.Call("total_score").AsInt32() + ids.Count * target_count_weight); }

    private bool _is_better_reposition_score_input(GodotObject c, GodotObject b) { if (c == null) return false; if (b == null) return true; if (c.Call("target_count").AsInt32() != b.Call("target_count").AsInt32()) return c.Call("target_count").AsInt32() > b.Call("target_count").AsInt32(); if (c.Call("position_objective_score").AsInt32() != b.Call("position_objective_score").AsInt32()) return c.Call("position_objective_score").AsInt32() > b.Call("position_objective_score").AsInt32(); if (c.Call("total_score").AsInt32() != b.Call("total_score").AsInt32()) return c.Call("total_score").AsInt32() > b.Call("total_score").AsInt32(); return c.Call("resource_cost_score").AsInt32() < b.Call("resource_cost_score").AsInt32(); }

    private static bool _is_multi_unit_skill(SkillDef sd) => sd?.combat_profile != null && (sd.combat_profile as CombatSkillDef).target_selection_mode == "multi_unit";

    private bool _has_explicit_distance_contract() => desired_min_distance >= 0 && desired_max_distance >= desired_min_distance && (distance_reference == "target_unit" || distance_reference == "enemy_frontline");

    public override Godot.Collections.Array<string> validate_schema() { var e = base.validate_schema(); if (target_count_weight < 0) e.Add($"MoveToMultiUnitSkillPositionAction {action_id} target_count_weight must be >= 0."); return e; }
}
