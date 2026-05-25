using Godot;

[GlobalClass]
public partial class UseChargePathAoeAction : EnemyAiAction
{
    private static readonly StringName PATH_STEP_AOE_EFFECT_TYPE = "path_step_aoe";

    [Export] public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();
    [Export] public StringName target_selector { get; set; } = "nearest_enemy";
    [Export] public int minimum_hit_count { get; set; } = 1;
    [Export] public int desired_min_distance { get; set; } = 1;
    [Export] public int desired_max_distance { get; set; } = 1;

    public override BattleAiDecision decide(GodotObject context) { AiTraceRecorder.enter("decide:charge_path_aoe"); var r = _decide_impl(context); AiTraceRecorder.exit("decide:charge_path_aoe"); return r; }

    private BattleAiDecision _decide_impl(GodotObject context)
    {
        var at = _begin_action_trace(context, new Godot.Collections.Dictionary { {"action_kind","charge_path_aoe"},{"target_selector",(string)target_selector},{"minimum_hit_count",minimum_hit_count},{"desired_min_distance",desired_min_distance},{"desired_max_distance",desired_max_distance} });
        var targets = _sort_target_units(context, "enemy", target_selector);
        if (targets.Count == 0) { _trace_add_block_reason(at, "no_valid_targets"); _finalize_action_trace(context, at); return null; }
        var focusTarget = targets[0].AsGodotObject() as BattleUnitState;
        BattleAiDecision bd = null; GodotObject bsi = null; BattleAiDecision fd = null;
        var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState;
        var state = context.Get("state").AsGodotObject();
        foreach (var sid in _resolve_known_skill_ids(context, skill_ids)) {
            _trace_count_increment(at, "skill_considered_count", 1);
            var sd = _get_skill_def(context, sid);
            if (sd?.combat_profile == null || (sd.combat_profile as CombatSkillDef).target_mode != "ground") { _trace_add_block_reason(at, sd == null ? "missing_skill_def" : "non_ground_skill"); continue; }
            var br = _get_skill_cast_block_reason(context, sd); if (br.Length > 0) { _trace_add_block_reason(at, br); continue; }
            foreach (var cvV in _get_ground_variants(context, sd)) { var cv = cvV.AsGodotObject() as CombatCastVariantDef; if (cv == null || !_is_charge_variant(cv)) continue;
                var pse = _get_path_step_aoe_effect(cv); if (pse == null) { _trace_add_block_reason(at, "missing_path_step_aoe"); continue; }
                for (int y = 0; y < state.Get("map_size").AsVector2I().Y; y++) for (int x = 0; x < state.Get("map_size").AsVector2I().X; x++) {
                    _trace_count_increment(at, "evaluation_count", 1);
                    var tc = new Vector2I(x, y);
                    var ci = _resolve_charge_target_info(ctxUnitState, tc);
                    if (!ci.ContainsKey("valid") || !ci["valid"].AsBool()) continue;
                    var cmd = _build_ground_skill_command(context, sid, cv.variant_id, new Godot.Collections.Array { tc });
                    var pv = context.Call("preview_command", cmd).AsGodotObject();
                    if (pv == null || !pv.Get("allowed").AsBool()) { _trace_count_increment(at, "preview_reject_count", 1); continue; }
                    var pm = _build_path_step_hit_metrics(context, sd, pse, pv.Get("resolved_anchor_coord").AsVector2I());
                    int phc = pm.ContainsKey("path_step_hit_count") ? pm["path_step_hit_count"].AsInt32() : 0;
                    if (phc < minimum_hit_count) { _trace_add_block_reason(at, "minimum_hit_count"); continue; }
                    var ra = pm.ContainsKey("resolved_anchor_coord") ? pm["resolved_anchor_coord"].AsVector2I() : pv.Get("resolved_anchor_coord").AsVector2I();
                    int rmd = pm.ContainsKey("resolved_move_distance") ? pm["resolved_move_distance"].AsInt32() : 0;
                    int cas = 10 + Mathf.Max(rmd - 1, 0) * 4;
                    var posMeta = new Godot.Collections.Dictionary { {"action_kind","skill"},{"action_base_score",cas},{"position_target_unit",focusTarget},{"position_anchor_coord",ra},{"desired_min_distance",desired_min_distance},{"desired_max_distance",desired_max_distance},{"action_label",_format_skill_variant_label(sd,cv)},{"path_step_aoe_effect",pse} };
                    foreach (var mk in pm.Keys) posMeta[mk] = pm[mk];
                    var si = _build_skill_score_input(context, sd, cmd, pv, Variant.From(cv.effect_defs).AsGodotArray(), posMeta);
                    _trace_offer_candidate(at, _build_candidate_summary(_format_skill_variant_label(sd,cv), cmd, si, new Godot.Collections.Dictionary { {"path_step_hit_count",phc},{"path_step_unique_target_count",pm.ContainsKey("path_step_unique_target_count")?pm["path_step_unique_target_count"].AsInt32():0},{"resolved_anchor_coord",ra},{"resolved_move_distance",rmd},{"skill_id",(string)sid} }));
                    if (si == null) { if (fd == null) fd = _create_decision(cmd, $"{ctxUnitState.display_name} 准备用 {sd.display_name} 沿途命中 {phc} 次。"); continue; }
                    if (!_is_better_skill_score_input(si, bsi)) continue;
                    bsi = si; bd = _create_scored_decision(cmd, si, $"{ctxUnitState.display_name} 准备用 {sd.display_name} 沿途命中 {phc} 次（评分 {si.Call("total_score").AsInt32()}）。");
                }
            }
        }
        var r = bd ?? fd; _finalize_action_trace(context, at, r); return r;
    }

    private static CombatEffectDef _get_path_step_aoe_effect(CombatCastVariantDef cv) { if (cv == null) return null; foreach (var r in cv.effect_defs) { var ed = r as CombatEffectDef; if (ed != null && ed.effect_type == PATH_STEP_AOE_EFFECT_TYPE) return ed; } return null; }

    private static Godot.Collections.Dictionary _resolve_charge_target_info(BattleUnitState us, Vector2I tc) { if (us == null) return new Godot.Collections.Dictionary { {"valid",false} }; us.refresh_footprint(); int minX=us.coord.X,maxX=us.coord.X+us.footprint_size.X-1,minY=us.coord.Y,maxY=us.coord.Y+us.footprint_size.Y-1; if (tc.Y>=minY&&tc.Y<=maxY){if(tc.X<minX)return new Godot.Collections.Dictionary{{"valid",true},{"distance",minX-tc.X},{"direction",Vector2I.Left}};if(tc.X>maxX)return new Godot.Collections.Dictionary{{"valid",true},{"distance",tc.X-maxX},{"direction",Vector2I.Right}};} if(tc.X>=minX&&tc.X<=maxX){if(tc.Y<minY)return new Godot.Collections.Dictionary{{"valid",true},{"distance",minY-tc.Y},{"direction",Vector2I.Up}};if(tc.Y>maxY)return new Godot.Collections.Dictionary{{"valid",true},{"distance",tc.Y-maxY},{"direction",Vector2I.Down}};} return new Godot.Collections.Dictionary{{"valid",false}}; }

    private Godot.Collections.Dictionary _build_path_step_hit_metrics(GodotObject context, SkillDef sd, CombatEffectDef pse, Vector2I rac) { var er = new Godot.Collections.Dictionary { {"resolved_anchor_coord",rac},{"resolved_move_distance",0},{"path_step_hit_count",0},{"path_step_unique_target_count",0},{"path_step_hit_counts_by_unit_id",new Godot.Collections.Dictionary()} }; if (context?.Get("state").AsGodotObject() == null || context?.Get("unit_state").AsGodotObject() == null || context?.Get("grid_service").AsGodotObject() == null || pse == null || rac == new Vector2I(-1,-1)) return er; var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState; var path = _build_resolved_anchor_path(ctxUnitState.coord, rac); if (path.Count == 0) return er; bool arh = pse.@params.ContainsKey("allow_repeat_hits_across_steps") && (bool)pse.@params["allow_repeat_hits_across_steps"]; var tf = BattleTargetTeamRules.resolve_effect_target_filter(sd, pse); var hcbu = new Godot.Collections.Dictionary(); int thc = 0; var state = context.Get("state").AsGodotObject();
        foreach (var ac in path) { var ecs = _build_path_step_effect_coords(context, ac, pse); if (ecs.Count == 0) continue; var suis = new Godot.Collections.Dictionary();
            foreach (var uv in state.Get("units").AsGodotDictionary().Values) { var tu = uv.AsGodotObject() as BattleUnitState; if (tu == null || !tu.is_alive) continue; if (!BattleTargetTeamRules.is_unit_valid_for_filter(ctxUnitState, tu, tf)) continue; if (!_unit_intersects_coords(tu, ecs)) continue; if (!arh && hcbu.ContainsKey(tu.unit_id)) continue; if (suis.ContainsKey(tu.unit_id)) continue; suis[tu.unit_id] = true; }
            foreach (var uid in suis.Keys) { hcbu[uid] = (hcbu.ContainsKey(uid) ? hcbu[uid].AsInt32() : 0) + 1; thc++; }
        }
        er["resolved_move_distance"] = path.Count; er["path_step_hit_count"] = thc; er["path_step_unique_target_count"] = hcbu.Count; er["path_step_hit_counts_by_unit_id"] = hcbu; return er;
    }

    private static Godot.Collections.Array<Vector2I> _build_resolved_anchor_path(Vector2I sc, Vector2I rac) { var p = new Godot.Collections.Array<Vector2I>(); var delta = rac - sc; var dir = Vector2I.Zero; int dist = 0; if (delta.Y == 0 && delta.X != 0) { dir = delta.X > 0 ? Vector2I.Right : Vector2I.Left; dist = Mathf.Abs(delta.X); } else if (delta.X == 0 && delta.Y != 0) { dir = delta.Y > 0 ? Vector2I.Down : Vector2I.Up; dist = Mathf.Abs(delta.Y); } if (dir == Vector2I.Zero || dist <= 0) return p; var ac = sc; for (int i = 0; i < dist; i++) { ac += dir; p.Add(ac); } return p; }

    private Godot.Collections.Array<Vector2I> _build_path_step_effect_coords(GodotObject context, Vector2I ac, CombatEffectDef pse) { var r = new Godot.Collections.Array<Vector2I>(); if (context?.Get("state").AsGodotObject() == null || context?.Get("grid_service").AsGodotObject() == null || pse == null) return r; var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState; var gs = context.Get("grid_service").AsGodotObject(); var state = context.Get("state").AsGodotObject(); var ss = ProgressionDataUtils.to_string_name(pse.@params.ContainsKey("step_shape") ? pse.@params["step_shape"] : "diamond"); int sr = Mathf.Max(pse.@params.ContainsKey("step_radius") ? pse.@params["step_radius"].AsInt32() : 1, 0); var cs = new Godot.Collections.Dictionary(); foreach (var oc in gs.Call("get_unit_target_coords", ctxUnitState, ac).AsGodotArray<Vector2I>()) foreach (var ec in gs.Call("get_area_coords", state, oc, ss, sr).AsGodotArray<Vector2I>()) cs[ec] = true; foreach (var cv in cs.Keys) r.Add(cv.AsVector2I()); return _sort_coords(r); }

    private static bool _unit_intersects_coords(BattleUnitState us, Godot.Collections.Array<Vector2I> coords) { if (us == null || coords.Count == 0) return false; var cs = new Godot.Collections.Dictionary(); foreach (var c in coords) cs[c] = true; us.refresh_footprint(); foreach (var oc in us.occupied_coords) if (cs.ContainsKey(oc)) return true; return false; }

    public override Godot.Collections.Array<string> validate_schema() { var e = _collect_base_validation_errors(); if (skill_ids.Count == 0) e.Add($"UseChargePathAoeAction {action_id} must declare at least one skill_id."); if (target_selector == "") e.Add($"UseChargePathAoeAction {action_id} is missing target_selector."); if (minimum_hit_count <= 0) e.Add($"UseChargePathAoeAction {action_id} minimum_hit_count must be >= 1."); if (desired_min_distance < 0) e.Add($"UseChargePathAoeAction {action_id} desired_min_distance must be >= 0."); if (desired_max_distance < desired_min_distance) e.Add($"UseChargePathAoeAction {action_id} desired_max_distance must be >= desired_min_distance."); return e; }
}
