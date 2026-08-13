using System;
using System.Collections.Generic;

public sealed partial class BattleAiScoreService
{
    internal static IReadOnlyList<CombatEffectDefinition> BuildLineThroughAttackExpectedEffects(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        CombatLineThroughAttackDefinition profile,
        int skillLevel,
        int targetIndex,
        int targetCount,
        AttackPreviewData hitPreview
    )
    {
        if (profile == null || targetCount <= 0 || targetIndex < 0 || targetIndex >= targetCount)
            return Array.Empty<CombatEffectDefinition>();

        int intermediateCount = Math.Max(targetCount - 1, 0);
        if (targetIndex < intermediateCount)
        {
            int hitRate = ReadStageHitRate(hitPreview, targetIndex);
            return ScaleLineThroughAttackEffects(
                effectDefinitions,
                profile.IntermediateWeaponDiceMultiplier,
                hitRate / 100.0
            );
        }

        double expectedSuccessfulWeaponDice = 0.0;
        int successCap = profile.GetSuccessfulIntermediateHitBonusCap(skillLevel);
        for (int successCount = 0; successCount <= successCap; successCount++)
        {
            int stageIndex = intermediateCount + successCount;
            if (hitPreview?.Stages == null || stageIndex >= hitPreview.Stages.Count)
                continue;
            AttackPreviewStage stage = hitPreview.Stages[stageIndex];
            expectedSuccessfulWeaponDice +=
                stage.ReachProbabilityBasisPoints / 10000.0
                * stage.HitRatePercent / 100.0
                * profile.GetPrimaryWeaponDiceMultiplier(skillLevel, successCount);
        }
        return ScaleLineThroughAttackEffects(
            effectDefinitions,
            1,
            expectedSuccessfulWeaponDice
        );
    }

    private static IReadOnlyList<CombatEffectDefinition> ScaleLineThroughAttackEffects(
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        int weaponDiceMultiplier,
        double expectedResolutionMultiplier
    )
    {
        var result = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effect in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effect == null)
                continue;
            CombatEffectDefinition scaled =
                effect.EffectKind == BattleEffectKind.Damage
                    ? effect
                        .WithWeaponDiceMultiplier(Math.Max(weaponDiceMultiplier, 1))
                        .WithPreResistanceDamageMultiplier(
                            effect.PreResistanceDamageMultiplier
                                * Math.Max(expectedResolutionMultiplier, 0.0),
                            Math.Max(weaponDiceMultiplier, 1)
                        )
                    : effect;
            result.Add(scaled);
        }
        return result.AsReadOnly();
    }

    private static int ReadStageHitRate(AttackPreviewData hitPreview, int stageIndex)
    {
        if (
            hitPreview?.Stages == null
            || stageIndex < 0
            || stageIndex >= hitPreview.Stages.Count
        )
        {
            return 0;
        }
        return Math.Clamp(hitPreview.Stages[stageIndex].HitRatePercent, 0, 100);
    }
}
