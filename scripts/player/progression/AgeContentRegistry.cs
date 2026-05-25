using Godot;

[GlobalClass]
public partial class AgeContentRegistry : IdentityContentRegistryBase
{
    public const string AGE_PROFILE_CONFIG_DIRECTORY = "res://data/configs/age_profiles";

    private Godot.Collections.Dictionary _age_profile_defs = new();

    public AgeContentRegistry()
    {
        _registry_label = "AgeContentRegistry";
        rebuild();
    }

    public static string age_profile_config_directory() => AGE_PROFILE_CONFIG_DIRECTORY;

    public void rebuild() => load_from_directory(AGE_PROFILE_CONFIG_DIRECTORY);

    public void load_from_directory(string directoryPath)
    {
        _age_profile_defs.Clear();
        _validation_errors.Clear();
        _scan_directory(directoryPath);
        foreach (var e in _collect_validation_errors())
            _validation_errors.Add(e);
    }

    public Godot.Collections.Dictionary get_age_profile_defs() => _age_profile_defs.Duplicate();

    protected override void _register_resource(string resourcePath)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load age profile config {resourcePath}.");
            return;
        }
        if (resource is not AgeProfileDef profileDef)
        {
            _validation_errors.Add($"Age profile config {resourcePath} is not an AgeProfileDef.");
            return;
        }
        if (profileDef.profile_id == "")
        {
            _validation_errors.Add($"Age profile config {resourcePath} is missing profile_id.");
            return;
        }
        if (_age_profile_defs.ContainsKey(profileDef.profile_id))
        {
            _validation_errors.Add($"Duplicate age profile_id registered: {profileDef.profile_id}");
            return;
        }

        _age_profile_defs[profileDef.profile_id] = profileDef;
    }

    private Godot.Collections.Array<string> _collect_validation_errors()
    {
        var errors = new Godot.Collections.Array<string>();
        foreach (var profileKey in _sorted_registry_keys(_age_profile_defs))
        {
            var profileId = new StringName(profileKey);
            if (!_age_profile_defs.ContainsKey(profileId))
                continue;
            if (_age_profile_defs[profileId].AsGodotObject() is not AgeProfileDef profileDef)
                continue;
            _append_age_profile_validation_errors(errors, profileId, profileDef);
        }
        return errors;
    }

    private void _append_age_profile_validation_errors(
        Godot.Collections.Array<string> errors,
        StringName profileId,
        AgeProfileDef profileDef
    )
    {
        var ownerLabel = $"AgeProfile {profileId}";
        _append_string_name_field_error(errors, ownerLabel, "profile_id", profileDef.profile_id);
        _append_string_name_field_error(errors, ownerLabel, "race_id", profileDef.race_id);

        var ageFields = new string[]
        {
            "child_age",
            "teen_age",
            "young_adult_age",
            "adult_age",
            "middle_age",
            "old_age",
            "venerable_age",
            "max_natural_age",
        };
        var previousValue = -1;
        var previousField = "";
        foreach (var fieldLabel in ageFields)
        {
            var value = profileDef.Get(fieldLabel);
            _append_int_field_error(errors, ownerLabel, fieldLabel, value);
            if (value.VariantType != Variant.Type.Int)
                continue;
            var intValue = value.AsInt32();
            if (intValue < 0)
                errors.Add($"{ownerLabel}.{fieldLabel} must be >= 0.");
            else if (previousValue >= 0 && intValue < previousValue)
                errors.Add($"{ownerLabel}.{fieldLabel} ({intValue}) must be >= {previousField} ({previousValue}).");
            previousValue = intValue;
            previousField = fieldLabel;
        }

        var maxNaturalAgeValue = profileDef.Get("max_natural_age");
        var maxNaturalAgeInt = maxNaturalAgeValue.VariantType == Variant.Type.Int ? maxNaturalAgeValue.AsInt32() : -1;
        _append_age_stage_rule_errors(errors, ownerLabel, V(profileDef.stage_rules), "stage_rules");

        var selectableStageIds = new Godot.Collections.Dictionary();
        foreach (var stageRuleVariant in V(profileDef.stage_rules))
        {
            if (stageRuleVariant.AsGodotObject() is AgeStageRule stageRule
                && stageRule.stage_id != ""
                && stageRule.selectable_in_creation)
                selectableStageIds[stageRule.stage_id] = true;
        }

        _append_string_name_array_errors(errors, ownerLabel, V(profileDef.creation_stage_ids), "creation_stage_ids");
        foreach (var creationStageIdVariant in V(profileDef.creation_stage_ids))
        {
            var stageIdName = ProgressionDataUtils.to_string_name(creationStageIdVariant);
            if (stageIdName != "" && !selectableStageIds.ContainsKey(stageIdName))
                errors.Add($"{ownerLabel}.creation_stage_ids references stage {stageIdName} that is not selectable_in_creation.");
        }

        _append_string_name_to_int_dictionary_errors(errors, ownerLabel, profileDef.default_age_by_stage, "default_age_by_stage");
        foreach (var stageKey in profileDef.default_age_by_stage.Keys)
        {
            var defaultAge = profileDef.default_age_by_stage[stageKey];
            if (defaultAge.VariantType != Variant.Type.Int)
                continue;
            var defaultAgeInt = defaultAge.AsInt32();
            if (defaultAgeInt < 0)
                errors.Add($"{ownerLabel}.default_age_by_stage[{stageKey.AsString()}] must be >= 0.");
            else if (maxNaturalAgeInt > 0 && defaultAgeInt > maxNaturalAgeInt)
                errors.Add($"{ownerLabel}.default_age_by_stage[{stageKey.AsString()}] ({defaultAgeInt}) exceeds max_natural_age ({maxNaturalAgeInt}).");
        }
    }

    private void _append_age_stage_rule_errors(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        Godot.Collections.Array stageRules,
        string fieldLabel
    )
    {
        var seenStageIds = new Godot.Collections.Dictionary();
        for (var index = 0; index < stageRules.Count; index++)
        {
            var stageRule = stageRules[index].AsGodotObject() as AgeStageRule;
            var stageLabel = $"{ownerLabel}.{fieldLabel}[{index}]";
            if (stageRule == null)
            {
                errors.Add($"{stageLabel} must be an AgeStageRule.");
                continue;
            }

            _append_string_name_field_error(errors, stageLabel, "stage_id", stageRule.stage_id);
            if (stageRule.stage_id != "")
            {
                if (seenStageIds.ContainsKey(stageRule.stage_id))
                    errors.Add($"{ownerLabel} declares duplicate stage_id {stageRule.stage_id}.");
                else
                    seenStageIds[stageRule.stage_id] = true;
            }
            _append_string_field_error(errors, stageLabel, "display_name", stageRule.display_name);
            _append_string_field_error(errors, stageLabel, "description", stageRule.description);
            _append_attribute_modifier_array_errors(errors, stageLabel, V(stageRule.attribute_modifiers), "attribute_modifiers");
            _append_string_name_array_errors(errors, stageLabel, V(stageRule.trait_ids), "trait_ids");
            if (stageRule.trait_ids.Count > 0)
                errors.Add($"{stageLabel}.trait_ids is not yet supported by runtime passive projection; remove or implement age stage trait projection first.");
            _append_string_array_errors(errors, stageLabel, V(stageRule.trait_summary), "trait_summary");
            _append_bool_field_error(errors, stageLabel, "selectable_in_creation", stageRule.selectable_in_creation);
            _append_bool_field_error(errors, stageLabel, "reachable_by_aging", stageRule.reachable_by_aging);
        }
    }

    private static Godot.Collections.Array V(Variant v) => v.AsGodotArray();
}
