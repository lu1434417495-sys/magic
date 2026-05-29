using Godot;

[GlobalClass]
public partial class StageAdvancementContentRegistry : IdentityContentRegistryBase
{
    public const string STAGE_ADVANCEMENT_CONFIG_DIRECTORY =
        "res://data/configs/stage_advancements";

    private static readonly StringName StageTargetAxisBloodline = "bloodline";
    private static readonly StringName StageTargetAxisDivine = "divine";

    private static readonly Godot.Collections.Dictionary ValidStageTargetAxes = new()
    {
        { new StringName("full"), true },
        { new StringName("physical"), true },
        { new StringName("mental"), true },
        { StageTargetAxisBloodline, true },
        { StageTargetAxisDivine, true },
        { new StringName("martial"), true },
        { new StringName("domain"), true },
    };

    private System.Collections.Generic.Dictionary<StringName, StageAdvancementModifier> _stage_advancement_defs = new();

    public StageAdvancementContentRegistry()
    {
        _registry_label = "StageAdvancementContentRegistry";
        rebuild();
    }

    public static string stage_advancement_config_directory() => STAGE_ADVANCEMENT_CONFIG_DIRECTORY;

    public void rebuild() => load_from_directory(STAGE_ADVANCEMENT_CONFIG_DIRECTORY);

    public void load_from_directory(string directoryPath)
    {
        _stage_advancement_defs.Clear();
        _validation_errors.Clear();
        _scan_directory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public Godot.Collections.Dictionary get_stage_advancement_defs()
    {
        var result = new Godot.Collections.Dictionary();
        foreach (var kvp in _stage_advancement_defs)
            result[kvp.Key] = kvp.Value;
        return result;
    }

    protected override void _register_resource(string resourcePath)
    {
        var resource = GodotContentResourceLifetime.Keep(GD.Load<Resource>(resourcePath));
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load stage advancement config {resourcePath}.");
            return;
        }
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
        if (!ValidStageTargetAxes.ContainsKey(modifier.target_axis))
            errors.Add($"{ownerLabel} uses unsupported target_axis {modifier.target_axis}.");
        _append_int_field_error(errors, ownerLabel, "stage_offset", modifier.stage_offset);
        _append_string_name_field_error(
            errors,
            ownerLabel,
            "max_stage_id",
            modifier.max_stage_id,
            true
        );

        var axisValue = modifier.target_axis;
        if (axisValue == StageTargetAxisBloodline || axisValue == StageTargetAxisDivine)
        {
            if (modifier.max_stage_id == "")
                errors.Add(
                    $"{ownerLabel}.max_stage_id must be non-empty for target_axis {axisValue}."
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
