using Godot;

internal static class MisfortuneContentRules
{
    internal static readonly StringName BlackStarBrandSkillId = "black_star_brand";
    internal static readonly StringName CrownBreakSkillId = "crown_break";
    internal static readonly StringName DoomSentenceSkillId = "doom_sentence";
    internal static readonly StringName BlackCrownSealSkillId = "black_crown_seal";

    internal static bool IsGatedSkill(StringName skillId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(skillId);
        return normalized == BlackStarBrandSkillId
            || normalized == CrownBreakSkillId
            || normalized == DoomSentenceSkillId
            || normalized == BlackCrownSealSkillId;
    }
}
