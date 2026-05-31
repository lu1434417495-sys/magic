using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

[GlobalClass]
public partial class BattleAiRuntimeActionPlan : RefCounted
{
    public StringName unit_id = "";
    public StringName brain_id = "";
    public string fingerprint = "";
    public GStringNameArray state_ids = new();
    public GDictionary actions_by_state = new();
    public GDictionary generated_actions_by_state = new();
    public GDictionary metadata_by_instance_id = new();
    public GDictionary skill_affordance_records_by_skill_id = new();
    public GStringArray warnings = new();
    public GStringArray errors = new();

    private readonly Dictionary<StringName, List<EnemyAiAction>> _actionsByState = new();
    private readonly Dictionary<StringName, List<EnemyAiAction>> _generatedActionsByState = new();
    private readonly Dictionary<long, RuntimeActionMetadata> _metadataByInstanceId = new();
    private readonly Dictionary<StringName, BattleAiSkillAffordanceRecord> _skillAffordanceRecordsBySkillId =
        new();

    public void set_source(BattleUnitState unit_state, EnemyAiBrainDef brain, GDictionary skill_defs)
    {
        unit_id = unit_state != null ? unit_state.unit_id : new StringName("");
        brain_id = brain != null ? ProgressionDataUtils.to_string_name(brain.brain_id) : "";
        fingerprint = build_fingerprint(unit_state, brain, skill_defs);
    }

    public void add_state_actions(StringName state_id, GArray actions)
    {
        AddStateActionsTyped(state_id, DecodeActionArray(actions));
    }

    internal void AddStateActionsTyped(StringName state_id, IEnumerable<EnemyAiAction> actions)
    {
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(state_id);
        if (normalizedStateId == "")
        {
            return;
        }
        EnsureState(normalizedStateId);
        List<EnemyAiAction> copiedActions = CopyActionList(actions);
        foreach (EnemyAiAction action in copiedActions)
        {
            if (!_metadataByInstanceId.ContainsKey(InstanceKey(action)))
            {
                SetActionMetadataTyped(
                    action,
                    RuntimeActionMetadata.ForAuthoredAction(normalizedStateId, action)
                );
            }
        }
        SetStateActions(normalizedStateId, copiedActions);
    }

    public void add_action(StringName state_id, EnemyAiAction action, GDictionary metadata = null)
    {
        if (action == null)
        {
            return;
        }
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(state_id);
        if (normalizedStateId == "")
        {
            return;
        }
        EnsureState(normalizedStateId);
        List<EnemyAiAction> stateActions = GetStateActions(normalizedStateId);
        stateActions.Add(action);
        SyncStateActionsMirror(normalizedStateId);

        GDictionary resolvedMetadata = metadata?.Duplicate(true) ?? new GDictionary();
        resolvedMetadata["state_id"] = normalizedStateId;
        if (!resolvedMetadata.ContainsKey("action_id"))
        {
            resolvedMetadata["action_id"] = ProgressionDataUtils.to_string_name(action.action_id);
        }
        if (!resolvedMetadata.ContainsKey("score_bucket_id"))
        {
            resolvedMetadata["score_bucket_id"] = ProgressionDataUtils.to_string_name(
                action.score_bucket_id
            );
        }
        RuntimeActionMetadata actionMetadata = SetActionMetadataTyped(action, resolvedMetadata);
        if (actionMetadata.generated)
        {
            List<EnemyAiAction> generatedActions = GetGeneratedActions(normalizedStateId);
            generatedActions.Add(action);
            SyncGeneratedActionsMirror(normalizedStateId);
        }
    }

    internal void AddGeneratedActionTyped(
        StringName state_id,
        EnemyAiAction action,
        StringName slot_id,
        StringName slot_role,
        StringName skill_id,
        StringName action_family,
        StringName source_action_id,
        string identity_key
    )
    {
        if (action == null)
        {
            return;
        }
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(state_id);
        if (normalizedStateId == "")
        {
            return;
        }
        EnsureState(normalizedStateId);
        List<EnemyAiAction> stateActions = GetStateActions(normalizedStateId);
        stateActions.Add(action);
        SyncStateActionsMirror(normalizedStateId);

        RuntimeActionMetadata metadata = RuntimeActionMetadata.ForGeneratedAction(
            normalizedStateId,
            action,
            slot_id,
            slot_role,
            skill_id,
            action_family,
            source_action_id,
            identity_key
        );
        SetActionMetadataTyped(action, metadata);
        List<EnemyAiAction> generatedActions = GetGeneratedActions(normalizedStateId);
        generatedActions.Add(action);
        SyncGeneratedActionsMirror(normalizedStateId);
    }

    public GArray get_actions(StringName state_id)
    {
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(state_id);
        if (!_actionsByState.TryGetValue(normalizedStateId, out List<EnemyAiAction> actions))
        {
            return new GArray();
        }
        return ToActionArray(actions);
    }

    internal IReadOnlyList<EnemyAiAction> GetTypedActions(StringName state_id)
    {
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(state_id);
        return _actionsByState.TryGetValue(normalizedStateId, out List<EnemyAiAction> actions)
            ? actions
            : System.Array.Empty<EnemyAiAction>();
    }

    internal bool HasActionIdentityKey(EnemyAiAction action, string identityKey)
    {
        if (action == null || string.IsNullOrEmpty(identityKey))
        {
            return false;
        }
        return _metadataByInstanceId.TryGetValue(
            InstanceKey(action),
            out RuntimeActionMetadata metadata
        ) && metadata.identity_key == identityKey;
    }

    internal bool TryGetSkillAffordances(
        StringName skill_id,
        out IReadOnlyList<StringName> affordances
    )
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skill_id);
        if (
            normalizedSkillId != ""
            && _skillAffordanceRecordsBySkillId.TryGetValue(
                normalizedSkillId,
                out BattleAiSkillAffordanceRecord record
            )
        )
        {
            affordances = record.affordances;
            return true;
        }
        affordances = System.Array.Empty<StringName>();
        return false;
    }

    public bool has_state(StringName state_id)
    {
        return _actionsByState.ContainsKey(ProgressionDataUtils.to_string_name(state_id));
    }

    public bool is_empty_state(StringName state_id)
    {
        return has_state(state_id) && get_actions(state_id).Count == 0;
    }

    public void set_action_metadata(EnemyAiAction action, GDictionary metadata)
    {
        SetActionMetadataTyped(action, metadata);
    }

    private RuntimeActionMetadata SetActionMetadataTyped(EnemyAiAction action, GDictionary metadata)
    {
        if (action == null)
        {
            return new RuntimeActionMetadata();
        }
        long instanceId = InstanceKey(action);
        RuntimeActionMetadata resolvedMetadata = RuntimeActionMetadata.FromDictionary(metadata, action);
        _metadataByInstanceId[instanceId] = resolvedMetadata;
        SyncMetadataMirror(instanceId);
        return resolvedMetadata;
    }

    private RuntimeActionMetadata SetActionMetadataTyped(
        EnemyAiAction action,
        RuntimeActionMetadata metadata
    )
    {
        if (action == null)
        {
            return new RuntimeActionMetadata();
        }
        long instanceId = InstanceKey(action);
        RuntimeActionMetadata resolvedMetadata =
            metadata ?? RuntimeActionMetadata.ForAuthoredAction("", action);
        resolvedMetadata.ApplyActionDefaults(action);
        _metadataByInstanceId[instanceId] = resolvedMetadata;
        SyncMetadataMirror(instanceId);
        return resolvedMetadata;
    }

    public GDictionary get_action_metadata(EnemyAiAction action)
    {
        if (action == null)
        {
            return new GDictionary();
        }
        long instanceId = InstanceKey(action);
        if (!_metadataByInstanceId.TryGetValue(instanceId, out RuntimeActionMetadata metadata))
        {
            return new GDictionary();
        }
        return metadata.ToDictionary();
    }

    public void set_skill_affordance_record(StringName skill_id, GDictionary record)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skill_id);
        if (normalizedSkillId == "")
        {
            return;
        }
        _skillAffordanceRecordsBySkillId[normalizedSkillId] =
            BattleAiSkillAffordanceRecord.FromDictionary(normalizedSkillId, record);
        SyncSkillAffordanceRecordMirror(normalizedSkillId);
    }

    internal void SetSkillAffordanceRecordTyped(BattleAiSkillAffordanceRecord record)
    {
        if (record == null)
        {
            return;
        }
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(record.skill_id);
        if (normalizedSkillId == "")
        {
            return;
        }
        BattleAiSkillAffordanceRecord storedRecord = record.Clone();
        storedRecord.skill_id = normalizedSkillId;
        _skillAffordanceRecordsBySkillId[normalizedSkillId] = storedRecord;
        SyncSkillAffordanceRecordMirror(normalizedSkillId);
    }

    internal bool TryGetSkillAffordanceRecordTyped(
        StringName skill_id,
        out BattleAiSkillAffordanceRecord record
    )
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skill_id);
        if (
            normalizedSkillId != ""
            && _skillAffordanceRecordsBySkillId.TryGetValue(
                normalizedSkillId,
                out BattleAiSkillAffordanceRecord storedRecord
            )
        )
        {
            record = storedRecord.Clone();
            return true;
        }
        record = null;
        return false;
    }

    public GDictionary get_skill_affordance_record(StringName skill_id)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skill_id);
        if (normalizedSkillId == "")
        {
            return new GDictionary();
        }
        if (
            !_skillAffordanceRecordsBySkillId.TryGetValue(
                normalizedSkillId,
                out BattleAiSkillAffordanceRecord record
            )
        )
        {
            return new GDictionary();
        }
        return record.ToDictionary();
    }

    public GStringArray validate()
    {
        var validationErrors = new GStringArray();
        if (unit_id == "")
        {
            validationErrors.Add("Runtime action plan is missing unit_id.");
        }
        if (brain_id == "")
        {
            validationErrors.Add("Runtime action plan is missing brain_id.");
        }
        foreach (StringName stateId in state_ids)
        {
            if (!_actionsByState.TryGetValue(stateId, out List<EnemyAiAction> stateActions))
            {
                validationErrors.Add(
                    $"Runtime action plan state {stateId} actions payload is invalid."
                );
                continue;
            }
            foreach (EnemyAiAction action in stateActions)
            {
                if (action == null)
                {
                    validationErrors.Add(
                        $"Runtime action plan state {stateId} contains null action."
                    );
                    continue;
                }
                if (!_metadataByInstanceId.ContainsKey(InstanceKey(action)))
                {
                    validationErrors.Add(
                        $"Runtime action plan action {ProgressionDataUtils.to_string_name(action.action_id)} is missing metadata."
                    );
                }
            }
        }
        errors = validationErrors.Duplicate();
        return validationErrors;
    }

    public bool is_stale_for(
        BattleUnitState unit_state,
        EnemyAiBrainDef brain,
        GDictionary skill_defs
    )
    {
        return fingerprint != build_fingerprint(unit_state, brain, skill_defs);
    }

    public static string build_fingerprint(
        BattleUnitState unit_state,
        EnemyAiBrainDef brain,
        GDictionary skill_defs
    )
    {
        var parts = new List<string>
        {
            $"unit={(unit_state != null ? unit_state.unit_id.ToString() : "")}",
            $"brain={(brain != null ? ProgressionDataUtils.to_string_name(brain.brain_id).ToString() : "")}",
            $"skills={BuildSkillSignature(unit_state)}",
            $"brain_shape={BuildBrainShapeSignature(brain)}",
        };
        return string.Join("|", parts);
    }

    private void EnsureState(StringName stateId)
    {
        if (!state_ids.Contains(stateId))
        {
            state_ids.Add(stateId);
        }
        if (!_actionsByState.ContainsKey(stateId))
        {
            _actionsByState[stateId] = new List<EnemyAiAction>();
            SyncStateActionsMirror(stateId);
        }
    }

    private List<EnemyAiAction> GetStateActions(StringName stateId)
    {
        if (!_actionsByState.TryGetValue(stateId, out List<EnemyAiAction> actions))
        {
            actions = new List<EnemyAiAction>();
            _actionsByState[stateId] = actions;
            SyncStateActionsMirror(stateId);
        }
        return actions;
    }

    private List<EnemyAiAction> GetGeneratedActions(StringName stateId)
    {
        if (!_generatedActionsByState.TryGetValue(stateId, out List<EnemyAiAction> actions))
        {
            actions = new List<EnemyAiAction>();
            _generatedActionsByState[stateId] = actions;
        }
        return actions;
    }

    private void SetStateActions(StringName stateId, List<EnemyAiAction> actions)
    {
        _actionsByState[stateId] = actions ?? new List<EnemyAiAction>();
        SyncStateActionsMirror(stateId);
    }

    private void SyncStateActionsMirror(StringName stateId)
    {
        actions_by_state[stateId] = _actionsByState.TryGetValue(
            stateId,
            out List<EnemyAiAction> actions
        )
            ? ToActionArray(actions)
            : new GArray();
    }

    private void SyncGeneratedActionsMirror(StringName stateId)
    {
        generated_actions_by_state[stateId] = _generatedActionsByState.TryGetValue(
            stateId,
            out List<EnemyAiAction> actions
        )
            ? ToActionArray(actions)
            : new GArray();
    }

    private void SyncMetadataMirror(long instanceId)
    {
        if (_metadataByInstanceId.TryGetValue(instanceId, out RuntimeActionMetadata metadata))
        {
            metadata_by_instance_id[instanceId] = metadata.ToDictionary();
        }
    }

    private void SyncSkillAffordanceRecordMirror(StringName skillId)
    {
        if (
            _skillAffordanceRecordsBySkillId.TryGetValue(
                skillId,
                out BattleAiSkillAffordanceRecord record
            )
        )
        {
            skill_affordance_records_by_skill_id[skillId] = record.ToDictionary();
        }
    }

    private static GArray ToActionArray(IEnumerable<EnemyAiAction> actions)
    {
        var result = new GArray();
        if (actions == null)
        {
            return result;
        }
        foreach (EnemyAiAction action in actions)
        {
            if (action != null)
            {
                result.Add(action);
            }
        }
        return result;
    }

    private static List<EnemyAiAction> DecodeActionArray(GArray actions)
    {
        var result = new List<EnemyAiAction>();
        foreach (RuntimeActionArrayValue actionValue in ReadActionArrayValues(actions))
        {
            if (actionValue.TryGetAction(out EnemyAiAction action))
            {
                result.Add(action);
            }
        }
        return result;
    }

    private readonly struct RuntimeActionArrayValue
    {
        public RuntimeActionArrayValue(Variant value)
        {
            Value = value;
        }

        public Variant Value { get; }

        public bool TryGetAction(out EnemyAiAction action)
        {
            action = Value.AsGodotObject() as EnemyAiAction;
            return action != null;
        }
    }

    private static List<RuntimeActionArrayValue> ReadActionArrayValues(GArray actions)
    {
        var result = new List<RuntimeActionArrayValue>();
        if (actions == null)
        {
            return result;
        }
        foreach (var rawAction in actions)
        {
            result.Add(new RuntimeActionArrayValue(rawAction));
        }
        return result;
    }

    private static List<EnemyAiAction> CopyActionList(IEnumerable<EnemyAiAction> actions)
    {
        var result = new List<EnemyAiAction>();
        if (actions == null)
        {
            return result;
        }
        foreach (EnemyAiAction action in actions)
        {
            if (action != null)
            {
                result.Add(action);
            }
        }
        return result;
    }

    private static string BuildSkillSignature(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return "";
        }
        var entries = new List<string>();
        foreach (StringName rawSkillId in unitState.known_active_skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (skillId == "")
            {
                continue;
            }
            int level = GetKnownSkillLevel(unitState, skillId);
            entries.Add($"{skillId}:{level}");
        }
        entries.Sort(System.StringComparer.Ordinal);
        return string.Join(",", entries);
    }

    private static int GetKnownSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (
            unitState?.known_skill_level_map == null
            || !unitState.known_skill_level_map.ContainsKey(skillId)
        )
        {
            return 1;
        }
        return unitState.known_skill_level_map[skillId].AsInt32();
    }

    private static string BuildBrainShapeSignature(EnemyAiBrainDef brain)
    {
        if (brain == null)
        {
            return "";
        }
        var stateEntries = new List<string>();
        foreach (EnemyAiStateDef stateDef in brain.get_resolved_states())
        {
            if (stateDef == null)
            {
                continue;
            }
            var actionEntries = new List<string>();
            foreach (EnemyAiAction action in stateDef.GetTypedActions())
            {
                var declaredSkillIds = new List<string>();
                foreach (StringName skillId in action.get_declared_skill_ids())
                {
                    declaredSkillIds.Add(skillId.ToString());
                }
                declaredSkillIds.Sort(System.StringComparer.Ordinal);
                string scriptPath = "";
                GodotObject script = action.GetScript().AsGodotObject();
                if (script is Resource scriptResource)
                {
                    scriptPath = scriptResource.ResourcePath;
                }
                actionEntries.Add(
                    string.Format(
                        "{0}:{1}:{2}:{3}",
                        ProgressionDataUtils.to_string_name(action.action_id),
                        scriptPath,
                        ProgressionDataUtils.to_string_name(action.score_bucket_id),
                        string.Join(",", declaredSkillIds)
                    )
                );
            }

            var slotEntries = new List<string>();
            foreach (
                EnemyAiGenerationSlotDef slot in stateDef.GetTypedGenerationSlots()
            )
            {
                slotEntries.Add(slot.to_signature().ToString());
            }
            stateEntries.Add(
                $"{stateDef.state_id}{{actions=[{string.Join(";", actionEntries)}];slots=[{string.Join(";", slotEntries)}]}}"
            );
        }

        var transitionEntries = new List<string>();
        foreach (EnemyAiTransitionRuleDef rule in brain.transition_rules)
        {
            if (rule != null)
            {
                transitionEntries.Add(rule.to_signature());
            }
        }
        transitionEntries.Sort(System.StringComparer.Ordinal);
        return $"states={string.Join("||", stateEntries)}|transitions={string.Join("||", transitionEntries)}";
    }

    private static long InstanceKey(EnemyAiAction action)
    {
        return unchecked((long)action.GetInstanceId());
    }

    private sealed class RuntimeActionMetadata
    {
        public bool generated;
        public StringName state_id = "";
        public StringName slot_id = "";
        public StringName slot_role = "";
        public StringName skill_id = "";
        public StringName variant_id = "";
        public StringName action_family = "";
        public StringName source_action_id = "";
        public StringName score_bucket_id = "";
        public StringName action_id = "";
        public string identity_key = "";
        public RuntimeActionExportMetadata runtime_action_metadata = new();
        private List<RuntimeMetadataExtraField> _extra_fields = new();

        public static RuntimeActionMetadata ForAuthoredAction(
            StringName stateId,
            EnemyAiAction action
        )
        {
            var result = new RuntimeActionMetadata
            {
                generated = false,
                state_id = ProgressionDataUtils.to_string_name(stateId),
                score_bucket_id =
                    action != null ? ProgressionDataUtils.to_string_name(action.score_bucket_id) : "",
                action_id =
                    action != null ? ProgressionDataUtils.to_string_name(action.action_id) : "",
            };
            result.ApplyActionDefaults(action);
            return result;
        }

        public static RuntimeActionMetadata ForGeneratedAction(
            StringName stateId,
            EnemyAiAction action,
            StringName slotId,
            StringName slotRole,
            StringName skillId,
            StringName actionFamily,
            StringName sourceActionId,
            string identityKey
        )
        {
            var result = new RuntimeActionMetadata
            {
                generated = true,
                state_id = ProgressionDataUtils.to_string_name(stateId),
                slot_id = ProgressionDataUtils.to_string_name(slotId),
                slot_role = ProgressionDataUtils.to_string_name(slotRole),
                skill_id = ProgressionDataUtils.to_string_name(skillId),
                variant_id = "",
                action_family = ProgressionDataUtils.to_string_name(actionFamily),
                source_action_id = ProgressionDataUtils.to_string_name(sourceActionId),
                score_bucket_id =
                    action != null ? ProgressionDataUtils.to_string_name(action.score_bucket_id) : "",
                action_id =
                    action != null ? ProgressionDataUtils.to_string_name(action.action_id) : "",
                identity_key = identityKey ?? "",
                runtime_action_metadata = RuntimeActionExportMetadata.ForGeneratedAction(
                    stateId,
                    slotId,
                    slotRole,
                    skillId,
                    actionFamily,
                    sourceActionId,
                    identityKey
                ),
            };
            result.ApplyActionDefaults(action);
            return result;
        }

        public static RuntimeActionMetadata FromDictionary(
            GDictionary metadata,
            EnemyAiAction action
        )
        {
            metadata ??= new GDictionary();
            var result = new RuntimeActionMetadata
            {
                generated = ReadBool(metadata, "generated"),
                state_id = ReadStringName(metadata, "state_id"),
                slot_id = ReadStringName(metadata, "slot_id"),
                slot_role = ReadStringName(metadata, "slot_role"),
                skill_id = ReadStringName(metadata, "skill_id"),
                variant_id = ReadStringName(metadata, "variant_id"),
                action_family = ReadStringName(metadata, "action_family"),
                source_action_id = ReadStringName(metadata, "source_action_id"),
                score_bucket_id = ReadStringName(metadata, "score_bucket_id"),
                action_id = ReadStringName(metadata, "action_id"),
                identity_key = ReadString(metadata, "identity_key"),
                runtime_action_metadata = RuntimeActionExportMetadata.FromDictionary(
                    ReadDictionary(metadata, "runtime_action_metadata")
                ),
                _extra_fields = ReadExtraFields(metadata),
            };

            if (result.action_id == "" && action != null)
            {
                result.action_id = ProgressionDataUtils.to_string_name(action.action_id);
            }
            if (result.score_bucket_id == "" && action != null)
            {
                result.score_bucket_id = ProgressionDataUtils.to_string_name(
                    action.score_bucket_id
                );
            }
            return result;
        }

        public void ApplyActionDefaults(EnemyAiAction action)
        {
            if (action_id == "" && action != null)
            {
                action_id = ProgressionDataUtils.to_string_name(action.action_id);
            }
            if (score_bucket_id == "" && action != null)
            {
                score_bucket_id = ProgressionDataUtils.to_string_name(action.score_bucket_id);
            }
        }

        public GDictionary ToDictionary()
        {
            GDictionary result = ExtraFieldsToDictionary(_extra_fields);
            result["generated"] = generated;
            result["state_id"] = state_id;
            if (slot_id != "")
            {
                result["slot_id"] = slot_id;
            }
            if (slot_role != "")
            {
                result["slot_role"] = slot_role;
            }
            if (skill_id != "")
            {
                result["skill_id"] = skill_id;
            }
            if (variant_id != "")
            {
                result["variant_id"] = variant_id;
            }
            if (action_family != "")
            {
                result["action_family"] = action_family;
            }
            if (source_action_id != "")
            {
                result["source_action_id"] = source_action_id;
            }
            result["score_bucket_id"] = score_bucket_id;
            result["action_id"] = action_id;
            if (!string.IsNullOrEmpty(identity_key))
            {
                result["identity_key"] = identity_key;
            }
            if (!runtime_action_metadata.IsEmpty())
            {
                result["runtime_action_metadata"] = runtime_action_metadata.ToDictionary();
            }
            return result;
        }
    }

    private readonly struct RuntimeMetadataExtraField
    {
        public RuntimeMetadataExtraField(Variant key, Variant value)
        {
            Key = key;
            Value = value;
            KeyText = ReadRuntimeMetadataKey(key);
        }

        public Variant Key { get; }

        public Variant Value { get; }

        public string KeyText { get; }

        public RuntimeMetadataExtraField Clone() => new(Key, CloneVariantValue(Value));
    }

    private sealed class RuntimeActionExportMetadata
    {
        public bool generated;
        public StringName state_id = "";
        public StringName slot_id = "";
        public StringName slot_role = "";
        public StringName skill_id = "";
        public StringName variant_id = "";
        public StringName action_family = "";
        public StringName source_action_id = "";
        public string identity_key = "";

        public static RuntimeActionExportMetadata ForGeneratedAction(
            StringName stateId,
            StringName slotId,
            StringName slotRole,
            StringName skillId,
            StringName actionFamily,
            StringName sourceActionId,
            string identityKey
        )
        {
            return new RuntimeActionExportMetadata
            {
                generated = true,
                state_id = ProgressionDataUtils.to_string_name(stateId),
                slot_id = ProgressionDataUtils.to_string_name(slotId),
                slot_role = ProgressionDataUtils.to_string_name(slotRole),
                skill_id = ProgressionDataUtils.to_string_name(skillId),
                variant_id = "",
                action_family = ProgressionDataUtils.to_string_name(actionFamily),
                source_action_id = ProgressionDataUtils.to_string_name(sourceActionId),
                identity_key = identityKey ?? "",
            };
        }

        public static RuntimeActionExportMetadata FromDictionary(GDictionary metadata)
        {
            metadata ??= new GDictionary();
            return new RuntimeActionExportMetadata
            {
                generated = ReadBool(metadata, "generated"),
                state_id = ReadStringName(metadata, "state_id"),
                slot_id = ReadStringName(metadata, "slot_id"),
                slot_role = ReadStringName(metadata, "slot_role"),
                skill_id = ReadStringName(metadata, "skill_id"),
                variant_id = ReadStringName(metadata, "variant_id"),
                action_family = ReadStringName(metadata, "action_family"),
                source_action_id = ReadStringName(metadata, "source_action_id"),
                identity_key = ReadString(metadata, "identity_key"),
            };
        }

        public bool IsEmpty()
        {
            return !generated
                && state_id == ""
                && slot_id == ""
                && slot_role == ""
                && skill_id == ""
                && variant_id == ""
                && action_family == ""
                && source_action_id == ""
                && string.IsNullOrEmpty(identity_key);
        }

        public GDictionary ToDictionary()
        {
            var result = new GDictionary
            {
                ["generated"] = generated,
                ["state_id"] = state_id,
                ["slot_id"] = slot_id,
                ["slot_role"] = slot_role,
                ["skill_id"] = skill_id,
                ["variant_id"] = variant_id,
                ["action_family"] = action_family,
                ["source_action_id"] = source_action_id,
                ["identity_key"] = identity_key,
            };
            return result;
        }
    }

    private static List<RuntimeMetadataExtraField> ReadExtraFields(GDictionary source)
    {
        var result = new List<RuntimeMetadataExtraField>();
        if (source == null)
        {
            return result;
        }
        foreach (var keyValue in source.Keys)
        {
            string key = ReadRuntimeMetadataKey(keyValue);
            if (
                IsTypedMetadataKey(key)
                || BattleAiSkillAffordanceRecord.IsTypedKey(key)
                || !TryGetDictionaryValue(source, keyValue, out Variant value)
            )
            {
                continue;
            }
            result.Add(new RuntimeMetadataExtraField(keyValue, value));
        }
        return result;
    }

    private static GDictionary ExtraFieldsToDictionary(
        IReadOnlyList<RuntimeMetadataExtraField> fields
    )
    {
        var result = new GDictionary();
        if (fields == null)
        {
            return result;
        }
        foreach (RuntimeMetadataExtraField field in fields)
        {
            RuntimeMetadataExtraField clonedField = field.Clone();
            result[clonedField.Key] = clonedField.Value;
        }
        return result;
    }

    private static bool TryGetDictionaryValue(
        GDictionary dictionary,
        Variant key,
        out Variant value
    )
    {
        value = default;
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return false;
        }
        value = dictionary[key];
        return true;
    }

    private static Variant CloneVariantValue(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.Dictionary
                => Variant.From(value.AsGodotDictionary().Duplicate(true)),
            Variant.Type.Array => Variant.From(value.AsGodotArray().Duplicate(true)),
            _ => value,
        };
    }

    private static string ReadRuntimeMetadataKey(Variant key)
    {
        return key.VariantType switch
        {
            Variant.Type.String => key.AsString(),
            Variant.Type.StringName => key.AsStringName().ToString(),
            Variant.Type.Nil => "",
            _ => key.ToString(),
        };
    }

    private static string ReadString(GDictionary data, string key, string fallback = "")
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        if (value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName)
        {
            return value.ToString();
        }
        return fallback;
    }

    private static StringName ReadStringName(GDictionary data, string key)
    {
        string value = ReadString(data, key);
        return !string.IsNullOrEmpty(value) ? new StringName(value) : "";
    }

    private static bool ReadBool(GDictionary data, string key, bool fallback = false)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static GDictionary ReadDictionary(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return new GDictionary();
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new GDictionary();
    }

    private static bool IsTypedMetadataKey(string key)
    {
        return key
            is "generated"
                or "state_id"
                or "slot_id"
                or "slot_role"
                or "skill_id"
                or "variant_id"
                or "action_family"
                or "source_action_id"
                or "score_bucket_id"
                or "action_id"
                or "identity_key"
                or "runtime_action_metadata";
    }

}
