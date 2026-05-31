using Godot;

[GlobalClass]
public partial class BattleAiSafetyGate : RefCounted
{
    public static bool is_eligible(BattleAiScoreInput score_input)
    {
        return get_rejection_reason(score_input).ToString().Length == 0;
    }

    public static StringName get_rejection_reason(BattleAiScoreInput score_input)
    {
        if (score_input == null)
        {
            return "missing_score_input";
        }

        StringName intent = score_input.action_intent;
        if (intent == null || intent.ToString().Length == 0)
        {
            GameLog.Warning(
                "BattleAiSafetyGate received empty action_intent; allowing candidate for legacy compatibility.",
                "ai.safety.empty_intent",
                "ai"
            );
            return "";
        }

        bool hasProjection = score_input.has_post_action_threat_projection;
        bool preLethal = score_input.pre_action_is_lethal_survival_risk;
        bool postLethal = score_input.post_action_is_lethal_survival_risk;
        int preDamage = score_input.pre_action_threat_expected_damage;
        int postDamage = score_input.post_action_remaining_threat_expected_damage;

        if (intent == BattleAiActionIntent.INTENT_OFFENSE())
        {
            if (hasProjection && !preLethal && postLethal)
            {
                return "offense_post_lethal_from_safe";
            }
            return "";
        }
        if (intent == BattleAiActionIntent.INTENT_ESCAPE())
        {
            if (!hasProjection)
                return "escape_missing_projection";
            if (postLethal)
                return "escape_post_lethal";
            if (postDamage >= preDamage)
                return "escape_not_safer";
            return "";
        }
        if (intent == BattleAiActionIntent.INTENT_SURVIVAL())
        {
            if (!hasProjection)
                return "survival_missing_projection";
            if (postLethal)
                return "survival_post_lethal";
            return "";
        }
        if (intent == BattleAiActionIntent.INTENT_POSITIONING())
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
            intent == BattleAiActionIntent.INTENT_CONTROL()
            || intent == BattleAiActionIntent.INTENT_WAIT()
        )
        {
            return "";
        }
        return "unknown_action_intent";
    }
}
