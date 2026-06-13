using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class UseMultiUnitSkillAction : EnemyAiAction
{
    [Export]
    public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int desired_min_distance { get; set; } = -1;

    [Export]
    public int desired_max_distance { get; set; } = -1;

    [Export]
    public StringName distance_reference { get; set; } = "";
    internal EnemyAiDistanceReference DistanceReferenceKind
    {
        get => EnemyAiDistanceReferences.ToKind(distance_reference);
        set => distance_reference = EnemyAiDistanceReferences.ToStringName(value);
    }

    [Export]
    public int candidate_pool_limit { get; set; } = 6;

    [Export]
    public int candidate_group_limit { get; set; } = 12;

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        AiTraceRecorder.Enter("decide:multi_unit_skill");
        var r = _decide_impl(context);
        AiTraceRecorder.Exit("decide:multi_unit_skill");
        return r;
    }

    private BattleAiDecision _decide_impl(BattleAiContext context)
    {
        if (!_has_explicit_distance_contract())
            return null;
        var at = _begin_action_trace(
            context,
            new System.Collections.Generic.Dictionary<string, object>
            {
                { "action_kind", "multi_unit_skill" },
                { "target_selector", (string)target_selector },
                { "distance_reference", (string)distance_reference },
                { "desired_min_distance", desired_min_distance },
                { "desired_max_distance", desired_max_distance },
                { "candidate_pool_limit", candidate_pool_limit },
                { "candidate_group_limit", candidate_group_limit },
            }
        );
        BattleAiDecision bd = null;
        BattleAiScoreInput bsi = null;
        BattleAiDecision fd = null;
        var ctxUnitState = context.unit_state;
        foreach (var sid in _resolve_known_skill_ids(context, skill_ids))
        {
            _trace_count_increment(at, "skill_considered_count", 1);
            var sd = _get_skill_def(context, sid);
            if (sd?.combat_profile == null || !_is_multi_unit_skill(sd))
            {
                _trace_add_block_reason(
                    at,
                    sd == null ? "missing_skill_def" : "non_multi_unit_skill"
                );
                continue;
            }
            var br = _get_skill_cast_block_reason(context, sd);
            if (br.Length > 0)
            {
                _trace_add_block_reason(at, br);
                continue;
            }
            List<BattleUnitState> st = _sort_target_units_typed(
                context,
                (sd.combat_profile as CombatSkillDef).target_team_filter,
                target_selector
            );
            if (st.Count == 0)
            {
                _trace_add_block_reason(at, "no_valid_targets");
                continue;
            }
            foreach (var cv in _get_multi_unit_cast_variants(context, sd))
            {
                if (cv != null && _is_charge_option(cv))
                    continue;
                var tgs = _build_target_groups(context, sd, cv, st);
                if (tgs.Count == 0)
                {
                    _trace_add_block_reason(at, "no_valid_target_groups");
                    continue;
                }
                foreach (var tg in tgs)
                {
                    _trace_count_increment(at, "evaluation_count", 1);
                    var cmd = _build_multi_unit_skill_command(context, sid, cv, tg);
                    BattlePreview pv = _build_fast_unit_skill_preview(context, sd, cv, cmd);
                    if (pv?.allowed != true)
                    {
                        _trace_count_increment(at, "preview_reject_count", 1);
                        continue;
                    }
                    var pm = _build_position_metadata(context, tg, sd);
                    pm["action_label"] = _format_skill_variant_label(sd, cv);
                    var si = _build_typed_skill_score_input(
                        context,
                        sd,
                        cmd,
                        pv,
                        _collect_multi_unit_effect_defs(sd, cv),
                        pm
                    );
                    int tc = cmd.target_unit_ids.Count;
                    if (si == null)
                    {
                        if (fd == null)
                            fd = _create_decision(
                                cmd,
                                $"{ctxUnitState.display_name} 准备用 {sd.display_name} 锁定 {tc} 个单位。"
                            );
                        _trace_offer_candidate(
                            at,
                            _build_candidate_summary(
                                _format_skill_variant_label(sd, cv),
                                cmd,
                                null,
                                new System.Collections.Generic.Dictionary<string, object>
                                {
                                    { "skill_id", (string)sid },
                                    { "target_count", tc },
                                }
                            )
                        );
                        continue;
                    }
                    _trace_offer_candidate(
                        at,
                        _build_candidate_summary(
                            _format_skill_variant_label(sd, cv),
                            cmd,
                            si,
                            new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "skill_id", (string)sid },
                                { "target_count", tc },
                            }
                        )
                    );
                    if (!_is_better_skill_score_input(si, bsi))
                        continue;
                    bsi = si;
                    bd = _create_scored_decision(
                        cmd,
                        si,
                        $"{ctxUnitState.display_name} 准备用 {sd.display_name} 锁定 {tc} 个单位（评分 {_score_total(si)}）。"
                    );
                }
            }
        }
        var r = bd ?? fd;
        _finalize_action_trace(context, at, r);
        return r;
    }

    private static bool _is_multi_unit_skill(SkillDef sd) =>
        sd?.combat_profile != null
        && (sd.combat_profile as CombatSkillDef).TargetSelectionModeKind
            == BattleTargetSelectionMode.MultiUnit;

    protected Godot.Collections.Array<CombatCastVariantDef> _get_multi_unit_cast_variants(
        BattleAiContext context,
        SkillDef sd
    )
    {
        var r = new Godot.Collections.Array<CombatCastVariantDef>();
        if (sd?.combat_profile == null)
            return r;
        var cp = sd.combat_profile as CombatSkillDef;
        if (cp.cast_variants.Count == 0)
        {
            r.Add(null);
            return r;
        }
        int sl = context?.unit_state != null ? _get_skill_level(context.unit_state, sd.skill_id) : 0;
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(context?.skill_catalog, sd, sl);
        foreach (var cv in effectiveProfile.UnlockedCastVariants)
            if (cv != null)
                r.Add(cv);
        return r;
    }

    private List<List<BattleUnitState>> _build_target_groups(
        BattleAiContext context,
        SkillDef sd,
        CombatCastVariantDef cv,
        IReadOnlyList<BattleUnitState> sortedTargets
    )
    {
        var groups = new List<List<BattleUnitState>>();
        var pool = _build_candidate_pool(context, sd, cv, sortedTargets);
        if (pool.Count == 0)
            return groups;
        var cp = sd.combat_profile as CombatSkillDef;
        int sl = _get_skill_level(context.unit_state, sd.skill_id);
        int minC = Mathf.Max(cp.min_target_count, 1);
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(context?.skill_catalog, sd, sl);
        int maxC = Mathf.Max(effectiveProfile.MaxTargetCount, minC);
        maxC = Mathf.Min(maxC, pool.Count);
        if (pool.Count < minC)
            return groups;
        var seen = new HashSet<string>();
        for (int count = maxC; count >= minC; count--)
        {
            if (count == 1)
            {
                foreach (var tu in pool)
                {
                    _append_target_group(
                        groups,
                        seen,
                        new List<BattleUnitState> { tu }
                    );
                    if (groups.Count >= candidate_group_limit)
                        return groups;
                }
                continue;
            }
            for (int si = 0; si <= pool.Count - count; si++)
            {
                var tg = new List<BattleUnitState>();
                for (int o = 0; o < count; o++)
                    tg.Add(pool[si + o]);
                _append_target_group(groups, seen, tg);
                if (groups.Count >= candidate_group_limit)
                    return groups;
            }
        }
        return groups;
    }

    private List<BattleUnitState> _build_candidate_pool(
        BattleAiContext context,
        SkillDef sd,
        CombatCastVariantDef cv,
        IEnumerable<BattleUnitState> st
    )
    {
        var pool = new List<BattleUnitState>();
        int minC = Mathf.Max((sd.combat_profile as CombatSkillDef).min_target_count, 1);
        foreach (BattleUnitState tu in st ?? System.Array.Empty<BattleUnitState>())
        {
            if (tu == null || pool.Count >= candidate_pool_limit)
                break;
            if (minC <= 1)
            {
                var scmd = _build_multi_unit_skill_command(
                    context,
                    sd.skill_id,
                    cv,
                    new List<BattleUnitState> { tu }
                );
                BattlePreview spv = _build_fast_unit_skill_preview(context, sd, cv, scmd, tu);
                if (spv?.allowed != true)
                    continue;
            }
            pool.Add(tu);
        }
        return pool;
    }

    private static void _append_target_group(
        List<List<BattleUnitState>> groups,
        HashSet<string> seen,
        List<BattleUnitState> tg
    )
    {
        if (tg.Count == 0)
            return;
        var key = _target_group_key(tg);
        if (key.Length == 0 || !seen.Add(key))
            return;
        groups.Add(tg);
    }

    private static string _target_group_key(IReadOnlyList<BattleUnitState> tg)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (var tu in tg)
            if (tu != null)
                parts.Add((string)tu.unit_id);
        return string.Join("|", parts);
    }

    private static BattleCommand _build_multi_unit_skill_command(
        BattleAiContext context,
        StringName sid,
        CombatCastVariantDef cv,
        IReadOnlyList<BattleUnitState> tg
    )
    {
        if (context?.unit_state == null)
            return null;
        var cmd = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = context.unit_state.unit_id,
            skill_id = sid,
            skill_variant_id = cv?.variant_id ?? new StringName(""),
        };
        foreach (var tu in tg)
        {
            if (tu == null)
                continue;
            cmd.target_unit_ids.Add(tu.unit_id);
            if (cmd.target_coord == new Vector2I(-1, -1))
                cmd.target_coord = tu.coord;
        }
        return cmd;
    }

    private static List<CombatEffectDef> _collect_multi_unit_effect_defs(
        SkillDef sd,
        CombatCastVariantDef cv
    )
    {
        var r = new List<CombatEffectDef>();
        if (sd?.combat_profile != null)
            foreach (var ed in (sd.combat_profile as CombatSkillDef).effect_defs)
                if (ed != null)
                    r.Add(ed);
        if (cv != null)
            foreach (var ed in cv.effect_defs)
                if (ed != null)
                    r.Add(ed);
        return r;
    }

    protected Dictionary<string, object> _build_position_metadata(
        BattleAiContext context,
        IReadOnlyList<BattleUnitState> tg,
        SkillDef sd
    )
    {
        var dc = _resolve_desired_distance_contract_typed(context, sd);
        if (DistanceReferenceKind == EnemyAiDistanceReference.TargetUnit)
        {
            var pt = tg.Count > 0 ? tg[0] : null;
            if (pt != null)
                dc["position_target_unit_id"] = pt.unit_id;
            else
                dc["position_objective_kind"] = "none";
        }
        else if (DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline)
        {
            var fl = _resolve_enemy_frontline_unit(context);
            if (fl != null)
                dc["position_target_unit_id"] = fl.unit_id;
            else
                dc["position_objective_kind"] = "none";
        }
        else
            dc["position_objective_kind"] = "none";
        return dc;
    }

    private BattleUnitState _resolve_enemy_frontline_unit(BattleAiContext context)
    {
        List<BattleUnitState> targets = _sort_target_units_typed(
            context,
            "enemy",
            "nearest_enemy"
        );
        return targets.Count > 0 ? targets[0] : null;
    }

    private bool _has_explicit_distance_contract() =>
        desired_min_distance >= 0
        && desired_max_distance >= desired_min_distance
        && (
            DistanceReferenceKind == EnemyAiDistanceReference.TargetUnit
            || DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline
        );

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        var e = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
            e.Add($"UseMultiUnitSkillAction {action_id} must declare at least one skill_id.");
        if (target_selector == "")
            e.Add($"UseMultiUnitSkillAction {action_id} is missing target_selector.");
        if (desired_min_distance < 0)
            e.Add($"UseMultiUnitSkillAction {action_id} desired_min_distance must be >= 0.");
        if (desired_max_distance < desired_min_distance)
            e.Add(
                $"UseMultiUnitSkillAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        if (
            DistanceReferenceKind != EnemyAiDistanceReference.TargetUnit
            && DistanceReferenceKind != EnemyAiDistanceReference.EnemyFrontline
        )
            e.Add(
                $"UseMultiUnitSkillAction {action_id} distance_reference must be target_unit or enemy_frontline."
            );
        if (candidate_pool_limit <= 0)
            e.Add($"UseMultiUnitSkillAction {action_id} candidate_pool_limit must be > 0.");
        if (candidate_group_limit <= 0)
            e.Add($"UseMultiUnitSkillAction {action_id} candidate_group_limit must be > 0.");
        return e;
    }
}
