using System;
using System.Collections.Generic;

internal sealed record JsonContentEntryContext(
    string DomainId,
    string EntryId,
    string SourceLabel,
    string JsonPointer
);

internal sealed class JsonContentDomainDescriptor<TDto, TImport>
    where TDto : notnull
    where TImport : notnull
{
    private readonly string _sourceDirectory;
    private readonly ContentJsonDocumentLoadOptions _loadOptions;
    private readonly ContentJsonDocumentLoader _documentLoader;
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
    internal JsonContentDomainDescriptor(
        string domainId,
        int schemaVersion,
        string entryIdPropertyName,
        string sourceDirectory,
        IContentJsonSourceReader sourceReader,
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
            throw new ArgumentException("Content domain ID is required.", nameof(domainId));
        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            throw new ArgumentException(
                "Content source directory is required.",
                nameof(sourceDirectory)
            );
        }

        ArgumentNullException.ThrowIfNull(sourceReader);
        ArgumentNullException.ThrowIfNull(nullabilityPolicy);
        ArgumentNullException.ThrowIfNull(parseDto);
        ArgumentNullException.ThrowIfNull(normalizeImportModel);
        ArgumentNullException.ThrowIfNull(validateDomainLocal);

        DomainId = domainId;
        SchemaVersion = schemaVersion;
        EntryIdPropertyName = entryIdPropertyName;
        _sourceDirectory = sourceDirectory;
        _loadOptions = new ContentJsonDocumentLoadOptions(
            schemaVersion,
            domainId,
            entryIdPropertyName
        );
        _documentLoader = new ContentJsonDocumentLoader(sourceReader);
        _nullabilityPolicy = nullabilityPolicy;
        _parseDto = parseDto;
        _normalizeImportModel = normalizeImportModel;
        _validateDomainLocal = validateDomainLocal;
    }

    internal string DomainId { get; }
    internal int SchemaVersion { get; }
    internal string EntryIdPropertyName { get; }

    internal ContentImportBatch<TImport> Import()
    {
        ContentJsonDocumentLoadResult loadResult = _documentLoader.LoadDirectory(
            _sourceDirectory,
            _loadOptions
        );
        var diagnostics = new List<ContentJsonDiagnostic>(loadResult.Diagnostics);
        var imports = new List<ContentImportEntry<TImport>>();

        foreach (ContentJsonDocumentEnvelope document in loadResult.Documents)
        {
            ContentJsonTemplateMergeResult mergeResult = ContentJsonTemplateMerger.Merge(
                document,
                _nullabilityPolicy
            );
            diagnostics.AddRange(mergeResult.Diagnostics);
            if (mergeResult.HasErrors)
                continue;

            for (int entryIndex = 0; entryIndex < mergeResult.Entries.Count; entryIndex += 1)
            {
                ContentJsonEntryDocument entry = mergeResult.Entries[entryIndex];
                var context = new JsonContentEntryContext(
                    DomainId,
                    entry.EntryId,
                    $"{FileLabel(document.FilePath)}#{entry.EntryId}",
                    $"/entries/{entryIndex}"
                );

                ContentImportStageResult<TDto> dtoResult = RequireStageResult(
                    _parseDto(context, entry.Json),
                    "DTO parser"
                );
                diagnostics.AddRange(dtoResult.Diagnostics);
                if (!dtoResult.HasValue)
                    continue;

                ContentImportStageResult<TImport> importResult = RequireStageResult(
                    _normalizeImportModel(context, dtoResult.Value),
                    "import-model normalizer"
                );
                diagnostics.AddRange(importResult.Diagnostics);
                if (!importResult.HasValue)
                    continue;

                IReadOnlyList<ContentJsonDiagnostic> validationDiagnostics =
                    _validateDomainLocal(context, importResult.Value)
                    ?? throw new InvalidOperationException(
                        $"Content domain '{DomainId}' validator returned null diagnostics."
                    );
                diagnostics.AddRange(validationDiagnostics);
                if (validationDiagnostics.Count > 0)
                    continue;

                imports.Add(new ContentImportEntry<TImport>(context, importResult.Value));
            }
        }

        return new ContentImportBatch<TImport>(imports, diagnostics);
    }

    private ContentImportStageResult<TValue> RequireStageResult<TValue>(
        ContentImportStageResult<TValue> result,
        string stageName
    )
        where TValue : notnull
    {
        return result
            ?? throw new InvalidOperationException(
                $"Content domain '{DomainId}' {stageName} returned a null result."
            );
    }

    private static string FileLabel(string filePath)
    {
        string normalized = (filePath ?? "").Replace('\\', '/');
        int separatorIndex = normalized.LastIndexOf('/');
        return separatorIndex >= 0 ? normalized[(separatorIndex + 1)..] : normalized;
    }
}
