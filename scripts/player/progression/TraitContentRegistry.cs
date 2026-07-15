using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;

public class TraitContentRegistry : IdentityContentRegistryBase
{
    private const string TraitConfigDirectoryPath = "res://data/configs/traits";

    private readonly Dictionary<StringName, TraitDefinition> _traitDefinitions = new();

    internal TraitContentRegistry(IContentResourceLoader resourceLoader)
        : this(resourceLoader, loadDefaultContent: true) { }

    internal TraitContentRegistry(
        IContentResourceLoader resourceLoader,
        bool loadDefaultContent
    )
        : base(resourceLoader)
    {
        _registry_label = "TraitContentRegistry";
        if (loadDefaultContent)
            Rebuild();
    }

    public void Rebuild() => LoadFromDirectory(TraitConfigDirectoryPath);

    public void LoadFromDirectory(string directoryPath)
    {
        LoadFromDirectories(new Godot.Collections.Array<string> { directoryPath });
    }

    public void LoadFromDirectories(Godot.Collections.Array<string> directoryPaths)
    {
        _traitDefinitions.Clear();
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

    public IReadOnlyDictionary<StringName, TraitDefinition> GetTraitDefsTyped() =>
        _snapshot_definitions(_traitDefinitions);

    public TraitDefinition GetTraitDef(StringName traitId)
    {
        StringName normalizedTraitId = ProgressionDataUtils.to_string_name(traitId);
        return _traitDefinitions.TryGetValue(
            normalizedTraitId,
            out TraitDefinition traitDefinition
        )
            ? traitDefinition
            : null;
    }

    public bool HasTrait(StringName traitId)
    {
        return GetTraitDef(traitId) != null;
    }

    protected override void ClearRegistryData()
    {
        _traitDefinitions.Clear();
    }

    protected override void _register_resource(string resourcePath)
    {
        Resource resource = _resourceLoader.LoadCanonical<Resource>(resourcePath);
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

        try
        {
            TraitDefinition traitDefinition = TraitDefinition.FromResource(traitDef);
            _traitDefinitions.Add(traitDefinition.TraitId, traitDefinition);
        }
        catch (InvalidDataException exception)
        {
            _validation_errors.Add(
                $"Trait config {resourcePath} projection failed: {exception.Message}"
            );
        }
    }

    private IReadOnlyList<string> CollectValidationErrors() =>
        ValidateDefinitions(_traitDefinitions);

    internal static IReadOnlyList<string> ValidateDefinitions(
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(traitDefinitions);

        var errors = new List<string>();
        foreach (StringName traitId in SortedTraitIds(traitDefinitions))
        {
            AppendTraitValidationErrors(errors, traitId, traitDefinitions[traitId]);
        }
        return new ReadOnlyCollection<string>(errors);
    }

    private static void AppendTraitValidationErrors(
        ICollection<string> errors,
        StringName traitId,
        TraitDefinition traitDef
    )
    {
        string ownerLabel = $"Trait {traitId}";
        _append_string_name_field_error(errors, ownerLabel, "trait_id", traitDef.TraitId);
        AppendStringFieldError(errors, ownerLabel, "display_name", traitDef.DisplayName);
        AppendStringFieldError(errors, ownerLabel, "description", traitDef.Description);

        _append_string_name_field_error(errors, ownerLabel, "effect_type", traitDef.EffectType);
        if (traitDef.EffectKind == TraitEffectKind.Unknown)
            errors.Add($"{ownerLabel}.effect_type uses unsupported value {traitDef.EffectType}.");

        _append_string_name_field_error(errors, ownerLabel, "trigger_type", traitDef.TriggerType);
        TraitTriggerKind triggerKind = traitDef.TriggerKind;
        if (triggerKind == TraitTriggerKind.Unknown)
            errors.Add($"{ownerLabel}.trigger_type uses unsupported value {traitDef.TriggerType}.");
        else if (
            triggerKind != TraitTriggerKind.Passive
            && !TraitTriggerContentRules.HasDispatchForEffectTrigger(
                traitDef.EffectType,
                traitDef.TriggerType
            )
        )
        {
            errors.Add(
                $"{ownerLabel}.trigger_type {traitDef.TriggerType} has no dispatch coverage for effect_type {traitDef.EffectType}."
            );
        }

        _append_string_name_field_error(errors, ownerLabel, "stack_policy", traitDef.StackPolicy);
        if (traitDef.StackPolicyKind == TraitStackPolicyKind.Unknown)
            errors.Add($"{ownerLabel}.stack_policy uses unsupported value {traitDef.StackPolicy}.");

        _append_string_name_field_error(errors, ownerLabel, "charge_scope", traitDef.ChargeScope);
        if (traitDef.ChargeScopeKind == TraitChargeScopeKind.Unknown)
            errors.Add($"{ownerLabel}.charge_scope uses unsupported value {traitDef.ChargeScope}.");

        _append_string_name_field_error(
            errors,
            ownerLabel,
            "charge_reset_timing",
            traitDef.ChargeResetTiming
        );
        if (traitDef.ChargeResetTimingKind == TraitChargeResetTimingKind.Unknown)
            errors.Add(
                $"{ownerLabel}.charge_reset_timing uses unsupported value {traitDef.ChargeResetTiming}."
            );

        AppendSourceValidationErrors(errors, ownerLabel, traitDef);
        _append_attribute_modifier_array_errors(
            errors,
            ownerLabel,
            traitDef.AttributeModifiers,
            "attribute_modifiers"
        );
        AppendPassiveProjectionValidationErrors(errors, ownerLabel, traitDef);
        AppendRollSchemaValidationErrors(errors, ownerLabel, traitDef);
        AppendHighestRollValidationErrors(errors, ownerLabel, traitDef);
    }

    private static void AppendPassiveProjectionValidationErrors(
        ICollection<string> errors,
        string ownerLabel,
        TraitDefinition traitDef
    )
    {
        HashSet<StringName> seenSaveTags = new();
        for (int index = 0; index < traitDef.SaveAdvantageTags.Count; index++)
        {
            StringName tag = traitDef.SaveAdvantageTags[index];
            if (tag == "")
            {
                errors.Add($"{ownerLabel}.save_advantage_tags[{index}] must be a non-empty StringName.");
                continue;
            }
            if (!seenSaveTags.Add(tag))
            {
                errors.Add($"{ownerLabel}.save_advantage_tags[{index}] duplicates save tag {tag}.");
            }
        }

        HashSet<StringName> seenDamageTags = new();
        for (int index = 0; index < traitDef.DamageResistanceEntries.Count; index++)
        {
            TraitDamageResistanceEntryDefinition entry = traitDef.DamageResistanceEntries[index];
            string entryLabel = $"{ownerLabel}.damage_resistance_entries[{index}]";
            if (entry == null)
            {
                errors.Add($"{entryLabel} must be a TraitDamageResistanceEntryDef.");
                continue;
            }

            StringName damageTag = entry.DamageTag;
            if (damageTag == "")
            {
                errors.Add($"{entryLabel}.damage_tag must be a non-empty StringName.");
            }
            else
            {
                if (DamageTagContentRules.ToDamageTagKind(damageTag) == DamageTagKind.Unknown)
                {
                    errors.Add(
                        $"{entryLabel}.damage_tag references unsupported damage tag {damageTag}; expected one of {DamageTagContentRules.ValidDamageTagLabel()}."
                    );
                }
                if (!seenDamageTags.Add(damageTag))
                {
                    errors.Add($"{entryLabel}.damage_tag duplicates damage tag {damageTag}.");
                }
            }

            StringName mitigationTier = entry.MitigationTier;
            if (mitigationTier == "")
            {
                errors.Add($"{entryLabel}.mitigation_tier must be a non-empty StringName.");
            }
            else if (
                DamageTagContentRules.ToMitigationTierKind(mitigationTier)
                == DamageMitigationTierKind.Unknown
            )
            {
                errors.Add(
                    $"{entryLabel}.mitigation_tier uses unsupported mitigation tier {mitigationTier}; expected one of {DamageTagContentRules.ValidMitigationTierLabel()}."
                );
            }
        }

        HashSet<StringName> seenSaveBonusAbilities = new();
        for (int index = 0; index < traitDef.SaveBonusEntries.Count; index++)
        {
            TraitSaveBonusEntryDefinition entry = traitDef.SaveBonusEntries[index];
            string entryLabel = $"{ownerLabel}.save_bonus_entries[{index}]";
            if (entry == null)
            {
                errors.Add($"{entryLabel} must be a TraitSaveBonusEntryDef.");
                continue;
            }

            StringName saveAbility = entry.SaveAbility;
            if (saveAbility == "" || !UnitBaseAttributes.IsBaseAttributeId(saveAbility))
            {
                errors.Add(
                    $"{entryLabel}.save_ability must reference a base attribute id, got {saveAbility}."
                );
            }
            else if (!seenSaveBonusAbilities.Add(saveAbility))
            {
                errors.Add($"{entryLabel}.save_ability duplicates save ability {saveAbility}.");
            }
            if (entry.Bonus == 0)
            {
                errors.Add($"{entryLabel}.bonus must be non-zero.");
            }
        }

        HashSet<StringName> seenPassiveStatuses = new();
        for (int index = 0; index < traitDef.PassiveStatusEffects.Count; index++)
        {
            TraitPassiveStatusEffectDefinition entry = traitDef.PassiveStatusEffects[index];
            string entryLabel = $"{ownerLabel}.passive_status_effects[{index}]";
            if (entry == null)
            {
                errors.Add($"{entryLabel} must be a TraitPassiveStatusEffectDef.");
                continue;
            }

            StringName statusId = entry.StatusId;
            if (statusId == "")
            {
                errors.Add($"{entryLabel}.status_id must be a non-empty StringName.");
            }
            else if (!seenPassiveStatuses.Add(statusId))
            {
                errors.Add($"{entryLabel}.status_id duplicates passive status {statusId}.");
            }

            if (entry.Power <= 0)
                errors.Add($"{entryLabel}.power must be positive.");
            if (entry.Stacks <= 0)
                errors.Add($"{entryLabel}.stacks must be positive.");
            if (entry.CountsAsDebuff && !entry.CountsAsDebuffOverride)
                errors.Add($"{entryLabel}.counts_as_debuff requires counts_as_debuff_override.");

            HashSet<StringName> seenImmunityTags = new();
            for (int tagIndex = 0; tagIndex < entry.SaveImmunityTags.Count; tagIndex++)
            {
                StringName saveTag = entry.SaveImmunityTags[tagIndex];
                if (saveTag == "")
                {
                    errors.Add($"{entryLabel}.save_immunity_tags[{tagIndex}] must be non-empty.");
                    continue;
                }
                if (!BattleSaveContentRules.IsValidSaveTag(saveTag))
                {
                    errors.Add(
                        $"{entryLabel}.save_immunity_tags[{tagIndex}] references unsupported save tag {saveTag}."
                    );
                }
                if (!seenImmunityTags.Add(saveTag))
                {
                    errors.Add(
                        $"{entryLabel}.save_immunity_tags[{tagIndex}] duplicates save tag {saveTag}."
                    );
                }
            }
        }
    }

    private static void AppendSourceValidationErrors(
        ICollection<string> errors,
        string ownerLabel,
        TraitDefinition traitDef
    )
    {
        if (traitDef.AllowedSourceKinds.Count == 0)
            errors.Add($"{ownerLabel}.allowed_source_kinds must include at least one allowed_source_kind.");

        HashSet<StringName> seenSources = new();
        bool allowsIdentity = false;
        for (int index = 0; index < traitDef.AllowedSourceKinds.Count; index++)
        {
            StringName sourceKind = traitDef.AllowedSourceKinds[index];
            TraitSourceKind typedSourceKind = TraitContentRules.ToSourceKind(sourceKind);
            if (typedSourceKind == TraitSourceKind.Unknown)
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
            if (typedSourceKind == TraitSourceKind.Identity)
                allowsIdentity = true;
        }

        if (allowsIdentity && traitDef.AttributeModifiers.Count > 0)
            errors.Add($"{ownerLabel}.attribute_modifiers must be empty for identity traits.");
    }

    private static void AppendStringFieldError(
        ICollection<string> errors,
        string ownerLabel,
        string fieldLabel,
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{ownerLabel}.{fieldLabel} must be a non-empty String.");
    }

    private static void AppendRollSchemaValidationErrors(
        ICollection<string> errors,
        string ownerLabel,
        TraitDefinition traitDef
    )
    {
        AppendRollSchemaSourceValidationErrors(errors, ownerLabel, traitDef);

        HashSet<StringName> seenKeys = new();
        for (int index = 0; index < traitDef.RollValueSchema.Count; index++)
        {
            TraitRollValueSchemaEntryDefinition entry = traitDef.RollValueSchema[index];
            string entryLabel = $"{ownerLabel}.roll_value_schema[{index}]";
            if (entry == null)
            {
                errors.Add($"{entryLabel} must be a TraitRollValueSchemaEntry.");
                continue;
            }

            if (entry.Key != "" && !seenKeys.Add(entry.Key))
                errors.Add($"{entryLabel}.key duplicates roll key {entry.Key}.");

            List<string> schemaErrors = new();
            entry.AppendSchemaErrors(schemaErrors, entryLabel);
            foreach (string schemaError in schemaErrors)
            {
                errors.Add(schemaError);
            }
        }
    }

    private static void AppendRollSchemaSourceValidationErrors(
        ICollection<string> errors,
        string ownerLabel,
        TraitDefinition traitDef
    )
    {
        if (traitDef.RollValueSchema.Count == 0)
            return;

        bool allowsInstanceSource = false;
        bool allowsFixedSource = false;
        foreach (StringName sourceKindValue in traitDef.AllowedSourceKinds)
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
        ICollection<string> errors,
        string ownerLabel,
        TraitDefinition traitDef
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

        foreach (TraitRollValueSchemaEntryDefinition entry in traitDef.RollValueSchema)
        {
            if (
                entry != null
                && entry.Key == compareKey
                && entry.ValueTypeKind == TraitRollValueType.Int
            )
                return;
        }

        errors.Add(
            $"{ownerLabel}.stack_policy highest_roll compare key {compareKey} must reference an int roll_value_schema entry."
        );
    }

    private static StringName GetHighestRollCompareKey(TraitDefinition traitDef)
    {
        return traitDef?.HighestRollCompareKey ?? "";
    }

    private static IReadOnlyList<StringName> SortedTraitIds(
        IReadOnlyDictionary<StringName, TraitDefinition> traitDefinitions
    )
    {
        var sortedKeys = new List<string>();
        foreach (StringName traitId in traitDefinitions.Keys)
            sortedKeys.Add(traitId.ToString());
        sortedKeys.Sort(StringComparer.Ordinal);

        var sortedIds = new List<StringName>(sortedKeys.Count);
        foreach (string traitKey in sortedKeys)
            sortedIds.Add(new StringName(traitKey));
        return sortedIds;
    }
}
