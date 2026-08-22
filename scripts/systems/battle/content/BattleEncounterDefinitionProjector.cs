#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

internal static class BattleEncounterDefinitionProjector
{
    internal static BattleEncounterDefinition Project(BattleEncounterImportModel import)
    {
        ArgumentNullException.ThrowIfNull(import);
        return new BattleEncounterDefinition(
            new StringName(import.EncounterId),
            import.DisplayName,
            new StringName(import.RosterProfileId),
            ProjectObjective(import.Objective),
            new BattleEncounterWorldResolutionDefinition(
                ProjectResolutionMode(import.WorldResolution.PlayerSuccessMode),
                ProjectResolutionMode(import.WorldResolution.PlayerFailureMode),
                ProjectResolutionMode(import.WorldResolution.DrawMode),
                import.WorldResolution.SuppressionSteps
            ),
            import.ScenarioActors.Select(actor => new BattleScenarioActorDefinition(
                new StringName(actor.ActorId),
                new StringName(actor.TemplateId),
                actor.DisplayName,
                new StringName(actor.SpawnZoneId),
                ProjectEdge(actor.SpawnEdge),
                actor.SpawnDepth
            ))
        );
    }

    private static BattleObjectiveDefinition ProjectObjective(BattleObjectiveImportModel import) =>
        import switch
        {
            BattleEliminationObjectiveImportModel => BattleEliminationObjectiveDefinition.Instance,
            BattleBossObjectiveImportModel value => new BattleBossObjectiveDefinition(new StringName(value.TargetActorId)),
            BattleRescueObjectiveImportModel value => new BattleRescueObjectiveDefinition(new StringName(value.TargetActorId)),
            BattleEscapeObjectiveImportModel value => new BattleEscapeObjectiveDefinition(new StringName(value.ExitZoneId), ProjectEdge(value.ExitEdge), value.ExitDepth),
            BattleEscortObjectiveImportModel value => new BattleEscortObjectiveDefinition(new StringName(value.TargetActorId), new StringName(value.ExitZoneId), ProjectEdge(value.ExitEdge), value.ExitDepth),
            BattleDefenseObjectiveImportModel value => new BattleDefenseObjectiveDefinition(new StringName(value.TargetActorId), value.DurationTu),
            BattleInterceptObjectiveImportModel value => new BattleInterceptObjectiveDefinition(new StringName(value.TargetActorId), new StringName(value.ExitZoneId), ProjectEdge(value.ExitEdge), value.ExitDepth),
            BattleNodeOperationObjectiveImportModel value => new BattleNodeOperationObjectiveDefinition(value.OperationNodes.Select(ProjectNode)),
            BattleControlObjectiveImportModel value => new BattleControlObjectiveDefinition(value.ControlZones.Select(ProjectZone), value.ScoreTarget),
            _ => throw new InvalidOperationException($"Unsupported battle objective import type {import?.GetType().Name ?? "null"}."),
        };

    private static BattleOperationNodeDefinition ProjectNode(BattleOperationNodeImportModel value) =>
        new(
            new StringName(value.NodeId),
            value.DisplayName,
            new StringName(value.ZoneId),
            ProjectEdge(value.PlacementEdge),
            value.PlacementDepth
        );

    private static BattleControlZoneDefinition ProjectZone(BattleControlZoneImportModel value) =>
        new(
            new StringName(value.ZoneId),
            value.DisplayName,
            ProjectEdge(value.PlacementEdge),
            value.PlacementDepth
        );

    private static BattleMapEdge ProjectEdge(BattleEncounterMapEdgeKind value) =>
        value switch
        {
            BattleEncounterMapEdgeKind.Left => BattleMapEdge.Left,
            BattleEncounterMapEdgeKind.Right => BattleMapEdge.Right,
            BattleEncounterMapEdgeKind.Top => BattleMapEdge.Top,
            BattleEncounterMapEdgeKind.Bottom => BattleMapEdge.Bottom,
            _ => BattleMapEdge.Unknown,
        };

    private static BattleWorldResolutionMode ProjectResolutionMode(
        BattleEncounterWorldResolutionKind value
    ) =>
        value switch
        {
            BattleEncounterWorldResolutionKind.Preserve => BattleWorldResolutionMode.Preserve,
            BattleEncounterWorldResolutionKind.Clear => BattleWorldResolutionMode.Clear,
            BattleEncounterWorldResolutionKind.Suppress => BattleWorldResolutionMode.Suppress,
            _ => (BattleWorldResolutionMode)(-1),
        };
}
