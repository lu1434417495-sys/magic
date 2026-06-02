using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleTargetCollectionService
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

    private static List<Vector2I> GridGetAreaCoords(
        BattleGridService gridService,
        BattleState state,
        Vector2I areaCenter,
        StringName areaPattern,
        int areaValue,
        Vector2I areaDirection
    )
    {
        var coords = new List<Vector2I>();
        if (gridService == null)
        {
            return coords;
        }
        foreach (
            Vector2I coord in gridService.get_area_coords(
                state,
                areaCenter,
                areaPattern,
                areaValue,
                areaDirection
            )
        )
        {
            coords.Add(coord);
        }
        return coords;
    }
}
