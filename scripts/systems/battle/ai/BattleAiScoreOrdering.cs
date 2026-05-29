using Godot;

[GlobalClass]
public partial class BattleAiScoreOrdering : RefCounted
{
    public static bool is_better(BattleAiScoreInput candidate, BattleAiScoreInput best_candidate)
    {
        if (candidate == null)
        {
            return false;
        }
        if (best_candidate == null)
        {
            return true;
        }

        int candidateBucket = candidate.score_bucket_priority;
        int bestBucket = best_candidate.score_bucket_priority;
        if (candidateBucket != bestBucket)
        {
            return candidateBucket > bestBucket;
        }

        int candidateLethalThreat = candidate.estimated_lethal_threat_target_count;
        int bestLethalThreat = best_candidate.estimated_lethal_threat_target_count;
        if (candidateLethalThreat != bestLethalThreat)
        {
            return candidateLethalThreat > bestLethalThreat;
        }

        int candidateLethal = candidate.estimated_lethal_target_count;
        int bestLethal = best_candidate.estimated_lethal_target_count;
        if (candidateLethal != bestLethal)
        {
            return candidateLethal > bestLethal;
        }

        int candidateScore = candidate.total_score;
        int bestScore = best_candidate.total_score;
        if (candidateScore != bestScore)
        {
            return candidateScore > bestScore;
        }

        int candidateHit = candidate.estimated_hit_rate_percent;
        int bestHit = best_candidate.estimated_hit_rate_percent;
        if (candidateHit != bestHit)
        {
            return candidateHit > bestHit;
        }

        int candidateMove = candidate.move_cost;
        int bestMove = best_candidate.move_cost;
        if (candidateMove != bestMove)
        {
            return candidateMove < bestMove;
        }

        int candidateResource = candidate.resource_cost_score;
        int bestResource = best_candidate.resource_cost_score;
        return candidateResource < bestResource;
    }
}
