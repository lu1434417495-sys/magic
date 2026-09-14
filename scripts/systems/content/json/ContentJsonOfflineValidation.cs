using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

internal interface IContentJsonOfflineValidationDomain
{
    string DomainId { get; }

    ContentJsonOfflineValidationReport Validate(
        string sourceDirectory,
        IContentJsonSourceReader sourceReader
    );
}

internal sealed class ContentJsonOfflineValidationDomain<TDto, TImport>
    : IContentJsonOfflineValidationDomain
    where TDto : notnull
    where TImport : notnull
{
    private readonly int _schemaVersion;
    private readonly string _entryIdPropertyName;
    private readonly ContentJsonNullabilityPolicy _nullabilityPolicy;
    private readonly Func<
        JsonContentEntryContext,
        string,
        ContentImportStageResult<TDto>
    > _parseDto;
    private readonly Func<
        JsonContentEntryContext,
        TDto,
        ContentImportStageResult<TImport>
    > _normalizeImportModel;
    private readonly Func<
        JsonContentEntryContext,
        TImport,
        IReadOnlyList<ContentJsonDiagnostic>
    > _validateDomainLocal;

    internal ContentJsonOfflineValidationDomain(
        string domainId,
        int schemaVersion,
        string entryIdPropertyName,
        ContentJsonNullabilityPolicy nullabilityPolicy,
        Func<JsonContentEntryContext, string, ContentImportStageResult<TDto>> parseDto,
        Func<JsonContentEntryContext, TDto, ContentImportStageResult<TImport>> normalizeImportModel,
        Func<
            JsonContentEntryContext,
            TImport,
            IReadOnlyList<ContentJsonDiagnostic>
        > validateDomainLocal
    )
    {
        if (string.IsNullOrWhiteSpace(domainId))
            throw new ArgumentException("Offline validation domain ID is required.", nameof(domainId));
        if (schemaVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        if (string.IsNullOrWhiteSpace(entryIdPropertyName))
        {
            throw new ArgumentException(
                "Offline validation entry ID property is required.",
                nameof(entryIdPropertyName)
            );
        }

        ArgumentNullException.ThrowIfNull(nullabilityPolicy);
        ArgumentNullException.ThrowIfNull(parseDto);
        ArgumentNullException.ThrowIfNull(normalizeImportModel);
        ArgumentNullException.ThrowIfNull(validateDomainLocal);

        DomainId = domainId;
        _schemaVersion = schemaVersion;
        _entryIdPropertyName = entryIdPropertyName;
        _nullabilityPolicy = nullabilityPolicy;
        _parseDto = parseDto;
        _normalizeImportModel = normalizeImportModel;
        _validateDomainLocal = validateDomainLocal;
    }

    public string DomainId { get; }

    public ContentJsonOfflineValidationReport Validate(
        string sourceDirectory,
        IContentJsonSourceReader sourceReader
    )
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            throw new ArgumentException(
                "Offline validation source directory is required.",
                nameof(sourceDirectory)
            );
        }
        ArgumentNullException.ThrowIfNull(sourceReader);

        var descriptor = new JsonContentDomainDescriptor<TDto, TImport>(
            DomainId,
            _schemaVersion,
            _entryIdPropertyName,
            sourceDirectory,
            sourceReader,
            _nullabilityPolicy,
            _parseDto,
            _normalizeImportModel,
            _validateDomainLocal
        );
        ContentImportBatch<TImport> batch = descriptor.Import();
        return new ContentJsonOfflineValidationReport(
            DomainId,
            batch.Entries.Count,
            batch.Diagnostics
        );
    }
}

internal sealed class ContentJsonOfflineValidationReport
{
    internal const string ProtocolId = "magic.content_json.validation/v1";

    internal ContentJsonOfflineValidationReport(
        string domainId,
        int validatedEntryCount,
        IEnumerable<ContentJsonDiagnostic> diagnostics
    )
    {
        if (string.IsNullOrWhiteSpace(domainId))
            throw new ArgumentException("Validation report domain ID is required.", nameof(domainId));
        if (validatedEntryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(validatedEntryCount));
        ArgumentNullException.ThrowIfNull(diagnostics);

        var orderedDiagnostics = diagnostics
            .Select(diagnostic =>
                diagnostic
                ?? throw new ArgumentException(
                    "Validation report diagnostics cannot contain null.",
                    nameof(diagnostics)
                )
            )
            .OrderBy(diagnostic => diagnostic.SourceLabel, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.JsonPointer, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.RuleId, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToList();

        DomainId = domainId;
        Diagnostics = new ReadOnlyCollection<ContentJsonDiagnostic>(orderedDiagnostics);
        ValidatedEntryCount = orderedDiagnostics.Count == 0 ? validatedEntryCount : 0;
    }

    internal string DomainId { get; }
    internal int ValidatedEntryCount { get; }
    internal IReadOnlyList<ContentJsonDiagnostic> Diagnostics { get; }
    internal bool Success => Diagnostics.Count == 0;
}

internal static class ContentJsonOfflineValidationProtocol
{
    internal static string FormatJson(ContentJsonOfflineValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return WriteJson(indented: true, writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("protocol", ContentJsonOfflineValidationReport.ProtocolId);
            writer.WriteString("domain", report.DomainId);
            writer.WriteBoolean("success", report.Success);
            writer.WriteNumber("validated_entry_count", report.ValidatedEntryCount);
            writer.WriteNumber("diagnostic_count", report.Diagnostics.Count);
            writer.WritePropertyName("diagnostics");
            writer.WriteStartArray();
            foreach (ContentJsonDiagnostic diagnostic in report.Diagnostics)
                WriteDiagnostic(writer, diagnostic, includeRecordType: false, report.DomainId);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    internal static string FormatNdjson(ContentJsonOfflineValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var output = new StringBuilder();
        foreach (ContentJsonDiagnostic diagnostic in report.Diagnostics)
        {
            output.Append(
                WriteJson(indented: false, writer =>
                    WriteDiagnostic(writer, diagnostic, includeRecordType: true, report.DomainId)
                )
            );
        }

        output.Append(
            WriteJson(indented: false, writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("type", "summary");
                writer.WriteString("protocol", ContentJsonOfflineValidationReport.ProtocolId);
                writer.WriteString("domain", report.DomainId);
                writer.WriteBoolean("success", report.Success);
                writer.WriteNumber("validated_entry_count", report.ValidatedEntryCount);
                writer.WriteNumber("diagnostic_count", report.Diagnostics.Count);
                writer.WriteEndObject();
            })
        );
        return output.ToString();
    }

    private static void WriteDiagnostic(
        Utf8JsonWriter writer,
        ContentJsonDiagnostic diagnostic,
        bool includeRecordType,
        string domainId
    )
    {
        writer.WriteStartObject();
        if (includeRecordType)
        {
            writer.WriteString("type", "diagnostic");
            writer.WriteString("protocol", ContentJsonOfflineValidationReport.ProtocolId);
            writer.WriteString("domain", domainId);
        }
        writer.WriteString("rule_id", diagnostic.RuleId ?? "");
        writer.WriteString("message", diagnostic.Message ?? "");
        writer.WriteString("source_label", diagnostic.SourceLabel ?? "");
        writer.WriteString("json_pointer", diagnostic.JsonPointer ?? "");
        writer.WriteEndObject();
    }

    private static string WriteJson(bool indented, Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (
            var writer = new Utf8JsonWriter(
                buffer,
                new JsonWriterOptions
                {
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                    Indented = indented,
                    SkipValidation = false,
                }
            )
        )
        {
            write(writer);
            writer.Flush();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
            + "\n";
    }
}
