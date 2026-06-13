using System.Collections.Generic;
using Godot;

/// <summary>
/// 单个技能在指定等级下的战斗有效配置快照。
/// 由 <see cref="SkillCatalog"/> 按 catalog revision 缓存；调用方只读，不应修改其中的资源引用。
/// </summary>
public sealed class SkillEffectiveCombatProfile
{
    public SkillEffectiveCombatProfile(
        SkillDef skillDef,
        CombatSkillDef combatProfile,
        int skillLevel,
        CombatSkillResourceCosts resourceCosts,
        int attackRollBonus,
        StringName areaPattern,
        int areaValue,
        int rangeValue,
        int maxTargetCount,
        IReadOnlyList<CombatCastVariantDef> unlockedCastVariants
    )
    {
        SkillDef = skillDef;
        CombatProfile = combatProfile;
        SkillLevel = skillLevel;
        ResourceCosts = resourceCosts;
        AttackRollBonus = attackRollBonus;
        AreaPattern = areaPattern;
        AreaValue = areaValue;
        RangeValue = rangeValue;
        MaxTargetCount = maxTargetCount;
        UnlockedCastVariants =
            unlockedCastVariants ?? System.Array.Empty<CombatCastVariantDef>();
    }

    public SkillDef SkillDef { get; }

    public CombatSkillDef CombatProfile { get; }

    public int SkillLevel { get; }

    public CombatSkillResourceCosts ResourceCosts { get; }

    public int AttackRollBonus { get; }

    public StringName AreaPattern { get; }

    public int AreaValue { get; }

    public int RangeValue { get; }

    public int MaxTargetCount { get; }

    public IReadOnlyList<CombatCastVariantDef> UnlockedCastVariants { get; }

    public bool HasCombatProfile => CombatProfile != null;
}
