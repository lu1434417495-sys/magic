using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using System;

[GlobalClass]
public partial class BattleAiMoveToRangeCandidateEvaluator : RefCounted
{
    private const int InfiniteTieBreaker = int.MaxValue;
    private const int RequiredFactCount = 21;

    private enum FactIndex
    {
        FriendlyLethalTargetCount = 0,
        FriendlyFireTargetCount = 1,
        FriendlyFirePenaltyScore = 2,
        HasPostActionThreatProjection = 3,
        PostActionIsLethalSurvivalRisk = 4,
        EstimatedLethalThreatTargetCount = 5,
        EstimatedLethalTargetCount = 6,
        IsEmergencySurvival = 7,
        TotalScore = 8,
        HitPayoffScore = 9,
        EffectiveTargetCount = 10,
        ResourceCostScore = 11,
        ScoreBucketPriority = 12,
        TargetCount = 13,
        PositionObjectiveScore = 14,
        PostActionRemainingThreatCount = 15,
        PostActionRemainingThreatExpectedDamage = 16,
        PostActionSurvivalMargin = 17,
        DistanceToPrimaryCoord = 18,
        DesiredMinDistance = 19,
        DesiredMaxDistance = 20,
    }

    private sealed class GroundAoeSetupMetrics
    {
        public int Score;
        public int EnemyHitCount;
        public int AllyHitCount;
        public StringName SkillId = "";
        public StringName SetupKind = "";
        public Vector2I Center = new(-1, -1);
        public StringName AreaPattern = "";
        public int AreaValue;
        public int CastRange;

        public bool IsUseful(int minimumTargetCount)
        {
            return EnemyHitCount >= Mathf.Max(minimumTargetCount, 1) && AllyHitCount == 0;
        }
    }

    public BattleAiDecision evaluate_move_to_range_request(
        BattleAiCandidateRequest request,
        BattleAiQueryService query,
        System.Func<StringName, Vector2I, BattleCommand> command_factory,
        System.Func<
            BattleAiCandidateRequest,
            BattleCommand,
            BattleAiScoreInput,
            string,
            int,
            BattleAiDecision
        > decision_factory
    )
    {
        if (request == null || query == null)
        {
            return Fail("evaluate_move_to_range_request requires request and query.");
        }
        BattleMovementQueryService movementService = query.get_movement_query_service();
        if (movementService == null)
        {
            return Fail("MoveToRange candidate evaluation requires movement query service.");
        }

        StringName actorId = request.ActorUnitId;
        StringName focusTargetId = request.FocusTargetUnitId;
        int desiredMinDistance = request.DesiredMinDistance;
        int desiredMaxDistance = request.DesiredMaxDistance;
        int maxCandidateCount = request.MaxCandidateCount;
        StringName actionId = request.ActionId;
        StringName actionIntent = request.ActionIntent;
        StringName scoreBucketId = request.ScoreBucketId;
        string actionLabel = request.ActionLabel;
        if (string.IsNullOrEmpty(actionLabel))
        {
            actionLabel = actionId.ToString();
        }

        BattleAiUnitSnapshot actorSnapshot = query.get_unit_snapshot(actorId);
        BattleAiUnitSnapshot targetSnapshot = query.get_unit_snapshot(focusTargetId);
        if (actorSnapshot == null || targetSnapshot == null)
        {
            return Fail("MoveToRange candidate request references a missing actor or target.");
        }

        Vector2I actorCoord = actorSnapshot.coord;
        Vector2I actorFootprint = actorSnapshot.footprint_size;
        StringName targetUnitId = targetSnapshot.unit_id;
        string targetDisplayName = targetSnapshot.display_name;
        if (
            !request.TryGetMoveToRangeSections(
                out MoveToRangePathSearchBudget pathBudget,
                out MoveToRangeTacticalParams tacticalParams,
                out MoveToRangeRuntimeMetadata runtimeMetadata,
                out string requestError
            )
        )
        {
            return Fail(requestError);
        }

        int currentDistance = query.distance_from_anchor_to_target(
            actorCoord,
            actorFootprint,
            targetUnitId
        );
        if (
            currentDistance >= desiredMinDistance
            && currentDistance <= desiredMaxDistance
            && !tacticalParams.AoeSetupEnabled
        )
        {
            return null;
        }

        GroundAoeSetupMetrics currentAoeSetup = BuildBestGroundAoeSetupMetrics(
            query,
            tacticalParams,
            actorSnapshot,
            actorCoord
        );

        if (query.is_unit_movement_blocked(actorId))
        {
            return null;
        }
        if (
            (actorSnapshot.has_taken_action_this_turn || actorSnapshot.has_moved_this_turn)
            && !actorSnapshot.can_use_locked_move_points_this_turn
        )
        {
            return null;
        }

        BattleDistanceBandPathTargetResult pathCandidateResult =
            movementService.CollectDistanceBandPathTargetsTyped(
            actorId,
            focusTargetId,
            desiredMinDistance,
            desiredMaxDistance,
            pathBudget.MaxCost,
            pathBudget.MaxNodes,
            pathBudget.MaxDestinations,
            pathBudget.PathTreeMinDestinationCount,
            pathBudget.IncludeOrigin,
            pathBudget.PreferProgress
        );
        if (pathCandidateResult == null || !pathCandidateResult.Ok)
        {
            return Fail(
                $"MoveToRange candidate distance-band path query failed: {pathCandidateResult?.RejectReason ?? new StringName("unknown")}."
            );
        }

        var pathCandidates = pathCandidateResult.Candidates;
        int pathRejectCount = pathCandidateResult.PathRejectCount;
        int evaluatedCount = 0;
        int previewRejectCount = 0;
        int bestPathCost = InfiniteTieBreaker;
        int bestPathLength = InfiniteTieBreaker;
        int[] bestFacts = null;
        BattleAiDecision bestDecision = null;
        StringName positionObjectiveKind = tacticalParams.PositionObjectiveKind;

        foreach (BattleDistanceBandPathTargetCandidate pathCandidate in pathCandidates)
        {
            if (evaluatedCount >= maxCandidateCount)
            {
                break;
            }

            Vector2I moveTarget = pathCandidate.Coord;
            if (moveTarget == actorCoord)
            {
                continue;
            }

            int pathCost = pathCandidate.PathCost;
            int pathLength = pathCandidate.PathLength;
            evaluatedCount++;

            BattleCommand command = command_factory.Invoke(actorId, moveTarget);
            if (command == null)
            {
                return Fail("MoveToRange command factory returned null.");
            }

            BattlePreview preview = BuildFastMovePreview(actorSnapshot, moveTarget, pathCandidate.SpentCost);

            var scoreMetadata = BuildScoreMetadata(
                actionIntent,
                focusTargetId,
                moveTarget,
                desiredMinDistance,
                desiredMaxDistance,
                positionObjectiveKind,
                preview.move_cost,
                runtimeMetadata
            );
            BattleAiScoreInput scoreInput = query.build_action_score_input(
                BattleTypedNames.ToStringName(BattleAiActionKind.Move),
                actionLabel,
                scoreBucketId,
                command,
                preview,
                scoreMetadata
            );
            if (scoreInput == null)
            {
                return Fail("MoveToRange candidate score callback returned null.");
            }
            ApplyGroundAoeSetupScore(
                scoreInput,
                query,
                tacticalParams,
                actorSnapshot,
                pathCandidate.DestinationCoord != new Vector2I(-1, -1)
                    ? pathCandidate.DestinationCoord
                    : moveTarget,
                moveTarget,
                currentAoeSetup
            );

            int[] candidateFacts = scoreInput.to_move_to_range_ordering_facts();
            if (candidateFacts == null || candidateFacts.Length < RequiredFactCount)
            {
                return Fail("MoveToRange score input returned invalid ordering facts.");
            }
            if (
                !IsBetterMoveToRangeScore(
                    candidateFacts,
                    bestFacts,
                    pathCost,
                    pathLength,
                    bestPathCost,
                    bestPathLength
                )
            )
            {
                continue;
            }

            bestFacts = candidateFacts;
            bestPathCost = pathCost;
            bestPathLength = pathLength;
            bestDecision = decision_factory.Invoke(
                request,
                command,
                scoreInput,
                targetDisplayName,
                pathCost
            );
            if (bestDecision == null)
            {
                return Fail("MoveToRange decision factory returned null.");
            }
        }

        if (bestDecision == null)
        {
            return null;
        }
        bestDecision.trace_counters =
            new GDictionary
            {
                ["evaluation_count"] = evaluatedCount,
                ["preview_reject_count"] = previewRejectCount,
                ["path_reject_count"] = pathRejectCount,
                ["chosen_reason"] = new StringName("best_score"),
            };
        return bestDecision;
    }

    private static void ApplyGroundAoeSetupScore(
        BattleAiScoreInput scoreInput,
        BattleAiQueryService query,
        MoveToRangeTacticalParams tacticalParams,
        BattleAiUnitSnapshot actorSnapshot,
        Vector2I setupAnchorCoord,
        Vector2I moveTargetCoord,
        GroundAoeSetupMetrics currentMetrics
    )
    {
        if (scoreInput == null || query == null || tacticalParams?.AoeSetupEnabled != true)
        {
            return;
        }

        GroundAoeSetupMetrics immediateMetrics = BuildBestGroundAoeSetupMetrics(
            query,
            tacticalParams,
            actorSnapshot,
            moveTargetCoord
        );
        GroundAoeSetupMetrics futureMetrics = setupAnchorCoord != moveTargetCoord
            ? BuildBestGroundAoeSetupMetrics(query, tacticalParams, actorSnapshot, setupAnchorCoord)
            : new GroundAoeSetupMetrics();
        GroundAoeSetupMetrics candidateMetrics = immediateMetrics;
        bool usingFutureSetup = false;
        if (!candidateMetrics.IsUseful(tacticalParams.AoeSetupMinTargetCount))
        {
            candidateMetrics = futureMetrics;
            usingFutureSetup = true;
        }
        else if (
            futureMetrics.IsUseful(tacticalParams.AoeSetupMinTargetCount)
            && futureMetrics.Score > candidateMetrics.Score
        )
        {
            int futureDelta = futureMetrics.Score - candidateMetrics.Score;
            int immediateDelta = candidateMetrics.Score
                - (
                    currentMetrics != null
                    && currentMetrics.IsUseful(tacticalParams.AoeSetupMinTargetCount)
                        ? currentMetrics.Score
                        : 0
                );
            if (futureDelta > Mathf.Max(immediateDelta, 0) * 2)
            {
                candidateMetrics = futureMetrics;
                usingFutureSetup = true;
            }
        }
        if (!candidateMetrics.IsUseful(tacticalParams.AoeSetupMinTargetCount))
        {
            return;
        }

        int currentScore =
            currentMetrics != null && currentMetrics.IsUseful(tacticalParams.AoeSetupMinTargetCount)
                ? currentMetrics.Score
                : 0;
        int improvement = Mathf.Max(candidateMetrics.Score - currentScore, 0);
        if (usingFutureSetup)
        {
            improvement = Mathf.Max(improvement / 2, 0);
        }
        if (improvement <= 0)
        {
            return;
        }

        scoreInput.position_objective_score += improvement;
        scoreInput.hit_payoff_score += improvement;
        scoreInput.total_score += improvement;
        scoreInput.effective_target_count = Mathf.Max(
            scoreInput.effective_target_count,
            candidateMetrics.EnemyHitCount
        );
        scoreInput.target_count = Mathf.Max(scoreInput.target_count, candidateMetrics.EnemyHitCount);
        scoreInput.target_coords.Add(candidateMetrics.Center);
        scoreInput.runtime_action_metadata["aoe_setup_bonus"] = improvement;
        scoreInput.runtime_action_metadata["aoe_setup_enemy_hit_count"] =
            candidateMetrics.EnemyHitCount;
        scoreInput.runtime_action_metadata["aoe_setup_ally_hit_count"] =
            candidateMetrics.AllyHitCount;
        scoreInput.runtime_action_metadata["aoe_setup_skill_id"] = candidateMetrics.SkillId;
        scoreInput.runtime_action_metadata["aoe_setup_kind"] = candidateMetrics.SetupKind;
        scoreInput.runtime_action_metadata["aoe_setup_center"] = candidateMetrics.Center;
        scoreInput.runtime_action_metadata["aoe_setup_anchor"] = setupAnchorCoord;
        scoreInput.runtime_action_metadata["aoe_setup_move_target"] = moveTargetCoord;
        scoreInput.runtime_action_metadata["aoe_setup_area_pattern"] =
            candidateMetrics.AreaPattern;
        scoreInput.runtime_action_metadata["aoe_setup_area_value"] = candidateMetrics.AreaValue;
        scoreInput.runtime_action_metadata["aoe_setup_cast_range"] = candidateMetrics.CastRange;
        scoreInput.runtime_action_metadata["aoe_setup_current_score"] = currentScore;
        scoreInput.runtime_action_metadata["aoe_setup_candidate_score"] =
            candidateMetrics.Score;
        scoreInput.runtime_action_metadata["aoe_setup_future_discounted"] = usingFutureSetup;
    }

    private static GroundAoeSetupMetrics BuildBestGroundAoeSetupMetrics(
        BattleAiQueryService query,
        MoveToRangeTacticalParams tacticalParams,
        BattleAiUnitSnapshot actorSnapshot,
        Vector2I anchorCoord
    )
    {
        var best = new GroundAoeSetupMetrics();
        if (
            query == null
            || tacticalParams?.AoeSetupEnabled != true
            || actorSnapshot == null
            || tacticalParams.RangeSkillIds.Count == 0
        )
        {
            return best;
        }

        Vector2I mapSize = query.get_map_size();
        if (mapSize.X <= 0 || mapSize.Y <= 0)
        {
            return best;
        }

        IReadOnlyList<BattleAiUnitSnapshot> enemies =
            query.GetLivingUnitSnapshotsTyped("enemy");
        if (enemies.Count == 0)
        {
            return best;
        }
        IReadOnlyList<BattleAiUnitSnapshot> allies =
            query.GetLivingUnitSnapshotsTyped("ally");

        foreach (StringName skillId in tacticalParams.RangeSkillIds)
        {
            if (skillId == "")
            {
                continue;
            }
            if (!query.TryGetSkillRecordTyped(skillId, out BattleAiQueryService.SkillRecord skillRecord))
            {
                continue;
            }
            if (!IsGroundAoeSkillRecord(skillRecord))
            {
                if (!IsRandomChainSkillRecord(skillRecord))
                {
                    continue;
                }
                int castRange = Mathf.Max(
                    skillRecord.actor_effective_cast_range > 0
                        ? skillRecord.actor_effective_cast_range
                        : skillRecord.range_value,
                    0
                );
                int enemyHitCount = CountUnitsWithinRange(
                    enemies,
                    anchorCoord,
                    actorSnapshot.footprint_size,
                    castRange
                );
                if (enemyHitCount < Mathf.Max(tacticalParams.AoeSetupMinTargetCount, 1))
                {
                    continue;
                }
                int allyHitCount = CountUnitsWithinRange(
                    allies,
                    anchorCoord,
                    actorSnapshot.footprint_size,
                    castRange
                );
                int score = BuildSetupScore(enemyHitCount, allyHitCount, tacticalParams);
                if (score <= best.Score)
                {
                    continue;
                }
                best = new GroundAoeSetupMetrics
                {
                    Score = score,
                    EnemyHitCount = enemyHitCount,
                    AllyHitCount = allyHitCount,
                    SkillId = skillId,
                    SetupKind = "random_chain",
                    Center = anchorCoord,
                    AreaPattern = "random_chain",
                    AreaValue = castRange,
                    CastRange = castRange,
                };
                continue;
            }

            int groundCastRange = Mathf.Max(
                skillRecord.actor_effective_cast_range > 0
                    ? skillRecord.actor_effective_cast_range
                    : skillRecord.range_value,
                0
            );
            StringName areaPattern = skillRecord.area_pattern;
            int areaValue = Mathf.Max(skillRecord.area_value, 0);
            if (groundCastRange < 0 || areaValue <= 0)
            {
                continue;
            }

            for (int y = 0; y < mapSize.Y; y += 1)
            {
                for (int x = 0; x < mapSize.X; x += 1)
                {
                    Vector2I center = new(x, y);
                    if (
                        DistanceFromAnchorFootprintToCoord(
                            anchorCoord,
                            actorSnapshot.footprint_size,
                            center
                        ) > groundCastRange
                    )
                    {
                        continue;
                    }

                    Vector2I facingDirection = center - anchorCoord;
                    int enemyHitCount = CountUnitsInArea(
                        enemies,
                        center,
                        areaPattern,
                        areaValue,
                        facingDirection,
                        mapSize
                    );
                    if (enemyHitCount < Mathf.Max(tacticalParams.AoeSetupMinTargetCount, 1))
                    {
                        continue;
                    }
                    int allyHitCount = CountUnitsInArea(
                        allies,
                        center,
                        areaPattern,
                        areaValue,
                        facingDirection,
                        mapSize
                    );
                    int score = BuildSetupScore(enemyHitCount, allyHitCount, tacticalParams);
                    if (score <= best.Score)
                    {
                        continue;
                    }
                    best = new GroundAoeSetupMetrics
                    {
                        Score = score,
                        EnemyHitCount = enemyHitCount,
                        AllyHitCount = allyHitCount,
                        SkillId = skillId,
                        SetupKind = "ground_aoe",
                        Center = center,
                        AreaPattern = areaPattern,
                        AreaValue = areaValue,
                        CastRange = groundCastRange,
                    };
                }
            }
        }
        return best;
    }

    private static int BuildSetupScore(
        int enemyHitCount,
        int allyHitCount,
        MoveToRangeTacticalParams tacticalParams
    )
    {
        int score =
            enemyHitCount * Mathf.Max(tacticalParams.AoeSetupTargetCountWeight, 0)
            + Mathf.Max(enemyHitCount - 1, 0)
                * Mathf.Max(tacticalParams.AoeSetupImprovementWeight, 0)
            - allyHitCount * Mathf.Max(tacticalParams.AoeSetupFriendlyFirePenalty, 0);
        if (allyHitCount > 0)
        {
            score -= Mathf.Max(tacticalParams.AoeSetupFriendlyFirePenalty, 0);
        }
        return score;
    }

    private static BattlePreview BuildFastMovePreview(
        BattleAiUnitSnapshot actorSnapshot,
        Vector2I moveTarget,
        int moveCost
    )
    {
        var preview = new BattlePreview
        {
            allowed = actorSnapshot != null && moveTarget != new Vector2I(-1, -1),
            move_cost = Mathf.Max(moveCost, 0),
            resolved_anchor_coord = moveTarget,
        };
        if (!preview.allowed)
        {
            return preview;
        }

        Vector2I footprintSize = NormalizeFootprint(actorSnapshot.footprint_size);
        for (int y = 0; y < footprintSize.Y; y += 1)
        {
            for (int x = 0; x < footprintSize.X; x += 1)
            {
                preview.target_coords.Add(moveTarget + new Vector2I(x, y));
            }
        }
        return preview;
    }

    private static bool IsGroundAoeSkillRecord(BattleAiQueryService.SkillRecord record)
    {
        if (record == null)
        {
            return false;
        }
        return record.target_mode == "ground"
            && record.area_value > 0
            && record.area_pattern != ""
            && record.area_pattern != "single"
            && record.area_pattern != "self";
    }

    private static bool IsRandomChainSkillRecord(BattleAiQueryService.SkillRecord record)
    {
        return record != null
            && record.target_mode == "unit"
            && record.target_selection_mode == "random_chain";
    }

    private static int CountUnitsWithinRange(
        IReadOnlyList<BattleAiUnitSnapshot> units,
        Vector2I anchorCoord,
        Vector2I actorFootprintSize,
        int castRange
    )
    {
        int count = 0;
        foreach (BattleAiUnitSnapshot unit in units ?? System.Array.Empty<BattleAiUnitSnapshot>())
        {
            if (
                unit != null
                && DistanceFromAnchorFootprintToUnit(anchorCoord, actorFootprintSize, unit)
                    <= castRange
            )
            {
                count += 1;
            }
        }
        return count;
    }

    private static int DistanceFromAnchorFootprintToUnit(
        Vector2I anchorCoord,
        Vector2I actorFootprintSize,
        BattleAiUnitSnapshot target
    )
    {
        if (target == null)
        {
            return int.MaxValue;
        }
        int bestDistance = int.MaxValue;
        foreach (Vector2I occupiedCoord in target.occupied_coords)
        {
            bestDistance = Mathf.Min(
                bestDistance,
                DistanceFromAnchorFootprintToCoord(
                    anchorCoord,
                    actorFootprintSize,
                    occupiedCoord
                )
            );
        }
        return bestDistance;
    }

    private static int CountUnitsInArea(
        IReadOnlyList<BattleAiUnitSnapshot> units,
        Vector2I center,
        StringName areaPattern,
        int areaValue,
        Vector2I facingDirection,
        Vector2I mapSize
    )
    {
        int count = 0;
        foreach (BattleAiUnitSnapshot unit in units ?? System.Array.Empty<BattleAiUnitSnapshot>())
        {
            if (unit != null && UnitIntersectsArea(unit, center, areaPattern, areaValue, facingDirection, mapSize))
            {
                count += 1;
            }
        }
        return count;
    }

    private static bool UnitIntersectsArea(
        BattleAiUnitSnapshot unit,
        Vector2I center,
        StringName areaPattern,
        int areaValue,
        Vector2I facingDirection,
        Vector2I mapSize
    )
    {
        if (unit == null)
        {
            return false;
        }
        foreach (Vector2I occupiedCoord in unit.occupied_coords)
        {
            if (CoordInArea(occupiedCoord, center, areaPattern, areaValue, facingDirection, mapSize))
            {
                return true;
            }
        }
        return false;
    }

    private static bool CoordInArea(
        Vector2I coord,
        Vector2I center,
        StringName areaPattern,
        int areaValue,
        Vector2I facingDirection,
        Vector2I mapSize
    )
    {
        if (!IsInside(coord, mapSize))
        {
            return false;
        }
        int radius = Mathf.Max(areaValue, 0);
        if (areaPattern == "" || areaPattern == "single" || areaPattern == "self" || radius <= 0)
        {
            return coord == center;
        }

        int dx = coord.X - center.X;
        int dy = coord.Y - center.Y;
        int absX = Mathf.Abs(dx);
        int absY = Mathf.Abs(dy);
        if (areaPattern == "diamond")
        {
            return absX + absY <= radius;
        }
        if (areaPattern == "square" || areaPattern == "radius")
        {
            return Mathf.Max(absX, absY) <= radius;
        }
        if (areaPattern == "cross")
        {
            return (dx == 0 && absY <= radius) || (dy == 0 && absX <= radius);
        }
        if (areaPattern == "line")
        {
            int axis = ResolveDirectionalLineAxis(center, facingDirection, mapSize);
            return axis == 0
                ? dy == 0 && absX <= radius
                : dx == 0 && absY <= radius;
        }
        if (areaPattern == "cone" || areaPattern == "narrow_cone")
        {
            return CoordInCone(
                coord,
                center,
                radius,
                ResolveAreaDirection(center, facingDirection, mapSize),
                areaPattern == "cone"
            );
        }
        if (areaPattern == "front_arc")
        {
            Vector2I direction = ResolveAreaDirection(center, facingDirection, mapSize);
            return direction.X != 0
                ? dx == 0 && absY <= radius
                : dy == 0 && absX <= radius;
        }
        return coord == center;
    }

    private static bool CoordInCone(
        Vector2I coord,
        Vector2I center,
        int radius,
        Vector2I direction,
        bool wide
    )
    {
        int forward;
        int lateral;
        if (direction == Vector2I.Left)
        {
            forward = center.X - coord.X;
            lateral = Mathf.Abs(coord.Y - center.Y);
        }
        else if (direction == Vector2I.Down)
        {
            forward = coord.Y - center.Y;
            lateral = Mathf.Abs(coord.X - center.X);
        }
        else if (direction == Vector2I.Up)
        {
            forward = center.Y - coord.Y;
            lateral = Mathf.Abs(coord.X - center.X);
        }
        else
        {
            forward = coord.X - center.X;
            lateral = Mathf.Abs(coord.Y - center.Y);
        }

        int minStep = wide ? 0 : 0;
        if (forward < minStep || forward > radius)
        {
            return false;
        }
        if (wide)
        {
            return forward == 0 ? coord == center : lateral <= forward;
        }
        int halfWidth = forward <= Mathf.Min(radius, 1) ? 1 : 0;
        return lateral <= halfWidth;
    }

    private static int ResolveDirectionalLineAxis(
        Vector2I center,
        Vector2I facingDirection,
        Vector2I mapSize
    )
    {
        Vector2I normalized = NormalizeAreaDirection(facingDirection);
        if (normalized != Vector2I.Zero)
        {
            return normalized.X != 0 ? 0 : 1;
        }
        int horizontalSpan = Mathf.Min(center.X, mapSize.X - 1 - center.X);
        int verticalSpan = Mathf.Min(center.Y, mapSize.Y - 1 - center.Y);
        return horizontalSpan >= verticalSpan ? 0 : 1;
    }

    private static Vector2I ResolveAreaDirection(
        Vector2I center,
        Vector2I facingDirection,
        Vector2I mapSize
    )
    {
        Vector2I normalized = NormalizeAreaDirection(facingDirection);
        if (normalized != Vector2I.Zero)
        {
            return normalized;
        }
        int rightSpan = Mathf.Max(mapSize.X - 1 - center.X, 0);
        int leftSpan = Mathf.Max(center.X, 0);
        int downSpan = Mathf.Max(mapSize.Y - 1 - center.Y, 0);
        int upSpan = Mathf.Max(center.Y, 0);
        Vector2I bestDirection = Vector2I.Right;
        int bestSpan = rightSpan;
        if (leftSpan > bestSpan)
        {
            bestDirection = Vector2I.Left;
            bestSpan = leftSpan;
        }
        if (downSpan > bestSpan)
        {
            bestDirection = Vector2I.Down;
            bestSpan = downSpan;
        }
        if (upSpan > bestSpan)
        {
            bestDirection = Vector2I.Up;
        }
        return bestDirection;
    }

    private static Vector2I NormalizeAreaDirection(Vector2I direction)
    {
        if (direction == Vector2I.Zero)
        {
            return Vector2I.Zero;
        }
        int absX = Mathf.Abs(direction.X);
        int absY = Mathf.Abs(direction.Y);
        if (absX >= absY && absX > 0)
        {
            return new Vector2I(direction.X > 0 ? 1 : -1, 0);
        }
        return absY > 0 ? new Vector2I(0, direction.Y > 0 ? 1 : -1) : Vector2I.Zero;
    }

    private static bool IsInside(Vector2I coord, Vector2I mapSize)
    {
        return coord.X >= 0 && coord.Y >= 0 && coord.X < mapSize.X && coord.Y < mapSize.Y;
    }

    private static int DistanceFromAnchorFootprintToCoord(
        Vector2I anchorCoord,
        Vector2I footprintSize,
        Vector2I targetCoord
    )
    {
        Vector2I normalizedSize = NormalizeFootprint(footprintSize);
        int bestDistance = int.MaxValue;
        for (int y = 0; y < normalizedSize.Y; y += 1)
        {
            for (int x = 0; x < normalizedSize.X; x += 1)
            {
                Vector2I sourceCoord = anchorCoord + new Vector2I(x, y);
                bestDistance = Mathf.Min(
                    bestDistance,
                    Mathf.Abs(sourceCoord.X - targetCoord.X)
                        + Mathf.Abs(sourceCoord.Y - targetCoord.Y)
                );
            }
        }
        return bestDistance < int.MaxValue ? bestDistance : -1;
    }

    private static Vector2I NormalizeFootprint(Vector2I footprintSize)
    {
        return new Vector2I(Mathf.Max(footprintSize.X, 1), Mathf.Max(footprintSize.Y, 1));
    }

    private static GDictionary BuildScoreMetadata(
        StringName actionIntent,
        StringName focusTargetUnitId,
        Vector2I positionAnchorCoord,
        int desiredMinDistance,
        int desiredMaxDistance,
        StringName positionObjectiveKind,
        int moveCost,
        MoveToRangeRuntimeMetadata runtimeMetadata
    )
    {
        return new GDictionary
        {
            ["action_intent"] = actionIntent,
            ["focus_target_unit_id"] = focusTargetUnitId,
            ["position_anchor_coord"] = positionAnchorCoord,
            ["desired_min_distance"] = desiredMinDistance,
            ["desired_max_distance"] = desiredMaxDistance,
            ["position_objective_kind"] = positionObjectiveKind,
            ["move_cost"] = moveCost,
            ["runtime_action_metadata"] = runtimeMetadata?.ToDictionary() ?? new GDictionary(),
        };
    }

    private static bool IsBetterMoveToRangeScore(
        int[] candidate,
        int[] best,
        int candidatePathCost,
        int candidatePathLength,
        int bestPathCost,
        int bestPathLength
    )
    {
        if (candidate == null)
        {
            return false;
        }
        if (best == null)
        {
            return true;
        }

        int candidateGap = GetDistanceGap(candidate);
        int bestGap = GetDistanceGap(best);
        if (candidateGap != bestGap)
        {
            if (candidateGap < 0)
            {
                return false;
            }
            if (bestGap < 0)
            {
                return true;
            }
            return candidateGap < bestGap;
        }
        if (IsBetterScore(candidate, best))
        {
            return true;
        }
        if (IsBetterScore(best, candidate))
        {
            return false;
        }
        if (candidatePathCost != bestPathCost)
        {
            return candidatePathCost < bestPathCost;
        }
        return candidatePathLength < bestPathLength;
    }

    private static bool IsBetterScore(int[] candidate, int[] best)
    {
        if (candidate == null)
        {
            return false;
        }
        if (best == null)
        {
            return true;
        }
        if (
            Get(candidate, FactIndex.FriendlyLethalTargetCount)
            != Get(best, FactIndex.FriendlyLethalTargetCount)
        )
        {
            return Get(candidate, FactIndex.FriendlyLethalTargetCount)
                < Get(best, FactIndex.FriendlyLethalTargetCount);
        }
        if (
            Get(candidate, FactIndex.FriendlyFireTargetCount)
            != Get(best, FactIndex.FriendlyFireTargetCount)
        )
        {
            return Get(candidate, FactIndex.FriendlyFireTargetCount)
                < Get(best, FactIndex.FriendlyFireTargetCount);
        }
        if (
            Get(candidate, FactIndex.FriendlyFirePenaltyScore)
            != Get(best, FactIndex.FriendlyFirePenaltyScore)
        )
        {
            return Get(candidate, FactIndex.FriendlyFirePenaltyScore)
                < Get(best, FactIndex.FriendlyFirePenaltyScore);
        }

        int survivalRiskComparison = ComparePostActionSurvivalRisk(candidate, best);
        if (survivalRiskComparison != 0)
        {
            return survivalRiskComparison > 0;
        }
        if (
            Get(candidate, FactIndex.EstimatedLethalThreatTargetCount)
            != Get(best, FactIndex.EstimatedLethalThreatTargetCount)
        )
        {
            return Get(candidate, FactIndex.EstimatedLethalThreatTargetCount)
                > Get(best, FactIndex.EstimatedLethalThreatTargetCount);
        }
        if (
            Get(candidate, FactIndex.EstimatedLethalTargetCount)
            != Get(best, FactIndex.EstimatedLethalTargetCount)
        )
        {
            return Get(candidate, FactIndex.EstimatedLethalTargetCount)
                > Get(best, FactIndex.EstimatedLethalTargetCount);
        }

        bool candidateIsEmergencySurvival = Get(candidate, FactIndex.IsEmergencySurvival) != 0;
        bool bestIsEmergencySurvival = Get(best, FactIndex.IsEmergencySurvival) != 0;
        if (candidateIsEmergencySurvival != bestIsEmergencySurvival)
        {
            return candidateIsEmergencySurvival;
        }

        if (
            Get(candidate, FactIndex.EstimatedLethalTargetCount) > 0
            && Get(best, FactIndex.EstimatedLethalTargetCount) > 0
        )
        {
            if (Get(candidate, FactIndex.TotalScore) != Get(best, FactIndex.TotalScore))
            {
                return Get(candidate, FactIndex.TotalScore) > Get(best, FactIndex.TotalScore);
            }
            if (Get(candidate, FactIndex.HitPayoffScore) != Get(best, FactIndex.HitPayoffScore))
            {
                return Get(candidate, FactIndex.HitPayoffScore)
                    > Get(best, FactIndex.HitPayoffScore);
            }
            if (
                Get(candidate, FactIndex.EffectiveTargetCount)
                != Get(best, FactIndex.EffectiveTargetCount)
            )
            {
                return Get(candidate, FactIndex.EffectiveTargetCount)
                    > Get(best, FactIndex.EffectiveTargetCount);
            }
            int lethalNonfatalRiskComparison = CompareNonfatalPostActionSurvivalRisk(
                candidate,
                best
            );
            if (lethalNonfatalRiskComparison != 0)
            {
                return lethalNonfatalRiskComparison > 0;
            }
            if (
                Get(candidate, FactIndex.ResourceCostScore)
                != Get(best, FactIndex.ResourceCostScore)
            )
            {
                return Get(candidate, FactIndex.ResourceCostScore)
                    < Get(best, FactIndex.ResourceCostScore);
            }
        }

        if (
            Get(candidate, FactIndex.ScoreBucketPriority)
            != Get(best, FactIndex.ScoreBucketPriority)
        )
        {
            return Get(candidate, FactIndex.ScoreBucketPriority)
                > Get(best, FactIndex.ScoreBucketPriority);
        }
        if (Get(candidate, FactIndex.TotalScore) != Get(best, FactIndex.TotalScore))
        {
            return Get(candidate, FactIndex.TotalScore) > Get(best, FactIndex.TotalScore);
        }
        if (Get(candidate, FactIndex.HitPayoffScore) != Get(best, FactIndex.HitPayoffScore))
        {
            return Get(candidate, FactIndex.HitPayoffScore) > Get(best, FactIndex.HitPayoffScore);
        }
        if (
            Get(candidate, FactIndex.EffectiveTargetCount)
            != Get(best, FactIndex.EffectiveTargetCount)
        )
        {
            return Get(candidate, FactIndex.EffectiveTargetCount)
                > Get(best, FactIndex.EffectiveTargetCount);
        }
        if (Get(candidate, FactIndex.TargetCount) != Get(best, FactIndex.TargetCount))
        {
            return Get(candidate, FactIndex.TargetCount) > Get(best, FactIndex.TargetCount);
        }

        int nonfatalRiskComparison = CompareNonfatalPostActionSurvivalRisk(candidate, best);
        if (nonfatalRiskComparison != 0)
        {
            return nonfatalRiskComparison > 0;
        }
        if (
            Get(candidate, FactIndex.PositionObjectiveScore)
            != Get(best, FactIndex.PositionObjectiveScore)
        )
        {
            return Get(candidate, FactIndex.PositionObjectiveScore)
                > Get(best, FactIndex.PositionObjectiveScore);
        }
        return Get(candidate, FactIndex.ResourceCostScore) < Get(best, FactIndex.ResourceCostScore);
    }

    private static int ComparePostActionSurvivalRisk(int[] candidate, int[] best)
    {
        if (
            Get(candidate, FactIndex.HasPostActionThreatProjection) == 0
            || Get(best, FactIndex.HasPostActionThreatProjection) == 0
        )
        {
            return 0;
        }
        bool candidateFatal = Get(candidate, FactIndex.PostActionIsLethalSurvivalRisk) != 0;
        bool bestFatal = Get(best, FactIndex.PostActionIsLethalSurvivalRisk) != 0;
        if (candidateFatal != bestFatal)
        {
            return candidateFatal ? -1 : 1;
        }
        return 0;
    }

    private static int CompareNonfatalPostActionSurvivalRisk(int[] candidate, int[] best)
    {
        if (
            Get(candidate, FactIndex.HasPostActionThreatProjection) == 0
            || Get(best, FactIndex.HasPostActionThreatProjection) == 0
        )
        {
            return 0;
        }
        if (
            Get(candidate, FactIndex.PostActionIsLethalSurvivalRisk) != 0
            || Get(best, FactIndex.PostActionIsLethalSurvivalRisk) != 0
        )
        {
            return 0;
        }

        bool candidateThreatFree = Get(candidate, FactIndex.PostActionRemainingThreatCount) <= 0;
        bool bestThreatFree = Get(best, FactIndex.PostActionRemainingThreatCount) <= 0;
        if (candidateThreatFree != bestThreatFree)
        {
            return candidateThreatFree ? 1 : -1;
        }

        int candidateDamage = Get(candidate, FactIndex.PostActionRemainingThreatExpectedDamage);
        int bestDamage = Get(best, FactIndex.PostActionRemainingThreatExpectedDamage);
        if (candidateDamage != bestDamage)
        {
            return candidateDamage < bestDamage ? 1 : -1;
        }

        int candidateCount = Get(candidate, FactIndex.PostActionRemainingThreatCount);
        int bestCount = Get(best, FactIndex.PostActionRemainingThreatCount);
        if (candidateCount != bestCount)
        {
            return candidateCount < bestCount ? 1 : -1;
        }

        int candidateMargin = Get(candidate, FactIndex.PostActionSurvivalMargin);
        int bestMargin = Get(best, FactIndex.PostActionSurvivalMargin);
        if (candidateMargin != bestMargin)
        {
            return candidateMargin > bestMargin ? 1 : -1;
        }
        return 0;
    }

    private static int GetDistanceGap(int[] facts)
    {
        if (facts == null)
        {
            return -1;
        }
        int distance = Get(facts, FactIndex.DistanceToPrimaryCoord);
        int minDistance = Get(facts, FactIndex.DesiredMinDistance);
        int maxDistance = Get(facts, FactIndex.DesiredMaxDistance);
        if (distance < 0 || minDistance < 0 || maxDistance < minDistance)
        {
            return -1;
        }
        if (distance < minDistance)
        {
            return minDistance - distance;
        }
        if (distance > maxDistance)
        {
            return distance - maxDistance;
        }
        return 0;
    }

    private static int Get(int[] facts, FactIndex index)
    {
        return facts[(int)index];
    }

    private static BattleAiDecision Fail(string message)
    {
        GameLog.Error($"BattleAiMoveToRangeCandidateEvaluator: {message}.", "ai.move_to_range.error", "ai");
        return null;
    }
}
