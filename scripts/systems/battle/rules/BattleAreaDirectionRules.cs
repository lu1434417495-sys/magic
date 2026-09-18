using Godot;

internal static class BattleAreaDirectionRules
{
    internal static Vector2I ResolveAreaDirection(
        CombatSkillDefinition combatProfile,
        Vector2I targetVector
    )
    {
        if (
            combatProfile?.AreaDirectionModeKind
            != CombatAreaDirectionMode.TargetVectorPerpendicular
        )
        {
            return targetVector;
        }
        if (targetVector == Vector2I.Zero)
        {
            return Vector2I.Zero;
        }
        return new Vector2I(-targetVector.Y, targetVector.X);
    }
}
