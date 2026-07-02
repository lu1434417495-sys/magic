using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// BattleDamageResolver 的 partial：伤害/加成/武器骰池的掷骰与骰面事件聚合。按阶段拆出，不改逻辑。
public partial class BattleDamageResolver
{






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
        return rollResult;
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
        var rolls = new List<int>();
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
            diceCount,
            diceSides,
            rolls.ToArray(),
            diceTotal,
            diceBonus,
            maxTotal,
            diceTotal == maxTotal
        );
    }

    private IReadOnlyList<EquipmentAbilityTaggedBonusDamageRoll> RollEquipmentAbilityBonusDamageDiceByTag(
        BattleUnitState sourceUnit,
        BattleUnitState targetUnit,
        DamageResolutionContext damageContext,
        StringName fallbackDamageTag,
        StringName rollMode = default
    )
    {
        if (
            _equipment_ability_runtime_service == null
            || sourceUnit == null
            || targetUnit == null
            || damageContext?.AttackSuccess != true
        )
        {
            return Array.Empty<EquipmentAbilityTaggedBonusDamageRoll>();
        }

        IReadOnlyList<BattleEquipmentAbilityBonusDamageDiceResult> diceResults =
            _equipment_ability_runtime_service.CollectBonusDamageDiceOnHit(
                new BattleEquipmentAbilityBonusDamageDiceContext
                {
                    SourceUnit = sourceUnit,
                    TargetUnit = targetUnit,
                    BattleState = _equipment_ability_runtime_service.GetBattleState(),
                    AttackSucceeded = true,
                    CriticalHit = damageContext.CriticalHit,
                }
            );
        var aggregateByTag = new List<EquipmentAbilityTaggedBonusDamageRoll>();
        foreach (BattleEquipmentAbilityBonusDamageDiceResult dice in diceResults)
        {
            if (
                dice == null
                || ((dice.DiceCount <= 0 || dice.DiceSides <= 0) && dice.FlatBonus <= 0)
            )
                continue;
            StringName damageTag = ResolveEquipmentAbilityBonusDamageTag(
                dice,
                fallbackDamageTag
            );
            if (damageTag == "")
                continue;
            DicePoolRollResult roll =
                dice.DiceCount > 0 && dice.DiceSides > 0
                    ? RollDicePool(
                        dice.DiceCount,
                        dice.DiceSides,
                        dice.FlatBonus,
                        "equipment_ability_bonus_damage_dice",
                        rollMode
                    )
                    : new DicePoolRollResult(
                        0,
                        0,
                        Array.Empty<int>(),
                        0,
                        dice.FlatBonus,
                        dice.FlatBonus,
                        true
                    );
            AddEquipmentAbilityBonusDamageRoll(aggregateByTag, damageTag, roll);
        }
        return aggregateByTag.Count == 0
            ? Array.Empty<EquipmentAbilityTaggedBonusDamageRoll>()
            : aggregateByTag;
    }

    private static void AddEquipmentAbilityBonusDamageRoll(
        List<EquipmentAbilityTaggedBonusDamageRoll> result,
        StringName damageTag,
        DicePoolRollResult roll
    )
    {
        if (result == null || damageTag == "" || (!roll.HasDice && roll.Bonus <= 0))
            return;
        for (int index = 0; index < result.Count; index++)
        {
            EquipmentAbilityTaggedBonusDamageRoll existing = result[index];
            if (existing.DamageTag != damageTag)
                continue;
            result[index] = new EquipmentAbilityTaggedBonusDamageRoll(
                damageTag,
                CombineDicePoolRolls(existing.Roll, roll)
            );
            return;
        }
        result.Add(new EquipmentAbilityTaggedBonusDamageRoll(damageTag, roll));
    }

    private static StringName ResolveEquipmentAbilityBonusDamageTag(
        BattleEquipmentAbilityBonusDamageDiceResult dice,
        StringName fallbackDamageTag
    )
    {
        foreach (StringName damageTag in dice?.DamageTags ?? Array.Empty<StringName>())
        {
            StringName normalized = ProgressionDataUtils.to_string_name(damageTag);
            if (DamageTagContentRules.ToDamageTagKind(normalized) != DamageTagKind.Unknown)
                return normalized;
        }

        StringName damageType = ProgressionDataUtils.to_string_name(dice?.DamageType ?? new StringName(""));
        if (DamageTagContentRules.ToDamageTagKind(damageType) != DamageTagKind.Unknown)
            return damageType;

        StringName fallback = ProgressionDataUtils.to_string_name(fallbackDamageTag);
        return DamageTagContentRules.ToDamageTagKind(fallback) != DamageTagKind.Unknown
            ? fallback
            : new StringName("");
    }

    private static DicePoolRollResult CombineDicePoolRolls(
        DicePoolRollResult left,
        DicePoolRollResult right
    )
    {
        if (!left.HasDice)
            return right;
        if (!right.HasDice)
            return left;

        int[] leftRolls = left.Rolls ?? Array.Empty<int>();
        int[] rightRolls = right.Rolls ?? Array.Empty<int>();
        int[] rolls = new int[leftRolls.Length + rightRolls.Length];
        Array.Copy(leftRolls, rolls, leftRolls.Length);
        Array.Copy(rightRolls, 0, rolls, leftRolls.Length, rightRolls.Length);
        int sides = left.Sides == right.Sides ? left.Sides : 0;
        int maxTotal = Math.Max(left.MaxTotal, 0) + Math.Max(right.MaxTotal, 0);
        int total = Math.Max(left.Total, 0) + Math.Max(right.Total, 0);
        return new DicePoolRollResult(
            Math.Max(left.Count, 0) + Math.Max(right.Count, 0),
            sides,
            rolls,
            total,
            left.Bonus + right.Bonus,
            maxTotal,
            maxTotal > 0 && total == maxTotal
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

    private static List<int> BuildPreviewDiceRolls(int diceCount, int diceSides, int diceTotal)
    {
        var rolls = new List<int>();
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
        StringName damageDiceHighTotalRollReason = "";
        DamageDiceMaxReasonKind skillDamageDiceIsMaxReason = DamageDiceMaxReasonKind.None;
        DamageDiceMaxReasonKind weaponDamageDiceIsMaxReason = DamageDiceMaxReasonKind.None;
        if (criticalHit && hasAnyRegularDice)
        {
            damageDiceHighTotalRoll = true;
            damageDiceHighTotalRollReason = DiceEventReasonCriticalHit;
        }
        else if (
            hasAnyRegularDice
            && regularDiceTotal * DamageDiceHighTotalThresholdDenominator
                >= regularDiceMaxTotal * DamageDiceHighTotalThresholdNumerator
        )
        {
            damageDiceHighTotalRoll = true;
            damageDiceHighTotalRollReason = DiceEventReasonDiceThreshold;
        }
        if (criticalHit && hasSkillDice)
        {
            skillDamageDiceIsMax = true;
            skillDamageDiceIsMaxReason = DamageDiceMaxReasonKind.CriticalHit;
        }
        else if (hasSkillDice && skillDiceTotal == skillDiceMaxTotal)
        {
            skillDamageDiceIsMax = true;
            skillDamageDiceIsMaxReason = DamageDiceMaxReasonKind.SkillDiceMax;
        }
        if (criticalHit && hasWeaponDice)
        {
            weaponDamageDiceIsMax = true;
            weaponDamageDiceIsMaxReason = DamageDiceMaxReasonKind.CriticalHit;
        }
        else if (hasWeaponDice && weaponDiceTotal == weaponDiceMaxTotal)
        {
            weaponDamageDiceIsMax = true;
            weaponDamageDiceIsMaxReason = DamageDiceMaxReasonKind.WeaponDiceMax;
        }
        return new DamageDiceEventFlags(
            new DamageDiceEventSnapshot(
                damageDiceHighTotalRoll,
                damageDiceHighTotalRollReason,
                skillDamageDiceIsMax,
                skillDamageDiceIsMaxReason,
                weaponDamageDiceIsMax,
                weaponDamageDiceIsMaxReason
            )
        );
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

}
