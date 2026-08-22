#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

internal static class BattleEncounterImportValidator
{
    internal static IReadOnlyList<ContentJsonDiagnostic> Validate(
        JsonContentEntryContext context,
        BattleEncounterImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        RequireIdentity(diagnostics, context, import.EncounterId);
        RequireText(diagnostics, context, import.DisplayName, "/display_name", "display_name");
        RequireText(
            diagnostics,
            context,
            import.RosterProfileId,
            "/roster_profile_id",
            "roster_profile_id"
        );

        var actorIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < import.ScenarioActors.Count; index += 1)
        {
            BattleScenarioActorImportModel actor = import.ScenarioActors[index];
            string pointer = $"/scenario_actors/{index}";
            RequireText(diagnostics, context, actor.ActorId, $"{pointer}/actor_id", "actor_id");
            if (!string.IsNullOrWhiteSpace(actor.ActorId) && !actorIds.Add(actor.ActorId))
            {
                Add(
                    diagnostics,
                    context,
                    BattleEncounterJsonRules.DuplicateId,
                    $"Duplicate scenario actor id '{actor.ActorId}'.",
                    $"{pointer}/actor_id"
                );
            }
            RequireText(diagnostics, context, actor.TemplateId, $"{pointer}/template_id", "template_id");
            RequireText(diagnostics, context, actor.SpawnZoneId, $"{pointer}/spawn_zone_id", "spawn_zone_id");
            ValidateEdge(diagnostics, context, actor.SpawnEdge, $"{pointer}/spawn_edge");
            RequirePositive(diagnostics, context, actor.SpawnDepth, $"{pointer}/spawn_depth", "spawn_depth");
        }

        ValidateObjective(diagnostics, context, import.Objective);
        ValidateResolution(diagnostics, context, import.WorldResolution);
        return diagnostics;
    }

    private static void ValidateObjective(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        BattleObjectiveImportModel objective
    )
    {
        switch (objective)
        {
            case BattleEliminationObjectiveImportModel:
                return;
            case BattleBossObjectiveImportModel value:
                RequireText(diagnostics, context, value.TargetActorId, "/objective/payload/target_actor_id", "target_actor_id");
                return;
            case BattleRescueObjectiveImportModel value:
                RequireText(diagnostics, context, value.TargetActorId, "/objective/payload/target_actor_id", "target_actor_id");
                return;
            case BattleEscapeObjectiveImportModel value:
                ValidateExit(diagnostics, context, value.ExitZoneId, value.ExitEdge, value.ExitDepth);
                return;
            case BattleEscortObjectiveImportModel value:
                RequireText(diagnostics, context, value.TargetActorId, "/objective/payload/target_actor_id", "target_actor_id");
                ValidateExit(diagnostics, context, value.ExitZoneId, value.ExitEdge, value.ExitDepth);
                return;
            case BattleDefenseObjectiveImportModel value:
                RequireText(diagnostics, context, value.TargetActorId, "/objective/payload/target_actor_id", "target_actor_id");
                if (value.DurationTu <= 0 || value.DurationTu % BattleEncounterImportRules.TuGranularity != 0)
                {
                    Add(
                        diagnostics,
                        context,
                        BattleEncounterJsonRules.ValueOutOfRange,
                        $"duration_tu must be a positive multiple of {BattleEncounterImportRules.TuGranularity}.",
                        "/objective/payload/duration_tu"
                    );
                }
                return;
            case BattleInterceptObjectiveImportModel value:
                RequireText(diagnostics, context, value.TargetActorId, "/objective/payload/target_actor_id", "target_actor_id");
                ValidateExit(diagnostics, context, value.ExitZoneId, value.ExitEdge, value.ExitDepth);
                return;
            case BattleNodeOperationObjectiveImportModel value:
                ValidateNodes(diagnostics, context, value.OperationNodes);
                return;
            case BattleControlObjectiveImportModel value:
                ValidateControl(diagnostics, context, value);
                return;
            default:
                Add(
                    diagnostics,
                    context,
                    BattleEncounterJsonRules.ValueUnsupported,
                    "Battle objective import kind is unsupported.",
                    "/objective/kind"
                );
                return;
        }
    }

    private static void ValidateNodes(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        IReadOnlyList<BattleOperationNodeImportModel> nodes
    )
    {
        if (nodes.Count == 0)
        {
            Add(diagnostics, context, BattleEncounterJsonRules.ValueRequired, "operation_nodes must be non-empty.", "/objective/payload/operation_nodes");
            return;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < nodes.Count; index += 1)
        {
            BattleOperationNodeImportModel node = nodes[index];
            string pointer = $"/objective/payload/operation_nodes/{index}";
            ValidateOwnedZone(
                diagnostics,
                context,
                node.NodeId,
                node.DisplayName,
                node.ZoneId,
                node.PlacementEdge,
                node.PlacementDepth,
                pointer
            );
            if (!string.IsNullOrWhiteSpace(node.NodeId) && !ids.Add(node.NodeId))
                Add(diagnostics, context, BattleEncounterJsonRules.DuplicateId, $"Duplicate operation node id '{node.NodeId}'.", $"{pointer}/node_id");
        }
    }

    private static void ValidateControl(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        BattleControlObjectiveImportModel objective
    )
    {
        if (objective.ControlZones.Count == 0)
        {
            Add(diagnostics, context, BattleEncounterJsonRules.ValueRequired, "control_zones must be non-empty.", "/objective/payload/control_zones");
        }
        if (objective.ScoreTarget <= 0 || objective.ScoreTarget % BattleEncounterImportRules.TuGranularity != 0)
        {
            Add(
                diagnostics,
                context,
                BattleEncounterJsonRules.ValueOutOfRange,
                $"score_target must be a positive multiple of {BattleEncounterImportRules.TuGranularity}.",
                "/objective/payload/score_target"
            );
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < objective.ControlZones.Count; index += 1)
        {
            BattleControlZoneImportModel zone = objective.ControlZones[index];
            string pointer = $"/objective/payload/control_zones/{index}";
            ValidateOwnedZone(
                diagnostics,
                context,
                zone.ZoneId,
                zone.DisplayName,
                zone.ZoneId,
                zone.PlacementEdge,
                zone.PlacementDepth,
                pointer
            );
            if (!string.IsNullOrWhiteSpace(zone.ZoneId) && !ids.Add(zone.ZoneId))
                Add(diagnostics, context, BattleEncounterJsonRules.DuplicateId, $"Duplicate control zone id '{zone.ZoneId}'.", $"{pointer}/zone_id");
        }
    }

    private static void ValidateOwnedZone(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        string id,
        string displayName,
        string zoneId,
        BattleEncounterMapEdgeKind edge,
        int depth,
        string pointer
    )
    {
        RequireText(diagnostics, context, id, $"{pointer}/{(pointer.Contains("operation_nodes", StringComparison.Ordinal) ? "node_id" : "zone_id")}", "id");
        RequireText(diagnostics, context, displayName, $"{pointer}/display_name", "display_name");
        RequireText(diagnostics, context, zoneId, $"{pointer}/zone_id", "zone_id");
        ValidateEdge(diagnostics, context, edge, $"{pointer}/placement_edge");
        RequirePositive(diagnostics, context, depth, $"{pointer}/placement_depth", "placement_depth");
    }

    private static void ValidateExit(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        string zoneId,
        BattleEncounterMapEdgeKind edge,
        int depth
    )
    {
        RequireText(diagnostics, context, zoneId, "/objective/payload/exit_zone_id", "exit_zone_id");
        ValidateEdge(diagnostics, context, edge, "/objective/payload/exit_edge");
        RequirePositive(diagnostics, context, depth, "/objective/payload/exit_depth", "exit_depth");
    }

    private static void ValidateResolution(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        BattleEncounterWorldResolutionImportModel resolution
    )
    {
        ValidateResolutionMode(diagnostics, context, resolution.PlayerSuccessMode, "/world_resolution/player_success_mode");
        ValidateResolutionMode(diagnostics, context, resolution.PlayerFailureMode, "/world_resolution/player_failure_mode");
        ValidateResolutionMode(diagnostics, context, resolution.DrawMode, "/world_resolution/draw_mode");
        bool suppresses = resolution.PlayerSuccessMode == BattleEncounterWorldResolutionKind.Suppress
            || resolution.PlayerFailureMode == BattleEncounterWorldResolutionKind.Suppress
            || resolution.DrawMode == BattleEncounterWorldResolutionKind.Suppress;
        if (resolution.SuppressionSteps < 0 || suppresses != (resolution.SuppressionSteps > 0))
        {
            Add(
                diagnostics,
                context,
                BattleEncounterJsonRules.ValueOutOfRange,
                "suppression_steps must be positive exactly when one resolution mode is suppress.",
                "/world_resolution/suppression_steps"
            );
        }
    }

    private static void ValidateResolutionMode(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        BattleEncounterWorldResolutionKind value,
        string pointer
    )
    {
        if (!Enum.IsDefined(value))
            Add(diagnostics, context, BattleEncounterJsonRules.ValueUnsupported, "World resolution mode is unsupported.", pointer);
    }

    private static void ValidateEdge(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        BattleEncounterMapEdgeKind value,
        string pointer
    )
    {
        if (!Enum.IsDefined(value) || value == BattleEncounterMapEdgeKind.Unknown)
            Add(diagnostics, context, BattleEncounterJsonRules.ValueUnsupported, "Battle map edge must be left, right, top, or bottom.", pointer);
    }

    private static void RequireIdentity(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            Add(diagnostics, context, BattleEncounterJsonRules.IdRequired, "encounter_id is required.", "/encounter_id");
        else if (!string.Equals(value, context.EntryId, StringComparison.Ordinal))
            Add(diagnostics, context, BattleEncounterJsonRules.IdMismatch, "encounter_id must match the envelope entry id.", "/encounter_id");
    }

    private static void RequireText(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        string value,
        string pointer,
        string label
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            Add(diagnostics, context, BattleEncounterJsonRules.ValueRequired, $"{label} is required.", pointer);
    }

    private static void RequirePositive(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        int value,
        string pointer,
        string label
    )
    {
        if (value <= 0)
            Add(diagnostics, context, BattleEncounterJsonRules.ValueOutOfRange, $"{label} must be positive.", pointer);
    }

    private static void Add(
        List<ContentJsonDiagnostic> diagnostics,
        JsonContentEntryContext context,
        string ruleId,
        string message,
        string pointer
    ) =>
        diagnostics.Add(
            new ContentJsonDiagnostic(
                ruleId,
                message,
                context.SourceLabel,
                $"{context.JsonPointer}{pointer}"
            )
        );
}
