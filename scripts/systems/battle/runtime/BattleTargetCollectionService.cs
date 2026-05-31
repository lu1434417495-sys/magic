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

    public BattleTargetCollectionResult CollectCombatProfileTargetCoords(
        BattleState state,
        BattleGridService gridService,
        Vector2I sourceCoord,
        CombatSkillDef combatProfile,
        IEnumerable<Vector2I> targetCoords,
        BattleUnitState sourceUnit = null,
        IEnumerable<BattleUnitState> targetUnits = null,
        int skillLevel = -1
    )
    {
        if (combatProfile == null)
        {
            return BattleTargetCollectionResult.UnhandledResult(targetCoords);
        }
        if (IsSelfTargetCollection(combatProfile, skillLevel))
        {
            return BattleTargetCollectionResult.HandledResult(
                CollectSelfTargetCoords(state, gridService, sourceCoord, sourceUnit)
            );
        }
        if (combatProfile.target_mode == Unit)
        {
            return BattleTargetCollectionResult.HandledResult(CollectTargetUnitCoords(targetUnits));
        }
        if (combatProfile.target_mode != Ground)
        {
            return BattleTargetCollectionResult.UnhandledResult(targetCoords);
        }
        if (state == null || gridService == null)
        {
            return BattleTargetCollectionResult.UnhandledResult(targetCoords);
        }

        StringName areaPattern = GetEffectiveAreaPattern(combatProfile, skillLevel);
        int areaValue = Math.Max(GetEffectiveAreaValue(combatProfile, skillLevel), 0);
        var coordSet = new HashSet<Vector2I>();
        foreach (Vector2I targetCoord in targetCoords ?? System.Array.Empty<Vector2I>())
        {
            if (!GridIsInside(gridService, state, targetCoord))
            {
                continue;
            }

            Vector2I areaCenter = targetCoord;
            if (areaPattern == Self && sourceCoord != MissingCoord)
            {
                areaCenter = sourceCoord;
            }
            bool collectedAny = false;
            Vector2I areaDirection =
                sourceCoord != MissingCoord ? areaCenter - sourceCoord : Vector2I.Zero;
            foreach (
                Vector2I effectCoord in GridGetAreaCoords(
                    gridService,
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
        return BattleTargetCollectionResult.HandledResult(coordSet);
    }

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
        return CollectCombatProfileTargetCoords(
                state,
                grid_service,
                source_coord,
                combat_profile,
                ToVector2IList(target_coords),
                source_unit,
                ToBattleUnitList(target_units),
                skill_level
            )
            .ToDictionary();
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

    private static IEnumerable<Vector2I> CollectSelfTargetCoords(
        BattleState state,
        BattleGridService gridService,
        Vector2I sourceCoord,
        BattleUnitState sourceUnit
    )
    {
        if (sourceUnit != null)
        {
            sourceUnit.refresh_footprint();
            return sourceUnit.occupied_coords;
        }
        if (state != null && gridService != null && GridIsInside(gridService, state, sourceCoord))
        {
            return new[] { sourceCoord };
        }
        return System.Array.Empty<Vector2I>();
    }

    private static IEnumerable<Vector2I> CollectTargetUnitCoords(
        IEnumerable<BattleUnitState> targetUnits
    )
    {
        var coordSet = new HashSet<Vector2I>();
        foreach (BattleUnitState targetUnit in targetUnits ?? System.Array.Empty<BattleUnitState>())
        {
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
        return coordSet;
    }

    private static List<Vector2I> ToVector2IList(GArray targetCoords)
    {
        var coords = new List<Vector2I>();
        foreach (var rawCoord in targetCoords ?? new GArray())
        {
            if (rawCoord.VariantType == Variant.Type.Vector2I)
            {
                coords.Add(rawCoord.AsVector2I());
            }
        }
        return coords;
    }

    private static List<BattleUnitState> ToBattleUnitList(GArray targetUnits)
    {
        var units = new List<BattleUnitState>();
        foreach (var rawTargetUnit in targetUnits ?? new GArray())
        {
            BattleUnitState targetUnit = ToBattleUnitState(rawTargetUnit);
            if (targetUnit != null)
            {
                units.Add(targetUnit);
            }
        }
        return units;
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
