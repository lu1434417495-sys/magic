#nullable enable

using System;
using System.Collections.Generic;

internal static class TraitContentJsonAuthoringDomain
{
    internal const string DomainId = "traits";
    internal const int SchemaVersion = 1;
    internal const string EntryIdPropertyName = "trait_id";
    internal const string ProductionDirectory = "res://data/configs/json/traits";

    private static readonly TraitImportModelValidator Validator = new();

    internal static ContentJsonSchemaDomainRegistration SchemaRegistration { get; } = new(
        DomainId,
        SchemaVersion,
        typeof(TraitJsonDocumentDto),
        "Magic trait JSON authoring schema",
        "Expanded trait entries with strict typed projections and bare save tags.",
        "res://data/schemas/content/traits.schema.json",
        "/data/configs/json/traits/**/*.json"
    );

    internal static JsonContentDomainDescriptor<TraitImportModel, TraitImportModel>
        CreateImportDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        CreateDescriptor(sourceDirectory, sourceReader, ValidateCurrentContract);

    internal static JsonContentDomainDescriptor<TraitImportModel, TraitImportModel>
        CreateSchemaImportDescriptor(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        ) => CreateDescriptor(
            sourceDirectory,
            sourceReader,
            static (_, _) => Array.Empty<ContentJsonDiagnostic>()
        );

    internal static IContentJsonOfflineValidationDomain CreateOfflineValidationDomain() =>
        new OfflineDomain();

    private static JsonContentDomainDescriptor<TraitImportModel, TraitImportModel>
        CreateDescriptor(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader,
            Func<
                JsonContentEntryContext,
                TraitImportModel,
                IReadOnlyList<ContentJsonDiagnostic>
            > validate
        ) => new(
            DomainId,
            SchemaVersion,
            EntryIdPropertyName,
            sourceDirectory,
            sourceReader,
            new ContentJsonNullabilityPolicy(Array.Empty<string>()),
            TraitJsonImportParser.Parse,
            static (_, import) => ContentImportStageResult<TraitImportModel>.Success(import),
            validate
        );

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateCurrentContract(
        JsonContentEntryContext context,
        TraitImportModel import
    ) => Validator.ValidateDomainLocal(context, import);

    private sealed class OfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => TraitContentJsonAuthoringDomain.DomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<TraitImportModel> batch =
                CreateImportDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }
}
