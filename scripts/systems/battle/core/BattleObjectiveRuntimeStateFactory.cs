using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal static class BattleObjectiveRuntimeStateFactory
{
    internal static bool TryCreate(
        BattleState state,
        BattleObjectiveDefinition definition,
        out BattleObjectiveRuntimeState runtimeState
    )
    {
        runtimeState = null;
        if (state == null || definition == null)
            return false;

        switch (definition)
        {
            case BattleEliminationObjectiveDefinition:
                runtimeState = new BattleEliminationObjectiveRuntimeState();
                return true;
            case BattleBossObjectiveDefinition bossDefinition:
                return TryCreateBoss(state, bossDefinition, out runtimeState);
            case BattleRescueObjectiveDefinition rescueDefinition:
                return TryCreateRescue(state, rescueDefinition, out runtimeState);
            case BattleEscapeObjectiveDefinition escapeDefinition:
                return TryCreateEscape(state, escapeDefinition, out runtimeState);
            case BattleEscortObjectiveDefinition escortDefinition:
                return TryCreateEscort(state, escortDefinition, out runtimeState);
            case BattleDefenseObjectiveDefinition defenseDefinition:
                return TryCreateDefense(state, defenseDefinition, out runtimeState);
            case BattleInterceptObjectiveDefinition interceptDefinition:
                return TryCreateIntercept(
                    state,
                    interceptDefinition,
                    out runtimeState
                );
            case BattleNodeOperationObjectiveDefinition nodeOperationDefinition:
                return TryCreateNodeOperation(
                    state,
                    nodeOperationDefinition,
                    out runtimeState
                );
            case BattleControlObjectiveDefinition controlDefinition:
                return TryCreateControl(
                    state,
                    controlDefinition,
                    out runtimeState
                );
            default:
                return false;
        }
    }

    private static bool TryCreateBoss(
        BattleState state,
        BattleBossObjectiveDefinition definition,
        out BattleObjectiveRuntimeState runtimeState
    )
    {
        runtimeState = null;
        List<StringName> requiredPartyUnitIds = ResolveRequiredPartyUnitIds(state);
        if (requiredPartyUnitIds.Count == 0)
            return false;

        if (
            !TryResolveRosterActor(
                state,
                definition.TargetActorId,
                out BattleUnitState target
            )
        )
            return false;

        runtimeState = new BattleBossObjectiveRuntimeState(
            definition.TargetActorId,
            target.unit_id,
            requiredPartyUnitIds
        );
        return true;
    }

    private static bool TryCreateDefense(
        BattleState state,
        BattleDefenseObjectiveDefinition definition,
        out BattleObjectiveRuntimeState runtimeState
    )
    {
        runtimeState = null;
        List<StringName> requiredPartyUnitIds = ResolveRequiredPartyUnitIds(state);
        int startTu = state?.timeline?.current_tu ?? -1;
        if (
            requiredPartyUnitIds.Count == 0
            || startTu < 0
            || definition.DurationTu > int.MaxValue - startTu
            || !TryResolveScenarioActor(
                state,
                definition.TargetActorId,
                out BattleUnitState target
            )
        )
        {
            return false;
        }

        runtimeState = new BattleDefenseObjectiveRuntimeState(
            definition.TargetActorId,
            target.unit_id,
            requiredPartyUnitIds,
            startTu,
            startTu + definition.DurationTu
        );
        return true;
    }

    private static bool TryCreateIntercept(
        BattleState state,
        BattleInterceptObjectiveDefinition definition,
        out BattleObjectiveRuntimeState runtimeState
    )
    {
        runtimeState = null;
        List<StringName> requiredPartyUnitIds = ResolveRequiredPartyUnitIds(state);
        if (
            requiredPartyUnitIds.Count == 0
            || !TryResolveRosterActor(
                state,
                definition.TargetActorId,
                out BattleUnitState target
            )
            || !TryResolveEdgeZoneCoords(
                state,
                definition.ExitEdge,
                definition.ExitDepth,
                out List<Vector2I> exitCoords
            )
        )
        {
            return false;
        }

        var exitCoordSet = new HashSet<Vector2I>(exitCoords);
        using var gridService = new BattleGridService();
        if (
            ResolveExitPlacements(
                state,
                target,
                ResolveFootprint(target),
                exitCoordSet,
                gridService
            ).Count
            == 0
        )
        {
            return false;
        }

        runtimeState = new BattleInterceptObjectiveRuntimeState(
            definition.TargetActorId,
            target.unit_id,
            definition.ExitZoneId,
            definition.ExitEdge,
            definition.ExitDepth,
            requiredPartyUnitIds,
            exitCoords
        );
        return true;
    }

    private static bool TryCreateRescue(
        BattleState state,
        BattleRescueObjectiveDefinition definition,
        out BattleObjectiveRuntimeState runtimeState
    )
    {
        runtimeState = null;
        List<StringName> requiredPartyUnitIds = ResolveRequiredPartyUnitIds(state);
        if (
            requiredPartyUnitIds.Count == 0
            || !TryResolveScenarioActor(
                state,
                definition.TargetActorId,
                out BattleUnitState target
            )
        )
        {
            return false;
        }
        runtimeState = new BattleRescueObjectiveRuntimeState(
            definition.TargetActorId,
            target.unit_id,
            requiredPartyUnitIds
        );
        return true;
    }

    private static bool TryCreateEscape(
        BattleState state,
        BattleEscapeObjectiveDefinition definition,
        out BattleObjectiveRuntimeState runtimeState
    )
    {
        runtimeState = null;
        List<StringName> requiredUnitIds = ResolveRequiredPartyUnitIds(state);
        if (requiredUnitIds.Count == 0)
            return false;
        if (
            !TryResolveEdgeZoneCoords(
                state,
                definition.ExitEdge,
                definition.ExitDepth,
                out List<Vector2I> exitCoords
            )
        )
            return false;

        var exitCoordSet = new HashSet<Vector2I>(exitCoords);
        if (
            !CanRequiredPartyFitInExitSimultaneously(
                state,
                requiredUnitIds,
                exitCoordSet
            )
        )
            return false;

        runtimeState = new BattleEscapeObjectiveRuntimeState(
            definition.ExitZoneId,
            definition.ExitEdge,
            definition.ExitDepth,
            requiredUnitIds,
            exitCoords
        );
        return true;
    }

    private static bool TryCreateEscort(
        BattleState state,
        BattleEscortObjectiveDefinition definition,
        out BattleObjectiveRuntimeState runtimeState
    )
    {
        runtimeState = null;
        List<StringName> requiredPartyUnitIds = ResolveRequiredPartyUnitIds(state);
        if (
            requiredPartyUnitIds.Count == 0
            || !TryResolveScenarioActor(
                state,
                definition.TargetActorId,
                out BattleUnitState target
            )
            || !TryResolveEdgeZoneCoords(
                state,
                definition.ExitEdge,
                definition.ExitDepth,
                out List<Vector2I> exitCoords
            )
        )
        {
            return false;
        }
        var exitCoordSet = new HashSet<Vector2I>(exitCoords);
        using var gridService = new BattleGridService();
        if (
            ResolveExitPlacements(
                state,
                target,
                ResolveFootprint(target),
                exitCoordSet,
                gridService
            ).Count
            == 0
        )
        {
            return false;
        }
        runtimeState = new BattleEscortObjectiveRuntimeState(
            definition.TargetActorId,
            target.unit_id,
            definition.ExitZoneId,
            definition.ExitEdge,
            definition.ExitDepth,
            requiredPartyUnitIds,
            exitCoords
        );
        return true;
    }

    private static bool TryCreateNodeOperation(
        BattleState state,
        BattleNodeOperationObjectiveDefinition definition,
        out BattleObjectiveRuntimeState runtimeState
    )
    {
        runtimeState = null;
        List<StringName> requiredPartyUnitIds = ResolveRequiredPartyUnitIds(state);
        if (requiredPartyUnitIds.Count == 0)
            return false;

        var usedCoords = new HashSet<Vector2I>();
        var runtimeNodes = new List<BattleOperationNodeRuntimeState>();
        foreach (BattleOperationNodeDefinition nodeDefinition in definition.OperationNodes)
        {
            if (
                !TryResolveEdgeZoneCoords(
                    state,
                    nodeDefinition.PlacementEdge,
                    nodeDefinition.PlacementDepth,
                    out List<Vector2I> candidateCoords
                )
            )
            {
                return false;
            }

            bool foundCoord = false;
            Vector2I selectedCoord = default;
            foreach (Vector2I candidateCoord in candidateCoords)
            {
                BattleCellState cell = state.GetCell(candidateCoord);
                if (
                    cell == null
                    || cell.occupant_unit_id != ""
                    || !usedCoords.Add(candidateCoord)
                )
                {
                    continue;
                }
                selectedCoord = candidateCoord;
                foundCoord = true;
                break;
            }
            if (!foundCoord)
                return false;

            runtimeNodes.Add(
                new BattleOperationNodeRuntimeState(
                    nodeDefinition.NodeId,
                    nodeDefinition.DisplayName,
                    nodeDefinition.ZoneId,
                    nodeDefinition.PlacementEdge,
                    nodeDefinition.PlacementDepth,
                    selectedCoord
                )
            );
        }

        runtimeState = new BattleNodeOperationObjectiveRuntimeState(
            requiredPartyUnitIds,
            runtimeNodes
        );
        return true;
    }

    private static bool TryCreateControl(
        BattleState state,
        BattleControlObjectiveDefinition definition,
        out BattleObjectiveRuntimeState runtimeState
    )
    {
        runtimeState = null;
        List<StringName> requiredPartyUnitIds = ResolveRequiredPartyUnitIds(state);
        if (requiredPartyUnitIds.Count == 0)
            return false;

        var usedCoords = new HashSet<Vector2I>();
        var runtimeZones = new List<BattleControlZoneRuntimeState>();
        using var gridService = new BattleGridService();
        foreach (BattleControlZoneDefinition zoneDefinition in definition.ControlZones)
        {
            if (
                !TryResolveEdgeZoneCoords(
                    state,
                    zoneDefinition.PlacementEdge,
                    zoneDefinition.PlacementDepth,
                    out List<Vector2I> zoneCoords
                )
            )
            {
                return false;
            }
            foreach (Vector2I coord in zoneCoords)
            {
                if (!usedCoords.Add(coord))
                    return false;
            }

            var zoneCoordSet = new HashSet<Vector2I>(zoneCoords);
            if (
                !HasLegalZonePlacement(
                    state,
                    state.GetAllyUnitIdsTyped(),
                    zoneCoordSet,
                    gridService
                )
                || !HasLegalZonePlacement(
                    state,
                    state.GetEnemyUnitIdsTyped(),
                    zoneCoordSet,
                    gridService
                )
            )
            {
                return false;
            }

            runtimeZones.Add(
                new BattleControlZoneRuntimeState(
                    zoneDefinition.ZoneId,
                    zoneDefinition.DisplayName,
                    zoneDefinition.PlacementEdge,
                    zoneDefinition.PlacementDepth,
                    zoneCoords
                )
            );
        }

        runtimeState = new BattleControlObjectiveRuntimeState(
            requiredPartyUnitIds,
            runtimeZones,
            definition.ScoreTarget
        );
        return true;
    }

    private static bool HasLegalZonePlacement(
        BattleState state,
        IEnumerable<StringName> unitIds,
        IReadOnlySet<Vector2I> zoneCoords,
        BattleGridService gridService
    )
    {
        foreach (StringName unitId in unitIds ?? Array.Empty<StringName>())
        {
            BattleUnitState unit = state.GetUnit(unitId);
            if (
                unit?.is_alive == true
                && ResolveExitPlacements(
                    state,
                    unit,
                    ResolveFootprint(unit),
                    zoneCoords,
                    gridService
                ).Count > 0
            )
            {
                return true;
            }
        }
        return false;
    }

    private static List<StringName> ResolveRequiredPartyUnitIds(BattleState state)
    {
        var requiredUnitIds = new List<StringName>();
        foreach (StringName allyUnitId in state.GetAllyUnitIdsTyped())
        {
            BattleUnitState unit = state.GetUnit(allyUnitId);
            if (
                unit != null
                && unit.unit_id != ""
                && unit.source_member_id != ""
                && !requiredUnitIds.Contains(unit.unit_id)
            )
            {
                requiredUnitIds.Add(unit.unit_id);
            }
        }
        requiredUnitIds.Sort(
            (left, right) =>
                StringComparer.Ordinal.Compare(left.ToString(), right.ToString())
        );
        return requiredUnitIds;
    }

    internal static bool TryResolveEdgeZoneCoords(
        BattleState state,
        BattleMapEdge edge,
        int depth,
        out List<Vector2I> exitCoords
    )
    {
        exitCoords = new List<Vector2I>();
        if (
            state == null
            || !Enum.IsDefined(edge)
            || edge == BattleMapEdge.Unknown
            || depth <= 0
        )
        {
            return false;
        }
        Vector2I mapSize = state.map_size;
        int relevantLength =
            edge is BattleMapEdge.Left or BattleMapEdge.Right
                ? mapSize.X
                : mapSize.Y;
        if (relevantLength <= 1 || depth >= relevantLength)
            return false;

        foreach (BattleState.BattleCellEntry entry in state.GetCellEntriesTyped())
        {
            BattleCellState cell = entry.Cell;
            Vector2I coord = entry.Coord;
            if (
                cell == null
                || !cell.passable
                || coord.X < 0
                || coord.Y < 0
                || coord.X >= mapSize.X
                || coord.Y >= mapSize.Y
                || !IsInExitStrip(coord, mapSize, edge, depth)
            )
            {
                continue;
            }
            exitCoords.Add(coord);
        }
        exitCoords = exitCoords
            .Distinct()
            .OrderBy(coord => coord.Y)
            .ThenBy(coord => coord.X)
            .ToList();
        return exitCoords.Count > 0;
    }

    private static bool TryResolveScenarioActor(
        BattleState state,
        StringName actorId,
        out BattleUnitState target
    )
    {
        target = null;
        if (state == null || actorId == "")
            return false;
        List<BattleUnitState> matches = state
            .GetUnitsTyped()
            .Where(
                unit =>
                    unit != null
                    && unit.encounter_actor_id == actorId
                    && unit.source_member_id == ""
                    && state.GetAllyUnitIdsTyped().Contains(unit.unit_id)
            )
            .ToList();
        if (matches.Count != 1)
            return false;
        target = matches[0];
        return true;
    }

    private static bool TryResolveRosterActor(
        BattleState state,
        StringName actorId,
        out BattleUnitState target
    )
    {
        target = null;
        if (state == null || actorId == "")
            return false;
        var enemyUnitIds = new HashSet<StringName>(state.GetEnemyUnitIdsTyped());
        List<BattleUnitState> matches = state
            .GetUnitsTyped()
            .Where(
                unit =>
                    unit != null
                    && unit.encounter_actor_id == actorId
                    && enemyUnitIds.Contains(unit.unit_id)
            )
            .ToList();
        if (matches.Count != 1)
            return false;
        target = matches[0];
        return true;
    }

    private static bool CanRequiredPartyFitInExitSimultaneously(
        BattleState state,
        IReadOnlyList<StringName> requiredUnitIds,
        IReadOnlySet<Vector2I> exitCoords
    )
    {
        if (
            state == null
            || requiredUnitIds == null
            || requiredUnitIds.Count == 0
            || exitCoords == null
            || exitCoords.Count == 0
        )
        {
            return false;
        }

        var placementGroups = new List<List<Vector2I[]>>();
        using var gridService = new BattleGridService();
        int requiredCellCount = 0;
        foreach (StringName requiredUnitId in requiredUnitIds)
        {
            BattleUnitState unit = state.GetUnit(requiredUnitId);
            if (unit == null)
                return false;
            Vector2I footprint = ResolveFootprint(unit);
            requiredCellCount += footprint.X * footprint.Y;
            List<Vector2I[]> placements = ResolveExitPlacements(
                state,
                unit,
                footprint,
                exitCoords,
                gridService
            );
            if (placements.Count == 0)
                return false;
            placementGroups.Add(placements);
        }
        if (requiredCellCount > exitCoords.Count)
            return false;

        placementGroups.Sort(
            (left, right) => left.Count.CompareTo(right.Count)
        );
        return TryAssignNonOverlappingPlacements(
            placementGroups,
            0,
            new HashSet<Vector2I>()
        );
    }

    private static List<Vector2I[]> ResolveExitPlacements(
        BattleState state,
        BattleUnitState unit,
        Vector2I footprint,
        IReadOnlySet<Vector2I> exitCoords,
        BattleGridService gridService
    )
    {
        var placements = new List<Vector2I[]>();
        for (int y = 0; y < state.map_size.Y; y++)
        {
            for (int x = 0; x < state.map_size.X; x++)
            {
                var occupiedCoords = new Vector2I[footprint.X * footprint.Y];
                int occupiedIndex = 0;
                bool fits = true;
                for (int offsetY = 0; offsetY < footprint.Y && fits; offsetY++)
                {
                    for (int offsetX = 0; offsetX < footprint.X; offsetX++)
                    {
                        Vector2I coord = new(x + offsetX, y + offsetY);
                        BattleCellState cell = state.GetCell(coord);
                        if (
                            cell == null
                            || !cell.passable
                            || !exitCoords.Contains(coord)
                        )
                        {
                            fits = false;
                            break;
                        }
                        occupiedCoords[occupiedIndex++] = coord;
                    }
                }
                if (
                    fits
                    && gridService.CanFitFootprintIgnoringOccupants(
                        state,
                        new Vector2I(x, y),
                        footprint,
                        unit
                    )
                )
                {
                    placements.Add(occupiedCoords);
                }
            }
        }
        return placements;
    }

    private static bool TryAssignNonOverlappingPlacements(
        IReadOnlyList<List<Vector2I[]>> placementGroups,
        int groupIndex,
        HashSet<Vector2I> occupiedCoords
    )
    {
        if (groupIndex >= placementGroups.Count)
            return true;
        foreach (Vector2I[] placement in placementGroups[groupIndex])
        {
            bool overlaps = false;
            foreach (Vector2I coord in placement)
            {
                if (occupiedCoords.Contains(coord))
                {
                    overlaps = true;
                    break;
                }
            }
            if (overlaps)
                continue;

            foreach (Vector2I coord in placement)
                occupiedCoords.Add(coord);
            if (
                TryAssignNonOverlappingPlacements(
                    placementGroups,
                    groupIndex + 1,
                    occupiedCoords
                )
            )
            {
                return true;
            }
            foreach (Vector2I coord in placement)
                occupiedCoords.Remove(coord);
        }
        return false;
    }

    private static Vector2I ResolveFootprint(BattleUnitState unit)
    {
        Vector2I footprint = unit?.footprint_size ?? Vector2I.One;
        if (footprint.X <= 0 || footprint.Y <= 0)
        {
            footprint = BattleUnitState.GetFootprintSizeForBodySize(
                unit?.body_size ?? BattleUnitState.BodySizeMedium
            );
        }
        return new Vector2I(
            Math.Max(footprint.X, 1),
            Math.Max(footprint.Y, 1)
        );
    }

    private static bool IsInExitStrip(
        Vector2I coord,
        Vector2I mapSize,
        BattleMapEdge edge,
        int depth
    ) =>
        edge switch
        {
            BattleMapEdge.Left => coord.X < depth,
            BattleMapEdge.Right => coord.X >= mapSize.X - depth,
            BattleMapEdge.Top => coord.Y < depth,
            BattleMapEdge.Bottom => coord.Y >= mapSize.Y - depth,
            _ => false,
        };
}
