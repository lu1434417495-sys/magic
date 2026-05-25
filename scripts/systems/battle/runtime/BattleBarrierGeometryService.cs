using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleBarrierGeometryService : RefCounted
{
    public GDictionary classify_footprint_transition(
        GodotObject state,
        GArray from_footprint,
        GArray to_footprint,
        GArray barrier_coords)
    {
        GDictionary barrierLookup = CoordLookup(barrier_coords);
        bool fromInside = FootprintOverlapsLookup(from_footprint, barrierLookup);
        bool toInside = FootprintOverlapsLookup(to_footprint, barrierLookup);
        return new GDictionary
        {
            ["crosses_boundary"] = fromInside != toInside,
            ["from_inside"] = fromInside,
            ["to_inside"] = toInside,
        };
    }

    public bool line_crosses_barrier_area(
        GodotObject state,
        Vector2I source_coord,
        Vector2I target_coord,
        GArray barrier_coords)
    {
        GDictionary barrierLookup = CoordLookup(barrier_coords);
        bool sourceInside = barrierLookup.ContainsKey(source_coord);
        bool targetInside = barrierLookup.ContainsKey(target_coord);
        if (sourceInside && targetInside)
        {
            return false;
        }
        if (sourceInside != targetInside)
        {
            return true;
        }
        foreach (Variant coordVariant in LineCoords(source_coord, target_coord))
        {
            Vector2I coord = coordVariant.AsVector2I();
            if (coord == source_coord || coord == target_coord)
            {
                continue;
            }
            if (barrierLookup.ContainsKey(coord))
            {
                return true;
            }
        }
        return false;
    }

    public bool coord_inside_barrier(Vector2I coord, GArray barrier_coords)
    {
        return CoordLookup(barrier_coords).ContainsKey(coord);
    }

    private static bool FootprintOverlapsLookup(GArray footprint, GDictionary lookup)
    {
        if (footprint == null || lookup == null)
        {
            return false;
        }
        foreach (Variant coordVariant in footprint)
        {
            if (coordVariant.VariantType == Variant.Type.Vector2I && lookup.ContainsKey(coordVariant.AsVector2I()))
            {
                return true;
            }
        }
        return false;
    }

    private static GDictionary CoordLookup(GArray coords)
    {
        GDictionary lookup = new();
        if (coords == null)
        {
            return lookup;
        }
        foreach (Variant coordVariant in coords)
        {
            if (coordVariant.VariantType == Variant.Type.Vector2I)
            {
                lookup[coordVariant.AsVector2I()] = true;
            }
        }
        return lookup;
    }

    private static GArray LineCoords(Vector2I fromCoord, Vector2I toCoord)
    {
        GArray coords = new();
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
