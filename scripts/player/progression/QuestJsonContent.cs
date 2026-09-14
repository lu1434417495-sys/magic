#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

internal static class QuestJsonContentDomain
{
    internal const int SchemaVersion = 1;
    internal const string DomainId = "quests";
    internal const string DirectoryPath = "res://data/configs/json/quests";

    internal static ContentJsonSchemaDomainRegistration SchemaRegistration { get; } =
        new(
            DomainId,
            SchemaVersion,
            typeof(QuestJsonDocumentDto),
            "Magic quest JSON authoring schema",
            "Strict closed-kind quest contract projected through a plain import model.",
            "res://data/schemas/content/quests.schema.json",
            "/data/configs/json/quests/**/*.json"
        );

    internal static IReadOnlyList<ContentJsonSchemaDomainRegistration> SchemaRegistrations { get; } =
        Array.AsReadOnly(new[] { SchemaRegistration });

    internal static JsonContentDomainDescriptor<QuestJsonDto, QuestImportModel> CreateDescriptor(
        string sourceDirectory,
        IContentJsonSourceReader sourceReader
    ) =>
        new(
            DomainId,
            SchemaVersion,
            "quest_id",
            sourceDirectory,
            sourceReader,
            new ContentJsonNullabilityPolicy(Array.Empty<string>()),
            QuestJsonImportParser.Parse,
            QuestJsonImportParser.Normalize,
            QuestImportValidator.Validate
        );

    internal static IEnumerable<IContentJsonOfflineValidationDomain> CreateOfflineDomains()
    {
        yield return new OfflineDomain();
    }

    private sealed class OfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => QuestJsonContentDomain.DomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<QuestImportModel> batch = CreateDescriptor(
                sourceDirectory,
                sourceReader
            ).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }
}

internal static class QuestJsonRules
{
    internal const string InvalidDto = "quest.dto.invalid";
    internal const string InvalidPayload = "quest.payload.invalid";
    internal const string UnknownKind = "quest.kind.unknown";
    internal const string IdRequired = "quest.id.required";
    internal const string IdMismatch = "quest.id.mismatch";
    internal const string CollectionRequired = "quest.collection.required";
    internal const string ValueOutOfRange = "quest.value.out_of_range";
}

[Description("Quest document envelope.")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class QuestJsonDocumentDto
{
    [JsonPropertyName("schema"), JsonRequired]
    [ContentJsonSchemaConst(QuestJsonContentDomain.SchemaVersion)]
    public int Schema { get; init; }

    [JsonPropertyName("domain"), JsonRequired]
    [ContentJsonSchemaConst(QuestJsonContentDomain.DomainId)]
    public string Domain { get; init; } = "";

    [JsonPropertyName("family"), JsonRequired]
    public string Family { get; init; } = "";

    [JsonPropertyName("templates"), JsonRequired]
    [ContentJsonSchemaPartialObjectValues(typeof(ContentJsonTemplateReferenceSchemaDto))]
    public IReadOnlyDictionary<string, QuestJsonDto> Templates { get; init; } =
        EmptyMap<QuestJsonDto>.Value;

    [JsonPropertyName("entries"), JsonRequired]
    [ContentJsonSchemaEntryControlMembers(
        typeof(ContentJsonTemplateReferenceSchemaDto),
        "quest_id",
        "template"
    )]
    public IReadOnlyList<QuestJsonDto> Entries { get; init; } = Array.Empty<QuestJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class QuestJsonDto
{
    [JsonPropertyName("quest_id"), JsonRequired]
    public string QuestId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("description"), JsonRequired]
    public string Description { get; init; } = "";

    [JsonPropertyName("provider_kind"), JsonRequired]
    public string ProviderKind { get; init; } = "";

    [JsonPropertyName("provider_interaction_id"), JsonRequired]
    public string ProviderInteractionId { get; init; } = "";

    [JsonPropertyName("listing_channels"), JsonRequired]
    public IReadOnlyList<string> ListingChannels { get; init; } = Array.Empty<string>();

    [JsonPropertyName("listing_settlement_ids"), JsonRequired]
    public IReadOnlyList<string> ListingSettlementIds { get; init; } = Array.Empty<string>();

    [JsonPropertyName("tags"), JsonRequired]
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    [JsonPropertyName("accept_requirements"), JsonRequired]
    public IReadOnlyList<QuestAcceptRequirementJsonDto> AcceptRequirements { get; init; } =
        Array.Empty<QuestAcceptRequirementJsonDto>();

    [JsonPropertyName("objectives"), JsonRequired]
    public IReadOnlyList<QuestObjectiveJsonDto> Objectives { get; init; } =
        Array.Empty<QuestObjectiveJsonDto>();

    [JsonPropertyName("rewards"), JsonRequired]
    public IReadOnlyList<QuestRewardJsonDto> Rewards { get; init; } =
        Array.Empty<QuestRewardJsonDto>();

    [JsonPropertyName("is_repeatable"), JsonRequired]
    public bool IsRepeatable { get; init; }

    [JsonPropertyName("failure_policy"), JsonRequired]
    public string FailurePolicy { get; init; } = "";

    [JsonPropertyName("danger_tier_override"), JsonRequired]
    public int DangerTierOverride { get; init; }

    [JsonPropertyName("accept_dialogue_text"), JsonRequired]
    public string AcceptDialogueText { get; init; } = "";

    [JsonPropertyName("accept_feedback_success"), JsonRequired]
    public string AcceptFeedbackSuccess { get; init; } = "";

    [JsonPropertyName("accept_feedback_failure"), JsonRequired]
    public string AcceptFeedbackFailure { get; init; } = "";

    [JsonPropertyName("accept_confirmation_text"), JsonRequired]
    public string AcceptConfirmationText { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(QuestAcceptRequirementClosedKindSpec))]
internal sealed class QuestAcceptRequirementJsonDto
{
    [JsonPropertyName("kind"), JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload"), JsonRequired]
    public object Payload { get; init; } = null!;
}

internal sealed class QuestAcceptRequirementClosedKindSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch(
                    "quest_completed",
                    typeof(QuestRequirementPayloadJsonDto)
                ),
                new ContentJsonSchemaClosedKindBranch(
                    "quest_active",
                    typeof(QuestRequirementPayloadJsonDto)
                ),
                new ContentJsonSchemaClosedKindBranch(
                    "quest_not_completed",
                    typeof(QuestRequirementPayloadJsonDto)
                ),
            }
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class QuestRequirementPayloadJsonDto
{
    [JsonPropertyName("quest_id"), JsonRequired]
    public string QuestId { get; init; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(QuestObjectiveClosedKindSpec))]
internal sealed class QuestObjectiveJsonDto
{
    [JsonPropertyName("objective_id"), JsonRequired]
    public string ObjectiveId { get; init; } = "";

    [JsonPropertyName("kind"), JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload"), JsonRequired]
    public object Payload { get; init; } = null!;
}

internal sealed class QuestObjectiveClosedKindSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch(
                    "submit_item",
                    typeof(QuestTargetObjectivePayloadJsonDto)
                ),
                new ContentJsonSchemaClosedKindBranch(
                    "defeat_enemy",
                    typeof(QuestEnemyObjectivePayloadJsonDto)
                ),
                new ContentJsonSchemaClosedKindBranch(
                    "defeat_enemy_in_single_battle",
                    typeof(QuestEnemyObjectivePayloadJsonDto)
                ),
                new ContentJsonSchemaClosedKindBranch(
                    "settlement_action",
                    typeof(QuestTargetObjectivePayloadJsonDto)
                ),
            }
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class QuestTargetObjectivePayloadJsonDto
{
    [JsonPropertyName("target_id"), JsonRequired]
    public string TargetId { get; init; } = "";

    [JsonPropertyName("target_value"), JsonRequired]
    public int TargetValue { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class QuestEnemyObjectivePayloadJsonDto
{
    [JsonPropertyName("target_id"), JsonRequired]
    public string TargetId { get; init; } = "";

    [JsonPropertyName("target_value"), JsonRequired]
    public int TargetValue { get; init; }

    [JsonPropertyName("encounter")]
    public QuestEncounterBindingJsonDto? Encounter { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class QuestEncounterBindingJsonDto
{
    [JsonPropertyName("profile_id"), JsonRequired]
    public string ProfileId { get; init; } = "";

    [JsonPropertyName("display_name"), JsonRequired]
    public string DisplayName { get; init; } = "";

    [JsonPropertyName("growth_stage"), JsonRequired]
    public int GrowthStage { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(QuestRewardClosedKindSpec))]
internal sealed class QuestRewardJsonDto
{
    [JsonPropertyName("kind"), JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload"), JsonRequired]
    public object Payload { get; init; } = null!;
}

internal sealed class QuestRewardClosedKindSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch("gold", typeof(QuestGoldRewardPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch("item", typeof(QuestItemRewardPayloadJsonDto)),
                new ContentJsonSchemaClosedKindBranch(
                    "pending_character_reward",
                    typeof(QuestPendingCharacterRewardPayloadJsonDto)
                ),
            }
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class QuestGoldRewardPayloadJsonDto
{
    [JsonPropertyName("amount"), JsonRequired]
    public int Amount { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class QuestItemRewardPayloadJsonDto
{
    [JsonPropertyName("item_id"), JsonRequired]
    public string ItemId { get; init; } = "";

    [JsonPropertyName("quantity"), JsonRequired]
    public int Quantity { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class QuestPendingCharacterRewardPayloadJsonDto
{
    [JsonPropertyName("member_id"), JsonRequired]
    public string MemberId { get; init; } = "";

    [JsonPropertyName("entries"), JsonRequired]
    public IReadOnlyList<QuestPendingRewardJsonDto> Entries { get; init; } =
        Array.Empty<QuestPendingRewardJsonDto>();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
[ContentJsonSchemaClosedKind(typeof(QuestPendingRewardClosedKindSpec))]
internal sealed class QuestPendingRewardJsonDto
{
    [JsonPropertyName("kind"), JsonRequired]
    public string Kind { get; init; } = "";

    [JsonPropertyName("payload"), JsonRequired]
    public object Payload { get; init; } = null!;
}

internal sealed class QuestPendingRewardClosedKindSpec : IContentJsonSchemaClosedKindSpec
{
    public string DiscriminatorPropertyName => "kind";
    public string PayloadPropertyName => "payload";
    public IReadOnlyList<ContentJsonSchemaClosedKindBranch> Branches { get; } =
        Array.AsReadOnly(
            new[]
            {
                new ContentJsonSchemaClosedKindBranch(
                    "skill_unlock",
                    typeof(QuestPendingRewardValuePayloadJsonDto)
                ),
                new ContentJsonSchemaClosedKindBranch(
                    "knowledge_unlock",
                    typeof(QuestPendingRewardValuePayloadJsonDto)
                ),
                new ContentJsonSchemaClosedKindBranch(
                    "skill_mastery",
                    typeof(QuestPendingRewardValuePayloadJsonDto)
                ),
                new ContentJsonSchemaClosedKindBranch(
                    "attribute_delta",
                    typeof(QuestPendingRewardValuePayloadJsonDto)
                ),
                new ContentJsonSchemaClosedKindBranch(
                    "attribute_progress",
                    typeof(QuestPendingRewardValuePayloadJsonDto)
                ),
            }
        );
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class QuestPendingRewardValuePayloadJsonDto
{
    [JsonPropertyName("target_id"), JsonRequired]
    public string TargetId { get; init; } = "";

    [JsonPropertyName("amount"), JsonRequired]
    public int Amount { get; init; }
}

internal sealed record QuestImportModel(
    string QuestId,
    string DisplayName,
    string Description,
    string ProviderInteractionId,
    IReadOnlyList<string> Tags,
    IReadOnlyList<QuestAcceptRequirementImportModel> AcceptRequirements,
    IReadOnlyList<QuestObjectiveImportModel> Objectives,
    IReadOnlyList<QuestRewardImportModel> Rewards,
    bool IsRepeatable,
    string FailurePolicy,
    string ProviderKind,
    IReadOnlyList<string> ListingChannels,
    IReadOnlyList<string> ListingSettlementIds,
    string AcceptDialogueText,
    string AcceptFeedbackSuccess,
    string AcceptFeedbackFailure,
    string AcceptConfirmationText,
    int DangerTierOverride
);

internal sealed record QuestAcceptRequirementImportModel(string RequirementType, string QuestId);

internal sealed record QuestObjectiveImportModel(
    string ObjectiveId,
    string ObjectiveType,
    string TargetId,
    int? TargetValue,
    string EncounterProfileId,
    string EncounterDisplayName,
    int? EncounterGrowthStage,
    bool HasInvalidEncounterGrowthStage = false
)
{
    internal bool HasStrictTargetValue => TargetValue.HasValue;
    internal bool HasEncounterGrowthStage =>
        EncounterGrowthStage.HasValue || HasInvalidEncounterGrowthStage;
    internal bool HasStrictEncounterGrowthStage =>
        EncounterGrowthStage.HasValue && !HasInvalidEncounterGrowthStage;
}

internal sealed record QuestRewardImportModel(
    string RewardType,
    int? GoldAmount,
    string ItemId,
    int? ItemQuantity,
    string PendingRewardMemberId,
    IReadOnlyList<QuestPendingRewardImportModel> PendingRewardEntries
)
{
    internal bool HasStrictGoldAmount => GoldAmount.HasValue;
    internal bool HasStrictItemQuantity => ItemQuantity.HasValue;
}

internal sealed record QuestPendingRewardImportModel(
    bool IsDictionaryEntry,
    string EntryType,
    string TargetId,
    int? Amount
)
{
    internal bool HasStrictAmount => Amount.HasValue;
}

internal static class QuestJsonImportParser
{
    internal static ContentImportStageResult<QuestJsonDto> Parse(
        JsonContentEntryContext context,
        string json
    ) =>
        ContentJsonStrictDtoParser.Parse(
            context,
            json,
            QuestJsonSerializerContext.Default.QuestJsonDto,
            QuestJsonRules.InvalidDto
        );

    internal static ContentImportStageResult<QuestImportModel> Normalize(
        JsonContentEntryContext context,
        QuestJsonDto dto
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        IReadOnlyList<QuestAcceptRequirementImportModel> requirements =
            NormalizeRequirements(context, dto.AcceptRequirements, diagnostics);
        IReadOnlyList<QuestObjectiveImportModel> objectives = NormalizeObjectives(
            context,
            dto.Objectives,
            diagnostics
        );
        IReadOnlyList<QuestRewardImportModel> rewards = NormalizeRewards(
            context,
            dto.Rewards,
            diagnostics
        );
        if (diagnostics.Count > 0)
            return ContentImportStageResult<QuestImportModel>.Failure(diagnostics);
        return ContentImportStageResult<QuestImportModel>.Success(
            new QuestImportModel(
                dto.QuestId,
                dto.DisplayName,
                dto.Description,
                dto.ProviderInteractionId,
                dto.Tags,
                requirements,
                objectives,
                rewards,
                dto.IsRepeatable,
                dto.FailurePolicy,
                dto.ProviderKind,
                dto.ListingChannels,
                dto.ListingSettlementIds,
                dto.AcceptDialogueText,
                dto.AcceptFeedbackSuccess,
                dto.AcceptFeedbackFailure,
                dto.AcceptConfirmationText,
                dto.DangerTierOverride
            )
        );
    }

    private static IReadOnlyList<QuestAcceptRequirementImportModel> NormalizeRequirements(
        JsonContentEntryContext context,
        IReadOnlyList<QuestAcceptRequirementJsonDto> source,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var result = new List<QuestAcceptRequirementImportModel>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            QuestAcceptRequirementJsonDto entry = source[index];
            string pointer = $"/accept_requirements/{index}";
            if (!TryGetPayload(context, entry.Payload, pointer, diagnostics, out string json))
                continue;
            if (
                entry.Kind is not "quest_completed" and not "quest_active" and not "quest_not_completed"
            )
            {
                AddUnknown(context, diagnostics, pointer + "/kind");
                continue;
            }
            if (
                TryParse(
                    Nested(context, pointer + "/payload"),
                    json,
                    QuestJsonSerializerContext.Default.QuestRequirementPayloadJsonDto,
                    out QuestRequirementPayloadJsonDto? payload,
                    out IReadOnlyList<ContentJsonDiagnostic> payloadDiagnostics
                )
            )
            {
                result.Add(new QuestAcceptRequirementImportModel(entry.Kind, payload!.QuestId));
            }
            else
            {
                diagnostics.AddRange(payloadDiagnostics);
            }
        }
        return new ReadOnlyCollection<QuestAcceptRequirementImportModel>(result);
    }

    private static IReadOnlyList<QuestObjectiveImportModel> NormalizeObjectives(
        JsonContentEntryContext context,
        IReadOnlyList<QuestObjectiveJsonDto> source,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var result = new List<QuestObjectiveImportModel>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            QuestObjectiveJsonDto entry = source[index];
            string pointer = $"/objectives/{index}";
            if (!TryGetPayload(context, entry.Payload, pointer, diagnostics, out string json))
                continue;
            JsonContentEntryContext payloadContext = Nested(context, pointer + "/payload");
            if (entry.Kind is "submit_item" or "settlement_action")
            {
                if (
                    TryParse(
                        payloadContext,
                        json,
                        QuestJsonSerializerContext.Default.QuestTargetObjectivePayloadJsonDto,
                        out QuestTargetObjectivePayloadJsonDto? payload,
                        out IReadOnlyList<ContentJsonDiagnostic> payloadDiagnostics
                    )
                )
                {
                    result.Add(
                        new QuestObjectiveImportModel(
                            entry.ObjectiveId,
                            entry.Kind,
                            payload!.TargetId,
                            payload.TargetValue,
                            "",
                            "",
                            null
                        )
                    );
                }
                else
                {
                    diagnostics.AddRange(payloadDiagnostics);
                }
                continue;
            }
            if (entry.Kind is "defeat_enemy" or "defeat_enemy_in_single_battle")
            {
                if (
                    TryParse(
                        payloadContext,
                        json,
                        QuestJsonSerializerContext.Default.QuestEnemyObjectivePayloadJsonDto,
                        out QuestEnemyObjectivePayloadJsonDto? payload,
                        out IReadOnlyList<ContentJsonDiagnostic> payloadDiagnostics
                    )
                )
                {
                    QuestEncounterBindingJsonDto? encounter = payload!.Encounter;
                    result.Add(
                        new QuestObjectiveImportModel(
                            entry.ObjectiveId,
                            entry.Kind,
                            payload.TargetId,
                            payload.TargetValue,
                            encounter?.ProfileId ?? "",
                            encounter?.DisplayName ?? "",
                            encounter?.GrowthStage
                        )
                    );
                }
                else
                {
                    diagnostics.AddRange(payloadDiagnostics);
                }
                continue;
            }
            AddUnknown(context, diagnostics, pointer + "/kind");
        }
        return new ReadOnlyCollection<QuestObjectiveImportModel>(result);
    }

    private static IReadOnlyList<QuestRewardImportModel> NormalizeRewards(
        JsonContentEntryContext context,
        IReadOnlyList<QuestRewardJsonDto> source,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var result = new List<QuestRewardImportModel>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            QuestRewardJsonDto entry = source[index];
            string pointer = $"/rewards/{index}";
            if (!TryGetPayload(context, entry.Payload, pointer, diagnostics, out string json))
                continue;
            JsonContentEntryContext payloadContext = Nested(context, pointer + "/payload");
            switch (entry.Kind)
            {
                case "gold":
                    if (
                        TryParse(
                            payloadContext,
                            json,
                            QuestJsonSerializerContext.Default.QuestGoldRewardPayloadJsonDto,
                            out QuestGoldRewardPayloadJsonDto? gold,
                            out IReadOnlyList<ContentJsonDiagnostic> goldDiagnostics
                        )
                    )
                        result.Add(new QuestRewardImportModel(entry.Kind, gold!.Amount, "", null, "", Array.Empty<QuestPendingRewardImportModel>()));
                    else
                        diagnostics.AddRange(goldDiagnostics);
                    break;
                case "item":
                    if (
                        TryParse(
                            payloadContext,
                            json,
                            QuestJsonSerializerContext.Default.QuestItemRewardPayloadJsonDto,
                            out QuestItemRewardPayloadJsonDto? item,
                            out IReadOnlyList<ContentJsonDiagnostic> itemDiagnostics
                        )
                    )
                        result.Add(new QuestRewardImportModel(entry.Kind, null, item!.ItemId, item.Quantity, "", Array.Empty<QuestPendingRewardImportModel>()));
                    else
                        diagnostics.AddRange(itemDiagnostics);
                    break;
                case "pending_character_reward":
                    if (
                        TryParse(
                            payloadContext,
                            json,
                            QuestJsonSerializerContext.Default.QuestPendingCharacterRewardPayloadJsonDto,
                            out QuestPendingCharacterRewardPayloadJsonDto? pending,
                            out IReadOnlyList<ContentJsonDiagnostic> pendingDiagnostics
                        )
                    )
                    {
                        IReadOnlyList<QuestPendingRewardImportModel> entries =
                            NormalizePendingRewards(context, pointer, pending!.Entries, diagnostics);
                        result.Add(new QuestRewardImportModel(entry.Kind, null, "", null, pending.MemberId, entries));
                    }
                    else
                        diagnostics.AddRange(pendingDiagnostics);
                    break;
                default:
                    AddUnknown(context, diagnostics, pointer + "/kind");
                    break;
            }
        }
        return new ReadOnlyCollection<QuestRewardImportModel>(result);
    }

    private static IReadOnlyList<QuestPendingRewardImportModel> NormalizePendingRewards(
        JsonContentEntryContext context,
        string rewardPointer,
        IReadOnlyList<QuestPendingRewardJsonDto> source,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var result = new List<QuestPendingRewardImportModel>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            QuestPendingRewardJsonDto entry = source[index];
            string pointer = $"{rewardPointer}/payload/entries/{index}";
            if (!TryGetPayload(context, entry.Payload, pointer, diagnostics, out string json))
                continue;
            if (
                entry.Kind
                    is not "knowledge_unlock"
                        and not "skill_unlock"
                        and not "skill_mastery"
                        and not "attribute_delta"
                        and not "attribute_progress"
            )
            {
                AddUnknown(context, diagnostics, pointer + "/kind");
                continue;
            }
            if (
                TryParse(
                    Nested(context, pointer + "/payload"),
                    json,
                    QuestJsonSerializerContext.Default.QuestPendingRewardValuePayloadJsonDto,
                    out QuestPendingRewardValuePayloadJsonDto? payload,
                    out IReadOnlyList<ContentJsonDiagnostic> payloadDiagnostics
                )
            )
            {
                result.Add(new QuestPendingRewardImportModel(true, entry.Kind, payload!.TargetId, payload.Amount));
            }
            else
            {
                diagnostics.AddRange(payloadDiagnostics);
            }
        }
        return new ReadOnlyCollection<QuestPendingRewardImportModel>(result);
    }

    private static bool TryGetPayload(
        JsonContentEntryContext context,
        object rawPayload,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics,
        out string json
    )
    {
        if (rawPayload is JsonElement payload && payload.ValueKind == JsonValueKind.Object)
        {
            json = payload.GetRawText();
            return true;
        }
        diagnostics.Add(
            new ContentJsonDiagnostic(
                QuestJsonRules.InvalidPayload,
                "Closed-kind payload must be a JSON object.",
                context.SourceLabel,
                $"{context.JsonPointer}{pointer}/payload"
            )
        );
        json = "";
        return false;
    }

    private static bool TryParse<TPayload>(
        JsonContentEntryContext context,
        string json,
        JsonTypeInfo<TPayload> typeInfo,
        out TPayload? result,
        out IReadOnlyList<ContentJsonDiagnostic> diagnostics
    )
        where TPayload : class
    {
        ContentImportStageResult<TPayload> parsed = ContentJsonStrictDtoParser.Parse(
            context,
            json,
            typeInfo,
            QuestJsonRules.InvalidPayload
        );
        result = parsed.HasValue ? parsed.Value : null;
        diagnostics = parsed.Diagnostics;
        return parsed.HasValue;
    }

    private static JsonContentEntryContext Nested(
        JsonContentEntryContext context,
        string pointer
    ) =>
        new(
            context.DomainId,
            context.EntryId,
            context.SourceLabel,
            $"{context.JsonPointer}{pointer}"
        );

    private static void AddUnknown(
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics,
        string pointer
    ) =>
        diagnostics.Add(
            new ContentJsonDiagnostic(
                QuestJsonRules.UnknownKind,
                "Quest closed-kind discriminator is not registered.",
                context.SourceLabel,
                $"{context.JsonPointer}{pointer}"
            )
        );
}

internal static class QuestImportValidator
{
    internal static IReadOnlyList<ContentJsonDiagnostic> Validate(
        JsonContentEntryContext context,
        QuestImportModel import
    )
    {
        var diagnostics = new List<ContentJsonDiagnostic>();
        if (string.IsNullOrWhiteSpace(import.QuestId))
            Add(diagnostics, context, QuestJsonRules.IdRequired, "quest_id is required.", "/quest_id");
        else if (!string.Equals(import.QuestId, context.EntryId, StringComparison.Ordinal))
            Add(diagnostics, context, QuestJsonRules.IdMismatch, "quest_id must match the envelope entry ID.", "/quest_id");
        if (import.Objectives.Count == 0)
            Add(diagnostics, context, QuestJsonRules.CollectionRequired, "objectives must not be empty.", "/objectives");
        if (import.ListingChannels.Count == 0)
            Add(diagnostics, context, QuestJsonRules.CollectionRequired, "listing_channels must not be empty.", "/listing_channels");
        if (import.DangerTierOverride is < 0 or > 5)
            Add(diagnostics, context, QuestJsonRules.ValueOutOfRange, "danger_tier_override must be within 0..5.", "/danger_tier_override");
        return diagnostics;
    }

    private static void Add(
        List<ContentJsonDiagnostic> target,
        JsonContentEntryContext context,
        string ruleId,
        string message,
        string pointer
    ) =>
        target.Add(
            new ContentJsonDiagnostic(
                ruleId,
                message,
                context.SourceLabel,
                $"{context.JsonPointer}{pointer}"
            )
        );
}

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified
)]
[JsonSerializable(typeof(QuestJsonDto))]
[JsonSerializable(typeof(QuestRequirementPayloadJsonDto))]
[JsonSerializable(typeof(QuestTargetObjectivePayloadJsonDto))]
[JsonSerializable(typeof(QuestEnemyObjectivePayloadJsonDto))]
[JsonSerializable(typeof(QuestGoldRewardPayloadJsonDto))]
[JsonSerializable(typeof(QuestItemRewardPayloadJsonDto))]
[JsonSerializable(typeof(QuestPendingCharacterRewardPayloadJsonDto))]
[JsonSerializable(typeof(QuestPendingRewardValuePayloadJsonDto))]
internal partial class QuestJsonSerializerContext : JsonSerializerContext { }
