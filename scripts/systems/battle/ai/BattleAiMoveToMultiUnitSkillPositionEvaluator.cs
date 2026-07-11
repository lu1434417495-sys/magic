using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiMoveToMultiUnitSkillPositionEvaluator
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName ActionKindMove = "move";
    private static readonly StringName ActionKindMoveToMultiUnitPosition =
        "move_to_multi_unit_skill_position";

    private readonly BattleAiTypedActionHelper _helper = new();

    internal BattleAiDecision Evaluate(
        MoveToMultiUnitSkillPositionActionDefinition action,
        BattleAiContext context
    )
    {
        if (action == null || context == null || !HasExplicitDistanceContract(action))
            return null;

        BattleUnitState actor = context.unit_state;
        if (actor == null)
            return null;

        AiActionTrace actionTrace = BeginActionTrace(
            action,
            context,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action_kind"] = ActionKindMoveToMultiUnitPosition.ToString(),
                ["target_selector"] = action.TargetSelector.ToString(),
                ["distance_reference"] = action.DistanceReference.ToString(),
                ["desired_min_distance"] = action.DesiredMinDistance,
                ["desired_max_distance"] = action.DesiredMaxDistance,
                ["candidate_pool_limit"] = action.CandidatePoolLimit,
                ["candidate_group_limit"] = action.CandidateGroupLimit,
                ["target_count_weight"] = action.TargetCountWeight,
            }
        );

        if (IsUnitMovementBlocked(context, actor))
        {
            TraceAddBlockReason(actionTrace, "movement_blocked");
            FinalizeActionTrace(context, actionTrace);
            return null;
        }

        int moveBudget = ResolveCurrentMoveBudget(actor);
        if (moveBudget <= 0)
        {
            TraceAddBlockReason(
                actionTrace,
                IsNormalMovementLocked(actor) ? "movement_locked" : "no_move_budget"
            );
            FinalizeActionTrace(context, actionTrace);
            return null;
        }

        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        foreach (BattleAvailableSkillEntry skillEntry in _helper.ResolveAvailableSkillEntries(context, action.SkillIds))
        {
            StringName skillId = skillEntry.EntryRef.SkillId;
            TraceCountIncrement(actionTrace, "skill_considered_count", 1);
            SkillDefinition skillDefinition = _helper.GetSkillDefinition(context, skillEntry);
            if (!IsMultiUnitSkill(skillDefinition))
            {
                TraceAddBlockReason(
                    actionTrace,
                    skillDefinition == null ? "missing_skill_definition" : "non_multi_unit_skill"
                );
                continue;
            }

            BattleSkillCastBlockReasonKind blockReason = _helper.GetSkillCastBlockReason(
                context,
                skillDefinition
            );
            if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
            {
                TraceAddBlockReason(
                    actionTrace,
                    BattleSkillCastBlockReasonKinds.ToTraceKey(blockReason)
                );
                continue;
            }

            List<BattleUnitState> sortedTargets = _helper.SortTargetUnits(
                context,
                skillDefinition.CombatProfile.TargetTeamFilter,
                action.TargetSelector
            );
            if (sortedTargets.Count == 0)
            {
                TraceAddBlockReason(actionTrace, "no_valid_targets");
                continue;
            }

            foreach (CombatCastVariantDefinition castVariant in GetMultiUnitCastVariants(
                context,
                skillDefinition,
                skillEntry.SkillLevel
            ))
            {
                if (castVariant != null && IsChargeOption(castVariant))
                    continue;

                List<BattleUnitState> currentGroup = BuildAnchorTargetGroup(
                    context,
                    action,
                    skillEntry.SkillLevel,
                    skillDefinition,
                    sortedTargets,
                    actor.coord
                );
                int currentTargetCount = currentGroup.Count;
                foreach (Vector2I destination in CollectReachableMoveCandidates(context, action))
                {
                    TraceCountIncrement(actionTrace, "evaluation_count", 1);
                    List<BattleUnitState> targetGroup = BuildAnchorTargetGroup(
                        context,
                        action,
                        skillEntry.SkillLevel,
                        skillDefinition,
                        sortedTargets,
                        destination
                    );
                    int targetCount = targetGroup.Count;
                    if (targetCount <= currentTargetCount)
                    {
                        TraceAddBlockReason(actionTrace, "does_not_improve_target_count");
                        continue;
                    }

                    BattleCommand command = EnemyAiActionHelper.BuildMoveCommand(
                        context,
                        destination
                    );
                    BattlePreview preview = BuildFastTypedMovePreview(context, destination);
                    if (preview?.allowed != true)
                    {
                        TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                        continue;
                    }

                    Dictionary<string, object> positionMetadata = BuildPositionMetadata(
                        context,
                        action,
                        targetGroup,
                        skillDefinition
                    );
                    positionMetadata["position_anchor_coord"] = destination;
                    BattleAiScoreInput scoreInput = BuildActionScoreInput(
                        action,
                        context,
                        ActionKindMove,
                        action.ActionId.ToString(),
                        command,
                        preview,
                        positionMetadata
                    );
                    if (scoreInput == null)
                        continue;

                    ApplyTargetGroupScore(action, scoreInput, targetGroup);
                    TraceOfferCandidate(
                        actionTrace,
                        EnemyAiActionHelper.BuildCandidateSummary(
                            $"move_to_multi_{destination.X}_{destination.Y}",
                            command,
                            scoreInput,
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["skill_id"] = skillId.ToString(),
                                ["current_target_count"] = currentTargetCount,
                                ["target_count"] = targetCount,
                            }
                        )
                    );

                    if (!IsBetterRepositionScoreInput(scoreInput, bestScoreInput))
                        continue;

                    bestScoreInput = scoreInput;
                    bestDecision = EnemyAiActionHelper.CreateScoredDecision(
                        action.ActionId,
                        action.ScoreBucketId,
                        command,
                        scoreInput,
                        $"{actor.display_name} 准备移动到更适合 {skillDefinition.DisplayName} 的位置，可覆盖 {targetCount} 个目标（评分 {ScoreTotal(scoreInput)}）。"
                    );
                }
            }
        }

        FinalizeActionTrace(context, actionTrace, bestDecision);
        return bestDecision;
    }

    private List<BattleUnitState> BuildAnchorTargetGroup(
        BattleAiContext context,
        MoveToMultiUnitSkillPositionActionDefinition action,
        int skillLevel,
        SkillDefinition skillDefinition,
        IReadOnlyList<BattleUnitState> sortedTargets,
        Vector2I anchor
    )
    {
        var group = new List<BattleUnitState>();
        if (context?.unit_state == null || skillDefinition?.CombatProfile == null)
            return group;

        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        SkillEffectiveCombatDefinition effectiveDefinition =
            context?.skill_catalog?.GetEffectiveCombatDefinition(
                skillDefinition.SkillId,
                skillLevel
            ) ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        int minCount = Math.Max(combatProfile.MinTargetCount, 1);
        int maxCount = Math.Max(effectiveDefinition.MaxTargetCount, minCount);
        int poolLimit = Math.Max(action.CandidatePoolLimit, 0);
        foreach (BattleUnitState target in sortedTargets ?? Array.Empty<BattleUnitState>())
        {
            if (target == null || group.Count >= maxCount || group.Count >= poolLimit)
                break;
            if (!CanAnchorTargetUnit(context, skillDefinition, anchor, target))
                continue;
            group.Add(target);
        }
        return group.Count >= minCount ? group : new List<BattleUnitState>();
    }

    private static bool CanAnchorTargetUnit(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        Vector2I anchor,
        BattleUnitState target
    )
    {
        if (context?.unit_state == null || context.grid_service == null)
            return false;
        if (target == null || !target.is_alive)
            return false;
        if (
            !MatchesTargetFilter(
                context,
                target,
                skillDefinition?.CombatProfile?.TargetTeamFilter ?? EmptyStringName
            )
        )
        {
            return false;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillRange(
            context.unit_state,
            skillDefinition,
            context.skill_catalog
        );
        return DistanceFromAnchorToUnit(context, context.unit_state, anchor, target)
            <= effectiveRange;
    }

    private static List<Vector2I> CollectReachableMoveCandidates(
        BattleAiContext context,
        MoveToMultiUnitSkillPositionActionDefinition action
    )
    {
        var candidates = new List<Vector2I>();
        if (context?.state == null || context.unit_state == null || context.grid_service == null)
            return candidates;

        BattleUnitState actor = context.unit_state;
        Vector2I origin = actor.coord;
        if (IsUnitMovementBlocked(context, actor))
            return candidates;

        int maxMovePoints = ResolveCurrentMoveBudget(actor);
        if (maxMovePoints <= 0)
            return candidates;

        var seen = new HashSet<Vector2I>();
        var bestCosts = new Dictionary<Vector2I, int> { [origin] = 0 };
        var frontier = new List<(Vector2I Coord, int Cost)> { (origin, 0) };
        BattleGridService grid = context.grid_service;
        BattleState state = context.state;
        while (frontier.Count > 0)
        {
            (Vector2I current, int currentCost) = frontier[0];
            frontier.RemoveAt(0);
            if (!bestCosts.TryGetValue(current, out int bestCost) || bestCost != currentCost)
                continue;

            foreach (Vector2I neighbor in grid.GetNeighbors4(state, current))
            {
                if (!grid.CanUnitStepBetweenAnchors(state, actor, current, neighbor))
                    continue;
                int nextCost = currentCost + context.GetMoveCost(actor, neighbor);
                if (nextCost > maxMovePoints)
                    continue;
                if (bestCosts.TryGetValue(neighbor, out int knownCost) && knownCost <= nextCost)
                    continue;
                bestCosts[neighbor] = nextCost;
                frontier.Add((neighbor, nextCost));
                if (seen.Add(neighbor))
                    candidates.Add(neighbor);
            }
        }

        candidates.Sort(
            (left, right) =>
            {
                int leftDistance = DistanceFromAnchorToNearestTarget(context, action, left);
                int rightDistance = DistanceFromAnchorToNearestTarget(context, action, right);
                if (leftDistance == rightDistance)
                {
                    if (left.Y != right.Y)
                        return left.Y.CompareTo(right.Y);
                    return left.X.CompareTo(right.X);
                }
                return leftDistance.CompareTo(rightDistance);
            }
        );
        return candidates;
    }

    private static int DistanceFromAnchorToNearestTarget(
        BattleAiContext context,
        MoveToMultiUnitSkillPositionActionDefinition action,
        Vector2I anchor
    )
    {
        List<BattleUnitState> targets = new BattleAiTypedActionHelper().SortTargetUnits(
            context,
            "enemy",
            action?.TargetSelector ?? "nearest_enemy"
        );
        if (targets.Count == 0)
            return 999999;
        return DistanceFromAnchorToUnit(context, context?.unit_state, anchor, targets[0]);
    }

    private Dictionary<string, object> BuildPositionMetadata(
        BattleAiContext context,
        MoveToMultiUnitSkillPositionActionDefinition action,
        IReadOnlyList<BattleUnitState> targetGroup,
        SkillDefinition skillDefinition
    )
    {
        Dictionary<string, object> metadata = ResolveDesiredDistanceContract(
            context,
            action,
            skillDefinition
        );
        if (action.DistanceReferenceKind == EnemyAiDistanceReference.TargetUnit)
        {
            BattleUnitState primaryTarget = targetGroup.Count > 0 ? targetGroup[0] : null;
            if (primaryTarget != null)
                metadata["position_target_unit_id"] = primaryTarget.unit_id;
            else
                metadata["position_objective_kind"] = "none";
        }
        else if (action.DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline)
        {
            BattleUnitState frontline = ResolveEnemyFrontlineUnit(context);
            if (frontline != null)
                metadata["position_target_unit_id"] = frontline.unit_id;
            else
                metadata["position_objective_kind"] = "none";
        }
        else
        {
            metadata["position_objective_kind"] = "none";
        }
        return metadata;
    }

    private static Dictionary<string, object> ResolveDesiredDistanceContract(
        BattleAiContext context,
        MoveToMultiUnitSkillPositionActionDefinition action,
        SkillDefinition skillDefinition
    )
    {
        int configuredMinDistance = action?.DesiredMinDistance ?? 0;
        int configuredMaxDistance = action?.DesiredMaxDistance ?? 0;
        int effectiveAttackRange = ResolveEffectiveAttackRange(context, skillDefinition);
        int resolvedMaxDistance =
            effectiveAttackRange >= 0 ? effectiveAttackRange : configuredMaxDistance;
        int resolvedMinDistance = configuredMinDistance;
        if (resolvedMaxDistance >= 0 && resolvedMinDistance > resolvedMaxDistance)
            resolvedMinDistance = resolvedMaxDistance;
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = resolvedMinDistance,
            ["desired_max_distance"] = Math.Max(resolvedMaxDistance, resolvedMinDistance),
            ["configured_desired_min_distance"] = configuredMinDistance,
            ["configured_desired_max_distance"] = configuredMaxDistance,
            ["effective_attack_range"] = effectiveAttackRange,
        };
    }

    private BattleUnitState ResolveEnemyFrontlineUnit(BattleAiContext context)
    {
        List<BattleUnitState> targets = _helper.SortTargetUnits(
            context,
            "enemy",
            "nearest_enemy"
        );
        return targets.Count > 0 ? targets[0] : null;
    }

    private static BattleAiScoreInput BuildActionScoreInput(
        MoveToMultiUnitSkillPositionActionDefinition action,
        BattleAiContext context,
        StringName actionKind,
        string actionLabel,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        if (context == null)
            return null;

        Dictionary<string, object> scoringMetadata = CloneMetadata(metadata);
        scoringMetadata["score_bucket_id"] = action.ScoreBucketId;
        scoringMetadata["action_intent"] = ResolveMetadataActionIntent(
            scoringMetadata,
            ResolveDefaultActionIntent(action, actionKind)
        );
        scoringMetadata = context.MergeCurrentActionMetadataTyped(scoringMetadata);
        StringName resolvedScoreBucketId = ReadMetadataStringName(
            scoringMetadata,
            "score_bucket_id",
            action.ScoreBucketId
        );
        return context.BuildActionScoreInputTyped(
            actionKind,
            actionLabel,
            resolvedScoreBucketId,
            command,
            preview,
            scoringMetadata
        );
    }

    private static BattlePreview BuildFastTypedMovePreview(
        BattleAiContext context,
        Vector2I targetCoord,
        int moveCost = -1
    )
    {
        var preview = new BattlePreview();
        BattleUnitState actor = context?.unit_state;
        BattleGridService grid = context?.grid_service;
        BattleState state = context?.state;
        if (actor == null || grid == null || state == null || targetCoord == new Vector2I(-1, -1))
            return preview;
        if (!grid.CanPlaceUnit(state, actor, targetCoord))
            return preview;

        preview.allowed = true;
        preview.resolved_anchor_coord = targetCoord;
        preview.move_cost =
            moveCost >= 0 ? moveCost : Math.Max(context.GetMoveCost(actor, targetCoord), 0);
        foreach (Vector2I coord in grid.GetUnitTargetCoords(actor, targetCoord))
            preview.AddTargetCoord(coord);
        return preview;
    }

    private static void ApplyTargetGroupScore(
        MoveToMultiUnitSkillPositionActionDefinition action,
        BattleAiScoreInput scoreInput,
        IEnumerable<BattleUnitState> targetGroup
    )
    {
        if (scoreInput == null)
            return;

        var ids = new List<StringName>();
        var coords = new List<Vector2I>();
        foreach (BattleUnitState target in targetGroup ?? Array.Empty<BattleUnitState>())
        {
            if (target == null)
                continue;
            ids.Add(target.unit_id);
            coords.Add(target.coord);
        }
        scoreInput.target_unit_ids = ids;
        scoreInput.target_coords = coords;
        scoreInput.target_count = ids.Count;
        scoreInput.total_score += ids.Count * Math.Max(action.TargetCountWeight, 0);
    }

    private static bool IsBetterRepositionScoreInput(
        BattleAiScoreInput candidate,
        BattleAiScoreInput best
    )
    {
        if (candidate == null)
            return false;
        if (best == null)
            return true;
        if (ScoreTargetCount(candidate) != ScoreTargetCount(best))
            return ScoreTargetCount(candidate) > ScoreTargetCount(best);
        if (ScorePositionObjective(candidate) != ScorePositionObjective(best))
            return ScorePositionObjective(candidate) > ScorePositionObjective(best);
        if (ScoreTotal(candidate) != ScoreTotal(best))
            return ScoreTotal(candidate) > ScoreTotal(best);
        return ScoreResourceCost(candidate) < ScoreResourceCost(best);
    }

    private static bool IsMultiUnitSkill(SkillDefinition skillDefinition) =>
        skillDefinition?.CombatProfile != null
        && skillDefinition.CombatProfile.TargetSelectionModeKind
            == BattleTargetSelectionMode.MultiUnit;

    private static bool HasExplicitDistanceContract(
        MoveToMultiUnitSkillPositionActionDefinition action
    )
    {
        return action.DesiredMinDistance >= 0
            && action.DesiredMaxDistance >= action.DesiredMinDistance
            && (
                action.DistanceReferenceKind == EnemyAiDistanceReference.TargetUnit
                || action.DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline
            );
    }

    private static bool IsChargeOption(CombatCastVariantDefinition castVariant)
    {
        if (castVariant == null)
            return false;
        foreach (
            CombatEffectDefinition effectDefinition in castVariant.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition != null && effectDefinition.EffectKind == BattleEffectKind.Charge)
                return true;
        }
        return false;
    }

    private static List<CombatCastVariantDefinition> GetMultiUnitCastVariants(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        var result = new List<CombatCastVariantDefinition>();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return result;
        if (combatProfile.CastVariants.Count == 0)
        {
            result.Add(null);
            return result;
        }

        SkillEffectiveCombatDefinition effectiveDefinition =
            context?.skill_catalog?.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
            ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        foreach (CombatCastVariantDefinition castVariant in effectiveDefinition.UnlockedCastVariants)
        {
            if (castVariant != null)
                result.Add(castVariant);
        }
        return result;
    }

    private static int ResolveCurrentMoveBudget(BattleUnitState unitState)
    {
        if (unitState == null || unitState.current_move_points <= 0)
            return 0;
        return IsNormalMovementLocked(unitState) && !unitState.can_use_locked_move_points_this_turn
            ? 0
            : Math.Max(unitState.current_move_points, 0);
    }

    private static bool IsNormalMovementLocked(BattleUnitState unitState)
    {
        return unitState != null
            && (unitState.has_taken_action_this_turn || unitState.has_moved_this_turn);
    }

    private static bool IsUnitMovementBlocked(BattleAiContext context, BattleUnitState unitState)
    {
        if (unitState == null)
            return true;
        return context?.ai_query_service?.IsUnitMovementBlocked(unitState.unit_id) == true;
    }

    private static bool MatchesTargetFilter(
        BattleAiContext context,
        BattleUnitState targetUnit,
        StringName targetFilter
    )
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || targetUnit == null)
            return false;
        return BattleTargetTeamRules.IsUnitValidForFilter(
            actor,
            targetUnit,
            targetFilter,
            new BattleTargetTeamRules.TargetFilterOptions(
                MadnessTargetAnyTeam: actor.ai_blackboard?.madness_target_any_team == true
            )
        );
    }

    private static int DistanceFromAnchorToUnit(
        BattleAiContext context,
        BattleUnitState actor,
        Vector2I anchor,
        BattleUnitState target
    )
    {
        if (context?.grid_service == null || actor == null || target == null)
            return 999999;

        BattleGridService grid = context.grid_service;
        actor.RefreshFootprint();
        target.RefreshFootprint();
        int bestDistance = 999999;
        foreach (Vector2I sourceCoord in grid.GetFootprintCoords(anchor, actor.footprint_size))
        foreach (Vector2I targetCoord in target.occupied_coords)
            bestDistance = Math.Min(bestDistance, grid.GetDistance(sourceCoord, targetCoord));
        return bestDistance;
    }

    private static int ResolveEffectiveAttackRange(
        BattleAiContext context,
        SkillDefinition skillDefinition
    )
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || skillDefinition == null)
            return -1;
        return BattleRangeService.GetEffectiveSkillDistanceContractRange(
            actor,
            skillDefinition,
            context.skill_catalog
        );
    }

    private static int GetSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
            return 0;
        int knownSkillLevel = unitState.GetKnownSkillLevelTyped(skillId);
        return knownSkillLevel > 0
            ? knownSkillLevel
            : unitState.known_active_skill_ids.Contains(skillId)
                ? 1
                : 0;
    }

    private static StringName ResolveDefaultActionIntent(
        MoveToMultiUnitSkillPositionActionDefinition action,
        StringName actionKind
    )
    {
        if (
            BattleAiActionIntent.IsValid(action.ActionIntent)
            && action.ActionIntent != BattleAiActionIntent.Positioning
        )
        {
            return action.ActionIntent;
        }
        StringName defaultIntent = BattleAiActionIntent.DefaultForActionKind(actionKind);
        return defaultIntent != "" ? defaultIntent : action.ActionIntent;
    }

    private static StringName ResolveMetadataActionIntent(
        IReadOnlyDictionary<string, object> metadata,
        StringName fallback
    )
    {
        StringName metadataIntent = ReadMetadataStringName(metadata, "action_intent", "");
        if (BattleAiActionIntent.IsValid(metadataIntent))
            return metadataIntent;
        return BattleAiActionIntent.IsValid(fallback) ? fallback : "";
    }

    private static AiActionTrace BeginActionTrace(
        MoveToMultiUnitSkillPositionActionDefinition action,
        BattleAiContext context,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        Dictionary<string, object> traceMetadata =
            context != null ? context.MergeCurrentActionMetadataTyped(metadata) : CloneMetadata(metadata);
        StringName scoreBucketId = ReadMetadataStringName(
            traceMetadata,
            "score_bucket_id",
            action?.ScoreBucketId ?? EmptyStringName
        );
        return EnemyAiActionHelper.BeginActionTrace(
            action?.ActionId ?? EmptyStringName,
            scoreBucketId,
            context,
            traceMetadata
        );
    }

    private static void TraceCountIncrement(
        AiActionTrace actionTrace,
        string key,
        int amount = 1
    ) => EnemyAiActionHelper.TraceCountIncrement(actionTrace, key, amount);

    private static void TraceAddBlockReason(AiActionTrace actionTrace, string reasonKey) =>
        EnemyAiActionHelper.TraceAddBlockReason(actionTrace, reasonKey);

    private static void TraceOfferCandidate(
        AiActionTrace actionTrace,
        AiCandidateSummary candidateSummary
    ) => EnemyAiActionHelper.TraceOfferCandidate(actionTrace, candidateSummary, 5);

    private static StringName FinalizeActionTrace(
        BattleAiContext context,
        AiActionTrace actionTrace,
        BattleAiDecision bestDecision = null
    ) => EnemyAiActionHelper.FinalizeActionTrace(context, actionTrace, bestDecision);

    private static Dictionary<string, object> CloneMetadata(
        IReadOnlyDictionary<string, object> source
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source == null)
            return result;
        foreach (KeyValuePair<string, object> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                result[entry.Key] = entry.Value;
        }
        return result;
    }

    private static StringName ReadMetadataStringName(
        IReadOnlyDictionary<string, object> source,
        string key,
        StringName fallback = default
    )
    {
        if (
            source == null
            || string.IsNullOrEmpty(key)
            || !source.TryGetValue(key, out object value)
            || value == null
        )
        {
            return fallback;
        }
        return value switch
        {
            StringName stringName => stringName,
            string text when !string.IsNullOrEmpty(text) => new StringName(text),
            Variant variant when variant.VariantType == Variant.Type.StringName =>
                variant.AsStringName(),
            Variant variant when variant.VariantType == Variant.Type.String =>
                new StringName(variant.AsString()),
            _ => fallback,
        };
    }

    private static int ScoreTotal(BattleAiScoreInput scoreInput) => scoreInput?.total_score ?? 0;

    private static int ScoreTargetCount(BattleAiScoreInput scoreInput) =>
        scoreInput?.target_count ?? 0;

    private static int ScorePositionObjective(BattleAiScoreInput scoreInput) =>
        scoreInput?.position_objective_score ?? 0;

    private static int ScoreResourceCost(BattleAiScoreInput scoreInput) =>
        scoreInput?.resource_cost_score ?? 0;
}
