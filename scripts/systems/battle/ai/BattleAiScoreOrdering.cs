public static class BattleAiScoreOrdering
{
    public static bool IsBetter(BattleAiScoreInput candidate, BattleAiScoreInput bestCandidate)
    {
        if (candidate == null)
        {
            return false;
        }
        if (bestCandidate == null)
        {
            return true;
        }

        int candidateBucket = candidate.score_bucket_priority;
        int bestBucket = bestCandidate.score_bucket_priority;
        if (candidateBucket != bestBucket)
        {
            return candidateBucket > bestBucket;
        }

        int candidateLethalThreat = candidate.estimated_lethal_threat_target_count;
        int bestLethalThreat = bestCandidate.estimated_lethal_threat_target_count;
        if (candidateLethalThreat != bestLethalThreat)
        {
            return candidateLethalThreat > bestLethalThreat;
        }

        int candidateLethal = candidate.estimated_lethal_target_count;
        int bestLethal = bestCandidate.estimated_lethal_target_count;
        if (candidateLethal != bestLethal)
        {
            return candidateLethal > bestLethal;
        }

        int candidateScore = candidate.total_score;
        int bestScore = bestCandidate.total_score;
        if (candidateScore != bestScore)
        {
            return candidateScore > bestScore;
        }

        int candidateHit = candidate.estimated_hit_rate_percent;
        int bestHit = bestCandidate.estimated_hit_rate_percent;
        if (candidateHit != bestHit)
        {
            return candidateHit > bestHit;
        }

        int candidateMove = candidate.move_cost;
        int bestMove = bestCandidate.move_cost;
        if (candidateMove != bestMove)
        {
            return candidateMove < bestMove;
        }

        int candidateResource = candidate.resource_cost_score;
        int bestResource = bestCandidate.resource_cost_score;
        return candidateResource < bestResource;
    }
}
