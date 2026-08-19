using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal sealed class EquipmentAbilityBindingValidator
{
    private readonly IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> _conditionSpecs;
    private readonly IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> _actionSpecs;
    private readonly IReadOnlyDictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec>
        _triggerTimingSpecs;

    internal EquipmentAbilityBindingValidator(
        IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> conditionSpecs,
        IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> actionSpecs,
        IReadOnlyDictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec> triggerTimingSpecs
    )
    {
        _conditionSpecs = conditionSpecs;
        _actionSpecs = actionSpecs;
        _triggerTimingSpecs = triggerTimingSpecs;
    }

    internal void ValidateBinding(
        EquipmentAbilityBindingImportModel binding,
        EquipmentAbilityContentValidationContext context,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> loadedBindings,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        if (binding.binding_id == "")
        {
            EquipmentAbilityContentRegistry.AddError(errors, "EQA_BINDING_MISSING_ID", path, "binding_id is required");
        }
        if (binding.trait_id == "" || !EquipmentAbilityContentRegistry.ContainsValue(context.KnownTraitIds, binding.trait_id))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_REFERENCE_MISSING_TRAIT",
                path,
                $"trait_id {binding.trait_id} is not known"
            );
        }
        if (!EquipmentAbilityDefinitionProjection.TryParseOverrideMode(binding.override_mode, out EquipmentAbilityBindingOverrideMode mode))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_BINDING_OVERRIDE_MODE_UNSUPPORTED",
                $"{path}.override_mode",
                $"override_mode {binding.override_mode} is not supported"
            );
        }
        else if (mode == EquipmentAbilityBindingOverrideMode.Add)
        {
            if (binding.replaces_binding_id != "")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_BINDING_REPLACE_ID_UNEXPECTED",
                    $"{path}.replaces_binding_id",
                    "add bindings must not declare replaces_binding_id"
                );
            }
            if (binding.binding_id != "" && loadedBindings.ContainsKey(binding.binding_id))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_BINDING_DUPLICATE_ID",
                    path,
                    $"duplicate binding_id {binding.binding_id}"
                );
            }
        }
        else if (
            binding.replaces_binding_id == ""
            || !loadedBindings.ContainsKey(binding.replaces_binding_id)
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_BINDING_REPLACE_TARGET_MISSING",
                $"{path}.replaces_binding_id",
                $"replace_binding target {binding.replaces_binding_id} must already be loaded"
            );
        }
        else if (
            binding.binding_id != ""
            && binding.binding_id != binding.replaces_binding_id
            && loadedBindings.ContainsKey(binding.binding_id)
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_BINDING_REPLACE_ID_COLLISION",
                path,
                $"replace_binding binding_id {binding.binding_id} collides with an unrelated loaded binding"
            );
        }

        ValidateSourceKinds(binding, errors);
        ValidateRequiredEffectiveTraits(binding, context, errors);
        ValidateActivationSource(binding, context, errors);
        HashSet<StringName> declaredStateKeys = ValidateStateSchemas(binding, errors);
        ValidateReactions(binding, context, declaredStateKeys, errors);
        ValidateFatalIntercepts(binding, context, declaredStateKeys, errors);
        ValidateMitigationAuras(binding, errors);
        ValidateMovementTrails(binding, context, errors);
        ValidateGrantedActions(binding, context, errors);
        ValidateTemporalProgressModifiers(binding, errors);
        ValidateCognitionCeilingModifiers(binding, errors);
        ValidateWeaponProfileOverlays(binding, context, errors);
        ValidateWorldEffects(binding, context, declaredStateKeys, errors);
    }

    private static void ValidateRequiredEffectiveTraits(
        EquipmentAbilityBindingImportModel binding,
        EquipmentAbilityContentValidationContext context,
        List<string> errors
    )
    {
        if (binding?.required_effective_trait_ids == null)
            return;

        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        var seen = new HashSet<StringName>();
        for (int index = 0; index < binding.required_effective_trait_ids.Count; index++)
        {
            StringName traitId = ProgressionDataUtils.to_string_name(
                binding.required_effective_trait_ids[index]
            );
            string traitPath = $"{path}.required_effective_trait_ids[{index}]";
            if (traitId == "")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_REQUIRED_EFFECTIVE_TRAIT_EMPTY",
                    traitPath,
                    "required effective trait id must not be empty"
                );
                continue;
            }
            if (!seen.Add(traitId))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_REQUIRED_EFFECTIVE_TRAIT_DUPLICATE",
                    traitPath,
                    $"required effective trait {traitId} must not be duplicated"
                );
                continue;
            }
            if (!EquipmentAbilityContentRegistry.ContainsValue(context.KnownTraitIds, traitId))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_REFERENCE_MISSING_REQUIRED_EFFECTIVE_TRAIT",
                    traitPath,
                    $"required effective trait {traitId} is not known"
                );
            }
        }
    }

    private static void ValidateSourceKinds(
        EquipmentAbilityBindingImportModel binding,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        foreach (StringName sourceKind in binding.allowed_source_kinds)
        {
            TraitSourceKind parsed = TraitContentRules.ToSourceKind(sourceKind);
            if (
                parsed != TraitSourceKind.EquipmentFixed
                && parsed != TraitSourceKind.EquipmentRoll
                && parsed != TraitSourceKind.GearSetThreshold
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_SOURCE_KIND_UNSUPPORTED",
                    $"{path}.allowed_source_kinds[{sourceKind}]",
                    $"allowed_source_kind {sourceKind} is not supported for equipment abilities"
                );
            }
        }
    }

    private static void ValidateActivationSource(
        EquipmentAbilityBindingImportModel binding,
        EquipmentAbilityContentValidationContext context,
        List<string> errors
    )
    {
        if (binding == null || binding.activation_status_id == "")
            return;
        ValidateStatusReference(
            binding.activation_status_id,
            context,
            $"{EquipmentAbilityContentRegistry.BindingPath(binding)}.activation_status_id",
            errors
        );
        if (binding.granted_actions?.Count > 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_ACTIVATION_GRANTED_ACTION_UNSUPPORTED",
                $"{EquipmentAbilityContentRegistry.BindingPath(binding)}.granted_actions",
                "status-activated bindings cannot grant command actions"
            );
        }
        if (binding.world_effects?.Count > 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATUS_ACTIVATION_WORLD_EFFECT_UNSUPPORTED",
                $"{EquipmentAbilityContentRegistry.BindingPath(binding)}.world_effects",
                "status-activated bindings cannot project persistent world effects"
            );
        }
    }

    private static HashSet<StringName> ValidateStateSchemas(
        EquipmentAbilityBindingImportModel binding,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        var keys = new HashSet<StringName>();
        foreach (EquipmentAbilityStateSchemaImportModel schema in binding.state_schemas)
        {
            if (schema == null)
                continue;
            StringName stateKey = ProgressionDataUtils.to_string_name(schema.state_key);
            if (stateKey == "")
                continue;
            keys.Add(stateKey);
            bool persistentReset =
                schema.reset_timing == "per_world_day"
                || schema.reset_timing == "per_world_month"
                || schema.reset_timing == "persistent_counter";
            bool invalidReset =
                schema.reset_timing == "per_day"
                || schema.reset_timing == "per_month"
                || schema.reset_timing == "per_rest"
                || schema.reset_timing == "per_short_rest"
                || schema.reset_timing == "per_long_rest";
            if (invalidReset)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_STATE_RESET_POLICY_UNSUPPORTED",
                    $"{path}.state_schemas[{schema.state_key}].reset_timing",
                    $"reset_timing {schema.reset_timing} is not supported in V1"
                );
            }
            if (persistentReset && schema.owner_scope != "equipment_instance")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_STATE_PERSISTENT_OWNER_INVALID",
                    $"{path}.state_schemas[{schema.state_key}]",
                    $"reset_timing {schema.reset_timing} requires owner_scope equipment_instance"
                );
            }
        }
        foreach (EquipmentAbilityStateSchemaImportModel schema in binding.state_schemas)
            ValidateStateSchemaSync(schema, keys, path, errors);
        return keys;
    }

    private static void ValidateStateSchemaSync(
        EquipmentAbilityStateSchemaImportModel schema,
        HashSet<StringName> declaredStateKeys,
        string bindingPath,
        List<string> errors
    )
    {
        if (schema == null)
            return;
        StringName stateKey = ProgressionDataUtils.to_string_name(schema.state_key);
        if (stateKey == "")
            return;

        StringName sourceStateKey = ProgressionDataUtils.to_string_name(
            schema.sync_source_state_key
        );
        StringName syncAggregation = ProgressionDataUtils.to_string_name(
            schema.sync_aggregation
        );
        string statePath = $"{bindingPath}.state_schemas[{stateKey}]";
        if (sourceStateKey == "")
        {
            if (syncAggregation != "" || schema.sync_int_literal != 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_STATE_SYNC_INVALID",
                    $"{statePath}.sync_source_state_key",
                    "state sync aggregation requires sync_source_state_key"
                );
            }
            return;
        }

        if (sourceStateKey == stateKey)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATE_SYNC_INVALID",
                $"{statePath}.sync_source_state_key",
                "state sync source cannot be the target state itself"
            );
        }
        if (!declaredStateKeys.Contains(sourceStateKey))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_STATE_SYNC_SOURCE_UNDECLARED",
                $"{statePath}.sync_source_state_key",
                $"sync_source_state_key {sourceStateKey} is not declared by binding state_schemas"
            );
        }

        if (syncAggregation == "" || syncAggregation == "value")
        {
            if (schema.sync_int_literal != 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_STATE_SYNC_INVALID",
                    $"{statePath}.sync_int_literal",
                    "value state sync does not accept sync_int_literal"
                );
            }
            return;
        }
        if (syncAggregation == "floor_div")
        {
            if (schema.sync_int_literal <= 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_STATE_SYNC_INVALID",
                    $"{statePath}.sync_int_literal",
                    "floor_div state sync requires positive sync_int_literal"
                );
            }
            return;
        }

        EquipmentAbilityContentRegistry.AddError(
            errors,
            "EQA_STATE_SYNC_INVALID",
            $"{statePath}.sync_aggregation",
            $"state sync_aggregation {syncAggregation} is not supported"
        );
    }

    private static void ValidateTemporalProgressModifiers(
        EquipmentAbilityBindingImportModel binding,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        var seenIds = new HashSet<StringName>();
        foreach (EquipmentTemporalProgressModifierImportModel modifier in binding.temporal_progress_modifiers)
        {
            if (modifier == null)
                continue;
            StringName modifierId = ProgressionDataUtils.to_string_name(modifier.modifier_id);
            string modifierPath = $"{path}.temporal_progress_modifiers[{modifierId}]";
            if (modifierId == "")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_ID_MISSING",
                    modifierPath,
                    "temporal progress modifier requires modifier_id"
                );
            }
            else if (!seenIds.Add(modifierId))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_DUPLICATE",
                    modifierPath,
                    $"temporal progress modifier {modifierId} is duplicated"
                );
            }
            if (!modifier.applies_to_action_progress && !modifier.applies_to_cast_progress)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_SCOPE_INVALID",
                    modifierPath,
                    "temporal progress modifier must apply to action progress or cast progress"
                );
            }
            if (modifier.save_dc <= 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_DC_INVALID",
                    $"{modifierPath}.save_dc",
                    "temporal progress modifier save_dc must be positive"
                );
            }
            if (modifier.attribute_modifier_id == "")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_ATTRIBUTE_INVALID",
                    $"{modifierPath}.attribute_modifier_id",
                    "temporal progress modifier requires attribute_modifier_id"
                );
            }
            if (modifier.success_rate_percent <= 0 || modifier.failure_rate_percent <= 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_RATE_INVALID",
                    modifierPath,
                    "temporal progress modifier rates must be positive percentages"
                );
            }
            else if (
                modifier.success_rate_percent
                    > ActionCadenceContentRules.MaxTemporalProgressRatePercent
                || modifier.failure_rate_percent
                    > ActionCadenceContentRules.MaxTemporalProgressRatePercent
            )
            {
                // 超过上限会让最快阈值的单位在单 step 内跨两次阈值，吞掉一次行动。
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_RATE_TOO_HIGH",
                    modifierPath,
                    $"temporal progress modifier rates must not exceed "
                        + $"{ActionCadenceContentRules.MaxTemporalProgressRatePercent}%"
                );
            }
        }
    }

    private static void ValidateCognitionCeilingModifiers(
        EquipmentAbilityBindingImportModel binding,
        List<string> errors
    )
    {
        string path =
            EquipmentAbilityContentRegistry.BindingPath(binding);
        var seenIds = new HashSet<StringName>();
        foreach (
            EquipmentCognitionCeilingModifierImportModel modifier
            in binding.cognition_ceiling_modifiers
        )
        {
            if (modifier == null)
                continue;
            StringName modifierId =
                ProgressionDataUtils.to_string_name(
                    modifier.modifier_id
                );
            string modifierPath =
                $"{path}.cognition_ceiling_modifiers[{modifierId}]";
            if (modifierId == "")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_COGNITION_CEILING_MODIFIER_ID_MISSING",
                    modifierPath,
                    "cognition ceiling modifier requires modifier_id"
                );
            }
            else if (!seenIds.Add(modifierId))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_COGNITION_CEILING_MODIFIER_DUPLICATE",
                    modifierPath,
                    $"cognition ceiling modifier {modifierId} is duplicated"
                );
            }
            BattleCognitionKind cognitionCeiling =
                BattleCognitionContentRules.ToKind(
                    modifier.cognition_ceiling
                );
            if (
                !BattleCognitionContentRules.IsKnown(
                    cognitionCeiling
                )
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_COGNITION_CEILING_INVALID",
                    $"{modifierPath}.cognition_ceiling",
                    "cognition_ceiling must be mindless, instinctive, or sapient"
                );
            }
        }
    }

    private void ValidateReactions(
        EquipmentAbilityBindingImportModel binding,
        EquipmentAbilityContentValidationContext context,
        HashSet<StringName> declaredStateKeys,
        List<string> errors
    )
    {
        foreach (EquipmentAbilityReactionImportModel reaction in binding.reactions)
        {
            if (reaction == null)
                continue;
            string path = $"{EquipmentAbilityContentRegistry.BindingPath(binding)}.reactions[{EquipmentAbilityContentRegistry.ReactionLabel(reaction)}]";
            bool triggerParsed = EquipmentAbilityDefinitionProjection.TryParseTrigger(reaction.trigger, out EquipmentAbilityTriggerKind trigger);
            bool timingParsed = EquipmentAbilityDefinitionProjection.TryParseTiming(reaction.timing, out EquipmentAbilityTimingKind timing);
            if (!triggerParsed)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_TRIGGER_UNKNOWN_ID",
                    $"{path}.trigger",
                    $"trigger {reaction.trigger} is not registered"
                );
            }
            if (!timingParsed)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_TIMING_UNKNOWN_ID",
                    $"{path}.timing",
                    $"timing {reaction.timing} is not registered"
                );
            }
            if (triggerParsed && timingParsed)
                ValidateTriggerTiming(trigger, timing, path, errors);
            if (reaction.requires_player_confirmation)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_REACTION_CONFIRMATION_UNSUPPORTED",
                    path,
                    "requires_player_confirmation is not supported in V1"
                );
            }
            ValidateConditionGroup(
                reaction.condition_group,
                $"{path}.condition_group",
                context,
                errors
            );
            ValidateProjectedEffectCategories(reaction, path, errors);

            foreach (EquipmentAbilityActionImportModel action in reaction.actions)
            {
                ValidateAction(action, path, context, declaredStateKeys, trigger, errors);
            }
            ValidateOutcomeTable(
                reaction.outcome_table,
                $"{path}.outcome_table",
                context,
                declaredStateKeys,
                trigger,
                errors
            );
        }
    }

    private static void ValidateProjectedEffectCategories(
        EquipmentAbilityReactionImportModel reaction,
        string path,
        List<string> errors
    )
    {
        if (reaction?.projected_effect_categories == null)
            return;

        var declared = new HashSet<StringName>();
        for (int index = 0; index < reaction.projected_effect_categories.Count; index++)
        {
            StringName category = ProgressionDataUtils.to_string_name(
                reaction.projected_effect_categories[index]
            );
            if (category == "")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_PROJECTED_EFFECT_CATEGORY_EMPTY",
                    $"{path}.projected_effect_categories[{index}]",
                    "projected effect category must be non-empty"
                );
            }
            else if (
                CombatEffectCategoryContentRules.IsDerivedProjectileCategory(category)
                || CombatEffectCategoryContentRules.IsRemovedProjectileCategory(category)
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_PROJECTED_EFFECT_CATEGORY_RESERVED",
                    $"{path}.projected_effect_categories[{index}]",
                    $"projected effect category {category} is owned by typed projectile_kind"
                );
            }
            else if (!declared.Add(category))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_PROJECTED_EFFECT_CATEGORY_DUPLICATE",
                    $"{path}.projected_effect_categories[{index}]",
                    $"projected effect category {category} is duplicated"
                );
            }
        }

        if (declared.Count == 0)
            return;

        var required = new HashSet<StringName>();
        AppendRequiredProjectedEffectCategories(reaction.actions, required);
        foreach (
            EquipmentOutcomeEntryImportModel entry
            in reaction.outcome_table?.entries ?? Array.Empty<EquipmentOutcomeEntryImportModel>()
        )
            AppendRequiredProjectedEffectCategories(entry?.actions, required);
        foreach (StringName category in required)
        {
            if (declared.Contains(category))
                continue;
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_PROJECTED_EFFECT_CATEGORY_MISSING",
                $"{path}.projected_effect_categories",
                $"projected effect categories must include {category} required by the reaction payload"
            );
        }
    }

    private static void AppendRequiredProjectedEffectCategories(
        IEnumerable<EquipmentAbilityActionImportModel> actions,
        HashSet<StringName> required
    )
    {
        if (actions == null || required == null)
            return;
        foreach (EquipmentAbilityActionImportModel action in actions)
        {
            if (action?.payload is AddDamageDiceActionPayloadImportModel bonusDamage)
            {
                AppendRequiredProjectedDamageCategories(
                    bonusDamage.damage_type,
                    bonusDamage.damage_tags,
                    required
                );
            }
            else if (action?.payload is DealDamageActionPayloadImportModel directDamage)
            {
                AppendRequiredProjectedDamageCategories(
                    directDamage.damage_type,
                    directDamage.damage_tags,
                    required
                );
            }
            else if (action?.payload is ApplyStatusActionPayloadImportModel status)
            {
                AppendRequiredProjectedCategories("", status.save_tag, required);
            }
        }
    }

    private static void AppendRequiredProjectedDamageCategories(
        StringName damageType,
        IEnumerable<string> damageTags,
        HashSet<StringName> required
    )
    {
        AppendRequiredProjectedCategories(damageType, "", required);
        foreach (string damageTag in damageTags ?? Array.Empty<string>())
            AppendRequiredProjectedCategories(damageTag, "", required);
    }

    private static void AppendRequiredProjectedCategories(
        StringName damageTag,
        StringName saveTag,
        HashSet<StringName> required
    )
    {
        foreach (
            StringName category in CombatEffectCategoryContentRules.RequiredEffectCategories(
                damageTag,
                saveTag,
                BattleEffectKind.Unknown
            )
        )
        {
            if (category != "")
                required.Add(category);
        }
    }

    private void ValidateConditionGroup(
        EquipmentAbilityConditionGroupImportModel group,
        string path,
        EquipmentAbilityContentValidationContext context,
        List<string> errors
    )
    {
        if (group == null)
            return;
        foreach (EquipmentAbilityConditionImportModel condition in group.conditions)
        {
            if (condition == null)
                continue;
            string conditionPath = $"{path}.conditions[{condition.condition_id}]";
            if (!_conditionSpecs.TryGetValue(condition.kind, out EquipmentAbilityHandlerSpec spec))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_HANDLER_UNKNOWN_ID",
                    conditionPath,
                    $"condition handler {condition.kind} is not registered"
                );
                continue;
            }
            if (condition.payload == null || !spec.PayloadImportModelType.IsInstanceOfType(condition.payload))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_HANDLER_PAYLOAD_TYPE_MISMATCH",
                    conditionPath,
                    $"condition {condition.kind} payload type does not match spec"
                );
                continue;
            }
            if (condition.payload is HasStatusConditionPayloadImportModel statusPayload)
            {
                ValidateStatusReference(
                    statusPayload.status_id,
                    context,
                    $"{conditionPath}.payload.status_id",
                    errors
                );
            }
            else if (condition.payload is CompareFactConditionPayloadImportModel comparePayload)
            {
                ValidateFactQuery(
                    comparePayload.left,
                    context,
                    $"{conditionPath}.payload.left",
                    errors
                );
                ValidateFactQuery(
                    comparePayload.right,
                    context,
                    $"{conditionPath}.payload.right",
                    errors
                );
            }
        }
        foreach (EquipmentAbilityConditionGroupImportModel child in group.groups)
        {
            if (child == null)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_CONDITION_GROUP_TYPE_INVALID",
                    $"{path}.groups",
                    "nested condition group must not be null"
                );
                continue;
            }
            ValidateConditionGroup(child, $"{path}.groups", context, errors);
        }
    }

    private void ValidateAction(
        EquipmentAbilityActionImportModel action,
        string reactionPath,
        EquipmentAbilityContentValidationContext context,
        HashSet<StringName> declaredStateKeys,
        EquipmentAbilityTriggerKind trigger,
        List<string> errors
    )
    {
        if (action == null)
            return;
        string path = $"{reactionPath}.actions[{action.action_id}]";
        if (!_actionSpecs.TryGetValue(action.kind, out EquipmentAbilityHandlerSpec spec))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_HANDLER_UNKNOWN_ID",
                path,
                $"action handler {action.kind} is not registered"
            );
            return;
        }
        if (action.payload == null || !spec.PayloadImportModelType.IsInstanceOfType(action.payload))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_HANDLER_PAYLOAD_TYPE_MISMATCH",
                path,
                $"action {action.kind} payload type does not match spec"
            );
            return;
        }
        ValidateStateAccessContracts(spec.StateAccess, action.payload, declaredStateKeys, path, errors);

        switch (action.payload)
        {
            case AddDamageDiceActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateAddDamageDicePayload(payload, context, path, errors);
                break;
            case ImmediateWeaponAttackActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateImmediateWeaponAttackPayload(payload, context, path, errors);
                break;
            case DealDamageActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateDealDamagePayload(payload, context, path, errors);
                break;
            case HealActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateHealPayload(payload, path, errors);
                break;
            case HealFromFactActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateHealFromFactPayload(payload, context, path, errors);
                break;
            case AttackRollBonusActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateAttackRollBonusPayload(payload, path, errors);
                break;
            case AttackRollAdvantageActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateAttackRollAdvantagePayload(payload, path, errors);
                break;
            case CriticalHitOverrideActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateCriticalHitOverridePayload(payload, path, errors);
                break;
            case EquipmentAttackDefenseModifierImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateAttackDefenseModifierPayload(payload, path, errors);
                break;
            case DamageRollModeOverrideActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateDamageRollModeOverridePayload(payload, path, errors);
                break;
            case DamageReductionActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateDamageReductionPayload(payload, context, path, errors);
                break;
            case LootQuantityMultiplierActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateLootQuantityMultiplierPayload(payload, path, errors);
                break;
            case ApplyStatusActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateApplyStatusPayload(payload, context, path, errors);
                break;
            case ModifyActionPointsActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateModifyActionPointsPayload(payload, context, path, errors);
                break;
            case ScheduleAreaEffectActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateScheduleAreaEffectPayload(payload, context, path, errors);
                break;
            case ApplyBattleTerrainEffectAfterCheckActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateApplyBattleTerrainEffectAfterCheckPayload(payload, path, errors);
                break;
            case ApplyEdgeFeatureActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateApplyEdgeFeaturePayload(payload, path, errors);
                break;
            case ModifyAbilityStateActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateModifyAbilityStatePayload(payload, path, errors);
                break;
            case MarkTargetActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateMarkTargetPayload(payload, context, path, errors);
                break;
            case ClearStatusActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateClearStatusPayload(payload, context, path, errors);
                break;
            case TriggerSkillActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateTriggerSkillPayload(payload, context, path, errors);
                break;
            case SummonUnitsActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateSummonUnitsPayload(payload, context, path, errors);
                break;
            case ConsumeSummonedUnitsActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateConsumeSummonedUnitsPayload(payload, path, errors);
                break;
            case ConsumeStatusStacksActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateConsumeStatusStacksPayload(payload, context, path, errors);
                break;
            case SummonedUnitAttackRollModifierActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateSummonedUnitAttackRollModifierPayload(payload, path, errors);
                break;
            case EquipmentDurabilityDamageActionPayloadImportModel payload:
                EquipmentAbilityPayloadValidators.ValidateDurabilityPayload(payload, context, path, errors);
                break;
        }
        ValidateConditionGroup(action.condition_group, $"{path}.condition_group", context, errors);
    }

    private void ValidateOutcomeTable(
        EquipmentOutcomeTableImportModel table,
        string path,
        EquipmentAbilityContentValidationContext context,
        HashSet<StringName> declaredStateKeys,
        EquipmentAbilityTriggerKind trigger,
        List<string> errors
    )
    {
        if (table == null)
            return;
        int index = 0;
        foreach (EquipmentOutcomeEntryImportModel entry in table.entries)
        {
            if (entry == null)
            {
                index++;
                continue;
            }
            string entryPath = $"{path}.entries[{index}]";
            foreach (EquipmentAbilityActionImportModel action in entry.actions)
                ValidateAction(action, entryPath, context, declaredStateKeys, trigger, errors);
            index++;
        }
    }

    private static void ValidateStateAccessContracts(
        EquipmentAbilityStateAccessSpec stateAccess,
        IEquipmentAbilityPayloadImportModel payload,
        HashSet<StringName> declaredStateKeys,
        string path,
        List<string> errors
    )
    {
        if (stateAccess == null)
            return;
        ValidateStateAccessContracts(stateAccess.Reads, payload, declaredStateKeys, path, errors);
        ValidateStateAccessContracts(stateAccess.Writes, payload, declaredStateKeys, path, errors);
        ValidateStateAccessContracts(stateAccess.Creates, payload, declaredStateKeys, path, errors);
        ValidateStateAccessContracts(stateAccess.Clears, payload, declaredStateKeys, path, errors);
    }

    private static void ValidateStateAccessContracts(
        IReadOnlyList<EquipmentAbilityStateContract> contracts,
        IEquipmentAbilityPayloadImportModel payload,
        HashSet<StringName> declaredStateKeys,
        string path,
        List<string> errors
    )
    {
        if (contracts == null || contracts.Count == 0)
            return;
        foreach (EquipmentAbilityStateContract contract in contracts)
        {
            if (contract == null || !contract.StateKeyMustBeDeclaredInBinding)
                continue;
            if (EquipmentAbilityPayloadValidators.ReadStringNamePayloadMember(payload, "binding_id") != "")
                continue;
            StringName stateKey = contract.StateKey;
            if (stateKey == "" && !string.IsNullOrWhiteSpace(contract.StateKeyPayloadMemberName))
                stateKey = EquipmentAbilityPayloadValidators.ReadStringNamePayloadMember(payload, contract.StateKeyPayloadMemberName);
            EquipmentAbilityPayloadValidators.ValidateDeclaredStateKey(stateKey, declaredStateKeys, path, errors);
        }
    }

    private void ValidateFatalIntercepts(
        EquipmentAbilityBindingImportModel binding,
        EquipmentAbilityContentValidationContext context,
        HashSet<StringName> declaredStateKeys,
        List<string> errors
    )
    {
        string bindingPath = EquipmentAbilityContentRegistry.BindingPath(binding);
        var seenIds = new HashSet<StringName>();
        var seenOrders = new HashSet<int>();
        foreach (
            EquipmentFatalInterceptImportModel intercept
            in binding.fatal_intercepts ?? Array.Empty<EquipmentFatalInterceptImportModel>()
        )
        {
            if (intercept == null)
                continue;
            StringName interceptId = ProgressionDataUtils.to_string_name(intercept.intercept_id);
            string path = $"{bindingPath}.fatal_intercepts[{interceptId}]";
            if (interceptId == "")
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_FATAL_INTERCEPT_ID_MISSING",
                    path,
                    "fatal intercept requires intercept_id"
                );
            }
            else if (!seenIds.Add(interceptId))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_FATAL_INTERCEPT_ID_DUPLICATE",
                    path,
                    $"fatal intercept {interceptId} is duplicated"
                );
            }

            if (intercept.resolution_order < 0 || !seenOrders.Add(intercept.resolution_order))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_FATAL_INTERCEPT_ORDER_INVALID",
                    $"{path}.resolution_order",
                    "resolution_order must be non-negative and unique within the binding"
                );
            }
            if (intercept.protection_priority <= 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_FATAL_INTERCEPT_PRIORITY_INVALID",
                    $"{path}.protection_priority",
                    "protection_priority must be positive"
                );
            }

            bool usageParsed = EquipmentAbilityUsagePeriodKinds.TryParse(
                intercept.usage_period_kind,
                out EquipmentAbilityUsagePeriodKind usagePeriodKind
            );
            if (
                !usageParsed
                || (
                    usagePeriodKind != EquipmentAbilityUsagePeriodKind.PerBattle
                    && !EquipmentAbilityUsagePeriodKinds.IsPersistentWorldPeriod(
                        usagePeriodKind
                    )
                )
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_FATAL_INTERCEPT_USAGE_PERIOD_UNSUPPORTED",
                    $"{path}.usage_period_kind",
                    "fatal intercept usage_period_kind must be per_battle, per_world_day, or per_world_month"
                );
            }
            else if (
                binding.activation_status_id != ""
                && EquipmentAbilityUsagePeriodKinds.IsPersistentWorldPeriod(usagePeriodKind)
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_STATUS_FATAL_PERSISTENT_USAGE_UNSUPPORTED",
                    $"{path}.usage_period_kind",
                    "status-activated fatal intercepts require per_battle usage"
                );
            }
            if (intercept.max_attempts_per_period <= 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_FATAL_INTERCEPT_USAGE_LIMIT_INVALID",
                    $"{path}.max_attempts_per_period",
                    "max_attempts_per_period must be positive"
                );
            }

            ValidateFatalInterceptRollGate(intercept.roll_gate, $"{path}.roll_gate", errors);
            bool recoveryParsed = EquipmentAbilityDefinitionProjection.TryParseFatalInterceptRecoveryKind(
                intercept.recovery_kind,
                out EquipmentFatalInterceptRecoveryKind recoveryKind
            );
            if (!recoveryParsed)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_FATAL_INTERCEPT_RECOVERY_KIND_UNSUPPORTED",
                    $"{path}.recovery_kind",
                    "recovery_kind must be hp_dice or max_hp_percent"
                );
                continue;
            }

            if (recoveryKind == EquipmentFatalInterceptRecoveryKind.HpDice)
            {
                ValidateFatalInterceptDice(
                    intercept.recovery_dice,
                    $"{path}.recovery_dice",
                    errors
                );
                if (intercept.recovery_percent_basis_points != 0)
                {
                    EquipmentAbilityContentRegistry.AddError(
                        errors,
                        "EQA_FATAL_INTERCEPT_RECOVERY_SHAPE_INVALID",
                        path,
                        "hp_dice recovery must not declare recovery_percent_basis_points"
                    );
                }
            }
            else
            {
                if (intercept.recovery_dice != null)
                {
                    EquipmentAbilityContentRegistry.AddError(
                        errors,
                        "EQA_FATAL_INTERCEPT_RECOVERY_SHAPE_INVALID",
                        path,
                        "max_hp_percent recovery must not declare recovery_dice"
                    );
                }
                if (
                    intercept.recovery_percent_basis_points <= 0
                    || intercept.recovery_percent_basis_points > 10000
                )
                {
                    EquipmentAbilityContentRegistry.AddError(
                        errors,
                        "EQA_FATAL_INTERCEPT_RECOVERY_PERCENT_INVALID",
                        $"{path}.recovery_percent_basis_points",
                        "recovery_percent_basis_points must be within 1..10000"
                    );
                }
            }

            int actionIndex = 0;
            foreach (
                EquipmentAbilityActionImportModel action
                in intercept.success_actions
                    ?? Array.Empty<EquipmentAbilityActionImportModel>()
            )
            {
                string actionPath = $"{path}.success_actions[{actionIndex}]";
                if (
                    action != null
                    && action.kind != "apply_status"
                    && action.kind != "trigger_skill"
                )
                {
                    EquipmentAbilityContentRegistry.AddError(
                        errors,
                        "EQA_FATAL_INTERCEPT_SUCCESS_ACTION_UNSUPPORTED",
                        actionPath,
                        "fatal intercept success actions support apply_status or trigger_skill"
                    );
                }
                ValidateAction(
                    action,
                    $"{path}.success_actions",
                    context,
                    declaredStateKeys,
                    EquipmentAbilityTriggerKind.OnDamageTakenFinalized,
                    errors
                );
                actionIndex++;
            }
        }
    }

    private static void ValidateMitigationAuras(
        EquipmentAbilityBindingImportModel binding,
        List<string> errors
    )
    {
        string bindingPath = EquipmentAbilityContentRegistry.BindingPath(binding);
        var seenIds = new HashSet<StringName>();
        foreach (
            EquipmentMitigationAuraImportModel aura
            in binding.mitigation_auras
                ?? Array.Empty<EquipmentMitigationAuraImportModel>()
        )
        {
            if (aura == null)
                continue;
            StringName auraId = ProgressionDataUtils.to_string_name(aura.aura_id);
            string path = $"{bindingPath}.mitigation_auras[{auraId}]";
            if (auraId == "" || !seenIds.Add(auraId))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MITIGATION_AURA_ID_INVALID",
                    $"{path}.aura_id",
                    "mitigation aura requires a unique non-empty aura_id"
                );
            }
            if (aura.radius < 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MITIGATION_AURA_RADIUS_INVALID",
                    $"{path}.radius",
                    "mitigation aura radius must be non-negative"
                );
            }
            if (!CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(aura.target_team_filter))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MITIGATION_AURA_TARGET_FILTER_INVALID",
                    $"{path}.target_team_filter",
                    $"mitigation aura target_team_filter must be one of {CombatTargetTeamContentRules.ValidSkillTargetTeamFilterLabel()}"
                );
            }
            if (DamageTagContentRules.ToDamageTagKind(aura.damage_tag) == DamageTagKind.Unknown)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MITIGATION_AURA_DAMAGE_TAG_INVALID",
                    $"{path}.damage_tag",
                    $"mitigation aura damage_tag must be one of {DamageTagContentRules.ValidDamageTagLabel()}"
                );
            }
            DamageMitigationTierKind tier = DamageTagContentRules.ToMitigationTierKind(
                aura.mitigation_tier
            );
            if (
                tier == DamageMitigationTierKind.Unknown
                || tier == DamageMitigationTierKind.Normal
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MITIGATION_AURA_TIER_INVALID",
                    $"{path}.mitigation_tier",
                    "mitigation aura tier must be half, double, or immune"
                );
            }
        }
    }

    private static void ValidateMovementTrails(
        EquipmentAbilityBindingImportModel binding,
        EquipmentAbilityContentValidationContext context,
        List<string> errors
    )
    {
        string bindingPath = EquipmentAbilityContentRegistry.BindingPath(binding);
        var seenIds = new HashSet<StringName>();
        foreach (
            EquipmentMovementTrailImportModel trail
            in binding.movement_trails
                ?? Array.Empty<EquipmentMovementTrailImportModel>()
        )
        {
            if (trail == null)
                continue;
            StringName trailId = ProgressionDataUtils.to_string_name(trail.trail_id);
            string path = $"{bindingPath}.movement_trails[{trailId}]";
            if (trailId == "" || !seenIds.Add(trailId))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MOVEMENT_TRAIL_ID_INVALID",
                    $"{path}.trail_id",
                    "movement trail requires a unique non-empty trail_id"
                );
            }
            if (trail.priority < 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MOVEMENT_TRAIL_PRIORITY_INVALID",
                    $"{path}.priority",
                    "movement trail priority must be non-negative"
                );
            }
            if (trail.duration_tu <= 0 || trail.duration_tu % 5 != 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MOVEMENT_TRAIL_DURATION_INVALID",
                    $"{path}.duration_tu",
                    "movement trail duration_tu must be positive and aligned to 5 TU"
                );
            }
            if (!CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(trail.target_team_filter))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MOVEMENT_TRAIL_TARGET_FILTER_INVALID",
                    $"{path}.target_team_filter",
                    $"movement trail target_team_filter must be one of {CombatTargetTeamContentRules.ValidSkillTargetTeamFilterLabel()}"
                );
            }
            ValidateFatalInterceptDice(trail.damage_dice, $"{path}.damage_dice", errors);
            if (trail.damage_dice?.terms?.Count != 1)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MOVEMENT_TRAIL_DICE_INVALID",
                    $"{path}.damage_dice",
                    "movement trail damage requires exactly one fixed dice term"
                );
            }
            if (DamageTagContentRules.ToDamageTagKind(trail.damage_tag) == DamageTagKind.Unknown)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MOVEMENT_TRAIL_DAMAGE_TAG_INVALID",
                    $"{path}.damage_tag",
                    $"movement trail damage_tag must be one of {DamageTagContentRules.ValidDamageTagLabel()}"
                );
            }
            foreach (
                string damageTag
                in trail.damage_tags ?? Array.Empty<string>()
            )
            {
                if (DamageTagContentRules.ToDamageTagKind(damageTag) != DamageTagKind.Unknown)
                    continue;
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_MOVEMENT_TRAIL_DAMAGE_TAG_INVALID",
                    $"{path}.damage_tags[{damageTag}]",
                    $"movement trail damage tag {damageTag} is not known"
                );
            }
            if (trail.required_skill_id != "")
            {
                ValidateSkillReference(
                    trail.required_skill_id,
                    context,
                    $"{path}.required_skill_id",
                    errors
                );
            }
        }
    }

    private static void ValidateFatalInterceptRollGate(
        EquipmentRollGateImportModel rollGate,
        string path,
        List<string> errors
    )
    {
        if (rollGate == null)
            return;
        if (ProgressionDataUtils.to_string_name(rollGate.rng_stream) == "")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_FATAL_INTERCEPT_ROLL_GATE_INVALID",
                $"{path}.rng_stream",
                "fatal intercept roll_gate requires rng_stream"
            );
        }
        ValidateFatalInterceptDice(rollGate.roll, $"{path}.roll", errors);
        StringName compare = ProgressionDataUtils.to_string_name(rollGate.compare);
        if (
            compare != "lte"
            && compare != "lt"
            && compare != "gte"
            && compare != "gt"
            && compare != "eq"
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_FATAL_INTERCEPT_ROLL_GATE_INVALID",
                $"{path}.compare",
                "fatal intercept roll_gate compare must be lte, lt, gte, gt, or eq"
            );
        }
    }

    private static void ValidateFatalInterceptDice(
        DiceExpressionImportModel dice,
        string path,
        List<string> errors
    )
    {
        if (dice == null || dice.terms == null || dice.terms.Count == 0)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_FATAL_INTERCEPT_DICE_INVALID",
                path,
                "fatal intercept dice requires at least one term"
            );
            return;
        }
        if (dice.flat_bonus < 0 || ProgressionDataUtils.to_string_name(dice.preview_policy) != "")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_FATAL_INTERCEPT_DICE_INVALID",
                path,
                "fatal intercept dice requires non-negative flat_bonus and no preview_policy"
            );
        }
        long totalDice = 0;
        long maximum = Math.Max(dice.flat_bonus, 0);
        foreach (DiceExpressionTermImportModel term in dice.terms)
        {
            if (
                term == null
                || term.dice_count <= 0
                || term.dice_sides <= 0
                || term.count_bonus_fact != null
                || term.count_bonus_multiplier != 0.0f
                || term.max_dice_count != 0
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_FATAL_INTERCEPT_DICE_INVALID",
                    path,
                    "fatal intercept dice terms require fixed positive count/sides without fact scaling"
                );
                continue;
            }
            totalDice += term.dice_count;
            maximum += (long)term.dice_count * term.dice_sides;
        }
        if (totalDice > 64L || maximum > 10000L)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_FATAL_INTERCEPT_DICE_INVALID",
                path,
                "fatal intercept dice must not exceed 64 dice or a maximum total of 10000"
            );
        }
    }

    private void ValidateGrantedActions(
        EquipmentAbilityBindingImportModel binding,
        EquipmentAbilityContentValidationContext context,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        var seen = new HashSet<StringName>();
        foreach (EquipmentGrantedActionImportModel grant in binding.granted_actions)
        {
            if (grant == null)
                continue;
            string grantPath = $"{path}.granted_actions[{grant.granted_action_id}]";
            if (grant.granted_action_id == "" || !seen.Add(grant.granted_action_id))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_GRANTED_SKILL_COMPOSITION_INVALID",
                    grantPath,
                    "granted_action_id must be non-empty and unique for stable SkillEntryId composition"
                );
            }
            if (!EquipmentAbilityDefinitionProjection.TryParseGrantedKind(grant.granted_kind, out _))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_GRANTED_KIND_UNSUPPORTED",
                    $"{grantPath}.granted_kind",
                    $"granted_kind {grant.granted_kind} is not supported in V1"
                );
            }
            ValidateSkillReference(grant.skill_id, context, $"{grantPath}.skill_id", errors);
            if (grant.skill_id == "" || grant.skill_level <= 0)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_GRANTED_SKILL_COMPOSITION_INVALID",
                    grantPath,
                    "equipment granted skills require skill_id and positive skill_level"
                );
            }
            bool usageKindParsed = EquipmentAbilityUsagePeriodKinds.TryParse(
                grant.usage_period_kind,
                out EquipmentAbilityUsagePeriodKind usagePeriodKind
            );
            if (!usageKindParsed)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_GRANTED_USAGE_PERIOD_UNSUPPORTED",
                    $"{grantPath}.usage_period_kind",
                    $"usage_period_kind {grant.usage_period_kind} is not supported in V1"
                );
            }
            if (
                EquipmentAbilityUsagePeriodKinds.IsLimited(usagePeriodKind)
                && grant.max_uses_per_period <= 0
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_GRANTED_USAGE_LIMIT_INVALID",
                    $"{grantPath}.max_uses_per_period",
                    "limited equipment granted skills require positive max_uses_per_period"
                );
            }
            if (
                !EquipmentAbilityUsagePeriodKinds.IsLimited(usagePeriodKind)
                && grant.max_uses_per_period != 0
            )
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_GRANTED_USAGE_LIMIT_INVALID",
                    $"{grantPath}.max_uses_per_period",
                    "max_uses_per_period requires usage_period_kind"
                );
            }
            ValidateConditionGroup(
                grant.availability_conditions,
                $"{grantPath}.availability_conditions",
                context,
                errors
            );
        }
    }

    private void ValidateWeaponProfileOverlays(
        EquipmentAbilityBindingImportModel binding,
        EquipmentAbilityContentValidationContext context,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        foreach (EquipmentWeaponProfileOverlayImportModel overlay in binding.weapon_profile_overlays)
        {
            if (overlay == null)
                continue;
            string overlayPath = $"{path}.weapon_profile_overlays[{overlay.overlay_id}]";
            ValidateConditionGroup(
                overlay.condition_group,
                $"{overlayPath}.condition_group",
                context,
                errors
            );
        }
    }

    private void ValidateWorldEffects(
        EquipmentAbilityBindingImportModel binding,
        EquipmentAbilityContentValidationContext context,
        HashSet<StringName> declaredStateKeys,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        foreach (EquipmentWorldEffectImportModel effect in binding.world_effects)
        {
            if (effect == null)
                continue;
            string effectPath = $"{path}.world_effects[{effect.world_effect_id}]";
            bool triggerParsed = EquipmentAbilityDefinitionProjection.TryParseTrigger(effect.trigger, out EquipmentAbilityTriggerKind trigger);
            bool timingParsed = EquipmentAbilityDefinitionProjection.TryParseTiming(effect.timing, out EquipmentAbilityTimingKind timing);
            if (!triggerParsed)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_TRIGGER_UNKNOWN_ID",
                    $"{effectPath}.trigger",
                    $"trigger {effect.trigger} is not registered"
                );
            }
            if (!timingParsed)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_TIMING_UNKNOWN_ID",
                    $"{effectPath}.timing",
                    $"timing {effect.timing} is not registered"
                );
            }
            if (triggerParsed && timingParsed)
                ValidateTriggerTiming(trigger, timing, effectPath, errors);
            ValidateConditionGroup(
                effect.condition_group,
                $"{effectPath}.condition_group",
                context,
                errors
            );
            foreach (EquipmentAbilityActionImportModel action in effect.actions)
                ValidateAction(action, effectPath, context, declaredStateKeys, trigger, errors);
        }
    }

    internal static void ValidateStatusReference(
        StringName statusId,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (statusId == "" || !context.KnownStatusIds.Contains(statusId))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_REFERENCE_UNKNOWN_STATUS",
                path,
                $"status_id {statusId} is not known"
            );
        }
    }

    internal static void ValidateFactQuery(
        EquipmentAbilityFactQueryImportModel query,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (query == null)
            return;
        if (!EquipmentAbilityClosedVocabulary.IsKnownFactQueryKind(query.query_kind))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_FACT_QUERY_KIND_UNKNOWN",
                $"{path}.query_kind",
                $"fact query kind {query.query_kind} is not registered"
            );
            return;
        }
        if (query.query_kind == "fact" && !EquipmentAbilityClosedVocabulary.IsKnownFactId(query.fact_id))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_FACT_ID_UNKNOWN",
                $"{path}.fact_id",
                $"fact_id {query.fact_id} is not registered"
            );
            return;
        }
        if (!EquipmentAbilityClosedVocabulary.IsKnownFactSubject(query.subject))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_FACT_SUBJECT_UNKNOWN",
                $"{path}.subject",
                $"fact subject {query.subject} is not registered"
            );
        }
        if (!EquipmentAbilityClosedVocabulary.IsKnownFactAggregation(query.aggregation))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_FACT_AGGREGATION_UNKNOWN",
                $"{path}.aggregation",
                $"fact aggregation {query.aggregation} is not registered"
            );
        }
        if (!EquipmentAbilityClosedVocabulary.IsKnownFactValueKind(query.value_kind))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_FACT_VALUE_KIND_UNKNOWN",
                $"{path}.value_kind",
                $"fact value kind {query.value_kind} is not registered"
            );
        }
        if (query.fact_id == "status_stacks" || query.fact_id == "source_status_total_stacks")
        {
            ValidateStatusReference(query.status_id, context, $"{path}.status_id", errors);
            return;
        }
        if (query.fact_id == "attribute_value" && query.attribute_id == "")
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_FACT_ATTRIBUTE_ID_MISSING",
                $"{path}.attribute_id",
                "attribute_value fact requires attribute_id"
            );
        }
    }

    internal static void ValidateSkillReference(
        StringName skillId,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (skillId == "" || !context.KnownSkillIds.Contains(skillId))
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_REFERENCE_UNKNOWN_SKILL",
                path,
                $"skill_id {skillId} is not known"
            );
        }
    }

    internal static void ValidateAutomaticSkillReference(
        StringName skillId,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (
            skillId != ""
            && context?.WindupSkillIds != null
            && context.WindupSkillIds.Contains(skillId)
        )
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_REFERENCE_WINDUP_SKILL_UNSUPPORTED",
                path,
                $"skill_id {skillId} requires manual windup tier selection and cannot be triggered automatically"
            );
        }
    }

    private void ValidateTriggerTiming(
        EquipmentAbilityTriggerKind trigger,
        EquipmentAbilityTimingKind timing,
        string path,
        List<string> errors
    )
    {
        if (
            _triggerTimingSpecs.TryGetValue(trigger, out EquipmentAbilityTriggerTimingSpec spec)
            && spec.AllowedTimings.Contains(timing)
        )
        {
            return;
        }
        EquipmentAbilityContentRegistry.AddError(
            errors,
            "EQA_TRIGGER_TIMING_UNSUPPORTED",
            path,
            $"trigger {trigger} does not support timing {timing}"
        );
    }
}
