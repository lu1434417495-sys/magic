using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class TraitContentRegistry : IdentityContentRegistryBase
{
    private const string TraitConfigDirectoryPath = "res://data/configs/traits";

    private readonly List<TraitDef> _traitDefs = new();

    public TraitContentRegistry()
    {
        _registry_label = "TraitContentRegistry";
        Rebuild();
    }

    public void Rebuild() => LoadFromDirectory(TraitConfigDirectoryPath);

    public void LoadFromDirectory(string directoryPath)
    {
        LoadFromDirectories(new Godot.Collections.Array<string> { directoryPath });
    }

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _traitDefs.Clear();
        _validation_errors.Clear();
        foreach (string directoryPath in directoryPaths)
        {
            _scan_directory(directoryPath);
        }
        foreach (string error in CollectValidationErrors())
        {
            _validation_errors.Add(error);
        }
    }

    public IReadOnlyList<TraitDef> GetTraitDefsTyped()
    {
        return new List<TraitDef>(_traitDefs);
    }

    public TraitDef GetTraitDef(StringName traitId)
    {
        StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
        foreach (TraitDef traitDef in _traitDefs)
            if (traitDef != null && traitDef.trait_id == normalizedTraitId)
                return traitDef;
        return null;
    }

    public bool HasTrait(StringName traitId)
    {
        return GetTraitDef(traitId) != null;
    }

    protected override void ClearRegistryData()
    {
        _traitDefs.Clear();
    }

    protected override void _register_resource(string resourcePath)
    {
        Resource resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validation_errors.Add($"Failed to load trait config {resourcePath}.");
            return;
        }
        if (resource is not TraitDef traitDef)
        {
            _validation_errors.Add($"Trait config {resourcePath} is not a TraitDef.");
            return;
        }

        StringName traitId = traitDef.trait_id;
        if (traitId == "")
        {
            _validation_errors.Add($"Trait config {resourcePath} is missing trait_id.");
            return;
        }
        if (HasTrait(traitId))
        {
            _validation_errors.Add($"Duplicate trait_id registered: {traitId}");
            return;
        }

        _traitDefs.Add(traitDef);
    }

    private Godot.Collections.Array<string> CollectValidationErrors()
    {
        Godot.Collections.Array<string> errors = new();
        foreach (string traitKey in _sorted_registry_keys(GetTraitIds()))
        {
            StringName traitId = new(traitKey);
            AppendTraitValidationErrors(errors, traitId, GetTraitDef(traitId));
        }
        return errors;
    }

    private List<StringName> GetTraitIds()
    {
        List<StringName> ids = new();
        foreach (TraitDef traitDef in _traitDefs)
            if (traitDef != null && traitDef.trait_id != "")
                ids.Add(traitDef.trait_id);
        return ids;
    }

    private void AppendTraitValidationErrors(
        Godot.Collections.Array<string> errors,
        StringName traitId,
        TraitDef traitDef
    )
    {
        string ownerLabel = $"Trait {traitId}";
        _append_string_name_field_error(errors, ownerLabel, "trait_id", traitDef.trait_id);
        AppendStringFieldError(errors, ownerLabel, "display_name", traitDef.display_name);
        AppendStringFieldError(errors, ownerLabel, "description", traitDef.description);

        _append_string_name_field_error(errors, ownerLabel, "effect_type", traitDef.effect_type);
        if (!TraitContentRules.IsValidEffectType(traitDef.effect_type))
            errors.Add($"{ownerLabel}.effect_type uses unsupported value {traitDef.effect_type}.");

        _append_string_name_field_error(errors, ownerLabel, "trigger_type", traitDef.trigger_type);
        TraitTriggerKind triggerKind = TraitTriggerContentRules.ToTriggerKind(
            traitDef.trigger_type
        );
        if (triggerKind == TraitTriggerKind.Unknown)
            errors.Add($"{ownerLabel}.trigger_type uses unsupported value {traitDef.trigger_type}.");
        else if (
            triggerKind != TraitTriggerKind.Passive
            && !TraitTriggerContentRules.HasDispatchForEffectTrigger(
                traitDef.effect_type,
                traitDef.trigger_type
            )
        )
        {
            errors.Add(
                $"{ownerLabel}.trigger_type {traitDef.trigger_type} has no dispatch coverage for effect_type {traitDef.effect_type}."
            );
        }

        _append_string_name_field_error(errors, ownerLabel, "stack_policy", traitDef.stack_policy);
        if (!TraitContentRules.IsValidStackPolicy(traitDef.stack_policy))
            errors.Add($"{ownerLabel}.stack_policy uses unsupported value {traitDef.stack_policy}.");

        _append_string_name_field_error(errors, ownerLabel, "charge_scope", traitDef.charge_scope);
        if (!TraitContentRules.IsValidChargeScope(traitDef.charge_scope))
            errors.Add($"{ownerLabel}.charge_scope uses unsupported value {traitDef.charge_scope}.");

        _append_string_name_field_error(
            errors,
            ownerLabel,
            "charge_reset_timing",
            traitDef.charge_reset_timing
        );
        if (!TraitContentRules.IsValidChargeResetTiming(traitDef.charge_reset_timing))
            errors.Add(
                $"{ownerLabel}.charge_reset_timing uses unsupported value {traitDef.charge_reset_timing}."
            );

        AppendSourceValidationErrors(errors, ownerLabel, traitDef);
        _append_attribute_modifier_array_errors(
            errors,
            ownerLabel,
            V(traitDef.attribute_modifiers),
            "attribute_modifiers"
        );
        AppendRollSchemaValidationErrors(errors, ownerLabel, traitDef);
        AppendHighestRollValidationErrors(errors, ownerLabel, traitDef);
    }

    private static void AppendSourceValidationErrors(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        TraitDef traitDef
    )
    {
        if (traitDef.allowed_source_kinds.Count == 0)
            errors.Add($"{ownerLabel}.allowed_source_kinds must include at least one allowed_source_kind.");

        HashSet<StringName> seenSources = new();
        bool allowsIdentity = false;
        for (int index = 0; index < traitDef.allowed_source_kinds.Count; index++)
        {
            StringName sourceKind = traitDef.allowed_source_kinds[index];
            if (!TraitContentRules.IsValidSourceType(sourceKind))
            {
                errors.Add(
                    $"{ownerLabel}.allowed_source_kinds[{index}] uses unsupported allowed_source_kind {sourceKind}."
                );
                continue;
            }
            if (!seenSources.Add(sourceKind))
                errors.Add(
                    $"{ownerLabel}.allowed_source_kinds[{index}] duplicates allowed_source_kind {sourceKind}."
                );
            if (sourceKind == TraitContentRules.ToStringName(TraitSourceKind.Identity))
                allowsIdentity = true;
        }

        if (allowsIdentity && traitDef.attribute_modifiers.Count > 0)
            errors.Add($"{ownerLabel}.attribute_modifiers must be empty for identity traits.");
    }

    private static void AppendStringFieldError(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        string fieldLabel,
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{ownerLabel}.{fieldLabel} must be a non-empty String.");
    }

    private static void AppendRollSchemaValidationErrors(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        TraitDef traitDef
    )
    {
        AppendRollSchemaSourceValidationErrors(errors, ownerLabel, traitDef);

        HashSet<StringName> seenKeys = new();
        for (int index = 0; index < traitDef.roll_value_schema.Count; index++)
        {
            TraitRollValueSchemaEntry entry = traitDef.roll_value_schema[index];
            string entryLabel = $"{ownerLabel}.roll_value_schema[{index}]";
            if (entry == null)
            {
                errors.Add($"{entryLabel} must be a TraitRollValueSchemaEntry.");
                continue;
            }

            if (entry.key != "" && !seenKeys.Add(entry.key))
                errors.Add($"{entryLabel}.key duplicates roll key {entry.key}.");

            List<string> schemaErrors = new();
            entry.AppendSchemaErrors(schemaErrors, entryLabel);
            foreach (string schemaError in schemaErrors)
            {
                errors.Add(schemaError);
            }
        }
    }

    private static void AppendRollSchemaSourceValidationErrors(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        TraitDef traitDef
    )
    {
        if (traitDef.roll_value_schema.Count == 0)
            return;

        bool allowsInstanceSource = false;
        bool allowsFixedSource = false;
        foreach (StringName sourceKindValue in traitDef.allowed_source_kinds)
        {
            switch (TraitContentRules.ToSourceKind(sourceKindValue))
            {
                case TraitSourceKind.Character:
                case TraitSourceKind.EquipmentRoll:
                    allowsInstanceSource = true;
                    break;
                case TraitSourceKind.Identity:
                case TraitSourceKind.EquipmentFixed:
                    allowsFixedSource = true;
                    break;
            }
        }

        if (!allowsInstanceSource)
            errors.Add(
                $"{ownerLabel}.roll_value_schema requires an instance source such as character or equipment_roll."
            );
        if (allowsFixedSource)
            errors.Add(
                $"{ownerLabel}.roll_value_schema cannot be used by fixed sources such as identity or equipment_fixed."
            );
    }

    private static void AppendHighestRollValidationErrors(
        Godot.Collections.Array<string> errors,
        string ownerLabel,
        TraitDef traitDef
    )
    {
        if (traitDef.StackPolicyKind != TraitStackPolicyKind.HighestRoll)
            return;

        StringName compareKey = GetHighestRollCompareKey(traitDef);
        if (compareKey == "")
        {
            errors.Add(
                $"{ownerLabel}.stack_policy highest_roll requires highest_roll_compare_key or an int roll_value_schema entry."
            );
            return;
        }

        foreach (TraitRollValueSchemaEntry entry in traitDef.roll_value_schema)
        {
            if (
                entry != null
                && entry.key == compareKey
                && entry.ValueTypeKind == TraitRollValueType.Int
            )
                return;
        }

        errors.Add(
            $"{ownerLabel}.stack_policy highest_roll compare key {compareKey} must reference an int roll_value_schema entry."
        );
    }

    private static StringName GetHighestRollCompareKey(TraitDef traitDef)
    {
        return traitDef?.GetHighestRollCompareKey() ?? "";
    }

    private static Godot.Collections.Array V<[MustBeVariant] T>(
        Godot.Collections.Array<T> values
    )
    {
        var result = new Godot.Collections.Array();
        if (values == null)
            return result;
        foreach (T value in values)
            result.Add(Variant.From(value));
        return result;
    }
}
