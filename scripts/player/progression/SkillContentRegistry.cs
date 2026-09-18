using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;
using Godot.Collections;

public class SkillContentRegistry : System.IDisposable
{
    private const string SkillConfigDirectory = "res://data/configs/json/skills";

    private readonly List<string> _validationErrors = new();
    private readonly System.Collections.Generic.Dictionary<StringName, SkillImportModel>
        _skillImports = new();
    private readonly System.Collections.Generic.Dictionary<StringName, SkillDefinition>
        _skillDefinitions = new();
    public Array<string> _validation_errors
    {
        get => ToGodotStringArray(_validationErrors);
        set
        {
            _validationErrors.Clear();
            if (value == null)
                return;
            foreach (string error in value)
                _validationErrors.Add(error);
        }
    }
    private bool _disposed;
    private readonly SkillImportModelValidator _importModelValidator = new();

    internal SkillContentRegistry()
        : this(loadDefaultContent: true) { }

    internal SkillContentRegistry(bool loadDefaultContent)
    {
        if (loadDefaultContent)
            Rebuild();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        System.GC.SuppressFinalize(this);
        DisposeManagedRegistry();
    }

    private void DisposeManagedRegistry()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _skillImports.Clear();
        _skillDefinitions.Clear();
        _validationErrors.Clear();
    }

    public void Rebuild()
    {
        LoadFromDirectory(SkillConfigDirectory);
    }

    public void LoadFromDirectory(string directoryPath)
    {
        _skillImports.Clear();
        _skillDefinitions.Clear();
        _validationErrors.Clear();
        ScanDirectory(directoryPath);
        AppendArray(_validationErrors, CollectValidationErrors());
        foreach (
            KeyValuePair<StringName, SkillImportModel> pair in _skillImports
        )
        {
            _skillDefinitions.Add(
                pair.Key,
                SkillDefinitionProjector.Project(pair.Value)
            );
        }
    }

    internal IReadOnlyDictionary<StringName, SkillDefinition> GetSkillDefinitionsTyped()
    {
        return new ReadOnlyDictionary<StringName, SkillDefinition>(
            new System.Collections.Generic.Dictionary<StringName, SkillDefinition>(
                _skillDefinitions
            )
        );
    }

    public Array<string> Validate()
    {
        var copy = new Array<string>();
        foreach (string error in _validationErrors)
            copy.Add(error);
        return copy;
    }

    private void ScanDirectory(string directoryPath)
    {
        ContentImportBatch<SkillImportModel> batch =
            SkillContentJsonAuthoringDomain.CreateImportDescriptor(
                directoryPath,
                new GodotContentJsonSourceReader()
            ).Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
        {
            _validationErrors.Add(
                $"{diagnostic.RuleId} {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}"
            );
        }
        if (batch.HasErrors)
            return;
        foreach (ContentImportEntry<SkillImportModel> entry in batch.Entries)
        {
            StringName skillId = entry.Import.SkillId.Value;
            if (!_skillImports.TryAdd(skillId, entry.Import))
                _validationErrors.Add($"Duplicate skill_id registered: {skillId}");
        }
    }

    private Array<string> CollectValidationErrors()
    {
        var errors = new Array<string>();
        foreach (string message in _importModelValidator.ValidateBatchMessages(_skillImports))
            errors.Add(message);
        return errors;
    }

    private static void AppendArray(List<string> target, Array<string> source)
    {
        foreach (string value in source)
            target.Add(value);
    }

    private static Array<string> ToGodotStringArray(IEnumerable<string> values)
    {
        var result = new Array<string>();
        if (values == null)
            return result;
        foreach (string value in values)
            result.Add(value);
        return result;
    }
}
