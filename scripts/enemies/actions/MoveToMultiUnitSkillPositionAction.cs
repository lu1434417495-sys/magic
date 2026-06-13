using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class MoveToMultiUnitSkillPositionAction : UseMultiUnitSkillAction
{
    [Export]
    public int target_count_weight { get; set; } = 40;

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        AiTraceRecorder.Enter("decide:move_to_multi_unit_skill_position");
        var r = _decide_impl(context);
        AiTraceRecorder.Exit("decide:move_to_multi_unit_skill_position");
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
                { "action_kind", "move_to_multi_unit_skill_position" },
                { "target_selector", (string)target_selector },
                { "distance_reference", (string)distance_reference },
                { "desired_min_distance", desired_min_distance },
                { "desired_max_distance", desired_max_distance },
                { "candidate_pool_limit", candidate_pool_limit },
                { "candidate_group_limit", candidate_group_limit },
                { "target_count_weight", target_count_weight },
            }
        );
        BattleAiDecision bd = null;
        BattleAiScoreInput bsi = null;
        var ctxUnitState = context?.unit_state;
        if (ctxUnitState == null)
        {
            _trace_add_block_reason(at, "missing_context");
            _finalize_action_trace(context, at);
            return null;
        }
        if (_is_unit_movement_blocked(context, ctxUnitState))
        {
            _trace_add_block_reason(at, "movement_blocked");
            _finalize_action_trace(context, at);
            return null;
        }
        int moveBudget = _resolve_current_move_budget(ctxUnitState);
        if (moveBudget <= 0)
        {
            _trace_add_block_reason(
                at,
                _is_normal_movement_locked(ctxUnitState) ? "movement_locked" : "no_move_budget"
            );
            _finalize_action_trace(context, at);
            return null;
        }
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
                List<BattleUnitState> cg = _build_anchor_target_group(
                    context,
                    sd,
                    cv,
                    st,
                    ctxUnitState.coord
                );
                int ctc = cg.Count;
                foreach (var dest in _collect_reachable_move_candidates(context))
                {
                    _trace_count_increment(at, "evaluation_count", 1);
                    List<BattleUnitState> tg = _build_anchor_target_group(context, sd, cv, st, dest);
                    int tc = tg.Count;
                    if (tc <= ctc)
                    {
                        _trace_add_block_reason(at, "does_not_improve_target_count");
                        continue;
                    }
                    var cmd = _build_move_command(context, dest);
                    BattlePreview pv = _build_fast_typed_move_preview(context, dest);
                    if (pv?.allowed != true)
                    {
                        _trace_count_increment(at, "preview_reject_count", 1);
                        continue;
                    }
                    var pm = _build_position_metadata(context, tg, sd);
                    pm["position_anchor_coord"] = dest;
                    var si = _build_typed_action_score_input(
                        context,
                        "move",
                        (string)action_id,
                        cmd,
                        pv,
                        pm
                    );
                    if (si == null)
                        continue;
                    _apply_target_group_score(si, tg);
                    _trace_offer_candidate(
                        at,
                        _build_candidate_summary(
                            $"move_to_multi_{dest.X}_{dest.Y}",
                            cmd,
                            si,
                            new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "skill_id", (string)sid },
                                { "current_target_count", ctc },
                                { "target_count", tc },
                            }
                        )
                    );
                    if (!_is_better_reposition_score_input(si, bsi))
                        continue;
                    bsi = si;
                    bd = _create_scored_decision(
                        cmd,
                        si,
                        $"{ctxUnitState.display_name} 准备移动到更适合 {sd.display_name} 的位置，可覆盖 {tc} 个目标（评分 {_score_total(si)}）。"
                    );
                }
            }
        }
        _finalize_action_trace(context, at, bd);
        return bd;
    }

    private List<BattleUnitState> _build_anchor_target_group(
        BattleAiContext context,
        SkillDef sd,
        CombatCastVariantDef cv,
        IReadOnlyList<BattleUnitState> st,
        Vector2I anchor
    )
    {
        var g = new List<BattleUnitState>();
        if (context?.unit_state == null || sd?.combat_profile == null)
            return g;
        var cp = sd.combat_profile as CombatSkillDef;
        int sl = _get_skill_level(
            context.unit_state,
            sd.skill_id
        );
        SkillEffectiveCombatProfile effectiveProfile =
            SkillEffectiveCombatProfileResolver.Resolve(context?.skill_catalog, sd, sl);
        int minC = Mathf.Max(cp.min_target_count, 1),
            maxC = Mathf.Max(effectiveProfile.MaxTargetCount, minC);
        foreach (BattleUnitState tu in st ?? System.Array.Empty<BattleUnitState>())
        {
            if (tu == null || g.Count >= maxC || g.Count >= candidate_pool_limit)
                break;
            if (!_can_anchor_target_unit(context, sd, anchor, tu))
                continue;
            g.Add(tu);
        }
        return g.Count >= minC ? g : new List<BattleUnitState>();
    }

    private bool _can_anchor_target_unit(
        BattleAiContext context,
        SkillDef sd,
        Vector2I anchor,
        BattleUnitState tu
    )
    {
        if (
            context?.unit_state == null
            || context.grid_service == null
        )
            return false;
        if (tu == null || !tu.is_alive)
            return false;
        if (
            !_matches_target_filter(
                context,
                tu,
                (sd.combat_profile as CombatSkillDef).target_team_filter
            )
        )
            return false;
        int er = BattleRangeService.GetEffectiveSkillRange(
            context.unit_state,
            sd,
            context.skill_catalog
        );
        return _distance_from_anchor_to_unit(
                context,
                context.unit_state,
                anchor,
                tu
            ) <= er;
    }

    private List<Vector2I> _collect_reachable_move_candidates(
        BattleAiContext context
    )
    {
        var candidates = new List<Vector2I>();
        if (
            context?.state == null
            || context?.unit_state == null
            || context?.grid_service == null
        )
            return candidates;
        var ctxUnitState = context.unit_state;
        var origin = ctxUnitState.coord;
        if (_is_unit_movement_blocked(context, ctxUnitState))
            return candidates;
        int maxMp = _resolve_current_move_budget(ctxUnitState);
        if (maxMp <= 0)
            return candidates;
        var seen = new HashSet<Vector2I>();
        var bestCosts = new Dictionary<Vector2I, int> { { origin, 0 } };
        var frontier = new List<(Vector2I coord, int cost)>
        {
            (origin, 0),
        };
        var gs = context.grid_service;
        var state = context.state;
        while (frontier.Count > 0)
        {
            var (cur, curCost) = frontier[0];
            frontier.RemoveAt(0);
            if (!bestCosts.TryGetValue(cur, out int bestCost) || bestCost != curCost)
                continue;
            foreach (Vector2I nv in gs.GetNeighbors4(state, cur))
            {
                var n = nv;
                if (!gs.CanUnitStepBetweenAnchors(state, ctxUnitState, cur, n))
                    continue;
                int nc = curCost + context.GetMoveCost(ctxUnitState, n);
                if (nc > maxMp)
                    continue;
                if (bestCosts.TryGetValue(n, out int knownCost) && knownCost <= nc)
                    continue;
                bestCosts[n] = nc;
                frontier.Add((n, nc));
                if (seen.Add(n))
                {
                    candidates.Add(n);
                }
            }
        }
        candidates.Sort(
            (l, r) =>
            {
                int ld = _distance_from_anchor_to_nearest_target(context, l),
                    rd = _distance_from_anchor_to_nearest_target(context, r);
                if (ld == rd)
                {
                    if (l.Y != r.Y)
                        return l.Y.CompareTo(r.Y);
                    return l.X.CompareTo(r.X);
                }
                return ld.CompareTo(rd);
            }
        );
        return candidates;
    }

    private int _distance_from_anchor_to_nearest_target(BattleAiContext context, Vector2I anchor)
    {
        List<BattleUnitState> t = _sort_target_units_typed(context, "enemy", target_selector);
        if (t.Count == 0)
            return 999999;
        return _distance_from_anchor_to_unit(
            context,
            context.unit_state,
            anchor,
            t[0]
        );
    }

    private void _apply_target_group_score(
        BattleAiScoreInput si,
        IEnumerable<BattleUnitState> tg
    )
    {
        if (si is not BattleAiScoreInput typed)
            return;
        var ids = new List<StringName>();
        var coords = new List<Vector2I>();
        foreach (var tu in tg)
        {
            if (tu == null)
                continue;
            ids.Add(tu.unit_id);
            coords.Add(tu.coord);
        }
        typed.target_unit_ids = ids;
        typed.target_coords = coords;
        typed.target_count = ids.Count;
        typed.total_score += ids.Count * target_count_weight;
    }

    private bool _is_better_reposition_score_input(BattleAiScoreInput c, BattleAiScoreInput b)
    {
        if (c == null)
            return false;
        if (b == null)
            return true;
        if (_score_target_count(c) != _score_target_count(b))
            return _score_target_count(c) > _score_target_count(b);
        if (_score_position_objective(c) != _score_position_objective(b))
            return _score_position_objective(c) > _score_position_objective(b);
        if (_score_total(c) != _score_total(b))
            return _score_total(c) > _score_total(b);
        return _score_resource_cost(c) < _score_resource_cost(b);
    }

    private static bool _is_multi_unit_skill(SkillDef sd) =>
        sd?.combat_profile != null
        && (sd.combat_profile as CombatSkillDef).TargetSelectionModeKind
            == BattleTargetSelectionMode.MultiUnit;

    private bool _has_explicit_distance_contract() =>
        desired_min_distance >= 0
        && desired_max_distance >= desired_min_distance
        && (
            DistanceReferenceKind == EnemyAiDistanceReference.TargetUnit
            || DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline
        );

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        var e = base.ValidateSchema();
        if (target_count_weight < 0)
            e.Add(
                $"MoveToMultiUnitSkillPositionAction {action_id} target_count_weight must be >= 0."
            );
        return e;
    }
}
