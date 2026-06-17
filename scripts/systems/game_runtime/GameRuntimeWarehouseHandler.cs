using System;
using Godot;
using Godot.Collections;

public sealed class GameRuntimeWarehouseHandler
{
    private static readonly string RuntimeUnavailableMessage = "运行时尚未初始化。";

    private sealed class WarehouseTransactionSnapshot
    {
        internal Dictionary RuntimeState { get; set; } = new();
        public PartyState PartyState { get; set; }
        internal Dictionary WorldData { get; set; } = new();
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

    public void Dispose()
    {
        _runtime = null;
    }

    internal Dictionary GetWarehouseWindowData()
    {
        if (!HasRuntime())
            return new Dictionary();
        if (GetPartyState() == null || GetPartyWarehouseService() == null)
            return new Dictionary();
        return BuildWarehouseWindowData();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandOpenPartyWarehouseTyped()
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (GetPartyState() == null)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (IsBattleActive())
            return CommandErrorTyped("当前处于战斗中，不能打开共享仓库。");

        if (GetActiveModalKind() == RuntimeModalKind.Settlement)
        {
            OpenPartyWarehouseWindow("据点服务");
            UpdateStatus("已从据点窗口打开共享仓库。");
        }
        else
        {
            OpenPartyWarehouseWindow("队伍管理");
            UpdateStatus("已打开共享仓库。");
        }
        return CommandOkTyped();
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandDiscardOneTyped(
        StringName itemId,
        StringName instanceId = default
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (GetActiveModalKind() != RuntimeModalKind.Warehouse)
            return CommandErrorTyped("共享仓库当前未打开。");
        if (GetPartyWarehouseService() == null)
            return CommandErrorTyped("共享仓库服务尚未准备完成。");

        var partyWarehouseService = GetPartyWarehouseService();
        var itemName = GetItemDisplayName(itemId);
        var snapshot = CaptureWarehouseTransactionSnapshot();
        var result = RemoveWarehouseItemOrInstance(partyWarehouseService, itemId, 1, instanceId);
        if (result.RemovedQuantity <= 0)
        {
            var failureMessage = BuildDiscardFailureMessage(itemId, result);
            UpdateStatus(failureMessage);
            return CommandErrorTyped(failureMessage);
        }

        var successMessage = string.Format("已从共享仓库丢弃 1 件 {0}。", itemName);
        var persistError = PersistPartyState();
        if (persistError == Error.Ok)
        {
            UpdateStatus(successMessage);
            return CommandOkTyped(successMessage);
        }

        var rollbackMessage = string.Format(
            "已从共享仓库丢弃 1 件 {0}，但队伍状态持久化失败，操作已回滚。",
            itemName
        );
        RollbackWarehouseTransaction(snapshot);
        UpdateStatus(rollbackMessage);
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            rollbackMessage,
            GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
        );
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandDiscardAllTyped(
        StringName itemId,
        StringName instanceId = default
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (GetActiveModalKind() != RuntimeModalKind.Warehouse)
            return CommandErrorTyped("共享仓库当前未打开。");
        if (GetPartyWarehouseService() == null)
            return CommandErrorTyped("共享仓库服务尚未准备完成。");

        var partyWarehouseService = GetPartyWarehouseService();
        var itemName = GetItemDisplayName(itemId);
        var normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        ItemDef itemDef = partyWarehouseService.GetItemDef(itemId);
        bool isEquipment = itemDef != null && itemDef.IsEquipment();
        if (isEquipment && normalizedInstanceId == "")
        {
            var missingInstanceMessage = string.Format(
                "请选择要丢弃的 {0} 装备实例；装备不会按物品 ID 批量丢弃。",
                itemName
            );
            UpdateStatus(missingInstanceMessage);
            return CommandErrorTyped(missingInstanceMessage);
        }

        var totalQuantity = partyWarehouseService.CountItem(itemId);
        if (totalQuantity <= 0)
        {
            var noStockMessage = string.Format("{0} 当前没有可丢弃的库存。", itemName);
            UpdateStatus(noStockMessage);
            return CommandErrorTyped(noStockMessage);
        }

        var snapshot = CaptureWarehouseTransactionSnapshot();
        var result = RemoveWarehouseItemOrInstance(
            partyWarehouseService,
            itemId,
            totalQuantity,
            instanceId
        );
        var removedQuantity = result.RemovedQuantity;
        if (removedQuantity <= 0)
        {
            var failureMessage = BuildDiscardFailureMessage(itemId, result);
            UpdateStatus(failureMessage);
            return CommandErrorTyped(failureMessage);
        }

        var successMessage = BuildDiscardAllSuccessMessage(itemName, result, removedQuantity);
        var persistError = PersistPartyState();
        if (persistError == Error.Ok)
        {
            UpdateStatus(successMessage);
            return CommandOkTyped(successMessage);
        }

        var rollbackMessage = BuildDiscardAllRollbackMessage(itemName, result);
        RollbackWarehouseTransaction(snapshot);
        UpdateStatus(rollbackMessage);
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            rollbackMessage,
            GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
        );
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandUseItemTyped(
        StringName itemId,
        StringName memberId = default,
        PartyItemUseService.PartyItemUseOptions options = null
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (GetActiveModalKind() != RuntimeModalKind.Warehouse)
            return CommandErrorTyped("共享仓库当前未打开。");
        var resolvedMemberId = ResolveWarehouseTargetMemberId(memberId);
        if (resolvedMemberId == "")
            return CommandErrorTyped("当前没有可使用技能书的目标角色。");

        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var partyItemUseService = GetPartyItemUseService();
        if (partyItemUseService == null)
        {
            string unavailableMessage = "当前技能书服务尚未准备完成。";
            UpdateStatus(unavailableMessage);
            return CommandErrorTyped(unavailableMessage);
        }

        var snapshot = CaptureWarehouseTransactionSnapshot();
        var useResult = partyItemUseService.UseItemTyped(
            normalizedItemId,
            resolvedMemberId,
            options
        );
        if (!useResult.Success)
        {
            var failureMessage = BuildWarehouseUseFailureMessage(useResult);
            UpdateStatus(failureMessage);
            return CommandErrorTyped(failureMessage);
        }

        SetPartySelectedMemberId(resolvedMemberId);
        var itemName = GetItemDisplayName(normalizedItemId);
        var skillName = GetSkillDisplayName(useResult.SkillId);
        var memberName = GetMemberDisplayName(resolvedMemberId);
        var successMessage = string.Format(
            "已让 {0} 使用 {1}，学会 {2}。",
            memberName,
            itemName,
            skillName
        );
        var persistError = PersistPartyState();
        if (persistError == Error.Ok)
        {
            UpdateStatus(successMessage);
            return CommandOkTyped(successMessage);
        }

        var rollbackMessage = string.Format(
            "已让 {0} 使用 {1}，学会 {2}，但队伍状态持久化失败，操作已回滚。",
            memberName,
            itemName,
            skillName
        );
        RollbackWarehouseTransaction(snapshot);
        UpdateStatus(rollbackMessage);
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            rollbackMessage,
            GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
        );
    }

    internal GameRuntimeFacade.RuntimeCommandResult CommandAddItemTyped(
        StringName itemId,
        int quantity
    )
    {
        if (!HasRuntime())
            return RuntimeUnavailableTypedResult();
        if (GetPartyState() == null)
            return CommandErrorTyped("当前不存在队伍数据。");
        if (IsBattleActive())
            return CommandErrorTyped("当前处于战斗中，不能直接改动共享仓库。");
        if (quantity <= 0)
            return GameRuntimeFacade.RuntimeCommandResult.Failure(
                "加入数量必须大于 0。",
                GameRuntimeFacade.RuntimeCommandCode.InvalidArgument
            );
        if (GetPartyWarehouseService() == null)
            return CommandErrorTyped("共享仓库服务尚未准备完成。");

        var snapshot = CaptureWarehouseTransactionSnapshot();
        var normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        var result = GetPartyWarehouseService().AddItemTyped(normalizedItemId, quantity);
        var addedQuantity = result.AddedQuantity;
        if (addedQuantity <= 0)
            return CommandErrorTyped(
                string.Format("{0} 当前无法加入共享仓库。", GetItemDisplayName(normalizedItemId))
            );

        var successMessage = string.Format(
            "已向共享仓库加入 {0} 件 {1}。",
            addedQuantity,
            GetItemDisplayName(normalizedItemId)
        );
        var remainingQuantity = result.RemainingQuantity;
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
            return CommandOkTyped(successMessage);
        }
        var rollbackMessage = string.Format(
            "{0} 但队伍状态持久化失败，操作已回滚。",
            successMessage
        );
        RollbackWarehouseTransaction(snapshot);
        UpdateStatus(rollbackMessage);
        return GameRuntimeFacade.RuntimeCommandResult.Failure(
            rollbackMessage,
            GameRuntimeFacade.RuntimeCommandCode.PersistenceFailure
        );
    }

    public void OpenPartyWarehouseWindow(string entryLabel)
    {
        if (!HasRuntime())
            return;
        if (IsBattleActive())
            return;

        SetActiveModalKind(RuntimeModalKind.Warehouse);
        SetActiveWarehouseEntryLabel(string.IsNullOrEmpty(entryLabel) ? "共享入口" : entryLabel);

        var partyWarehouseService = GetPartyWarehouseService();
        var gameSession = GetGameSession();
        if (partyWarehouseService != null)
        {
            var itemDefs =
                gameSession != null
                    ? gameSession.GetItemDefsTyped()
                    : new System.Collections.Generic.Dictionary<StringName, ItemDef>();
            Func<StringName> equipmentInstanceIdAllocator =
                gameSession != null ? gameSession.AllocateEquipmentInstanceId : null;
            partyWarehouseService.Setup(GetPartyState(), itemDefs, equipmentInstanceIdAllocator);
        }
    }

    public void OnPartyWarehouseWindowClosed()
    {
        if (!HasRuntime())
            return;

        SetActiveModalKind(RuntimeModalKind.None);
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
            && partyState.GetMemberState(normalizedMemberId) != null
        )
            return normalizedMemberId;
        var selectedMemberId = GetPartySelectedMemberId();
        if (
            selectedMemberId != ""
            && partyState.GetMemberState(selectedMemberId) != null
        )
            return selectedMemberId;
        var leaderMemberId = partyState.leader_member_id;
        if (
            leaderMemberId != ""
            && partyState.GetMemberState(leaderMemberId) != null
        )
            return leaderMemberId;
        var activeMemberIds = partyState.active_member_ids;
        foreach (var memberId in activeMemberIds)
        {
            if (
                partyState.GetMemberState(memberId) != null
            )
                return memberId;
        }
        var reserveMemberIds = partyState.reserve_member_ids;
        foreach (var memberId in reserveMemberIds)
        {
            if (
                partyState.GetMemberState(memberId) != null
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
            if (partyState.GetMemberState(id) == null)
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
            if (partyState.GetMemberState(id) == null)
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

    private string BuildWarehouseUseFailureMessage(PartyItemUseService.PartyItemUseResult useResult)
    {
        if (useResult == null)
            return "当前技能书服务尚未准备完成。";
        var itemId = useResult.ItemId;
        var memberId = useResult.MemberId;
        var reason = useResult.Reason;
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
            var preview = useResult.PracticeReplacementStatus
                ?? PracticeSkillLearnStatus.NonPractice();
            if (useResult.SkillId != "")
                return string.Format(
                    "{0} 会替换 {1} 当前的同系练功技能 {2}，新技能预计为 {3} 级；确认后才会消耗技能书。",
                    itemName,
                    memberName,
                    GetSkillDisplayName(preview.ExistingSkillId),
                    preview.PredictedLevel
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
            totalCapacity = partyWarehouseService.GetTotalCapacity();
            usedSlots = partyWarehouseService.GetUsedSlots();
            freeSlots = partyWarehouseService.GetFreeSlots();
            isOverCapacity = partyWarehouseService.IsOverCapacity();

            foreach (var inventoryEntryData in partyWarehouseService.GetInventoryEntriesTyped())
            {
                var entryData = inventoryEntryData.ToDictionary();
                entryData["granted_skill_name"] = GetSkillDisplayName(
                    inventoryEntryData.GrantedSkillId
                );
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

    private PartyWarehouseService.WarehouseRemoveItemResult RemoveWarehouseItemOrInstance(
        PartyWarehouseService partyWarehouseService,
        StringName itemId,
        int quantity,
        StringName instanceId = default
    )
    {
        var normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        ItemDef itemDef = partyWarehouseService?.GetItemDef(itemId);
        if (itemDef != null && itemDef.IsEquipment())
            return partyWarehouseService.RemoveEquipmentInstanceTyped(itemId, normalizedInstanceId);
        return partyWarehouseService.RemoveItemTyped(itemId, quantity);
    }

    private static string BuildDiscardAllSuccessMessage(
        string itemName,
        PartyWarehouseService.WarehouseRemoveItemResult result,
        int removedQuantity
    )
    {
        if (result != null && result.IncludeInstanceId)
            return string.Format(
                "已从共享仓库丢弃 {0} 装备实例 {1}。",
                itemName,
                result.InstanceId
            );
        return string.Format(
            "已从共享仓库丢弃全部 {0}，共 {1} 件。",
            itemName,
            removedQuantity
        );
    }

    private static string BuildDiscardAllRollbackMessage(
        string itemName,
        PartyWarehouseService.WarehouseRemoveItemResult result
    )
    {
        if (result != null && result.IncludeInstanceId)
            return string.Format(
                "已从共享仓库丢弃 {0} 装备实例 {1}，但队伍状态持久化失败，操作已回滚。",
                itemName,
                result.InstanceId
            );
        return string.Format(
            "已从共享仓库丢弃全部 {0}，但队伍状态持久化失败，操作已回滚。",
            itemName
        );
    }

    private string BuildDiscardFailureMessage(
        StringName itemId,
        PartyWarehouseService.WarehouseRemoveItemResult result
    )
    {
        var itemName = GetItemDisplayName(itemId);
        var errorCode = result?.ErrorCode ?? "";
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
        return _runtime.GetItemDisplayName(itemId);
    }

    private string GetSkillDisplayName(StringName skillId)
    {
        var gameSession = GetGameSession();
        if (gameSession == null)
            return skillId.ToString();
        var skillDefs = gameSession.GetSkillDefsTyped();
        if (
            skillDefs != null
            && skillDefs.TryGetValue(skillId, out SkillDef skillDef)
            && skillDef != null
            && !string.IsNullOrEmpty(skillDef.display_name)
        )
            return skillDef.display_name;
        return skillId.ToString();
    }

    private string GetMemberDisplayName(StringName memberId)
    {
        if (!HasRuntime())
            return memberId.ToString();
        return _runtime.GetMemberDisplayName(memberId);
    }

    private bool HasRuntime()
    {
        return _runtime != null;
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

    private PartyState GetPartyState()
    {
        if (!HasRuntime())
            return null;
        return _runtime.GetPartyState();
    }

    private PartyWarehouseService GetPartyWarehouseService()
    {
        if (!HasRuntime())
            return null;
        return _runtime.GetPartyWarehouseService();
    }

    private PartyItemUseService GetPartyItemUseService()
    {
        if (!HasRuntime())
            return null;
        return _runtime.GetPartyItemUseService();
    }

    private GameSession GetGameSession()
    {
        if (!HasRuntime())
            return null;
        return _runtime.GetGameSession();
    }

    private bool IsBattleActive()
    {
        if (!HasRuntime())
            return false;
        return _runtime.IsBattleActive();
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

    private void SetActiveWarehouseEntryLabel(string entryLabel)
    {
        if (HasRuntime())
            _runtime.SetActiveWarehouseEntryLabel(entryLabel);
    }

    private string GetActiveWarehouseMetaLabel()
    {
        if (!HasRuntime())
            return "";
        return _runtime.GetActiveWarehouseEntryLabel();
    }

    private Error PersistPartyState()
    {
        if (!HasRuntime())
            return Error.Unavailable;
        return (Error)_runtime.PersistPartyState();
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
            var runtimeState = gameSession.CaptureRuntimeState();
            if (runtimeState != null)
                snapshot.RuntimeState = runtimeState.Duplicate(true);
        }

        var partyState = GetPartyState();
        if (partyState != null)
            snapshot.PartyState = partyState.DuplicateState();

        if (gameSession != null)
        {
            var worldData = gameSession.GetWorldData();
            if (worldData != null)
                snapshot.WorldData = worldData.Duplicate(true);
        }

        return snapshot;
    }

    private bool RollbackWarehouseTransaction(WarehouseTransactionSnapshot snapshot)
    {
        if (snapshot == null || snapshot.PartyState == null)
            return false;

        var restoredPartyState = snapshot.PartyState.DuplicateState();
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
            gameSession.RestoreRuntimeState(restoredRuntimeState);
        }
        else if (gameSession != null)
        {
            gameSession.SetPartyState(restoredPartyState);
            if (restoredWorldData.Count > 0)
                gameSession.SetWorldData(restoredWorldData);
            gameSession.DiscardPendingSave();
        }

        if (HasRuntime())
        {
            _runtime.SetPartyState(restoredPartyState);
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

        dataContext.BindRootWorldData(restoredWorldData);
        dataContext.SyncActiveWorldContext(
            _runtime.GetGenerationConfig(),
            _runtime.GetGridSystem(),
            _runtime.GetPlayerCoord(),
            _runtime.GetSelectedCoord()
        );
    }

    private void SetPartySelectedMemberId(StringName memberId)
    {
        if (HasRuntime())
            _runtime.SetPartySelectedMemberId(memberId);
    }

    private StringName GetPartySelectedMemberId()
    {
        if (!HasRuntime())
            return "";
        return _runtime.GetPartySelectedMemberId();
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
