using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class UseChargePathAoeAction : EnemyAiAction
{
    private static readonly StringName PATH_STEP_AOE_EFFECT_TYPE = "path_step_aoe";

    private readonly struct ChargeTargetInfo
    {
        public ChargeTargetInfo(bool valid, int distance = 0, Vector2I direction = default)
        {
            Valid = valid;
            Distance = distance;
            Direction = direction;
        }

        public bool Valid { get; }
        public int Distance { get; }
        public Vector2I Direction { get; }
    }

    private sealed class PathStepHitMetrics
    {
        public Vector2I ResolvedAnchorCoord = new(-1, -1);
        public int ResolvedMoveDistance;
        public int HitCount;
        public Dictionary<StringName, int> HitCountsByUnitId = new();

        public int UniqueTargetCount => HitCountsByUnitId.Count;

        public void AddHit(StringName unitId)
        {
            StringName normalizedUnitId = ProgressionDataUtils.to_string_name(unitId);
            if (normalizedUnitId == "")
            {
                return;
            }
            HitCountsByUnitId[normalizedUnitId] =
                HitCountsByUnitId.GetValueOrDefault(normalizedUnitId, 0) + 1;
            HitCount += 1;
        }

        public void ApplyToMetadata(
            IDictionary<string, object> metadata,
            CombatEffectDef pathStepEffect
        )
        {
            if (metadata == null)
            {
                return;
            }
            metadata["resolved_anchor_coord"] = ResolvedAnchorCoord;
            metadata["resolved_move_distance"] = ResolvedMoveDistance;
            metadata["path_step_hit_count"] = HitCount;
            metadata["path_step_unique_target_count"] = UniqueTargetCount;
            metadata["path_step_hit_counts_by_unit_id"] = BuildHitCountsMetadata();
            if (pathStepEffect != null)
            {
                metadata["path_step_aoe_effect"] = pathStepEffect;
            }
        }

        private Dictionary<StringName, int> BuildHitCountsMetadata()
        {
            var result = new Dictionary<StringName, int>();
            foreach (KeyValuePair<StringName, int> entry in HitCountsByUnitId)
            {
                if (entry.Key != "" && entry.Value > 0)
                {
                    result[entry.Key] = entry.Value;
                }
            }
            return result;
        }
    }

    [Export]
    public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int minimum_hit_count { get; set; } = 1;

    [Export]
    public int desired_min_distance { get; set; } = 1;

    [Export]
    public int desired_max_distance { get; set; } = 1;

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        AiTraceRecorder.Enter("decide:charge_path_aoe");
        var r = _decide_impl(context);
        AiTraceRecorder.Exit("decide:charge_path_aoe");
        return r;
    }

    private BattleAiDecision _decide_impl(BattleAiContext context)
    {
        var at = _begin_action_trace(
            context,
            new System.Collections.Generic.Dictionary<string, object>
            {
                { "action_kind", "charge_path_aoe" },
                { "target_selector", (string)target_selector },
                { "minimum_hit_count", minimum_hit_count },
                { "desired_min_distance", desired_min_distance },
                { "desired_max_distance", desired_max_distance },
            }
        );
        List<BattleUnitState> targets = _sort_target_units_typed(context, "enemy", target_selector);
        if (targets.Count == 0)
        {
            _trace_add_block_reason(at, "no_valid_targets");
            _finalize_action_trace(context, at);
            return null;
        }
        var focusTarget = targets[0];
        BattleAiDecision bd = null;
        BattleAiScoreInput bsi = null;
        BattleAiDecision fd = null;
        var ctxUnitState = context.unit_state;
        var state = context.state;
        foreach (var sid in _resolve_known_skill_ids(context, skill_ids))
        {
            _trace_count_increment(at, "skill_considered_count", 1);
            var sd = _get_skill_def(context, sid);
            if (
                sd?.combat_profile == null
                || (sd.combat_profile as CombatSkillDef).TargetModeKind != BattleTargetMode.Ground
            )
            {
                _trace_add_block_reason(at, sd == null ? "missing_skill_def" : "non_ground_skill");
                continue;
            }
            var br = _get_skill_cast_block_reason(context, sd);
            if (BattleSkillCastBlockReasonKinds.IsBlocked(br))
            {
                _trace_add_block_reason(at, BattleSkillCastBlockReasonKinds.ToTraceKey(br));
                continue;
            }
            foreach (CombatCastVariantDef cv in _get_ground_options_typed(context, sd))
            {
                if (cv == null || !_is_charge_option(cv))
                    continue;
                var pse = _get_path_step_aoe_effect(cv);
                if (pse == null)
                {
                    _trace_add_block_reason(at, "missing_path_step_aoe");
                    continue;
                }
                for (int y = 0; y < state.map_size.Y; y++)
                for (int x = 0; x < state.map_size.X; x++)
                {
                    _trace_count_increment(at, "evaluation_count", 1);
                    var tc = new Vector2I(x, y);
                    ChargeTargetInfo chargeTargetInfo = _resolve_charge_target_info(
                        ctxUnitState,
                        tc
                    );
                    if (!chargeTargetInfo.Valid)
                    {
                        continue;
                    }
                    var cmd = _build_typed_ground_skill_command(
                        context,
                        sid,
                        cv.variant_id,
                        new[] { tc }
                    );
                    AiTraceRecorder.Enter("charge_path_aoe:formal_preview");
                    BattlePreview pv = context.PreviewCommand(cmd);
                    AiTraceRecorder.Exit("charge_path_aoe:formal_preview");
                    pv ??= BuildFastChargePathPreview(
                        ctxUnitState,
                        cmd,
                        chargeTargetInfo,
                        tc
                    );
                    if (pv?.allowed != true)
                    {
                        _trace_count_increment(at, "preview_reject_count", 1);
                        continue;
                    }
                    PathStepHitMetrics metrics = _build_path_step_hit_metrics(
                        context,
                        sd,
                        pse,
                        pv.resolved_anchor_coord
                    );
                    int phc = metrics.HitCount;
                    if (phc < minimum_hit_count)
                    {
                        _trace_add_block_reason(at, "minimum_hit_count");
                        continue;
                    }
                    var ra = metrics.ResolvedAnchorCoord != new Vector2I(-1, -1)
                        ? metrics.ResolvedAnchorCoord
                        : pv.resolved_anchor_coord;
                    int rmd = metrics.ResolvedMoveDistance;
                    int cas = 10 + Mathf.Max(rmd - 1, 0) * 4;
                    var posMeta = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        { "action_kind", "skill" },
                        { "action_base_score", cas },
                        { "position_target_unit_id", focusTarget.unit_id },
                        { "position_anchor_coord", ra },
                        { "desired_min_distance", desired_min_distance },
                        { "desired_max_distance", desired_max_distance },
                        { "action_label", _format_skill_variant_label(sd, cv) },
                    };
                    metrics.ApplyToMetadata(posMeta, pse);
                    var si = _build_typed_skill_score_input(
                        context,
                        sd,
                        cmd,
                        pv,
                        cv.effect_defs,
                        posMeta
                    );
                    _trace_offer_candidate(
                        at,
                        _build_candidate_summary(
                            _format_skill_variant_label(sd, cv),
                            cmd,
                            si,
                            new System.Collections.Generic.Dictionary<string, object>
                            {
                                { "path_step_hit_count", phc },
                                { "path_step_unique_target_count", metrics.UniqueTargetCount },
                                { "resolved_anchor_coord", ra },
                                { "resolved_move_distance", rmd },
                                { "skill_id", (string)sid },
                            }
                        )
                    );
                    if (si == null)
                    {
                        if (fd == null)
                            fd = _create_decision(
                                cmd,
                                $"{ctxUnitState.display_name} 准备用 {sd.display_name} 沿途命中 {phc} 次。"
                            );
                        continue;
                    }
                    if (!_is_better_skill_score_input(si, bsi))
                        continue;
                    bsi = si;
                    bd = _create_scored_decision(
                        cmd,
                        si,
                        $"{ctxUnitState.display_name} 准备用 {sd.display_name} 沿途命中 {phc} 次（评分 {_score_total(si)}）。"
                    );
                }
            }
        }
        var r = bd ?? fd;
        _finalize_action_trace(context, at, r);
        return r;
    }

    private static CombatEffectDef _get_path_step_aoe_effect(CombatCastVariantDef cv)
    {
        if (cv == null)
            return null;
        foreach (var r in cv.effect_defs)
        {
            var ed = r as CombatEffectDef;
            if (ed != null && ed.EffectKind == BattleEffectKind.PathStepAoe)
                return ed;
        }
        return null;
    }

    private static ChargeTargetInfo _resolve_charge_target_info(
        BattleUnitState us,
        Vector2I tc
    )
    {
        if (us == null)
            return new ChargeTargetInfo(false);
        us.RefreshFootprint();
        int minX = us.coord.X,
            maxX = us.coord.X + us.footprint_size.X - 1,
            minY = us.coord.Y,
            maxY = us.coord.Y + us.footprint_size.Y - 1;
        if (tc.Y >= minY && tc.Y <= maxY)
        {
            if (tc.X < minX)
                return new ChargeTargetInfo(true, minX - tc.X, Vector2I.Left);
            if (tc.X > maxX)
                return new ChargeTargetInfo(true, tc.X - maxX, Vector2I.Right);
        }
        if (tc.X >= minX && tc.X <= maxX)
        {
            if (tc.Y < minY)
                return new ChargeTargetInfo(true, minY - tc.Y, Vector2I.Up);
            if (tc.Y > maxY)
                return new ChargeTargetInfo(true, tc.Y - maxY, Vector2I.Down);
        }
        return new ChargeTargetInfo(false);
    }

    private static BattlePreview BuildFastChargePathPreview(
        BattleUnitState actor,
        BattleCommand command,
        ChargeTargetInfo chargeInfo,
        Vector2I targetCoord
    )
    {
        Vector2I resolvedAnchor =
            actor != null && chargeInfo.Valid
                ? actor.coord + chargeInfo.Direction * chargeInfo.Distance
                : new Vector2I(-1, -1);
        var preview = new BattlePreview
        {
            allowed = command != null && resolvedAnchor != new Vector2I(-1, -1),
            resolved_anchor_coord = resolvedAnchor,
            move_cost = Mathf.Max(chargeInfo.Distance, 0),
        };
        if (preview.allowed)
        {
            preview.AddTargetCoord(targetCoord);
        }
        return preview;
    }

    private PathStepHitMetrics _build_path_step_hit_metrics(
        BattleAiContext context,
        SkillDef sd,
        CombatEffectDef pse,
        Vector2I rac
    )
    {
        var result = new PathStepHitMetrics { ResolvedAnchorCoord = rac };
        if (
            context?.state == null
            || context?.unit_state == null
            || context?.grid_service == null
            || pse == null
            || rac == new Vector2I(-1, -1)
        )
            return result;
        var ctxUnitState = context.unit_state;
        var path = _build_resolved_anchor_path(ctxUnitState.coord, rac);
        if (path.Count == 0)
            return result;
        bool arh = pse.allow_repeat_hits_across_steps;
        var tf = BattleTargetTeamRules.ResolveEffectTargetFilter(sd, pse);
        var state = context.state;
        foreach (var ac in path)
        {
            var ecs = _build_path_step_effect_coords(context, ac, pse);
            if (ecs.Count == 0)
                continue;
            var stepUnitIds = new HashSet<StringName>();
            foreach (BattleUnitState tu in state.GetUnitsTyped())
            {
                if (tu == null || !tu.is_alive)
                    continue;
                if (!BattleTargetTeamRules.IsUnitValidForFilter(ctxUnitState, tu, tf))
                    continue;
                if (!_unit_intersects_coords(tu, ecs))
                    continue;
                if (!arh && result.HitCountsByUnitId.ContainsKey(tu.unit_id))
                    continue;
                if (stepUnitIds.Contains(tu.unit_id))
                    continue;
                stepUnitIds.Add(tu.unit_id);
            }
            foreach (StringName unitId in stepUnitIds)
            {
                result.AddHit(unitId);
            }
        }
        result.ResolvedMoveDistance = path.Count;
        return result;
    }

    private static List<Vector2I> _build_resolved_anchor_path(
        Vector2I sc,
        Vector2I rac
    )
    {
        var p = new List<Vector2I>();
        var delta = rac - sc;
        var dir = Vector2I.Zero;
        int dist = 0;
        if (delta.Y == 0 && delta.X != 0)
        {
            dir = delta.X > 0 ? Vector2I.Right : Vector2I.Left;
            dist = Mathf.Abs(delta.X);
        }
        else if (delta.X == 0 && delta.Y != 0)
        {
            dir = delta.Y > 0 ? Vector2I.Down : Vector2I.Up;
            dist = Mathf.Abs(delta.Y);
        }
        if (dir == Vector2I.Zero || dist <= 0)
            return p;
        var ac = sc;
        for (int i = 0; i < dist; i++)
        {
            ac += dir;
            p.Add(ac);
        }
        return p;
    }

    private List<Vector2I> _build_path_step_effect_coords(
        BattleAiContext context,
        Vector2I ac,
        CombatEffectDef pse
    )
    {
        var result = new List<Vector2I>();
        if (
            context?.state == null
            || context?.grid_service == null
            || pse == null
        )
            return result;
        var ctxUnitState = context.unit_state;
        var gs = context.grid_service;
        var state = context.state;
        var ss = ProgressionDataUtils.to_string_name(
            pse.@params.ContainsKey("step_shape") ? pse.@params["step_shape"] : "diamond"
        );
        int sr = Mathf.Max(
            pse.@params.ContainsKey("step_radius") ? pse.@params["step_radius"].AsInt32() : 1,
            0
        );
        var coordSet = new HashSet<Vector2I>();
        foreach (
            Vector2I oc in gs.GetUnitTargetCoords(ctxUnitState, ac)
        )
        foreach (
            Vector2I ec in gs.GetAreaCoords(state, oc, ss, sr, Vector2I.Zero)
        )
            coordSet.Add(ec);
        result.AddRange(coordSet);
        SortCoordsInPlace(result);
        return result;
    }

    private static bool _unit_intersects_coords(
        BattleUnitState us,
        IReadOnlyCollection<Vector2I> coords
    )
    {
        if (us == null || coords == null || coords.Count == 0)
            return false;
        var coordSet = new HashSet<Vector2I>(coords);
        us.RefreshFootprint();
        foreach (Vector2I occupiedCoord in us.occupied_coords)
        {
            if (coordSet.Contains(occupiedCoord))
            {
                return true;
            }
        }
        return false;
    }

    private static void SortCoordsInPlace(List<Vector2I> coords)
    {
        coords.Sort(
            (left, right) =>
            {
                int yComparison = left.Y.CompareTo(right.Y);
                return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
            }
        );
    }

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        var e = _collect_base_validation_errors();
        if (skill_ids.Count == 0)
            e.Add($"UseChargePathAoeAction {action_id} must declare at least one skill_id.");
        if (target_selector == "")
            e.Add($"UseChargePathAoeAction {action_id} is missing target_selector.");
        _append_enemy_focus_target_selector_errors(e, "UseChargePathAoeAction", target_selector);
        if (minimum_hit_count <= 0)
            e.Add($"UseChargePathAoeAction {action_id} minimum_hit_count must be >= 1.");
        if (desired_min_distance < 0)
            e.Add($"UseChargePathAoeAction {action_id} desired_min_distance must be >= 0.");
        if (desired_max_distance < desired_min_distance)
            e.Add(
                $"UseChargePathAoeAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        return e;
    }
}
