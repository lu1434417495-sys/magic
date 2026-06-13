using System;
using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;

[GlobalClass]
internal partial class GameRuntimeBattleLootCommitService : RefCounted
{
    private WeakReference<GameRuntimeFacade> _runtimeRef;

    internal sealed class ItemCommitResult
    {
        internal bool Ok { get; private set; }
        internal string ErrorCode { get; private set; } = "";
        internal string BlockedItemId { get; private set; } = "";
        internal int CommittedItemCount { get; private set; }
        internal GArray OverflowEntries { get; private set; } = new();

        internal static ItemCommitResult Create(
            bool ok,
            string errorCode,
            string blockedItemId,
            int committedItemCount,
            GArray overflowEntries
        )
        {
            return new ItemCommitResult
            {
                Ok = ok,
                ErrorCode = errorCode ?? "",
                BlockedItemId = blockedItemId ?? "",
                CommittedItemCount = Mathf.Max(committedItemCount, 0),
                OverflowEntries = overflowEntries?.Duplicate(true) ?? new GArray(),
            };
        }

    }

    internal sealed class BattleLootCommitResult
    {
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
            GArray overflowEntries,
            int overflowEntryCount
        )
        {
            var normalizedOverflowEntries = overflowEntries?.Duplicate(true) ?? new GArray();
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

        internal static BattleLootCommitResult Success() => Create(true, "", "", 0, new GArray(), 0);

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

    internal new void Dispose()
    {
        _runtime = null;
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
                new GArray(),
                0
            );

        battleResolutionResult.SetOverflowEntries(new GArray());
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
                new GArray(),
                0
            );

        var itemDefs = gameSession.GetItemDefsTyped();
        _runtime.SetupPartyWarehouseService(partyWarehouseService, partyState, itemDefs);

        var warehouseState = partyState.warehouse_state;
        WarehouseState warehouseStateBefore = null;
        if (warehouseState != null)
            warehouseStateBefore = warehouseState.DuplicateState();

        var fateRunFlagsBefore = new Dictionary();
        if (partyState != null)
            fateRunFlagsBefore = partyState.CaptureFateRunFlags().Duplicate(true);

        var overflowEntries = new GArray();
        var committedItemCount = 0;
        var effectiveLootEntries = ResolveEffectiveBattleLootEntriesForCommit(
            battleResolutionResult
        );
        battleResolutionResult.SetLootEntries(effectiveLootEntries);

        foreach (var lootEntryValue in battleResolutionResult.loot_entries)
        {
            if (lootEntryValue.VariantType != Variant.Type.Dictionary)
                continue;
            var lootEntryData = lootEntryValue.AsGodotDictionary();
            var dropType = DictionaryStringName(
                lootEntryData,
                "drop_type",
                BattleLootIds.ToStringName(BattleLootDropKind.Item)
            );
            BattleLootDropKind dropKind = BattleLootIds.ToDropKind(dropType);

            if (dropKind == BattleLootDropKind.EquipmentInstance)
            {
                var instanceCommitResult = CommitEquipmentInstanceLootEntry(lootEntryData);
                if (!instanceCommitResult.Ok)
                {
                    partyState.warehouse_state = warehouseStateBefore;
                    partyState.ApplyFateRunFlags(fateRunFlagsBefore);
                    _runtime.SetupPartyWarehouseService(
                        partyWarehouseService,
                        partyState,
                        itemDefs
                    );
                    return BattleLootCommitResult.Create(
                        false,
                        FallbackString(
                            instanceCommitResult.ErrorCode,
                            "battle_loot_equipment_instance_failed"
                        ),
                        instanceCommitResult.BlockedItemId,
                        0,
                        new GArray(),
                        0
                    );
                }
                committedItemCount += instanceCommitResult.CommittedItemCount;
                AppendOverflowEntries(overflowEntries, instanceCommitResult.OverflowEntries);
                continue;
            }

            if (dropKind == BattleLootDropKind.RandomEquipment)
            {
                var equipmentCommitResult = CommitRandomEquipmentLootEntry(lootEntryData);
                if (!equipmentCommitResult.Ok)
                {
                    partyState.warehouse_state = warehouseStateBefore;
                    partyState.ApplyFateRunFlags(fateRunFlagsBefore);
                    _runtime.SetupPartyWarehouseService(
                        partyWarehouseService,
                        partyState,
                        itemDefs
                    );
                    return BattleLootCommitResult.Create(
                        false,
                        FallbackString(
                            equipmentCommitResult.ErrorCode,
                            "battle_loot_random_equipment_failed"
                        ),
                        equipmentCommitResult.BlockedItemId,
                        0,
                        new GArray(),
                        0
                    );
                }
                committedItemCount += equipmentCommitResult.CommittedItemCount;
                AppendOverflowEntries(overflowEntries, equipmentCommitResult.OverflowEntries);
                continue;
            }

            var itemCommitResult = CommitFixedItemLootEntry(lootEntryData);
            if (!itemCommitResult.Ok)
            {
                partyState.warehouse_state = warehouseStateBefore;
                partyState.ApplyFateRunFlags(fateRunFlagsBefore);
                _runtime.SetupPartyWarehouseService(
                    partyWarehouseService,
                    partyState,
                    itemDefs
                );
                return BattleLootCommitResult.Create(
                    false,
                    FallbackString(itemCommitResult.ErrorCode, "battle_loot_item_missing_def"),
                    itemCommitResult.BlockedItemId,
                    0,
                    new GArray(),
                    0
                );
            }
            committedItemCount += itemCommitResult.CommittedItemCount;
            if (IsOrdinaryBattleCalamityConversionEntry(lootEntryData))
                MarkRegularBattleCalamityShardsCommitted(itemCommitResult.CommittedItemCount);
            AppendOverflowEntries(overflowEntries, itemCommitResult.OverflowEntries);
        }

        battleResolutionResult.SetOverflowEntries(overflowEntries);
        var overflowItemId = "";
        if (
            battleResolutionResult.overflow_entries.Count > 0
            && battleResolutionResult.overflow_entries[0].VariantType == Variant.Type.Dictionary
        )
            overflowItemId = DictionaryString(
                battleResolutionResult.overflow_entries[0].AsGodotDictionary(),
                "item_id",
                ""
            );
        return BattleLootCommitResult.Create(
            true,
            "",
            overflowItemId,
            committedItemCount,
            battleResolutionResult.overflow_entries.Duplicate(true),
            battleResolutionResult.overflow_entries.Count
        );
    }

    private ItemCommitResult CommitFixedItemLootEntry(Dictionary lootEntryData)
    {
        if (
            !TryNormalizeLootEntryOfType(
                lootEntryData,
                BattleLootDropKind.Item,
                out Dictionary normalizedEntry
            )
        )
            return ItemCommitResult.Create(true, "", "", 0, new GArray());
        var itemId = ReadRequiredStringName(normalizedEntry, "item_id");
        var quantity = ReadRequiredInt(normalizedEntry, "quantity");
        if (itemId == "" || quantity <= 0)
            return ItemCommitResult.Create(true, "", "", 0, new GArray());
        var partyWarehouseService = _runtime._party_warehouse_service;
        var addResult = partyWarehouseService.AddItemTyped(itemId, quantity);
        if (!addResult.ItemFound)
            return ItemCommitResult.Create(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                new GArray()
            );
        var overflowEntries = new GArray();
        var remainingQuantity = Mathf.Max(addResult.RemainingQuantity, 0);
        if (remainingQuantity > 0)
            overflowEntries.Add(BuildBattleOverflowEntry(normalizedEntry, remainingQuantity));
        return ItemCommitResult.Create(
            true,
            "",
            "",
            Mathf.Max(addResult.AddedQuantity, 0),
            overflowEntries
        );
    }

    private ItemCommitResult CommitRandomEquipmentLootEntry(Dictionary lootEntryData)
    {
        if (
            !TryNormalizeLootEntryOfType(
                lootEntryData,
                BattleLootDropKind.RandomEquipment,
                out Dictionary normalizedEntry
            )
        )
            return ItemCommitResult.Create(true, "", "", 0, new GArray());
        var itemId = ReadRequiredStringName(normalizedEntry, "item_id");
        var quantity = ReadRequiredInt(normalizedEntry, "quantity");
        var dropLuck = Mathf.Clamp(ReadRequiredInt(normalizedEntry, "drop_luck"), -6, 5);
        if (itemId == "" || quantity <= 0)
            return ItemCommitResult.Create(true, "", "", 0, new GArray());
        var gameSession = _runtime._game_session;
        var itemDefs = gameSession.GetItemDefsTyped();
        itemDefs.TryGetValue(itemId, out ItemDef itemDef);
        if (itemDef == null)
            return ItemCommitResult.Create(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                new GArray()
            );
        if (!itemDef.IsEquipment())
            return ItemCommitResult.Create(
                false,
                "battle_loot_random_equipment_invalid_item",
                itemId.ToString(),
                0,
                new GArray()
            );
        var equipmentDropService = _runtime._equipment_drop_service;
        if (equipmentDropService == null)
            return ItemCommitResult.Create(
                false,
                "battle_loot_random_equipment_service_unavailable",
                itemId.ToString(),
                0,
                new GArray()
            );
        var rolledInstances = equipmentDropService.RollItemInstances(itemId, quantity, dropLuck);
        var committedItemCount = 0;
        var overflowQuantity = 0;
        foreach (EquipmentInstanceState rolledInstance in rolledInstances)
        {
            if (rolledInstance == null)
                continue;
            var rolledItemId = rolledInstance?.item_id ?? new StringName("");
            var partyWarehouseService = _runtime._party_warehouse_service;
            var addResult = partyWarehouseService.AddEquipmentInstanceTyped(rolledInstance);
            if (!addResult.ItemFound)
                return ItemCommitResult.Create(
                    false,
                    "battle_loot_item_missing_def",
                    rolledItemId.ToString(),
                    0,
                    new GArray()
                );
            if (!addResult.IsEquipment)
                return ItemCommitResult.Create(
                    false,
                    "battle_loot_random_equipment_invalid_item",
                    rolledItemId.ToString(),
                    0,
                    new GArray()
                );
            if (addResult.RemainingQuantity > 0)
            {
                overflowQuantity++;
                continue;
            }
            committedItemCount++;
        }
        var overflowEntries = new GArray();
        if (overflowQuantity > 0)
            overflowEntries.Add(BuildBattleOverflowEntry(normalizedEntry, overflowQuantity));
        return ItemCommitResult.Create(true, "", "", committedItemCount, overflowEntries);
    }

    internal ItemCommitResult CommitEquipmentInstanceLootEntry(Dictionary lootEntryData)
    {
        if (
            !TryNormalizeLootEntryOfType(
                lootEntryData,
                BattleLootDropKind.EquipmentInstance,
                out Dictionary normalizedEntry
            )
        )
            return ItemCommitResult.Create(
                false,
                "battle_loot_equipment_instance_invalid_payload",
                DictionaryString(lootEntryData, "item_id", ""),
                0,
                new GArray()
            );
        if (!TryDictionary(normalizedEntry, "equipment_instance", out var equipmentInstanceData))
            return ItemCommitResult.Create(
                false,
                "battle_loot_equipment_instance_missing_payload",
                DictionaryString(normalizedEntry, "item_id", ""),
                0,
                new GArray()
            );
        var equipmentInstance = EquipmentInstanceState.FromDictionary(equipmentInstanceData);
        if (equipmentInstance == null)
            return ItemCommitResult.Create(
                false,
                "battle_loot_equipment_instance_invalid_payload",
                DictionaryString(normalizedEntry, "item_id", ""),
                0,
                new GArray()
            );
        var itemId = ProgressionDataUtils.to_string_name(equipmentInstance.item_id);
        if (itemId == "")
            return ItemCommitResult.Create(
                false,
                "battle_loot_equipment_instance_invalid_payload",
                "",
                0,
                new GArray()
            );
        var gameSession = _runtime._game_session;
        var itemDefs = gameSession.GetItemDefsTyped();
        itemDefs.TryGetValue(itemId, out ItemDef itemDef);
        if (itemDef == null)
            return ItemCommitResult.Create(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                new GArray()
            );
        if (!itemDef.IsEquipment())
            return ItemCommitResult.Create(
                false,
                "battle_loot_random_equipment_invalid_item",
                itemId.ToString(),
                0,
                new GArray()
            );
        var partyWarehouseService = _runtime._party_warehouse_service;
        var addResult = partyWarehouseService.AddEquipmentInstanceTyped(equipmentInstance);
        if (addResult.RemainingQuantity > 0)
            return ItemCommitResult.Create(
                true,
                "",
                "",
                0,
                new GArray { BuildBattleOverflowEntry(normalizedEntry, 1) }
            );
        return ItemCommitResult.Create(true, "", "", 1, new GArray());
    }

    private Dictionary BuildBattleOverflowEntry(Dictionary lootEntryData, int overflowQuantity)
    {
        var overflowEntry = lootEntryData.Duplicate(true);
        overflowEntry["quantity"] = Mathf.Max(overflowQuantity, 0);
        return overflowEntry;
    }

    private static void AppendOverflowEntries(GArray target, GArray source)
    {
        if (target == null || source == null)
            return;
        foreach (var overflowValue in source)
        {
            if (overflowValue.VariantType == Variant.Type.Dictionary)
                target.Add(overflowValue.AsGodotDictionary().Duplicate(true));
        }
    }

    private static string FallbackString(string value, string fallback)
    {
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private Godot.Collections.Array ResolveEffectiveBattleLootEntriesForCommit(
        BattleResolutionResult battleResolutionResult
    )
    {
        var adjustedEntries = new Godot.Collections.Array();
        if (battleResolutionResult == null)
            return adjustedEntries;
        var remainingRegularCap = GetRemainingRegularBattleCalamityShardCap();
        var mergeIndexByKey = new Dictionary();
        foreach (var lootEntryValue in battleResolutionResult.loot_entries)
        {
            if (lootEntryValue.VariantType != Variant.Type.Dictionary)
                continue;
            var lootEntry = BattleResolutionResult.NormalizeFormalDropEntryPayload(
                lootEntryValue.AsGodotDictionary()
            );
            if (lootEntry == null)
                continue;
            if (IsOrdinaryBattleCalamityConversionEntry(lootEntry))
            {
                var allowedQuantity = Mathf.Min(
                    Mathf.Max(DictionaryInt(lootEntry, "quantity", 0), 0),
                    remainingRegularCap
                );
                remainingRegularCap = Mathf.Max(remainingRegularCap - allowedQuantity, 0);
                if (allowedQuantity <= 0)
                    continue;
                lootEntry["quantity"] = allowedQuantity;
            }
            var mergeKey = BuildBattleLootMergeKey(lootEntry);
            if (!string.IsNullOrEmpty(mergeKey) && mergeIndexByKey.ContainsKey(mergeKey))
            {
                var entryIndex = mergeIndexByKey[mergeKey].AsInt32();
                if (entryIndex >= 0 && entryIndex < adjustedEntries.Count)
                {
                    var mergedEntry = adjustedEntries[entryIndex]
                        .AsGodotDictionary()
                        .Duplicate(true);
                    mergedEntry["quantity"] =
                        DictionaryInt(mergedEntry, "quantity", 0)
                        + DictionaryInt(lootEntry, "quantity", 0);
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

    private static bool TryNormalizeLootEntryOfType(
        Dictionary lootEntryData,
        BattleLootDropKind expectedDropKind,
        out Dictionary normalizedEntry
    )
    {
        normalizedEntry = BattleResolutionResult.NormalizeFormalDropEntryPayload(lootEntryData);
        if (normalizedEntry == null)
            return false;
        return BattleLootIds.ToDropKind(ReadRequiredStringName(normalizedEntry, "drop_type"))
            == expectedDropKind;
    }

    private static StringName ReadRequiredStringName(Dictionary source, string key)
    {
        if (!TryRead(source, key, out Variant value) || value.VariantType != Variant.Type.String)
            return "";
        return new StringName(value.AsString());
    }

    private static int ReadRequiredInt(Dictionary source, string key)
    {
        if (source == null || !source.ContainsKey(key))
            return 0;
        return source[key].AsInt32();
    }

    private string BuildBattleLootMergeKey(Dictionary lootEntryData)
    {
        if (lootEntryData == null || lootEntryData.Count == 0)
            return "";
        var dropType = DictionaryStringName(lootEntryData, "drop_type", "");
        BattleLootDropKind dropKind = BattleLootIds.ToDropKind(dropType);
        var itemId = DictionaryStringName(lootEntryData, "item_id", "");
        if (itemId == "")
            return "";
        if (dropKind == BattleLootDropKind.Item)
            return string.Format("{0}|{1}", dropType, itemId);
        if (dropKind == BattleLootDropKind.RandomEquipment)
            return string.Format(
                "{0}|{1}|{2}",
                dropType,
                itemId,
                Mathf.Clamp(DictionaryInt(lootEntryData, "drop_luck", 0), -6, 5)
            );
        return "";
    }

    private bool IsOrdinaryBattleCalamityConversionEntry(Dictionary lootEntryData)
    {
        if (lootEntryData == null || lootEntryData.Count == 0)
            return false;
        var itemId = DictionaryStringName(lootEntryData, "item_id", "");
        var dropSourceKind = DictionaryStringName(lootEntryData, "drop_source_kind", "");
        var dropSourceId = DictionaryStringName(lootEntryData, "drop_source_id", "");
        return BattleLootIds.ToSpecialItemKind(itemId) == BattleLootSpecialItemKind.CalamityShard
            && BattleLootIds.ToSourceKind(dropSourceKind) == BattleLootSourceKind.CalamityConversion
            && BattleLootIds.ToSourceIdKind(dropSourceId) == BattleLootSourceIdKind.OrdinaryBattle;
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
        var lootEntries = battleResolutionResult.loot_entries.Duplicate(true);
        var overflowEntries = battleResolutionResult.overflow_entries.Duplicate(true);
        if (lootEntries.Count == 0 && overflowEntries.Count == 0)
            return new Dictionary();
        return new Dictionary
        {
            ["battle_name"] = battleName,
            ["winner_faction_id"] = winnerFactionId,
            ["loot_entries"] = lootEntries,
            ["loot_entry_count"] = lootEntries.Count,
            ["loot_summary_text"] = FormatBattleDropEntriesInternal(lootEntries),
            ["overflow_entries"] = overflowEntries,
            ["overflow_entry_count"] = overflowEntries.Count,
            ["overflow_summary_text"] = FormatBattleDropEntriesInternal(overflowEntries),
            ["commit_ok"] = lootCommitResult?.Ok ?? false,
            ["commit_error_code"] = lootCommitResult?.ErrorCode ?? "",
        };
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
        if (_runtime == null)
            return factionId;
        return _runtime.FormatFactionLabel(factionId);
    }

    private string GetItemDisplayName(StringName itemId)
    {
        if (_runtime == null)
            return itemId.ToString();
        return _runtime.GetItemDisplayName(itemId);
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
