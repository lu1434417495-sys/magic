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

internal sealed class BattleContingencySystem : IDisposable
{
    private readonly Dictionary<StringName, BattleContingencyInstance> _instancesById = new();
    private readonly Dictionary<StringName, List<StringName>> _instanceIdsByMemberId = new();
    private readonly Dictionary<StringName, HashSet<StringName>> _consumedSetupIdsByMemberId = new();
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
        _consumedSetupIdsByMemberId.Clear();
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

    internal ContingencyReleaseContext EnterReleaseContext(StringName instanceId)
    {
        instanceId = Normalize(instanceId);
        if (instanceId == "" || !_instancesById.TryGetValue(instanceId, out BattleContingencyInstance instance))
            return ContingencyReleaseContext.Empty;

        ContingencyReleaseContext context = BuildReleaseContext(instance, instance.Setup?.Trigger?.Type ?? "");
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
        ContingencyFrozenTriggerFacts facts
    )
    {
        List<AutoCastRequest> requests = new();
        foreach (ResolvedStoredSpell resolved in ResolveStoredSpellEntriesForRelease(context, facts))
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
                continue;

            ContingencyReleaseContext context = EnterReleaseContext(queuedContext.InstanceId);
            if (!context.IsValid)
                continue;
            IReadOnlyList<AutoCastRequest> requests = BuildAutoCastRequestsForRelease(context, facts);
            if (instance.Setup?.ReleaseModeKind == ContingencyReleaseModeKind.SequentialRelease)
            {
                foreach (AutoCastRequest request in requests)
                    if (request?.IsValid == true)
                        _sequentialAutoCastQueue.Enqueue(request);
                executed += ExecuteNextSequentialAutoCastForOwner(context.OwnerUnitId, batch);
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

    internal void OnBattleConfirmed()
    {
        EnqueueTrigger("combat_started", "");
    }

    internal void OnOwnerTurnStarted(BattleUnitState ownerUnit)
    {
        if (ownerUnit == null)
            return;
        EnqueueTrigger("owner_turn_started", ownerUnit.unit_id);
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
                }
            );
        }

        return new GDictionary
        {
            ["instances"] = instances,
            ["queued_release_contexts"] = queued,
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
    }

    private IEnumerable<BattleContingencyInstance> GetInstancesForMember(StringName memberId)
    {
        if (!_instanceIdsByMemberId.TryGetValue(memberId, out List<StringName> instanceIds))
            yield break;
        foreach (StringName instanceId in instanceIds)
            if (_instancesById.TryGetValue(instanceId, out BattleContingencyInstance instance))
                yield return instance;
    }

    private void EnqueueTrigger(StringName triggerType, StringName triggeringUnitId)
    {
        triggerType = Normalize(triggerType);
        triggeringUnitId = Normalize(triggeringUnitId);
        foreach (BattleContingencyInstance instance in GetInstancesTyped())
        {
            if (instance == null || instance.Suppressed)
                continue;
            if (instance.Setup?.Trigger?.Type != triggerType)
                continue;
            if (triggerType == "owner_turn_started" && instance.OwnerUnitId != triggeringUnitId)
                continue;
            if (IsSetupConsumedForMember(instance.OwnerMemberId, instance.SetupId))
                continue;
            _releaseQueue.Enqueue(BuildReleaseContext(instance, triggerType, triggeringUnitId));
        }
    }

    private ContingencyReleaseContext BuildReleaseContext(
        BattleContingencyInstance instance,
        StringName triggerType,
        StringName triggeringUnitId = default
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
            Suppressed = instance?.Suppressed ?? false,
        };

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
        ContingencyFrozenTriggerFacts facts
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
            if (
                !result.Ok
                && spell?.FallbackPolicyKind == ContingencyFallbackPolicyKind.AbortRemainingIfInvalid
            )
                break;
        }
        return results;
    }

    private readonly record struct ResolvedStoredSpell(
        ContingencyStoredSpellEntryState StoredSpell,
        ContingencyTargetResolutionResult TargetResolution
    );
}
