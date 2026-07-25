using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleTargetCollectionService
{
    private static readonly StringName Empty = "";
    private static readonly Vector2I MissingCoord = new(-1, -1);

    internal BattleTargetCollectionResult CollectCombatProfileTargetCoords(
        BattleState state,
        BattleGridService gridService,
        Vector2I sourceCoord,
        CombatSkillDefinition combatProfile,
        IEnumerable<Vector2I> targetCoords,
        BattleUnitState sourceUnit = null,
        IEnumerable<BattleUnitState> targetUnits = null,
        int skillLevel = -1
    )
    {
        SkillEffectiveCombatDefinition effectiveProfile =
            skillLevel >= 0
                ? SkillEffectiveCombatDefinition.BuildUncached(combatProfile, skillLevel)
                : null;
        return CollectTargetCoords(
            state,
            gridService,
            sourceCoord,
            combatProfile,
            effectiveProfile,
            targetCoords,
            sourceUnit,
            targetUnits
        );
    }

    internal BattleTargetCollectionResult CollectCombatProfileTargetCoords(
        BattleState state,
        BattleGridService gridService,
        Vector2I sourceCoord,
        CombatSkillDefinition combatProfile,
        IEnumerable<Vector2I> targetCoords,
        BattleUnitReadView sourceUnit,
        IEnumerable<BattleUnitReadView> targetUnits,
        int skillLevel = -1
    )
    {
        SkillEffectiveCombatDefinition effectiveProfile =
            skillLevel >= 0
                ? SkillEffectiveCombatDefinition.BuildUncached(combatProfile, skillLevel)
                : null;
        return CollectTargetCoords(
            state,
            gridService,
            sourceCoord,
            combatProfile,
            effectiveProfile,
            targetCoords,
            sourceUnit,
            targetUnits
        );
    }

    private BattleTargetCollectionResult CollectTargetCoords(
        BattleState state,
        BattleGridService gridService,
        Vector2I sourceCoord,
        CombatSkillDefinition combatProfile,
        SkillEffectiveCombatDefinition effectiveProfile,
        IEnumerable<Vector2I> targetCoords,
        BattleUnitState sourceUnit = null,
        IEnumerable<BattleUnitState> targetUnits = null
    )
    {
        if (combatProfile == null)
        {
            return BattleTargetCollectionResult.UnhandledResult(targetCoords);
        }
        if (IsSelfTargetCollection(combatProfile, effectiveProfile))
        {
            return BattleTargetCollectionResult.HandledResult(
                CollectSelfTargetCoords(state, gridService, sourceCoord, sourceUnit)
            );
        }
        if (combatProfile.TargetModeKind == BattleTargetMode.Unit)
        {
            return BattleTargetCollectionResult.HandledResult(CollectTargetUnitCoords(targetUnits));
        }
        if (combatProfile.TargetModeKind != BattleTargetMode.Ground)
        {
            return BattleTargetCollectionResult.UnhandledResult(targetCoords);
        }
        if (state == null || gridService == null)
        {
            return BattleTargetCollectionResult.UnhandledResult(targetCoords);
        }

        StringName areaPattern = GetEffectiveAreaPattern(combatProfile, effectiveProfile);
        int areaValue = Math.Max(GetEffectiveAreaValue(combatProfile, effectiveProfile), 0);
        var coordSet = new HashSet<Vector2I>();
        foreach (Vector2I targetCoord in targetCoords ?? System.Array.Empty<Vector2I>())
        {
            if (!GridIsInside(gridService, state, targetCoord))
            {
                continue;
            }

            if (
                TryCollectCasterTargetVectorLineCoords(
                    state,
                    gridService,
                    sourceCoord,
                    targetCoord,
                    combatProfile,
                    areaPattern,
                    areaValue,
                    out List<Vector2I> lineCoords
                )
            )
            {
                foreach (Vector2I effectCoord in lineCoords)
                {
                    coordSet.Add(effectCoord);
                }
                continue;
            }

            Vector2I areaCenter = targetCoord;
            if (BattleTypedNames.ToAreaPattern(areaPattern) == BattleAreaPattern.Self && sourceCoord != MissingCoord)
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

    private BattleTargetCollectionResult CollectTargetCoords(
        BattleState state,
        BattleGridService gridService,
        Vector2I sourceCoord,
        CombatSkillDefinition combatProfile,
        SkillEffectiveCombatDefinition effectiveProfile,
        IEnumerable<Vector2I> targetCoords,
        BattleUnitReadView sourceUnit,
        IEnumerable<BattleUnitReadView> targetUnits
    )
    {
        if (combatProfile == null)
        {
            return BattleTargetCollectionResult.UnhandledResult(targetCoords);
        }
        if (IsSelfTargetCollection(combatProfile, effectiveProfile))
        {
            return BattleTargetCollectionResult.HandledResult(
                CollectSelfTargetCoords(state, gridService, sourceCoord, sourceUnit)
            );
        }
        if (combatProfile.TargetModeKind == BattleTargetMode.Unit)
        {
            return BattleTargetCollectionResult.HandledResult(CollectTargetUnitCoords(targetUnits));
        }
        if (combatProfile.TargetModeKind != BattleTargetMode.Ground)
        {
            return BattleTargetCollectionResult.UnhandledResult(targetCoords);
        }
        if (state == null || gridService == null)
        {
            return BattleTargetCollectionResult.UnhandledResult(targetCoords);
        }

        StringName areaPattern = GetEffectiveAreaPattern(combatProfile, effectiveProfile);
        int areaValue = Math.Max(GetEffectiveAreaValue(combatProfile, effectiveProfile), 0);
        var coordSet = new HashSet<Vector2I>();
        foreach (Vector2I targetCoord in targetCoords ?? System.Array.Empty<Vector2I>())
        {
            if (!GridIsInside(gridService, state, targetCoord))
            {
                continue;
            }

            if (
                TryCollectCasterTargetVectorLineCoords(
                    state,
                    gridService,
                    sourceCoord,
                    targetCoord,
                    combatProfile,
                    areaPattern,
                    areaValue,
                    out List<Vector2I> lineCoords
                )
            )
            {
                foreach (Vector2I effectCoord in lineCoords)
                {
                    coordSet.Add(effectCoord);
                }
                continue;
            }

            Vector2I areaCenter = targetCoord;
            if (BattleTypedNames.ToAreaPattern(areaPattern) == BattleAreaPattern.Self && sourceCoord != MissingCoord)
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

    private static bool IsSelfTargetCollection(
        CombatSkillDefinition combatProfile,
        SkillEffectiveCombatDefinition effectiveProfile
    )
    {
        if (combatProfile == null)
        {
            return false;
        }
        if (combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.Self)
        {
            return true;
        }
        if (combatProfile.TargetFilterKind == BattleTargetFilter.Self)
        {
            return true;
        }
        return BattleTypedNames.ToAreaPattern(GetEffectiveAreaPattern(combatProfile, effectiveProfile))
            == BattleAreaPattern.Self;
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
            return sourceUnit.GetOccupiedCoordsReadViewTyped();
        }
        if (state != null && gridService != null && GridIsInside(gridService, state, sourceCoord))
        {
            return new[] { sourceCoord };
        }
        return System.Array.Empty<Vector2I>();
    }

    private static IEnumerable<Vector2I> CollectSelfTargetCoords(
        BattleState state,
        BattleGridService gridService,
        Vector2I sourceCoord,
        BattleUnitReadView sourceUnit
    )
    {
        if (sourceUnit.IsValid)
        {
            return sourceUnit.GetOccupiedCoords();
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
            foreach (
                Vector2I occupiedCoord in targetUnit.GetOccupiedCoordsReadViewTyped()
            )
            {
                coordSet.Add(occupiedCoord);
            }
        }
        return coordSet;
    }

    private static IEnumerable<Vector2I> CollectTargetUnitCoords(
        IEnumerable<BattleUnitReadView> targetUnits
    )
    {
        var coordSet = new HashSet<Vector2I>();
        foreach (BattleUnitReadView targetUnit in targetUnits ?? System.Array.Empty<BattleUnitReadView>())
        {
            if (!targetUnit.IsValid)
            {
                continue;
            }
            foreach (Vector2I occupiedCoord in targetUnit.GetOccupiedCoords())
            {
                coordSet.Add(occupiedCoord);
            }
        }
        return coordSet;
    }

    private static StringName GetEffectiveAreaPattern(
        CombatSkillDefinition combatProfile,
        SkillEffectiveCombatDefinition effectiveProfile
    )
    {
        if (combatProfile == null)
        {
            return Empty;
        }
        return effectiveProfile != null ? effectiveProfile.AreaPattern : combatProfile.AreaPattern;
    }

    private static int GetEffectiveAreaValue(
        CombatSkillDefinition combatProfile,
        SkillEffectiveCombatDefinition effectiveProfile
    )
    {
        if (combatProfile == null)
        {
            return 0;
        }
        return effectiveProfile != null ? effectiveProfile.AreaValue : combatProfile.AreaValue;
    }

    private static bool TryCollectCasterTargetVectorLineCoords(
        BattleState state,
        BattleGridService gridService,
        Vector2I sourceCoord,
        Vector2I targetCoord,
        CombatSkillDefinition combatProfile,
        StringName areaPattern,
        int areaValue,
        out List<Vector2I> coords
    )
    {
        coords = new List<Vector2I>();
        if (
            combatProfile == null
            || BattleTypedNames.ToAreaPattern(areaPattern) != BattleAreaPattern.Line
            || combatProfile.AreaOriginModeKind != CombatAreaOriginMode.Caster
            || combatProfile.AreaDirectionModeKind != CombatAreaDirectionMode.TargetVector
            || sourceCoord == MissingCoord
        )
        {
            return false;
        }

        Vector2I delta = targetCoord - sourceCoord;
        if (delta == Vector2I.Zero || (delta.X != 0 && delta.Y != 0))
        {
            return true;
        }

        Vector2I step =
            delta.X != 0
                ? new Vector2I(Math.Sign(delta.X), 0)
                : new Vector2I(0, Math.Sign(delta.Y));
        int distance = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
        int maxDistance = areaValue > 0 ? Math.Min(distance, areaValue) : distance;
        for (int index = 1; index <= maxDistance; index++)
        {
            Vector2I coord = sourceCoord + (step * index);
            if (GridIsInside(gridService, state, coord))
            {
                coords.Add(coord);
            }
        }
        return true;
    }

    private static bool GridIsInside(BattleGridService gridService, BattleState state, Vector2I coord)
    {
        return gridService != null && gridService.IsInside(state, coord);
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
            Vector2I coord in gridService.GetAreaCoords(
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
