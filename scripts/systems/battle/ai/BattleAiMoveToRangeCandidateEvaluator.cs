using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using System;

internal sealed class BattleAiMoveToRangeCandidateEvaluator
{
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

    private readonly record struct GroundAoeSetupCacheKey(
        Vector2I AnchorCoord,
        Vector2I ActorFootprintSize
    );

    private sealed class UnitAreaIndex
    {
        private readonly Dictionary<Vector2I, ulong[]> _unitMaskByCoord = new();
        private readonly Vector2I _mapSize;
        private readonly int _laneCount;

        private UnitAreaIndex(Vector2I mapSize, int laneCount)
        {
            _mapSize = mapSize;
            _laneCount = laneCount;
        }

        public static UnitAreaIndex From(
            IReadOnlyList<BattleAiUnitSnapshot> units,
            Vector2I mapSize
        )
        {
            int nonNullUnitCount = 0;
            foreach (BattleAiUnitSnapshot unit in units ?? System.Array.Empty<BattleAiUnitSnapshot>())
            {
                if (unit != null)
                {
                    nonNullUnitCount += 1;
                }
            }
            int laneCount = (nonNullUnitCount + 63) / 64;
            var index = new UnitAreaIndex(mapSize, laneCount);
            int unitIndex = 0;
            foreach (BattleAiUnitSnapshot unit in units ?? System.Array.Empty<BattleAiUnitSnapshot>())
            {
                if (unit == null)
                {
                    continue;
                }
                int laneIndex = unitIndex / 64;
                ulong unitMask = 1UL << (unitIndex % 64);
                foreach (Vector2I occupiedCoord in unit.occupied_coords)
                {
                    if (!IsInside(occupiedCoord, mapSize))
                    {
                        continue;
                    }
                    if (!index._unitMaskByCoord.TryGetValue(occupiedCoord, out ulong[] coordMask))
                    {
                        coordMask = new ulong[laneCount];
                        index._unitMaskByCoord[occupiedCoord] = coordMask;
                    }
                    coordMask[laneIndex] |= unitMask;
                }
                unitIndex += 1;
            }
            return index;
        }

        public int CountUnitsInArea(
            Vector2I center,
            StringName areaPattern,
            int areaValue,
            Vector2I facingDirection
        )
        {
            if (_unitMaskByCoord.Count == 0)
            {
                return 0;
            }
            int radius = Mathf.Max(areaValue, 0);
            BattleAreaPattern patternKind = BattleTypedNames.ToAreaPattern(areaPattern);
            if (
                patternKind == BattleAreaPattern.Unknown
                || patternKind == BattleAreaPattern.Single
                || patternKind == BattleAreaPattern.Self
                || radius <= 0
            )
            {
                return _unitMaskByCoord.TryGetValue(center, out ulong[] singleMask)
                    ? PopCount(singleMask)
                    : 0;
            }

            ulong[] mask = new ulong[_laneCount];
            int minY = Mathf.Max(center.Y - radius, 0);
            int maxY = Mathf.Min(center.Y + radius, _mapSize.Y - 1);
            int minX = Mathf.Max(center.X - radius, 0);
            int maxX = Mathf.Min(center.X + radius, _mapSize.X - 1);
            for (int y = minY; y <= maxY; y += 1)
            {
                for (int x = minX; x <= maxX; x += 1)
                {
                    Vector2I coord = new(x, y);
                    if (!CoordInArea(coord, center, areaPattern, radius, facingDirection, _mapSize))
                    {
                        continue;
                    }
                    if (_unitMaskByCoord.TryGetValue(coord, out ulong[] coordMask))
                    {
                        for (int laneIndex = 0; laneIndex < _laneCount; laneIndex += 1)
                        {
                            mask[laneIndex] |= coordMask[laneIndex];
                        }
                    }
                }
            }
            return PopCount(mask);
        }

        private static int PopCount(ulong[] values)
        {
            int count = 0;
            foreach (ulong value in values ?? System.Array.Empty<ulong>())
            {
                count += PopCount(value);
            }
            return count;
        }

        private static int PopCount(ulong value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count += 1;
            }
            return count;
        }
    }

    public BattleAiDecision EvaluateMoveToRangeRequest(
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
            return Fail("EvaluateMoveToRangeRequest requires request and query.");
        }
        BattleMovementQueryService movementService = query.GetMovementQueryService();
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

        BattleAiUnitSnapshot actorSnapshot = query.GetUnitSnapshot(actorId);
        BattleAiUnitSnapshot targetSnapshot = query.GetUnitSnapshot(focusTargetId);
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
        var aoeSetupCache = new Dictionary<GroundAoeSetupCacheKey, GroundAoeSetupMetrics>();

        int currentDistance = query.DistanceFromAnchorToTarget(
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

        GroundAoeSetupMetrics currentAoeSetup = GetBestGroundAoeSetupMetrics(
            query,
            tacticalParams,
            actorSnapshot,
            actorCoord,
            aoeSetupCache
        );

        if (query.IsUnitMovementBlocked(actorId))
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
        int evaluatedCount = 0;
        int bestPathCost = MoveToRangeScoreOrdering.InfiniteTieBreaker;
        int bestPathLength = MoveToRangeScoreOrdering.InfiniteTieBreaker;
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
            BattleAiScoreInput scoreInput = query.BuildActionScoreInput(
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
                currentAoeSetup,
                aoeSetupCache
            );

            int[] candidateFacts = scoreInput.ToMoveToRangeOrderingFacts();
            if (candidateFacts == null || candidateFacts.Length < MoveToRangeScoreOrdering.RequiredFactCount)
            {
                return Fail("MoveToRange score input returned invalid ordering facts.");
            }
            if (
                !MoveToRangeScoreOrdering.IsBetterCandidate(
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
        return bestDecision;
    }

    private static void ApplyGroundAoeSetupScore(
        BattleAiScoreInput scoreInput,
        BattleAiQueryService query,
        MoveToRangeTacticalParams tacticalParams,
        BattleAiUnitSnapshot actorSnapshot,
        Vector2I setupAnchorCoord,
        Vector2I moveTargetCoord,
        GroundAoeSetupMetrics currentMetrics,
        Dictionary<GroundAoeSetupCacheKey, GroundAoeSetupMetrics> aoeSetupCache
    )
    {
        if (scoreInput == null || query == null || tacticalParams?.AoeSetupEnabled != true)
        {
            return;
        }

        GroundAoeSetupMetrics immediateMetrics = GetBestGroundAoeSetupMetrics(
            query,
            tacticalParams,
            actorSnapshot,
            moveTargetCoord,
            aoeSetupCache
        );
        GroundAoeSetupMetrics futureMetrics = setupAnchorCoord != moveTargetCoord
            ? GetBestGroundAoeSetupMetrics(
                query,
                tacticalParams,
                actorSnapshot,
                setupAnchorCoord,
                aoeSetupCache
            )
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
        BattleAiScoreRuntimeMetadata runtimeMetadata =
            scoreInput.runtime_action_metadata ?? new BattleAiScoreRuntimeMetadata();
        runtimeMetadata.aoe_setup_bonus = improvement;
        runtimeMetadata.aoe_setup_enemy_hit_count = candidateMetrics.EnemyHitCount;
        runtimeMetadata.aoe_setup_ally_hit_count = candidateMetrics.AllyHitCount;
        runtimeMetadata.aoe_setup_skill_id = candidateMetrics.SkillId;
        runtimeMetadata.aoe_setup_kind = candidateMetrics.SetupKind;
        runtimeMetadata.aoe_setup_center = candidateMetrics.Center;
        runtimeMetadata.aoe_setup_anchor = setupAnchorCoord;
        runtimeMetadata.aoe_setup_move_target = moveTargetCoord;
        runtimeMetadata.aoe_setup_area_pattern = candidateMetrics.AreaPattern;
        runtimeMetadata.aoe_setup_area_value = candidateMetrics.AreaValue;
        runtimeMetadata.aoe_setup_cast_range = candidateMetrics.CastRange;
        runtimeMetadata.aoe_setup_current_score = currentScore;
        runtimeMetadata.aoe_setup_candidate_score = candidateMetrics.Score;
        runtimeMetadata.HasAoeSetupFutureDiscounted = true;
        runtimeMetadata.aoe_setup_future_discounted = usingFutureSetup;
        scoreInput.runtime_action_metadata = runtimeMetadata;
    }

    private static GroundAoeSetupMetrics GetBestGroundAoeSetupMetrics(
        BattleAiQueryService query,
        MoveToRangeTacticalParams tacticalParams,
        BattleAiUnitSnapshot actorSnapshot,
        Vector2I anchorCoord,
        Dictionary<GroundAoeSetupCacheKey, GroundAoeSetupMetrics> aoeSetupCache
    )
    {
        if (actorSnapshot == null)
        {
            return new GroundAoeSetupMetrics();
        }
        aoeSetupCache ??= new Dictionary<GroundAoeSetupCacheKey, GroundAoeSetupMetrics>();
        var key = new GroundAoeSetupCacheKey(
            anchorCoord,
            NormalizeFootprint(actorSnapshot.footprint_size)
        );
        if (aoeSetupCache.TryGetValue(key, out GroundAoeSetupMetrics cached))
        {
            return cached;
        }
        GroundAoeSetupMetrics metrics = BuildBestGroundAoeSetupMetrics(
            query,
            tacticalParams,
            actorSnapshot,
            anchorCoord
        );
        aoeSetupCache[key] = metrics;
        return metrics;
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

        Vector2I mapSize = query.GetMapSize();
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
        UnitAreaIndex enemyAreaIndex = UnitAreaIndex.From(enemies, mapSize);
        UnitAreaIndex allyAreaIndex = UnitAreaIndex.From(allies, mapSize);

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
            StringName areaPattern = BattleTypedNames.ToStringName(skillRecord.area_pattern);
            int areaValue = Mathf.Max(skillRecord.area_value, 0);
            if (groundCastRange < 0 || areaValue <= 0)
            {
                continue;
            }

            // Only ground cells within the skill cast range can host a usable center, so
            // restrict the scan to the cast-range bounding box instead of the whole map.
            // Cells outside this box always exceed the cast range and were already skipped
            // by the per-cell range check below, so the considered center set is unchanged.
            var scanBounds = ComputeGroundAoeScanBounds(
                anchorCoord,
                actorSnapshot.footprint_size,
                groundCastRange,
                mapSize
            );

            for (int y = scanBounds.MinY; y <= scanBounds.MaxY; y += 1)
            {
                for (int x = scanBounds.MinX; x <= scanBounds.MaxX; x += 1)
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
                    int enemyHitCount = enemyAreaIndex.CountUnitsInArea(
                        center,
                        areaPattern,
                        areaValue,
                        facingDirection
                    );
                    if (enemyHitCount < Mathf.Max(tacticalParams.AoeSetupMinTargetCount, 1))
                    {
                        continue;
                    }
                    int allyHitCount = allyAreaIndex.CountUnitsInArea(
                        center,
                        areaPattern,
                        areaValue,
                        facingDirection
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
                preview.AddTargetCoord(moveTarget + new Vector2I(x, y));
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
        return record.target_mode == BattleTargetMode.Ground
            && record.area_value > 0
            && record.area_pattern != BattleAreaPattern.Unknown
            && record.area_pattern != BattleAreaPattern.Single
            && record.area_pattern != BattleAreaPattern.Self;
    }

    private static bool IsRandomChainSkillRecord(BattleAiQueryService.SkillRecord record)
    {
        return record != null
            && record.target_mode == BattleTargetMode.Unit
            && record.target_selection_mode == BattleTargetSelectionMode.RandomChain;
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
        BattleAreaPattern patternKind = BattleTypedNames.ToAreaPattern(areaPattern);
        if (
            patternKind == BattleAreaPattern.Unknown
            || patternKind == BattleAreaPattern.Single
            || patternKind == BattleAreaPattern.Self
            || radius <= 0
        )
        {
            return coord == center;
        }

        int dx = coord.X - center.X;
        int dy = coord.Y - center.Y;
        int absX = Mathf.Abs(dx);
        int absY = Mathf.Abs(dy);
        if (patternKind == BattleAreaPattern.Diamond)
        {
            return absX + absY <= radius;
        }
        if (
            patternKind == BattleAreaPattern.Square
            || patternKind == BattleAreaPattern.Radius
        )
        {
            return Mathf.Max(absX, absY) <= radius;
        }
        if (patternKind == BattleAreaPattern.Cross)
        {
            return (dx == 0 && absY <= radius) || (dy == 0 && absX <= radius);
        }
        if (patternKind == BattleAreaPattern.Line)
        {
            int axis = ResolveDirectionalLineAxis(center, facingDirection, mapSize);
            return axis == 0
                ? dy == 0 && absX <= radius
                : dx == 0 && absY <= radius;
        }
        if (
            patternKind == BattleAreaPattern.Cone
            || patternKind == BattleAreaPattern.NarrowCone
        )
        {
            return CoordInCone(
                coord,
                center,
                radius,
                ResolveAreaDirection(center, facingDirection, mapSize),
                patternKind == BattleAreaPattern.Cone
            );
        }
        if (patternKind == BattleAreaPattern.FrontArc)
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

    // Returns the inclusive [minX, maxX, minY, maxY] map-clamped bounding box of cells whose
    // Manhattan distance to the anchor footprint can be within castRange. Any cell outside this
    // box is strictly farther than castRange from every footprint cell, so it can never pass the
    // per-cell range check; the box is therefore a safe superset of the scanned center set.
    private static (int MinX, int MaxX, int MinY, int MaxY) ComputeGroundAoeScanBounds(
        Vector2I anchorCoord,
        Vector2I footprintSize,
        int castRange,
        Vector2I mapSize
    )
    {
        Vector2I normalizedSize = NormalizeFootprint(footprintSize);
        int range = Mathf.Max(castRange, 0);
        int minX = Mathf.Max(anchorCoord.X - range, 0);
        int maxX = Mathf.Min(anchorCoord.X + normalizedSize.X - 1 + range, mapSize.X - 1);
        int minY = Mathf.Max(anchorCoord.Y - range, 0);
        int maxY = Mathf.Min(anchorCoord.Y + normalizedSize.Y - 1 + range, mapSize.Y - 1);
        return (minX, maxX, minY, maxY);
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

    private static Dictionary<string, object> BuildScoreMetadata(
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
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["action_intent"] = actionIntent,
            ["focus_target_unit_id"] = focusTargetUnitId,
            ["position_anchor_coord"] = positionAnchorCoord,
            ["desired_min_distance"] = desiredMinDistance,
            ["desired_max_distance"] = desiredMaxDistance,
            ["position_objective_kind"] = positionObjectiveKind,
            ["move_cost"] = moveCost,
            ["runtime_action_metadata"] = BuildRuntimeMetadataDictionary(runtimeMetadata),
        };
    }

    private static Dictionary<string, object> BuildRuntimeMetadataDictionary(
        MoveToRangeRuntimeMetadata runtimeMetadata
    )
    {
        if (runtimeMetadata == null)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["configured_desired_min_distance"] = runtimeMetadata.ConfiguredDesiredMinDistance,
            ["configured_desired_max_distance"] = runtimeMetadata.ConfiguredDesiredMaxDistance,
            ["effective_attack_range"] = runtimeMetadata.EffectiveAttackRange,
        };
    }

    private static BattleAiDecision Fail(string message)
    {
        GameLog.Error($"BattleAiMoveToRangeCandidateEvaluator: {message}.", "ai.move_to_range.error", "ai");
        return null;
    }
}
