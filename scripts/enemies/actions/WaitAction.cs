using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class WaitAction : EnemyAiAction
{
    private const int TU_GRANULARITY = 5,
        STAMINA_RECOVERY_PROGRESS_BASE = 11,
        STAMINA_RECOVERY_PROGRESS_DENOMINATOR = 10,
        STAMINA_RESTING_RECOVERY_MULTIPLIER = 2;

    private sealed class ActiveRestProfile
    {
        public bool Active;
        public bool WillRest;
        public int CurrentStamina;
        public int ProjectedRestStamina;
        public int DesiredStamina;
        public int StaminaMax;
    }

    [Export]
    public int active_rest_action_base_score { get; set; } = 10;

    [Export]
    public int active_rest_min_stamina_residue { get; set; } = 1;

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        AiTraceRecorder.Enter("decide:wait");
        var r = _decide_impl(context);
        AiTraceRecorder.Exit("decide:wait");
        return r;
    }

    private BattleAiDecision _decide_impl(BattleAiContext context)
    {
        var arp = _build_active_rest_profile(context);
        var actionTrace = _begin_action_trace(
            context,
            new System.Collections.Generic.Dictionary<string, object>
            {
                { "action_kind", "wait" },
                { "active_rest", arp.Active },
                { "will_rest", arp.WillRest },
                { "current_stamina", arp.CurrentStamina },
                { "projected_rest_stamina", arp.ProjectedRestStamina },
                { "desired_stamina", arp.DesiredStamina },
            }
        );
        var command = _build_wait_command(context);
        var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["position_objective_kind"] = "none"
        };
        if (arp.Active)
        {
            metadata["action_base_score"] = active_rest_action_base_score;
            metadata["active_rest"] = true;
        }
        var scoreInput = _build_typed_action_score_input(
            context,
            "wait",
            action_id.ToString(),
            command,
            null,
            metadata
        );
        var ctxUnitState = context.unit_state;
        string reasonText = $"{ctxUnitState.display_name} 没有更优动作，选择待机。";
        if (arp.Active)
            reasonText =
                $"{ctxUnitState.display_name} 体力不足，选择主动休息以恢复到 {arp.ProjectedRestStamina}/{arp.StaminaMax}。";
        else if (arp.WillRest)
            reasonText = $"{ctxUnitState.display_name} 没有更优动作，选择休息恢复体力。";
        var decision = _create_scored_decision(command, scoreInput, reasonText);
        _trace_offer_candidate(actionTrace, _build_candidate_summary("wait", command, scoreInput));
        _finalize_action_trace(context, actionTrace, decision);
        return decision;
    }

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        var e = _collect_base_validation_errors();
        if (active_rest_action_base_score < -1000)
            e.Add($"WaitAction {action_id} active_rest_action_base_score is unexpectedly low.");
        if (active_rest_min_stamina_residue < 0)
            e.Add($"WaitAction {action_id} active_rest_min_stamina_residue must be >= 0.");
        return e;
    }

    private ActiveRestProfile _build_active_rest_profile(BattleAiContext context)
    {
        var p = new ActiveRestProfile();
        if (context?.unit_state == null)
            return p;
        var us = context.unit_state;
        int sm = _get_unit_stamina_max(us),
            cs = Mathf.Max(us.current_stamina, 0);
        p.CurrentStamina = cs;
        p.StaminaMax = sm;
        p.WillRest = _will_wait_trigger_rest(us, cs, sm);
        if (sm <= 0 || cs >= sm)
        {
            p.ProjectedRestStamina = cs;
            return p;
        }
        if (us.has_taken_action_this_turn)
        {
            p.ProjectedRestStamina = cs;
            return p;
        }
        if (_has_affordable_legal_hostile_skill(context))
        {
            p.ProjectedRestStamina = cs;
            return p;
        }
        int ds = _resolve_desired_rest_stamina(context);
        p.DesiredStamina = ds;
        if (ds <= 0 || cs >= ds)
        {
            p.ProjectedRestStamina = cs;
            return p;
        }
        int ps = Mathf.Min(
            cs + _estimate_resting_recovery(us, _resolve_action_threshold_tu(us)),
            sm
        );
        p.ProjectedRestStamina = ps;
        p.Active = ps >= ds;
        return p;
    }

    private static bool _will_wait_trigger_rest(BattleUnitState us, int cs, int sm) =>
        us != null && !us.has_taken_action_this_turn && sm > 0 && cs < sm;

    private bool _has_affordable_legal_hostile_skill(BattleAiContext context)
    {
        if (context?.unit_state == null)
            return false;
        var us = context.unit_state;
        foreach (var rsi in us.known_active_skill_ids)
        {
            var sid = ProgressionDataUtils.to_string_name(rsi);
            SkillDefinition skillDefinition = _get_skill_definition(context, sid);
            if (
                skillDefinition?.CombatProfile == null
                || !_is_hostile_threat_skill(skillDefinition)
            )
                continue;
            if (!_can_cast_skill(context, skillDefinition))
                continue;
            if (_has_legal_unit_skill_target(context, skillDefinition))
                return true;
        }
        return false;
    }

    private bool _has_legal_unit_skill_target(
        BattleAiContext context,
        SkillDefinition skillDefinition
    )
    {
        if (context == null || skillDefinition?.CombatProfile == null)
            return false;
        if (skillDefinition.CombatProfile.TargetModeKind != BattleTargetMode.Unit)
            return false;
        foreach (BattleUnitState targetUnit in _sort_target_units_typed(
            context,
            "enemy",
            "nearest_enemy"
        ))
        {
            var cmd = _build_unit_skill_command(
                context,
                skillDefinition.SkillId,
                targetUnit
            );
            BattlePreview preview = _build_fast_unit_skill_preview(
                context,
                skillDefinition,
                null,
                cmd,
                targetUnit
            );
            if (preview?.allowed == true)
                return true;
        }
        return false;
    }

    private bool _can_cast_skill(BattleAiContext context, SkillDefinition skillDefinition)
    {
        if (context?.unit_state == null || skillDefinition?.CombatProfile == null)
            return false;
        return !BattleSkillCastBlockReasonKinds.IsBlocked(
            _get_skill_cast_block_reason(context, skillDefinition)
        );
    }

    private int _resolve_desired_rest_stamina(BattleAiContext context)
    {
        if (context?.unit_state == null)
            return 0;
        var us = context.unit_state;
        int dc = _get_skill_stamina_cost(context, "basic_attack");
        foreach (var rsi in us.known_active_skill_ids)
        {
            var sid = ProgressionDataUtils.to_string_name(rsi);
            SkillDefinition skillDefinition = _get_skill_definition(context, sid);
            if (
                skillDefinition?.CombatProfile == null
                || !_is_hostile_threat_skill(skillDefinition)
            )
                continue;
            int sc = _get_skill_stamina_cost(context, sid);
            if (sc <= 0)
                continue;
            dc = dc <= 0 ? sc : Mathf.Min(dc, sc);
        }
        return dc <= 0 ? 0 : dc + active_rest_min_stamina_residue;
    }

    private int _get_skill_stamina_cost(BattleAiContext context, StringName sid)
    {
        SkillDefinition skillDefinition = _get_skill_definition(context, sid);
        if (skillDefinition?.CombatProfile == null)
            return 0;
        int sl = context?.unit_state != null ? _get_skill_level(context.unit_state, sid) : 1;
        SkillEffectiveCombatDefinition effectiveDefinition =
            context?.skill_catalog?.GetEffectiveCombatDefinition(sid, Mathf.Max(sl, 1))
            ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, Mathf.Max(sl, 1));
        CombatSkillResourceCosts costs = effectiveDefinition.ResourceCosts;
        return Mathf.Max(costs.StaminaCost, 0);
    }

    private int _estimate_resting_recovery(BattleUnitState us, int tuDelta)
    {
        if (us == null || tuDelta <= 0)
            return 0;
        int tc = Mathf.Max(tuDelta / TU_GRANULARITY, 0);
        if (tc <= 0)
            return 0;
        int pgpt = STAMINA_RECOVERY_PROGRESS_BASE + _get_unit_constitution(us);
        pgpt = _apply_stamina_recovery_percent_bonus(us, pgpt);
        pgpt *= STAMINA_RESTING_RECOVERY_MULTIPLIER;
        int progress = Mathf.Max(us.stamina_recovery_progress, 0);
        int recovered = 0;
        for (int i = 0; i < tc; i++)
        {
            progress += pgpt;
            recovered += progress / STAMINA_RECOVERY_PROGRESS_DENOMINATOR;
            progress %= STAMINA_RECOVERY_PROGRESS_DENOMINATOR;
        }
        return recovered;
    }

    private static int _resolve_action_threshold_tu(BattleUnitState us) =>
        us != null ? Mathf.Max(us.action_threshold, 1) : 30;

    private static int _get_unit_constitution(BattleUnitState us) =>
        us?.attribute_snapshot != null
            ? Mathf.Max(
                us.attribute_snapshot.GetValue(UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution)),
                0
            )
            : 0;

    private static int _get_unit_stamina_max(BattleUnitState us) =>
        us?.attribute_snapshot != null
            ? Mathf.Max(
                us.attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax)),
                0
            )
            : 0;

    private static int _apply_stamina_recovery_percent_bonus(BattleUnitState us, int bpg)
    {
        if (us?.attribute_snapshot == null)
            return bpg;
        int pb = Mathf.Max(
            us.attribute_snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.StaminaRecoveryPercentBonus)),
            0
        );
        return pb <= 0 ? bpg : Mathf.FloorToInt(bpg * (100f + pb) / 100f);
    }
}
