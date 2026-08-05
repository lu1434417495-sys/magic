using System;
using System.Collections.Generic;
using Godot;

internal readonly record struct BattleWindupQuote(
    int Tier,
    int MaxTier,
    int SkillLevel,
    int StrengthModifier,
    int ConstitutionModifier,
    int TuPerTier,
    int TotalWindupTu,
    int AdditionalStaminaCost,
    int TotalStaminaCost,
    int WeaponDiceMultiplier
);

internal static class BattleWindupRules
{
    private static readonly StringName StrengthModifier = "strength_modifier";
    private static readonly StringName ConstitutionModifier = "constitution_modifier";
    private static readonly HashSet<StringName> InterruptingStatusIds = new()
    {
        "prone",
        "stunned",
        "paralyzed",
        "frozen",
        "petrified",
        "madness",
        "staggered",
        "meteor_concussed",
    };

    internal static int GetNaturalMaxTier(BattleUnitReadView unit)
    {
        int constitutionModifier = unit.GetAttributeValue(ConstitutionModifier);
        long roundedUpHalf = ((long)constitutionModifier + 1L) / 2L;
        return (int)Math.Clamp(roundedUpHalf, 1L, int.MaxValue);
    }

    internal static int GetMaxTier(
        BattleUnitReadView unit,
        SkillDefinition skillDefinition
    )
    {
        CombatWindupDefinition windup = skillDefinition?.CombatProfile?.Windup;
        if (!unit.IsValid || windup == null)
            return 0;
        int skillLevel = unit.GetKnownSkillLevel(skillDefinition.SkillId);
        int naturalCap = GetNaturalMaxTier(unit);
        int skillCap = windup.GetSkillTierCap(skillLevel);
        return skillCap > 0 ? Math.Min(naturalCap, skillCap) : naturalCap;
    }

    internal static bool TryBuildQuote(
        BattleUnitReadView unit,
        SkillDefinition skillDefinition,
        int selectedTier,
        out BattleWindupQuote quote,
        out string message,
        bool requireAffordable = true
    )
    {
        quote = default;
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        CombatWindupDefinition windup = combatProfile?.Windup;
        if (!unit.IsValid || skillDefinition == null || windup == null)
        {
            message = "该技能没有可用的蓄力配置。";
            return false;
        }
        int maxTier = GetMaxTier(unit, skillDefinition);
        if (selectedTier < 1 || selectedTier > maxTier)
        {
            message = $"蓄力挡位无效；当前可选择 1～{maxTier} 挡。";
            return false;
        }
        int skillLevel = unit.GetKnownSkillLevel(skillDefinition.SkillId);
        int strengthModifier = unit.GetAttributeValue(StrengthModifier);
        int constitutionModifier = unit.GetAttributeValue(ConstitutionModifier);
        int tuPerTier = GetTuPerTier(strengthModifier);
        CombatSkillResourceCosts baseCosts =
            combatProfile.GetEffectiveResourceCostValues(skillLevel);
        long additionalStaminaCostValue =
            (long)selectedTier * windup.StaminaCostPerTier;
        long totalStaminaCostValue =
            Math.Max(baseCosts.StaminaCost, 0) + additionalStaminaCostValue;
        long totalWindupTuValue = (long)selectedTier * tuPerTier;
        long weaponDiceMultiplierValue =
            (long)windup.GetBaseWeaponDiceMultiplier(skillLevel)
            + (long)selectedTier * windup.WeaponDicePerTier;
        if (
            additionalStaminaCostValue > int.MaxValue
            || totalStaminaCostValue > int.MaxValue
            || totalWindupTuValue > int.MaxValue
            || weaponDiceMultiplierValue > int.MaxValue
        )
        {
            message = "该蓄力挡位的费用、时间或武器骰超出可结算范围。";
            return false;
        }
        int additionalStaminaCost = (int)additionalStaminaCostValue;
        int totalStaminaCost = (int)totalStaminaCostValue;
        if (requireAffordable && unit.CurrentStamina < totalStaminaCost)
        {
            message =
                $"体力不足：{selectedTier} 挡需要 {totalStaminaCost} 体力（基础 {Math.Max(baseCosts.StaminaCost, 0)} + 蓄力 {additionalStaminaCost}）。";
            return false;
        }
        quote = new BattleWindupQuote(
            selectedTier,
            maxTier,
            skillLevel,
            strengthModifier,
            constitutionModifier,
            tuPerTier,
            (int)totalWindupTuValue,
            additionalStaminaCost,
            totalStaminaCost,
            (int)weaponDiceMultiplierValue
        );
        message = "";
        return true;
    }

    internal static IReadOnlyList<CombatEffectDefinition> ApplyWeaponDiceMultiplier(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        int multiplier
    )
    {
        if (effectDefinitions == null || effectDefinitions.Count == 0)
            return Array.Empty<CombatEffectDefinition>();
        var result = new List<CombatEffectDefinition>(effectDefinitions.Count);
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions)
        {
            result.Add(
                effectDefinition?.AddWeaponDice == true
                    ? effectDefinition.WithWeaponDiceMultiplier(Math.Max(multiplier, 1))
                    : effectDefinition
            );
        }
        return result;
    }

    internal static bool HasInterruptingStatus(BattleUnitState unitState)
    {
        if (unitState == null)
            return false;
        foreach (StringName statusId in InterruptingStatusIds)
        {
            if (unitState.HasStatusEffect(statusId))
                return true;
        }
        return false;
    }

    internal static int GetTuPerTier(int strengthModifier)
    {
        if (strengthModifier >= 5)
            return 5;
        if (strengthModifier >= 3)
            return 10;
        if (strengthModifier >= 1)
            return 15;
        return 20;
    }
}
