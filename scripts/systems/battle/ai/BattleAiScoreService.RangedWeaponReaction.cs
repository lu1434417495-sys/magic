using System;
using System.Collections.Generic;
using Godot;

public partial class BattleAiScoreService
{
    private static List<CombatEffectDefinition>
        FilterRangedWeaponReactionReadinessEffects(
            IReadOnlyList<CombatEffectDefinition> effectDefinitions,
            SkillDefinition skillDefinition
        )
    {
        CombatRangedWeaponReactionDefinition profile =
            skillDefinition?.CombatProfile?.RangedWeaponReaction;
        if (profile == null)
            return new List<CombatEffectDefinition>(
                effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
            );

        var filtered = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effect in
                effectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effect?.EffectKind == BattleEffectKind.Status
                && effect.StatusId == profile.ReadinessStatusId
            )
            {
                continue;
            }
            filtered.Add(effect);
        }
        return filtered;
    }

    private void PopulateRangedWeaponReactionThreatMetrics(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        SkillDefinition skillDefinition
    )
    {
        CombatRangedWeaponReactionDefinition profile =
            skillDefinition?.CombatProfile?.RangedWeaponReaction;
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (scoreInput == null || profile == null || state == null || actor == null)
            return;

        int skillLevel = Math.Max(
            actor.GetKnownSkillLevelTyped(skillDefinition.SkillId, fallback: 0),
            0
        );
        CombatEffectDefinition readinessEffect = FindRangedWeaponReactionReadinessEffect(
            skillDefinition.CombatProfile,
            profile,
            skillLevel
        );
        if (readinessEffect == null)
            return;

        List<int> expectedShotDamage = EstimateRangedWeaponReactionShotDamage(
            context,
            actor,
            skillDefinition,
            profile,
            skillLevel,
            Math.Max(readinessEffect.DurationTu, 0)
        );
        if (expectedShotDamage.Count == 0)
            return;
        expectedShotDamage.Sort((left, right) => right.CompareTo(left));

        int proposedOrbCount = Math.Max(readinessEffect.Power, 0);
        int proposedDuration = Math.Max(readinessEffect.DurationTu, 0);
        int proposedResponseCount = Math.Min(proposedOrbCount, expectedShotDamage.Count);
        int proposedValue = SumFirst(expectedShotDamage, proposedResponseCount);

        BattleStatusEffectState existing = actor.GetStatusEffect(profile.ReadinessStatusId);
        int existingOrbCount = existing != null
            && existing.source_skill_id == skillDefinition.SkillId
                ? Math.Max(existing.stacks, 0)
                : 0;
        int existingDuration = existingOrbCount > 0 ? Math.Max(existing.duration, 0) : 0;
        int existingResponseCount = Math.Min(existingOrbCount, expectedShotDamage.Count);
        int existingValue = SumFirst(expectedShotDamage, existingResponseCount);
        if (proposedDuration > 0 && existingDuration < proposedDuration)
        {
            existingValue = (int)Math.Clamp(
                (long)existingValue * existingDuration / proposedDuration,
                0L,
                int.MaxValue
            );
        }
        int marginalExpectedDamage = Math.Max(proposedValue - existingValue, 0);
        if (marginalExpectedDamage <= 0)
            return;

        scoreInput.estimated_damage += marginalExpectedDamage;
        scoreInput.estimated_enemy_damage += marginalExpectedDamage;
        scoreInput.enemy_target_count += proposedResponseCount;
        scoreInput.ally_target_count += 1;
        scoreInput.effective_target_count += 1;
        scoreInput.hit_payoff_score +=
            marginalExpectedDamage * Math.Max(_scoreProfile?.DamageWeight ?? 0, 0);
    }

    private static CombatEffectDefinition FindRangedWeaponReactionReadinessEffect(
        CombatSkillDefinition combatProfile,
        CombatRangedWeaponReactionDefinition profile,
        int skillLevel
    )
    {
        foreach (
            CombatEffectDefinition effect in
                combatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effect?.EffectKind == BattleEffectKind.Status
                && effect.StatusId == profile.ReadinessStatusId
                && effect.IsUnlockedAtSkillLevel(skillLevel)
            )
            {
                return effect;
            }
        }
        return null;
    }

    private static List<int> EstimateRangedWeaponReactionShotDamage(
        IBattleAiScoreContext context,
        BattleUnitState actor,
        SkillDefinition reactionSkill,
        CombatRangedWeaponReactionDefinition profile,
        int reactionSkillLevel,
        int durationTu
    )
    {
        var estimates = new List<int>();
        BattleState state = ContextState(context);
        if (state == null || durationTu <= 0)
            return estimates;

        using var hitResolver = new BattleHitResolver();
        foreach (BattleUnitState hostile in state.GetUnitsTyped())
        {
            if (
                hostile?.IsAlive() != true
                || hostile.faction_id == ""
                || actor.faction_id == ""
                || hostile.faction_id == actor.faction_id
            )
            {
                continue;
            }
            BattleWeaponProjectionValues weapon =
                hostile.GetWeaponProjectionReadViewTyped().Values;
            BattleWeaponDiceValues weaponDice = weapon.ActiveDice;
            if (
                weapon.RangeType != new StringName("ranged")
                || !profile.SupportsWeaponFamily(weapon.Family)
                || !weaponDice.HasUsableDice
                || BattleGridDistanceService.GetDistanceBetweenUnits(hostile, actor)
                    > Math.Max(weapon.AttackRange, 0)
            )
            {
                continue;
            }

            int weaponDiceMultiplier = ResolveThreateningMainWeaponDiceMultiplier(
                context,
                hostile,
                profile,
                durationTu
            );
            int diceCount = (int)Math.Clamp(
                (long)Math.Max(weaponDice.DiceCount, 0) * weaponDiceMultiplier,
                0L,
                int.MaxValue
            );
            int averageDamage = diceCount * (Math.Max(weaponDice.DiceSides, 0) + 1) / 2;
            AttackCheckInput rawCheck = hitResolver.BuildSkillDefinitionAttackCheck(
                actor,
                hostile,
                reactionSkill,
                profile.GetAttackRollBonus(reactionSkillLevel),
                0
            );
            AttackCheckInput fateAwareCheck = hitResolver.BuildFateAwareAttackCheckPreview(
                state,
                actor,
                hostile,
                rawCheck
            );
            int expectedDamage = (int)Math.Clamp(
                (long)Math.Max(averageDamage, 0)
                    * Mathf.Clamp(fateAwareCheck.SuccessRatePercent, 0, 100)
                    / 100L,
                0L,
                int.MaxValue
            );
            if (expectedDamage > 0)
                estimates.Add(expectedDamage);
        }
        return estimates;
    }

    private static int ResolveThreateningMainWeaponDiceMultiplier(
        IBattleAiScoreContext context,
        BattleUnitState hostile,
        CombatRangedWeaponReactionDefinition reactionProfile,
        int durationTu
    )
    {
        int bestMultiplier = 1;
        IReadOnlyDictionary<StringName, SkillDefinition> definitions =
            ContextSkillDefinitions(context);
        foreach (StringName skillId in hostile.GetKnownActiveSkillIdsTyped())
        {
            if (hostile.GetCooldownTyped(skillId) > durationTu)
                continue;
            SkillDefinition skill = GetSkillDefinition(definitions, skillId);
            CombatSkillDefinition combat = skill?.CombatProfile;
            if (
                combat == null
                || combat.TargetFilterKind is BattleTargetFilter.Ally or BattleTargetFilter.Self
                || !reactionProfile.SupportsWeaponFamily(
                    hostile.GetWeaponProjectionReadViewTyped().Values.Family
                )
            )
            {
                continue;
            }
            int level = Math.Max(hostile.GetKnownSkillLevelTyped(skillId, fallback: 0), 0);
            foreach (CombatEffectDefinition effect in combat.EffectDefinitions)
            {
                if (
                    effect?.EffectKind == BattleEffectKind.Damage
                    && effect.AddWeaponDice
                    && effect.IsUnlockedAtSkillLevel(level)
                )
                {
                    bestMultiplier = Math.Max(bestMultiplier, effect.WeaponDiceMultiplier);
                    break;
                }
            }
        }
        return bestMultiplier;
    }

    private static int SumFirst(IReadOnlyList<int> values, int count)
    {
        long total = 0;
        for (int index = 0; index < Math.Min(Math.Max(count, 0), values.Count); index++)
            total += Math.Max(values[index], 0);
        return (int)Math.Clamp(total, 0L, int.MaxValue);
    }
}
