using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal sealed class BattleAiMoveToAdvantageActionEvaluator
{
    private static readonly StringName ModeAdvantage = "advantage";
    private static readonly StringName ModeSurvival = "survival";
    private static readonly StringName ModeHighGround = "high_ground";

    private readonly BattleAiTypedActionHelper _helper = new();

    private enum PositioningMode
    {
        Unknown,
        Advantage,
        Survival,
        HighGround,
    }

    private readonly struct MoveCandidate
    {
        internal MoveCandidate(
            Vector2I coord,
            int distance,
            int safety,
            int height,
            int moveCost
        )
        {
            Coord = coord;
            Distance = distance;
            Safety = safety;
            Height = height;
            MoveCost = moveCost;
        }

        internal Vector2I Coord { get; }
        internal int Distance { get; }
        internal int Safety { get; }
        internal int Height { get; }
        internal int MoveCost { get; }
    }

    internal BattleAiDecision Evaluate(
        MoveToAdvantagePositionActionDefinition action,
        BattleAiContext context
    )
    {
        if (action == null)
            return null;
        AiTraceRecorder.Enter("decide:move_to_advantage_position");
        try
        {
            return EvaluateImpl(action, context);
        }
        finally
        {
            AiTraceRecorder.Exit("decide:move_to_advantage_position");
        }
    }

    private BattleAiDecision EvaluateImpl(
        MoveToAdvantagePositionActionDefinition action,
        BattleAiContext context
    )
    {
        PositioningMode positioningMode = ToPositioningMode(action.PositioningMode);
        IReadOnlyDictionary<string, object> distanceContract =
            BattleAiActionEvaluatorUtilities.ResolveDesiredDistanceContract(
                context,
                action.DesiredMinDistance,
                action.DesiredMaxDistance,
                action.RangeSkillIds,
                _helper
            );
        int resolvedMinDistance = ReadMetadataInt(
            distanceContract,
            "desired_min_distance",
            action.DesiredMinDistance
        );
        int resolvedMaxDistance = ReadMetadataInt(
            distanceContract,
            "desired_max_distance",
            action.DesiredMaxDistance
        );
        int effectiveAttackRange = ReadMetadataInt(
            distanceContract,
            "effective_attack_range",
            -1
        );
        AiActionTrace trace = context?.trace_enabled == true
            ? BeginActionTrace(
                action,
                context,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["action_kind"] = "move_to_advantage_position",
                    ["target_selector"] = action.TargetSelector.ToString(),
                    ["desired_min_distance"] = resolvedMinDistance,
                    ["desired_max_distance"] = resolvedMaxDistance,
                    ["configured_desired_min_distance"] = action.DesiredMinDistance,
                    ["configured_desired_max_distance"] = action.DesiredMaxDistance,
                    ["effective_attack_range"] = effectiveAttackRange,
                    ["range_skill_ids"] = new List<StringName>(action.RangeSkillIds),
                    ["minimum_safe_distance"] = action.MinimumSafeDistance,
                    ["safe_distance_margin"] = action.SafeDistanceMargin,
                    ["min_distance_progress_when_beyond_band"] =
                        action.MinDistanceProgressWhenBeyondBand,
                    ["positioning_mode"] = action.PositioningMode.ToString(),
                    ["high_ground_weight"] = action.HighGroundWeight,
                    ["safety_weight"] = action.SafetyWeight,
                    ["distance_band_weight"] = action.DistanceBandWeight,
                }
            )
            : null;
        if (
            context?.state == null
            || context.unit_state == null
            || context.grid_service == null
        )
        {
            EnemyAiActionHelper.TraceAddBlockReason(trace, "missing_context");
            EnemyAiActionHelper.FinalizeActionTrace(context, trace);
            return null;
        }

        BattleUnitState actor = context.unit_state;
        List<BattleUnitState> targets = _helper.SortTargetUnits(
            context,
            "enemy",
            action.TargetSelector
        );
        BattleGridService grid = context.grid_service;
        BattleState state = context.state;
        int currentHeight =
            grid.GetCellState(state, actor.GetAnchorCoord())?.current_height
            ?? 0;
        int focusTargetCount = Math.Max(targets.Count, 1);
        for (int focusTargetIndex = 0; focusTargetIndex < focusTargetCount; focusTargetIndex++)
        {
            BattleUnitState focusTarget =
                targets.Count > 0 ? targets[focusTargetIndex] : null;
            int currentFocusDistance = focusTarget != null
                ? BattleAiActionEvaluatorUtilities.DistanceFromAnchorToUnit(
                    context,
                    actor,
                    actor.GetAnchorCoord(),
                    focusTarget
                )
                : -1;
            if (positioningMode == PositioningMode.Survival && focusTarget != null)
            {
                int currentSafeDistance = BattleAiActionEvaluatorUtilities.ResolveTargetSafeDistance(
                    context,
                    focusTarget,
                    action.MinimumSafeDistance,
                    action.SafeDistanceMargin,
                    _helper
                );
                if (currentFocusDistance >= currentSafeDistance)
                {
                    EnemyAiActionHelper.TraceAddBlockReason(trace, "already_safe");
                    EnemyAiActionHelper.FinalizeActionTrace(context, trace);
                    return null;
                }
            }

            BattleAiDecision bestDecision = null;
            BattleAiScoreInput bestScoreInput = null;

            bool useFastCandidates = TryCollectFastMoveCandidates(
                action,
                context,
                focusTarget,
                currentHeight,
                positioningMode,
                out List<MoveCandidate> fastCandidates
            );
            IEnumerable<MoveCandidate> candidateSequence = fastCandidates;
            if (!useFastCandidates)
            {
                var candidates = new List<(Vector2I Coord, int Distance, int Safety, int Height)>();
                for (int y = 0; y < state.map_size.Y; y++)
                {
                    for (int x = 0; x < state.map_size.X; x++)
                    {
                        var coord = new Vector2I(x, y);
                        if (
                            !grid.CanPlaceFootprint(
                                state,
                                coord,
                                actor.GetFootprintSize(),
                                actor.unit_id,
                                actor
                            )
                        )
                        {
                            continue;
                        }
                        int height = grid.GetCellState(state, coord)?.current_height ?? 0;
                        if (
                            positioningMode == PositioningMode.HighGround
                            && height <= currentHeight
                        )
                        {
                            continue;
                        }
                        int distance = focusTarget != null
                            ? BattleAiActionEvaluatorUtilities.DistanceFromAnchorToUnit(
                                context,
                                actor,
                                coord,
                                focusTarget
                            )
                            : 0;
                        int safety = BattleAiActionEvaluatorUtilities.ResolveTargetSafeDistance(
                            context,
                            focusTarget,
                            action.MinimumSafeDistance,
                            action.SafeDistanceMargin,
                            _helper
                        );
                        candidates.Add((coord, distance, safety, height));
                    }
                }
                SortFullScanCandidates(action, candidates, positioningMode);
                candidateSequence = candidates.Select(
                    candidate =>
                        new MoveCandidate(
                            candidate.Coord,
                            candidate.Distance,
                            candidate.Safety,
                            candidate.Height,
                            0
                        )
                );
            }

            int evaluationCount = 0;
            foreach (MoveCandidate candidate in candidateSequence)
            {
                if (evaluationCount >= action.CandidateLimit)
                    break;
                evaluationCount++;
                EnemyAiActionHelper.TraceCountIncrement(trace, "evaluation_count");
                if (
                    ShouldSkipCandidateWithoutDistanceProgress(
                        action,
                        positioningMode,
                        currentFocusDistance,
                        candidate.Distance,
                        resolvedMaxDistance
                    )
                )
                {
                    EnemyAiActionHelper.TraceCountIncrement(
                        trace,
                        "no_distance_progress_skip_count"
                    );
                    continue;
                }

                BattleCommand command = EnemyAiActionHelper.BuildMoveCommand(
                    context,
                    candidate.Coord
                );
                BattlePreview preview = BuildFastMovePreview(
                    context,
                    candidate.Coord,
                    candidate.MoveCost
                );
                if (preview?.allowed != true)
                {
                    EnemyAiActionHelper.TraceCountIncrement(trace, "preview_reject_count");
                    continue;
                }

                BattleAiScoreInput scoreInput =
                    BattleAiActionEvaluatorUtilities.BuildActionScoreInput(
                        action,
                        context,
                        "move",
                        action.ActionId.ToString(),
                        command,
                        preview,
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["position_target_unit_id"] =
                                focusTarget?.unit_id ?? new StringName(""),
                            ["position_anchor_coord"] = candidate.Coord,
                            ["desired_min_distance"] = resolvedMinDistance,
                            ["desired_max_distance"] = resolvedMaxDistance,
                            ["position_current_distance"] = candidate.Distance,
                            ["position_safe_distance"] = candidate.Safety,
                            ["position_objective_kind"] = "distance_band_progress",
                            ["high_ground_weight"] = action.HighGroundWeight,
                            ["safety_weight"] = action.SafetyWeight,
                            ["distance_band_weight"] = action.DistanceBandWeight,
                            ["move_cost"] = candidate.MoveCost,
                        }
                    );
                if (
                    positioningMode == PositioningMode.Survival
                    && BattleAiActionEvaluatorUtilities.IsUnthreatenedReposition(
                        scoreInput,
                        action.MinSurvivalMarginGainToEscape
                    )
                )
                {
                    EnemyAiActionHelper.TraceCountIncrement(
                        trace,
                        "no_survival_gain_skip_count"
                    );
                    continue;
                }
                if (trace != null)
                {
                    EnemyAiActionHelper.TraceOfferCandidate(
                        trace,
                        EnemyAiActionHelper.BuildCandidateSummary(
                            $"move_to_{candidate.Coord.X}_{candidate.Coord.Y}",
                            command,
                            scoreInput,
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["coord"] = candidate.Coord,
                                ["dist"] = candidate.Distance,
                                ["height"] = candidate.Height,
                            }
                        )
                    );
                }
                if (!BattleAiDecisionEngine.IsBetterScoreInputTyped(scoreInput, bestScoreInput))
                    continue;
                bestScoreInput = scoreInput;
                bestDecision = EnemyAiActionHelper.CreateScoredDecision(
                    action.ActionId,
                    action.ScoreBucketId,
                    command,
                    scoreInput,
                    $"{actor.display_name} 移动到 ({candidate.Coord.X},{candidate.Coord.Y})（评分 {BattleAiActionEvaluatorUtilities.ScoreTotal(scoreInput)}）。"
                );
            }
            if (bestDecision != null)
            {
                EnemyAiActionHelper.FinalizeActionTrace(context, trace, bestDecision);
                return bestDecision;
            }
        }

        EnemyAiActionHelper.FinalizeActionTrace(context, trace);
        return null;
    }

    private bool TryCollectFastMoveCandidates(
        MoveToAdvantagePositionActionDefinition action,
        BattleAiContext context,
        BattleUnitState focusTarget,
        int currentHeight,
        PositioningMode positioningMode,
        out List<MoveCandidate> result
    )
    {
        result = new List<MoveCandidate>();
        if (
            context?.state == null
            || context.unit_state == null
            || context.grid_service == null
        )
        {
            return false;
        }
        if (
            BattleAiActionEvaluatorUtilities.IsUnitMovementBlocked(
                context,
                context.unit_state
            )
        )
        {
            return true;
        }
        int moveBudget = BattleAiActionEvaluatorUtilities.ResolveCurrentMoveBudget(
            context.unit_state
        );
        if (moveBudget <= 0)
            return true;

        BattleState state = context.state;
        BattleUnitState actor = context.unit_state;
        BattleGridService grid = context.grid_service;
        var frontier = new Queue<(Vector2I Coord, int Cost)>();
        Vector2I actorCoord = actor.GetAnchorCoord();
        var bestCosts = new Dictionary<Vector2I, int> { [actorCoord] = 0 };
        frontier.Enqueue((actorCoord, 0));
        int safety = BattleAiActionEvaluatorUtilities.ResolveTargetSafeDistance(
            context,
            focusTarget,
            action.MinimumSafeDistance,
            action.SafeDistanceMargin,
            _helper
        );
        while (frontier.Count > 0)
        {
            (Vector2I currentCoord, int currentCost) = frontier.Dequeue();
            if (currentCost != bestCosts.GetValueOrDefault(currentCoord, int.MaxValue))
                continue;

            foreach (Vector2I neighbor in grid.GetNeighbors4(state, currentCoord))
            {
                if (!grid.CanUnitStepBetweenAnchors(state, actor, currentCoord, neighbor))
                    continue;
                int nextCost = currentCost + Mathf.Max(context.GetMoveCost(actor, neighbor), 1);
                if (nextCost > moveBudget)
                    continue;
                if (
                    bestCosts.TryGetValue(neighbor, out int existingCost)
                    && nextCost >= existingCost
                )
                {
                    continue;
                }

                bestCosts[neighbor] = nextCost;
                frontier.Enqueue((neighbor, nextCost));
                int height = grid.GetCellState(state, neighbor)?.current_height ?? currentHeight;
                if (
                    positioningMode == PositioningMode.HighGround
                    && height <= currentHeight
                )
                {
                    continue;
                }
                int distance = focusTarget != null
                    ? BattleAiActionEvaluatorUtilities.DistanceFromAnchorToUnit(
                        context,
                        actor,
                        neighbor,
                        focusTarget
                    )
                    : 0;
                result.Add(new MoveCandidate(neighbor, distance, safety, height, nextCost));
            }
        }
        SortFastCandidates(action, result, positioningMode);
        return true;
    }

    private static bool ShouldSkipCandidateWithoutDistanceProgress(
        MoveToAdvantagePositionActionDefinition action,
        PositioningMode positioningMode,
        int currentDistance,
        int candidateDistance,
        int resolvedMaxDistance
    )
    {
        if (
            positioningMode != PositioningMode.HighGround
            || action.MinDistanceProgressWhenBeyondBand <= 0
            || currentDistance < 0
            || candidateDistance < 0
            || resolvedMaxDistance < 0
            || currentDistance <= resolvedMaxDistance
        )
        {
            return false;
        }
        return currentDistance - candidateDistance
            < action.MinDistanceProgressWhenBeyondBand;
    }

    private static void SortFullScanCandidates(
        MoveToAdvantagePositionActionDefinition action,
        List<(Vector2I Coord, int Distance, int Safety, int Height)> candidates,
        PositioningMode positioningMode
    )
    {
        if (positioningMode == PositioningMode.Survival)
        {
            candidates.Sort(
                (left, right) =>
                {
                    int leftSafety = Mathf.Max(left.Safety - left.Distance, 0);
                    int rightSafety = Mathf.Max(right.Safety - right.Distance, 0);
                    return leftSafety != rightSafety
                        ? rightSafety.CompareTo(leftSafety)
                        : left.Distance.CompareTo(right.Distance);
                }
            );
        }
        else if (positioningMode == PositioningMode.HighGround)
        {
            candidates.Sort(
                (left, right) =>
                {
                    if (left.Height != right.Height)
                        return right.Height.CompareTo(left.Height);
                    int leftSafety = Mathf.Max(left.Safety - left.Distance, 0);
                    int rightSafety = Mathf.Max(right.Safety - right.Distance, 0);
                    return leftSafety != rightSafety
                        ? rightSafety.CompareTo(leftSafety)
                        : left.Distance.CompareTo(right.Distance);
                }
            );
        }
        else
        {
            candidates.Sort(
                (left, right) =>
                {
                    int leftDistance = Mathf.Abs(
                        left.Distance - action.DesiredMinDistance
                    );
                    int rightDistance = Mathf.Abs(
                        right.Distance - action.DesiredMinDistance
                    );
                    if (leftDistance != rightDistance)
                        return leftDistance.CompareTo(rightDistance);
                    int leftSafety = Mathf.Max(left.Safety - left.Distance, 0);
                    int rightSafety = Mathf.Max(right.Safety - right.Distance, 0);
                    return leftSafety != rightSafety
                        ? rightSafety.CompareTo(leftSafety)
                        : right.Height.CompareTo(left.Height);
                }
            );
        }
    }

    private static void SortFastCandidates(
        MoveToAdvantagePositionActionDefinition action,
        List<MoveCandidate> candidates,
        PositioningMode positioningMode
    )
    {
        if (positioningMode == PositioningMode.Survival)
        {
            candidates.Sort(
                (left, right) =>
                {
                    int leftSafety = Mathf.Max(left.Safety - left.Distance, 0);
                    int rightSafety = Mathf.Max(right.Safety - right.Distance, 0);
                    return leftSafety != rightSafety
                        ? rightSafety.CompareTo(leftSafety)
                        : left.Distance.CompareTo(right.Distance);
                }
            );
        }
        else if (positioningMode == PositioningMode.HighGround)
        {
            candidates.Sort(
                (left, right) =>
                {
                    if (left.Height != right.Height)
                        return right.Height.CompareTo(left.Height);
                    int leftSafety = Mathf.Max(left.Safety - left.Distance, 0);
                    int rightSafety = Mathf.Max(right.Safety - right.Distance, 0);
                    return leftSafety != rightSafety
                        ? rightSafety.CompareTo(leftSafety)
                        : left.Distance.CompareTo(right.Distance);
                }
            );
        }
        else
        {
            candidates.Sort(
                (left, right) =>
                {
                    int leftDistance = Mathf.Abs(
                        left.Distance - action.DesiredMinDistance
                    );
                    int rightDistance = Mathf.Abs(
                        right.Distance - action.DesiredMinDistance
                    );
                    if (leftDistance != rightDistance)
                        return leftDistance.CompareTo(rightDistance);
                    int leftSafety = Mathf.Max(left.Safety - left.Distance, 0);
                    int rightSafety = Mathf.Max(right.Safety - right.Distance, 0);
                    return leftSafety != rightSafety
                        ? rightSafety.CompareTo(leftSafety)
                        : right.Height.CompareTo(left.Height);
                }
            );
        }
    }

    private static BattlePreview BuildFastMovePreview(
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

    private static AiActionTrace BeginActionTrace(
        MoveToAdvantagePositionActionDefinition action,
        BattleAiContext context,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        Dictionary<string, object> merged;
        if (context != null)
        {
            merged = context.MergeCurrentActionMetadataTyped(metadata);
        }
        else
        {
            merged = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> entry in metadata)
                merged[entry.Key] = entry.Value;
        }
        StringName scoreBucketId = merged.TryGetValue(
            "score_bucket_id",
            out object rawBucket
        )
            ? ProgressionDataUtils.to_string_name(rawBucket)
            : action.ScoreBucketId;
        return EnemyAiActionHelper.BeginActionTrace(
            action.ActionId,
            scoreBucketId,
            context,
            merged
        );
    }

    private static PositioningMode ToPositioningMode(StringName mode)
    {
        if (mode == ModeAdvantage)
            return PositioningMode.Advantage;
        if (mode == ModeSurvival)
            return PositioningMode.Survival;
        if (mode == ModeHighGround)
            return PositioningMode.HighGround;
        return PositioningMode.Unknown;
    }

    private static int ReadMetadataInt(
        IReadOnlyDictionary<string, object> metadata,
        string key,
        int fallback
    )
    {
        if (metadata == null || !metadata.TryGetValue(key, out object value) || value == null)
            return fallback;
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
