using System.Collections.Generic;
using Godot;

internal sealed class BattleAiRuntimeActionPlan
{
    public StringName unit_id = "";
    public StringName brain_id = "";
    public string fingerprint = "";
    public List<string> warnings = new();
    public List<string> errors = new();

    private readonly Dictionary<StringName, List<EnemyAiAction>> _actionsByState = new();
    private readonly Dictionary<StringName, List<EnemyAiAction>> _generatedActionsByState = new();
    private readonly Dictionary<long, RuntimeActionMetadata> _metadataByInstanceId = new();
    private readonly Dictionary<StringName, BattleAiSkillAffordanceRecord> _skillAffordanceRecordsBySkillId =
        new();
    private readonly GodotTransientResourceScope _transientScope =
        new("BattleAiRuntimeActionPlan");
    private readonly RuntimeEnemyAiResourceFactory _enemyAiResourceFactory;

    internal BattleAiRuntimeActionPlan()
    {
        _enemyAiResourceFactory = new RuntimeEnemyAiResourceFactory(
            _transientScope,
            "BattleAiRuntimeActionPlan"
        );
    }

    public void SetSource(BattleUnitState unitState, EnemyAiBrainDef brain)
    {
        unit_id = unitState != null ? unitState.unit_id : new StringName("");
        brain_id = brain != null ? ProgressionDataUtils.to_string_name(brain.brain_id) : "";
        fingerprint = BuildFingerprint(unitState, brain);
    }

    internal void AddStateActions(StringName state_id, IEnumerable<EnemyAiAction> actions)
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
            GodotObjectOwnershipRegistry.AssertBorrowedOrOwnedKnown(
                action,
                "BattleAiRuntimeActionPlan.AddStateActions"
            );
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

    internal void AddAction(StringName state_id, EnemyAiAction action, RuntimeActionMetadata metadata = null)
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
        GodotObjectOwnershipRegistry.AssertBorrowedOrOwnedKnown(
            action,
            "BattleAiRuntimeActionPlan.AddAction"
        );
        List<EnemyAiAction> stateActions = GetStateActions(normalizedStateId);
        stateActions.Add(action);

        RuntimeActionMetadata actionMetadata =
            metadata ?? RuntimeActionMetadata.ForAuthoredAction(normalizedStateId, action);
        actionMetadata.state_id = normalizedStateId;
        actionMetadata.ApplyActionDefaults(action);
        SetActionMetadata(action, actionMetadata);
        if (actionMetadata.generated)
        {
            List<EnemyAiAction> generatedActions = GetGeneratedActions(normalizedStateId);
            generatedActions.Add(action);
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
        GodotObjectOwnershipRegistry.AssertBorrowedOrOwnedKnown(
            action,
            "BattleAiRuntimeActionPlan.AddGeneratedActionTyped"
        );
        List<EnemyAiAction> stateActions = GetStateActions(normalizedStateId);
        stateActions.Add(action);

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
    }

    internal EnemyAiAction OwnRuntimeAction(EnemyAiAction action, string reason)
    {
        return _enemyAiResourceFactory.OwnAction(action, reason);
    }

    internal void Clear()
    {
        _actionsByState.Clear();
        _generatedActionsByState.Clear();
        _metadataByInstanceId.Clear();
        _skillAffordanceRecordsBySkillId.Clear();
        warnings.Clear();
        errors.Clear();
        unit_id = "";
        brain_id = "";
        fingerprint = "";
        _transientScope.Drain();
    }

    internal IReadOnlyList<EnemyAiAction> GetActions(StringName state_id)
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

    public bool HasState(StringName state_id)
    {
        return _actionsByState.ContainsKey(ProgressionDataUtils.to_string_name(state_id));
    }

    public bool IsEmptyState(StringName state_id)
    {
        return HasState(state_id) && GetActions(state_id).Count == 0;
    }

    internal void SetActionMetadata(EnemyAiAction action, RuntimeActionMetadata metadata)
    {
        SetActionMetadataTyped(action, metadata);
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
        return resolvedMetadata;
    }

    internal RuntimeActionMetadata GetActionMetadata(EnemyAiAction action)
    {
        if (action == null)
        {
            return new RuntimeActionMetadata();
        }
        long instanceId = InstanceKey(action);
        if (!_metadataByInstanceId.TryGetValue(instanceId, out RuntimeActionMetadata metadata))
        {
            return new RuntimeActionMetadata();
        }
        return metadata.Clone();
    }

    internal void SetSkillAffordanceRecord(StringName skill_id, BattleAiSkillAffordanceRecord record)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skill_id);
        if (normalizedSkillId == "")
        {
            return;
        }
        BattleAiSkillAffordanceRecord storedRecord = record?.Clone();
        if (storedRecord == null)
        {
            return;
        }
        storedRecord.skill_id = normalizedSkillId;
        _skillAffordanceRecordsBySkillId[normalizedSkillId] = storedRecord;
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

    public List<string> Validate()
    {
        var validationErrors = new List<string>();
        if (unit_id == "")
        {
            validationErrors.Add("Runtime action plan is missing unit_id.");
        }
        if (brain_id == "")
        {
            validationErrors.Add("Runtime action plan is missing brain_id.");
        }
        foreach (StringName stateId in _actionsByState.Keys)
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
        errors = new List<string>(validationErrors);
        return validationErrors;
    }

    public bool IsStaleFor(BattleUnitState unitState, EnemyAiBrainDef brain)
    {
        return fingerprint != BuildFingerprint(unitState, brain);
    }

    public static string BuildFingerprint(BattleUnitState unitState, EnemyAiBrainDef brain)
    {
        var parts = new List<string>
        {
            $"unit={(unitState != null ? unitState.unit_id.ToString() : "")}",
            $"brain={(brain != null ? ProgressionDataUtils.to_string_name(brain.brain_id).ToString() : "")}",
            $"skills={BuildSkillSignature(unitState)}",
            $"brain_shape={BuildBrainShapeSignature(brain)}",
        };
        return string.Join("|", parts);
    }

    private void EnsureState(StringName stateId)
    {
        if (!_actionsByState.ContainsKey(stateId))
        {
            _actionsByState[stateId] = new List<EnemyAiAction>();
        }
    }

    private List<EnemyAiAction> GetStateActions(StringName stateId)
    {
        if (!_actionsByState.TryGetValue(stateId, out List<EnemyAiAction> actions))
        {
            actions = new List<EnemyAiAction>();
            _actionsByState[stateId] = actions;
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
        if (unitState == null)
        {
            return 1;
        }
        return unitState.GetKnownSkillLevelTyped(skillId);
    }

    private static string BuildBrainShapeSignature(EnemyAiBrainDef brain)
    {
        if (brain == null)
        {
            return "";
        }
        var stateEntries = new List<string>();
        foreach (EnemyAiStateDef stateDef in brain.GetResolvedStates())
        {
            if (stateDef == null)
            {
                continue;
            }
            var actionEntries = new List<string>();
            foreach (EnemyAiAction action in stateDef.GetTypedActions())
            {
                var declaredSkillIds = new List<string>();
                foreach (StringName skillId in action.GetDeclaredSkillIds())
                {
                    declaredSkillIds.Add(skillId.ToString());
                }
                declaredSkillIds.Sort(System.StringComparer.Ordinal);
                string scriptPath = "";
                Resource scriptResource = action.GetScript().As<Resource>();
                if (scriptResource != null)
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
                slotEntries.Add(slot.BuildSignature());
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
                transitionEntries.Add(rule.ToSignature());
            }
        }
        transitionEntries.Sort(System.StringComparer.Ordinal);
        return $"states={string.Join("||", stateEntries)}|transitions={string.Join("||", transitionEntries)}";
    }

    private static long InstanceKey(EnemyAiAction action)
    {
        return unchecked((long)action.GetInstanceId());
    }

    internal sealed class RuntimeActionMetadata
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

        public RuntimeActionMetadata Clone()
        {
            return new RuntimeActionMetadata
            {
                generated = generated,
                state_id = state_id,
                slot_id = slot_id,
                slot_role = slot_role,
                skill_id = skill_id,
                variant_id = variant_id,
                action_family = action_family,
                source_action_id = source_action_id,
                score_bucket_id = score_bucket_id,
                action_id = action_id,
                identity_key = identity_key ?? "",
                runtime_action_metadata = runtime_action_metadata?.Clone() ?? new RuntimeActionExportMetadata(),
            };
        }

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

    }

    internal sealed class RuntimeActionExportMetadata
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

        public RuntimeActionExportMetadata Clone()
        {
            return new RuntimeActionExportMetadata
            {
                generated = generated,
                state_id = state_id,
                slot_id = slot_id,
                slot_role = slot_role,
                skill_id = skill_id,
                variant_id = variant_id,
                action_family = action_family,
                source_action_id = source_action_id,
                identity_key = identity_key ?? "",
            };
        }

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

    }

}
