using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;

internal sealed class GameRuntimeBattleLootCommitService : IDisposable
{
    private WeakReference<GameRuntimeFacade> _runtimeRef;

    internal sealed class ItemCommitResult
    {
        internal bool Ok { get; private set; }
        internal string ErrorCode { get; private set; } = "";
        internal string BlockedItemId { get; private set; } = "";
        internal int CommittedItemCount { get; private set; }
        private readonly List<BattleLootEntry> _overflowEntries = new();
        internal IReadOnlyList<BattleLootEntry> OverflowEntries => _overflowEntries;

        internal static ItemCommitResult Create(
            bool ok,
            string errorCode,
            string blockedItemId,
            int committedItemCount,
            IEnumerable<BattleLootEntry> overflowEntries
        )
        {
            var result = new ItemCommitResult
            {
                Ok = ok,
                ErrorCode = errorCode ?? "",
                BlockedItemId = blockedItemId ?? "",
                CommittedItemCount = Mathf.Max(committedItemCount, 0),
            };
            foreach (BattleLootEntry entry in overflowEntries ?? System.Array.Empty<BattleLootEntry>())
            {
                BattleLootEntry duplicate = entry?.Duplicate();
                if (duplicate != null)
                    result._overflowEntries.Add(duplicate);
            }
            return result;
        }

    }

    internal sealed class BattleLootCommitResult : IDisposable
    {
        private bool _disposed;
        internal bool Ok { get; private set; }
        internal string ErrorCode { get; private set; } = "";
        internal string BlockedItemId { get; private set; } = "";
        internal int CommittedItemCount { get; private set; }
        internal GArray OverflowEntries { get; private set; } = new();
        internal int OverflowEntryCount { get; private set; }

        internal static BattleLootCommitResult Create(
            bool ok,
            string errorCode,
            string blockedItemId,
            int committedItemCount,
            IEnumerable<BattleLootEntry> overflowEntries,
            int overflowEntryCount
        )
        {
            var normalizedOverflowEntries = BattleLootEntryPayload.ProjectEntries(
                overflowEntries
            );
            return new BattleLootCommitResult
            {
                Ok = ok,
                ErrorCode = errorCode ?? "",
                BlockedItemId = blockedItemId ?? "",
                CommittedItemCount = Mathf.Max(committedItemCount, 0),
                OverflowEntries = normalizedOverflowEntries,
                OverflowEntryCount = Mathf.Max(overflowEntryCount, normalizedOverflowEntries.Count),
            };
        }

        internal static BattleLootCommitResult Success() =>
            Create(true, "", "", 0, System.Array.Empty<BattleLootEntry>(), 0);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            DisposeOwnedArray(OverflowEntries);
            OverflowEntries = null;
            GC.SuppressFinalize(this);
        }

    }

    private GameRuntimeFacade _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GameRuntimeFacade>(value) : null;
    }

    internal void Setup(GameRuntimeFacade runtime)
    {
        _runtime = runtime;
    }

    public void Dispose()
    {
        _runtime = null;
        GC.SuppressFinalize(this);
    }

    internal BattleLootCommitResult CommitBattleLootToSharedWarehouseTyped(
        BattleResolutionResult battleResolutionResult
    )
    {
        return CommitBattleLootToSharedWarehouseInternal(battleResolutionResult);
    }

    internal void ClearRegularBattleCalamityShardFlags()
    {
        ClearRegularBattleCalamityShardFlagsInternal();
    }

    internal string BuildBattleResolutionStatusMessageTyped(
        string battleName,
        string winnerFactionId,
        BattleLootCommitResult lootCommitResult,
        bool persistedOk
    )
    {
        return BuildBattleResolutionStatusMessageInternal(
            battleName,
            winnerFactionId,
            lootCommitResult,
            persistedOk
        );
    }

    internal Dictionary BuildLastBattleLootSnapshotTyped(
        string battleName,
        string winnerFactionId,
        BattleResolutionResult battleResolutionResult,
        BattleLootCommitResult lootCommitResult
    )
    {
        return BuildLastBattleLootSnapshotInternal(
            battleName,
            winnerFactionId,
            battleResolutionResult,
            lootCommitResult
        );
    }

    internal string FormatBattleDropEntries(Godot.Collections.Array dropEntryOptions)
    {
        return FormatBattleDropEntriesInternal(dropEntryOptions);
    }

    private BattleLootCommitResult CommitBattleLootToSharedWarehouseInternal(
        BattleResolutionResult battleResolutionResult
    )
    {
        if (battleResolutionResult == null)
            return BattleLootCommitResult.Create(
                false,
                "missing_battle_resolution_result",
                "",
                0,
                System.Array.Empty<BattleLootEntry>(),
                0
            );

        battleResolutionResult.SetOverflowEntries(System.Array.Empty<BattleLootEntry>());
        if (battleResolutionResult.winner_faction_id != "player")
            return BattleLootCommitResult.Success();

        var partyState = _runtime._party_state;
        var partyWarehouseService = _runtime._party_warehouse_service;
        var gameSession = _runtime._game_session;
        if (partyState == null || partyWarehouseService == null || gameSession == null)
            return BattleLootCommitResult.Create(
                false,
                "warehouse_service_unavailable",
                "",
                0,
                System.Array.Empty<BattleLootEntry>(),
                0
            );

        var itemDefs = gameSession.GetItemDefsTyped();
        _runtime.SetupPartyWarehouseService(partyWarehouseService, partyState, itemDefs);

        WarehouseState warehouseStateBefore = null;
        Dictionary fateRunFlagsBefore = null;
        try
        {
            var warehouseState = partyState.warehouse_state;
            if (warehouseState != null)
                warehouseStateBefore = warehouseState.DuplicateState();

            fateRunFlagsBefore = partyState.CaptureFateRunFlags();

            var overflowEntries = new List<BattleLootEntry>();
            var committedItemCount = 0;
            var effectiveLootEntries = ResolveEffectiveBattleLootEntriesForCommit(
                battleResolutionResult
            );
            battleResolutionResult.SetLootEntries(effectiveLootEntries);

            foreach (BattleLootEntry lootEntry in battleResolutionResult.loot_entries)
            {
                if (lootEntry == null)
                    continue;

                WarehouseState entryWarehouseStateBefore = null;
                Dictionary entryFateRunFlagsBefore = null;
                try
                {
                    entryWarehouseStateBefore = partyState.warehouse_state?.DuplicateState();
                    entryFateRunFlagsBefore = partyState.CaptureFateRunFlags();

                    if (lootEntry.DropKind == BattleLootDropKind.EquipmentInstance)
                    {
                        var instanceCommitResult = CommitEquipmentInstanceLootEntry(lootEntry);
                        if (!instanceCommitResult.Ok)
                        {
                            if (IsFatalLootCommitError(instanceCommitResult.ErrorCode))
                            {
                                RestoreLootCommitState(
                                    partyState,
                                    partyWarehouseService,
                                    itemDefs,
                                    warehouseStateBefore,
                                    fateRunFlagsBefore
                                );
                                return BattleLootCommitResult.Create(
                                    false,
                                    instanceCommitResult.ErrorCode,
                                    instanceCommitResult.BlockedItemId,
                                    0,
                                    System.Array.Empty<BattleLootEntry>(),
                                    0
                                );
                            }
                            RestoreLootCommitState(
                                partyState,
                                partyWarehouseService,
                                itemDefs,
                                entryWarehouseStateBefore,
                                entryFateRunFlagsBefore
                            );
                            LogDroppedBattleLootEntry(
                                lootEntry,
                                instanceCommitResult,
                                "battle_loot_equipment_instance_failed"
                            );
                            continue;
                        }
                        committedItemCount += instanceCommitResult.CommittedItemCount;
                        AppendOverflowEntries(overflowEntries, instanceCommitResult.OverflowEntries);
                        continue;
                    }

                    if (lootEntry.DropKind == BattleLootDropKind.RandomEquipment)
                    {
                        var equipmentCommitResult = CommitRandomEquipmentLootEntry(lootEntry);
                        if (!equipmentCommitResult.Ok)
                        {
                            if (IsFatalLootCommitError(equipmentCommitResult.ErrorCode))
                            {
                                RestoreLootCommitState(
                                    partyState,
                                    partyWarehouseService,
                                    itemDefs,
                                    warehouseStateBefore,
                                    fateRunFlagsBefore
                                );
                                return BattleLootCommitResult.Create(
                                    false,
                                    equipmentCommitResult.ErrorCode,
                                    equipmentCommitResult.BlockedItemId,
                                    0,
                                    System.Array.Empty<BattleLootEntry>(),
                                    0
                                );
                            }
                            RestoreLootCommitState(
                                partyState,
                                partyWarehouseService,
                                itemDefs,
                                entryWarehouseStateBefore,
                                entryFateRunFlagsBefore
                            );
                            LogDroppedBattleLootEntry(
                                lootEntry,
                                equipmentCommitResult,
                                "battle_loot_random_equipment_failed"
                            );
                            continue;
                        }
                        committedItemCount += equipmentCommitResult.CommittedItemCount;
                        AppendOverflowEntries(overflowEntries, equipmentCommitResult.OverflowEntries);
                        continue;
                    }

                    var itemCommitResult = CommitFixedItemLootEntry(lootEntry);
                    if (!itemCommitResult.Ok)
                    {
                        if (IsFatalLootCommitError(itemCommitResult.ErrorCode))
                        {
                            RestoreLootCommitState(
                                partyState,
                                partyWarehouseService,
                                itemDefs,
                                warehouseStateBefore,
                                fateRunFlagsBefore
                            );
                            return BattleLootCommitResult.Create(
                                false,
                                itemCommitResult.ErrorCode,
                                itemCommitResult.BlockedItemId,
                                0,
                                System.Array.Empty<BattleLootEntry>(),
                                0
                            );
                        }
                        RestoreLootCommitState(
                            partyState,
                            partyWarehouseService,
                            itemDefs,
                            entryWarehouseStateBefore,
                            entryFateRunFlagsBefore
                        );
                        LogDroppedBattleLootEntry(
                            lootEntry,
                            itemCommitResult,
                            "battle_loot_item_missing_def"
                        );
                        continue;
                    }
                    committedItemCount += itemCommitResult.CommittedItemCount;
                    if (IsOrdinaryBattleCalamityConversionEntry(lootEntry))
                        MarkRegularBattleCalamityShardsCommitted(itemCommitResult.CommittedItemCount);
                    AppendOverflowEntries(overflowEntries, itemCommitResult.OverflowEntries);
                }
                finally
                {
                    entryWarehouseStateBefore?.Dispose();
                    DisposeOwnedDictionary(entryFateRunFlagsBefore);
                }
            }

            battleResolutionResult.SetOverflowEntries(overflowEntries);
            var overflowItemId = battleResolutionResult.overflow_entries.Count > 0
                ? battleResolutionResult.overflow_entries[0].ItemId.ToString()
                : "";
            return BattleLootCommitResult.Create(
                true,
                "",
                overflowItemId,
                committedItemCount,
                battleResolutionResult.overflow_entries,
                battleResolutionResult.overflow_entries.Count
            );
        }
        finally
        {
            warehouseStateBefore?.Dispose();
            DisposeOwnedDictionary(fateRunFlagsBefore);
        }
    }

    private ItemCommitResult CommitFixedItemLootEntry(BattleLootEntry lootEntry)
    {
        if (lootEntry == null || lootEntry.DropKind != BattleLootDropKind.Item)
            return ItemCommitResult.Create(true, "", "", 0, System.Array.Empty<BattleLootEntry>());
        var itemId = lootEntry.ItemId;
        var quantity = lootEntry.Quantity;
        if (itemId == "" || quantity <= 0)
            return ItemCommitResult.Create(true, "", "", 0, System.Array.Empty<BattleLootEntry>());
        var partyWarehouseService = _runtime._party_warehouse_service;
        var addResult = partyWarehouseService.AddItemTyped(itemId, quantity);
        if (!addResult.ItemFound)
            return ItemCommitResult.Create(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        var overflowEntries = new List<BattleLootEntry>();
        var remainingQuantity = Mathf.Max(addResult.RemainingQuantity, 0);
        if (remainingQuantity > 0)
            overflowEntries.Add(BuildBattleOverflowEntry(lootEntry, remainingQuantity));
        return ItemCommitResult.Create(
            true,
            "",
            "",
            Mathf.Max(addResult.AddedQuantity, 0),
            overflowEntries
        );
    }

    private ItemCommitResult CommitRandomEquipmentLootEntry(BattleLootEntry lootEntry)
    {
        if (lootEntry == null || lootEntry.DropKind != BattleLootDropKind.RandomEquipment)
            return ItemCommitResult.Create(true, "", "", 0, System.Array.Empty<BattleLootEntry>());
        var itemId = lootEntry.ItemId;
        var quantity = lootEntry.Quantity;
        var dropLuck = lootEntry.DropLuck;
        if (itemId == "" || quantity <= 0)
            return ItemCommitResult.Create(true, "", "", 0, System.Array.Empty<BattleLootEntry>());
        var gameSession = _runtime._game_session;
        var itemDefs = gameSession.GetItemDefsTyped();
        itemDefs.TryGetValue(itemId, out ItemDef itemDef);
        if (itemDef == null)
            return ItemCommitResult.Create(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        if (!itemDef.IsEquipment())
            return ItemCommitResult.Create(
                false,
                "battle_loot_random_equipment_invalid_item",
                itemId.ToString(),
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        var equipmentDropService = _runtime._equipment_drop_service;
        if (equipmentDropService == null)
            return ItemCommitResult.Create(
                false,
                "battle_loot_random_equipment_service_unavailable",
                itemId.ToString(),
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        var rolledInstances = equipmentDropService.RollItemInstances(itemId, quantity, dropLuck);
        var committedItemCount = 0;
        var overflowQuantity = 0;
        foreach (EquipmentInstanceState rolledInstance in rolledInstances)
        {
            if (rolledInstance == null)
                continue;
            string rolledItemId = rolledInstance.item_id?.ToString() ?? "";
            var partyWarehouseService = _runtime._party_warehouse_service;
            var addResult = partyWarehouseService.AddEquipmentInstanceTyped(rolledInstance);
            if (!addResult.ItemFound)
            {
                GodotRefCountedDisposer.DisposeIfValid(rolledInstance);
                return ItemCommitResult.Create(
                    false,
                    "battle_loot_item_missing_def",
                    rolledItemId,
                    0,
                    System.Array.Empty<BattleLootEntry>()
                );
            }
            if (!addResult.IsEquipment)
            {
                GodotRefCountedDisposer.DisposeIfValid(rolledInstance);
                return ItemCommitResult.Create(
                    false,
                    "battle_loot_random_equipment_invalid_item",
                    rolledItemId,
                    0,
                    System.Array.Empty<BattleLootEntry>()
                );
            }
            if (addResult.RemainingQuantity > 0)
            {
                GodotRefCountedDisposer.DisposeIfValid(rolledInstance);
                overflowQuantity++;
                continue;
            }
            committedItemCount++;
        }
        var overflowEntries = new List<BattleLootEntry>();
        if (overflowQuantity > 0)
            overflowEntries.Add(BuildBattleOverflowEntry(lootEntry, overflowQuantity));
        return ItemCommitResult.Create(true, "", "", committedItemCount, overflowEntries);
    }

    private ItemCommitResult CommitEquipmentInstanceLootEntry(BattleLootEntry lootEntry)
    {
        if (lootEntry == null || lootEntry.DropKind != BattleLootDropKind.EquipmentInstance)
            return ItemCommitResult.Create(
                false,
                "battle_loot_equipment_instance_invalid_payload",
                lootEntry?.ItemId.ToString() ?? "",
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        EquipmentInstanceState equipmentInstance = lootEntry.EquipmentInstance?.DuplicateState();
        StringName itemId = equipmentInstance?.item_id;
        string itemIdText = itemId?.ToString() ?? "";
        if (itemId == null || itemId == "")
        {
            GodotRefCountedDisposer.DisposeIfValid(equipmentInstance);
            return ItemCommitResult.Create(
                false,
                "battle_loot_equipment_instance_invalid_payload",
                "",
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        }
        var gameSession = _runtime._game_session;
        var itemDefs = gameSession.GetItemDefsTyped();
        itemDefs.TryGetValue(itemId, out ItemDef itemDef);
        if (itemDef == null)
        {
            GodotRefCountedDisposer.DisposeIfValid(equipmentInstance);
            return ItemCommitResult.Create(
                false,
                "battle_loot_item_missing_def",
                itemIdText,
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        }
        if (!itemDef.IsEquipment())
        {
            GodotRefCountedDisposer.DisposeIfValid(equipmentInstance);
            return ItemCommitResult.Create(
                false,
                "battle_loot_random_equipment_invalid_item",
                itemIdText,
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        }
        var partyWarehouseService = _runtime._party_warehouse_service;
        var addResult = partyWarehouseService.AddEquipmentInstanceTyped(equipmentInstance);
        if (addResult.RemainingQuantity > 0)
        {
            GodotRefCountedDisposer.DisposeIfValid(equipmentInstance);
            return ItemCommitResult.Create(
                true,
                "",
                "",
                0,
                new[] { BuildBattleOverflowEntry(lootEntry, 1) }
            );
        }
        return ItemCommitResult.Create(true, "", "", 1, System.Array.Empty<BattleLootEntry>());
    }

    private BattleLootEntry BuildBattleOverflowEntry(BattleLootEntry lootEntry, int overflowQuantity)
    {
        return lootEntry?.WithQuantity(Mathf.Max(overflowQuantity, 0));
    }

    private static void AppendOverflowEntries(
        List<BattleLootEntry> target,
        IEnumerable<BattleLootEntry> source
    )
    {
        if (target == null || source == null)
            return;
        foreach (BattleLootEntry overflowValue in source)
        {
            BattleLootEntry duplicate = overflowValue?.Duplicate();
            if (duplicate != null)
                target.Add(duplicate);
        }
    }

    private static string FallbackString(string value, string fallback)
    {
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private void RestoreLootCommitState(
        PartyState partyState,
        PartyWarehouseService partyWarehouseService,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        WarehouseState warehouseStateBefore,
        Godot.Collections.Dictionary fateRunFlagsBefore
    )
    {
        if (partyState == null)
            return;
        partyState.warehouse_state = warehouseStateBefore?.DuplicateState();
        partyState.ApplyFateRunFlags(fateRunFlagsBefore);
        _runtime.SetupPartyWarehouseService(partyWarehouseService, partyState, itemDefs);
    }

    private static bool IsFatalLootCommitError(string errorCode) =>
        errorCode == "battle_loot_random_equipment_service_unavailable";

    private void LogDroppedBattleLootEntry(
        BattleLootEntry lootEntry,
        ItemCommitResult commitResult,
        string fallbackErrorCode
    )
    {
        string errorCode = FallbackString(commitResult?.ErrorCode, fallbackErrorCode);
        string blockedItemId = FallbackString(
            commitResult?.BlockedItemId,
            lootEntry?.ItemId.ToString() ?? ""
        );
        using var context = new Dictionary
        {
            ["error_code"] = errorCode,
            ["blocked_item_id"] = blockedItemId,
            ["drop_type"] =
                lootEntry != null ? BattleLootIds.ToStringName(lootEntry.DropKind).ToString() : "",
            ["drop_source_kind"] =
                lootEntry != null ? BattleLootIds.ToStringName(lootEntry.SourceKind).ToString() : "",
            ["drop_source_id"] = lootEntry?.SourceId.ToString() ?? "",
            ["drop_entry_id"] = lootEntry?.DropEntryId.ToString() ?? "",
            ["item_id"] = lootEntry?.ItemId.ToString() ?? "",
            ["quantity"] = lootEntry?.Quantity ?? 0,
        };
        string message = string.IsNullOrEmpty(blockedItemId)
            ? "战斗掉落奖励不合法，已丢弃。"
            : $"战斗掉落奖励 {blockedItemId} 不合法，已丢弃。";
        string contextText = Json.Stringify(context);
        GameLog.Warning(message, "battle.loot_dropped", "battle", contextText);
        var runtime = _runtime;
        runtime?._log_runtime_event(
            "warn",
            "battle",
            "battle.loot_dropped",
            message,
            contextText
        );
    }

    private List<BattleLootEntry> ResolveEffectiveBattleLootEntriesForCommit(
        BattleResolutionResult battleResolutionResult
    )
    {
        var adjustedEntries = new List<BattleLootEntry>();
        if (battleResolutionResult == null)
            return adjustedEntries;
        var remainingRegularCap = GetRemainingRegularBattleCalamityShardCap();
        var mergeIndexByKey = new System.Collections.Generic.Dictionary<string, int>();
        foreach (BattleLootEntry rawLootEntry in battleResolutionResult.loot_entries)
        {
            BattleLootEntry lootEntry = rawLootEntry?.Duplicate();
            if (lootEntry == null)
                continue;
            if (IsOrdinaryBattleCalamityConversionEntry(lootEntry))
            {
                var allowedQuantity = Mathf.Min(
                    Mathf.Max(lootEntry.Quantity, 0),
                    remainingRegularCap
                );
                remainingRegularCap = Mathf.Max(remainingRegularCap - allowedQuantity, 0);
                if (allowedQuantity <= 0)
                    continue;
                lootEntry = lootEntry.WithQuantity(allowedQuantity);
            }
            var mergeKey = BuildBattleLootMergeKey(lootEntry);
            if (
                !string.IsNullOrEmpty(mergeKey)
                && mergeIndexByKey.TryGetValue(mergeKey, out int entryIndex)
            )
            {
                if (entryIndex >= 0 && entryIndex < adjustedEntries.Count)
                {
                    BattleLootEntry mergedEntry = adjustedEntries[entryIndex]
                        .WithQuantity(adjustedEntries[entryIndex].Quantity + lootEntry.Quantity);
                    if (mergedEntry != null)
                        adjustedEntries[entryIndex] = mergedEntry;
                    continue;
                }
            }
            if (!string.IsNullOrEmpty(mergeKey))
                mergeIndexByKey[mergeKey] = adjustedEntries.Count;
            adjustedEntries.Add(lootEntry);
        }
        return adjustedEntries;
    }

    private string BuildBattleLootMergeKey(BattleLootEntry lootEntry)
    {
        if (lootEntry == null || lootEntry.ItemId == "")
            return "";
        if (lootEntry.DropKind == BattleLootDropKind.Item)
            return string.Format("{0}|{1}", lootEntry.DropKind, lootEntry.ItemId);
        if (lootEntry.DropKind == BattleLootDropKind.RandomEquipment)
            return string.Format(
                "{0}|{1}|{2}",
                lootEntry.DropKind,
                lootEntry.ItemId,
                lootEntry.DropLuck
            );
        return "";
    }

    private bool IsOrdinaryBattleCalamityConversionEntry(BattleLootEntry lootEntry)
    {
        if (lootEntry == null)
            return false;
        return BattleLootIds.ToSpecialItemKind(lootEntry.ItemId)
                == BattleLootSpecialItemKind.CalamityShard
            && lootEntry.SourceKind == BattleLootSourceKind.CalamityConversion
            && BattleLootIds.ToSourceIdKind(lootEntry.SourceId)
                == BattleLootSourceIdKind.OrdinaryBattle;
    }

    private int GetRemainingRegularBattleCalamityShardCap()
    {
        return Mathf.Max(
            BattleLootIds.OrdinaryBattleCalamityShardChapterCap
                - GetRegularBattleCalamityShardCountThisChapter(),
            0
        );
    }

    private int GetRegularBattleCalamityShardCountThisChapter()
    {
        var partyState = _runtime._party_state;
        if (partyState == null)
            return 0;
        var shardCount = 0;
        for (
            int slotIndex = 0;
            slotIndex < BattleLootIds.OrdinaryBattleCalamityShardChapterCap;
            slotIndex++
        )
        {
            var flagId = BuildRegularBattleCalamityShardFlagId(slotIndex);
            if (partyState.GetFateRunFlag(flagId, false))
                shardCount++;
        }
        return shardCount;
    }

    private void MarkRegularBattleCalamityShardsCommitted(int quantity)
    {
        var partyState = _runtime._party_state;
        if (partyState == null || quantity <= 0)
            return;
        var remainingToMark = Mathf.Min(quantity, GetRemainingRegularBattleCalamityShardCap());
        if (remainingToMark <= 0)
            return;
        for (
            int slotIndex = 0;
            slotIndex < BattleLootIds.OrdinaryBattleCalamityShardChapterCap;
            slotIndex++
        )
        {
            var flagId = BuildRegularBattleCalamityShardFlagId(slotIndex);
            if (partyState.GetFateRunFlag(flagId, false))
                continue;
            partyState.SetFateRunFlag(flagId, true);
            remainingToMark--;
            if (remainingToMark <= 0)
                return;
        }
    }

    private void ClearRegularBattleCalamityShardFlagsInternal()
    {
        var partyState = _runtime._party_state;
        if (partyState == null)
            return;
        for (
            int slotIndex = 0;
            slotIndex < BattleLootIds.OrdinaryBattleCalamityShardChapterCap;
            slotIndex++
        )
            partyState.ClearFateRunFlag(BuildRegularBattleCalamityShardFlagId(slotIndex));
    }

    private StringName BuildRegularBattleCalamityShardFlagId(int slotIndex)
    {
        return ProgressionDataUtils.to_string_name(
            string.Format(
                "{0}{1}",
                BattleLootIds.CalamityShardChapterFlagPrefix,
                Mathf.Max(slotIndex, 0)
            )
        );
    }

    private string BuildBattleResolutionStatusMessageInternal(
        string battleName,
        string winnerFactionId,
        BattleLootCommitResult lootCommitResult,
        bool persistedOk
    )
    {
        string message;
        if (persistedOk)
            message = string.Format(
                "{0} 战斗结束，胜利方：{1}。已返回世界地图并统一保存。",
                battleName,
                FormatFactionLabel(winnerFactionId)
            );
        else
            message = string.Format("{0} 战斗结束，但战后持久化失败。", battleName);
        var lootStatusSuffix = BuildBattleLootStatusSuffix(lootCommitResult);
        if (string.IsNullOrEmpty(lootStatusSuffix))
            return message;
        return string.Format("{0} {1}", message, lootStatusSuffix);
    }

    private string BuildBattleLootStatusSuffix(BattleLootCommitResult lootCommitResult)
    {
        if (lootCommitResult == null)
            return "";
        if (!lootCommitResult.Ok)
        {
            if (!string.IsNullOrEmpty(lootCommitResult.BlockedItemId))
                return string.Format(
                    "战斗掉落写入共享仓库失败：{0}。",
                    GetItemDisplayName(lootCommitResult.BlockedItemId)
                );
            return "战斗掉落写入共享仓库失败。";
        }
        var overflowText = FormatBattleDropEntriesInternal(lootCommitResult.OverflowEntries);
        if (string.IsNullOrEmpty(overflowText))
            return "";
        return string.Format("未装下的掉落：{0}。", overflowText);
    }

    private Dictionary BuildLastBattleLootSnapshotInternal(
        string battleName,
        string winnerFactionId,
        BattleResolutionResult battleResolutionResult,
        BattleLootCommitResult lootCommitResult
    )
    {
        if (battleResolutionResult == null)
            return new Dictionary();
        var lootEntryPayloads = BattleLootEntryPayload.ProjectEntries(
            battleResolutionResult.loot_entries
        );
        var overflowEntryPayloads = BattleLootEntryPayload.ProjectEntries(
            battleResolutionResult.overflow_entries
        );
        if (lootEntryPayloads.Count == 0 && overflowEntryPayloads.Count == 0)
        {
            DisposeOwnedArray(lootEntryPayloads);
            DisposeOwnedArray(overflowEntryPayloads);
            return new Dictionary();
        }
        return new Dictionary
        {
            ["battle_name"] = battleName,
            ["winner_faction_id"] = winnerFactionId,
            ["loot_entries"] = lootEntryPayloads,
            ["loot_entry_count"] = lootEntryPayloads.Count,
            ["loot_summary_text"] = FormatBattleDropEntriesInternal(
                battleResolutionResult.loot_entries
            ),
            ["overflow_entries"] = overflowEntryPayloads,
            ["overflow_entry_count"] = overflowEntryPayloads.Count,
            ["overflow_summary_text"] = FormatBattleDropEntriesInternal(
                battleResolutionResult.overflow_entries
            ),
            ["commit_ok"] = lootCommitResult?.Ok ?? false,
            ["commit_error_code"] = lootCommitResult?.ErrorCode ?? "",
        };
    }

    private string FormatBattleDropEntriesInternal(IEnumerable<BattleLootEntry> dropEntryOptions)
    {
        var quantitiesByItem = new System.Collections.Generic.Dictionary<string, int>(
            StringComparer.Ordinal
        );
        var orderedItemIds = new List<string>();
        foreach (BattleLootEntry dropEntry in dropEntryOptions ?? System.Array.Empty<BattleLootEntry>())
        {
            var itemId = dropEntry?.ItemId?.ToString() ?? "";
            var quantity = Mathf.Max(dropEntry?.Quantity ?? 0, 0);
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                continue;
            if (!quantitiesByItem.ContainsKey(itemId))
            {
                orderedItemIds.Add(itemId);
                quantitiesByItem[itemId] = 0;
            }
            quantitiesByItem[itemId] += quantity;
        }
        return FormatItemQuantities(quantitiesByItem, orderedItemIds);
    }

    private string FormatBattleDropEntriesInternal(Godot.Collections.Array dropEntryOptions)
    {
        if (dropEntryOptions == null)
            return "";

        var quantitiesByItem = new System.Collections.Generic.Dictionary<string, int>(
            StringComparer.Ordinal
        );
        var orderedItemIds = new List<string>();
        foreach (Variant dropEntryValue in dropEntryOptions)
        {
            try
            {
                if (dropEntryValue.VariantType != Variant.Type.Dictionary)
                    continue;

                Dictionary dropEntryData = dropEntryValue.AsGodotDictionary();
                try
                {
                    var itemId = DictionaryStringNameText(dropEntryData, "item_id", "");
                    var quantity = Mathf.Max(DictionaryInt(dropEntryData, "quantity", 0), 0);
                    if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                        continue;
                    if (!quantitiesByItem.ContainsKey(itemId))
                    {
                        orderedItemIds.Add(itemId);
                        quantitiesByItem[itemId] = 0;
                    }
                    quantitiesByItem[itemId] += quantity;
                }
                finally
                {
                    dropEntryData.Dispose();
                }
            }
            finally
            {
                dropEntryValue.Dispose();
            }
        }
        return FormatItemQuantities(quantitiesByItem, orderedItemIds);
    }

    private string FormatItemQuantities(
        System.Collections.Generic.Dictionary<string, int> quantitiesByItem,
        List<string> orderedItemIds
    )
    {
        var parts = new List<string>();
        foreach (var itemId in orderedItemIds)
            parts.Add(
                string.Format(
                    "{0} x{1}",
                    GetItemDisplayName(itemId),
                    quantitiesByItem[itemId]
                )
            );
        return string.Join("、", parts);
    }

    private static void DisposeOwnedDictionary(Dictionary payload)
    {
        if (payload == null)
            return;
        foreach (Variant value in payload.Values)
        {
            try
            {
                DisposeOwnedVariantPayload(value);
            }
            finally
            {
                value.Dispose();
            }
        }
        payload.Clear();
        payload.Dispose();
    }

    private static void DisposeOwnedArray(GArray payload)
    {
        if (payload == null)
            return;
        foreach (Variant value in payload)
        {
            try
            {
                DisposeOwnedVariantPayload(value);
            }
            finally
            {
                value.Dispose();
            }
        }
        payload.Clear();
        payload.Dispose();
    }

    private static void DisposeOwnedVariantPayload(Variant value)
    {
        if (value.VariantType == Variant.Type.Dictionary)
        {
            DisposeOwnedDictionary(value.AsGodotDictionary());
            return;
        }
        if (value.VariantType == Variant.Type.Array)
        {
            DisposeOwnedArray(value.AsGodotArray());
        }
    }

    private string FormatFactionLabel(string factionId)
    {
        if (_runtime == null)
            return factionId;
        return _runtime.FormatFactionLabel(factionId);
    }

    private string GetItemDisplayName(StringName itemId)
    {
        return GetItemDisplayName(itemId?.ToString() ?? "");
    }

    private string GetItemDisplayName(string itemId)
    {
        if (_runtime == null)
            return itemId ?? "";
        string normalizedItemId = itemId?.StripEdges() ?? "";
        if (string.IsNullOrEmpty(normalizedItemId))
            return "";
        foreach (var itemDefEntry in _runtime.GetItemDefsTyped())
        {
            if (itemDefEntry.Key.ToString() != normalizedItemId)
                continue;
            ItemDef itemDef = itemDefEntry.Value;
            return itemDef != null && !string.IsNullOrEmpty(itemDef.display_name)
                ? itemDef.display_name
                : normalizedItemId;
        }
        return normalizedItemId;
    }

    private static bool DictionaryBool(Dictionary dictionary, string key, bool fallback)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        try
        {
            return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
        }
        finally
        {
            value.Dispose();
        }
    }

    private static int DictionaryInt(Dictionary dictionary, string key, int fallback)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        try
        {
            return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
        }
        finally
        {
            value.Dispose();
        }
    }

    private static string DictionaryString(Dictionary dictionary, string key, string fallback)
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        try
        {
            return value.VariantType switch
            {
                Variant.Type.String => value.AsString(),
                _ => fallback,
            };
        }
        finally
        {
            value.Dispose();
        }
    }

    private static bool TryRead(Dictionary dictionary, string key, out Variant value)
    {
        value = default;
        if (dictionary == null || !dictionary.ContainsKey(key))
            return false;
        value = dictionary[key];
        return value.VariantType != Variant.Type.Nil;
    }

    private static string DictionaryStringNameText(
        Dictionary dictionary,
        string key,
        string fallback
    )
    {
        if (!TryRead(dictionary, key, out Variant value))
            return fallback;
        try
        {
            return value.VariantType == Variant.Type.String ? value.AsString() : fallback;
        }
        finally
        {
            value.Dispose();
        }
    }

    private static Godot.Collections.Array DictionaryArray(
        Dictionary dictionary,
        string key,
        Godot.Collections.Array fallback
    )
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsGodotArray();
    }

    private static bool TryDictionary(Dictionary dictionary, string key, out Dictionary value)
    {
        value = null;
        if (dictionary == null || !dictionary.ContainsKey(key))
            return false;
        var rawValue = dictionary[key];
        if (rawValue.VariantType != Variant.Type.Dictionary)
            return false;
        value = rawValue.AsGodotDictionary();
        return true;
    }

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out GameRuntimeFacade target)
        )
            return null;
        return target;
    }
}
