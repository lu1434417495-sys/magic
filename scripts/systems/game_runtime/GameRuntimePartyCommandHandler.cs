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

    public void setup(GameRuntimeFacade runtime)
    {
        Setup(runtime);
    }

    public void Dispose()
    {
        _runtime = null;
    }

    public void dispose()
    {
        Dispose();
    }

    public Dictionary command_open_party() => CommandOpenParty();

    public Dictionary command_select_party_member(StringName memberId) =>
        CommandSelectPartyMember(memberId);

    public Dictionary command_set_party_leader(StringName memberId) =>
        CommandSetPartyLeader(memberId);

    public Dictionary command_move_member_to_active(StringName memberId) =>
        CommandMoveMemberToActive(memberId);

    public Dictionary command_move_member_to_reserve(StringName memberId) =>
        CommandMoveMemberToReserve(memberId);

    public Dictionary command_party_equip_item(
        StringName memberId,
        StringName itemId,
        StringName slotId,
        StringName instanceId
    ) => CommandPartyEquipItem(memberId, itemId, slotId, instanceId);

    public Dictionary command_party_unequip_item(StringName memberId, StringName slotId) =>
        CommandPartyUnequipItem(memberId, slotId);

    public Dictionary apply_party_roster(
        Array<StringName> activeMemberIds,
        Array<StringName> reserveMemberIds
    ) => ApplyPartyRoster(activeMemberIds, reserveMemberIds);

    public void open_party_management_window() => OpenPartyManagementWindow();

    public void on_party_leader_change_requested(StringName memberId) =>
        OnPartyLeaderChangeRequested(memberId);

    public void on_party_roster_change_requested(
        Array<StringName> activeMemberIds,
        Array<StringName> reserveMemberIds
    ) => OnPartyRosterChangeRequested(activeMemberIds, reserveMemberIds);

    public void on_party_management_window_closed() => OnPartyManagementWindowClosed();

    public void on_party_management_warehouse_requested() => OnPartyManagementWarehouseRequested();

    public void apply_party_state_to_runtime(string successMessage) =>
        ApplyPartyStateToRuntime(successMessage);

    public Dictionary CommandOpenParty()
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        if (!HasGenerationConfig())
            return CommandError("世界地图尚未初始化。");
        if (IsBattleActive())
            return CommandError("当前处于战斗中，不能打开队伍管理。");
        if (IsModalWindowOpen())
            return CommandError("当前有窗口打开，不能打开队伍管理。");
        OpenPartyManagementWindow();
        return CommandOk();
    }

    public Dictionary CommandSelectPartyMember(StringName memberId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        var partyState = GetPartyState();
        if (partyState == null)
            return CommandError("当前不存在队伍数据。");
        if (partyState.get_member_state(memberId) == null)
            return CommandError(string.Format("未找到队伍成员 {0}。", memberId));
        var activeMemberIds = partyState.active_member_ids;
        var reserveMemberIds = partyState.reserve_member_ids;
        if (!HasMemberId(activeMemberIds, memberId) && !HasMemberId(reserveMemberIds, memberId))
            return CommandError(
                string.Format("{0} 当前不在队伍编成中。", GetMemberDisplayName(memberId))
            );
        if (GetActiveModalId() == "")
            SetActiveModalId("party");
        SetPartySelectedMemberId(memberId);
        UpdateStatus(string.Format("已选中队员 {0}。", GetMemberDisplayName(memberId)));
        return CommandOk();
    }

    public Dictionary CommandSetPartyLeader(StringName memberId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        var partyState = GetPartyState();
        if (partyState == null)
            return CommandError("当前不存在队伍数据。");
        var activeMemberIds = partyState.active_member_ids;
        if (!HasMemberId(activeMemberIds, memberId))
            return CommandError("只有上阵成员才能成为队长。");
        OnPartyLeaderChangeRequested(memberId);
        SetPartySelectedMemberId(memberId);
        return CommandOk();
    }

    public Dictionary CommandMoveMemberToActive(StringName memberId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        var partyState = GetPartyState();
        if (partyState == null)
            return CommandError("当前不存在队伍数据。");
        var reserveMemberIds = partyState.reserve_member_ids;
        if (!HasMemberId(reserveMemberIds, memberId))
            return CommandError(
                string.Format("{0} 当前不在替补列表中。", GetMemberDisplayName(memberId))
            );
        var activeMemberIds = partyState.active_member_ids;
        if (activeMemberIds.Count >= 4)
            return CommandError("上阵人数已达到上限。");
        var activeIds = NormalizeMemberIds(activeMemberIds);
        var reserveIds = NormalizeMemberIds(reserveMemberIds);
        reserveIds = WithoutMemberId(reserveIds, memberId);
        if (!HasMemberId(activeIds, memberId))
            activeIds.Add(memberId);
        OnPartyRosterChangeRequested(activeIds, reserveIds);
        SetPartySelectedMemberId(memberId);
        return CommandOk();
    }

    public Dictionary CommandMoveMemberToReserve(StringName memberId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        var partyState = GetPartyState();
        if (partyState == null)
            return CommandError("当前不存在队伍数据。");
        var activeMemberIds = partyState.active_member_ids;
        if (!HasMemberId(activeMemberIds, memberId))
            return CommandError(
                string.Format("{0} 当前不在上阵列表中。", GetMemberDisplayName(memberId))
            );
        if (memberId == GetMainCharacterMemberId(partyState))
            return CommandError("主角必须保持上阵，不能移至替补。");
        if (activeMemberIds.Count <= 1)
            return CommandError("队伍至少需要保留一名上阵成员。");
        var activeIds = NormalizeMemberIds(activeMemberIds);
        var reserveIds = NormalizeMemberIds(partyState.reserve_member_ids);
        activeIds = WithoutMemberId(activeIds, memberId);
        if (!HasMemberId(reserveIds, memberId))
            reserveIds.Add(memberId);
        OnPartyRosterChangeRequested(activeIds, reserveIds);
        SetPartySelectedMemberId(memberId);
        return CommandOk();
    }

    public Dictionary CommandPartyEquipItem(
        StringName memberId,
        StringName itemId,
        StringName slotId,
        StringName instanceId = default
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        if (GetPartyState() == null)
            return CommandError("当前不存在队伍数据。");
        if (IsBattleActive())
            return CommandError("当前处于战斗中，不能调整装备。");
        var activeModalId = GetActiveModalId();
        if (
            activeModalId == "reward"
            || activeModalId == "promotion"
            || activeModalId == "settlement"
            || activeModalId == "character_info"
        )
            return CommandError("当前窗口会阻止装备切换。");

        var result = EquipPartyItem(memberId, itemId, slotId, instanceId);
        if (!DictionaryBool(result, "success", false))
            return CommandError(BuildEquipmentErrorMessage(result, true));

        SetPartySelectedMemberId(memberId);
        var itemName = GetItemDisplayName(DictionaryStringName(result, "item_id"));
        var slotLabel = DictionaryString(result, "slot_label");
        var successMessage = string.Format(
            "已为 {0} 装备 {1}（{2}）。",
            GetMemberDisplayName(memberId),
            itemName,
            slotLabel
        );
        var previousItemId = DictionaryStringName(result, "previous_item_id");
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
        return CommandOk();
    }

    public Dictionary CommandPartyUnequipItem(StringName memberId, StringName slotId)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        if (GetPartyState() == null)
            return CommandError("当前不存在队伍数据。");
        if (IsBattleActive())
            return CommandError("当前处于战斗中，不能调整装备。");
        var activeModalId = GetActiveModalId();
        if (
            activeModalId == "reward"
            || activeModalId == "promotion"
            || activeModalId == "settlement"
            || activeModalId == "character_info"
        )
            return CommandError("当前窗口会阻止装备切换。");

        var result = UnequipPartyItem(memberId, slotId);
        if (!DictionaryBool(result, "success", false))
            return CommandError(BuildEquipmentErrorMessage(result, false));

        SetPartySelectedMemberId(memberId);
        var itemName = GetItemDisplayName(DictionaryStringName(result, "item_id"));
        var slotLabel = DictionaryString(result, "slot_label");
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
        return CommandOk();
    }

    public Dictionary ApplyPartyRoster(
        Array<StringName> activeMemberIds,
        Array<StringName> reserveMemberIds
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        var partyState = GetPartyState();
        if (partyState == null)
            return CommandError("当前不存在队伍数据。");
        var rosterError = ValidateMainCharacterRoster(
            activeMemberIds,
            reserveMemberIds,
            partyState
        );
        if (!string.IsNullOrEmpty(rosterError))
            return CommandError(rosterError);
        OnPartyRosterChangeRequested(activeMemberIds, reserveMemberIds);
        return CommandOk();
    }

    public void OpenPartyManagementWindow()
    {
        if (!HasRuntime())
            return;
        if (IsBattleActive())
            return;
        var partyState = GetPartyState();
        SetActiveModalId("party");
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
        SetActiveModalId("");
        UpdateStatus("已关闭队伍管理窗口。");
        PresentPendingRewardIfReady();
    }

    public void OnPartyManagementWarehouseRequested()
    {
        if (!HasRuntime())
            return;
        SetActiveModalId("");
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
        return (Error)_runtime.persist_party_state();
    }

    private string GetItemDisplayName(StringName itemId)
    {
        if (!HasRuntime())
            return itemId.ToString();
        return _runtime.get_item_display_name(itemId);
    }

    private StringName GetMainCharacterMemberId(PartyState partyState)
    {
        if (partyState == null)
            return "";
        var memberId = partyState.get_resolved_main_character_member_id();
        if (memberId == "")
            return "";
        if (partyState.is_member_dead(memberId))
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
        return _runtime.get_member_display_name(memberId);
    }

    private string GetSkillDisplayName(StringName skillId)
    {
        var gameSession = GetGameSession();
        if (gameSession == null)
            return skillId.ToString();
        var skillDefs = gameSession.get_skill_defs();
        if (skillDefs.ContainsKey(skillId))
        {
            var skillDef = skillDefs[skillId].As<SkillDef>();
            if (skillDef != null && !string.IsNullOrEmpty(skillDef.display_name))
                return skillDef.display_name;
        }
        return skillId.ToString();
    }

    private string BuildEquipmentErrorMessage(Dictionary result, bool isEquipAction)
    {
        var memberId = DictionaryStringName(result, "member_id");
        var slotLabel = DictionaryString(result, "slot_label", "装备槽");
        var itemId = DictionaryStringName(result, "item_id");
        var errorCode = DictionaryString(result, "error_code");
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
        return _runtime.build_command_ok(message);
    }

    private Dictionary CommandError(string message)
    {
        if (!HasRuntime())
            return new Dictionary { ["ok"] = false, ["message"] = message };
        return _runtime.build_command_error(message);
    }

    private Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }

    private bool HasGenerationConfig()
    {
        if (!HasRuntime())
            return false;
        return _runtime.get_generation_config() != null;
    }

    private bool IsBattleActive()
    {
        if (!HasRuntime())
            return false;
        return _runtime.is_battle_active();
    }

    private bool IsModalWindowOpen()
    {
        if (!HasRuntime())
            return false;
        return _runtime.is_modal_window_open();
    }

    private PartyState GetPartyState()
    {
        if (!HasRuntime())
            return null;
        return _runtime.get_party_state();
    }

    private void SetPartyState(PartyState partyState)
    {
        if (HasRuntime())
            _runtime.set_party_state(partyState);
    }

    private string GetActiveModalId()
    {
        if (!HasRuntime())
            return "";
        return _runtime.get_active_modal_id();
    }

    private void SetActiveModalId(string modalId)
    {
        if (HasRuntime())
            _runtime.set_runtime_active_modal_id(modalId);
    }

    private StringName GetPartySelectedMemberId()
    {
        if (!HasRuntime())
            return "";
        return _runtime.get_party_selected_member_id();
    }

    private void SetPartySelectedMemberId(StringName memberId)
    {
        if (HasRuntime())
            _runtime.set_party_selected_member_id(memberId);
    }

    private Dictionary EquipPartyItem(
        StringName memberId,
        StringName itemId,
        StringName slotId,
        StringName instanceId
    )
    {
        if (!HasRuntime())
            return new Dictionary();
        return _runtime.equip_party_item(memberId, itemId, slotId, instanceId);
    }

    private Dictionary UnequipPartyItem(StringName memberId, StringName slotId)
    {
        if (!HasRuntime())
            return new Dictionary();
        return _runtime.unequip_party_item(memberId, slotId);
    }

    private void SyncCharacterManagementPartyState()
    {
        if (HasRuntime())
            _runtime.sync_character_management_party_state();
    }

    private void OpenPartyWarehouseWindow(string entryLabel)
    {
        if (HasRuntime())
            _runtime.open_party_warehouse_window(entryLabel);
    }

    private bool PresentPendingRewardIfReady()
    {
        if (!HasRuntime())
            return false;
        return _runtime.present_pending_reward_if_ready();
    }

    private void UpdateStatus(string message)
    {
        if (HasRuntime())
            _runtime.update_status(message);
    }

    private string GetStatusText()
    {
        if (!HasRuntime())
            return "";
        return _runtime.get_status_text();
    }

    private GameSession GetGameSession()
    {
        if (!HasRuntime())
            return null;
        return _runtime.get_game_session();
    }

    private void RefreshFog()
    {
        if (HasRuntime())
            _runtime.refresh_fog();
    }

    private static bool DictionaryBool(Dictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static string DictionaryString(Dictionary dictionary, string key, string fallback = "")
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        var value = dictionary[key];
        return value.VariantType != Variant.Type.Nil ? value.AsString() : fallback;
    }

    private static StringName DictionaryStringName(Dictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return "";
        return ProgressionDataUtils.to_string_name(dictionary[key]);
    }

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GameRuntimeFacade target))
            return null;
        return target;
    }
}
