using System;
using System.Collections.Generic;
using Godot;

public partial class BattleAiScoreService
{
    // A bounded next-activation estimate, using canonical hit probabilities and damage budgets.
    // Longer windows do not multiply this score by an assumed number of future attacks.
    private void PopulateIncomingAttackDefenseMetrics(
        BattleAiScoreInput input, IBattleAiScoreContext context,
        IReadOnlyList<CombatEffectDefinition> effects)
    {
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (input == null || input.preview?.allowed != true || state == null || actor == null)
            return;
        using var hit = new BattleHitResolver();
        using var resolution = new BattleSkillResolutionRules();
        foreach (StringName id in input.target_unit_ids)
        {
            BattleUnitState defender = GetUnit(state, id);
            if (defender?.IsAlive() != true || defender.faction_id != actor.faction_id)
                continue;
            var proposed = defender.DuplicateForPreview();
            int duration = 0;
            foreach (CombatEffectDefinition effect in effects ?? Array.Empty<CombatEffectDefinition>())
            {
                if (effect?.IncomingAttackRollDisadvantage != true
                    || !IsUnitValidForEffect(actor, defender, ResolveEffectTargetFilter(null, effect), effect))
                    continue;
                duration = Math.Max(duration, effect.DurationTu);
                BattleStatusEffectState existing = proposed.GetStatusEffect(effect.StatusId);
                proposed.SetStatusEffect(new BattleStatusEffectState
                {
                    status_id = effect.StatusId, source_unit_id = actor.unit_id,
                    incoming_attack_roll_disadvantage = true,
                    duration = Math.Max(effect.DurationTu, existing?.duration ?? 0),
                    power = 1, stacks = 1,
                });
            }
            if (duration <= 0)
                continue;
            int targetRelief = 0;
            foreach (BattleUnitState threat in GetHostileThreatUnitsForActor(context))
            {
                if (threat?.IsAlive() != true)
                    continue;
                int ready = ResolveThreatReadyInTu(state, threat);
                if (ready >= duration)
                    continue;
                BattleUnitState projectedThreat = BuildThreatActivationProjection(threat, ready);
                BattleUnitState before = defender.DuplicateForPreview();
                BattleUnitState after = proposed.DuplicateForPreview();
                if (ready > 0)
                {
                    BattleStatusDurationRules.AdvanceUnitProjection(before, ready);
                    BattleStatusDurationRules.AdvanceUnitProjection(after, ready);
                }
                int bestRelief = 0;
                var candidates = new HashSet<StringName>();
                foreach (StringName knownSkill in threat.GetKnownActiveSkillsViewTyped())
                    candidates.Add(knownSkill);
                if (context.basic_attack_skill_id != new StringName(""))
                    candidates.Add(context.basic_attack_skill_id);
                foreach (StringName skillId in candidates)
                {
                    SkillDefinition skill = GetSkillDefinition(ContextSkillDefinitions(context), skillId);
                    if (skill?.CombatProfile == null
                        || !CanThreatUseSkillAtActivation(context, threat, projectedThreat, skill, ready)
                        || resolution.IsForceHitNoCritSkill(skill, projectedThreat))
                        continue;
                    var damageEffects = CollectRoleThreatEffectDefinitions(
                        projectedThreat, skill, ContextSkillCatalog(context));
                    if (!IsDamageSkill(damageEffects)
                        || !resolution.ShouldResolveUnitSkillAsFateAttack(
                            projectedThreat, before, skill, damageEffects))
                        continue;
                    int distance = DistanceBetweenUnits(context, threat, defender);
                    int range = BattleRangeService.GetEffectiveSkillThreatRange(
                        projectedThreat, skill, ContextSkillCatalog(context));
                    if (range <= 0 || distance < 0 || distance > range)
                        continue;
                    AttackCheckInput beforeCheck = hit.BuildFateAwareAttackCheckPreview(state,
                        projectedThreat, before, hit.BuildSkillAttackCheck(projectedThreat, before, skill));
                    AttackCheckInput afterCheck = hit.BuildFateAwareAttackCheckPreview(state,
                        projectedThreat, after, hit.BuildSkillAttackCheck(projectedThreat, after, skill));
                    if (beforeCheck.Invalid || afterCheck.Invalid)
                        continue;
                    int chanceDelta = Math.Max(beforeCheck.SuccessRatePercent - afterCheck.SuccessRatePercent, 0);
                    int damage = Math.Max(EstimateDamageForTargetResult(
                        projectedThreat, damageEffects, before, skillId, state)?.IncomingBudgetDamage ?? 0, 0);
                    bestRelief = Math.Max(bestRelief, (int)Math.Clamp(
                        (long)damage * chanceDelta / 100, 0L, int.MaxValue));
                }
                targetRelief += bestRelief;
            }
            if (targetRelief > 0)
            {
                input.estimated_incoming_attack_damage_relief += targetRelief;
                input.ally_target_count++;
                input.effective_target_count++;
            }
        }
        input.hit_payoff_score += input.estimated_incoming_attack_damage_relief
            * Math.Max(_scoreProfile.DamageWeight, 0);
    }
}
