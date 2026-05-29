using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleVirtualBoardOverlay : RefCounted
{
    private readonly GDictionary _coord_overrides = new();
    private readonly GDictionary _unit_coords = new();
    private readonly GDictionary _blocked_coords = new();
    private readonly GDictionary _released_units = new();

    public void release_unit(StringName unit_id)
    {
        StringName normalized = NormalizeStringName(unit_id);
        if (IsEmpty(normalized))
        {
            return;
        }
        _released_units[normalized] = true;
        if (!_unit_coords.ContainsKey(normalized))
        {
            return;
        }

        var coordsValue = _unit_coords[normalized];
        if (coordsValue.VariantType == Variant.Type.Array)
        {
            foreach (var coordValue in coordsValue.AsGodotArray())
            {
                _coord_overrides.Remove(coordValue);
            }
        }
        _unit_coords.Remove(normalized);
    }

    public void place_unit(StringName unit_id, Vector2I anchor_coord, Vector2I footprint_size)
    {
        StringName normalized = NormalizeStringName(unit_id);
        if (IsEmpty(normalized))
        {
            Fail("BattleVirtualBoardOverlay.place_unit requires unit_id.");
            return;
        }

        _released_units.Remove(normalized);
        release_unit(normalized);
        _released_units.Remove(normalized);

        var coords = new Godot.Collections.Array<Vector2I>();
        Vector2I size = new(Math.Max(footprint_size.X, 1), Math.Max(footprint_size.Y, 1));
        for (int y = 0; y < size.Y; y++)
        {
            for (int x = 0; x < size.X; x++)
            {
                Vector2I coord = anchor_coord + new Vector2I(x, y);
                coords.Add(coord);
                _coord_overrides[coord] = normalized;
            }
        }
        _unit_coords[normalized] = coords;
    }

    public void block_coords(GArray coords, StringName blocker_id)
    {
        if (coords == null || coords.Count == 0)
        {
            return;
        }
        StringName normalized = NormalizeStringName(blocker_id);
        if (IsEmpty(normalized))
        {
            Fail("BattleVirtualBoardOverlay.block_coords requires blocker_id.");
            return;
        }

        foreach (var coordValue in coords)
        {
            if (coordValue.VariantType != Variant.Type.Vector2I)
            {
                continue;
            }
            Vector2I coord = coordValue.AsVector2I();
            _coord_overrides[coord] = normalized;
            _blocked_coords[coord] = normalized;
        }
    }

    public StringName get_occupant(Vector2I coord, StringName base_occupant_id = default)
    {
        if (_coord_overrides.ContainsKey(coord))
        {
            return NormalizeStringName(_coord_overrides[coord]);
        }
        StringName normalizedBase = NormalizeStringName(base_occupant_id);
        if (!IsEmpty(normalizedBase) && _released_units.ContainsKey(normalizedBase))
        {
            return "";
        }
        return normalizedBase;
    }

    public bool has_override(Vector2I coord)
    {
        return _coord_overrides.ContainsKey(coord);
    }

    public BattleVirtualBoardOverlay duplicate_overlay()
    {
        BattleVirtualBoardOverlay duplicate = new();
        CopyDictionary(_coord_overrides, duplicate._coord_overrides);
        CopyDictionary(_unit_coords, duplicate._unit_coords);
        CopyDictionary(_blocked_coords, duplicate._blocked_coords);
        CopyDictionary(_released_units, duplicate._released_units);
        return duplicate;
    }

    public GDictionary describe()
    {
        GDictionary unitPayload = new();
        foreach (var unitId in _unit_coords.Keys)
        {
            var coordsValue = _unit_coords[unitId];
            unitPayload[unitId.ToString()] =
                coordsValue.VariantType == Variant.Type.Array
                    ? coordsValue.AsGodotArray().Duplicate()
                    : new GArray();
        }

        GArray blockPayload = new();
        foreach (var coordValue in _blocked_coords.Keys)
        {
            blockPayload.Add(
                new GDictionary
                {
                    ["coord"] = coordValue,
                    ["blocker_id"] = NormalizeStringName(_blocked_coords[coordValue]),
                }
            );
        }

        GArray releasedUnitIds = new();
        foreach (var unitId in _released_units.Keys)
        {
            releasedUnitIds.Add(unitId);
        }

        return new GDictionary
        {
            ["units"] = unitPayload,
            ["blocked_coords"] = blockPayload,
            ["released_unit_ids"] = releasedUnitIds,
            ["override_count"] = _coord_overrides.Count,
        };
    }

    private static void CopyDictionary(GDictionary source, GDictionary target)
    {
        foreach (var key in source.Keys)
        {
            var value = source[key];
            target[key] =
                value.VariantType == Variant.Type.Array
                    ? value.AsGodotArray().Duplicate(true)
                    : value;
        }
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
