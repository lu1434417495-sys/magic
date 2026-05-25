using Godot;

[GlobalClass]
public partial class IdentityContentRegistryBase : RefCounted
{
    private static readonly Script AttributeModifierScript = GD.Load<Script>("res://scripts/player/progression/attribute_modifier.gd");
    private static readonly Script RacialGrantedSkillScript = GD.Load<Script>("res://scripts/player/progression/RacialGrantedSkill.cs");

    private static readonly Godot.Collections.Array<StringName> ResourceAttributeIds = new()
    {
        "hp_max",
        "character_hp_max_percent_bonus",
        "mp_max",
        "stamina_max",
        "stamina_recovery_percent_bonus",
        "aura_max",
        "action_points",
        "action_threshold",
    };

    private static readonly Godot.Collections.Array<StringName> CombatAttributeIds = new()
    {
        "armor_class",
        "armor_ac_bonus",
        "shield_ac_bonus",
        "dodge_bonus",
        "deflection_bonus",
        "armor_max_dex_bonus",
    };

    private static Godot.Collections.Dictionary _allowed_attribute_id_cache = new();

    private static Godot.Collections.Dictionary _allowed_attribute_id_set()
    {
        if (_allowed_attribute_id_cache.Count > 0)
            return _allowed_attribute_id_cache;
        var allowed = new Godot.Collections.Dictionary();
        foreach (var attributeId in UnitBaseAttributes.BASE_ATTRIBUTE_IDS())
            allowed[attributeId] = true;
        foreach (var attributeId in ResourceAttributeIds)
            allowed[attributeId] = true;
        foreach (var attributeId in CombatAttributeIds)
            allowed[attributeId] = true;
        _allowed_attribute_id_cache = allowed;
        return allowed;
    }

    protected string _registry_label = "IdentityContentRegistry";
    protected Godot.Collections.Array<string> _validation_errors = new();

    public Godot.Collections.Array<string> validate()
    {
        var copy = new Godot.Collections.Array<string>();
        foreach (var e in _validation_errors) copy.Add(e);
        return copy;
    }

    protected void _scan_directory(string directoryPath)
    {
        var globalizedPath = ProjectSettings.GlobalizePath(directoryPath);
        if (!DirAccess.DirExistsAbsolute(globalizedPath))
        {
            _validation_errors.Add($"{_registry_label} could not find {directoryPath}.");
            return;
        }
        var directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validation_errors.Add($"{_registry_label} could not open {directoryPath}.");
            return;
        }
        directory.ListDirBegin();
        while (true)
        {
            string entryName = directory.GetNext();
            if (string.IsNullOrEmpty(entryName))
                break;
            if (entryName == "." || entryName == "..")
                continue;
            string entryPath = $"{directoryPath}/{entryName}";
            if (directory.CurrentIsDir())
            {
                _scan_directory(entryPath);
                continue;
            }
            if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                continue;
            _register_resource(entryPath);
        }
        directory.ListDirEnd();
    }

    protected virtual void _register_resource(string resourcePath)
    {
        _validation_errors.Add($"{_registry_label} does not implement resource registration for {resourcePath}.");
    }

    protected Godot.Collections.Array<string> _sorted_registry_keys(Godot.Collections.Dictionary registry)
    {
        return ProgressionDataUtils.sorted_string_keys(registry);
    }

    protected void _append_string_name_field_error(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        string fieldLabel,
        Variant value,
        bool allowEmpty = false
    )
    {
        if (value.VariantType != Variant.Type.StringName)
        {
            errors.Add($"{ownerLabel}.{fieldLabel} must be a StringName.");
            return;
        }
        if (!allowEmpty && value.AsStringName() == "")
            errors.Add($"{ownerLabel}.{fieldLabel} must be a non-empty StringName.");
    }

    protected void _append_string_field_error(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        string fieldLabel,
        Variant value
    )
    {
        if (value.VariantType != Variant.Type.String)
            errors.Add($"{ownerLabel}.{fieldLabel} must be a String.");
    }

    protected void _append_int_field_error(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        string fieldLabel,
        Variant value
    )
    {
        if (value.VariantType != Variant.Type.Int)
            errors.Add($"{ownerLabel}.{fieldLabel} must be an int.");
    }

    protected void _append_bool_field_error(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        string fieldLabel,
        Variant value
    )
    {
        if (value.VariantType != Variant.Type.Bool)
            errors.Add($"{ownerLabel}.{fieldLabel} must be a bool.");
    }

    protected void _append_string_name_array_errors(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        Godot.Collections.Array values,
        string fieldLabel,
        bool allowEmptyValues = false
    )
    {
        for (int index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (value.VariantType != Variant.Type.StringName)
            {
                errors.Add($"{ownerLabel}.{fieldLabel}[{index}] must be a StringName.");
                continue;
            }
            if (!allowEmptyValues && value.AsStringName() == "")
                errors.Add($"{ownerLabel}.{fieldLabel}[{index}] must be a non-empty StringName.");
        }
    }

    protected void _append_string_array_errors(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        Godot.Collections.Array values,
        string fieldLabel
    )
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (values[index].VariantType != Variant.Type.String)
                errors.Add($"{ownerLabel}.{fieldLabel}[{index}] must be a String.");
        }
    }

    protected void _append_string_name_to_string_name_dictionary_errors(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        Godot.Collections.Dictionary values,
        string fieldLabel
    )
    {
        foreach (var key in values.Keys)
        {
            if (!_is_string_or_string_name(key) || key.AsString().StripEdges() == "")
                errors.Add($"{ownerLabel}.{fieldLabel} key {key} must be a non-empty String or StringName.");
            var dictValue = values[key];
            if (!_is_string_or_string_name(dictValue) || dictValue.AsString().StripEdges() == "")
                errors.Add($"{ownerLabel}.{fieldLabel}[{key}] must be a non-empty String or StringName.");
        }
    }

    protected void _append_string_name_to_int_dictionary_errors(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        Godot.Collections.Dictionary values,
        string fieldLabel,
        bool requireNonNegative = false
    )
    {
        foreach (var key in values.Keys)
        {
            if (!_is_string_or_string_name(key) || key.AsString().StripEdges() == "")
                errors.Add($"{ownerLabel}.{fieldLabel} key {key} must be a non-empty String or StringName.");
            var dictValue = values[key];
            if (dictValue.VariantType != Variant.Type.Int)
            {
                errors.Add($"{ownerLabel}.{fieldLabel}[{key}] must be an int.");
                continue;
            }
            if (requireNonNegative && (int)(long)dictValue < 0)
                errors.Add($"{ownerLabel}.{fieldLabel}[{key}] must be >= 0.");
        }
    }

    protected void _append_attribute_modifier_array_errors(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        Godot.Collections.Array modifiers,
        string fieldLabel
    )
    {
        for (int index = 0; index < modifiers.Count; index++)
        {
            var modifier = modifiers[index].AsGodotObject();
            var modifierLabel = $"{ownerLabel}.{fieldLabel}[{index}]";
            if (modifier == null || modifier.GetScript().AsGodotObject() != AttributeModifierScript)
            {
                errors.Add($"{modifierLabel} must be an AttributeModifier.");
                continue;
            }
            _append_string_name_field_error(errors, modifierLabel, "attribute_id", modifier.Get("attribute_id"));
            var attrId = modifier.Get("attribute_id");
            if (attrId.VariantType == Variant.Type.StringName && attrId.AsStringName() != ""
                && !_allowed_attribute_id_set().ContainsKey(attrId))
            {
                errors.Add($"{modifierLabel}.attribute_id {attrId} is not a recognized base/resource/combat attribute id.");
            }
            _append_string_name_field_error(errors, modifierLabel, "mode", modifier.Get("mode"));
            var mode = modifier.Get("mode");
            if (!(mode.AsStringName() == "flat" || mode.AsStringName() == "percent"))
                errors.Add($"{modifierLabel}.mode uses unsupported value {mode}.");
            _append_int_field_error(errors, modifierLabel, "value", modifier.Get("value"));
            _append_int_field_error(errors, modifierLabel, "value_per_rank", modifier.Get("value_per_rank"));
            _append_string_name_field_error(errors, modifierLabel, "source_type", modifier.Get("source_type"), true);
            _append_string_name_field_error(errors, modifierLabel, "source_id", modifier.Get("source_id"), true);
        }
    }

    protected void _append_racial_granted_skill_array_errors(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        Godot.Collections.Array grantedSkills,
        string fieldLabel
    )
    {
        for (int index = 0; index < grantedSkills.Count; index++)
        {
            var grantedSkill = grantedSkills[index].AsGodotObject();
            var skillLabel = $"{ownerLabel}.{fieldLabel}[{index}]";
            if (grantedSkill == null || grantedSkill.GetScript().AsGodotObject() != RacialGrantedSkillScript)
            {
                errors.Add($"{skillLabel} must be a RacialGrantedSkill.");
                continue;
            }
            _append_string_name_field_error(errors, skillLabel, "skill_id", grantedSkill.Get("skill_id"));
            _append_int_field_error(errors, skillLabel, "minimum_skill_level", grantedSkill.Get("minimum_skill_level"));
            var minLevel = grantedSkill.Get("minimum_skill_level");
            if (minLevel.VariantType == Variant.Type.Int && minLevel.AsInt32() < 0)
                errors.Add($"{skillLabel}.minimum_skill_level must be >= 0.");
            _append_string_name_field_error(errors, skillLabel, "charge_kind", grantedSkill.Get("charge_kind"));
            var chargeKind = grantedSkill.Get("charge_kind").AsStringName();
            if (!(chargeKind == "at_will" || chargeKind == "per_battle" || chargeKind == "per_turn"))
                errors.Add($"{skillLabel}.charge_kind uses unsupported value {chargeKind}.");
            _append_int_field_error(errors, skillLabel, "charges", grantedSkill.Get("charges"));
            var charges = grantedSkill.Get("charges").AsInt32();
            if (grantedSkill.Get("charges").VariantType == Variant.Type.Int
                && (chargeKind == "per_battle" || chargeKind == "per_turn") && charges <= 0)
                errors.Add($"{skillLabel}.charges must be > 0 for charge_kind {chargeKind}.");
        }
    }

    protected static bool _is_string_or_string_name(Variant value)
    {
        return value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName;
    }
}
