using System;
using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class GameRuntimeBattleLootCommitService : RefCounted
{
    private WeakReference<GameRuntimeFacade> _runtimeRef;

    private sealed class ItemCommitResult
    {
        public bool Ok { get; private set; }
        public string ErrorCode { get; private set; } = "";
        public string BlockedItemId { get; private set; } = "";
        public int CommittedItemCount { get; private set; }
        public GArray OverflowEntries { get; private set; } = new();

        public static ItemCommitResult Create(
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

        public Dictionary ToDictionary()
        {
            return new Dictionary
            {
                ["ok"] = Ok,
                ["error_code"] = ErrorCode,
                ["blocked_item_id"] = BlockedItemId,
                ["committed_item_count"] = CommittedItemCount,
                ["overflow_entries"] = OverflowEntries.Duplicate(true),
            };
        }
    }

    public sealed class BattleLootCommitResult
    {
        public bool Ok { get; private set; }
        public string ErrorCode { get; private set; } = "";
        public string BlockedItemId { get; private set; } = "";
        public int CommittedItemCount { get; private set; }
        public GArray OverflowEntries { get; private set; } = new();
        public int OverflowEntryCount { get; private set; }

        public static BattleLootCommitResult Create(
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

        public static BattleLootCommitResult Success() => Create(true, "", "", 0, new GArray(), 0);

        public static BattleLootCommitResult FromDictionary(Dictionary payload)
        {
            if (payload == null || payload.Count == 0)
                return Create(false, "", "", 0, new GArray(), 0);
            return Create(
                DictionaryBool(payload, "ok", false),
                DictionaryString(payload, "error_code", ""),
                DictionaryString(payload, "blocked_item_id", ""),
                DictionaryInt(payload, "committed_item_count", 0),
                DictionaryArray(payload, "overflow_entries", new GArray()),
                DictionaryInt(payload, "overflow_entry_count", 0)
            );
        }

        public Dictionary ToDictionary()
        {
            return new Dictionary
            {
                ["ok"] = Ok,
                ["error_code"] = ErrorCode,
                ["blocked_item_id"] = BlockedItemId,
                ["committed_item_count"] = CommittedItemCount,
                ["overflow_entries"] = OverflowEntries.Duplicate(true),
                ["overflow_entry_count"] = OverflowEntryCount,
            };
        }
    }

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

    public new void Dispose()
    {
        _runtime = null;
    }

    public void dispose()
    {
        Dispose();
    }

    public Dictionary CommitBattleLootToSharedWarehouse(
        BattleResolutionResult battleResolutionResult
    )
    {
        return CommitBattleLootToSharedWarehouseTyped(battleResolutionResult).ToDictionary();
    }

    public BattleLootCommitResult CommitBattleLootToSharedWarehouseTyped(
        BattleResolutionResult battleResolutionResult
    )
    {
        return CommitBattleLootToSharedWarehouseInternal(battleResolutionResult);
    }

    public Dictionary commit_battle_loot_to_shared_warehouse(
        BattleResolutionResult battleResolutionResult
    )
    {
        return CommitBattleLootToSharedWarehouse(battleResolutionResult);
    }

    public void ClearRegularBattleCalamityShardFlags()
    {
        ClearRegularBattleCalamityShardFlagsInternal();
    }

    public void clear_regular_battle_calamity_shard_flags()
    {
        ClearRegularBattleCalamityShardFlags();
    }

    public string BuildBattleResolutionStatusMessage(
        string battleName,
        string winnerFactionId,
        Dictionary lootCommitResult,
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

    public string BuildBattleResolutionStatusMessageTyped(
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

    public string build_battle_resolution_status_message(
        string battleName,
        string winnerFactionId,
        Dictionary lootCommitResult,
        bool persistedOk
    )
    {
        return BuildBattleResolutionStatusMessage(
            battleName,
            winnerFactionId,
            lootCommitResult,
            persistedOk
        );
    }

    public Dictionary BuildLastBattleLootSnapshot(
        string battleName,
        string winnerFactionId,
        BattleResolutionResult battleResolutionResult,
        Dictionary lootCommitResult
    )
    {
        return BuildLastBattleLootSnapshotInternal(
            battleName,
            winnerFactionId,
            battleResolutionResult,
            lootCommitResult
        );
    }

    public Dictionary BuildLastBattleLootSnapshotTyped(
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

    public Dictionary build_last_battle_loot_snapshot(
        string battleName,
        string winnerFactionId,
        BattleResolutionResult battleResolutionResult,
        Dictionary lootCommitResult
    )
    {
        return BuildLastBattleLootSnapshot(
            battleName,
            winnerFactionId,
            battleResolutionResult,
            lootCommitResult
        );
    }

    public string FormatBattleDropEntries(Godot.Collections.Array dropEntryOptions)
    {
        return FormatBattleDropEntriesInternal(dropEntryOptions);
    }

    public string format_battle_drop_entries(Godot.Collections.Array dropEntryOptions)
    {
        return FormatBattleDropEntries(dropEntryOptions);
    }

    public Dictionary _commit_fixed_item_loot_entry(Dictionary lootEntryData)
    {
        return CommitFixedItemLootEntry(lootEntryData).ToDictionary();
    }

    public Dictionary _commit_equipment_instance_loot_entry(Dictionary lootEntryData)
    {
        return CommitEquipmentInstanceLootEntry(lootEntryData).ToDictionary();
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

        battleResolutionResult.set_overflow_entries(new GArray());
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

        var itemDefs = gameSession.get_item_defs();
        _runtime._setup_party_warehouse_service(partyWarehouseService, partyState, itemDefs);

        var warehouseState = partyState.warehouse_state;
        WarehouseState warehouseStateBefore = null;
        if (warehouseState != null)
            warehouseStateBefore = warehouseState.duplicate_state();

        var fateRunFlagsBefore = new Dictionary();
        if (partyState != null)
            fateRunFlagsBefore = partyState.capture_fate_run_flags().Duplicate(true);

        var overflowEntries = new GArray();
        var committedItemCount = 0;
        var effectiveLootEntries = ResolveEffectiveBattleLootEntriesForCommit(
            battleResolutionResult
        );
        battleResolutionResult.set_loot_entries(effectiveLootEntries);

        foreach (var lootEntryValue in battleResolutionResult.loot_entries)
        {
            if (lootEntryValue.VariantType != Variant.Type.Dictionary)
                continue;
            var lootEntryData = lootEntryValue.AsGodotDictionary();
            var dropType = DictionaryStringName(
                lootEntryData,
                "drop_type",
                BattleLootConstants.DROP_TYPE_ITEM()
            );

            if (dropType == BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE())
            {
                var instanceCommitResult = CommitEquipmentInstanceLootEntry(lootEntryData);
                if (!instanceCommitResult.Ok)
                {
                    partyState.warehouse_state = warehouseStateBefore;
                    partyState.apply_fate_run_flags(fateRunFlagsBefore);
                    _runtime._setup_party_warehouse_service(
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

            if (dropType == BattleLootConstants.DROP_TYPE_RANDOM_EQUIPMENT())
            {
                var equipmentCommitResult = CommitRandomEquipmentLootEntry(lootEntryData);
                if (!equipmentCommitResult.Ok)
                {
                    partyState.warehouse_state = warehouseStateBefore;
                    partyState.apply_fate_run_flags(fateRunFlagsBefore);
                    _runtime._setup_party_warehouse_service(
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
                partyState.apply_fate_run_flags(fateRunFlagsBefore);
                _runtime._setup_party_warehouse_service(
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

        battleResolutionResult.set_overflow_entries(overflowEntries);
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
        var itemId = DictionaryStringName(lootEntryData, "item_id", "");
        var quantity = Mathf.Max(DictionaryInt(lootEntryData, "quantity", 0), 0);
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
            overflowEntries.Add(BuildBattleOverflowEntry(lootEntryData, remainingQuantity));
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
        var itemId = DictionaryStringName(lootEntryData, "item_id", "");
        var quantity = Mathf.Max(DictionaryInt(lootEntryData, "quantity", 0), 0);
        var dropLuck = Mathf.Clamp(DictionaryInt(lootEntryData, "drop_luck", 0), -6, 5);
        if (itemId == "" || quantity <= 0)
            return ItemCommitResult.Create(true, "", "", 0, new GArray());
        var gameSession = _runtime._game_session;
        var itemDefs = gameSession.get_item_defs();
        var itemDef = itemDefs.ContainsKey(itemId) ? itemDefs[itemId].AsGodotObject() as ItemDef : null;
        if (itemDef == null)
            return ItemCommitResult.Create(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                new GArray()
            );
        if (!itemDef.is_equipment())
            return ItemCommitResult.Create(
                false,
                "battle_loot_random_equipment_invalid_item",
                itemId.ToString(),
                0,
                new GArray()
            );
        var equipmentDropService = _runtime._equipment_drop_service;
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
            overflowEntries.Add(BuildBattleOverflowEntry(lootEntryData, overflowQuantity));
        return ItemCommitResult.Create(true, "", "", committedItemCount, overflowEntries);
    }

    private ItemCommitResult CommitEquipmentInstanceLootEntry(Dictionary lootEntryData)
    {
        if (!TryDictionary(lootEntryData, "equipment_instance", out var equipmentInstanceData))
            return ItemCommitResult.Create(
                false,
                "battle_loot_equipment_instance_missing_payload",
                DictionaryString(lootEntryData, "item_id", ""),
                0,
                new GArray()
            );
        var equipmentInstance = EquipmentInstanceState.from_dict(equipmentInstanceData);
        if (equipmentInstance == null)
            return ItemCommitResult.Create(
                false,
                "battle_loot_equipment_instance_invalid_payload",
                DictionaryString(lootEntryData, "item_id", ""),
                0,
                new GArray()
            );
        var itemId = ProgressionDataUtils.to_string_name(equipmentInstance.item_id);
        if (itemId == "")
        {
            itemId = DictionaryStringName(lootEntryData, "item_id", "");
            equipmentInstance.item_id = itemId;
        }
        if (itemId == "")
            return ItemCommitResult.Create(
                false,
                "battle_loot_equipment_instance_invalid_payload",
                "",
                0,
                new GArray()
            );
        var gameSession = _runtime._game_session;
        var itemDefs = gameSession.get_item_defs();
        var itemDef = itemDefs.ContainsKey(itemId) ? itemDefs[itemId].AsGodotObject() as ItemDef : null;
        if (itemDef == null)
            return ItemCommitResult.Create(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                new GArray()
            );
        if (!itemDef.is_equipment())
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
                new GArray { BuildBattleOverflowEntry(lootEntryData, 1) }
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
            var lootEntry = lootEntryValue.AsGodotDictionary().Duplicate(true);
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

    private string BuildBattleLootMergeKey(Dictionary lootEntryData)
    {
        if (lootEntryData == null || lootEntryData.Count == 0)
            return "";
        var dropType = DictionaryStringName(lootEntryData, "drop_type", "");
        var itemId = DictionaryStringName(lootEntryData, "item_id", "");
        if (itemId == "")
            return "";
        if (dropType == BattleLootConstants.DROP_TYPE_ITEM())
            return string.Format("{0}|{1}", dropType, itemId);
        if (dropType == BattleLootConstants.DROP_TYPE_RANDOM_EQUIPMENT())
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
        return itemId == BattleLootConstants.ITEM_CALAMITY_SHARD()
            && dropSourceKind == BattleLootConstants.SOURCE_KIND_CALAMITY_CONVERSION()
            && dropSourceId == BattleLootConstants.SOURCE_ID_ORDINARY_BATTLE();
    }

    private int GetRemainingRegularBattleCalamityShardCap()
    {
        return Mathf.Max(
            BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP()
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
            slotIndex < BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP();
            slotIndex++
        )
        {
            var flagId = BuildRegularBattleCalamityShardFlagId(slotIndex);
            if (partyState.get_fate_run_flag(flagId, false))
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
            slotIndex < BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP();
            slotIndex++
        )
        {
            var flagId = BuildRegularBattleCalamityShardFlagId(slotIndex);
            if (partyState.get_fate_run_flag(flagId, false))
                continue;
            partyState.set_fate_run_flag(flagId, true);
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
            slotIndex < BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP();
            slotIndex++
        )
            partyState.clear_fate_run_flag(BuildRegularBattleCalamityShardFlagId(slotIndex));
    }

    private StringName BuildRegularBattleCalamityShardFlagId(int slotIndex)
    {
        return ProgressionDataUtils.to_string_name(
            string.Format(
                "{0}{1}",
                BattleLootConstants.CALAMITY_SHARD_CHAPTER_FLAG_PREFIX(),
                Mathf.Max(slotIndex, 0)
            )
        );
    }

    private string BuildBattleResolutionStatusMessageInternal(
        string battleName,
        string winnerFactionId,
        Dictionary lootCommitResult,
        bool persistedOk
    )
    {
        return BuildBattleResolutionStatusMessageInternal(
            battleName,
            winnerFactionId,
            BattleLootCommitResult.FromDictionary(lootCommitResult),
            persistedOk
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
        Dictionary lootCommitResult
    )
    {
        return BuildLastBattleLootSnapshotInternal(
            battleName,
            winnerFactionId,
            battleResolutionResult,
            BattleLootCommitResult.FromDictionary(lootCommitResult)
        );
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
        return _runtime._format_faction_label(factionId);
    }

    private string GetItemDisplayName(StringName itemId)
    {
        if (_runtime == null)
            return itemId.ToString();
        return _runtime._get_item_display_name(itemId);
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
