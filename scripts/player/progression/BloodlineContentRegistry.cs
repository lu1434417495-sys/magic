using System.Collections.Generic;
using Godot;

public class BloodlineContentRegistry : IdentityContentRegistryBase
{
    private const string BloodlineConfigDirectoryPath = ProfessionIdentityJsonDomains.BloodlineDirectory;

    private readonly Dictionary<StringName, BloodlineDefinition> _bloodline_defs = new();
    private readonly Dictionary<StringName, BloodlineStageDefinition> _bloodline_stage_defs = new();
    private readonly IContentJsonSourceReader _jsonSourceReader;

    internal BloodlineContentRegistry(bool loadDefaultContent = true)
        : this(new GodotContentJsonSourceReader(), loadDefaultContent) { }

    internal BloodlineContentRegistry(IContentJsonSourceReader jsonSourceReader, bool loadDefaultContent = true)
        : base()
    {
        _jsonSourceReader = jsonSourceReader
            ?? throw new System.ArgumentNullException(nameof(jsonSourceReader));
        _registry_label = "BloodlineContentRegistry";
        if (loadDefaultContent)
            Rebuild();
    }

    public void Rebuild() => LoadFromDirectory(BloodlineConfigDirectoryPath);

    public void LoadFromDirectory(string directoryPath)
    {
        LoadFromDirectories(new Godot.Collections.Array<string> { directoryPath });
    }

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _bloodline_defs.Clear();
        _bloodline_stage_defs.Clear();
        _validation_errors.Clear();
        foreach (var directoryPath in directoryPaths)
            ImportDirectory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public IReadOnlyDictionary<StringName, BloodlineDefinition> GetBloodlineDefsTyped() =>
        _snapshot_definitions(_bloodline_defs);

    public IReadOnlyDictionary<StringName, BloodlineStageDefinition> GetBloodlineStageDefsTyped() =>
        _snapshot_definitions(_bloodline_stage_defs);

    protected override void ClearRegistryData()
    {
        _bloodline_defs.Clear();
        _bloodline_stage_defs.Clear();
    }

    private void ImportDirectory(string directoryPath)
    {
        ContentImportBatch<BloodlineImportModel> batch = ProfessionIdentityJsonImport
            .CreateBloodlineDescriptor(directoryPath, _jsonSourceReader)
            .Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
            _validation_errors.Add(ProfessionIdentityJsonImport.FormatDiagnostic(diagnostic));
        foreach (ContentImportEntry<BloodlineImportModel> entry in batch.Entries)
        {
            try
            {
                if (entry.Import.Kind == "bloodline")
                {
                    BloodlineDefinition definition = ProfessionIdentityDefinitionProjector.ProjectBloodline(entry.Import);
                    if (!_bloodline_defs.TryAdd(definition.BloodlineId, definition))
                        _validation_errors.Add($"Duplicate bloodline_id registered: {definition.BloodlineId}");
                }
                else
                {
                    BloodlineStageDefinition definition = ProfessionIdentityDefinitionProjector.ProjectBloodlineStage(entry.Import);
                    if (!_bloodline_stage_defs.TryAdd(definition.StageId, definition))
                        _validation_errors.Add($"Duplicate bloodline stage_id registered: {definition.StageId}");
                }
            }
            catch (System.Exception exception)
                when (exception is System.IO.InvalidDataException
                    or System.InvalidOperationException)
            {
                _validation_errors.Add(
                    $"Bloodline JSON {entry.Context.SourceLabel} projection failed: {exception.GetType().Name}: {exception.Message}"
                );
            }
        }
    }

    private List<string> _collect_validation_errors()
    {
        var errors = new List<string>();
        foreach (var bloodlineKey in _sorted_registry_keys(_bloodline_defs.Keys))
        {
            var bloodlineId = new StringName(bloodlineKey);
            _append_bloodline_validation_errors(errors, bloodlineId, _bloodline_defs[bloodlineId]);
        }
        foreach (var stageKey in _sorted_registry_keys(_bloodline_stage_defs.Keys))
        {
            var stageId = new StringName(stageKey);
            _append_bloodline_stage_validation_errors(errors, stageId, _bloodline_stage_defs[stageId]);
        }
        return errors;
    }

    private void _append_bloodline_validation_errors(
        ICollection<string> errors,
        StringName bloodlineId,
        BloodlineDefinition bloodlineDef
    )
    {
        var ownerLabel = $"Bloodline {bloodlineId}";
        _append_string_name_field_error(
            errors,
            ownerLabel,
            "bloodline_id",
            bloodlineDef.BloodlineId
        );
        _append_required_string_field_error(
            errors,
            ownerLabel,
            "display_name",
            bloodlineDef.DisplayName
        );
        _append_required_string_field_error(
            errors,
            ownerLabel,
            "description",
            bloodlineDef.Description
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            bloodlineDef.StageIds,
            "stage_ids"
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            bloodlineDef.TraitIds,
            "trait_ids"
        );
        _append_racial_granted_skill_array_errors(
            errors,
            ownerLabel,
            bloodlineDef.RacialGrantedSkills,
            "racial_granted_skills"
        );
        _append_attribute_modifier_array_errors(
            errors,
            ownerLabel,
            bloodlineDef.AttributeModifiers,
            "attribute_modifiers"
        );
        _append_string_array_errors(
            errors,
            ownerLabel,
            bloodlineDef.TraitSummary,
            "trait_summary"
        );
    }

    private void _append_bloodline_stage_validation_errors(
        ICollection<string> errors,
        StringName stageId,
        BloodlineStageDefinition stageDef
    )
    {
        var ownerLabel = $"BloodlineStage {stageId}";
        _append_string_name_field_error(errors, ownerLabel, "stage_id", stageDef.StageId);
        _append_string_name_field_error(errors, ownerLabel, "bloodline_id", stageDef.BloodlineId);
        _append_required_string_field_error(
            errors,
            ownerLabel,
            "display_name",
            stageDef.DisplayName
        );
        _append_required_string_field_error(
            errors,
            ownerLabel,
            "description",
            stageDef.Description
        );
        _append_attribute_modifier_array_errors(
            errors,
            ownerLabel,
            stageDef.AttributeModifiers,
            "attribute_modifiers"
        );
        _append_string_name_array_errors(errors, ownerLabel, stageDef.TraitIds, "trait_ids");
        _append_racial_granted_skill_array_errors(
            errors,
            ownerLabel,
            stageDef.RacialGrantedSkills,
            "racial_granted_skills"
        );
        _append_string_array_errors(errors, ownerLabel, stageDef.TraitSummary, "trait_summary");
    }

}
