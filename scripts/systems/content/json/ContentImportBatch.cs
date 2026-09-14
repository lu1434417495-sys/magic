using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal sealed record ContentImportEntry<TImport>(
    JsonContentEntryContext Context,
    TImport Import
)
    where TImport : notnull;

internal sealed class ContentImportBatch<TImport>
    where TImport : notnull
{
    internal ContentImportBatch(
        IEnumerable<ContentImportEntry<TImport>> entries,
        IEnumerable<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var diagnosticCopy = new List<ContentJsonDiagnostic>(diagnostics);
        var entryCopy = diagnosticCopy.Count == 0
            ? new List<ContentImportEntry<TImport>>(entries)
            : new List<ContentImportEntry<TImport>>();

        Entries = new ReadOnlyCollection<ContentImportEntry<TImport>>(entryCopy);
        Diagnostics = new ReadOnlyCollection<ContentJsonDiagnostic>(diagnosticCopy);
    }

    internal IReadOnlyList<ContentImportEntry<TImport>> Entries { get; }
    internal IReadOnlyList<ContentJsonDiagnostic> Diagnostics { get; }
    internal bool HasErrors => Diagnostics.Count > 0;
}

internal sealed class ContentImportStageResult<TValue>
    where TValue : notnull
{
    private readonly TValue _value;

    private ContentImportStageResult(
        TValue value,
        bool hasValue,
        IEnumerable<ContentJsonDiagnostic> diagnostics
    )
    {
        _value = value;
        HasValue = hasValue;
        Diagnostics = new ReadOnlyCollection<ContentJsonDiagnostic>(
            new List<ContentJsonDiagnostic>(diagnostics)
        );
    }

    internal TValue Value =>
        HasValue
            ? _value
            : throw new InvalidOperationException(
                "A failed content import stage does not expose a value."
            );
    internal bool HasValue { get; }
    internal IReadOnlyList<ContentJsonDiagnostic> Diagnostics { get; }

    internal static ContentImportStageResult<TValue> Success(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ContentImportStageResult<TValue>(
            value,
            hasValue: true,
            Array.Empty<ContentJsonDiagnostic>()
        );
    }

    internal static ContentImportStageResult<TValue> Failure(
        IEnumerable<ContentJsonDiagnostic> diagnostics
    )
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var diagnosticCopy = new List<ContentJsonDiagnostic>(diagnostics);
        if (diagnosticCopy.Count == 0)
        {
            throw new ArgumentException(
                "A failed content import stage must provide at least one diagnostic.",
                nameof(diagnostics)
            );
        }

        return new ContentImportStageResult<TValue>(default, hasValue: false, diagnosticCopy);
    }

    internal static ContentImportStageResult<TValue> Failure(
        ContentJsonDiagnostic diagnostic
    )
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return Failure(new[] { diagnostic });
    }
}
