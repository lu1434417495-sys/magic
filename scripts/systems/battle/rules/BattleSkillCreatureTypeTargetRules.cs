using Godot;

internal static class BattleSkillCreatureTypeTargetRules
{
    internal static bool Allows(
        CombatSkillDefinition combatProfile,
        BattleUnitState targetUnit
    )
    {
        if (targetUnit == null)
            return false;
        foreach (
            StringName excludedTag
            in combatProfile?.ExcludedTargetCreatureTypeTags
                ?? System.Array.Empty<StringName>()
        )
        {
            if (excludedTag != "" && targetUnit.HasCreatureTypeTag(excludedTag))
                return false;
        }
        return true;
    }

    internal static bool Allows(
        CombatSkillDefinition combatProfile,
        BattleUnitReadView targetUnit
    )
    {
        if (!targetUnit.IsValid)
            return false;
        foreach (
            StringName excludedTag
            in combatProfile?.ExcludedTargetCreatureTypeTags
                ?? System.Array.Empty<StringName>()
        )
        {
            if (excludedTag != "" && targetUnit.HasCreatureTypeTag(excludedTag))
                return false;
        }
        return true;
    }
}
