using Godot;

public static class BattleAiSafetyGate
{
    public static bool IsEligible(BattleAiScoreInput scoreInput)
    {
        return GetRejectionReason(scoreInput).ToString().Length == 0;
    }

    public static StringName GetRejectionReason(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
        {
            return "missing_score_input";
        }

        StringName intent = scoreInput.action_intent;
        if (intent == null || intent.ToString().Length == 0)
        {
            GameLog.Warning(
                "BattleAiSafetyGate received empty action_intent; allowing candidate for legacy compatibility.",
                "ai.safety.empty_intent",
                "ai"
            );
            return "";
        }

        bool hasProjection = scoreInput.has_post_action_threat_projection;
        bool preLethal = scoreInput.pre_action_is_lethal_survival_risk;
        bool postLethal = scoreInput.post_action_is_lethal_survival_risk;
        int preDamage = scoreInput.pre_action_threat_expected_damage;
        int postDamage = scoreInput.post_action_remaining_threat_expected_damage;

        if (intent == BattleAiActionIntent.Offense)
        {
            if (hasProjection && !preLethal && postLethal)
            {
                return "offense_post_lethal_from_safe";
            }
            return "";
        }
        if (intent == BattleAiActionIntent.Escape)
        {
            if (!hasProjection)
                return "escape_missing_projection";
            if (postLethal)
                return "escape_post_lethal";
            if (postDamage >= preDamage)
                return "escape_not_safer";
            return "";
        }
        if (intent == BattleAiActionIntent.Survival)
        {
            if (!hasProjection)
                return "survival_missing_projection";
            if (postLethal)
                return "survival_post_lethal";
            return "";
        }
        if (intent == BattleAiActionIntent.Positioning)
        {
            if (!hasProjection)
                return "positioning_missing_projection";
            if (!preLethal && postLethal)
                return "positioning_post_lethal_from_safe";
            if (preDamage <= 0 && postDamage > 0)
                return "positioning_introduces_threat";
            return "";
        }
        if (
            intent == BattleAiActionIntent.Control
            || intent == BattleAiActionIntent.Wait
        )
        {
            return "";
        }
        return "unknown_action_intent";
    }
}
