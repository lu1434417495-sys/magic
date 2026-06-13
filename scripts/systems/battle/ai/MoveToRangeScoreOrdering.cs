internal static class MoveToRangeScoreOrdering
{
    internal const int InfiniteTieBreaker = int.MaxValue;
    internal const int RequiredFactCount = 21;

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

    internal static bool IsBetterCandidate(
        BattleAiScoreInput candidate,
        int candidatePathCost,
        int candidatePathLength,
        BattleAiScoreInput best,
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
        return IsBetterCandidate(
            candidate.ToMoveToRangeOrderingFacts(),
            best.ToMoveToRangeOrderingFacts(),
            candidatePathCost,
            candidatePathLength,
            bestPathCost,
            bestPathLength
        );
    }

    internal static bool IsBetterCandidate(
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
}
