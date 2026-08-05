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
        EquipmentAbilityBindingDef binding,
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
        HashSet<StringName> declaredStateKeys = ValidateStateSchemas(binding, errors);
        ValidateReactions(binding, context, declaredStateKeys, errors);
        ValidateGrantedActions(binding, context, errors);
        ValidateTemporalProgressModifiers(binding, errors);
        ValidateCognitionCeilingModifiers(binding, errors);
        ValidateWeaponProfileOverlays(binding, context, errors);
        ValidateWorldEffects(binding, context, declaredStateKeys, errors);
    }

    private static void ValidateSourceKinds(
        EquipmentAbilityBindingDef binding,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        foreach (StringName sourceKind in binding.allowed_source_kinds)
        {
            TraitSourceKind parsed = TraitContentRules.ToSourceKind(sourceKind);
            if (parsed != TraitSourceKind.EquipmentFixed && parsed != TraitSourceKind.EquipmentRoll)
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

    private static HashSet<StringName> ValidateStateSchemas(
        EquipmentAbilityBindingDef binding,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        var keys = new HashSet<StringName>();
        foreach (EquipmentAbilityStateSchemaDef schema in binding.state_schemas)
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
        foreach (EquipmentAbilityStateSchemaDef schema in binding.state_schemas)
            ValidateStateSchemaSync(schema, keys, path, errors);
        return keys;
    }

    private static void ValidateStateSchemaSync(
        EquipmentAbilityStateSchemaDef schema,
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
        EquipmentAbilityBindingDef binding,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        var seenIds = new HashSet<StringName>();
        foreach (EquipmentTemporalProgressModifierDef modifier in binding.temporal_progress_modifiers)
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
        }
    }

    private static void ValidateCognitionCeilingModifiers(
        EquipmentAbilityBindingDef binding,
        List<string> errors
    )
    {
        string path =
            EquipmentAbilityContentRegistry.BindingPath(binding);
        var seenIds = new HashSet<StringName>();
        foreach (
            EquipmentCognitionCeilingModifierDef modifier
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
        EquipmentAbilityBindingDef binding,
        EquipmentAbilityContentValidationContext context,
        HashSet<StringName> declaredStateKeys,
        List<string> errors
    )
    {
        foreach (EquipmentAbilityReactionDef reaction in binding.reactions)
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

            foreach (EquipmentAbilityActionDef action in reaction.actions)
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
        EquipmentAbilityReactionDef reaction,
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
        foreach (EquipmentOutcomeEntryDef entry in reaction.outcome_table?.entries ?? new())
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
        IEnumerable<EquipmentAbilityActionDef> actions,
        HashSet<StringName> required
    )
    {
        if (actions == null || required == null)
            return;
        foreach (EquipmentAbilityActionDef action in actions)
        {
            if (action?.payload is AddDamageDiceActionPayloadDef bonusDamage)
            {
                AppendRequiredProjectedDamageCategories(
                    bonusDamage.damage_type,
                    bonusDamage.damage_tags,
                    required
                );
            }
            else if (action?.payload is DealDamageActionPayloadDef directDamage)
            {
                AppendRequiredProjectedDamageCategories(
                    directDamage.damage_type,
                    directDamage.damage_tags,
                    required
                );
            }
            else if (action?.payload is ApplyStatusActionPayloadDef status)
            {
                AppendRequiredProjectedCategories("", status.save_tag, required);
            }
        }
    }

    private static void AppendRequiredProjectedDamageCategories(
        StringName damageType,
        IEnumerable<StringName> damageTags,
        HashSet<StringName> required
    )
    {
        AppendRequiredProjectedCategories(damageType, "", required);
        foreach (StringName damageTag in damageTags ?? Array.Empty<StringName>())
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
        EquipmentAbilityConditionGroupDef group,
        string path,
        EquipmentAbilityContentValidationContext context,
        List<string> errors
    )
    {
        if (group == null)
            return;
        foreach (EquipmentAbilityConditionDef condition in group.conditions)
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
            if (condition.payload == null || !spec.PayloadResourceType.IsInstanceOfType(condition.payload))
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_HANDLER_PAYLOAD_TYPE_MISMATCH",
                    conditionPath,
                    $"condition {condition.kind} payload type does not match spec"
                );
                continue;
            }
            if (condition.payload is HasStatusConditionPayloadDef statusPayload)
            {
                ValidateStatusReference(
                    statusPayload.status_id,
                    context,
                    $"{conditionPath}.payload.status_id",
                    errors
                );
            }
            else if (condition.payload is CompareFactConditionPayloadDef comparePayload)
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
        foreach (Resource childResource in group.groups)
        {
            if (childResource is not EquipmentAbilityConditionGroupDef child)
            {
                EquipmentAbilityContentRegistry.AddError(
                    errors,
                    "EQA_CONDITION_GROUP_TYPE_INVALID",
                    $"{path}.groups",
                    "nested condition group must use EquipmentAbilityConditionGroupDef"
                );
                continue;
            }
            ValidateConditionGroup(child, $"{path}.groups", context, errors);
        }
    }

    private void ValidateAction(
        EquipmentAbilityActionDef action,
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
        if (action.payload == null || !spec.PayloadResourceType.IsInstanceOfType(action.payload))
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
        if (trigger == EquipmentAbilityTriggerKind.OnBattleEnd && spec.MutationPolicy == EquipmentAbilityMutationPolicyKind.Mutating)
        {
            EquipmentAbilityContentRegistry.AddError(
                errors,
                "EQA_BATTLE_END_MUTATION_UNSUPPORTED",
                path,
                "on_battle_end mutating actions require staged commit fields not present in the V1 static gate"
            );
        }

        switch (action.payload)
        {
            case AddDamageDiceActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateAddDamageDicePayload(payload, context, path, errors);
                break;
            case ImmediateWeaponAttackActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateImmediateWeaponAttackPayload(payload, context, path, errors);
                break;
            case DealDamageActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateDealDamagePayload(payload, context, path, errors);
                break;
            case HealActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateHealPayload(payload, path, errors);
                break;
            case HealFromFactActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateHealFromFactPayload(payload, context, path, errors);
                break;
            case AttackRollBonusActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateAttackRollBonusPayload(payload, path, errors);
                break;
            case AttackRollAdvantageActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateAttackRollAdvantagePayload(payload, path, errors);
                break;
            case CriticalHitOverrideActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateCriticalHitOverridePayload(payload, path, errors);
                break;
            case EquipmentAttackDefenseModifierDef payload:
                EquipmentAbilityPayloadValidators.ValidateAttackDefenseModifierPayload(payload, path, errors);
                break;
            case DamageRollModeOverrideActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateDamageRollModeOverridePayload(payload, path, errors);
                break;
            case DamageReductionActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateDamageReductionPayload(payload, context, path, errors);
                break;
            case LootQuantityMultiplierActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateLootQuantityMultiplierPayload(payload, path, errors);
                break;
            case ApplyStatusActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateApplyStatusPayload(payload, context, path, errors);
                break;
            case ModifyActionPointsActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateModifyActionPointsPayload(payload, context, path, errors);
                break;
            case ScheduleAreaEffectActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateScheduleAreaEffectPayload(payload, context, path, errors);
                break;
            case ApplyBattleTerrainEffectAfterCheckActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateApplyBattleTerrainEffectAfterCheckPayload(payload, path, errors);
                break;
            case ApplyEdgeFeatureActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateApplyEdgeFeaturePayload(payload, path, errors);
                break;
            case ModifyAbilityStateActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateModifyAbilityStatePayload(payload, path, errors);
                break;
            case MarkTargetActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateMarkTargetPayload(payload, context, path, errors);
                break;
            case ClearStatusActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateClearStatusPayload(payload, context, path, errors);
                break;
            case TriggerSkillActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateTriggerSkillPayload(payload, context, path, errors);
                break;
            case GrantSkillActionPayloadDef payload:
                ValidateSkillReference(payload.skill_id, context, $"{path}.payload.skill_id", errors);
                if (payload.skill_id == "" || payload.skill_level <= 0)
                    EquipmentAbilityContentRegistry.AddError(errors, "EQA_ACTION_REQUIRED_FIELD_MISSING", path, "grant_skill requires skill_id and positive skill_level");
                break;
            case SummonUnitsActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateSummonUnitsPayload(payload, context, path, errors);
                break;
            case ConsumeSummonedUnitsActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateConsumeSummonedUnitsPayload(payload, path, errors);
                break;
            case ConsumeStatusStacksActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateConsumeStatusStacksPayload(payload, context, path, errors);
                break;
            case SummonedUnitAttackRollModifierActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateSummonedUnitAttackRollModifierPayload(payload, path, errors);
                break;
            case EquipmentDurabilityDamageActionPayloadDef payload:
                EquipmentAbilityPayloadValidators.ValidateDurabilityPayload(payload, context, path, errors);
                break;
        }
        ValidateConditionGroup(action.condition_group, $"{path}.condition_group", context, errors);
    }

    private void ValidateOutcomeTable(
        EquipmentOutcomeTableDef table,
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
        foreach (EquipmentOutcomeEntryDef entry in table.entries)
        {
            if (entry == null)
            {
                index++;
                continue;
            }
            string entryPath = $"{path}.entries[{index}]";
            foreach (EquipmentAbilityActionDef action in entry.actions)
                ValidateAction(action, entryPath, context, declaredStateKeys, trigger, errors);
            index++;
        }
    }

    private static void ValidateStateAccessContracts(
        EquipmentAbilityStateAccessSpec stateAccess,
        Resource payload,
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
        Resource payload,
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

    private void ValidateGrantedActions(
        EquipmentAbilityBindingDef binding,
        EquipmentAbilityContentValidationContext context,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        var seen = new HashSet<StringName>();
        foreach (EquipmentGrantedActionDef grant in binding.granted_actions)
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
        EquipmentAbilityBindingDef binding,
        EquipmentAbilityContentValidationContext context,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        foreach (EquipmentWeaponProfileOverlayDef overlay in binding.weapon_profile_overlays)
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
        EquipmentAbilityBindingDef binding,
        EquipmentAbilityContentValidationContext context,
        HashSet<StringName> declaredStateKeys,
        List<string> errors
    )
    {
        string path = EquipmentAbilityContentRegistry.BindingPath(binding);
        foreach (EquipmentWorldEffectDef effect in binding.world_effects)
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
            foreach (EquipmentAbilityActionDef action in effect.actions)
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
        EquipmentAbilityFactQueryDef query,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (query == null)
            return;
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
