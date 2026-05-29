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
    private static readonly Vector2I MissingCoord = new(-1, -1);

    public GDictionary collect_combat_profile_target_coords(
        BattleState state,
        BattleGridService grid_service,
        Vector2I source_coord,
        CombatSkillDef combat_profile,
        GArray target_coords
    )
    {
        return collect_combat_profile_target_coords(
            state,
            grid_service,
            source_coord,
            combat_profile,
            target_coords,
            null,
            new GArray(),
            -1
        );
    }

    public GDictionary collect_combat_profile_target_coords(
        BattleState state,
        BattleGridService grid_service,
        Vector2I source_coord,
        CombatSkillDef combat_profile,
        GArray target_coords,
        BattleUnitState source_unit
    )
    {
        return collect_combat_profile_target_coords(
            state,
            grid_service,
            source_coord,
            combat_profile,
            target_coords,
            source_unit,
            new GArray(),
            -1
        );
    }

    public GDictionary collect_combat_profile_target_coords(
        BattleState state,
        BattleGridService grid_service,
        Vector2I source_coord,
        CombatSkillDef combat_profile,
        GArray target_coords,
        BattleUnitState source_unit,
        GArray target_units
    )
    {
        return collect_combat_profile_target_coords(
            state,
            grid_service,
            source_coord,
            combat_profile,
            target_coords,
            source_unit,
            target_units,
            -1
        );
    }

    public GDictionary collect_combat_profile_target_coords(
        BattleState state,
        BattleGridService grid_service,
        Vector2I source_coord,
        CombatSkillDef combat_profile,
        GArray target_coords,
        BattleUnitState source_unit,
        GArray target_units,
        int skill_level
    )
    {
        if (combat_profile == null)
        {
            return BuildUnhandledResult(target_coords);
        }
        if (IsSelfTargetCollection(combat_profile, skill_level))
        {
            return BuildHandledResult(
                CollectSelfTargetCoords(state, grid_service, source_coord, source_unit)
            );
        }
        if (combat_profile.target_mode == Unit)
        {
            return BuildHandledResult(CollectTargetUnitCoords(target_units));
        }
        if (combat_profile.target_mode != Ground)
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
        foreach (var rawTargetCoord in target_coords ?? new GArray())
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
            Vector2I areaDirection =
                source_coord != MissingCoord ? areaCenter - source_coord : Vector2I.Zero;
            foreach (
                Vector2I effectCoord in GridGetAreaCoords(
                    grid_service,
                    state,
                    areaCenter,
                    areaPattern,
                    areaValue,
                    areaDirection
                )
            )
            {
                coordSet.Add(effectCoord);
                collectedAny = true;
            }
            if (!collectedAny)
            {
                coordSet.Add(areaCenter);
            }
        }
        return new GDictionary { ["handled"] = true, ["target_coords"] = SortCoords(coordSet) };
    }

    private static bool IsSelfTargetCollection(CombatSkillDef combatProfile, int skillLevel)
    {
        if (combatProfile == null)
        {
            return false;
        }
        if (combatProfile.target_selection_mode == Self)
        {
            return true;
        }
        if (combatProfile.target_team_filter == Self)
        {
            return true;
        }
        return GetEffectiveAreaPattern(combatProfile, skillLevel) == Self;
    }

    private static GVector2IArray CollectSelfTargetCoords(
        BattleState state,
        BattleGridService gridService,
        Vector2I sourceCoord,
        BattleUnitState sourceUnit
    )
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
        foreach (var rawTargetUnit in targetUnits ?? new GArray())
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
        return new GDictionary { ["handled"] = true, ["target_coords"] = SortCoords(targetCoords) };
    }

    private static GDictionary BuildHandledResult(IEnumerable<Vector2I> targetCoords)
    {
        return new GDictionary { ["handled"] = true, ["target_coords"] = SortCoords(targetCoords) };
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
        foreach (var rawCoord in targetCoords ?? new GArray())
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
        coords.Sort(
            (left, right) =>
            {
                int yCompare = left.Y.CompareTo(right.Y);
                return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
            }
        );
        var result = new GVector2IArray();
        foreach (Vector2I coord in coords)
        {
            result.Add(coord);
        }
        return result;
    }

    private static StringName GetEffectiveAreaPattern(CombatSkillDef combatProfile, int skillLevel)
    {
        if (combatProfile == null)
        {
            return Empty;
        }
        return skillLevel >= 0
            ? combatProfile.get_effective_area_pattern(skillLevel)
            : combatProfile.area_pattern;
    }

    private static int GetEffectiveAreaValue(CombatSkillDef combatProfile, int skillLevel)
    {
        if (combatProfile == null)
        {
            return 0;
        }
        return skillLevel >= 0
            ? combatProfile.get_effective_area_value(skillLevel)
            : combatProfile.area_value;
    }

    private static bool GridIsInside(BattleGridService gridService, BattleState state, Vector2I coord)
    {
        return gridService != null && gridService.is_inside(state, coord);
    }

    private static GVector2IArray GridGetAreaCoords(
        BattleGridService gridService,
        BattleState state,
        Vector2I areaCenter,
        StringName areaPattern,
        int areaValue,
        Vector2I areaDirection
    )
    {
        if (gridService == null)
        {
            return new GVector2IArray();
        }
        return gridService.get_area_coords(
            state,
            areaCenter,
            areaPattern,
            areaValue,
            areaDirection
        );
    }

    private static BattleUnitState ToBattleUnitState(object rawValue)
    {
        if (rawValue is not Variant value)
        {
            return rawValue as BattleUnitState;
        }
        return value.VariantType == Variant.Type.Object
            ? value.AsGodotObject() as BattleUnitState
            : null;
    }

}
