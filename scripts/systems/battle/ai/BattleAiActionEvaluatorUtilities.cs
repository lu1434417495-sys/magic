using System;
using System.Collections.Generic;
using Godot;

internal static class BattleAiActionEvaluatorUtilities
{
    internal static BattlePreview BuildFastMovePreview(
        BattleAiContext context,
        Vector2I targetCoord,
        int moveCost = -1
    )
    {
        var preview = new BattlePreview();
        BattleUnitState actor = context?.unit_state;
        BattleGridService grid = context?.grid_service;
        BattleState state = context?.state;
        if (
            actor == null
            || grid == null
            || state == null
            || targetCoord == new Vector2I(-1, -1)
            || !grid.CanPlaceUnit(state, actor, targetCoord)
        )
        {
            return preview;
        }
        preview.allowed = true;
        preview.resolved_anchor_coord = targetCoord;
        preview.move_cost = moveCost >= 0
            ? moveCost
            : Math.Max(context.GetMoveCost(actor, targetCoord), 0);
        foreach (Vector2I coord in grid.GetUnitTargetCoords(actor, targetCoord))
            preview.AddTargetCoord(coord);
        return preview;
    }

    /// <summary>
    /// Cheapest accumulated move cost to every anchor the actor can reach this turn, origin
    /// excluded. Evaluators that only walk the four adjacent cells miss destinations two or three
    /// steps out, which is where a safe cell usually is.
    /// </summary>
    internal static Dictionary<Vector2I, int> CollectReachableAnchorCosts(
        BattleAiContext context,
        BattleUnitState actor
    )
    {
        var reachable = new Dictionary<Vector2I, int>();
        BattleState state = context?.state;
        BattleGridService grid = context?.grid_service;
        if (state == null || actor == null || grid == null)
            return reachable;
        if (IsUnitMovementBlocked(context, actor))
            return reachable;

        int maxMovePoints = ResolveCurrentMoveBudget(actor);
        if (maxMovePoints <= 0)
            return reachable;

        Vector2I origin = actor.GetAnchorCoord();
        var frontier = new Queue<(Vector2I Coord, int Cost)>();
        var bestCosts = new Dictionary<Vector2I, int> { [origin] = 0 };
        frontier.Enqueue((origin, 0));
        while (frontier.Count > 0)
        {
            (Vector2I currentCoord, int currentCost) = frontier.Dequeue();
            if (
                !bestCosts.TryGetValue(currentCoord, out int bestCurrentCost)
                || currentCost != bestCurrentCost
            )
            {
                continue;
            }
            foreach (Vector2I neighbor in grid.GetNeighbors4(state, currentCoord))
            {
                if (!grid.CanUnitStepBetweenAnchors(state, actor, currentCoord, neighbor))
                    continue;
                int nextCost = currentCost + Math.Max(context.GetMoveCost(actor, neighbor), 1);
                if (nextCost > maxMovePoints)
                    continue;
                if (
                    bestCosts.TryGetValue(neighbor, out int bestNeighborCost)
                    && nextCost >= bestNeighborCost
                )
                {
                    continue;
                }
                bestCosts[neighbor] = nextCost;
                frontier.Enqueue((neighbor, nextCost));
                reachable[neighbor] = nextCost;
            }
        }
        reachable.Remove(origin);
        return reachable;
    }

    /// <summary>
    /// Reachable anchors in a deterministic order: cheapest first, then row-major. Dictionary
    /// enumeration order must never decide which candidate an evaluator scores first.
    /// </summary>
    internal static List<KeyValuePair<Vector2I, int>> SortReachableAnchorCosts(
        Dictionary<Vector2I, int> reachable
    )
    {
        var ordered = new List<KeyValuePair<Vector2I, int>>(
            reachable ?? new Dictionary<Vector2I, int>()
        );
        ordered.Sort(
            (left, right) =>
            {
                if (left.Value != right.Value)
                    return left.Value.CompareTo(right.Value);
                if (left.Key.Y != right.Key.Y)
                    return left.Key.Y.CompareTo(right.Key.Y);
                return left.Key.X.CompareTo(right.Key.X);
            }
        );
        return ordered;
    }

    internal static int ResolveCurrentMoveBudget(BattleUnitState unit) =>
        unit == null || unit.GetCurrentMovePoints() <= 0
            ? 0
            : IsNormalMovementLocked(unit)
                && !unit.CanUseLockedMovePointsThisTurnTyped()
                ? 0
                : Mathf.Max(unit.GetCurrentMovePoints(), 0);

    internal static bool IsNormalMovementLocked(BattleUnitState unit) =>
        unit?.IsNormalMovementLockedThisTurnTyped() ?? false;

    internal static bool IsUnitMovementBlocked(BattleAiContext context, BattleUnitState unit) =>
        unit == null || context?.GetAiQueryService()?.IsUnitMovementBlocked(unit.unit_id) == true;

    internal static int DistanceBetweenUnits(
        BattleAiContext context,
        BattleUnitState first,
        BattleUnitState second
    ) => context?.grid_service != null
        ? context.grid_service.GetDistanceBetweenUnits(first, second)
        : 999999;

    internal static int DistanceFromAnchorToUnit(
        BattleAiContext context,
        BattleUnitState actor,
        Vector2I anchor,
        BattleUnitState target
    )
    {
        if (context?.grid_service == null || actor == null || target == null)
            return 999999;
        BattleGridService grid = context.grid_service;
        int bestDistance = 999999;
        foreach (
            Vector2I sourceCoord in grid.GetFootprintCoords(
                anchor,
                actor.GetFootprintSize()
            )
        )
        foreach (
            Vector2I targetCoord in target.GetOccupiedCoordsReadViewTyped()
        )
            bestDistance = Mathf.Min(bestDistance, grid.GetDistance(sourceCoord, targetCoord));
        return bestDistance;
    }

    internal static int ResolveUnitEffectiveThreatRange(
        BattleAiContext context,
        BattleUnitState target,
        BattleAiTypedActionHelper helper
    )
    {
        if (context == null || target == null || helper == null)
            return -1;
        int bestRange = -1;
        foreach (
            StringName rawSkillId in target.GetKnownActiveSkillsViewTyped()
        )
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (skillId == "")
                continue;
            SkillDefinition skill = helper.GetSkillDefinition(context, skillId);
            if (!BattleAiTypedActionHelper.IsHostileThreatSkill(skill))
                continue;
            bestRange = Mathf.Max(
                bestRange,
                BattleRangeService.GetEffectiveSkillThreatRange(
                    target,
                    skill,
                    context.skill_catalog
                )
            );
        }
        return bestRange < 0 ? BattleRangeService.GetWeaponAttackRange(target) : bestRange;
    }

    internal static int ResolveTargetSafeDistance(
        BattleAiContext context,
        BattleUnitState target,
        int configuredMinimum,
        int margin,
        BattleAiTypedActionHelper helper
    )
    {
        int minimum = Mathf.Max(configuredMinimum, 0);
        int threatRange = ResolveUnitEffectiveThreatRange(context, target, helper);
        return threatRange <= 0
            ? minimum
            : Mathf.Max(minimum, threatRange + Mathf.Max(margin, 0));
    }

    internal static BattleUnitState SelectMostUnsafeTarget(
        BattleAiContext context,
        IEnumerable<BattleUnitState> targets,
        Vector2I anchor,
        int configuredMinimum,
        int margin,
        BattleAiTypedActionHelper helper
    )
    {
        BattleUnitState bestTarget = null;
        int bestUnsafeGap = -1;
        int bestDistance = 999999;
        BattleUnitState actor = context?.unit_state;
        foreach (BattleUnitState target in targets ?? Array.Empty<BattleUnitState>())
        {
            if (target == null)
                continue;
            int distance = DistanceFromAnchorToUnit(context, actor, anchor, target);
            int safeDistance = ResolveTargetSafeDistance(
                context,
                target,
                configuredMinimum,
                margin,
                helper
            );
            int unsafeGap = Mathf.Max(safeDistance - distance, 0);
            if (
                bestTarget == null
                || unsafeGap > bestUnsafeGap
                || (unsafeGap == bestUnsafeGap && distance < bestDistance)
            )
            {
                bestTarget = target;
                bestUnsafeGap = unsafeGap;
                bestDistance = distance;
            }
        }
        return bestTarget;
    }

    internal static BattleAiScoreInput BuildActionScoreInput(
        EnemyAiActionDefinition action,
        BattleAiContext context,
        StringName actionKind,
        string actionLabel,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        if (action == null || context == null)
            return null;
        Dictionary<string, object> scoreMetadata = context.MergeCurrentActionMetadataTyped(
            metadata
        );
        scoreMetadata["score_bucket_id"] = action.ScoreBucketId;
        scoreMetadata["action_intent"] = action.ActionIntent;
        scoreMetadata["action_kind"] = actionKind;
        scoreMetadata["action_label"] = string.IsNullOrEmpty(actionLabel)
            ? action.ActionId.ToString()
            : actionLabel;
        return context.BuildActionScoreInputTyped(
            actionKind,
            scoreMetadata["action_label"].ToString(),
            action.ScoreBucketId,
            command,
            preview,
            scoreMetadata
        );
    }

    internal static BattleAiScoreInput BuildSkillScoreInput(
        EnemyAiActionDefinition action,
        BattleAiContext context,
        SkillDefinition skill,
        BattleCommand command,
        BattlePreview preview,
        IEnumerable<CombatEffectDefinition> effects,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        if (action == null || context == null || skill == null)
            return null;
        Dictionary<string, object> scoreMetadata = context.MergeCurrentActionMetadataTyped(
            metadata
        );
        scoreMetadata["score_bucket_id"] = action.ScoreBucketId;
        scoreMetadata["action_intent"] = action.ActionIntent != ""
            ? action.ActionIntent
            : BattleAiActionIntent.InferForSkill(skill, effects);
        scoreMetadata["action_kind"] = scoreMetadata.TryGetValue(
            "action_kind",
            out object actionKindValue
        )
            ? actionKindValue
            : "skill";
        scoreMetadata["action_label"] = scoreMetadata.TryGetValue(
            "action_label",
            out object actionLabelValue
        )
            ? actionLabelValue
            : !string.IsNullOrEmpty(skill.DisplayName)
                ? skill.DisplayName
                : action.ActionId.ToString();
        return context.BuildSkillScoreInputTyped(
            skill,
            command,
            preview,
            effects,
            scoreMetadata
        );
    }

    internal static Dictionary<string, object> ResolveDesiredDistanceContract(
        BattleAiContext context,
        int configuredMinDistance,
        int configuredMaxDistance,
        IEnumerable<StringName> rangeSkillIds,
        BattleAiTypedActionHelper helper
    )
    {
        int effectiveAttackRange = ResolveEffectiveAttackRange(
            context,
            rangeSkillIds,
            helper
        );
        int resolvedMaxDistance = effectiveAttackRange >= 0
            ? effectiveAttackRange
            : configuredMaxDistance;
        int resolvedMinDistance = configuredMinDistance;
        if (resolvedMaxDistance >= 0 && resolvedMinDistance > resolvedMaxDistance)
            resolvedMinDistance = resolvedMaxDistance;
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = resolvedMinDistance,
            ["desired_max_distance"] = Mathf.Max(resolvedMaxDistance, resolvedMinDistance),
            ["configured_desired_min_distance"] = configuredMinDistance,
            ["configured_desired_max_distance"] = configuredMaxDistance,
            ["effective_attack_range"] = effectiveAttackRange,
        };
    }

    internal static int ResolveEffectiveAttackRange(
        BattleAiContext context,
        IEnumerable<StringName> rangeSkillIds,
        BattleAiTypedActionHelper helper
    )
    {
        if (context?.unit_state == null || helper == null)
            return -1;
        int bestRange = -1;
        foreach (
            BattleAvailableSkillEntry entry in helper.ResolveAvailableSkillEntries(
                context,
                rangeSkillIds
            )
        )
        {
            SkillDefinition skill = helper.GetSkillDefinition(context, entry);
            if (
                skill?.CombatProfile == null
                || BattleSkillCastBlockReasonKinds.IsBlocked(
                    helper.GetSkillCastBlockReason(context, skill)
                )
            )
            {
                continue;
            }
            bestRange = Mathf.Max(
                bestRange,
                BattleRangeService.GetEffectiveSkillDistanceContractRange(
                    context.unit_state,
                    skill,
                    context.skill_catalog
                )
            );
        }
        return bestRange;
    }

    internal static int GetHpBasisPoints(BattleUnitState unit)
    {
        if (unit?.attribute_snapshot == null)
            return 10000;
        int maxHp = Mathf.Max(unit.attribute_snapshot.GetValue("hp_max"), 1);
        int currentHp = Mathf.Clamp(unit.GetCurrentHp(), 0, maxHp);
        return Mathf.Clamp((currentHp * 10000) / maxHp, 0, 10000);
    }

    internal static bool IsUnthreatenedReposition(
        BattleAiScoreInput scoreInput,
        int minimumSurvivalMarginGain
    ) =>
        scoreInput != null
        && scoreInput.has_post_action_threat_projection
        && !scoreInput.pre_action_is_lethal_survival_risk
        && scoreInput.post_action_survival_margin - scoreInput.pre_action_survival_margin
            < minimumSurvivalMarginGain;

    internal static int ScoreTotal(BattleAiScoreInput scoreInput) => scoreInput?.total_score ?? 0;

    internal static int ScoreDistanceToPrimaryCoord(BattleAiScoreInput scoreInput) =>
        scoreInput?.distance_to_primary_coord ?? -1;
}
