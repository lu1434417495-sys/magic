using System.Collections.Generic;
using System.IO;
using Godot;

public class BloodlineContentRegistry : IdentityContentRegistryBase
{
    private const string BloodlineConfigDirectoryPath = "res://data/configs/bloodlines";

    private readonly Dictionary<StringName, BloodlineDefinition> _bloodline_defs = new();
    private readonly Dictionary<StringName, BloodlineStageDefinition> _bloodline_stage_defs = new();

    internal BloodlineContentRegistry(IContentResourceLoader resourceLoader)
        : this(resourceLoader, loadDefaultContent: true) { }

    internal BloodlineContentRegistry(
        IContentResourceLoader resourceLoader,
        bool loadDefaultContent
    )
        : base(resourceLoader)
    {
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
            _scan_directory(directoryPath);
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

    protected override void _register_resource(string resourcePath)
    {
        Resource resource = _resourceLoader.LoadCanonical<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load bloodline config {resourcePath}.");
            return;
        }
        if (resource is BloodlineDef bloodlineDef)
        {
            _register_bloodline(resourcePath, bloodlineDef);
            return;
        }
        if (resource is BloodlineStageDef stageDef)
        {
            _register_bloodline_stage(resourcePath, stageDef);
            return;
        }
        _validation_errors.Add(
            $"Bloodline config {resourcePath} is not a BloodlineDef or BloodlineStageDef."
        );
    }

    private void _register_bloodline(string resourcePath, BloodlineDef bloodlineDef)
    {
        if (bloodlineDef == null)
        {
            _validation_errors.Add(
                $"Bloodline config {resourcePath} failed to cast to BloodlineDef."
            );
            return;
        }
        if (bloodlineDef.bloodline_id == "")
        {
            _validation_errors.Add($"Bloodline config {resourcePath} is missing bloodline_id.");
            return;
        }
        if (_bloodline_defs.ContainsKey(bloodlineDef.bloodline_id))
        {
            _validation_errors.Add(
                $"Duplicate bloodline_id registered: {bloodlineDef.bloodline_id}"
            );
            return;
        }
        try
        {
            BloodlineDefinition definition = BloodlineDefinition.FromResource(
                bloodlineDef,
                $"bloodline.{bloodlineDef.bloodline_id}"
            );
            _bloodline_defs.Add(definition.BloodlineId, definition);
        }
        catch (InvalidDataException exception)
        {
            _validation_errors.Add(
                $"Bloodline config {resourcePath} projection failed: {exception.Message}"
            );
        }
    }

    private void _register_bloodline_stage(string resourcePath, BloodlineStageDef stageDef)
    {
        if (stageDef == null)
        {
            _validation_errors.Add(
                $"Bloodline stage config {resourcePath} failed to cast to BloodlineStageDef."
            );
            return;
        }
        if (stageDef.stage_id == "")
        {
            _validation_errors.Add($"Bloodline stage config {resourcePath} is missing stage_id.");
            return;
        }
        if (_bloodline_stage_defs.ContainsKey(stageDef.stage_id))
        {
            _validation_errors.Add($"Duplicate bloodline stage_id registered: {stageDef.stage_id}");
            return;
        }
        try
        {
            BloodlineStageDefinition definition = BloodlineStageDefinition.FromResource(
                stageDef,
                $"bloodline_stage.{stageDef.stage_id}"
            );
            _bloodline_stage_defs.Add(definition.StageId, definition);
        }
        catch (InvalidDataException exception)
        {
            _validation_errors.Add(
                $"Bloodline stage config {resourcePath} projection failed: {exception.Message}"
            );
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
        _append_string_field_error(errors, ownerLabel, "display_name", bloodlineDef.DisplayName);
        _append_string_field_error(errors, ownerLabel, "description", bloodlineDef.Description);
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
        _append_string_array_errors(errors, ownerLabel, stageDef.TraitSummary, "trait_summary");
    }

}
