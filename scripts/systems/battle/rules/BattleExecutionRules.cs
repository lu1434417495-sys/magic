using System;
using Godot;

public readonly record struct BattleExecuteSoulFractureParams(
    bool HasValue,
    StringName StatusId,
    int DurationTu,
    int HealMultiplierPercent,
    int ShieldGainMultiplierPercent
)
{
    public static BattleExecuteSoulFractureParams Empty =>
        new(false, BattleExecutionRules.SoulFractureStatusId, 0, 100, 100);
}

public readonly record struct BattleExecutePlan(
    StringName Branch,
    int CurrentHp,
    int MaxHp,
    int Threshold,
    int FatalDamage,
    bool BypassShield,
    BattleExecuteSoulFractureParams SoulFractureParams
)
{
    public bool CanExecute => Branch == BattleExecutionRules.BranchLowHpExecute;
}

public readonly record struct BattleExecutionRuleParams(
    StringName SkillId,
    int ThresholdBaseValue,
    int ThresholdLevelAnchor,
    int ThresholdLevelBonusPerDelta,
    int ThresholdMaxHpRatioPercent,
    int ThresholdCapMaxHpRatioPercent,
    int SoulFractureDurationTu,
    int HealMultiplierPercent,
    int ShieldGainMultiplierPercent
)
{
    public static BattleExecutionRuleParams Defaults(StringName skillId = default) =>
        new(Normalize(skillId), 0, 17, 5, 20, 50, 0, 100, 100);

    public static BattleExecutionRuleParams FromEffect(
        CombatEffectDefinition effectDefinition,
        StringName skillId = default
    )
    {
        return new BattleExecutionRuleParams(
            Normalize(skillId),
            Math.Max(effectDefinition?.ThresholdBaseValue ?? 0, 0),
            Math.Max(effectDefinition?.ThresholdLevelAnchor ?? 17, 0),
            Math.Max(effectDefinition?.ThresholdLevelBonusPerDelta ?? 5, 0),
            Math.Max(effectDefinition?.ThresholdMaxHpRatioPercent ?? 20, 0),
            Math.Max(effectDefinition?.ThresholdCapMaxHpRatioPercent ?? 50, 0),
            Math.Max(effectDefinition?.SoulFractureDurationTu ?? 0, 0),
            Math.Clamp(effectDefinition?.HealMultiplierPercent ?? 100, 0, 100),
            Math.Clamp(effectDefinition?.ShieldGainMultiplierPercent ?? 100, 0, 100)
        );
    }

    private static StringName Normalize(StringName value) => value ?? new StringName("");

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}

public static class BattleExecutionRules
{
    internal static readonly StringName BossTargetStatId = "boss_target";
    internal static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    internal static readonly StringName BranchInvalidTarget = "invalid_target";
    internal static readonly StringName BranchLowHpExecute = "low_hp_execute";
    internal static readonly StringName SoulFractureStatusId = "soul_fracture";

    private static readonly StringName HpMax = "hp_max";

    public static int ResolveThreshold(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleExecutionRuleParams parameters
    )
    {
        int skillLevel = 0;
        if (!IsEmpty(parameters.SkillId) && sourceUnit != null)
        {
            skillLevel = sourceUnit.GetKnownSkillLevelTyped(parameters.SkillId);
        }
        int levelBonus =
            Math.Max(skillLevel - parameters.ThresholdLevelAnchor, 0)
            * parameters.ThresholdLevelBonusPerDelta;

        int targetMaxHp = Math.Max(GetAttributeValue(targetUnit, HpMax), 0);
        int hpFloor = Math.Max(targetMaxHp * parameters.ThresholdMaxHpRatioPercent / 100, 0);
        int rawThreshold = Math.Max(parameters.ThresholdBaseValue, hpFloor) + levelBonus;
        if (targetMaxHp > 0)
        {
            rawThreshold = Math.Max(rawThreshold, 1);
        }
        int cap = Math.Max(targetMaxHp * parameters.ThresholdCapMaxHpRatioPercent / 100, 0);
        return cap > 0 ? Math.Min(rawThreshold, cap) : rawThreshold;
    }

    internal static int ResolveThreshold(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        BattleExecutionRuleParams parameters
    )
    {
        int skillLevel = 0;
        if (!IsEmpty(parameters.SkillId) && sourceUnit.IsValid)
        {
            skillLevel = sourceUnit.GetKnownSkillLevel(parameters.SkillId);
        }
        int levelBonus =
            Math.Max(skillLevel - parameters.ThresholdLevelAnchor, 0)
            * parameters.ThresholdLevelBonusPerDelta;

        int targetMaxHp = Math.Max(targetUnit.GetAttributeValue(HpMax), 0);
        int hpFloor = Math.Max(targetMaxHp * parameters.ThresholdMaxHpRatioPercent / 100, 0);
        int rawThreshold = Math.Max(parameters.ThresholdBaseValue, hpFloor) + levelBonus;
        if (targetMaxHp > 0)
        {
            rawThreshold = Math.Max(rawThreshold, 1);
        }
        int cap = Math.Max(targetMaxHp * parameters.ThresholdCapMaxHpRatioPercent / 100, 0);
        return cap > 0 ? Math.Min(rawThreshold, cap) : rawThreshold;
    }

    public static BattleExecutePlan BuildExecutePlan(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleExecutionRuleParams parameters
    )
    {
        int maxHp = Math.Max(GetAttributeValue(targetUnit, HpMax), 0);
        int currentHp = targetUnit != null ? Math.Max(targetUnit.GetCurrentHp(), 0) : 0;
        int threshold = ResolveThreshold(sourceUnit, targetUnit, parameters);

        if (targetUnit != null && targetUnit.IsAlive() && currentHp > 0 && currentHp <= threshold)
        {
            return new BattleExecutePlan(
                BranchLowHpExecute,
                currentHp,
                maxHp,
                threshold,
                currentHp,
                true,
                BuildSoulFractureParams(parameters)
            );
        }

        return new BattleExecutePlan(
            BranchInvalidTarget,
            currentHp,
            maxHp,
            threshold,
            0,
            false,
            BattleExecuteSoulFractureParams.Empty
        );
    }

    internal static BattleExecutePlan BuildExecutePlan(
        BattleUnitReadView sourceUnit,
        BattleUnitReadView targetUnit,
        BattleExecutionRuleParams parameters
    )
    {
        int maxHp = Math.Max(targetUnit.GetAttributeValue(HpMax), 0);
        int currentHp = targetUnit.IsValid ? Math.Max(targetUnit.CurrentHp, 0) : 0;
        int threshold = ResolveThreshold(sourceUnit, targetUnit, parameters);

        if (targetUnit.IsValid && targetUnit.IsAlive && currentHp > 0 && currentHp <= threshold)
        {
            return new BattleExecutePlan(
                BranchLowHpExecute,
                currentHp,
                maxHp,
                threshold,
                currentHp,
                true,
                BuildSoulFractureParams(parameters)
            );
        }

        return new BattleExecutePlan(
            BranchInvalidTarget,
            currentHp,
            maxHp,
            threshold,
            0,
            false,
            BattleExecuteSoulFractureParams.Empty
        );
    }

    public static bool IsBossTarget(BattleUnitState targetUnit)
    {
        if (targetUnit == null || targetUnit.attribute_snapshot == null)
        {
            return false;
        }
        return GetAttributeValue(targetUnit, BossTargetStatId) > 0
            || GetAttributeValue(targetUnit, FortuneMarkTargetStatId) > 1;
    }

    public static bool IsEliteOrBossTarget(BattleUnitState targetUnit)
    {
        if (targetUnit == null || targetUnit.attribute_snapshot == null)
        {
            return false;
        }
        return GetAttributeValue(targetUnit, BossTargetStatId) > 0
            || GetAttributeValue(targetUnit, FortuneMarkTargetStatId) > 0;
    }

    private static BattleExecuteSoulFractureParams BuildSoulFractureParams(
        BattleExecutionRuleParams parameters
    )
    {
        if (parameters.SoulFractureDurationTu <= 0)
        {
            return BattleExecuteSoulFractureParams.Empty;
        }
        return new BattleExecuteSoulFractureParams(
            true,
            SoulFractureStatusId,
            parameters.SoulFractureDurationTu,
            parameters.HealMultiplierPercent,
            parameters.ShieldGainMultiplierPercent
        );
    }

    private static int GetAttributeValue(BattleUnitState unit, StringName attributeId)
    {
        AttributeSnapshot attributeSnapshot = unit?.attribute_snapshot;
        if (attributeSnapshot == null || IsEmpty(attributeId))
        {
            return 0;
        }
        return attributeSnapshot.GetValue(attributeId);
    }

    private static int ReadInt(Godot.Collections.Dictionary source, StringName key)
    {
        if (source == null || IsEmpty(key) || !source.ContainsKey(key))
        {
            return 0;
        }
        try
        {
            return source[key].AsInt32();
        }
        catch
        {
            return int.TryParse(source[key].ToString(), out int parsed) ? parsed : 0;
        }
    }

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}
