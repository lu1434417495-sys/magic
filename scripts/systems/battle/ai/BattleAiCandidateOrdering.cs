using Godot;

/// <summary>
/// The candidate preference order the AI decision engine actually applies. Kept in one place so
/// diagnostics (trace top candidates) rank candidates the same way the engine picks them; ranking
/// by <c>total_score</c> alone hides friendly-fire and survival tie-breaks that decide the winner.
/// </summary>
internal sealed class BattleAiCandidateFacts
{
    public StringName ScoreBucketId = "";
    public int EstimatedFriendlyLethalTargetCount;
    public int EstimatedFriendlyFireTargetCount;
    public int FriendlyFirePenaltyScore;
    public int EstimatedLethalThreatTargetCount;
    public int EstimatedLethalTargetCount;
    public int TotalScore;
    public int HitPayoffScore;
    public int EffectiveTargetCount;
    public int WastedTargetSlotCount;
    public int ResourceCostScore;
    public int ScoreBucketPriority;
    public int TargetCount;
    public int PositionObjectiveScore;
    public int EnemyTargetCount;
    public int AllyTargetCount;
    public int EstimatedDamage;
    public int EstimatedControlCount;
    public int PositionCurrentDistance;
    public int PositionSafeDistance;
    public int DistanceToPrimaryCoord;
    public bool HasPostActionThreatProjection;
    public bool PreActionIsLethalSurvivalRisk;
    public bool PostActionIsLethalSurvivalRisk;
    public int PreActionThreatExpectedDamage;
    public int PostActionRemainingThreatExpectedDamage;
    public int PostActionSurvivalMargin;
    public int PostActionRemainingThreatCount;
}

internal static class BattleAiCandidateOrdering
{
    private static readonly StringName ArcherSurvivalBucketId = "archer_survival";

    internal static BattleAiCandidateFacts FromScoreInput(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
        {
            return null;
        }
        return new BattleAiCandidateFacts
        {
            ScoreBucketId = scoreInput.score_bucket_id,
            EstimatedFriendlyLethalTargetCount = scoreInput.estimated_friendly_lethal_target_count,
            EstimatedFriendlyFireTargetCount = scoreInput.estimated_friendly_fire_target_count,
            FriendlyFirePenaltyScore = scoreInput.friendly_fire_penalty_score,
            EstimatedLethalThreatTargetCount = scoreInput.estimated_lethal_threat_target_count,
            EstimatedLethalTargetCount = scoreInput.estimated_lethal_target_count,
            TotalScore = scoreInput.total_score,
            HitPayoffScore = scoreInput.hit_payoff_score,
            EffectiveTargetCount = scoreInput.effective_target_count,
            WastedTargetSlotCount = scoreInput.wasted_target_slot_count,
            ResourceCostScore = scoreInput.resource_cost_score,
            ScoreBucketPriority = scoreInput.score_bucket_priority,
            TargetCount = scoreInput.target_count,
            PositionObjectiveScore = scoreInput.position_objective_score,
            EnemyTargetCount = scoreInput.enemy_target_count,
            AllyTargetCount = scoreInput.ally_target_count,
            EstimatedDamage = scoreInput.estimated_damage,
            EstimatedControlCount = scoreInput.estimated_control_count,
            PositionCurrentDistance = scoreInput.position_current_distance,
            PositionSafeDistance = scoreInput.position_safe_distance,
            DistanceToPrimaryCoord = scoreInput.distance_to_primary_coord,
            HasPostActionThreatProjection = scoreInput.has_post_action_threat_projection,
            PreActionIsLethalSurvivalRisk = scoreInput.pre_action_is_lethal_survival_risk,
            PostActionIsLethalSurvivalRisk = scoreInput.post_action_is_lethal_survival_risk,
            PreActionThreatExpectedDamage = scoreInput.pre_action_threat_expected_damage,
            PostActionRemainingThreatExpectedDamage =
                scoreInput.post_action_remaining_threat_expected_damage,
            PostActionSurvivalMargin = scoreInput.post_action_survival_margin,
            PostActionRemainingThreatCount = scoreInput.post_action_remaining_threat_count,
        };
    }

    internal static bool IsBetter(BattleAiScoreInput candidate, BattleAiScoreInput bestCandidate)
    {
        if (candidate == null)
        {
            return false;
        }
        if (bestCandidate == null)
        {
            return true;
        }
        return IsBetter(FromScoreInput(candidate), FromScoreInput(bestCandidate));
    }

    internal static bool IsBetter(
        BattleAiCandidateFacts candidate,
        BattleAiCandidateFacts bestCandidate
    )
    {
        if (candidate == null)
        {
            return false;
        }
        if (bestCandidate == null)
        {
            return true;
        }
        if (
            candidate.EstimatedFriendlyLethalTargetCount
            != bestCandidate.EstimatedFriendlyLethalTargetCount
        )
        {
            return candidate.EstimatedFriendlyLethalTargetCount
                < bestCandidate.EstimatedFriendlyLethalTargetCount;
        }
        if (
            candidate.EstimatedFriendlyFireTargetCount
            != bestCandidate.EstimatedFriendlyFireTargetCount
        )
        {
            return candidate.EstimatedFriendlyFireTargetCount
                < bestCandidate.EstimatedFriendlyFireTargetCount;
        }
        if (candidate.FriendlyFirePenaltyScore != bestCandidate.FriendlyFirePenaltyScore)
        {
            return candidate.FriendlyFirePenaltyScore < bestCandidate.FriendlyFirePenaltyScore;
        }

        int survivalRiskComparison = ComparePostActionSurvivalRisk(candidate, bestCandidate);
        if (survivalRiskComparison != 0)
        {
            return survivalRiskComparison > 0;
        }
        if (
            candidate.EstimatedLethalThreatTargetCount
            != bestCandidate.EstimatedLethalThreatTargetCount
        )
        {
            return candidate.EstimatedLethalThreatTargetCount
                > bestCandidate.EstimatedLethalThreatTargetCount;
        }
        if (candidate.EstimatedLethalTargetCount != bestCandidate.EstimatedLethalTargetCount)
        {
            return candidate.EstimatedLethalTargetCount > bestCandidate.EstimatedLethalTargetCount;
        }

        bool candidateEmergency = IsEmergencySurvival(candidate);
        bool bestEmergency = IsEmergencySurvival(bestCandidate);
        if (candidateEmergency != bestEmergency)
        {
            return candidateEmergency;
        }

        if (
            candidate.EstimatedLethalTargetCount > 0
            && bestCandidate.EstimatedLethalTargetCount > 0
        )
        {
            if (candidate.TotalScore != bestCandidate.TotalScore)
            {
                return candidate.TotalScore > bestCandidate.TotalScore;
            }
            if (candidate.HitPayoffScore != bestCandidate.HitPayoffScore)
            {
                return candidate.HitPayoffScore > bestCandidate.HitPayoffScore;
            }
            if (candidate.EffectiveTargetCount != bestCandidate.EffectiveTargetCount)
            {
                return candidate.EffectiveTargetCount > bestCandidate.EffectiveTargetCount;
            }
            if (candidate.WastedTargetSlotCount != bestCandidate.WastedTargetSlotCount)
            {
                return candidate.WastedTargetSlotCount < bestCandidate.WastedTargetSlotCount;
            }
            int lethalNonfatalRiskComparison = CompareNonfatalPostActionSurvivalRisk(
                candidate,
                bestCandidate
            );
            if (lethalNonfatalRiskComparison != 0)
            {
                return lethalNonfatalRiskComparison > 0;
            }
            if (candidate.ResourceCostScore != bestCandidate.ResourceCostScore)
            {
                return candidate.ResourceCostScore < bestCandidate.ResourceCostScore;
            }
        }

        if (candidate.ScoreBucketPriority != bestCandidate.ScoreBucketPriority)
        {
            return candidate.ScoreBucketPriority > bestCandidate.ScoreBucketPriority;
        }
        if (candidate.TotalScore != bestCandidate.TotalScore)
        {
            return candidate.TotalScore > bestCandidate.TotalScore;
        }
        if (candidate.HitPayoffScore != bestCandidate.HitPayoffScore)
        {
            return candidate.HitPayoffScore > bestCandidate.HitPayoffScore;
        }
        if (candidate.EffectiveTargetCount != bestCandidate.EffectiveTargetCount)
        {
            return candidate.EffectiveTargetCount > bestCandidate.EffectiveTargetCount;
        }
        // A slot the preview shows landing nothing is never worth having. Skills that bill per
        // slot already pay via resource_cost_score; on aggregate skills this is the only signal.
        if (candidate.WastedTargetSlotCount != bestCandidate.WastedTargetSlotCount)
        {
            return candidate.WastedTargetSlotCount < bestCandidate.WastedTargetSlotCount;
        }
        if (candidate.TargetCount != bestCandidate.TargetCount)
        {
            return candidate.TargetCount > bestCandidate.TargetCount;
        }

        int nonfatalRiskComparison = CompareNonfatalPostActionSurvivalRisk(
            candidate,
            bestCandidate
        );
        if (nonfatalRiskComparison != 0)
        {
            return nonfatalRiskComparison > 0;
        }
        if (candidate.PositionObjectiveScore != bestCandidate.PositionObjectiveScore)
        {
            return candidate.PositionObjectiveScore > bestCandidate.PositionObjectiveScore;
        }
        return candidate.ResourceCostScore < bestCandidate.ResourceCostScore;
    }

    private static bool IsEmergencySurvival(BattleAiCandidateFacts scoreInput)
    {
        if (scoreInput == null)
        {
            return false;
        }
        if (scoreInput.ScoreBucketId != ArcherSurvivalBucketId)
        {
            return false;
        }
        if (scoreInput.HasPostActionThreatProjection)
        {
            if (
                scoreInput.PreActionIsLethalSurvivalRisk
                && !scoreInput.PostActionIsLethalSurvivalRisk
            )
            {
                return true;
            }
            if (
                scoreInput.PreActionThreatExpectedDamage
                    > scoreInput.PostActionRemainingThreatExpectedDamage
                && scoreInput.PostActionSurvivalMargin >= 0
            )
            {
                return true;
            }
        }
        if (scoreInput.TargetCount > 0 || scoreInput.EffectiveTargetCount > 0)
        {
            return false;
        }
        if (scoreInput.EnemyTargetCount > 0 || scoreInput.AllyTargetCount > 0)
        {
            return false;
        }
        if (scoreInput.EstimatedDamage != 0 || scoreInput.EstimatedControlCount != 0)
        {
            return false;
        }
        if (scoreInput.PositionCurrentDistance >= 0 && scoreInput.PositionSafeDistance > 0)
        {
            int currentGap = scoreInput.PositionSafeDistance - scoreInput.PositionCurrentDistance;
            if (currentGap < 2)
            {
                return false;
            }
            if (scoreInput.DistanceToPrimaryCoord >= 0)
            {
                return scoreInput.DistanceToPrimaryCoord >= scoreInput.PositionSafeDistance;
            }
            return scoreInput.PositionObjectiveScore > 0;
        }
        return scoreInput.PositionObjectiveScore > 0;
    }

    private static int ComparePostActionSurvivalRisk(
        BattleAiCandidateFacts candidate,
        BattleAiCandidateFacts bestCandidate
    )
    {
        if (candidate == null || bestCandidate == null)
        {
            return 0;
        }
        if (
            !candidate.HasPostActionThreatProjection || !bestCandidate.HasPostActionThreatProjection
        )
        {
            return 0;
        }
        bool candidateFatal = candidate.PostActionIsLethalSurvivalRisk;
        bool bestFatal = bestCandidate.PostActionIsLethalSurvivalRisk;
        if (candidateFatal != bestFatal)
        {
            return candidateFatal ? -1 : 1;
        }
        return 0;
    }

    private static int CompareNonfatalPostActionSurvivalRisk(
        BattleAiCandidateFacts candidate,
        BattleAiCandidateFacts bestCandidate
    )
    {
        if (candidate == null || bestCandidate == null)
        {
            return 0;
        }
        if (
            !candidate.HasPostActionThreatProjection || !bestCandidate.HasPostActionThreatProjection
        )
        {
            return 0;
        }
        if (
            candidate.PostActionIsLethalSurvivalRisk || bestCandidate.PostActionIsLethalSurvivalRisk
        )
        {
            return 0;
        }

        bool candidateThreatFree = candidate.PostActionRemainingThreatCount <= 0;
        bool bestThreatFree = bestCandidate.PostActionRemainingThreatCount <= 0;
        if (candidateThreatFree != bestThreatFree)
        {
            return candidateThreatFree ? 1 : -1;
        }

        int candidateDamage = candidate.PostActionRemainingThreatExpectedDamage;
        int bestDamage = bestCandidate.PostActionRemainingThreatExpectedDamage;
        if (candidateDamage != bestDamage)
        {
            return candidateDamage < bestDamage ? 1 : -1;
        }

        int candidateCount = candidate.PostActionRemainingThreatCount;
        int bestCount = bestCandidate.PostActionRemainingThreatCount;
        if (candidateCount != bestCount)
        {
            return candidateCount < bestCount ? 1 : -1;
        }

        int candidateMargin = candidate.PostActionSurvivalMargin;
        int bestMargin = bestCandidate.PostActionSurvivalMargin;
        if (candidateMargin != bestMargin)
        {
            return candidateMargin > bestMargin ? 1 : -1;
        }
        return 0;
    }
}
