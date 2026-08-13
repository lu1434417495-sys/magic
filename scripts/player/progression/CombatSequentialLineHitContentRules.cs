using System;
using Godot;

internal static class CombatSequentialLineHitContentRules
{
    internal static void AppendValidationErrors(
        Godot.Collections.Array<string> errors,
        StringName skillId,
        CombatSkillDef combatProfile,
        SkillDef skillDef
    )
    {
        CombatSequentialLineHitDef profile = combatProfile?.sequential_line_hit_profile;
        if (profile == null)
            return;

        const string profilePath = "combat_profile.sequential_line_hit_profile";
        if (
            combatProfile.TargetModeKind != BattleTargetMode.Unit
            || combatProfile.TargetFilterKind != BattleTargetFilter.Enemy
            || combatProfile.TargetSelectionModeKind != BattleTargetSelectionMode.SingleUnit
            || combatProfile.min_target_count != 1
            || combatProfile.allow_repeat_target
            || combatProfile.max_hits_per_target != 1
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} requires one selected enemy unit and max_hits_per_target=1."
            );
        }
        if (
            combatProfile.WeaponRangePolicyKind != CombatWeaponRangePolicy.Configured
            || !combatProfile.requires_los
            || combatProfile.ProjectileKindTyped != CombatProjectileKind.Magical
            || combatProfile.AttackResolutionModeKind != CombatSkillAttackResolutionMode.Auto
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} requires configured range, LOS, magical projectile delivery, and auto attack resolution."
            );
        }
        if (
            combatProfile.required_weapon_families.Count != 0
            || combatProfile.required_weapon_type_ids.Count != 0
            || combatProfile.allows_natural_weapon
            || combatProfile.requires_heavy_weapon
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} cannot require or resolve through a weapon."
            );
        }
        if (
            combatProfile.cast_variants.Count > 0
            || combatProfile.passive_effect_defs.Count > 0
            || combatProfile.casting_time_tu != 0
            || combatProfile.windup_profile != null
            || combatProfile.directional_piercing_profile != null
            || combatProfile.approach_attack_profile != null
            || combatProfile.line_through_attack_profile != null
            || combatProfile.spell_reaction_profile != null
            || combatProfile.ranged_weapon_reaction_profile != null
            || combatProfile.random_chain_attack_count > 0
            || combatProfile.special_resolution_profile_id != ""
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} cannot combine with another special execution path, delayed casting, cast variants, or passive effects."
            );
        }
        if (skillDef?.contingency_automation_profile?.can_be_stored_in_contingency == true)
        {
            errors.Add(
                $"Skill {skillId} {profilePath} cannot be stored in contingency."
            );
        }

        int maxLevel = Math.Max(skillDef?.max_level ?? 0, 0);
        ValidateCurve(
            errors,
            skillId,
            profilePath,
            "minimum_primary_distance_curve",
            profile.minimum_primary_distance_curve,
            maxLevel,
            value => value > 0,
            "positive"
        );
        ValidateCurve(
            errors,
            skillId,
            profilePath,
            "continuation_range_curve",
            profile.continuation_range_curve,
            maxLevel,
            value => value > 0,
            "positive"
        );
        ValidateCurve(
            errors,
            skillId,
            profilePath,
            "follow_up_attack_penalty_curve",
            profile.follow_up_attack_penalty_curve,
            maxLevel,
            value => value >= 0,
            "non-negative"
        );
        for (int level = 0; level <= maxLevel; level++)
        {
            int minimumDistance = ReadCurveValue(
                profile.minimum_primary_distance_curve,
                level
            );
            int configuredRange = combatProfile.GetEffectiveRangeValue(level);
            if (
                configuredRange <= 0
                || minimumDistance <= 0
                || minimumDistance > configuredRange
                || combatProfile.GetEffectiveMaxTargetCount(level) < 2
            )
            {
                errors.Add(
                    $"Skill {skillId} {profilePath} requires minimum distance within configured range and at least two potential targets at every level."
                );
                break;
            }
        }

        if (combatProfile.effect_defs.Count == 0)
        {
            errors.Add(
                $"Skill {skillId} {profilePath} requires level-gated ordinary spell damage effects."
            );
            return;
        }
        foreach (CombatEffectDef damage in combatProfile.effect_defs)
        {
            if (!IsOrdinarySpellDamage(damage))
            {
                errors.Add(
                    $"Skill {skillId} {profilePath} effects must be ordinary non-weapon dice damage without fixed damage, bonus dice, percentage scaling, or mitigation bypass."
                );
                break;
            }
        }
        for (int level = 0; level <= maxLevel; level++)
        {
            int activeDamageCount = 0;
            foreach (CombatEffectDef damage in combatProfile.effect_defs)
            {
                if (
                    damage != null
                    && level >= Math.Max(damage.min_skill_level, 0)
                    && (damage.max_skill_level < 0 || level <= damage.max_skill_level)
                )
                {
                    activeDamageCount++;
                }
            }
            if (activeDamageCount == 1)
                continue;
            errors.Add(
                $"Skill {skillId} {profilePath} requires exactly one active damage effect at level {level}."
            );
            break;
        }
    }

    private static bool IsOrdinarySpellDamage(CombatEffectDef damage) =>
        damage != null
        && damage.EffectKind == BattleEffectKind.Damage
        && damage.power == 0
        && damage.dice_count > 0
        && damage.dice_sides > 1
        && damage.dice_bonus == 0
        && damage.bonus_damage_dice_count == 0
        && damage.bonus_damage_dice_sides == 0
        && damage.bonus_damage_dice_bonus == 0
        && !damage.add_weapon_dice
        && !damage.requires_weapon
        && !damage.use_weapon_physical_damage_tag
        && !damage.resolve_as_weapon_attack
        && damage.damage_ratio_percent == 100
        && Math.Abs(damage.pre_resistance_damage_multiplier - 1.0) <= 0.000001
        && damage.dr_bypass_tag == ""
        && damage.mitigation_bypass_damage_tags.Count == 0
        && damage.mitigation_bypass_tiers.Count == 0;

    private static void ValidateCurve(
        Godot.Collections.Array<string> errors,
        StringName skillId,
        string profilePath,
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
            $"Skill {skillId} {profilePath} {fieldName} must cover levels 0 through max_level with {valueRule} values."
        );
    }

    private static int ReadCurveValue(int[] curve, int level) =>
        curve == null || curve.Length == 0
            ? 0
            : curve[Math.Clamp(level, 0, curve.Length - 1)];
}
