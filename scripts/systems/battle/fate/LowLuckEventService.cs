using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class LowLuckEventService : RefCounted
{
    private static readonly StringName EventBrokenBridgeSurvival = "broken_bridge_survival";
    private static readonly StringName EventLampWithoutWitness = "lamp_without_witness";
    private static readonly StringName EventBorrowedRoad = "borrowed_road";
    private static readonly StringName EventReverseFateAmuletReward = "reverse_fate_amulet_reward";
    private static readonly StringName EventBlackStarWedgeReward = "black_star_wedge_reward";
    private static readonly StringName EventBloodDebtShawlReward = "blood_debt_shawl_reward";
    private static readonly StringName EventDeadRoadLanternReward = "dead_road_lantern_reward";

    private const string MetaFlagPrefix = "low_luck_event:";
    private const int LowLuckThreshold = -4;
    private static readonly StringName SourceTypeStoryEvent = "story_event";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName KnowledgeLampWithoutWitness = "low_luck_black_market_hint";
    private static readonly StringName KnowledgeBorrowedRoad = "low_luck_borrowed_road";

    private static readonly string[] RestFacilityKeywords =
    {
        "inn",
        "shrine",
        "gambl",
        "旅店",
        "旅舍",
        "神龛",
        "赌坊",
    };

    private IBattleRuntimeCharacterGateway _characterGateway = null;
    private BattleFateEventBus _fateEventBus = null;
    private readonly GDictionary _hardshipSurvivalByBattleId = new();
    private readonly GDictionary _criticalFailByBattleId = new();

    public static StringName EVENT_BROKEN_BRIDGE_SURVIVAL() => EventBrokenBridgeSurvival;

    public static StringName EVENT_LAMP_WITHOUT_WITNESS() => EventLampWithoutWitness;

    public static StringName EVENT_BORROWED_ROAD() => EventBorrowedRoad;

    public static StringName EVENT_REVERSE_FATE_AMULET_REWARD() => EventReverseFateAmuletReward;

    public static StringName EVENT_BLACK_STAR_WEDGE_REWARD() => EventBlackStarWedgeReward;

    public static StringName EVENT_BLOOD_DEBT_SHAWL_REWARD() => EventBloodDebtShawlReward;

    public static StringName EVENT_DEAD_ROAD_LANTERN_REWARD() => EventDeadRoadLanternReward;

    public static string META_FLAG_PREFIX() => MetaFlagPrefix;

    public void Setup(IBattleRuntimeCharacterGateway characterGateway, GodotObject fateEventBusObj)
    {
        _characterGateway = characterGateway;
        BindFateEventBus(fateEventBusObj);
    }

    public void setup(
        IBattleRuntimeCharacterGateway character_gateway = null,
        BattleFateEventBus fate_event_bus = null
    )
    {
        Setup(character_gateway, fate_event_bus);
    }

    public void BindFateEventBus(GodotObject fateEventBusObj)
    {
        if (_fateEventBus != null)
            _fateEventBus.EventDispatched -= _OnFateEvent;
        _fateEventBus = fateEventBusObj as BattleFateEventBus;
        if (_fateEventBus != null)
            _fateEventBus.EventDispatched += _OnFateEvent;
    }

    public void bind_fate_event_bus(BattleFateEventBus fate_event_bus = null)
    {
        BindFateEventBus(fate_event_bus);
    }

    public new void Dispose()
    {
        BindFateEventBus(null);
        _characterGateway = null;
        _hardshipSurvivalByBattleId.Clear();
        _criticalFailByBattleId.Clear();
        base.Dispose();
    }

    public void dispose()
    {
        BindFateEventBus(null);
        _characterGateway = null;
        _hardshipSurvivalByBattleId.Clear();
        _criticalFailByBattleId.Clear();
    }

    public GDictionary HandleBattleResolution(
        GodotObject battleState,
        GodotObject battleResolutionResult
    )
    {
        var result = _NewResult();
        var battleId = _ResolveBattleId(battleState, battleResolutionResult);
        if (battleId == "")
            return result;

        bool playerWon =
            battleResolutionResult != null
            && battleResolutionResult.Get("winner_faction_id").AsStringName() == "player";
        if (playerWon)
        {
            var hardshipMembers = _GetBattleMemberIds(_hardshipSurvivalByBattleId, battleId);
            foreach (var memberId in hardshipMembers)
            {
                if (!_IsBattleMemberAlive(battleState, memberId))
                    continue;
                if (
                    !_MarkMetaFlagIfFirst(
                        _BuildEventMetaFlagId(EventBrokenBridgeSurvival, memberId)
                    )
                )
                    continue;
                _AppendUniqueStringName(
                    result["triggered_event_ids"].AsGodotArray(),
                    EventBrokenBridgeSurvival
                );
                result["loot_entries"]
                    .AsGodotArray()
                    .Add(
                        _BuildFixedItemLootEntry(
                            EventBrokenBridgeSurvival,
                            memberId,
                            BattleLootConstants.ITEM_CALAMITY_SHARD(),
                            1,
                            "断桥生还"
                        )
                    );
            }

            var criticalFailMembers = _GetBattleMemberIds(_criticalFailByBattleId, battleId);
            foreach (var memberId in criticalFailMembers)
            {
                if (!_IsBattleMemberAlive(battleState, memberId))
                    continue;
                if (!_MarkMetaFlagIfFirst(_BuildEventMetaFlagId(EventBorrowedRoad, memberId)))
                    continue;
                var reward = _BuildPendingReward(
                    memberId,
                    EventBorrowedRoad,
                    "死里借来的路",
                    new GArray
                    {
                        new GDictionary
                        {
                            ["entry_type"] = "knowledge_unlock",
                            ["target_id"] = KnowledgeBorrowedRoad.ToString(),
                            ["target_label"] = "借来的路",
                            ["amount"] = 1,
                            ["reason_text"] = "这名角色学会了如何从坏运留下的裂缝里继续前进。",
                        },
                    },
                    "一次大失败之后仍把整场战斗赢了下来。"
                );
                if (reward.Count == 0)
                {
                    _ClearMetaFlag(_BuildEventMetaFlagId(EventBorrowedRoad, memberId));
                    continue;
                }
                _AppendUniqueStringName(
                    result["triggered_event_ids"].AsGodotArray(),
                    EventBorrowedRoad
                );
                result["pending_character_rewards"].AsGodotArray().Add(reward);
            }

            bool battleHasEliteOrBoss = _BattleHasEliteOrBossEnemy(battleState);
            if (battleHasEliteOrBoss)
            {
                var reverseFateMemberId = _FindFirstAliveMemberIdInBattle(
                    criticalFailMembers,
                    battleState
                );
                if (
                    reverseFateMemberId != ""
                    && _MarkMetaFlagIfFirst(_BuildEventMetaFlagId(EventReverseFateAmuletReward))
                )
                {
                    _AppendUniqueStringName(
                        result["triggered_event_ids"].AsGodotArray(),
                        EventReverseFateAmuletReward
                    );
                    result["loot_entries"]
                        .AsGodotArray()
                        .Add(
                            _BuildFixedItemLootEntry(
                                EventReverseFateAmuletReward,
                                reverseFateMemberId,
                                LowLuckRelicRules.ITEM_REVERSE_FATE_AMULET,
                                1,
                                "逆命护符"
                            )
                        );
                }
                var blackStarMemberId = _FindFirstAliveMemberIdInBattle(
                    hardshipMembers,
                    battleState
                );
                if (
                    blackStarMemberId != ""
                    && _MarkMetaFlagIfFirst(_BuildEventMetaFlagId(EventBlackStarWedgeReward))
                )
                {
                    _AppendUniqueStringName(
                        result["triggered_event_ids"].AsGodotArray(),
                        EventBlackStarWedgeReward
                    );
                    result["loot_entries"]
                        .AsGodotArray()
                        .Add(
                            _BuildFixedItemLootEntry(
                                EventBlackStarWedgeReward,
                                blackStarMemberId,
                                LowLuckRelicRules.ITEM_BLACK_STAR_WEDGE,
                                1,
                                "黑星楔钉"
                            )
                        );
                }
            }

            var bloodDebtMemberId = _FindFirstBloodDebtCandidateId(battleState);
            if (
                bloodDebtMemberId != ""
                && _MarkMetaFlagIfFirst(_BuildEventMetaFlagId(EventBloodDebtShawlReward))
            )
            {
                _AppendUniqueStringName(
                    result["triggered_event_ids"].AsGodotArray(),
                    EventBloodDebtShawlReward
                );
                result["loot_entries"]
                    .AsGodotArray()
                    .Add(
                        _BuildFixedItemLootEntry(
                            EventBloodDebtShawlReward,
                            bloodDebtMemberId,
                            LowLuckRelicRules.ITEM_BLOOD_DEBT_SHAWL,
                            1,
                            "血债披肩"
                        )
                    );
            }

            var lanternMemberId = _FindFirstAliveMemberIdInBattle(
                _IntersectMemberIds(hardshipMembers, criticalFailMembers),
                battleState
            );
            if (
                lanternMemberId != ""
                && _MarkMetaFlagIfFirst(_BuildEventMetaFlagId(EventDeadRoadLanternReward))
            )
            {
                _AppendUniqueStringName(
                    result["triggered_event_ids"].AsGodotArray(),
                    EventDeadRoadLanternReward
                );
                result["loot_entries"]
                    .AsGodotArray()
                    .Add(
                        _BuildFixedItemLootEntry(
                            EventDeadRoadLanternReward,
                            lanternMemberId,
                            LowLuckRelicRules.ITEM_DEAD_ROAD_LANTERN,
                            1,
                            "亡途灯笼"
                        )
                    );
            }
        }
        _ClearBattleTracking(battleId);
        return result;
    }

    public GDictionary handle_battle_resolution(
        GodotObject battle_state,
        GodotObject battle_resolution_result
    )
    {
        return HandleBattleResolution(battle_state, battle_resolution_result);
    }

    public GDictionary HandleSettlementAction(GDictionary context)
    {
        var result = _NewResult();
        if (!_IsLampWithoutWitnessContext(context))
            return result;
        if (!_MarkMetaFlagIfFirst(_BuildEventMetaFlagId(EventLampWithoutWitness)))
            return result;

        var partyState = _GetPartyState();
        var memberId = _FindFirstLowLuckMemberId(partyState);
        if (memberId == "")
        {
            _ClearMetaFlag(_BuildEventMetaFlagId(EventLampWithoutWitness));
            return result;
        }

        var reward = _BuildPendingReward(
            memberId,
            EventLampWithoutWitness,
            "灯下无人",
            new GArray
            {
                new GDictionary
                {
                    ["entry_type"] = "knowledge_unlock",
                    ["target_id"] = KnowledgeLampWithoutWitness.ToString(),
                    ["target_label"] = "黑市知识",
                    ["amount"] = 1,
                    ["reason_text"] = "灯下空出来的位置，让这名角色先一步看懂了黑市留下的暗号。",
                },
            },
            "神龛 / 旅舍 / 赌坊的休整没有带来安慰，却留下了固定线索。"
        );
        if (reward.Count == 0)
        {
            _ClearMetaFlag(_BuildEventMetaFlagId(EventLampWithoutWitness));
            return result;
        }

        _AppendUniqueStringName(
            result["triggered_event_ids"].AsGodotArray(),
            EventLampWithoutWitness
        );
        result["pending_character_rewards"].AsGodotArray().Add(reward);
        return result;
    }

    public GDictionary handle_settlement_action(GDictionary context)
    {
        return HandleSettlementAction(context ?? new GDictionary());
    }

    private void _OnFateEvent(StringName eventType, GDictionary payload)
    {
        switch ((string)eventType)
        {
            case "hardship_survival":
                _TrackHardshipSurvival(payload);
                break;
            case "critical_fail":
                _TrackCriticalFail(payload);
                break;
        }
    }

    private void _TrackHardshipSurvival(GDictionary payload)
    {
        var battleId = ProgressionDataUtils.to_string_name(
            payload.GetValueOrDefault("battle_id", "")
        );
        var memberId = ProgressionDataUtils.to_string_name(
            payload.GetValueOrDefault("attacker_member_id", "")
        );
        if (battleId == "" || memberId == "")
            return;
        if (!payload.GetValueOrDefault("attacker_low_hp_hardship", false).AsBool())
            return;
        var strongDebuffIds = ProgressionDataUtils.to_string_name_array(
            payload.GetValueOrDefault("attacker_strong_attack_debuff_ids", new GArray())
        );
        if (strongDebuffIds.Count == 0)
            return;
        if (!_IsLowLuckMemberPayload(memberId, payload))
            return;
        _MarkBattleMember(_hardshipSurvivalByBattleId, battleId, memberId);
    }

    private void _TrackCriticalFail(GDictionary payload)
    {
        var battleId = ProgressionDataUtils.to_string_name(
            payload.GetValueOrDefault("battle_id", "")
        );
        var memberId = ProgressionDataUtils.to_string_name(
            payload.GetValueOrDefault("attacker_member_id", "")
        );
        if (battleId == "" || memberId == "")
            return;
        if (!_IsLowLuckMemberPayload(memberId, payload))
            return;
        _MarkBattleMember(_criticalFailByBattleId, battleId, memberId);
    }

    private bool _IsLowLuckMemberPayload(StringName memberId, GDictionary payload)
    {
        var luckSnapshot = payload.GetValueOrDefault("luck_snapshot", new GDictionary());
        if (luckSnapshot.VariantType == Variant.Type.Dictionary)
        {
            int hiddenLuck = luckSnapshot
                .AsGodotDictionary()
                .GetValueOrDefault("hidden_luck_at_birth", 0)
                .AsInt32();
            if (hiddenLuck <= LowLuckThreshold)
                return true;
        }
        var memberState = _GetMemberState(memberId);
        return memberState != null && memberState.get_hidden_luck_at_birth() <= LowLuckThreshold;
    }

    private bool _IsLampWithoutWitnessContext(GDictionary context)
    {
        var actionId = context.GetValueOrDefault("action_id", "").AsString().ToLower();
        var interactionScriptId = context
            .GetValueOrDefault("interaction_script_id", "")
            .AsString()
            .ToLower();
        var facilityId = context.GetValueOrDefault("facility_id", "").AsString().ToLower();
        var facilityName = context.GetValueOrDefault("facility_name", "").AsString();
        var serviceType = context.GetValueOrDefault("service_type", "").AsString();
        if (
            interactionScriptId == "service_rest_basic"
            || interactionScriptId == "service_rest_full"
        )
            return true;
        if (actionId.Contains("rest") || actionId.Contains("gambl") || actionId.Contains("shrine"))
            return true;
        if (
            facilityId.Contains("inn")
            || facilityId.Contains("shrine")
            || facilityId.Contains("gambl")
        )
            return true;
        var haystack = $"{facilityName} {serviceType}".ToLower();
        foreach (var keyword in RestFacilityKeywords)
        {
            if (haystack.Contains(keyword))
                return true;
        }
        return false;
    }

    private GDictionary _BuildPendingReward(
        StringName memberId,
        StringName eventId,
        string sourceLabel,
        GArray entryOptions,
        string summaryText
    )
    {
        if (_characterGateway == null || memberId == "")
            return new GDictionary();
        var reward = (_characterGateway as CharacterManagementModule)?.build_pending_character_reward(
            memberId,
            _BuildRewardId(eventId, memberId),
            SourceTypeStoryEvent,
            eventId,
            sourceLabel,
            entryOptions,
            summaryText
        );
        return reward?.to_dict() ?? new GDictionary();
    }

    private GDictionary _BuildFixedItemLootEntry(
        StringName eventId,
        StringName memberId,
        StringName itemId,
        int quantity,
        string sourceLabel
    )
    {
        return new GDictionary
        {
            ["drop_type"] = BattleLootConstants.DROP_TYPE_ITEM().ToString(),
            ["drop_source_kind"] = BattleLootConstants.SOURCE_KIND_LOW_LUCK_EVENT().ToString(),
            ["drop_source_id"] = eventId.ToString(),
            ["drop_source_label"] = sourceLabel,
            ["drop_entry_id"] = $"{eventId}:{memberId}",
            ["item_id"] = itemId.ToString(),
            ["quantity"] = Mathf.Max(quantity, 0),
        };
    }

    private StringName _ResolveBattleId(GodotObject battleState, GodotObject battleResolutionResult)
    {
        if (
            battleResolutionResult != null
            && battleResolutionResult.Get("battle_id").AsStringName() != ""
        )
            return battleResolutionResult.Get("battle_id").AsStringName();
        return battleState != null ? battleState.Get("battle_id").AsStringName() : "";
    }

    private void _MarkBattleMember(GDictionary store, StringName battleId, StringName memberId)
    {
        if (battleId == "" || memberId == "")
            return;
        if (!store.ContainsKey(battleId))
            store[battleId] = new GDictionary();
        var battleMembers = store[battleId].AsGodotDictionary();
        battleMembers[memberId] = true;
    }

    private List<StringName> _GetBattleMemberIds(GDictionary store, StringName battleId)
    {
        var memberIds = new List<StringName>();
        if (battleId == "")
            return memberIds;
        if (!store.ContainsKey(battleId))
            return memberIds;
        var battleMembers = store[battleId].AsGodotDictionary();
        foreach (var memberKey in battleMembers.Keys)
        {
            if (memberKey.VariantType != Variant.Type.StringName)
                continue;
            var memberId = ProgressionDataUtils.to_string_name(memberKey);
            if (memberId == "")
                continue;
            if (battleMembers[memberKey].AsBool())
                memberIds.Add(memberId);
        }
        memberIds.Sort((a, b) => a.ToString().CompareTo(b.ToString()));
        return memberIds;
    }

    private void _ClearBattleTracking(StringName battleId)
    {
        if (battleId == "")
            return;
        _hardshipSurvivalByBattleId.Remove(battleId);
        _criticalFailByBattleId.Remove(battleId);
    }

    private StringName _FindFirstLowLuckMemberId(PartyState partyState)
    {
        if (partyState == null)
            return "";
        foreach (var memberId in _BuildOrderedMemberIds(partyState))
        {
            var memberState = partyState.get_member_state(memberId);
            if (memberState == null || memberState.is_dead)
                continue;
            if (memberState.get_hidden_luck_at_birth() <= LowLuckThreshold)
                return memberId;
        }
        return "";
    }

    private List<StringName> _BuildOrderedMemberIds(PartyState partyState)
    {
        var orderedMemberIds = new List<StringName>();
        if (partyState == null)
            return orderedMemberIds;
        _AppendUniqueMemberIds(
            orderedMemberIds,
            ProgressionDataUtils.to_string_name_array(partyState.Get("active_member_ids"))
        );
        _AppendUniqueMemberIds(
            orderedMemberIds,
            ProgressionDataUtils.to_string_name_array(partyState.Get("reserve_member_ids"))
        );
        foreach (
            var memberKey in ProgressionDataUtils.sorted_string_keys(
                partyState.Get("member_states").AsGodotDictionary()
            )
        )
            _AppendUniqueMemberId(orderedMemberIds, new StringName(memberKey));
        return orderedMemberIds;
    }

    private void _AppendUniqueMemberIds(
        List<StringName> target,
        Godot.Collections.Array<StringName> values
    )
    {
        foreach (var value in values)
            _AppendUniqueMemberId(target, value);
    }

    private void _AppendUniqueMemberId(List<StringName> target, StringName value)
    {
        if (value == "" || target.Contains(value))
            return;
        target.Add(value);
    }

    private bool _IsBattleMemberAlive(GodotObject battleState, StringName memberId)
    {
        if (battleState == null || memberId == "")
            return false;
        foreach (var unitValue in battleState.Get("units").AsGodotDictionary().Values)
        {
            var unitState = unitValue.AsGodotObject() as BattleUnitState;
            if (unitState == null)
                continue;
            var sourceMemberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
            if (sourceMemberId != memberId)
                continue;
            return unitState.is_alive;
        }
        var memberState = _GetMemberState(memberId);
        return memberState != null && !memberState.is_dead;
    }

    private StringName _FindFirstAliveMemberIdInBattle(
        List<StringName> memberIds,
        GodotObject battleState
    )
    {
        foreach (var memberId in memberIds)
        {
            if (_IsBattleMemberAlive(battleState, memberId))
                return memberId;
        }
        return "";
    }

    private List<StringName> _IntersectMemberIds(List<StringName> first, List<StringName> second)
    {
        var intersected = new List<StringName>();
        foreach (var memberId in first)
        {
            if (memberId == "" || intersected.Contains(memberId) || !second.Contains(memberId))
                continue;
            intersected.Add(memberId);
        }
        return intersected;
    }

    private bool _BattleHasEliteOrBossEnemy(GodotObject battleState)
    {
        if (battleState == null)
            return false;
        var targetUnitIds = new List<StringName>();
        var enemyUnitIds = battleState.Get("enemy_unit_ids").AsGodotArray<StringName>();
        if (enemyUnitIds.Count > 0)
        {
            foreach (var id in enemyUnitIds)
                targetUnitIds.Add(id);
        }
        else
        {
            foreach (var unitValue in battleState.Get("units").AsGodotDictionary().Values)
            {
                var unitState = unitValue.AsGodotObject() as BattleUnitState;
                if (unitState == null || unitState.faction_id == "player")
                    continue;
                targetUnitIds.Add(unitState.unit_id);
            }
        }
        var units = battleState.Get("units").AsGodotDictionary();
        foreach (var unitId in targetUnitIds)
        {
            var unitState = units.ContainsKey(unitId) ? units[unitId].As<BattleUnitState>() : null;
            if (unitState == null || unitState.attribute_snapshot == null)
                continue;
            if (
                unitState.attribute_snapshot.get_value(FortuneMarkTargetStatId)
                > 0
            )
                return true;
        }
        return false;
    }

    private StringName _FindFirstBloodDebtCandidateId(GodotObject battleState)
    {
        var partyState = _GetPartyState();
        if (battleState == null || partyState == null)
            return "";
        foreach (var memberId in _BuildOrderedMemberIds(partyState))
        {
            var memberState = partyState.get_member_state(memberId);
            if (memberState == null || memberState.get_hidden_luck_at_birth() > LowLuckThreshold)
                continue;
            if (!_IsBattleMemberAlive(battleState, memberId))
                continue;
            if (_BattleHasFallenPlayerAlly(battleState, memberId))
                return memberId;
        }
        return "";
    }

    private bool _BattleHasFallenPlayerAlly(GodotObject battleState, StringName survivingMemberId)
    {
        if (battleState == null)
            return false;
        foreach (var unitValue in battleState.Get("units").AsGodotDictionary().Values)
        {
            var unitState = unitValue.AsGodotObject() as BattleUnitState;
            if (unitState == null || unitState.faction_id != "player")
                continue;
            var memberId = ProgressionDataUtils.to_string_name(unitState.source_member_id);
            if (memberId == "" || memberId == survivingMemberId)
                continue;
            if (!unitState.is_alive)
                return true;
        }
        return false;
    }

    private PartyState _GetPartyState()
    {
        return _characterGateway?.get_party_state();
    }

    private PartyMemberState _GetMemberState(StringName memberId)
    {
        if (_characterGateway == null || memberId == "")
            return null;
        return _characterGateway.get_member_state(memberId);
    }

    private bool _MarkMetaFlagIfFirst(StringName flagId)
    {
        var partyState = _GetPartyState();
        if (partyState == null || flagId == "")
            return false;
        if (partyState.has_meta_flag(flagId))
            return false;
        partyState.set_meta_flag(flagId, true);
        return true;
    }

    private void _ClearMetaFlag(StringName flagId)
    {
        var partyState = _GetPartyState();
        if (partyState == null || flagId == "")
            return;
        partyState.clear_meta_flag(flagId);
    }

    private StringName _BuildEventMetaFlagId(StringName eventId, StringName memberId = null)
    {
        if (eventId == "")
            return "";
        if (memberId == null || memberId == "")
            return ProgressionDataUtils.to_string_name($"{MetaFlagPrefix}{eventId}");
        return ProgressionDataUtils.to_string_name($"{MetaFlagPrefix}{eventId}:{memberId}");
    }

    private StringName _BuildRewardId(StringName eventId, StringName memberId)
    {
        return ProgressionDataUtils.to_string_name($"{MetaFlagPrefix}reward:{eventId}:{memberId}");
    }

    private GDictionary _NewResult()
    {
        return new GDictionary
        {
            ["triggered_event_ids"] = new GArray(),
            ["loot_entries"] = new GArray(),
            ["pending_character_rewards"] = new GArray(),
        };
    }

    private void _AppendUniqueStringName(GArray values, StringName value)
    {
        if (value == "" || values == null)
            return;
        foreach (var existing in values)
        {
            if (existing.AsStringName() == value)
                return;
        }
        values.Add(value);
    }
}
