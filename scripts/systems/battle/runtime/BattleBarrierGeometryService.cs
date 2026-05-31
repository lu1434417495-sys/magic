using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public readonly record struct BattleBarrierFootprintTransition(
    bool CrossesBoundary,
    bool FromInside,
    bool ToInside
)
{
    public GDictionary ToDictionary() =>
        new()
        {
            ["crosses_boundary"] = CrossesBoundary,
            ["from_inside"] = FromInside,
            ["to_inside"] = ToInside,
        };
}

[GlobalClass]
public partial class BattleBarrierGeometryService : RefCounted
{
    public GDictionary classify_footprint_transition(
        BattleState state,
        GArray from_footprint,
        GArray to_footprint,
        GArray barrier_coords
    )
    {
        return ClassifyFootprintTransition(
            state,
            from_footprint,
            to_footprint,
            barrier_coords
        ).ToDictionary();
    }

    public BattleBarrierFootprintTransition ClassifyFootprintTransition(
        BattleState state,
        GArray fromFootprint,
        GArray toFootprint,
        GArray barrierCoords
    )
    {
        HashSet<Vector2I> barrierLookup = CoordLookup(barrierCoords);
        bool fromInside = FootprintOverlapsLookup(fromFootprint, barrierLookup);
        bool toInside = FootprintOverlapsLookup(toFootprint, barrierLookup);
        return new BattleBarrierFootprintTransition(fromInside != toInside, fromInside, toInside);
    }

    public bool line_crosses_barrier_area(
        BattleState state,
        Vector2I source_coord,
        Vector2I target_coord,
        GArray barrier_coords
    )
    {
        HashSet<Vector2I> barrierLookup = CoordLookup(barrier_coords);
        bool sourceInside = barrierLookup.Contains(source_coord);
        bool targetInside = barrierLookup.Contains(target_coord);
        if (sourceInside && targetInside)
        {
            return false;
        }
        if (sourceInside != targetInside)
        {
            return true;
        }
        foreach (Vector2I coord in LineCoords(source_coord, target_coord))
        {
            if (coord == source_coord || coord == target_coord)
            {
                continue;
            }
            if (barrierLookup.Contains(coord))
            {
                return true;
            }
        }
        return false;
    }

    public bool coord_inside_barrier(Vector2I coord, GArray barrier_coords)
    {
        return CoordLookup(barrier_coords).Contains(coord);
    }

    private static bool FootprintOverlapsLookup(GArray footprint, HashSet<Vector2I> lookup)
    {
        if (footprint == null || lookup == null)
        {
            return false;
        }
        foreach (var coordValue in footprint)
        {
            if (
                coordValue.VariantType == Variant.Type.Vector2I
                && lookup.Contains(coordValue.AsVector2I())
            )
            {
                return true;
            }
        }
        return false;
    }

    private static HashSet<Vector2I> CoordLookup(GArray coords)
    {
        HashSet<Vector2I> lookup = new();
        if (coords == null)
        {
            return lookup;
        }
        foreach (var coordValue in coords)
        {
            if (coordValue.VariantType == Variant.Type.Vector2I)
            {
                lookup.Add(coordValue.AsVector2I());
            }
        }
        return lookup;
    }

    private static List<Vector2I> LineCoords(Vector2I fromCoord, Vector2I toCoord)
    {
        List<Vector2I> coords = new();
        int x0 = fromCoord.X;
        int y0 = fromCoord.Y;
        int x1 = toCoord.X;
        int y1 = toCoord.Y;
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            coords.Add(new Vector2I(x0, y0));
            if (x0 == x1 && y0 == y1)
            {
                break;
            }
            int doubledError = 2 * error;
            if (doubledError >= dy)
            {
                error += dy;
                x0 += sx;
            }
            if (doubledError <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
        return coords;
    }
}
