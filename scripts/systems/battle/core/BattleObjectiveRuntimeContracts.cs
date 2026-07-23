using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

internal enum BattleOutcomeKind
{
    Unknown = 0,
    PlayerSuccess,
    PlayerFailure,
    Draw,
}

internal enum BattleEndReasonKind
{
    None = 0,
    EliminationHostilesDefeated,
    EliminationAlliesDefeated,
    EliminationMutualDestruction,
    BossTargetDefeated,
    BossPartyDefeated,
    BossMutualDestruction,
    RescueTargetSecured,
    RescueTargetDefeated,
    RescuePartyDefeated,
    EscapeRequiredUnitsReachedExit,
    EscapeRequiredUnitDefeated,
    EscortTargetReachedExit,
    EscortTargetDefeated,
    EscortPartyDefeated,
    InterceptTargetDefeated,
    InterceptTargetEscaped,
    InterceptPartyDefeated,
    InterceptMutualDestruction,
    DefenseDeadlineReached,
    DefenseTargetDefeated,
    DefensePartyDefeated,
    NodeOperationAllNodesCompleted,
    NodeOperationPartyDefeated,
    ControlPlayerScoreReached,
    ControlHostileScoreReached,
    ControlScoresTied,
    ControlPartyDefeated,
}

internal enum BattleObjectiveEvaluationKind
{
    Invalid = 0,
    InProgress,
    Terminal,
}

internal enum BattleOutcomeFlushResult
{
    InvalidObjective = 0,
    NoChange,
    DecisionLatched,
    Completed,
    AlreadyCompleted,
}

internal abstract class BattleObjectiveRuntimeState
{
    protected BattleObjectiveRuntimeState(BattleObjectiveMode mode)
    {
        Mode = mode;
    }

    internal BattleObjectiveMode Mode { get; }

    internal abstract BattleObjectiveRuntimeState DuplicateState();
}

internal sealed class BattleEliminationObjectiveRuntimeState
    : BattleObjectiveRuntimeState
{
    internal BattleEliminationObjectiveRuntimeState()
        : base(BattleObjectiveMode.Elimination) { }

    internal override BattleObjectiveRuntimeState DuplicateState() =>
        new BattleEliminationObjectiveRuntimeState();
}

internal sealed class BattleBossObjectiveRuntimeState : BattleObjectiveRuntimeState
{
    internal BattleBossObjectiveRuntimeState(
        StringName targetActorId,
        StringName targetUnitId,
        IEnumerable<StringName> requiredPartyUnitIds
    )
        : base(BattleObjectiveMode.Boss)
    {
        if (targetActorId == "")
            throw new ArgumentException(
                "Boss objective target actor id must not be empty.",
                nameof(targetActorId)
            );
        if (targetUnitId == "")
            throw new ArgumentException(
                "Boss objective target unit id must not be empty.",
                nameof(targetUnitId)
            );
        IReadOnlyList<StringName> normalizedPartyIds = NormalizeUnitIds(
            requiredPartyUnitIds
        );
        if (normalizedPartyIds.Count == 0)
            throw new ArgumentException(
                "Boss objective requires at least one persistent party unit.",
                nameof(requiredPartyUnitIds)
            );
        TargetActorId = targetActorId;
        TargetUnitId = targetUnitId;
        RequiredPartyUnitIds = normalizedPartyIds;
    }

    internal StringName TargetActorId { get; }
    internal StringName TargetUnitId { get; }
    internal IReadOnlyList<StringName> RequiredPartyUnitIds { get; }

    internal override BattleObjectiveRuntimeState DuplicateState() =>
        new BattleBossObjectiveRuntimeState(
            TargetActorId,
            TargetUnitId,
            RequiredPartyUnitIds
        );

    private static IReadOnlyList<StringName> NormalizeUnitIds(
        IEnumerable<StringName> unitIds
    )
    {
        var normalized = (unitIds ?? Array.Empty<StringName>())
            .Where(unitId => unitId != "")
            .Select(unitId => unitId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(unitId => unitId, StringComparer.Ordinal)
            .Select(unitId => new StringName(unitId))
            .ToList();
        return new ReadOnlyCollection<StringName>(normalized);
    }
}

internal sealed class BattleEscapeObjectiveRuntimeState : BattleObjectiveRuntimeState
{
    private readonly HashSet<Vector2I> _exitCoordSet;

    internal BattleEscapeObjectiveRuntimeState(
        StringName exitZoneId,
        BattleMapEdge exitEdge,
        int exitDepth,
        IEnumerable<StringName> requiredUnitIds,
        IEnumerable<Vector2I> exitCoords
    )
        : base(BattleObjectiveMode.Escape)
    {
        if (exitZoneId == "")
            throw new ArgumentException(
                "Escape objective exit zone id must not be empty.",
                nameof(exitZoneId)
            );
        if (!Enum.IsDefined(exitEdge) || exitEdge == BattleMapEdge.Unknown)
            throw new ArgumentOutOfRangeException(nameof(exitEdge));
        if (exitDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(exitDepth));

        List<StringName> normalizedUnitIds = (requiredUnitIds ?? Array.Empty<StringName>())
            .Where(unitId => unitId != "")
            .Select(unitId => unitId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(unitId => unitId, StringComparer.Ordinal)
            .Select(unitId => new StringName(unitId))
            .ToList();
        if (normalizedUnitIds.Count == 0)
            throw new ArgumentException(
                "Escape objective requires at least one persistent party unit.",
                nameof(requiredUnitIds)
            );

        List<Vector2I> normalizedExitCoords = (exitCoords ?? Array.Empty<Vector2I>())
            .Distinct()
            .OrderBy(coord => coord.Y)
            .ThenBy(coord => coord.X)
            .ToList();
        if (normalizedExitCoords.Count == 0)
            throw new ArgumentException(
                "Escape objective exit zone must contain at least one valid cell.",
                nameof(exitCoords)
            );

        ExitZoneId = exitZoneId;
        ExitEdge = exitEdge;
        ExitDepth = exitDepth;
        RequiredUnitIds = new ReadOnlyCollection<StringName>(normalizedUnitIds);
        ExitCoords = new ReadOnlyCollection<Vector2I>(normalizedExitCoords);
        _exitCoordSet = new HashSet<Vector2I>(normalizedExitCoords);
    }

    internal StringName ExitZoneId { get; }
    internal BattleMapEdge ExitEdge { get; }
    internal int ExitDepth { get; }
    internal IReadOnlyList<StringName> RequiredUnitIds { get; }
    internal IReadOnlyList<Vector2I> ExitCoords { get; }

    internal bool ContainsExitCoord(Vector2I coord) => _exitCoordSet.Contains(coord);

    internal override BattleObjectiveRuntimeState DuplicateState() =>
        new BattleEscapeObjectiveRuntimeState(
            ExitZoneId,
            ExitEdge,
            ExitDepth,
            RequiredUnitIds,
            ExitCoords
        );
}

internal sealed class BattleRescueObjectiveRuntimeState : BattleObjectiveRuntimeState
{
    internal BattleRescueObjectiveRuntimeState(
        StringName targetActorId,
        StringName targetUnitId,
        IEnumerable<StringName> requiredPartyUnitIds,
        bool targetSecured = false
    )
        : base(BattleObjectiveMode.Rescue)
    {
        if (targetActorId == "")
            throw new ArgumentException(
                "Rescue objective target actor id must not be empty.",
                nameof(targetActorId)
            );
        if (targetUnitId == "")
            throw new ArgumentException(
                "Rescue objective target unit id must not be empty.",
                nameof(targetUnitId)
            );
        IReadOnlyList<StringName> normalizedPartyIds = NormalizeUnitIds(
            requiredPartyUnitIds
        );
        if (normalizedPartyIds.Count == 0)
            throw new ArgumentException(
                "Rescue objective requires at least one persistent party unit.",
                nameof(requiredPartyUnitIds)
            );
        TargetActorId = targetActorId;
        TargetUnitId = targetUnitId;
        RequiredPartyUnitIds = normalizedPartyIds;
        TargetSecured = targetSecured;
    }

    internal StringName TargetActorId { get; }
    internal StringName TargetUnitId { get; }
    internal IReadOnlyList<StringName> RequiredPartyUnitIds { get; }
    internal bool TargetSecured { get; private set; }

    internal bool TrySecureTarget()
    {
        if (TargetSecured)
            return false;
        TargetSecured = true;
        return true;
    }

    internal override BattleObjectiveRuntimeState DuplicateState() =>
        new BattleRescueObjectiveRuntimeState(
            TargetActorId,
            TargetUnitId,
            RequiredPartyUnitIds,
            TargetSecured
        );

    private static IReadOnlyList<StringName> NormalizeUnitIds(
        IEnumerable<StringName> unitIds
    )
    {
        List<StringName> normalized = (unitIds ?? Array.Empty<StringName>())
            .Where(unitId => unitId != "")
            .Select(unitId => unitId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(unitId => unitId, StringComparer.Ordinal)
            .Select(unitId => new StringName(unitId))
            .ToList();
        return new ReadOnlyCollection<StringName>(normalized);
    }
}

internal sealed class BattleEscortObjectiveRuntimeState : BattleObjectiveRuntimeState
{
    private readonly HashSet<Vector2I> _exitCoordSet;

    internal BattleEscortObjectiveRuntimeState(
        StringName targetActorId,
        StringName targetUnitId,
        StringName exitZoneId,
        BattleMapEdge exitEdge,
        int exitDepth,
        IEnumerable<StringName> requiredPartyUnitIds,
        IEnumerable<Vector2I> exitCoords
    )
        : base(BattleObjectiveMode.Escort)
    {
        if (targetActorId == "")
            throw new ArgumentException(
                "Escort objective target actor id must not be empty.",
                nameof(targetActorId)
            );
        if (targetUnitId == "")
            throw new ArgumentException(
                "Escort objective target unit id must not be empty.",
                nameof(targetUnitId)
            );
        if (exitZoneId == "")
            throw new ArgumentException(
                "Escort objective exit zone id must not be empty.",
                nameof(exitZoneId)
            );
        if (!Enum.IsDefined(exitEdge) || exitEdge == BattleMapEdge.Unknown)
            throw new ArgumentOutOfRangeException(nameof(exitEdge));
        if (exitDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(exitDepth));
        List<StringName> normalizedPartyIds = (
            requiredPartyUnitIds ?? Array.Empty<StringName>()
        )
            .Where(unitId => unitId != "")
            .Select(unitId => unitId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(unitId => unitId, StringComparer.Ordinal)
            .Select(unitId => new StringName(unitId))
            .ToList();
        if (normalizedPartyIds.Count == 0)
            throw new ArgumentException(
                "Escort objective requires at least one persistent party unit.",
                nameof(requiredPartyUnitIds)
            );
        List<Vector2I> normalizedExitCoords = (
            exitCoords ?? Array.Empty<Vector2I>()
        )
            .Distinct()
            .OrderBy(coord => coord.Y)
            .ThenBy(coord => coord.X)
            .ToList();
        if (normalizedExitCoords.Count == 0)
            throw new ArgumentException(
                "Escort objective exit zone must contain at least one valid cell.",
                nameof(exitCoords)
            );
        TargetActorId = targetActorId;
        TargetUnitId = targetUnitId;
        ExitZoneId = exitZoneId;
        ExitEdge = exitEdge;
        ExitDepth = exitDepth;
        RequiredPartyUnitIds = new ReadOnlyCollection<StringName>(
            normalizedPartyIds
        );
        ExitCoords = new ReadOnlyCollection<Vector2I>(normalizedExitCoords);
        _exitCoordSet = new HashSet<Vector2I>(normalizedExitCoords);
    }

    internal StringName TargetActorId { get; }
    internal StringName TargetUnitId { get; }
    internal StringName ExitZoneId { get; }
    internal BattleMapEdge ExitEdge { get; }
    internal int ExitDepth { get; }
    internal IReadOnlyList<StringName> RequiredPartyUnitIds { get; }
    internal IReadOnlyList<Vector2I> ExitCoords { get; }

    internal bool ContainsExitCoord(Vector2I coord) => _exitCoordSet.Contains(coord);

    internal override BattleObjectiveRuntimeState DuplicateState() =>
        new BattleEscortObjectiveRuntimeState(
            TargetActorId,
            TargetUnitId,
            ExitZoneId,
            ExitEdge,
            ExitDepth,
            RequiredPartyUnitIds,
            ExitCoords
        );
}

internal sealed class BattleDefenseObjectiveRuntimeState
    : BattleObjectiveRuntimeState
{
    internal BattleDefenseObjectiveRuntimeState(
        StringName targetActorId,
        StringName targetUnitId,
        IEnumerable<StringName> requiredPartyUnitIds,
        int startTu,
        int deadlineTu
    )
        : base(BattleObjectiveMode.Defense)
    {
        if (targetActorId == "")
            throw new ArgumentException(
                "Defense objective target actor id must not be empty.",
                nameof(targetActorId)
            );
        if (targetUnitId == "")
            throw new ArgumentException(
                "Defense objective target unit id must not be empty.",
                nameof(targetUnitId)
            );
        if (startTu < 0)
            throw new ArgumentOutOfRangeException(nameof(startTu));
        if (
            deadlineTu <= startTu
            || (deadlineTu - startTu) % BattleTimelineState.TuGranularity != 0
        )
        {
            throw new ArgumentOutOfRangeException(nameof(deadlineTu));
        }
        List<StringName> normalizedPartyIds = (
            requiredPartyUnitIds ?? Array.Empty<StringName>()
        )
            .Where(unitId => unitId != "")
            .Select(unitId => unitId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(unitId => unitId, StringComparer.Ordinal)
            .Select(unitId => new StringName(unitId))
            .ToList();
        if (normalizedPartyIds.Count == 0)
            throw new ArgumentException(
                "Defense objective requires at least one persistent party unit.",
                nameof(requiredPartyUnitIds)
            );

        TargetActorId = targetActorId;
        TargetUnitId = targetUnitId;
        RequiredPartyUnitIds = new ReadOnlyCollection<StringName>(
            normalizedPartyIds
        );
        StartTu = startTu;
        DeadlineTu = deadlineTu;
    }

    internal StringName TargetActorId { get; }
    internal StringName TargetUnitId { get; }
    internal IReadOnlyList<StringName> RequiredPartyUnitIds { get; }
    internal int StartTu { get; }
    internal int DeadlineTu { get; }

    internal override BattleObjectiveRuntimeState DuplicateState() =>
        new BattleDefenseObjectiveRuntimeState(
            TargetActorId,
            TargetUnitId,
            RequiredPartyUnitIds,
            StartTu,
            DeadlineTu
        );
}

internal sealed class BattleInterceptObjectiveRuntimeState
    : BattleObjectiveRuntimeState
{
    private readonly HashSet<Vector2I> _exitCoordSet;

    internal BattleInterceptObjectiveRuntimeState(
        StringName targetActorId,
        StringName targetUnitId,
        StringName exitZoneId,
        BattleMapEdge exitEdge,
        int exitDepth,
        IEnumerable<StringName> requiredPartyUnitIds,
        IEnumerable<Vector2I> exitCoords
    )
        : base(BattleObjectiveMode.Intercept)
    {
        if (targetActorId == "")
            throw new ArgumentException(
                "Intercept objective target actor id must not be empty.",
                nameof(targetActorId)
            );
        if (targetUnitId == "")
            throw new ArgumentException(
                "Intercept objective target unit id must not be empty.",
                nameof(targetUnitId)
            );
        if (exitZoneId == "")
            throw new ArgumentException(
                "Intercept objective exit zone id must not be empty.",
                nameof(exitZoneId)
            );
        if (!Enum.IsDefined(exitEdge) || exitEdge == BattleMapEdge.Unknown)
            throw new ArgumentOutOfRangeException(nameof(exitEdge));
        if (exitDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(exitDepth));

        List<StringName> normalizedPartyIds = (
            requiredPartyUnitIds ?? Array.Empty<StringName>()
        )
            .Where(unitId => unitId != "")
            .Select(unitId => unitId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(unitId => unitId, StringComparer.Ordinal)
            .Select(unitId => new StringName(unitId))
            .ToList();
        if (normalizedPartyIds.Count == 0)
            throw new ArgumentException(
                "Intercept objective requires at least one persistent party unit.",
                nameof(requiredPartyUnitIds)
            );
        List<Vector2I> normalizedExitCoords = (
            exitCoords ?? Array.Empty<Vector2I>()
        )
            .Distinct()
            .OrderBy(coord => coord.Y)
            .ThenBy(coord => coord.X)
            .ToList();
        if (normalizedExitCoords.Count == 0)
            throw new ArgumentException(
                "Intercept objective exit zone must contain at least one valid cell.",
                nameof(exitCoords)
            );

        TargetActorId = targetActorId;
        TargetUnitId = targetUnitId;
        ExitZoneId = exitZoneId;
        ExitEdge = exitEdge;
        ExitDepth = exitDepth;
        RequiredPartyUnitIds = new ReadOnlyCollection<StringName>(
            normalizedPartyIds
        );
        ExitCoords = new ReadOnlyCollection<Vector2I>(normalizedExitCoords);
        _exitCoordSet = new HashSet<Vector2I>(normalizedExitCoords);
    }

    internal StringName TargetActorId { get; }
    internal StringName TargetUnitId { get; }
    internal StringName ExitZoneId { get; }
    internal BattleMapEdge ExitEdge { get; }
    internal int ExitDepth { get; }
    internal IReadOnlyList<StringName> RequiredPartyUnitIds { get; }
    internal IReadOnlyList<Vector2I> ExitCoords { get; }

    internal bool ContainsExitCoord(Vector2I coord) => _exitCoordSet.Contains(coord);

    internal override BattleObjectiveRuntimeState DuplicateState() =>
        new BattleInterceptObjectiveRuntimeState(
            TargetActorId,
            TargetUnitId,
            ExitZoneId,
            ExitEdge,
            ExitDepth,
            RequiredPartyUnitIds,
            ExitCoords
        );
}

internal sealed class BattleOperationNodeRuntimeState
{
    internal BattleOperationNodeRuntimeState(
        StringName nodeId,
        string displayName,
        StringName zoneId,
        BattleMapEdge placementEdge,
        int placementDepth,
        Vector2I coord,
        bool isCompleted = false
    )
    {
        if (nodeId == "")
            throw new ArgumentException(
                "Operation node id must not be empty.",
                nameof(nodeId)
            );
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException(
                "Operation node display name must not be empty.",
                nameof(displayName)
            );
        if (zoneId == "")
            throw new ArgumentException(
                "Operation node zone id must not be empty.",
                nameof(zoneId)
            );
        if (
            !Enum.IsDefined(placementEdge)
            || placementEdge == BattleMapEdge.Unknown
        )
        {
            throw new ArgumentOutOfRangeException(nameof(placementEdge));
        }
        if (placementDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(placementDepth));

        NodeId = nodeId;
        DisplayName = displayName;
        ZoneId = zoneId;
        PlacementEdge = placementEdge;
        PlacementDepth = placementDepth;
        Coord = coord;
        IsCompleted = isCompleted;
    }

    internal StringName NodeId { get; }
    internal string DisplayName { get; }
    internal StringName ZoneId { get; }
    internal BattleMapEdge PlacementEdge { get; }
    internal int PlacementDepth { get; }
    internal Vector2I Coord { get; }
    internal bool IsCompleted { get; private set; }

    internal bool TryComplete()
    {
        if (IsCompleted)
            return false;
        IsCompleted = true;
        return true;
    }

    internal BattleOperationNodeRuntimeState DuplicateState() =>
        new(
            NodeId,
            DisplayName,
            ZoneId,
            PlacementEdge,
            PlacementDepth,
            Coord,
            IsCompleted
        );
}

internal sealed class BattleNodeOperationObjectiveRuntimeState
    : BattleObjectiveRuntimeState
{
    private readonly Dictionary<StringName, BattleOperationNodeRuntimeState>
        _nodesById;
    private readonly Dictionary<Vector2I, BattleOperationNodeRuntimeState>
        _nodesByCoord;

    internal BattleNodeOperationObjectiveRuntimeState(
        IEnumerable<StringName> requiredPartyUnitIds,
        IEnumerable<BattleOperationNodeRuntimeState> operationNodes
    )
        : base(BattleObjectiveMode.NodeOperation)
    {
        List<StringName> normalizedPartyIds = (
            requiredPartyUnitIds ?? Array.Empty<StringName>()
        )
            .Where(unitId => unitId != "")
            .Select(unitId => unitId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(unitId => unitId, StringComparer.Ordinal)
            .Select(unitId => new StringName(unitId))
            .ToList();
        if (normalizedPartyIds.Count == 0)
        {
            throw new ArgumentException(
                "Node operation objective requires at least one persistent party unit.",
                nameof(requiredPartyUnitIds)
            );
        }

        List<BattleOperationNodeRuntimeState> normalizedNodes = (
            operationNodes ?? Array.Empty<BattleOperationNodeRuntimeState>()
        )
            .Where(node => node != null)
            .OrderBy(node => node.NodeId.ToString(), StringComparer.Ordinal)
            .Select(node => node.DuplicateState())
            .ToList();
        if (normalizedNodes.Count == 0)
        {
            throw new ArgumentException(
                "Node operation objective requires at least one operation node.",
                nameof(operationNodes)
            );
        }
        if (
            normalizedNodes
                .Select(node => node.NodeId.ToString())
                .Distinct(StringComparer.Ordinal)
                .Count()
            != normalizedNodes.Count
            || normalizedNodes.Select(node => node.Coord).Distinct().Count()
                != normalizedNodes.Count
        )
        {
            throw new ArgumentException(
                "Node operation runtime node ids and coordinates must be unique.",
                nameof(operationNodes)
            );
        }

        RequiredPartyUnitIds = new ReadOnlyCollection<StringName>(
            normalizedPartyIds
        );
        OperationNodes = new ReadOnlyCollection<BattleOperationNodeRuntimeState>(
            normalizedNodes
        );
        _nodesById = normalizedNodes.ToDictionary(node => node.NodeId);
        _nodesByCoord = normalizedNodes.ToDictionary(node => node.Coord);
    }

    internal IReadOnlyList<StringName> RequiredPartyUnitIds { get; }
    internal IReadOnlyList<BattleOperationNodeRuntimeState> OperationNodes { get; }
    internal int CompletedNodeCount =>
        OperationNodes.Count(node => node.IsCompleted);
    internal int IncompleteNodeCount => OperationNodes.Count - CompletedNodeCount;
    internal bool AllNodesCompleted => IncompleteNodeCount == 0;

    internal bool TryGetNodeAtCoord(
        Vector2I coord,
        out BattleOperationNodeRuntimeState node
    ) => _nodesByCoord.TryGetValue(coord, out node);

    internal bool TryCompleteNode(StringName nodeId)
    {
        return nodeId != ""
            && _nodesById.TryGetValue(nodeId, out BattleOperationNodeRuntimeState node)
            && node.TryComplete();
    }

    internal override BattleObjectiveRuntimeState DuplicateState() =>
        new BattleNodeOperationObjectiveRuntimeState(
            RequiredPartyUnitIds,
            OperationNodes
        );
}

internal sealed class BattleControlZoneRuntimeState
{
    private readonly HashSet<Vector2I> _coordSet;

    internal BattleControlZoneRuntimeState(
        StringName zoneId,
        string displayName,
        BattleMapEdge placementEdge,
        int placementDepth,
        IEnumerable<Vector2I> coords
    )
    {
        if (zoneId == "")
            throw new ArgumentException(
                "Control zone id must not be empty.",
                nameof(zoneId)
            );
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException(
                "Control zone display name must not be empty.",
                nameof(displayName)
            );
        if (
            !Enum.IsDefined(placementEdge)
            || placementEdge == BattleMapEdge.Unknown
        )
        {
            throw new ArgumentOutOfRangeException(nameof(placementEdge));
        }
        if (placementDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(placementDepth));

        List<Vector2I> normalizedCoords = (coords ?? Array.Empty<Vector2I>())
            .Distinct()
            .OrderBy(coord => coord.Y)
            .ThenBy(coord => coord.X)
            .ToList();
        if (normalizedCoords.Count == 0)
        {
            throw new ArgumentException(
                "Control zone must contain at least one valid cell.",
                nameof(coords)
            );
        }

        ZoneId = zoneId;
        DisplayName = displayName;
        PlacementEdge = placementEdge;
        PlacementDepth = placementDepth;
        Coords = new ReadOnlyCollection<Vector2I>(normalizedCoords);
        _coordSet = new HashSet<Vector2I>(normalizedCoords);
    }

    internal StringName ZoneId { get; }
    internal string DisplayName { get; }
    internal BattleMapEdge PlacementEdge { get; }
    internal int PlacementDepth { get; }
    internal IReadOnlyList<Vector2I> Coords { get; }

    internal bool ContainsCoord(Vector2I coord) => _coordSet.Contains(coord);

    internal BattleControlZoneRuntimeState DuplicateState() =>
        new(
            ZoneId,
            DisplayName,
            PlacementEdge,
            PlacementDepth,
            Coords
        );
}

internal sealed class BattleControlObjectiveRuntimeState
    : BattleObjectiveRuntimeState
{
    internal BattleControlObjectiveRuntimeState(
        IEnumerable<StringName> requiredPartyUnitIds,
        IEnumerable<BattleControlZoneRuntimeState> controlZones,
        int scoreTarget,
        int playerScore = 0,
        int hostileScore = 0
    )
        : base(BattleObjectiveMode.Control)
    {
        List<StringName> normalizedPartyIds = (
            requiredPartyUnitIds ?? Array.Empty<StringName>()
        )
            .Where(unitId => unitId != "")
            .Select(unitId => unitId.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(unitId => unitId, StringComparer.Ordinal)
            .Select(unitId => new StringName(unitId))
            .ToList();
        if (normalizedPartyIds.Count == 0)
        {
            throw new ArgumentException(
                "Control objective requires at least one persistent party unit.",
                nameof(requiredPartyUnitIds)
            );
        }

        List<BattleControlZoneRuntimeState> normalizedZones = (
            controlZones ?? Array.Empty<BattleControlZoneRuntimeState>()
        )
            .Where(zone => zone != null)
            .OrderBy(zone => zone.ZoneId.ToString(), StringComparer.Ordinal)
            .Select(zone => zone.DuplicateState())
            .ToList();
        if (normalizedZones.Count == 0)
        {
            throw new ArgumentException(
                "Control objective requires at least one control zone.",
                nameof(controlZones)
            );
        }
        if (
            normalizedZones
                .Select(zone => zone.ZoneId.ToString())
                .Distinct(StringComparer.Ordinal)
                .Count()
            != normalizedZones.Count
        )
        {
            throw new ArgumentException(
                "Control objective runtime zone ids must be unique.",
                nameof(controlZones)
            );
        }
        var occupiedCoords = new HashSet<Vector2I>();
        foreach (BattleControlZoneRuntimeState zone in normalizedZones)
        {
            foreach (Vector2I coord in zone.Coords)
            {
                if (!occupiedCoords.Add(coord))
                {
                    throw new ArgumentException(
                        "Control objective runtime zones must not overlap.",
                        nameof(controlZones)
                    );
                }
            }
        }
        if (
            scoreTarget <= 0
            || scoreTarget % BattleTimelineState.TuGranularity != 0
        )
        {
            throw new ArgumentOutOfRangeException(nameof(scoreTarget));
        }
        if (
            playerScore < 0
            || playerScore > scoreTarget
            || hostileScore < 0
            || hostileScore > scoreTarget
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(playerScore),
                "Control objective scores must stay inside the configured target range."
            );
        }

        RequiredPartyUnitIds = new ReadOnlyCollection<StringName>(
            normalizedPartyIds
        );
        ControlZones = new ReadOnlyCollection<BattleControlZoneRuntimeState>(
            normalizedZones
        );
        ScoreTarget = scoreTarget;
        PlayerScore = playerScore;
        HostileScore = hostileScore;
    }

    internal IReadOnlyList<StringName> RequiredPartyUnitIds { get; }
    internal IReadOnlyList<BattleControlZoneRuntimeState> ControlZones { get; }
    internal int ScoreTarget { get; }
    internal int PlayerScore { get; private set; }
    internal int HostileScore { get; private set; }

    internal bool TryAdvanceScores(
        int playerControlledZoneCount,
        int hostileControlledZoneCount,
        int tuDelta
    )
    {
        if (
            playerControlledZoneCount < 0
            || playerControlledZoneCount > ControlZones.Count
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(playerControlledZoneCount)
            );
        }
        if (
            hostileControlledZoneCount < 0
            || hostileControlledZoneCount > ControlZones.Count
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(hostileControlledZoneCount)
            );
        }
        if (
            tuDelta <= 0
            || tuDelta % BattleTimelineState.TuGranularity != 0
        )
        {
            throw new ArgumentOutOfRangeException(nameof(tuDelta));
        }

        int nextPlayerScore = ClampScoreIncrement(
            PlayerScore,
            playerControlledZoneCount,
            tuDelta
        );
        int nextHostileScore = ClampScoreIncrement(
            HostileScore,
            hostileControlledZoneCount,
            tuDelta
        );
        if (
            nextPlayerScore == PlayerScore
            && nextHostileScore == HostileScore
        )
        {
            return false;
        }
        PlayerScore = nextPlayerScore;
        HostileScore = nextHostileScore;
        return true;
    }

    internal override BattleObjectiveRuntimeState DuplicateState() =>
        new BattleControlObjectiveRuntimeState(
            RequiredPartyUnitIds,
            ControlZones,
            ScoreTarget,
            PlayerScore,
            HostileScore
        );

    private int ClampScoreIncrement(
        int currentScore,
        int controlledZoneCount,
        int tuDelta
    )
    {
        long increment = (long)controlledZoneCount * tuDelta;
        return (int)Math.Min((long)ScoreTarget, currentScore + increment);
    }
}

internal sealed class BattleFinalDecision
{
    internal BattleFinalDecision(
        BattleObjectiveMode objectiveMode,
        BattleOutcomeKind outcome,
        BattleEndReasonKind endReason,
        int decisionTu
    )
    {
        if (objectiveMode == BattleObjectiveMode.Unknown)
            throw new ArgumentOutOfRangeException(nameof(objectiveMode));
        if (outcome == BattleOutcomeKind.Unknown)
            throw new ArgumentOutOfRangeException(nameof(outcome));
        if (endReason == BattleEndReasonKind.None)
            throw new ArgumentOutOfRangeException(nameof(endReason));
        if (decisionTu < 0)
            throw new ArgumentOutOfRangeException(nameof(decisionTu));
        if (!IsSupportedCombination(objectiveMode, outcome, endReason))
        {
            throw new ArgumentException(
                $"Unsupported battle final decision combination: {objectiveMode}/{outcome}/{endReason}."
            );
        }
        ObjectiveMode = objectiveMode;
        Outcome = outcome;
        EndReason = endReason;
        DecisionTu = decisionTu;
    }

    internal BattleObjectiveMode ObjectiveMode { get; }
    internal BattleOutcomeKind Outcome { get; }
    internal BattleEndReasonKind EndReason { get; }
    internal int DecisionTu { get; }

    internal StringName WinnerFactionId =>
        BattleObjectiveRuntimeCodec.ToWinnerFactionId(Outcome);

    internal BattleFinalDecision DuplicateState() =>
        new(ObjectiveMode, Outcome, EndReason, DecisionTu);

    private static bool IsSupportedCombination(
        BattleObjectiveMode objectiveMode,
        BattleOutcomeKind outcome,
        BattleEndReasonKind endReason
    )
    {
        return (objectiveMode, outcome, endReason) switch
        {
            (
                BattleObjectiveMode.Elimination,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.EliminationHostilesDefeated
            ) => true,
            (
                BattleObjectiveMode.Elimination,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.EliminationAlliesDefeated
            ) => true,
            (
                BattleObjectiveMode.Elimination,
                BattleOutcomeKind.Draw,
                BattleEndReasonKind.EliminationMutualDestruction
            ) => true,
            (
                BattleObjectiveMode.Boss,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.BossTargetDefeated
            ) => true,
            (
                BattleObjectiveMode.Boss,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.BossPartyDefeated
            ) => true,
            (
                BattleObjectiveMode.Boss,
                BattleOutcomeKind.Draw,
                BattleEndReasonKind.BossMutualDestruction
            ) => true,
            (
                BattleObjectiveMode.Rescue,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.RescueTargetSecured
            ) => true,
            (
                BattleObjectiveMode.Rescue,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.RescueTargetDefeated
            ) => true,
            (
                BattleObjectiveMode.Rescue,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.RescuePartyDefeated
            ) => true,
            (
                BattleObjectiveMode.Escape,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.EscapeRequiredUnitsReachedExit
            ) => true,
            (
                BattleObjectiveMode.Escape,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.EscapeRequiredUnitDefeated
            ) => true,
            (
                BattleObjectiveMode.Escort,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.EscortTargetReachedExit
            ) => true,
            (
                BattleObjectiveMode.Escort,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.EscortTargetDefeated
            ) => true,
            (
                BattleObjectiveMode.Escort,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.EscortPartyDefeated
            ) => true,
            (
                BattleObjectiveMode.Defense,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.DefenseDeadlineReached
            ) => true,
            (
                BattleObjectiveMode.Defense,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.DefenseTargetDefeated
            ) => true,
            (
                BattleObjectiveMode.Defense,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.DefensePartyDefeated
            ) => true,
            (
                BattleObjectiveMode.Intercept,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.InterceptTargetDefeated
            ) => true,
            (
                BattleObjectiveMode.Intercept,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.InterceptTargetEscaped
            ) => true,
            (
                BattleObjectiveMode.Intercept,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.InterceptPartyDefeated
            ) => true,
            (
                BattleObjectiveMode.Intercept,
                BattleOutcomeKind.Draw,
                BattleEndReasonKind.InterceptMutualDestruction
            ) => true,
            (
                BattleObjectiveMode.NodeOperation,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.NodeOperationAllNodesCompleted
            ) => true,
            (
                BattleObjectiveMode.NodeOperation,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.NodeOperationPartyDefeated
            ) => true,
            (
                BattleObjectiveMode.Control,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.ControlPlayerScoreReached
            ) => true,
            (
                BattleObjectiveMode.Control,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.ControlHostileScoreReached
            ) => true,
            (
                BattleObjectiveMode.Control,
                BattleOutcomeKind.Draw,
                BattleEndReasonKind.ControlScoresTied
            ) => true,
            (
                BattleObjectiveMode.Control,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.ControlPartyDefeated
            ) => true,
            _ => false,
        };
    }
}

internal readonly struct BattleObjectiveEvaluationResult
{
    private BattleObjectiveEvaluationResult(
        BattleObjectiveEvaluationKind kind,
        BattleFinalDecision decision
    )
    {
        Kind = kind;
        Decision = decision;
    }

    internal BattleObjectiveEvaluationKind Kind { get; }
    internal BattleFinalDecision Decision { get; }

    internal static BattleObjectiveEvaluationResult Invalid() =>
        new(BattleObjectiveEvaluationKind.Invalid, null);

    internal static BattleObjectiveEvaluationResult InProgress() =>
        new(BattleObjectiveEvaluationKind.InProgress, null);

    internal static BattleObjectiveEvaluationResult Terminal(
        BattleFinalDecision decision
    ) =>
        new(
            BattleObjectiveEvaluationKind.Terminal,
            decision ?? throw new ArgumentNullException(nameof(decision))
        );
}
