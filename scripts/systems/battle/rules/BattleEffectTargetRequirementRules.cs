using Godot;

internal static class BattleEffectTargetRequirementRules
{
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
