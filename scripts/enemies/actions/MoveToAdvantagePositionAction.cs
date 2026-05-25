using Godot;
using System.Collections.Generic;
using System.Linq;

[GlobalClass]
public partial class MoveToAdvantagePositionAction : EnemyAiAction
{
    private static readonly StringName MODE_ADVANTAGE = "advantage", MODE_SURVIVAL = "survival", MODE_HIGH_GROUND = "high_ground";

    [Export] public StringName target_selector { get; set; } = "nearest_enemy";
    [Export] public int desired_min_distance { get; set; } = 3;
    [Export] public int desired_max_distance { get; set; } = 5;
    [Export] public Godot.Collections.Array<StringName> range_skill_ids { get; set; } = new();
    [Export] public int minimum_safe_distance { get; set; } = 3;
    [Export] public int safe_distance_margin { get; set; } = 1;
    [Export] public StringName positioning_mode { get; set; } = MODE_ADVANTAGE;
    [Export] public int high_ground_weight { get; set; } = 60;
    [Export] public int safety_weight { get; set; } = 50;
    [Export] public int distance_band_weight { get; set; } = 20;
    [Export] public int candidate_limit { get; set; } = 96;

    public override BattleAiDecision decide(GodotObject context) { AiTraceRecorder.enter("decide:move_to_advantage_position"); var r = _decide_impl(context); AiTraceRecorder.exit("decide:move_to_advantage_position"); return r; }

    private BattleAiDecision _decide_impl(GodotObject context)
    {
        var dc = _resolve_desired_distance_contract(context, null, range_skill_ids);
        var at = _begin_action_trace(context, new Godot.Collections.Dictionary { {"action_kind","move_to_advantage_position"},{"target_selector",(string)target_selector},{"desired_min_distance",dc.ContainsKey("desired_min_distance")?dc["desired_min_distance"].AsInt32():desired_min_distance},{"desired_max_distance",dc.ContainsKey("desired_max_distance")?dc["desired_max_distance"].AsInt32():desired_max_distance},{"configured_desired_min_distance",desired_min_distance},{"configured_desired_max_distance",desired_max_distance},{"effective_attack_range",dc.ContainsKey("effective_attack_range")?dc["effective_attack_range"].AsInt32():-1},{"range_skill_ids",new Godot.Collections.Array<StringName>(range_skill_ids)},{"minimum_safe_distance",minimum_safe_distance},{"safe_distance_margin",safe_distance_margin},{"positioning_mode",(string)positioning_mode},{"high_ground_weight",high_ground_weight},{"safety_weight",safety_weight},{"distance_band_weight",distance_band_weight} });
        if (context?.Get("state").AsGodotObject() == null || context?.Get("unit_state").AsGodotObject() == null || context?.Get("grid_service").AsGodotObject() == null) { _trace_add_block_reason(at, "missing_context"); _finalize_action_trace(context, at); return null; }
        var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState;
        var targets = _sort_target_units(context, "enemy", target_selector);
        BattleAiDecision bd = null; GodotObject bsi = null;
        var gs = context.Get("grid_service").AsGodotObject(); var state = context.Get("state").AsGodotObject();
        int my = state.Get("map_size").AsVector2I().Y, mx = state.Get("map_size").AsVector2I().X;
        var candidates = new List<(Vector2I coord, int dist, int safety, int height)>();
        for (int y = 0; y < my; y++) for (int x = 0; x < mx; x++) { var c = new Vector2I(x, y); if (!gs.Call("can_place_footprint", state, c, ctxUnitState.footprint_size, ctxUnitState.unit_id, ctxUnitState).AsBool()) continue; int dist = targets.Count > 0 ? _distance_between_units(context, ctxUnitState, targets[0].AsGodotObject() as BattleUnitState) : 0; int safety = _resolve_target_safe_distance(context, targets.Count > 0 ? targets[0].AsGodotObject() as BattleUnitState : null, minimum_safe_distance, safe_distance_margin); int height = gs.Call("get_cell_height", state, c).AsInt32(); candidates.Add((c, dist, safety, height)); }
        if (positioning_mode == MODE_SURVIVAL) candidates.Sort((a, b) => { int sa = Mathf.Max(a.safety - a.dist, 0), sb = Mathf.Max(b.safety - b.dist, 0); if (sa != sb) return sb.CompareTo(sa); return a.dist.CompareTo(b.dist); });
        else if (positioning_mode == MODE_HIGH_GROUND) candidates.Sort((a, b) => { if (a.height != b.height) return b.height.CompareTo(a.height); int sa = Mathf.Max(a.safety - a.dist, 0), sb = Mathf.Max(b.safety - b.dist, 0); if (sa != sb) return sb.CompareTo(sa); return a.dist.CompareTo(b.dist); });
        else candidates.Sort((a, b) => { int da = Mathf.Abs(a.dist - desired_min_distance), db = Mathf.Abs(b.dist - desired_min_distance); if (da != db) return da.CompareTo(db); int sa = Mathf.Max(a.safety - a.dist, 0), sb = Mathf.Max(b.safety - b.dist, 0); if (sa != sb) return sb.CompareTo(sa); return b.height.CompareTo(a.height); });
        int evalCount = 0;
        foreach (var (coord, dist, safety, height) in candidates) { if (evalCount >= candidate_limit) break; evalCount++;
            _trace_count_increment(at, "evaluation_count", 1); var cmd = _build_move_command(context, coord);
            var pv = context.Call("preview_command", cmd).AsGodotObject(); if (pv == null || !pv.Get("allowed").AsBool()) { _trace_count_increment(at, "preview_reject_count", 1); continue; }
            var si = _build_action_score_input(context, "move", (string)action_id, cmd, pv, new Godot.Collections.Dictionary { {"position_target_unit",targets.Count>0?targets[0].AsGodotObject():null},{"position_anchor_coord",coord},{"desired_min_distance",dc.ContainsKey("desired_min_distance")?dc["desired_min_distance"].AsInt32():desired_min_distance},{"desired_max_distance",dc.ContainsKey("desired_max_distance")?dc["desired_max_distance"].AsInt32():desired_max_distance},{"position_current_distance",dist},{"position_safe_distance",safety},{"position_objective_kind","distance_band_progress"},{"high_ground_weight",high_ground_weight},{"safety_weight",safety_weight},{"distance_band_weight",distance_band_weight} });
            _trace_offer_candidate(at, _build_candidate_summary($"move_to_{coord.X}_{coord.Y}", cmd, si, new Godot.Collections.Dictionary { {"coord",coord},{"dist",dist},{"height",height} }));
            if (!_is_better_skill_score_input(si, bsi)) continue; bsi = si; bd = _create_scored_decision(cmd, si, $"{ctxUnitState.display_name} 移动到 ({coord.X},{coord.Y})（评分 {si.Call("total_score").AsInt32()}）。");
        }
        _finalize_action_trace(context, at, bd); return bd;
    }

    public override Godot.Collections.Array<string> validate_schema() { var e = _collect_base_validation_errors(); if (target_selector == "") e.Add($"MoveToAdvantagePositionAction {action_id} is missing target_selector."); if (desired_min_distance < 0) e.Add($"MoveToAdvantagePositionAction {action_id} desired_min_distance must be >= 0."); if (desired_max_distance < desired_min_distance) e.Add($"MoveToAdvantagePositionAction {action_id} desired_max_distance must be >= desired_min_distance."); if (minimum_safe_distance < 0) e.Add($"MoveToAdvantagePositionAction {action_id} minimum_safe_distance must be >= 0."); if (safe_distance_margin < 0) e.Add($"MoveToAdvantagePositionAction {action_id} safe_distance_margin must be >= 0."); if (positioning_mode != MODE_ADVANTAGE && positioning_mode != MODE_SURVIVAL && positioning_mode != MODE_HIGH_GROUND) e.Add($"MoveToAdvantagePositionAction {action_id} positioning_mode must be advantage, survival, or high_ground."); return e; }
}
