using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// BattleDamageResolver 的 partial：伤害/加成/武器骰池的掷骰与骰面事件聚合。按阶段拆出，不改逻辑。
public partial class BattleDamageResolver : RefCounted
{
    private DicePoolRollResult RollDamageDice(
        CombatEffectDef effectDef,
        bool includeBonus = true,
        string fieldPrefix = "damage_dice",
        StringName rollMode = default
    )
    {
        if (effectDef == null)
        {
            return DicePoolRollResult.Empty;
        }
        int diceCount = Math.Max(effectDef.dice_count, 0);
        int diceSides = Math.Max(effectDef.dice_sides, 0);
        int diceBonus = includeBonus ? effectDef.dice_bonus : 0;
        return RollDicePool(
            diceCount,
            diceSides,
            diceBonus,
            fieldPrefix,
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private DicePoolRollResult RollBonusDamageDice(
        CombatEffectDef effectDef,
        bool includeBonus = true,
        string fieldPrefix = "bonus_damage_dice",
        StringName rollMode = default
    )
    {
        if (effectDef == null)
        {
            return DicePoolRollResult.Empty;
        }
        int diceCount = Math.Max(effectDef.bonus_damage_dice_count, 0);
        int diceSides = Math.Max(effectDef.bonus_damage_dice_sides, 0);
        int diceBonus = includeBonus ? effectDef.bonus_damage_dice_bonus : 0;
        return RollDicePool(
            diceCount,
            diceSides,
            diceBonus,
            fieldPrefix,
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private DicePoolRollResult RollWeaponDice(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        bool includeBonus = true,
        string fieldPrefix = "weapon_damage_dice",
        StringName rollMode = default
    )
    {
        if (!ShouldAddWeaponDice(effectDef))
        {
            return DicePoolRollResult.Empty;
        }
        WeaponDice dice = GetCurrentWeaponDamageDice(sourceUnit);
        if (dice == null || dice.IsEmpty())
        {
            return DicePoolRollResult.Empty;
        }
        int diceCount = Math.Max(dice.dice_count, 0);
        int diceSides = Math.Max(dice.dice_sides, 0);
        int diceBonus = includeBonus ? dice.flat_bonus : 0;
        return RollDicePool(
            diceCount,
            diceSides,
            diceBonus,
            fieldPrefix,
            IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode
        );
    }

    private DicePoolRollResult RollDicePool(
        int diceCount,
        int diceSides,
        int diceBonus,
        string fieldPrefix,
        StringName rollMode = default
    )
    {
        if (string.IsNullOrEmpty(fieldPrefix))
        {
            return DicePoolRollResult.Empty;
        }
        DicePoolRollResult rollResult = RollDicePoolValues(
            diceCount,
            diceSides,
            diceBonus,
            rollMode
        );
        if (!rollResult.HasDice)
        {
            return DicePoolRollResult.Empty;
        }
        GDictionary payload = new()
        {
            [$"{fieldPrefix}_count"] = rollResult.Count,
            [$"{fieldPrefix}_sides"] = rollResult.Sides,
            [$"{fieldPrefix}_rolls"] = rollResult.Rolls,
            [$"{fieldPrefix}_total"] = rollResult.Total,
            [$"{fieldPrefix}_bonus"] = rollResult.Bonus,
            [$"{fieldPrefix}_max_total"] = rollResult.MaxTotal,
            [$"{fieldPrefix}_is_max"] = rollResult.IsMax,
        };
        return rollResult with { Payload = payload };
    }

    private DicePoolRollResult RollDicePoolValues(
        int diceCount,
        int diceSides,
        int diceBonus,
        StringName rollMode = default
    )
    {
        if (diceCount <= 0 || diceSides <= 0)
        {
            return DicePoolRollResult.Empty;
        }
        StringName resolvedRollMode = IsEmpty(rollMode) ? DamagePreviewRollModeRandom : rollMode;
        var rolls = new GArray();
        int diceTotal = BuildDicePoolTotal(diceCount, diceSides, resolvedRollMode);
        if (resolvedRollMode == DamagePreviewRollModeRandom)
        {
            diceTotal = 0;
            for (int i = 0; i < diceCount; i++)
            {
                int roll = RollDamageDieVirtual(diceSides);
                rolls.Add(roll);
                diceTotal += roll;
            }
        }
        else
        {
            rolls = BuildPreviewDiceRolls(diceCount, diceSides, diceTotal);
        }
        int maxTotal = diceCount * diceSides;
        return new DicePoolRollResult(
            new GDictionary(),
            diceCount,
            diceSides,
            rolls,
            diceTotal,
            diceBonus,
            maxTotal,
            diceTotal == maxTotal
        );
    }

    private int RollDamageDieVirtual(int diceSides)
    {
        return _roll_damage_die(diceSides);
    }

    private static int BuildDicePoolTotal(int diceCount, int diceSides, StringName rollMode)
    {
        if (rollMode == DamagePreviewRollModeAverage)
        {
            return RoundToInt((double)diceCount * (diceSides + 1) / 2.0);
        }
        if (rollMode == DamagePreviewRollModeMaximum)
        {
            return diceCount * diceSides;
        }
        return 0;
    }

    private static GArray BuildPreviewDiceRolls(int diceCount, int diceSides, int diceTotal)
    {
        var rolls = new GArray();
        if (diceCount <= 0)
        {
            return rolls;
        }
        int remainingTotal = Math.Clamp(diceTotal, diceCount, diceCount * diceSides);
        for (int index = 0; index < diceCount; index++)
        {
            int remainingDice = diceCount - index;
            int roll = Math.Clamp(RoundToInt((double)remainingTotal / remainingDice), 1, diceSides);
            rolls.Add(roll);
            remainingTotal -= roll;
        }
        return rolls;
    }

    private static DamageDiceEventFlags BuildDamageDiceEventFlags(
        bool criticalHit,
        DicePoolRollResult skillRoll,
        DicePoolRollResult weaponRoll,
        DicePoolRollResult bonusSkillRoll = default
    )
    {
        int skillDiceCount = skillRoll.Count;
        int skillDiceSides = skillRoll.Sides;
        int skillDiceTotal = skillRoll.Total;
        int skillDiceMaxTotal = skillRoll.MaxTotal;
        int bonusSkillDiceCount = bonusSkillRoll.Count;
        int bonusSkillDiceSides = bonusSkillRoll.Sides;
        int bonusSkillDiceTotal = bonusSkillRoll.Total;
        int bonusSkillDiceMaxTotal = bonusSkillRoll.MaxTotal;
        bool hasSkillDice =
            (skillDiceCount > 0 && skillDiceSides > 0 && skillDiceMaxTotal > 0)
            || (bonusSkillDiceCount > 0 && bonusSkillDiceSides > 0 && bonusSkillDiceMaxTotal > 0);
        skillDiceTotal += bonusSkillDiceTotal;
        skillDiceMaxTotal += bonusSkillDiceMaxTotal;

        int weaponDiceCount = weaponRoll.Count;
        int weaponDiceSides = weaponRoll.Sides;
        int weaponDiceTotal = weaponRoll.Total;
        int weaponDiceMaxTotal = weaponRoll.MaxTotal;
        bool hasWeaponDice = weaponDiceCount > 0 && weaponDiceSides > 0 && weaponDiceMaxTotal > 0;
        bool hasAnyRegularDice = hasSkillDice || hasWeaponDice;
        int regularDiceTotal = skillDiceTotal + weaponDiceTotal;
        int regularDiceMaxTotal = skillDiceMaxTotal + weaponDiceMaxTotal;

        bool damageDiceHighTotalRoll = false;
        bool skillDamageDiceIsMax = false;
        bool weaponDamageDiceIsMax = false;
        GDictionary result = new()
        {
            ["damage_dice_high_total_roll"] = false,
            ["damage_dice_high_total_roll_reason"] = new StringName(""),
            ["skill_damage_dice_is_max"] = false,
            ["skill_damage_dice_is_max_reason"] = new StringName(""),
            ["weapon_damage_dice_is_max"] = false,
            ["weapon_damage_dice_is_max_reason"] = new StringName(""),
        };
        if (criticalHit && hasAnyRegularDice)
        {
            damageDiceHighTotalRoll = true;
            result["damage_dice_high_total_roll"] = true;
            result["damage_dice_high_total_roll_reason"] = DiceEventReasonCriticalHit;
        }
        else if (
            hasAnyRegularDice
            && regularDiceTotal * DamageDiceHighTotalThresholdDenominator
                >= regularDiceMaxTotal * DamageDiceHighTotalThresholdNumerator
        )
        {
            damageDiceHighTotalRoll = true;
            result["damage_dice_high_total_roll"] = true;
            result["damage_dice_high_total_roll_reason"] = DiceEventReasonDiceThreshold;
        }
        if (criticalHit && hasSkillDice)
        {
            skillDamageDiceIsMax = true;
            result["skill_damage_dice_is_max"] = true;
            result["skill_damage_dice_is_max_reason"] = DiceEventReasonCriticalHit;
        }
        else if (hasSkillDice && skillDiceTotal == skillDiceMaxTotal)
        {
            skillDamageDiceIsMax = true;
            result["skill_damage_dice_is_max"] = true;
            result["skill_damage_dice_is_max_reason"] = DiceEventReasonSkillDiceMax;
        }
        if (criticalHit && hasWeaponDice)
        {
            weaponDamageDiceIsMax = true;
            result["weapon_damage_dice_is_max"] = true;
            result["weapon_damage_dice_is_max_reason"] = DiceEventReasonCriticalHit;
        }
        else if (hasWeaponDice && weaponDiceTotal == weaponDiceMaxTotal)
        {
            weaponDamageDiceIsMax = true;
            result["weapon_damage_dice_is_max"] = true;
            result["weapon_damage_dice_is_max_reason"] = DiceEventReasonWeaponDiceMax;
        }
        return new DamageDiceEventFlags(
            result,
            new DamageDiceEventSnapshot(
                damageDiceHighTotalRoll,
                skillDamageDiceIsMax,
                weaponDamageDiceIsMax
            )
        );
    }

    private static void ApplyDamageDiceEventFlags(GDictionary result, GDictionary eventFlags)
    {
        foreach (var key in eventFlags.Keys)
        {
            result[key] = eventFlags[key];
        }
    }

    private static GDictionary EnsureDamageDiceEventDefaults(GDictionary @event)
    {
        @event ??= new GDictionary();
        if (!HasKey(@event, "damage_dice_high_total_roll"))
            @event["damage_dice_high_total_roll"] = false;
        if (!HasKey(@event, "damage_dice_high_total_roll_reason"))
            @event["damage_dice_high_total_roll_reason"] = new StringName("");
        if (!HasKey(@event, "skill_damage_dice_is_max"))
            @event["skill_damage_dice_is_max"] = false;
        if (!HasKey(@event, "skill_damage_dice_is_max_reason"))
            @event["skill_damage_dice_is_max_reason"] = new StringName("");
        if (!HasKey(@event, "weapon_damage_dice_is_max"))
            @event["weapon_damage_dice_is_max"] = false;
        if (!HasKey(@event, "weapon_damage_dice_is_max_reason"))
            @event["weapon_damage_dice_is_max_reason"] = new StringName("");
        return @event;
    }

    private static void AttachDamageEventAggregates(GDictionary result)
    {
        result["damage_dice_high_total_roll"] = false;
        result["skill_damage_dice_is_max"] = false;
        result["weapon_damage_dice_is_max"] = false;
        GArray damageEvents = GetArray(result, "damage_events");
        foreach (GDictionary eventValue in ReadDictionaryItems(damageEvents))
        {
            DamageDiceEventSnapshot damageEvent = DamageDiceEventSnapshot.FromDictionary(
                eventValue
            );
            if (damageEvent.DamageDiceHighTotalRoll)
                result["damage_dice_high_total_roll"] = true;
            if (damageEvent.SkillDamageDiceIsMax)
                result["skill_damage_dice_is_max"] = true;
            if (damageEvent.WeaponDamageDiceIsMax)
                result["weapon_damage_dice_is_max"] = true;
        }
    }

    private static void AttachDamageEventAggregates(
        GDictionary result,
        IEnumerable<DamageDiceEventSnapshot> damageEvents
    )
    {
        result["damage_dice_high_total_roll"] = false;
        result["skill_damage_dice_is_max"] = false;
        result["weapon_damage_dice_is_max"] = false;
        if (damageEvents == null)
        {
            return;
        }
        foreach (DamageDiceEventSnapshot damageEvent in damageEvents)
        {
            if (damageEvent.DamageDiceHighTotalRoll)
                result["damage_dice_high_total_roll"] = true;
            if (damageEvent.SkillDamageDiceIsMax)
                result["skill_damage_dice_is_max"] = true;
            if (damageEvent.WeaponDamageDiceIsMax)
                result["weapon_damage_dice_is_max"] = true;
        }
    }
}
