using System.Collections.Generic;
using Godot;

internal static class BattleEffectTargetRequirementRules
{
    internal static bool AllowsDeadUnitTarget(CombatEffectDefinition effectDefinition) =>
        effectDefinition?.EffectKind == BattleEffectKind.HealFatal;

    internal static bool AllowsDeadUnitTarget(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (CombatEffectDefinition effectDefinition in
            effectDefinitions ?? System.Array.Empty<CombatEffectDefinition>())
        {
            if (AllowsDeadUnitTarget(effectDefinition))
                return true;
        }
        return false;
    }

    internal static bool IsSatisfied(
        CombatEffectDefinition effectDefinition,
        BattleUnitState targetUnit
    )
    {
        StringName requiredTag =
            effectDefinition?.RequiredTargetCreatureTypeTag ?? new StringName("");
        if (
            requiredTag != ""
            && targetUnit?.HasCreatureTypeTag(requiredTag) != true
        )
        {
            return false;
        }
        BattleCognitionKind minimum =
            effectDefinition?.RequiredTargetMinCognition
            ?? BattleCognitionKind.Unknown;
        return !BattleCognitionContentRules.IsKnown(minimum)
            || BattleCognitionRules.MeetsMinimum(
                targetUnit,
                minimum
            );
    }

    internal static bool IsSatisfied(
        CombatEffectDefinition effectDefinition,
        BattleUnitReadView targetUnit
    )
    {
        StringName requiredTag =
            effectDefinition?.RequiredTargetCreatureTypeTag ?? new StringName("");
        if (
            requiredTag != ""
            && !targetUnit.HasCreatureTypeTag(requiredTag)
        )
        {
            return false;
        }
        BattleCognitionKind minimum =
            effectDefinition?.RequiredTargetMinCognition
            ?? BattleCognitionKind.Unknown;
        return !BattleCognitionContentRules.IsKnown(minimum)
            || BattleCognitionRules.MeetsMinimum(
                targetUnit,
                minimum
            );
    }
}
