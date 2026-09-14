using System;
using System.Collections.Generic;
using Godot;

internal static class BattleWeightedStatusOutcomeRules
{
    internal static int GetTotalWeight(
        IReadOnlyList<CombatWeightedStatusOutcomeDefinition> outcomes
    )
    {
        long total = 0;
        foreach (
            CombatWeightedStatusOutcomeDefinition outcome in
                outcomes ?? Array.Empty<CombatWeightedStatusOutcomeDefinition>()
        )
        {
            if (outcome != null && outcome.StatusEffect != null && outcome.Weight > 0)
                total += outcome.Weight;
        }
        return (int)Math.Clamp(total, 0L, int.MaxValue);
    }

    internal static CombatWeightedStatusOutcomeDefinition SelectByRoll(
        IReadOnlyList<CombatWeightedStatusOutcomeDefinition> outcomes,
        int oneBasedRoll
    )
    {
        int totalWeight = GetTotalWeight(outcomes);
        if (totalWeight <= 0)
            return null;

        int remaining = Math.Clamp(oneBasedRoll, 1, totalWeight);
        foreach (
            CombatWeightedStatusOutcomeDefinition outcome in
                outcomes ?? Array.Empty<CombatWeightedStatusOutcomeDefinition>()
        )
        {
            if (outcome == null || outcome.StatusEffect == null || outcome.Weight <= 0)
                continue;
            remaining -= outcome.Weight;
            if (remaining <= 0)
                return outcome;
        }
        return null;
    }

    internal static IReadOnlyList<BattleWeightedStatusOutcomePreviewData> BuildPreview(
        IReadOnlyList<CombatWeightedStatusOutcomeDefinition> outcomes,
        int saveFailureProbabilityBasisPoints
    )
    {
        int totalWeight = GetTotalWeight(outcomes);
        if (totalWeight <= 0)
            return Array.Empty<BattleWeightedStatusOutcomePreviewData>();

        var result = new List<BattleWeightedStatusOutcomePreviewData>();
        int remainingConditionalBasisPoints = 10000;
        int remainingApplicationBasisPoints = Math.Clamp(
            saveFailureProbabilityBasisPoints,
            0,
            10000
        );
        int remainingWeight = totalWeight;
        foreach (
            CombatWeightedStatusOutcomeDefinition outcome in
                outcomes ?? Array.Empty<CombatWeightedStatusOutcomeDefinition>()
        )
        {
            CombatEffectDefinition statusEffect = outcome?.StatusEffect;
            if (statusEffect == null || outcome.Weight <= 0)
                continue;
            bool consumesRemainingWeight = outcome.Weight == remainingWeight;
            int conditionalBasisPoints =
                consumesRemainingWeight
                    ? remainingConditionalBasisPoints
                    : (int)Math.Clamp(
                        Math.Round(
                            outcome.Weight * 10000.0 / totalWeight,
                            MidpointRounding.AwayFromZero
                        ),
                        0.0,
                        remainingConditionalBasisPoints
                    );
            remainingConditionalBasisPoints -= conditionalBasisPoints;
            int applicationBasisPoints =
                consumesRemainingWeight
                    ? remainingApplicationBasisPoints
                    : (int)Math.Clamp(
                        Math.Round(
                            Math.Clamp(saveFailureProbabilityBasisPoints, 0, 10000)
                                * outcome.Weight
                                / (double)totalWeight,
                            MidpointRounding.AwayFromZero
                        ),
                        0.0,
                        remainingApplicationBasisPoints
                    );
            remainingApplicationBasisPoints -= applicationBasisPoints;
            StringName statusId = ProgressionDataUtils.to_string_name(
                statusEffect.StatusId
            );
            string displayName = statusEffect.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = BattleStatusSemanticTable.GetDisplayLabel(statusId);
            result.Add(
                new BattleWeightedStatusOutcomePreviewData(
                    outcome.OutcomeId,
                    statusId,
                    displayName,
                    outcome.Weight,
                    totalWeight,
                    conditionalBasisPoints,
                    applicationBasisPoints,
                    statusEffect.DurationTu,
                    statusEffect.Power,
                    statusEffect.AttackRollPenalty,
                    statusEffect.LockCounterattack,
                    statusEffect.LockGuard,
                    statusEffect.LockDodgeBonus,
                    statusEffect.LockCrit
                )
            );
            remainingWeight -= outcome.Weight;
        }
        return result.AsReadOnly();
    }
}
