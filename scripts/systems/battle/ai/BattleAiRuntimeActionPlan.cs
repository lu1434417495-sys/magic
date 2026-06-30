using System.Collections.Generic;
using Godot;

internal sealed class BattleAiRuntimeActionPlan : System.IDisposable
{
    public StringName unit_id = "";
    public StringName brain_id = "";
    public string fingerprint = "";
    public List<string> warnings = new();
    public List<string> errors = new();

    private readonly Dictionary<StringName, List<EnemyAiAction>> _actionsByState = new();
    private readonly Dictionary<StringName, List<EnemyAiAction>> _generatedActionsByState = new();
    private readonly Dictionary<StringName, List<BattleAiRuntimeActionEntry>> _entriesByState =
        new();
    private readonly Dictionary<long, RuntimeActionMetadata> _metadataByInstanceId = new();
    private readonly Dictionary<StringName, BattleAiSkillAffordanceRecord> _skillAffordanceRecordsBySkillId =
        new();
    private readonly GodotTransientResourceScope _transientScope =
        new("BattleAiRuntimeActionPlan");
    private readonly RuntimeEnemyAiResourceFactory _enemyAiResourceFactory;
    private bool _disposed;

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
        List<BattleAiRuntimeActionEntry> entries = GetStateEntries(normalizedStateId);
        entries.Clear();
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
            entries.Add(
                BattleAiRuntimeActionEntry.FromResource(action, GetActionMetadata(action))
            );
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
        GetStateEntries(normalizedStateId)
            .Add(BattleAiRuntimeActionEntry.FromResource(action, actionMetadata));
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
        GetStateEntries(normalizedStateId)
            .Add(BattleAiRuntimeActionEntry.FromResource(action, metadata));
        List<EnemyAiAction> generatedActions = GetGeneratedActions(normalizedStateId);
        generatedActions.Add(action);
    }

    internal void AddGeneratedMoveToRangeActionTyped(
        StringName state_id,
        BattleAiGeneratedMoveToRangeAction action,
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
        RuntimeActionMetadata metadata = RuntimeActionMetadata.ForGeneratedPlainAction(
            normalizedStateId,
            action.ActionId,
            action.ScoreBucketId,
            slot_id,
            slot_role,
            skill_id,
            action_family,
            source_action_id,
            identity_key
        );
        GetStateEntries(normalizedStateId)
            .Add(BattleAiRuntimeActionEntry.FromGeneratedMoveToRange(action, metadata));
    }

    internal void AddGeneratedUseUnitSkillActionTyped(
        StringName state_id,
        BattleAiUnitSkillActionSpec action,
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
        RuntimeActionMetadata metadata = RuntimeActionMetadata.ForGeneratedPlainAction(
            normalizedStateId,
            action.ActionId,
            action.ScoreBucketId,
            slot_id,
            slot_role,
            skill_id,
            action_family,
            source_action_id,
            identity_key
        );
        GetStateEntries(normalizedStateId)
            .Add(BattleAiRuntimeActionEntry.FromGeneratedUseUnitSkill(action, metadata));
    }

    internal void AddGeneratedRandomChainSkillActionTyped(
        StringName state_id,
        BattleAiRandomChainSkillActionSpec action,
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
        RuntimeActionMetadata metadata = RuntimeActionMetadata.ForGeneratedPlainAction(
            normalizedStateId,
            action.ActionId,
            action.ScoreBucketId,
            slot_id,
            slot_role,
            skill_id,
            action_family,
            source_action_id,
            identity_key
        );
        GetStateEntries(normalizedStateId)
            .Add(BattleAiRuntimeActionEntry.FromGeneratedRandomChainSkill(action, metadata));
    }

    internal void AddGeneratedMultiUnitSkillActionTyped(
        StringName state_id,
        BattleAiMultiUnitSkillActionSpec action,
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
        RuntimeActionMetadata metadata = RuntimeActionMetadata.ForGeneratedPlainAction(
            normalizedStateId,
            action.ActionId,
            action.ScoreBucketId,
            slot_id,
            slot_role,
            skill_id,
            action_family,
            source_action_id,
            identity_key
        );
        GetStateEntries(normalizedStateId)
            .Add(BattleAiRuntimeActionEntry.FromGeneratedMultiUnitSkill(action, metadata));
    }

    internal void AddGeneratedMoveToMultiUnitSkillPositionActionTyped(
        StringName state_id,
        BattleAiMoveToMultiUnitSkillPositionActionSpec action,
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
        RuntimeActionMetadata metadata = RuntimeActionMetadata.ForGeneratedPlainAction(
            normalizedStateId,
            action.ActionId,
            action.ScoreBucketId,
            slot_id,
            slot_role,
            skill_id,
            action_family,
            source_action_id,
            identity_key
        );
        GetStateEntries(normalizedStateId)
            .Add(
                BattleAiRuntimeActionEntry.FromGeneratedMoveToMultiUnitSkillPosition(
                    action,
                    metadata
                )
            );
    }

    internal void AddGeneratedChargeActionTyped(
        StringName state_id,
        BattleAiChargeActionSpec action,
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
        RuntimeActionMetadata metadata = RuntimeActionMetadata.ForGeneratedPlainAction(
            normalizedStateId,
            action.ActionId,
            action.ScoreBucketId,
            slot_id,
            slot_role,
            skill_id,
            action_family,
            source_action_id,
            identity_key
        );
        GetStateEntries(normalizedStateId)
            .Add(BattleAiRuntimeActionEntry.FromGeneratedCharge(action, metadata));
    }

    internal void AddGeneratedChargePathAoeActionTyped(
        StringName state_id,
        BattleAiChargePathAoeActionSpec action,
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
        RuntimeActionMetadata metadata = RuntimeActionMetadata.ForGeneratedPlainAction(
            normalizedStateId,
            action.ActionId,
            action.ScoreBucketId,
            slot_id,
            slot_role,
            skill_id,
            action_family,
            source_action_id,
            identity_key
        );
        GetStateEntries(normalizedStateId)
            .Add(BattleAiRuntimeActionEntry.FromGeneratedChargePathAoe(action, metadata));
    }

    internal void AddGeneratedGroundSkillActionTyped(
        StringName state_id,
        BattleAiGroundSkillActionSpec action,
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
        RuntimeActionMetadata metadata = RuntimeActionMetadata.ForGeneratedPlainAction(
            normalizedStateId,
            action.ActionId,
            action.ScoreBucketId,
            slot_id,
            slot_role,
            skill_id,
            action_family,
            source_action_id,
            identity_key
        );
        GetStateEntries(normalizedStateId)
            .Add(BattleAiRuntimeActionEntry.FromGeneratedGroundSkill(action, metadata));
    }

    internal EnemyAiAction OwnRuntimeAction(EnemyAiAction action, string reason)
    {
        return _enemyAiResourceFactory.OwnAction(action, reason);
    }

    internal Godot.Collections.Array<StringName> NewRuntimeStringNameArray(
        IEnumerable<StringName> values,
        string reason
    )
    {
        return _enemyAiResourceFactory.NewStringNameArray(values, reason);
    }

    internal void Clear()
    {
        _actionsByState.Clear();
        _generatedActionsByState.Clear();
        _entriesByState.Clear();
        _metadataByInstanceId.Clear();
        _skillAffordanceRecordsBySkillId.Clear();
        warnings.Clear();
        errors.Clear();
        unit_id = "";
        brain_id = "";
        fingerprint = "";
        _transientScope.Drain();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Clear();
        _transientScope.Dispose();
    }

    internal IReadOnlyList<EnemyAiAction> GetActions(StringName state_id)
    {
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(state_id);
        return _actionsByState.TryGetValue(normalizedStateId, out List<EnemyAiAction> actions)
            ? actions
            : System.Array.Empty<EnemyAiAction>();
    }

    internal IReadOnlyList<BattleAiRuntimeActionEntry> GetActionEntries(StringName state_id)
    {
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(state_id);
        return _entriesByState.TryGetValue(
            normalizedStateId,
            out List<BattleAiRuntimeActionEntry> entries
        )
            ? entries
            : System.Array.Empty<BattleAiRuntimeActionEntry>();
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

    internal bool HasActionIdentityKey(string identityKey)
    {
        if (string.IsNullOrEmpty(identityKey))
        {
            return false;
        }
        foreach (List<BattleAiRuntimeActionEntry> entries in _entriesByState.Values)
        {
            foreach (BattleAiRuntimeActionEntry entry in entries)
            {
                if (entry?.Metadata?.identity_key == identityKey)
                {
                    return true;
                }
            }
        }
        return false;
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
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(state_id);
        return _actionsByState.ContainsKey(normalizedStateId)
            || _entriesByState.ContainsKey(normalizedStateId);
    }

    public bool IsEmptyState(StringName state_id)
    {
        return HasState(state_id) && GetActionEntries(state_id).Count == 0;
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
            if (!_entriesByState.TryGetValue(stateId, out List<BattleAiRuntimeActionEntry> stateEntries))
            {
                validationErrors.Add(
                    $"Runtime action plan state {stateId} actions payload is invalid."
                );
                continue;
            }
            foreach (BattleAiRuntimeActionEntry entry in stateEntries)
            {
                if (entry == null)
                {
                    validationErrors.Add(
                        $"Runtime action plan state {stateId} contains null action."
                    );
                    continue;
                }
                if (entry.Metadata == null)
                {
                    validationErrors.Add(
                        $"Runtime action plan action {entry.ActionId} is missing metadata."
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
        if (!_entriesByState.ContainsKey(stateId))
        {
            _entriesByState[stateId] = new List<BattleAiRuntimeActionEntry>();
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

    private List<BattleAiRuntimeActionEntry> GetStateEntries(StringName stateId)
    {
        if (!_entriesByState.TryGetValue(stateId, out List<BattleAiRuntimeActionEntry> entries))
        {
            entries = new List<BattleAiRuntimeActionEntry>();
            _entriesByState[stateId] = entries;
        }
        return entries;
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
        BattleSkillAvailabilityService availabilityService = new(
            (IReadOnlyDictionary<StringName, SkillDefinition>)null
        );
        BattleSkillAvailabilityView availabilityView = availabilityService.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = unitState,
                Consumer = BattleSkillAvailabilityConsumer.AiPlanning,
                IncludeKnownSkills = true,
                IncludeEquipmentSkills = false,
                IncludeScopedAutoCast = false,
            }
        );
        foreach (BattleAvailableSkillEntry entry in availabilityView.SkillEntries)
        {
            StringName skillEntryId = ProgressionDataUtils.to_string_name(
                entry.EntryRef.SkillEntryId
            );
            StringName skillId = ProgressionDataUtils.to_string_name(entry.EntryRef.SkillId);
            if (skillEntryId == "" || skillId == "")
            {
                continue;
            }
            entries.Add($"{skillEntryId}:{skillId}:{entry.SkillLevel}");
        }
        entries.Sort(System.StringComparer.Ordinal);
        return string.Join(",", entries);
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
        public bool force_candidate_request_evaluation;
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
                force_candidate_request_evaluation = force_candidate_request_evaluation,
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
            result.force_candidate_request_evaluation = ShouldForceCandidateRequest(action);
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

        public static RuntimeActionMetadata ForGeneratedPlainAction(
            StringName stateId,
            StringName actionId,
            StringName scoreBucketId,
            StringName slotId,
            StringName slotRole,
            StringName skillId,
            StringName actionFamily,
            StringName sourceActionId,
            string identityKey
        )
        {
            return new RuntimeActionMetadata
            {
                generated = true,
                state_id = ProgressionDataUtils.to_string_name(stateId),
                slot_id = ProgressionDataUtils.to_string_name(slotId),
                slot_role = ProgressionDataUtils.to_string_name(slotRole),
                skill_id = ProgressionDataUtils.to_string_name(skillId),
                variant_id = "",
                action_family = ProgressionDataUtils.to_string_name(actionFamily),
                source_action_id = ProgressionDataUtils.to_string_name(sourceActionId),
                score_bucket_id = ProgressionDataUtils.to_string_name(scoreBucketId),
                action_id = ProgressionDataUtils.to_string_name(actionId),
                identity_key = identityKey ?? "",
                force_candidate_request_evaluation = true,
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

        private static bool ShouldForceCandidateRequest(EnemyAiAction action)
        {
            return action is MoveToRangeAction moveToRange
                && moveToRange.CanUseGeneratedCandidateRequestMode();
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
