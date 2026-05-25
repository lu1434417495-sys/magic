using Godot;

[GlobalClass]
public partial class BloodlineContentRegistry : IdentityContentRegistryBase
{
    public const string BLOODLINE_CONFIG_DIRECTORY = "res://data/configs/bloodlines";

    private Godot.Collections.Dictionary _bloodline_defs = new();
    private Godot.Collections.Dictionary _bloodline_stage_defs = new();

    public BloodlineContentRegistry()
    {
        _registry_label = "BloodlineContentRegistry";
        rebuild();
    }

    public static string bloodline_config_directory() => BLOODLINE_CONFIG_DIRECTORY;

    public void rebuild() => load_from_directory(BLOODLINE_CONFIG_DIRECTORY);

    public void load_from_directory(string directoryPath)
    {
        _bloodline_defs.Clear();
        _bloodline_stage_defs.Clear();
        _validation_errors.Clear();
        _scan_directory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public Godot.Collections.Dictionary get_bloodline_defs() => _bloodline_defs.Duplicate();
    public Godot.Collections.Dictionary get_bloodline_stage_defs() => _bloodline_stage_defs.Duplicate();

    protected override void _register_resource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
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
        _validation_errors.Add($"Bloodline config {resourcePath} is not a BloodlineDef or BloodlineStageDef.");
    }

    private void _register_bloodline(string resourcePath, BloodlineDef bloodlineDef)
    {
        if (bloodlineDef == null)
        {
            _validation_errors.Add($"Bloodline config {resourcePath} failed to cast to BloodlineDef.");
            return;
        }
        if (bloodlineDef.bloodline_id == "")
        {
            _validation_errors.Add($"Bloodline config {resourcePath} is missing bloodline_id.");
            return;
        }
        if (_bloodline_defs.ContainsKey(bloodlineDef.bloodline_id))
        {
            _validation_errors.Add($"Duplicate bloodline_id registered: {bloodlineDef.bloodline_id}");
            return;
        }
        _bloodline_defs[bloodlineDef.bloodline_id] = bloodlineDef;
    }

    private void _register_bloodline_stage(string resourcePath, BloodlineStageDef stageDef)
    {
        if (stageDef == null)
        {
            _validation_errors.Add($"Bloodline stage config {resourcePath} failed to cast to BloodlineStageDef.");
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
        _bloodline_stage_defs[stageDef.stage_id] = stageDef;
    }

    private Godot.Collections.Array<string> _collect_validation_errors()
    {
        var errors = new Godot.Collections.Array<string>();
        foreach (var bloodlineKey in _sorted_registry_keys(_bloodline_defs))
        {
            var bloodlineId = new StringName(bloodlineKey);
            if (_bloodline_defs[bloodlineId].AsGodotObject() is BloodlineDef bloodlineDef)
                _append_bloodline_validation_errors(errors, bloodlineId, bloodlineDef);
        }
        foreach (var stageKey in _sorted_registry_keys(_bloodline_stage_defs))
        {
            var stageId = new StringName(stageKey);
            if (_bloodline_stage_defs[stageId].AsGodotObject() is BloodlineStageDef stageDef)
                _append_bloodline_stage_validation_errors(errors, stageId, stageDef);
        }
        return errors;
    }

    private void _append_bloodline_validation_errors(
        Godot.Collections.Array<string> errors,
        StringName bloodlineId,
        BloodlineDef bloodlineDef
    )
    {
        var ownerLabel = $"Bloodline {bloodlineId}";
        _append_string_name_field_error(errors, ownerLabel, "bloodline_id", bloodlineDef.bloodline_id);
        _append_string_field_error(errors, ownerLabel, "display_name", bloodlineDef.display_name);
        _append_string_field_error(errors, ownerLabel, "description", bloodlineDef.description);
        _append_string_name_array_errors(errors, ownerLabel, V(bloodlineDef.stage_ids), "stage_ids");
        _append_string_name_array_errors(errors, ownerLabel, V(bloodlineDef.trait_ids), "trait_ids");
        _append_racial_granted_skill_array_errors(errors, ownerLabel, V(bloodlineDef.racial_granted_skills), "racial_granted_skills");
        _append_attribute_modifier_array_errors(errors, ownerLabel, V(bloodlineDef.attribute_modifiers), "attribute_modifiers");
        _append_string_array_errors(errors, ownerLabel, V(bloodlineDef.trait_summary), "trait_summary");
    }

    private void _append_bloodline_stage_validation_errors(
        Godot.Collections.Array<string> errors,
        StringName stageId,
        BloodlineStageDef stageDef
    )
    {
        var ownerLabel = $"BloodlineStage {stageId}";
        _append_string_name_field_error(errors, ownerLabel, "stage_id", stageDef.stage_id);
        _append_string_name_field_error(errors, ownerLabel, "bloodline_id", stageDef.bloodline_id);
        _append_string_field_error(errors, ownerLabel, "display_name", stageDef.display_name);
        _append_string_field_error(errors, ownerLabel, "description", stageDef.description);
        _append_attribute_modifier_array_errors(errors, ownerLabel, V(stageDef.attribute_modifiers), "attribute_modifiers");
        _append_string_name_array_errors(errors, ownerLabel, V(stageDef.trait_ids), "trait_ids");
        _append_racial_granted_skill_array_errors(errors, ownerLabel, V(stageDef.racial_granted_skills), "racial_granted_skills");
        _append_string_array_errors(errors, ownerLabel, V(stageDef.trait_summary), "trait_summary");
    }

    private static Godot.Collections.Array V(Variant v) => v.AsGodotArray();
}
