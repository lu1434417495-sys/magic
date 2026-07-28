using Godot;

internal static class SkillProfessionPromotionRules
{
    private static readonly StringName WeaponTrainingTag = "weapon_training";

    internal static bool CanTriggerProfessionPromotion(
        SkillDefinition skillDefinition
    ) =>
        skillDefinition != null
        && !skillDefinition.HasTag(WeaponTrainingTag);
}
