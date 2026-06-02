using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GameRuntimeBattleWritebackService : RefCounted
{
    private WeakReference<GameRuntimeFacade> _runtimeRef;

    public sealed class BattleLocalWritebackResult
    {
        public bool Ok { get; }
        public string ErrorCode { get; }
        public Dictionary Details { get; }
        public int CommittedMemberCount { get; }
        public int UsedSlots { get; }
        public int Capacity { get; }

        private BattleLocalWritebackResult(
            bool ok,
            string errorCode,
            Dictionary details,
            int committedMemberCount,
            int usedSlots,
            int capacity
        )
        {
            Ok = ok;
            ErrorCode = errorCode ?? "";
            Details = details?.Duplicate(true) ?? new Dictionary();
            CommittedMemberCount = Mathf.Max(committedMemberCount, 0);
            UsedSlots = Mathf.Max(usedSlots, 0);
            Capacity = Mathf.Max(capacity, 0);
        }

        public static BattleLocalWritebackResult Success(
            int committedMemberCount,
            int usedSlots,
            int capacity
        ) =>
            new(true, "", new Dictionary(), committedMemberCount, usedSlots, capacity);

        public static BattleLocalWritebackResult Failed(
            string errorCode,
            Dictionary details = null
        ) =>
            new(false, errorCode, details, 0, 0, 0);

        public static BattleLocalWritebackResult FromFailureDictionary(Dictionary failure)
        {
            return Failed(
                DictionaryString(
                    failure,
                    "error_code",
                    "battle_local_writeback_inoption_failed"
                ),
                DictionaryDictionary(failure, "details")
            );
        }

        public Dictionary ToDictionary()
        {
            return Ok
                ? new Dictionary
                {
                    ["ok"] = true,
                    ["error_code"] = "",
                    ["committed_member_count"] = CommittedMemberCount,
                    ["used_slots"] = UsedSlots,
                    ["capacity"] = Capacity,
                }
                : BuildBattleLocalWritebackFailure(ErrorCode, Details);
        }
    }

    private sealed class BattleLocalCandidateValidationResult
    {
        public bool Ok { get; private set; }
        public Dictionary Failure { get; private set; } = new();
        public int UsedSlots { get; private set; }
        public int Capacity { get; private set; }

        public static BattleLocalCandidateValidationResult Success(int usedSlots, int capacity)
        {
            return new BattleLocalCandidateValidationResult
            {
                Ok = true,
                UsedSlots = usedSlots,
                Capacity = capacity,
            };
        }

        public static BattleLocalCandidateValidationResult Failed(Dictionary failure)
        {
            return new BattleLocalCandidateValidationResult
            {
                Ok = false,
                Failure = failure ?? BuildBattleLocalWritebackFailure(
                    "battle_local_writeback_invalid_candidate_party"
                ),
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

    public Dictionary CommitBattleLocalViewsToPartyState(
        BattleState battleState,
        PartyState partyState
    )
    {
        return CommitBattleLocalViewsToPartyStateTyped(battleState, partyState).ToDictionary();
    }

    public BattleLocalWritebackResult CommitBattleLocalViewsToPartyStateTyped(
        BattleState battleState,
        PartyState partyState
    )
    {
        return CommitBattleLocalViewsToPartyStateInternal(battleState, partyState);
    }

    public Dictionary commit_battle_local_views_to_party_state(
        BattleState battleState,
        PartyState partyState
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

    private BattleLocalWritebackResult CommitBattleLocalViewsToPartyStateInternal(
        BattleState battleState,
        PartyState partyState
    )
    {
        if (battleState == null)
            return BattleLocalWritebackResult.Failed(
                "battle_local_writeback_missing_battle_state"
            );
        if (partyState == null)
            return BattleLocalWritebackResult.Failed(
                "battle_local_writeback_missing_party_state"
            );

        var candidateParty = ClonePartyStateForBattleWriteback(partyState);
        if (candidateParty == null)
            return BattleLocalWritebackResult.Failed(
                "battle_local_writeback_invalid_party_state"
            );

        var backpackView = battleState.get_party_backpack_view();
        if (backpackView == null)
            return BattleLocalWritebackResult.Failed(
                "battle_local_writeback_invalid_backpack_view"
            );

        WarehouseState warehouseState = backpackView.duplicate_state();
        if (warehouseState == null)
            return BattleLocalWritebackResult.Failed(
                "battle_local_writeback_invalid_backpack_view"
            );

        candidateParty.warehouse_state = warehouseState;

        var committedMemberIds = new Dictionary();
        foreach (var allyUnitId in battleState.ally_unit_ids)
        {
            var unitState = battleState.units[allyUnitId].As<BattleUnitState>();
            if (unitState == null)
                return BattleLocalWritebackResult.FromFailureDictionary(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_missing_ally_unit",
                        new Dictionary { ["unit_id"] = allyUnitId.ToString() }
                    )
                );

            var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
            if (memberId == "")
                continue;

            if (committedMemberIds.ContainsKey(memberId))
                return BattleLocalWritebackResult.FromFailureDictionary(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_duplicate_member_unit",
                        new Dictionary { ["member_id"] = memberId.ToString() }
                    )
                );

            PartyMemberState memberState = candidateParty.get_member_state(memberId);
            if (memberState == null)
                return BattleLocalWritebackResult.FromFailureDictionary(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_member_not_found",
                        new Dictionary
                        {
                            ["member_id"] = memberId.ToString(),
                            ["unit_id"] = unitState.unit_id.ToString(),
                        }
                    )
                );

            if (!unitState.equipment_view_initialized)
                return BattleLocalWritebackResult.FromFailureDictionary(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_uninitialized_equipment_view",
                        new Dictionary
                        {
                            ["member_id"] = memberId.ToString(),
                            ["unit_id"] = unitState.unit_id.ToString(),
                        }
                    )
                );

            var equipmentView = unitState.equipment_view;
            if (equipmentView == null)
                return BattleLocalWritebackResult.FromFailureDictionary(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_invalid_equipment_view",
                        new Dictionary
                        {
                            ["member_id"] = memberId.ToString(),
                            ["unit_id"] = unitState.unit_id.ToString(),
                        }
                    )
                );

            EquipmentState equipmentCopy = equipmentView.duplicate_state();
            if (equipmentCopy == null)
                return BattleLocalWritebackResult.FromFailureDictionary(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_invalid_equipment_view",
                        new Dictionary
                        {
                            ["member_id"] = memberId.ToString(),
                            ["unit_id"] = unitState.unit_id.ToString(),
                        }
                    )
                );

            memberState.equipment_state = equipmentCopy;
            committedMemberIds[memberId] = true;
        }

        var validationResult = ValidateBattleLocalCandidatePartyState(candidateParty);
        if (!validationResult.Ok)
            return BattleLocalWritebackResult.FromFailureDictionary(validationResult.Failure);

        _runtime.set_party_state(candidateParty);
        SyncRuntimePartyServicesAfterBattleLocalWriteback();

        return BattleLocalWritebackResult.Success(
            committedMemberIds.Count,
            validationResult.UsedSlots,
            validationResult.Capacity
        );
    }

    private PartyState ClonePartyStateForBattleWriteback(PartyState partyState)
    {
        if (partyState == null)
            return null;
        return partyState.duplicate_state();
    }

    private BattleLocalCandidateValidationResult ValidateBattleLocalCandidatePartyState(
        PartyState candidateParty
    )
    {
        var warehouseState = candidateParty?.warehouse_state;
        if (candidateParty == null || warehouseState == null)
            return BattleLocalCandidateValidationResult.Failed(
                BuildBattleLocalWritebackFailure(
                    "battle_local_writeback_invalid_candidate_party"
                )
            );

        var instanceOwnerById = new Dictionary();
        foreach (EquipmentInstanceState instanceObj in warehouseState.GetNonEmptyEquipmentInstancesTyped())
        {
            if (instanceObj == null)
                continue;
            var instanceId = ProgressionDataUtils.to_string_name(instanceObj.instance_id);
            var itemId = ProgressionDataUtils.to_string_name(instanceObj.item_id);
            if (!TryRegisterBattleLocalInstanceOwner(
                instanceOwnerById,
                instanceId,
                itemId,
                "backpack",
                out Dictionary failure
            ))
                return BattleLocalCandidateValidationResult.Failed(failure);
        }

        foreach (var memberIdStr in ProgressionDataUtils.sorted_string_keys(candidateParty.member_states))
        {
            var memberId = (StringName)memberIdStr;
            PartyMemberState memberState = candidateParty.get_member_state(memberId);
            if (memberState == null)
                continue;

            EquipmentState equipmentState = memberState.equipment_state;
            if (equipmentState == null)
                return BattleLocalCandidateValidationResult.Failed(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_invalid_equipment_state",
                        new Dictionary { ["member_id"] = memberId.ToString() }
                    )
                );

            foreach (StringName entrySlotId in equipmentState.GetEntrySlotIdsTyped())
            {
                var itemId = ProgressionDataUtils.to_string_name(
                    equipmentState.get_equipped_item_id(entrySlotId)
                );
                if (itemId == "")
                    return BattleLocalCandidateValidationResult.Failed(
                        BuildBattleLocalWritebackFailure(
                            "battle_local_writeback_invalid_equipment_entry",
                            new Dictionary
                            {
                                ["member_id"] = memberId.ToString(),
                                ["entry_slot_id"] = entrySlotId.ToString(),
                            }
                        )
                    );

                var instanceId = ProgressionDataUtils.to_string_name(
                    equipmentState.get_equipped_instance_id(entrySlotId)
                );
                if (instanceId == "")
                    continue;

                var ownerLabel = string.Format("equipment:{0}:{1}", memberId, entrySlotId);
                if (!TryRegisterBattleLocalInstanceOwner(
                    instanceOwnerById,
                    instanceId,
                    itemId,
                    ownerLabel,
                    out Dictionary failure
                ))
                    return BattleLocalCandidateValidationResult.Failed(failure);
            }
        }

        var itemDefs = GetRuntimeItemDefs();
        var capacityService = new PartyWarehouseService();
        capacityService.setup(candidateParty, itemDefs);
        var usedSlots = capacityService.get_used_slots();
        var capacity = capacityService.get_total_capacity();

        if (usedSlots > capacity)
            return BattleLocalCandidateValidationResult.Failed(
                BuildBattleLocalWritebackFailure(
                    "battle_local_writeback_capacity_mismatch",
                    new Dictionary { ["used_slots"] = usedSlots, ["capacity"] = capacity }
                )
            );

        return BattleLocalCandidateValidationResult.Success(usedSlots, capacity);
    }

    private bool TryRegisterBattleLocalInstanceOwner(
        Dictionary instanceOwnerById,
        StringName instanceId,
        StringName itemId,
        string ownerLabel,
        out Dictionary failure
    )
    {
        failure = new Dictionary();
        if (instanceId == "")
            return true;

        var instanceKey = instanceId.ToString();
        if (instanceOwnerById.ContainsKey(instanceKey))
        {
            var previousOwner = DictionaryDictionary(instanceOwnerById, instanceKey);
            failure = BuildBattleLocalWritebackFailure(
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
            return false;
        }

        instanceOwnerById[instanceKey] = new Dictionary
        {
            ["owner"] = ownerLabel,
            ["item_id"] = itemId.ToString(),
        };
        return true;
    }

    private void SyncRuntimePartyServicesAfterBattleLocalWriteback()
    {
        var itemDefs = GetRuntimeItemDefs();
        PartyState partyState = _runtime?.get_party_state();

        CharacterManagementModule characterManagement = _runtime?.get_character_management();
        if (characterManagement != null)
            characterManagement.set_party_state(partyState);

        PartyWarehouseService partyWarehouseService = _runtime?.get_party_warehouse_service();
        if (partyWarehouseService != null)
            _runtime._setup_party_warehouse_service(partyWarehouseService, partyState, itemDefs);

        PartyItemUseService partyItemUseService = _runtime?.get_party_item_use_service();
        if (partyItemUseService != null)
        {
            var skillDefs = new Dictionary();
            GameSession gameSession = _runtime?.get_game_session();
            if (gameSession != null)
                skillDefs = gameSession.get_skill_defs();
            partyItemUseService.setup(
                partyState,
                itemDefs,
                skillDefs,
                partyWarehouseService,
                characterManagement
            );
        }

        PartyEquipmentService partyEquipmentService = _runtime?.get_party_equipment_service();
        if (partyEquipmentService != null)
        {
            var allocator = _runtime._get_equipment_instance_id_allocator();
            partyEquipmentService.setup(
                partyState,
                itemDefs,
                partyWarehouseService,
                allocator
            );
        }
    }

    private Dictionary GetRuntimeItemDefs()
    {
        GameSession gameSession = _runtime?.get_game_session();
        if (gameSession != null)
            return gameSession.get_item_defs();
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
        _runtime.update_status(statusMessage);
        _runtime._log_runtime_event(
            "error",
            "battle",
            "battle.local_writeback_inoption_failed",
            _runtime.get_status_text(),
            Json.Stringify(new Dictionary
            {
                ["battle"] = battleSummary,
                ["winner_faction_id"] = winnerFactionId,
                ["error_code"] = errorCode,
                ["details"] = details,
            })
        );
        System.Diagnostics.Debug.Assert(false, message);
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

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GameRuntimeFacade target))
            return null;
        return target;
    }
}
