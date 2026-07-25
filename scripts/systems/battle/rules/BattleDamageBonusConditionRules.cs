using System;
using Godot;

internal static class BattleDamageBonusConditionRules
{
    internal static bool IsMet(
        CombatEffectDefinition effectDefinition,
        BattleUnitState targetUnit
    )
    {
        if (effectDefinition == null || targetUnit == null)
        {
            return false;
        }

        return effectDefinition.BonusConditionKind switch
        {
            BattleDamageBonusConditionKind.TargetLowHp =>
                IsTargetLowHp(effectDefinition, targetUnit),
            BattleDamageBonusConditionKind.TargetDebuffCount =>
                TargetHasEnoughDebuffs(effectDefinition, targetUnit),
            BattleDamageBonusConditionKind.TargetCreatureType =>
                TargetHasCreatureType(effectDefinition, targetUnit),
            BattleDamageBonusConditionKind.TargetHardControlled =>
                BattleStatusSemanticTable.IsHardControlled(targetUnit),
            BattleDamageBonusConditionKind.TargetHasShield => targetUnit.HasShield(),
            _ => false,
        };
    }

    private static bool IsTargetLowHp(
        CombatEffectDefinition effectDefinition,
        BattleUnitState targetUnit
    )
    {
        int maxHp =
            targetUnit.attribute_snapshot?.GetValue(
                AttributeService.ToStringName(AttributeIdKind.HpMax)
            ) ?? 0;
        if (maxHp <= 0)
        {
            maxHp = Math.Max(targetUnit.GetCurrentHp(), 1);
        }
        int thresholdPercent =
            effectDefinition.HpRatioThresholdPercent > 0
                ? Math.Clamp(effectDefinition.HpRatioThresholdPercent, 0, 100)
                : 50;
        return targetUnit.GetCurrentHp() * 100 <= maxHp * thresholdPercent;
    }

    private static bool TargetHasEnoughDebuffs(
        CombatEffectDefinition effectDefinition,
        BattleUnitState targetUnit
    )
    {
        int threshold = Math.Max(effectDefinition.DebuffCountThreshold, 1);
        int count = 0;
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            if (
                !BattleStatusSemanticTable.IsHarmfulStatusEntry(
                    targetUnit.GetStatusEffect(statusId)
                )
            )
            {
                continue;
            }
            count += 1;
            if (count >= threshold)
            {
                return true;
            }
        }
        return false;
    }

    private static bool TargetHasCreatureType(
        CombatEffectDefinition effectDefinition,
        BattleUnitState targetUnit
    )
    {
        StringName requiredTag = ProgressionDataUtils.to_string_name(
            effectDefinition.BonusConditionCreatureTypeTag
        );
        return requiredTag != ""
            && targetUnit.HasCreatureTypeTag(requiredTag);
    }
}
