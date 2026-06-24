using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleContingencyInstance
{
    internal BattleContingencyInstance(
        StringName instanceId,
        StringName setupId,
        StringName ownerMemberId,
        StringName ownerUnitId,
        ContingencyMatrixSetupState setup
    )
    {
        InstanceId = instanceId;
        SetupId = setupId;
        OwnerMemberId = ownerMemberId;
        OwnerUnitId = ownerUnitId;
        CasterUnitId = ownerUnitId;
        Setup = setup?.DuplicateState();
    }

    internal StringName InstanceId { get; }
    internal StringName SetupId { get; }
    internal StringName OwnerMemberId { get; }
    internal StringName OwnerUnitId { get; }
    internal StringName CasterUnitId { get; }
    internal ContingencyMatrixSetupState Setup { get; }
    internal bool Suppressed { get; private set; }

    internal void SetSuppressed(bool suppressed) => Suppressed = suppressed;
}

internal sealed class BattleContingencySystem : IBattleDamageApplicationHook, IDisposable
{
    private readonly Dictionary<StringName, BattleContingencyInstance> _instancesById = new();
    private readonly Dictionary<StringName, List<StringName>> _instanceIdsByMemberId = new();
    private readonly Dictionary<StringName, List<StringName>> _instanceIdsByTriggerType = new();
    private readonly Dictionary<StringName, HashSet<StringName>> _consumedSetupIdsByMemberId = new();
    private readonly HashSet<string> _queuedSourceEventKeys = new(StringComparer.Ordinal);
    private readonly Queue<ContingencyReleaseContext> _releaseQueue = new();
    private readonly Queue<AutoCastRequest> _sequentialAutoCastQueue = new();
    private readonly ContingencyTargetResolverService _targetResolver = new();
    private BattleRuntimeModule _runtime;
    private bool _disposed;

    internal void Setup(BattleRuntimeModule runtime)
    {
        _runtime = runtime;
    }

    internal void ResetForBattle(PartyState partyState, BattleState battleState)
    {
        ClearBattleState();
        if (partyState == null || battleState == null)
            return;

        foreach (StringName allyUnitId in battleState.ally_unit_ids)
        {
            if (!battleState.TryGetUnitTyped(allyUnitId, out BattleUnitState unitState))
                continue;
            StringName memberId = Normalize(unitState?.source_member_id ?? "");
            if (memberId == "")
                continue;
            PartyMemberState memberState = partyState.GetMemberState(memberId);
            if (memberState == null)
                continue;
            foreach (ContingencyMatrixSetupState setup in memberState.GetContingencySetupsTyped())
            {
                if (setup == null || !setup.Enabled || !setup.Charged || setup.SetupId == "")
                    continue;
                AddInstance(memberId, unitState.unit_id, setup);
            }
        }
    }

    internal void ClearBattleState()
    {
        _instancesById.Clear();
        _instanceIdsByMemberId.Clear();
        _instanceIdsByTriggerType.Clear();
        _consumedSetupIdsByMemberId.Clear();
        _queuedSourceEventKeys.Clear();
        _releaseQueue.Clear();
        _sequentialAutoCastQueue.Clear();
    }

    internal IReadOnlyList<BattleContingencyInstance> GetInstancesTyped()
    {
        List<BattleContingencyInstance> result = new(_instancesById.Values);
        result.Sort((left, right) => string.CompareOrdinal(left.InstanceId.ToString(), right.InstanceId.ToString()));
        return result;
    }

    internal IReadOnlyList<ContingencyReleaseContext> GetQueuedReleaseContextsTyped() =>
        new List<ContingencyReleaseContext>(_releaseQueue);

    internal int GetQueuedSequentialAutoCastCount()
    {
        int count = 0;
        foreach (AutoCastRequest request in _sequentialAutoCastQueue)
            if (request?.IsValid == true)
                count += 1;
        return count;
    }

    internal bool HasInstanceForSetup(StringName memberId, StringName setupId)
    {
        memberId = Normalize(memberId);
        setupId = Normalize(setupId);
        if (memberId == "" || setupId == "")
            return false;
        foreach (BattleContingencyInstance instance in GetInstancesForMember(memberId))
            if (instance.SetupId == setupId)
                return true;
        return false;
    }

    internal bool IsSetupConsumedForMember(StringName memberId, StringName setupId)
    {
        memberId = Normalize(memberId);
        setupId = Normalize(setupId);
        return memberId != ""
            && setupId != ""
            && _consumedSetupIdsByMemberId.TryGetValue(memberId, out HashSet<StringName> setupIds)
            && setupIds.Contains(setupId);
    }

    internal int GetReleasedReservedMpMaxForMember(StringName memberId)
    {
        memberId = Normalize(memberId);
        if (memberId == "")
            return 0;
        int total = 0;
        foreach (BattleContingencyInstance instance in GetInstancesForMember(memberId))
        {
            if (instance?.Setup == null || !IsSetupConsumedForMember(memberId, instance.SetupId))
                continue;
            total += Mathf.Max(instance.Setup.ReservedMpMax, 0);
        }
        return total;
    }

    internal int GetEffectiveReservedMpMaxForMember(StringName memberId, int persistentReservedMpMax)
    {
        int released = GetReleasedReservedMpMaxForMember(memberId);
        return Mathf.Max(Mathf.Max(persistentReservedMpMax, 0) - released, 0);
    }

    internal ContingencyReleaseContext EnterReleaseContext(
        StringName instanceId,
        ContingencyFrozenTriggerFacts frozenFacts = null,
        StringName triggerType = default,
        StringName triggeringUnitId = default,
        StringName sourceEventId = default
    )
    {
        instanceId = Normalize(instanceId);
        if (instanceId == "" || !_instancesById.TryGetValue(instanceId, out BattleContingencyInstance instance))
            return ContingencyReleaseContext.Empty;

        triggerType = Normalize(triggerType);
        if (triggerType == "")
            triggerType = instance.Setup?.Trigger?.Type ?? "";
        ContingencyReleaseContext context = BuildReleaseContext(
            instance,
            triggerType,
            triggeringUnitId,
            frozenFacts,
            sourceEventId
        );
        MarkConsumed(instance);
        RefreshOwnerUnit(instance.OwnerUnitId);
        return context;
    }

    internal IReadOnlyList<ContingencyTargetResolutionResult> ResolveStoredSpellTargetsForRelease(
        ContingencyReleaseContext context,
        ContingencyFrozenTriggerFacts facts
    )
    {
        List<ContingencyTargetResolutionResult> results = new();
        foreach (ResolvedStoredSpell resolved in ResolveStoredSpellEntriesForRelease(context, facts))
            results.Add(resolved.TargetResolution);
        return results;
    }

    internal IReadOnlyList<AutoCastRequest> BuildAutoCastRequestsForRelease(
        ContingencyReleaseContext context,
        ContingencyFrozenTriggerFacts facts,
        BattleEventBatch batch = null
    )
    {
        List<AutoCastRequest> requests = new();
        foreach (ResolvedStoredSpell resolved in ResolveStoredSpellEntriesForRelease(context, facts, batch))
        {
            if (resolved.TargetResolution?.Ok != true || resolved.StoredSpell == null)
                continue;
            requests.Add(
                new AutoCastRequest
                {
                    CasterUnitId = context.CasterUnitId,
                    OwnerMemberId = context.OwnerMemberId,
                    OwnerUnitId = context.OwnerUnitId,
                    SetupId = context.SetupId,
                    InstanceId = context.InstanceId,
                    StoredSkillId = resolved.StoredSpell.StoredSkillId,
                    CastLevel = resolved.StoredSpell.CastLevel,
                    TargetResolution = resolved.TargetResolution,
                    ReleaseContext = context,
                    FrozenFacts = facts ?? ContingencyFrozenTriggerFacts.Empty,
                }
            );
        }
        return requests;
    }

    internal int ExecuteQueuedReleaseContexts(
        ContingencyFrozenTriggerFacts facts,
        BattleEventBatch batch
    )
    {
        int executed = 0;
        int contextCount = _releaseQueue.Count;
        for (int i = 0; i < contextCount; i++)
        {
            ContingencyReleaseContext queuedContext = _releaseQueue.Dequeue();
            if (queuedContext == null || !queuedContext.IsValid)
                continue;
            if (!_instancesById.TryGetValue(queuedContext.InstanceId, out BattleContingencyInstance instance))
                continue;
            if (IsSetupConsumedForMember(instance.OwnerMemberId, instance.SetupId))
            {
                AppendContingencyReportEntry(
                    batch,
                    "contingency_depleted",
                    "depleted",
                    "setup_already_consumed",
                    instance,
                    sourceEventId: queuedContext.SourceEventId,
                    triggerType: queuedContext.TriggerType
                );
                continue;
            }

            ContingencyFrozenTriggerFacts releaseFacts =
                queuedContext.FrozenFacts ?? facts ?? ContingencyFrozenTriggerFacts.Empty;
            ContingencyReleaseContext context = EnterReleaseContext(
                queuedContext.InstanceId,
                releaseFacts,
                queuedContext.TriggerType,
                queuedContext.TriggeringUnitId,
                queuedContext.SourceEventId
            );
            if (!context.IsValid)
                continue;
            bool sequential = instance.Setup?.ReleaseModeKind == ContingencyReleaseModeKind.SequentialRelease;
            AppendContingencyReportEntry(
                batch,
                "contingency_released",
                "released",
                sequential ? "sequential_queued" : "ok",
                instance,
                sourceEventId: context.SourceEventId,
                triggerType: context.TriggerType
            );
            IReadOnlyList<AutoCastRequest> requests = BuildAutoCastRequestsForRelease(
                context,
                releaseFacts,
                batch
            );
            if (sequential)
            {
                foreach (AutoCastRequest request in requests)
                    if (request?.IsValid == true)
                        _sequentialAutoCastQueue.Enqueue(request);
                continue;
            }
            foreach (AutoCastRequest request in requests)
                if (_runtime?.ExecuteAutoCast(request, batch) == true)
                    executed += 1;
        }
        return executed;
    }

    internal int ExecuteNextSequentialAutoCastForOwner(StringName ownerUnitId, BattleEventBatch batch)
    {
        ownerUnitId = Normalize(ownerUnitId);
        if (ownerUnitId == "" || _sequentialAutoCastQueue.Count == 0)
            return 0;
        int count = _sequentialAutoCastQueue.Count;
        AutoCastRequest selected = null;
        for (int i = 0; i < count; i++)
        {
            AutoCastRequest request = _sequentialAutoCastQueue.Dequeue();
            if (selected == null && request?.OwnerUnitId == ownerUnitId)
            {
                selected = request;
                continue;
            }
            if (request?.IsValid == true)
                _sequentialAutoCastQueue.Enqueue(request);
        }
        return selected != null && _runtime?.ExecuteAutoCast(selected, batch) == true ? 1 : 0;
    }

    internal void OnBattleConfirmed(BattleEventBatch batch = null)
    {
        QueueTrigger("combat_started", "", batch);
    }

    internal void OnOwnerTurnStarted(BattleUnitState ownerUnit, BattleEventBatch batch = null)
    {
        if (ownerUnit == null)
            return;
        QueueTrigger("owner_turn_started", ownerUnit.unit_id, batch);
    }

    internal void OnHookFact(ContingencyHookFact fact)
    {
        if (fact == null || !fact.CanTriggerContingencies)
            return;
        StringName triggerType = Normalize(fact.TriggerType);
        if (triggerType == "")
            return;

        foreach (BattleContingencyInstance instance in GetInstancesForTrigger(triggerType))
        {
            if (instance == null)
                continue;
            if (instance.Suppressed)
                continue;
            if (IsSetupConsumedForMember(instance.OwnerMemberId, instance.SetupId))
                continue;
            if (!IsOwnerLive(instance.OwnerUnitId))
                continue;
            if (!HookMatchesInstance(instance, fact))
                continue;
            if (!ReserveSourceEventQueueSlot(instance, fact.SourceEventId))
                continue;

            _releaseQueue.Enqueue(
                BuildReleaseContext(
                    instance,
                    triggerType,
                    fact.SourceUnitId,
                    fact.ToFrozenFacts(_runtime?.GetState(), instance.OwnerUnitId),
                    fact.SourceEventId
                )
            );
        }
    }

    public BattleDamageApplicationHookResult BeforeDamageResolved(
        BattleDamageApplicationHookContext context
    )
    {
        if (
            context == null
            || context.TargetUnit == null
            || context.DamageInput.ResolvedDamage <= 0
            || context.DamageInput.SuppressDamageApplicationHook
            || context.Origin?.CanTriggerContingencies == false
        )
        {
            return BattleDamageApplicationHookResult.None;
        }

        StringName sourceEventId =
            _runtime?.AllocateContingencySourceEventId("damage_hook") ?? "";
        StringName damageEventId =
            _runtime?.AllocateContingencySourceEventId("damage_event") ?? sourceEventId;
        ContingencyFrozenTriggerFacts facts = BuildDamageFrozenFacts(context);
        int executed = 0;
        int matched = 0;
        bool cancelCurrentDamage = false;
        foreach (BattleContingencyInstance instance in GetDamageTriggerInstances(context))
        {
            if (instance == null)
                continue;
            if (!ReserveSourceEventQueueSlot(instance, sourceEventId))
                continue;
            matched += 1;
            AppendContingencyReportEntry(
                context.Batch,
                "contingency_triggered",
                "triggered",
                "damage_hook_matched",
                instance,
                sourceEventId,
                damageEventId,
                instance.Setup?.Trigger?.Type ?? ""
            );
            ContingencyReleaseContext releaseContext = EnterReleaseContext(
                instance.InstanceId,
                facts,
                instance.Setup?.Trigger?.Type ?? "",
                context.SourceUnit?.unit_id ?? "",
                sourceEventId
            );
            if (!releaseContext.IsValid)
                continue;
            bool sequential = instance.Setup?.ReleaseModeKind == ContingencyReleaseModeKind.SequentialRelease;
            AppendContingencyReportEntry(
                context.Batch,
                "contingency_released",
                "released",
                sequential ? "sequential_queued" : "ok",
                instance,
                sourceEventId,
                damageEventId,
                releaseContext.TriggerType
            );

            IReadOnlyList<AutoCastRequest> requests = BuildAutoCastRequestsForRelease(
                releaseContext,
                facts,
                context.Batch
            );
            if (sequential)
            {
                foreach (AutoCastRequest request in requests)
                    if (request?.IsValid == true)
                        _sequentialAutoCastQueue.Enqueue(request);
                continue;
            }
            foreach (AutoCastRequest request in requests)
            {
                if (_runtime?.ExecuteAutoCast(request, context.Batch) == true)
                {
                    executed += 1;
                    if (request.TargetResolution?.MovedOutsideCurrentDamageEvent == true)
                        cancelCurrentDamage = true;
                }
            }
        }

        if (cancelCurrentDamage)
            return BattleDamageApplicationHookResult.Cancel();
        return executed > 0 || matched > 0
            ? BattleDamageApplicationHookResult.StateChanged()
            : BattleDamageApplicationHookResult.None;
    }

    internal GDictionary BuildSnapshot()
    {
        GArray instances = new();
        foreach (BattleContingencyInstance instance in GetInstancesTyped())
        {
            if (instance == null)
                continue;
            instances.Add(
                new GDictionary
                {
                    ["instance_id"] = instance.InstanceId.ToString(),
                    ["setup_id"] = instance.SetupId.ToString(),
                    ["owner_member_id"] = instance.OwnerMemberId.ToString(),
                    ["owner_unit_id"] = instance.OwnerUnitId.ToString(),
                    ["caster_unit_id"] = instance.CasterUnitId.ToString(),
                    ["trigger_type"] = (instance.Setup?.Trigger?.Type ?? new StringName("")).ToString(),
                    ["release_mode"] = (instance.Setup?.ReleaseMode ?? new StringName("")).ToString(),
                    ["stored_spells"] = BuildStoredSpellSnapshot(instance.Setup?.StoredSpells),
                    ["suppressed"] = instance.Suppressed,
                    ["consumed"] = IsSetupConsumedForMember(instance.OwnerMemberId, instance.SetupId),
                }
            );
        }

        GArray queued = new();
        foreach (ContingencyReleaseContext context in _releaseQueue)
        {
            if (context == null || !context.IsValid)
                continue;
            queued.Add(
                new GDictionary
                {
                    ["instance_id"] = context.InstanceId.ToString(),
                    ["setup_id"] = context.SetupId.ToString(),
                    ["owner_member_id"] = context.OwnerMemberId.ToString(),
                    ["owner_unit_id"] = context.OwnerUnitId.ToString(),
                    ["trigger_type"] = context.TriggerType.ToString(),
                    ["triggering_unit_id"] = context.TriggeringUnitId.ToString(),
                    ["source_event_id"] = context.SourceEventId.ToString(),
                }
            );
        }

        GDictionary triggerCandidateIndex = new();
        List<StringName> triggerTypes = new(_instanceIdsByTriggerType.Keys);
        triggerTypes.Sort(CompareStringNamesOrdinal);
        foreach (StringName triggerType in triggerTypes)
        {
            GArray instanceIds = new();
            foreach (StringName instanceId in GetInstanceIdsForTrigger(triggerType))
                instanceIds.Add(instanceId.ToString());
            triggerCandidateIndex[triggerType.ToString()] = instanceIds;
        }

        return new GDictionary
        {
            ["instances"] = instances,
            ["queued_release_contexts"] = queued,
            ["release_queue_count"] = _releaseQueue.Count,
            ["sequential_auto_cast_queue_count"] = GetQueuedSequentialAutoCastCount(),
            ["trigger_candidate_index"] = triggerCandidateIndex,
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ClearBattleState();
        _runtime = null;
    }

    private void AddInstance(
        StringName memberId,
        StringName ownerUnitId,
        ContingencyMatrixSetupState setup
    )
    {
        StringName setupId = Normalize(setup?.SetupId ?? "");
        if (setupId == "" || memberId == "" || ownerUnitId == "")
            return;
        StringName instanceId = new($"{memberId}:{setupId}");
        if (_instancesById.ContainsKey(instanceId))
            return;

        BattleContingencyInstance instance = new(instanceId, setupId, memberId, ownerUnitId, setup);
        _instancesById[instanceId] = instance;
        if (!_instanceIdsByMemberId.TryGetValue(memberId, out List<StringName> instanceIds))
        {
            instanceIds = new List<StringName>();
            _instanceIdsByMemberId[memberId] = instanceIds;
        }
        instanceIds.Add(instanceId);
        AddTriggerIndexEntry(setup.Trigger?.Type ?? "", instanceId);
    }

    private IEnumerable<BattleContingencyInstance> GetInstancesForMember(StringName memberId)
    {
        if (!_instanceIdsByMemberId.TryGetValue(memberId, out List<StringName> instanceIds))
            yield break;
        foreach (StringName instanceId in instanceIds)
            if (_instancesById.TryGetValue(instanceId, out BattleContingencyInstance instance))
                yield return instance;
    }

    private void QueueTrigger(
        StringName triggerType,
        StringName triggeringUnitId,
        BattleEventBatch batch
    )
    {
        triggerType = Normalize(triggerType);
        triggeringUnitId = Normalize(triggeringUnitId);
        foreach (BattleContingencyInstance instance in GetInstancesForTrigger(triggerType))
        {
            if (instance == null)
                continue;
            if (triggerType == "owner_turn_started" && instance.OwnerUnitId != triggeringUnitId)
                continue;
            if (instance.Suppressed)
            {
                AppendContingencyReportEntry(
                    batch,
                    "contingency_suppressed",
                    "suppressed",
                    "instance_suppressed",
                    instance,
                    triggerType: triggerType
                );
                continue;
            }
            if (IsSetupConsumedForMember(instance.OwnerMemberId, instance.SetupId))
            {
                AppendContingencyReportEntry(
                    batch,
                    "contingency_depleted",
                    "depleted",
                    "setup_already_consumed",
                    instance,
                    triggerType: triggerType
                );
                continue;
            }
            _releaseQueue.Enqueue(BuildReleaseContext(instance, triggerType, triggeringUnitId));
            AppendContingencyReportEntry(
                batch,
                "contingency_triggered",
                "triggered",
                "trigger_matched",
                instance,
                triggerType: triggerType
            );
        }
    }

    private ContingencyReleaseContext BuildReleaseContext(
        BattleContingencyInstance instance,
        StringName triggerType,
        StringName triggeringUnitId = default,
        ContingencyFrozenTriggerFacts frozenFacts = null,
        StringName sourceEventId = default
    ) =>
        new()
        {
            InstanceId = instance?.InstanceId ?? "",
            SetupId = instance?.SetupId ?? "",
            OwnerMemberId = instance?.OwnerMemberId ?? "",
            OwnerUnitId = instance?.OwnerUnitId ?? "",
            CasterUnitId = instance?.CasterUnitId ?? "",
            TriggerType = Normalize(triggerType),
            TriggeringUnitId = Normalize(triggeringUnitId),
            SourceEventId = Normalize(sourceEventId),
            FrozenFacts = frozenFacts ?? ContingencyFrozenTriggerFacts.Empty,
            Suppressed = instance?.Suppressed ?? false,
        };

    private void AddTriggerIndexEntry(StringName triggerType, StringName instanceId)
    {
        triggerType = Normalize(triggerType);
        instanceId = Normalize(instanceId);
        if (triggerType == "" || instanceId == "")
            return;
        if (!_instanceIdsByTriggerType.TryGetValue(triggerType, out List<StringName> instanceIds))
        {
            instanceIds = new List<StringName>();
            _instanceIdsByTriggerType[triggerType] = instanceIds;
        }
        if (!instanceIds.Contains(instanceId))
            instanceIds.Add(instanceId);
        instanceIds.Sort(CompareStringNamesOrdinal);
    }

    private IEnumerable<BattleContingencyInstance> GetInstancesForTrigger(StringName triggerType)
    {
        foreach (StringName instanceId in GetInstanceIdsForTrigger(triggerType))
            if (_instancesById.TryGetValue(instanceId, out BattleContingencyInstance instance))
                yield return instance;
    }

    private IReadOnlyList<StringName> GetInstanceIdsForTrigger(StringName triggerType)
    {
        triggerType = Normalize(triggerType);
        if (triggerType == "" || !_instanceIdsByTriggerType.TryGetValue(triggerType, out List<StringName> instanceIds))
            return Array.Empty<StringName>();
        List<StringName> result = new(instanceIds);
        result.Sort(CompareStringNamesOrdinal);
        return result;
    }

    private static int CompareStringNamesOrdinal(StringName left, StringName right) =>
        string.CompareOrdinal(left.ToString(), right.ToString());

    private bool HookMatchesInstance(BattleContingencyInstance instance, ContingencyHookFact fact)
    {
        if (instance == null || fact == null)
            return false;
        if (fact.TriggerType == "hp_below_percent")
            return MatchesHpBelowPercent(instance, fact);
        if (fact.TriggerType == "status_applied")
            return MatchesStatusApplied(instance, fact);
        if (fact.TriggerType == "enemy_enter_radius")
            return MatchesEnemyEnterRadius(instance, fact);
        if (fact.TriggerType == "affected_by_spell")
            return MatchesAffectedBySpell(instance, fact);
        return false;
    }

    private IEnumerable<BattleContingencyInstance> GetDamageTriggerInstances(
        BattleDamageApplicationHookContext context
    )
    {
        foreach (BattleContingencyInstance instance in GetInstancesForTrigger("incoming_damage_percent"))
            if (MatchesIncomingDamagePercent(instance, context))
                yield return instance;
        if (!context.Projection.WouldBeFatalBeforeDeathPrevention)
            yield break;
        foreach (BattleContingencyInstance instance in GetInstancesForTrigger("fatal_damage_incoming"))
            if (MatchesFatalDamageIncoming(instance, context))
                yield return instance;
    }

    private bool MatchesIncomingDamagePercent(
        BattleContingencyInstance instance,
        BattleDamageApplicationHookContext context
    )
    {
        if (!CanDamageTriggerInstance(instance, context))
            return false;
        int percent = ReadInt(instance.Setup?.Trigger?.Payload, "damage_percent", 0);
        if (percent <= 0)
            return false;
        int maxHp = GetMaxHp(context.TargetUnit);
        return maxHp > 0 && context.Projection.HpDamage * 100 >= maxHp * percent;
    }

    private bool MatchesFatalDamageIncoming(
        BattleContingencyInstance instance,
        BattleDamageApplicationHookContext context
    ) =>
        CanDamageTriggerInstance(instance, context)
        && context.Projection.WouldBeFatalBeforeDeathPrevention;

    private bool CanDamageTriggerInstance(
        BattleContingencyInstance instance,
        BattleDamageApplicationHookContext context
    )
    {
        if (instance == null || context == null)
            return false;
        if (instance.Suppressed)
            return false;
        if (IsSetupConsumedForMember(instance.OwnerMemberId, instance.SetupId))
            return false;
        if (!IsOwnerLive(instance.OwnerUnitId))
            return false;
        if (context.TargetUnit?.unit_id != instance.OwnerUnitId)
            return false;
        BattleUnitState sourceUnit = context.SourceUnit;
        return sourceUnit != null
            && sourceUnit.unit_id != instance.OwnerUnitId
            && IsHostile(context.TargetUnit, sourceUnit);
    }

    private ContingencyFrozenTriggerFacts BuildDamageFrozenFacts(
        BattleDamageApplicationHookContext context
    )
    {
        Vector2I sourceCell = context.SourceUnit?.coord ?? new Vector2I(-1, -1);
        Vector2I targetCell = context.TargetUnit?.coord ?? new Vector2I(-1, -1);
        return new ContingencyFrozenTriggerFacts
        {
            TriggerSourceUnitId = context.SourceUnit?.unit_id ?? "",
            TriggerSourceCell = sourceCell,
            TriggerTargetUnitId = context.TargetUnit?.unit_id ?? "",
            TriggerTargetCell = targetCell,
            TriggerCell = targetCell,
            CurrentDamageEventAreaCells = context.CurrentDamageEventAreaCells,
            FatalDamageIncoming = context.Projection.WouldBeFatalBeforeDeathPrevention,
        };
    }

    private void AppendContingencyReportEntry(
        BattleEventBatch batch,
        string entryType,
        string decision,
        string reasonId,
        BattleContingencyInstance instance,
        StringName sourceEventId = default,
        StringName damageEventId = default,
        StringName triggerType = default,
        StringName storedSkillId = default,
        StringName targetResolver = default
    )
    {
        if (batch == null || instance == null)
            return;
        triggerType = Normalize(triggerType);
        if (triggerType == "")
            triggerType = instance.Setup?.Trigger?.Type ?? "";
        GDictionary entry = new()
        {
            ["entry_type"] = entryType ?? "",
            ["decision"] = decision ?? "",
            ["reason_id"] = reasonId ?? "",
            ["owner_member_id"] = instance.OwnerMemberId.ToString(),
            ["owner_unit_id"] = instance.OwnerUnitId.ToString(),
            ["setup_id"] = instance.SetupId.ToString(),
            ["source_event_id"] = Normalize(sourceEventId).ToString(),
            ["damage_event_id"] = Normalize(damageEventId).ToString(),
            ["trigger_type"] = triggerType.ToString(),
            ["release_mode"] = (instance.Setup?.ReleaseMode ?? new StringName("")).ToString(),
            ["stored_skill_id"] = Normalize(storedSkillId).ToString(),
            ["target_resolver"] = Normalize(targetResolver).ToString(),
        };
        batch.AddReportEntry(entry);
    }

    private static int GetMaxHp(BattleUnitState unitState)
    {
        if (unitState?.attribute_snapshot == null)
            return Math.Max(unitState?.current_hp ?? 0, 0);
        int maxHp = unitState.attribute_snapshot.GetValue(AttributeService.HP_MAX);
        return Math.Max(maxHp, Math.Max(unitState.current_hp, 0));
    }

    private bool MatchesHpBelowPercent(BattleContingencyInstance instance, ContingencyHookFact fact)
    {
        if (fact.TargetUnitId != instance.OwnerUnitId || fact.MaxHp <= 0)
            return false;
        int percent = ReadInt(instance.Setup?.Trigger?.Payload, "percent", 0);
        if (percent <= 0)
            return false;
        bool currentBelow = fact.CurrentHp * 100 <= fact.MaxHp * percent;
        if (!currentBelow)
            return false;
        bool crossingOnly = ReadBool(instance.Setup?.Trigger?.Payload, "crossing_only");
        return !crossingOnly || fact.PreviousHp * 100 > fact.MaxHp * percent;
    }

    private bool MatchesStatusApplied(BattleContingencyInstance instance, ContingencyHookFact fact)
    {
        if (fact.TargetUnitId != instance.OwnerUnitId || fact.StatusIds.Count == 0)
            return false;
        GArray configuredStatusIds = ReadArray(instance.Setup?.Trigger?.Payload, "status_tags");
        foreach (StringName statusId in fact.StatusIds)
        {
            if (ContainsStringName(configuredStatusIds, statusId))
                return true;
        }
        return false;
    }

    private bool MatchesEnemyEnterRadius(BattleContingencyInstance instance, ContingencyHookFact fact)
    {
        if (fact.SourceUnitId == "" || fact.SourceUnitId == instance.OwnerUnitId)
            return false;
        BattleState state = _runtime?.GetState();
        if (
            state == null
            || !state.TryGetUnitTyped(instance.OwnerUnitId, out BattleUnitState ownerUnit)
            || !state.TryGetUnitTyped(fact.SourceUnitId, out BattleUnitState sourceUnit)
        )
            return false;
        if (!IsHostile(ownerUnit, sourceUnit))
            return false;
        int radius = ReadInt(instance.Setup?.Trigger?.Payload, "radius", 0);
        if (radius <= 0)
            return false;
        int previousDistance = Manhattan(ownerUnit.coord, fact.PreviousSourceCell);
        int currentDistance = Manhattan(ownerUnit.coord, fact.SourceCell);
        return currentDistance <= radius && previousDistance > radius;
    }

    private bool MatchesAffectedBySpell(BattleContingencyInstance instance, ContingencyHookFact fact)
    {
        if (fact.SourceUnitId == "" || fact.SourceUnitId == instance.OwnerUnitId)
            return false;
        BattleState state = _runtime?.GetState();
        if (
            state == null
            || !state.TryGetUnitTyped(instance.OwnerUnitId, out BattleUnitState ownerUnit)
            || !state.TryGetUnitTyped(fact.SourceUnitId, out BattleUnitState sourceUnit)
        )
            return false;
        if (!IsHostile(ownerUnit, sourceUnit))
            return false;
        if (fact.TargetUnitId == instance.OwnerUnitId)
            return true;
        foreach (StringName affectedUnitId in fact.AffectedUnitIds)
            if (affectedUnitId == instance.OwnerUnitId)
                return true;
        return false;
    }

    private bool IsOwnerLive(StringName ownerUnitId)
    {
        BattleState state = _runtime?.GetState();
        return state != null
            && ownerUnitId != ""
            && state.TryGetUnitTyped(ownerUnitId, out BattleUnitState ownerUnit)
            && ownerUnit?.is_alive == true;
    }

    private bool ReserveSourceEventQueueSlot(
        BattleContingencyInstance instance,
        StringName sourceEventId
    )
    {
        sourceEventId = Normalize(sourceEventId);
        if (instance?.OwnerMemberId == "" || sourceEventId == "")
            return true;
        string key = $"{instance.OwnerMemberId}:{sourceEventId}";
        return _queuedSourceEventKeys.Add(key);
    }

    private static bool IsHostile(BattleUnitState ownerUnit, BattleUnitState sourceUnit) =>
        ownerUnit != null
        && sourceUnit != null
        && ownerUnit.faction_id != ""
        && sourceUnit.faction_id != ""
        && ownerUnit.faction_id != sourceUnit.faction_id;

    private static int Manhattan(Vector2I left, Vector2I right)
    {
        if (left.X < 0 || left.Y < 0 || right.X < 0 || right.Y < 0)
            return int.MaxValue / 2;
        return Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
    }

    private static bool ReadBool(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return false;
        return source[key].AsBool();
    }

    private static int ReadInt(GDictionary source, string key, int fallback)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return fallback;
        return source[key].AsInt32();
    }

    private static GArray ReadArray(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return new GArray();
        return source[key].AsGodotArray();
    }

    private static GArray BuildStoredSpellSnapshot(
        IReadOnlyList<ContingencyStoredSpellEntryState> spells
    )
    {
        GArray result = new();
        foreach (ContingencyStoredSpellEntryState spell in spells ?? Array.Empty<ContingencyStoredSpellEntryState>())
        {
            if (spell == null)
                continue;
            result.Add(
                new GDictionary
                {
                    ["stored_skill_id"] = spell.StoredSkillId.ToString(),
                    ["cast_level"] = spell.CastLevel,
                    ["order"] = spell.Order,
                    ["target_resolver"] = new GDictionary
                    {
                        ["type"] = spell.TargetResolver?.Type.ToString() ?? "",
                    },
                }
            );
        }
        return result;
    }

    private static bool ContainsStringName(GArray values, StringName expected)
    {
        expected = Normalize(expected);
        if (values == null || expected == "")
            return false;
        foreach (Variant value in values)
        {
            StringName parsed = ProgressionDataUtils.to_string_name(value);
            if (parsed == expected)
                return true;
        }
        return false;
    }

    private void MarkConsumed(BattleContingencyInstance instance)
    {
        if (instance == null || instance.OwnerMemberId == "" || instance.SetupId == "")
            return;
        if (!_consumedSetupIdsByMemberId.TryGetValue(instance.OwnerMemberId, out HashSet<StringName> setupIds))
        {
            setupIds = new HashSet<StringName>();
            _consumedSetupIdsByMemberId[instance.OwnerMemberId] = setupIds;
        }
        if (!setupIds.Add(instance.SetupId))
            return;

        BattleUnitState ownerUnit = FindOwnerUnit(instance.OwnerUnitId);
        ownerUnit?.MarkContingencySetupConsumed(instance.SetupId);
    }

    private void RefreshOwnerUnit(StringName ownerUnitId)
    {
        BattleUnitState ownerUnit = FindOwnerUnit(ownerUnitId);
        if (ownerUnit == null)
            return;
        _runtime?.RefreshBattleUnitForContingencyOverlay(ownerUnit);
    }

    private BattleUnitState FindOwnerUnit(StringName ownerUnitId)
    {
        BattleState state = _runtime?.GetState();
        return state != null && state.TryGetUnitTyped(ownerUnitId, out BattleUnitState unitState)
            ? unitState
            : null;
    }

    private static StringName Normalize(StringName value) =>
        ProgressionDataUtils.to_string_name(value);

    private IReadOnlyList<ResolvedStoredSpell> ResolveStoredSpellEntriesForRelease(
        ContingencyReleaseContext context,
        ContingencyFrozenTriggerFacts facts,
        BattleEventBatch batch = null
    )
    {
        List<ResolvedStoredSpell> results = new();
        if (context == null || !context.IsValid)
            return results;
        if (!_instancesById.TryGetValue(context.InstanceId, out BattleContingencyInstance instance))
            return results;

        BattleState state = _runtime?.GetState();
        BattleGridService gridService = _runtime?.GetGridService();
        foreach (ContingencyStoredSpellEntryState spell in instance.Setup?.StoredSpells ?? Array.Empty<ContingencyStoredSpellEntryState>())
        {
            SkillDef skillDef = _runtime?.GetSkillDefTyped(spell?.StoredSkillId ?? "");
            ContingencyTargetResolutionResult result = _targetResolver.ResolveTarget(
                new ContingencyTargetResolutionRequest
                {
                    BattleState = state,
                    GridService = gridService,
                    OwnerUnitId = context.OwnerUnitId,
                    ResolverState = spell?.TargetResolver,
                    FrozenFacts = facts ?? ContingencyFrozenTriggerFacts.Empty,
                    StoredSkillDef = skillDef,
                }
            );
            results.Add(new ResolvedStoredSpell(spell, result));
            if (result.Ok != true)
            {
                AppendContingencyReportEntry(
                    batch,
                    "contingency_spell_skipped",
                    "skipped",
                    NormalizeSkipReason(result.ReasonId).ToString(),
                    instance,
                    sourceEventId: context.SourceEventId,
                    triggerType: context.TriggerType,
                    storedSkillId: spell?.StoredSkillId ?? "",
                    targetResolver: spell?.TargetResolver?.Type ?? ""
                );
            }
            if (
                !result.Ok
                && spell?.FallbackPolicyKind == ContingencyFallbackPolicyKind.AbortRemainingIfInvalid
            )
                break;
        }
        return results;
    }

    private static StringName NormalizeSkipReason(StringName reasonId)
    {
        reasonId = Normalize(reasonId);
        if (reasonId == "trigger_source_unit_missing" || reasonId == "trigger_source_cell_missing")
            return "trigger_source_missing";
        if (reasonId == "trigger_target_unit_missing" || reasonId == "trigger_target_cell_missing")
            return "trigger_target_missing";
        return reasonId;
    }

    private readonly record struct ResolvedStoredSpell(
        ContingencyStoredSpellEntryState StoredSpell,
        ContingencyTargetResolutionResult TargetResolution
    );
}
