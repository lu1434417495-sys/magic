#nullable enable

using System;
using System.Collections.Generic;

internal static class ItemContentJsonAuthoringDomain
{
    internal const string DomainId = "items";
    internal const int SchemaVersion = 1;
    internal const string EntryIdPropertyName = "item_id";
    internal const string ProductionDirectory = "res://data/configs/json/items";

    private static readonly ItemImportModelValidator Validator = new();
    private static readonly ContentJsonNullabilityPolicy NullabilityPolicy = new(
        new[]
        {
            "/equip_requirement",
            "/weapon_profile",
            "/weapon_profile/one_handed_dice",
            "/weapon_profile/two_handed_dice",
        }
    );

    internal static ContentJsonSchemaDomainRegistration SchemaRegistration { get; } =
        new(
            domainId: DomainId,
            schemaVersion: SchemaVersion,
            documentDtoType: typeof(ItemJsonDocumentDto),
            title: "Magic item JSON authoring schema",
            description:
                "Flat, fully resolved item definitions. Legacy base_item_id and item template chains are intentionally absent.",
            trackedSchemaPath: "res://data/schemas/content/items.schema.json",
            contentFileMatch: "/data/configs/json/items/**/*.json"
        );

    internal static JsonContentDomainDescriptor<ItemJsonDto, ItemImportModel>
        CreateImportDescriptor(string sourceDirectory, IContentJsonSourceReader sourceReader) =>
        CreateDescriptor(sourceDirectory, sourceReader, Validator.ValidateDomainLocal);

    internal static JsonContentDomainDescriptor<ItemJsonDto, ItemImportModel>
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

    private static JsonContentDomainDescriptor<ItemJsonDto, ItemImportModel>
        CreateDescriptor(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader,
            Func<
                JsonContentEntryContext,
                ItemImportModel,
                IReadOnlyList<ContentJsonDiagnostic>
            > validate
        ) => new(
            domainId: DomainId,
            schemaVersion: SchemaVersion,
            entryIdPropertyName: EntryIdPropertyName,
            sourceDirectory: sourceDirectory,
            sourceReader: sourceReader,
            nullabilityPolicy: NullabilityPolicy,
            parseDto: ItemJsonImportParser.Parse,
            normalizeImportModel: ItemJsonImportParser.Normalize,
            validateDomainLocal: validate
        );

    private sealed class OfflineDomain : IContentJsonOfflineValidationDomain
    {
        public string DomainId => ItemContentJsonAuthoringDomain.DomainId;

        public ContentJsonOfflineValidationReport Validate(
            string sourceDirectory,
            IContentJsonSourceReader sourceReader
        )
        {
            ContentImportBatch<ItemImportModel> batch =
                CreateImportDescriptor(sourceDirectory, sourceReader).Import();
            return new ContentJsonOfflineValidationReport(
                DomainId,
                batch.Entries.Count,
                batch.Diagnostics
            );
        }
    }
}
