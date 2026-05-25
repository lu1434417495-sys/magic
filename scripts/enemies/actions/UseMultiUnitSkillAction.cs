using Godot;

[GlobalClass]
public partial class UseMultiUnitSkillAction : EnemyAiAction
{
    private static readonly StringName DISTANCE_REF_TARGET_UNIT = "target_unit", DISTANCE_REF_ENEMY_FRONTLINE = "enemy_frontline";

    [Export] public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();
    [Export] public StringName target_selector { get; set; } = "nearest_enemy";
    [Export] public int desired_min_distance { get; set; } = -1;
    [Export] public int desired_max_distance { get; set; } = -1;
    [Export] public StringName distance_reference { get; set; } = "";
    [Export] public int candidate_pool_limit { get; set; } = 6;
    [Export] public int candidate_group_limit { get; set; } = 12;

    public override BattleAiDecision decide(GodotObject context) { AiTraceRecorder.enter("decide:multi_unit_skill"); var r = _decide_impl(context); AiTraceRecorder.exit("decide:multi_unit_skill"); return r; }

    private BattleAiDecision _decide_impl(GodotObject context)
    {
        if (!_has_explicit_distance_contract()) return null;
        var at = _begin_action_trace(context, new Godot.Collections.Dictionary { {"action_kind","multi_unit_skill"},{"target_selector",(string)target_selector},{"distance_reference",(string)distance_reference},{"desired_min_distance",desired_min_distance},{"desired_max_distance",desired_max_distance},{"candidate_pool_limit",candidate_pool_limit},{"candidate_group_limit",candidate_group_limit} });
        BattleAiDecision bd = null; GodotObject bsi = null; BattleAiDecision fd = null;
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
                var tgs = _build_target_groups(context, sd, cv, st);
                if (tgs.Count == 0) { _trace_add_block_reason(at, "no_valid_target_groups"); continue; }
                foreach (var tg in tgs) {
                    _trace_count_increment(at, "evaluation_count", 1);
                    var cmd = _build_multi_unit_skill_command(context, sid, cv, tg);
                    var pv = context.Call("preview_command", cmd).AsGodotObject();
                    if (pv == null || !pv.Get("allowed").AsBool()) { _trace_count_increment(at, "preview_reject_count", 1); continue; }
                    var pm = _build_position_metadata(context, tg, sd); pm["action_label"] = _format_skill_variant_label(sd, cv);
                    var si = _build_skill_score_input(context, sd, cmd, pv, _collect_multi_unit_effect_defs(sd, cv), pm);
                    int tc = cmd.target_unit_ids.Count;
                    if (si == null) { if (fd == null) fd = _create_decision(cmd, $"{ctxUnitState.display_name} 准备用 {sd.display_name} 锁定 {tc} 个单位。"); _trace_offer_candidate(at, _build_candidate_summary(_format_skill_variant_label(sd, cv), cmd, null, new Godot.Collections.Dictionary { {"skill_id",(string)sid},{"target_count",tc} })); continue; }
                    _trace_offer_candidate(at, _build_candidate_summary(_format_skill_variant_label(sd, cv), cmd, si, new Godot.Collections.Dictionary { {"skill_id",(string)sid},{"target_count",tc} }));
                    if (!_is_better_skill_score_input(si, bsi)) continue;
                    bsi = si; bd = _create_scored_decision(cmd, si, $"{ctxUnitState.display_name} 准备用 {sd.display_name} 锁定 {tc} 个单位（评分 {si.Call("total_score").AsInt32()}）。");
                }
            }
        }
        var r = bd ?? fd; _finalize_action_trace(context, at, r); return r;
    }

    private static bool _is_multi_unit_skill(SkillDef sd) => sd?.combat_profile != null && (sd.combat_profile as CombatSkillDef).target_selection_mode == "multi_unit";

    protected Godot.Collections.Array<CombatCastVariantDef> _get_multi_unit_cast_variants(GodotObject context, SkillDef sd) { var r = new Godot.Collections.Array<CombatCastVariantDef>(); if (sd?.combat_profile == null) return r; var cp = sd.combat_profile as CombatSkillDef; if (cp.cast_variants.Count == 0) { r.Add(null); return r; } int sl = context != null ? _get_skill_level(context.Get("unit_state").AsGodotObject() as BattleUnitState, sd.skill_id) : 0; foreach (var cv in cp.get_unlocked_cast_variants(sl)) if (cv != null) r.Add(cv); return r; }

    private Godot.Collections.Array<Godot.Collections.Array<BattleUnitState>> _build_target_groups(GodotObject context, SkillDef sd, CombatCastVariantDef cv, Godot.Collections.Array sortedTargets) { var groups = new Godot.Collections.Array<Godot.Collections.Array<BattleUnitState>>(); var pool = _build_candidate_pool(context, sd, cv, sortedTargets); if (pool.Count == 0) return groups; var cp = sd.combat_profile as CombatSkillDef; int sl = _get_skill_level(context.Get("unit_state").AsGodotObject() as BattleUnitState, sd.skill_id); int minC = Mathf.Max(cp.min_target_count, 1); int maxC = Mathf.Max(cp.get_effective_max_target_count(sl), minC); maxC = Mathf.Min(maxC, pool.Count); if (pool.Count < minC) return groups; var seen = new Godot.Collections.Dictionary(); for (int count = maxC; count >= minC; count--) { if (count == 1) { foreach (var tu in pool) { _append_target_group(groups, seen, new Godot.Collections.Array<BattleUnitState> { tu }); if (groups.Count >= candidate_group_limit) return groups; } continue; } for (int si = 0; si <= pool.Count - count; si++) { var tg = new Godot.Collections.Array<BattleUnitState>(); for (int o = 0; o < count; o++) tg.Add(pool[si + o]); _append_target_group(groups, seen, tg); if (groups.Count >= candidate_group_limit) return groups; } } return groups; }

    private Godot.Collections.Array<BattleUnitState> _build_candidate_pool(GodotObject context, SkillDef sd, CombatCastVariantDef cv, Godot.Collections.Array st) { var pool = new Godot.Collections.Array<BattleUnitState>(); int minC = Mathf.Max((sd.combat_profile as CombatSkillDef).min_target_count, 1); foreach (var sv in st) { var tu = sv.AsGodotObject() as BattleUnitState; if (tu == null || pool.Count >= candidate_pool_limit) break; if (minC <= 1) { var scmd = _build_multi_unit_skill_command(context, sd.skill_id, cv, new Godot.Collections.Array<BattleUnitState> { tu }); var spv = context.Call("preview_command", scmd).AsGodotObject(); if (spv == null || !spv.Get("allowed").AsBool()) continue; } pool.Add(tu); } return pool; }

    private static void _append_target_group(Godot.Collections.Array<Godot.Collections.Array<BattleUnitState>> groups, Godot.Collections.Dictionary seen, Godot.Collections.Array<BattleUnitState> tg) { if (tg.Count == 0) return; var key = _target_group_key(tg); if (key.Length == 0 || seen.ContainsKey(key)) return; seen[key] = true; groups.Add(tg); }

    private static string _target_group_key(Godot.Collections.Array<BattleUnitState> tg) { var parts = new System.Collections.Generic.List<string>(); foreach (var tu in tg) if (tu != null) parts.Add((string)tu.unit_id); return string.Join("|", parts); }

    private static BattleCommand _build_multi_unit_skill_command(GodotObject context, StringName sid, CombatCastVariantDef cv, Godot.Collections.Array<BattleUnitState> tg) { if (context?.Get("unit_state").AsGodotObject() == null) return null; var cmd = new BattleCommand { command_type = BattleCommand.TYPE_SKILL(), unit_id = (context.Get("unit_state").AsGodotObject() as BattleUnitState).unit_id, skill_id = sid, skill_variant_id = cv?.variant_id ?? new StringName("") }; foreach (var tu in tg) { if (tu == null) continue; cmd.target_unit_ids.Add(tu.unit_id); if (cmd.target_coord == new Vector2I(-1, -1)) cmd.target_coord = tu.coord; } return cmd; }

    private static Godot.Collections.Array _collect_multi_unit_effect_defs(SkillDef sd, CombatCastVariantDef cv) { var r = new Godot.Collections.Array(); if (sd?.combat_profile != null) foreach (var ed in (sd.combat_profile as CombatSkillDef).effect_defs) if (ed != null) r.Add(ed); if (cv != null) foreach (var ed in cv.effect_defs) if (ed != null) r.Add(ed); return r; }

    protected Godot.Collections.Dictionary _build_position_metadata(GodotObject context, Godot.Collections.Array<BattleUnitState> tg, SkillDef sd) { var dc = _resolve_desired_distance_contract(context, sd); if (distance_reference == DISTANCE_REF_TARGET_UNIT) { var pt = tg.Count > 0 ? tg[0] : null; if (pt != null) dc["position_target_unit"] = pt; else dc["position_objective_kind"] = "none"; } else if (distance_reference == DISTANCE_REF_ENEMY_FRONTLINE) { var fl = _resolve_enemy_frontline_unit(context); if (fl != null) dc["position_target_unit"] = fl; else dc["position_objective_kind"] = "none"; } else dc["position_objective_kind"] = "none"; return dc; }

    private GodotObject _resolve_enemy_frontline_unit(GodotObject context) { var t = _sort_target_units(context, "enemy", "nearest_enemy"); return t.Count > 0 ? t[0].AsGodotObject() : null; }

    private bool _has_explicit_distance_contract() => desired_min_distance >= 0 && desired_max_distance >= desired_min_distance && (distance_reference == DISTANCE_REF_TARGET_UNIT || distance_reference == DISTANCE_REF_ENEMY_FRONTLINE);

    public override Godot.Collections.Array<string> validate_schema() { var e = _collect_base_validation_errors(); if (skill_ids.Count == 0) e.Add($"UseMultiUnitSkillAction {action_id} must declare at least one skill_id."); if (target_selector == "") e.Add($"UseMultiUnitSkillAction {action_id} is missing target_selector."); if (desired_min_distance < 0) e.Add($"UseMultiUnitSkillAction {action_id} desired_min_distance must be >= 0."); if (desired_max_distance < desired_min_distance) e.Add($"UseMultiUnitSkillAction {action_id} desired_max_distance must be >= desired_min_distance."); if (distance_reference != DISTANCE_REF_TARGET_UNIT && distance_reference != DISTANCE_REF_ENEMY_FRONTLINE) e.Add($"UseMultiUnitSkillAction {action_id} distance_reference must be target_unit or enemy_frontline."); if (candidate_pool_limit <= 0) e.Add($"UseMultiUnitSkillAction {action_id} candidate_pool_limit must be > 0."); if (candidate_group_limit <= 0) e.Add($"UseMultiUnitSkillAction {action_id} candidate_group_limit must be > 0."); return e; }
}
