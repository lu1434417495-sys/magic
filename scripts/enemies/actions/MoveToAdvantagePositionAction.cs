using System.Collections.Generic;
using System.Linq;
using Godot;

[GlobalClass]
public partial class MoveToAdvantagePositionAction : EnemyAiAction
{
    private static readonly StringName MODE_ADVANTAGE = "advantage",
        MODE_SURVIVAL = "survival",
        MODE_HIGH_GROUND = "high_ground";

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

    public override BattleAiDecision decide(BattleAiContext context)
    {
        AiTraceRecorder.enter("decide:move_to_advantage_position");
        var r = _decide_impl(context);
        AiTraceRecorder.exit("decide:move_to_advantage_position");
        return r;
    }

    private BattleAiDecision _decide_impl(BattleAiContext context)
    {
        var dc = _resolve_desired_distance_contract(context, null, range_skill_ids);
        var at = _begin_action_trace(
            context,
            new Godot.Collections.Dictionary
            {
                { "action_kind", "move_to_advantage_position" },
                { "target_selector", (string)target_selector },
                {
                    "desired_min_distance",
                    dc.ContainsKey("desired_min_distance")
                        ? dc["desired_min_distance"].AsInt32()
                        : desired_min_distance
                },
                {
                    "desired_max_distance",
                    dc.ContainsKey("desired_max_distance")
                        ? dc["desired_max_distance"].AsInt32()
                        : desired_max_distance
                },
                { "configured_desired_min_distance", desired_min_distance },
                { "configured_desired_max_distance", desired_max_distance },
                {
                    "effective_attack_range",
                    dc.ContainsKey("effective_attack_range")
                        ? dc["effective_attack_range"].AsInt32()
                        : -1
                },
                { "range_skill_ids", new Godot.Collections.Array<StringName>(range_skill_ids) },
                { "minimum_safe_distance", minimum_safe_distance },
                { "safe_distance_margin", safe_distance_margin },
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
        if (positioning_mode == MODE_SURVIVAL && focusTarget != null)
        {
            int currentDistance = _distance_from_anchor_to_unit(
                context,
                ctxUnitState,
                ctxUnitState.coord,
                focusTarget
            );
            int currentSafeDistance = _resolve_target_safe_distance(
                context,
                focusTarget,
                minimum_safe_distance,
                safe_distance_margin
            );
            if (currentDistance >= currentSafeDistance)
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
        var currentCell = gs.get_cell(state, ctxUnitState.coord);
        int currentHeight = currentCell?.current_height ?? 0;

        List<MoveCandidate> fastCandidates;
        bool useFastCandidates = _try_collect_fast_move_candidates(
            context,
            focusTarget,
            currentHeight,
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
                        !gs.can_place_footprint(
                            state,
                            c,
                            ctxUnitState.footprint_size,
                            ctxUnitState.unit_id,
                            ctxUnitState
                        )
                    )
                        continue;
                    var cell = gs.get_cell(state, c);
                    int height = cell?.current_height ?? 0;
                    if (positioning_mode == MODE_HIGH_GROUND && height <= currentHeight)
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

            _sort_legacy_candidates(candidates);
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
            var cmd = _build_move_command(context, candidate.Coord);
            BattlePreview pv = _build_fast_move_preview(context, candidate.Coord, candidate.MoveCost);
            if (pv?.allowed != true)
            {
                _trace_count_increment(at, "preview_reject_count", 1);
                continue;
            }
            var si = _build_action_score_input(
                context,
                "move",
                (string)action_id,
                cmd,
                pv,
                new Godot.Collections.Dictionary
                {
                    { "position_target_unit_id", focusTarget?.unit_id ?? new StringName("") },
                    { "position_anchor_coord", candidate.Coord },
                    {
                        "desired_min_distance",
                        dc.ContainsKey("desired_min_distance")
                            ? dc["desired_min_distance"].AsInt32()
                            : desired_min_distance
                    },
                    {
                        "desired_max_distance",
                        dc.ContainsKey("desired_max_distance")
                            ? dc["desired_max_distance"].AsInt32()
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
            _trace_offer_candidate(
                at,
                _build_candidate_summary(
                    $"move_to_{candidate.Coord.X}_{candidate.Coord.Y}",
                    cmd,
                    si,
                    new Godot.Collections.Dictionary
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

    private bool _try_collect_fast_move_candidates(
        BattleAiContext context,
        BattleUnitState focusTarget,
        int currentHeight,
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

        int moveBudget = _resolve_current_move_budget(context.unit_state);
        if (moveBudget <= 0)
        {
            return true;
        }

        BattleState state = context.state;
        BattleUnitState actor = context.unit_state;
        BattleGridService grid = context.grid_service;
        if (
            (actor.has_taken_action_this_turn || actor.has_moved_this_turn)
            && !actor.can_use_locked_move_points_this_turn
        )
        {
            return true;
        }
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

            foreach (Vector2I neighbor in grid.get_neighbors_4(state, currentCoord))
            {
                if (!grid.can_unit_step_between_anchors(state, actor, currentCoord, neighbor))
                {
                    continue;
                }
                int nextCost = currentCost + Mathf.Max(context.get_move_cost(actor, neighbor), 1);
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
                BattleCellState cell = grid.get_cell(state, neighbor);
                int height = cell?.current_height ?? currentHeight;
                if (positioning_mode == MODE_HIGH_GROUND && height <= currentHeight)
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

        _sort_fast_candidates(result);
        return true;
    }

    private void _sort_legacy_candidates(
        List<(Vector2I coord, int dist, int safety, int height)> candidates
    )
    {
        if (positioning_mode == MODE_SURVIVAL)
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
        else if (positioning_mode == MODE_HIGH_GROUND)
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

    private void _sort_fast_candidates(List<MoveCandidate> candidates)
    {
        if (positioning_mode == MODE_SURVIVAL)
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
        else if (positioning_mode == MODE_HIGH_GROUND)
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
            Vector2I coord in context.grid_service.get_unit_target_coords(
                context.unit_state,
                targetCoord
            )
        )
        {
            preview.target_coords.Add(coord);
        }
        return preview;
    }

    private static int _resolve_current_move_budget(BattleUnitState unitState)
    {
        if (unitState == null || unitState.current_move_points <= 0)
        {
            return 0;
        }
        bool lockedByPriorAction = unitState.has_taken_action_this_turn || unitState.has_moved_this_turn;
        return lockedByPriorAction && !unitState.can_use_locked_move_points_this_turn
            ? 0
            : Mathf.Max(unitState.current_move_points, 0);
    }

    public override Godot.Collections.Array<string> validate_schema()
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
        if (
            positioning_mode != MODE_ADVANTAGE
            && positioning_mode != MODE_SURVIVAL
            && positioning_mode != MODE_HIGH_GROUND
        )
            e.Add(
                $"MoveToAdvantagePositionAction {action_id} positioning_mode must be advantage, survival, or high_ground."
            );
        return e;
    }
}
