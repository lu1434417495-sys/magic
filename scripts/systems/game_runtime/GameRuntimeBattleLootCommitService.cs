using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;

internal sealed class GameRuntimeBattleLootCommitService : IDisposable
{
    private WeakReference<IGameRuntimeBattleLootCommitPort> _portRef;

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

    internal sealed class BattleLootCommitResult
    {
        internal bool Ok { get; private set; }
        internal string ErrorCode { get; private set; } = "";
        internal string BlockedItemId { get; private set; } = "";
        internal int CommittedItemCount { get; private set; }
        private readonly List<BattleLootEntry> _overflowEntries = new();
        internal IReadOnlyList<BattleLootEntry> OverflowEntries => _overflowEntries;
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
            var result = new BattleLootCommitResult
            {
                Ok = ok,
                ErrorCode = errorCode ?? "",
                BlockedItemId = blockedItemId ?? "",
                CommittedItemCount = Mathf.Max(committedItemCount, 0),
                OverflowEntryCount = Mathf.Max(overflowEntryCount, 0),
            };
            foreach (BattleLootEntry entry in overflowEntries ?? System.Array.Empty<BattleLootEntry>())
            {
                BattleLootEntry duplicate = entry?.Duplicate();
                if (duplicate == null || duplicate.IsEmpty)
                    continue;
                result._overflowEntries.Add(duplicate);
            }
            result.OverflowEntryCount = Mathf.Max(result.OverflowEntryCount, result._overflowEntries.Count);
            return result;
        }

        internal static BattleLootCommitResult Success() =>
            Create(true, "", "", 0, System.Array.Empty<BattleLootEntry>(), 0);

        internal GArray ProjectOverflowEntries() =>
            BattleLootEntryPayload.ProjectEntries(_overflowEntries);

    }

    private IGameRuntimeBattleLootCommitPort _port
    {
        get => ResolveWeakRef(_portRef);
        set =>
            _portRef =
                value != null
                    ? new WeakReference<IGameRuntimeBattleLootCommitPort>(value)
                    : null;
    }

    internal void Setup(IGameRuntimeBattleLootCommitPort port)
    {
        _port = port;
    }

    public void Dispose()
    {
        _port = null;
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
        if (battleResolutionResult.outcome != BattleOutcomeKind.PlayerSuccess)
            return BattleLootCommitResult.Success();

        if (_port == null || !_port.TryPrepareBattleLootCommit())
            return BattleLootCommitResult.Create(
                false,
                "warehouse_service_unavailable",
                "",
                0,
                System.Array.Empty<BattleLootEntry>(),
                0
            );

        IBattleLootCommitCheckpoint commitCheckpoint =
            _port.CaptureBattleLootCommitCheckpoint();

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

            IBattleLootCommitCheckpoint entryCheckpoint =
                _port.CaptureBattleLootCommitCheckpoint();

            if (lootEntry.DropKind == BattleLootDropKind.EquipmentInstance)
            {
                var instanceCommitResult = CommitEquipmentInstanceLootEntry(lootEntry);
                if (!instanceCommitResult.Ok)
                {
                    if (IsFatalLootCommitError(instanceCommitResult.ErrorCode))
                    {
                        _port.RestoreBattleLootCommitCheckpoint(commitCheckpoint);
                        return BattleLootCommitResult.Create(
                            false,
                            instanceCommitResult.ErrorCode,
                            instanceCommitResult.BlockedItemId,
                            0,
                            System.Array.Empty<BattleLootEntry>(),
                            0
                        );
                    }
                    _port.RestoreBattleLootCommitCheckpoint(entryCheckpoint);
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
                        _port.RestoreBattleLootCommitCheckpoint(commitCheckpoint);
                        return BattleLootCommitResult.Create(
                            false,
                            equipmentCommitResult.ErrorCode,
                            equipmentCommitResult.BlockedItemId,
                            0,
                            System.Array.Empty<BattleLootEntry>(),
                            0
                        );
                    }
                    _port.RestoreBattleLootCommitCheckpoint(entryCheckpoint);
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
                    _port.RestoreBattleLootCommitCheckpoint(commitCheckpoint);
                    return BattleLootCommitResult.Create(
                        false,
                        itemCommitResult.ErrorCode,
                        itemCommitResult.BlockedItemId,
                        0,
                        System.Array.Empty<BattleLootEntry>(),
                        0
                    );
                }
                _port.RestoreBattleLootCommitCheckpoint(entryCheckpoint);
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

    private ItemCommitResult CommitFixedItemLootEntry(BattleLootEntry lootEntry)
    {
        if (lootEntry == null || lootEntry.DropKind != BattleLootDropKind.Item)
            return ItemCommitResult.Create(true, "", "", 0, System.Array.Empty<BattleLootEntry>());
        var itemId = lootEntry.ItemId;
        var quantity = lootEntry.Quantity;
        if (itemId == "" || quantity <= 0)
            return ItemCommitResult.Create(true, "", "", 0, System.Array.Empty<BattleLootEntry>());
        var addResult = _port.AddBattleLootItem(itemId, quantity);
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
        BattleLootItemDefinitionKind itemKind =
            _port.ResolveBattleLootItemDefinitionKind(itemId);
        if (itemKind == BattleLootItemDefinitionKind.Missing)
            return ItemCommitResult.Create(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        if (itemKind != BattleLootItemDefinitionKind.Equipment)
            return ItemCommitResult.Create(
                false,
                "battle_loot_random_equipment_invalid_item",
                itemId.ToString(),
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        if (
            !_port.TryRollBattleLootEquipment(
                itemId,
                quantity,
                dropLuck,
                out IReadOnlyList<EquipmentInstanceState> rolledInstances
            )
        )
            return ItemCommitResult.Create(
                false,
                "battle_loot_random_equipment_service_unavailable",
                itemId.ToString(),
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        var committedItemCount = 0;
        var overflowQuantity = 0;
        foreach (EquipmentInstanceState rolledInstance in rolledInstances)
        {
            if (rolledInstance == null)
                continue;
            var rolledItemId = rolledInstance?.item_id ?? new StringName("");
            var addResult = _port.AddBattleLootEquipmentInstance(rolledInstance);
            if (!addResult.ItemFound)
                return ItemCommitResult.Create(
                    false,
                    "battle_loot_item_missing_def",
                    rolledItemId.ToString(),
                    0,
                    System.Array.Empty<BattleLootEntry>()
                );
            if (!addResult.IsEquipment)
                return ItemCommitResult.Create(
                    false,
                    "battle_loot_random_equipment_invalid_item",
                    rolledItemId.ToString(),
                    0,
                    System.Array.Empty<BattleLootEntry>()
                );
            if (addResult.RemainingQuantity > 0)
            {
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
        var itemId = ProgressionDataUtils.to_string_name(equipmentInstance?.item_id ?? new StringName(""));
        if (itemId == "")
            return ItemCommitResult.Create(
                false,
                "battle_loot_equipment_instance_invalid_payload",
                "",
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        BattleLootItemDefinitionKind itemKind =
            _port.ResolveBattleLootItemDefinitionKind(itemId);
        if (itemKind == BattleLootItemDefinitionKind.Missing)
            return ItemCommitResult.Create(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        if (itemKind != BattleLootItemDefinitionKind.Equipment)
            return ItemCommitResult.Create(
                false,
                "battle_loot_random_equipment_invalid_item",
                itemId.ToString(),
                0,
                System.Array.Empty<BattleLootEntry>()
            );
        var addResult = _port.AddBattleLootEquipmentInstance(equipmentInstance);
        if (addResult.RemainingQuantity > 0)
            return ItemCommitResult.Create(
                true,
                "",
                "",
                0,
                new[] { BuildBattleOverflowEntry(lootEntry, 1) }
            );
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
        var context = new Dictionary
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
        if (_port == null)
            return 0;
        var shardCount = 0;
        for (
            int slotIndex = 0;
            slotIndex < BattleLootIds.OrdinaryBattleCalamityShardChapterCap;
            slotIndex++
        )
        {
            var flagId = BuildRegularBattleCalamityShardFlagId(slotIndex);
            if (_port.GetBattleLootFateRunFlag(flagId))
                shardCount++;
        }
        return shardCount;
    }

    private void MarkRegularBattleCalamityShardsCommitted(int quantity)
    {
        if (_port == null || quantity <= 0)
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
            if (_port.GetBattleLootFateRunFlag(flagId))
                continue;
            _port.SetBattleLootFateRunFlag(flagId);
            remainingToMark--;
            if (remainingToMark <= 0)
                return;
        }
    }

    private void ClearRegularBattleCalamityShardFlagsInternal()
    {
        if (_port == null)
            return;
        for (
            int slotIndex = 0;
            slotIndex < BattleLootIds.OrdinaryBattleCalamityShardChapterCap;
            slotIndex++
        )
            _port.ClearBattleLootFateRunFlag(
                BuildRegularBattleCalamityShardFlagId(slotIndex)
            );
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
                    GetItemDisplayName(new StringName(lootCommitResult.BlockedItemId))
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
            return new Dictionary();
        return new Dictionary
        {
            ["battle_name"] = battleName,
            ["objective_mode"] = BattleObjectiveRuntimeCodec.ToWireValue(
                battleResolutionResult.objective_mode
            ),
            ["outcome"] = BattleObjectiveRuntimeCodec.ToWireValue(
                battleResolutionResult.outcome
            ),
            ["end_reason"] = BattleObjectiveRuntimeCodec.ToWireValue(
                battleResolutionResult.end_reason
            ),
            ["decision_tu"] = battleResolutionResult.final_decision?.DecisionTu ?? -1,
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
        var quantitiesByItem = new Dictionary();
        var orderedItemIds = new Array<StringName>();
        foreach (BattleLootEntry dropEntry in dropEntryOptions ?? System.Array.Empty<BattleLootEntry>())
        {
            var itemId = dropEntry?.ItemId ?? new StringName("");
            var quantity = Mathf.Max(dropEntry?.Quantity ?? 0, 0);
            if (itemId == "" || quantity <= 0)
                continue;
            if (!quantitiesByItem.ContainsKey(itemId))
            {
                orderedItemIds.Add(itemId);
                quantitiesByItem[itemId] = 0;
            }
            quantitiesByItem[itemId] = quantitiesByItem[itemId].AsInt32() + quantity;
        }
        return FormatItemQuantities(quantitiesByItem, orderedItemIds);
    }

    private string FormatBattleDropEntriesInternal(Godot.Collections.Array dropEntryOptions)
    {
        var quantitiesByItem = new Dictionary();
        var orderedItemIds = new Array<StringName>();
        foreach (var dropEntryValue in dropEntryOptions)
        {
            if (dropEntryValue.VariantType != Variant.Type.Dictionary)
                continue;
            var dropEntryData = dropEntryValue.AsGodotDictionary();
            var itemId = DictionaryStringName(dropEntryData, "item_id", "");
            var quantity = Mathf.Max(DictionaryInt(dropEntryData, "quantity", 0), 0);
            if (itemId == "" || quantity <= 0)
                continue;
            if (!quantitiesByItem.ContainsKey(itemId))
            {
                orderedItemIds.Add(itemId);
                quantitiesByItem[itemId] = 0;
            }
            quantitiesByItem[itemId] = quantitiesByItem[itemId].AsInt32() + quantity;
        }
        return FormatItemQuantities(quantitiesByItem, orderedItemIds);
    }

    private string FormatItemQuantities(Dictionary quantitiesByItem, Array<StringName> orderedItemIds)
    {
        var parts = new Array<string>();
        foreach (var itemId in orderedItemIds)
            parts.Add(
                string.Format(
                    "{0} x{1}",
                    GetItemDisplayName(itemId),
                    quantitiesByItem[itemId].AsInt32()
                )
            );
        return string.Join("、", parts);
    }

    private string FormatFactionLabel(string factionId)
    {
        if (_port == null)
            return factionId;
        return _port.FormatBattleLootFactionLabel(factionId);
    }

    private string GetItemDisplayName(StringName itemId)
    {
        if (_port == null)
            return itemId.ToString();
        return _port.GetBattleLootItemDisplayName(itemId);
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
        if (!TryRead(dictionary, key, out Variant value) || value.VariantType != Variant.Type.String)
            return fallback;
        return new StringName(value.AsString());
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

    private static IGameRuntimeBattleLootCommitPort ResolveWeakRef(
        WeakReference<IGameRuntimeBattleLootCommitPort> weakRef
    )
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out IGameRuntimeBattleLootCommitPort target)
        )
            return null;
        return target;
    }
}
