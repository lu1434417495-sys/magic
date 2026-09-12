internal static class MisfortuneContentRules
{
    internal static bool IsGatedBehavior(SkillRuntimeBehaviorKind behavior) =>
        behavior is SkillRuntimeBehaviorKind.BlackStarBrand
            or SkillRuntimeBehaviorKind.CrownBreak
            or SkillRuntimeBehaviorKind.DoomSentence
            or SkillRuntimeBehaviorKind.BlackCrownSeal;
}
