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
        int castingTimeTu,
        int castingMaintenanceDc,
        int castingSpellControlDc,
        PendingCastBindingModeKind pendingCastBindingMode,
        StringName areaPattern,
        int areaValue,
        int rangeValue,
        int maxTargetCount,
        int fumbleProtectionLimit,
        bool hasSpellFateControl,
        bool usesGroundAnchorDriftBacklash,
        IReadOnlyList<CombatCastVariantDefinition> unlockedCastVariants
    )
    {
        SkillDefinition = skillDefinition;
        CombatProfile = combatProfile;
        SkillLevel = skillLevel;
        ResourceCosts = resourceCosts;
        AttackRollBonus = attackRollBonus;
        CastingTimeTu = castingTimeTu;
        CastingMaintenanceDc = castingMaintenanceDc;
        CastingSpellControlDc = castingSpellControlDc;
        PendingCastBindingMode = pendingCastBindingMode;
        AreaPattern = areaPattern;
        AreaValue = areaValue;
        RangeValue = rangeValue;
        MaxTargetCount = maxTargetCount;
        FumbleProtectionLimit = fumbleProtectionLimit;
        HasSpellFateControl = hasSpellFateControl;
        UsesGroundAnchorDriftBacklash = usesGroundAnchorDriftBacklash;
        UnlockedCastVariants = unlockedCastVariants ?? EmptyCastVariants;
    }

    public SkillDefinition SkillDefinition { get; }
    public CombatSkillDefinition CombatProfile { get; }
    public int SkillLevel { get; }
    public CombatSkillResourceCosts ResourceCosts { get; }
    public int AttackRollBonus { get; }
    public int CastingTimeTu { get; }
    public int CastingMaintenanceDc { get; }
    public int CastingSpellControlDc { get; }
    public PendingCastBindingModeKind PendingCastBindingMode { get; }
    public StringName AreaPattern { get; }
    public int AreaValue { get; }
    public int RangeValue { get; }
    public int MaxTargetCount { get; }
    public int FumbleProtectionLimit { get; }
    public bool HasSpellFateControl { get; }
    public bool UsesGroundAnchorDriftBacklash { get; }
    public IReadOnlyList<CombatCastVariantDefinition> UnlockedCastVariants { get; }
    public bool HasCombatProfile => CombatProfile != null;

    internal static SkillEffectiveCombatDefinition BuildUncached(
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        if (skillDefinition?.CombatProfile == null)
            return BuildMissing(skillLevel);
        return BuildUncached(skillDefinition, skillDefinition.CombatProfile, skillLevel);
    }

    internal static SkillEffectiveCombatDefinition BuildUncached(
        CombatSkillDefinition combatProfile,
        int skillLevel
    )
    {
        return BuildUncached(null, combatProfile, skillLevel);
    }

    private static SkillEffectiveCombatDefinition BuildUncached(
        SkillDefinition skillDefinition,
        CombatSkillDefinition profile,
        int skillLevel
    )
    {
        if (profile == null)
            return BuildMissing(skillLevel);
        return new SkillEffectiveCombatDefinition(
            skillDefinition,
            profile,
            skillLevel,
            profile.GetEffectiveResourceCostValues(skillLevel),
            profile.GetEffectiveAttackRollBonus(skillLevel),
            profile.GetEffectiveCastingTimeTu(skillLevel),
            profile.GetEffectiveCastingMaintenanceDc(skillLevel),
            profile.GetEffectiveCastingSpellControlDc(skillLevel),
            profile.GetEffectivePendingCastBindingMode(skillLevel),
            profile.GetEffectiveAreaPattern(skillLevel),
            profile.GetEffectiveAreaValue(skillLevel),
            profile.GetEffectiveRangeValue(skillLevel),
            profile.GetEffectiveMaxTargetCount(skillLevel),
            profile.GetFumbleProtectionLimit(skillLevel),
            profile.HasSpellFateControl(),
            profile.UsesGroundAnchorDriftBacklash(),
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
            0,
            0,
            0,
            PendingCastBindingModeKind.SoftAnchor,
            EmptyAreaPattern,
            0,
            0,
            0,
            0,
            false,
            false,
            EmptyCastVariants
        );
    }
}
