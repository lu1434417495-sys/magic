#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

internal static class EnemyContentJsonImportParser
{
    internal static ContentImportStageResult<EnemyAiBrainJsonDto> ParseBrain(
        JsonContentEntryContext context,
        string json
    ) => ContentJsonStrictDtoParser.Parse(
        context,
        json,
        EnemyContentJsonSerializerContext.Default.EnemyAiBrainJsonDto,
        EnemyContentJsonRules.InvalidBrainDto
    );

    internal static ContentImportStageResult<EnemyTemplateJsonDto> ParseTemplate(
        JsonContentEntryContext context,
        string json
    ) => ContentJsonStrictDtoParser.Parse(
        context,
        json,
        EnemyContentJsonSerializerContext.Default.EnemyTemplateJsonDto,
        EnemyContentJsonRules.InvalidTemplateDto
    );

    internal static ContentImportStageResult<EncounterRosterJsonDto> ParseRoster(
        JsonContentEntryContext context,
        string json
    ) => ContentJsonStrictDtoParser.Parse(
        context,
        json,
        EnemyContentJsonSerializerContext.Default.EncounterRosterJsonDto,
        EnemyContentJsonRules.InvalidRosterDto
    );

    internal static ContentImportStageResult<EnemyAiBrainImportModel> NormalizeBrain(
        JsonContentEntryContext context,
        EnemyAiBrainJsonDto dto
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        var states = new List<EnemyAiStateImportModel>();
        for (int stateIndex = 0; stateIndex < dto.States.Count; stateIndex += 1)
        {
            EnemyAiStateJsonDto state = dto.States[stateIndex];
            var actions = new List<EnemyAiActionImportModel>();
            for (int actionIndex = 0; actionIndex < state.Actions.Count; actionIndex += 1)
            {
                EnemyAiActionJsonDto action = state.Actions[actionIndex];
                string pointer = $"/states/{stateIndex}/actions/{actionIndex}";
                if (action.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(Diagnostic(
                        EnemyContentJsonRules.InvalidActionPayload,
                        "Enemy AI action payload must be a JSON object.",
                        context,
                        $"{pointer}/payload"
                    ));
                    continue;
                }

                if (!TryParseActionPayload(context, pointer, action.Kind, payload, out EnemyAiActionPayloadJsonDto? parsed, out IReadOnlyList<ContentJsonDiagnostic> payloadDiagnostics))
                {
                    diagnostics.AddRange(payloadDiagnostics);
                    continue;
                }
                actions.Add(new EnemyAiActionImportModel(action.Kind, parsed!));
            }
            states.Add(new EnemyAiStateImportModel(
                state.StateId,
                actions,
                state.GenerationSlots
            ));
        }

        if (diagnostics.Count > 0)
            return ContentImportStageResult<EnemyAiBrainImportModel>.Failure(diagnostics);
        return ContentImportStageResult<EnemyAiBrainImportModel>.Success(
            new EnemyAiBrainImportModel(
                dto.BrainId,
                dto.DefaultStateId,
                dto.ScoreProfile,
                states,
                dto.TransitionRules
            )
        );
    }

    private static bool TryParseActionPayload(
        JsonContentEntryContext context,
        string actionPointer,
        string kind,
        JsonElement payload,
        out EnemyAiActionPayloadJsonDto? result,
        out IReadOnlyList<ContentJsonDiagnostic> diagnostics
    )
    {
        var payloadContext = new JsonContentEntryContext(
            context.DomainId,
            context.EntryId,
            context.SourceLabel,
            $"{context.JsonPointer}{actionPointer}/payload"
        );
        string json = payload.GetRawText();
        switch (kind)
        {
            case "move_to_advantage_position":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.MoveToAdvantagePositionActionPayloadJsonDto, out result, out diagnostics);
            case "move_to_multi_unit_skill_position":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.MoveToMultiUnitSkillPositionActionPayloadJsonDto, out result, out diagnostics);
            case "move_to_range":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.MoveToRangeActionPayloadJsonDto, out result, out diagnostics);
            case "retreat":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.RetreatActionPayloadJsonDto, out result, out diagnostics);
            case "use_charge":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.UseChargeActionPayloadJsonDto, out result, out diagnostics);
            case "use_charge_path_aoe":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.UseChargePathAoeActionPayloadJsonDto, out result, out diagnostics);
            case "use_ground_reposition_skill":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.UseGroundRepositionSkillActionPayloadJsonDto, out result, out diagnostics);
            case "use_ground_skill":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.UseGroundSkillActionPayloadJsonDto, out result, out diagnostics);
            case "use_multi_unit_skill":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.UseMultiUnitSkillActionPayloadJsonDto, out result, out diagnostics);
            case "use_random_chain_skill":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.UseRandomChainSkillActionPayloadJsonDto, out result, out diagnostics);
            case "use_unit_skill":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.UseUnitSkillActionPayloadJsonDto, out result, out diagnostics);
            case "wait":
                return Parse(payloadContext, json, EnemyContentJsonSerializerContext.Default.WaitActionPayloadJsonDto, out result, out diagnostics);
            default:
                result = null;
                diagnostics = new[]
                {
                    Diagnostic(
                        EnemyContentJsonRules.UnknownActionKind,
                        "Enemy AI action kind is not registered by the closed action spec.",
                        context,
                        $"{actionPointer}/kind"
                    ),
                };
                return false;
        }
    }

    private static bool Parse<TPayload>(
        JsonContentEntryContext context,
        string json,
        JsonTypeInfo<TPayload> typeInfo,
        out EnemyAiActionPayloadJsonDto? result,
        out IReadOnlyList<ContentJsonDiagnostic> diagnostics
    ) where TPayload : EnemyAiActionPayloadJsonDto
    {
        ContentImportStageResult<TPayload> parsed = ContentJsonStrictDtoParser.Parse(
            context,
            json,
            typeInfo,
            EnemyContentJsonRules.InvalidActionPayload
        );
        result = parsed.HasValue ? parsed.Value : null;
        diagnostics = parsed.Diagnostics;
        return parsed.HasValue;
    }

    private static ContentJsonDiagnostic Diagnostic(
        string ruleId,
        string message,
        JsonContentEntryContext context,
        string relativePointer
    ) => new(
        ruleId,
        message,
        context.SourceLabel,
        $"{context.JsonPointer}{relativePointer}"
    );
}
