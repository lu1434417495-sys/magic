using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GameRuntimeWarehouseHandler : RefCounted
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";

    private sealed class WarehouseTransactionSnapshot
    {
        public Dictionary RuntimeState { get; set; } = new();
        public Dictionary PartyStatePayload { get; set; } = new();
        public Dictionary WorldData { get; set; } = new();
        public StringName SelectedMemberId { get; set; } = "";
    }

    private WeakReference<GodotObject> _runtimeRef;

    private GodotObject _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void Setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public void setup(GodotObject runtime)
    {
        Setup(runtime);
    }

    public new void Dispose()
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
        var result = GetPartyWarehouseService()
            .Call("add_item", normalizedItemId, quantity)
            .AsGodotDictionary();
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
        var totalQuantity = partyWarehouseService.Call("count_item", itemId).AsInt32();
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
        var useResult = partyItemUseService
            .Call("use_item", itemId, resolvedMemberId, options)
            .AsGodotDictionary();
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
            && partyState.Call("get_member_state", normalizedMemberId).AsGodotObject() != null
        )
            return normalizedMemberId;
        var selectedMemberId = GetPartySelectedMemberId();
        if (
            selectedMemberId != ""
            && partyState.Call("get_member_state", selectedMemberId).AsGodotObject() != null
        )
            return selectedMemberId;
        var leaderMemberId = partyState.Get("leader_member_id").AsStringName();
        if (
            leaderMemberId != ""
            && partyState.Call("get_member_state", leaderMemberId).AsGodotObject() != null
        )
            return leaderMemberId;
        var activeMemberIds = partyState.Get("active_member_ids").AsGodotArray();
        foreach (var memberId in activeMemberIds)
        {
            if (
                partyState.Call("get_member_state", memberId.AsStringName()).AsGodotObject() != null
            )
                return memberId.AsStringName();
        }
        var reserveMemberIds = partyState.Get("reserve_member_ids").AsGodotArray();
        foreach (var memberId in reserveMemberIds)
        {
            if (
                partyState.Call("get_member_state", memberId.AsStringName()).AsGodotObject() != null
            )
                return memberId.AsStringName();
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

        var activeMemberIds = partyState.Get("active_member_ids").AsGodotArray();
        foreach (var memberId in activeMemberIds)
        {
            var id = memberId.AsStringName();
            if (id == "" || seenMemberIds.ContainsKey(id))
                continue;
            if (partyState.Call("get_member_state", id).AsGodotObject() == null)
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

        var reserveMemberIds = partyState.Get("reserve_member_ids").AsGodotArray();
        foreach (var memberId in reserveMemberIds)
        {
            var id = memberId.AsStringName();
            if (id == "" || seenMemberIds.ContainsKey(id))
                continue;
            if (partyState.Call("get_member_state", id).AsGodotObject() == null)
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
            totalCapacity = partyWarehouseService.Call("get_total_capacity").AsInt32();
            usedSlots = partyWarehouseService.Call("get_used_slots").AsInt32();
            freeSlots = partyWarehouseService.Call("get_free_slots").AsInt32();
            isOverCapacity = partyWarehouseService.Call("is_over_capacity").AsBool();

            foreach (
                var entryValue in partyWarehouseService
                    .Call("get_inventory_entries")
                    .AsGodotArray()
            )
            {
                if (entryValue.VariantType != Variant.Type.Dictionary)
                    continue;
                var entryData = entryValue.AsGodotDictionary().Duplicate(true);
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
        GodotObject partyWarehouseService,
        StringName itemId,
        int quantity,
        StringName instanceId = default
    )
    {
        var normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        GodotObject itemDef = null;
        if (partyWarehouseService != null && partyWarehouseService.HasMethod("get_item_def"))
            itemDef = partyWarehouseService.Call("get_item_def", itemId).AsGodotObject();
        if (itemDef != null && itemDef.Call("is_equipment").AsBool())
            return partyWarehouseService
                .Call("remove_equipment_instance", itemId, normalizedInstanceId)
                .AsGodotDictionary();
        return partyWarehouseService.Call("remove_item", itemId, quantity).AsGodotDictionary();
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
        return _runtime.Call("get_item_display_name", itemId).AsString();
    }

    private string GetSkillDisplayName(StringName skillId)
    {
        var gameSession = GetGameSession();
        if (gameSession == null)
            return skillId.ToString();
        var skillDefs = gameSession.Call("get_skill_defs").AsGodotDictionary();
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
        return _runtime.Call("get_member_display_name", memberId).AsString();
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
        return _runtime.Call("build_command_ok", message).AsGodotDictionary();
    }

    private Dictionary CommandError(string message)
    {
        if (!HasRuntime())
            return new Dictionary { ["ok"] = false, ["message"] = message };
        return _runtime.Call("build_command_error", message).AsGodotDictionary();
    }

    private Dictionary RuntimeUnavailableError()
    {
        return new Dictionary { ["ok"] = false, ["message"] = RuntimeUnavailableMessage };
    }

    private PartyState GetPartyState()
    {
        if (!HasRuntime())
            return null;
        return _runtime.Call("get_party_state").AsGodotObject() as PartyState;
    }

    private PartyWarehouseService GetPartyWarehouseService()
    {
        if (!HasRuntime())
            return null;
        return _runtime.Call("get_party_warehouse_service").AsGodotObject()
            as PartyWarehouseService;
    }

    private GodotObject GetPartyItemUseService()
    {
        if (!HasRuntime())
            return null;
        return _runtime.Call("get_party_item_use_service").AsGodotObject();
    }

    private GameSession GetGameSession()
    {
        if (!HasRuntime())
            return null;
        return _runtime.Call("get_game_session").AsGodotObject() as GameSession;
    }

    private bool IsBattleActive()
    {
        if (!HasRuntime())
            return false;
        return _runtime.Call("is_battle_active").AsBool();
    }

    private string GetActiveModalId()
    {
        if (!HasRuntime())
            return "";
        return _runtime.Call("get_active_modal_id").AsString();
    }

    private void SetActiveModalId(string modalId)
    {
        if (HasRuntime())
            _runtime.Call("set_runtime_active_modal_id", modalId);
    }

    private void SetActiveWarehouseEntryLabel(string entryLabel)
    {
        if (HasRuntime())
            _runtime.Call("set_active_warehouse_entry_label", entryLabel);
    }

    private string GetActiveWarehouseMetaLabel()
    {
        if (!HasRuntime())
            return "";
        return _runtime.Call("get_active_warehouse_entry_label").AsString();
    }

    private Error PersistPartyState()
    {
        if (!HasRuntime())
            return Error.Unavailable;
        return (Error)_runtime.Call("persist_party_state").AsInt32();
    }

    private WarehouseTransactionSnapshot CaptureWarehouseTransactionSnapshot()
    {
        var snapshot = new WarehouseTransactionSnapshot
        {
            SelectedMemberId = GetPartySelectedMemberId(),
        };

        var gameSession = GetGameSession();
        if (gameSession != null && gameSession.HasMethod("_capture_runtime_state"))
        {
            var runtimeState = gameSession.Call("_capture_runtime_state");
            if (runtimeState.VariantType == Variant.Type.Dictionary)
                snapshot.RuntimeState = runtimeState.AsGodotDictionary().Duplicate(true);
        }

        var partyState = GetPartyState();
        if (partyState != null && partyState.HasMethod("to_dict"))
        {
            var partyPayload = partyState.Call("to_dict");
            if (partyPayload.VariantType == Variant.Type.Dictionary)
                snapshot.PartyStatePayload = partyPayload.AsGodotDictionary().Duplicate(true);
        }

        if (gameSession != null && gameSession.HasMethod("get_world_data"))
        {
            var worldData = gameSession.Call("get_world_data");
            if (worldData.VariantType == Variant.Type.Dictionary)
                snapshot.WorldData = worldData.AsGodotDictionary().Duplicate(true);
        }

        return snapshot;
    }

    private bool RollbackWarehouseTransaction(WarehouseTransactionSnapshot snapshot)
    {
        if (snapshot == null || snapshot.PartyStatePayload.Count == 0)
            return false;

        var restoredPartyState = PartyState.from_dict(snapshot.PartyStatePayload);
        if (restoredPartyState == null)
            return false;

        var restoredWorldData =
            snapshot.WorldData.Count > 0 ? snapshot.WorldData.Duplicate(true) : new Dictionary();
        var gameSession = GetGameSession();

        if (
            gameSession != null
            && gameSession.HasMethod("_restore_runtime_state")
            && snapshot.RuntimeState.Count > 0
        )
        {
            var restoredRuntimeState = snapshot.RuntimeState.Duplicate(true);
            restoredRuntimeState["party_state"] = restoredPartyState;
            if (restoredWorldData.Count > 0)
                restoredRuntimeState["world_data"] = restoredWorldData;
            gameSession.Call("_restore_runtime_state", restoredRuntimeState);
        }
        else if (gameSession != null)
        {
            if (gameSession.HasMethod("set_party_state"))
                gameSession.Call("set_party_state", restoredPartyState);
            if (restoredWorldData.Count > 0 && gameSession.HasMethod("set_world_data"))
                gameSession.Call("set_world_data", restoredWorldData);
            if (gameSession.HasMethod("discard_pending_save"))
                gameSession.Call("discard_pending_save");
        }

        if (HasRuntime())
        {
            _runtime.Call("set_party_state", restoredPartyState);
            SetPartySelectedMemberId(snapshot.SelectedMemberId);
            RestoreWorldDataContext(restoredWorldData);
        }

        return true;
    }

    private void RestoreWorldDataContext(Dictionary restoredWorldData)
    {
        if (!HasRuntime() || restoredWorldData == null || restoredWorldData.Count == 0)
            return;

        GodotObject dataContext = null;
        var dataContextValue = _runtime.Get("_world_map_data_context");
        if (dataContextValue.VariantType == Variant.Type.Object)
            dataContext = dataContextValue.AsGodotObject();
        if (dataContext == null || !dataContext.HasMethod("bind_root_world_data"))
            return;

        dataContext.Call("bind_root_world_data", restoredWorldData);
        if (!dataContext.HasMethod("sync_active_world_context"))
            return;
        if (!_runtime.HasMethod("get_generation_config") || !_runtime.HasMethod("get_grid_system"))
            return;
        dataContext.Call(
            "sync_active_world_context",
            _runtime.Call("get_generation_config"),
            _runtime.Call("get_grid_system"),
            _runtime.Call("get_player_coord"),
            _runtime.Call("get_selected_coord")
        );
    }

    private void SetPartySelectedMemberId(StringName memberId)
    {
        if (HasRuntime())
            _runtime.Call("set_party_selected_member_id", memberId);
    }

    private StringName GetPartySelectedMemberId()
    {
        if (!HasRuntime())
            return "";
        return _runtime.Call("get_party_selected_member_id").AsStringName();
    }

    private bool PresentPendingRewardIfReady()
    {
        if (!HasRuntime())
            return false;
        return _runtime.Call("present_pending_reward_if_ready").AsBool();
    }

    private void UpdateStatus(string message)
    {
        if (HasRuntime())
            _runtime.Call("update_status", message);
    }

    private string GetStatusText()
    {
        if (!HasRuntime())
            return "";
        return _runtime.Call("get_status_text").AsString();
    }

    private static bool DictionaryBool(Dictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsBool();
    }

    private static int DictionaryInt(Dictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsInt32();
    }

    private static string DictionaryString(Dictionary dictionary, string key, string fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsString();
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

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out GodotObject target)
            || !GodotObject.IsInstanceValid(target)
        )
            return null;
        return target;
    }
}
