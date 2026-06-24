using Godot;

public class StageAdvancementContentRegistry : IdentityContentRegistryBase
{
    private const string StageAdvancementConfigDirectoryPath =
        "res://data/configs/stage_advancements";

    private System.Collections.Generic.Dictionary<StringName, StageAdvancementModifier> _stage_advancement_defs = new();

    public StageAdvancementContentRegistry()
    {
        _registry_label = "StageAdvancementContentRegistry";
        Rebuild();
    }

    public void Rebuild() => LoadFromDirectory(StageAdvancementConfigDirectoryPath);

    public void LoadFromDirectory(string directoryPath)
    {
        LoadFromDirectories(new Godot.Collections.Array<string> { directoryPath });
    }

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _stage_advancement_defs.Clear();
        _validation_errors.Clear();
        foreach (var directoryPath in directoryPaths)
            _scan_directory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public System.Collections.Generic.IReadOnlyDictionary<StringName, StageAdvancementModifier> GetStageAdvancementDefsTyped()
    {
        return new System.Collections.Generic.Dictionary<StringName, StageAdvancementModifier>(
            _stage_advancement_defs
        );
    }

    protected override void ClearRegistryData()
    {
        _stage_advancement_defs.Clear();
    }

    protected override void _register_resource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load stage advancement config {resourcePath}.");
            return;
        }
        GodotContentOwnership.RegisterBorrowedContent(resource, resourcePath);
        if (resource is not StageAdvancementModifier modifier)
        {
            _validation_errors.Add(
                $"Stage advancement config {resourcePath} is not a StageAdvancementModifier."
            );
            return;
        }
        if (modifier.modifier_id == "")
        {
            _validation_errors.Add(
                $"Stage advancement config {resourcePath} is missing modifier_id."
            );
            return;
        }
        if (_stage_advancement_defs.ContainsKey(modifier.modifier_id))
        {
            _validation_errors.Add(
                $"Duplicate stage advancement modifier_id registered: {modifier.modifier_id}"
            );
            return;
        }

        _stage_advancement_defs[modifier.modifier_id] = modifier;
    }

    private Godot.Collections.Array<string> _collect_validation_errors()
    {
        var errors = new Godot.Collections.Array<string>();
        foreach (var modifierKey in _sorted_registry_keys(_stage_advancement_defs.Keys))
        {
            var modifierId = new StringName(modifierKey);
            _append_stage_advancement_validation_errors(errors, modifierId, _stage_advancement_defs[modifierId]);
        }
        return errors;
    }

    private void _append_stage_advancement_validation_errors(
        Godot.Collections.Array<string> errors,
        StringName modifierId,
        StageAdvancementModifier modifier
    )
    {
        var ownerLabel = $"StageAdvancement {modifierId}";
        _append_string_name_field_error(errors, ownerLabel, "modifier_id", modifier.modifier_id);
        _append_string_field_error(errors, ownerLabel, "display_name", modifier.display_name);
        _append_string_name_field_error(errors, ownerLabel, "target_axis", modifier.target_axis);
        StageAdvancementTargetAxis axisKind = modifier.TargetAxisKind;
        if (axisKind == StageAdvancementTargetAxis.Unknown)
            errors.Add($"{ownerLabel} uses unsupported target_axis {modifier.target_axis}.");
        _append_int_field_error(errors, ownerLabel, "stage_offset", modifier.stage_offset);
        _append_string_name_field_error(
            errors,
            ownerLabel,
            "max_stage_id",
            modifier.max_stage_id,
            true
        );

        if (
            axisKind == StageAdvancementTargetAxis.Bloodline
            || axisKind == StageAdvancementTargetAxis.Divine
        )
        {
            if (modifier.max_stage_id == "")
                errors.Add(
                    $"{ownerLabel}.max_stage_id must be non-empty for target_axis {modifier.target_axis}."
                );
        }
        else if (modifier.stage_offset <= 0)
        {
            errors.Add(
                $"{ownerLabel}.stage_offset must be > 0 for age-stage axis (current value yields no advancement)."
            );
        }

        _append_string_name_array_errors(
            errors,
            ownerLabel,
            V(modifier.applies_to_race_ids),
            "applies_to_race_ids"
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            V(modifier.applies_to_subrace_ids),
            "applies_to_subrace_ids"
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            V(modifier.applies_to_bloodline_ids),
            "applies_to_bloodline_ids"
        );
        _append_string_name_array_errors(
            errors,
            ownerLabel,
            V(modifier.applies_to_ascension_ids),
            "applies_to_ascension_ids"
        );
        _append_bool_field_error(
            errors,
            ownerLabel,
            "grants_attributes",
            modifier.grants_attributes
        );
        _append_bool_field_error(errors, ownerLabel, "grants_traits", modifier.grants_traits);
        _append_bool_field_error(
            errors,
            ownerLabel,
            "grants_body_size_change",
            modifier.grants_body_size_change
        );
    }

    private static Godot.Collections.Array V<[MustBeVariant] T>(Godot.Collections.Array<T> values)
    {
        var result = new Godot.Collections.Array();
        if (values == null)
            return result;
        foreach (T value in values)
            result.Add(Variant.From(value));
        return result;
    }
}
