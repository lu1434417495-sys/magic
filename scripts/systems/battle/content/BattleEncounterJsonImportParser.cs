#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

internal static class BattleEncounterJsonImportParser
{
    internal static ContentImportStageResult<BattleEncounterJsonDto> Parse(
        JsonContentEntryContext context,
        string json
    ) =>
        ContentJsonStrictDtoParser.Parse(
            context,
            json,
            BattleEncounterJsonSerializerContext.Default.BattleEncounterJsonDto,
            BattleEncounterJsonRules.InvalidDto
        );

    internal static ContentImportStageResult<BattleEncounterImportModel> Normalize(
        JsonContentEntryContext context,
        BattleEncounterJsonDto dto
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        BattleObjectiveImportModel? objective = NormalizeObjective(
            context,
            dto.Objective,
            diagnostics
        );
        var scenarioActors = new List<BattleScenarioActorImportModel>();
        for (int index = 0; index < dto.ScenarioActors.Count; index += 1)
        {
            BattleScenarioActorJsonDto actor = dto.ScenarioActors[index];
            scenarioActors.Add(
                new BattleScenarioActorImportModel(
                    actor.ActorId,
                    actor.TemplateId,
                    actor.DisplayName,
                    actor.SpawnZoneId,
                    ParseEdge(actor.SpawnEdge),
                    actor.SpawnDepth
                )
            );
        }

        BattleEncounterWorldResolutionJsonDto resolution = dto.WorldResolution;
        if (objective is null || diagnostics.Count > 0)
        {
            return ContentImportStageResult<BattleEncounterImportModel>.Failure(diagnostics);
        }
        return ContentImportStageResult<BattleEncounterImportModel>.Success(
            new BattleEncounterImportModel(
                dto.EncounterId,
                dto.DisplayName,
                dto.RosterProfileId,
                objective,
                scenarioActors,
                new BattleEncounterWorldResolutionImportModel(
                    ParseWorldResolutionMode(resolution.PlayerSuccessMode),
                    ParseWorldResolutionMode(resolution.PlayerFailureMode),
                    ParseWorldResolutionMode(resolution.DrawMode),
                    resolution.SuppressionSteps
                )
            )
        );
    }

    private static BattleObjectiveImportModel? NormalizeObjective(
        JsonContentEntryContext context,
        BattleObjectiveJsonDto objective,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (objective.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(
                Diagnostic(
                    context,
                    BattleEncounterJsonRules.InvalidObjectivePayload,
                    "Battle objective payload must be a JSON object.",
                    "/objective/payload"
                )
            );
            return null;
        }

        var payloadContext = new JsonContentEntryContext(
            context.DomainId,
            context.EntryId,
            context.SourceLabel,
            $"{context.JsonPointer}/objective/payload"
        );
        string json = payload.GetRawText();
        return objective.Kind switch
        {
            "elimination" => ParseObjective(
                payloadContext,
                json,
                BattleEncounterJsonSerializerContext.Default.BattleEliminationObjectivePayloadJsonDto,
                diagnostics,
                static _ => new BattleEliminationObjectiveImportModel()
            ),
            "boss" => ParseObjective(
                payloadContext,
                json,
                BattleEncounterJsonSerializerContext.Default.BattleBossObjectivePayloadJsonDto,
                diagnostics,
                static value => new BattleBossObjectiveImportModel(value.TargetActorId)
            ),
            "rescue" => ParseObjective(
                payloadContext,
                json,
                BattleEncounterJsonSerializerContext.Default.BattleRescueObjectivePayloadJsonDto,
                diagnostics,
                static value => new BattleRescueObjectiveImportModel(value.TargetActorId)
            ),
            "escape" => ParseObjective(
                payloadContext,
                json,
                BattleEncounterJsonSerializerContext.Default.BattleEscapeObjectivePayloadJsonDto,
                diagnostics,
                static value => new BattleEscapeObjectiveImportModel(
                    value.ExitZoneId,
                    ParseEdge(value.ExitEdge),
                    value.ExitDepth
                )
            ),
            "escort" => ParseObjective(
                payloadContext,
                json,
                BattleEncounterJsonSerializerContext.Default.BattleEscortObjectivePayloadJsonDto,
                diagnostics,
                static value => new BattleEscortObjectiveImportModel(
                    value.TargetActorId,
                    value.ExitZoneId,
                    ParseEdge(value.ExitEdge),
                    value.ExitDepth
                )
            ),
            "defense" => ParseObjective(
                payloadContext,
                json,
                BattleEncounterJsonSerializerContext.Default.BattleDefenseObjectivePayloadJsonDto,
                diagnostics,
                static value => new BattleDefenseObjectiveImportModel(
                    value.TargetActorId,
                    value.DurationTu
                )
            ),
            "intercept" => ParseObjective(
                payloadContext,
                json,
                BattleEncounterJsonSerializerContext.Default.BattleInterceptObjectivePayloadJsonDto,
                diagnostics,
                static value => new BattleInterceptObjectiveImportModel(
                    value.TargetActorId,
                    value.ExitZoneId,
                    ParseEdge(value.ExitEdge),
                    value.ExitDepth
                )
            ),
            "node_operation" => ParseObjective(
                payloadContext,
                json,
                BattleEncounterJsonSerializerContext.Default.BattleNodeOperationObjectivePayloadJsonDto,
                diagnostics,
                static value => new BattleNodeOperationObjectiveImportModel(
                    value.OperationNodes
                        .Select(node => new BattleOperationNodeImportModel(
                            node.NodeId,
                            node.DisplayName,
                            node.ZoneId,
                            ParseEdge(node.PlacementEdge),
                            node.PlacementDepth
                        ))
                        .ToArray()
                )
            ),
            "control" => ParseObjective(
                payloadContext,
                json,
                BattleEncounterJsonSerializerContext.Default.BattleControlObjectivePayloadJsonDto,
                diagnostics,
                static value => new BattleControlObjectiveImportModel(
                    value.ControlZones
                        .Select(zone => new BattleControlZoneImportModel(
                            zone.ZoneId,
                            zone.DisplayName,
                            ParseEdge(zone.PlacementEdge),
                            zone.PlacementDepth
                        ))
                        .ToArray(),
                    value.ScoreTarget
                )
            ),
            _ => UnknownObjective(context, objective.Kind, diagnostics),
        };
    }

    private static BattleObjectiveImportModel? ParseObjective<TPayload>(
        JsonContentEntryContext context,
        string json,
        JsonTypeInfo<TPayload> typeInfo,
        List<ContentJsonDiagnostic> diagnostics,
        Func<TPayload, BattleObjectiveImportModel> projector
    ) where TPayload : notnull
    {
        ContentImportStageResult<TPayload> parsed = ContentJsonStrictDtoParser.Parse(
            context,
            json,
            typeInfo,
            BattleEncounterJsonRules.InvalidObjectivePayload
        );
        diagnostics.AddRange(parsed.Diagnostics);
        return parsed.HasValue ? projector(parsed.Value) : null;
    }

    private static BattleObjectiveImportModel? UnknownObjective(
        JsonContentEntryContext context,
        string kind,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        diagnostics.Add(
            Diagnostic(
                context,
                BattleEncounterJsonRules.UnknownObjectiveKind,
                $"Battle objective kind '{kind}' is not registered by the closed nine-kind spec.",
                "/objective/kind"
            )
        );
        return null;
    }

    internal static BattleEncounterMapEdgeKind ParseEdge(string value) =>
        value switch
        {
            "left" => BattleEncounterMapEdgeKind.Left,
            "right" => BattleEncounterMapEdgeKind.Right,
            "top" => BattleEncounterMapEdgeKind.Top,
            "bottom" => BattleEncounterMapEdgeKind.Bottom,
            _ => BattleEncounterMapEdgeKind.Unknown,
        };

    internal static BattleEncounterWorldResolutionKind ParseWorldResolutionMode(string value) =>
        value switch
        {
            "preserve" => BattleEncounterWorldResolutionKind.Preserve,
            "clear" => BattleEncounterWorldResolutionKind.Clear,
            "suppress" => BattleEncounterWorldResolutionKind.Suppress,
            _ => BattleEncounterWorldResolutionKind.Unknown,
        };

    private static ContentJsonDiagnostic Diagnostic(
        JsonContentEntryContext context,
        string ruleId,
        string message,
        string relativePointer
    ) =>
        new(
            ruleId,
            message,
            context.SourceLabel,
            $"{context.JsonPointer}{relativePointer}"
        );
}

internal static class BattleEncounterJsonAuthoringDomains
{
    private static readonly ContentJsonNullabilityPolicy NullabilityPolicy =
        new(Array.Empty<string>());

    internal static IReadOnlyList<ContentJsonSchemaDomainRegistration> SchemaRegistrations { get; } =
        Array.AsReadOnly(new[] { BattleEncounterJsonDomain.SchemaRegistration });

    internal static JsonContentDomainDescriptor<BattleEncounterJsonDto, BattleEncounterImportModel>
        CreateDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            BattleEncounterJsonDomain.DomainId,
            BattleEncounterJsonDomain.SchemaVersion,
            "encounter_id",
            sourceDirectory,
            sourceReader,
            NullabilityPolicy,
            BattleEncounterJsonImportParser.Parse,
            BattleEncounterJsonImportParser.Normalize,
            BattleEncounterImportValidator.Validate
        );

    internal static IEnumerable<IContentJsonOfflineValidationDomain> CreateOfflineDomains()
    {
        yield return new OfflineDomain();
    }

    private sealed class OfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => BattleEncounterJsonDomain.DomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<BattleEncounterImportModel> batch =
                CreateDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }
}
