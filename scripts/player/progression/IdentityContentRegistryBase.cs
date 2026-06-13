using Godot;

[GlobalClass]
public partial class IdentityContentRegistryBase : RefCounted
{
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

        foreach (var attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
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

    public IdentityContentRegistryBase()
    {
        System.GC.SuppressFinalize(this);
    }

    public Godot.Collections.Array<string> Validate()
    {
        var copy = new Godot.Collections.Array<string>();

        foreach (var e in _validation_errors)
            copy.Add(e);

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
        _validation_errors.Add(
            $"{_registry_label} does not implement resource registration for {resourcePath}."
        );
    }

    protected System.Collections.Generic.List<string> _sorted_registry_keys(
        Godot.Collections.Dictionary registry
    )
    {
        return ProgressionDataUtils.sorted_string_keys(registry);
    }

    protected System.Collections.Generic.List<string> _sorted_registry_keys(
        System.Collections.Generic.IEnumerable<StringName> keys
    )
    {
        var sorted = new System.Collections.Generic.List<string>();
        foreach (var key in keys)
            sorted.Add(key.ToString());
        sorted.Sort(System.StringComparer.Ordinal);
        return sorted;
    }

    protected void _append_string_name_field_error(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        string fieldLabel,
        StringName value,
        bool allowEmpty = false
    )
    {
        if (!allowEmpty && value == "")
            errors.Add($"{ownerLabel}.{fieldLabel} must be a non-empty StringName.");
    }

    protected void _append_string_field_error(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        string fieldLabel,
        string value
    )
    {
    }

    protected void _append_int_field_error(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        string fieldLabel,
        int value
    )
    {
    }

    protected void _append_bool_field_error(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        string fieldLabel,
        bool value
    )
    {
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

            if (!TryAsStrictStringName(value, out StringName parsedValue))
            {
                errors.Add($"{ownerLabel}.{fieldLabel}[{index}] must be a StringName.");

                continue;
            }

            if (!allowEmptyValues && parsedValue == "")
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
            if (!TryAsStrictString(values[index], out _))
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
            if (key.VariantType != Variant.Type.StringName || key.AsStringName() == "")
                errors.Add(
                    $"{ownerLabel}.{fieldLabel} key {key} must be a non-empty StringName."
                );

            var dictValue = values[key];

            if (
                dictValue.VariantType != Variant.Type.StringName
                || dictValue.AsStringName() == ""
            )
                errors.Add(
                    $"{ownerLabel}.{fieldLabel}[{key}] must be a non-empty StringName."
                );
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
            if (!TryAsStringLike(key, out string keyText) || keyText.StripEdges() == "")
                errors.Add(
                    $"{ownerLabel}.{fieldLabel} key {key} must be a non-empty String or StringName."
                );

            var dictValue = values[key];

            if (!TryAsStrictInt(dictValue, out int dictIntValue))
            {
                errors.Add($"{ownerLabel}.{fieldLabel}[{key}] must be an int.");

                continue;
            }

            if (requireNonNegative && dictIntValue < 0)
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
            TryAsObject(modifiers[index], out AttributeModifier modifier);

            var modifierLabel = $"{ownerLabel}.{fieldLabel}[{index}]";

            if (modifier == null)
            {
                errors.Add($"{modifierLabel} must be an AttributeModifier.");

                continue;
            }

            StringName attrId = modifier.attribute_id;
            if (attrId == "")
                errors.Add($"{modifierLabel}.attribute_id must be a non-empty StringName.");

            if (
                attrId != ""
                && !_allowed_attribute_id_set().ContainsKey(attrId)
            )
            {
                errors.Add(
                    $"{modifierLabel}.attribute_id {attrId} is not a recognized base/resource/combat attribute id."
                );
            }

            StringName mode = modifier.mode;
            if (mode == "")
                errors.Add($"{modifierLabel}.mode must be a non-empty StringName.");

            if (!AttributeModifier.IsValidMode(mode))
                errors.Add($"{modifierLabel}.mode uses unsupported value {mode}.");
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
            TryAsObject(grantedSkills[index], out RacialGrantedSkill grantedSkill);

            var skillLabel = $"{ownerLabel}.{fieldLabel}[{index}]";

            if (grantedSkill == null)
            {
                errors.Add($"{skillLabel} must be a RacialGrantedSkill.");

                continue;
            }

            StringName skillId = grantedSkill.skill_id;
            if (skillId == "")
                errors.Add($"{skillLabel}.skill_id must be a non-empty StringName.");

            int minLevel = grantedSkill.minimum_skill_level;
            if (minLevel < 0)
                errors.Add($"{skillLabel}.minimum_skill_level must be >= 0.");

            RacialSkillChargeKind chargeKind = grantedSkill.ChargeKind;
            if (chargeKind == RacialSkillChargeKind.Unknown)
                errors.Add($"{skillLabel}.charge_kind must be a non-empty StringName.");

            if (chargeKind == RacialSkillChargeKind.Unknown)
                errors.Add(
                    $"{skillLabel}.charge_kind uses unsupported value {grantedSkill.charge_kind}."
                );

            int charges = grantedSkill.charges;
            if (
                (
                    chargeKind
                    is RacialSkillChargeKind.PerBattle
                        or RacialSkillChargeKind.PerTurn
                )
                && charges <= 0
            )
                errors.Add(
                    $"{skillLabel}.charges must be > 0 for charge_kind {grantedSkill.charge_kind}."
                );
        }
    }

    private static bool TryAsStrictStringName(object rawValue, out StringName value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.StringName)
        {
            value = variant.AsStringName();
            return true;
        }
        if (rawValue is StringName stringNameValue)
        {
            value = stringNameValue;
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStrictString(object rawValue, out string value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString();
            return true;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStringLike(object rawValue, out string value)
    {
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == Variant.Type.String)
            {
                value = variant.AsString();
                return true;
            }
            if (variant.VariantType == Variant.Type.StringName)
            {
                value = variant.AsStringName().ToString();
                return true;
            }
            value = "";
            return false;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        if (rawValue is StringName stringNameValue)
        {
            value = stringNameValue.ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStrictInt(object rawValue, out int value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Int)
        {
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsObject<T>(object rawValue, out T value)
        where T : GodotObject
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Object)
        {
            value = variant.AsGodotObject() as T;
            return value != null;
        }
        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = null;
        return false;
    }

}
