using System.Linq;
using System;
using System.Collections.Generic;
using Godot;

internal static class CombatLineThroughAttackDefinitionValidationRules
{
    internal static void AppendValidationErrors(
        Godot.Collections.Array<string> errors,
        StringName skillId,
        CombatSkillDefinition combatProfile,
        SkillDefinition skillDef
    )
    {
        CombatLineThroughAttackDefinition profile = combatProfile?.LineThroughAttack;
        if (profile == null)
            return;

        if (
            combatProfile.TargetModeKind != BattleTargetMode.Unit
            || combatProfile.TargetFilterKind != BattleTargetFilter.Enemy
            || combatProfile.TargetSelectionMode != new StringName("single_unit")
            || combatProfile.MinTargetCount != 1
            || combatProfile.MaxTargetCount != 1
            || combatProfile.AllowRepeatTarget
            || combatProfile.MaxHitsPerTarget != 1
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile requires exactly one enemy unit target."
            );
        }
        if (
            combatProfile.WeaponRangePolicyKind != CombatWeaponRangePolicy.Configured
            || !combatProfile.RequiresLos
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile requires configured range and LOS."
            );
        }
        if (profile.MaximumWeaponRange <= 0 || profile.IntermediateWeaponDiceMultiplier <= 0)
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile weapon range and intermediate weapon dice must be positive."
            );
        }
        if (
            profile.SuccessfulIntermediateHitBonusWeaponDice < 0
            || profile.SuccessfulIntermediateHitAttackRollBonus < 0
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile successful-hit bonuses must be non-negative."
            );
        }

        int maxLevel = Math.Max(skillDef?.MaxLevel ?? 0, 0);
        ValidateCurve(
            errors,
            skillId,
            "primary_weapon_dice_multiplier_curve",
            profile.PrimaryWeaponDiceMultiplierCurve,
            maxLevel,
            value => value > 0,
            "positive"
        );
        ValidateCurve(
            errors,
            skillId,
            "primary_attack_roll_bonus_curve",
            profile.PrimaryAttackRollBonusCurve,
            maxLevel,
            value => value >= 0,
            "non-negative"
        );
        ValidateCurve(
            errors,
            skillId,
            "successful_intermediate_hit_bonus_cap_curve",
            profile.SuccessfulIntermediateHitBonusCapCurve,
            maxLevel,
            value => value >= 0,
            "non-negative"
        );
        for (int level = 0; level <= maxLevel; level++)
        {
            if (
                combatProfile.GetEffectiveRangeValue(level) <= 0
                || combatProfile.GetEffectiveAttackRollBonus(level) != 0
            )
            {
                errors.Add(
                    $"Skill {skillId} combat_profile.line_through_attack_profile requires positive range and zero generic attack_roll_bonus at every level."
                );
                break;
            }
        }
        if (combatProfile.AllowsNaturalWeapon)
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile requires an equipped weapon and cannot allow natural weapons."
            );
        }
        if (
            combatProfile.CastVariants.Count > 0
            || combatProfile.PassiveEffectDefinitions.Count > 0
            || combatProfile.CastingTimeTu != 0
            || combatProfile.Windup != null
            || combatProfile.DirectionalPiercing != null
            || combatProfile.ApproachAttack != null
            || combatProfile.SpellReaction != null
            || combatProfile.RandomChainAttackCount > 0
            || combatProfile.SpecialResolutionProfileId != ""
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile cannot combine with passive effects, cast variants, delayed casting, windup, directional piercing, approach attacks, spell reactions, random chain, or special profiles."
            );
        }
        if (skillDef?.ContingencyAutomationProfile?.CanBeStoredInContingency == true)
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile cannot be stored in contingency."
            );
        }
        if (combatProfile.EffectDefinitions.Count != 1)
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile requires exactly one standard weapon damage effect."
            );
            return;
        }

        CombatEffectDefinition damage = combatProfile.EffectDefinitions[0];
        if (
            damage == null
            || damage.EffectKind != BattleEffectKind.Damage
            || !damage.AddWeaponDice
            || !damage.RequiresWeapon
            || !damage.UseWeaponPhysicalDamageTag
            || !damage.ResolveAsWeaponAttack
            || damage.WeaponDiceMultiplier != 1
            || damage.Power != 0
            || damage.DiceCount != 0
            || damage.DiceSides != 0
            || damage.DiceBonus != 0
            || damage.BonusDamageDiceCount != 0
            || damage.BonusDamageDiceSides != 0
            || damage.BonusDamageDiceBonus != 0
            || damage.DamageRatioPercent != 100
            || Math.Abs(damage.PreResistanceDamageMultiplier - 1.0) > 0.000001
            || damage.DrBypassTag != ""
            || damage.MitigationBypassDamageTags.Count != 0
            || damage.MitigationBypassTiers.Count != 0
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile damage must be one 1W ordinary current-weapon attack without fixed damage, percentage scaling, or mitigation bypass."
            );
        }
    }

    private static void ValidateCurve(
        Godot.Collections.Array<string> errors,
        StringName skillId,
        string fieldName,
        IReadOnlyList<int> curve,
        int maxLevel,
        Func<int, bool> predicate,
        string valueRule
    )
    {
        if (
            curve != null
            && curve.Count > maxLevel
            && curve.All(predicate)
        )
        {
            return;
        }
        errors.Add(
            $"Skill {skillId} combat_profile.line_through_attack_profile {fieldName} must cover levels 0 through max_level with {valueRule} values."
        );
    }
}
