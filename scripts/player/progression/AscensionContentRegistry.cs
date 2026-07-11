using System.Collections.Generic;
using System.IO;
using Godot;

public class AscensionContentRegistry : IdentityContentRegistryBase
{
    private const string AscensionConfigDirectoryPath = "res://data/configs/ascensions";

    private readonly Dictionary<StringName, AscensionDefinition> _ascension_defs = new();
    private readonly Dictionary<StringName, AscensionStageDefinition> _ascension_stage_defs = new();

    public AscensionContentRegistry()
    {
        _registry_label = "AscensionContentRegistry";
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
            _scan_directory(directoryPath);
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

    protected override void _register_resource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load ascension config {resourcePath}.");
            return;
        }
        GodotContentOwnership.RegisterBorrowedContent(resource, resourcePath);
        if (resource is AscensionDef ascensionDef)
        {
            _register_ascension(resourcePath, ascensionDef);
            return;
        }
        if (resource is AscensionStageDef stageDef)
        {
            _register_ascension_stage(resourcePath, stageDef);
            return;
        }
        _validation_errors.Add(
            $"Ascension config {resourcePath} is not an AscensionDef or AscensionStageDef."
        );
    }

    private void _register_ascension(string resourcePath, AscensionDef ascensionDef)
    {
        if (ascensionDef == null)
        {
            _validation_errors.Add(
                $"Ascension config {resourcePath} failed to cast to AscensionDef."
            );
            return;
        }
        if (ascensionDef.ascension_id == "")
        {
            _validation_errors.Add($"Ascension config {resourcePath} is missing ascension_id.");
            return;
        }
        if (_ascension_defs.ContainsKey(ascensionDef.ascension_id))
        {
            _validation_errors.Add(
                $"Duplicate ascension_id registered: {ascensionDef.ascension_id}"
            );
            return;
        }
        try
        {
            AscensionDefinition definition = AscensionDefinition.FromResource(
                ascensionDef,
                $"ascension.{ascensionDef.ascension_id}"
            );
            _ascension_defs.Add(definition.AscensionId, definition);
        }
        catch (InvalidDataException exception)
        {
            _validation_errors.Add(
                $"Ascension config {resourcePath} projection failed: {exception.Message}"
            );
        }
    }

    private void _register_ascension_stage(string resourcePath, AscensionStageDef stageDef)
    {
        if (stageDef == null)
        {
            _validation_errors.Add(
                $"Ascension stage config {resourcePath} failed to cast to AscensionStageDef."
            );
            return;
        }
        if (stageDef.stage_id == "")
        {
            _validation_errors.Add($"Ascension stage config {resourcePath} is missing stage_id.");
            return;
        }
        if (_ascension_stage_defs.ContainsKey(stageDef.stage_id))
        {
            _validation_errors.Add($"Duplicate ascension stage_id registered: {stageDef.stage_id}");
            return;
        }
        try
        {
            AscensionStageDefinition definition = AscensionStageDefinition.FromResource(
                stageDef,
                $"ascension_stage.{stageDef.stage_id}"
            );
            _ascension_stage_defs.Add(definition.StageId, definition);
        }
        catch (InvalidDataException exception)
        {
            _validation_errors.Add(
                $"Ascension stage config {resourcePath} projection failed: {exception.Message}"
            );
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
        _append_string_field_error(errors, ownerLabel, "display_name", ascensionDef.DisplayName);
        _append_string_field_error(errors, ownerLabel, "description", ascensionDef.Description);
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
        _append_bool_field_error(
            errors,
            ownerLabel,
            "replaces_age_growth",
            ascensionDef.ReplacesAgeGrowth
        );
        _append_bool_field_error(
            errors,
            ownerLabel,
            "suppresses_original_race_traits",
            ascensionDef.SuppressesOriginalRaceTraits
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
        _append_string_field_error(errors, ownerLabel, "display_name", stageDef.DisplayName);
        _append_string_field_error(errors, ownerLabel, "description", stageDef.Description);
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
