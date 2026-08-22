#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

internal sealed record ProfessionImportModel(string EntryId, ProfessionJsonDto Content);
internal sealed record RaceImportModel(string EntryId, RaceJsonDto Content);
internal sealed record SubraceImportModel(string EntryId, SubraceJsonDto Content);
internal sealed record FaithImportModel(string EntryId, FaithDeityJsonDto Content);
internal sealed record AgeProfileImportModel(string EntryId, AgeProfileJsonDto Content);
internal sealed record BloodlineImportModel(
    string EntryId,
    string Kind,
    BloodlineJsonDto? Bloodline,
    BloodlineStageJsonDto? Stage
);
internal sealed record AscensionImportModel(
    string EntryId,
    string Kind,
    AscensionJsonDto? Ascension,
    AscensionStageJsonDto? Stage
);
internal sealed record StageAdvancementImportModel(string EntryId, StageAdvancementJsonDto Content);

internal static class ProfessionIdentityJsonImport
{
    private const string InvalidDtoRule = "identity.json.dto.invalid";
    private const string UnknownKindRule = "identity.json.kind.unknown";
    private const string IdentityMismatchRule = "identity.json.id.mismatch";
    private static readonly ContentJsonNullabilityPolicy StrictNullability =
        new(Array.Empty<string>());
    private static readonly ContentJsonNullabilityPolicy ProfessionNullability =
        new(new[] { "/unlock_requirement" });

    internal static JsonContentDomainDescriptor<ProfessionJsonDto, ProfessionImportModel>
        CreateProfessionDescriptor(string directory, IContentJsonSourceReader reader) =>
        new(
            ProfessionIdentityJsonDomains.ProfessionDomain,
            ProfessionIdentityJsonDomains.SchemaVersion,
            "profession_id",
            directory,
            reader,
            ProfessionNullability,
            static (context, json) => Parse(
                context,
                json,
                ProfessionIdentityJsonSerializerContext.Default.ProfessionJsonDto
            ),
            static (_, dto) => ContentImportStageResult<ProfessionImportModel>.Success(
                new ProfessionImportModel(dto.ProfessionId, dto)
            ),
            ValidateProfession
        );

    internal static JsonContentDomainDescriptor<RaceJsonDto, RaceImportModel>
        CreateRaceDescriptor(string directory, IContentJsonSourceReader reader) =>
        new(
            ProfessionIdentityJsonDomains.RaceDomain,
            ProfessionIdentityJsonDomains.SchemaVersion,
            "race_id",
            directory,
            reader,
            StrictNullability,
            static (context, json) => Parse(
                context,
                json,
                ProfessionIdentityJsonSerializerContext.Default.RaceJsonDto
            ),
            static (_, dto) => ContentImportStageResult<RaceImportModel>.Success(
                new RaceImportModel(dto.RaceId, dto)
            ),
            ValidateRace
        );

    internal static JsonContentDomainDescriptor<SubraceJsonDto, SubraceImportModel>
        CreateSubraceDescriptor(string directory, IContentJsonSourceReader reader) =>
        new(
            ProfessionIdentityJsonDomains.SubraceDomain,
            ProfessionIdentityJsonDomains.SchemaVersion,
            "subrace_id",
            directory,
            reader,
            StrictNullability,
            static (context, json) => Parse(
                context,
                json,
                ProfessionIdentityJsonSerializerContext.Default.SubraceJsonDto
            ),
            static (_, dto) => ContentImportStageResult<SubraceImportModel>.Success(
                new SubraceImportModel(dto.SubraceId, dto)
            ),
            ValidateSubrace
        );

    internal static JsonContentDomainDescriptor<FaithDeityJsonDto, FaithImportModel>
        CreateFaithDescriptor(string directory, IContentJsonSourceReader reader) =>
        new(
            ProfessionIdentityJsonDomains.FaithDomain,
            ProfessionIdentityJsonDomains.SchemaVersion,
            "deity_id",
            directory,
            reader,
            StrictNullability,
            static (context, json) => Parse(
                context,
                json,
                ProfessionIdentityJsonSerializerContext.Default.FaithDeityJsonDto
            ),
            static (_, dto) => ContentImportStageResult<FaithImportModel>.Success(
                new FaithImportModel(dto.DeityId, dto)
            ),
            ValidateFaith
        );

    internal static JsonContentDomainDescriptor<AgeProfileJsonDto, AgeProfileImportModel>
        CreateAgeProfileDescriptor(string directory, IContentJsonSourceReader reader) =>
        new(
            ProfessionIdentityJsonDomains.AgeProfileDomain,
            ProfessionIdentityJsonDomains.SchemaVersion,
            "profile_id",
            directory,
            reader,
            StrictNullability,
            static (context, json) => Parse(
                context,
                json,
                ProfessionIdentityJsonSerializerContext.Default.AgeProfileJsonDto
            ),
            static (_, dto) => ContentImportStageResult<AgeProfileImportModel>.Success(
                new AgeProfileImportModel(dto.ProfileId, dto)
            ),
            ValidateAgeProfile
        );

    internal static JsonContentDomainDescriptor<BloodlineEntryJsonDto, BloodlineImportModel>
        CreateBloodlineDescriptor(string directory, IContentJsonSourceReader reader) =>
        new(
            ProfessionIdentityJsonDomains.BloodlineDomain,
            ProfessionIdentityJsonDomains.SchemaVersion,
            "entry_id",
            directory,
            reader,
            StrictNullability,
            static (context, json) => Parse(
                context,
                json,
                ProfessionIdentityJsonSerializerContext.Default.BloodlineEntryJsonDto
            ),
            NormalizeBloodline,
            ValidateBloodline
        );

    internal static JsonContentDomainDescriptor<AscensionEntryJsonDto, AscensionImportModel>
        CreateAscensionDescriptor(string directory, IContentJsonSourceReader reader) =>
        new(
            ProfessionIdentityJsonDomains.AscensionDomain,
            ProfessionIdentityJsonDomains.SchemaVersion,
            "entry_id",
            directory,
            reader,
            StrictNullability,
            static (context, json) => Parse(
                context,
                json,
                ProfessionIdentityJsonSerializerContext.Default.AscensionEntryJsonDto
            ),
            NormalizeAscension,
            ValidateAscension
        );

    internal static JsonContentDomainDescriptor<StageAdvancementJsonDto, StageAdvancementImportModel>
        CreateStageAdvancementDescriptor(string directory, IContentJsonSourceReader reader) =>
        new(
            ProfessionIdentityJsonDomains.StageAdvancementDomain,
            ProfessionIdentityJsonDomains.SchemaVersion,
            "modifier_id",
            directory,
            reader,
            StrictNullability,
            static (context, json) => Parse(
                context,
                json,
                ProfessionIdentityJsonSerializerContext.Default.StageAdvancementJsonDto
            ),
            static (_, dto) => ContentImportStageResult<StageAdvancementImportModel>.Success(
                new StageAdvancementImportModel(dto.ModifierId, dto)
            ),
            ValidateStageAdvancement
        );

    internal static IEnumerable<IContentJsonOfflineValidationDomain> CreateOfflineDomains()
    {
        yield return Offline(
            ProfessionIdentityJsonDomains.ProfessionDomain,
            static (directory, reader) => Report(
                ProfessionIdentityJsonDomains.ProfessionDomain,
                CreateProfessionDescriptor(directory, reader).Import()
            )
        );
        yield return Offline(
            ProfessionIdentityJsonDomains.RaceDomain,
            static (directory, reader) => Report(
                ProfessionIdentityJsonDomains.RaceDomain,
                CreateRaceDescriptor(directory, reader).Import()
            )
        );
        yield return Offline(
            ProfessionIdentityJsonDomains.SubraceDomain,
            static (directory, reader) => Report(
                ProfessionIdentityJsonDomains.SubraceDomain,
                CreateSubraceDescriptor(directory, reader).Import()
            )
        );
        yield return Offline(
            ProfessionIdentityJsonDomains.FaithDomain,
            static (directory, reader) => Report(
                ProfessionIdentityJsonDomains.FaithDomain,
                CreateFaithDescriptor(directory, reader).Import()
            )
        );
        yield return Offline(
            ProfessionIdentityJsonDomains.AgeProfileDomain,
            static (directory, reader) => Report(
                ProfessionIdentityJsonDomains.AgeProfileDomain,
                CreateAgeProfileDescriptor(directory, reader).Import()
            )
        );
        yield return Offline(
            ProfessionIdentityJsonDomains.BloodlineDomain,
            static (directory, reader) => Report(
                ProfessionIdentityJsonDomains.BloodlineDomain,
                CreateBloodlineDescriptor(directory, reader).Import()
            )
        );
        yield return Offline(
            ProfessionIdentityJsonDomains.AscensionDomain,
            static (directory, reader) => Report(
                ProfessionIdentityJsonDomains.AscensionDomain,
                CreateAscensionDescriptor(directory, reader).Import()
            )
        );
        yield return Offline(
            ProfessionIdentityJsonDomains.StageAdvancementDomain,
            static (directory, reader) => Report(
                ProfessionIdentityJsonDomains.StageAdvancementDomain,
                CreateStageAdvancementDescriptor(directory, reader).Import()
            )
        );
    }

    internal static string FormatDiagnostic(ContentJsonDiagnostic diagnostic) =>
        $"[{diagnostic.RuleId}] {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}";

    private static ContentImportStageResult<T> Parse<T>(
        JsonContentEntryContext context,
        string json,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo
    ) where T : notnull =>
        ContentJsonStrictDtoParser.Parse(context, json, typeInfo, InvalidDtoRule);

    private static ContentImportStageResult<BloodlineImportModel> NormalizeBloodline(
        JsonContentEntryContext context,
        BloodlineEntryJsonDto dto
    )
    {
        if (dto.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
            return InvalidPayload<BloodlineImportModel>(context);
        JsonContentEntryContext payloadContext = PayloadContext(context);
        if (dto.Kind == "bloodline")
        {
            ContentImportStageResult<BloodlineJsonDto> result = Parse(
                payloadContext,
                payload.GetRawText(),
                ProfessionIdentityJsonSerializerContext.Default.BloodlineJsonDto
            );
            return result.HasValue
                ? ContentImportStageResult<BloodlineImportModel>.Success(
                    new BloodlineImportModel(dto.EntryId, dto.Kind, result.Value, null)
                )
                : ContentImportStageResult<BloodlineImportModel>.Failure(result.Diagnostics);
        }
        if (dto.Kind == "stage")
        {
            ContentImportStageResult<BloodlineStageJsonDto> result = Parse(
                payloadContext,
                payload.GetRawText(),
                ProfessionIdentityJsonSerializerContext.Default.BloodlineStageJsonDto
            );
            return result.HasValue
                ? ContentImportStageResult<BloodlineImportModel>.Success(
                    new BloodlineImportModel(dto.EntryId, dto.Kind, null, result.Value)
                )
                : ContentImportStageResult<BloodlineImportModel>.Failure(result.Diagnostics);
        }
        return UnknownKind<BloodlineImportModel>(context, dto.Kind);
    }

    private static ContentImportStageResult<AscensionImportModel> NormalizeAscension(
        JsonContentEntryContext context,
        AscensionEntryJsonDto dto
    )
    {
        if (dto.Payload is not JsonElement payload || payload.ValueKind != JsonValueKind.Object)
            return InvalidPayload<AscensionImportModel>(context);
        JsonContentEntryContext payloadContext = PayloadContext(context);
        if (dto.Kind == "ascension")
        {
            ContentImportStageResult<AscensionJsonDto> result = Parse(
                payloadContext,
                payload.GetRawText(),
                ProfessionIdentityJsonSerializerContext.Default.AscensionJsonDto
            );
            return result.HasValue
                ? ContentImportStageResult<AscensionImportModel>.Success(
                    new AscensionImportModel(dto.EntryId, dto.Kind, result.Value, null)
                )
                : ContentImportStageResult<AscensionImportModel>.Failure(result.Diagnostics);
        }
        if (dto.Kind == "stage")
        {
            ContentImportStageResult<AscensionStageJsonDto> result = Parse(
                payloadContext,
                payload.GetRawText(),
                ProfessionIdentityJsonSerializerContext.Default.AscensionStageJsonDto
            );
            return result.HasValue
                ? ContentImportStageResult<AscensionImportModel>.Success(
                    new AscensionImportModel(dto.EntryId, dto.Kind, null, result.Value)
                )
                : ContentImportStageResult<AscensionImportModel>.Failure(result.Diagnostics);
        }
        return UnknownKind<AscensionImportModel>(context, dto.Kind);
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateIdentity(
        JsonContentEntryContext context,
        string identity
    ) =>
        string.Equals(context.EntryId, identity, StringComparison.Ordinal)
            ? Array.Empty<ContentJsonDiagnostic>()
            : new[]
            {
                new ContentJsonDiagnostic(
                    IdentityMismatchRule,
                    "Entry identity must match the document envelope identity.",
                    context.SourceLabel,
                    context.JsonPointer
                ),
            };

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateBloodline(
        JsonContentEntryContext context,
        BloodlineImportModel import
    )
    {
        var diagnostics = IdentityDiagnostics(
            context,
            import.Kind == "bloodline"
                ? import.Bloodline?.BloodlineId ?? ""
                : import.Stage?.StageId ?? ""
        );
        if (import.Kind == "bloodline" && import.Bloodline is { } root)
        {
            Required(diagnostics, context, root.DisplayName, "/payload/display_name");
            Required(diagnostics, context, root.Description, "/payload/description");
            DuplicateStrings(diagnostics, context, root.StageIds, "/payload/stage_ids");
        }
        else if (import.Stage is { } stage)
        {
            Required(diagnostics, context, stage.BloodlineId, "/payload/bloodline_id");
            Required(diagnostics, context, stage.DisplayName, "/payload/display_name");
            Required(diagnostics, context, stage.Description, "/payload/description");
        }
        return diagnostics;
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateAscension(
        JsonContentEntryContext context,
        AscensionImportModel import
    )
    {
        var diagnostics = IdentityDiagnostics(
            context,
            import.Kind == "ascension"
                ? import.Ascension?.AscensionId ?? ""
                : import.Stage?.StageId ?? ""
        );
        if (import.Kind == "ascension" && import.Ascension is { } root)
        {
            Required(diagnostics, context, root.DisplayName, "/payload/display_name");
            Required(diagnostics, context, root.Description, "/payload/description");
            DuplicateStrings(diagnostics, context, root.StageIds, "/payload/stage_ids");
        }
        else if (import.Stage is { } stage)
        {
            Required(diagnostics, context, stage.AscensionId, "/payload/ascension_id");
            Required(diagnostics, context, stage.DisplayName, "/payload/display_name");
            Required(diagnostics, context, stage.Description, "/payload/description");
        }
        return diagnostics;
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateProfession(
        JsonContentEntryContext context,
        ProfessionImportModel import
    )
    {
        var diagnostics = IdentityDiagnostics(context, import.EntryId);
        ProfessionJsonDto value = import.Content;
        Required(diagnostics, context, value.DisplayName, "/display_name");
        Required(diagnostics, context, value.Description, "/description");
        Positive(diagnostics, context, value.MaxRank, "/max_rank");
        Positive(diagnostics, context, value.HitDieSides, "/hit_die_sides");
        Supported(diagnostics, context, value.BabProgression, new[] { "full", "three_quarter", "half" }, "/bab_progression");
        Supported(diagnostics, context, value.ReactivationMode, new[] { "auto", "manual" }, "/reactivation_mode");
        Supported(diagnostics, context, value.DependencyVisibilityMode, new[] { "count_when_hidden", "ignore_when_hidden" }, "/dependency_visibility_mode");
        DuplicateInts(diagnostics, context, value.RankRequirements.Select(item => item.TargetRank), "/rank_requirements");
        return diagnostics;
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateRace(
        JsonContentEntryContext context,
        RaceImportModel import
    )
    {
        var diagnostics = IdentityDiagnostics(context, import.EntryId);
        RaceJsonDto value = import.Content;
        Required(diagnostics, context, value.DisplayName, "/display_name");
        Required(diagnostics, context, value.Description, "/description");
        Required(diagnostics, context, value.AgeProfileId, "/age_profile_id");
        Required(diagnostics, context, value.DefaultSubraceId, "/default_subrace_id");
        Required(diagnostics, context, value.BodySizeCategory, "/body_size_category");
        Positive(diagnostics, context, value.BaseSpeed, "/base_speed");
        DuplicateStrings(diagnostics, context, value.SubraceIds, "/subrace_ids");
        return diagnostics;
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateSubrace(
        JsonContentEntryContext context,
        SubraceImportModel import
    )
    {
        var diagnostics = IdentityDiagnostics(context, import.EntryId);
        SubraceJsonDto value = import.Content;
        Required(diagnostics, context, value.ParentRaceId, "/parent_race_id");
        Required(diagnostics, context, value.DisplayName, "/display_name");
        Required(diagnostics, context, value.Description, "/description");
        return diagnostics;
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateFaith(
        JsonContentEntryContext context,
        FaithImportModel import
    )
    {
        var diagnostics = IdentityDiagnostics(context, import.EntryId);
        FaithDeityJsonDto value = import.Content;
        Required(diagnostics, context, value.DisplayName, "/display_name");
        Required(diagnostics, context, value.RankProgressStatId, "/rank_progress_stat_id");
        if (value.RankDefs.Count == 0)
            Invalid(diagnostics, context, "Faith deity must declare at least one rank.", "/rank_defs");
        DuplicateInts(diagnostics, context, value.RankDefs.Select(rank => rank.RankIndex), "/rank_defs");
        for (int index = 0; index < value.RankDefs.Count; index++)
        {
            FaithRankJsonDto rank = value.RankDefs[index];
            Positive(diagnostics, context, rank.RankIndex, $"/rank_defs/{index}/rank_index");
            Required(diagnostics, context, rank.RankName, $"/rank_defs/{index}/rank_name");
            NonNegative(diagnostics, context, rank.RequiredGold, $"/rank_defs/{index}/required_gold");
            NonNegative(diagnostics, context, rank.RequiredLevel, $"/rank_defs/{index}/required_level");
            if (rank.RewardEntries.Count == 0)
                Invalid(diagnostics, context, "Faith rank must declare rewards.", $"/rank_defs/{index}/reward_entries");
        }
        return diagnostics;
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateAgeProfile(
        JsonContentEntryContext context,
        AgeProfileImportModel import
    )
    {
        var diagnostics = IdentityDiagnostics(context, import.EntryId);
        AgeProfileJsonDto value = import.Content;
        Required(diagnostics, context, value.RaceId, "/race_id");
        int[] ages = { value.ChildAge, value.TeenAge, value.YoungAdultAge, value.AdultAge, value.MiddleAge, value.OldAge, value.VenerableAge, value.MaxNaturalAge };
        for (int index = 0; index < ages.Length; index++)
        {
            NonNegative(diagnostics, context, ages[index], "/age_thresholds");
            if (index > 0 && ages[index] < ages[index - 1])
                Invalid(diagnostics, context, "Age thresholds must be monotonic.", "/age_thresholds");
        }
        DuplicateStrings(diagnostics, context, value.StageRules.Select(stage => stage.StageId), "/stage_rules");
        DuplicateStrings(diagnostics, context, value.CreationStageIds, "/creation_stage_ids");
        return diagnostics;
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateStageAdvancement(
        JsonContentEntryContext context,
        StageAdvancementImportModel import
    )
    {
        var diagnostics = IdentityDiagnostics(context, import.EntryId);
        StageAdvancementJsonDto value = import.Content;
        Required(diagnostics, context, value.DisplayName, "/display_name");
        Supported(diagnostics, context, value.TargetAxis, new[] { "full", "physical", "mental", "bloodline", "divine", "martial", "domain" }, "/target_axis");
        if (value.TargetAxis is "bloodline" or "divine")
            Required(diagnostics, context, value.MaxStageId, "/max_stage_id");
        else
            Positive(diagnostics, context, value.StageOffset, "/stage_offset");
        return diagnostics;
    }

    private static List<ContentJsonDiagnostic> IdentityDiagnostics(
        JsonContentEntryContext context,
        string identity
    ) => new(ValidateIdentity(context, identity));

    private static void Required(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, string value, string pointer)
    {
        if (string.IsNullOrWhiteSpace(value))
            Invalid(target, context, "Value must be a non-empty string.", pointer);
    }

    private static void Positive(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, int value, string pointer)
    {
        if (value <= 0)
            Invalid(target, context, "Value must be greater than zero.", pointer);
    }

    private static void NonNegative(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, int value, string pointer)
    {
        if (value < 0)
            Invalid(target, context, "Value must be non-negative.", pointer);
    }

    private static void Supported(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, string value, IEnumerable<string> supported, string pointer)
    {
        if (!supported.Contains(value, StringComparer.Ordinal))
            Invalid(target, context, $"Unsupported value '{value}'.", pointer);
    }

    private static void DuplicateStrings(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, IEnumerable<string> values, string pointer)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string value in values)
            if (!string.IsNullOrWhiteSpace(value) && !seen.Add(value))
                Invalid(target, context, $"Duplicate value '{value}'.", pointer);
    }

    private static void DuplicateInts(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, IEnumerable<int> values, string pointer)
    {
        var seen = new HashSet<int>();
        foreach (int value in values)
            if (!seen.Add(value))
                Invalid(target, context, $"Duplicate value '{value}'.", pointer);
    }

    private static void Invalid(List<ContentJsonDiagnostic> target, JsonContentEntryContext context, string message, string pointer) =>
        target.Add(new ContentJsonDiagnostic("identity.json.value.invalid", message, context.SourceLabel, context.JsonPointer + pointer));

    private static JsonContentEntryContext PayloadContext(JsonContentEntryContext context) =>
        new(context.DomainId, context.EntryId, context.SourceLabel, context.JsonPointer + "/payload");

    private static ContentImportStageResult<T> InvalidPayload<T>(JsonContentEntryContext context)
        where T : notnull =>
        ContentImportStageResult<T>.Failure(
            new ContentJsonDiagnostic(
                InvalidDtoRule,
                "Polymorphic content payload must be a JSON object.",
                context.SourceLabel,
                context.JsonPointer + "/payload"
            )
        );

    private static ContentImportStageResult<T> UnknownKind<T>(
        JsonContentEntryContext context,
        string kind
    ) where T : notnull =>
        ContentImportStageResult<T>.Failure(
            new ContentJsonDiagnostic(
                UnknownKindRule,
                $"Unsupported identity content kind '{kind}'.",
                context.SourceLabel,
                context.JsonPointer + "/kind"
            )
        );

    private static IContentJsonOfflineValidationDomain Offline(
        string domainId,
        Func<string, IContentJsonSourceReader, ContentJsonOfflineValidationReport> validate
    ) => new OfflineDomain(domainId, validate);

    private static ContentJsonOfflineValidationReport Report<T>(
        string domainId,
        ContentImportBatch<T> batch
    ) where T : notnull =>
        new(domainId, batch.Entries.Count, batch.Diagnostics);

    private sealed class OfflineDomain : IContentJsonOfflineValidationDomain
    {
        private readonly Func<
            string,
            IContentJsonSourceReader,
            ContentJsonOfflineValidationReport
        > _validate;

        internal OfflineDomain(
            string domainId,
            Func<string, IContentJsonSourceReader, ContentJsonOfflineValidationReport> validate
        )
        {
            DomainId = domainId;
            _validate = validate;
        }

        public string DomainId { get; }

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        ) => _validate(sourceDirectory, sourceReader);
    }
}
