using System.Collections.Generic;
using Godot;

public class AscensionContentRegistry : IdentityContentRegistryBase
{
    private const string AscensionConfigDirectoryPath = ProfessionIdentityJsonDomains.AscensionDirectory;

    private readonly Dictionary<StringName, AscensionDefinition> _ascension_defs = new();
    private readonly Dictionary<StringName, AscensionStageDefinition> _ascension_stage_defs = new();
    private readonly IContentJsonSourceReader _jsonSourceReader;

    internal AscensionContentRegistry(bool loadDefaultContent = true)
        : this(new GodotContentJsonSourceReader(), loadDefaultContent) { }

    internal AscensionContentRegistry(IContentJsonSourceReader jsonSourceReader, bool loadDefaultContent = true)
        : base()
    {
        _jsonSourceReader = jsonSourceReader
            ?? throw new System.ArgumentNullException(nameof(jsonSourceReader));
        _registry_label = "AscensionContentRegistry";
        if (loadDefaultContent)
            Rebuild();
    }

    public void Rebuild() => LoadFromDirectory(AscensionConfigDirectoryPath);

    public void LoadFromDirectory(string directoryPath)
    {
        LoadFromDirectories(new Godot.Collections.Array<string> { directoryPath });
    }

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _ascension_defs.Clear();
        _ascension_stage_defs.Clear();
        _validation_errors.Clear();
        foreach (var directoryPath in directoryPaths)
            ImportDirectory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public IReadOnlyDictionary<StringName, AscensionDefinition> GetAscensionDefsTyped() =>
        _snapshot_definitions(_ascension_defs);

    public IReadOnlyDictionary<StringName, AscensionStageDefinition> GetAscensionStageDefsTyped() =>
        _snapshot_definitions(_ascension_stage_defs);

    protected override void ClearRegistryData()
    {
        _ascension_defs.Clear();
        _ascension_stage_defs.Clear();
    }

    private void ImportDirectory(string directoryPath)
    {
        ContentImportBatch<AscensionImportModel> batch = ProfessionIdentityJsonImport
            .CreateAscensionDescriptor(directoryPath, _jsonSourceReader)
            .Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
            _validation_errors.Add(ProfessionIdentityJsonImport.FormatDiagnostic(diagnostic));
        foreach (ContentImportEntry<AscensionImportModel> entry in batch.Entries)
        {
            try
            {
                if (entry.Import.Kind == "ascension")
                {
                    AscensionDefinition definition = ProfessionIdentityDefinitionProjector.ProjectAscension(entry.Import);
                    if (!_ascension_defs.TryAdd(definition.AscensionId, definition))
                        _validation_errors.Add($"Duplicate ascension_id registered: {definition.AscensionId}");
                }
                else
                {
                    AscensionStageDefinition definition = ProfessionIdentityDefinitionProjector.ProjectAscensionStage(entry.Import);
                    if (!_ascension_stage_defs.TryAdd(definition.StageId, definition))
                        _validation_errors.Add($"Duplicate ascension stage_id registered: {definition.StageId}");
                }
            }
            catch (System.Exception exception)
                when (exception is System.IO.InvalidDataException
                    or System.InvalidOperationException)
            {
                _validation_errors.Add(
                    $"Ascension JSON {entry.Context.SourceLabel} projection failed: {exception.GetType().Name}: {exception.Message}"
                );
            }
        }
    }

    private List<string> _collect_validation_errors()
    {
        var errors = new List<string>();
        foreach (var ascensionKey in _sorted_registry_keys(_ascension_defs.Keys))
        {
            var ascensionId = new StringName(ascensionKey);
            _append_ascension_validation_errors(errors, ascensionId, _ascension_defs[ascensionId]);
        }
        foreach (var stageKey in _sorted_registry_keys(_ascension_stage_defs.Keys))
        {
            var stageId = new StringName(stageKey);
            _append_ascension_stage_validation_errors(errors, stageId, _ascension_stage_defs[stageId]);
        }
        return errors;
    }

    private void _append_ascension_validation_errors(
        ICollection<string> errors,
        StringName ascensionId,
        AscensionDefinition ascensionDef
    )
    {
        var ownerLabel = $"Ascension {ascensionId}";
        _append_string_name_field_error(
            errors,
            ownerLabel,
            "ascension_id",
            ascensionDef.AscensionId
        );
        _append_required_string_field_error(
            errors,
            ownerLabel,
            "display_name",
            ascensionDef.DisplayName
        );
        _append_required_string_field_error(
            errors,
            ownerLabel,
            "description",
            ascensionDef.Description
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            ascensionDef.StageIds,
            "stage_ids"
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            ascensionDef.TraitIds,
            "trait_ids"
        );
        _append_racial_granted_skill_array_errors(
            errors,
            ownerLabel,
            ascensionDef.RacialGrantedSkills,
            "racial_granted_skills"
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            ascensionDef.AllowedRaceIds,
            "allowed_race_ids"
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            ascensionDef.AllowedSubraceIds,
            "allowed_subrace_ids"
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            ascensionDef.AllowedBloodlineIds,
            "allowed_bloodline_ids"
        );
        _append_string_array_errors(
            errors,
            ownerLabel,
            ascensionDef.TraitSummary,
            "trait_summary"
        );
    }

    private void _append_ascension_stage_validation_errors(
        ICollection<string> errors,
        StringName stageId,
        AscensionStageDefinition stageDef
    )
    {
        var ownerLabel = $"AscensionStage {stageId}";
        _append_string_name_field_error(errors, ownerLabel, "stage_id", stageDef.StageId);
        _append_string_name_field_error(errors, ownerLabel, "ascension_id", stageDef.AscensionId);
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
        _append_string_name_field_error(
            errors,
            ownerLabel,
            "body_size_category_override",
            stageDef.BodySizeCategoryOverride,
            true
        );
        _append_string_array_errors(errors, ownerLabel, stageDef.TraitSummary, "trait_summary");
    }

}
