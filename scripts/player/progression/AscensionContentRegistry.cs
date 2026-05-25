using Godot;

[GlobalClass]
public partial class AscensionContentRegistry : IdentityContentRegistryBase
{
    public const string ASCENSION_CONFIG_DIRECTORY = "res://data/configs/ascensions";

    private Godot.Collections.Dictionary _ascension_defs = new();
    private Godot.Collections.Dictionary _ascension_stage_defs = new();

    public AscensionContentRegistry()
    {
        _registry_label = "AscensionContentRegistry";
        rebuild();
    }

    public static string ascension_config_directory() => ASCENSION_CONFIG_DIRECTORY;

    public void rebuild() => load_from_directory(ASCENSION_CONFIG_DIRECTORY);

    public void load_from_directory(string directoryPath)
    {
        _ascension_defs.Clear();
        _ascension_stage_defs.Clear();
        _validation_errors.Clear();
        _scan_directory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public Godot.Collections.Dictionary get_ascension_defs() => _ascension_defs.Duplicate();
    public Godot.Collections.Dictionary get_ascension_stage_defs() => _ascension_stage_defs.Duplicate();

    protected override void _register_resource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load ascension config {resourcePath}.");
            return;
        }
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
        _validation_errors.Add($"Ascension config {resourcePath} is not an AscensionDef or AscensionStageDef.");
    }

    private void _register_ascension(string resourcePath, AscensionDef ascensionDef)
    {
        if (ascensionDef == null)
        {
            _validation_errors.Add($"Ascension config {resourcePath} failed to cast to AscensionDef.");
            return;
        }
        if (ascensionDef.ascension_id == "")
        {
            _validation_errors.Add($"Ascension config {resourcePath} is missing ascension_id.");
            return;
        }
        if (_ascension_defs.ContainsKey(ascensionDef.ascension_id))
        {
            _validation_errors.Add($"Duplicate ascension_id registered: {ascensionDef.ascension_id}");
            return;
        }
        _ascension_defs[ascensionDef.ascension_id] = ascensionDef;
    }

    private void _register_ascension_stage(string resourcePath, AscensionStageDef stageDef)
    {
        if (stageDef == null)
        {
            _validation_errors.Add($"Ascension stage config {resourcePath} failed to cast to AscensionStageDef.");
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
        _ascension_stage_defs[stageDef.stage_id] = stageDef;
    }

    private Godot.Collections.Array<string> _collect_validation_errors()
    {
        var errors = new Godot.Collections.Array<string>();
        foreach (var ascensionKey in _sorted_registry_keys(_ascension_defs))
        {
            var ascensionId = new StringName(ascensionKey);
            if (_ascension_defs[ascensionId].AsGodotObject() is AscensionDef ascensionDef)
                _append_ascension_validation_errors(errors, ascensionId, ascensionDef);
        }
        foreach (var stageKey in _sorted_registry_keys(_ascension_stage_defs))
        {
            var stageId = new StringName(stageKey);
            if (_ascension_stage_defs[stageId].AsGodotObject() is AscensionStageDef stageDef)
                _append_ascension_stage_validation_errors(errors, stageId, stageDef);
        }
        return errors;
    }

    private void _append_ascension_validation_errors(
        Godot.Collections.Array<string> errors,
        StringName ascensionId,
        AscensionDef ascensionDef
    )
    {
        var ownerLabel = $"Ascension {ascensionId}";
        _append_string_name_field_error(errors, ownerLabel, "ascension_id", ascensionDef.ascension_id);
        _append_string_field_error(errors, ownerLabel, "display_name", ascensionDef.display_name);
        _append_string_field_error(errors, ownerLabel, "description", ascensionDef.description);
        _append_string_name_array_errors(errors, ownerLabel, V(ascensionDef.stage_ids), "stage_ids");
        _append_string_name_array_errors(errors, ownerLabel, V(ascensionDef.trait_ids), "trait_ids");
        _append_racial_granted_skill_array_errors(errors, ownerLabel, V(ascensionDef.racial_granted_skills), "racial_granted_skills");
        _append_string_name_array_errors(errors, ownerLabel, V(ascensionDef.allowed_race_ids), "allowed_race_ids");
        _append_string_name_array_errors(errors, ownerLabel, V(ascensionDef.allowed_subrace_ids), "allowed_subrace_ids");
        _append_string_name_array_errors(errors, ownerLabel, V(ascensionDef.allowed_bloodline_ids), "allowed_bloodline_ids");
        _append_string_array_errors(errors, ownerLabel, V(ascensionDef.trait_summary), "trait_summary");
        _append_bool_field_error(errors, ownerLabel, "replaces_age_growth", ascensionDef.replaces_age_growth);
        _append_bool_field_error(errors, ownerLabel, "suppresses_original_race_traits", ascensionDef.suppresses_original_race_traits);
    }

    private void _append_ascension_stage_validation_errors(
        Godot.Collections.Array<string> errors,
        StringName stageId,
        AscensionStageDef stageDef
    )
    {
        var ownerLabel = $"AscensionStage {stageId}";
        _append_string_name_field_error(errors, ownerLabel, "stage_id", stageDef.stage_id);
        _append_string_name_field_error(errors, ownerLabel, "ascension_id", stageDef.ascension_id);
        _append_string_field_error(errors, ownerLabel, "display_name", stageDef.display_name);
        _append_string_field_error(errors, ownerLabel, "description", stageDef.description);
        _append_attribute_modifier_array_errors(errors, ownerLabel, V(stageDef.attribute_modifiers), "attribute_modifiers");
        _append_string_name_array_errors(errors, ownerLabel, V(stageDef.trait_ids), "trait_ids");
        _append_racial_granted_skill_array_errors(errors, ownerLabel, V(stageDef.racial_granted_skills), "racial_granted_skills");
        _append_string_name_field_error(errors, ownerLabel, "body_size_category_override", stageDef.body_size_category_override, true);
        _append_string_array_errors(errors, ownerLabel, V(stageDef.trait_summary), "trait_summary");
    }

    private static Godot.Collections.Array V(Variant v) => v.AsGodotArray();
}
