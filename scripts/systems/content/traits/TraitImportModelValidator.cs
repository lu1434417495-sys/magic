#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

internal sealed record TraitValidationRuleSpec(string RuleId, string Description);

internal static class TraitValidationRules
{
    internal const string Id = "trait.validation.id";
    internal const string DisplayName = "trait.validation.display_name";
    internal const string Description = "trait.validation.description";
    internal const string EffectRequired = "trait.validation.effect_required";
    internal const string EffectKnown = "trait.validation.effect_known";
    internal const string TriggerRequired = "trait.validation.trigger_required";
    internal const string TriggerKnown = "trait.validation.trigger_known";
    internal const string TriggerDispatch = "trait.validation.trigger_dispatch";
    internal const string StackRequired = "trait.validation.stack_required";
    internal const string StackKnown = "trait.validation.stack_known";
    internal const string ChargeScopeRequired = "trait.validation.charge_scope_required";
    internal const string ChargeScopeKnown = "trait.validation.charge_scope_known";
    internal const string ChargeResetRequired = "trait.validation.charge_reset_required";
    internal const string ChargeResetKnown = "trait.validation.charge_reset_known";
    internal const string SourceRequired = "trait.validation.source_required";
    internal const string SourceKnown = "trait.validation.source_known";
    internal const string SourceDuplicate = "trait.validation.source_duplicate";
    internal const string IdentityModifier = "trait.validation.identity_modifier";
    internal const string ModifierAttributeRequired = "trait.validation.modifier_attribute_required";
    internal const string ModifierAttributeKnown = "trait.validation.modifier_attribute_known";
    internal const string ModifierModeRequired = "trait.validation.modifier_mode_required";
    internal const string ModifierModeKnown = "trait.validation.modifier_mode_known";
    internal const string SaveTagRequired = "trait.validation.save_tag_required";
    internal const string SaveTagBare = "trait.validation.save_tag_bare";
    internal const string SaveTagKnown = "trait.validation.save_tag_known";
    internal const string SaveTagDuplicate = "trait.validation.save_tag_duplicate";
    internal const string DamageTagRequired = "trait.validation.damage_tag_required";
    internal const string DamageTagKnown = "trait.validation.damage_tag_known";
    internal const string DamageTagDuplicate = "trait.validation.damage_tag_duplicate";
    internal const string MitigationRequired = "trait.validation.mitigation_required";
    internal const string MitigationKnown = "trait.validation.mitigation_known";
    internal const string SaveAbility = "trait.validation.save_ability";
    internal const string SaveAbilityDuplicate = "trait.validation.save_ability_duplicate";
    internal const string SaveBonusNonzero = "trait.validation.save_bonus_nonzero";
    internal const string SaveTagBonusTag = "trait.validation.save_tag_bonus_tag";
    internal const string SaveTagBonusDuplicate = "trait.validation.save_tag_bonus_duplicate";
    internal const string SaveTagBonusPositive = "trait.validation.save_tag_bonus_positive";
    internal const string SaveTagBonusStackMode = "trait.validation.save_tag_bonus_stack_mode";
    internal const string PassiveStatusRequired = "trait.validation.passive_status_required";
    internal const string PassiveStatusDuplicate = "trait.validation.passive_status_duplicate";
    internal const string PassivePowerPositive = "trait.validation.passive_power_positive";
    internal const string PassiveStacksPositive = "trait.validation.passive_stacks_positive";
    internal const string PassiveDebuffOverride = "trait.validation.passive_debuff_override";
    internal const string RollInstanceSource = "trait.validation.roll_instance_source";
    internal const string RollFixedSource = "trait.validation.roll_fixed_source";
    internal const string RollKeyRequired = "trait.validation.roll_key_required";
    internal const string RollKeyDuplicate = "trait.validation.roll_key_duplicate";
    internal const string RollIntRange = "trait.validation.roll_int_range";
    internal const string RollStringValues = "trait.validation.roll_string_values";
    internal const string RollTypeKnown = "trait.validation.roll_type_known";
    internal const string HighestRollKeyRequired = "trait.validation.highest_roll_key_required";
    internal const string HighestRollKeyInt = "trait.validation.highest_roll_key_int";

    internal static IReadOnlyList<TraitValidationRuleSpec> Inventory { get; } =
        new ReadOnlyCollection<TraitValidationRuleSpec>(
            new List<TraitValidationRuleSpec>
            {
                new(TraitJsonImportRules.InvalidDto, "entry matches the strict non-null DTO contract"),
                new(Id, "trait_id is a lowercase stable ID and may contain dot-separated segments"),
                new(DisplayName, "display_name is nonblank"),
                new(Description, "description is nonblank"),
                new(EffectRequired, "effect_type is nonempty"),
                new(EffectKnown, "effect_type maps to a known trait effect"),
                new(TriggerRequired, "trigger_type is nonempty"),
                new(TriggerKnown, "trigger_type maps to a known trigger"),
                new(TriggerDispatch, "non-passive effect and trigger pair has dispatch coverage"),
                new(StackRequired, "stack_policy is nonempty"),
                new(StackKnown, "stack_policy is known"),
                new(ChargeScopeRequired, "charge_scope is nonempty"),
                new(ChargeScopeKnown, "charge_scope is known"),
                new(ChargeResetRequired, "charge_reset_timing is nonempty"),
                new(ChargeResetKnown, "charge_reset_timing is known"),
                new(SourceRequired, "at least one allowed_source_kind is declared"),
                new(SourceKnown, "allowed_source_kind is known"),
                new(SourceDuplicate, "allowed_source_kind is unique"),
                new(IdentityModifier, "identity traits do not declare attribute modifiers"),
                new(ModifierAttributeRequired, "attribute modifier attribute_id is nonempty"),
                new(ModifierAttributeKnown, "attribute modifier attribute_id is recognized"),
                new(ModifierModeRequired, "attribute modifier mode is nonempty"),
                new(ModifierModeKnown, "attribute modifier mode is flat or percent"),
                new(SaveTagRequired, "save tag is nonempty"),
                new(SaveTagBare, "save tag uses bare syntax without semantic suffix"),
                new(SaveTagKnown, "save tag is registered"),
                new(SaveTagDuplicate, "save tag is unique within its field"),
                new(DamageTagRequired, "damage resistance damage_tag is nonempty"),
                new(DamageTagKnown, "damage resistance damage_tag is known"),
                new(DamageTagDuplicate, "damage resistance damage_tag is unique"),
                new(MitigationRequired, "mitigation_tier is nonempty"),
                new(MitigationKnown, "mitigation_tier is known"),
                new(SaveAbility, "save bonus ability is a base attribute"),
                new(SaveAbilityDuplicate, "save bonus ability is unique"),
                new(SaveBonusNonzero, "save bonus is nonzero"),
                new(SaveTagBonusTag, "save-tag bonus references a registered save tag"),
                new(SaveTagBonusDuplicate, "save-tag bonus key is unique"),
                new(SaveTagBonusPositive, "save-tag bonus is positive"),
                new(SaveTagBonusStackMode, "save-tag bonus stack mode is add or highest"),
                new(PassiveStatusRequired, "passive status_id is nonempty"),
                new(PassiveStatusDuplicate, "passive status_id is unique"),
                new(PassivePowerPositive, "passive status power is positive"),
                new(PassiveStacksPositive, "passive status stacks is positive"),
                new(PassiveDebuffOverride, "passive debuff value requires an explicit override"),
                new(RollInstanceSource, "roll schema has a character or equipment_roll source"),
                new(RollFixedSource, "roll schema has no identity or equipment_fixed source"),
                new(RollKeyRequired, "roll schema key is nonempty"),
                new(RollKeyDuplicate, "roll schema key is unique"),
                new(RollIntRange, "int roll schema minimum does not exceed maximum"),
                new(RollStringValues, "string_name roll schema has allowed values"),
                new(RollTypeKnown, "roll schema value_type is known"),
                new(HighestRollKeyRequired, "highest_roll has a compare key"),
                new(HighestRollKeyInt, "highest_roll compare key references an int schema entry"),
            }
        );
}

internal sealed class TraitImportModelValidator
{
    private static readonly string[] RemovedSaveTagSuffixes =
    {
        "_advantage",
        "_disadvantage",
        "_immunity",
    };

    internal IReadOnlyList<ContentJsonDiagnostic> ValidateDomainLocal(
        JsonContentEntryContext context,
        TraitImportModel import
    )
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(import);
        var diagnostics = new List<ContentJsonDiagnostic>();
        string owner = $"Trait {import.TraitId}";

        if (!IsStableTraitId(import.TraitId))
        {
            Add(
                diagnostics,
                TraitValidationRules.Id,
                context,
                "/trait_id",
                $"{owner}.trait_id must be a lowercase stable ID with optional dot-separated segments."
            );
        }
        if (string.IsNullOrWhiteSpace(import.DisplayName))
            Add(diagnostics, TraitValidationRules.DisplayName, context, "/display_name", $"{owner}.display_name must be a non-empty String.");
        if (string.IsNullOrWhiteSpace(import.Description))
            Add(diagnostics, TraitValidationRules.Description, context, "/description", $"{owner}.description must be a non-empty String.");

        ValidateTypedRoot(context, import, owner, diagnostics);
        ValidateSourcesAndModifiers(context, import, owner, diagnostics);
        ValidateSaveTags(context, import.SaveAdvantageTags, owner + ".save_advantage_tags", "/save_advantage_tags", diagnostics);
        ValidateSaveTags(context, import.SaveDisadvantageTags, owner + ".save_disadvantage_tags", "/save_disadvantage_tags", diagnostics);
        ValidateSaveTags(context, import.SaveImmunityTags, owner + ".save_immunity_tags", "/save_immunity_tags", diagnostics);
        ValidatePassiveProjections(context, import, owner, diagnostics);
        ValidateRollSchema(context, import, owner, diagnostics);
        ValidateHighestRoll(context, import, owner, diagnostics);
        return diagnostics;
    }

    internal IReadOnlyList<string> ValidateMessages(TraitImportModel import)
    {
        ArgumentNullException.ThrowIfNull(import);
        var context = new JsonContentEntryContext("traits", import.TraitId, $"Trait {import.TraitId}", "");
        var messages = new List<string>();
        foreach (ContentJsonDiagnostic diagnostic in ValidateDomainLocal(context, import))
            messages.Add(diagnostic.Message);
        return new ReadOnlyCollection<string>(messages);
    }

    internal static bool IsStableTraitId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 192 || value[0] is not (>= 'a' and <= 'z'))
            return false;
        bool segmentHasCharacter = false;
        for (int index = 0; index < value.Length; index += 1)
        {
            char character = value[index];
            if (character == '.')
            {
                if (!segmentHasCharacter || index == value.Length - 1)
                    return false;
                segmentHasCharacter = false;
                continue;
            }
            if (!(character is >= 'a' and <= 'z') && !char.IsAsciiDigit(character) && character != '_')
                return false;
            segmentHasCharacter = true;
        }
        return segmentHasCharacter;
    }

    private static void ValidateTypedRoot(
        JsonContentEntryContext context,
        TraitImportModel import,
        string owner,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        string effect = import.EffectType;
        string trigger = import.TriggerType;
        string stack = import.StackPolicy;
        string chargeScope = import.ChargeScope;
        string chargeReset = import.ChargeResetTiming;

        if (effect.Length == 0)
            Add(diagnostics, TraitValidationRules.EffectRequired, context, "/effect_type", $"{owner}.effect_type must be a non-empty StringName.");
        if (!TraitImportValueRules.IsEffectType(effect))
            Add(diagnostics, TraitValidationRules.EffectKnown, context, "/effect_type", $"{owner}.effect_type uses unsupported value {effect}.");
        if (trigger.Length == 0)
            Add(diagnostics, TraitValidationRules.TriggerRequired, context, "/trigger_type", $"{owner}.trigger_type must be a non-empty StringName.");
        if (!TraitImportValueRules.IsTriggerType(trigger))
            Add(diagnostics, TraitValidationRules.TriggerKnown, context, "/trigger_type", $"{owner}.trigger_type uses unsupported value {trigger}.");
        else if (trigger != TraitImportValueRules.PassiveTrigger && !TraitImportValueRules.HasDispatch(effect, trigger))
            Add(diagnostics, TraitValidationRules.TriggerDispatch, context, "/trigger_type", $"{owner}.trigger_type {trigger} has no dispatch coverage for effect_type {effect}.");
        if (stack.Length == 0)
            Add(diagnostics, TraitValidationRules.StackRequired, context, "/stack_policy", $"{owner}.stack_policy must be a non-empty StringName.");
        if (!TraitImportValueRules.IsStackPolicy(stack))
            Add(diagnostics, TraitValidationRules.StackKnown, context, "/stack_policy", $"{owner}.stack_policy uses unsupported value {stack}.");
        if (chargeScope.Length == 0)
            Add(diagnostics, TraitValidationRules.ChargeScopeRequired, context, "/charge_scope", $"{owner}.charge_scope must be a non-empty StringName.");
        if (!TraitImportValueRules.IsChargeScope(chargeScope))
            Add(diagnostics, TraitValidationRules.ChargeScopeKnown, context, "/charge_scope", $"{owner}.charge_scope uses unsupported value {chargeScope}.");
        if (chargeReset.Length == 0)
            Add(diagnostics, TraitValidationRules.ChargeResetRequired, context, "/charge_reset_timing", $"{owner}.charge_reset_timing must be a non-empty StringName.");
        if (!TraitImportValueRules.IsChargeResetTiming(chargeReset))
            Add(diagnostics, TraitValidationRules.ChargeResetKnown, context, "/charge_reset_timing", $"{owner}.charge_reset_timing uses unsupported value {chargeReset}.");
    }

    private static void ValidateSourcesAndModifiers(
        JsonContentEntryContext context,
        TraitImportModel import,
        string owner,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        if (import.AllowedSourceKinds.Count == 0)
            Add(diagnostics, TraitValidationRules.SourceRequired, context, "/allowed_source_kinds", $"{owner}.allowed_source_kinds must include at least one allowed_source_kind.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool allowsIdentity = false;
        for (int index = 0; index < import.AllowedSourceKinds.Count; index += 1)
        {
            string value = import.AllowedSourceKinds[index];
            string pointer = $"/allowed_source_kinds/{index}";
            bool knownSource = TraitImportValueRules.IsSourceKind(value);
            if (!knownSource)
                Add(diagnostics, TraitValidationRules.SourceKnown, context, pointer, $"{owner}.allowed_source_kinds[{index}] uses unsupported allowed_source_kind {value}.");
            else if (!seen.Add(value))
                Add(diagnostics, TraitValidationRules.SourceDuplicate, context, pointer, $"{owner}.allowed_source_kinds[{index}] duplicates allowed_source_kind {value}.");
            if (knownSource && value == TraitImportValueRules.IdentitySource)
                allowsIdentity = true;
        }
        if (allowsIdentity && import.AttributeModifiers.Count > 0)
            Add(diagnostics, TraitValidationRules.IdentityModifier, context, "/attribute_modifiers", $"{owner}.attribute_modifiers must be empty for identity traits.");

        for (int index = 0; index < import.AttributeModifiers.Count; index += 1)
        {
            TraitAttributeModifierImportModel modifier = import.AttributeModifiers[index];
            string pointer = $"/attribute_modifiers/{index}";
            string attributeId = modifier.AttributeId;
            string mode = modifier.Mode;
            if (attributeId.Length == 0)
                Add(diagnostics, TraitValidationRules.ModifierAttributeRequired, context, pointer + "/attribute_id", $"{owner}.attribute_modifiers[{index}].attribute_id must be a non-empty StringName.");
            else if (!TraitImportValueRules.IsAllowedAttributeId(attributeId))
                Add(diagnostics, TraitValidationRules.ModifierAttributeKnown, context, pointer + "/attribute_id", $"{owner}.attribute_modifiers[{index}].attribute_id {attributeId} is not a recognized base/resource/combat/derived attribute id.");
            if (mode.Length == 0)
                Add(diagnostics, TraitValidationRules.ModifierModeRequired, context, pointer + "/mode", $"{owner}.attribute_modifiers[{index}].mode must be a non-empty StringName.");
            if (!TraitImportValueRules.IsModifierMode(mode))
                Add(diagnostics, TraitValidationRules.ModifierModeKnown, context, pointer + "/mode", $"{owner}.attribute_modifiers[{index}].mode uses unsupported value {mode}.");
        }
    }

    private static void ValidatePassiveProjections(
        JsonContentEntryContext context,
        TraitImportModel import,
        string owner,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        var damageTags = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < import.DamageResistanceEntries.Count; index += 1)
        {
            TraitDamageResistanceEntryImportModel entry = import.DamageResistanceEntries[index];
            string pointer = $"/damage_resistance_entries/{index}";
            if (string.IsNullOrEmpty(entry.DamageTag))
                Add(diagnostics, TraitValidationRules.DamageTagRequired, context, pointer + "/damage_tag", $"{owner}.damage_resistance_entries[{index}].damage_tag must be a non-empty StringName.");
            else
            {
                if (!TraitImportValueRules.IsDamageTag(entry.DamageTag))
                    Add(diagnostics, TraitValidationRules.DamageTagKnown, context, pointer + "/damage_tag", $"{owner}.damage_resistance_entries[{index}].damage_tag references unsupported damage tag {entry.DamageTag}; expected one of acid, fire, force, freeze, lightning, magic, negative_energy, physical_blunt, physical_pierce, physical_slash, poison, psychic, radiant, thunder.");
                if (!damageTags.Add(entry.DamageTag))
                    Add(diagnostics, TraitValidationRules.DamageTagDuplicate, context, pointer + "/damage_tag", $"{owner}.damage_resistance_entries[{index}].damage_tag duplicates damage tag {entry.DamageTag}.");
            }
            if (string.IsNullOrEmpty(entry.MitigationTier))
                Add(diagnostics, TraitValidationRules.MitigationRequired, context, pointer + "/mitigation_tier", $"{owner}.damage_resistance_entries[{index}].mitigation_tier must be a non-empty StringName.");
            else if (!TraitImportValueRules.IsMitigationTier(entry.MitigationTier))
                Add(diagnostics, TraitValidationRules.MitigationKnown, context, pointer + "/mitigation_tier", $"{owner}.damage_resistance_entries[{index}].mitigation_tier uses unsupported mitigation tier {entry.MitigationTier}; expected one of double, half, immune, normal.");
        }

        var saveAbilities = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < import.SaveBonusEntries.Count; index += 1)
        {
            TraitSaveBonusEntryImportModel entry = import.SaveBonusEntries[index];
            string pointer = $"/save_bonus_entries/{index}";
            if (!TraitImportValueRules.IsBaseAttribute(entry.SaveAbility))
                Add(diagnostics, TraitValidationRules.SaveAbility, context, pointer + "/save_ability", $"{owner}.save_bonus_entries[{index}].save_ability must reference a base attribute id, got {entry.SaveAbility}.");
            else if (!saveAbilities.Add(entry.SaveAbility))
                Add(diagnostics, TraitValidationRules.SaveAbilityDuplicate, context, pointer + "/save_ability", $"{owner}.save_bonus_entries[{index}].save_ability duplicates save ability {entry.SaveAbility}.");
            if (entry.Bonus == 0)
                Add(diagnostics, TraitValidationRules.SaveBonusNonzero, context, pointer + "/bonus", $"{owner}.save_bonus_entries[{index}].bonus must be non-zero.");
        }

        var saveTagBonusKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < import.SaveTagBonusEntries.Count; index += 1)
        {
            TraitSaveTagBonusEntryImportModel entry = import.SaveTagBonusEntries[index];
            string pointer = $"/save_tag_bonus_entries/{index}";
            if (string.IsNullOrEmpty(entry.SaveTag))
            {
                Add(
                    diagnostics,
                    TraitValidationRules.SaveTagBonusTag,
                    context,
                    pointer + "/save_tag",
                    $"{owner}.save_tag_bonus_entries[{index}].save_tag must be a non-empty StringName."
                );
            }
            else if (!TraitImportValueRules.IsSaveTag(entry.SaveTag))
            {
                Add(
                    diagnostics,
                    TraitValidationRules.SaveTagBonusTag,
                    context,
                    pointer + "/save_tag",
                    $"{owner}.save_tag_bonus_entries[{index}].save_tag references unsupported save tag {entry.SaveTag}."
                );
            }
            string key = $"{entry.SaveTag}\t{entry.StackMode}";
            if (!saveTagBonusKeys.Add(key))
            {
                Add(
                    diagnostics,
                    TraitValidationRules.SaveTagBonusDuplicate,
                    context,
                    pointer,
                    $"{owner}.save_tag_bonus_entries[{index}] duplicates save tag bonus {entry.SaveTag}/{entry.StackMode}."
                );
            }
            if (entry.Bonus <= 0)
            {
                Add(
                    diagnostics,
                    TraitValidationRules.SaveTagBonusPositive,
                    context,
                    pointer + "/bonus",
                    $"{owner}.save_tag_bonus_entries[{index}].bonus must be positive."
                );
            }
            if (entry.StackMode != "add" && entry.StackMode != "highest")
            {
                Add(
                    diagnostics,
                    TraitValidationRules.SaveTagBonusStackMode,
                    context,
                    pointer + "/stack_mode",
                    $"{owner}.save_tag_bonus_entries[{index}].stack_mode must be add or highest."
                );
            }
        }

        var statuses = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < import.PassiveStatusEffects.Count; index += 1)
        {
            TraitPassiveStatusEffectImportModel entry = import.PassiveStatusEffects[index];
            string pointer = $"/passive_status_effects/{index}";
            if (string.IsNullOrEmpty(entry.StatusId))
                Add(diagnostics, TraitValidationRules.PassiveStatusRequired, context, pointer + "/status_id", $"{owner}.passive_status_effects[{index}].status_id must be a non-empty StringName.");
            else if (!statuses.Add(entry.StatusId))
                Add(diagnostics, TraitValidationRules.PassiveStatusDuplicate, context, pointer + "/status_id", $"{owner}.passive_status_effects[{index}].status_id duplicates passive status {entry.StatusId}.");
            if (entry.Power <= 0)
                Add(diagnostics, TraitValidationRules.PassivePowerPositive, context, pointer + "/power", $"{owner}.passive_status_effects[{index}].power must be positive.");
            if (entry.Stacks <= 0)
                Add(diagnostics, TraitValidationRules.PassiveStacksPositive, context, pointer + "/stacks", $"{owner}.passive_status_effects[{index}].stacks must be positive.");
            if (entry.CountsAsDebuff && !entry.CountsAsDebuffOverride)
                Add(diagnostics, TraitValidationRules.PassiveDebuffOverride, context, pointer + "/counts_as_debuff", $"{owner}.passive_status_effects[{index}].counts_as_debuff requires counts_as_debuff_override.");
            ValidateSaveTags(context, entry.SaveImmunityTags, $"{owner}.passive_status_effects[{index}].save_immunity_tags", pointer + "/save_immunity_tags", diagnostics);
        }
    }

    private static void ValidateSaveTags(
        JsonContentEntryContext context,
        IReadOnlyList<string> tags,
        string fieldLabel,
        string basePointer,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < tags.Count; index += 1)
        {
            string tag = tags[index];
            string pointer = $"{basePointer}/{index}";
            if (string.IsNullOrEmpty(tag))
            {
                Add(diagnostics, TraitValidationRules.SaveTagRequired, context, pointer, $"{fieldLabel}[{index}] must be a non-empty StringName.");
            }
            else
            {
                string? suffix = FindRemovedSaveTagSuffix(tag);
                if (suffix != null)
                    Add(diagnostics, TraitValidationRules.SaveTagBare, context, pointer, $"{fieldLabel}[{index}] entry {tag} uses removed suffix syntax; write the bare save tag {tag[..^suffix.Length]} in the field matching its semantics.");
                else if (!TraitImportValueRules.IsSaveTag(tag))
                    Add(diagnostics, TraitValidationRules.SaveTagKnown, context, pointer, $"{fieldLabel}[{index}] entry {tag} is not a supported save tag.");
            }
            if (!seen.Add(tag))
                Add(diagnostics, TraitValidationRules.SaveTagDuplicate, context, pointer, $"{fieldLabel}[{index}] duplicates save tag {tag}.");
        }
    }

    private static void ValidateRollSchema(
        JsonContentEntryContext context,
        TraitImportModel import,
        string owner,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        if (import.RollValueSchema.Count == 0)
            return;
        bool allowsInstance = false;
        bool allowsFixed = false;
        foreach (string source in import.AllowedSourceKinds)
        {
            if (TraitImportValueRules.IsInstanceSource(source))
                allowsInstance = true;
            if (TraitImportValueRules.IsFixedSource(source))
                allowsFixed = true;
        }
        if (!allowsInstance)
            Add(diagnostics, TraitValidationRules.RollInstanceSource, context, "/roll_value_schema", $"{owner}.roll_value_schema requires an instance source such as character or equipment_roll.");
        if (allowsFixed)
            Add(diagnostics, TraitValidationRules.RollFixedSource, context, "/roll_value_schema", $"{owner}.roll_value_schema cannot be used by fixed sources such as identity or equipment_fixed.");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < import.RollValueSchema.Count; index += 1)
        {
            TraitRollValueSchemaEntryImportModel entry = import.RollValueSchema[index];
            string pointer = $"/roll_value_schema/{index}";
            if (string.IsNullOrEmpty(entry.Key))
                Add(diagnostics, TraitValidationRules.RollKeyRequired, context, pointer + "/key", $"{owner}.roll_value_schema[{index}]: roll_value_schema entry missing key.");
            else if (!keys.Add(entry.Key))
                Add(diagnostics, TraitValidationRules.RollKeyDuplicate, context, pointer + "/key", $"{owner}.roll_value_schema[{index}].key duplicates roll key {entry.Key}.");

            switch (entry.ValueType)
            {
                case TraitImportValueRules.RollInt:
                    if (entry.MinValue > entry.MaxValue)
                        Add(diagnostics, TraitValidationRules.RollIntRange, context, pointer, $"{owner}.roll_value_schema[{index}].{entry.Key}: min_value {entry.MinValue} > max_value {entry.MaxValue}.");
                    break;
                case TraitImportValueRules.RollStringName:
                    if (entry.AllowedValues.Count == 0)
                        Add(diagnostics, TraitValidationRules.RollStringValues, context, pointer + "/allowed_values", $"{owner}.roll_value_schema[{index}].{entry.Key}: string_name roll needs non-empty allowed_values.");
                    break;
                case TraitImportValueRules.RollBool:
                    break;
                default:
                    Add(diagnostics, TraitValidationRules.RollTypeKnown, context, pointer + "/value_type", $"{owner}.roll_value_schema[{index}].{entry.Key}: unsupported value_type {entry.ValueType}.");
                    break;
            }
        }
    }

    private static void ValidateHighestRoll(
        JsonContentEntryContext context,
        TraitImportModel import,
        string owner,
        ICollection<ContentJsonDiagnostic> diagnostics
    )
    {
        if (import.StackPolicy != TraitImportValueRules.HighestRollStackPolicy)
            return;
        string compareKey = import.HighestRollCompareKey;
        if (string.IsNullOrEmpty(compareKey))
        {
            foreach (TraitRollValueSchemaEntryImportModel entry in import.RollValueSchema)
            {
                if (entry.ValueType == TraitImportValueRules.RollInt && !string.IsNullOrEmpty(entry.Key))
                {
                    compareKey = entry.Key;
                    break;
                }
            }
        }
        if (string.IsNullOrEmpty(compareKey))
        {
            Add(diagnostics, TraitValidationRules.HighestRollKeyRequired, context, "/highest_roll_compare_key", $"{owner}.stack_policy highest_roll requires highest_roll_compare_key or an int roll_value_schema entry.");
            return;
        }
        foreach (TraitRollValueSchemaEntryImportModel entry in import.RollValueSchema)
        {
            if (entry.Key == compareKey && entry.ValueType == TraitImportValueRules.RollInt)
                return;
        }
        Add(diagnostics, TraitValidationRules.HighestRollKeyInt, context, "/highest_roll_compare_key", $"{owner}.stack_policy highest_roll compare key {compareKey} must reference an int roll_value_schema entry.");
    }

    private static string? FindRemovedSaveTagSuffix(string value)
    {
        foreach (string suffix in RemovedSaveTagSuffixes)
        {
            if (value.EndsWith(suffix, StringComparison.Ordinal))
                return suffix;
        }
        return null;
    }

    private static void Add(
        ICollection<ContentJsonDiagnostic> diagnostics,
        string ruleId,
        JsonContentEntryContext context,
        string relativePointer,
        string message
    ) => diagnostics.Add(
        new ContentJsonDiagnostic(
            ruleId,
            message,
            context.SourceLabel,
            context.JsonPointer + relativePointer
        )
    );
}
