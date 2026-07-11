using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiRuntimeActionPlan : IDisposable
{
    public StringName unit_id = "";
    public StringName brain_id = "";
    public string fingerprint = "";
    public List<string> warnings = new();
    public List<string> errors = new();

    private readonly Dictionary<StringName, List<BattleAiRuntimeActionEntry>> _entriesByState =
        new();
    private readonly Dictionary<StringName, BattleAiSkillAffordanceRecord> _skillAffordanceRecordsBySkillId =
        new();
    private bool _disposed;

    internal bool HasRuntimeBorrowers =>
        _entriesByState.Count != 0 || _skillAffordanceRecordsBySkillId.Count != 0;

    public void SetSource(BattleUnitState unitState, EnemyAiBrainDefinition brain)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        unit_id = unitState?.unit_id ?? new StringName("");
        brain_id = brain?.BrainId ?? new StringName("");
        fingerprint = BuildFingerprint(unitState, brain);
    }

    internal void AddStateActions(
        StringName stateId,
        IEnumerable<EnemyAiActionDefinition> actions
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(stateId);
        if (normalizedStateId == "")
            return;

        List<BattleAiRuntimeActionEntry> entries = GetOrCreateStateEntries(normalizedStateId);
        entries.Clear();
        if (actions == null)
            return;

        foreach (EnemyAiActionDefinition action in actions)
        {
            if (action == null)
                continue;
            entries.Add(
                new BattleAiRuntimeActionEntry(
                    action,
                    RuntimeActionMetadata.ForAuthoredAction(normalizedStateId, action)
                )
            );
        }
    }

    internal void AddAction(
        StringName stateId,
        EnemyAiActionDefinition action,
        RuntimeActionMetadata metadata = null
    )
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (action == null)
            return;

        StringName normalizedStateId = ProgressionDataUtils.to_string_name(stateId);
        if (normalizedStateId == "")
            return;

        RuntimeActionMetadata resolvedMetadata =
            metadata?.Clone() ?? RuntimeActionMetadata.ForAuthoredAction(normalizedStateId, action);
        resolvedMetadata.state_id = normalizedStateId;
        resolvedMetadata.ApplyActionDefaults(action);
        GetOrCreateStateEntries(normalizedStateId)
            .Add(new BattleAiRuntimeActionEntry(action, resolvedMetadata));
    }

    internal void AddGeneratedAction(
        StringName stateId,
        EnemyAiActionDefinition action,
        StringName slotId,
        StringName slotRole,
        StringName skillId,
        StringName actionFamily,
        StringName sourceActionId,
        string identityKey
    )
    {
        if (action == null)
            return;
        AddAction(
            stateId,
            action,
            RuntimeActionMetadata.ForGeneratedAction(
                stateId,
                action,
                slotId,
                slotRole,
                skillId,
                actionFamily,
                sourceActionId,
                identityKey
            )
        );
    }

    internal void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ClearBorrowedState();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ClearBorrowedState();
    }

    internal IReadOnlyList<EnemyAiActionDefinition> GetActions(StringName stateId)
    {
        IReadOnlyList<BattleAiRuntimeActionEntry> entries = GetActionEntries(stateId);
        if (entries.Count == 0)
            return Array.Empty<EnemyAiActionDefinition>();

        var actions = new List<EnemyAiActionDefinition>(entries.Count);
        foreach (BattleAiRuntimeActionEntry entry in entries)
        {
            if (entry?.Action != null)
                actions.Add(entry.Action);
        }
        return actions;
    }

    internal IReadOnlyList<BattleAiRuntimeActionEntry> GetActionEntries(StringName stateId)
    {
        StringName normalizedStateId = ProgressionDataUtils.to_string_name(stateId);
        return _entriesByState.TryGetValue(
            normalizedStateId,
            out List<BattleAiRuntimeActionEntry> entries
        )
            ? entries
            : Array.Empty<BattleAiRuntimeActionEntry>();
    }

    internal bool HasActionIdentityKey(string identityKey)
    {
        if (string.IsNullOrEmpty(identityKey))
            return false;
        foreach (List<BattleAiRuntimeActionEntry> entries in _entriesByState.Values)
        {
            foreach (BattleAiRuntimeActionEntry entry in entries)
            {
                if (entry?.Metadata?.identity_key == identityKey)
                    return true;
            }
        }
        return false;
    }

    internal RuntimeActionMetadata GetActionMetadata(EnemyAiActionDefinition action)
    {
        if (action == null)
            return new RuntimeActionMetadata();
        foreach (List<BattleAiRuntimeActionEntry> entries in _entriesByState.Values)
        {
            foreach (BattleAiRuntimeActionEntry entry in entries)
            {
                if (ReferenceEquals(entry?.Action, action))
                    return entry.Metadata?.Clone() ?? new RuntimeActionMetadata();
            }
        }
        return new RuntimeActionMetadata();
    }

    internal bool TryGetSkillAffordances(
        StringName skillId,
        out IReadOnlyList<StringName> affordances
    )
    {
        if (
            TryGetSkillAffordanceRecordTyped(
                skillId,
                out BattleAiSkillAffordanceRecord record
            )
        )
        {
            affordances = record.affordances;
            return true;
        }
        affordances = Array.Empty<StringName>();
        return false;
    }

    public bool HasState(StringName stateId) =>
        _entriesByState.ContainsKey(ProgressionDataUtils.to_string_name(stateId));

    public bool IsEmptyState(StringName stateId) =>
        HasState(stateId) && GetActionEntries(stateId).Count == 0;

    internal void SetSkillAffordanceRecord(
        StringName skillId,
        BattleAiSkillAffordanceRecord record
    )
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        BattleAiSkillAffordanceRecord storedRecord = record?.Clone();
        if (normalizedSkillId == "" || storedRecord == null)
            return;
        storedRecord.skill_id = normalizedSkillId;
        _skillAffordanceRecordsBySkillId[normalizedSkillId] = storedRecord;
    }

    internal void SetSkillAffordanceRecordTyped(BattleAiSkillAffordanceRecord record)
    {
        if (record != null)
            SetSkillAffordanceRecord(record.skill_id, record);
    }

    internal bool TryGetSkillAffordanceRecordTyped(
        StringName skillId,
        out BattleAiSkillAffordanceRecord record
    )
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
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
            validationErrors.Add("Runtime action plan is missing unit_id.");
        if (brain_id == "")
            validationErrors.Add("Runtime action plan is missing brain_id.");

        foreach ((StringName stateId, List<BattleAiRuntimeActionEntry> stateEntries) in _entriesByState)
        {
            foreach (BattleAiRuntimeActionEntry entry in stateEntries)
            {
                if (entry == null)
                {
                    validationErrors.Add(
                        $"Runtime action plan state {stateId} contains null action."
                    );
                    continue;
                }
                if (entry.Action == null)
                    validationErrors.Add(
                        $"Runtime action plan state {stateId} contains null action definition."
                    );
                if (entry.Metadata == null)
                    validationErrors.Add(
                        $"Runtime action plan action {entry.ActionId} is missing metadata."
                    );
            }
        }
        errors = new List<string>(validationErrors);
        return validationErrors;
    }

    public bool IsStaleFor(BattleUnitState unitState, EnemyAiBrainDefinition brain) =>
        fingerprint != BuildFingerprint(unitState, brain);

    public static string BuildFingerprint(
        BattleUnitState unitState,
        EnemyAiBrainDefinition brain
    )
    {
        var parts = new List<string>
        {
            $"unit={unitState?.unit_id.ToString() ?? ""}",
            $"brain={brain?.BrainId.ToString() ?? ""}",
            $"skills={BuildSkillSignature(unitState)}",
            $"brain_shape={BuildBrainShapeSignature(brain)}",
        };
        return string.Join("|", parts);
    }

    private void ClearBorrowedState()
    {
        _entriesByState.Clear();
        _skillAffordanceRecordsBySkillId.Clear();
        warnings.Clear();
        errors.Clear();
        unit_id = "";
        brain_id = "";
        fingerprint = "";
    }

    private List<BattleAiRuntimeActionEntry> GetOrCreateStateEntries(StringName stateId)
    {
        if (!_entriesByState.TryGetValue(stateId, out List<BattleAiRuntimeActionEntry> entries))
        {
            entries = new List<BattleAiRuntimeActionEntry>();
            _entriesByState[stateId] = entries;
        }
        return entries;
    }

    private static string BuildSkillSignature(BattleUnitState unitState)
    {
        if (unitState == null)
            return "";

        var entries = new List<string>();
        BattleSkillAvailabilityView availabilityView = new BattleSkillAvailabilityService(
            (IReadOnlyDictionary<StringName, SkillDefinition>)null
        ).BuildView(
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
            StringName entryId = ProgressionDataUtils.to_string_name(
                entry.EntryRef.SkillEntryId
            );
            StringName skillId = ProgressionDataUtils.to_string_name(entry.EntryRef.SkillId);
            if (entryId != "" && skillId != "")
                entries.Add($"{entryId}:{skillId}:{entry.SkillLevel}");
        }
        entries.Sort(StringComparer.Ordinal);
        return string.Join(",", entries);
    }

    private static string BuildBrainShapeSignature(EnemyAiBrainDefinition brain)
    {
        if (brain == null)
            return "";

        var stateEntries = new List<string>();
        foreach (EnemyAiStateDefinition state in brain.StateOrder)
        {
            if (state == null)
                continue;
            var actionEntries = new List<string>();
            foreach (EnemyAiActionDefinition action in state.Actions)
            {
                if (action == null)
                    continue;
                var declaredSkillIds = new List<string>();
                foreach (StringName skillId in action.DeclaredSkillIds)
                    declaredSkillIds.Add(skillId.ToString());
                declaredSkillIds.Sort(StringComparer.Ordinal);
                actionEntries.Add(
                    $"{action.ActionId}:{GetAuthoredActionScriptPath(action.Kind)}:{action.ScoreBucketId}:{string.Join(",", declaredSkillIds)}"
                );
            }

            var slotEntries = new List<string>();
            foreach (EnemyAiGenerationSlotDefinition slot in state.GenerationSlots)
            {
                if (slot != null)
                    slotEntries.Add(slot.BuildSignature());
            }
            stateEntries.Add(
                $"{state.StateId}{{actions=[{string.Join(";", actionEntries)}];slots=[{string.Join(";", slotEntries)}]}}"
            );
        }

        var transitionEntries = new List<string>();
        foreach (EnemyAiTransitionRuleDefinition rule in brain.TransitionRules)
        {
            if (rule != null)
                transitionEntries.Add(BuildTransitionSignature(rule));
        }
        transitionEntries.Sort(StringComparer.Ordinal);
        return $"states={string.Join("||", stateEntries)}|transitions={string.Join("||", transitionEntries)}";
    }

    private static string BuildTransitionSignature(EnemyAiTransitionRuleDefinition rule)
    {
        var fromStateIds = new List<string>();
        foreach (StringName stateId in rule.FromStateIds)
            fromStateIds.Add(stateId.ToString());
        var conditions = new List<string>();
        foreach (EnemyAiTransitionConditionDefinition condition in rule.Conditions)
        {
            if (condition != null)
                conditions.Add(BuildTransitionConditionSignature(condition));
        }
        return $"{rule.Order}:{rule.RuleId}:{rule.TargetStateId}:from=[{string.Join(",", fromStateIds)}]:conditions=[{string.Join(";", conditions)}]";
    }

    private static string BuildTransitionConditionSignature(
        EnemyAiTransitionConditionDefinition condition
    )
    {
        var stateIds = new List<string>();
        foreach (StringName stateId in condition.StateIds)
            stateIds.Add(stateId.ToString());
        var affordances = new List<string>();
        foreach (StringName affordance in condition.Affordances)
            affordances.Add(affordance.ToString());
        return $"{condition.Predicate}(bp={condition.BasisPoints},dist={condition.MaxDistance},states={string.Join(",", stateIds)},affordances={string.Join(",", affordances)})";
    }

    private static string GetAuthoredActionScriptPath(EnemyAiActionKind kind) =>
        kind switch
        {
            EnemyAiActionKind.UseUnitSkill =>
                "res://scripts/enemies/actions/UseUnitSkillAction.cs",
            EnemyAiActionKind.UseGroundSkill =>
                "res://scripts/enemies/actions/UseGroundSkillAction.cs",
            EnemyAiActionKind.UseMultiUnitSkill =>
                "res://scripts/enemies/actions/UseMultiUnitSkillAction.cs",
            EnemyAiActionKind.MoveToMultiUnitSkillPosition =>
                "res://scripts/enemies/actions/MoveToMultiUnitSkillPositionAction.cs",
            EnemyAiActionKind.UseRandomChainSkill =>
                "res://scripts/enemies/actions/UseRandomChainSkillAction.cs",
            EnemyAiActionKind.UseCharge =>
                "res://scripts/enemies/actions/UseChargeAction.cs",
            EnemyAiActionKind.UseChargePathAoe =>
                "res://scripts/enemies/actions/UseChargePathAoeAction.cs",
            EnemyAiActionKind.MoveToRange =>
                "res://scripts/enemies/actions/MoveToRangeAction.cs",
            EnemyAiActionKind.MoveToAdvantagePosition =>
                "res://scripts/enemies/actions/MoveToAdvantagePositionAction.cs",
            EnemyAiActionKind.UseGroundRepositionSkill =>
                "res://scripts/enemies/actions/UseGroundRepositionSkillAction.cs",
            EnemyAiActionKind.Retreat => "res://scripts/enemies/actions/RetreatAction.cs",
            EnemyAiActionKind.Wait => "res://scripts/enemies/actions/WaitAction.cs",
            _ => "",
        };

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

        public RuntimeActionMetadata Clone() =>
            new()
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
                runtime_action_metadata =
                    runtime_action_metadata?.Clone() ?? new RuntimeActionExportMetadata(),
            };

        public static RuntimeActionMetadata ForAuthoredAction(
            StringName stateId,
            EnemyAiActionDefinition action
        )
        {
            var result = new RuntimeActionMetadata
            {
                generated = false,
                state_id = ProgressionDataUtils.to_string_name(stateId),
                score_bucket_id = action?.ScoreBucketId ?? new StringName(""),
                action_id = action?.ActionId ?? new StringName(""),
                force_candidate_request_evaluation = ShouldForceCandidateRequest(action),
            };
            result.ApplyActionDefaults(action);
            return result;
        }

        public static RuntimeActionMetadata ForGeneratedAction(
            StringName stateId,
            EnemyAiActionDefinition action,
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
                score_bucket_id = action?.ScoreBucketId ?? new StringName(""),
                action_id = action?.ActionId ?? new StringName(""),
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
            result.ApplyActionDefaults(action);
            return result;
        }

        public void ApplyActionDefaults(EnemyAiActionDefinition action)
        {
            if (action_id == "" && action != null)
                action_id = action.ActionId;
            if (score_bucket_id == "" && action != null)
                score_bucket_id = action.ScoreBucketId;
        }

        private static bool ShouldForceCandidateRequest(EnemyAiActionDefinition action) =>
            action is MoveToRangeActionDefinition moveToRange
            && moveToRange.ScreeningMode == (StringName)"none";
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

        public RuntimeActionExportMetadata Clone() =>
            new()
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

        public static RuntimeActionExportMetadata ForGeneratedAction(
            StringName stateId,
            StringName slotId,
            StringName slotRole,
            StringName skillId,
            StringName actionFamily,
            StringName sourceActionId,
            string identityKey
        ) =>
            new()
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

        public bool IsEmpty() =>
            !generated
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
