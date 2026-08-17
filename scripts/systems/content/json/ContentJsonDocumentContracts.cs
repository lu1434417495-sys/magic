using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal sealed record ContentJsonSourceText(string FilePath, string Utf8Json);

internal sealed class ContentJsonDocumentLoadOptions
{
    internal ContentJsonDocumentLoadOptions(
        int expectedSchemaVersion,
        string expectedDomain,
        string entryIdPropertyName
    )
    {
        if (expectedSchemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedSchemaVersion));
        if (string.IsNullOrWhiteSpace(expectedDomain))
            throw new ArgumentException("Expected domain is required.", nameof(expectedDomain));
        if (string.IsNullOrWhiteSpace(entryIdPropertyName))
        {
            throw new ArgumentException(
                "Entry ID property name is required.",
                nameof(entryIdPropertyName)
            );
        }

        ExpectedSchemaVersion = expectedSchemaVersion;
        ExpectedDomain = expectedDomain;
        EntryIdPropertyName = entryIdPropertyName;
    }

    internal int ExpectedSchemaVersion { get; }
    internal string ExpectedDomain { get; }
    internal string EntryIdPropertyName { get; }
}

internal sealed record ContentJsonDiagnostic(
    string RuleId,
    string Message,
    string SourceLabel,
    string JsonPointer
);

internal sealed record ContentJsonEntryDocument(string EntryId, string Json);

internal sealed class ContentJsonDocumentEnvelope
{
    internal ContentJsonDocumentEnvelope(
        string filePath,
        int schemaVersion,
        string domain,
        string family,
        IDictionary<string, string> templates,
        IList<ContentJsonEntryDocument> entries
    )
    {
        FilePath = filePath;
        SchemaVersion = schemaVersion;
        Domain = domain;
        Family = family;
        Templates = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(templates, StringComparer.Ordinal)
        );
        Entries = new ReadOnlyCollection<ContentJsonEntryDocument>(
            new List<ContentJsonEntryDocument>(entries)
        );
    }

    internal string FilePath { get; }
    internal int SchemaVersion { get; }
    internal string Domain { get; }
    internal string Family { get; }
    internal IReadOnlyDictionary<string, string> Templates { get; }
    internal IReadOnlyList<ContentJsonEntryDocument> Entries { get; }
}

internal sealed class ContentJsonDocumentLoadResult
{
    internal ContentJsonDocumentLoadResult(
        IList<ContentJsonDocumentEnvelope> documents,
        IList<ContentJsonDiagnostic> diagnostics
    )
    {
        Documents = new ReadOnlyCollection<ContentJsonDocumentEnvelope>(
            new List<ContentJsonDocumentEnvelope>(documents)
        );
        Diagnostics = new ReadOnlyCollection<ContentJsonDiagnostic>(
            new List<ContentJsonDiagnostic>(diagnostics)
        );
    }

    internal IReadOnlyList<ContentJsonDocumentEnvelope> Documents { get; }
    internal IReadOnlyList<ContentJsonDiagnostic> Diagnostics { get; }
    internal bool HasErrors => Diagnostics.Count > 0;
}

internal interface IContentJsonSourceReader
{
    IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath);
}
