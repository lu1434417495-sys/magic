using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

[GlobalClass]
public partial class BattleTargetCollectionService : RefCounted
{
    private static readonly StringName Empty = "";
    private static readonly StringName Self = "self";
    private static readonly StringName Unit = "unit";
    private static readonly StringName Ground = "ground";
    private static readonly StringName PropAreaPattern = "area_pattern";
    private static readonly StringName PropAreaValue = "area_value";
    private static readonly StringName PropTargetMode = "target_mode";
    private static readonly StringName PropTargetSelectionMode = "target_selection_mode";
    private static readonly StringName PropTargetTeamFilter = "target_team_filter";
    private static readonly Vector2I MissingCoord = new(-1, -1);

    public GDictionary collect_combat_profile_target_coords(
        GodotObject state,
        GodotObject grid_service,
        Vector2I source_coord,
        GodotObject combat_profile,
        GArray target_coords)
    {
        return collect_combat_profile_target_coords(
            state,
            grid_service,
            source_coord,
            combat_profile,
            target_coords,
            null,
            new GArray(),
            -1);
    }

    public GDictionary collect_combat_profile_target_coords(
        GodotObject state,
        GodotObject grid_service,
        Vector2I source_coord,
        GodotObject combat_profile,
        GArray target_coords,
        BattleUnitState source_unit)
    {
        return collect_combat_profile_target_coords(
            state,
            grid_service,
            source_coord,
            combat_profile,
            target_coords,
            source_unit,
            new GArray(),
            -1);
    }

    public GDictionary collect_combat_profile_target_coords(
        GodotObject state,
        GodotObject grid_service,
        Vector2I source_coord,
        GodotObject combat_profile,
        GArray target_coords,
        BattleUnitState source_unit,
        GArray target_units)
    {
        return collect_combat_profile_target_coords(
            state,
            grid_service,
            source_coord,
            combat_profile,
            target_coords,
            source_unit,
            target_units,
            -1);
    }

    public GDictionary collect_combat_profile_target_coords(
        GodotObject state,
        GodotObject grid_service,
        Vector2I source_coord,
        GodotObject combat_profile,
        GArray target_coords,
        BattleUnitState source_unit,
        GArray target_units,
        int skill_level)
    {
        if (combat_profile == null)
        {
            return BuildUnhandledResult(target_coords);
        }
        if (IsSelfTargetCollection(combat_profile, skill_level))
        {
            return BuildHandledResult(CollectSelfTargetCoords(state, grid_service, source_coord, source_unit));
        }
        if (GdInterop.GetStringName(combat_profile, PropTargetMode) == Unit)
        {
            return BuildHandledResult(CollectTargetUnitCoords(target_units));
        }
        if (GdInterop.GetStringName(combat_profile, PropTargetMode) != Ground)
        {
            return BuildUnhandledResult(target_coords);
        }
        if (state == null || grid_service == null)
        {
            return BuildUnhandledResult(target_coords);
        }

        StringName areaPattern = GetEffectiveAreaPattern(combat_profile, skill_level);
        int areaValue = Math.Max(GetEffectiveAreaValue(combat_profile, skill_level), 0);
        var coordSet = new HashSet<Vector2I>();
        foreach (Variant rawTargetCoord in target_coords ?? new GArray())
        {
            if (rawTargetCoord.VariantType != Variant.Type.Vector2I)
            {
                continue;
            }
            Vector2I targetCoord = rawTargetCoord.AsVector2I();
            if (!GridIsInside(grid_service, state, targetCoord))
            {
                continue;
            }

            Vector2I areaCenter = targetCoord;
            if (areaPattern == Self && source_coord != MissingCoord)
            {
                areaCenter = source_coord;
            }
            bool collectedAny = false;
            Vector2I areaDirection = source_coord != MissingCoord ? areaCenter - source_coord : Vector2I.Zero;
            foreach (Vector2I effectCoord in GridGetAreaCoords(grid_service, state, areaCenter, areaPattern, areaValue, areaDirection))
            {
                coordSet.Add(effectCoord);
                collectedAny = true;
            }
            if (!collectedAny)
            {
                coordSet.Add(areaCenter);
            }
        }
        return new GDictionary
        {
            ["handled"] = true,
            ["target_coords"] = SortCoords(coordSet),
        };
    }

    private static bool IsSelfTargetCollection(GodotObject combatProfile, int skillLevel)
    {
        if (combatProfile == null)
        {
            return false;
        }
        if (GdInterop.GetStringName(combatProfile, PropTargetSelectionMode) == Self)
        {
            return true;
        }
        if (GdInterop.GetStringName(combatProfile, PropTargetTeamFilter) == Self)
        {
            return true;
        }
        return GetEffectiveAreaPattern(combatProfile, skillLevel) == Self;
    }

    private static GVector2IArray CollectSelfTargetCoords(
        GodotObject state,
        GodotObject gridService,
        Vector2I sourceCoord,
        BattleUnitState sourceUnit)
    {
        if (sourceUnit != null)
        {
            sourceUnit.refresh_footprint();
            return SortCoords(sourceUnit.occupied_coords);
        }
        if (state != null && gridService != null && GridIsInside(gridService, state, sourceCoord))
        {
            return new GVector2IArray { sourceCoord };
        }
        return new GVector2IArray();
    }

    private static GVector2IArray CollectTargetUnitCoords(GArray targetUnits)
    {
        var coordSet = new HashSet<Vector2I>();
        foreach (Variant rawTargetUnit in targetUnits ?? new GArray())
        {
            BattleUnitState targetUnit = ToBattleUnitState(rawTargetUnit);
            if (targetUnit == null)
            {
                continue;
            }
            targetUnit.refresh_footprint();
            foreach (Vector2I occupiedCoord in targetUnit.occupied_coords)
            {
                coordSet.Add(occupiedCoord);
            }
        }
        return SortCoords(coordSet);
    }

    private static GDictionary BuildHandledResult(GArray targetCoords)
    {
        return new GDictionary
        {
            ["handled"] = true,
            ["target_coords"] = SortCoords(targetCoords),
        };
    }

    private static GDictionary BuildHandledResult(IEnumerable<Vector2I> targetCoords)
    {
        return new GDictionary
        {
            ["handled"] = true,
            ["target_coords"] = SortCoords(targetCoords),
        };
    }

    private static GDictionary BuildUnhandledResult(GArray targetCoords)
    {
        return new GDictionary
        {
            ["handled"] = false,
            ["target_coords"] = SortCoords(targetCoords),
        };
    }

    private static GVector2IArray SortCoords(GArray targetCoords)
    {
        var coords = new List<Vector2I>();
        foreach (Variant rawCoord in targetCoords ?? new GArray())
        {
            if (rawCoord.VariantType == Variant.Type.Vector2I)
            {
                coords.Add(rawCoord.AsVector2I());
            }
        }
        return SortCoords(coords);
    }

    private static GVector2IArray SortCoords(IEnumerable<Vector2I> targetCoords)
    {
        var coords = new List<Vector2I>(targetCoords ?? Array.Empty<Vector2I>());
        coords.Sort((left, right) =>
        {
            int yCompare = left.Y.CompareTo(right.Y);
            return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
        });
        var result = new GVector2IArray();
        foreach (Vector2I coord in coords)
        {
            result.Add(coord);
        }
        return result;
    }

    private static StringName GetEffectiveAreaPattern(GodotObject combatProfile, int skillLevel)
    {
        if (combatProfile == null)
        {
            return Empty;
        }
        Variant value = skillLevel >= 0
            ? combatProfile.Call("get_effective_area_pattern", skillLevel)
            : combatProfile.Get(PropAreaPattern);
        return ToStringName(value);
    }

    private static int GetEffectiveAreaValue(GodotObject combatProfile, int skillLevel)
    {
        if (combatProfile == null)
        {
            return 0;
        }
        Variant value = skillLevel >= 0
            ? combatProfile.Call("get_effective_area_value", skillLevel)
            : combatProfile.Get(PropAreaValue);
        return ToInt(value);
    }

    private static bool GridIsInside(GodotObject gridService, GodotObject state, Vector2I coord)
    {
        return gridService switch
        {
            null => false,
            BattleGridService typedGridService => typedGridService.is_inside(state, coord),
            _ => gridService.Call("is_inside", state, coord).AsBool(),
        };
    }

    private static GVector2IArray GridGetAreaCoords(
        GodotObject gridService,
        GodotObject state,
        Vector2I areaCenter,
        StringName areaPattern,
        int areaValue,
        Vector2I areaDirection)
    {
        if (gridService == null)
        {
            return new GVector2IArray();
        }
        if (gridService is BattleGridService typedGridService)
        {
            return typedGridService.get_area_coords(state, areaCenter, areaPattern, areaValue, areaDirection);
        }
        Variant rawResult = gridService.Call("get_area_coords", state, areaCenter, areaPattern, areaValue, areaDirection);
        return rawResult.VariantType == Variant.Type.Array
            ? SortCoords(rawResult.AsGodotArray())
            : new GVector2IArray();
    }

    private static BattleUnitState ToBattleUnitState(Variant value)
    {
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as BattleUnitState : null;
    }

    private static StringName ToStringName(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            Variant.Type.Nil => Empty,
            _ => new StringName(value.ToString()),
        };
    }

    private static int ToInt(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => (int)value.AsDouble(),
            Variant.Type.String => int.TryParse(value.AsString(), out int parsed) ? parsed : 0,
            _ => 0,
        };
    }
}
