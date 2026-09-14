#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

public sealed class TraitContentRegistry : IDisposable
{
    private readonly Dictionary<StringName, TraitDefinition> _traitDefinitions = new();
    private readonly List<string> _validationErrors = new();
    private bool _disposed;

    internal TraitContentRegistry()
        : this(loadDefaultContent: true) { }

    internal TraitContentRegistry(bool loadDefaultContent)
    {
        if (loadDefaultContent)
            Rebuild();
    }

    /// <summary>Loads the only production trait source: strict JSON authoring documents.</summary>
    public void Rebuild() => LoadFromJsonDirectory(
        TraitContentJsonAuthoringDomain.ProductionDirectory,
        new GodotContentJsonSourceReader()
    );

    internal void LoadFromJsonDirectory(
        string directoryPath,
        IContentJsonSourceReader sourceReader
    )
    {
        _traitDefinitions.Clear();
        _validationErrors.Clear();
        // 内容形状问题走 batch.Diagnostics；目录打不开、权限不足、编码损坏这类 IO 故障
        // 由 reader 抛出并直接上浮，跟其它 *ContentRegistry 一致——把它们改写成
        // 校验错误字符串会丢掉异常类型和调用栈，还会把磁盘问题误报成内容作者的错。
        ContentImportBatch<TraitImportModel> batch =
            TraitContentJsonAuthoringDomain
                .CreateImportDescriptor(directoryPath, sourceReader)
                .Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
            _validationErrors.Add(FormatDiagnostic(diagnostic));
        if (batch.HasErrors)
            return;

        foreach (ContentImportEntry<TraitImportModel> entry in batch.Entries)
            RegisterImport(entry.Import, entry.Context.SourceLabel);
    }

    public IReadOnlyDictionary<StringName, TraitDefinition> GetTraitDefsTyped() =>
        new ReadOnlyDictionary<StringName, TraitDefinition>(
            new Dictionary<StringName, TraitDefinition>(_traitDefinitions)
        );

    public TraitDefinition GetTraitDef(StringName traitId)
    {
        StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
        return _traitDefinitions.TryGetValue(
            normalizedTraitId,
            out TraitDefinition? traitDefinition
        )
            ? traitDefinition
            : null!;
    }

    public bool HasTrait(StringName traitId) => GetTraitDef(traitId) != null;

    public Godot.Collections.Array<string> Validate()
    {
        var result = new Godot.Collections.Array<string>();
        foreach (string error in _validationErrors)
            result.Add(error);
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _traitDefinitions.Clear();
        _validationErrors.Clear();
        GC.SuppressFinalize(this);
    }

    internal static IReadOnlyList<string> ValidateDefinitions(
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(traitDefinitions);
        var errors = new List<string>();
        var ids = new List<string>();
        foreach (StringName traitId in traitDefinitions.Keys)
            ids.Add(traitId.ToString());
        ids.Sort(StringComparer.Ordinal);

        var validator = new TraitImportModelValidator();
        foreach (string id in ids)
        {
            TraitDefinition? definition = traitDefinitions[new StringName(id)];
            if (definition == null)
            {
                errors.Add($"Trait {id} must be a TraitDefinition.");
                continue;
            }
            errors.AddRange(
                validator.ValidateMessages(TraitDefinitionProjector.ToImport(definition))
            );
        }
        return new ReadOnlyCollection<string>(errors);
    }

    private void RegisterImport(TraitImportModel import, string sourceLabel)
    {
        StringName traitId = new(import.TraitId);
        if (_traitDefinitions.ContainsKey(traitId))
        {
            _validationErrors.Add(
                $"Duplicate trait_id registered: {traitId} ({sourceLabel})."
            );
            return;
        }
        _traitDefinitions.Add(traitId, TraitDefinitionProjector.Project(import));
    }

    private static string FormatDiagnostic(ContentJsonDiagnostic diagnostic) =>
        $"{diagnostic.RuleId} {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}";
}
