using System;
using Godot;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class BattleGridDistanceService : RefCounted
{
    public static int get_distance(Vector2I first_coord, Vector2I second_coord)
    {
        return Math.Abs(first_coord.X - second_coord.X) + Math.Abs(first_coord.Y - second_coord.Y);
    }

    public static int get_distance_from_unit_to_coord(BattleUnitState unit_state, Vector2I target_coord)
    {
        if (unit_state == null)
        {
            return 999999;
        }
        unit_state.refresh_footprint();
        int bestDistance = 999999;
        foreach (Vector2I occupiedCoord in unit_state.occupied_coords)
        {
            bestDistance = Math.Min(bestDistance, get_distance(occupiedCoord, target_coord));
        }
        return bestDistance;
    }

    public static int get_distance_between_units(BattleUnitState first_unit, BattleUnitState second_unit)
    {
        if (first_unit == null || second_unit == null)
        {
            return 999999;
        }
        first_unit.refresh_footprint();
        second_unit.refresh_footprint();
        int bestDistance = 999999;
        foreach (Vector2I firstCoord in first_unit.occupied_coords)
        {
            foreach (Vector2I secondCoord in second_unit.occupied_coords)
            {
                bestDistance = Math.Min(bestDistance, get_distance(firstCoord, secondCoord));
            }
        }
        return bestDistance;
    }

    public static GArray get_unit_footprint_coords(BattleUnitState unit_state)
    {
        if (unit_state == null)
        {
            return new GArray();
        }
        unit_state.refresh_footprint();
        return DuplicateArray(unit_state.occupied_coords);
    }

    private static GArray DuplicateArray(Godot.Collections.Array<Vector2I> values)
    {
        var result = new GArray();
        foreach (Vector2I value in values ?? new Godot.Collections.Array<Vector2I>())
        {
            result.Add(value);
        }
        return result;
    }

}
