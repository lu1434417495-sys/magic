using Godot;

/// <summary>
/// Shared exit-zone geometry for objective runtime states that freeze a set of
/// exit cells (escape, escort, intercept). Keeping the "fully inside exit" test
/// in one place avoids the copy-paste that previously lived in the evaluation
/// service, the HUD snapshot builder and the objective AI evaluator.
/// </summary>
internal interface IBattleExitObjective
{
    bool ContainsExitCoord(Vector2I coord);
}

internal static class BattleExitObjectiveRules
{
    internal static bool IsUnitFullyInsideExit(
        BattleUnitState unit,
        IBattleExitObjective exitObjective
    )
    {
        BattleUnitGeometryReadView geometry =
            unit?.GetGeometryReadViewTyped()
            ?? BattleUnitGeometryReadView.MissingOwner;
        if (
            unit == null
            || !geometry.OwnerPresent
            || !geometry.OccupiedCoords.IsPresent
            || geometry.OccupiedCoords.Count == 0
            || exitObjective == null
        )
        {
            return false;
        }
        foreach (Vector2I occupiedCoord in geometry.OccupiedCoords)
        {
            if (!exitObjective.ContainsExitCoord(occupiedCoord))
                return false;
        }
        return true;
    }
}
