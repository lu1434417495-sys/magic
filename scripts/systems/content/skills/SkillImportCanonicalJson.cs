#nullable enable

using System;
using System.Collections.Generic;

internal static class SkillImportCanonicalJson
{
    internal const string FamilyId = "mage_prismatic_ward";

    private sealed record EmptyTemplateMap;

    private sealed class SkillImportDocument
    {
        internal SkillImportDocument(SkillImportModel entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            Entries = Array.AsReadOnly(new[] { entry });
        }

        internal IReadOnlyList<SkillImportModel> Entries { get; }
        internal EmptyTemplateMap Templates { get; } = new();
    }

    private static readonly ContentCanonicalJsonValueSchema<EmptyTemplateMap> EmptyTemplates =
        ContentCanonicalJsonValue.Object(
            new ContentCanonicalJsonObjectSchema<EmptyTemplateMap>()
        );

    private static readonly ContentCanonicalJsonObjectSchema<SkillImportDocument> DocumentSchema =
        new(
            ContentCanonicalJsonProperty<SkillImportDocument>.Required(
                "schema",
                static _ => SkillContentJsonAuthoringDomain.SchemaVersion,
                ContentCanonicalJsonValue.Int32
            ),
            ContentCanonicalJsonProperty<SkillImportDocument>.Required(
                "domain",
                static _ => SkillContentJsonAuthoringDomain.DomainId,
                ContentCanonicalJsonValue.Text
            ),
            ContentCanonicalJsonProperty<SkillImportDocument>.Required(
                "family",
                static _ => FamilyId,
                ContentCanonicalJsonValue.Text
            ),
            ContentCanonicalJsonProperty<SkillImportDocument>.Required(
                "templates",
                static value => value.Templates,
                EmptyTemplates
            ),
            ContentCanonicalJsonProperty<SkillImportDocument>.Required(
                "entries",
                static value => value.Entries,
                ContentCanonicalJsonValue.Array(
                    ContentCanonicalJsonValue.Object(SkillCanonicalJsonSchema.EntrySchema)
                )
            )
        );

    internal static string WriteSingleEntry(
        ContentCanonicalJsonWriter writer,
        SkillImportModel entry
    )
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(entry);
        return writer.Write(new SkillImportDocument(entry), DocumentSchema, indented: true) + "\n";
    }

    internal static string WriteEntry(
        ContentCanonicalJsonWriter writer,
        SkillImportModel entry
    )
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(entry);
        return writer.Write(entry, SkillCanonicalJsonSchema.EntrySchema, indented: true);
    }
}
