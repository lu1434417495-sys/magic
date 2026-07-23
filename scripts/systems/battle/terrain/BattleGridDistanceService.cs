using System;
using Godot;

internal static class BattleGridDistanceService
{
    public static int GetDistance(Vector2I first_coord, Vector2I second_coord)
    {
        return Math.Abs(first_coord.X - second_coord.X) + Math.Abs(first_coord.Y - second_coord.Y);
    }

    public static int GetDistanceFromUnitToCoord(
        BattleUnitState unit_state,
        Vector2I target_coord
    )
    {
        if (unit_state == null)
        {
            return 999999;
        }
        int bestDistance = 999999;
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            bestDistance = Math.Min(bestDistance, GetDistance(occupiedCoord, target_coord));
        }
        return bestDistance;
    }

    public static int GetDistanceBetweenUnits(
        BattleUnitState first_unit,
        BattleUnitState second_unit
    )
    {
        if (first_unit == null || second_unit == null)
        {
            return 999999;
        }
        int bestDistance = 999999;
        foreach (Vector2I firstCoord in first_unit.occupied_coords)
        {
            foreach (Vector2I secondCoord in second_unit.occupied_coords)
            {
                bestDistance = Math.Min(bestDistance, GetDistance(firstCoord, secondCoord));
            }
        }
        return bestDistance;
    }

}
