using System.Collections.Generic;
using Godot;

public sealed partial class GameRuntimeFacade : IGameRuntimePartyCommandPort
{
    PartyCommandSnapshot IGameRuntimePartyCommandQuery.CapturePartyCommandSnapshot()
    {
        PartyState partyState = _party_state;
        List<StringName> memberIds = new();
        if (partyState != null)
        {
            foreach (PartyMemberState memberState in partyState.GetMemberStates())
            {
                if (memberState != null && memberState.member_id != "")
                    memberIds.Add(memberState.member_id);
            }
        }

        StringName livingMainCharacterMemberId = "";
        if (partyState != null)
        {
            StringName mainCharacterMemberId = partyState.GetResolvedMainCharacterMemberId();
            if (mainCharacterMemberId != "" && !partyState.IsMemberDead(mainCharacterMemberId))
                livingMainCharacterMemberId = mainCharacterMemberId;
        }

        return new PartyCommandSnapshot(
            GetGenerationDefinition() != null,
            partyState != null,
            IsBattleActive(),
            _active_modal_kind,
            livingMainCharacterMemberId,
            memberIds,
            partyState?.active_member_ids,
            partyState?.reserve_member_ids
        );
    }

    string IGameRuntimePartyCommandQuery.GetMemberDisplayName(StringName memberId) =>
        GetMemberDisplayNameInternal(memberId);

    string IGameRuntimePartyCommandQuery.GetItemDisplayName(StringName itemId) =>
        GetItemDisplayName(itemId);

    void IGameRuntimePartyCommandMutationPort.OpenPartyManagement(string statusMessage)
    {
        _active_modal_kind = RuntimeModalKind.Party;
        if (
            _party_selected_member_id == ""
            && _party_state?.active_member_ids is { Count: > 0 } activeMemberIds
        )
            _party_selected_member_id = activeMemberIds[0];
        UpdateStatusInternal(statusMessage);
    }

    void IGameRuntimePartyCommandMutationPort.SelectPartyMember(
        StringName memberId,
        string statusMessage
    )
    {
        if (_active_modal_kind == RuntimeModalKind.None)
            _active_modal_kind = RuntimeModalKind.Party;
        _party_selected_member_id = memberId;
        UpdateStatusInternal(statusMessage);
    }

    void IGameRuntimePartyCommandMutationPort.SetPartySelection(StringName memberId) =>
        _party_selected_member_id = memberId;

    void IGameRuntimePartyCommandMutationPort.ApplyPartyLeaderChange(
        StringName memberId,
        string successMessage
    )
    {
        if (_party_state == null)
            return;
        _party_state.leader_member_id = memberId;
        ApplyPartyCommandStateToRuntime(successMessage);
    }

    void IGameRuntimePartyCommandMutationPort.ApplyPartyRosterChange(
        IReadOnlyList<StringName> activeMemberIds,
        IReadOnlyList<StringName> reserveMemberIds,
        string successMessage
    )
    {
        if (_party_state == null)
            return;
        StringNameList normalizedActiveMemberIds = new(activeMemberIds);
        _party_state.active_member_ids = normalizedActiveMemberIds;
        _party_state.reserve_member_ids = new StringNameList(reserveMemberIds);
        if (
            !ContainsPartyCommandMemberId(
                normalizedActiveMemberIds,
                _party_state.leader_member_id
            )
            && normalizedActiveMemberIds.Count > 0
        )
            _party_state.leader_member_id = normalizedActiveMemberIds[0];
        ApplyPartyCommandStateToRuntime(successMessage);
    }

    PartyEquipmentCommandResult IGameRuntimePartyCommandMutationPort.EquipPartyItemAndPersist(
        StringName memberId,
        StringName itemId,
        StringName slotId,
        StringName instanceId
    )
    {
        PartyEquipmentService.EquipmentActionResult result =
            _party_equipment_service?.EquipItemTyped(memberId, itemId, slotId, instanceId)
            ?? new PartyEquipmentService.EquipmentActionResult(
                false,
                memberId,
                slotId,
                itemId,
                "",
                "",
                "",
                "runtime_unavailable"
            );
        if (!result.Success)
            return ToPartyEquipmentCommandResult(result);

        _party_selected_member_id = memberId;
        string memberDisplayName = GetMemberDisplayNameInternal(memberId);
        string itemDisplayName = GetItemDisplayName(result.ItemId);
        string previousItemDisplayName =
            result.PreviousItemId != "" ? GetItemDisplayName(result.PreviousItemId) : "";
        Error persistenceError = (Error)PersistPartyStateInternal();
        return ToPartyEquipmentCommandResult(
            result,
            persistenceError,
            memberDisplayName,
            itemDisplayName,
            previousItemDisplayName
        );
    }

    PartyEquipmentCommandResult IGameRuntimePartyCommandMutationPort.UnequipPartyItemAndPersist(
        StringName memberId,
        StringName slotId
    )
    {
        PartyEquipmentService.EquipmentActionResult result =
            _party_equipment_service?.UnequipItemTyped(memberId, slotId)
            ?? new PartyEquipmentService.EquipmentActionResult(
                false,
                memberId,
                slotId,
                "",
                "",
                "",
                "",
                "runtime_unavailable"
            );
        if (!result.Success)
            return ToPartyEquipmentCommandResult(result);

        _party_selected_member_id = memberId;
        string memberDisplayName = GetMemberDisplayNameInternal(memberId);
        string itemDisplayName = GetItemDisplayName(result.ItemId);
        Error persistenceError = (Error)PersistPartyStateInternal();
        return ToPartyEquipmentCommandResult(
            result,
            persistenceError,
            memberDisplayName,
            itemDisplayName,
            ""
        );
    }

    void IGameRuntimePartyCommandMutationPort.ClosePartyManagementAndPresentPendingReward(
        string statusMessage
    )
    {
        _active_modal_kind = RuntimeModalKind.None;
        UpdateStatusInternal(statusMessage);
        _reward_flow_handler?.PresentPendingRewardIfReady();
    }

    void IGameRuntimePartyCommandMutationPort.UpdatePartyStatus(string message) =>
        UpdateStatusInternal(message);

    private void ApplyPartyCommandStateToRuntime(string successMessage)
    {
        _character_management?.SetPartyState(_party_state);
        ReportPartyCommandPersistResult((Error)PersistPartyStateInternal(), successMessage);
    }

    private void ReportPartyCommandPersistResult(Error persistError, string successMessage) =>
        UpdateStatusInternal(
            persistError == Error.Ok
                ? successMessage
                : string.Format("{0} 但队伍状态持久化失败。", successMessage)
        );

    private static PartyEquipmentCommandResult ToPartyEquipmentCommandResult(
        PartyEquipmentService.EquipmentActionResult result,
        Error persistenceError = Error.Ok,
        string memberDisplayName = "",
        string itemDisplayName = "",
        string previousItemDisplayName = ""
    ) =>
        new(
            result.Success,
            result.MemberId,
            memberDisplayName,
            result.SlotLabel,
            result.ItemId,
            itemDisplayName,
            result.PreviousItemId,
            previousItemDisplayName,
            result.ErrorCode,
            persistenceError
        );

    private static bool ContainsPartyCommandMemberId(
        IEnumerable<StringName> memberIds,
        StringName memberId
    )
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (memberIds == null || normalizedMemberId == "")
            return false;
        foreach (StringName rawMemberId in memberIds)
        {
            if (
                ProgressionDataUtils.to_string_name(rawMemberId).ToString()
                == normalizedMemberId.ToString()
            )
                return true;
        }
        return false;
    }
}
