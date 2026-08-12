using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

public sealed class CombatDirectionalPiercingDefinition
{
    public CombatDirectionalPiercingDefinition(
        IReadOnlyList<int> baseDamagePercentCurve,
        int successfulHitDecayPercent,
        int minimumDamagePercent,
        int staminaFlatBase,
        int staminaRangeSquareCoefficient,
        int staminaStrengthSquareScale,
        int minimumStaminaCost,
        int maximumHeightDelta
    )
    {
        BaseDamagePercentCurve = new ReadOnlyCollection<int>(
            new List<int>(baseDamagePercentCurve ?? Array.Empty<int>())
        );
        SuccessfulHitDecayPercent = successfulHitDecayPercent;
        MinimumDamagePercent = minimumDamagePercent;
        StaminaFlatBase = staminaFlatBase;
        StaminaRangeSquareCoefficient = staminaRangeSquareCoefficient;
        StaminaStrengthSquareScale = staminaStrengthSquareScale;
        MinimumStaminaCost = minimumStaminaCost;
        MaximumHeightDelta = maximumHeightDelta;
    }

    public IReadOnlyList<int> BaseDamagePercentCurve { get; }
    public int SuccessfulHitDecayPercent { get; }
    public int MinimumDamagePercent { get; }
    public int StaminaFlatBase { get; }
    public int StaminaRangeSquareCoefficient { get; }
    public int StaminaStrengthSquareScale { get; }
    public int MinimumStaminaCost { get; }
    public int MaximumHeightDelta { get; }

    public int GetBaseDamagePercent(int skillLevel)
    {
        if (BaseDamagePercentCurve.Count == 0)
            return 100;
        return BaseDamagePercentCurve[
            Math.Clamp(skillLevel, 0, BaseDamagePercentCurve.Count - 1)
        ];
    }
}
