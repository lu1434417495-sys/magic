#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;

internal static class BattleEncounterJsonDomain
{
    internal const int SchemaVersion = 1;
    internal const string DomainId = "battle_encounters";
    internal const string Directory = "res://data/configs/json/battle_encounters";

    internal static ContentJsonSchemaDomainRegistration SchemaRegistration { get; } =
        new(
            DomainId,
            SchemaVersion,
            typeof(BattleEncounterJsonDocumentDto),
            "Magic battle encounter JSON authoring schema",
            "Strict battle encounter contract with nine closed objective kinds.",
            "res://data/schemas/content/battle_encounters.schema.json",
            "/data/configs/json/battle_encounters/**/*.json"
        );
}

internal static class BattleEncounterJsonRules
{
    internal const string InvalidDto = "battle_encounter.dto.invalid";
    internal const string InvalidObjectivePayload = "battle_encounter.objective.invalid_payload";
    internal const string UnknownObjectiveKind = "battle_encounter.objective.unknown_kind";
    internal const string IdRequired = "battle_encounter.id.required";
    internal const string IdMismatch = "battle_encounter.id.mismatch";
    internal const string ValueRequired = "battle_encounter.value.required";
    internal const string ValueOutOfRange = "battle_encounter.value.out_of_range";
    internal const string ValueUnsupported = "battle_encounter.value.unsupported";
    internal const string DuplicateId = "battle_encounter.id.duplicate";
}

internal enum BattleObjectiveKind
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

internal enum BattleEncounterMapEdgeKind
{
    Unknown = 0,
    Left,
    Right,
    Top,
    Bottom,
}

internal enum BattleEncounterWorldResolutionKind
{
    Unknown = 0,
    Preserve,
    Clear,
    Suppress,
}

[Description("Battle encounter document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleEncounterJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired]
    [ContentJsonSchemaConst(BattleEncounterJsonDomain.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain"), JsonRequired]
    [ContentJsonSchemaConst(BattleEncounterJsonDomain.DomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family"), JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates"), JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, BattleEncounterJsonDto> Templates { get; init; } =
        new ReadOnlyDictionary<string, BattleEncounterJsonDto>(
            new Dictionary<string, BattleEncounterJsonDto>()
        );

    [JsonPropertyName("entries"), JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "encounter_id",
        "template"
    )]
    public IReadOnlyList<BattleEncounterJsonDto> Entries { get; init; } =
        Array.Empty<BattleEncounterJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleEncounterJsonDto
{
    [JsonPropertyName("encounter_id"), JsonRequired]
    public string EncounterId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("roster_profile_id"), JsonRequired]
    public string RosterProfileId { get; init; } = "";

    [JsonPropertyName("objective"), JsonRequired]
    public BattleObjectiveJsonDto Objective { get; init; } = new();

    [JsonPropertyName("scenario_actors"), JsonRequired]
    public IReadOnlyList<BattleScenarioActorJsonDto> ScenarioActors { get; init; } =
        Array.Empty<BattleScenarioActorJsonDto>();

    [JsonPropertyName("world_resolution"), JsonRequired]
    public BattleEncounterWorldResolutionJsonDto WorldResolution { get; init; } = new();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(BattleObjectiveClosedKindSchemaSpec))]
internal sealed class BattleObjectiveJsonDto
{
    [JsonPropertyName("kind"), JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload"), JsonRequired]
    public object Payload { get; init; } = null!;
}

internal sealed class BattleObjectiveClosedKindSchemaSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch("elimination", typeof(BattleEliminationObjectivePayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("boss", typeof(BattleBossObjectivePayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("rescue", typeof(BattleRescueObjectivePayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("escape", typeof(BattleEscapeObjectivePayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("escort", typeof(BattleEscortObjectivePayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("defense", typeof(BattleDefenseObjectivePayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("intercept", typeof(BattleInterceptObjectivePayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("node_operation", typeof(BattleNodeOperationObjectivePayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("control", typeof(BattleControlObjectivePayloadJsonDto)),
            }
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleEliminationObjectivePayloadJsonDto { }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleBossObjectivePayloadJsonDto
{
    [JsonPropertyName("target_actor_id"), JsonRequired]
    public string TargetActorId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleRescueObjectivePayloadJsonDto
{
    [JsonPropertyName("target_actor_id"), JsonRequired]
    public string TargetActorId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleEscapeObjectivePayloadJsonDto
{
    [JsonPropertyName("exit_zone_id"), JsonRequired]
    public string ExitZoneId { get; init; } = "";

    [JsonPropertyName("exit_edge"), JsonRequired]
    public string ExitEdge { get; init; } = "";

    [JsonPropertyName("exit_depth"), JsonRequired]
    public int ExitDepth { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleEscortObjectivePayloadJsonDto
{
    [JsonPropertyName("target_actor_id"), JsonRequired]
    public string TargetActorId { get; init; } = "";

    [JsonPropertyName("exit_zone_id"), JsonRequired]
    public string ExitZoneId { get; init; } = "";

    [JsonPropertyName("exit_edge"), JsonRequired]
    public string ExitEdge { get; init; } = "";

    [JsonPropertyName("exit_depth"), JsonRequired]
    public int ExitDepth { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleDefenseObjectivePayloadJsonDto
{
    [JsonPropertyName("target_actor_id"), JsonRequired]
    public string TargetActorId { get; init; } = "";

    [JsonPropertyName("duration_tu"), JsonRequired]
    public int DurationTu { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleInterceptObjectivePayloadJsonDto
{
    [JsonPropertyName("target_actor_id"), JsonRequired]
    public string TargetActorId { get; init; } = "";

    [JsonPropertyName("exit_zone_id"), JsonRequired]
    public string ExitZoneId { get; init; } = "";

    [JsonPropertyName("exit_edge"), JsonRequired]
    public string ExitEdge { get; init; } = "";

    [JsonPropertyName("exit_depth"), JsonRequired]
    public int ExitDepth { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleNodeOperationObjectivePayloadJsonDto
{
    [JsonPropertyName("operation_nodes"), JsonRequired]
    public IReadOnlyList<BattleOperationNodeJsonDto> OperationNodes { get; init; } =
        Array.Empty<BattleOperationNodeJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleControlObjectivePayloadJsonDto
{
    [JsonPropertyName("control_zones"), JsonRequired]
    public IReadOnlyList<BattleControlZoneJsonDto> ControlZones { get; init; } =
        Array.Empty<BattleControlZoneJsonDto>();

    [JsonPropertyName("score_target"), JsonRequired]
    public int ScoreTarget { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleScenarioActorJsonDto
{
    [JsonPropertyName("actor_id"), JsonRequired]
    public string ActorId { get; init; } = "";

    [JsonPropertyName("template_id"), JsonRequired]
    public string TemplateId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("spawn_zone_id"), JsonRequired]
    public string SpawnZoneId { get; init; } = "";

    [JsonPropertyName("spawn_edge"), JsonRequired]
    public string SpawnEdge { get; init; } = "";

    [JsonPropertyName("spawn_depth"), JsonRequired]
    public int SpawnDepth { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleOperationNodeJsonDto
{
    [JsonPropertyName("node_id"), JsonRequired]
    public string NodeId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("zone_id"), JsonRequired]
    public string ZoneId { get; init; } = "";

    [JsonPropertyName("placement_edge"), JsonRequired]
    public string PlacementEdge { get; init; } = "";

    [JsonPropertyName("placement_depth"), JsonRequired]
    public int PlacementDepth { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleControlZoneJsonDto
{
    [JsonPropertyName("zone_id"), JsonRequired]
    public string ZoneId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("placement_edge"), JsonRequired]
    public string PlacementEdge { get; init; } = "";

    [JsonPropertyName("placement_depth"), JsonRequired]
    public int PlacementDepth { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class BattleEncounterWorldResolutionJsonDto
{
    [JsonPropertyName("player_success_mode"), JsonRequired]
    public string PlayerSuccessMode { get; init; } = "";

    [JsonPropertyName("player_failure_mode"), JsonRequired]
    public string PlayerFailureMode { get; init; } = "";

    [JsonPropertyName("draw_mode"), JsonRequired]
    public string DrawMode { get; init; } = "";

    [JsonPropertyName("suppression_steps"), JsonRequired]
    public int SuppressionSteps { get; init; }
}

internal sealed record BattleEncounterImportModel(
    string EncounterId,
    string DisplayName,
    string RosterProfileId,
    BattleObjectiveImportModel Objective,
    IReadOnlyList<BattleScenarioActorImportModel> ScenarioActors,
    BattleEncounterWorldResolutionImportModel WorldResolution
);

internal abstract record BattleObjectiveImportModel(BattleObjectiveKind Kind);
internal sealed record BattleEliminationObjectiveImportModel()
    : BattleObjectiveImportModel(BattleObjectiveKind.Elimination);
internal sealed record BattleBossObjectiveImportModel(string TargetActorId)
    : BattleObjectiveImportModel(BattleObjectiveKind.Boss);
internal sealed record BattleRescueObjectiveImportModel(string TargetActorId)
    : BattleObjectiveImportModel(BattleObjectiveKind.Rescue);
internal sealed record BattleEscapeObjectiveImportModel(
    string ExitZoneId,
    BattleEncounterMapEdgeKind ExitEdge,
    int ExitDepth
) : BattleObjectiveImportModel(BattleObjectiveKind.Escape);
internal sealed record BattleEscortObjectiveImportModel(
    string TargetActorId,
    string ExitZoneId,
    BattleEncounterMapEdgeKind ExitEdge,
    int ExitDepth
) : BattleObjectiveImportModel(BattleObjectiveKind.Escort);
internal sealed record BattleDefenseObjectiveImportModel(string TargetActorId, int DurationTu)
    : BattleObjectiveImportModel(BattleObjectiveKind.Defense);
internal sealed record BattleInterceptObjectiveImportModel(
    string TargetActorId,
    string ExitZoneId,
    BattleEncounterMapEdgeKind ExitEdge,
    int ExitDepth
) : BattleObjectiveImportModel(BattleObjectiveKind.Intercept);
internal sealed record BattleNodeOperationObjectiveImportModel(
    IReadOnlyList<BattleOperationNodeImportModel> OperationNodes
) : BattleObjectiveImportModel(BattleObjectiveKind.NodeOperation);
internal sealed record BattleControlObjectiveImportModel(
    IReadOnlyList<BattleControlZoneImportModel> ControlZones,
    int ScoreTarget
) : BattleObjectiveImportModel(BattleObjectiveKind.Control);

internal sealed record BattleScenarioActorImportModel(
    string ActorId,
    string TemplateId,
    string DisplayName,
    string SpawnZoneId,
    BattleEncounterMapEdgeKind SpawnEdge,
    int SpawnDepth
);

internal sealed record BattleOperationNodeImportModel(
    string NodeId,
    string DisplayName,
    string ZoneId,
    BattleEncounterMapEdgeKind PlacementEdge,
    int PlacementDepth
);

internal sealed record BattleControlZoneImportModel(
    string ZoneId,
    string DisplayName,
    BattleEncounterMapEdgeKind PlacementEdge,
    int PlacementDepth
);

internal sealed record BattleEncounterWorldResolutionImportModel(
    BattleEncounterWorldResolutionKind PlayerSuccessMode,
    BattleEncounterWorldResolutionKind PlayerFailureMode,
    BattleEncounterWorldResolutionKind DrawMode,
    int SuppressionSteps
);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
)]
[JsonSerializable(typeof(BattleEncounterJsonDto))]
[JsonSerializable(typeof(BattleEliminationObjectivePayloadJsonDto))]
[JsonSerializable(typeof(BattleBossObjectivePayloadJsonDto))]
[JsonSerializable(typeof(BattleRescueObjectivePayloadJsonDto))]
[JsonSerializable(typeof(BattleEscapeObjectivePayloadJsonDto))]
[JsonSerializable(typeof(BattleEscortObjectivePayloadJsonDto))]
[JsonSerializable(typeof(BattleDefenseObjectivePayloadJsonDto))]
[JsonSerializable(typeof(BattleInterceptObjectivePayloadJsonDto))]
[JsonSerializable(typeof(BattleNodeOperationObjectivePayloadJsonDto))]
[JsonSerializable(typeof(BattleControlObjectivePayloadJsonDto))]
internal partial class BattleEncounterJsonSerializerContext : JsonSerializerContext { }
