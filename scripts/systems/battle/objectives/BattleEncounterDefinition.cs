using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

internal enum BattleObjectiveMode
{
    Unknown = 0,
    Elimination,
    Boss,
    Rescue,
    Escape,
    Escort,
    Defense,
    Intercept,
    NodeOperation,
    Control,
}

public enum BattleMapEdge
{
    Unknown = 0,
    Left,
    Right,
    Top,
    Bottom,
}

public enum BattleWorldResolutionMode
{
    Preserve = 0,
    Clear,
    Suppress,
}

internal abstract class BattleObjectiveDefinition
{
    protected BattleObjectiveDefinition(BattleObjectiveMode mode)
    {
        if (mode == BattleObjectiveMode.Unknown)
            throw new ArgumentOutOfRangeException(nameof(mode));
        Mode = mode;
    }

    internal BattleObjectiveMode Mode { get; }
}

internal sealed class BattleEliminationObjectiveDefinition : BattleObjectiveDefinition
{
    private BattleEliminationObjectiveDefinition()
        : base(BattleObjectiveMode.Elimination) { }

    internal static BattleEliminationObjectiveDefinition Instance { get; } = new();
}

internal sealed class BattleBossObjectiveDefinition : BattleObjectiveDefinition
{
    internal BattleBossObjectiveDefinition(StringName targetActorId)
        : base(BattleObjectiveMode.Boss)
    {
        if (targetActorId == "")
            throw new ArgumentException(
                "Boss objective target actor id must not be empty.",
                nameof(targetActorId)
            );
        TargetActorId = targetActorId;
    }

    internal StringName TargetActorId { get; }
}

internal sealed class BattleEscapeObjectiveDefinition : BattleObjectiveDefinition
{
    internal BattleEscapeObjectiveDefinition(
        StringName exitZoneId,
        BattleMapEdge exitEdge,
        int exitDepth
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
        ExitZoneId = exitZoneId;
        ExitEdge = exitEdge;
        ExitDepth = exitDepth;
    }

    internal StringName ExitZoneId { get; }
    internal BattleMapEdge ExitEdge { get; }
    internal int ExitDepth { get; }
}

internal sealed class BattleRescueObjectiveDefinition : BattleObjectiveDefinition
{
    internal BattleRescueObjectiveDefinition(StringName targetActorId)
        : base(BattleObjectiveMode.Rescue)
    {
        if (targetActorId == "")
            throw new ArgumentException(
                "Rescue objective target actor id must not be empty.",
                nameof(targetActorId)
            );
        TargetActorId = targetActorId;
    }

    internal StringName TargetActorId { get; }
}

internal sealed class BattleEscortObjectiveDefinition : BattleObjectiveDefinition
{
    internal BattleEscortObjectiveDefinition(
        StringName targetActorId,
        StringName exitZoneId,
        BattleMapEdge exitEdge,
        int exitDepth
    )
        : base(BattleObjectiveMode.Escort)
    {
        if (targetActorId == "")
            throw new ArgumentException(
                "Escort objective target actor id must not be empty.",
                nameof(targetActorId)
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
        TargetActorId = targetActorId;
        ExitZoneId = exitZoneId;
        ExitEdge = exitEdge;
        ExitDepth = exitDepth;
    }

    internal StringName TargetActorId { get; }
    internal StringName ExitZoneId { get; }
    internal BattleMapEdge ExitEdge { get; }
    internal int ExitDepth { get; }
}

internal sealed class BattleDefenseObjectiveDefinition : BattleObjectiveDefinition
{
    internal BattleDefenseObjectiveDefinition(
        StringName targetActorId,
        int durationTu
    )
        : base(BattleObjectiveMode.Defense)
    {
        if (targetActorId == "")
            throw new ArgumentException(
                "Defense objective target actor id must not be empty.",
                nameof(targetActorId)
            );
        if (
            durationTu <= 0
            || durationTu % BattleTimelineState.TuGranularity != 0
        )
        {
            throw new ArgumentOutOfRangeException(nameof(durationTu));
        }
        TargetActorId = targetActorId;
        DurationTu = durationTu;
    }

    internal StringName TargetActorId { get; }
    internal int DurationTu { get; }
}

internal sealed class BattleInterceptObjectiveDefinition : BattleObjectiveDefinition
{
    internal BattleInterceptObjectiveDefinition(
        StringName targetActorId,
        StringName exitZoneId,
        BattleMapEdge exitEdge,
        int exitDepth
    )
        : base(BattleObjectiveMode.Intercept)
    {
        if (targetActorId == "")
            throw new ArgumentException(
                "Intercept objective target actor id must not be empty.",
                nameof(targetActorId)
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
        TargetActorId = targetActorId;
        ExitZoneId = exitZoneId;
        ExitEdge = exitEdge;
        ExitDepth = exitDepth;
    }

    internal StringName TargetActorId { get; }
    internal StringName ExitZoneId { get; }
    internal BattleMapEdge ExitEdge { get; }
    internal int ExitDepth { get; }
}

internal sealed class BattleOperationNodeDefinition
{
    internal BattleOperationNodeDefinition(
        StringName nodeId,
        string displayName,
        StringName zoneId,
        BattleMapEdge placementEdge,
        int placementDepth
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
    }

    internal StringName NodeId { get; }
    internal string DisplayName { get; }
    internal StringName ZoneId { get; }
    internal BattleMapEdge PlacementEdge { get; }
    internal int PlacementDepth { get; }
}

internal sealed class BattleNodeOperationObjectiveDefinition
    : BattleObjectiveDefinition
{
    internal BattleNodeOperationObjectiveDefinition(
        IEnumerable<BattleOperationNodeDefinition> operationNodes
    )
        : base(BattleObjectiveMode.NodeOperation)
    {
        List<BattleOperationNodeDefinition> normalizedNodes = (
            operationNodes ?? Array.Empty<BattleOperationNodeDefinition>()
        )
            .Where(node => node != null)
            .OrderBy(node => node.NodeId.ToString(), StringComparer.Ordinal)
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
        )
        {
            throw new ArgumentException(
                "Node operation objective node ids must be unique.",
                nameof(operationNodes)
            );
        }
        OperationNodes = new ReadOnlyCollection<BattleOperationNodeDefinition>(
            normalizedNodes
        );
    }

    internal IReadOnlyList<BattleOperationNodeDefinition> OperationNodes { get; }
}

internal sealed class BattleScenarioActorDefinition
{
    internal BattleScenarioActorDefinition(
        StringName actorId,
        StringName templateId,
        string displayName,
        StringName spawnZoneId,
        BattleMapEdge spawnEdge,
        int spawnDepth
    )
    {
        if (actorId == "")
            throw new ArgumentException(
                "Battle scenario actor id must not be empty.",
                nameof(actorId)
            );
        if (templateId == "")
            throw new ArgumentException(
                "Battle scenario actor template id must not be empty.",
                nameof(templateId)
            );
        if (spawnZoneId == "")
            throw new ArgumentException(
                "Battle scenario actor spawn zone id must not be empty.",
                nameof(spawnZoneId)
            );
        if (!Enum.IsDefined(spawnEdge) || spawnEdge == BattleMapEdge.Unknown)
            throw new ArgumentOutOfRangeException(nameof(spawnEdge));
        if (spawnDepth <= 0)
            throw new ArgumentOutOfRangeException(nameof(spawnDepth));
        ActorId = actorId;
        TemplateId = templateId;
        DisplayName = displayName ?? "";
        SpawnZoneId = spawnZoneId;
        SpawnEdge = spawnEdge;
        SpawnDepth = spawnDepth;
    }

    internal StringName ActorId { get; }
    internal StringName TemplateId { get; }
    internal string DisplayName { get; }
    internal StringName SpawnZoneId { get; }
    internal BattleMapEdge SpawnEdge { get; }
    internal int SpawnDepth { get; }
}

internal sealed class BattleEncounterWorldResolutionDefinition
{
    internal BattleEncounterWorldResolutionDefinition(
        BattleWorldResolutionMode playerSuccessMode,
        BattleWorldResolutionMode playerFailureMode,
        BattleWorldResolutionMode drawMode,
        int suppressionSteps
    )
    {
        if (!Enum.IsDefined(playerSuccessMode))
            throw new ArgumentOutOfRangeException(nameof(playerSuccessMode));
        if (!Enum.IsDefined(playerFailureMode))
            throw new ArgumentOutOfRangeException(nameof(playerFailureMode));
        if (!Enum.IsDefined(drawMode))
            throw new ArgumentOutOfRangeException(nameof(drawMode));
        if (suppressionSteps < 0)
            throw new ArgumentOutOfRangeException(nameof(suppressionSteps));
        bool usesSuppression =
            playerSuccessMode == BattleWorldResolutionMode.Suppress
            || playerFailureMode == BattleWorldResolutionMode.Suppress
            || drawMode == BattleWorldResolutionMode.Suppress;
        if (usesSuppression != (suppressionSteps > 0))
        {
            throw new ArgumentException(
                "Battle encounter suppression steps must be positive exactly when a resolution mode suppresses the encounter."
            );
        }
        PlayerSuccessMode = playerSuccessMode;
        PlayerFailureMode = playerFailureMode;
        DrawMode = drawMode;
        SuppressionSteps = suppressionSteps;
    }

    internal BattleWorldResolutionMode PlayerSuccessMode { get; }
    internal BattleWorldResolutionMode PlayerFailureMode { get; }
    internal BattleWorldResolutionMode DrawMode { get; }
    internal int SuppressionSteps { get; }
}

internal sealed class BattleEncounterDefinition
{
    internal BattleEncounterDefinition(
        StringName encounterId,
        string displayName,
        StringName rosterProfileId,
        BattleObjectiveDefinition objective,
        BattleEncounterWorldResolutionDefinition worldResolution,
        IEnumerable<BattleScenarioActorDefinition> scenarioActors = null
    )
    {
        if (encounterId == "")
            throw new ArgumentException("Battle encounter id must not be empty.", nameof(encounterId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Battle encounter display name must not be empty.", nameof(displayName));
        if (rosterProfileId == "")
            throw new ArgumentException("Battle encounter roster profile id must not be empty.", nameof(rosterProfileId));
        EncounterId = encounterId;
        DisplayName = displayName ?? "";
        RosterProfileId = rosterProfileId;
        Objective = objective ?? throw new ArgumentNullException(nameof(objective));
        WorldResolution =
            worldResolution ?? throw new ArgumentNullException(nameof(worldResolution));
        List<BattleScenarioActorDefinition> normalizedScenarioActors = (
            scenarioActors ?? Array.Empty<BattleScenarioActorDefinition>()
        )
            .Where(actor => actor != null)
            .ToList();
        if (
            normalizedScenarioActors
                .Select(actor => actor.ActorId.ToString())
                .Distinct(StringComparer.Ordinal)
                .Count()
            != normalizedScenarioActors.Count
        )
        {
            throw new ArgumentException(
                "Battle encounter scenario actor ids must be unique.",
                nameof(scenarioActors)
            );
        }
        ScenarioActors = new ReadOnlyCollection<BattleScenarioActorDefinition>(
            normalizedScenarioActors
        );
    }

    internal StringName EncounterId { get; }
    internal string DisplayName { get; }
    internal StringName RosterProfileId { get; }
    internal BattleObjectiveDefinition Objective { get; }
    internal BattleEncounterWorldResolutionDefinition WorldResolution { get; }
    internal IReadOnlyList<BattleScenarioActorDefinition> ScenarioActors { get; }
}
