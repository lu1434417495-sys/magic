using Godot;

/// <summary>Shared distinction between the training milestone and the current trainable cap.</summary>
public static class PromotionEligibilityRules
{
    public const int CompletedSkillCheckBonus = 1;

    public static int GetMilestoneLevel(SkillDefinition definition, UnitProgress progress)
    {
        int absoluteMax = SkillEffectiveMaxLevelRules.GetEffectiveAbsoluteMaxLevel(definition, progress);
        return definition?.NonCoreMaxLevel > 0
            ? Mathf.Min(absoluteMax, definition.NonCoreMaxLevel)
            : absoluteMax;
    }

    public static bool HasReachedMilestone(SkillDefinition definition, UnitSkillProgress skill, UnitProgress progress)
    {
        int level = GetMilestoneLevel(definition, progress);
        return level > 0 && skill?.is_learned == true && skill.skill_level >= level;
    }

    public static bool IsReadyTrigger(SkillDefinition definition, UnitSkillProgress skill, UnitProgress progress) =>
        progress != null && SkillProfessionPromotionRules.CanTriggerProfessionPromotion(definition)
        && HasReachedMilestone(definition, skill, progress)
        && !progress.HasUsedGrowthTrigger(skill.skill_id);

    public static bool HasCoreQualification(SkillDefinition definition, UnitSkillProgress skill,
        UnitProgress progress, bool projectedCore = false) =>
        definition != null && skill?.is_learned == true && (skill.is_core || projectedCore)
        && (progress?.HasUsedGrowthTrigger(skill.skill_id) == true || HasReachedMilestone(definition, skill, progress));
}
