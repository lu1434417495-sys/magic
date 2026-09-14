using System.Linq;
using System;
using System.Collections.Generic;
using Godot;

internal static class CombatSequentialLineHitDefinitionValidationRules
{
    internal static void AppendValidationErrors(
        Godot.Collections.Array<string> errors,
        StringName skillId,
        CombatSkillDefinition combatProfile,
        SkillDefinition skillDef
    )
    {
        CombatSequentialLineHitDefinition profile = combatProfile?.SequentialLineHit;
        if (profile == null)
            return;

        const string profilePath = "combat_profile.sequential_line_hit_profile";
        if (
            combatProfile.TargetModeKind != BattleTargetMode.Unit
            || combatProfile.TargetFilterKind != BattleTargetFilter.Enemy
            || combatProfile.TargetSelectionModeKind != BattleTargetSelectionMode.SingleUnit
            || combatProfile.MinTargetCount != 1
            || combatProfile.AllowRepeatTarget
            || combatProfile.MaxHitsPerTarget != 1
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} requires one selected enemy unit and max_hits_per_target=1."
            );
        }
        if (
            combatProfile.WeaponRangePolicyKind != CombatWeaponRangePolicy.Configured
            || !combatProfile.RequiresLos
            || combatProfile.ProjectileKindTyped != CombatProjectileKind.Magical
            || combatProfile.AttackResolutionModeKind != CombatSkillAttackResolutionMode.Auto
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} requires configured range, LOS, magical projectile delivery, and auto attack resolution."
            );
        }
        if (
            combatProfile.RequiredWeaponFamilies.Count != 0
            || combatProfile.RequiredWeaponTypeIds.Count != 0
            || combatProfile.AllowsNaturalWeapon
            || combatProfile.RequiresHeavyWeapon
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} cannot require or resolve through a weapon."
            );
        }
        if (
            combatProfile.CastVariants.Count > 0
            || combatProfile.PassiveEffectDefinitions.Count > 0
            || combatProfile.CastingTimeTu != 0
            || combatProfile.Windup != null
            || combatProfile.DirectionalPiercing != null
            || combatProfile.ApproachAttack != null
            || combatProfile.LineThroughAttack != null
            || combatProfile.SpellReaction != null
            || combatProfile.RangedWeaponReaction != null
            || combatProfile.RandomChainAttackCount > 0
            || combatProfile.SpecialResolutionProfileId != ""
        )
        {
            errors.Add(
                $"Skill {skillId} {profilePath} cannot combine with another special execution path, delayed casting, cast variants, or passive effects."
            );
        }
        if (skillDef?.ContingencyAutomationProfile?.CanBeStoredInContingency == true)
        {
            errors.Add(
                $"Skill {skillId} {profilePath} cannot be stored in contingency."
            );
        }

        int maxLevel = Math.Max(skillDef?.MaxLevel ?? 0, 0);
        ValidateCurve(
            errors,
            skillId,
            profilePath,
            "minimum_primary_distance_curve",
            profile.MinimumPrimaryDistanceCurve,
            maxLevel,
            value => value > 0,
            "positive"
        );
        ValidateCurve(
            errors,
            skillId,
            profilePath,
            "continuation_range_curve",
            profile.ContinuationRangeCurve,
            maxLevel,
            value => value > 0,
            "positive"
        );
        ValidateCurve(
            errors,
            skillId,
            profilePath,
            "follow_up_attack_penalty_curve",
            profile.FollowUpAttackPenaltyCurve,
            maxLevel,
            value => value >= 0,
            "non-negative"
        );
        for (int level = 0; level <= maxLevel; level++)
        {
            int minimumDistance = ReadCurveValue(
                profile.MinimumPrimaryDistanceCurve,
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

        if (combatProfile.EffectDefinitions.Count == 0)
        {
            errors.Add(
                $"Skill {skillId} {profilePath} requires level-gated ordinary spell damage effects."
            );
            return;
        }
        foreach (CombatEffectDefinition damage in combatProfile.EffectDefinitions)
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
            foreach (CombatEffectDefinition damage in combatProfile.EffectDefinitions)
            {
                if (
                    damage != null
                    && level >= Math.Max(damage.MinSkillLevel, 0)
                    && (damage.MaxSkillLevel < 0 || level <= damage.MaxSkillLevel)
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

    private static bool IsOrdinarySpellDamage(CombatEffectDefinition damage) =>
        damage != null
        && damage.EffectKind == BattleEffectKind.Damage
        && damage.Power == 0
        && damage.DiceCount > 0
        && damage.DiceSides > 1
        && damage.DiceBonus == 0
        && damage.BonusDamageDiceCount == 0
        && damage.BonusDamageDiceSides == 0
        && damage.BonusDamageDiceBonus == 0
        && !damage.AddWeaponDice
        && !damage.RequiresWeapon
        && !damage.UseWeaponPhysicalDamageTag
        && !damage.ResolveAsWeaponAttack
        && damage.DamageRatioPercent == 100
        && Math.Abs(damage.PreResistanceDamageMultiplier - 1.0) <= 0.000001
        && damage.DrBypassTag == ""
        && damage.MitigationBypassDamageTags.Count == 0
        && damage.MitigationBypassTiers.Count == 0;

    private static void ValidateCurve(
        Godot.Collections.Array<string> errors,
        StringName skillId,
        string profilePath,
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
            $"Skill {skillId} {profilePath} {fieldName} must cover levels 0 through max_level with {valueRule} values."
        );
    }

    private static int ReadCurveValue(IReadOnlyList<int> curve, int level) =>
        curve == null || curve.Count == 0
            ? 0
            : curve[Math.Clamp(level, 0, curve.Count - 1)];
}
