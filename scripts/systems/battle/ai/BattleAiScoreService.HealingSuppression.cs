using System;
using System.Collections.Generic;
using Godot;

public partial class BattleAiScoreService
{
    private void PopulateHealingSuppressionMetrics(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context
    )
    {
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (
            scoreInput?.preview == null
            || state == null
            || actor == null
            || scoreInput.preview.StatusContributionPreviewsTyped.Count == 0
        )
        {
            return;
        }

        foreach (
            BattleStatusContributionPreviewData preview
            in scoreInput.preview.StatusContributionPreviewsTyped
        )
        {
            if (
                preview?.HealMultiplierPercent is not int incomingMultiplierPercent
                || incomingMultiplierPercent >= 100
                || preview.ResultDurationTu <= 0
            )
            {
                continue;
            }
            BattleUnitState targetUnit = GetUnit(state, preview.TargetUnitId);
            if (
                targetUnit == null
                || !targetUnit.IsAlive()
                || !FactionHasHealingSkill(context, targetUnit.faction_id)
            )
            {
                continue;
            }

            int marginalBasisPoints = EstimateMarginalHealingSuppressionBasisPoints(
                targetUnit,
                preview.StatusId,
                incomingMultiplierPercent,
                preview.ResultDurationTu
            );
            if (marginalBasisPoints <= 0)
                continue;

            int healableHp = EstimateHealableHpAfterAction(scoreInput, targetUnit);
            if (healableHp <= 0)
                continue;
            int hitRatePercent = Math.Clamp(scoreInput.estimated_hit_rate_percent, 0, 100);
            int deniedHealing = RoundToInt(
                (double)healableHp
                    * marginalBasisPoints
                    / 10000.0
                    * hitRatePercent
                    / 100.0
            );
            if (deniedHealing <= 0)
                continue;

            bool isAlly = targetUnit.faction_id == actor.faction_id;
            if (isAlly)
            {
                scoreInput.estimated_ally_healing_denied += deniedHealing;
                scoreInput.hit_payoff_score -= deniedHealing * _scoreProfile.HealWeight;
            }
            else
            {
                scoreInput.estimated_enemy_healing_denied += deniedHealing;
                scoreInput.hit_payoff_score += deniedHealing * _scoreProfile.HealWeight;
            }
            if (scoreInput.effective_target_count <= 0)
                scoreInput.effective_target_count = 1;
        }
    }

    private bool FactionHasHealingSkill(
        IBattleAiScoreContext context,
        StringName factionId
    )
    {
        BattleState state = ContextState(context);
        if (state == null)
            return false;
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
            ContextSkillDefinitions(context);
        foreach (BattleUnitState unit in state.GetUnitsTyped())
        {
            if (unit == null || !unit.IsAlive() || unit.faction_id != factionId)
                continue;
            foreach (StringName skillId in unit.GetKnownActiveSkillsViewTyped())
            {
                SkillDefinition skillDefinition = GetSkillDefinition(
                    skillDefinitions,
                    skillId
                );
                if (skillDefinition?.CombatProfile == null)
                    continue;
                foreach (
                    CombatEffectDefinition effect
                    in CollectRoleThreatEffectDefinitions(
                        unit,
                        skillDefinition,
                        ContextSkillCatalog(context)
                    )
                )
                {
                    if (effect?.EffectKind == BattleEffectKind.Heal)
                        return true;
                }
            }
        }
        return false;
    }

    private static int EstimateHealableHpAfterAction(
        BattleAiScoreInput scoreInput,
        BattleUnitState targetUnit
    )
    {
        int projectedHpDamage = 0;
        if (
            scoreInput.damage_estimates_by_target_id.TryGetValue(
                targetUnit.unit_id,
                out List<DamageEstimateBreakdown> estimates
            )
        )
        {
            foreach (DamageEstimateBreakdown estimate in estimates)
                projectedHpDamage += Math.Max(estimate?.HpDamage ?? 0, 0);
        }
        int missingHp = Math.Max(GetUnitMaxHp(targetUnit) - targetUnit.GetCurrentHp(), 0);
        return Math.Min(missingHp + projectedHpDamage, GetUnitMaxHp(targetUnit));
    }

    private static int EstimateMarginalHealingSuppressionBasisPoints(
        BattleUnitState targetUnit,
        StringName incomingStatusId,
        int incomingMultiplierPercent,
        int incomingDurationTu
    )
    {
        var boundaries = new SortedSet<int> { 0, incomingDurationTu };
        foreach (BattleStatusEffectState status in targetUnit.GetStatusEffectsTyped())
        {
            if (
                status != null
                && !status.IsEmpty()
                && status.duration > 0
                && status.duration < incomingDurationTu
                && status.TryGetHealMultiplierPercentTyped(out _)
            )
            {
                boundaries.Add(status.duration);
            }
        }

        long weightedMarginalPercentTu = 0;
        int previousBoundary = 0;
        foreach (int boundary in boundaries)
        {
            if (boundary <= previousBoundary)
                continue;
            int baselinePercent = ResolveTimelineHealMultiplierPercent(
                targetUnit,
                previousBoundary,
                "",
                100,
                0
            );
            int afterPercent = ResolveTimelineHealMultiplierPercent(
                targetUnit,
                previousBoundary,
                incomingStatusId,
                incomingMultiplierPercent,
                incomingDurationTu
            );
            weightedMarginalPercentTu +=
                (long)Math.Max(baselinePercent - afterPercent, 0)
                * (boundary - previousBoundary);
            previousBoundary = boundary;
        }
        return (int)Math.Clamp(
            weightedMarginalPercentTu * 100L / incomingDurationTu,
            0L,
            10000L
        );
    }

    private static int ResolveTimelineHealMultiplierPercent(
        BattleUnitState targetUnit,
        int elapsedTu,
        StringName replacementStatusId,
        int replacementMultiplierPercent,
        int replacementDurationTu
    )
    {
        int result = 100;
        foreach (BattleStatusEffectState status in targetUnit.GetStatusEffectsTyped())
        {
            if (
                status == null
                || status.IsEmpty()
                || (replacementStatusId != "" && status.status_id == replacementStatusId)
                || (status.duration >= 0 && status.duration <= elapsedTu)
                || !status.TryGetHealMultiplierPercentTyped(out int multiplierPercent)
            )
            {
                continue;
            }
            result = Math.Min(result, Math.Clamp(multiplierPercent, 0, 100));
        }
        if (replacementStatusId != "" && elapsedTu < replacementDurationTu)
        {
            result = Math.Min(
                result,
                Math.Clamp(replacementMultiplierPercent, 0, 100)
            );
        }
        return result;
    }
}
