using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed partial class GameRuntimeFacade : IGameRuntimeWarehousePort
{
    private sealed class WarehouseTransactionSnapshot
    {
        public IReadOnlyDictionary<string, object> RuntimeStatePlain { get; init; } =
            new Dictionary<string, object>(StringComparer.Ordinal);
        public PartyState PartyState { get; init; }
        public IReadOnlyDictionary<string, object> WorldDataPlain { get; init; } =
            new Dictionary<string, object>(StringComparer.Ordinal);
        public StringName SelectedMemberId { get; init; }
    }

    WarehouseCommandContextSnapshot IGameRuntimeWarehousePort.CaptureWarehouseCommandContext() =>
        new(
            _party_state != null,
            _party_warehouse_service != null,
            IsBattleActive(),
            _active_modal_kind
        );

    WarehouseWindowSnapshot IGameRuntimeWarehousePort.CaptureWarehouseWindowSnapshot()
    {
        if (_party_state == null || _party_warehouse_service == null)
            return WarehouseWindowSnapshot.Empty;

        var targetMembers = new List<WarehouseTargetMemberSnapshot>();
        var seenMemberIds = new HashSet<StringName>();
        AppendWarehouseTargetMembers(
            targetMembers,
            seenMemberIds,
            _party_state.active_member_ids,
            "active"
        );
        AppendWarehouseTargetMembers(
            targetMembers,
            seenMemberIds,
            _party_state.reserve_member_ids,
            "reserve"
        );

        IReadOnlyDictionary<StringName, TraitDefinition> traitDefs =
            GetContentCatalogTyped()?.GetTraitDefsTyped()
            ?? new Dictionary<StringName, TraitDefinition>();
        var entries = new List<WarehouseInventoryEntrySnapshot>();
        foreach (
            WarehouseInventoryEntry entry in _party_warehouse_service.GetInventoryEntriesTyped()
        )
        {
            entries.Add(
                new WarehouseInventoryEntrySnapshot
                {
                    ItemId = entry.ItemId,
                    DisplayName = entry.DisplayName,
                    Description = ItemTraitDetailText.Compose(
                        entry.Description,
                        entry.ItemDefinition,
                        traitDefs
                    ),
                    Icon = entry.Icon,
                    Quantity = entry.Quantity,
                    TotalQuantity = entry.TotalQuantity,
                    IsStackable = entry.IsStackable,
                    StackLimit = entry.StackLimit,
                    ItemCategory = entry.ItemCategory,
                    IsSkillBook = entry.IsSkillBook,
                    GrantedSkillId = entry.GrantedSkillId,
                    GrantedSkillName = GetSkillDisplayName(entry.GrantedSkillId),
                    StorageMode = entry.StorageMode,
                    InstanceId = entry.InstanceId,
                    Rarity = entry.Rarity,
                    CurrentDurability = entry.CurrentDurability,
                    HasEquipmentInstance = entry.HasEquipmentInstance,
                }
            );
        }

        return new WarehouseWindowSnapshot(
            true,
            _party_warehouse_service.GetTotalCapacity(),
            _party_warehouse_service.GetUsedSlots(),
            _party_warehouse_service.GetFreeSlots(),
            _party_warehouse_service.IsOverCapacity(),
            _active_warehouse_entry_label,
            ResolveWarehouseTargetMemberIdInternal(),
            targetMembers,
            entries
        );
    }

    void IGameRuntimeWarehousePort.OpenWarehouse(string entryLabel)
    {
        if (_party_state == null || IsBattleActive())
            return;
        SetRuntimeActiveModalKind(RuntimeModalKind.Warehouse);
        _active_warehouse_entry_label = string.IsNullOrEmpty(entryLabel)
            ? "共享入口"
            : entryLabel;
        if (_party_warehouse_service == null)
            return;
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefs =
            _game_session?.GetItemDefsTyped()
            ?? new Dictionary<StringName, ItemDefinition>();
        Func<StringName> allocator =
            _game_session != null ? _game_session.AllocateEquipmentInstanceId : null;
        _party_warehouse_service.Setup(_party_state, itemDefs, allocator);
    }

    void IGameRuntimeWarehousePort.CloseWarehouseAndPresentPendingReward(string statusMessage)
    {
        SetRuntimeActiveModalKind(RuntimeModalKind.None);
        _active_warehouse_entry_label = "";
        UpdateStatus(statusMessage);
        PresentPendingRewardIfReady();
    }

    void IGameRuntimeWarehousePort.UpdateWarehouseStatus(string message) => UpdateStatus(message);

    WarehouseDiscardMutationResult IGameRuntimeWarehousePort.DiscardOneAndStage(
        StringName itemId,
        StringName instanceId
    )
    {
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        string itemName = GetItemDisplayName(normalizedItemId);
        WarehouseTransactionSnapshot snapshot = CaptureWarehouseTransactionSnapshot();
        PartyWarehouseService.WarehouseRemoveItemResult result =
            RemoveWarehouseItemOrInstanceInternal(
                _party_warehouse_service,
                normalizedItemId,
                1,
                instanceId
            );
        if (result == null || result.RemovedQuantity <= 0)
        {
            return new WarehouseDiscardMutationResult(
                false,
                ToWarehouseDiscardFailureKind(result?.ErrorCode),
                normalizedItemId,
                itemName,
                0
            );
        }
        if ((Error)StagePartyStateInternal() == Error.Ok)
        {
            return new WarehouseDiscardMutationResult(
                true,
                WarehouseDiscardFailureKind.None,
                normalizedItemId,
                itemName,
                result.RemovedQuantity
            );
        }
        RollbackWarehouseTransaction(snapshot);
        return new WarehouseDiscardMutationResult(
            false,
            WarehouseDiscardFailureKind.StageFailed,
            normalizedItemId,
            itemName,
            result.RemovedQuantity
        );
    }

    WarehouseDiscardMutationResult IGameRuntimeWarehousePort.DiscardAllAndStage(
        StringName itemId
    )
    {
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        string itemName = GetItemDisplayName(normalizedItemId);
        ItemDefinition itemDef = _party_warehouse_service?.GetItemDef(normalizedItemId);
        if (itemDef != null && itemDef.IsEquipment())
        {
            return new WarehouseDiscardMutationResult(
                false,
                WarehouseDiscardFailureKind.UnsupportedDiscardAllEquipment,
                normalizedItemId,
                itemName,
                0
            );
        }
        int totalQuantity = _party_warehouse_service?.CountItem(normalizedItemId) ?? 0;
        if (totalQuantity <= 0)
        {
            return new WarehouseDiscardMutationResult(
                false,
                WarehouseDiscardFailureKind.MissingStock,
                normalizedItemId,
                itemName,
                0
            );
        }

        WarehouseTransactionSnapshot snapshot = CaptureWarehouseTransactionSnapshot();
        PartyWarehouseService.WarehouseRemoveItemResult result =
            _party_warehouse_service.RemoveItemTyped(normalizedItemId, totalQuantity);
        if (result == null || result.RemovedQuantity <= 0)
        {
            return new WarehouseDiscardMutationResult(
                false,
                ToWarehouseDiscardFailureKind(result?.ErrorCode),
                normalizedItemId,
                itemName,
                0
            );
        }
        if ((Error)StagePartyStateInternal() == Error.Ok)
        {
            return new WarehouseDiscardMutationResult(
                true,
                WarehouseDiscardFailureKind.None,
                normalizedItemId,
                itemName,
                result.RemovedQuantity
            );
        }
        RollbackWarehouseTransaction(snapshot);
        return new WarehouseDiscardMutationResult(
            false,
            WarehouseDiscardFailureKind.StageFailed,
            normalizedItemId,
            itemName,
            result.RemovedQuantity
        );
    }

    WarehouseUseMutationResult IGameRuntimeWarehousePort.UseItemAndStage(
        StringName itemId,
        StringName memberId,
        PartyItemUseService.PartyItemUseOptions options
    )
    {
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        StringName resolvedMemberId = ResolveWarehouseTargetMemberIdInternal(memberId);
        string itemName = GetItemDisplayName(normalizedItemId);
        if (resolvedMemberId == "")
        {
            return BuildWarehouseUseResult(
                false,
                WarehouseUseFailureKind.MissingTargetMember,
                normalizedItemId,
                itemName,
                resolvedMemberId,
                "",
                "",
                "",
                null
            );
        }
        if (_party_item_use_service == null)
        {
            return BuildWarehouseUseResult(
                false,
                WarehouseUseFailureKind.ServiceUnavailable,
                normalizedItemId,
                itemName,
                resolvedMemberId,
                GetMemberDisplayName(resolvedMemberId),
                "",
                "",
                null
            );
        }

        WarehouseTransactionSnapshot snapshot = CaptureWarehouseTransactionSnapshot();
        PartyItemUseService.PartyItemUseResult useResult = _party_item_use_service.UseItemTyped(
            normalizedItemId,
            resolvedMemberId,
            options
        );
        if (useResult == null || !useResult.Success)
        {
            return BuildWarehouseUseResult(
                false,
                useResult == null
                    ? WarehouseUseFailureKind.ServiceUnavailable
                    : ToWarehouseUseFailureKind(useResult.Reason),
                normalizedItemId,
                itemName,
                resolvedMemberId,
                GetMemberDisplayName(resolvedMemberId),
                useResult?.SkillId ?? default,
                GetSkillDisplayName(useResult?.SkillId ?? default),
                useResult?.PracticeReplacementStatus
            );
        }

        _party_selected_member_id = resolvedMemberId;
        string memberName = GetMemberDisplayName(resolvedMemberId);
        string skillName = GetSkillDisplayName(useResult.SkillId);
        if ((Error)StagePartyStateInternal() == Error.Ok)
        {
            return BuildWarehouseUseResult(
                true,
                WarehouseUseFailureKind.None,
                normalizedItemId,
                itemName,
                resolvedMemberId,
                memberName,
                useResult.SkillId,
                skillName,
                useResult.PracticeReplacementStatus
            );
        }
        RollbackWarehouseTransaction(snapshot);
        return BuildWarehouseUseResult(
            false,
            WarehouseUseFailureKind.StageFailed,
            normalizedItemId,
            itemName,
            resolvedMemberId,
            memberName,
            useResult.SkillId,
            skillName,
            useResult.PracticeReplacementStatus
        );
    }

    WarehouseAddMutationResult IGameRuntimeWarehousePort.AddItemAndStage(
        StringName itemId,
        int quantity
    )
    {
        StringName normalizedItemId = ProgressionDataUtils.to_string_name(itemId);
        string itemName = GetItemDisplayName(normalizedItemId);
        WarehouseTransactionSnapshot snapshot = CaptureWarehouseTransactionSnapshot();
        PartyWarehouseService.WarehouseAddItemResult result =
            _party_warehouse_service?.AddItemTyped(normalizedItemId, quantity);
        if (result == null || result.AddedQuantity <= 0)
        {
            return new WarehouseAddMutationResult(
                false,
                WarehouseAddFailureKind.MutationFailed,
                normalizedItemId,
                itemName,
                0,
                result?.RemainingQuantity ?? quantity
            );
        }
        if ((Error)StagePartyStateInternal() == Error.Ok)
        {
            return new WarehouseAddMutationResult(
                true,
                WarehouseAddFailureKind.None,
                normalizedItemId,
                itemName,
                result.AddedQuantity,
                result.RemainingQuantity
            );
        }
        RollbackWarehouseTransaction(snapshot);
        return new WarehouseAddMutationResult(
            false,
            WarehouseAddFailureKind.StageFailed,
            normalizedItemId,
            itemName,
            result.AddedQuantity,
            result.RemainingQuantity
        );
    }

    private void AppendWarehouseTargetMembers(
        List<WarehouseTargetMemberSnapshot> target,
        HashSet<StringName> seenMemberIds,
        IEnumerable<StringName> memberIds,
        string rosterRole
    )
    {
        if (memberIds == null)
            return;
        foreach (StringName memberId in memberIds)
        {
            if (
                memberId == ""
                || !seenMemberIds.Add(memberId)
                || _party_state?.GetMemberState(memberId) == null
            )
            {
                continue;
            }
            target.Add(
                new WarehouseTargetMemberSnapshot(
                    memberId,
                    GetMemberDisplayName(memberId),
                    rosterRole
                )
            );
        }
    }

    private StringName ResolveWarehouseTargetMemberIdInternal(
        StringName preferredMemberId = default
    )
    {
        if (_party_state == null)
            return "";
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(preferredMemberId);
        if (
            normalizedMemberId != ""
            && _party_state.GetMemberState(normalizedMemberId) != null
        )
        {
            return normalizedMemberId;
        }
        if (
            _party_selected_member_id != ""
            && _party_state.GetMemberState(_party_selected_member_id) != null
        )
        {
            return _party_selected_member_id;
        }
        if (
            _party_state.leader_member_id != ""
            && _party_state.GetMemberState(_party_state.leader_member_id) != null
        )
        {
            return _party_state.leader_member_id;
        }
        foreach (StringName candidate in _party_state.active_member_ids)
        {
            if (_party_state.GetMemberState(candidate) != null)
                return candidate;
        }
        foreach (StringName candidate in _party_state.reserve_member_ids)
        {
            if (_party_state.GetMemberState(candidate) != null)
                return candidate;
        }
        return "";
    }

    private WarehouseTransactionSnapshot CaptureWarehouseTransactionSnapshot()
    {
        IReadOnlyDictionary<string, object> runtimeState =
            new Dictionary<string, object>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, object> worldData =
            new Dictionary<string, object>(StringComparer.Ordinal);
        if (_game_session != null)
        {
            using GodotProjectionLease<GDictionary> runtimeStateLease =
                _game_session.CaptureRuntimeStateLease();
            runtimeState = RuntimePlainPayload.NormalizeDictionary(
                runtimeStateLease.Value,
                "GameRuntimeFacade.WarehouseTransaction.RuntimeState"
            );
            using GodotProjectionLease<GDictionary> worldDataLease =
                _game_session.GetWorldDataLease();
            worldData = RuntimePlainPayload.NormalizeDictionary(
                worldDataLease.Value,
                "GameRuntimeFacade.WarehouseTransaction.WorldData"
            );
        }
        return new WarehouseTransactionSnapshot
        {
            RuntimeStatePlain = runtimeState,
            PartyState = _party_state?.DuplicateState(),
            WorldDataPlain = worldData,
            SelectedMemberId = _party_selected_member_id,
        };
    }

    private bool RollbackWarehouseTransaction(WarehouseTransactionSnapshot snapshot)
    {
        if (snapshot?.PartyState == null)
            return false;
        PartyState restoredPartyState = snapshot.PartyState.DuplicateState();
        if (restoredPartyState == null)
            return false;

        Dictionary<string, object> restoredWorldDataPlain =
            RuntimePlainPayload.CloneDictionary(snapshot.WorldDataPlain);
        using GodotProjectionLease<GDictionary> worldDataLease =
            RuntimePlainPayload.ProjectDictionaryLease(
                restoredWorldDataPlain,
                "GameRuntimeFacade.WarehouseTransaction.RestoreWorldData",
                LifetimeDomain.Request,
                "GameRuntimeFacade.WarehouseTransaction.RestoreWorldData"
            );
        GDictionary restoredWorldData = worldDataLease.Value;
        Dictionary<string, object> restoredRuntimeStatePlain =
            RuntimePlainPayload.CloneDictionary(snapshot.RuntimeStatePlain);
        if (_game_session != null && restoredRuntimeStatePlain.Count > 0)
        {
            restoredRuntimeStatePlain["party_state"] =
                restoredPartyState.BuildSaveSnapshotPlain();
            if (restoredWorldDataPlain.Count > 0)
                restoredRuntimeStatePlain["world_data"] = restoredWorldDataPlain;
            using GodotProjectionLease<GDictionary> runtimeStateLease =
                RuntimePlainPayload.ProjectDictionaryLease(
                    restoredRuntimeStatePlain,
                    "GameRuntimeFacade.WarehouseTransaction.RestoreRuntimeState",
                    LifetimeDomain.Request,
                    "GameRuntimeFacade.WarehouseTransaction.RestoreRuntimeState"
                );
            _game_session.RestoreRuntimeState(runtimeStateLease.Value);
        }
        else if (_game_session != null)
        {
            _game_session.SetPartyState(restoredPartyState);
            if (restoredWorldData.Count > 0)
                _game_session.SetWorldData(restoredWorldData);
            _game_session.DiscardPendingSave();
        }

        SetPartyState(restoredPartyState);
        _party_selected_member_id = snapshot.SelectedMemberId;
        RestoreWarehouseWorldDataContext(restoredWorldData);
        return true;
    }

    private void RestoreWarehouseWorldDataContext(GDictionary restoredWorldData)
    {
        if (
            restoredWorldData == null
            || restoredWorldData.Count == 0
            || _world_map_data_context == null
        )
        {
            return;
        }
        _world_map_data_context.BindRootWorldData(restoredWorldData);
        _world_map_data_context.SyncActiveWorldContext(
            GetGenerationDefinition(),
            GetGridSystem(),
            _player_coord,
            _selected_coord
        );
    }

    private static PartyWarehouseService.WarehouseRemoveItemResult
        RemoveWarehouseItemOrInstanceInternal(
            PartyWarehouseService warehouseService,
            StringName itemId,
            int quantity,
            StringName instanceId
        )
    {
        StringName normalizedInstanceId = ProgressionDataUtils.to_string_name(instanceId);
        ItemDefinition itemDef = warehouseService?.GetItemDef(itemId);
        if (itemDef != null && itemDef.IsEquipment())
        {
            return warehouseService.RemoveEquipmentInstanceTyped(
                itemId,
                normalizedInstanceId
            );
        }
        return warehouseService?.RemoveItemTyped(itemId, quantity);
    }

    private static WarehouseDiscardFailureKind ToWarehouseDiscardFailureKind(
        string errorCode
    ) =>
        errorCode switch
        {
            "equipment_instance_id_required" =>
                WarehouseDiscardFailureKind.EquipmentInstanceIdRequired,
            "warehouse_missing_instance" =>
                WarehouseDiscardFailureKind.WarehouseMissingInstance,
            "equipment_instance_item_mismatch" =>
                WarehouseDiscardFailureKind.EquipmentInstanceItemMismatch,
            "item_not_equipment" => WarehouseDiscardFailureKind.ItemNotEquipment,
            "item_not_found" => WarehouseDiscardFailureKind.ItemNotFound,
            _ => WarehouseDiscardFailureKind.MutationFailed,
        };

    private static WarehouseUseFailureKind ToWarehouseUseFailureKind(string reason) =>
        reason switch
        {
            "missing_item_def" => WarehouseUseFailureKind.MissingItemDefinition,
            "item_not_usable" => WarehouseUseFailureKind.ItemNotUsable,
            "missing_member" => WarehouseUseFailureKind.MissingMember,
            "missing_inventory" => WarehouseUseFailureKind.MissingInventory,
            "missing_skill_def" => WarehouseUseFailureKind.MissingSkillDefinition,
            "learn_failed" => WarehouseUseFailureKind.LearnFailed,
            "practice_replacement_confirmation_required" =>
                WarehouseUseFailureKind.PracticeReplacementConfirmationRequired,
            "consume_failed" => WarehouseUseFailureKind.ConsumeFailed,
            "service_unavailable" => WarehouseUseFailureKind.ServiceUnavailable,
            _ => WarehouseUseFailureKind.Unknown,
        };

    private WarehouseUseMutationResult BuildWarehouseUseResult(
        bool success,
        WarehouseUseFailureKind failureKind,
        StringName itemId,
        string itemName,
        StringName memberId,
        string memberName,
        StringName skillId,
        string skillName,
        PracticeSkillLearnStatus practiceStatus
    ) =>
        new(
            success,
            failureKind,
            itemId,
            itemName,
            memberId,
            memberName,
            skillId,
            skillName,
            practiceStatus != null
                ? GetSkillDisplayName(practiceStatus.ExistingSkillId)
                : "",
            practiceStatus?.PredictedLevel ?? 0
        );
}
