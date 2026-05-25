using System;
using Godot;
using Godot.Collections;

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

    public new void Dispose()
    {
        _runtime = null;
    }

    public Dictionary CommitBattleLootToSharedWarehouse(BattleResolutionResult battleResolutionResult)
    {
        return CommitBattleLootToSharedWarehouseInternal(battleResolutionResult);
    }

    public void ClearRegularBattleCalamityShardFlags()
    {
        ClearRegularBattleCalamityShardFlagsInternal();
    }

    public string BuildBattleResolutionStatusMessage(string battleName, string winnerFactionId, Dictionary lootCommitResult, bool persistedOk)
    {
        return BuildBattleResolutionStatusMessageInternal(battleName, winnerFactionId, lootCommitResult, persistedOk);
    }

    public Dictionary BuildLastBattleLootSnapshot(string battleName, string winnerFactionId, BattleResolutionResult battleResolutionResult, Dictionary lootCommitResult)
    {
        return BuildLastBattleLootSnapshotInternal(battleName, winnerFactionId, battleResolutionResult, lootCommitResult);
    }

    public string FormatBattleDropEntries(Godot.Collections.Array dropEntryVariants)
    {
        return FormatBattleDropEntriesInternal(dropEntryVariants);
    }

    private Dictionary CommitBattleLootToSharedWarehouseInternal(BattleResolutionResult battleResolutionResult)
    {
        if (battleResolutionResult == null)
            return BuildLootCommitResult(false, "missing_battle_resolution_result", "", 0, new Array<Dictionary>(), 0);

        battleResolutionResult.set_overflow_entries(new Array<Dictionary>());
        if (battleResolutionResult.winner_faction_id != "player")
            return BuildLootCommitResult(true, "", "", 0, new Array<Dictionary>(), 0);

        var partyState = _runtime.Get("_party_state").AsGodotObject();
        var partyWarehouseService = _runtime.Get("_party_warehouse_service").AsGodotObject();
        var gameSession = _runtime.Get("_game_session").AsGodotObject();
        if (partyState == null || partyWarehouseService == null || gameSession == null)
            return BuildLootCommitResult(false, "warehouse_service_unavailable", "", 0, new Array<Dictionary>(), 0);

        var itemDefs = gameSession.Call("get_item_defs").AsGodotDictionary();
        _runtime.Call("_setup_party_warehouse_service", partyWarehouseService, partyState, itemDefs);

        var warehouseState = partyState.Get("warehouse_state").AsGodotObject();
        GodotObject warehouseStateBefore = null;
        if (warehouseState != null)
            warehouseStateBefore = warehouseState.Call("duplicate_state").AsGodotObject();

        var fateRunFlagsBefore = new Dictionary();
        if (partyState != null && partyState.HasMethod("get_fate_run_flags"))
            fateRunFlagsBefore = partyState.Call("get_fate_run_flags").AsGodotDictionary().Duplicate(true);

        var overflowEntries = new Array<Dictionary>();
        var committedItemCount = 0;
        var effectiveLootEntries = ResolveEffectiveBattleLootEntriesForCommit(battleResolutionResult);
        battleResolutionResult.set_loot_entries(effectiveLootEntries);

        foreach (var lootEntryVariant in battleResolutionResult.loot_entries)
        {
            if (lootEntryVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var lootEntryData = lootEntryVariant.AsGodotDictionary();
            var dropType = ProgressionDataUtils.to_string_name(DictionaryGet(lootEntryData, "drop_type", BattleLootConstants.DROP_TYPE_ITEM()));

            if (dropType == BattleLootConstants.DROP_TYPE_EQUIPMENT_INSTANCE())
            {
                var instanceCommitResult = CommitEquipmentInstanceLootEntry(lootEntryData);
                if (!DictionaryGet(instanceCommitResult, "ok", false).AsBool())
                {
                    partyState.Set("warehouse_state", warehouseStateBefore);
                    partyState.Call("set_fate_run_flags", fateRunFlagsBefore);
                    _runtime.Call("_setup_party_warehouse_service", partyWarehouseService, partyState, itemDefs);
                    return BuildLootCommitResult(false,
                        DictionaryGet(instanceCommitResult, "error_code", "battle_loot_equipment_instance_failed").AsString(),
                        DictionaryGet(instanceCommitResult, "blocked_item_id", "").AsString(),
                        0, new Array<Dictionary>(), 0);
                }
                committedItemCount += DictionaryGet(instanceCommitResult, "committed_item_count", 0).AsInt32();
                foreach (var overflowVariant in DictionaryGet(instanceCommitResult, "overflow_entries", new Godot.Collections.Array()).AsGodotArray())
                {
                    if (overflowVariant.VariantType == Variant.Type.Dictionary)
                        overflowEntries.Add(overflowVariant.AsGodotDictionary().Duplicate(true));
                }
                continue;
            }

            if (dropType == BattleLootConstants.DROP_TYPE_RANDOM_EQUIPMENT())
            {
                var equipmentCommitResult = CommitRandomEquipmentLootEntry(lootEntryData);
                if (!DictionaryGet(equipmentCommitResult, "ok", false).AsBool())
                {
                    partyState.Set("warehouse_state", warehouseStateBefore);
                    partyState.Call("set_fate_run_flags", fateRunFlagsBefore);
                    _runtime.Call("_setup_party_warehouse_service", partyWarehouseService, partyState, itemDefs);
                    return BuildLootCommitResult(false,
                        DictionaryGet(equipmentCommitResult, "error_code", "battle_loot_random_equipment_failed").AsString(),
                        DictionaryGet(equipmentCommitResult, "blocked_item_id", "").AsString(),
                        0, new Array<Dictionary>(), 0);
                }
                committedItemCount += DictionaryGet(equipmentCommitResult, "committed_item_count", 0).AsInt32();
                foreach (var overflowVariant in DictionaryGet(equipmentCommitResult, "overflow_entries", new Godot.Collections.Array()).AsGodotArray())
                {
                    if (overflowVariant.VariantType == Variant.Type.Dictionary)
                        overflowEntries.Add(overflowVariant.AsGodotDictionary().Duplicate(true));
                }
                continue;
            }

            var itemCommitResult = CommitFixedItemLootEntry(lootEntryData);
            if (!DictionaryGet(itemCommitResult, "ok", false).AsBool())
            {
                partyState.Set("warehouse_state", warehouseStateBefore);
                partyState.Call("set_fate_run_flags", fateRunFlagsBefore);
                _runtime.Call("_setup_party_warehouse_service", partyWarehouseService, partyState, itemDefs);
                return BuildLootCommitResult(false,
                    DictionaryGet(itemCommitResult, "error_code", "battle_loot_item_missing_def").AsString(),
                    DictionaryGet(itemCommitResult, "blocked_item_id", "").AsString(),
                    0, new Array<Dictionary>(), 0);
            }
            committedItemCount += DictionaryGet(itemCommitResult, "committed_item_count", 0).AsInt32();
            if (IsOrdinaryBattleCalamityConversionEntry(lootEntryData))
                MarkRegularBattleCalamityShardsCommitted(DictionaryGet(itemCommitResult, "committed_item_count", 0).AsInt32());
            foreach (var overflowVariant in DictionaryGet(itemCommitResult, "overflow_entries", new Godot.Collections.Array()).AsGodotArray())
            {
                if (overflowVariant.VariantType == Variant.Type.Dictionary)
                    overflowEntries.Add(overflowVariant.AsGodotDictionary().Duplicate(true));
            }
        }

        battleResolutionResult.set_overflow_entries(overflowEntries);
        var overflowItemId = "";
        if (battleResolutionResult.overflow_entries.Count > 0 && battleResolutionResult.overflow_entries[0].VariantType == Variant.Type.Dictionary)
            overflowItemId = battleResolutionResult.overflow_entries[0].AsGodotDictionary().Get("item_id").AsString();
        return BuildLootCommitResult(true, "", overflowItemId, committedItemCount, battleResolutionResult.overflow_entries.Duplicate(true), battleResolutionResult.overflow_entries.Count);
    }

    private Dictionary CommitFixedItemLootEntry(Dictionary lootEntryData)
    {
        var itemId = ProgressionDataUtils.to_string_name(DictionaryGet(lootEntryData, "item_id", ""));
        var quantity = Mathf.Max(DictionaryGet(lootEntryData, "quantity", 0).AsInt32(), 0);
        if (itemId == "" || quantity <= 0)
            return BuildItemCommitResult(true, "", "", 0, new Array<Dictionary>());
        var partyWarehouseService = _runtime.Get("_party_warehouse_service").AsGodotObject();
        var addResult = partyWarehouseService.Call("add_item", itemId, quantity).AsGodotDictionary();
        if (!DictionaryGet(addResult, "item_found", false).AsBool())
            return BuildItemCommitResult(false, "battle_loot_item_missing_def", itemId.ToString(), 0, new Array<Dictionary>());
        var overflowEntries = new Array<Dictionary>();
        var remainingQuantity = DictionaryGet(addResult, "remaining_quantity", 0).AsInt32();
        if (remainingQuantity > 0)
            overflowEntries.Add(BuildBattleOverflowEntry(lootEntryData, remainingQuantity));
        return BuildItemCommitResult(true, "", "", DictionaryGet(addResult, "added_quantity", 0).AsInt32(), overflowEntries);
    }

    private Dictionary CommitRandomEquipmentLootEntry(Dictionary lootEntryData)
    {
        var itemId = ProgressionDataUtils.to_string_name(DictionaryGet(lootEntryData, "item_id", ""));
        var quantity = Mathf.Max(DictionaryGet(lootEntryData, "quantity", 0).AsInt32(), 0);
        var dropLuck = Mathf.Clamp(DictionaryGet(lootEntryData, "drop_luck", 0).AsInt32(), -6, 5);
        if (itemId == "" || quantity <= 0)
            return BuildItemCommitResult(true, "", "", 0, new Array<Dictionary>());
        var gameSession = _runtime.Get("_game_session").AsGodotObject();
        var itemDefs = gameSession.Call("get_item_defs").AsGodotDictionary();
        var itemDef = itemDefs.ContainsKey(itemId) ? itemDefs[itemId].AsGodotObject() : null;
        if (itemDef == null)
            return BuildItemCommitResult(false, "battle_loot_item_missing_def", itemId.ToString(), 0, new Array<Dictionary>());
        if (!itemDef.Call("is_equipment").AsBool())
            return BuildItemCommitResult(false, "battle_loot_random_equipment_invalid_item", itemId.ToString(), 0, new Array<Dictionary>());
        var equipmentDropService = _runtime.Get("_equipment_drop_service").AsGodotObject();
        var rolledInstances = equipmentDropService.Call("roll_item_instances", itemId, quantity, dropLuck).AsGodotArray();
        var committedItemCount = 0;
        var overflowQuantity = 0;
        foreach (var rolledInstanceVariant in rolledInstances)
        {
            if (rolledInstanceVariant.VariantType == Variant.Type.Nil)
                continue;
            var rolledInstance = rolledInstanceVariant.AsGodotObject();
            var rolledItemId = ProgressionDataUtils.to_string_name(rolledInstance.Get("item_id"));
            var partyWarehouseService = _runtime.Get("_party_warehouse_service").AsGodotObject();
            var addResult = partyWarehouseService.Call("add_equipment_instance", rolledInstance).AsGodotDictionary();
            if (!DictionaryGet(addResult, "item_found", false).AsBool())
                return BuildItemCommitResult(false, "battle_loot_item_missing_def", rolledItemId.ToString(), 0, new Array<Dictionary>());
            if (!DictionaryGet(addResult, "is_equipment", false).AsBool())
                return BuildItemCommitResult(false, "battle_loot_random_equipment_invalid_item", rolledItemId.ToString(), 0, new Array<Dictionary>());
            if (DictionaryGet(addResult, "remaining_quantity", 0).AsInt32() > 0)
            {
                overflowQuantity++;
                continue;
            }
            committedItemCount++;
        }
        var overflowEntries = new Array<Dictionary>();
        if (overflowQuantity > 0)
            overflowEntries.Add(BuildBattleOverflowEntry(lootEntryData, overflowQuantity));
        return BuildItemCommitResult(true, "", "", committedItemCount, overflowEntries);
    }

    private Dictionary CommitEquipmentInstanceLootEntry(Dictionary lootEntryData)
    {
        if (!lootEntryData.ContainsKey("equipment_instance") || DictionaryGet(lootEntryData, "equipment_instance", default(Variant)).VariantType != Variant.Type.Dictionary)
            return BuildItemCommitResult(false, "battle_loot_equipment_instance_missing_payload", DictionaryGet(lootEntryData, "item_id", "").AsString(), 0, new Array<Dictionary>());
        var equipmentInstanceVariant = DictionaryGet(lootEntryData, "equipment_instance", default(Variant));
        var equipmentInstance = EquipmentInstanceState.from_dict(equipmentInstanceVariant.AsGodotDictionary());
        if (equipmentInstance == null)
            return BuildItemCommitResult(false, "battle_loot_equipment_instance_invalid_payload", DictionaryGet(lootEntryData, "item_id", "").AsString(), 0, new Array<Dictionary>());
        var itemId = ProgressionDataUtils.to_string_name(equipmentInstance.item_id);
        if (itemId == "")
        {
            itemId = ProgressionDataUtils.to_string_name(DictionaryGet(lootEntryData, "item_id", ""));
            equipmentInstance.item_id = itemId;
        }
        if (itemId == "")
            return BuildItemCommitResult(false, "battle_loot_equipment_instance_invalid_payload", "", 0, new Array<Dictionary>());
        var gameSession = _runtime.Get("_game_session").AsGodotObject();
        var itemDefs = gameSession.Call("get_item_defs").AsGodotDictionary();
        var itemDef = itemDefs.ContainsKey(itemId) ? itemDefs[itemId].AsGodotObject() : null;
        if (itemDef == null)
            return BuildItemCommitResult(false, "battle_loot_item_missing_def", itemId.ToString(), 0, new Array<Dictionary>());
        if (!itemDef.Call("is_equipment").AsBool())
            return BuildItemCommitResult(false, "battle_loot_random_equipment_invalid_item", itemId.ToString(), 0, new Array<Dictionary>());
        var partyWarehouseService = _runtime.Get("_party_warehouse_service").AsGodotObject();
        var addResult = partyWarehouseService.Call("add_equipment_instance", equipmentInstance).AsGodotDictionary();
        if (DictionaryGet(addResult, "remaining_quantity", 0).AsInt32() > 0)
            return BuildItemCommitResult(true, "", "", 0, new Array<Dictionary> { BuildBattleOverflowEntry(lootEntryData, 1) });
        return BuildItemCommitResult(true, "", "", 1, new Array<Dictionary>());
    }

    private Dictionary BuildBattleOverflowEntry(Dictionary lootEntryData, int overflowQuantity)
    {
        var overflowEntry = lootEntryData.Duplicate(true);
        overflowEntry["quantity"] = Mathf.Max(overflowQuantity, 0);
        return overflowEntry;
    }

    private Array<Dictionary> ResolveEffectiveBattleLootEntriesForCommit(BattleResolutionResult battleResolutionResult)
    {
        var adjustedEntries = new Array<Dictionary>();
        if (battleResolutionResult == null)
            return adjustedEntries;
        var remainingRegularCap = GetRemainingRegularBattleCalamityShardCap();
        var mergeIndexByKey = new Dictionary();
        foreach (var lootEntryVariant in battleResolutionResult.loot_entries)
        {
            if (lootEntryVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var lootEntry = lootEntryVariant.AsGodotDictionary().Duplicate(true);
            if (IsOrdinaryBattleCalamityConversionEntry(lootEntry))
            {
                var allowedQuantity = Mathf.Min(Mathf.Max(DictionaryGet(lootEntry, "quantity", 0).AsInt32(), 0), remainingRegularCap);
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
                    var mergedEntry = adjustedEntries[entryIndex].Duplicate(true);
                    mergedEntry["quantity"] = DictionaryGet(mergedEntry, "quantity", 0).AsInt32() + DictionaryGet(lootEntry, "quantity", 0).AsInt32();
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
        var dropType = ProgressionDataUtils.to_string_name(DictionaryGet(lootEntryData, "drop_type", ""));
        var itemId = ProgressionDataUtils.to_string_name(DictionaryGet(lootEntryData, "item_id", ""));
        if (itemId == "")
            return "";
        if (dropType == BattleLootConstants.DROP_TYPE_ITEM())
            return string.Format("{0}|{1}", dropType, itemId);
        if (dropType == BattleLootConstants.DROP_TYPE_RANDOM_EQUIPMENT())
            return string.Format("{0}|{1}|{2}", dropType, itemId, Mathf.Clamp(DictionaryGet(lootEntryData, "drop_luck", 0).AsInt32(), -6, 5));
        return "";
    }

    private bool IsOrdinaryBattleCalamityConversionEntry(Dictionary lootEntryData)
    {
        if (lootEntryData == null || lootEntryData.Count == 0)
            return false;
        var itemId = ProgressionDataUtils.to_string_name(DictionaryGet(lootEntryData, "item_id", ""));
        var dropSourceKind = ProgressionDataUtils.to_string_name(DictionaryGet(lootEntryData, "drop_source_kind", ""));
        var dropSourceId = ProgressionDataUtils.to_string_name(DictionaryGet(lootEntryData, "drop_source_id", ""));
        return itemId == BattleLootConstants.ITEM_CALAMITY_SHARD()
            && dropSourceKind == BattleLootConstants.SOURCE_KIND_CALAMITY_CONVERSION()
            && dropSourceId == BattleLootConstants.SOURCE_ID_ORDINARY_BATTLE();
    }

    private int GetRemainingRegularBattleCalamityShardCap()
    {
        return Mathf.Max(BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP() - GetRegularBattleCalamityShardCountThisChapter(), 0);
    }

    private int GetRegularBattleCalamityShardCountThisChapter()
    {
        var partyState = _runtime.Get("_party_state").AsGodotObject();
        if (partyState == null)
            return 0;
        var shardCount = 0;
        for (int slotIndex = 0; slotIndex < BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP(); slotIndex++)
        {
            var flagId = BuildRegularBattleCalamityShardFlagId(slotIndex);
            if (partyState.HasMethod("get_fate_run_flag") && partyState.Call("get_fate_run_flag", flagId, false).AsBool())
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
        for (int slotIndex = 0; slotIndex < BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP(); slotIndex++)
        {
            var flagId = BuildRegularBattleCalamityShardFlagId(slotIndex);
            if (partyState.HasMethod("get_fate_run_flag") && partyState.Call("get_fate_run_flag", flagId, false).AsBool())
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
        for (int slotIndex = 0; slotIndex < BattleLootConstants.ORDINARY_BATTLE_CALAMITY_SHARD_CHAPTER_CAP(); slotIndex++)
            partyState.Call("clear_fate_run_flag", BuildRegularBattleCalamityShardFlagId(slotIndex));
    }

    private StringName BuildRegularBattleCalamityShardFlagId(int slotIndex)
    {
        return ProgressionDataUtils.to_string_name(string.Format("{0}{1}", BattleLootConstants.CALAMITY_SHARD_CHAPTER_FLAG_PREFIX(), Mathf.Max(slotIndex, 0)));
    }

    private string BuildBattleResolutionStatusMessageInternal(string battleName, string winnerFactionId, Dictionary lootCommitResult, bool persistedOk)
    {
        string message;
        if (persistedOk)
            message = string.Format("{0} 战斗结束，胜利方：{1}。已返回世界地图并统一保存。", battleName, FormatFactionLabel(winnerFactionId));
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
        if (!DictionaryGet(lootCommitResult, "ok", false).AsBool())
        {
            var blockedItemId = ProgressionDataUtils.to_string_name(DictionaryGet(lootCommitResult, "blocked_item_id", ""));
            if (blockedItemId != "")
                return string.Format("战斗掉落写入共享仓库失败：{0}。", GetItemDisplayName(blockedItemId));
            return "战斗掉落写入共享仓库失败。";
        }
        var overflowText = FormatBattleDropEntriesInternal(DictionaryGet(lootCommitResult, "overflow_entries", new Godot.Collections.Array()).AsGodotArray());
        if (string.IsNullOrEmpty(overflowText))
            return "";
        return string.Format("未装下的掉落：{0}。", overflowText);
    }

    private Dictionary BuildLastBattleLootSnapshotInternal(string battleName, string winnerFactionId, BattleResolutionResult battleResolutionResult, Dictionary lootCommitResult)
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
            ["commit_ok"] = DictionaryGet(lootCommitResult, "ok", false).AsBool(),
            ["commit_error_code"] = DictionaryGet(lootCommitResult, "error_code", "").AsString(),
        };
    }

    private string FormatBattleDropEntriesInternal(Godot.Collections.Array dropEntryVariants)
    {
        var quantitiesByItem = new Dictionary();
        var orderedItemIds = new Array<StringName>();
        foreach (var dropEntryVariant in dropEntryVariants)
        {
            if (dropEntryVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var dropEntryData = dropEntryVariant.AsGodotDictionary();
            var itemId = ProgressionDataUtils.to_string_name(DictionaryGet(dropEntryData, "item_id", ""));
            var quantity = Mathf.Max(DictionaryGet(dropEntryData, "quantity", 0).AsInt32(), 0);
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
            parts.Add(string.Format("{0} x{1}", GetItemDisplayName(itemId), quantitiesByItem[itemId].AsInt32()));
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

    private static Dictionary BuildLootCommitResult(bool ok, string errorCode, string blockedItemId, int committedItemCount, Array<Dictionary> overflowEntries, int overflowEntryCount)
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

    private static Dictionary BuildItemCommitResult(bool ok, string errorCode, string blockedItemId, int committedItemCount, Array<Dictionary> overflowEntries)
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

    private static Variant DictionaryGet(Dictionary dictionary, Variant key, Variant fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key];
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GodotObject target) || !GodotObject.IsInstanceValid(target))
            return null;
        return target;
    }
}

