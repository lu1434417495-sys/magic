using System;
using System.Collections.Generic;

public sealed partial class BattleAiScoreService
{
    internal static IReadOnlyList<CombatEffectDefinition> BuildSequentialLineHitExpectedEffects(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        int targetIndex,
        AttackPreviewData hitPreview
    )
    {
        if (
            targetIndex < 0
            || hitPreview?.Stages == null
            || targetIndex >= hitPreview.Stages.Count
        )
        {
            return Array.Empty<CombatEffectDefinition>();
        }
        AttackPreviewStage stage = hitPreview.Stages[targetIndex];
        double expectedResolutionMultiplier =
            Math.Clamp(stage.ReachProbabilityBasisPoints, 0, 10000) / 10000.0
            * Math.Clamp(stage.HitRatePercent, 0, 100) / 100.0;
        var result = new List<CombatEffectDefinition>();
        foreach (
            CombatEffectDefinition effect in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effect == null)
                continue;
            result.Add(
                effect.EffectKind == BattleEffectKind.Damage
                    ? effect.WithPreResistanceDamageMultiplier(
                        effect.PreResistanceDamageMultiplier
                            * expectedResolutionMultiplier,
                        1
                    )
                    : effect
            );
        }
        return result.AsReadOnly();
    }
}
