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

    public static BattleExecuteSoulFractureParams DefaultResisted =>
        new(false, BattleExecutionRules.SoulFractureStatusId, 60, 100, 100);
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
    StringName ThresholdAbilityMod,
    int ThresholdAbilityModMultiplier,
    int ThresholdMaxHpRatioPercent,
    int ThresholdCapMaxHpRatioPercent,
    int SoulFractureDurationTu,
    int HealMultiplierPercent,
    int ShieldGainMultiplierPercent,
    int BossNonLethalDamageMaxHpRatioPercent,
    int BossNonLethalDamageFloor,
    int NonLethalDamageRatioPercent
)
{
    private static readonly StringName DefaultThresholdAbilityMod = "intelligence_modifier";

    public static BattleExecutionRuleParams Defaults(StringName skillId = default) =>
        new(
            Normalize(skillId),
            0,
            17,
            5,
            DefaultThresholdAbilityMod,
            5,
            20,
            50,
            0,
            100,
            100,
            12,
            25,
            30
        );

    public static BattleExecutionRuleParams FromEffect(CombatEffectDef effectDef)
    {
        Godot.Collections.Dictionary parameters = effectDef?.@params;
        return new BattleExecutionRuleParams(
            ReadStringName(parameters, "skill_id"),
            Math.Max(ReadInt(parameters, "threshold_base_value", 0), 0),
            Math.Max(ReadInt(parameters, "threshold_level_anchor", 17), 0),
            Math.Max(ReadInt(parameters, "threshold_level_bonus_per_delta", 5), 0),
            ReadStringName(parameters, "threshold_ability_mod", DefaultThresholdAbilityMod),
            Math.Max(ReadInt(parameters, "threshold_ability_mod_multiplier", 5), 0),
            Math.Max(ReadInt(parameters, "threshold_max_hp_ratio_percent", 20), 0),
            Math.Max(ReadInt(parameters, "threshold_cap_max_hp_ratio_percent", 50), 0),
            ReadInt(parameters, "soul_fracture_duration_tu"),
            ReadInt(parameters, "heal_multiplier_percent", 100),
            ReadInt(parameters, "shield_gain_multiplier_percent", 100),
            Math.Max(ReadInt(parameters, "boss_non_lethal_damage_max_hp_ratio_percent", 12), 0),
            Math.Max(ReadInt(parameters, "boss_non_lethal_damage_floor", 25), 1),
            Math.Max(ReadInt(parameters, "non_lethal_damage_ratio_percent", 30), 0)
        );
    }

    private static int ReadInt(
        Godot.Collections.Dictionary source,
        string key,
        int fallback = 0
    )
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
        {
            return fallback;
        }
        try
        {
            return source[key].AsInt32();
        }
        catch
        {
            return int.TryParse(source[key].ToString(), out int parsed) ? parsed : fallback;
        }
    }

    private static StringName ReadStringName(
        Godot.Collections.Dictionary source,
        string key,
        StringName fallback = default
    )
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
        {
            return Normalize(fallback);
        }
        StringName parsed = ProgressionDataUtils.to_string_name(source[key]);
        return IsEmpty(parsed) ? Normalize(fallback) : parsed;
    }

    private static StringName Normalize(StringName value) => value ?? new StringName("");

    private static bool IsEmpty(StringName value) =>
        value == null || string.IsNullOrEmpty(value.ToString());
}

public static class BattleExecutionRules
{
    public static readonly StringName BossTargetStatId = "boss_target";
    public static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    public static readonly StringName BranchInvalidTarget = "invalid_target";
    public static readonly StringName BranchLowHpExecute = "low_hp_execute";
    public static readonly StringName SoulFractureStatusId = "soul_fracture";

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
            skillLevel = ReadInt(sourceUnit.known_skill_level_map, parameters.SkillId);
        }
        int levelBonus =
            Math.Max(skillLevel - parameters.ThresholdLevelAnchor, 0)
            * parameters.ThresholdLevelBonusPerDelta;

        int abilityMod = !IsEmpty(parameters.ThresholdAbilityMod) && sourceUnit != null
            ? GetAttributeValue(sourceUnit, parameters.ThresholdAbilityMod)
            : 0;

        int targetMaxHp = Math.Max(GetAttributeValue(targetUnit, HpMax), 0);
        int hpFloor = Math.Max(targetMaxHp * parameters.ThresholdMaxHpRatioPercent / 100, 0);
        int rawThreshold =
            Math.Max(parameters.ThresholdBaseValue, hpFloor)
            + levelBonus
            + abilityMod * parameters.ThresholdAbilityModMultiplier;
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
        int currentHp = targetUnit != null ? Math.Max(targetUnit.current_hp, 0) : 0;
        int threshold = ResolveThreshold(sourceUnit, targetUnit, parameters);

        if (targetUnit != null && currentHp <= threshold)
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

    public static int ResolveNonLethalDamage(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        BattleExecutionRuleParams parameters,
        bool isBoss = false
    )
    {
        if (isBoss)
        {
            int targetMaxHp = Math.Max(GetAttributeValue(targetUnit, HpMax), 0);
            return Math.Max(
                targetMaxHp * parameters.BossNonLethalDamageMaxHpRatioPercent / 100,
                parameters.BossNonLethalDamageFloor
            );
        }

        int threshold = ResolveThreshold(sourceUnit, targetUnit, parameters);
        return Math.Max(threshold * parameters.NonLethalDamageRatioPercent / 100, 1);
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
        return attributeSnapshot.get_value(attributeId);
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
