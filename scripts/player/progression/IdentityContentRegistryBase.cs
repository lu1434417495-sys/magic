using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public class IdentityContentRegistryBase : System.IDisposable
{
    private static readonly StringName[] ResourceAttributeIds =
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

    private static readonly StringName[] CombatAttributeIds =
    {
        "armor_class",
        AttributeContentRules.ArmorAcBonus,
        AttributeContentRules.ShieldAcBonus,
        AttributeContentRules.DodgeBonus,
        AttributeContentRules.DeflectionBonus,
        "armor_max_dex_bonus",
    };

    private static HashSet<StringName> _allowed_attribute_id_cache = new();

    private static HashSet<StringName> _allowed_attribute_id_set()
    {
        if (_allowed_attribute_id_cache.Count > 0)
            return _allowed_attribute_id_cache;

        var allowed = new HashSet<StringName>();

        foreach (var attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
        {
            allowed.Add(attributeId);
            StringName modifierId = AttributeSnapshot.GetBaseAttributeModifierId(attributeId);
            if (modifierId != "")
                allowed.Add(modifierId);
        }

        foreach (var attributeId in ResourceAttributeIds)
            allowed.Add(attributeId);

        foreach (var attributeId in CombatAttributeIds)
            allowed.Add(attributeId);

        _allowed_attribute_id_cache = allowed;

        return allowed;
    }

    protected string _registry_label = "IdentityContentRegistry";
    private protected readonly IContentResourceLoader _resourceLoader;

    protected System.Collections.Generic.List<string> _validation_errors = new();
    private bool _disposed;

    internal IdentityContentRegistryBase(IContentResourceLoader resourceLoader)
    {
        _resourceLoader = resourceLoader
            ?? throw new System.ArgumentNullException(nameof(resourceLoader));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        System.GC.SuppressFinalize(this);
        DisposeManagedRegistry();
    }

    private void DisposeManagedRegistry()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        ClearRegistryData();
        _validation_errors.Clear();
    }

    protected virtual void ClearRegistryData()
    {
    }

    public Godot.Collections.Array<string> Validate()
    {
        var copy = new Godot.Collections.Array<string>();

        foreach (string e in _validation_errors)
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

        DirAccess directory = DirAccess.Open(directoryPath);

        if (directory == null)
        {
            _validation_errors.Add($"{_registry_label} could not open {directoryPath}.");

            return;
        }

        try
        {
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
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }
    }

    protected virtual void _register_resource(string resourcePath)
    {
        _validation_errors.Add(
            $"{_registry_label} does not implement resource registration for {resourcePath}."
        );
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

    protected static IReadOnlyDictionary<StringName, T> _snapshot_definitions<T>(
        IReadOnlyDictionary<StringName, T> source
    )
        where T : class
    {
        return new ReadOnlyDictionary<StringName, T>(
            source == null
                ? new Dictionary<StringName, T>()
                : new Dictionary<StringName, T>(source)
        );
    }

    protected static void _append_string_name_field_error(
        System.Collections.Generic.ICollection<string> errors,
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
        System.Collections.Generic.ICollection<string> errors,
        string ownerLabel,
        string fieldLabel,
        string value
    )
    {
    }

    protected void _append_int_field_error(
        System.Collections.Generic.ICollection<string> errors,
        string ownerLabel,
        string fieldLabel,
        int value
    )
    {
    }

    protected void _append_bool_field_error(
        System.Collections.Generic.ICollection<string> errors,
        string ownerLabel,
        string fieldLabel,
        bool value
    )
    {
    }

    protected void _append_string_name_array_errors(
        System.Collections.Generic.ICollection<string> errors,
        string ownerLabel,
        IReadOnlyList<StringName> values,
        string fieldLabel,
        bool allowEmptyValues = false
    )
    {
        if (values == null)
        {
            errors.Add($"{ownerLabel}.{fieldLabel} must be a non-null list.");
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            if (!allowEmptyValues && values[index] == "")
                errors.Add($"{ownerLabel}.{fieldLabel}[{index}] must be a non-empty StringName.");
        }
    }

    protected void _append_string_array_errors(
        System.Collections.Generic.ICollection<string> errors,
        string ownerLabel,
        IReadOnlyList<string> values,
        string fieldLabel
    )
    {
        if (values == null)
        {
            errors.Add($"{ownerLabel}.{fieldLabel} must be a non-null list.");
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            if (values[index] == null)
                errors.Add($"{ownerLabel}.{fieldLabel}[{index}] must be a String.");
        }
    }

    protected void _append_string_name_to_string_name_dictionary_errors(
        System.Collections.Generic.ICollection<string> errors,
        string ownerLabel,
        IReadOnlyDictionary<StringName, StringName> values,
        string fieldLabel
    )
    {
        if (values == null)
        {
            errors.Add($"{ownerLabel}.{fieldLabel} must be a non-null map.");
            return;
        }

        foreach ((StringName key, StringName value) in values)
        {
            if (key == "")
                errors.Add(
                    $"{ownerLabel}.{fieldLabel} key {key} must be a non-empty StringName."
                );

            if (value == "")
                errors.Add(
                    $"{ownerLabel}.{fieldLabel}[{key}] must be a non-empty StringName."
                );
        }
    }

    protected void _append_string_name_to_int_dictionary_errors(
        System.Collections.Generic.ICollection<string> errors,
        string ownerLabel,
        IReadOnlyDictionary<StringName, int> values,
        string fieldLabel,
        bool requireNonNegative = false
    )
    {
        if (values == null)
        {
            errors.Add($"{ownerLabel}.{fieldLabel} must be a non-null map.");
            return;
        }

        foreach ((StringName key, int value) in values)
        {
            if (key == "")
                errors.Add(
                    $"{ownerLabel}.{fieldLabel} key {key} must be a non-empty String or StringName."
                );

            if (requireNonNegative && value < 0)
                errors.Add($"{ownerLabel}.{fieldLabel}[{key}] must be >= 0.");
        }
    }

    protected static void _append_attribute_modifier_array_errors(
        System.Collections.Generic.ICollection<string> errors,
        string ownerLabel,
        IReadOnlyList<AttributeModifierDefinition> modifiers,
        string fieldLabel
    )
    {
        if (modifiers == null)
        {
            errors.Add($"{ownerLabel}.{fieldLabel} must be a non-null list.");
            return;
        }

        for (int index = 0; index < modifiers.Count; index++)
        {
            AttributeModifierDefinition modifier = modifiers[index];
            var modifierLabel = $"{ownerLabel}.{fieldLabel}[{index}]";
            if (modifier == null)
            {
                errors.Add($"{modifierLabel} must be an AttributeModifier.");
                continue;
            }

            StringName attrId = modifier.AttributeId;
            if (attrId == "")
                errors.Add($"{modifierLabel}.attribute_id must be a non-empty StringName.");

            if (attrId != "" && !_allowed_attribute_id_set().Contains(attrId))
            {
                errors.Add(
                    $"{modifierLabel}.attribute_id {attrId} is not a recognized base/resource/combat/derived attribute id."
                );
            }

            StringName mode = modifier.Mode;
            if (mode == "")
                errors.Add($"{modifierLabel}.mode must be a non-empty StringName.");

            if (!AttributeModifier.IsValidMode(mode))
                errors.Add($"{modifierLabel}.mode uses unsupported value {mode}.");
        }
    }

    protected void _append_racial_granted_skill_array_errors(
        System.Collections.Generic.ICollection<string> errors,
        string ownerLabel,
        IReadOnlyList<RacialGrantedSkillDefinition> grantedSkills,
        string fieldLabel
    )
    {
        if (grantedSkills == null)
        {
            errors.Add($"{ownerLabel}.{fieldLabel} must be a non-null list.");
            return;
        }

        for (int index = 0; index < grantedSkills.Count; index++)
        {
            RacialGrantedSkillDefinition grantedSkill = grantedSkills[index];
            var skillLabel = $"{ownerLabel}.{fieldLabel}[{index}]";
            if (grantedSkill == null)
            {
                errors.Add($"{skillLabel} must be a RacialGrantedSkill.");
                continue;
            }

            StringName skillId = grantedSkill.SkillId;
            if (skillId == "")
                errors.Add($"{skillLabel}.skill_id must be a non-empty StringName.");

            int minLevel = grantedSkill.MinimumSkillLevel;
            if (minLevel < 0)
                errors.Add($"{skillLabel}.minimum_skill_level must be >= 0.");

            RacialSkillChargeKind chargeKind = grantedSkill.ChargeKindKind;
            if (chargeKind == RacialSkillChargeKind.Unknown)
                errors.Add($"{skillLabel}.charge_kind must be a non-empty StringName.");

            if (chargeKind == RacialSkillChargeKind.Unknown)
                errors.Add(
                    $"{skillLabel}.charge_kind uses unsupported value {grantedSkill.ChargeKind}."
                );

            int charges = grantedSkill.Charges;
            if (
                chargeKind
                    is RacialSkillChargeKind.PerBattle
                        or RacialSkillChargeKind.PerTurn
                && charges <= 0
            )
                errors.Add(
                    $"{skillLabel}.charges must be > 0 for charge_kind {grantedSkill.ChargeKind}."
                );
        }
    }

}
