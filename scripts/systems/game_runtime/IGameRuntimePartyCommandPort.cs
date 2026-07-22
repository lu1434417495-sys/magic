using System;
using System.Collections.Generic;
using Godot;

// Party-command application boundary. Queries return detached facts, while mutations
// describe complete party workflows without exposing PartyState or inventory services.
internal interface IGameRuntimePartyCommandQuery
{
    PartyCommandSnapshot CapturePartyCommandSnapshot();

    string GetMemberDisplayName(StringName memberId);

    string GetItemDisplayName(StringName itemId);
}

internal interface IGameRuntimePartyCommandMutationPort
{
    void OpenPartyManagement(string statusMessage);

    void SelectPartyMember(StringName memberId, string statusMessage);

    void SetPartySelection(StringName memberId);

    void ApplyPartyLeaderChange(StringName memberId, string successMessage);

    void ApplyPartyRosterChange(
        IReadOnlyList<StringName> activeMemberIds,
        IReadOnlyList<StringName> reserveMemberIds,
        string successMessage
    );

    PartyEquipmentCommandResult EquipPartyItemAndPersist(
        StringName memberId,
        StringName itemId,
        StringName slotId,
        StringName instanceId
    );

    PartyEquipmentCommandResult UnequipPartyItemAndPersist(
        StringName memberId,
        StringName slotId
    );

    void ClosePartyManagementAndPresentPendingReward(string statusMessage);

    void UpdatePartyStatus(string message);
}

internal interface IGameRuntimePartyCommandPort
    : IGameRuntimePartyCommandQuery,
        IGameRuntimePartyCommandMutationPort { }

internal sealed class PartyCommandSnapshot
{
    internal bool HasGenerationDefinition { get; }
    internal bool HasPartyState { get; }
    internal bool IsBattleActive { get; }
    internal RuntimeModalKind ActiveModalKind { get; }
    internal StringName LivingMainCharacterMemberId { get; }
    internal IReadOnlyList<StringName> ActiveMemberIds { get; }
    internal IReadOnlyList<StringName> ReserveMemberIds { get; }

    private IReadOnlyList<StringName> MemberIds { get; }

    internal bool IsModalWindowOpen => ActiveModalKind != RuntimeModalKind.None;

    internal PartyCommandSnapshot(
        bool hasGenerationDefinition,
        bool hasPartyState,
        bool isBattleActive,
        RuntimeModalKind activeModalKind,
        StringName livingMainCharacterMemberId,
        IEnumerable<StringName> memberIds,
        IEnumerable<StringName> activeMemberIds,
        IEnumerable<StringName> reserveMemberIds
    )
    {
        HasGenerationDefinition = hasGenerationDefinition;
        HasPartyState = hasPartyState;
        IsBattleActive = isBattleActive;
        ActiveModalKind = activeModalKind;
        LivingMainCharacterMemberId = livingMainCharacterMemberId;
        MemberIds = Copy(memberIds);
        ActiveMemberIds = Copy(activeMemberIds);
        ReserveMemberIds = Copy(reserveMemberIds);
    }

    internal bool HasMember(StringName memberId) => Contains(MemberIds, memberId);

    internal bool IsActiveMember(StringName memberId) => Contains(ActiveMemberIds, memberId);

    internal bool IsReserveMember(StringName memberId) => Contains(ReserveMemberIds, memberId);

    private static IReadOnlyList<StringName> Copy(IEnumerable<StringName> values) =>
        new List<StringName>(values ?? Array.Empty<StringName>()).AsReadOnly();

    private static bool Contains(IEnumerable<StringName> values, StringName memberId)
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (values == null || normalizedMemberId == "")
            return false;
        foreach (StringName rawMemberId in values)
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

internal readonly struct PartyEquipmentCommandResult
{
    internal bool Success { get; }
    internal StringName MemberId { get; }
    internal string MemberDisplayName { get; }
    internal string SlotLabel { get; }
    internal StringName ItemId { get; }
    internal string ItemDisplayName { get; }
    internal StringName PreviousItemId { get; }
    internal string PreviousItemDisplayName { get; }
    internal string ErrorCode { get; }
    internal Error PersistenceError { get; }

    internal PartyEquipmentCommandResult(
        bool success,
        StringName memberId,
        string memberDisplayName,
        string slotLabel,
        StringName itemId,
        string itemDisplayName,
        StringName previousItemId,
        string previousItemDisplayName,
        string errorCode,
        Error persistenceError
    )
    {
        Success = success;
        MemberId = memberId;
        MemberDisplayName = memberDisplayName ?? "";
        SlotLabel = slotLabel ?? "";
        ItemId = itemId;
        ItemDisplayName = itemDisplayName ?? "";
        PreviousItemId = previousItemId;
        PreviousItemDisplayName = previousItemDisplayName ?? "";
        ErrorCode = errorCode ?? "";
        PersistenceError = persistenceError;
    }
}
