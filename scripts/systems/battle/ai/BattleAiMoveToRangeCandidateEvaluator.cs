using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

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

    public GodotObject evaluate_move_to_range_request(
        GodotObject request,
        GodotObject query,
        Callable command_factory,
        Callable decision_factory)
    {
        if (request == null || query == null)
        {
            return Fail("evaluate_move_to_range_request requires request and query.");
        }
        GodotObject movement = query.Call("get_movement_query_service").AsGodotObject();
        if (movement == null)
        {
            return Fail("MoveToRange candidate evaluation requires movement query service.");
        }

        StringName actorId = GetStringName(request, "actor_unit_id");
        StringName focusTargetId = GetStringName(request, "focus_target_unit_id");
        int desiredMinDistance = GetInt(request, "desired_min_distance");
        int desiredMaxDistance = GetInt(request, "desired_max_distance");
        int maxCandidateCount = GetInt(request, "max_candidate_count");
        StringName actionId = GetStringName(request, "action_id");
        StringName actionIntent = GetStringName(request, "action_intent");
        StringName scoreBucketId = GetStringName(request, "score_bucket_id");
        string actionLabel = GetString(request, "action_label");
        if (string.IsNullOrEmpty(actionLabel))
        {
            actionLabel = actionId.ToString();
        }

        GodotObject actorSnapshot = query.Call("get_unit_snapshot", actorId).AsGodotObject();
        GodotObject targetSnapshot = query.Call("get_unit_snapshot", focusTargetId).AsGodotObject();
        if (actorSnapshot == null || targetSnapshot == null)
        {
            return Fail("MoveToRange candidate request references a missing actor or target.");
        }

        Vector2I actorCoord = GetVector2I(actorSnapshot, "coord");
        Vector2I actorFootprint = GetVector2I(actorSnapshot, "footprint_size", Vector2I.One);
        StringName targetUnitId = GetStringName(targetSnapshot, "unit_id");
        string targetDisplayName = GetString(targetSnapshot, "display_name");
        int currentDistance = query.Call("distance_from_anchor_to_target", actorCoord, actorFootprint, targetUnitId).AsInt32();
        if (currentDistance >= desiredMinDistance && currentDistance <= desiredMaxDistance)
        {
            return null;
        }

        GDictionary pathBudget = GetDictionary(request, "path_search_budget");
        GDictionary tacticalParams = GetDictionary(request, "tactical_params");
        GDictionary runtimeMetadata = GetDictionary(request, "runtime_metadata");
        int maxCost = GetInt(pathBudget, "max_cost");
        var options = new GDictionary
        {
            ["path_budget"] = pathBudget.Duplicate(true),
            ["max_candidate_count"] = maxCandidateCount,
            ["prefer_progress"] = GetBool(pathBudget, "prefer_progress", true),
        };

        GDictionary pathCandidateResult = movement is BattleMovementQueryService movementService
            ? movementService.collect_distance_band_path_targets(
                query,
                actorId,
                focusTargetId,
                desiredMinDistance,
                desiredMaxDistance,
                maxCost,
                null,
                options)
            : movement.Call(
                "collect_distance_band_path_targets",
                query,
                actorId,
                focusTargetId,
                desiredMinDistance,
                desiredMaxDistance,
                maxCost,
                Variant.From<GodotObject>(null),
                options).AsGodotDictionary();
        if (!GetBool(pathCandidateResult, "ok"))
        {
            return Fail($"MoveToRange candidate distance-band path query failed: {GetStringName(pathCandidateResult, "reject_reason")}.");
        }

        GArray pathTargetCoords = GetArray(pathCandidateResult, "target_coords");
        GArray pathCosts = GetArray(pathCandidateResult, "costs");
        GArray pathLengths = GetArray(pathCandidateResult, "path_lengths");
        int pathRejectCount = GetInt(pathCandidateResult, "path_reject_count");
        int evaluatedCount = 0;
        int previewRejectCount = 0;
        int bestPathCost = InfiniteTieBreaker;
        int bestPathLength = InfiniteTieBreaker;
        int[] bestFacts = null;
        GodotObject bestDecision = null;
        StringName positionObjectiveKind = GetStringName(tacticalParams, "position_objective_kind", "distance_band_progress");

        for (int index = 0; index < pathTargetCoords.Count; index++)
        {
            if (evaluatedCount >= maxCandidateCount)
            {
                break;
            }

            Vector2I moveTarget = pathTargetCoords[index].AsVector2I();
            if (moveTarget == actorCoord)
            {
                continue;
            }

            int pathCost = index < pathCosts.Count ? pathCosts[index].AsInt32() : 0;
            int pathLength = index < pathLengths.Count ? pathLengths[index].AsInt32() : 0;
            evaluatedCount++;

            GodotObject command = command_factory.Call(actorId, moveTarget).AsGodotObject();
            if (command == null)
            {
                return Fail("MoveToRange command factory returned null.");
            }

            GodotObject preview = query.Call("preview_command", command).AsGodotObject();
            if (preview == null || !GetBool(preview, "allowed"))
            {
                previewRejectCount++;
                continue;
            }

            var scoreMetadata = BuildScoreMetadata(
                actionIntent,
                focusTargetId,
                moveTarget,
                desiredMinDistance,
                desiredMaxDistance,
                positionObjectiveKind,
                runtimeMetadata);
            GodotObject scoreInput = query.Call(
                "build_action_score_input",
                BattleTypedNames.ToStringName(BattleAiActionKind.Move),
                actionLabel,
                scoreBucketId,
                command,
                preview,
                scoreMetadata).AsGodotObject();
            if (scoreInput == null)
            {
                return Fail("MoveToRange candidate score callback returned null.");
            }

            int[] candidateFacts = scoreInput.Call("to_move_to_range_ordering_facts").AsInt32Array();
            if (candidateFacts == null || candidateFacts.Length < RequiredFactCount)
            {
                return Fail("MoveToRange score input returned invalid ordering facts.");
            }
            if (!IsBetterMoveToRangeScore(candidateFacts, bestFacts, pathCost, pathLength, bestPathCost, bestPathLength))
            {
                continue;
            }

            bestFacts = candidateFacts;
            bestPathCost = pathCost;
            bestPathLength = pathLength;
            bestDecision = decision_factory.Call(request, command, scoreInput, targetDisplayName, pathCost).AsGodotObject();
            if (bestDecision == null)
            {
                return Fail("MoveToRange decision factory returned null.");
            }
        }

        if (bestDecision == null)
        {
            return null;
        }
        bestDecision.Set(
            "trace_counters",
            new GDictionary
            {
                ["evaluation_count"] = evaluatedCount,
                ["preview_reject_count"] = previewRejectCount,
                ["path_reject_count"] = pathRejectCount,
                ["chosen_reason"] = new StringName("best_score"),
            });
        return bestDecision;
    }

    private static GDictionary BuildScoreMetadata(
        StringName actionIntent,
        StringName focusTargetUnitId,
        Vector2I positionAnchorCoord,
        int desiredMinDistance,
        int desiredMaxDistance,
        StringName positionObjectiveKind,
        GDictionary runtimeMetadata)
    {
        return new GDictionary
        {
            ["action_intent"] = actionIntent,
            ["focus_target_unit_id"] = focusTargetUnitId,
            ["position_anchor_coord"] = positionAnchorCoord,
            ["desired_min_distance"] = desiredMinDistance,
            ["desired_max_distance"] = desiredMaxDistance,
            ["position_objective_kind"] = positionObjectiveKind,
            ["move_cost"] = 0,
            ["runtime_action_metadata"] = runtimeMetadata.Duplicate(true),
        };
    }

    private static bool IsBetterMoveToRangeScore(
        int[] candidate,
        int[] best,
        int candidatePathCost,
        int candidatePathLength,
        int bestPathCost,
        int bestPathLength)
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
        if (Get(candidate, FactIndex.FriendlyLethalTargetCount) != Get(best, FactIndex.FriendlyLethalTargetCount))
        {
            return Get(candidate, FactIndex.FriendlyLethalTargetCount) < Get(best, FactIndex.FriendlyLethalTargetCount);
        }
        if (Get(candidate, FactIndex.FriendlyFireTargetCount) != Get(best, FactIndex.FriendlyFireTargetCount))
        {
            return Get(candidate, FactIndex.FriendlyFireTargetCount) < Get(best, FactIndex.FriendlyFireTargetCount);
        }
        if (Get(candidate, FactIndex.FriendlyFirePenaltyScore) != Get(best, FactIndex.FriendlyFirePenaltyScore))
        {
            return Get(candidate, FactIndex.FriendlyFirePenaltyScore) < Get(best, FactIndex.FriendlyFirePenaltyScore);
        }

        int survivalRiskComparison = ComparePostActionSurvivalRisk(candidate, best);
        if (survivalRiskComparison != 0)
        {
            return survivalRiskComparison > 0;
        }
        if (Get(candidate, FactIndex.EstimatedLethalThreatTargetCount) != Get(best, FactIndex.EstimatedLethalThreatTargetCount))
        {
            return Get(candidate, FactIndex.EstimatedLethalThreatTargetCount) > Get(best, FactIndex.EstimatedLethalThreatTargetCount);
        }
        if (Get(candidate, FactIndex.EstimatedLethalTargetCount) != Get(best, FactIndex.EstimatedLethalTargetCount))
        {
            return Get(candidate, FactIndex.EstimatedLethalTargetCount) > Get(best, FactIndex.EstimatedLethalTargetCount);
        }

        bool candidateIsEmergencySurvival = Get(candidate, FactIndex.IsEmergencySurvival) != 0;
        bool bestIsEmergencySurvival = Get(best, FactIndex.IsEmergencySurvival) != 0;
        if (candidateIsEmergencySurvival != bestIsEmergencySurvival)
        {
            return candidateIsEmergencySurvival;
        }

        if (Get(candidate, FactIndex.EstimatedLethalTargetCount) > 0 && Get(best, FactIndex.EstimatedLethalTargetCount) > 0)
        {
            if (Get(candidate, FactIndex.TotalScore) != Get(best, FactIndex.TotalScore))
            {
                return Get(candidate, FactIndex.TotalScore) > Get(best, FactIndex.TotalScore);
            }
            if (Get(candidate, FactIndex.HitPayoffScore) != Get(best, FactIndex.HitPayoffScore))
            {
                return Get(candidate, FactIndex.HitPayoffScore) > Get(best, FactIndex.HitPayoffScore);
            }
            if (Get(candidate, FactIndex.EffectiveTargetCount) != Get(best, FactIndex.EffectiveTargetCount))
            {
                return Get(candidate, FactIndex.EffectiveTargetCount) > Get(best, FactIndex.EffectiveTargetCount);
            }
            int lethalNonfatalRiskComparison = CompareNonfatalPostActionSurvivalRisk(candidate, best);
            if (lethalNonfatalRiskComparison != 0)
            {
                return lethalNonfatalRiskComparison > 0;
            }
            if (Get(candidate, FactIndex.ResourceCostScore) != Get(best, FactIndex.ResourceCostScore))
            {
                return Get(candidate, FactIndex.ResourceCostScore) < Get(best, FactIndex.ResourceCostScore);
            }
        }

        if (Get(candidate, FactIndex.ScoreBucketPriority) != Get(best, FactIndex.ScoreBucketPriority))
        {
            return Get(candidate, FactIndex.ScoreBucketPriority) > Get(best, FactIndex.ScoreBucketPriority);
        }
        if (Get(candidate, FactIndex.TotalScore) != Get(best, FactIndex.TotalScore))
        {
            return Get(candidate, FactIndex.TotalScore) > Get(best, FactIndex.TotalScore);
        }
        if (Get(candidate, FactIndex.HitPayoffScore) != Get(best, FactIndex.HitPayoffScore))
        {
            return Get(candidate, FactIndex.HitPayoffScore) > Get(best, FactIndex.HitPayoffScore);
        }
        if (Get(candidate, FactIndex.EffectiveTargetCount) != Get(best, FactIndex.EffectiveTargetCount))
        {
            return Get(candidate, FactIndex.EffectiveTargetCount) > Get(best, FactIndex.EffectiveTargetCount);
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
        if (Get(candidate, FactIndex.PositionObjectiveScore) != Get(best, FactIndex.PositionObjectiveScore))
        {
            return Get(candidate, FactIndex.PositionObjectiveScore) > Get(best, FactIndex.PositionObjectiveScore);
        }
        return Get(candidate, FactIndex.ResourceCostScore) < Get(best, FactIndex.ResourceCostScore);
    }

    private static int ComparePostActionSurvivalRisk(int[] candidate, int[] best)
    {
        if (Get(candidate, FactIndex.HasPostActionThreatProjection) == 0 || Get(best, FactIndex.HasPostActionThreatProjection) == 0)
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
        if (Get(candidate, FactIndex.HasPostActionThreatProjection) == 0 || Get(best, FactIndex.HasPostActionThreatProjection) == 0)
        {
            return 0;
        }
        if (Get(candidate, FactIndex.PostActionIsLethalSurvivalRisk) != 0 || Get(best, FactIndex.PostActionIsLethalSurvivalRisk) != 0)
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

    private static GDictionary GetDictionary(GodotObject source, string property)
    {
        Variant value = source.Get(property);
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    private static GDictionary GetDictionary(GDictionary source, string key)
    {
        Variant value = GetVariant(source, key);
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    private static GArray GetArray(GDictionary source, string key)
    {
        Variant value = GetVariant(source, key);
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static int GetInt(GodotObject source, string property, int defaultValue = 0)
    {
        Variant value = source.Get(property);
        return value.VariantType == Variant.Type.Nil ? defaultValue : value.AsInt32();
    }

    private static int GetInt(GDictionary source, string key, int defaultValue = 0)
    {
        Variant value = GetVariant(source, key);
        return value.VariantType == Variant.Type.Nil ? defaultValue : value.AsInt32();
    }

    private static bool GetBool(GodotObject source, string property, bool defaultValue = false)
    {
        Variant value = source.Get(property);
        return value.VariantType == Variant.Type.Nil ? defaultValue : value.AsBool();
    }

    private static bool GetBool(GDictionary source, string key, bool defaultValue = false)
    {
        Variant value = GetVariant(source, key);
        return value.VariantType == Variant.Type.Nil ? defaultValue : value.AsBool();
    }

    private static string GetString(GodotObject source, string property, string defaultValue = "")
    {
        Variant value = source.Get(property);
        return value.VariantType == Variant.Type.Nil ? defaultValue : value.AsString();
    }

    private static StringName GetStringName(GodotObject source, string property, string defaultValue = "")
    {
        Variant value = source.Get(property);
        return value.VariantType == Variant.Type.Nil ? new StringName(defaultValue) : value.AsStringName();
    }

    private static StringName GetStringName(GDictionary source, string key, string defaultValue = "")
    {
        Variant value = GetVariant(source, key);
        return value.VariantType == Variant.Type.Nil ? new StringName(defaultValue) : value.AsStringName();
    }

    private static Vector2I GetVector2I(GodotObject source, string property, Vector2I defaultValue = default)
    {
        Variant value = source.Get(property);
        return value.VariantType == Variant.Type.Nil ? defaultValue : value.AsVector2I();
    }

    private static Variant GetVariant(GDictionary source, string key)
    {
        return source != null && source.ContainsKey(key) ? source[key] : default;
    }

    private static GodotObject Fail(string message)
    {
        GD.PushError($"BattleAiMoveToRangeCandidateEvaluator: {message}");
        return null;
    }
}
