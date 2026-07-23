using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class BattleObjectiveNodeProgressSnapshot
{
    internal BattleObjectiveNodeProgressSnapshot(
        StringName nodeId,
        string displayName,
        StringName zoneId,
        Vector2I coord,
        bool isCompleted
    )
    {
        NodeId = nodeId;
        DisplayName = displayName ?? "";
        ZoneId = zoneId;
        Coord = coord;
        IsCompleted = isCompleted;
    }

    internal StringName NodeId { get; }
    internal string DisplayName { get; }
    internal StringName ZoneId { get; }
    internal Vector2I Coord { get; }
    internal bool IsCompleted { get; }
}

internal sealed class BattleObjectiveControlZoneProgressSnapshot
{
    private readonly ReadOnlyCollection<Vector2I> _coords;

    internal BattleObjectiveControlZoneProgressSnapshot(
        StringName zoneId,
        string displayName,
        BattleMapEdge placementEdge,
        int placementDepth,
        IEnumerable<Vector2I> coords,
        BattleControlZoneOccupancyKind occupancy
    )
    {
        ZoneId = zoneId;
        DisplayName = displayName ?? "";
        PlacementEdge = placementEdge;
        PlacementDepth = Math.Max(placementDepth, 0);
        _coords = new List<Vector2I>(
            coords ?? Array.Empty<Vector2I>()
        ).AsReadOnly();
        Occupancy = occupancy;
    }

    internal StringName ZoneId { get; }
    internal string DisplayName { get; }
    internal BattleMapEdge PlacementEdge { get; }
    internal int PlacementDepth { get; }
    internal IReadOnlyList<Vector2I> Coords => _coords;
    internal BattleControlZoneOccupancyKind Occupancy { get; }
    internal string OccupancyWireValue =>
        Occupancy switch
        {
            BattleControlZoneOccupancyKind.Player => "player",
            BattleControlZoneOccupancyKind.Hostile => "hostile",
            BattleControlZoneOccupancyKind.Contested => "contested",
            _ => "neutral",
        };
}

internal sealed class BattleObjectiveProgressSnapshot
{
    private readonly ReadOnlyCollection<StringName> _requiredUnitIds;
    private readonly ReadOnlyCollection<StringName> _aliveRequiredUnitIds;
    private readonly ReadOnlyCollection<StringName> _reachedExitUnitIds;
    private readonly ReadOnlyCollection<Vector2I> _exitCoords;
    private readonly ReadOnlyCollection<BattleObjectiveNodeProgressSnapshot>
        _operationNodes;
    private readonly ReadOnlyCollection<BattleObjectiveControlZoneProgressSnapshot>
        _controlZones;

    internal static BattleObjectiveProgressSnapshot Empty { get; } = new();

    private BattleObjectiveProgressSnapshot()
    {
        _requiredUnitIds = new List<StringName>().AsReadOnly();
        _aliveRequiredUnitIds = new List<StringName>().AsReadOnly();
        _reachedExitUnitIds = new List<StringName>().AsReadOnly();
        _exitCoords = new List<Vector2I>().AsReadOnly();
        _operationNodes =
            new List<BattleObjectiveNodeProgressSnapshot>().AsReadOnly();
        _controlZones =
            new List<BattleObjectiveControlZoneProgressSnapshot>().AsReadOnly();
    }

    private BattleObjectiveProgressSnapshot(
        BattleObjectiveMode mode,
        StringName targetActorId,
        StringName targetUnitId,
        string targetDisplayName,
        bool targetAlive,
        bool targetSecured,
        bool targetReachedExit,
        StringName exitZoneId,
        BattleMapEdge exitEdge,
        int exitDepth,
        IEnumerable<StringName> requiredUnitIds,
        IEnumerable<StringName> aliveRequiredUnitIds,
        IEnumerable<StringName> reachedExitUnitIds,
        IEnumerable<Vector2I> exitCoords,
        int enemyUnitCount,
        int aliveEnemyUnitCount,
        int currentTu = 0,
        int startTu = 0,
        int deadlineTu = 0,
        IEnumerable<BattleObjectiveNodeProgressSnapshot> operationNodes = null,
        IEnumerable<BattleObjectiveControlZoneProgressSnapshot> controlZones = null,
        int playerControlScore = 0,
        int hostileControlScore = 0,
        int controlScoreTarget = 0
    )
    {
        Mode = mode;
        TargetActorId = targetActorId;
        TargetUnitId = targetUnitId;
        TargetDisplayName = targetDisplayName ?? "";
        TargetAlive = targetAlive;
        TargetSecured = targetSecured;
        TargetReachedExit = targetReachedExit;
        ExitZoneId = exitZoneId;
        ExitEdge = exitEdge;
        ExitDepth = Math.Max(exitDepth, 0);
        _requiredUnitIds = CopyStringNames(requiredUnitIds);
        _aliveRequiredUnitIds = CopyStringNames(aliveRequiredUnitIds);
        _reachedExitUnitIds = CopyStringNames(reachedExitUnitIds);
        _exitCoords = CopyCoords(exitCoords);
        EnemyUnitCount = Math.Max(enemyUnitCount, 0);
        AliveEnemyUnitCount = Math.Clamp(aliveEnemyUnitCount, 0, EnemyUnitCount);
        CurrentTu = Math.Max(currentTu, 0);
        StartTu = Math.Max(startTu, 0);
        DeadlineTu = Math.Max(deadlineTu, 0);
        _operationNodes = new List<BattleObjectiveNodeProgressSnapshot>(
            operationNodes ?? Array.Empty<BattleObjectiveNodeProgressSnapshot>()
        ).AsReadOnly();
        _controlZones = new List<BattleObjectiveControlZoneProgressSnapshot>(
            controlZones
                ?? Array.Empty<BattleObjectiveControlZoneProgressSnapshot>()
        ).AsReadOnly();
        PlayerControlScore = Math.Max(playerControlScore, 0);
        HostileControlScore = Math.Max(hostileControlScore, 0);
        ControlScoreTarget = Math.Max(controlScoreTarget, 0);
    }

    internal bool IsValid => Mode != BattleObjectiveMode.Unknown;
    internal BattleObjectiveMode Mode { get; }
    internal StringName TargetActorId { get; } = "";
    internal StringName TargetUnitId { get; } = "";
    internal string TargetDisplayName { get; } = "";
    internal bool TargetAlive { get; }
    internal bool TargetSecured { get; }
    internal bool TargetReachedExit { get; }
    internal StringName ExitZoneId { get; } = "";
    internal BattleMapEdge ExitEdge { get; }
    internal int ExitDepth { get; }
    internal IReadOnlyList<StringName> RequiredUnitIds => _requiredUnitIds;
    internal IReadOnlyList<StringName> AliveRequiredUnitIds => _aliveRequiredUnitIds;
    internal IReadOnlyList<StringName> ReachedExitUnitIds => _reachedExitUnitIds;
    internal IReadOnlyList<Vector2I> ExitCoords => _exitCoords;
    internal int RequiredUnitCount => _requiredUnitIds.Count;
    internal int AliveRequiredUnitCount => _aliveRequiredUnitIds.Count;
    internal int ReachedExitUnitCount => _reachedExitUnitIds.Count;
    internal int EnemyUnitCount { get; }
    internal int AliveEnemyUnitCount { get; }
    internal int CurrentTu { get; }
    internal int StartTu { get; }
    internal int DeadlineTu { get; }
    internal int RemainingTu => Math.Max(DeadlineTu - CurrentTu, 0);
    internal IReadOnlyList<BattleObjectiveNodeProgressSnapshot> OperationNodes =>
        _operationNodes;
    internal int OperationNodeCount => _operationNodes.Count;
    internal int CompletedOperationNodeCount
    {
        get
        {
            int count = 0;
            foreach (BattleObjectiveNodeProgressSnapshot node in _operationNodes)
            {
                if (node.IsCompleted)
                    count++;
            }
            return count;
        }
    }
    internal int IncompleteOperationNodeCount =>
        OperationNodeCount - CompletedOperationNodeCount;
    internal IReadOnlyList<BattleObjectiveControlZoneProgressSnapshot>
        ControlZones => _controlZones;
    internal int ControlZoneCount => _controlZones.Count;
    internal int PlayerControlScore { get; }
    internal int HostileControlScore { get; }
    internal int ControlScoreTarget { get; }

    internal string ExitEdgeWireValue =>
        ExitEdge switch
        {
            BattleMapEdge.Left => "left",
            BattleMapEdge.Right => "right",
            BattleMapEdge.Top => "top",
            BattleMapEdge.Bottom => "bottom",
            _ => "unknown",
        };

    internal static BattleObjectiveProgressSnapshot Capture(BattleState state)
    {
        BattleObjectiveRuntimeState objectiveRuntimeState =
            state?.ObjectiveRuntimeState;
        if (state == null || objectiveRuntimeState == null)
            return Empty;

        int enemyUnitCount = 0;
        int aliveEnemyUnitCount = 0;
        foreach (StringName enemyUnitId in state.GetEnemyUnitIdsTyped())
        {
            enemyUnitCount++;
            if (state.GetUnit(enemyUnitId)?.is_alive == true)
                aliveEnemyUnitCount++;
        }

        return objectiveRuntimeState switch
        {
            BattleEliminationObjectiveRuntimeState => new BattleObjectiveProgressSnapshot(
                BattleObjectiveMode.Elimination,
                "",
                "",
                "",
                false,
                false,
                false,
                "",
                BattleMapEdge.Unknown,
                0,
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                Array.Empty<Vector2I>(),
                enemyUnitCount,
                aliveEnemyUnitCount
            ),
            BattleBossObjectiveRuntimeState bossObjective =>
                CaptureBoss(
                    state,
                    bossObjective,
                    enemyUnitCount,
                    aliveEnemyUnitCount
                ),
            BattleRescueObjectiveRuntimeState rescueObjective =>
                CaptureRescue(
                    state,
                    rescueObjective,
                    enemyUnitCount,
                    aliveEnemyUnitCount
                ),
            BattleEscapeObjectiveRuntimeState escapeObjective =>
                CaptureEscape(
                    state,
                    escapeObjective,
                    enemyUnitCount,
                    aliveEnemyUnitCount
                ),
            BattleEscortObjectiveRuntimeState escortObjective =>
                CaptureEscort(
                    state,
                    escortObjective,
                    enemyUnitCount,
                    aliveEnemyUnitCount
                ),
            BattleDefenseObjectiveRuntimeState defenseObjective =>
                CaptureDefense(
                    state,
                    defenseObjective,
                    enemyUnitCount,
                    aliveEnemyUnitCount
                ),
            BattleInterceptObjectiveRuntimeState interceptObjective =>
                CaptureIntercept(
                    state,
                    interceptObjective,
                    enemyUnitCount,
                    aliveEnemyUnitCount
                ),
            BattleNodeOperationObjectiveRuntimeState nodeOperationObjective =>
                CaptureNodeOperation(
                    state,
                    nodeOperationObjective,
                    enemyUnitCount,
                    aliveEnemyUnitCount
                ),
            BattleControlObjectiveRuntimeState controlObjective =>
                CaptureControl(
                    state,
                    controlObjective,
                    enemyUnitCount,
                    aliveEnemyUnitCount
                ),
            _ => Empty,
        };
    }

    private static BattleObjectiveProgressSnapshot CaptureBoss(
        BattleState state,
        BattleBossObjectiveRuntimeState objective,
        int enemyUnitCount,
        int aliveEnemyUnitCount
    )
    {
        BattleUnitState targetUnit = state.GetUnit(objective.TargetUnitId);
        string targetDisplayName =
            targetUnit != null && !string.IsNullOrWhiteSpace(targetUnit.display_name)
                ? targetUnit.display_name
                : objective.TargetUnitId.ToString();
        var aliveRequiredUnitIds = new List<StringName>();
        foreach (StringName unitId in objective.RequiredPartyUnitIds)
        {
            if (state.GetUnit(unitId)?.is_alive == true)
                aliveRequiredUnitIds.Add(unitId);
        }
        return new BattleObjectiveProgressSnapshot(
            BattleObjectiveMode.Boss,
            objective.TargetActorId,
            objective.TargetUnitId,
            targetDisplayName,
            targetUnit?.is_alive == true,
            false,
            false,
            "",
            BattleMapEdge.Unknown,
            0,
            objective.RequiredPartyUnitIds,
            aliveRequiredUnitIds,
            Array.Empty<StringName>(),
            Array.Empty<Vector2I>(),
            enemyUnitCount,
            aliveEnemyUnitCount
        );
    }

    private static BattleObjectiveProgressSnapshot CaptureRescue(
        BattleState state,
        BattleRescueObjectiveRuntimeState objective,
        int enemyUnitCount,
        int aliveEnemyUnitCount
    )
    {
        BattleUnitState targetUnit = state.GetUnit(objective.TargetUnitId);
        return new BattleObjectiveProgressSnapshot(
            BattleObjectiveMode.Rescue,
            objective.TargetActorId,
            objective.TargetUnitId,
            ResolveDisplayName(targetUnit, objective.TargetUnitId),
            targetUnit?.is_alive == true,
            objective.TargetSecured,
            false,
            "",
            BattleMapEdge.Unknown,
            0,
            objective.RequiredPartyUnitIds,
            AliveUnitIds(state, objective.RequiredPartyUnitIds),
            Array.Empty<StringName>(),
            Array.Empty<Vector2I>(),
            enemyUnitCount,
            aliveEnemyUnitCount
        );
    }

    private static BattleObjectiveProgressSnapshot CaptureEscape(
        BattleState state,
        BattleEscapeObjectiveRuntimeState objective,
        int enemyUnitCount,
        int aliveEnemyUnitCount
    )
    {
        var aliveRequiredUnitIds = new List<StringName>();
        var reachedExitUnitIds = new List<StringName>();
        foreach (StringName unitId in objective.RequiredUnitIds)
        {
            BattleUnitState unit = state.GetUnit(unitId);
            if (unit?.is_alive != true)
                continue;
            aliveRequiredUnitIds.Add(unitId);
            if (IsFullyInsideExit(unit, objective))
                reachedExitUnitIds.Add(unitId);
        }
        return new BattleObjectiveProgressSnapshot(
            BattleObjectiveMode.Escape,
            "",
            "",
            "",
            false,
            false,
            false,
            objective.ExitZoneId,
            objective.ExitEdge,
            objective.ExitDepth,
            objective.RequiredUnitIds,
            aliveRequiredUnitIds,
            reachedExitUnitIds,
            objective.ExitCoords,
            enemyUnitCount,
            aliveEnemyUnitCount
        );
    }

    private static BattleObjectiveProgressSnapshot CaptureEscort(
        BattleState state,
        BattleEscortObjectiveRuntimeState objective,
        int enemyUnitCount,
        int aliveEnemyUnitCount
    )
    {
        BattleUnitState targetUnit = state.GetUnit(objective.TargetUnitId);
        bool reachedExit = IsFullyInsideExit(targetUnit, objective);
        return new BattleObjectiveProgressSnapshot(
            BattleObjectiveMode.Escort,
            objective.TargetActorId,
            objective.TargetUnitId,
            ResolveDisplayName(targetUnit, objective.TargetUnitId),
            targetUnit?.is_alive == true,
            false,
            reachedExit,
            objective.ExitZoneId,
            objective.ExitEdge,
            objective.ExitDepth,
            objective.RequiredPartyUnitIds,
            AliveUnitIds(state, objective.RequiredPartyUnitIds),
            reachedExit
                ? new[] { objective.TargetUnitId }
                : Array.Empty<StringName>(),
            objective.ExitCoords,
            enemyUnitCount,
            aliveEnemyUnitCount
        );
    }

    private static BattleObjectiveProgressSnapshot CaptureDefense(
        BattleState state,
        BattleDefenseObjectiveRuntimeState objective,
        int enemyUnitCount,
        int aliveEnemyUnitCount
    )
    {
        BattleUnitState targetUnit = state.GetUnit(objective.TargetUnitId);
        return new BattleObjectiveProgressSnapshot(
            BattleObjectiveMode.Defense,
            objective.TargetActorId,
            objective.TargetUnitId,
            ResolveDisplayName(targetUnit, objective.TargetUnitId),
            targetUnit?.is_alive == true,
            false,
            false,
            "",
            BattleMapEdge.Unknown,
            0,
            objective.RequiredPartyUnitIds,
            AliveUnitIds(state, objective.RequiredPartyUnitIds),
            Array.Empty<StringName>(),
            Array.Empty<Vector2I>(),
            enemyUnitCount,
            aliveEnemyUnitCount,
            state.timeline?.current_tu ?? 0,
            objective.StartTu,
            objective.DeadlineTu
        );
    }

    private static BattleObjectiveProgressSnapshot CaptureIntercept(
        BattleState state,
        BattleInterceptObjectiveRuntimeState objective,
        int enemyUnitCount,
        int aliveEnemyUnitCount
    )
    {
        BattleUnitState targetUnit = state.GetUnit(objective.TargetUnitId);
        bool reachedExit = IsFullyInsideExit(targetUnit, objective);
        return new BattleObjectiveProgressSnapshot(
            BattleObjectiveMode.Intercept,
            objective.TargetActorId,
            objective.TargetUnitId,
            ResolveDisplayName(targetUnit, objective.TargetUnitId),
            targetUnit?.is_alive == true,
            false,
            reachedExit,
            objective.ExitZoneId,
            objective.ExitEdge,
            objective.ExitDepth,
            objective.RequiredPartyUnitIds,
            AliveUnitIds(state, objective.RequiredPartyUnitIds),
            reachedExit
                ? new[] { objective.TargetUnitId }
                : Array.Empty<StringName>(),
            objective.ExitCoords,
            enemyUnitCount,
            aliveEnemyUnitCount
        );
    }

    private static BattleObjectiveProgressSnapshot CaptureNodeOperation(
        BattleState state,
        BattleNodeOperationObjectiveRuntimeState objective,
        int enemyUnitCount,
        int aliveEnemyUnitCount
    )
    {
        var nodes = new List<BattleObjectiveNodeProgressSnapshot>();
        foreach (BattleOperationNodeRuntimeState node in objective.OperationNodes)
        {
            nodes.Add(
                new BattleObjectiveNodeProgressSnapshot(
                    node.NodeId,
                    node.DisplayName,
                    node.ZoneId,
                    node.Coord,
                    node.IsCompleted
                )
            );
        }
        return new BattleObjectiveProgressSnapshot(
            BattleObjectiveMode.NodeOperation,
            "",
            "",
            "",
            false,
            false,
            false,
            "",
            BattleMapEdge.Unknown,
            0,
            objective.RequiredPartyUnitIds,
            AliveUnitIds(state, objective.RequiredPartyUnitIds),
            Array.Empty<StringName>(),
            Array.Empty<Vector2I>(),
            enemyUnitCount,
            aliveEnemyUnitCount,
            operationNodes: nodes
        );
    }

    private static BattleObjectiveProgressSnapshot CaptureControl(
        BattleState state,
        BattleControlObjectiveRuntimeState objective,
        int enemyUnitCount,
        int aliveEnemyUnitCount
    )
    {
        var zones = new List<BattleObjectiveControlZoneProgressSnapshot>();
        foreach (BattleControlZoneRuntimeState zone in objective.ControlZones)
        {
            zones.Add(
                new BattleObjectiveControlZoneProgressSnapshot(
                    zone.ZoneId,
                    zone.DisplayName,
                    zone.PlacementEdge,
                    zone.PlacementDepth,
                    zone.Coords,
                    BattleControlObjectiveRules.ResolveOccupancy(state, zone)
                )
            );
        }
        return new BattleObjectiveProgressSnapshot(
            BattleObjectiveMode.Control,
            "",
            "",
            "",
            false,
            false,
            false,
            "",
            BattleMapEdge.Unknown,
            0,
            objective.RequiredPartyUnitIds,
            AliveUnitIds(state, objective.RequiredPartyUnitIds),
            Array.Empty<StringName>(),
            Array.Empty<Vector2I>(),
            enemyUnitCount,
            aliveEnemyUnitCount,
            currentTu: state.timeline?.current_tu ?? 0,
            controlZones: zones,
            playerControlScore: objective.PlayerScore,
            hostileControlScore: objective.HostileScore,
            controlScoreTarget: objective.ScoreTarget
        );
    }

    private static bool IsFullyInsideExit(
        BattleUnitState unit,
        BattleEscapeObjectiveRuntimeState objective
    )
    {
        if (
            unit?.occupied_coords == null
            || unit.occupied_coords.Count == 0
            || objective == null
        )
        {
            return false;
        }
        foreach (Vector2I occupiedCoord in unit.occupied_coords)
        {
            if (!objective.ContainsExitCoord(occupiedCoord))
                return false;
        }
        return true;
    }

    private static bool IsFullyInsideExit(
        BattleUnitState unit,
        BattleEscortObjectiveRuntimeState objective
    )
    {
        if (
            unit?.occupied_coords == null
            || unit.occupied_coords.Count == 0
            || objective == null
        )
        {
            return false;
        }
        foreach (Vector2I occupiedCoord in unit.occupied_coords)
        {
            if (!objective.ContainsExitCoord(occupiedCoord))
                return false;
        }
        return true;
    }

    private static bool IsFullyInsideExit(
        BattleUnitState unit,
        BattleInterceptObjectiveRuntimeState objective
    )
    {
        if (
            unit?.occupied_coords == null
            || unit.occupied_coords.Count == 0
            || objective == null
        )
        {
            return false;
        }
        foreach (Vector2I occupiedCoord in unit.occupied_coords)
        {
            if (!objective.ContainsExitCoord(occupiedCoord))
                return false;
        }
        return true;
    }

    private static string ResolveDisplayName(
        BattleUnitState unit,
        StringName fallbackId
    ) =>
        unit != null && !string.IsNullOrWhiteSpace(unit.display_name)
            ? unit.display_name
            : fallbackId.ToString();

    private static IReadOnlyList<StringName> AliveUnitIds(
        BattleState state,
        IEnumerable<StringName> unitIds
    )
    {
        var result = new List<StringName>();
        foreach (StringName unitId in unitIds ?? Array.Empty<StringName>())
        {
            if (state?.GetUnit(unitId)?.is_alive == true)
                result.Add(unitId);
        }
        return result;
    }

    private static ReadOnlyCollection<StringName> CopyStringNames(
        IEnumerable<StringName> source
    )
    {
        var result = new List<StringName>();
        foreach (StringName value in source ?? Array.Empty<StringName>())
            result.Add(value);
        return result.AsReadOnly();
    }

    private static ReadOnlyCollection<Vector2I> CopyCoords(
        IEnumerable<Vector2I> source
    )
    {
        var result = new List<Vector2I>();
        foreach (Vector2I value in source ?? Array.Empty<Vector2I>())
            result.Add(value);
        return result.AsReadOnly();
    }
}
