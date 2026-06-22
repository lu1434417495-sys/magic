using System;
using Godot;
using Godot.Collections;

public sealed class GameRuntimePartyCommandHandler
{
    private static readonly StringName RuntimeUnavailableMessage = "运行时尚未初始化。";

    private WeakReference<GameRuntimeFacade> _runtimeRef;

    private GameRuntimeFacade _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GameRuntimeFacade>(value) : null;
    }

    public void Setup(GameRuntimeFacade runtime)
    {
        _runtime = runtime;
    }

    public void Dispose()
    {
        _runtime = null;
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandOpenPartyTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (!HasGenerationConfig())
            return CommandErrorTyped("世界地图尚未初始化。");
        if (IsBattleActive())
            return CommandErrorTyped("当前处于战斗中，不能打开队伍管理。");
        if (IsModalWindowOpen())
            return CommandErrorTyped("当前有窗口打开，不能打开队伍管理。");
        OpenPartyManagementWindow();
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandSelectPartyMemberTyped(StringName memberId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        var partyState = GetPartyState();
        if (partyState == null)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (partyState.GetMemberState(memberId) == null)
            return CommandErrorTyped(string.Format("未找到队伍成员 {0}。", memberId));
        var activeMemberIds = partyState.active_member_ids;
        var reserveMemberIds = partyState.reserve_member_ids;
        if (!HasMemberId(activeMemberIds, memberId) && !HasMemberId(reserveMemberIds, memberId))
            return CommandErrorTyped(
                string.Format("{0} 当前不在队伍编成中。", GetMemberDisplayName(memberId))
            );
        if (GetActiveModalKind() == RuntimeModalKind.None)
            SetActiveModalKind(RuntimeModalKind.Party);
        SetPartySelectedMemberId(memberId);
        UpdateStatus(string.Format("已选中队员 {0}。", GetMemberDisplayName(memberId)));
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandSetPartyLeaderTyped(StringName memberId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        var partyState = GetPartyState();
        if (partyState == null)
            return CommandErrorTyped("当前不存在队伍数据。");
        var activeMemberIds = partyState.active_member_ids;
        if (!HasMemberId(activeMemberIds, memberId))
            return CommandErrorTyped("只有上阵成员才能成为队长。");
        OnPartyLeaderChangeRequested(memberId);
        SetPartySelectedMemberId(memberId);
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandMoveMemberToActiveTyped(
        StringName memberId
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        var partyState = GetPartyState();
        if (partyState == null)
            return CommandErrorTyped("当前不存在队伍数据。");
        var reserveMemberIds = partyState.reserve_member_ids;
        if (!HasMemberId(reserveMemberIds, memberId))
            return CommandErrorTyped(
                string.Format("{0} 当前不在替补列表中。", GetMemberDisplayName(memberId))
            );
        var activeMemberIds = partyState.active_member_ids;
        if (activeMemberIds.Count >= 4)
            return CommandErrorTyped("上阵人数已达到上限。");
        var activeIds = NormalizeMemberIds(activeMemberIds);
        var reserveIds = NormalizeMemberIds(reserveMemberIds);
        reserveIds = WithoutMemberId(reserveIds, memberId);
        if (!HasMemberId(activeIds, memberId))
            activeIds.Add(memberId);
        OnPartyRosterChangeRequested(activeIds, reserveIds);
        SetPartySelectedMemberId(memberId);
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandMoveMemberToReserveTyped(
        StringName memberId
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        var partyState = GetPartyState();
        if (partyState == null)
            return CommandErrorTyped("当前不存在队伍数据。");
        var activeMemberIds = partyState.active_member_ids;
        if (!HasMemberId(activeMemberIds, memberId))
            return CommandErrorTyped(
                string.Format("{0} 当前不在上阵列表中。", GetMemberDisplayName(memberId))
            );
        if (memberId == GetMainCharacterMemberId(partyState))
            return CommandErrorTyped("主角必须保持上阵，不能移至替补。");
        if (activeMemberIds.Count <= 1)
            return CommandErrorTyped("队伍至少需要保留一名上阵成员。");
        var activeIds = NormalizeMemberIds(activeMemberIds);
        var reserveIds = NormalizeMemberIds(partyState.reserve_member_ids);
        activeIds = WithoutMemberId(activeIds, memberId);
        if (!HasMemberId(reserveIds, memberId))
            reserveIds.Add(memberId);
        OnPartyRosterChangeRequested(activeIds, reserveIds);
        SetPartySelectedMemberId(memberId);
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandApplyPartyRosterTyped(
        Array<StringName> activeMemberIds,
        Array<StringName> reserveMemberIds
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        var partyState = GetPartyState();
        if (partyState == null)
            return CommandErrorTyped("当前不存在队伍数据。");
        var rosterError = ValidateMainCharacterRoster(
            activeMemberIds,
            reserveMemberIds,
            partyState
        );
        if (!string.IsNullOrEmpty(rosterError))
            return CommandErrorTyped(rosterError);
        OnPartyRosterChangeRequested(activeMemberIds, reserveMemberIds);
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
        if (GetPartyState() == null)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (IsBattleActive())
            return CommandErrorTyped("当前处于战斗中，不能调整装备。");
        var activeModalKind = GetActiveModalKind();
        if (
            activeModalKind == RuntimeModalKind.Reward
            || activeModalKind == RuntimeModalKind.Promotion
            || activeModalKind == RuntimeModalKind.Settlement
            || activeModalKind == RuntimeModalKind.CharacterInfo
        )
            return CommandErrorTyped("当前窗口会阻止装备切换。");

        var result = EquipPartyItem(memberId, itemId, slotId, instanceId);
        if (!result.Success)
            return CommandErrorTyped(BuildEquipmentErrorMessage(result, true));

        SetPartySelectedMemberId(memberId);
        var itemName = GetItemDisplayName(result.ItemId);
        var slotLabel = result.SlotLabel;
        var successMessage = string.Format(
            "已为 {0} 装备 {1}（{2}）。",
            GetMemberDisplayName(memberId),
            itemName,
            slotLabel
        );
        var previousItemId = result.PreviousItemId;
        if (previousItemId != "")
        {
            successMessage = string.Format(
                "已为 {0} 装备 {1}（{2}），并卸下 {3}。",
                GetMemberDisplayName(memberId),
                itemName,
                slotLabel,
                GetItemDisplayName(previousItemId)
            );
        }

        var persistError = PersistPartyState();
        if (persistError == Error.Ok)
            UpdateStatus(successMessage);
        else
            UpdateStatus(string.Format("{0} 但队伍状态持久化失败。", successMessage));
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandPartyUnequipItemTyped(
        StringName memberId,
        StringName slotId
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (GetPartyState() == null)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (IsBattleActive())
            return CommandErrorTyped("当前处于战斗中，不能调整装备。");
        var activeModalKind = GetActiveModalKind();
        if (
            activeModalKind == RuntimeModalKind.Reward
            || activeModalKind == RuntimeModalKind.Promotion
            || activeModalKind == RuntimeModalKind.Settlement
            || activeModalKind == RuntimeModalKind.CharacterInfo
        )
            return CommandErrorTyped("当前窗口会阻止装备切换。");

        var result = UnequipPartyItem(memberId, slotId);
        if (!result.Success)
            return CommandErrorTyped(BuildEquipmentErrorMessage(result, false));

        SetPartySelectedMemberId(memberId);
        var itemName = GetItemDisplayName(result.ItemId);
        var slotLabel = result.SlotLabel;
        var successMessage = string.Format(
            "已从 {0} 的 {1} 卸下 {2}。",
            GetMemberDisplayName(memberId),
            slotLabel,
            itemName
        );
        var persistError = PersistPartyState();
        if (persistError == Error.Ok)
            UpdateStatus(successMessage);
        else
            UpdateStatus(string.Format("{0} 但队伍状态持久化失败。", successMessage));
        return CommandOkTyped();
    }

    public void OpenPartyManagementWindow()
    {
        if (!HasRuntime())
            return;
        if (IsBattleActive())
            return;
        var partyState = GetPartyState();
        SetActiveModalKind(RuntimeModalKind.Party);
        var selectedMemberId = GetPartySelectedMemberId();
        if (selectedMemberId == "" && partyState != null)
        {
            var activeMemberIds = partyState.active_member_ids;
            if (activeMemberIds.Count > 0)
                SetPartySelectedMemberId(activeMemberIds[0]);
        }
        UpdateStatus("已打开队伍管理窗口。");
    }

    public void OnPartyLeaderChangeRequested(StringName memberId)
    {
        var partyState = GetPartyState();
        if (!HasRuntime() || partyState == null)
            return;
        partyState.leader_member_id = memberId;
        ApplyPartyStateToRuntime(string.Format("队长已切换为 {0}。", memberId));
    }

    public void OnPartyRosterChangeRequested(
        Array<StringName> activeMemberIds,
        Array<StringName> reserveMemberIds
    )
    {
        var partyState = GetPartyState();
        if (!HasRuntime() || partyState == null)
            return;
        var rosterError = ValidateMainCharacterRoster(
            activeMemberIds,
            reserveMemberIds,
            partyState
        );
        if (!string.IsNullOrEmpty(rosterError))
        {
            UpdateStatus(rosterError);
            return;
        }
        var normalizedActiveMemberIds = NormalizeMemberIds(activeMemberIds);
        var normalizedReserveMemberIds = NormalizeMemberIds(reserveMemberIds);
        partyState.active_member_ids = normalizedActiveMemberIds;
        partyState.reserve_member_ids = normalizedReserveMemberIds;
        if (
            !HasMemberId(normalizedActiveMemberIds, partyState.leader_member_id)
            && normalizedActiveMemberIds.Count > 0
        )
            partyState.leader_member_id = normalizedActiveMemberIds[0];
        ApplyPartyStateToRuntime("队伍编成已更新。");
    }

    public void OnPartyManagementWindowClosed()
    {
        if (!HasRuntime())
            return;
        SetActiveModalKind(RuntimeModalKind.None);
        UpdateStatus("已关闭队伍管理窗口。");
        PresentPendingRewardIfReady();
    }

    public void OnPartyManagementWarehouseRequested()
    {
        if (!HasRuntime())
            return;
        SetActiveModalKind(RuntimeModalKind.None);
        OpenPartyWarehouseWindow("队伍管理");
        UpdateStatus("已从队伍管理打开共享仓库。");
    }

    public void ApplyPartyStateToRuntime(string successMessage)
    {
        if (!HasRuntime())
            return;
        SyncCharacterManagementPartyState();
        var persistError = PersistPartyState();
        if (persistError == Error.Ok)
            UpdateStatus(successMessage);
        else
            UpdateStatus(string.Format("{0} 但队伍状态持久化失败。", successMessage));
    }

    private Error PersistPartyState()
    {
        if (!HasRuntime())
            return Error.Unavailable;
        return (Error)_runtime.PersistPartyState();
    }

    private string GetItemDisplayName(StringName itemId)
    {
        if (!HasRuntime())
            return itemId.ToString();
        return _runtime.GetItemDisplayName(itemId);
    }

    private StringName GetMainCharacterMemberId(PartyState partyState)
    {
        if (partyState == null)
            return "";
        var memberId = partyState.GetResolvedMainCharacterMemberId();
        if (memberId == "")
            return "";
        if (partyState.IsMemberDead(memberId))
            return "";
        return memberId;
    }

    private string ValidateMainCharacterRoster(
        Array<StringName> activeMemberIds,
        Array<StringName> reserveMemberIds,
        PartyState partyState
    )
    {
        var memberId = GetMainCharacterMemberId(partyState);
        if (memberId == "")
            return "";
        if (HasMemberId(reserveMemberIds, memberId) || !HasMemberId(activeMemberIds, memberId))
            return "主角必须保持上阵，不能移至替补。";
        return "";
    }

    private static bool HasMemberId(Array<StringName> memberIds, StringName memberId)
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

    private static Array<StringName> NormalizeMemberIds(Array<StringName> memberIds)
    {
        var result = new Array<StringName>();
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

    private static Array<StringName> WithoutMemberId(
        Array<StringName> memberIds,
        StringName memberId
    )
    {
        var result = new Array<StringName>();
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
        return _runtime.GetMemberDisplayName(memberId);
    }

    private string GetSkillDisplayName(StringName skillId)
    {
        return HasRuntime() ? _runtime.GetSkillDisplayName(skillId) : skillId.ToString();
    }

    private string BuildEquipmentErrorMessage(
        PartyEquipmentService.EquipmentActionResult result,
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
            case "requirement_failed":
                return string.Format("{0} 不满足装备要求。", GetItemDisplayName(itemId));
            default:
                return isEquipAction ? "装备操作失败。" : "卸装操作失败。";
        }
    }

    private bool HasRuntime()
    {
        return _runtime != null;
    }

    private Dictionary CommandOk(string message = "")
    {
        if (!HasRuntime())
            return new Dictionary
            {
                ["ok"] = true,
                ["message"] = message,
                ["battle_refresh_mode"] = "",
            };
        return _runtime.BuildCommandOk(message);
    }

    private GameRuntimeFacade.RuntimeCommandResult CommandOkTyped(string message = "")
    {
        return GameRuntimeFacade.RuntimeCommandResult.Success(message ?? "");
    }

    private Dictionary CommandError(string message)
    {
        if (!HasRuntime())
            return new Dictionary { ["ok"] = false, ["message"] = message };
        return _runtime.BuildCommandError(message);
    }

    private GameRuntimeFacade.RuntimeCommandResult CommandErrorTyped(string message)
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            message ?? "",
            GameRuntimeFacade.RuntimeCommandCode.InvalidState
        );
    }

    private Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }

    private GameRuntimeFacade.RuntimeCommandResult RuntimeUnavailableTypedResult()
    {
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            RuntimeUnavailableMessage,
            GameRuntimeFacade.RuntimeCommandCode.RuntimeUnavailable
        );
    }

    private bool HasGenerationConfig()
    {
        if (!HasRuntime())
            return false;
        return _runtime.GetGenerationConfig() != null;
    }

    private bool IsBattleActive()
    {
        if (!HasRuntime())
            return false;
        return _runtime.IsBattleActive();
    }

    private bool IsModalWindowOpen()
    {
        if (!HasRuntime())
            return false;
        return _runtime.IsModalWindowOpen();
    }

    private PartyState GetPartyState()
    {
        if (!HasRuntime())
            return null;
        return _runtime.GetPartyState();
    }

    private void SetPartyState(PartyState partyState)
    {
        if (HasRuntime())
            _runtime.SetPartyState(partyState);
    }

    private RuntimeModalKind GetActiveModalKind()
    {
        return HasRuntime() ? _runtime.GetActiveModalKind() : RuntimeModalKind.None;
    }

    private void SetActiveModalKind(RuntimeModalKind modalKind)
    {
        if (HasRuntime())
            _runtime.SetRuntimeActiveModalKind(modalKind);
    }

    private StringName GetPartySelectedMemberId()
    {
        if (!HasRuntime())
            return "";
        return _runtime.GetPartySelectedMemberId();
    }

    private void SetPartySelectedMemberId(StringName memberId)
    {
        if (HasRuntime())
            _runtime.SetPartySelectedMemberId(memberId);
    }

    private PartyEquipmentService.EquipmentActionResult EquipPartyItem(
        StringName memberId,
        StringName itemId,
        StringName slotId,
        StringName instanceId
    )
    {
        if (!HasRuntime())
            return new PartyEquipmentService.EquipmentActionResult(
                false,
                memberId,
                slotId,
                itemId,
                "",
                "",
                "",
                "runtime_unavailable"
            );
        var service = _runtime.GetPartyEquipmentService();
        return service != null
            ? service.EquipItemTyped(memberId, itemId, slotId, instanceId)
            : new PartyEquipmentService.EquipmentActionResult(
                false,
                memberId,
                slotId,
                itemId,
                "",
                "",
                "",
                "runtime_unavailable"
            );
    }

    private PartyEquipmentService.EquipmentActionResult UnequipPartyItem(
        StringName memberId,
        StringName slotId
    )
    {
        if (!HasRuntime())
            return new PartyEquipmentService.EquipmentActionResult(
                false,
                memberId,
                slotId,
                "",
                "",
                "",
                "",
                "runtime_unavailable"
            );
        var service = _runtime.GetPartyEquipmentService();
        return service != null
            ? service.UnequipItemTyped(memberId, slotId)
            : new PartyEquipmentService.EquipmentActionResult(
                false,
                memberId,
                slotId,
                "",
                "",
                "",
                "",
                "runtime_unavailable"
            );
    }

    private void SyncCharacterManagementPartyState()
    {
        if (HasRuntime())
            _runtime.SyncCharacterManagementPartyState();
    }

    private void OpenPartyWarehouseWindow(string entryLabel)
    {
        if (HasRuntime())
            _runtime.OpenPartyWarehouseWindow(entryLabel);
    }

    private bool PresentPendingRewardIfReady()
    {
        if (!HasRuntime())
            return false;
        return _runtime.PresentPendingRewardIfReady();
    }

    private void UpdateStatus(string message)
    {
        if (HasRuntime())
            _runtime.UpdateStatus(message);
    }

    private string GetStatusText()
    {
        if (!HasRuntime())
            return "";
        return _runtime.GetStatusText();
    }

    private GameSession GetGameSession()
    {
        if (!HasRuntime())
            return null;
        return _runtime.GetGameSession();
    }

    private void RefreshFog()
    {
        if (HasRuntime())
            _runtime.RefreshFog();
    }

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GameRuntimeFacade target))
            return null;
        return target;
    }
}
