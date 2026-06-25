using System.Collections.Generic;
using Godot;

/// <summary>
/// 单个技能在指定等级下的 plain C# 战斗有效配置快照。
/// 它不持有 <see cref="SkillDef"/> / <see cref="CombatSkillDef"/> / Godot collection wrapper。
/// </summary>
public sealed class SkillEffectiveCombatDefinition
{
    private static readonly StringName EmptyAreaPattern = "";
    private static readonly IReadOnlyList<CombatCastVariantDefinition> EmptyCastVariants =
        System.Array.Empty<CombatCastVariantDefinition>();

    public SkillEffectiveCombatDefinition(
        SkillDefinition skillDefinition,
        CombatSkillDefinition combatProfile,
        int skillLevel,
        CombatSkillResourceCosts resourceCosts,
        int attackRollBonus,
        StringName areaPattern,
        int areaValue,
        int rangeValue,
        int maxTargetCount,
        IReadOnlyList<CombatCastVariantDefinition> unlockedCastVariants
    )
    {
        SkillDefinition = skillDefinition;
        CombatProfile = combatProfile;
        SkillLevel = skillLevel;
        ResourceCosts = resourceCosts;
        AttackRollBonus = attackRollBonus;
        AreaPattern = areaPattern;
        AreaValue = areaValue;
        RangeValue = rangeValue;
        MaxTargetCount = maxTargetCount;
        UnlockedCastVariants = unlockedCastVariants ?? EmptyCastVariants;
    }

    public SkillDefinition SkillDefinition { get; }
    public CombatSkillDefinition CombatProfile { get; }
    public int SkillLevel { get; }
    public CombatSkillResourceCosts ResourceCosts { get; }
    public int AttackRollBonus { get; }
    public StringName AreaPattern { get; }
    public int AreaValue { get; }
    public int RangeValue { get; }
    public int MaxTargetCount { get; }
    public IReadOnlyList<CombatCastVariantDefinition> UnlockedCastVariants { get; }
    public bool HasCombatProfile => CombatProfile != null;

    internal static SkillEffectiveCombatDefinition BuildUncached(
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        if (skillDefinition?.CombatProfile == null)
            return BuildMissing(skillLevel);
        CombatSkillDefinition profile = skillDefinition.CombatProfile;
        return new SkillEffectiveCombatDefinition(
            skillDefinition,
            profile,
            skillLevel,
            profile.GetEffectiveResourceCostValues(skillLevel),
            profile.GetEffectiveAttackRollBonus(skillLevel),
            profile.GetEffectiveAreaPattern(skillLevel),
            profile.GetEffectiveAreaValue(skillLevel),
            profile.GetEffectiveRangeValue(skillLevel),
            profile.GetEffectiveMaxTargetCount(skillLevel),
            profile.GetUnlockedCastVariants(skillLevel)
        );
    }

    internal static SkillEffectiveCombatDefinition BuildMissing(int skillLevel)
    {
        return new SkillEffectiveCombatDefinition(
            null,
            null,
            skillLevel,
            CombatSkillResourceCosts.Zero,
            0,
            EmptyAreaPattern,
            0,
            0,
            0,
            EmptyCastVariants
        );
    }
}
