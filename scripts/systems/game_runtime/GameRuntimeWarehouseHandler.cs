using System;
using Godot;
using Godot.Collections;

public sealed class GameRuntimeWarehouseHandler
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";

    private sealed class WarehouseTransactionSnapshot
    {
        public Dictionary RuntimeState { get; set; } = new();
        public PartyState PartyState { get; set; }
        public Dictionary WorldData { get; set; } = new();
        public StringName SelectedMemberId { get; set; } = "";
    }

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

    public Dictionary get_warehouse_window_data() => GetWarehouseWindowData();

    public Dictionary command_open_party_warehouse() => CommandOpenPartyWarehouse();

    public Dictionary command_discard_one(StringName itemId, StringName instanceId) =>
        CommandDiscardOne(itemId, instanceId);

    public Dictionary command_discard_all(StringName itemId, StringName instanceId) =>
        CommandDiscardAll(itemId, instanceId);

    public Dictionary command_use_item(
        StringName itemId,
        StringName memberId,
        Dictionary options
    ) => CommandUseItem(itemId, memberId, options);

    public Dictionary command_add_item(StringName itemId, int quantity) =>
        CommandAddItem(itemId, quantity);

    public void open_party_warehouse_window(string entryLabel) =>
        OpenPartyWarehouseWindow(entryLabel);

    public Dictionary on_party_warehouse_discard_one_requested(
        StringName itemId,
        StringName instanceId
    ) => OnPartyWarehouseDiscardOneRequested(itemId, instanceId);

    public Dictionary on_party_warehouse_discard_all_requested(
        StringName itemId,
        StringName instanceId
    ) => OnPartyWarehouseDiscardAllRequested(itemId, instanceId);

    public Dictionary on_party_warehouse_use_requested(
        StringName itemId,
        StringName memberId,
        Dictionary options
    ) => OnPartyWarehouseUseRequested(itemId, memberId, options);

    public void on_party_warehouse_window_closed() => OnPartyWarehouseWindowClosed();

    public Dictionary GetWarehouseWindowData()
    {
        if (!HasRuntime())
            return new Dictionary();
        if (GetPartyState() == null || GetPartyWarehouseService() == null)
            return new Dictionary();
        return BuildWarehouseWindowData();
    }

    public Dictionary CommandOpenPartyWarehouse()
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        if (GetPartyState() == null)
            return CommandError("当前不存在队伍数据。");
        if (IsBattleActive())
            return CommandError("当前处于战斗中，不能打开共享仓库。");

        if (GetActiveModalId() == "settlement")
        {
            OpenPartyWarehouseWindow("据点服务");
            UpdateStatus("已从据点窗口打开共享仓库。");
        }
        else
        {
            OpenPartyWarehouseWindow("队伍管理");
            UpdateStatus("已打开共享仓库。");
        }
        return CommandOk();
    }

    public Dictionary CommandDiscardOne(StringName itemId, StringName instanceId = default)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        if (GetActiveModalId() != "warehouse")
            return CommandError("共享仓库当前未打开。");
        if (GetPartyWarehouseService() == null)
            return CommandError("共享仓库服务尚未准备完成。");
        var result = OnPartyWarehouseDiscardOneRequested(itemId, instanceId);
        if (!DictionaryBool(result, "success", true))
            return CommandError(DictionaryString(result, "message", "当前无法丢弃该物品。"));
        return CommandOk();
    }

    public Dictionary CommandDiscardAll(StringName itemId, StringName instanceId = default)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        if (GetActiveModalId() != "warehouse")
            return CommandError("共享仓库当前未打开。");
        if (GetPartyWarehouseService() == null)
            return CommandError("共享仓库服务尚未准备完成。");
        var result = OnPartyWarehouseDiscardAllRequested(itemId, instanceId);
        if (!DictionaryBool(result, "success", true))
            return CommandError(DictionaryString(result, "message", "当前无法丢弃该物品。"));
        return CommandOk();
    }

    public Dictionary CommandUseItem(
        StringName itemId,
        StringName memberId = default,
        Dictionary options = null
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        if (GetActiveModalId() != "warehouse")
            return CommandError("共享仓库当前未打开。");
        var resolvedMemberId = ResolveWarehouseTargetMemberId(memberId);
        if (resolvedMemberId == "")
            return CommandError("当前没有可使用技能书的目标角色。");
        var useResult = OnPartyWarehouseUseRequested(
            itemId,
            resolvedMemberId,
            options ?? new Dictionary()
        );
        if (!DictionaryBool(useResult, "success", false))
            return CommandError(DictionaryString(useResult, "message", "当前无法使用该物品。"));
        return CommandOk();
    }

    public Dictionary CommandAddItem(StringName itemId, int quantity)
    {
        if (!HasRuntime())
            return RuntimeUnavailableError();
        if (GetPartyState() == null)
            return CommandError("当前不存在队伍数据。");
        if (IsBattleActive())
            return CommandError("当前处于战斗中，不能直接改动共享仓库。");
        if (quantity <= 0)
            return CommandError("加入数量必须大于 0。");
        if (GetPartyWarehouseService() == null)
            return CommandError("共享仓库服务尚未准备完成。");

        var snapshot = CaptureWarehouseTransactionSnapshot();
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var result = GetPartyWarehouseService().add_item(normalizedItemId, quantity);
        var addedQuantity = DictionaryInt(result, "added_quantity", 0);
        if (addedQuantity <= 0)
            return CommandError(
                string.Format("{0} 当前无法加入共享仓库。", GetItemDisplayName(normalizedItemId))
            );

        var successMessage = string.Format(
            "已向共享仓库加入 {0} 件 {1}。",
            addedQuantity,
            GetItemDisplayName(normalizedItemId)
        );
        var remainingQuantity = DictionaryInt(result, "remaining_quantity", 0);
        if (remainingQuantity > 0)
            successMessage = string.Format(
                "已向共享仓库加入 {0} 件 {1}，仍有 {2} 件未能放入。",
                addedQuantity,
                GetItemDisplayName(normalizedItemId),
                remainingQuantity
            );

        var persistError = PersistPartyState();
        if (persistError == Error.Ok)
        {
            UpdateStatus(successMessage);
        }
        else
        {
            var rollbackMessage = string.Format(
                "{0} 但队伍状态持久化失败，操作已回滚。",
                successMessage
            );
            RollbackWarehouseTransaction(snapshot);
            UpdateStatus(rollbackMessage);
            return CommandError(rollbackMessage);
        }
        return CommandOk();
    }

    public void OpenPartyWarehouseWindow(string entryLabel)
    {
        if (!HasRuntime())
            return;
        if (IsBattleActive())
            return;

        SetActiveModalId("warehouse");
        SetActiveWarehouseEntryLabel(string.IsNullOrEmpty(entryLabel) ? "共享入口" : entryLabel);

        var partyWarehouseService = GetPartyWarehouseService();
        var gameSession = GetGameSession();
        if (partyWarehouseService != null)
        {
            var itemDefs = gameSession != null ? gameSession.get_item_defs() : new Dictionary();
            Func<StringName> equipmentInstanceIdAllocator =
                gameSession != null ? gameSession.allocate_equipment_instance_id : null;
            partyWarehouseService.setup(GetPartyState(), itemDefs, equipmentInstanceIdAllocator);
        }
    }

    public Dictionary OnPartyWarehouseDiscardOneRequested(
        StringName itemId,
        StringName instanceId = default
    )
    {
        var partyWarehouseService = GetPartyWarehouseService();
        if (!HasRuntime() || partyWarehouseService == null)
            return new Dictionary { ["success"] = false, ["message"] = RuntimeUnavailableMessage };

        var itemName = GetItemDisplayName(itemId);
        var snapshot = CaptureWarehouseTransactionSnapshot();
        var result = RemoveWarehouseItemOrInstance(partyWarehouseService, itemId, 1, instanceId);
        if (DictionaryInt(result, "removed_quantity", 0) <= 0)
        {
            var failureMessage = BuildDiscardFailureMessage(itemId, result);
            UpdateStatus(failureMessage);
            return new Dictionary { ["success"] = false, ["message"] = failureMessage };
        }

        var persistError = PersistPartyState();
        if (persistError == Error.Ok)
        {
            UpdateStatus(string.Format("已从共享仓库丢弃 1 件 {0}。", itemName));
        }
        else
        {
            var rollbackMessage = string.Format(
                "已从共享仓库丢弃 1 件 {0}，但队伍状态持久化失败，操作已回滚。",
                itemName
            );
            RollbackWarehouseTransaction(snapshot);
            UpdateStatus(rollbackMessage);
            return new Dictionary { ["success"] = false, ["message"] = rollbackMessage };
        }
        return new Dictionary { ["success"] = true, ["message"] = GetStatusText() };
    }

    public Dictionary OnPartyWarehouseDiscardAllRequested(
        StringName itemId,
        StringName instanceId = default
    )
    {
        var partyWarehouseService = GetPartyWarehouseService();
        if (!HasRuntime() || partyWarehouseService == null)
            return new Dictionary { ["success"] = false, ["message"] = RuntimeUnavailableMessage };

        var itemName = GetItemDisplayName(itemId);
        var totalQuantity = partyWarehouseService.count_item(itemId);
        if (totalQuantity <= 0)
        {
            var noStockMessage = string.Format("{0} 当前没有可丢弃的库存。", itemName);
            UpdateStatus(noStockMessage);
            return new Dictionary { ["success"] = false, ["message"] = noStockMessage };
        }

        var snapshot = CaptureWarehouseTransactionSnapshot();
        var result = RemoveWarehouseItemOrInstance(
            partyWarehouseService,
            itemId,
            totalQuantity,
            instanceId
        );
        var removedQuantity = DictionaryInt(result, "removed_quantity", 0);
        if (removedQuantity <= 0)
        {
            var failureMessage = BuildDiscardFailureMessage(itemId, result);
            UpdateStatus(failureMessage);
            return new Dictionary { ["success"] = false, ["message"] = failureMessage };
        }

        var persistError = PersistPartyState();
        if (persistError == Error.Ok)
        {
            UpdateStatus(
                string.Format("已从共享仓库丢弃全部 {0}，共 {1} 件。", itemName, removedQuantity)
            );
        }
        else
        {
            var rollbackMessage = string.Format(
                "已从共享仓库丢弃全部 {0}，但队伍状态持久化失败，操作已回滚。",
                itemName
            );
            RollbackWarehouseTransaction(snapshot);
            UpdateStatus(rollbackMessage);
            return new Dictionary { ["success"] = false, ["message"] = rollbackMessage };
        }
        return new Dictionary { ["success"] = true, ["message"] = GetStatusText() };
    }

    public Dictionary OnPartyWarehouseUseRequested(
        StringName itemId,
        StringName memberId,
        Dictionary options
    )
    {
        if (!HasRuntime())
            return new Dictionary
            {
                ["success"] = false,
                ["reason"] = ProgressionDataUtils.to_string_name("service_unavailable"),
                ["item_id"] = ProgressionDataUtils.to_string_name(itemId).ToString(),
                ["member_id"] = "",
                ["skill_id"] = new StringName(""),
                ["consumed_quantity"] = 0,
                ["message"] = RuntimeUnavailableMessage,
            };

        var resolvedMemberId = ResolveWarehouseTargetMemberId(memberId);
        if (resolvedMemberId == "")
        {
            var missingMemberResult = new Dictionary
            {
                ["success"] = false,
                ["reason"] = ProgressionDataUtils.to_string_name("missing_member"),
                ["item_id"] = ProgressionDataUtils.to_string_name(itemId).ToString(),
                ["member_id"] = "",
                ["skill_id"] = new StringName(""),
                ["consumed_quantity"] = 0,
            };
            missingMemberResult["message"] = BuildWarehouseUseFailureMessage(missingMemberResult);
            UpdateStatus(DictionaryString(missingMemberResult, "message", ""));
            return missingMemberResult;
        }

        var partyItemUseService = GetPartyItemUseService();
        if (partyItemUseService == null)
        {
            var unavailableResult = new Dictionary
            {
                ["success"] = false,
                ["reason"] = ProgressionDataUtils.to_string_name("service_unavailable"),
                ["item_id"] = ProgressionDataUtils.to_string_name(itemId).ToString(),
                ["member_id"] = resolvedMemberId.ToString(),
                ["skill_id"] = new StringName(""),
                ["consumed_quantity"] = 0,
            };
            unavailableResult["message"] = BuildWarehouseUseFailureMessage(unavailableResult);
            UpdateStatus(DictionaryString(unavailableResult, "message", ""));
            return unavailableResult;
        }

        var snapshot = CaptureWarehouseTransactionSnapshot();
        var useResult = partyItemUseService.use_item(itemId, resolvedMemberId, options);
        if (!DictionaryBool(useResult, "success", false))
        {
            var failureMessage = BuildWarehouseUseFailureMessage(useResult);
            useResult["message"] = failureMessage;
            UpdateStatus(failureMessage);
            return useResult;
        }

        SetPartySelectedMemberId(resolvedMemberId);
        var itemName = GetItemDisplayName(itemId);
        var skillName = GetSkillDisplayName(DictionaryStringName(useResult, "skill_id", ""));
        var memberName = GetMemberDisplayName(resolvedMemberId);
        var persistError = PersistPartyState();
        if (persistError == Error.Ok)
        {
            useResult["message"] = string.Format(
                "已让 {0} 使用 {1}，学会 {2}。",
                memberName,
                itemName,
                skillName
            );
        }
        else
        {
            var rollbackMessage = string.Format(
                "已让 {0} 使用 {1}，学会 {2}，但队伍状态持久化失败，操作已回滚。",
                memberName,
                itemName,
                skillName
            );
            RollbackWarehouseTransaction(snapshot);
            useResult["success"] = false;
            useResult["message"] = rollbackMessage;
        }
        UpdateStatus(DictionaryString(useResult, "message", ""));
        return useResult;
    }

    public void OnPartyWarehouseWindowClosed()
    {
        if (!HasRuntime())
            return;

        SetActiveModalId("");
        SetActiveWarehouseEntryLabel("");
        UpdateStatus("已关闭共享仓库。");
        PresentPendingRewardIfReady();
    }

    private StringName ResolveWarehouseTargetMemberId(StringName preferredMemberId = default)
    {
        var partyState = GetPartyState();
        if (!HasRuntime() || partyState == null)
            return "";
        var normalizedMemberId = ProgressionDataUtils.to_string_name(preferredMemberId);
        if (
            normalizedMemberId != ""
            && partyState.get_member_state(normalizedMemberId) != null
        )
            return normalizedMemberId;
        var selectedMemberId = GetPartySelectedMemberId();
        if (
            selectedMemberId != ""
            && partyState.get_member_state(selectedMemberId) != null
        )
            return selectedMemberId;
        var leaderMemberId = partyState.leader_member_id;
        if (
            leaderMemberId != ""
            && partyState.get_member_state(leaderMemberId) != null
        )
            return leaderMemberId;
        var activeMemberIds = partyState.active_member_ids;
        foreach (var memberId in activeMemberIds)
        {
            if (
                partyState.get_member_state(memberId) != null
            )
                return memberId;
        }
        var reserveMemberIds = partyState.reserve_member_ids;
        foreach (var memberId in reserveMemberIds)
        {
            if (
                partyState.get_member_state(memberId) != null
            )
                return memberId;
        }
        return "";
    }

    private Godot.Collections.Array BuildWarehouseTargetMemberEntries()
    {
        var entries = new Godot.Collections.Array();
        var seenMemberIds = new Dictionary();
        var partyState = GetPartyState();
        if (!HasRuntime() || partyState == null)
            return entries;

        var activeMemberIds = partyState.active_member_ids;
        foreach (var memberId in activeMemberIds)
        {
            var id = memberId;
            if (id == "" || seenMemberIds.ContainsKey(id))
                continue;
            if (partyState.get_member_state(id) == null)
                continue;
            seenMemberIds[id] = true;
            entries.Add(
                new Dictionary
                {
                    ["member_id"] = id.ToString(),
                    ["display_name"] = GetMemberDisplayName(id),
                    ["roster_role"] = "active",
                }
            );
        }

        var reserveMemberIds = partyState.reserve_member_ids;
        foreach (var memberId in reserveMemberIds)
        {
            var id = memberId;
            if (id == "" || seenMemberIds.ContainsKey(id))
                continue;
            if (partyState.get_member_state(id) == null)
                continue;
            seenMemberIds[id] = true;
            entries.Add(
                new Dictionary
                {
                    ["member_id"] = id.ToString(),
                    ["display_name"] = GetMemberDisplayName(id),
                    ["roster_role"] = "reserve",
                }
            );
        }
        return entries;
    }

    private string BuildWarehouseUseFailureMessage(Dictionary useResult)
    {
        var itemId = DictionaryStringName(useResult, "item_id", "");
        var memberId = DictionaryStringName(useResult, "member_id", "");
        var reason = DictionaryStringName(useResult, "reason", "");
        var itemName = GetItemDisplayName(itemId);
        var memberName = GetMemberDisplayName(memberId);
        if (reason == "missing_item_def")
            return string.Format("{0} 的物品定义缺失，当前无法使用。", itemName);
        if (reason == "item_not_usable")
            return string.Format("{0} 当前不是可使用的技能书。", itemName);
        if (reason == "missing_member")
            return string.Format("当前找不到可使用 {0} 的目标角色。", itemName);
        if (reason == "missing_inventory")
            return string.Format("{0} 当前没有可使用的库存。", itemName);
        if (reason == "missing_skill_def")
            return string.Format("{0} 对应的技能定义缺失，当前无法使用。", itemName);
        if (reason == "learn_failed")
            return string.Format(
                "{0} 当前无法让 {1} 学会，可能已学会或未满足前置条件。",
                itemName,
                memberName
            );
        if (reason == "practice_replacement_confirmation_required")
        {
            var preview = DictionaryDictionary(
                useResult,
                "practice_replacement_preview",
                new Dictionary()
            );
            var oldSkillId = DictionaryStringName(preview, "existing_skill_id", "");
            var newSkillId = DictionaryStringName(useResult, "skill_id", "");
            if (newSkillId != "")
                return string.Format(
                    "{0} 会替换 {1} 当前的同系练功技能 {2}，新技能预计为 {3} 级；确认后才会消耗技能书。",
                    itemName,
                    memberName,
                    GetSkillDisplayName(oldSkillId),
                    DictionaryInt(preview, "predicted_level", 0)
                );
            return string.Format("{0} 需要确认练功技能替换后才能使用。", itemName);
        }
        if (reason == "consume_failed")
            return string.Format("{0} 已触发学习，但库存扣减失败。", itemName);
        if (reason == "service_unavailable")
            return "当前技能书服务尚未准备完成。";
        return string.Format("{0} 当前无法使用。", itemName);
    }

    private Dictionary BuildWarehouseWindowData()
    {
        var totalCapacity = 0;
        var usedSlots = 0;
        var freeSlots = 0;
        var isOverCapacity = false;
        var inventoryEntries = new Godot.Collections.Array();
        var targetMembers = BuildWarehouseTargetMemberEntries();

        var partyWarehouseService = GetPartyWarehouseService();
        if (partyWarehouseService != null)
        {
            totalCapacity = partyWarehouseService.get_total_capacity();
            usedSlots = partyWarehouseService.get_used_slots();
            freeSlots = partyWarehouseService.get_free_slots();
            isOverCapacity = partyWarehouseService.is_over_capacity();

            foreach (var entryData in partyWarehouseService.get_inventory_entries())
            {
                var grantedSkillId = DictionaryStringName(entryData, "granted_skill_id", "");
                entryData["granted_skill_name"] = GetSkillDisplayName(grantedSkillId);
                inventoryEntries.Add(entryData);
            }
        }

        var summaryText = string.Format(
            "容量 {0} 格  |  已用 {1} 格  |  空余 {2} 格",
            totalCapacity,
            usedSlots,
            freeSlots
        );
        var statusText =
            "当前版本支持查看、丢弃和让指定角色使用技能书。非装备物品会优先补满同类堆栈，装备则按实例独立占格。";
        if (isOverCapacity)
            statusText = string.Format(
                "仓库当前超容 {0} 格。已存物品不会被删除，但此时不能继续新增条目，只能整理和移除。",
                usedSlots - totalCapacity
            );

        return new Dictionary
        {
            ["title"] = "共享仓库",
            ["meta"] = string.Format(
                "入口：{0}  |  规则：全队共享、按堆栈/实例占格、不计重量。",
                GetActiveWarehouseMetaLabel()
            ),
            ["summary_text"] = summaryText,
            ["status_text"] = statusText,
            ["target_members"] = targetMembers,
            ["default_target_member_id"] = ResolveWarehouseTargetMemberId().ToString(),
            ["entries"] = inventoryEntries,
        };
    }

    private Dictionary RemoveWarehouseItemOrInstance(
        PartyWarehouseService partyWarehouseService,
        StringName itemId,
        int quantity,
        StringName instanceId = default
    )
    {
        var normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        ItemDef itemDef = partyWarehouseService?.get_item_def(itemId);
        if (itemDef != null && itemDef.is_equipment())
            return partyWarehouseService.remove_equipment_instance(itemId, normalizedInstanceId);
        return partyWarehouseService.remove_item(itemId, quantity);
    }

    private string BuildDiscardFailureMessage(StringName itemId, Dictionary result)
    {
        var itemName = GetItemDisplayName(itemId);
        var errorCode = DictionaryString(result, "error_code", "");
        switch (errorCode)
        {
            case "equipment_instance_id_required":
                return string.Format("请选择要丢弃的 {0} 装备实例。", itemName);
            case "warehouse_missing_instance":
                return string.Format("共享仓库中没有指定的 {0} 装备实例。", itemName);
            case "equipment_instance_item_mismatch":
                return string.Format("指定装备实例不属于 {0}。", itemName);
            case "item_not_equipment":
                return string.Format("{0} 不是装备实例，无法按实例丢弃。", itemName);
            case "item_not_found":
                return string.Format("未找到物品定义 {0}。", itemId);
            default:
                return string.Format("{0} 当前没有可丢弃的库存。", itemName);
        }
    }

    private string GetItemDisplayName(StringName itemId)
    {
        if (!HasRuntime())
            return itemId.ToString();
        return _runtime.get_item_display_name(itemId);
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

    private string GetMemberDisplayName(StringName memberId)
    {
        if (!HasRuntime())
            return memberId.ToString();
        return _runtime.get_member_display_name(memberId);
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

    private PartyState GetPartyState()
    {
        if (!HasRuntime())
            return null;
        return _runtime.get_party_state();
    }

    private PartyWarehouseService GetPartyWarehouseService()
    {
        if (!HasRuntime())
            return null;
        return _runtime.get_party_warehouse_service();
    }

    private PartyItemUseService GetPartyItemUseService()
    {
        if (!HasRuntime())
            return null;
        return _runtime.get_party_item_use_service();
    }

    private GameSession GetGameSession()
    {
        if (!HasRuntime())
            return null;
        return _runtime.get_game_session();
    }

    private bool IsBattleActive()
    {
        if (!HasRuntime())
            return false;
        return _runtime.is_battle_active();
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

    private void SetActiveWarehouseEntryLabel(string entryLabel)
    {
        if (HasRuntime())
            _runtime.set_active_warehouse_entry_label(entryLabel);
    }

    private string GetActiveWarehouseMetaLabel()
    {
        if (!HasRuntime())
            return "";
        return _runtime.get_active_warehouse_entry_label();
    }

    private Error PersistPartyState()
    {
        if (!HasRuntime())
            return Error.Unavailable;
        return (Error)_runtime.persist_party_state();
    }

    private WarehouseTransactionSnapshot CaptureWarehouseTransactionSnapshot()
    {
        var snapshot = new WarehouseTransactionSnapshot
        {
            SelectedMemberId = GetPartySelectedMemberId(),
        };

        var gameSession = GetGameSession();
        if (gameSession != null)
        {
            var runtimeState = gameSession._capture_runtime_state();
            if (runtimeState != null)
                snapshot.RuntimeState = runtimeState.Duplicate(true);
        }

        var partyState = GetPartyState();
        if (partyState != null)
            snapshot.PartyState = partyState.duplicate_state();

        if (gameSession != null)
        {
            var worldData = gameSession.get_world_data();
            if (worldData != null)
                snapshot.WorldData = worldData.Duplicate(true);
        }

        return snapshot;
    }

    private bool RollbackWarehouseTransaction(WarehouseTransactionSnapshot snapshot)
    {
        if (snapshot == null || snapshot.PartyState == null)
            return false;

        var restoredPartyState = snapshot.PartyState.duplicate_state();
        if (restoredPartyState == null)
            return false;

        var restoredWorldData =
            snapshot.WorldData.Count > 0 ? snapshot.WorldData.Duplicate(true) : new Dictionary();
        var gameSession = GetGameSession();

        if (gameSession != null && snapshot.RuntimeState.Count > 0)
        {
            var restoredRuntimeState = snapshot.RuntimeState.Duplicate(true);
            restoredRuntimeState["party_state"] = restoredPartyState;
            if (restoredWorldData.Count > 0)
                restoredRuntimeState["world_data"] = restoredWorldData;
            gameSession._restore_runtime_state(restoredRuntimeState);
        }
        else if (gameSession != null)
        {
            gameSession.set_party_state(restoredPartyState);
            if (restoredWorldData.Count > 0)
                gameSession.set_world_data(restoredWorldData);
            gameSession.discard_pending_save();
        }

        if (HasRuntime())
        {
            _runtime.set_party_state(restoredPartyState);
            SetPartySelectedMemberId(snapshot.SelectedMemberId);
            RestoreWorldDataContext(restoredWorldData);
        }

        return true;
    }

    private void RestoreWorldDataContext(Dictionary restoredWorldData)
    {
        if (!HasRuntime() || restoredWorldData == null || restoredWorldData.Count == 0)
            return;

        var dataContext = _runtime._world_map_data_context;
        if (dataContext == null)
            return;

        dataContext.bind_root_world_data(restoredWorldData);
        dataContext.SyncActiveWorldContext(
            _runtime.get_generation_config(),
            _runtime.get_grid_system(),
            _runtime.get_player_coord(),
            _runtime.get_selected_coord()
        );
    }

    private void SetPartySelectedMemberId(StringName memberId)
    {
        if (HasRuntime())
            _runtime.set_party_selected_member_id(memberId);
    }

    private StringName GetPartySelectedMemberId()
    {
        if (!HasRuntime())
            return "";
        return _runtime.get_party_selected_member_id();
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

    private static bool DictionaryBool(Dictionary dictionary, string key, bool fallback)
    {
        if (!TryRead(dictionary, key, out Variant value) || value.VariantType != Variant.Type.Bool)
            return fallback;
        return value.AsBool();
    }

    private static int DictionaryInt(Dictionary dictionary, string key, int fallback)
    {
        if (!TryRead(dictionary, key, out Variant value) || value.VariantType != Variant.Type.Int)
            return fallback;
        return value.AsInt32();
    }

    private static string DictionaryString(Dictionary dictionary, string key, string fallback)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => fallback,
        };
    }

    private static bool TryRead(Dictionary dictionary, string key, out Variant value)
    {
        value = default;
        if (dictionary == null || !dictionary.ContainsKey(key))
            return false;
        value = dictionary[key];
        return value.VariantType != Variant.Type.Nil;
    }

    private static StringName DictionaryStringName(
        Dictionary dictionary,
        string key,
        StringName fallback
    )
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return ProgressionDataUtils.to_string_name(dictionary[key]);
    }

    private static Dictionary DictionaryDictionary(
        Dictionary dictionary,
        string key,
        Dictionary fallback
    )
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsGodotDictionary();
    }

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out GameRuntimeFacade target)
            || !GodotObject.IsInstanceValid(target)
        )
            return null;
        return target;
    }
}
