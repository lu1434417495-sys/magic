using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

internal sealed class FateRuntimeModule
{
    private const int BaseCalamityCap = 3;
    private const int BlackStarBrandRepeatCalamityCost = 1;

    private static readonly StringName BlackStarBrandSkillId = "black_star_brand";
    private static readonly StringName CrownBreakSkillId = "crown_break";
    private static readonly StringName DoomSentenceSkillId = "doom_sentence";
    private static readonly StringName BlackCrownSealSkillId = "black_crown_seal";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";

    private IBattleRuntimeCharacterGateway _characterGateway;
    private BattleRuntimeModule _battleRuntimeGateway;
    private BattleFateEventBus _fateEventBus;
    private Func<StringName, BattleUnitState> _unitByMemberIdResolver;
    private FortuneService _fortuneService = new();
    private FortunaGuidanceService _fortunaGuidanceService = new();
    private LowLuckEventService _lowLuckEventService = new();
    private MisfortuneGuidanceService _misfortuneGuidanceService = new();
    private MisfortuneService _misfortuneService = new();

    internal void Setup(
        IBattleRuntimeCharacterGateway character_gateway = null,
        BattleFateEventBus fate_event_bus = null,
        BattleRuntimeModule battle_runtime_gateway = null,
        Func<StringName, BattleUnitState> unit_by_member_id_resolver = null
    )
    {
        _characterGateway = character_gateway;
        _battleRuntimeGateway = battle_runtime_gateway;
        _unitByMemberIdResolver = unit_by_member_id_resolver;

        // Guidance must see the pre-mark state before FortuneService mutates fortune_marked on the same bus event.
        _fortunaGuidanceService?.Setup(_characterGateway);
        _fortuneService?.Setup(_characterGateway);
        _lowLuckEventService?.Setup(_characterGateway);
        BindFateEventBusAdapters(fate_event_bus);
        _misfortuneService?.Setup(fate_event_bus, _unitByMemberIdResolver);
        _misfortuneGuidanceService?.Setup(_characterGateway, _battleRuntimeGateway);
    }

    internal void DisposeRuntime()
    {
        BindFateEventBusAdapters(null);
        _fortunaGuidanceService?.Dispose();
        _fortuneService?.Dispose();
        _misfortuneService?.Dispose();
        _lowLuckEventService?.Dispose();
        _misfortuneGuidanceService?.Dispose();
        _characterGateway = null;
        _battleRuntimeGateway = null;
        _fateEventBus = null;
        _unitByMemberIdResolver = null;
    }

    internal FateRuntimeRollbackState CaptureRollbackState() =>
        new(_lowLuckEventService?.CaptureRollbackState());

    internal void RestoreRollbackState(FateRuntimeRollbackState snapshot)
    {
        if (snapshot == null)
            return;
        _lowLuckEventService?.RestoreRollbackState(snapshot.LowLuckEventState);
    }

    internal void BeginBattle(BattleCalamityStore calamity_store = null)
    {
        _misfortuneService?.BeginBattle(calamity_store ?? new BattleCalamityStore());
    }

    internal int GetMemberCalamity(StringName member_id)
    {
        if (_misfortuneService == null)
            return 0;
        return _misfortuneService.GetMemberCalamity(member_id);
    }

    internal int GetMemberCalamityCap(StringName member_id)
    {
        if (_misfortuneService == null)
            return BaseCalamityCap;
        return _misfortuneService.GetMemberCalamityCap(member_id);
    }

    internal int GetBlackStarBrandCastCost(StringName member_id)
    {
        if (_misfortuneService == null)
            return BlackStarBrandRepeatCalamityCost;
        return _misfortuneService.GetBlackStarBrandCalamityCost(member_id);
    }

    internal bool HasMisfortuneReason(StringName member_id, StringName reason_id)
    {
        return _misfortuneService != null
            && _misfortuneService.HasTriggeredReason(member_id, reason_id);
    }

    internal string GetMisfortuneSkillCastBlockReason(
        BattleUnitState unit_state,
        StringName skill_id
    )
    {
        if (_misfortuneService == null)
            return GetSkillSidecarMissingMessage(skill_id);
        return _misfortuneService.GetSkillCastBlockMessage(unit_state, skill_id);
    }

    internal MisfortuneSkillCastResult ConsumeMisfortuneSkillCastResult(
        BattleUnitState unit_state,
        StringName skill_id
    )
    {
        if (_misfortuneService == null)
        {
            return MisfortuneSkillCastResult.Failure(GetSkillSidecarMissingMessage(skill_id));
        }
        return _misfortuneService.ConsumeSkillCastResult(unit_state, skill_id);
    }

    internal GDictionary HandleMisfortuneTrigger(MisfortuneTriggerRequest request)
    {
        if (_misfortuneService == null)
            return new GDictionary();
        return _misfortuneService.HandleTrigger(request);
    }

    internal GDictionary HandleMemberBossPhaseChanged(
        StringName member_id,
        StringName phase_id = default
    )
    {
        BattleUnitState unitState = ResolveUnitByMemberId(member_id);
        if (unitState == null)
            return new GDictionary();
        GDictionary result = HandleMisfortuneTrigger(
            MisfortuneTriggerRequest.BossPhaseChanged(unitState, phase_id)
        );
        return result;
    }

    internal GDictionary HandleAppliedStatuses(BattleUnitState target_unit, GArray status_effect_ids)
    {
        if (_misfortuneService == null)
            return new GDictionary();
        return _misfortuneService.HandleAppliedStatuses(
            target_unit,
            status_effect_ids ?? new GArray()
        );
    }

    internal GDictionary HandleAppliedStatuses(
        BattleUnitState target_unit,
        GStringNameArray status_effect_ids
    )
    {
        if (_misfortuneService == null)
            return new GDictionary();
        return _misfortuneService.HandleAppliedStatuses(
            target_unit,
            status_effect_ids ?? new GStringNameArray()
        );
    }

    internal GDictionary HandleBattleResolution(
        BattleState battle_state,
        BattleResolutionResult battle_resolution_result,
        ICollection<PendingCharacterReward> pendingCharacterRewards = null
    )
    {
        LowLuckEventResult lowLuckEventResult = new();
        if (_lowLuckEventService != null)
        {
            lowLuckEventResult = _lowLuckEventService.HandleBattleResolution(
                BuildLowLuckBattleResolutionInput(battle_state, battle_resolution_result)
            );
            MergeLowLuckBattleResultIntoResolution(
                battle_resolution_result,
                lowLuckEventResult,
                pendingCharacterRewards
            );
        }

        Godot.Collections.Array<StringName> fortunaGuidanceUnlocks = new();
        if (_fortunaGuidanceService != null)
            fortunaGuidanceUnlocks = ToStringNameArray(
                _fortunaGuidanceService.HandleBattleResolution(
                    battle_state,
                    battle_resolution_result
                )
            );

        Godot.Collections.Array<StringName> misfortuneGuidanceUnlocks = new();
        if (_misfortuneGuidanceService != null)
            misfortuneGuidanceUnlocks = ToStringNameArray(
                _misfortuneGuidanceService.HandleBattleResolution(
                    battle_state,
                    battle_resolution_result
                )
            );

        return new GDictionary
        {
            ["fortuna_guidance_unlocks"] = fortunaGuidanceUnlocks,
            ["misfortune_guidance_unlocks"] = misfortuneGuidanceUnlocks,
            ["low_luck_event_result"] = LowLuckEventResultToDictionary(lowLuckEventResult),
        };
    }

    internal Godot.Collections.Array<StringName> HandleFortunaChapterCompleted(GDictionary payload)
    {
        if (_fortunaGuidanceService == null)
            return new Godot.Collections.Array<StringName>();
        return ToStringNameArray(
            _fortunaGuidanceService.HandleChapterCompleted(
                BuildFortunaChapterCompletionInput(payload ?? new GDictionary())
            )
        );
    }

    internal Godot.Collections.Array<StringName> HandleMisfortuneForgeResult(
        StringName member_id,
        SettlementServiceResult result,
        IReadOnlyDictionary<StringName, ItemDef> item_defs = null
    )
    {
        if (_misfortuneGuidanceService == null)
            return new Godot.Collections.Array<StringName>();
        return ToStringNameArray(
            _misfortuneGuidanceService.HandleForgeResult(
                member_id,
                BuildMisfortuneForgeGuidanceInput(result),
                item_defs != null
                    ? new Dictionary<StringName, ItemDef>(item_defs)
                    : new Dictionary<StringName, ItemDef>()
            )
        );
    }

    internal GDictionary ResolveLowLuckSettlementEventRewards(GDictionary context)
    {
        if (_lowLuckEventService == null)
            return new GDictionary();
        return LowLuckEventResultToDictionary(
            _lowLuckEventService.HandleSettlementAction(
                BuildLowLuckSettlementActionInput(context ?? new GDictionary())
            )
        );
    }

    internal void ClearMisfortuneExaltedReadyFlags(GArray member_ids = null)
    {
        if (_misfortuneGuidanceService != null)
            _misfortuneGuidanceService.ClearExaltedReadyFlags(
                ReadStringNameList(member_ids ?? new GArray())
            );
    }

    internal IReadOnlyDictionary<StringName, int> GetCalamityByMemberIdSnapshot()
    {
        return _misfortuneService?.GetCalamityByMemberIdSnapshot()
            ?? new Dictionary<StringName, int>();
    }

    private void BindFateEventBusAdapters(BattleFateEventBus fateEventBus = null)
    {
        if (_fateEventBus != null)
        {
            _fateEventBus.EventDispatched -= OnFortunaGuidanceFateEvent;
            _fateEventBus.EventDispatched -= OnFortuneFateEvent;
            _fateEventBus.EventDispatched -= OnLowLuckFateEvent;
        }
        _fateEventBus = fateEventBus;
        if (_fateEventBus != null)
        {
            _fateEventBus.EventDispatched += OnFortunaGuidanceFateEvent;
            _fateEventBus.EventDispatched += OnFortuneFateEvent;
            _fateEventBus.EventDispatched += OnLowLuckFateEvent;
        }
    }

    private void OnFortunaGuidanceFateEvent(BattleFateEventPayload payload)
    {
        _fortunaGuidanceService?.HandleFateEvent(
            (payload ?? BattleFateEventPayload.Empty()).ToFortunaGuidancePayload().ToInput()
        );
    }

    private void OnFortuneFateEvent(BattleFateEventPayload payload)
    {
        if (payload == null || payload.EventType != FortuneService.CriticalSuccessUnderDisadvantageEventId)
            return;
        _fortuneService?.TryGrantFortuneMark(
            payload.ToFortuneCriticalPayload().ToFortuneMarkEventInput()
        );
    }

    private void OnLowLuckFateEvent(BattleFateEventPayload payload)
    {
        _lowLuckEventService?.HandleFateEvent(
            payload?.EventType ?? new StringName(""),
            (payload ?? BattleFateEventPayload.Empty()).ToLowLuckPayload()
        );
    }

    private BattleUnitState ResolveUnitByMemberId(StringName memberId)
    {
        StringName normalizedMemberId = ProgressionDataUtils.to_string_name(memberId);
        if (IsEmpty(normalizedMemberId) || _unitByMemberIdResolver == null)
            return null;
        return _unitByMemberIdResolver.Invoke(normalizedMemberId);
    }

    private static void MergeLowLuckBattleResultIntoResolution(
        BattleResolutionResult battleResolutionResult,
        LowLuckEventResult lowLuckEventResult,
        ICollection<PendingCharacterReward> pendingCharacterRewards
    )
    {
        if (
            battleResolutionResult == null
            || lowLuckEventResult == null
            || lowLuckEventResult.IsEmpty
        )
            return;

        if (lowLuckEventResult.LootEntries.Count > 0)
        {
            List<BattleLootEntry> mergedLootEntries = new();
            foreach (BattleLootEntry entry in battleResolutionResult.loot_entries)
            {
                BattleLootEntry duplicate = entry?.Duplicate();
                if (duplicate != null)
                    mergedLootEntries.Add(duplicate);
            }
            foreach (LowLuckLootEntry entry in lowLuckEventResult.LootEntries)
            {
                BattleLootEntry lootEntry = LowLuckLootEntryToBattleLootEntry(entry);
                if (lootEntry != null)
                    mergedLootEntries.Add(lootEntry);
            }
            battleResolutionResult.SetLootEntries(mergedLootEntries);
        }

        if (
            pendingCharacterRewards != null
            && lowLuckEventResult.PendingCharacterRewards.Count > 0
        )
        {
            foreach (PendingCharacterReward reward in lowLuckEventResult.PendingCharacterRewards)
            {
                if (reward != null && !reward.IsEmpty())
                    pendingCharacterRewards.Add(reward.DuplicateState());
            }
        }
    }

    private static string GetSkillSidecarMissingMessage(StringName skillId)
    {
        StringName normalizedSkillId = ProgressionDataUtils.to_string_name(skillId);
        if (normalizedSkillId == BlackStarBrandSkillId)
            return "黑星烙印的 calamity sidecar 未初始化。";
        if (normalizedSkillId == CrownBreakSkillId)
            return "折冠的 calamity sidecar 未初始化。";
        if (normalizedSkillId == DoomSentenceSkillId)
            return "厄命宣判的 calamity sidecar 未初始化。";
        if (normalizedSkillId == BlackCrownSealSkillId)
            return "黑冠封印的 battle sidecar 未初始化。";
        return "Misfortune battle sidecar 未初始化。";
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        var value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static MisfortuneForgeGuidanceInput BuildMisfortuneForgeGuidanceInput(
        SettlementServiceResult result
    )
    {
        if (result == null)
            return MisfortuneForgeGuidanceInput.Empty;
        return BuildMisfortuneForgeGuidanceInput(
            result.Success,
            SettlementServiceResultProjection.ProjectInventoryDelta(result),
            SettlementServiceResultProjection.ProjectServiceSideEffects(result)
        );
    }

    private static MisfortuneForgeGuidanceInput BuildMisfortuneForgeGuidanceInput(
        GDictionary result
    )
    {
        if (result == null)
            return MisfortuneForgeGuidanceInput.Empty;
        return BuildMisfortuneForgeGuidanceInput(
            ReadBool(result, "success"),
            ReadDictionary(result, "inventory_delta"),
            ReadDictionary(result, "service_side_effects")
        );
    }

    private static MisfortuneForgeGuidanceInput BuildMisfortuneForgeGuidanceInput(
        bool success,
        GDictionary inventoryDelta,
        GDictionary serviceSideEffects
    )
    {
        return new MisfortuneForgeGuidanceInput(
            success,
            ReadForgeGuidanceEntries(inventoryDelta, "removed_entries"),
            ReadForgeGuidanceEntries(inventoryDelta, "added_entries"),
            ReadStringName(serviceSideEffects, "output_item_id")
        );
    }

    private static LowLuckBattleResolutionInput BuildLowLuckBattleResolutionInput(
        BattleState battleState,
        BattleResolutionResult battleResolutionResult
    )
    {
        StringName battleId = "";
        if (battleResolutionResult != null && battleResolutionResult.battle_id != "")
            battleId = battleResolutionResult.battle_id;
        else if (battleState != null)
            battleId = battleState.battle_id;

        bool playerWon =
            battleResolutionResult != null
            && battleResolutionResult.winner_faction_id == "player";

        return new LowLuckBattleResolutionInput(
            battleId,
            playerWon,
            BuildLowLuckBattleUnitSnapshots(battleState)
        );
    }

    private static List<LowLuckBattleUnitSnapshot> BuildLowLuckBattleUnitSnapshots(
        BattleState battleState
    )
    {
        List<LowLuckBattleUnitSnapshot> units = new();
        if (battleState == null)
            return units;

        bool hasExplicitEnemyUnitIds =
            battleState.enemy_unit_ids != null && battleState.enemy_unit_ids.Count > 0;
        foreach (BattleUnitState unitState in battleState.Units())
        {
            if (unitState == null)
                continue;

            bool isEnemy = hasExplicitEnemyUnitIds
                ? ContainsStringName(battleState.enemy_unit_ids, unitState.unit_id)
                : unitState.faction_id != "player";
            bool isEliteOrBoss =
                unitState.attribute_snapshot != null
                && unitState.attribute_snapshot.GetValue(FortuneMarkTargetStatId) > 0;
            units.Add(
                new LowLuckBattleUnitSnapshot(
                    unitState.unit_id,
                    unitState.source_member_id,
                    unitState.faction_id,
                    unitState.is_alive,
                    isEnemy,
                    isEliteOrBoss
                )
            );
        }
        return units;
    }

    private static LowLuckSettlementActionInput BuildLowLuckSettlementActionInput(
        GDictionary context
    )
    {
        return new LowLuckSettlementActionInput(
            ReadString(context, "action_id"),
            ReadString(context, "interaction_script_id"),
            ReadString(context, "facility_id"),
            ReadString(context, "facility_name"),
            ReadString(context, "service_type")
        );
    }

    private static GDictionary LowLuckEventResultToDictionary(LowLuckEventResult result)
    {
        Godot.Collections.Array<StringName> triggeredEventIds = new();
        GArray lootEntries = new();
        GArray pendingCharacterRewards = new();

        if (result != null)
        {
            foreach (StringName eventId in result.TriggeredEventIds)
                triggeredEventIds.Add(eventId);
            foreach (LowLuckLootEntry entry in result.LootEntries)
                lootEntries.Add(LowLuckLootEntryToDictionary(entry));
            foreach (PendingCharacterReward reward in result.PendingCharacterRewards)
            {
                if (reward != null && !reward.IsEmpty())
                    pendingCharacterRewards.Add(PendingCharacterRewardPayload.Project(reward));
            }
        }

        return new GDictionary
        {
            ["triggered_event_ids"] = triggeredEventIds,
            ["loot_entries"] = lootEntries,
            ["pending_character_rewards"] = pendingCharacterRewards,
        };
    }

    private static GDictionary LowLuckLootEntryToDictionary(LowLuckLootEntry entry)
    {
        return BattleLootEntryPayload.ProjectEntry(LowLuckLootEntryToBattleLootEntry(entry));
    }

    private static BattleLootEntry LowLuckLootEntryToBattleLootEntry(LowLuckLootEntry entry) =>
        BattleLootEntry.Create(
            entry.DropKind,
            entry.DropSourceKind,
            entry.DropSourceId,
            entry.DropSourceLabel ?? "",
            entry.DropEntryId,
            entry.ItemId,
            Math.Max(entry.Quantity, 0)
        );

    private static bool ContainsStringName(
        IEnumerable<StringName> values,
        StringName targetValue
    )
    {
        StringName normalizedTargetValue = ProgressionDataUtils.to_string_name(targetValue);
        if (values == null || normalizedTargetValue == "")
            return false;
        foreach (StringName value in values)
        {
            if (ProgressionDataUtils.to_string_name(value) == normalizedTargetValue)
                return true;
        }
        return false;
    }

    private static FortunaChapterCompletionInput BuildFortunaChapterCompletionInput(
        GDictionary payload
    )
    {
        return new FortunaChapterCompletionInput
        {
            MemberIds = ReadStringNameList(ReadArray(payload, "member_ids")),
            HadPermanentDeath = ReadBool(payload, "had_permanent_death"),
        };
    }

    private static List<MisfortuneForgeGuidanceItemEntry> ReadForgeGuidanceEntries(
        GDictionary source,
        string key
    )
    {
        var result = new List<MisfortuneForgeGuidanceItemEntry>();
        foreach (Variant entryValue in ReadArray(source, key))
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary entryData = entryValue.AsGodotDictionary();
            var itemId = ReadStringName(entryData, "item_id");
            int quantity = ReadInt(entryData, "quantity");
            if (itemId != "" && quantity > 0)
                result.Add(new MisfortuneForgeGuidanceItemEntry(itemId, quantity));
        }
        return result;
    }

    private static List<StringName> ReadStringNameList(GArray values)
    {
        var result = new List<StringName>();
        if (values == null)
            return result;
        foreach (Variant value in values)
        {
            var stringName = ProgressionDataUtils.to_string_name(value);
            if (stringName != "" && !result.Contains(stringName))
                result.Add(stringName);
        }
        return result;
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        if (values == null)
            return result;
        foreach (var value in values)
        {
            var stringName = ProgressionDataUtils.to_string_name(value);
            if (stringName != "" && !result.Contains(stringName))
                result.Add(stringName);
        }
        return result;
    }

    private static GDictionary ReadDictionary(GDictionary data, string key)
    {
        var value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static bool ReadBool(GDictionary data, string key)
    {
        var value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Bool && value.AsBool();
    }

    private static int ReadInt(GDictionary data, string key)
    {
        var value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    private static string ReadString(GDictionary data, string key)
    {
        Variant value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Nil ? "" : value.AsString();
    }

    private static StringName ReadStringName(GDictionary data, string key)
    {
        return ProgressionDataUtils.to_string_name(ReadValue(data, key));
    }

    private static Variant ReadValue(GDictionary data, string key)
    {
        if (data == null)
            return default;
        if (data.ContainsKey(key))
            return data[key];
        return default;
    }
}

internal sealed class FateRuntimeRollbackState
{
    internal FateRuntimeRollbackState(LowLuckEventRollbackState lowLuckEventState)
    {
        LowLuckEventState = lowLuckEventState;
    }

    internal LowLuckEventRollbackState LowLuckEventState { get; }
}
