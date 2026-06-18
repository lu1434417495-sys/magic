using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleVirtualBoardOverlay
{
    private readonly Dictionary<Vector2I, StringName> _coordOverrides = new();
    private readonly Dictionary<StringName, List<Vector2I>> _unitCoords = new();
    private readonly Dictionary<Vector2I, StringName> _blockedCoords = new();
    private readonly HashSet<StringName> _releasedUnits = new();

    public void ReleaseUnit(StringName unit_id)
    {
        StringName normalized = NormalizeStringName(unit_id);
        if (IsEmpty(normalized))
        {
            return;
        }
        _releasedUnits.Add(normalized);
        if (!_unitCoords.TryGetValue(normalized, out List<Vector2I> coords))
        {
            return;
        }

        foreach (Vector2I coord in coords)
            _coordOverrides.Remove(coord);
        _unitCoords.Remove(normalized);
    }

    public void PlaceUnit(StringName unit_id, Vector2I anchor_coord, Vector2I footprint_size)
    {
        StringName normalized = NormalizeStringName(unit_id);
        if (IsEmpty(normalized))
        {
            Fail("BattleVirtualBoardOverlay.place_unit requires unit_id.");
            return;
        }

        _releasedUnits.Remove(normalized);
        ReleaseUnit(normalized);
        _releasedUnits.Remove(normalized);

        var coords = new List<Vector2I>();
        Vector2I size = new(Math.Max(footprint_size.X, 1), Math.Max(footprint_size.Y, 1));
        for (int y = 0; y < size.Y; y++)
        {
            for (int x = 0; x < size.X; x++)
            {
                Vector2I coord = anchor_coord + new Vector2I(x, y);
                coords.Add(coord);
                _coordOverrides[coord] = normalized;
            }
        }
        _unitCoords[normalized] = coords;
    }

    public StringName GetOccupant(Vector2I coord, StringName base_occupant_id = default)
    {
        if (_coordOverrides.TryGetValue(coord, out StringName overrideOccupant))
        {
            return NormalizeStringName(overrideOccupant);
        }
        StringName normalizedBase = NormalizeStringName(base_occupant_id);
        if (!IsEmpty(normalizedBase) && _releasedUnits.Contains(normalizedBase))
        {
            return "";
        }
        return normalizedBase;
    }

    public bool HasOverride(Vector2I coord)
    {
        return _coordOverrides.ContainsKey(coord);
    }

    internal GDictionary Describe()
    {
        GDictionary unitPayload = new();
        foreach (KeyValuePair<StringName, List<Vector2I>> entry in _unitCoords)
        {
            var coordsPayload = new GArray();
            foreach (Vector2I coord in entry.Value)
                coordsPayload.Add(coord);
            unitPayload[entry.Key.ToString()] = coordsPayload;
        }

        GArray blockPayload = new();
        foreach (KeyValuePair<Vector2I, StringName> entry in _blockedCoords)
        {
            blockPayload.Add(
                new GDictionary
                {
                    ["coord"] = entry.Key,
                    ["blocker_id"] = NormalizeStringName(entry.Value),
                }
            );
        }

        GArray releasedUnitIds = new();
        foreach (StringName unitId in _releasedUnits)
        {
            releasedUnitIds.Add(unitId);
        }

        return new GDictionary
        {
            ["units"] = unitPayload,
            ["blocked_coords"] = blockPayload,
            ["released_unit_ids"] = releasedUnitIds,
            ["override_count"] = _coordOverrides.Count,
        };
    }

    private static StringName NormalizeStringName(object rawValue)
    {
        if (rawValue is not Variant value)
        {
            return new StringName(rawValue?.ToString() ?? "");
        }
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(value.ToString()),
        };
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static void Fail(string message)
    {
        GameLog.Error(message, "battle.virtual_board.failed", "battle");
    }
}
