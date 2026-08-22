#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

internal static class BattleSpecialProfileJsonImportParser
{
    internal static ContentImportStageResult<BattleSpecialProfileManifestJsonDto> ParseManifest(
        JsonContentEntryContext context,
        string json
    ) =>
        ContentJsonStrictDtoParser.Parse(
            context,
            json,
            BattleSpecialProfileJsonSerializerContext.Default.BattleSpecialProfileManifestJsonDto,
            BattleSpecialProfileJsonRules.InvalidManifestDto
        );

    internal static ContentImportStageResult<BattleSpecialProfileJsonDto> ParseProfile(
        JsonContentEntryContext context,
        string json
    ) =>
        ContentJsonStrictDtoParser.Parse(
            context,
            json,
            BattleSpecialProfileJsonSerializerContext.Default.BattleSpecialProfileJsonDto,
            BattleSpecialProfileJsonRules.InvalidProfileDto
        );

    internal static ContentImportStageResult<BattleSpecialProfileManifestImportModel>
        NormalizeManifest(
            JsonContentEntryContext _,
            BattleSpecialProfileManifestJsonDto dto
        ) =>
        ContentImportStageResult<BattleSpecialProfileManifestImportModel>.Success(
            new BattleSpecialProfileManifestImportModel(
                dto.ProfileId,
                dto.SchemaVersion,
                dto.OwningSkillIds.ToArray(),
                dto.RuntimeResolverId,
                dto.RuntimeReadPolicy,
                dto.PresentationMetadata.DisplayName,
                dto.PresentationMetadata.CoverageShapeId,
                dto.PresentationMetadata.Radius,
                dto.DeferredCapabilities
                    .Select(value => new BattleSpecialProfileDeferredCapabilityImportModel(
                        value.CapabilityId,
                        value.Status
                    ))
                    .ToArray(),
                dto.SunsetWarningDate,
                dto.SunsetHardBlockDate
            )
        );

    internal static ContentImportStageResult<BattleSpecialProfileImportModel> NormalizeProfile(
        JsonContentEntryContext context,
        BattleSpecialProfileJsonDto dto
    )
    {
        if (
            dto.Profile?.Payload is not JsonElement payload
            || payload.ValueKind != JsonValueKind.Object
        )
        {
            return ContentImportStageResult<BattleSpecialProfileImportModel>.Failure(
                Diagnostic(
                    context,
                    BattleSpecialProfileJsonRules.InvalidPayload,
                    "Battle special profile payload must be a JSON object.",
                    "/profile/payload"
                )
            );
        }
        if (!string.Equals(dto.Profile.Kind, "meteor_swarm", StringComparison.Ordinal))
        {
            return ContentImportStageResult<BattleSpecialProfileImportModel>.Failure(
                Diagnostic(
                    context,
                    BattleSpecialProfileJsonRules.UnknownKind,
                    $"Battle special profile kind '{dto.Profile.Kind}' is not registered by the closed kind spec.",
                    "/profile/kind"
                )
            );
        }

        var payloadContext = new JsonContentEntryContext(
            context.DomainId,
            context.EntryId,
            context.SourceLabel,
            $"{context.JsonPointer}/profile/payload"
        );
        ContentImportStageResult<MeteorSwarmProfilePayloadJsonDto> parsed =
            ContentJsonStrictDtoParser.Parse(
                payloadContext,
                payload.GetRawText(),
                BattleSpecialProfileJsonSerializerContext.Default.MeteorSwarmProfilePayloadJsonDto,
                BattleSpecialProfileJsonRules.InvalidPayload
            );
        if (!parsed.HasValue)
            return ContentImportStageResult<BattleSpecialProfileImportModel>.Failure(parsed.Diagnostics);

        MeteorSwarmProfilePayloadJsonDto value = parsed.Value;
        var impactComponents = value.ImpactComponents
            .Select(component => new MeteorSwarmImpactComponentImportModel(
                component.ComponentId,
                component.RoleLabel,
                component.DamageTag,
                component.BasePower,
                component.DiceCount,
                component.DiceSides,
                component.RingWeight,
                component.SaveProfileId,
                component.CanCrit,
                component.MasteryWeight,
                component.RingMin,
                component.RingMax,
                new Dictionary<string, double>(
                    component.RingDamageScaleBasisPoints,
                    StringComparer.Ordinal
                )
            ))
            .ToArray();
        var terrainProfiles = value.TerrainProfiles
            .Select(terrain => new MeteorSwarmTerrainProfileImportModel(
                terrain.TerrainProfileId,
                terrain.RingMin,
                terrain.RingMax,
                terrain.TickEffectType,
                terrain.LifetimePolicy,
                terrain.MoveCostDelta,
                terrain.MoveCostStackKey,
                terrain.MoveCostStackMode,
                terrain.RenderOverlayId,
                terrain.OverlayPriority,
                terrain.DurationTu,
                terrain.TickIntervalTu,
                ProjectAccuracyModifier(terrain.AccuracyModifierSpec)
            ))
            .ToArray();
        return ContentImportStageResult<BattleSpecialProfileImportModel>.Success(
            new BattleSpecialProfileImportModel(
                dto.ProfileId,
                BattleSpecialProfileImportKind.MeteorSwarm,
                new MeteorSwarmProfileImportModel(
                    value.CoverageShapeId,
                    value.Radius,
                    value.ProfileVersion,
                    impactComponents,
                    value.ConcussedStatusId,
                    terrainProfiles,
                    value.FriendlyFireSoftExpectedHpPercent,
                    value.FriendlyFireHardExpectedHpPercent,
                    value.FriendlyFireHardWorstCaseHpPercent
                )
            )
        );
    }

    private static BattleAttackRollModifierImportModel? ProjectAccuracyModifier(
        BattleAttackRollModifierJsonDto? value
    ) =>
        value is null
            ? null
            : new BattleAttackRollModifierImportModel(
                value.SourceDomain,
                value.Label,
                value.ModifierDelta,
                value.StackKey,
                value.StackMode,
                value.RollKindFilter,
                value.EndpointMode,
                value.DistanceMinExclusive,
                value.DistanceMaxInclusive,
                value.TargetTeamFilter,
                value.FootprintMode,
                value.AppliesTo
            );

    private static ContentJsonDiagnostic Diagnostic(
        JsonContentEntryContext context,
        string ruleId,
        string message,
        string pointer
    ) =>
        new(
            ruleId,
            message,
            context.SourceLabel,
            $"{context.JsonPointer}{pointer}"
        );
}

internal static class BattleSpecialProfileJsonAuthoringDomains
{
    private static readonly ContentJsonNullabilityPolicy NullabilityPolicy =
        new(Array.Empty<string>());

    internal static IReadOnlyList<ContentJsonSchemaDomainRegistration> SchemaRegistrations { get; } =
        Array.AsReadOnly(
            new[]
            {
                BattleSpecialProfileJsonDomains.ManifestSchemaRegistration,
                BattleSpecialProfileJsonDomains.ProfileSchemaRegistration,
            }
        );

    internal static JsonContentDomainDescriptor<
        BattleSpecialProfileManifestJsonDto,
        BattleSpecialProfileManifestImportModel
    > CreateManifestDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            BattleSpecialProfileJsonDomains.ManifestDomainId,
            BattleSpecialProfileJsonDomains.SchemaVersion,
            "profile_id",
            sourceDirectory,
            sourceReader,
            NullabilityPolicy,
            BattleSpecialProfileJsonImportParser.ParseManifest,
            BattleSpecialProfileJsonImportParser.NormalizeManifest,
            BattleSpecialProfileImportValidator.ValidateManifest
        );

    internal static JsonContentDomainDescriptor<
        BattleSpecialProfileJsonDto,
        BattleSpecialProfileImportModel
    > CreateProfileDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        new(
            BattleSpecialProfileJsonDomains.ProfileDomainId,
            BattleSpecialProfileJsonDomains.SchemaVersion,
            "profile_id",
            sourceDirectory,
            sourceReader,
            NullabilityPolicy,
            BattleSpecialProfileJsonImportParser.ParseProfile,
            BattleSpecialProfileJsonImportParser.NormalizeProfile,
            BattleSpecialProfileImportValidator.ValidateProfile
        );

    internal static IEnumerable<IContentJsonOfflineValidationDomain> CreateOfflineDomains()
    {
        yield return new OfflineManifestDomain();
        yield return new OfflineProfileDomain();
    }

    private sealed class OfflineManifestDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => BattleSpecialProfileJsonDomains.ManifestDomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<BattleSpecialProfileManifestImportModel> batch =
                CreateManifestDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }

    private sealed class OfflineProfileDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => BattleSpecialProfileJsonDomains.ProfileDomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<BattleSpecialProfileImportModel> batch =
                CreateProfileDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }
}
