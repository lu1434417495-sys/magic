using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

internal sealed class ContentJsonDocumentLoader
{
    internal const string InvalidJsonRule = "content.json.document.invalid_json";
    internal const string InvalidRootRule = "content.json.document.invalid_root";
    internal const string UnknownRootMemberRule = "content.json.document.unknown_root_member";
    internal const string InvalidSchemaRule = "content.json.document.invalid_schema";
    internal const string SchemaMismatchRule = "content.json.document.schema_mismatch";
    internal const string InvalidDomainRule = "content.json.document.invalid_domain";
    internal const string DomainMismatchRule = "content.json.document.domain_mismatch";
    internal const string InvalidFamilyRule = "content.json.document.invalid_family";
    internal const string InvalidTemplatesRule = "content.json.document.invalid_templates";
    internal const string InvalidEntriesRule = "content.json.document.invalid_entries";
    internal const string InvalidEntryRule = "content.json.document.invalid_entry";
    internal const string InvalidEntryIdRule = "content.json.document.invalid_entry_id";
    internal const string DuplicateEntryIdRule = "content.json.document.duplicate_entry_id";

    private readonly IContentJsonSourceReader _sourceReader;

    internal ContentJsonDocumentLoader(IContentJsonSourceReader sourceReader)
    {
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    internal ContentJsonDocumentLoadResult LoadDirectory(
        string directoryPath,
        ContentJsonDocumentLoadOptions options
    )
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("JSON content directory is required.", nameof(directoryPath));

        return ParseDocuments(_sourceReader.ReadUtf8Documents(directoryPath), options);
    }

    internal static ContentJsonDocumentLoadResult ParseDocuments(
        IEnumerable<ContentJsonSourceText> sources,
        ContentJsonDocumentLoadOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(options);

        var documents = new List<ContentJsonDocumentEnvelope>();
        var diagnostics = new List<ContentJsonDiagnostic>();
        var entryOccurrences = new List<EntryOccurrence>();

        foreach (ContentJsonSourceText source in sources)
        {
            if (source == null)
                throw new ArgumentException("JSON content source cannot be null.", nameof(sources));

            ParseDocument(source, options, documents, diagnostics, entryOccurrences);
        }

        AppendDuplicateEntryDiagnostics(entryOccurrences, diagnostics);
        return new ContentJsonDocumentLoadResult(documents, diagnostics);
    }

    private static void ParseDocument(
        ContentJsonSourceText source,
        ContentJsonDocumentLoadOptions options,
        List<ContentJsonDocumentEnvelope> documents,
        List<ContentJsonDiagnostic> diagnostics,
        List<EntryOccurrence> entryOccurrences
    )
    {
        string fileLabel = FileLabel(source.FilePath);
        int initialDiagnosticCount = diagnostics.Count;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                source.Utf8Json ?? "",
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                }
            );
        }
        catch (JsonException exception)
        {
            diagnostics.Add(
                new ContentJsonDiagnostic(
                    InvalidJsonRule,
                    $"JSON document is malformed: {exception.Message}",
                    fileLabel,
                    ""
                )
            );
            return;
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(
                    new ContentJsonDiagnostic(
                        InvalidRootRule,
                        "JSON document root must be an object.",
                        fileLabel,
                        ""
                    )
                );
                return;
            }

            ValidateRootMembers(root, fileLabel, diagnostics);
            int schema = ReadSchema(root, fileLabel, options, diagnostics);
            string domain = ReadDomain(root, fileLabel, options, diagnostics);
            string family = ReadFamily(root, fileLabel, diagnostics);
            Dictionary<string, string> templates = ReadTemplates(root, fileLabel, diagnostics);
            List<ContentJsonEntryDocument> entries = ReadEntries(
                root,
                source.FilePath,
                fileLabel,
                options,
                diagnostics,
                entryOccurrences
            );

            if (diagnostics.Count != initialDiagnosticCount)
                return;

            documents.Add(
                new ContentJsonDocumentEnvelope(
                    source.FilePath,
                    schema,
                    domain,
                    family,
                    templates,
                    entries
                )
            );
        }
    }

    private static void ValidateRootMembers(
        JsonElement root,
        string fileLabel,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (IsKnownRootMember(property.Name))
                continue;

            diagnostics.Add(
                new ContentJsonDiagnostic(
                    UnknownRootMemberRule,
                    $"Unknown JSON document root member '{property.Name}'.",
                    fileLabel,
                    Pointer(property.Name)
                )
            );
        }
    }

    private static int ReadSchema(
        JsonElement root,
        string fileLabel,
        ContentJsonDocumentLoadOptions options,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (
            !root.TryGetProperty("schema", out JsonElement value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out int schema)
        )
        {
            diagnostics.Add(
                new ContentJsonDiagnostic(
                    InvalidSchemaRule,
                    "JSON document 'schema' must be an integer.",
                    fileLabel,
                    "/schema"
                )
            );
            return 0;
        }

        if (schema != options.ExpectedSchemaVersion)
        {
            diagnostics.Add(
                new ContentJsonDiagnostic(
                    SchemaMismatchRule,
                    $"JSON document schema {schema} does not match expected schema {options.ExpectedSchemaVersion}.",
                    fileLabel,
                    "/schema"
                )
            );
        }
        return schema;
    }

    private static string ReadDomain(
        JsonElement root,
        string fileLabel,
        ContentJsonDocumentLoadOptions options,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (
            !root.TryGetProperty("domain", out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString())
        )
        {
            diagnostics.Add(
                new ContentJsonDiagnostic(
                    InvalidDomainRule,
                    "JSON document 'domain' must be a non-empty string.",
                    fileLabel,
                    "/domain"
                )
            );
            return "";
        }

        string domain = value.GetString();
        if (!string.Equals(domain, options.ExpectedDomain, StringComparison.Ordinal))
        {
            diagnostics.Add(
                new ContentJsonDiagnostic(
                    DomainMismatchRule,
                    $"JSON document domain '{domain}' does not match expected domain '{options.ExpectedDomain}'.",
                    fileLabel,
                    "/domain"
                )
            );
        }
        return domain;
    }

    private static string ReadFamily(
        JsonElement root,
        string fileLabel,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (
            !root.TryGetProperty("family", out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString())
        )
        {
            diagnostics.Add(
                new ContentJsonDiagnostic(
                    InvalidFamilyRule,
                    "JSON document 'family' must be a non-empty string.",
                    fileLabel,
                    "/family"
                )
            );
            return "";
        }
        return value.GetString();
    }

    private static Dictionary<string, string> ReadTemplates(
        JsonElement root,
        string fileLabel,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var templates = new Dictionary<string, string>(StringComparer.Ordinal);
        if (
            !root.TryGetProperty("templates", out JsonElement value)
            || value.ValueKind != JsonValueKind.Object
        )
        {
            diagnostics.Add(
                new ContentJsonDiagnostic(
                    InvalidTemplatesRule,
                    "JSON document 'templates' must be an object.",
                    fileLabel,
                    "/templates"
                )
            );
            return templates;
        }

        foreach (JsonProperty template in value.EnumerateObject())
            templates[template.Name] = template.Value.GetRawText();
        return templates;
    }

    private static List<ContentJsonEntryDocument> ReadEntries(
        JsonElement root,
        string filePath,
        string fileLabel,
        ContentJsonDocumentLoadOptions options,
        List<ContentJsonDiagnostic> diagnostics,
        List<EntryOccurrence> entryOccurrences
    )
    {
        var entries = new List<ContentJsonEntryDocument>();
        if (
            !root.TryGetProperty("entries", out JsonElement value)
            || value.ValueKind != JsonValueKind.Array
        )
        {
            diagnostics.Add(
                new ContentJsonDiagnostic(
                    InvalidEntriesRule,
                    "JSON document 'entries' must be an array.",
                    fileLabel,
                    "/entries"
                )
            );
            return entries;
        }

        int entryIndex = 0;
        foreach (JsonElement entry in value.EnumerateArray())
        {
            string entryPointer = $"/entries/{entryIndex}";
            if (entry.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add(
                    new ContentJsonDiagnostic(
                        InvalidEntryRule,
                        "JSON document entry must be an object.",
                        fileLabel,
                        entryPointer
                    )
                );
                entryIndex += 1;
                continue;
            }

            if (
                !entry.TryGetProperty(options.EntryIdPropertyName, out JsonElement idValue)
                || idValue.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(idValue.GetString())
            )
            {
                diagnostics.Add(
                    new ContentJsonDiagnostic(
                        InvalidEntryIdRule,
                        $"JSON document entry '{options.EntryIdPropertyName}' must be a non-empty string.",
                        fileLabel,
                        $"{entryPointer}/{EscapePointerToken(options.EntryIdPropertyName)}"
                    )
                );
                entryIndex += 1;
                continue;
            }

            string entryId = idValue.GetString();
            entries.Add(new ContentJsonEntryDocument(entryId, entry.GetRawText()));
            entryOccurrences.Add(
                new EntryOccurrence(
                    entryId,
                    filePath,
                    $"{entryPointer}/{EscapePointerToken(options.EntryIdPropertyName)}"
                )
            );
            entryIndex += 1;
        }
        return entries;
    }

    private static void AppendDuplicateEntryDiagnostics(
        IReadOnlyList<EntryOccurrence> occurrences,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        foreach (
            IGrouping<string, EntryOccurrence> duplicateGroup in occurrences
                .GroupBy(occurrence => occurrence.EntryId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
        )
        {
            foreach (EntryOccurrence occurrence in duplicateGroup)
            {
                diagnostics.Add(
                    new ContentJsonDiagnostic(
                        DuplicateEntryIdRule,
                        $"JSON content entry ID '{occurrence.EntryId}' is duplicated.",
                        $"{FileLabel(occurrence.FilePath)}#{occurrence.EntryId}",
                        occurrence.JsonPointer
                    )
                );
            }
        }
    }

    private static bool IsKnownRootMember(string member) =>
        member is "schema" or "domain" or "family" or "templates" or "entries";

    private static string Pointer(string propertyName) => $"/{EscapePointerToken(propertyName)}";

    private static string EscapePointerToken(string value) =>
        (value ?? "").Replace("~", "~0", StringComparison.Ordinal)
            .Replace("/", "~1", StringComparison.Ordinal);

    private static string FileLabel(string filePath)
    {
        string normalized = (filePath ?? "").Replace('\\', '/');
        int separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex >= 0 ? normalized[(separatorIndex + 1)..] : normalized;
    }

    private sealed record EntryOccurrence(string EntryId, string FilePath, string JsonPointer);
}
