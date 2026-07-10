using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Godot;

internal sealed class EquipmentAbilityContentRegistry : IDisposable
{
    private const int TuGranularity = 5;
    private static readonly StringName StatusStackRefresh = "refresh";
    private static readonly StringName StatusStackAdd = "add";

    private readonly Dictionary<StringName, EquipmentAbilityContentPackDefinition> _packsById = new();
    private readonly Dictionary<StringName, EquipmentAbilityBindingDefinition> _bindingsById = new();
    private readonly Dictionary<StringName, List<EquipmentAbilityBindingDefinition>> _bindingsByTraitId = new();
    private readonly IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> _conditionSpecs =
        EquipmentAbilityBuiltInHandlerSpecs.BuildConditionSpecs();
    private readonly IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> _actionSpecs =
        EquipmentAbilityBuiltInHandlerSpecs.BuildActionSpecs();
    private readonly IReadOnlyDictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec> _triggerTimingSpecs =
        EquipmentAbilityBuiltInHandlerSpecs.BuildTriggerTimingSpecs();

    private EquipmentAbilityRegistryBuildResult _lastBuildResult = new()
    {
        Success = true,
        Revision = 0,
        Errors = Array.Empty<string>(),
    };
    private int _revision;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Clear();
        GC.SuppressFinalize(this);
    }

    public int GetRevision() => _revision;

    public EquipmentAbilityRegistryBuildResult GetLastBuildResultTyped() => _lastBuildResult;

    public IReadOnlyDictionary<StringName, EquipmentAbilityContentPackDefinition> GetPackDefinitionsTyped()
    {
        return Snapshot(_packsById);
    }

    public IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> GetBindingDefinitionsTyped()
    {
        return Snapshot(_bindingsById);
    }

    public IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> GetConditionHandlerSpecsTyped() =>
        _conditionSpecs;

    public IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> GetActionHandlerSpecsTyped() =>
        _actionSpecs;

    public IReadOnlyDictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec> GetTriggerTimingSpecsTyped() =>
        _triggerTimingSpecs;

    public EquipmentAbilityRegistryBuildResult Rebuild(
        IReadOnlyList<EquipmentAbilityContentPackDef> packs,
        EquipmentAbilityContentValidationContext validationContext
    )
    {
        _revision++;
        var errors = new List<string>();
        validationContext ??= new EquipmentAbilityContentValidationContext();
        List<EquipmentAbilityContentPackDef> sortedPacks = SortPacks(packs, errors);
        var nextPacks = new Dictionary<StringName, EquipmentAbilityContentPackDefinition>();
        var nextBindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>();
        var nextByTrait = new Dictionary<StringName, List<EquipmentAbilityBindingDefinition>>();

        if (errors.Count == 0)
        {
            foreach (EquipmentAbilityContentPackDef pack in sortedPacks)
            {
                string packPath = PackPath(pack);
                var projectedBindings = new List<EquipmentAbilityBindingDefinition>();
                foreach (EquipmentAbilityBindingDef binding in pack.bindings)
                {
                    if (binding == null)
                    {
                        AddError(
                            errors,
                            "EQA_BINDING_NULL",
                            $"{packPath}.bindings",
                            "binding entry must not be null"
                        );
                        continue;
                    }

                    ValidateBinding(binding, validationContext, nextBindings, errors);
                    EquipmentAbilityBindingDefinition definition = ProjectBinding(binding);
                    projectedBindings.Add(definition);

                    if (errors.Count > 0)
                    {
                        continue;
                    }

                    if (definition.OverrideMode == EquipmentAbilityBindingOverrideMode.ReplaceBinding)
                    {
                        if (nextBindings.TryGetValue(definition.ReplacesBindingId, out var replaced))
                        {
                            nextBindings.Remove(definition.ReplacesBindingId);
                            RemoveTraitIndex(nextByTrait, replaced);
                        }
                    }
                    nextBindings[definition.BindingId] = definition;
                    AddTraitIndex(nextByTrait, definition);
                }

                if (errors.Count == 0)
                {
                    EquipmentAbilityContentPackDefinition packDefinition = ProjectPack(
                        pack,
                        projectedBindings
                    );
                    nextPacks[packDefinition.PackId] = packDefinition;
                }
            }
        }

        bool success = errors.Count == 0;
        if (success)
        {
            _packsById.Clear();
            foreach ((StringName key, EquipmentAbilityContentPackDefinition value) in nextPacks)
                _packsById[key] = value;
            _bindingsById.Clear();
            foreach ((StringName key, EquipmentAbilityBindingDefinition value) in nextBindings)
                _bindingsById[key] = value;
            _bindingsByTraitId.Clear();
            foreach ((StringName key, List<EquipmentAbilityBindingDefinition> value) in nextByTrait)
                _bindingsByTraitId[key] = value;
        }

        _lastBuildResult = new EquipmentAbilityRegistryBuildResult
        {
            Success = success,
            Revision = _revision,
            Errors = new ReadOnlyCollection<string>(errors),
        };
        return _lastBuildResult;
    }

    public IReadOnlyList<EquipmentAbilityBindingDefinition> FindBindings(
        StringName traitId,
        TraitSourceKind sourceKind,
        IReadOnlySet<StringName> traitCategories,
        ItemDef sourceItem
    )
    {
        if (traitId == "" || sourceKind == TraitSourceKind.Unknown)
            return Array.Empty<EquipmentAbilityBindingDefinition>();
        if (!_bindingsByTraitId.TryGetValue(traitId, out List<EquipmentAbilityBindingDefinition> candidates))
            return Array.Empty<EquipmentAbilityBindingDefinition>();
        return EquipmentAbilityBindingMatcher.FindBindings(
            candidates,
            traitId,
            sourceKind,
            traitCategories,
            sourceItem
        );
    }

    private void Clear()
    {
        _packsById.Clear();
        _bindingsById.Clear();
        _bindingsByTraitId.Clear();
        _lastBuildResult = new EquipmentAbilityRegistryBuildResult
        {
            Success = true,
            Revision = _revision,
            Errors = Array.Empty<string>(),
        };
    }

    private static List<EquipmentAbilityContentPackDef> SortPacks(
        IReadOnlyList<EquipmentAbilityContentPackDef> packs,
        List<string> errors
    )
    {
        var input = new List<EquipmentAbilityContentPackDef>();
        if (packs == null || packs.Count == 0)
            return input;

        var byId = new Dictionary<StringName, EquipmentAbilityContentPackDef>();
        foreach (EquipmentAbilityContentPackDef pack in packs)
        {
            if (pack == null)
            {
                AddError(errors, "EQA_PACK_NULL", "equipment_ability.packs", "pack must not be null");
                continue;
            }
            if (pack.pack_id == "")
            {
                AddError(
                    errors,
                    "EQA_PACK_MISSING_ID",
                    "equipment_ability.packs[<missing>]",
                    "pack_id is required"
                );
                continue;
            }
            if (pack.schema_version != 1)
            {
                AddError(
                    errors,
                    "EQA_PACK_SCHEMA_VERSION_UNSUPPORTED",
                    PackPath(pack),
                    "schema_version must be exactly 1"
                );
            }
            if (byId.ContainsKey(pack.pack_id))
            {
                AddError(
                    errors,
                    "EQA_PACK_DUPLICATE_ID",
                    PackPath(pack),
                    $"duplicate pack_id {pack.pack_id}"
                );
                continue;
            }
            byId[pack.pack_id] = pack;
            input.Add(pack);
        }

        foreach (EquipmentAbilityContentPackDef pack in input)
        {
            foreach (StringName dependency in pack.dependencies)
            {
                if (dependency == "" || !byId.ContainsKey(dependency))
                {
                    AddError(
                        errors,
                        "EQA_PACK_DEPENDENCY_MISSING",
                        $"{PackPath(pack)}.dependencies[{dependency}]",
                        $"missing dependency {dependency}"
                    );
                }
            }
        }
        if (errors.Count > 0)
            return input;

        var result = new List<EquipmentAbilityContentPackDef>();
        var emitted = new HashSet<StringName>();
        while (result.Count < input.Count)
        {
            var candidates = new List<EquipmentAbilityContentPackDef>();
            foreach (EquipmentAbilityContentPackDef pack in input)
            {
                if (emitted.Contains(pack.pack_id))
                    continue;
                bool ready = true;
                foreach (StringName dependency in pack.dependencies)
                {
                    if (!emitted.Contains(dependency))
                    {
                        ready = false;
                        break;
                    }
                }
                if (ready)
                    candidates.Add(pack);
            }
            if (candidates.Count == 0)
            {
                AddError(
                    errors,
                    "EQA_PACK_DEPENDENCY_CYCLE",
                    "equipment_ability.packs",
                    "pack dependency graph contains a cycle"
                );
                return input;
            }
            candidates.Sort(ComparePackOrder);
            EquipmentAbilityContentPackDef next = candidates[0];
            emitted.Add(next.pack_id);
            result.Add(next);
        }
        return result;
    }

    private static int ComparePackOrder(
        EquipmentAbilityContentPackDef left,
        EquipmentAbilityContentPackDef right
    )
    {
        int loadOrderCompare = left.load_order.CompareTo(right.load_order);
        return loadOrderCompare != 0
            ? loadOrderCompare
            : string.CompareOrdinal(left.pack_id.ToString(), right.pack_id.ToString());
    }

    private void ValidateBinding(
        EquipmentAbilityBindingDef binding,
        EquipmentAbilityContentValidationContext context,
        IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> loadedBindings,
        List<string> errors
    )
    {
        string path = BindingPath(binding);
        if (binding.binding_id == "")
        {
            AddError(errors, "EQA_BINDING_MISSING_ID", path, "binding_id is required");
        }
        if (binding.trait_id == "" || !ContainsValue(context.KnownTraitIds, binding.trait_id))
        {
            AddError(
                errors,
                "EQA_REFERENCE_MISSING_TRAIT",
                path,
                $"trait_id {binding.trait_id} is not known"
            );
        }
        if (!TryParseOverrideMode(binding.override_mode, out EquipmentAbilityBindingOverrideMode mode))
        {
            AddError(
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
                AddError(
                    errors,
                    "EQA_BINDING_REPLACE_ID_UNEXPECTED",
                    $"{path}.replaces_binding_id",
                    "add bindings must not declare replaces_binding_id"
                );
            }
            if (binding.binding_id != "" && loadedBindings.ContainsKey(binding.binding_id))
            {
                AddError(
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
            AddError(
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
            AddError(
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
        ValidateWeaponProfileOverlays(binding, context, errors);
        ValidateWorldEffects(binding, context, declaredStateKeys, errors);
    }

    private static void ValidateSourceKinds(
        EquipmentAbilityBindingDef binding,
        List<string> errors
    )
    {
        string path = BindingPath(binding);
        foreach (StringName sourceKind in binding.allowed_source_kinds)
        {
            TraitSourceKind parsed = TraitContentRules.ToSourceKind(sourceKind);
            if (parsed != TraitSourceKind.EquipmentFixed && parsed != TraitSourceKind.EquipmentRoll)
            {
                AddError(
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
        string path = BindingPath(binding);
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
                AddError(
                    errors,
                    "EQA_STATE_RESET_POLICY_UNSUPPORTED",
                    $"{path}.state_schemas[{schema.state_key}].reset_timing",
                    $"reset_timing {schema.reset_timing} is not supported in V1"
                );
            }
            if (persistentReset && schema.owner_scope != "equipment_instance")
            {
                AddError(
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
                AddError(
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
            AddError(
                errors,
                "EQA_STATE_SYNC_INVALID",
                $"{statePath}.sync_source_state_key",
                "state sync source cannot be the target state itself"
            );
        }
        if (!declaredStateKeys.Contains(sourceStateKey))
        {
            AddError(
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
                AddError(
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
                AddError(
                    errors,
                    "EQA_STATE_SYNC_INVALID",
                    $"{statePath}.sync_int_literal",
                    "floor_div state sync requires positive sync_int_literal"
                );
            }
            return;
        }

        AddError(
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
        string path = BindingPath(binding);
        var seenIds = new HashSet<StringName>();
        foreach (EquipmentTemporalProgressModifierDef modifier in binding.temporal_progress_modifiers)
        {
            if (modifier == null)
                continue;
            StringName modifierId = ProgressionDataUtils.to_string_name(modifier.modifier_id);
            string modifierPath = $"{path}.temporal_progress_modifiers[{modifierId}]";
            if (modifierId == "")
            {
                AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_ID_MISSING",
                    modifierPath,
                    "temporal progress modifier requires modifier_id"
                );
            }
            else if (!seenIds.Add(modifierId))
            {
                AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_DUPLICATE",
                    modifierPath,
                    $"temporal progress modifier {modifierId} is duplicated"
                );
            }
            if (!modifier.applies_to_action_progress && !modifier.applies_to_cast_progress)
            {
                AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_SCOPE_INVALID",
                    modifierPath,
                    "temporal progress modifier must apply to action progress or cast progress"
                );
            }
            if (modifier.save_dc <= 0)
            {
                AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_DC_INVALID",
                    $"{modifierPath}.save_dc",
                    "temporal progress modifier save_dc must be positive"
                );
            }
            if (modifier.attribute_modifier_id == "")
            {
                AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_ATTRIBUTE_INVALID",
                    $"{modifierPath}.attribute_modifier_id",
                    "temporal progress modifier requires attribute_modifier_id"
                );
            }
            if (modifier.success_rate_percent <= 0 || modifier.failure_rate_percent <= 0)
            {
                AddError(
                    errors,
                    "EQA_TEMPORAL_PROGRESS_MODIFIER_RATE_INVALID",
                    modifierPath,
                    "temporal progress modifier rates must be positive percentages"
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
            string path = $"{BindingPath(binding)}.reactions[{ReactionLabel(reaction)}]";
            bool triggerParsed = TryParseTrigger(reaction.trigger, out EquipmentAbilityTriggerKind trigger);
            bool timingParsed = TryParseTiming(reaction.timing, out EquipmentAbilityTimingKind timing);
            if (!triggerParsed)
            {
                AddError(
                    errors,
                    "EQA_TRIGGER_UNKNOWN_ID",
                    $"{path}.trigger",
                    $"trigger {reaction.trigger} is not registered"
                );
            }
            if (!timingParsed)
            {
                AddError(
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
                AddError(
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
                AddError(
                    errors,
                    "EQA_HANDLER_UNKNOWN_ID",
                    conditionPath,
                    $"condition handler {condition.kind} is not registered"
                );
                continue;
            }
            if (condition.payload == null || !spec.PayloadResourceType.IsInstanceOfType(condition.payload))
            {
                AddError(
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
        foreach (EquipmentAbilityConditionGroupDef child in group.groups)
            ValidateConditionGroup(child, $"{path}.groups", context, errors);
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
            AddError(
                errors,
                "EQA_HANDLER_UNKNOWN_ID",
                path,
                $"action handler {action.kind} is not registered"
            );
            return;
        }
        if (action.payload == null || !spec.PayloadResourceType.IsInstanceOfType(action.payload))
        {
            AddError(
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
            AddError(
                errors,
                "EQA_BATTLE_END_MUTATION_UNSUPPORTED",
                path,
                "on_battle_end mutating actions require staged commit fields not present in the V1 static gate"
            );
        }

        switch (action.payload)
        {
            case AddDamageDiceActionPayloadDef payload:
                ValidateAddDamageDicePayload(payload, context, path, errors);
                break;
            case ImmediateWeaponAttackActionPayloadDef payload:
                ValidateImmediateWeaponAttackPayload(payload, context, path, errors);
                break;
            case DealDamageActionPayloadDef payload:
                ValidateDealDamagePayload(payload, context, path, errors);
                break;
            case HealActionPayloadDef payload:
                ValidateHealPayload(payload, path, errors);
                break;
            case HealFromFactActionPayloadDef payload:
                ValidateHealFromFactPayload(payload, context, path, errors);
                break;
            case AttackRollBonusActionPayloadDef payload:
                ValidateAttackRollBonusPayload(payload, path, errors);
                break;
            case AttackRollAdvantageActionPayloadDef payload:
                ValidateAttackRollAdvantagePayload(payload, path, errors);
                break;
            case CriticalHitOverrideActionPayloadDef payload:
                ValidateCriticalHitOverridePayload(payload, path, errors);
                break;
            case EquipmentAttackDefenseModifierDef payload:
                ValidateAttackDefenseModifierPayload(payload, path, errors);
                break;
            case DamageRollModeOverrideActionPayloadDef payload:
                ValidateDamageRollModeOverridePayload(payload, path, errors);
                break;
            case DamageReductionActionPayloadDef payload:
                ValidateDamageReductionPayload(payload, context, path, errors);
                break;
            case LootQuantityMultiplierActionPayloadDef payload:
                ValidateLootQuantityMultiplierPayload(payload, path, errors);
                break;
            case ApplyStatusActionPayloadDef payload:
                ValidateApplyStatusPayload(payload, context, path, errors);
                break;
            case ModifyActionPointsActionPayloadDef payload:
                ValidateModifyActionPointsPayload(payload, context, path, errors);
                break;
            case ScheduleAreaEffectActionPayloadDef payload:
                ValidateScheduleAreaEffectPayload(payload, context, path, errors);
                break;
            case ApplyBattleTerrainEffectAfterCheckActionPayloadDef payload:
                ValidateApplyBattleTerrainEffectAfterCheckPayload(payload, path, errors);
                break;
            case ApplyEdgeFeatureActionPayloadDef payload:
                ValidateApplyEdgeFeaturePayload(payload, path, errors);
                break;
            case ModifyAbilityStateActionPayloadDef payload:
                ValidateModifyAbilityStatePayload(payload, path, errors);
                break;
            case MarkTargetActionPayloadDef payload:
                ValidateMarkTargetPayload(payload, context, path, errors);
                break;
            case ClearStatusActionPayloadDef payload:
                ValidateClearStatusPayload(payload, context, path, errors);
                break;
            case TriggerSkillActionPayloadDef payload:
                ValidateTriggerSkillPayload(payload, context, path, errors);
                break;
            case GrantSkillActionPayloadDef payload:
                ValidateSkillReference(payload.skill_id, context, $"{path}.payload.skill_id", errors);
                if (payload.skill_id == "" || payload.skill_level <= 0)
                    AddError(errors, "EQA_ACTION_REQUIRED_FIELD_MISSING", path, "grant_skill requires skill_id and positive skill_level");
                break;
            case SummonUnitsActionPayloadDef payload:
                ValidateSummonUnitsPayload(payload, context, path, errors);
                break;
            case ConsumeSummonedUnitsActionPayloadDef payload:
                ValidateConsumeSummonedUnitsPayload(payload, path, errors);
                break;
            case ConsumeStatusStacksActionPayloadDef payload:
                ValidateConsumeStatusStacksPayload(payload, context, path, errors);
                break;
            case SummonedUnitAttackRollModifierActionPayloadDef payload:
                ValidateSummonedUnitAttackRollModifierPayload(payload, path, errors);
                break;
            case EquipmentDurabilityDamageActionPayloadDef payload:
                ValidateDurabilityPayload(payload, context, path, errors);
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
            if (ReadStringNamePayloadMember(payload, "binding_id") != "")
                continue;
            StringName stateKey = contract.StateKey;
            if (stateKey == "" && !string.IsNullOrWhiteSpace(contract.StateKeyPayloadMemberName))
                stateKey = ReadStringNamePayloadMember(payload, contract.StateKeyPayloadMemberName);
            ValidateDeclaredStateKey(stateKey, declaredStateKeys, path, errors);
        }
    }

    private static StringName ReadStringNamePayloadMember(Resource payload, string memberName)
    {
        if (payload == null || string.IsNullOrWhiteSpace(memberName))
            return "";
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        Type type = payload.GetType();
        PropertyInfo property = type.GetProperty(memberName, flags);
        object raw = property != null ? property.GetValue(payload) : null;
        if (raw == null)
        {
            FieldInfo field = type.GetField(memberName, flags);
            raw = field != null ? field.GetValue(payload) : null;
        }
        if (raw is StringName name)
            return name;
        if (raw is string text)
            return new StringName(text);
        return "";
    }

    private static void ValidateAddDamageDicePayload(
        AddDamageDiceActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        bool hasDiceTerm = payload.dice != null && payload.dice.terms.Count > 0;
        bool hasFlatBonus = payload.dice != null && payload.dice.flat_bonus > 0;
        if (
            payload.target_selector == ""
            || payload.damage_type == ""
            || payload.dice == null
            || (!hasDiceTerm && !hasFlatBonus)
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "add_damage_dice requires target_selector, damage_type, and dice terms or positive flat_bonus"
            );
        }
        if (HasKnownValues(context.KnownDamageTypes) && !context.KnownDamageTypes.Contains(payload.damage_type))
        {
            AddError(
                errors,
                "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
                $"{path}.payload.damage_type",
                $"damage_type {payload.damage_type} is not known"
            );
        }
        ValidateDamageTagArray(
            payload.damage_tags,
            context,
            $"{path}.payload.damage_tags",
            errors
        );
        ValidateMitigationBypassArrays(
            payload.mitigation_bypass_damage_tags,
            payload.mitigation_bypass_tiers,
            context,
            path,
            errors
        );
        if (payload.dice != null)
        {
            foreach (DiceExpressionTermDef term in payload.dice.terms)
            {
                if (term == null || term.dice_count <= 0 || term.dice_sides <= 0)
                {
                    AddError(
                        errors,
                        "EQA_DICE_INVALID",
                        $"{path}.payload.dice",
                        "dice terms must have positive dice_count and dice_sides"
                    );
                }
            }
        }
    }

    private static void ValidateImmediateWeaponAttackPayload(
        ImmediateWeaponAttackActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (
            payload.anchor_selector == ""
            || payload.target_team_filter == ""
            || payload.radius < 0
            || payload.max_attacks <= 0
            || payload.skill_id == ""
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "immediate_weapon_attack requires anchor_selector, target_team_filter, non-negative radius, positive max_attacks, and skill_id"
            );
        }
        ValidateSkillReference(payload.skill_id, context, $"{path}.payload.skill_id", errors);
        StringName filter = ProgressionDataUtils.to_string_name(payload.target_team_filter);
        if (filter != "enemy" && filter != "ally" && filter != "any")
        {
            AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.target_team_filter",
                "immediate_weapon_attack target_team_filter must be enemy, ally, or any"
            );
        }
    }

    private static void ValidateDealDamagePayload(
        DealDamageActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        bool hasDiceTerm = payload.dice != null && payload.dice.terms.Count > 0;
        bool hasFlatBonus = payload.dice != null && payload.dice.flat_bonus > 0;
        if (
            payload.target_selector == ""
            || payload.damage_type == ""
            || payload.dice == null
            || (!hasDiceTerm && !hasFlatBonus)
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "deal_damage requires target_selector, damage_type, and dice terms or positive flat_bonus"
            );
        }
        if (HasKnownValues(context.KnownDamageTypes) && !context.KnownDamageTypes.Contains(payload.damage_type))
        {
            AddError(
                errors,
                "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
                $"{path}.payload.damage_type",
                $"damage_type {payload.damage_type} is not known"
            );
        }
        ValidateDamageTagArray(
            payload.damage_tags,
            context,
            $"{path}.payload.damage_tags",
            errors
        );
        ValidateMitigationBypassArrays(
            payload.mitigation_bypass_damage_tags,
            payload.mitigation_bypass_tiers,
            context,
            path,
            errors
        );
        if (payload.dice != null)
        {
            foreach (DiceExpressionTermDef term in payload.dice.terms)
            {
                if (term == null || term.dice_count <= 0 || term.dice_sides <= 0)
                {
                    AddError(
                        errors,
                        "EQA_DICE_INVALID",
                        $"{path}.payload.dice",
                        "dice terms must have positive dice_count and dice_sides"
                    );
                }
            }
        }
    }

    private static void ValidateHealPayload(
        HealActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        bool hasDiceTerm = payload.dice != null && payload.dice.terms.Count > 0;
        bool hasFlatBonus = payload.dice != null && payload.dice.flat_bonus > 0;
        if (payload.target_selector == "" || payload.dice == null || (!hasDiceTerm && !hasFlatBonus))
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "heal requires target_selector and dice terms or positive flat_bonus"
            );
        }
        if (payload.dice != null)
        {
            foreach (DiceExpressionTermDef term in payload.dice.terms)
            {
                if (term == null || term.dice_count <= 0 || term.dice_sides <= 0)
                {
                    AddError(
                        errors,
                        "EQA_DICE_INVALID",
                        $"{path}.payload.dice",
                        "dice terms must have positive dice_count and dice_sides"
                    );
                }
            }
        }
    }

    private static void ValidateHealFromFactPayload(
        HealFromFactActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.amount_fact == null || payload.multiplier_percent <= 0)
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "heal_from_fact requires target_selector, amount_fact and positive multiplier_percent"
            );
        }
        ValidateFactQuery(payload.amount_fact, context, $"{path}.payload.amount_fact", errors);
        if (payload.max_amount < 0)
        {
            AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.max_amount",
                "heal_from_fact max_amount must be zero or positive"
            );
        }
    }

    private static void ValidateDamageTagArray(
        Godot.Collections.Array<StringName> damageTags,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (damageTags == null || damageTags.Count == 0)
            return;
        for (int index = 0; index < damageTags.Count; index++)
        {
            StringName damageTag = ProgressionDataUtils.to_string_name(damageTags[index]);
            if (damageTag == "")
            {
                AddError(
                    errors,
                    "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
                    $"{path}[{index}]",
                    "damage tag must be non-empty"
                );
                continue;
            }
            if (HasKnownValues(context.KnownDamageTypes) && !context.KnownDamageTypes.Contains(damageTag))
            {
                AddError(
                    errors,
                    "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
                    $"{path}[{index}]",
                    $"damage tag {damageTag} is not known"
                );
            }
        }
    }

    private static void ValidateMitigationBypassArrays(
        Godot.Collections.Array<StringName> damageTags,
        Godot.Collections.Array<StringName> tiers,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        int tagCount = damageTags?.Count ?? 0;
        int tierCount = tiers?.Count ?? 0;
        if (tagCount == 0 && tierCount == 0)
            return;
        if (tagCount == 0 || tierCount == 0)
        {
            AddError(
                errors,
                "EQA_MITIGATION_BYPASS_INCOMPLETE",
                $"{path}.payload",
                "mitigation bypass requires both mitigation_bypass_damage_tags and mitigation_bypass_tiers"
            );
        }
        ValidateDamageTagArray(
            damageTags,
            context,
            $"{path}.payload.mitigation_bypass_damage_tags",
            errors
        );
        for (int index = 0; index < tierCount; index++)
        {
            StringName tier = ProgressionDataUtils.to_string_name(tiers[index]);
            if (
                DamageTagContentRules.ToMitigationTierKind(tier)
                == DamageMitigationTierKind.Unknown
            )
            {
                AddError(
                    errors,
                    "EQA_MITIGATION_BYPASS_TIER_INVALID",
                    $"{path}.payload.mitigation_bypass_tiers[{index}]",
                    $"mitigation bypass tier {tier} is not supported"
                );
            }
        }
    }

    private static void ValidateAttackRollBonusPayload(
        AttackRollBonusActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        bool hasDynamicBonus =
            ProgressionDataUtils.to_string_name(payload.attribute_modifier_id) != "";
        if (payload.target_selector == "" || (payload.bonus == 0 && !hasDynamicBonus))
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "attack_roll_bonus requires target_selector and non-zero bonus or attribute_modifier_id"
            );
        }
    }

    private static void ValidateAttackRollAdvantagePayload(
        AttackRollAdvantageActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.mode != "advantage")
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "attack_roll_advantage requires target_selector and mode=advantage"
            );
        }
    }

    private static void ValidateAttackDefenseModifierPayload(
        EquipmentAttackDefenseModifierDef payload,
        string path,
        List<string> errors
    )
    {
        if (
            payload.modifier_id == ""
            || (
                (payload.ignored_ac_components?.Count ?? 0) == 0
                && (payload.ac_component_multipliers?.Count ?? 0) == 0
                && !payload.lock_dodge_bonus
            )
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "attack_defense_modifier requires modifier_id and at least one AC adjustment"
            );
        }

        var ignored = new HashSet<StringName>();
        foreach (StringName componentId in payload.ignored_ac_components ?? new Godot.Collections.Array<StringName>())
        {
            if (!IsKnownAcComponent(componentId))
            {
                AddError(
                    errors,
                    "EQA_ATTACK_DEFENSE_AC_COMPONENT_UNKNOWN",
                    $"{path}.payload.ignored_ac_components[{componentId}]",
                    $"AC component {componentId} is not registered"
                );
                continue;
            }
            ignored.Add(componentId);
        }

        foreach (EquipmentAcComponentMultiplierDef multiplier in payload.ac_component_multipliers ?? new Godot.Collections.Array<EquipmentAcComponentMultiplierDef>())
        {
            if (multiplier == null)
                continue;
            if (!IsKnownAcComponent(multiplier.ac_component_id))
            {
                AddError(
                    errors,
                    "EQA_ATTACK_DEFENSE_AC_COMPONENT_UNKNOWN",
                    $"{path}.payload.ac_component_multipliers[{multiplier.ac_component_id}]",
                    $"AC component {multiplier.ac_component_id} is not registered"
                );
            }
            if (ignored.Contains(multiplier.ac_component_id))
            {
                AddError(
                    errors,
                    "EQA_ATTACK_DEFENSE_AC_COMPONENT_CONFLICT",
                    $"{path}.payload.ac_component_multipliers[{multiplier.ac_component_id}]",
                    $"AC component {multiplier.ac_component_id} cannot be both ignored and multiplied"
                );
            }
            if (multiplier.multiplier_percent < 0 || multiplier.multiplier_percent > 100)
            {
                AddError(
                    errors,
                    "EQA_ATTACK_DEFENSE_MULTIPLIER_INVALID",
                    $"{path}.payload.ac_component_multipliers[{multiplier.ac_component_id}].multiplier_percent",
                    "AC component multiplier percent must be between 0 and 100"
                );
            }
            if (multiplier.stack_mode != "" && multiplier.stack_mode != "min")
            {
                AddError(
                    errors,
                    "EQA_ATTACK_DEFENSE_STACK_MODE_UNSUPPORTED",
                    $"{path}.payload.ac_component_multipliers[{multiplier.ac_component_id}].stack_mode",
                    "AC component multiplier stack_mode must be empty or min"
                );
            }
        }

        bool hasEquipmentFilter =
            (payload.required_target_item_tags?.Count ?? 0) > 0
            || (payload.required_target_equipment_type_ids?.Count ?? 0) > 0;
        if (hasEquipmentFilter && payload.required_target_equipment_selector == "")
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                $"{path}.payload.required_target_equipment_selector",
                "attack_defense_modifier target equipment filters require required_target_equipment_selector"
            );
        }
        if (
            payload.required_target_equipment_selector != ""
            && payload.required_target_equipment_selector != "target_armor"
            && payload.required_target_equipment_selector != "target_shield"
        )
        {
            AddError(
                errors,
                "EQA_ATTACK_DEFENSE_TARGET_EQUIPMENT_SELECTOR_UNSUPPORTED",
                $"{path}.payload.required_target_equipment_selector",
                $"target equipment selector {payload.required_target_equipment_selector} is not supported"
            );
        }
        if (payload.cover_policy != "" && payload.cover_policy != "normal")
        {
            AddError(
                errors,
                "EQA_ATTACK_DEFENSE_COVER_POLICY_UNSUPPORTED",
                $"{path}.payload.cover_policy",
                $"cover policy {payload.cover_policy} is not supported"
            );
        }
        if (
            payload.projectile_obstacle_policy != ""
            && payload.projectile_obstacle_policy != "normal"
        )
        {
            AddError(
                errors,
                "EQA_ATTACK_DEFENSE_PROJECTILE_POLICY_UNSUPPORTED",
                $"{path}.payload.projectile_obstacle_policy",
                $"projectile obstacle policy {payload.projectile_obstacle_policy} is not supported"
            );
        }
    }

    private static void ValidateDamageRollModeOverridePayload(
        DamageRollModeOverrideActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (
            payload.target_selector == ""
            || (
                payload.roll_mode != "random"
                && payload.roll_mode != "average"
                && payload.roll_mode != "maximum"
            )
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "damage_roll_mode_override requires target_selector and roll_mode random/average/maximum"
            );
        }
    }

    private static void ValidateDamageReductionPayload(
        DamageReductionActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (
            payload.target_selector == ""
            || payload.amount <= 0
            || (payload.damage_tags?.Count ?? 0) == 0
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "damage_reduction requires target_selector, positive amount, and at least one damage tag"
            );
        }
        ValidateDamageTagArray(
            payload.damage_tags,
            context,
            $"{path}.payload.damage_tags",
            errors
        );
        if (
            payload.target_selector != ""
            && payload.target_selector != "self"
            && payload.target_selector != "holder"
            && payload.target_selector != "defender"
            && payload.target_selector != "damage_target"
        )
        {
            AddError(
                errors,
                "EQA_DAMAGE_REDUCTION_TARGET_SELECTOR_UNSUPPORTED",
                $"{path}.payload.target_selector",
                $"damage_reduction target_selector {payload.target_selector} is not supported"
            );
        }
    }

    private static void ValidateLootQuantityMultiplierPayload(
        LootQuantityMultiplierActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.multiplier_percent <= 0)
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "loot_quantity_multiplier requires target_selector and positive multiplier_percent"
            );
        }
        foreach (StringName dropKind in payload.affected_drop_kinds)
        {
            if (dropKind == "" || BattleLootIds.ToDropKind(dropKind) == BattleLootDropKind.Unknown)
            {
                AddError(
                    errors,
                    "EQA_LOOT_DROP_KIND_INVALID",
                    $"{path}.payload.affected_drop_kinds",
                    $"loot_quantity_multiplier drop kind {dropKind} is not supported"
                );
            }
        }
    }

    private static void ValidateApplyStatusPayload(
        ApplyStatusActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.status_id == "")
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "apply_status requires target_selector and status_id"
            );
        }
        ValidateStatusReference(payload.status_id, context, $"{path}.payload.status_id", errors);
        ValidateStatusSemanticPayload(
            payload.stack_behavior,
            payload.stack_limit,
            payload.counts_as_debuff_override,
            payload.counts_as_debuff,
            payload.undispellable,
            payload.dispellable_magic,
            payload.dispellable_harmful_magic,
            payload.dispellable_beneficial_magic,
            path,
            "apply_status",
            "",
            errors
        );
        if (payload.source_bound_attack_roll_penalty_min_stacks <= 0)
        {
            AddError(
                errors,
                "EQA_STATUS_SOURCE_BOUND_MIN_STACKS_INVALID",
                $"{path}.payload.source_bound_attack_roll_penalty_min_stacks",
                "apply_status source_bound_attack_roll_penalty_min_stacks must be positive"
            );
        }
        if (payload.source_bound_incoming_attack_roll_bonus_min_stacks <= 0)
        {
            AddError(
                errors,
                "EQA_STATUS_SOURCE_BOUND_MIN_STACKS_INVALID",
                $"{path}.payload.source_bound_incoming_attack_roll_bonus_min_stacks",
                "apply_status source_bound_incoming_attack_roll_bonus_min_stacks must be positive"
            );
        }
        if (payload.heal_multiplier_percent < 0 || payload.heal_multiplier_percent > 100)
        {
            AddError(
                errors,
                "EQA_STATUS_HEAL_MULTIPLIER_INVALID",
                $"{path}.payload.heal_multiplier_percent",
                "apply_status heal_multiplier_percent must be between 0 and 100"
            );
        }
        if (payload.save_dc > 0 && (payload.save_ability == "" || payload.save_tag == ""))
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "apply_status save gate requires save_ability and save_tag when save_dc is positive"
            );
        }
        bool hasTimelineDamageDice =
            payload.timeline_damage_dice_count > 0
            || payload.timeline_damage_dice_sides > 0
            || payload.timeline_damage_flat_bonus > 0;
        if (payload.tick_interval_tu < 0)
        {
            AddError(
                errors,
                "EQA_STATUS_TICK_INVALID",
                $"{path}.payload.tick_interval_tu",
                "apply_status tick_interval_tu must be >= 0"
            );
        }
        if (payload.timeline_damage_dice_count < 0 || payload.timeline_damage_dice_sides < 0)
        {
            AddError(
                errors,
                "EQA_STATUS_TIMELINE_DAMAGE_DICE_INVALID",
                path,
                "apply_status timeline damage dice count/sides must be >= 0"
            );
        }
        if (payload.timeline_damage_flat_bonus < 0)
        {
            AddError(
                errors,
                "EQA_STATUS_TIMELINE_DAMAGE_DICE_INVALID",
                $"{path}.payload.timeline_damage_flat_bonus",
                "apply_status timeline_damage_flat_bonus must be >= 0"
            );
        }
        if (hasTimelineDamageDice)
        {
            if (payload.tick_interval_tu <= 0)
            {
                AddError(
                    errors,
                    "EQA_STATUS_TICK_INVALID",
                    $"{path}.payload.tick_interval_tu",
                    "apply_status timeline damage dice require positive tick_interval_tu"
                );
            }
            if (payload.timeline_damage_dice_count <= 0 || payload.timeline_damage_dice_sides <= 0)
            {
                AddError(
                    errors,
                    "EQA_STATUS_TIMELINE_DAMAGE_DICE_INVALID",
                    path,
                    "apply_status timeline damage dice require positive dice count and sides"
                );
            }
        }
    }

    private static void ValidateModifyActionPointsPayload(
        ModifyActionPointsActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        StringName mode = ProgressionDataUtils.to_string_name(payload.mode);
        if (payload.target_selector == "" || mode == "")
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "modify_action_points requires target_selector and mode"
            );
        }
        if (
            mode != ""
            && mode != "add_base_action_points"
            && mode != "subtract_current_action_points"
            && mode != "restore_current_action_points_capped"
            && mode != "set_next_turn_ap_to_zero"
        )
        {
            AddError(
                errors,
                "EQA_ACTION_MODE_INVALID",
                $"{path}.payload.mode",
                $"modify_action_points mode {mode} is not supported"
            );
        }
        if (payload.amount < 0)
        {
            AddError(
                errors,
                "EQA_ACTION_AMOUNT_INVALID",
                $"{path}.payload.amount",
                "modify_action_points amount must be >= 0"
            );
        }
        if (mode == "subtract_current_action_points" && payload.amount <= 0)
        {
            AddError(
                errors,
                "EQA_ACTION_AMOUNT_INVALID",
                $"{path}.payload.amount",
                "modify_action_points subtract_current_action_points requires positive amount"
            );
        }
        if (mode == "restore_current_action_points_capped" && payload.amount <= 0)
        {
            AddError(
                errors,
                "EQA_ACTION_AMOUNT_INVALID",
                $"{path}.payload.amount",
                "modify_action_points restore_current_action_points_capped requires positive amount"
            );
        }
        if (mode == "set_next_turn_ap_to_zero")
        {
            if (payload.status_id == "")
            {
                AddError(
                    errors,
                    "EQA_ACTION_REQUIRED_FIELD_MISSING",
                    path,
                    "modify_action_points set_next_turn_ap_to_zero requires status_id"
                );
            }
            ValidateStatusReference(payload.status_id, context, $"{path}.payload.status_id", errors);
        }
    }

    private static void ValidateMarkTargetPayload(
        MarkTargetActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.state_key == "")
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "mark_target requires target_selector and state_key"
            );
        }
        if (payload.mirror_status_id != "")
        {
            ValidateStatusReference(
                payload.mirror_status_id,
                context,
                $"{path}.payload.mirror_status_id",
                errors
            );
            ValidateStatusSemanticPayload(
                payload.mirror_status_stack_behavior,
                payload.mirror_status_stack_limit,
                countsAsDebuffOverride: false,
                countsAsDebuff: false,
                undispellable: false,
                dispellableMagic: false,
                dispellableHarmfulMagic: false,
                dispellableBeneficialMagic: false,
                path,
                "mark_target mirror",
                "mirror_status_",
                errors
            );
        }
        foreach (StringName statusId in payload.clear_status_ids_on_replace ?? new Godot.Collections.Array<StringName>())
        {
            ValidateStatusReference(
                statusId,
                context,
                $"{path}.payload.clear_status_ids_on_replace[{statusId}]",
                errors
            );
        }
    }

    private static void ValidateSummonUnitsPayload(
        SummonUnitsActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (
            payload == null
            || payload.state_key == ""
            || payload.count_dice == null
            || payload.count_dice.terms.Count == 0
            || payload.max_living_units <= 0
            || string.IsNullOrWhiteSpace(payload.unit_display_name)
            || payload.hp_max <= 0
            || payload.armor_class <= 0
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "summon_units requires state_key, count_dice terms, max_living_units, unit_display_name, positive hp_max, and positive armor_class"
            );
        }
        if (payload != null && payload.spawn_radius < 0)
        {
            AddError(
                errors,
                "EQA_SUMMON_SPAWN_RADIUS_INVALID",
                $"{path}.payload.spawn_radius",
                "summon_units spawn_radius must be >= 0"
            );
        }
        foreach (StringName skillId in payload?.known_active_skill_ids ?? new Godot.Collections.Array<StringName>())
        {
            StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (normalizedSkillId == "")
            {
                AddError(
                    errors,
                    "EQA_SUMMON_KNOWN_SKILL_ID_EMPTY",
                    $"{path}.payload.known_active_skill_ids",
                    "summon_units known_active_skill_ids cannot contain empty ids"
                );
                continue;
            }
            if (HasKnownValues(context.KnownSkillIds) && !context.KnownSkillIds.Contains(normalizedSkillId))
            {
                AddError(
                    errors,
                    "EQA_REFERENCE_UNKNOWN_SKILL",
                    $"{path}.payload.known_active_skill_ids",
                    $"skill_id {normalizedSkillId} is not known"
                );
            }
        }
        bool hasNaturalWeapon =
            payload != null
            && (
                payload.natural_weapon_profile_type_id != ""
                || payload.natural_weapon_damage_tag != ""
                || payload.natural_weapon_attack_range > 0
                || payload.natural_weapon_damage_dice != null
                || payload.natural_weapon_family != ""
            );
        if (hasNaturalWeapon)
        {
            bool hasOneDiceTerm = payload.natural_weapon_damage_dice?.terms?.Count == 1;
            DiceExpressionTermDef term = hasOneDiceTerm
                ? payload.natural_weapon_damage_dice.terms[0]
                : null;
            if (
                payload.natural_weapon_profile_type_id == ""
                || payload.natural_weapon_damage_tag == ""
                || payload.natural_weapon_attack_range <= 0
                || term == null
                || term.dice_count <= 0
                || term.dice_sides <= 0
            )
            {
                AddError(
                    errors,
                    "EQA_SUMMON_NATURAL_WEAPON_INVALID",
                    $"{path}.payload.natural_weapon_damage_dice",
                    "summon_units natural weapon requires profile type, damage tag, positive attack range, and exactly one positive dice term"
                );
            }
            else if (
                DamageTagContentRules.ToDamageTagKind(payload.natural_weapon_damage_tag)
                == DamageTagKind.Unknown
            )
            {
                AddError(
                    errors,
                    "EQA_REFERENCE_UNKNOWN_DAMAGE_TYPE",
                    $"{path}.payload.natural_weapon_damage_tag",
                    $"damage tag {payload.natural_weapon_damage_tag} is not known"
                );
            }
        }
        foreach (DiceExpressionTermDef term in payload?.count_dice?.terms ?? new Godot.Collections.Array<DiceExpressionTermDef>())
        {
            if (term == null || term.dice_count <= 0 || term.dice_sides <= 0)
            {
                AddError(
                    errors,
                    "EQA_DICE_INVALID",
                    $"{path}.payload.count_dice",
                    "summon_units dice terms must have positive dice_count and dice_sides"
                );
            }
        }
    }

    private static void ValidateConsumeSummonedUnitsPayload(
        ConsumeSummonedUnitsActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (payload == null || payload.state_key == "" || payload.count <= 0)
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "consume_summoned_units requires state_key and positive count"
            );
        }
    }

    private static void ValidateConsumeStatusStacksPayload(
        ConsumeStatusStacksActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload == null || payload.target_selector == "" || payload.status_id == "" || payload.count <= 0)
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "consume_status_stacks requires target_selector, status_id and positive count"
            );
            return;
        }
        ValidateStatusReference(payload.status_id, context, $"{path}.payload.status_id", errors);
        if (payload.selection_mode != "" && payload.selection_mode != "highest_stacks")
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                $"{path}.payload.selection_mode",
                $"consume_status_stacks selection_mode {payload.selection_mode} is not supported"
            );
        }
    }

    private static void ValidateSummonedUnitAttackRollModifierPayload(
        SummonedUnitAttackRollModifierActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (
            payload == null
            || payload.target_selector == ""
            || payload.source_binding_id == ""
            || payload.state_key == ""
            || payload.bonus_per_unit == 0
            || payload.max_absolute_bonus <= 0
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "summoned_unit_attack_roll_modifier requires target_selector, source_binding_id, state_key, non-zero bonus_per_unit, and positive max_absolute_bonus"
            );
        }
        if (payload != null && payload.radius < 0)
        {
            AddError(
                errors,
                "EQA_SUMMON_RADIUS_INVALID",
                $"{path}.payload.radius",
                "summoned_unit_attack_roll_modifier radius must be >= 0"
            );
        }
    }

    private static void ValidateClearStatusPayload(
        ClearStatusActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.status_id == "")
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "clear_status requires target_selector and status_id"
            );
        }
        if (payload.target_selector == "marked_target" || payload.target_selector == "equipment_target_mark")
        {
            if (payload.mark_binding_id == "" || payload.mark_state_key == "")
            {
                AddError(
                    errors,
                    "EQA_ACTION_REQUIRED_FIELD_MISSING",
                    path,
                    "clear_status marked_target selector requires mark_binding_id and mark_state_key"
                );
            }
        }
        ValidateStatusReference(payload.status_id, context, $"{path}.payload.status_id", errors);
    }

    private static void ValidateCriticalHitOverridePayload(
        CriticalHitOverrideActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "")
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "critical_hit_override requires target_selector"
            );
        }
    }

    private static void ValidateTriggerSkillPayload(
        TriggerSkillActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.skill_id == "" || payload.skill_level <= 0 || payload.target_selector == "")
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "trigger_skill requires skill_id, positive skill_level, and target_selector"
            );
        }
        ValidateSkillReference(payload.skill_id, context, $"{path}.payload.skill_id", errors);
    }

    private static void ValidateStatusSemanticPayload(
        StringName stackBehavior,
        int stackLimit,
        bool countsAsDebuffOverride,
        bool countsAsDebuff,
        bool undispellable,
        bool dispellableMagic,
        bool dispellableHarmfulMagic,
        bool dispellableBeneficialMagic,
        string path,
        string ownerLabel,
        string fieldPrefix,
        List<string> errors
    )
    {
        StringName normalizedStackBehavior = ProgressionDataUtils.to_string_name(stackBehavior);
        if (
            normalizedStackBehavior != ""
            && normalizedStackBehavior != StatusStackRefresh
            && normalizedStackBehavior != StatusStackAdd
        )
        {
            AddError(
                errors,
                "EQA_STATUS_STACK_BEHAVIOR_INVALID",
                $"{path}.payload.{fieldPrefix}stack_behavior",
                $"{ownerLabel} status stack_behavior must be refresh or add"
            );
        }
        if (stackLimit < 0)
        {
            AddError(
                errors,
                "EQA_STATUS_STACK_LIMIT_INVALID",
                $"{path}.payload.{fieldPrefix}stack_limit",
                $"{ownerLabel} status stack_limit must be >= 0"
            );
        }
        if (countsAsDebuff && !countsAsDebuffOverride)
        {
            AddError(
                errors,
                "EQA_STATUS_DEBUFF_FLAG_INVALID",
                $"{path}.payload.{fieldPrefix}counts_as_debuff",
                $"{ownerLabel} status counts_as_debuff requires counts_as_debuff_override"
            );
        }
        if (
            undispellable
            && (dispellableMagic || dispellableHarmfulMagic || dispellableBeneficialMagic)
        )
        {
            AddError(
                errors,
                "EQA_STATUS_DISPEL_FLAG_INVALID",
                $"{path}.payload.{fieldPrefix}undispellable",
                $"{ownerLabel} status cannot be both undispellable and dispellable"
            );
        }
    }

    private static void ValidateScheduleAreaEffectPayload(
        ScheduleAreaEffectActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (
            payload.anchor_selector == ""
            || payload.delay_tu <= 0
            || payload.terrain_effect_id == ""
            || payload.area_pattern == ""
            || payload.lifetime_policy == ""
            || payload.effect_type == ""
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "schedule_area_effect requires anchor_selector, positive delay_tu, terrain_effect_id, area_pattern, lifetime_policy, and effect_type"
            );
        }
        if (payload.delay_tu > 0 && payload.delay_tu % TuGranularity != 0)
        {
            AddError(
                errors,
                "EQA_TU_GRANULARITY_INVALID",
                $"{path}.payload.delay_tu",
                $"schedule_area_effect delay_tu must be a multiple of {TuGranularity}"
            );
        }
        if (payload.area_value < 0)
        {
            AddError(
                errors,
                "EQA_AREA_VALUE_INVALID",
                $"{path}.payload.area_value",
                "schedule_area_effect area_value must be >= 0"
            );
        }
        if (!CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(payload.target_team_filter))
        {
            AddError(
                errors,
                "EQA_TARGET_TEAM_FILTER_INVALID",
                $"{path}.payload.target_team_filter",
                $"schedule_area_effect target_team_filter {payload.target_team_filter} is not supported"
            );
        }

        bool hasContactStatus = payload.contact_status_id != "";
        if (!hasContactStatus)
        {
            return;
        }
        ValidateStatusReference(
            payload.contact_status_id,
            context,
            $"{path}.payload.contact_status_id",
            errors
        );
        ValidateStatusSemanticPayload(
            payload.contact_stack_behavior,
            payload.contact_stack_limit,
            payload.contact_counts_as_debuff_override,
            payload.contact_counts_as_debuff,
            payload.contact_undispellable,
            payload.contact_dispellable_magic,
            payload.contact_dispellable_harmful_magic,
            payload.contact_dispellable_beneficial_magic,
            path,
            "schedule_area_effect contact",
            "contact_",
            errors
        );
        if (payload.contact_status_duration_tu <= 0)
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                $"{path}.payload.contact_status_duration_tu",
                "schedule_area_effect contact status requires positive contact_status_duration_tu"
            );
        }
        if (payload.contact_save_dc > 0 && (payload.contact_save_ability == "" || payload.contact_save_tag == ""))
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "schedule_area_effect contact save gate requires contact_save_ability and contact_save_tag when contact_save_dc is positive"
            );
        }
        if (payload.contact_tick_interval_tu < 0)
        {
            AddError(
                errors,
                "EQA_STATUS_TICK_INVALID",
                $"{path}.payload.contact_tick_interval_tu",
                "schedule_area_effect contact_tick_interval_tu must be >= 0"
            );
        }
        if (
            payload.contact_tick_interval_tu > 0
            && payload.contact_tick_interval_tu % TuGranularity != 0
        )
        {
            AddError(
                errors,
                "EQA_TU_GRANULARITY_INVALID",
                $"{path}.payload.contact_tick_interval_tu",
                $"schedule_area_effect contact_tick_interval_tu must be a multiple of {TuGranularity}"
            );
        }
        bool hasContactTimelineDamage =
            payload.contact_timeline_damage_dice_count > 0
            || payload.contact_timeline_damage_dice_sides > 0
            || payload.contact_timeline_damage_flat_bonus > 0;
        if (
            payload.contact_timeline_damage_dice_count < 0
            || payload.contact_timeline_damage_dice_sides < 0
            || payload.contact_timeline_damage_flat_bonus < 0
        )
        {
            AddError(
                errors,
                "EQA_STATUS_TIMELINE_DAMAGE_DICE_INVALID",
                path,
                "schedule_area_effect contact timeline damage dice fields must be >= 0"
            );
        }
        if (hasContactTimelineDamage)
        {
            if (payload.contact_tick_interval_tu <= 0)
            {
                AddError(
                    errors,
                    "EQA_STATUS_TICK_INVALID",
                    $"{path}.payload.contact_tick_interval_tu",
                    "schedule_area_effect contact timeline damage requires positive contact_tick_interval_tu"
                );
            }
            if (
                payload.contact_timeline_damage_dice_count <= 0
                || payload.contact_timeline_damage_dice_sides <= 0
            )
            {
                AddError(
                    errors,
                    "EQA_STATUS_TIMELINE_DAMAGE_DICE_INVALID",
                    path,
                    "schedule_area_effect contact timeline damage requires positive dice count and sides"
                );
            }
        }
    }

    private static void ValidateApplyBattleTerrainEffectAfterCheckPayload(
        ApplyBattleTerrainEffectAfterCheckActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (
            payload.anchor_selector == ""
            || payload.terrain_effect_id == ""
            || payload.check_attribute_modifier_id == ""
            || payload.check_compare == ""
            || payload.check_threshold <= 0
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "apply_battle_terrain_effect_after_check requires anchor_selector, terrain_effect_id, check_attribute_modifier_id, check_compare, and positive check_threshold"
            );
        }
        if (payload.move_cost_delta <= 0)
        {
            AddError(
                errors,
                "EQA_MOVE_COST_DELTA_INVALID",
                $"{path}.payload.move_cost_delta",
                "apply_battle_terrain_effect_after_check move_cost_delta must be positive"
            );
        }
        if (!CombatTargetTeamContentRules.IsValidSkillTargetTeamFilter(payload.target_team_filter))
        {
            AddError(
                errors,
                "EQA_TARGET_TEAM_FILTER_INVALID",
                $"{path}.payload.target_team_filter",
                $"apply_battle_terrain_effect_after_check target_team_filter {payload.target_team_filter} is not supported"
            );
        }
        if (!IsValidIntCompareOperator(payload.check_compare))
        {
            AddError(
                errors,
                "EQA_COMPARE_OPERATOR_INVALID",
                $"{path}.payload.check_compare",
                $"apply_battle_terrain_effect_after_check check_compare {payload.check_compare} is not supported"
            );
        }
    }

    private static void ValidateApplyEdgeFeaturePayload(
        ApplyEdgeFeatureActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (
            payload.from_selector == ""
            || payload.to_selector == ""
            || payload.duration_tu <= 0
            || payload.feature_kind == ""
            || payload.render_kind == ""
            || payload.interaction_kind == ""
            || payload.state_tag == ""
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "apply_edge_feature requires from_selector, to_selector, positive duration_tu, feature_kind, render_kind, interaction_kind, and state_tag"
            );
        }
        if (payload.duration_tu > 0 && payload.duration_tu % TuGranularity != 0)
        {
            AddError(
                errors,
                "EQA_TU_GRANULARITY_INVALID",
                $"{path}.payload.duration_tu",
                $"apply_edge_feature duration_tu must be a multiple of {TuGranularity}"
            );
        }
        if (payload.max_active_edges < 0)
        {
            AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.max_active_edges",
                "apply_edge_feature max_active_edges must be >= 0"
            );
        }
        if (payload.render_layers < 0)
        {
            AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.render_layers",
                "apply_edge_feature render_layers must be >= 0"
            );
        }
        if (!IsValidEdgeEndpointSelector(payload.from_selector, sourceOnly: true))
        {
            AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.from_selector",
                "apply_edge_feature from_selector must be source, owner, attacker, or source_attacker"
            );
        }
        if (!IsValidEdgeEndpointSelector(payload.to_selector, sourceOnly: false))
        {
            AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.to_selector",
                "apply_edge_feature to_selector must be target or attack_target"
            );
        }
        if (BattleEdgeFeatureState.ToFeatureKind(payload.feature_kind) == BattleEdgeFeatureKind.Unknown)
        {
            AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.feature_kind",
                $"apply_edge_feature feature_kind {payload.feature_kind} is not supported"
            );
        }
        if (BattleEdgeFeatureState.ToRenderKind(payload.render_kind) == BattleEdgeRenderKind.Unknown)
        {
            AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.render_kind",
                $"apply_edge_feature render_kind {payload.render_kind} is not supported"
            );
        }
        if (
            BattleEdgeFeatureState.ToInteractionKind(payload.interaction_kind)
            == BattleEdgeInteractionKind.Unknown
        )
        {
            AddError(
                errors,
                "EQA_ACTION_INVALID_VALUE",
                $"{path}.payload.interaction_kind",
                $"apply_edge_feature interaction_kind {payload.interaction_kind} is not supported"
            );
        }
    }

    private static void ValidateModifyAbilityStatePayload(
        ModifyAbilityStateActionPayloadDef payload,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.state_key == "")
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "modify_ability_state requires target_selector and state_key"
            );
        }
    }

    private static bool IsValidIntCompareOperator(StringName compare)
    {
        return ProgressionDataUtils.to_string_name(compare).ToString() switch
        {
            "lte" or "lt" or "gte" or "gt" or "eq" => true,
            _ => false,
        };
    }

    private static bool IsValidEdgeEndpointSelector(StringName selector, bool sourceOnly)
    {
        string normalized = ProgressionDataUtils.to_string_name(selector).ToString();
        if (normalized == "source" || normalized == "owner" || normalized == "attacker" || normalized == "source_attacker")
            return true;
        return !sourceOnly && (normalized == "target" || normalized == "attack_target");
    }

    private static void ValidateDurabilityPayload(
        EquipmentDurabilityDamageActionPayloadDef payload,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (payload.target_selector == "" || payload.durability_loss <= 0)
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "equipment_durability_damage requires target_selector and positive durability_loss"
            );
        }
        if (payload.max_damaged_items != 1)
        {
            AddError(
                errors,
                "EQA_DURABILITY_MAX_DAMAGED_ITEMS_UNSUPPORTED",
                $"{path}.payload.max_damaged_items",
                "V1 requires max_damaged_items = 1"
            );
        }
        if (payload.max_target_rarity < -1 || !IsKnownEquipmentRarity(payload.max_target_rarity))
        {
            AddError(
                errors,
                "EQA_DURABILITY_TARGET_RARITY_INVALID",
                $"{path}.payload.max_target_rarity",
                "max_target_rarity must be -1 or a valid EquipmentInstanceState.RarityTier value"
            );
        }
        if (HasKnownValues(context.KnownEquipmentSlotIds))
        {
            foreach (StringName slot in payload.target_slots)
            {
                if (!context.KnownEquipmentSlotIds.Contains(slot))
                {
                    AddError(
                        errors,
                        "EQA_REFERENCE_UNKNOWN_SLOT",
                        $"{path}.payload.target_slots[{slot}]",
                        $"equipment slot {slot} is not known"
                    );
                }
            }
        }
        HashSet<StringName> weightedSlots = new();
        if (payload.slot_weights != null)
        {
            for (int index = 0; index < payload.slot_weights.Count; index++)
            {
                EquipmentSlotWeightDef weight = payload.slot_weights[index];
                if (weight == null || weight.slot_id == "" || weight.weight <= 0)
                {
                    AddError(
                        errors,
                        "EQA_SLOT_WEIGHT_INVALID",
                        $"{path}.payload.slot_weights[{index}]",
                        "slot_weights entries require slot_id and positive weight"
                    );
                    continue;
                }
                if (!weightedSlots.Add(weight.slot_id))
                {
                    AddError(
                        errors,
                        "EQA_SLOT_WEIGHT_DUPLICATE",
                        $"{path}.payload.slot_weights[{weight.slot_id}]",
                        $"slot_weight for {weight.slot_id} is duplicated"
                    );
                }
                if (
                    HasKnownValues(context.KnownEquipmentSlotIds)
                    && !context.KnownEquipmentSlotIds.Contains(weight.slot_id)
                )
                {
                    AddError(
                        errors,
                        "EQA_REFERENCE_UNKNOWN_SLOT",
                        $"{path}.payload.slot_weights[{weight.slot_id}]",
                        $"equipment slot {weight.slot_id} is not known"
                    );
                }
            }
        }
    }

    private static bool IsKnownEquipmentRarity(int rarity)
    {
        return rarity == -1 || EquipmentInstanceState.IsValidRarity(rarity);
    }

    private static void ValidateDeclaredStateKey(
        StringName stateKey,
        HashSet<StringName> declaredStateKeys,
        string path,
        List<string> errors
    )
    {
        if (stateKey == "" || declaredStateKeys.Contains(stateKey))
            return;
        AddError(
            errors,
            "EQA_STATE_KEY_UNDECLARED",
            $"{path}.payload.state_key",
            $"state_key {stateKey} is not declared by binding state_schemas"
        );
    }

    private void ValidateGrantedActions(
        EquipmentAbilityBindingDef binding,
        EquipmentAbilityContentValidationContext context,
        List<string> errors
    )
    {
        string path = BindingPath(binding);
        var seen = new HashSet<StringName>();
        foreach (EquipmentGrantedActionDef grant in binding.granted_actions)
        {
            if (grant == null)
                continue;
            string grantPath = $"{path}.granted_actions[{grant.granted_action_id}]";
            if (grant.granted_action_id == "" || !seen.Add(grant.granted_action_id))
            {
                AddError(
                    errors,
                    "EQA_GRANTED_SKILL_COMPOSITION_INVALID",
                    grantPath,
                    "granted_action_id must be non-empty and unique for stable SkillEntryId composition"
                );
            }
            if (!TryParseGrantedKind(grant.granted_kind, out _))
            {
                AddError(
                    errors,
                    "EQA_GRANTED_KIND_UNSUPPORTED",
                    $"{grantPath}.granted_kind",
                    $"granted_kind {grant.granted_kind} is not supported in V1"
                );
            }
            ValidateSkillReference(grant.skill_id, context, $"{grantPath}.skill_id", errors);
            if (grant.skill_id == "" || grant.skill_level <= 0)
            {
                AddError(
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
                AddError(
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
                AddError(
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
                AddError(
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
        string path = BindingPath(binding);
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
        string path = BindingPath(binding);
        foreach (EquipmentWorldEffectDef effect in binding.world_effects)
        {
            if (effect == null)
                continue;
            string effectPath = $"{path}.world_effects[{effect.world_effect_id}]";
            bool triggerParsed = TryParseTrigger(effect.trigger, out EquipmentAbilityTriggerKind trigger);
            bool timingParsed = TryParseTiming(effect.timing, out EquipmentAbilityTimingKind timing);
            if (!triggerParsed)
            {
                AddError(
                    errors,
                    "EQA_TRIGGER_UNKNOWN_ID",
                    $"{effectPath}.trigger",
                    $"trigger {effect.trigger} is not registered"
                );
            }
            if (!timingParsed)
            {
                AddError(
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

    private static void ValidateStatusReference(
        StringName statusId,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (statusId == "" || (HasKnownValues(context.KnownStatusIds) && !context.KnownStatusIds.Contains(statusId)))
        {
            AddError(
                errors,
                "EQA_REFERENCE_UNKNOWN_STATUS",
                path,
                $"status_id {statusId} is not known"
            );
        }
    }

    private static void ValidateFactQuery(
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
            AddError(
                errors,
                "EQA_FACT_ATTRIBUTE_ID_MISSING",
                $"{path}.attribute_id",
                "attribute_value fact requires attribute_id"
            );
        }
    }

    private static void ValidateSkillReference(
        StringName skillId,
        EquipmentAbilityContentValidationContext context,
        string path,
        List<string> errors
    )
    {
        if (skillId == "" || (HasKnownValues(context.KnownSkillIds) && !context.KnownSkillIds.Contains(skillId)))
        {
            AddError(
                errors,
                "EQA_REFERENCE_UNKNOWN_SKILL",
                path,
                $"skill_id {skillId} is not known"
            );
        }
    }

    private static EquipmentAbilityContentPackDefinition ProjectPack(
        EquipmentAbilityContentPackDef source,
        IReadOnlyList<EquipmentAbilityBindingDefinition> bindings
    )
    {
        return new EquipmentAbilityContentPackDefinition
        {
            PackId = source.pack_id,
            SchemaVersion = source.schema_version,
            LoadOrder = source.load_order,
            Dependencies = CopyStringNames(source.dependencies),
            Bindings = new ReadOnlyCollection<EquipmentAbilityBindingDefinition>(
                new List<EquipmentAbilityBindingDefinition>(bindings)
            ),
            ResourcePath = source.ResourcePath ?? "",
        };
    }

    private static EquipmentAbilityBindingDefinition ProjectBinding(EquipmentAbilityBindingDef source)
    {
        return new EquipmentAbilityBindingDefinition
        {
            BindingId = source.binding_id,
            TraitId = source.trait_id,
            OverrideMode = TryParseOverrideMode(source.override_mode, out var mode)
                ? mode
                : EquipmentAbilityBindingOverrideMode.Add,
            ReplacesBindingId = source.replaces_binding_id,
            AllowedSourceKinds = ProjectSourceKinds(source.allowed_source_kinds),
            RequiredTraitCategories = CopyStringNameSet(source.required_trait_categories),
            RequiredItemTags = CopyStringNameSet(source.required_item_tags),
            SupportedEquipmentTypeIds = CopyStringNameSet(source.supported_equipment_type_ids),
            StateSchemas = ProjectStateSchemas(source.state_schemas),
            Reactions = ProjectReactions(source.reactions),
            GrantedActions = ProjectGrantedActions(source.granted_actions),
            TemporalProgressModifiers = ProjectTemporalProgressModifiers(
                source.binding_id,
                source.temporal_progress_modifiers
            ),
            WeaponProfileOverlays = ProjectWeaponProfileOverlays(source.weapon_profile_overlays),
            WorldEffects = ProjectWorldEffects(source.world_effects),
            ResourcePath = source.ResourcePath ?? "",
        };
    }

    private static IReadOnlyList<EquipmentTemporalProgressModifierDefinition> ProjectTemporalProgressModifiers(
        StringName bindingId,
        Godot.Collections.Array<EquipmentTemporalProgressModifierDef> values
    )
    {
        if (values == null || values.Count == 0)
            return Array.Empty<EquipmentTemporalProgressModifierDefinition>();
        var result = new List<EquipmentTemporalProgressModifierDefinition>();
        foreach (EquipmentTemporalProgressModifierDef value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentTemporalProgressModifierDefinition
                {
                    ModifierId = value.modifier_id,
                    BindingId = bindingId,
                    AppliesToActionProgress = value.applies_to_action_progress,
                    AppliesToCastProgress = value.applies_to_cast_progress,
                    SaveDc = Math.Max(value.save_dc, 0),
                    AttributeModifierId = value.attribute_modifier_id,
                    SuccessRatePercent = Math.Max(value.success_rate_percent, 0),
                    FailureRatePercent = Math.Max(value.failure_rate_percent, 0),
                    Label = value.label ?? "",
                }
            );
        }
        return result.Count > 0
            ? new ReadOnlyCollection<EquipmentTemporalProgressModifierDefinition>(result)
            : Array.Empty<EquipmentTemporalProgressModifierDefinition>();
    }

    private static IReadOnlySet<StringName> ProjectSourceKinds(
        Godot.Collections.Array<StringName> values
    )
    {
        var result = new HashSet<StringName>();
        foreach (StringName value in values)
        {
            TraitSourceKind kind = TraitContentRules.ToSourceKind(value);
            if (kind == TraitSourceKind.EquipmentFixed || kind == TraitSourceKind.EquipmentRoll)
                result.Add(TraitContentRules.ToStringName(kind));
        }
        return EquipmentAbilityReadOnlySet<StringName>.From(result);
    }

    private static IReadOnlyList<EquipmentAbilityStateSchemaDefinition> ProjectStateSchemas(
        Godot.Collections.Array<EquipmentAbilityStateSchemaDef> values
    )
    {
        var result = new List<EquipmentAbilityStateSchemaDefinition>();
        foreach (EquipmentAbilityStateSchemaDef value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentAbilityStateSchemaDefinition
                {
                    StateKey = value.state_key,
                    OwnerScope = value.owner_scope,
                    ValueKind = value.value_kind,
                    InitialIntValue = value.initial_int_value,
                    MaxIntValue = value.max_int_value,
                    ResetTiming = value.reset_timing,
                    PersistOutsideBattle = value.persist_outside_battle,
                    VisibleToUi = value.visible_to_ui,
                    SyncSourceStateKey = value.sync_source_state_key,
                    SyncAggregation = value.sync_aggregation,
                    SyncIntLiteral = value.sync_int_literal,
                }
            );
        }
        return new ReadOnlyCollection<EquipmentAbilityStateSchemaDefinition>(result);
    }

    private static IReadOnlyList<EquipmentAbilityReactionDefinition> ProjectReactions(
        Godot.Collections.Array<EquipmentAbilityReactionDef> values
    )
    {
        var result = new List<EquipmentAbilityReactionDefinition>();
        foreach (EquipmentAbilityReactionDef value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentAbilityReactionDefinition
                {
                    ReactionId = value.reaction_id,
                    Trigger = TryParseTrigger(value.trigger, out var trigger)
                        ? trigger
                        : EquipmentAbilityTriggerKind.OnHit,
                    Timing = TryParseTiming(value.timing, out var timing)
                        ? timing
                        : EquipmentAbilityTimingKind.AfterHit,
                    Priority = value.priority,
                    OnceScope = value.once_scope,
                    RequiresPlayerConfirmation = value.requires_player_confirmation,
                    ConditionGroup = ProjectConditionGroup(value.condition_group),
                    RollGate = ProjectRollGate(value.roll_gate),
                    OutcomeTable = ProjectOutcomeTable(value.outcome_table),
                    Actions = ProjectActions(value.actions),
                }
            );
        }
        return new ReadOnlyCollection<EquipmentAbilityReactionDefinition>(result);
    }

    private static EquipmentConditionGroupDefinition ProjectConditionGroup(
        EquipmentAbilityConditionGroupDef value
    )
    {
        if (value == null)
            return null;
        var conditions = new List<EquipmentAbilityConditionDefinition>();
        foreach (EquipmentAbilityConditionDef condition in value.conditions)
        {
            if (condition == null)
                continue;
            conditions.Add(
                new EquipmentAbilityConditionDefinition
                {
                    ConditionId = condition.condition_id,
                    Kind = condition.kind,
                    PayloadDefinition = ProjectConditionPayload(condition.payload),
                }
            );
        }
        var groups = new List<EquipmentConditionGroupDefinition>();
        foreach (EquipmentAbilityConditionGroupDef group in value.groups)
        {
            EquipmentConditionGroupDefinition projected = ProjectConditionGroup(group);
            if (projected != null)
                groups.Add(projected);
        }
        return new EquipmentConditionGroupDefinition
        {
            Mode = value.mode,
            Negate = value.negate,
            Conditions = new ReadOnlyCollection<EquipmentAbilityConditionDefinition>(conditions),
            Groups = new ReadOnlyCollection<EquipmentConditionGroupDefinition>(groups),
        };
    }

    private static EquipmentAbilityConditionPayloadDefinition ProjectConditionPayload(Resource payload)
    {
        return payload switch
        {
            HasStatusConditionPayloadDef status => new HasStatusConditionPayloadDefinition
            {
                Subject = status.subject,
                StatusId = status.status_id,
            },
            CompareFactConditionPayloadDef compare => new CompareFactConditionPayloadDefinition
            {
                Left = ProjectFactQuery(compare.left),
                Compare = compare.compare,
                Right = ProjectFactQuery(compare.right),
            },
            HasEquipmentTagConditionPayloadDef tags => new HasEquipmentTagConditionPayloadDefinition
            {
                Subject = tags.subject,
                EquipmentSelector = tags.equipment_selector,
                AllTags = CopyStringNames(tags.all_tags),
                AnyTags = CopyStringNames(tags.any_tags),
            },
            _ => null,
        };
    }

    private static IReadOnlyList<EquipmentAbilityActionDefinition> ProjectActions(
        Godot.Collections.Array<EquipmentAbilityActionDef> values
    )
    {
        var result = new List<EquipmentAbilityActionDefinition>();
        foreach (EquipmentAbilityActionDef value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentAbilityActionDefinition
                {
                    ActionId = value.action_id,
                    Kind = value.kind,
                    PayloadDefinition = ProjectActionPayload(value.payload),
                    ConditionGroup = ProjectConditionGroup(value.condition_group),
                    RollGate = ProjectRollGate(value.roll_gate),
                }
            );
        }
        return new ReadOnlyCollection<EquipmentAbilityActionDefinition>(result);
    }

    private static EquipmentAbilityActionPayloadDefinition ProjectActionPayload(Resource payload)
    {
        return payload switch
        {
            AddDamageDiceActionPayloadDef damage => new AddDamageDiceActionPayloadDefinition
            {
                TargetSelector = damage.target_selector,
                Dice = ProjectDice(damage.dice),
                DamageType = damage.damage_type,
                Subtract = damage.subtract,
                DamageTags = CopyStringNames(damage.damage_tags),
                MitigationBypassDamageTags = CopyStringNames(
                    damage.mitigation_bypass_damage_tags
                ),
                MitigationBypassTiers = CopyStringNames(damage.mitigation_bypass_tiers),
            },
            ImmediateWeaponAttackActionPayloadDef weaponAttack => new ImmediateWeaponAttackActionPayloadDefinition
            {
                AnchorSelector = weaponAttack.anchor_selector,
                TargetTeamFilter = weaponAttack.target_team_filter,
                Radius = Math.Max(weaponAttack.radius, 0),
                MaxAttacks = Math.Max(weaponAttack.max_attacks, 0),
                SkillId = weaponAttack.skill_id,
                RequireWeaponRange = weaponAttack.require_weapon_range,
            },
            DealDamageActionPayloadDef directDamage => new DealDamageActionPayloadDefinition
            {
                TargetSelector = directDamage.target_selector,
                Dice = ProjectDice(directDamage.dice),
                DamageType = directDamage.damage_type,
                DamageTags = CopyStringNames(directDamage.damage_tags),
                MitigationBypassDamageTags = CopyStringNames(
                    directDamage.mitigation_bypass_damage_tags
                ),
                MitigationBypassTiers = CopyStringNames(directDamage.mitigation_bypass_tiers),
            },
            HealActionPayloadDef heal => new HealActionPayloadDefinition
            {
                TargetSelector = heal.target_selector,
                Dice = ProjectDice(heal.dice),
            },
            HealFromFactActionPayloadDef healFromFact => new HealFromFactActionPayloadDefinition
            {
                TargetSelector = healFromFact.target_selector,
                AmountFact = ProjectFactQuery(healFromFact.amount_fact),
                MultiplierPercent = healFromFact.multiplier_percent,
                MaxAmount = healFromFact.max_amount,
            },
            AttackRollBonusActionPayloadDef attackRoll => new AttackRollBonusActionPayloadDefinition
            {
                TargetSelector = attackRoll.target_selector,
                Bonus = attackRoll.bonus,
                AttributeModifierId = attackRoll.attribute_modifier_id,
                StackMode = attackRoll.stack_mode,
                Label = attackRoll.label ?? "",
                RequireWeaponDamage = attackRoll.require_weapon_damage,
            },
            AttackRollAdvantageActionPayloadDef attackAdvantage => new AttackRollAdvantageActionPayloadDefinition
            {
                TargetSelector = attackAdvantage.target_selector,
                Mode = attackAdvantage.mode,
                StackMode = attackAdvantage.stack_mode,
                Label = attackAdvantage.label ?? "",
            },
            CriticalHitOverrideActionPayloadDef critical => new CriticalHitOverrideActionPayloadDefinition
            {
                TargetSelector = critical.target_selector,
                RequireWeaponDamage = critical.require_weapon_damage,
                Label = critical.label ?? "",
            },
            EquipmentAttackDefenseModifierDef defense => new EquipmentAttackDefenseModifierDefinition
            {
                ModifierId = defense.modifier_id,
                IgnoredAcComponents = CopyStringNames(defense.ignored_ac_components),
                AcComponentMultipliers = ProjectAcComponentMultipliers(defense.ac_component_multipliers),
                LockDodgeBonus = defense.lock_dodge_bonus,
                RequiredTargetEquipmentSelector = defense.required_target_equipment_selector,
                RequiredTargetItemTags = CopyStringNames(defense.required_target_item_tags),
                RequiredTargetEquipmentTypeIds = CopyStringNames(
                    defense.required_target_equipment_type_ids
                ),
                CoverPolicy = defense.cover_policy,
                ProjectileObstaclePolicy = defense.projectile_obstacle_policy,
                TraceLabel = defense.trace_label,
            },
            DamageRollModeOverrideActionPayloadDef damageRollMode =>
                new DamageRollModeOverrideActionPayloadDefinition
                {
                    TargetSelector = damageRollMode.target_selector,
                    RollMode = damageRollMode.roll_mode,
                    StackMode = damageRollMode.stack_mode,
                    Label = damageRollMode.label ?? "",
                },
            DamageReductionActionPayloadDef damageReduction =>
                new DamageReductionActionPayloadDefinition
                {
                    TargetSelector = damageReduction.target_selector,
                    Amount = damageReduction.amount,
                    DamageTags = CopyStringNames(damageReduction.damage_tags),
                    Label = damageReduction.label ?? "",
                },
            LootQuantityMultiplierActionPayloadDef loot => new LootQuantityMultiplierActionPayloadDefinition
            {
                TargetSelector = loot.target_selector,
                MultiplierPercent = loot.multiplier_percent,
                AffectedDropKinds = CopyStringNames(loot.affected_drop_kinds),
                AnyItemTags = CopyStringNames(loot.any_item_tags),
            },
            ApplyStatusActionPayloadDef status => new ApplyStatusActionPayloadDefinition
            {
                TargetSelector = status.target_selector,
                StatusId = status.status_id,
                DurationTurns = status.duration_turns,
                DurationTu = status.duration_tu,
                StackDelta = status.stack_delta,
                StackBehavior = status.stack_behavior,
                StackLimit = status.stack_limit,
                DisplayLabel = status.display_label ?? "",
                AttackRollPenalty = status.attack_roll_penalty,
                SourceBoundAttackRollPenalty = status.source_bound_attack_roll_penalty,
                SourceBoundAttackRollPenaltyMinStacks =
                    status.source_bound_attack_roll_penalty_min_stacks,
                SourceBoundIncomingAttackRollBonusPerStack =
                    status.source_bound_incoming_attack_roll_bonus_per_stack,
                SourceBoundIncomingAttackRollBonusMinStacks =
                    status.source_bound_incoming_attack_roll_bonus_min_stacks,
                OverrideHealMultiplierPercent = status.override_heal_multiplier_percent,
                HealMultiplierPercent = status.heal_multiplier_percent,
                MovePointCapacityDelta = status.move_point_capacity_delta,
                ForcedMoveImmune = status.forced_move_immune,
                CountsAsDebuffOverride = status.counts_as_debuff_override,
                CountsAsDebuff = status.counts_as_debuff,
                Undispellable = status.undispellable,
                DispellableMagic = status.dispellable_magic,
                DispellableHarmfulMagic = status.dispellable_harmful_magic,
                DispellableBeneficialMagic = status.dispellable_beneficial_magic,
                LockCounterattack = status.lock_counterattack,
                LockGuard = status.lock_guard,
                LockDodgeBonus = status.lock_dodge_bonus,
                TickIntervalTu = status.tick_interval_tu,
                TimelineDamageDiceCount = status.timeline_damage_dice_count,
                TimelineDamageDiceSides = status.timeline_damage_dice_sides,
                TimelineDamageFlatBonus = status.timeline_damage_flat_bonus,
                SaveDc = status.save_dc,
                SaveAbility = status.save_ability,
                SaveTag = status.save_tag,
                ApplyOnSaveFailure = status.apply_on_save_failure,
            },
            ModifyActionPointsActionPayloadDef actionPoints => new ModifyActionPointsActionPayloadDefinition
            {
                TargetSelector = actionPoints.target_selector,
                Mode = actionPoints.mode,
                Amount = actionPoints.amount,
                StatusId = actionPoints.status_id,
                DisplayLabel = actionPoints.display_label ?? "",
            },
            ScheduleAreaEffectActionPayloadDef schedule => new ScheduleAreaEffectActionPayloadDefinition
            {
                AnchorSelector = schedule.anchor_selector,
                DelayTu = schedule.delay_tu,
                TerrainEffectId = schedule.terrain_effect_id,
                AreaPattern = schedule.area_pattern,
                AreaValue = schedule.area_value,
                LifetimePolicy = schedule.lifetime_policy,
                EffectType = schedule.effect_type,
                TargetTeamFilter = schedule.target_team_filter,
                StackBehavior = schedule.stack_behavior,
                DisplayName = schedule.display_name ?? "",
                RenderOverlayId = schedule.render_overlay_id,
                OverlayPriority = schedule.overlay_priority,
                ContactStatusId = schedule.contact_status_id,
                ContactStatusDurationTu = schedule.contact_status_duration_tu,
                ContactStackBehavior = schedule.contact_stack_behavior,
                ContactStackLimit = schedule.contact_stack_limit,
                ContactStatusDisplayLabel = schedule.contact_status_display_label ?? "",
                ContactCountsAsDebuffOverride = schedule.contact_counts_as_debuff_override,
                ContactCountsAsDebuff = schedule.contact_counts_as_debuff,
                ContactUndispellable = schedule.contact_undispellable,
                ContactDispellableMagic = schedule.contact_dispellable_magic,
                ContactDispellableHarmfulMagic = schedule.contact_dispellable_harmful_magic,
                ContactDispellableBeneficialMagic = schedule.contact_dispellable_beneficial_magic,
                ContactSaveDc = schedule.contact_save_dc,
                ContactSaveAbility = schedule.contact_save_ability,
                ContactSaveTag = schedule.contact_save_tag,
                ContactApplyOnSaveFailure = schedule.contact_apply_on_save_failure,
                ContactTickIntervalTu = schedule.contact_tick_interval_tu,
                ContactTimelineDamageDiceCount = schedule.contact_timeline_damage_dice_count,
                ContactTimelineDamageDiceSides = schedule.contact_timeline_damage_dice_sides,
                ContactTimelineDamageFlatBonus = schedule.contact_timeline_damage_flat_bonus,
                ContactBlockedByTraitId = schedule.contact_blocked_by_trait_id,
            },
            ApplyBattleTerrainEffectAfterCheckActionPayloadDef terrainCheck =>
                new ApplyBattleTerrainEffectAfterCheckActionPayloadDefinition
                {
                    AnchorSelector = terrainCheck.anchor_selector,
                    TerrainEffectId = terrainCheck.terrain_effect_id,
                    MoveCostDelta = terrainCheck.move_cost_delta,
                    TargetTeamFilter = terrainCheck.target_team_filter,
                    StackBehavior = terrainCheck.stack_behavior,
                    DisplayName = terrainCheck.display_name ?? "",
                    RenderOverlayId = terrainCheck.render_overlay_id,
                    OverlayPriority = terrainCheck.overlay_priority,
                    CheckAttributeModifierId = terrainCheck.check_attribute_modifier_id,
                    CheckCompare = terrainCheck.check_compare,
                    CheckThreshold = terrainCheck.check_threshold,
                    NaturalTwentyAutoSuccess = terrainCheck.natural_twenty_auto_success,
                    NaturalOneAutoFailure = terrainCheck.natural_one_auto_failure,
                },
            ApplyEdgeFeatureActionPayloadDef edgeFeature => new ApplyEdgeFeatureActionPayloadDefinition
            {
                FromSelector = edgeFeature.from_selector,
                ToSelector = edgeFeature.to_selector,
                DurationTu = edgeFeature.duration_tu,
                MaxActiveEdges = edgeFeature.max_active_edges,
                RefreshExisting = edgeFeature.refresh_existing,
                RequireAdjacent = edgeFeature.require_adjacent,
                FeatureKind = edgeFeature.feature_kind,
                RenderKind = edgeFeature.render_kind,
                RenderLayers = edgeFeature.render_layers,
                BlocksMove = edgeFeature.blocks_move,
                BlocksOccupancy = edgeFeature.blocks_occupancy,
                BlocksLos = edgeFeature.blocks_los,
                InteractionKind = edgeFeature.interaction_kind,
                StateTag = edgeFeature.state_tag,
            },
            ModifyAbilityStateActionPayloadDef state => new ModifyAbilityStateActionPayloadDefinition
            {
                TargetSelector = state.target_selector,
                BindingId = state.binding_id,
                StateKey = state.state_key,
                Operation = state.operation,
                IntDelta = state.int_delta,
            },
            MarkTargetActionPayloadDef mark => new MarkTargetActionPayloadDefinition
            {
                TargetSelector = mark.target_selector,
                StateKey = mark.state_key,
                StackDelta = mark.stack_delta,
                RemoveOnSourceMissing = mark.remove_on_source_missing,
                RemoveOnTargetDefeated = mark.remove_on_target_defeated,
                UniquePerSource = mark.unique_per_source,
                MirrorStatusId = mark.mirror_status_id,
                MirrorStatusDurationTu = mark.mirror_status_duration_tu,
                MirrorStatusStackBehavior = mark.mirror_status_stack_behavior,
                MirrorStatusStackLimit = mark.mirror_status_stack_limit,
                MirrorStatusDisplayLabel = mark.mirror_status_display_label ?? "",
                ClearStatusIdsOnReplace = CopyStringNames(mark.clear_status_ids_on_replace),
            },
            ClearStatusActionPayloadDef clear => new ClearStatusActionPayloadDefinition
            {
                TargetSelector = clear.target_selector,
                StatusId = clear.status_id,
                MarkBindingId = clear.mark_binding_id,
                MarkStateKey = clear.mark_state_key,
                RequireSourceUnitMatch = clear.require_source_unit_match,
                ClearTargetMark = clear.clear_target_mark,
            },
            TriggerSkillActionPayloadDef triggerSkill => new TriggerSkillActionPayloadDefinition
            {
                SkillId = triggerSkill.skill_id,
                SkillLevel = Math.Max(triggerSkill.skill_level, 1),
                TargetSelector = triggerSkill.target_selector,
                MergeIntoParentResult = triggerSkill.merge_into_parent_result,
                HandleTargetDefeat = triggerSkill.handle_target_defeat,
                ActivationLog = triggerSkill.activation_log ?? "",
                SaveLogLabel = triggerSkill.save_log_label ?? "",
            },
            GrantSkillActionPayloadDef grant => new GrantSkillActionPayloadDefinition
            {
                SkillId = grant.skill_id,
                SkillLevel = grant.skill_level,
                AvailabilityStateKey = grant.availability_state_key,
            },
            SummonUnitsActionPayloadDef summon => new SummonUnitsActionPayloadDefinition
            {
                AnchorSelector = summon.anchor_selector,
                StateKey = summon.state_key,
                CountDice = ProjectDice(summon.count_dice),
                MaxLivingUnits = summon.max_living_units,
                DurationTu = summon.duration_tu,
                SpawnRadius = summon.spawn_radius,
                UnitIdPrefix = summon.unit_id_prefix,
                UnitDisplayName = summon.unit_display_name ?? "",
                BodySizeCategory = summon.body_size_category,
                ControlMode = summon.control_mode,
                AiBrainId = summon.ai_brain_id,
                AiStateId = summon.ai_state_id,
                HpMax = summon.hp_max,
                ArmorClass = summon.armor_class,
                AttackBonus = summon.attack_bonus,
                BaseAttackBonus = summon.base_attack_bonus,
                ActionPoints = summon.action_points,
                MovePoints = summon.move_points,
                KnownActiveSkillIds = CopyStringNames(summon.known_active_skill_ids),
                NaturalWeaponProfileTypeId = summon.natural_weapon_profile_type_id,
                NaturalWeaponDamageTag = summon.natural_weapon_damage_tag,
                NaturalWeaponAttackRange = summon.natural_weapon_attack_range,
                NaturalWeaponDamageDice = ProjectDice(summon.natural_weapon_damage_dice),
                NaturalWeaponFamily = summon.natural_weapon_family,
                CreatureTypeTags = CopyStringNames(summon.creature_type_tags),
                MovementTags = CopyStringNames(summon.movement_tags),
            },
            ConsumeSummonedUnitsActionPayloadDef consume =>
                new ConsumeSummonedUnitsActionPayloadDefinition
                {
                    SourceBindingId = consume.source_binding_id,
                    StateKey = consume.state_key,
                    Count = consume.count,
                    SelectionMode = consume.selection_mode,
                },
            ConsumeStatusStacksActionPayloadDef consumeStacks =>
                new ConsumeStatusStacksActionPayloadDefinition
                {
                    TargetSelector = consumeStacks.target_selector,
                    StatusId = consumeStacks.status_id,
                    Count = consumeStacks.count,
                    RequireSourceUnitMatch = consumeStacks.require_source_unit_match,
                    SelectionMode = consumeStacks.selection_mode,
                },
            SummonedUnitAttackRollModifierActionPayloadDef summonedModifier =>
                new SummonedUnitAttackRollModifierActionPayloadDefinition
                {
                    TargetSelector = summonedModifier.target_selector,
                    SourceBindingId = summonedModifier.source_binding_id,
                    StateKey = summonedModifier.state_key,
                    Radius = summonedModifier.radius,
                    BonusPerUnit = summonedModifier.bonus_per_unit,
                    MaxAbsoluteBonus = summonedModifier.max_absolute_bonus,
                    MinUnits = summonedModifier.min_units,
                    StackMode = summonedModifier.stack_mode,
                    Label = summonedModifier.label ?? "",
                },
            EquipmentDurabilityDamageActionPayloadDef durability =>
                new EquipmentDurabilityDamageActionPayloadDefinition
                {
                    TargetSelector = durability.target_selector,
                    TargetSlots = CopyStringNames(durability.target_slots),
                    SlotWeights = ProjectSlotWeights(durability.slot_weights),
                    RequiredItemTags = CopyStringNames(durability.required_item_tags),
                    RequiredEquipmentTypeIds = CopyStringNames(durability.required_equipment_type_ids),
                    DurabilityLoss = durability.durability_loss,
                    SaveTag = durability.save_tag,
                    SaveDc = durability.save_dc,
                    RequireAttackSuccess = durability.require_attack_success,
                    MaxDamagedItems = durability.max_damaged_items,
                    MaxTargetRarity = durability.max_target_rarity,
                },
            _ => null,
        };
    }

    private static IReadOnlyList<EquipmentAcComponentMultiplierDefinition> ProjectAcComponentMultipliers(
        Godot.Collections.Array<EquipmentAcComponentMultiplierDef> values
    )
    {
        if (values == null || values.Count == 0)
            return Array.Empty<EquipmentAcComponentMultiplierDefinition>();
        var result = new List<EquipmentAcComponentMultiplierDefinition>();
        foreach (EquipmentAcComponentMultiplierDef value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentAcComponentMultiplierDefinition
                {
                    AcComponentId = value.ac_component_id,
                    MultiplierPercent = value.multiplier_percent,
                    StackMode = value.stack_mode,
                }
            );
        }
        return result.Count > 0
            ? new ReadOnlyCollection<EquipmentAcComponentMultiplierDefinition>(result)
            : Array.Empty<EquipmentAcComponentMultiplierDefinition>();
    }

    private static DiceExpressionDefinition ProjectDice(DiceExpressionDef value)
    {
        if (value == null)
            return null;
        var terms = new List<DiceExpressionTermDefinition>();
        foreach (DiceExpressionTermDef term in value.terms)
        {
            if (term == null)
                continue;
            terms.Add(
                new DiceExpressionTermDefinition
                {
                    DiceCount = term.dice_count,
                    DiceSides = term.dice_sides,
                    CountBonusFact = ProjectFactQuery(term.count_bonus_fact),
                    CountBonusMultiplier = term.count_bonus_multiplier,
                    MaxDiceCount = term.max_dice_count,
                }
            );
        }
        return new DiceExpressionDefinition
        {
            Terms = new ReadOnlyCollection<DiceExpressionTermDefinition>(terms),
            FlatBonus = value.flat_bonus,
            PreviewPolicy = value.preview_policy,
        };
    }

    private static EquipmentAbilityFactQueryDefinition ProjectFactQuery(EquipmentAbilityFactQueryDef value)
    {
        if (value == null)
            return null;
        return new EquipmentAbilityFactQueryDefinition
        {
            QueryKind = value.query_kind,
            FactId = value.fact_id,
            Subject = value.subject,
            BindingId = value.binding_id,
            StateKey = value.state_key,
            StatusId = value.status_id,
            RequireSourceUnitMatch = value.require_source_unit_match,
            AttributeId = value.attribute_id,
            Aggregation = value.aggregation,
            ValueKind = value.value_kind,
            BoolLiteral = value.bool_literal,
            IntLiteral = value.int_literal,
            FloatLiteral = value.float_literal,
            StringNameLiteral = value.string_name_literal,
        };
    }

    private static EquipmentRollGateDefinition ProjectRollGate(EquipmentRollGateDef value)
    {
        return value == null
            ? null
            : new EquipmentRollGateDefinition
            {
                RngStream = value.rng_stream,
                Roll = ProjectDice(value.roll),
                Compare = value.compare,
                Threshold = value.threshold,
            };
    }

    private static EquipmentOutcomeTableDefinition ProjectOutcomeTable(EquipmentOutcomeTableDef value)
    {
        if (value == null)
            return null;
        var entries = new List<EquipmentOutcomeEntryDefinition>();
        foreach (EquipmentOutcomeEntryDef entry in value.entries)
        {
            if (entry == null)
                continue;
            entries.Add(
                new EquipmentOutcomeEntryDefinition
                {
                    MinRoll = entry.min_roll,
                    MaxRoll = entry.max_roll,
                    Actions = ProjectActions(entry.actions),
                }
            );
        }
        return new EquipmentOutcomeTableDefinition
        {
            TableId = value.table_id,
            Roll = ProjectDice(value.roll),
            Entries = new ReadOnlyCollection<EquipmentOutcomeEntryDefinition>(entries),
        };
    }

    private static IReadOnlyList<EquipmentGrantedActionDefinition> ProjectGrantedActions(
        Godot.Collections.Array<EquipmentGrantedActionDef> values
    )
    {
        var result = new List<EquipmentGrantedActionDefinition>();
        foreach (EquipmentGrantedActionDef value in values)
        {
            if (value == null)
                continue;
            EquipmentAbilityUsagePeriodKinds.TryParse(
                value.usage_period_kind,
                out EquipmentAbilityUsagePeriodKind usagePeriodKind
            );
            result.Add(
                new EquipmentGrantedActionDefinition
                {
                    GrantedActionId = value.granted_action_id,
                    GrantedKind = TryParseGrantedKind(value.granted_kind, out var grantedKind)
                        ? grantedKind
                        : EquipmentGrantedActionKind.Skill,
                    SkillId = value.skill_id,
                    SkillLevel = value.skill_level,
                    UsagePeriodKind = usagePeriodKind,
                    MaxUsesPerPeriod = value.max_uses_per_period,
                    DisplayCategory = value.display_category,
                    DisplayPriority = value.display_priority,
                    AvailabilityConditions = ProjectConditionGroup(value.availability_conditions),
                    ResourcePath = value.ResourcePath ?? "",
                }
            );
        }
        return new ReadOnlyCollection<EquipmentGrantedActionDefinition>(result);
    }

    private static IReadOnlyList<EquipmentWeaponProfileOverlayDefinition> ProjectWeaponProfileOverlays(
        Godot.Collections.Array<EquipmentWeaponProfileOverlayDef> values
    )
    {
        var result = new List<EquipmentWeaponProfileOverlayDefinition>();
        foreach (EquipmentWeaponProfileOverlayDef value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentWeaponProfileOverlayDefinition
                {
                    OverlayId = value.overlay_id,
                    Priority = value.priority,
                    ConditionGroup = ProjectConditionGroup(value.condition_group),
                    RequireEquippedWeapon = value.require_equipped_weapon,
                    RequiredWeaponFamilies = CopyStringNameSet(value.required_weapon_families),
                    RequiredWeaponTypeIds = CopyStringNameSet(value.required_weapon_type_ids),
                    AttackRangeDelta = value.attack_range_delta,
                    MinAttackRange = value.min_attack_range,
                    MaxAttackRange = value.max_attack_range,
                    OneHandedDiceOverlay = ProjectWeaponDiceOverlay(value.one_handed_dice_overlay),
                    TwoHandedDiceOverlay = ProjectWeaponDiceOverlay(value.two_handed_dice_overlay),
                    PhysicalDamageTagOverride = value.physical_damage_tag_override,
                    GripOverride = value.grip_override,
                    UsesTwoHandsOverride = value.uses_two_hands_override,
                    IsVersatileOverride = value.is_versatile_override,
                    ResourcePath = value.ResourcePath ?? "",
                }
            );
        }
        return new ReadOnlyCollection<EquipmentWeaponProfileOverlayDefinition>(result);
    }

    private static EquipmentWeaponDiceOverlayDefinition ProjectWeaponDiceOverlay(
        EquipmentWeaponDiceOverlayDef value
    )
    {
        return value == null
            ? null
            : new EquipmentWeaponDiceOverlayDefinition
            {
                Mode = value.mode,
                DiceCountDelta = value.dice_count_delta,
                DiceSidesOverride = value.dice_sides_override,
                FlatBonusDelta = value.flat_bonus_delta,
                DiceOverride = ProjectDice(value.dice_override),
            };
    }

    private static IReadOnlyList<EquipmentWorldEffectDefinition> ProjectWorldEffects(
        Godot.Collections.Array<EquipmentWorldEffectDef> values
    )
    {
        var result = new List<EquipmentWorldEffectDefinition>();
        foreach (EquipmentWorldEffectDef value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentWorldEffectDefinition
                {
                    WorldEffectId = value.world_effect_id,
                    Trigger = TryParseTrigger(value.trigger, out var trigger)
                        ? trigger
                        : EquipmentAbilityTriggerKind.OnHit,
                    Timing = TryParseTiming(value.timing, out var timing)
                        ? timing
                        : EquipmentAbilityTimingKind.AfterHit,
                    ConditionGroup = ProjectConditionGroup(value.condition_group),
                    Actions = ProjectActions(value.actions),
                }
            );
        }
        return new ReadOnlyCollection<EquipmentWorldEffectDefinition>(result);
    }

    private static IReadOnlyList<StringName> CopyStringNames(
        Godot.Collections.Array<StringName> values
    )
    {
        if (values == null || values.Count == 0)
            return Array.Empty<StringName>();
        var result = new List<StringName>();
        foreach (StringName value in values)
        {
            if (value != "")
                result.Add(value);
        }
        return result.Count > 0 ? new ReadOnlyCollection<StringName>(result) : Array.Empty<StringName>();
    }

    private static IReadOnlyList<EquipmentSlotWeightDefinition> ProjectSlotWeights(
        Godot.Collections.Array<EquipmentSlotWeightDef> values
    )
    {
        if (values == null || values.Count == 0)
            return Array.Empty<EquipmentSlotWeightDefinition>();
        var result = new List<EquipmentSlotWeightDefinition>();
        HashSet<StringName> seen = new();
        foreach (EquipmentSlotWeightDef value in values)
        {
            if (
                value == null
                || value.slot_id == ""
                || value.weight <= 0
                || !seen.Add(value.slot_id)
            )
            {
                continue;
            }
            result.Add(
                new EquipmentSlotWeightDefinition
                {
                    SlotId = value.slot_id,
                    Weight = value.weight,
                }
            );
        }
        return result.Count > 0
            ? new ReadOnlyCollection<EquipmentSlotWeightDefinition>(result)
            : Array.Empty<EquipmentSlotWeightDefinition>();
    }

    private static IReadOnlySet<StringName> CopyStringNameSet(
        Godot.Collections.Array<StringName> values
    )
    {
        var result = new HashSet<StringName>();
        if (values == null)
            return EquipmentAbilityReadOnlySet<StringName>.Empty;
        foreach (StringName value in values)
        {
            if (value != "")
                result.Add(value);
        }
        return EquipmentAbilityReadOnlySet<StringName>.From(result);
    }

    private static bool TryParseOverrideMode(
        StringName value,
        out EquipmentAbilityBindingOverrideMode mode
    )
    {
        if (value == "" || value == "add")
        {
            mode = EquipmentAbilityBindingOverrideMode.Add;
            return true;
        }
        if (value == "replace_binding")
        {
            mode = EquipmentAbilityBindingOverrideMode.ReplaceBinding;
            return true;
        }
        mode = EquipmentAbilityBindingOverrideMode.Add;
        return false;
    }

    private static bool TryParseTrigger(StringName value, out EquipmentAbilityTriggerKind trigger)
    {
        if (value == "on_hit")
        {
            trigger = EquipmentAbilityTriggerKind.OnHit;
            return true;
        }
        if (value == "on_kill")
        {
            trigger = EquipmentAbilityTriggerKind.OnKill;
            return true;
        }
        if (value == "on_battle_end")
        {
            trigger = EquipmentAbilityTriggerKind.OnBattleEnd;
            return true;
        }
        if (value == "on_granted_skill_used")
        {
            trigger = EquipmentAbilityTriggerKind.OnGrantedSkillUsed;
            return true;
        }
        if (value == "on_turn_end")
        {
            trigger = EquipmentAbilityTriggerKind.OnTurnEnd;
            return true;
        }
        if (value == "on_damage_roll")
        {
            trigger = EquipmentAbilityTriggerKind.OnDamageRoll;
            return true;
        }
        if (value == "on_damage_applied")
        {
            trigger = EquipmentAbilityTriggerKind.OnDamageApplied;
            return true;
        }
        if (value == "on_hit_received")
        {
            trigger = EquipmentAbilityTriggerKind.OnHitReceived;
            return true;
        }
        if (value == "on_attack_check")
        {
            trigger = EquipmentAbilityTriggerKind.OnAttackCheck;
            return true;
        }
        if (value == "on_target_mark_expired")
        {
            trigger = EquipmentAbilityTriggerKind.OnTargetMarkExpired;
            return true;
        }
        trigger = EquipmentAbilityTriggerKind.OnHit;
        return false;
    }

    private static bool TryParseTiming(StringName value, out EquipmentAbilityTimingKind timing)
    {
        if (value == "before_hit")
        {
            timing = EquipmentAbilityTimingKind.BeforeHit;
            return true;
        }
        if (value == "" || value == "after_hit")
        {
            timing = EquipmentAbilityTimingKind.AfterHit;
            return true;
        }
        if (value == "after_kill")
        {
            timing = EquipmentAbilityTimingKind.AfterKill;
            return true;
        }
        if (value == "after_battle")
        {
            timing = EquipmentAbilityTimingKind.AfterBattle;
            return true;
        }
        if (value == "after_skill")
        {
            timing = EquipmentAbilityTimingKind.AfterSkill;
            return true;
        }
        if (value == "after_turn")
        {
            timing = EquipmentAbilityTimingKind.AfterTurn;
            return true;
        }
        if (value == "before_damage")
        {
            timing = EquipmentAbilityTimingKind.BeforeDamage;
            return true;
        }
        if (value == "after_damage")
        {
            timing = EquipmentAbilityTimingKind.AfterDamage;
            return true;
        }
        if (value == "after_hit_received")
        {
            timing = EquipmentAbilityTimingKind.AfterHitReceived;
            return true;
        }
        if (value == "after_attack_check")
        {
            timing = EquipmentAbilityTimingKind.AfterAttackCheck;
            return true;
        }
        if (value == "after_status_expired")
        {
            timing = EquipmentAbilityTimingKind.AfterStatusExpired;
            return true;
        }
        timing = EquipmentAbilityTimingKind.AfterHit;
        return false;
    }

    private static bool TryParseGrantedKind(
        StringName value,
        out EquipmentGrantedActionKind grantedKind
    )
    {
        if (value == "skill")
        {
            grantedKind = EquipmentGrantedActionKind.Skill;
            return true;
        }
        grantedKind = EquipmentGrantedActionKind.Skill;
        return false;
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
        AddError(
            errors,
            "EQA_TRIGGER_TIMING_UNSUPPORTED",
            path,
            $"trigger {trigger} does not support timing {timing}"
        );
    }

    private static bool HasKnownValues(IReadOnlySet<StringName> values) =>
        values != null && values.Count > 0;

    private static bool ContainsValue(IReadOnlySet<StringName> source, StringName key) =>
        source != null && source.Contains(key);

    private static bool IsKnownAcComponent(StringName componentId)
    {
        if (componentId == "")
            return false;
        foreach (StringName known in AttributeService.AC_COMPONENT_ATTRIBUTE_IDS)
            if (known == componentId)
                return true;
        return false;
    }

    private static bool IsAllowed(StringName value, params string[] allowed)
    {
        foreach (string candidate in allowed)
        {
            if (value == candidate)
                return true;
        }
        return false;
    }

    private static void AddTraitIndex(
        Dictionary<StringName, List<EquipmentAbilityBindingDefinition>> index,
        EquipmentAbilityBindingDefinition binding
    )
    {
        if (!index.TryGetValue(binding.TraitId, out var list))
        {
            list = new List<EquipmentAbilityBindingDefinition>();
            index[binding.TraitId] = list;
        }
        list.Add(binding);
    }

    private static void RemoveTraitIndex(
        Dictionary<StringName, List<EquipmentAbilityBindingDefinition>> index,
        EquipmentAbilityBindingDefinition binding
    )
    {
        if (!index.TryGetValue(binding.TraitId, out var list))
            return;
        list.RemoveAll(candidate => candidate.BindingId == binding.BindingId);
        if (list.Count == 0)
            index.Remove(binding.TraitId);
    }

    private static IReadOnlyDictionary<StringName, T> Snapshot<T>(Dictionary<StringName, T> source)
    {
        return new ReadOnlyDictionary<StringName, T>(new Dictionary<StringName, T>(source));
    }

    private static string PackPath(EquipmentAbilityContentPackDef pack) =>
        $"equipment_ability.packs[{(pack?.pack_id == "" ? "<missing>" : pack?.pack_id.ToString() ?? "<null>")}]";

    private static string BindingPath(EquipmentAbilityBindingDef binding) =>
        $"equipment_ability.bindings[{(binding?.binding_id == "" ? "<missing>" : binding?.binding_id.ToString() ?? "<null>")}]";

    private static string ReactionLabel(EquipmentAbilityReactionDef reaction) =>
        reaction?.reaction_id == "" ? "<missing>" : reaction?.reaction_id.ToString() ?? "<null>";

    private static void AddError(List<string> errors, string code, string path, string message)
    {
        errors.Add($"{code} {path}: {message}");
    }
}
