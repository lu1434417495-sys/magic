using System;
using System.Collections.Generic;
using Godot;

public readonly record struct BattleBarrierFootprintTransition(
    bool CrossesBoundary,
    bool FromInside,
    bool ToInside
);

public static class BattleBarrierGeometryService
{
    public static BattleBarrierFootprintTransition ClassifyFootprintTransition(
        IEnumerable<Vector2I> fromFootprint,
        IEnumerable<Vector2I> toFootprint,
        IEnumerable<Vector2I> barrierCoords
    )
    {
        HashSet<Vector2I> barrierLookup = CoordLookup(barrierCoords);
        bool fromInside = FootprintOverlapsLookup(fromFootprint, barrierLookup);
        bool toInside = FootprintOverlapsLookup(toFootprint, barrierLookup);
        return new BattleBarrierFootprintTransition(fromInside != toInside, fromInside, toInside);
    }

    public static bool LineCrossesBarrierArea(
        Vector2I sourceCoord,
        Vector2I targetCoord,
        IEnumerable<Vector2I> barrierCoords
    )
    {
        HashSet<Vector2I> barrierLookup = CoordLookup(barrierCoords);
        bool sourceInside = barrierLookup.Contains(sourceCoord);
        bool targetInside = barrierLookup.Contains(targetCoord);
        if (sourceInside && targetInside)
        {
            return false;
        }
        if (sourceInside != targetInside)
        {
            return true;
        }
        foreach (Vector2I coord in LineCoords(sourceCoord, targetCoord))
        {
            if (coord == sourceCoord || coord == targetCoord)
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

    public static bool CoordInsideBarrier(
        Vector2I coord,
        IEnumerable<Vector2I> barrierCoords
    ) => CoordLookup(barrierCoords).Contains(coord);

    private static bool FootprintOverlapsLookup(
        IEnumerable<Vector2I> footprint,
        HashSet<Vector2I> lookup
    )
    {
        if (footprint == null || lookup == null)
        {
            return false;
        }
        foreach (Vector2I coord in footprint)
        {
            if (lookup.Contains(coord))
            {
                return true;
            }
        }
        return false;
    }

    private static HashSet<Vector2I> CoordLookup(IEnumerable<Vector2I> coords)
    {
        HashSet<Vector2I> lookup = new();
        if (coords == null)
        {
            return lookup;
        }
        foreach (Vector2I coord in coords)
        {
            lookup.Add(coord);
        }
        return lookup;
    }

    private static IEnumerable<Vector2I> LineCoords(Vector2I fromCoord, Vector2I toCoord)
    {
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
            yield return new Vector2I(x0, y0);
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
    }
}
