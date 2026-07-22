using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

public sealed class GameRuntimePartyCommandHandler
{
    private static readonly StringName RuntimeUnavailableMessage = "运行时尚未初始化。";

    private WeakReference<IGameRuntimePartyCommandPort> _portRef;

    private IGameRuntimePartyCommandPort _port
    {
        get => ResolveWeakRef(_portRef);
        set =>
            _portRef =
                value != null ? new WeakReference<IGameRuntimePartyCommandPort>(value) : null;
    }

    internal void Setup(IGameRuntimePartyCommandPort port)
    {
        _port = port;
    }

    public void Dispose()
    {
        _port = null;
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandOpenPartyTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        PartyCommandSnapshot snapshot = CapturePartyCommandSnapshot();
        if (!snapshot.HasGenerationDefinition)
            return CommandErrorTyped("世界地图尚未初始化。");
        if (snapshot.IsBattleActive)
            return CommandErrorTyped("当前处于战斗中，不能打开队伍管理。");
        if (snapshot.IsModalWindowOpen)
            return CommandErrorTyped("当前有窗口打开，不能打开队伍管理。");
        _port.OpenPartyManagement("已打开队伍管理窗口。");
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandSelectPartyMemberTyped(StringName memberId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        PartyCommandSnapshot snapshot = CapturePartyCommandSnapshot();
        if (!snapshot.HasPartyState)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (!snapshot.HasMember(memberId))
            return CommandErrorTyped(string.Format("未找到队伍成员 {0}。", memberId));
        if (!snapshot.IsActiveMember(memberId) && !snapshot.IsReserveMember(memberId))
            return CommandErrorTyped(
                string.Format("{0} 当前不在队伍编成中。", GetMemberDisplayName(memberId))
            );
        _port.SelectPartyMember(
            memberId,
            string.Format("已选中队员 {0}。", GetMemberDisplayName(memberId))
        );
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandSetPartyLeaderTyped(StringName memberId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        PartyCommandSnapshot snapshot = CapturePartyCommandSnapshot();
        if (!snapshot.HasPartyState)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (!snapshot.IsActiveMember(memberId))
            return CommandErrorTyped("只有上阵成员才能成为队长。");
        _port.ApplyPartyLeaderChange(
            memberId,
            string.Format("队长已切换为 {0}。", memberId)
        );
        _port.SetPartySelection(memberId);
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandMoveMemberToActiveTyped(
        StringName memberId
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        PartyCommandSnapshot snapshot = CapturePartyCommandSnapshot();
        if (!snapshot.HasPartyState)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (!snapshot.IsReserveMember(memberId))
            return CommandErrorTyped(
                string.Format("{0} 当前不在替补列表中。", GetMemberDisplayName(memberId))
            );
        if (snapshot.ActiveMemberIds.Count >= 4)
            return CommandErrorTyped("上阵人数已达到上限。");
        var activeIds = NormalizeMemberIds(snapshot.ActiveMemberIds);
        var reserveIds = NormalizeMemberIds(snapshot.ReserveMemberIds);
        reserveIds = WithoutMemberId(reserveIds, memberId);
        if (!HasMemberId(activeIds, memberId))
            activeIds.Add(memberId);
        _port.ApplyPartyRosterChange(activeIds, reserveIds, "队伍编成已更新。");
        _port.SetPartySelection(memberId);
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandMoveMemberToReserveTyped(
        StringName memberId
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        PartyCommandSnapshot snapshot = CapturePartyCommandSnapshot();
        if (!snapshot.HasPartyState)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (!snapshot.IsActiveMember(memberId))
            return CommandErrorTyped(
                string.Format("{0} 当前不在上阵列表中。", GetMemberDisplayName(memberId))
            );
        if (memberId == snapshot.LivingMainCharacterMemberId)
            return CommandErrorTyped("主角必须保持上阵，不能移至替补。");
        if (snapshot.ActiveMemberIds.Count <= 1)
            return CommandErrorTyped("队伍至少需要保留一名上阵成员。");
        var activeIds = NormalizeMemberIds(snapshot.ActiveMemberIds);
        var reserveIds = NormalizeMemberIds(snapshot.ReserveMemberIds);
        activeIds = WithoutMemberId(activeIds, memberId);
        if (!HasMemberId(reserveIds, memberId))
            reserveIds.Add(memberId);
        _port.ApplyPartyRosterChange(activeIds, reserveIds, "队伍编成已更新。");
        _port.SetPartySelection(memberId);
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandApplyPartyRosterTyped(
        Array<StringName> activeMemberIds,
        Array<StringName> reserveMemberIds
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        PartyCommandSnapshot snapshot = CapturePartyCommandSnapshot();
        if (!snapshot.HasPartyState)
            return CommandErrorTyped("当前不存在队伍数据。");
        var rosterError = ValidateMainCharacterRoster(
            activeMemberIds,
            reserveMemberIds,
            snapshot.LivingMainCharacterMemberId
        );
        if (!string.IsNullOrEmpty(rosterError))
            return CommandErrorTyped(rosterError);
        _port.ApplyPartyRosterChange(
            NormalizeMemberIds(activeMemberIds),
            NormalizeMemberIds(reserveMemberIds),
            "队伍编成已更新。"
        );
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandPartyEquipItemTyped(
        StringName memberId,
        StringName itemId,
        StringName slotId,
        StringName instanceId = default
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        PartyCommandSnapshot snapshot = CapturePartyCommandSnapshot();
        if (!snapshot.HasPartyState)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (snapshot.IsBattleActive)
            return CommandErrorTyped("当前处于战斗中，不能调整装备。");
        var activeModalKind = snapshot.ActiveModalKind;
        if (
            activeModalKind == RuntimeModalKind.Reward
            || activeModalKind == RuntimeModalKind.Promotion
            || activeModalKind == RuntimeModalKind.Settlement
            || activeModalKind == RuntimeModalKind.CharacterInfo
        )
            return CommandErrorTyped("当前窗口会阻止装备切换。");

        PartyEquipmentCommandResult result = _port.EquipPartyItemAndPersist(
            memberId,
            itemId,
            slotId,
            instanceId
        );
        if (!result.Success)
            return CommandErrorTyped(BuildEquipmentErrorMessage(result, true));

        var itemName = result.ItemDisplayName;
        var slotLabel = result.SlotLabel;
        var successMessage = string.Format(
            "已为 {0} 装备 {1}（{2}）。",
            result.MemberDisplayName,
            itemName,
            slotLabel
        );
        var previousItemId = result.PreviousItemId;
        if (previousItemId != "")
        {
            successMessage = string.Format(
                "已为 {0} 装备 {1}（{2}），并卸下 {3}。",
                result.MemberDisplayName,
                itemName,
                slotLabel,
                result.PreviousItemDisplayName
            );
        }

        if (result.PersistenceError != Error.Ok)
            successMessage = string.Format("{0} 但队伍状态持久化失败。", successMessage);
        _port.UpdatePartyStatus(successMessage);
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandPartyUnequipItemTyped(
        StringName memberId,
        StringName slotId
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        PartyCommandSnapshot snapshot = CapturePartyCommandSnapshot();
        if (!snapshot.HasPartyState)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (snapshot.IsBattleActive)
            return CommandErrorTyped("当前处于战斗中，不能调整装备。");
        var activeModalKind = snapshot.ActiveModalKind;
        if (
            activeModalKind == RuntimeModalKind.Reward
            || activeModalKind == RuntimeModalKind.Promotion
            || activeModalKind == RuntimeModalKind.Settlement
            || activeModalKind == RuntimeModalKind.CharacterInfo
        )
            return CommandErrorTyped("当前窗口会阻止装备切换。");

        PartyEquipmentCommandResult result = _port.UnequipPartyItemAndPersist(
            memberId,
            slotId
        );
        if (!result.Success)
            return CommandErrorTyped(BuildEquipmentErrorMessage(result, false));

        var itemName = result.ItemDisplayName;
        var slotLabel = result.SlotLabel;
        var successMessage = string.Format(
            "已从 {0} 的 {1} 卸下 {2}。",
            result.MemberDisplayName,
            slotLabel,
            itemName
        );
        if (result.PersistenceError != Error.Ok)
            successMessage = string.Format("{0} 但队伍状态持久化失败。", successMessage);
        _port.UpdatePartyStatus(successMessage);
        return CommandOkTyped();
    }

    internal void OnPartyManagementWindowClosed()
    {
        if (!HasRuntime())
            return;
        _port.ClosePartyManagementAndPresentPendingReward("已关闭队伍管理窗口。");
    }

    private string GetItemDisplayName(StringName itemId)
    {
        if (!HasRuntime())
            return itemId.ToString();
        return _port.GetItemDisplayName(itemId);
    }

    private string ValidateMainCharacterRoster(
        IEnumerable<StringName> activeMemberIds,
        IEnumerable<StringName> reserveMemberIds,
        StringName livingMainCharacterMemberId
    )
    {
        var memberId = livingMainCharacterMemberId;
        if (memberId == "")
            return "";
        if (HasMemberId(reserveMemberIds, memberId) || !HasMemberId(activeMemberIds, memberId))
            return "主角必须保持上阵，不能移至替补。";
        return "";
    }

    private static bool HasMemberId(IEnumerable<StringName> memberIds, StringName memberId)
    {
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (memberIds == null || normalizedMemberId == "")
            return false;
        foreach (var rawMemberId in memberIds)
        {
            if (
                ProgressionDataUtils.to_string_name(rawMemberId).ToString()
                == normalizedMemberId.ToString()
            )
                return true;
        }
        return false;
    }

    private static StringNameList NormalizeMemberIds(IEnumerable<StringName> memberIds)
    {
        var result = new StringNameList();
        if (memberIds == null)
            return result;
        foreach (var rawMemberId in memberIds)
        {
            var memberId = ProgressionDataUtils.to_string_name(rawMemberId);
            if (memberId != "" && !HasMemberId(result, memberId))
                result.Add(memberId);
        }
        return result;
    }

    private static StringNameList WithoutMemberId(
        IEnumerable<StringName> memberIds,
        StringName memberId
    )
    {
        var result = new StringNameList();
        var normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (memberIds == null)
            return result;
        foreach (var rawMemberId in memberIds)
        {
            var currentMemberId = ProgressionDataUtils.to_string_name(rawMemberId);
            if (currentMemberId != "" && currentMemberId.ToString() != normalizedMemberId.ToString())
                result.Add(currentMemberId);
        }
        return result;
    }

    private string GetMemberDisplayName(StringName memberId)
    {
        if (!HasRuntime())
            return memberId.ToString();
        return _port.GetMemberDisplayName(memberId);
    }

    private string BuildEquipmentErrorMessage(
        PartyEquipmentCommandResult result,
        bool isEquipAction
    )
    {
        var memberId = result.MemberId;
        var slotLabel = string.IsNullOrEmpty(result.SlotLabel) ? "装备槽" : result.SlotLabel;
        var itemId = result.ItemId;
        var errorCode = result.ErrorCode;
        switch (errorCode)
        {
            case "member_not_found":
                return string.Format("未找到队伍成员 {0}。", memberId);
            case "item_not_found":
                return string.Format("未找到物品定义 {0}。", itemId);
            case "item_not_equipment":
                return string.Format("{0} 不是可装备物品。", GetItemDisplayName(itemId));
            case "slot_unresolved":
                return string.Format("{0} 当前没有可用装备槽。", GetItemDisplayName(itemId));
            case "slot_not_allowed":
                return string.Format("{0} 不能装备到 {1}。", GetItemDisplayName(itemId), slotLabel);
            case "warehouse_missing_item":
                return string.Format(
                    "共享仓库中没有可用于装备的 {0}。",
                    GetItemDisplayName(itemId)
                );
            case "warehouse_missing_instance":
                return string.Format(
                    "共享仓库中没有指定的 {0} 装备实例。",
                    GetItemDisplayName(itemId)
                );
            case "equipment_instance_id_required":
                return string.Format(
                    "共享仓库中有多件 {0}，请指定装备实例。",
                    GetItemDisplayName(itemId)
                );
            case "equipment_instance_item_mismatch":
                return string.Format("指定装备实例不属于 {0}。", GetItemDisplayName(itemId));
            case "warehouse_blocked_swap":
                return string.Format("{0} 当前没有空间接回被替换下来的装备。", slotLabel);
            case "slot_invalid":
                return "装备槽无效。";
            case "slot_empty":
                return string.Format("{0} 当前没有已装备物品。", slotLabel);
            case "warehouse_full":
                return string.Format(
                    "共享仓库空间不足，无法卸下 {0}。",
                    GetItemDisplayName(itemId)
                );
            case "missing_profession":
                return string.Format(
                    "{0} 当前职业不满足 {1} 的装备要求。",
                    GetMemberDisplayName(memberId),
                    GetItemDisplayName(itemId)
                );
            case "body_size_too_small":
                return string.Format(
                    "{0} 体型过小，无法装备 {1}。",
                    GetMemberDisplayName(memberId),
                    GetItemDisplayName(itemId)
                );
            case "body_size_too_large":
                return string.Format(
                    "{0} 体型过大，无法装备 {1}。",
                    GetMemberDisplayName(memberId),
                    GetItemDisplayName(itemId)
                );
            case "attribute_too_low":
                return string.Format(
                    "{0} 属性不足，无法装备 {1}。",
                    GetMemberDisplayName(memberId),
                    GetItemDisplayName(itemId)
                );
            case "requirement_failed":
                return string.Format("{0} 不满足装备要求。", GetItemDisplayName(itemId));
            default:
                return isEquipAction ? "装备操作失败。" : "卸装操作失败。";
        }
    }

    private bool HasRuntime()
    {
        return _port != null;
    }

    private GameRuntimeFacade.RuntimeCommandResult CommandOkTyped(string message = "")
    {
        return GameRuntimeFacade.RuntimeCommandResult.Success(message ?? "");
    }

    private GameRuntimeFacade.RuntimeCommandResult CommandErrorTyped(string message)
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            message ?? "",
            GameRuntimeFacade.RuntimeCommandCode.InvalidState
        );
    }

    private GameRuntimeFacade.RuntimeCommandResult RuntimeUnavailableTypedResult()
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            RuntimeUnavailableMessage,
            GameRuntimeFacade.RuntimeCommandCode.RuntimeUnavailable
        );
    }

    private PartyCommandSnapshot CapturePartyCommandSnapshot() =>
        _port.CapturePartyCommandSnapshot();

    private static IGameRuntimePartyCommandPort ResolveWeakRef(
        WeakReference<IGameRuntimePartyCommandPort> weakRef
    )
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out IGameRuntimePartyCommandPort target)
        )
            return null;
        return target;
    }
}
