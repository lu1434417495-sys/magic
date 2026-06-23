using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

internal sealed class ContingencyConsumedCommitResult
{
    internal bool Ok { get; init; }
    internal string ErrorCode { get; init; } = "";
    internal StringName MemberId { get; init; } = "";
    internal int ConsumedCount { get; init; }

    internal static ContingencyConsumedCommitResult Success(
        StringName memberId,
        int consumedCount
    ) =>
        new()
        {
            Ok = true,
            MemberId = memberId,
            ConsumedCount = Mathf.Max(consumedCount, 0),
        };

    internal static ContingencyConsumedCommitResult Failure(
        string errorCode,
        StringName memberId
    ) =>
        new()
        {
            Ok = false,
            ErrorCode = errorCode ?? "",
            MemberId = memberId,
        };
}

internal sealed class CharacterBattleWritebackService
{
    private PartyState _partyState;
    private PartyWarehouseService _warehouseService;
    private Func<StringName, AttributeSnapshot> _attributeSnapshotProvider;

    internal void Setup(
        PartyState partyState,
        PartyWarehouseService warehouseService,
        Func<StringName, AttributeSnapshot> attributeSnapshotProvider
    )
    {
        _partyState = partyState;
        _warehouseService = warehouseService;
        _attributeSnapshotProvider = attributeSnapshotProvider;
    }

    internal void Clear()
    {
        _partyState = null;
        _warehouseService = null;
        _attributeSnapshotProvider = null;
    }

    internal void CommitResources(
        StringName memberId,
        int currentHp,
        int currentMp,
        int currentAura
    )
    {
        PartyMemberState memberState = _partyState?.GetMemberState(memberId);
        if (memberState == null)
            return;

        AttributeSnapshot snapshot = _attributeSnapshotProvider?.Invoke(memberId);
        if (snapshot == null)
            return;

        memberState.SetVitals(
            Mathf.Clamp(
                currentHp,
                0,
                Mathf.Max(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)), 1)
            ),
            Mathf.Clamp(
                currentMp,
                0,
                Mathf.Max(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.MpMax)), 0)
            ),
            Mathf.Clamp(
                currentAura,
                0,
                Mathf.Max(snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax)), 0)
            ),
            false
        );
    }

    internal ContingencyConsumedCommitResult CommitContingencyConsumedSetups(
        StringName memberId,
        IReadOnlyCollection<StringName> consumedSetupIds
    )
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return ContingencyConsumedCommitResult.Failure("invalid_member_id", normalizedMemberId);

        List<StringName> normalizedSetupIds = NormalizeConsumedSetupIds(consumedSetupIds);
        if (normalizedSetupIds == null)
            return ContingencyConsumedCommitResult.Failure("invalid_setup_id", normalizedMemberId);
        if (normalizedSetupIds.Count == 0)
            return ContingencyConsumedCommitResult.Success(normalizedMemberId, 0);

        PartyMemberState memberState = _partyState?.GetMemberState(normalizedMemberId);
        if (memberState == null)
            return ContingencyConsumedCommitResult.Failure("member_not_found", normalizedMemberId);

        List<ContingencyMatrixSetupState> nextSetups = new();
        HashSet<StringName> pendingConsumedIds = new(normalizedSetupIds);
        foreach (ContingencyMatrixSetupState setup in memberState.GetContingencySetupsTyped())
        {
            if (setup == null)
                continue;
            if (!pendingConsumedIds.Remove(setup.SetupId))
            {
                nextSetups.Add(setup.DuplicateState());
                continue;
            }

            ContingencyMatrixSetupState releasedSetup = BuildConsumedSetupRelease(setup);
            if (releasedSetup == null)
                return ContingencyConsumedCommitResult.Failure(
                    "invalid_consumed_setup_release",
                    normalizedMemberId
                );
            nextSetups.Add(releasedSetup);
        }

        if (pendingConsumedIds.Count > 0)
            return ContingencyConsumedCommitResult.Failure("setup_not_found", normalizedMemberId);

        PartyMemberState nextMember = memberState.WithContingencySetupsForMutation(nextSetups);
        _partyState.SetMemberState(nextMember);
        return ContingencyConsumedCommitResult.Success(
            normalizedMemberId,
            normalizedSetupIds.Count
        );
    }

    internal void CommitDeath(StringName memberId)
    {
        PartyMemberState memberState = _partyState?.GetMemberState(memberId);
        if (memberState == null)
            return;
        SalvageMemberEquipment(memberState);
        memberState.MarkDead();
        _partyState?.RemoveMemberFromRosters(memberId);
    }

    internal void CommitKo(StringName memberId) => CommitDeath(memberId);

    internal int FlushAfterBattle() => (int)Error.Ok;

    private static List<StringName> NormalizeConsumedSetupIds(
        IReadOnlyCollection<StringName> consumedSetupIds
    )
    {
        List<StringName> result = new();
        HashSet<StringName> seen = new();
        foreach (StringName rawSetupId in consumedSetupIds ?? Array.Empty<StringName>())
        {
            StringName setupId = ProgressionDataUtils.to_string_name(rawSetupId);
            if (setupId == "")
                return null;
            if (seen.Add(setupId))
                result.Add(setupId);
        }
        return result;
    }

    private static ContingencyMatrixSetupState BuildConsumedSetupRelease(
        ContingencyMatrixSetupState setup
    )
    {
        if (setup == null)
            return null;
        Godot.Collections.Dictionary payload = setup.ToDictionary();
        payload["charged"] = false;
        payload["reserved_mp_max"] = 0;
        payload["material_costs"] = new GArray();
        return ContingencyMatrixSetupState.FromDictionary(payload);
    }

    private void SalvageMemberEquipment(PartyMemberState memberState)
    {
        EquipmentState equipmentState = memberState?.equipment_state;
        if (equipmentState == null || _warehouseService == null)
            return;
        foreach (StringName entrySlotId in equipmentState.GetEntrySlotIdsTyped())
        {
            if (
                equipmentState.PopEquippedInstance(entrySlotId)
                is EquipmentInstanceState equippedInstance
            )
                _warehouseService.DepositEquipmentInstance(equippedInstance);
        }
    }
}
