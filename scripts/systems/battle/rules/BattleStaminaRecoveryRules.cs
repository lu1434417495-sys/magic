using System;
using Godot;

internal static class BattleStaminaRecoveryRules
{
    internal const int TuGranularity = 5;
    internal const int ProgressDenominator = 10;

    private const int ProgressBase = 11;
    private const int RestingRecoveryMultiplier = 2;

    internal static int ResolveTickCount(int tuDelta) =>
        Math.Max(tuDelta, 0) / TuGranularity;

    internal static int ResolveProgressGainPerTick(
        BattleUnitState unitState,
        bool isResting
    )
    {
        int progressGain = ProgressBase + ResolveConstitution(unitState);
        progressGain = ApplyRecoveryPercentBonus(unitState, progressGain);
        return isResting
            ? progressGain * RestingRecoveryMultiplier
            : progressGain;
    }

    internal static int ResolveSnapshotStaminaMax(BattleUnitState unitState)
    {
        return unitState?.attribute_snapshot == null
            ? 0
            : Math.Max(
                unitState.attribute_snapshot.GetValue(
                    AttributeService.ToStringName(
                        AttributeIdKind.StaminaMax
                    )
                ),
                0
            );
    }

    private static int ResolveConstitution(BattleUnitState unitState)
    {
        return unitState?.attribute_snapshot == null
            ? 0
            : Math.Max(
                unitState.attribute_snapshot.GetValue(
                    UnitBaseAttributes.ToStringName(
                        UnitBaseAttributeKind.Constitution
                    )
                ),
                0
            );
    }

    private static int ApplyRecoveryPercentBonus(
        BattleUnitState unitState,
        int baseProgressGain
    )
    {
        if (unitState?.attribute_snapshot == null)
        {
            return baseProgressGain;
        }
        int percentBonus = Math.Max(
            unitState.attribute_snapshot.GetValue(
                AttributeService.ToStringName(
                    AttributeIdKind.StaminaRecoveryPercentBonus
                )
            ),
            0
        );
        return percentBonus <= 0
            ? baseProgressGain
            : baseProgressGain * (100 + percentBonus) / 100;
    }
}
