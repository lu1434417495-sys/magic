using System;
using Godot;

internal static class CombatLineThroughAttackContentRules
{
    internal static void AppendValidationErrors(
        Godot.Collections.Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile,
        SkillDef skillDef
    )
    {
        CombatLineThroughAttackDef profile = combatProfile?.line_through_attack_profile;
        if (profile == null)
            return;

        if (
            combatProfile.TargetModeKind != BattleTargetMode.Unit
            || combatProfile.TargetFilterKind != BattleTargetFilter.Enemy
            || combatProfile.target_selection_mode != new StringName("single_unit")
            || combatProfile.min_target_count != 1
            || combatProfile.max_target_count != 1
            || combatProfile.allow_repeat_target
            || combatProfile.max_hits_per_target != 1
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile requires exactly one enemy unit target."
            );
        }
        if (
            combatProfile.WeaponRangePolicyKind != CombatWeaponRangePolicy.Configured
            || !combatProfile.requires_los
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile requires configured range and LOS."
            );
        }
        if (profile.maximum_weapon_range <= 0 || profile.intermediate_weapon_dice_multiplier <= 0)
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile weapon range and intermediate weapon dice must be positive."
            );
        }
        if (
            profile.successful_intermediate_hit_bonus_weapon_dice < 0
            || profile.successful_intermediate_hit_attack_roll_bonus < 0
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile successful-hit bonuses must be non-negative."
            );
        }

        int maxLevel = Math.Max(skillDef?.max_level ?? 0, 0);
        ValidateCurve(
            errors,
            skillId,
            "primary_weapon_dice_multiplier_curve",
            profile.primary_weapon_dice_multiplier_curve,
            maxLevel,
            value => value > 0,
            "positive"
        );
        ValidateCurve(
            errors,
            skillId,
            "primary_attack_roll_bonus_curve",
            profile.primary_attack_roll_bonus_curve,
            maxLevel,
            value => value >= 0,
            "non-negative"
        );
        ValidateCurve(
            errors,
            skillId,
            "successful_intermediate_hit_bonus_cap_curve",
            profile.successful_intermediate_hit_bonus_cap_curve,
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
        if (combatProfile.allows_natural_weapon)
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile requires an equipped weapon and cannot allow natural weapons."
            );
        }
        if (
            combatProfile.cast_variants.Count > 0
            || combatProfile.passive_effect_defs.Count > 0
            || combatProfile.casting_time_tu != 0
            || combatProfile.windup_profile != null
            || combatProfile.directional_piercing_profile != null
            || combatProfile.approach_attack_profile != null
            || combatProfile.spell_reaction_profile != null
            || combatProfile.random_chain_attack_count > 0
            || combatProfile.special_resolution_profile_id != ""
        )
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile cannot combine with passive effects, cast variants, delayed casting, windup, directional piercing, approach attacks, spell reactions, random chain, or special profiles."
            );
        }
        if (skillDef?.contingency_automation_profile?.can_be_stored_in_contingency == true)
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile cannot be stored in contingency."
            );
        }
        if (combatProfile.effect_defs.Count != 1)
        {
            errors.Add(
                $"Skill {skillId} combat_profile.line_through_attack_profile requires exactly one standard weapon damage effect."
            );
            return;
        }

        CombatEffectDef damage = combatProfile.effect_defs[0];
        if (
            damage == null
            || damage.EffectKind != BattleEffectKind.Damage
            || !damage.add_weapon_dice
            || !damage.requires_weapon
            || !damage.use_weapon_physical_damage_tag
            || !damage.resolve_as_weapon_attack
            || damage.weapon_dice_multiplier != 1
            || damage.power != 0
            || damage.dice_count != 0
            || damage.dice_sides != 0
            || damage.dice_bonus != 0
            || damage.bonus_damage_dice_count != 0
            || damage.bonus_damage_dice_sides != 0
            || damage.bonus_damage_dice_bonus != 0
            || damage.damage_ratio_percent != 100
            || Math.Abs(damage.pre_resistance_damage_multiplier - 1.0) > 0.000001
            || damage.dr_bypass_tag != ""
            || damage.mitigation_bypass_damage_tags.Count != 0
            || damage.mitigation_bypass_tiers.Count != 0
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
        int[] curve,
        int maxLevel,
        Func<int, bool> predicate,
        string valueRule
    )
    {
        if (
            curve != null
            && curve.Length > maxLevel
            && Array.TrueForAll(curve, value => predicate(value))
        )
        {
            return;
        }
        errors.Add(
            $"Skill {skillId} combat_profile.line_through_attack_profile {fieldName} must cover levels 0 through max_level with {valueRule} values."
        );
    }
}
