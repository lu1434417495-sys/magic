using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class BattleDamageResolver
{
    private static DamageOutcomeResult WithSaveResult(
        DamageOutcomeResult damageOutcome,
        BattleSaveResult saveResult,
        CombatEffectDefinition effectDefinition
    )
    {
        DamageEventResult @event = damageOutcome.Event;
        int resolvedDamage = ApplySaveResultToDamageEvent(
            ref @event,
            saveResult,
            effectDefinition,
            damageOutcome.ResolvedDamage
        );
        return damageOutcome with
        {
            Event = @event,
            ResolvedDamage = Math.Max(resolvedDamage, 0),
        };
    }

    private static int ApplySaveResultToDamageEvent(
        ref DamageEventResult damageEvent,
        BattleSaveResult saveResult,
        CombatEffectDefinition effectDefinition,
        int preSaveDamage
    )
    {
        return ApplySaveResultToDamageEvent(
            ref damageEvent,
            saveResult,
            effectDefinition != null && effectDefinition.SavePartialOnSuccess,
            preSaveDamage
        );
    }

    private static int ApplySaveResultToDamageEvent(
        ref DamageEventResult damageEvent,
        BattleSaveResult saveResult,
        bool savePartialOnSuccess,
        int preSaveDamage
    )
    {
        if (!saveResult.HasSave)
        {
            return Math.Max(preSaveDamage, 0);
        }
        preSaveDamage = Math.Max(preSaveDamage, 0);
        damageEvent.SaveResult = SaveResolutionFromBattleSave(saveResult);
        damageEvent.SaveSuccess = saveResult.Success;
        damageEvent.SaveImmune = saveResult.Immune;
        damageEvent.SavePartialApplied = false;
        damageEvent.PreSaveDamage = preSaveDamage;
        if (!saveResult.Success)
        {
            damageEvent.SaveAdjustedDamage = preSaveDamage;
            damageEvent.FullyAbsorbedBySave = false;
            return preSaveDamage;
        }
        int adjustedDamage = 0;
        if (
            savePartialOnSuccess
            && !saveResult.Immune
        )
        {
            adjustedDamage = preSaveDamage / 2;
            damageEvent.SavePartialApplied = true;
        }
        damageEvent.ResolvedDamage = adjustedDamage;
        damageEvent.SaveAdjustedDamage = adjustedDamage;
        damageEvent.FullyAbsorbedBySave = preSaveDamage > 0 && adjustedDamage <= 0;
        return adjustedDamage;
    }

    internal static SaveResolutionResult SaveResolutionFromBattleSave(BattleSaveResult saveResult)
    {
        return new SaveResolutionResult
        {
            HasSave = saveResult.HasSave,
            Success = saveResult.Success,
            Immune = saveResult.Immune,
            Roll = saveResult.NaturalRoll,
            Total = saveResult.RollTotal,
            NaturalRoll = saveResult.NaturalRoll,
            RollTotal = saveResult.RollTotal,
            Dc = saveResult.Dc,
            SaveKind = saveResult.SaveTag,
            Ability = saveResult.Ability,
            SaveTag = saveResult.SaveTag,
            AdvantageState = saveResult.AdvantageState,
            AbilityValue = saveResult.AbilityValue,
            AbilityModifier = saveResult.AbilityModifier,
            Bonus = saveResult.Bonus,
            Degree = saveResult.Degree.ToString(),
            Sources = CopySaveSources(saveResult.Sources),
        };
    }

    private static BattleSaveSource[] CopySaveSources(IReadOnlyList<BattleSaveSource> sources)
    {
        if (sources == null || sources.Count == 0)
        {
            return Array.Empty<BattleSaveSource>();
        }
        var result = new BattleSaveSource[sources.Count];
        for (int index = 0; index < sources.Count; index++)
        {
            result[index] = sources[index];
        }
        return result;
    }

    private double BuildOffenseMultiplier(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        double multiplier = GetPreResistanceDamageMultiplier(effectDefinition);
        if (HasBonusCondition(effectDefinition, targetUnit))
        {
            multiplier *= GetDamageRatioMultiplier(effectDefinition);
        }
        if (HasStatusEffect(sourceUnit, StatusAttackUp))
        {
            multiplier *= 1.0 + 0.10 * GetStatusStrength(sourceUnit, StatusAttackUp);
        }
        if (sourceUnit != null && sourceUnit.HasStatusEffect(StatusArcherPreAim))
        {
            multiplier *= 1.15;
        }
        if (targetUnit != null && targetUnit.HasStatusEffect(StatusMarked))
        {
            multiplier *= 1.10;
        }
        multiplier *= GetLowLuckBloodDebtMultiplier(targetUnit);
        multiplier *= GetSourceOutgoingDamageMultiplier(sourceUnit);
        multiplier *= GetTargetIncomingDamageMultiplier(targetUnit);
        multiplier *= GetTargetDamageMultiplierRuleMultiplier(targetUnit, effectDefinition);
        return Math.Max(multiplier, 0.0);
    }

    private static double GetTargetDamageMultiplierRuleMultiplier(
        BattleUnitState targetUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        if (
            targetUnit == null
            || effectDefinition?.TargetDamageMultiplierRules == null
            || effectDefinition.TargetDamageMultiplierRules.Count == 0
        )
        {
            return 1.0;
        }
        double multiplier = 1.0;
        foreach (
            CombatTargetDamageMultiplierRuleDefinition rule in
                effectDefinition.TargetDamageMultiplierRules
        )
        {
            if (rule == null || !rule.Matches(targetUnit))
            {
                continue;
            }
            multiplier *= Math.Max(rule.MultiplierPercent, 0) / 100.0;
        }
        return multiplier;
    }

    private static StringName ResolveDamageTag(
        BattleUnitState sourceUnit,
        CombatEffectDefinition effectDefinition
    )
    {
        if (ShouldUseWeaponPhysicalDamageTag(effectDefinition))
        {
            return GetUnitWeaponPhysicalDamageTag(sourceUnit);
        }
        StringName explicitEffectTag = effectDefinition?.DamageTag ?? new StringName("");
        return DamageTagContentRules.ToDamageTagKind(explicitEffectTag) != DamageTagKind.Unknown
            ? explicitEffectTag
            : new StringName("");
    }

    private static bool ShouldUseWeaponPhysicalDamageTag(
        CombatEffectDefinition effectDefinition
    )
    {
        return DamageEffectRuntimeParameters.FromEffect(effectDefinition).UseWeaponPhysicalDamageTag;
    }

    private static StringName GetUnitWeaponPhysicalDamageTag(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return "";
        }
        StringName damageTag = unitState.weapon_physical_damage_tag;
        return DamageTagContentRules.IsPhysicalDamageTag(
            DamageTagContentRules.ToDamageTagKind(damageTag)
        )
            ? damageTag
            : new StringName("");
    }

    private static bool DoesSaveBlockEffect(BattleSaveResult saveResult)
    {
        return saveResult.HasSave && saveResult.Success;
    }

    private static StringName ResolveStatusIdForSave(
        CombatEffectDefinition effectDefinition,
        BattleSaveResult saveResult
    )
    {
        if (effectDefinition == null)
        {
            return "";
        }
        StringName saveFailureStatusId = ProgressionDataUtils.to_string_name(
            effectDefinition.SaveFailureStatusId
        );
        if (saveResult.HasSave && !saveResult.Success && saveFailureStatusId != "")
        {
            return saveFailureStatusId;
        }
        return ProgressionDataUtils.to_string_name(effectDefinition.StatusId);
    }

    private int GetTargetSecondaryHitSaveBonus(BattleUnitState targetUnit)
    {
        if (targetUnit == null)
        {
            return 0;
        }
        int bonus = 0;
        foreach (StringName statusId in targetUnit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState statusEntry = targetUnit.GetStatusEffect(statusId);
            if (statusEntry == null)
            {
                continue;
            }
            bonus = Math.Max(
                bonus,
                statusEntry.control_save_bonus
            );
        }
        return bonus;
    }
}
