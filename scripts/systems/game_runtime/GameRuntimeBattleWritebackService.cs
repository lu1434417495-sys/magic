using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GameRuntimeBattleWritebackService : RefCounted
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

    public Dictionary CommitBattleLocalViewsToPartyState(
        BattleState battleState,
        GodotObject partyState
    )
    {
        return CommitBattleLocalViewsToPartyStateInternal(battleState, partyState);
    }

    public Dictionary commit_battle_local_views_to_party_state(
        BattleState battleState,
        GodotObject partyState
    )
    {
        return CommitBattleLocalViewsToPartyState(battleState, partyState);
    }

    public void ReportConsistencyFailure(
        Dictionary writebackResult,
        Dictionary battleSummary,
        string winnerFactionId
    )
    {
        ReportBattleLocalWritebackConsistencyFailure(writebackResult, battleSummary, winnerFactionId);
    }

    public void report_inoption_failure(
        Dictionary writebackResult,
        Dictionary battleSummary,
        string winnerFactionId
    )
    {
        ReportConsistencyFailure(writebackResult, battleSummary, winnerFactionId);
    }

    private Dictionary CommitBattleLocalViewsToPartyStateInternal(
        BattleState battleState,
        GodotObject partyState
    )
    {
        if (battleState == null)
            return BuildBattleLocalWritebackFailure("battle_local_writeback_missing_battle_state");
        if (partyState == null || !partyState.HasMethod("to_dict"))
            return BuildBattleLocalWritebackFailure("battle_local_writeback_missing_party_state");

        var candidateParty = ClonePartyStateForBattleWriteback(partyState);
        if (candidateParty == null)
            return BuildBattleLocalWritebackFailure("battle_local_writeback_invalid_party_state");

        var backpackView = battleState.get_party_backpack_view();
        if (backpackView == null || !backpackView.HasMethod("duplicate_state"))
            return BuildBattleLocalWritebackFailure("battle_local_writeback_invalid_backpack_view");

        var warehouseState = backpackView.Call("duplicate_state").AsGodotObject();
        if (warehouseState == null)
            return BuildBattleLocalWritebackFailure("battle_local_writeback_invalid_backpack_view");

        candidateParty.Set("warehouse_state", warehouseState);

        var committedMemberIds = new Dictionary();
        foreach (var allyUnitId in battleState.ally_unit_ids)
        {
            var unitState = battleState.units[allyUnitId].As<BattleUnitState>();
            if (unitState == null)
                return BuildBattleLocalWritebackFailure(
                    "battle_local_writeback_missing_ally_unit",
                    new Dictionary { ["unit_id"] = allyUnitId.ToString() }
                );

            var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
            if (memberId == "")
                continue;

            if (committedMemberIds.ContainsKey(memberId))
                return BuildBattleLocalWritebackFailure(
                    "battle_local_writeback_duplicate_member_unit",
                    new Dictionary { ["member_id"] = memberId.ToString() }
                );

            var memberState = candidateParty.Call("get_member_state", memberId).AsGodotObject();
            if (memberState == null)
                return BuildBattleLocalWritebackFailure(
                    "battle_local_writeback_member_not_found",
                    new Dictionary
                    {
                        ["member_id"] = memberId.ToString(),
                        ["unit_id"] = unitState.unit_id.ToString(),
                    }
                );

            if (!unitState.equipment_view_initialized)
                return BuildBattleLocalWritebackFailure(
                    "battle_local_writeback_uninitialized_equipment_view",
                    new Dictionary
                    {
                        ["member_id"] = memberId.ToString(),
                        ["unit_id"] = unitState.unit_id.ToString(),
                    }
                );

            var equipmentView = unitState.equipment_view;
            if (equipmentView == null || !equipmentView.HasMethod("duplicate_state"))
                return BuildBattleLocalWritebackFailure(
                    "battle_local_writeback_invalid_equipment_view",
                    new Dictionary
                    {
                        ["member_id"] = memberId.ToString(),
                        ["unit_id"] = unitState.unit_id.ToString(),
                    }
                );

            var equipmentCopy = equipmentView.Call("duplicate_state").AsGodotObject();
            if (equipmentCopy == null)
                return BuildBattleLocalWritebackFailure(
                    "battle_local_writeback_invalid_equipment_view",
                    new Dictionary
                    {
                        ["member_id"] = memberId.ToString(),
                        ["unit_id"] = unitState.unit_id.ToString(),
                    }
                );

            memberState.Set("equipment_state", equipmentCopy);
            committedMemberIds[memberId] = true;
        }

        var validationResult = ValidateBattleLocalCandidatePartyState(candidateParty);
        if (!DictionaryBool(validationResult, "ok", false))
            return validationResult;

        _runtime.Set("_party_state", candidateParty);
        SyncRuntimePartyServicesAfterBattleLocalWriteback();

        return new Dictionary
        {
            ["ok"] = true,
            ["error_code"] = "",
            ["committed_member_count"] = committedMemberIds.Count,
            ["used_slots"] = DictionaryInt(validationResult, "used_slots", 0),
            ["capacity"] = DictionaryInt(validationResult, "capacity", 0),
        };
    }

    private GodotObject ClonePartyStateForBattleWriteback(GodotObject partyState)
    {
        if (partyState == null || !partyState.HasMethod("to_dict"))
            return null;
        var partyPayload = partyState.Call("to_dict");
        if (partyPayload.VariantType != Variant.Type.Dictionary)
            return null;
        return PartyState.from_dict(partyPayload.AsGodotDictionary());
    }

    private Dictionary ValidateBattleLocalCandidatePartyState(GodotObject candidateParty)
    {
        var warehouseState = candidateParty.Get("warehouse_state").AsGodotObject();
        if (candidateParty == null || warehouseState == null)
            return BuildBattleLocalWritebackFailure(
                "battle_local_writeback_invalid_candidate_party"
            );

        var instanceOwnerById = new Dictionary();
        var nonEmptyInstances = warehouseState.Call("get_non_empty_instances").AsGodotArray();
        foreach (var instance in nonEmptyInstances)
        {
            var instanceObj = instance.AsGodotObject();
            if (instanceObj == null)
                continue;
            var instanceId = ProgressionDataUtils.to_string_name(instanceObj.Get("instance_id"));
            var itemId = ProgressionDataUtils.to_string_name(instanceObj.Get("item_id"));
            var registerResult = RegisterBattleLocalInstanceOwner(
                instanceOwnerById,
                instanceId,
                itemId,
                "backpack"
            );
            if (!DictionaryBool(registerResult, "ok", false))
                return registerResult;
        }

        var memberStates = candidateParty.Get("member_states").AsGodotDictionary();
        foreach (var memberIdStr in ProgressionDataUtils.sorted_string_keys(memberStates))
        {
            var memberId = (StringName)memberIdStr;
            var memberState = candidateParty.Call("get_member_state", memberId).AsGodotObject();
            if (memberState == null)
                continue;

            var equipmentState = memberState.Get("equipment_state").AsGodotObject();
            if (equipmentState == null || !equipmentState.HasMethod("get_entry_slot_ids"))
                return BuildBattleLocalWritebackFailure(
                    "battle_local_writeback_invalid_equipment_state",
                    new Dictionary { ["member_id"] = memberId.ToString() }
                );

            var entrySlotIds = equipmentState.Call("get_entry_slot_ids").AsGodotArray();
            foreach (var entrySlotId in entrySlotIds)
            {
                var itemId = ProgressionDataUtils.to_string_name(
                    equipmentState.Call("get_equipped_item_id", entrySlotId)
                );
                if (itemId == "")
                    return BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_invalid_equipment_entry",
                        new Dictionary
                        {
                            ["member_id"] = memberId.ToString(),
                            ["entry_slot_id"] = entrySlotId.ToString(),
                        }
                    );

                var instanceId = ProgressionDataUtils.to_string_name(
                    equipmentState.Call("get_equipped_instance_id", entrySlotId)
                );
                if (instanceId == "")
                    continue;

                var ownerLabel = string.Format("equipment:{0}:{1}", memberId, entrySlotId);
                var registerResult = RegisterBattleLocalInstanceOwner(
                    instanceOwnerById,
                    instanceId,
                    itemId,
                    ownerLabel
                );
                if (!DictionaryBool(registerResult, "ok", false))
                    return registerResult;
            }
        }

        var itemDefs = GetRuntimeItemDefs();
        var capacityService = new PartyWarehouseService();
        capacityService.setup(candidateParty as PartyState, itemDefs);
        var usedSlots = capacityService.get_used_slots();
        var capacity = capacityService.get_total_capacity();

        if (usedSlots > capacity)
            return BuildBattleLocalWritebackFailure(
                "battle_local_writeback_capacity_mismatch",
                new Dictionary { ["used_slots"] = usedSlots, ["capacity"] = capacity }
            );

        return new Dictionary
        {
            ["ok"] = true,
            ["error_code"] = "",
            ["used_slots"] = usedSlots,
            ["capacity"] = capacity,
        };
    }

    private Dictionary RegisterBattleLocalInstanceOwner(
        Dictionary instanceOwnerById,
        StringName instanceId,
        StringName itemId,
        string ownerLabel
    )
    {
        if (instanceId == "")
            return new Dictionary { ["ok"] = true, ["error_code"] = "" };

        var instanceKey = instanceId.ToString();
        if (instanceOwnerById.ContainsKey(instanceKey))
        {
            var previousOwner = DictionaryDictionary(instanceOwnerById, instanceKey);
            return BuildBattleLocalWritebackFailure(
                "battle_local_writeback_instance_conflict",
                new Dictionary
                {
                    ["instance_id"] = instanceKey,
                    ["item_id"] = itemId.ToString(),
                    ["owner"] = ownerLabel,
                    ["previous_owner"] = DictionaryString(previousOwner, "owner"),
                    ["previous_item_id"] = DictionaryString(previousOwner, "item_id"),
                }
            );
        }

        instanceOwnerById[instanceKey] = new Dictionary
        {
            ["owner"] = ownerLabel,
            ["item_id"] = itemId.ToString(),
        };
        return new Dictionary { ["ok"] = true, ["error_code"] = "" };
    }

    private void SyncRuntimePartyServicesAfterBattleLocalWriteback()
    {
        var itemDefs = GetRuntimeItemDefs();
        var partyState = _runtime.Get("_party_state").AsGodotObject();

        var characterManagement = _runtime.Get("_character_management").AsGodotObject();
        if (characterManagement != null)
            characterManagement.Call("set_party_state", partyState);

        var partyWarehouseService = _runtime.Get("_party_warehouse_service").AsGodotObject();
        if (partyWarehouseService != null)
            _runtime.Call(
                "_setup_party_warehouse_service",
                partyWarehouseService,
                partyState,
                itemDefs
            );

        var partyItemUseService = _runtime.Get("_party_item_use_service").AsGodotObject();
        if (partyItemUseService != null)
        {
            var skillDefs = new Dictionary();
            var gameSession = _runtime.Get("_game_session").AsGodotObject();
            if (gameSession != null)
                skillDefs = gameSession.Call("get_skill_defs").AsGodotDictionary();
            partyItemUseService.Call(
                "setup",
                partyState,
                itemDefs,
                skillDefs,
                partyWarehouseService,
                characterManagement
            );
        }

        var partyEquipmentService = _runtime.Get("_party_equipment_service").AsGodotObject();
        if (partyEquipmentService != null)
        {
            var allocator = _runtime.Call("_get_equipment_instance_id_allocator");
            partyEquipmentService.Call(
                "setup",
                partyState,
                itemDefs,
                partyWarehouseService,
                allocator
            );
        }
    }

    private Dictionary GetRuntimeItemDefs()
    {
        var gameSession = _runtime != null ? _runtime.Get("_game_session").AsGodotObject() : null;
        if (gameSession != null)
            return gameSession.Call("get_item_defs").AsGodotDictionary();
        return new Dictionary();
    }

    private static Dictionary BuildBattleLocalWritebackFailure(
        string errorCode,
        Dictionary details = null
    )
    {
        return new Dictionary
        {
            ["ok"] = false,
            ["error_code"] = errorCode,
            ["details"] = details != null ? details.Duplicate(true) : new Dictionary(),
        };
    }

    private void ReportBattleLocalWritebackConsistencyFailure(
        Dictionary writebackResult,
        Dictionary battleSummary,
        string winnerFactionId
    )
    {
        var errorCode = DictionaryString(
            writebackResult,
            "error_code",
            "battle_local_writeback_inoption_failed"
        );
        var details = DictionaryDictionary(writebackResult, "details").Duplicate(true);
        var message = string.Format(
            "Battle-local party writeback inoption failed: {0} {1}",
            errorCode,
            Json.Stringify(details)
        );
        GameLog.Error(message, "battle.writeback.failed", "battle");

        var statusMessage = string.Format(
            "战斗结算发生内部不变量错误：battle-local 队伍状态写回不可能失败但失败了（{0}）。",
            errorCode
        );
        _runtime.Call("_update_status", statusMessage);
        _runtime.Call(
            "_log_runtime_event",
            "error",
            "battle",
            "battle.local_writeback_inoption_failed",
            _runtime.Get("_current_status_message").AsString(),
            new Dictionary
            {
                ["battle"] = battleSummary,
                ["winner_faction_id"] = winnerFactionId,
                ["error_code"] = errorCode,
                ["details"] = details,
            }
        );
        System.Diagnostics.Debug.Assert(false, message);
    }

    private static bool DictionaryBool(Dictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static int DictionaryInt(Dictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static string DictionaryString(Dictionary dictionary, string key, string fallback = "")
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        var value = dictionary[key];
        return value.VariantType != Variant.Type.Nil ? value.AsString() : fallback;
    }

    private static Dictionary DictionaryDictionary(Dictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new Dictionary();
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Dictionary();
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
