#nullable enable

using System;
using System.Collections.Generic;

internal static class ItemImportCanonicalJson
{
    private sealed record EmptyTemplateMap;

    private sealed class ItemImportDocument
    {
        internal ItemImportDocument(string family, IReadOnlyList<ItemImportModel> entries)
        {
            if (string.IsNullOrWhiteSpace(family))
                throw new ArgumentException("Item family is required.", nameof(family));
            ArgumentNullException.ThrowIfNull(entries);
            Family = family;
            Entries = entries;
        }

        internal string Family { get; }
        internal IReadOnlyList<ItemImportModel> Entries { get; }
        internal EmptyTemplateMap Templates { get; } = new();
    }

    private static readonly ContentCanonicalJsonValueSchema<EmptyTemplateMap> EmptyTemplates =
        ContentCanonicalJsonValue.Object(new ContentCanonicalJsonObjectSchema<EmptyTemplateMap>());

    private static readonly ContentCanonicalJsonObjectSchema<ItemImportDocument> DocumentSchema =
        new(
            ContentCanonicalJsonProperty<ItemImportDocument>.Required(
                "schema",
                static _ => ItemContentJsonAuthoringDomain.SchemaVersion,
                ContentCanonicalJsonValue.Int32
            ),
            ContentCanonicalJsonProperty<ItemImportDocument>.Required(
                "domain",
                static _ => ItemContentJsonAuthoringDomain.DomainId,
                ContentCanonicalJsonValue.Text
            ),
            ContentCanonicalJsonProperty<ItemImportDocument>.Required(
                "family", static value => value.Family, ContentCanonicalJsonValue.Text
            ),
            ContentCanonicalJsonProperty<ItemImportDocument>.Required(
                "templates", static value => value.Templates, EmptyTemplates
            ),
            ContentCanonicalJsonProperty<ItemImportDocument>.Required(
                "entries",
                static value => value.Entries,
                ContentCanonicalJsonValue.Array(
                    ContentCanonicalJsonValue.Object(ItemCanonicalJsonSchema.EntrySchema)
                )
            )
        );

    internal static string WriteDocument(
        ContentCanonicalJsonWriter writer,
        string family,
        IReadOnlyList<ItemImportModel> entries
    )
    {
        ArgumentNullException.ThrowIfNull(writer);
        return NormalizeLineEndings(
            writer.Write(
                new ItemImportDocument(family, entries),
                DocumentSchema,
                indented: true
            )
        ) + "\n";
    }

    internal static string WriteEntry(ContentCanonicalJsonWriter writer, ItemImportModel entry)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(entry);
        return NormalizeLineEndings(
            writer.Write(entry, ItemCanonicalJsonSchema.EntrySchema, indented: true)
        );
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
}
