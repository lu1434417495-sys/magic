using System.Collections.Generic;
using System.Linq;
using Godot;

[GlobalClass]
public partial class MoveToAdvantagePositionAction : EnemyAiAction
{
    private static readonly StringName MODE_ADVANTAGE = "advantage",
        MODE_SURVIVAL = "survival",
        MODE_HIGH_GROUND = "high_ground";

    private enum MoveToAdvantagePositioningMode
    {
        Unknown,
        Advantage,
        Survival,
        HighGround,
    }

    [Export]
    public StringName target_selector { get; set; } = "nearest_enemy";

    [Export]
    public int desired_min_distance { get; set; } = 3;

    [Export]
    public int desired_max_distance { get; set; } = 5;

    [Export]
    public Godot.Collections.Array<StringName> range_skill_ids { get; set; } = new();

    [Export]
    public int minimum_safe_distance { get; set; } = 3;

    [Export]
    public int safe_distance_margin { get; set; } = 1;

    // Survival mode only: minimum survival-margin gain a retreat must buy before it is
    // offered when the actor faces no lethal risk. Default 1 stops an already-safe unit
    // from kiting forever instead of committing to offense. Negative = always retreat
    // (legacy). Exposed for simulation-based tuning via action override patches.
    [Export]
    public int min_survival_margin_gain_to_escape { get; set; } = 1;

    // High-ground mode only: when the actor is farther than desired_max_distance,
    // require high-ground moves to close this many distance steps toward the focus target.
    // 0 disables the gate.
    [Export]
    public int min_distance_progress_when_beyond_band { get; set; }

    [Export]
    public StringName positioning_mode { get; set; } = MODE_ADVANTAGE;

    [Export]
    public int high_ground_weight { get; set; } = 60;

    [Export]
    public int safety_weight { get; set; } = 50;

    [Export]
    public int distance_band_weight { get; set; } = 20;

    [Export]
    public int candidate_limit { get; set; } = 96;

    private MoveToAdvantagePositioningMode PositioningModeKind =>
        ToPositioningMode(positioning_mode);

    private readonly struct MoveCandidate
    {
        public readonly Vector2I Coord;
        public readonly int Dist;
        public readonly int Safety;
        public readonly int Height;
        public readonly int MoveCost;

        public MoveCandidate(Vector2I coord, int dist, int safety, int height, int moveCost)
        {
            Coord = coord;
            Dist = dist;
            Safety = safety;
            Height = height;
            MoveCost = moveCost;
        }
    }

    internal override BattleAiDecision Decide(BattleAiContext context)
    {
        AiTraceRecorder.Enter("decide:move_to_advantage_position");
        var r = _decide_impl(context);
        AiTraceRecorder.Exit("decide:move_to_advantage_position");
        return r;
    }

    private BattleAiDecision _decide_impl(BattleAiContext context)
    {
        MoveToAdvantagePositioningMode positioningMode = PositioningModeKind;
        IReadOnlyDictionary<string, object> dc = _resolve_desired_distance_contract_typed(
            context,
            (SkillDefinition)null,
            range_skill_ids
        );
        int resolvedMinDistance = ReadMetadataInt(
            dc,
            "desired_min_distance",
            desired_min_distance
        );
        int resolvedMaxDistance = ReadMetadataInt(
            dc,
            "desired_max_distance",
            desired_max_distance
        );
        int effectiveAttackRange = ReadMetadataInt(dc, "effective_attack_range", -1);
        var at = _begin_action_trace(
            context,
            new System.Collections.Generic.Dictionary<string, object>
            {
                { "action_kind", "move_to_advantage_position" },
                { "target_selector", (string)target_selector },
                { "desired_min_distance", resolvedMinDistance },
                { "desired_max_distance", resolvedMaxDistance },
                { "configured_desired_min_distance", desired_min_distance },
                { "configured_desired_max_distance", desired_max_distance },
                { "effective_attack_range", effectiveAttackRange },
                { "range_skill_ids", new List<StringName>(range_skill_ids) },
                { "minimum_safe_distance", minimum_safe_distance },
                { "safe_distance_margin", safe_distance_margin },
                {
                    "min_distance_progress_when_beyond_band",
                    min_distance_progress_when_beyond_band
                },
                { "positioning_mode", (string)positioning_mode },
                { "high_ground_weight", high_ground_weight },
                { "safety_weight", safety_weight },
                { "distance_band_weight", distance_band_weight },
            }
        );
        if (
            context?.state == null
            || context?.unit_state == null
            || context?.grid_service == null
        )
        {
            _trace_add_block_reason(at, "missing_context");
            _finalize_action_trace(context, at);
            return null;
        }
        var ctxUnitState = context.unit_state;
        List<BattleUnitState> targets = _sort_target_units_typed(
            context,
            "enemy",
            target_selector
        );
        BattleUnitState focusTarget = targets.Count > 0 ? targets[0] : null;
        int currentFocusDistance =
            focusTarget != null
                ? _distance_from_anchor_to_unit(
                    context,
                    ctxUnitState,
                    ctxUnitState.coord,
                    focusTarget
                )
                : -1;
        if (
            positioningMode == MoveToAdvantagePositioningMode.Survival
            && focusTarget != null
        )
        {
            int currentSafeDistance = _resolve_target_safe_distance(
                context,
                focusTarget,
                minimum_safe_distance,
                safe_distance_margin
            );
            if (currentFocusDistance >= currentSafeDistance)
            {
                _trace_add_block_reason(at, "already_safe");
                _finalize_action_trace(context, at);
                return null;
            }
        }
        BattleAiDecision bd = null;
        BattleAiScoreInput bsi = null;
        var gs = context.grid_service;
        var state = context.state;
        var currentCell = gs.GetCellState(state, ctxUnitState.coord);
        int currentHeight = currentCell?.current_height ?? 0;

        List<MoveCandidate> fastCandidates;
        bool useFastCandidates = _try_collect_fast_move_candidates(
            context,
            focusTarget,
            currentHeight,
            positioningMode,
            out fastCandidates
        );
        IEnumerable<MoveCandidate> candidateSequence = fastCandidates;
        if (!useFastCandidates)
        {
            int my = state.map_size.Y,
                mx = state.map_size.X;
            var candidates = new List<(Vector2I coord, int dist, int safety, int height)>();
            for (int y = 0; y < my; y++)
            {
                for (int x = 0; x < mx; x++)
                {
                    var c = new Vector2I(x, y);
                    if (
                        !gs.CanPlaceFootprint(
                            state,
                            c,
                            ctxUnitState.footprint_size,
                            ctxUnitState.unit_id,
                            ctxUnitState
                        )
                    )
                        continue;
                    var cell = gs.GetCellState(state, c);
                    int height = cell?.current_height ?? 0;
                    if (
                        positioningMode == MoveToAdvantagePositioningMode.HighGround
                        && height <= currentHeight
                    )
                        continue;
                    int dist =
                        focusTarget != null
                            ? _distance_from_anchor_to_unit(context, ctxUnitState, c, focusTarget)
                            : 0;
                    int safety = _resolve_target_safe_distance(
                        context,
                        focusTarget,
                        minimum_safe_distance,
                        safe_distance_margin
                    );
                    candidates.Add((c, dist, safety, height));
                }
            }

            _sort_full_scan_candidates(candidates, positioningMode);
            candidateSequence = candidates.Select(
                value => new MoveCandidate(
                    value.coord,
                    value.dist,
                    value.safety,
                    value.height,
                    0
                )
            );
        }

        int evalCount = 0;
        foreach (MoveCandidate candidate in candidateSequence)
        {
            if (evalCount >= candidate_limit)
                break;
            evalCount++;
            _trace_count_increment(at, "evaluation_count", 1);
            if (
                _should_skip_candidate_without_distance_progress(
                    positioningMode,
                    currentFocusDistance,
                    candidate.Dist,
                    resolvedMaxDistance
                )
            )
            {
                _trace_count_increment(at, "no_distance_progress_skip_count", 1);
                continue;
            }
            var cmd = _build_move_command(context, candidate.Coord);
            BattlePreview pv = _build_fast_move_preview(context, candidate.Coord, candidate.MoveCost);
            if (pv?.allowed != true)
            {
                _trace_count_increment(at, "preview_reject_count", 1);
                continue;
            }
            var si = _build_typed_action_score_input(
                context,
                "move",
                (string)action_id,
                cmd,
                pv,
                new Dictionary<string, object>(System.StringComparer.Ordinal)
                {
                    { "position_target_unit_id", focusTarget?.unit_id ?? new StringName("") },
                    { "position_anchor_coord", candidate.Coord },
                    {
                        "desired_min_distance",
                        dc.ContainsKey("desired_min_distance")
                            ? (int)dc["desired_min_distance"]
                            : desired_min_distance
                    },
                    {
                        "desired_max_distance",
                        dc.ContainsKey("desired_max_distance")
                            ? (int)dc["desired_max_distance"]
                            : desired_max_distance
                    },
                    { "position_current_distance", candidate.Dist },
                    { "position_safe_distance", candidate.Safety },
                    { "position_objective_kind", "distance_band_progress" },
                    { "high_ground_weight", high_ground_weight },
                    { "safety_weight", safety_weight },
                    { "distance_band_weight", distance_band_weight },
                    { "move_cost", candidate.MoveCost },
                }
            );
            if (
                positioningMode == MoveToAdvantagePositioningMode.Survival
                && _is_unthreatened_reposition(si, min_survival_margin_gain_to_escape)
            )
            {
                _trace_count_increment(at, "no_survival_gain_skip_count", 1);
                continue;
            }
            _trace_offer_candidate(
                at,
                _build_candidate_summary(
                    $"move_to_{candidate.Coord.X}_{candidate.Coord.Y}",
                    cmd,
                    si,
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "coord", candidate.Coord },
                        { "dist", candidate.Dist },
                        { "height", candidate.Height },
                    }
                )
            );
            if (!_is_better_skill_score_input(si, bsi))
                continue;
            bsi = si;
            bd = _create_scored_decision(
                cmd,
                si,
                $"{ctxUnitState.display_name} 移动到 ({candidate.Coord.X},{candidate.Coord.Y})（评分 {_score_total(si)}）。"
            );
        }
        _finalize_action_trace(context, at, bd);
        return bd;
    }

    private bool _should_skip_candidate_without_distance_progress(
        MoveToAdvantagePositioningMode positioningMode,
        int currentDistance,
        int candidateDistance,
        int resolvedMaxDistance
    )
    {
        if (
            positioningMode != MoveToAdvantagePositioningMode.HighGround
            || min_distance_progress_when_beyond_band <= 0
            || currentDistance < 0
            || candidateDistance < 0
            || resolvedMaxDistance < 0
            || currentDistance <= resolvedMaxDistance
        )
        {
            return false;
        }

        return currentDistance - candidateDistance < min_distance_progress_when_beyond_band;
    }

    private bool _try_collect_fast_move_candidates(
        BattleAiContext context,
        BattleUnitState focusTarget,
        int currentHeight,
        MoveToAdvantagePositioningMode positioningMode,
        out List<MoveCandidate> result
    )
    {
        result = new List<MoveCandidate>();
        if (
            context?.state == null
            || context?.unit_state == null
            || context.grid_service == null
        )
        {
            return false;
        }

        if (_is_unit_movement_blocked(context, context.unit_state))
        {
            return true;
        }

        int moveBudget = _resolve_current_move_budget(context.unit_state);
        if (moveBudget <= 0)
        {
            return true;
        }

        BattleState state = context.state;
        BattleUnitState actor = context.unit_state;
        BattleGridService grid = context.grid_service;
        var frontier = new Queue<(Vector2I Coord, int Cost)>();
        var bestCosts = new Dictionary<Vector2I, int> { [actor.coord] = 0 };
        frontier.Enqueue((actor.coord, 0));
        int safety = _resolve_target_safe_distance(
            context,
            focusTarget,
            minimum_safe_distance,
            safe_distance_margin
        );
        while (frontier.Count > 0)
        {
            (Vector2I currentCoord, int currentCost) = frontier.Dequeue();
            if (currentCost != bestCosts.GetValueOrDefault(currentCoord, int.MaxValue))
            {
                continue;
            }

            foreach (Vector2I neighbor in grid.GetNeighbors4(state, currentCoord))
            {
                if (!grid.CanUnitStepBetweenAnchors(state, actor, currentCoord, neighbor))
                {
                    continue;
                }
                int nextCost = currentCost + Mathf.Max(context.GetMoveCost(actor, neighbor), 1);
                if (nextCost > moveBudget)
                {
                    continue;
                }
                if (
                    bestCosts.TryGetValue(neighbor, out int existingCost)
                    && nextCost >= existingCost
                )
                {
                    continue;
                }

                bestCosts[neighbor] = nextCost;
                frontier.Enqueue((neighbor, nextCost));
                BattleCellState cell = grid.GetCellState(state, neighbor);
                int height = cell?.current_height ?? currentHeight;
                if (
                    positioningMode == MoveToAdvantagePositioningMode.HighGround
                    && height <= currentHeight
                )
                {
                    continue;
                }
                int dist =
                    focusTarget != null
                        ? _distance_from_anchor_to_unit(context, actor, neighbor, focusTarget)
                        : 0;
                result.Add(new MoveCandidate(neighbor, dist, safety, height, nextCost));
            }
        }

        _sort_fast_candidates(result, positioningMode);
        return true;
    }

    private void _sort_full_scan_candidates(
        List<(Vector2I coord, int dist, int safety, int height)> candidates,
        MoveToAdvantagePositioningMode positioningMode
    )
    {
        if (positioningMode == MoveToAdvantagePositioningMode.Survival)
            candidates.Sort(
                (a, b) =>
                {
                    int sa = Mathf.Max(a.safety - a.dist, 0),
                        sb = Mathf.Max(b.safety - b.dist, 0);
                    if (sa != sb)
                        return sb.CompareTo(sa);
                    return a.dist.CompareTo(b.dist);
                }
            );
        else if (positioningMode == MoveToAdvantagePositioningMode.HighGround)
            candidates.Sort(
                (a, b) =>
                {
                    if (a.height != b.height)
                        return b.height.CompareTo(a.height);
                    int sa = Mathf.Max(a.safety - a.dist, 0),
                        sb = Mathf.Max(b.safety - b.dist, 0);
                    if (sa != sb)
                        return sb.CompareTo(sa);
                    return a.dist.CompareTo(b.dist);
                }
            );
        else
            candidates.Sort(
                (a, b) =>
                {
                    int da = Mathf.Abs(a.dist - desired_min_distance),
                        db = Mathf.Abs(b.dist - desired_min_distance);
                    if (da != db)
                        return da.CompareTo(db);
                    int sa = Mathf.Max(a.safety - a.dist, 0),
                        sb = Mathf.Max(b.safety - b.dist, 0);
                    if (sa != sb)
                        return sb.CompareTo(sa);
                    return b.height.CompareTo(a.height);
                }
            );
    }

    private void _sort_fast_candidates(
        List<MoveCandidate> candidates,
        MoveToAdvantagePositioningMode positioningMode
    )
    {
        if (positioningMode == MoveToAdvantagePositioningMode.Survival)
            candidates.Sort(
                (a, b) =>
                {
                    int sa = Mathf.Max(a.Safety - a.Dist, 0),
                        sb = Mathf.Max(b.Safety - b.Dist, 0);
                    if (sa != sb)
                        return sb.CompareTo(sa);
                    return a.Dist.CompareTo(b.Dist);
                }
            );
        else if (positioningMode == MoveToAdvantagePositioningMode.HighGround)
            candidates.Sort(
                (a, b) =>
                {
                    if (a.Height != b.Height)
                        return b.Height.CompareTo(a.Height);
                    int sa = Mathf.Max(a.Safety - a.Dist, 0),
                        sb = Mathf.Max(b.Safety - b.Dist, 0);
                    if (sa != sb)
                        return sb.CompareTo(sa);
                    return a.Dist.CompareTo(b.Dist);
                }
            );
        else
            candidates.Sort(
                (a, b) =>
                {
                    int da = Mathf.Abs(a.Dist - desired_min_distance),
                        db = Mathf.Abs(b.Dist - desired_min_distance);
                    if (da != db)
                        return da.CompareTo(db);
                    int sa = Mathf.Max(a.Safety - a.Dist, 0),
                        sb = Mathf.Max(b.Safety - b.Dist, 0);
                    if (sa != sb)
                        return sb.CompareTo(sa);
                    return b.Height.CompareTo(a.Height);
                }
            );
    }

    private BattlePreview _build_fast_move_preview(
        BattleAiContext context,
        Vector2I targetCoord,
        int moveCost
    )
    {
        var preview = new BattlePreview
        {
            allowed = true,
            move_cost = Mathf.Max(moveCost, 0),
            resolved_anchor_coord = targetCoord,
        };
        if (context?.grid_service == null || context.unit_state == null)
        {
            preview.allowed = false;
            return preview;
        }

        foreach (
            Vector2I coord in context.grid_service.GetUnitTargetCoords(
                context.unit_state,
                targetCoord
            )
        )
        {
            preview.AddTargetCoord(coord);
        }
        return preview;
    }

    public override Godot.Collections.Array<string> ValidateSchema()
    {
        var e = _collect_base_validation_errors();
        if (target_selector == "")
            e.Add($"MoveToAdvantagePositionAction {action_id} is missing target_selector.");
        _append_enemy_focus_target_selector_errors(
            e,
            "MoveToAdvantagePositionAction",
            target_selector
        );
        if (desired_min_distance < 0)
            e.Add($"MoveToAdvantagePositionAction {action_id} desired_min_distance must be >= 0.");
        if (desired_max_distance < desired_min_distance)
            e.Add(
                $"MoveToAdvantagePositionAction {action_id} desired_max_distance must be >= desired_min_distance."
            );
        if (minimum_safe_distance < 0)
            e.Add($"MoveToAdvantagePositionAction {action_id} minimum_safe_distance must be >= 0.");
        if (safe_distance_margin < 0)
            e.Add($"MoveToAdvantagePositionAction {action_id} safe_distance_margin must be >= 0.");
        if (min_distance_progress_when_beyond_band < 0)
            e.Add(
                $"MoveToAdvantagePositionAction {action_id} min_distance_progress_when_beyond_band must be >= 0."
            );
        if (PositioningModeKind == MoveToAdvantagePositioningMode.Unknown)
            e.Add(
                $"MoveToAdvantagePositionAction {action_id} positioning_mode must be advantage, survival, or high_ground."
            );
        return e;
    }

    private static MoveToAdvantagePositioningMode ToPositioningMode(StringName mode)
    {
        if (mode == MODE_ADVANTAGE)
        {
            return MoveToAdvantagePositioningMode.Advantage;
        }
        if (mode == MODE_SURVIVAL)
        {
            return MoveToAdvantagePositioningMode.Survival;
        }
        if (mode == MODE_HIGH_GROUND)
        {
            return MoveToAdvantagePositioningMode.HighGround;
        }
        return MoveToAdvantagePositioningMode.Unknown;
    }

    private static int ReadMetadataInt(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        int fallback
    )
    {
        if (metadata == null || !metadata.TryGetValue(key, out object value) || value == null)
        {
            return fallback;
        }
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            bool boolValue => boolValue ? 1 : 0,
            string textValue when int.TryParse(textValue, out int parsed) => parsed,
            StringName stringNameValue
                when int.TryParse(stringNameValue.ToString(), out int parsed) => parsed,
            _ => fallback,
        };
    }
}
