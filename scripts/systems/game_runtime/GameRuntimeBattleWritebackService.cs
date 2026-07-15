using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using PlainDictionary = System.Collections.Generic.Dictionary<string, object>;

internal sealed class GameRuntimeBattleWritebackService : IDisposable
{
    private WeakReference<GameRuntimeFacade> _runtimeRef;

    internal sealed class BattleLocalWritebackResult
    {
        internal bool Ok { get; }
        internal string ErrorCode { get; }
        internal IReadOnlyDictionary<string, object> Details { get; }
        internal int CommittedMemberCount { get; }
        internal int UsedSlots { get; }
        internal int Capacity { get; }

        private BattleLocalWritebackResult(
            bool ok,
            string errorCode,
            IReadOnlyDictionary<string, object> details,
            int committedMemberCount,
            int usedSlots,
            int capacity
        )
        {
            Ok = ok;
            ErrorCode = errorCode ?? "";
            Details = ContentValueNormalizer.NormalizeDictionary(
                details ?? new PlainDictionary(StringComparer.Ordinal),
                "GameRuntimeBattleWritebackService.Result.details"
            );
            CommittedMemberCount = Mathf.Max(committedMemberCount, 0);
            UsedSlots = Mathf.Max(usedSlots, 0);
            Capacity = Mathf.Max(capacity, 0);
        }

        internal static BattleLocalWritebackResult Success(
            int committedMemberCount,
            int usedSlots,
            int capacity
        ) =>
            new(
                true,
                "",
                new PlainDictionary(StringComparer.Ordinal),
                committedMemberCount,
                usedSlots,
                capacity
            );

        internal static BattleLocalWritebackResult Failed(
            string errorCode,
            IReadOnlyDictionary<string, object> details = null
        ) =>
            new(false, errorCode, details, 0, 0, 0);

        internal static BattleLocalWritebackResult FromFailure(
            BattleLocalWritebackFailure failure
        )
        {
            return failure == null
                ? Failed("battle_local_writeback_inoption_failed")
                : Failed(failure.ErrorCode, failure.Details);
        }

    }

    internal sealed class BattleLocalWritebackFailure
    {
        private BattleLocalWritebackFailure(
            string errorCode,
            IReadOnlyDictionary<string, object> details
        )
        {
            ErrorCode = errorCode ?? "";
            Details = ContentValueNormalizer.NormalizeDictionary(
                details ?? new PlainDictionary(StringComparer.Ordinal),
                "GameRuntimeBattleWritebackService.Failure.details"
            );
        }

        internal string ErrorCode { get; }
        internal IReadOnlyDictionary<string, object> Details { get; }

        internal static BattleLocalWritebackFailure Create(
            string errorCode,
            IReadOnlyDictionary<string, object> details = null
        ) => new(errorCode, details);
    }

    private sealed class BattleLocalCandidateValidationResult
    {
        private BattleLocalCandidateValidationResult(
            bool ok,
            BattleLocalWritebackFailure failure,
            int usedSlots,
            int capacity
        )
        {
            Ok = ok;
            Failure = failure;
            UsedSlots = Mathf.Max(usedSlots, 0);
            Capacity = Mathf.Max(capacity, 0);
        }

        internal bool Ok { get; }
        internal BattleLocalWritebackFailure Failure { get; }
        internal int UsedSlots { get; }
        internal int Capacity { get; }

        internal static BattleLocalCandidateValidationResult Success(int usedSlots, int capacity)
        {
            return new BattleLocalCandidateValidationResult(true, null, usedSlots, capacity);
        }

        internal static BattleLocalCandidateValidationResult Failed(
            BattleLocalWritebackFailure failure
        )
        {
            return new BattleLocalCandidateValidationResult(
                false,
                failure
                    ?? BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_invalid_candidate_party"
                    ),
                0,
                0
            );
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

    internal BattleLocalWritebackResult CommitBattleLocalViewsToPartyStateTyped(
        BattleState battleState,
        PartyState partyState
    )
    {
        return CommitBattleLocalViewsToPartyStateInternal(battleState, partyState);
    }

    private void ReportConsistencyFailure(
        Dictionary writebackResult,
        Dictionary battleSummary,
        string winnerFactionId
    )
    {
        ReportBattleLocalWritebackConsistencyFailure(writebackResult, battleSummary, winnerFactionId);
    }

    internal void ReportInoptionFailure(
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

        var backpackView = battleState.GetPartyBackpackView();
        if (backpackView == null)
            return BattleLocalWritebackResult.Failed(
                "battle_local_writeback_invalid_backpack_view"
            );

        WarehouseState warehouseState = backpackView.DuplicateState();
        if (warehouseState == null)
            return BattleLocalWritebackResult.Failed(
                "battle_local_writeback_invalid_backpack_view"
            );

        candidateParty.warehouse_state = warehouseState;

        var committedMemberIds = new Dictionary();
        foreach (var allyUnitId in battleState.ally_unit_ids)
        {
            var unitState = battleState.GetUnit(allyUnitId);
            if (unitState == null)
                return BattleLocalWritebackResult.FromFailure(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_missing_ally_unit",
                        new PlainDictionary(StringComparer.Ordinal)
                        {
                            ["unit_id"] = allyUnitId.ToString(),
                        }
                    )
                );

            var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
            if (memberId == "")
                continue;

            if (committedMemberIds.ContainsKey(memberId))
                return BattleLocalWritebackResult.FromFailure(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_duplicate_member_unit",
                        new PlainDictionary(StringComparer.Ordinal)
                        {
                            ["member_id"] = memberId.ToString(),
                        }
                    )
                );

            PartyMemberState memberState = candidateParty.GetMemberState(memberId);
            if (memberState == null)
                return BattleLocalWritebackResult.FromFailure(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_member_not_found",
                        new PlainDictionary(StringComparer.Ordinal)
                        {
                            ["member_id"] = memberId.ToString(),
                            ["unit_id"] = unitState.unit_id.ToString(),
                        }
                    )
                );

            if (!unitState.equipment_view_initialized)
                return BattleLocalWritebackResult.FromFailure(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_uninitialized_equipment_view",
                        new PlainDictionary(StringComparer.Ordinal)
                        {
                            ["member_id"] = memberId.ToString(),
                            ["unit_id"] = unitState.unit_id.ToString(),
                        }
                    )
                );

            var equipmentView = unitState.equipment_view;
            if (equipmentView == null)
                return BattleLocalWritebackResult.FromFailure(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_invalid_equipment_view",
                        new PlainDictionary(StringComparer.Ordinal)
                        {
                            ["member_id"] = memberId.ToString(),
                            ["unit_id"] = unitState.unit_id.ToString(),
                        }
                    )
                );

            EquipmentState equipmentCopy = equipmentView.DuplicateState();
            if (equipmentCopy == null)
                return BattleLocalWritebackResult.FromFailure(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_invalid_equipment_view",
                        new PlainDictionary(StringComparer.Ordinal)
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
            return BattleLocalWritebackResult.FromFailure(validationResult.Failure);

        _runtime.SetPartyState(candidateParty);
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
        return partyState.DuplicateState();
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
                out BattleLocalWritebackFailure failure
            ))
                return BattleLocalCandidateValidationResult.Failed(failure);
        }

        foreach (var memberIdStr in candidateParty.member_states.GetSortedIdStrings())
        {
            var memberId = (StringName)memberIdStr;
            PartyMemberState memberState = candidateParty.GetMemberState(memberId);
            if (memberState == null)
                continue;

            EquipmentState equipmentState = memberState.equipment_state;
            if (equipmentState == null)
                return BattleLocalCandidateValidationResult.Failed(
                    BuildBattleLocalWritebackFailure(
                        "battle_local_writeback_invalid_equipment_state",
                        new PlainDictionary(StringComparer.Ordinal)
                        {
                            ["member_id"] = memberId.ToString(),
                        }
                    )
                );

            foreach (StringName entrySlotId in equipmentState.GetEntrySlotIdsTyped())
            {
                var itemId = ProgressionDataUtils.to_string_name(
                    equipmentState.GetEquippedItemId(entrySlotId)
                );
                if (itemId == "")
                    return BattleLocalCandidateValidationResult.Failed(
                        BuildBattleLocalWritebackFailure(
                            "battle_local_writeback_invalid_equipment_entry",
                            new PlainDictionary(StringComparer.Ordinal)
                            {
                                ["member_id"] = memberId.ToString(),
                                ["entry_slot_id"] = entrySlotId.ToString(),
                            }
                        )
                    );

                var instanceId = ProgressionDataUtils.to_string_name(
                    equipmentState.GetEquippedInstanceId(entrySlotId)
                );
                if (instanceId == "")
                    continue;

                var ownerLabel = string.Format("equipment:{0}:{1}", memberId, entrySlotId);
                if (!TryRegisterBattleLocalInstanceOwner(
                    instanceOwnerById,
                    instanceId,
                    itemId,
                    ownerLabel,
                    out BattleLocalWritebackFailure failure
                ))
                    return BattleLocalCandidateValidationResult.Failed(failure);
            }
        }

        var itemDefs = GetRuntimeItemDefsTyped();
        var capacityService = new PartyWarehouseService();
        capacityService.Setup(candidateParty, itemDefs);
        var usedSlots = capacityService.GetUsedSlots();
        var capacity = capacityService.GetTotalCapacity();

        if (usedSlots > capacity)
            return BattleLocalCandidateValidationResult.Failed(
                BuildBattleLocalWritebackFailure(
                    "battle_local_writeback_capacity_mismatch",
                    new PlainDictionary(StringComparer.Ordinal)
                    {
                        ["used_slots"] = usedSlots,
                        ["capacity"] = capacity,
                    }
                )
            );

        return BattleLocalCandidateValidationResult.Success(usedSlots, capacity);
    }

    private bool TryRegisterBattleLocalInstanceOwner(
        Dictionary instanceOwnerById,
        StringName instanceId,
        StringName itemId,
        string ownerLabel,
        out BattleLocalWritebackFailure failure
    )
    {
        failure = null;
        if (instanceId == "")
            return true;

        var instanceKey = instanceId.ToString();
        if (instanceOwnerById.ContainsKey(instanceKey))
        {
            var previousOwner = DictionaryDictionary(instanceOwnerById, instanceKey);
            failure = BuildBattleLocalWritebackFailure(
                "battle_local_writeback_instance_conflict",
                new PlainDictionary(StringComparer.Ordinal)
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
        var typedItemDefs = GetRuntimeItemDefsTyped();
        PartyState partyState = _runtime?.GetPartyState();

        CharacterManagementModule characterManagement = _runtime?.GetCharacterManagement();
        if (characterManagement != null)
            characterManagement.SetPartyState(partyState);

        PartyWarehouseService partyWarehouseService = _runtime?.GetPartyWarehouseService();
        if (partyWarehouseService != null)
            _runtime.SetupPartyWarehouseService(
                partyWarehouseService,
                partyState,
                typedItemDefs
            );

        PartyItemUseService partyItemUseService = _runtime?.GetPartyItemUseService();
        if (partyItemUseService != null)
        {
            IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions =
                new System.Collections.Generic.Dictionary<StringName, SkillDefinition>();
            GameSession gameSession = _runtime?.GetGameSession();
            if (gameSession != null)
                skillDefinitions =
                    gameSession.GetContentCatalogTyped().GetSkillDefinitionsTyped();
            partyItemUseService.Setup(
                partyState,
                typedItemDefs,
                skillDefinitions,
                partyWarehouseService,
                characterManagement
            );
        }

        PartyEquipmentService partyEquipmentService = _runtime?.GetPartyEquipmentService();
        if (partyEquipmentService != null)
        {
            var allocator = _runtime.GetEquipmentInstanceIdAllocator();
            partyEquipmentService.Setup(
                partyState,
                typedItemDefs,
                partyWarehouseService,
                allocator
            );
        }
    }

    private IReadOnlyDictionary<StringName, ItemDefinition> GetRuntimeItemDefsTyped()
    {
        GameSession gameSession = _runtime?.GetGameSession();
        if (gameSession != null)
            return gameSession.GetItemDefsTyped();
        return new System.Collections.Generic.Dictionary<StringName, ItemDefinition>();
    }

    private static BattleLocalWritebackFailure BuildBattleLocalWritebackFailure(
        string errorCode,
        IReadOnlyDictionary<string, object> details = null
    )
    {
        return BattleLocalWritebackFailure.Create(errorCode, details);
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
        using Dictionary details = DictionaryDictionary(writebackResult, "details");
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
        _runtime.UpdateStatus(statusMessage);
        _runtime._log_runtime_event(
            GameLogLevel.Error,
            "battle",
            "battle.local_writeback_inoption_failed",
            _runtime.GetStatusText(),
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
