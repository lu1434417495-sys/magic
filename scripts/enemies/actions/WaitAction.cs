using Godot;

[GlobalClass]
public partial class WaitAction : EnemyAiAction
{
    private const int TU_GRANULARITY = 5, STAMINA_RECOVERY_PROGRESS_BASE = 11, STAMINA_RECOVERY_PROGRESS_DENOMINATOR = 10, STAMINA_RESTING_RECOVERY_MULTIPLIER = 2;

    [Export] public int active_rest_action_base_score { get; set; } = 10;
    [Export] public int active_rest_min_stamina_residue { get; set; } = 1;

    public override BattleAiDecision decide(GodotObject context) { AiTraceRecorder.enter("decide:wait"); var r = _decide_impl(context); AiTraceRecorder.exit("decide:wait"); return r; }

    private BattleAiDecision _decide_impl(GodotObject context)
    {
        var arp = _build_active_rest_profile(context);
        var actionTrace = _begin_action_trace(context, new Godot.Collections.Dictionary { {"action_kind","wait"},{"active_rest",arp.ContainsKey("active")&&(bool)arp["active"]},{"will_rest",arp.ContainsKey("will_rest")&&(bool)arp["will_rest"]},{"current_stamina",arp.ContainsKey("current_stamina")?arp["current_stamina"].AsInt32():0},{"projected_rest_stamina",arp.ContainsKey("projected_rest_stamina")?arp["projected_rest_stamina"].AsInt32():0},{"desired_stamina",arp.ContainsKey("desired_stamina")?arp["desired_stamina"].AsInt32():0} });
        var command = _build_wait_command(context);
        var metadata = new Godot.Collections.Dictionary { {"position_objective_kind","none"} };
        if (arp.ContainsKey("active") && (bool)arp["active"]) { metadata["action_base_score"] = active_rest_action_base_score; metadata["active_rest"] = true; }
        var scoreInput = _build_action_score_input(context, "wait", (string)action_id, command, null, metadata);
        var ctxUnitState = context.Get("unit_state").AsGodotObject() as BattleUnitState;
        string reasonText = $"{ctxUnitState.display_name} 没有更优动作，选择待机。";
        if (arp.ContainsKey("active") && (bool)arp["active"]) reasonText = $"{ctxUnitState.display_name} 体力不足，选择主动休息以恢复到 {arp["projected_rest_stamina"]}/{arp["stamina_max"]}。";
        else if (arp.ContainsKey("will_rest") && (bool)arp["will_rest"]) reasonText = $"{ctxUnitState.display_name} 没有更优动作，选择休息恢复体力。";
        var decision = _create_scored_decision(command, scoreInput, reasonText);
        _trace_offer_candidate(actionTrace, _build_candidate_summary("wait", command, scoreInput));
        _finalize_action_trace(context, actionTrace, decision);
        return decision;
    }

    public override Godot.Collections.Array<string> validate_schema() { var e = _collect_base_validation_errors(); if (active_rest_action_base_score < -1000) e.Add($"WaitAction {action_id} active_rest_action_base_score is unexpectedly low."); if (active_rest_min_stamina_residue < 0) e.Add($"WaitAction {action_id} active_rest_min_stamina_residue must be >= 0."); return e; }

    private Godot.Collections.Dictionary _build_active_rest_profile(GodotObject context)
    {
        var p = new Godot.Collections.Dictionary { {"active",false},{"will_rest",false},{"current_stamina",0},{"projected_rest_stamina",0},{"desired_stamina",0},{"stamina_max",0} };
        if (context == null || context.Get("unit_state").AsGodotObject() == null) return p;
        var us = context.Get("unit_state").AsGodotObject() as BattleUnitState;
        int sm = _get_unit_stamina_max(us), cs = Mathf.Max(us.current_stamina, 0);
        p["current_stamina"] = cs; p["stamina_max"] = sm; p["will_rest"] = _will_wait_trigger_rest(us, cs, sm);
        if (sm <= 0 || cs >= sm) { p["projected_rest_stamina"] = cs; return p; }
        if (us.has_taken_action_this_turn) { p["projected_rest_stamina"] = cs; return p; }
        if (_has_affordable_legal_hostile_skill(context)) { p["projected_rest_stamina"] = cs; return p; }
        int ds = _resolve_desired_rest_stamina(context); p["desired_stamina"] = ds;
        if (ds <= 0 || cs >= ds) { p["projected_rest_stamina"] = cs; return p; }
        int ps = Mathf.Min(cs + _estimate_resting_recovery(us, _resolve_action_threshold_tu(us)), sm);
        p["projected_rest_stamina"] = ps; p["active"] = ps >= ds; return p;
    }

    private static bool _will_wait_trigger_rest(BattleUnitState us, int cs, int sm) => us != null && !us.has_taken_action_this_turn && sm > 0 && cs < sm;

    private bool _has_affordable_legal_hostile_skill(GodotObject context)
    {
        if (context == null || context.Get("unit_state").AsGodotObject() == null) return false;
        var us = context.Get("unit_state").AsGodotObject() as BattleUnitState;
        foreach (var rsi in us.known_active_skill_ids) { var sid = ProgressionDataUtils.to_string_name(rsi); var sd = _get_skill_def(context, sid); if (sd == null || sd.combat_profile == null || !_is_hostile_threat_skill(sd)) continue; if (!_can_pay_skill_cost(us, sd)) continue; if (_has_legal_unit_skill_target(context, sd)) return true; }
        return false;
    }

    private bool _has_legal_unit_skill_target(GodotObject context, SkillDef sd) { if (context == null || sd?.combat_profile == null) return false; if ((sd.combat_profile as CombatSkillDef).target_mode != "unit") return false; foreach (var tu in _sort_target_units(context, "enemy", "nearest_enemy")) { var cmd = _build_unit_skill_command(context, sd.skill_id, tu.AsGodotObject()); var preview = context.Call("preview_command", cmd).AsGodotObject(); if (preview != null && preview.Get("allowed").AsBool()) return true; } return false; }

    private bool _can_pay_skill_cost(BattleUnitState us, SkillDef sd) { if (us == null || sd?.combat_profile == null) return false; var cp = sd.combat_profile as CombatSkillDef; var costs = cp.get_effective_resource_costs(_get_skill_level(us, sd.skill_id)); if (_get_locked_combat_resource_block_reason(us, costs).Length > 0) return false; return us.current_ap >= (costs.ContainsKey("ap_cost") ? costs["ap_cost"].AsInt32() : cp.ap_cost) && us.current_mp >= (costs.ContainsKey("mp_cost") ? costs["mp_cost"].AsInt32() : cp.mp_cost) && us.current_stamina >= (costs.ContainsKey("stamina_cost") ? costs["stamina_cost"].AsInt32() : cp.stamina_cost) && us.current_aura >= (costs.ContainsKey("aura_cost") ? costs["aura_cost"].AsInt32() : cp.aura_cost); }

    private int _resolve_desired_rest_stamina(GodotObject context) { if (context == null || context.Get("unit_state").AsGodotObject() == null) return 0; var us = context.Get("unit_state").AsGodotObject() as BattleUnitState; int dc = _get_skill_stamina_cost(context, "basic_attack"); foreach (var rsi in us.known_active_skill_ids) { var sid = ProgressionDataUtils.to_string_name(rsi); var sd = _get_skill_def(context, sid); if (sd == null || sd.combat_profile == null || !_is_hostile_threat_skill(sd)) continue; int sc = _get_skill_stamina_cost(context, sid); if (sc <= 0) continue; dc = dc <= 0 ? sc : Mathf.Min(dc, sc); } return dc <= 0 ? 0 : dc + active_rest_min_stamina_residue; }

    private int _get_skill_stamina_cost(GodotObject context, StringName sid) { var sd = _get_skill_def(context, sid); if (sd?.combat_profile == null) return 0; int sl = context != null && context.Get("unit_state").AsGodotObject() != null ? _get_skill_level(context.Get("unit_state").AsGodotObject() as BattleUnitState, sid) : 1; var costs = (sd.combat_profile as CombatSkillDef).get_effective_resource_costs(Mathf.Max(sl, 1)); return Mathf.Max(costs.ContainsKey("stamina_cost") ? costs["stamina_cost"].AsInt32() : (sd.combat_profile as CombatSkillDef).stamina_cost, 0); }

    private int _estimate_resting_recovery(BattleUnitState us, int tuDelta) { if (us == null || tuDelta <= 0) return 0; int tc = Mathf.Max(tuDelta / TU_GRANULARITY, 0); if (tc <= 0) return 0; int pgpt = STAMINA_RECOVERY_PROGRESS_BASE + _get_unit_constitution(us); pgpt = _apply_stamina_recovery_percent_bonus(us, pgpt); pgpt *= STAMINA_RESTING_RECOVERY_MULTIPLIER; int progress = Mathf.Max(us.stamina_recovery_progress, 0); int recovered = 0; for (int i = 0; i < tc; i++) { progress += pgpt; recovered += progress / STAMINA_RECOVERY_PROGRESS_DENOMINATOR; progress %= STAMINA_RECOVERY_PROGRESS_DENOMINATOR; } return recovered; }

    private static int _resolve_action_threshold_tu(BattleUnitState us) => us != null ? Mathf.Max(us.action_threshold, 1) : 30;
    private static int _get_unit_constitution(BattleUnitState us) => us?.attribute_snapshot != null ? Mathf.Max(us.attribute_snapshot.Call("get_value", UnitBaseAttributes.CONSTITUTION_ID()).AsInt32(), 0) : 0;
    private static int _get_unit_stamina_max(BattleUnitState us) => us?.attribute_snapshot != null ? Mathf.Max(us.attribute_snapshot.Call("get_value", AttributeService.STAMINA_MAX_ID()).AsInt32(), 0) : 0;
    private static int _apply_stamina_recovery_percent_bonus(BattleUnitState us, int bpg) { if (us?.attribute_snapshot == null) return bpg; int pb = Mathf.Max(us.attribute_snapshot.Call("get_value", AttributeService.STAMINA_RECOVERY_PERCENT_BONUS_ID()).AsInt32(), 0); return pb <= 0 ? bpg : Mathf.FloorToInt(bpg * (100f + pb) / 100f); }
}
