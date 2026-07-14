using System;
using System.Collections.Generic;
using Godot;

public sealed class ContingencyConsumedCommitResult
{
    public bool Ok { get; init; }
    public string ErrorCode { get; init; } = "";
    public StringName MemberId { get; init; } = "";
    public int ConsumedCount { get; init; }

    public static ContingencyConsumedCommitResult Success(
        StringName memberId,
        int consumedCount
    ) =>
        new()
        {
            Ok = true,
            MemberId = memberId,
            ConsumedCount = Mathf.Max(consumedCount, 0),
        };

    public static ContingencyConsumedCommitResult Failure(
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

    internal BattleResourceCommitResult CommitResources(
        StringName memberId,
        int currentHp,
        int currentMp,
        int currentAura
    )
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (normalizedMemberId == "")
            return BattleResourceCommitResult.Failure("invalid_member_id", normalizedMemberId);

        PartyMemberState memberState = _partyState?.GetMemberState(normalizedMemberId);
        if (memberState == null)
            return BattleResourceCommitResult.Failure("member_not_found", normalizedMemberId);

        AttributeSnapshot snapshot = _attributeSnapshotProvider?.Invoke(normalizedMemberId);
        if (snapshot == null)
            return BattleResourceCommitResult.Failure(
                "attribute_snapshot_missing",
                normalizedMemberId
            );

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
        return BattleResourceCommitResult.Success(normalizedMemberId);
    }

    internal ContingencyConsumedCommitResult ValidateContingencyConsumedSetups(
        StringName memberId,
        IReadOnlyCollection<StringName> consumedSetupIds
    ) => TryBuildConsumedSetupsRelease(memberId, consumedSetupIds, out _);

    internal ContingencyConsumedCommitResult CommitContingencyConsumedSetups(
        StringName memberId,
        IReadOnlyCollection<StringName> consumedSetupIds
    )
    {
        ContingencyConsumedCommitResult result = TryBuildConsumedSetupsRelease(
            memberId,
            consumedSetupIds,
            out PartyMemberState nextMember
        );
        if (!result.Ok || nextMember == null)
            return result;
        _partyState.SetMemberState(nextMember);
        return result;
    }

    private ContingencyConsumedCommitResult TryBuildConsumedSetupsRelease(
        StringName memberId,
        IReadOnlyCollection<StringName> consumedSetupIds,
        out PartyMemberState nextMember
    )
    {
        nextMember = null;
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

        nextMember = memberState.WithContingencySetupsForMutation(nextSetups);
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
        return setup?.WithChargeState(
            charged: false,
            reservedMpMax: 0,
            Array.Empty<ContingencyMaterialCostState>()
        );
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
