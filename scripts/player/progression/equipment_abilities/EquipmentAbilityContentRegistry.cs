using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class EquipmentAbilityContentRegistry : IDisposable
{
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
        ValidateSourceTraces(binding, context, errors);
        ValidateReactions(binding, context, declaredStateKeys, errors);
        ValidateGrantedActions(binding, context, errors);
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
            if (schema.state_key == "")
                continue;
            keys.Add(schema.state_key);
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
        return keys;
    }

    private static void ValidateSourceTraces(
        EquipmentAbilityBindingDef binding,
        EquipmentAbilityContentValidationContext context,
        List<string> errors
    )
    {
        string path = BindingPath(binding);
        foreach (EquipmentAbilitySourceTraceDef trace in binding.source_traces)
        {
            if (trace == null)
                continue;
            bool valid = trace.source_kind == "by_family"
                && (trace.coverage_status == "" || IsAllowed(trace.coverage_status, "bound", "deferred", "content_cut"))
                && (trace.phase == "" || IsAllowed(trace.phase, "v1", "v2", "v3"));
            if (trace.source_kind == "by_family")
            {
                valid = valid
                    && trace.item_id != ""
                    && (!HasKnownValues(context.KnownItemIds) || context.KnownItemIds.Contains(trace.item_id))
                    && !string.IsNullOrWhiteSpace(trace.source_file)
                    && !trace.source_file.Contains("..", StringComparison.Ordinal)
                    && !trace.source_file.StartsWith("/", StringComparison.Ordinal)
                    && trace.source_file.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
            }
            if (!valid)
            {
                AddError(
                    errors,
                    "EQA_SOURCE_TRACE_INVALID",
                    $"{path}.source_traces",
                    "source trace uses unsupported enum values, unsafe by_family path, or missing item_id"
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
            case ApplyStatusActionPayloadDef payload:
                if (payload.target_selector == "" || payload.status_id == "")
                    AddError(errors, "EQA_ACTION_REQUIRED_FIELD_MISSING", path, "apply_status requires target_selector and status_id");
                ValidateStatusReference(payload.status_id, context, $"{path}.payload.status_id", errors);
                break;
            case ModifyAbilityStateActionPayloadDef payload:
                if (payload.target_selector == "" || payload.state_key == "")
                    AddError(errors, "EQA_ACTION_REQUIRED_FIELD_MISSING", path, "modify_ability_state requires target_selector and state_key");
                break;
            case MarkTargetActionPayloadDef payload:
                if (payload.target_selector == "" || payload.state_key == "")
                    AddError(errors, "EQA_ACTION_REQUIRED_FIELD_MISSING", path, "mark_target requires target_selector and state_key");
                break;
            case GrantSkillActionPayloadDef payload:
                ValidateSkillReference(payload.skill_id, context, $"{path}.payload.skill_id", errors);
                if (payload.skill_id == "" || payload.skill_level <= 0)
                    AddError(errors, "EQA_ACTION_REQUIRED_FIELD_MISSING", path, "grant_skill requires skill_id and positive skill_level");
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
        if (
            payload.target_selector == ""
            || payload.damage_type == ""
            || payload.dice == null
            || payload.dice.terms.Count == 0
        )
        {
            AddError(
                errors,
                "EQA_ACTION_REQUIRED_FIELD_MISSING",
                path,
                "add_damage_dice requires target_selector, damage_type, and at least one dice term"
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
            SourceTraces = ProjectSourceTraces(source.source_traces),
            StateSchemas = ProjectStateSchemas(source.state_schemas),
            Reactions = ProjectReactions(source.reactions),
            GrantedActions = ProjectGrantedActions(source.granted_actions),
            WeaponProfileOverlays = ProjectWeaponProfileOverlays(source.weapon_profile_overlays),
            WorldEffects = ProjectWorldEffects(source.world_effects),
            ResourcePath = source.ResourcePath ?? "",
        };
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

    private static IReadOnlyList<EquipmentAbilitySourceTraceDefinition> ProjectSourceTraces(
        Godot.Collections.Array<EquipmentAbilitySourceTraceDef> values
    )
    {
        var result = new List<EquipmentAbilitySourceTraceDefinition>();
        foreach (EquipmentAbilitySourceTraceDef value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentAbilitySourceTraceDefinition
                {
                    SourceKind = EquipmentAbilitySourceTraceKind.ByFamily,
                    SourceFile = value.source_file ?? "",
                    ItemId = value.item_id,
                    DisplayName = value.display_name ?? "",
                    BulletIndex = value.bullet_index,
                    BulletTitle = value.bullet_title ?? "",
                    BulletText = value.bullet_text ?? "",
                    MechanismFamily = value.mechanism_family,
                    CoverageStatus = ParseCoverageStatus(value.coverage_status),
                    Phase = ParsePhase(value.phase),
                    TestId = value.test_id,
                    Note = value.note ?? "",
                }
            );
        }
        return new ReadOnlyCollection<EquipmentAbilitySourceTraceDefinition>(result);
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
                DamageTags = CopyStringNames(damage.damage_tags),
            },
            ApplyStatusActionPayloadDef status => new ApplyStatusActionPayloadDefinition
            {
                TargetSelector = status.target_selector,
                StatusId = status.status_id,
                DurationTurns = status.duration_turns,
                StackDelta = status.stack_delta,
            },
            ModifyAbilityStateActionPayloadDef state => new ModifyAbilityStateActionPayloadDefinition
            {
                TargetSelector = state.target_selector,
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
            },
            GrantSkillActionPayloadDef grant => new GrantSkillActionPayloadDefinition
            {
                SkillId = grant.skill_id,
                SkillLevel = grant.skill_level,
                AvailabilityStateKey = grant.availability_state_key,
            },
            EquipmentDurabilityDamageActionPayloadDef durability =>
                new EquipmentDurabilityDamageActionPayloadDefinition
                {
                    TargetSelector = durability.target_selector,
                    TargetSlots = CopyStringNames(durability.target_slots),
                    SlotWeightMap = CopyStringNameIntMap(durability.slot_weight_map),
                    RequiredItemTags = CopyStringNames(durability.required_item_tags),
                    RequiredEquipmentTypeIds = CopyStringNames(durability.required_equipment_type_ids),
                    DurabilityLoss = durability.durability_loss,
                    SaveTag = durability.save_tag,
                    SaveDc = durability.save_dc,
                    RequireAttackSuccess = durability.require_attack_success,
                    MaxDamagedItems = durability.max_damaged_items,
                },
            _ => null,
        };
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
            result.Add(
                new EquipmentGrantedActionDefinition
                {
                    GrantedActionId = value.granted_action_id,
                    GrantedKind = TryParseGrantedKind(value.granted_kind, out var grantedKind)
                        ? grantedKind
                        : EquipmentGrantedActionKind.Skill,
                    SkillId = value.skill_id,
                    SkillLevel = value.skill_level,
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

    private static IReadOnlyDictionary<StringName, int> CopyStringNameIntMap(GDictionary values)
    {
        var result = new Dictionary<StringName, int>();
        if (values == null)
            return new ReadOnlyDictionary<StringName, int>(result);
        foreach (Variant rawKey in values.Keys)
        {
            StringName key = ProgressionDataUtils.to_string_name(rawKey);
            if (key == "")
                continue;
            Variant rawValue = values[rawKey];
            if (rawValue.VariantType == Variant.Type.Int)
                result[key] = rawValue.AsInt32();
        }
        return new ReadOnlyDictionary<StringName, int>(result);
    }

    private static EquipmentAbilityCoverageStatus ParseCoverageStatus(StringName value)
    {
        if (value == "deferred")
            return EquipmentAbilityCoverageStatus.Deferred;
        if (value == "content_cut")
            return EquipmentAbilityCoverageStatus.ContentCut;
        return EquipmentAbilityCoverageStatus.Bound;
    }

    private static EquipmentAbilityContentPhase ParsePhase(StringName value)
    {
        if (value == "v2")
            return EquipmentAbilityContentPhase.V2;
        if (value == "v3")
            return EquipmentAbilityContentPhase.V3;
        return EquipmentAbilityContentPhase.V1;
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
        if (value == "on_battle_end")
        {
            trigger = EquipmentAbilityTriggerKind.OnBattleEnd;
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
        if (value == "after_battle")
        {
            timing = EquipmentAbilityTimingKind.AfterBattle;
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
