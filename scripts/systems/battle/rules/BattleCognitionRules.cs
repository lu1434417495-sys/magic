using System;

internal static class BattleCognitionRules
{
    internal static BattleCognitionKind ResolveEffective(
        BattleUnitState unit
    )
    {
        if (unit == null)
            return BattleCognitionKind.Unknown;

        BattleCognitionKind result = unit.GetBaseCognitionKindTyped();
        foreach (
            BattleStatusEffectState status
            in unit.GetStatusEffectsTyped()
        )
        {
            if (status == null)
                continue;
            BattleCognitionKind ceiling =
                BattleStatusSemanticTable.GetCognitionCeiling(
                    status.status_id
                );
            result = ApplyCeiling(result, ceiling);
        }
        foreach (
            BattleCognitionCeilingModifierReadView modifier
            in unit.GetCognitionCeilingModifiersReadViewTyped()
        )
        {
            if (modifier != null)
                result = ApplyCeiling(result, modifier.Ceiling);
        }
        return result;
    }

    internal static BattleCognitionKind ResolveEffective(
        BattleUnitReadView unit
    ) =>
        unit.IsValid
            ? ResolveEffective(unit.UnsafeUnitForReadOnlyRules)
            : BattleCognitionKind.Unknown;

    internal static bool MeetsMinimum(
        BattleUnitState unit,
        BattleCognitionKind minimum
    ) =>
        BattleCognitionContentRules.IsKnown(minimum)
        && ResolveEffective(unit) >= minimum;

    internal static bool MeetsMinimum(
        BattleUnitReadView unit,
        BattleCognitionKind minimum
    ) =>
        BattleCognitionContentRules.IsKnown(minimum)
        && ResolveEffective(unit) >= minimum;

    private static BattleCognitionKind ApplyCeiling(
        BattleCognitionKind current,
        BattleCognitionKind ceiling
    )
    {
        if (
            !BattleCognitionContentRules.IsKnown(current)
            || !BattleCognitionContentRules.IsKnown(ceiling)
        )
        {
            return current;
        }
        return (BattleCognitionKind)Math.Min(
            (int)current,
            (int)ceiling
        );
    }
}
