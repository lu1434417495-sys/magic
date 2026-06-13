using Godot;

internal static class BattleAiSafetyGate
{
    internal static bool IsEligible(BattleAiScoreInput scoreInput)
    {
        return GetRejectionReason(scoreInput).ToString().Length == 0;
    }

    internal static StringName GetRejectionReason(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
        {
            return "missing_score_input";
        }

        StringName rawIntent = scoreInput.action_intent;
        if (rawIntent == null || rawIntent.ToString().Length == 0)
        {
            return "missing_action_intent";
        }
        BattleAiIntent intent = BattleAiActionIntent.ToKind(rawIntent);

        bool hasProjection = scoreInput.has_post_action_threat_projection;
        bool preLethal = scoreInput.pre_action_is_lethal_survival_risk;
        bool postLethal = scoreInput.post_action_is_lethal_survival_risk;
        int preDamage = scoreInput.pre_action_threat_expected_damage;
        int postDamage = scoreInput.post_action_remaining_threat_expected_damage;

        if (intent == BattleAiIntent.Offense)
        {
            if (hasProjection && !preLethal && postLethal)
            {
                return "offense_post_lethal_from_safe";
            }
            return "";
        }
        if (intent == BattleAiIntent.Escape)
        {
            if (!hasProjection)
                return "escape_missing_projection";
            if (postLethal)
                return "escape_post_lethal";
            if (postDamage >= preDamage)
                return "escape_not_safer";
            return "";
        }
        if (intent == BattleAiIntent.Survival)
        {
            if (!hasProjection)
                return "survival_missing_projection";
            if (postLethal)
                return "survival_post_lethal";
            return "";
        }
        if (intent == BattleAiIntent.Positioning)
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
            intent == BattleAiIntent.Control
            || intent == BattleAiIntent.Wait
        )
        {
            return "";
        }
        return "unknown_action_intent";
    }
}
