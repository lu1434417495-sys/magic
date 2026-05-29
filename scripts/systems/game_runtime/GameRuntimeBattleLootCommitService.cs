using System;
using Godot;
using Godot.Collections;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class GameRuntimeBattleLootCommitService : RefCounted
{
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

    public Dictionary CommitBattleLootToSharedWarehouse(
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
        return CommitFixedItemLootEntry(lootEntryData);
    }

    public Dictionary _commit_equipment_instance_loot_entry(Dictionary lootEntryData)
    {
        return CommitEquipmentInstanceLootEntry(lootEntryData);
    }

    private Dictionary CommitBattleLootToSharedWarehouseInternal(
        BattleResolutionResult battleResolutionResult
    )
    {
        if (battleResolutionResult == null)
            return BuildLootCommitResult(
                false,
                "missing_battle_resolution_result",
                "",
                0,
                new GArray(),
                0
            );

        battleResolutionResult.set_overflow_entries(new GArray());
        if (battleResolutionResult.winner_faction_id != "player")
            return BuildLootCommitResult(true, "", "", 0, new GArray(), 0);

        var partyState = _runtime.Get("_party_state").AsGodotObject();
        var partyWarehouseService = _runtime.Get("_party_warehouse_service").AsGodotObject();
        var gameSession = _runtime.Get("_game_session").AsGodotObject();
        if (partyState == null || partyWarehouseService == null || gameSession == null)
            return BuildLootCommitResult(
                false,
                "warehouse_service_unavailable",
                "",
                0,
                new GArray(),
                0
            );

        var itemDefs = gameSession.Call("get_item_defs").AsGodotDictionary();
        _runtime.Call(
            "_setup_party_warehouse_service",
            partyWarehouseService,
            partyState,
            itemDefs
        );

        var warehouseState = partyState.Get("warehouse_state").AsGodotObject();
        GodotObject warehouseStateBefore = null;
        if (warehouseState != null)
            warehouseStateBefore = warehouseState.Call("duplicate_state").AsGodotObject();

        var fateRunFlagsBefore = new Dictionary();
        if (partyState != null && partyState.HasMethod("get_fate_run_flags"))
            fateRunFlagsBefore = partyState
                .Call("get_fate_run_flags")
                .AsGodotDictionary()
                .Duplicate(true);

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
                if (!DictionaryBool(instanceCommitResult, "ok", false))
                {
                    partyState.Set("warehouse_state", warehouseStateBefore);
                    partyState.Call("set_fate_run_flags", fateRunFlagsBefore);
                    _runtime.Call(
                        "_setup_party_warehouse_service",
                        partyWarehouseService,
                        partyState,
                        itemDefs
                    );
                    return BuildLootCommitResult(
                        false,
                        DictionaryString(
                            instanceCommitResult,
                            "error_code",
                            "battle_loot_equipment_instance_failed"
                        ),
                        DictionaryString(instanceCommitResult, "blocked_item_id", ""),
                        0,
                        new GArray(),
                        0
                    );
                }
                committedItemCount += DictionaryInt(
                    instanceCommitResult,
                    "committed_item_count",
                    0
                );
                foreach (
                    var overflowValue in DictionaryArray(
                        instanceCommitResult,
                        "overflow_entries",
                        new Godot.Collections.Array()
                    )
                )
                {
                    if (overflowValue.VariantType == Variant.Type.Dictionary)
                        overflowEntries.Add(overflowValue.AsGodotDictionary().Duplicate(true));
                }
                continue;
            }

            if (dropType == BattleLootConstants.DROP_TYPE_RANDOM_EQUIPMENT())
            {
                var equipmentCommitResult = CommitRandomEquipmentLootEntry(lootEntryData);
                if (!DictionaryBool(equipmentCommitResult, "ok", false))
                {
                    partyState.Set("warehouse_state", warehouseStateBefore);
                    partyState.Call("set_fate_run_flags", fateRunFlagsBefore);
                    _runtime.Call(
                        "_setup_party_warehouse_service",
                        partyWarehouseService,
                        partyState,
                        itemDefs
                    );
                    return BuildLootCommitResult(
                        false,
                        DictionaryString(
                            equipmentCommitResult,
                            "error_code",
                            "battle_loot_random_equipment_failed"
                        ),
                        DictionaryString(equipmentCommitResult, "blocked_item_id", ""),
                        0,
                        new GArray(),
                        0
                    );
                }
                committedItemCount += DictionaryInt(
                    equipmentCommitResult,
                    "committed_item_count",
                    0
                );
                foreach (
                    var overflowValue in DictionaryArray(
                        equipmentCommitResult,
                        "overflow_entries",
                        new Godot.Collections.Array()
                    )
                )
                {
                    if (overflowValue.VariantType == Variant.Type.Dictionary)
                        overflowEntries.Add(overflowValue.AsGodotDictionary().Duplicate(true));
                }
                continue;
            }

            var itemCommitResult = CommitFixedItemLootEntry(lootEntryData);
            if (!DictionaryBool(itemCommitResult, "ok", false))
            {
                partyState.Set("warehouse_state", warehouseStateBefore);
                partyState.Call("set_fate_run_flags", fateRunFlagsBefore);
                _runtime.Call(
                    "_setup_party_warehouse_service",
                    partyWarehouseService,
                    partyState,
                    itemDefs
                );
                return BuildLootCommitResult(
                    false,
                    DictionaryString(itemCommitResult, "error_code", "battle_loot_item_missing_def"),
                    DictionaryString(itemCommitResult, "blocked_item_id", ""),
                    0,
                    new GArray(),
                    0
                );
            }
            committedItemCount += DictionaryInt(itemCommitResult, "committed_item_count", 0);
            if (IsOrdinaryBattleCalamityConversionEntry(lootEntryData))
                MarkRegularBattleCalamityShardsCommitted(
                    DictionaryInt(itemCommitResult, "committed_item_count", 0)
                );
            foreach (
                var overflowValue in DictionaryArray(
                    itemCommitResult,
                    "overflow_entries",
                    new Godot.Collections.Array()
                )
            )
            {
                if (overflowValue.VariantType == Variant.Type.Dictionary)
                    overflowEntries.Add(overflowValue.AsGodotDictionary().Duplicate(true));
            }
        }

        battleResolutionResult.set_overflow_entries(overflowEntries);
        var overflowItemId = "";
        if (
            battleResolutionResult.overflow_entries.Count > 0
            && battleResolutionResult.overflow_entries[0].VariantType == Variant.Type.Dictionary
        )
            overflowItemId = GdInterop.GetString(
                battleResolutionResult.overflow_entries[0].AsGodotDictionary(),
                "item_id"
            );
        return BuildLootCommitResult(
            true,
            "",
            overflowItemId,
            committedItemCount,
            battleResolutionResult.overflow_entries.Duplicate(true),
            battleResolutionResult.overflow_entries.Count
        );
    }

    private Dictionary CommitFixedItemLootEntry(Dictionary lootEntryData)
    {
        var itemId = DictionaryStringName(lootEntryData, "item_id", "");
        var quantity = Mathf.Max(DictionaryInt(lootEntryData, "quantity", 0), 0);
        if (itemId == "" || quantity <= 0)
            return BuildItemCommitResult(true, "", "", 0, new GArray());
        var partyWarehouseService = _runtime.Get("_party_warehouse_service").AsGodotObject();
        var addResult = partyWarehouseService
            .Call("add_item", itemId, quantity)
            .AsGodotDictionary();
        if (!DictionaryBool(addResult, "item_found", false))
            return BuildItemCommitResult(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                new GArray()
            );
        var overflowEntries = new GArray();
        var remainingQuantity = DictionaryInt(addResult, "remaining_quantity", 0);
        if (remainingQuantity > 0)
            overflowEntries.Add(BuildBattleOverflowEntry(lootEntryData, remainingQuantity));
        return BuildItemCommitResult(
            true,
            "",
            "",
            DictionaryInt(addResult, "added_quantity", 0),
            overflowEntries
        );
    }

    private Dictionary CommitRandomEquipmentLootEntry(Dictionary lootEntryData)
    {
        var itemId = DictionaryStringName(lootEntryData, "item_id", "");
        var quantity = Mathf.Max(DictionaryInt(lootEntryData, "quantity", 0), 0);
        var dropLuck = Mathf.Clamp(DictionaryInt(lootEntryData, "drop_luck", 0), -6, 5);
        if (itemId == "" || quantity <= 0)
            return BuildItemCommitResult(true, "", "", 0, new GArray());
        var gameSession = _runtime.Get("_game_session").AsGodotObject();
        var itemDefs = gameSession.Call("get_item_defs").AsGodotDictionary();
        var itemDef = itemDefs.ContainsKey(itemId) ? itemDefs[itemId].AsGodotObject() : null;
        if (itemDef == null)
            return BuildItemCommitResult(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                new GArray()
            );
        if (!itemDef.Call("is_equipment").AsBool())
            return BuildItemCommitResult(
                false,
                "battle_loot_random_equipment_invalid_item",
                itemId.ToString(),
                0,
                new GArray()
            );
        var equipmentDropService = _runtime.Get("_equipment_drop_service").AsGodotObject();
        var rolledInstances = equipmentDropService
            .Call("roll_item_instances", itemId, quantity, dropLuck)
            .AsGodotArray();
        var committedItemCount = 0;
        var overflowQuantity = 0;
        foreach (var rolledInstanceValue in rolledInstances)
        {
            if (rolledInstanceValue.VariantType == Variant.Type.Nil)
                continue;
            var rolledInstance = rolledInstanceValue.AsGodotObject();
            var rolledItemId = ProgressionDataUtils.to_string_name(rolledInstance.Get("item_id"));
            var partyWarehouseService = _runtime.Get("_party_warehouse_service").AsGodotObject();
            var addResult = partyWarehouseService
                .Call("add_equipment_instance", rolledInstance)
                .AsGodotDictionary();
            if (!DictionaryBool(addResult, "item_found", false))
                return BuildItemCommitResult(
                    false,
                    "battle_loot_item_missing_def",
                    rolledItemId.ToString(),
                    0,
                    new GArray()
                );
            if (!DictionaryBool(addResult, "is_equipment", false))
                return BuildItemCommitResult(
                    false,
                    "battle_loot_random_equipment_invalid_item",
                    rolledItemId.ToString(),
                    0,
                    new GArray()
                );
            if (DictionaryInt(addResult, "remaining_quantity", 0) > 0)
            {
                overflowQuantity++;
                continue;
            }
            committedItemCount++;
        }
        var overflowEntries = new GArray();
        if (overflowQuantity > 0)
            overflowEntries.Add(BuildBattleOverflowEntry(lootEntryData, overflowQuantity));
        return BuildItemCommitResult(true, "", "", committedItemCount, overflowEntries);
    }

    private Dictionary CommitEquipmentInstanceLootEntry(Dictionary lootEntryData)
    {
        if (!TryDictionary(lootEntryData, "equipment_instance", out var equipmentInstanceData))
            return BuildItemCommitResult(
                false,
                "battle_loot_equipment_instance_missing_payload",
                DictionaryString(lootEntryData, "item_id", ""),
                0,
                new GArray()
            );
        var equipmentInstance = EquipmentInstanceState.from_dict(equipmentInstanceData);
        if (equipmentInstance == null)
            return BuildItemCommitResult(
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
            return BuildItemCommitResult(
                false,
                "battle_loot_equipment_instance_invalid_payload",
                "",
                0,
                new GArray()
            );
        var gameSession = _runtime.Get("_game_session").AsGodotObject();
        var itemDefs = gameSession.Call("get_item_defs").AsGodotDictionary();
        var itemDef = itemDefs.ContainsKey(itemId) ? itemDefs[itemId].AsGodotObject() : null;
        if (itemDef == null)
            return BuildItemCommitResult(
                false,
                "battle_loot_item_missing_def",
                itemId.ToString(),
                0,
                new GArray()
            );
        if (!itemDef.Call("is_equipment").AsBool())
            return BuildItemCommitResult(
                false,
                "battle_loot_random_equipment_invalid_item",
                itemId.ToString(),
                0,
                new GArray()
            );
        var partyWarehouseService = _runtime.Get("_party_warehouse_service").AsGodotObject();
        var addResult = partyWarehouseService
            .Call("add_equipment_instance", equipmentInstance)
            .AsGodotDictionary();
        if (DictionaryInt(addResult, "remaining_quantity", 0) > 0)
            return BuildItemCommitResult(
                true,
                "",
                "",
                0,
                new GArray { BuildBattleOverflowEntry(lootEntryData, 1) }
            );
        return BuildItemCommitResult(true, "", "", 1, new GArray());
    }

    private Dictionary BuildBattleOverflowEntry(Dictionary lootEntryData, int overflowQuantity)
    {
        var overflowEntry = lootEntryData.Duplicate(true);
        overflowEntry["quantity"] = Mathf.Max(overflowQuantity, 0);
        return overflowEntry;
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
        var partyState = _runtime.Get("_party_state").AsGodotObject();
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
            if (
                partyState.HasMethod("get_fate_run_flag")
                && partyState.Call("get_fate_run_flag", flagId, false).AsBool()
            )
                shardCount++;
        }
        return shardCount;
    }

    private void MarkRegularBattleCalamityShardsCommitted(int quantity)
    {
        var partyState = _runtime.Get("_party_state").AsGodotObject();
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
            if (
                partyState.HasMethod("get_fate_run_flag")
                && partyState.Call("get_fate_run_flag", flagId, false).AsBool()
            )
                continue;
            if (partyState.HasMethod("set_fate_run_flag"))
                partyState.Call("set_fate_run_flag", flagId, true);
            remainingToMark--;
            if (remainingToMark <= 0)
                return;
        }
    }

    private void ClearRegularBattleCalamityShardFlagsInternal()
    {
        var partyState = _runtime.Get("_party_state").AsGodotObject();
        if (partyState == null || !partyState.HasMethod("clear_fate_run_flag"))
            return;
        for (
            int slotIndex = 0;
            slotIndex < BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP();
            slotIndex++
        )
            partyState.Call(
                "clear_fate_run_flag",
                BuildRegularBattleCalamityShardFlagId(slotIndex)
            );
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

    private string BuildBattleLootStatusSuffix(Dictionary lootCommitResult)
    {
        if (lootCommitResult == null || lootCommitResult.Count == 0)
            return "";
        if (!DictionaryBool(lootCommitResult, "ok", false))
        {
            var blockedItemId = DictionaryStringName(lootCommitResult, "blocked_item_id", "");
            if (blockedItemId != "")
                return string.Format(
                    "战斗掉落写入共享仓库失败：{0}。",
                    GetItemDisplayName(blockedItemId)
                );
            return "战斗掉落写入共享仓库失败。";
        }
        var overflowText = FormatBattleDropEntriesInternal(
            DictionaryArray(lootCommitResult, "overflow_entries", new Godot.Collections.Array())
        );
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
            ["commit_ok"] = DictionaryBool(lootCommitResult, "ok", false),
            ["commit_error_code"] = DictionaryString(lootCommitResult, "error_code", ""),
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
        return _runtime.Call("_format_faction_label", factionId).AsString();
    }

    private string GetItemDisplayName(StringName itemId)
    {
        if (_runtime == null)
            return itemId.ToString();
        return _runtime.Call("_get_item_display_name", itemId).AsString();
    }

    private static Dictionary BuildLootCommitResult(
        bool ok,
        string errorCode,
        string blockedItemId,
        int committedItemCount,
        Godot.Collections.Array overflowEntries,
        int overflowEntryCount
    )
    {
        return new Dictionary
        {
            ["ok"] = ok,
            ["error_code"] = errorCode,
            ["blocked_item_id"] = blockedItemId,
            ["committed_item_count"] = committedItemCount,
            ["overflow_entries"] = overflowEntries,
            ["overflow_entry_count"] = overflowEntryCount,
        };
    }

    private static Dictionary BuildItemCommitResult(
        bool ok,
        string errorCode,
        string blockedItemId,
        int committedItemCount,
        Godot.Collections.Array overflowEntries
    )
    {
        return new Dictionary
        {
            ["ok"] = ok,
            ["error_code"] = errorCode,
            ["blocked_item_id"] = blockedItemId,
            ["committed_item_count"] = committedItemCount,
            ["overflow_entries"] = overflowEntries,
        };
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
