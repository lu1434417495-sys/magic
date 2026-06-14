using System;
using System.Collections.Generic;
using Godot;

public sealed class LowLuckFateEventPayload
{
    public static LowLuckFateEventPayload Empty { get; } = new(
        "",
        "",
        false,
        null,
        null
    );

    private readonly List<StringName> _attackerStrongAttackDebuffIds = new();

    public LowLuckFateEventPayload(
        StringName battleId,
        StringName attackerMemberId,
        bool attackerLowHpHardship,
        IEnumerable<StringName> attackerStrongAttackDebuffIds,
        int? hiddenLuckAtBirth
    )
    {
        BattleId = ProgressionDataUtils.to_string_name(battleId);
        AttackerMemberId = ProgressionDataUtils.to_string_name(attackerMemberId);
        AttackerLowHpHardship = attackerLowHpHardship;
        HiddenLuckAtBirth = hiddenLuckAtBirth;

        if (attackerStrongAttackDebuffIds == null)
            return;
        foreach (StringName debuffId in attackerStrongAttackDebuffIds)
        {
            StringName normalizedDebuffId = ProgressionDataUtils.to_string_name(debuffId);
            if (normalizedDebuffId != "" && !_attackerStrongAttackDebuffIds.Contains(normalizedDebuffId))
                _attackerStrongAttackDebuffIds.Add(normalizedDebuffId);
        }
    }

    public StringName BattleId { get; }
    public StringName AttackerMemberId { get; }
    public bool AttackerLowHpHardship { get; }
    public IReadOnlyList<StringName> AttackerStrongAttackDebuffIds =>
        _attackerStrongAttackDebuffIds;
    public int? HiddenLuckAtBirth { get; }
}

public sealed class LowLuckSettlementActionInput
{
    public static LowLuckSettlementActionInput Empty { get; } = new("", "", "", "", "");

    public LowLuckSettlementActionInput(
        string actionId,
        string interactionScriptId,
        string facilityId,
        string facilityName,
        string serviceType
    )
    {
        ActionId = actionId ?? "";
        InteractionScriptId = interactionScriptId ?? "";
        FacilityId = facilityId ?? "";
        FacilityName = facilityName ?? "";
        ServiceType = serviceType ?? "";
    }

    public string ActionId { get; }
    public string InteractionScriptId { get; }
    public string FacilityId { get; }
    public string FacilityName { get; }
    public string ServiceType { get; }
}

public readonly record struct LowLuckBattleUnitSnapshot(
    StringName UnitId,
    StringName SourceMemberId,
    StringName FactionId,
    bool IsAlive,
    bool IsEnemy,
    bool IsEliteOrBoss
);

public sealed class LowLuckBattleResolutionInput
{
    public static LowLuckBattleResolutionInput Empty { get; } = new("", false, null);

    private readonly List<LowLuckBattleUnitSnapshot> _units = new();

    public LowLuckBattleResolutionInput(
        StringName battleId,
        bool playerWon,
        IEnumerable<LowLuckBattleUnitSnapshot> units
    )
    {
        BattleId = ProgressionDataUtils.to_string_name(battleId);
        PlayerWon = playerWon;
        if (units == null)
            return;
        foreach (LowLuckBattleUnitSnapshot unit in units)
        {
            StringName unitId = ProgressionDataUtils.to_string_name(unit.UnitId);
            StringName memberId = ProgressionDataUtils.to_string_name(unit.SourceMemberId);
            StringName factionId = ProgressionDataUtils.to_string_name(unit.FactionId);
            _units.Add(
                new LowLuckBattleUnitSnapshot(
                    unitId,
                    memberId,
                    factionId,
                    unit.IsAlive,
                    unit.IsEnemy,
                    unit.IsEliteOrBoss
                )
            );
        }
    }

    public StringName BattleId { get; }
    public bool PlayerWon { get; }
    public IReadOnlyList<LowLuckBattleUnitSnapshot> Units => _units;
}

public readonly record struct LowLuckLootEntry(
    BattleLootDropKind DropKind,
    BattleLootSourceKind DropSourceKind,
    StringName DropSourceId,
    string DropSourceLabel,
    StringName DropEntryId,
    StringName ItemId,
    int Quantity
);

public sealed class LowLuckEventResult
{
    private readonly List<StringName> _triggeredEventIds = new();
    private readonly List<LowLuckLootEntry> _lootEntries = new();
    private readonly List<PendingCharacterReward> _pendingCharacterRewards = new();

    public IReadOnlyList<StringName> TriggeredEventIds => _triggeredEventIds;
    public IReadOnlyList<LowLuckLootEntry> LootEntries => _lootEntries;
    public IReadOnlyList<PendingCharacterReward> PendingCharacterRewards =>
        _pendingCharacterRewards;
    public bool IsEmpty =>
        _triggeredEventIds.Count == 0
        && _lootEntries.Count == 0
        && _pendingCharacterRewards.Count == 0;

    internal void AddTriggeredEventId(StringName eventId)
    {
        StringName normalizedEventId = ProgressionDataUtils.to_string_name(eventId);
        if (normalizedEventId == "" || _triggeredEventIds.Contains(normalizedEventId))
            return;
        _triggeredEventIds.Add(normalizedEventId);
    }

    internal void AddLootEntry(LowLuckLootEntry lootEntry)
    {
        if (lootEntry.ItemId == "" || lootEntry.Quantity <= 0)
            return;
        _lootEntries.Add(lootEntry);
    }

    internal void AddPendingCharacterReward(PendingCharacterReward reward)
    {
        if (reward == null || reward.IsEmpty())
            return;
        _pendingCharacterRewards.Add(reward);
    }
}

internal enum LowLuckEventKind
{
    Unknown = 0,
    BrokenBridgeSurvival,
    LampWithoutWitness,
    BorrowedRoad,
    ReverseFateAmuletReward,
    BlackStarWedgeReward,
    BloodDebtShawlReward,
    DeadRoadLanternReward,
}

public sealed class LowLuckEventService
{
    private static readonly StringName EventBrokenBridgeSurvival = "broken_bridge_survival";
    private static readonly StringName EventLampWithoutWitness = "lamp_without_witness";
    private static readonly StringName EventBorrowedRoad = "borrowed_road";
    private static readonly StringName EventReverseFateAmuletReward =
        "reverse_fate_amulet_reward";
    private static readonly StringName EventBlackStarWedgeReward = "black_star_wedge_reward";
    private static readonly StringName EventBloodDebtShawlReward = "blood_debt_shawl_reward";
    private static readonly StringName EventDeadRoadLanternReward = "dead_road_lantern_reward";
    internal const string MetaFlagPrefix = "low_luck_event:";

    private const int LowLuckThreshold = -4;
    private static readonly StringName FateEventHardshipSurvival = "hardship_survival";
    private static readonly StringName FateEventCriticalFail = "critical_fail";
    private static readonly StringName SourceTypeStoryEvent = "story_event";
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

    private IBattleRuntimeCharacterGateway _characterGateway;
    private readonly Dictionary<StringName, HashSet<StringName>> _hardshipSurvivalByBattleId =
        new();
    private readonly Dictionary<StringName, HashSet<StringName>> _criticalFailByBattleId = new();

    internal static StringName ToStringName(LowLuckEventKind kind)
    {
        return kind switch
        {
            LowLuckEventKind.BrokenBridgeSurvival => EventBrokenBridgeSurvival,
            LowLuckEventKind.LampWithoutWitness => EventLampWithoutWitness,
            LowLuckEventKind.BorrowedRoad => EventBorrowedRoad,
            LowLuckEventKind.ReverseFateAmuletReward => EventReverseFateAmuletReward,
            LowLuckEventKind.BlackStarWedgeReward => EventBlackStarWedgeReward,
            LowLuckEventKind.BloodDebtShawlReward => EventBloodDebtShawlReward,
            LowLuckEventKind.DeadRoadLanternReward => EventDeadRoadLanternReward,
            _ => "",
        };
    }

    internal void Setup(IBattleRuntimeCharacterGateway characterGateway = null)
    {
        _characterGateway = characterGateway;
    }

    public void HandleFateEvent(StringName eventType, LowLuckFateEventPayload payload)
    {
        StringName normalizedEventType = ProgressionDataUtils.to_string_name(eventType);
        if (normalizedEventType == FateEventHardshipSurvival)
        {
            TrackHardshipSurvival(payload ?? LowLuckFateEventPayload.Empty);
            return;
        }
        if (normalizedEventType == FateEventCriticalFail)
            TrackCriticalFail(payload ?? LowLuckFateEventPayload.Empty);
    }

    public LowLuckEventResult HandleBattleResolution(LowLuckBattleResolutionInput input)
    {
        LowLuckEventResult result = new();
        input ??= LowLuckBattleResolutionInput.Empty;
        StringName battleId = input.BattleId;
        if (battleId == "")
            return result;

        if (input.PlayerWon)
        {
            List<StringName> hardshipMembers = GetBattleMemberIds(
                _hardshipSurvivalByBattleId,
                battleId
            );
            foreach (StringName memberId in hardshipMembers)
            {
                if (!IsBattleMemberAlive(input, memberId))
                    continue;
                if (!MarkMetaFlagIfFirst(BuildEventMetaFlagId(EventBrokenBridgeSurvival, memberId)))
                    continue;

                result.AddTriggeredEventId(EventBrokenBridgeSurvival);
                result.AddLootEntry(
                    BuildFixedItemLootEntry(
                        EventBrokenBridgeSurvival,
                        memberId,
                        BattleLootIds.ToStringName(BattleLootSpecialItemKind.CalamityShard),
                        1,
                        "断桥生还"
                    )
                );
            }

            List<StringName> criticalFailMembers = GetBattleMemberIds(
                _criticalFailByBattleId,
                battleId
            );
            foreach (StringName memberId in criticalFailMembers)
            {
                if (!IsBattleMemberAlive(input, memberId))
                    continue;
                if (!MarkMetaFlagIfFirst(BuildEventMetaFlagId(EventBorrowedRoad, memberId)))
                    continue;

                PendingCharacterReward reward = BuildPendingReward(
                    memberId,
                    EventBorrowedRoad,
                    "死里借来的路",
                    BuildRewardEntry(
                        PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.KnowledgeUnlock),
                        KnowledgeBorrowedRoad,
                        1,
                        "借来的路",
                        "这名角色学会了如何从坏运留下的裂缝里继续前进。"
                    ),
                    "一次大失败之后仍把整场战斗赢了下来。"
                );
                if (reward == null)
                {
                    ClearMetaFlag(BuildEventMetaFlagId(EventBorrowedRoad, memberId));
                    continue;
                }

                result.AddTriggeredEventId(EventBorrowedRoad);
                result.AddPendingCharacterReward(reward);
            }

            if (BattleHasEliteOrBossEnemy(input))
            {
                StringName reverseFateMemberId = FindFirstAliveMemberIdInBattle(
                    criticalFailMembers,
                    input
                );
                if (
                    reverseFateMemberId != ""
                    && MarkMetaFlagIfFirst(BuildEventMetaFlagId(EventReverseFateAmuletReward))
                )
                {
                    result.AddTriggeredEventId(EventReverseFateAmuletReward);
                    result.AddLootEntry(
                        BuildFixedItemLootEntry(
                            EventReverseFateAmuletReward,
                            reverseFateMemberId,
                            LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.ReverseFateAmulet),
                            1,
                            "逆命护符"
                        )
                    );
                }

                StringName blackStarMemberId = FindFirstAliveMemberIdInBattle(
                    hardshipMembers,
                    input
                );
                if (
                    blackStarMemberId != ""
                    && MarkMetaFlagIfFirst(BuildEventMetaFlagId(EventBlackStarWedgeReward))
                )
                {
                    result.AddTriggeredEventId(EventBlackStarWedgeReward);
                    result.AddLootEntry(
                        BuildFixedItemLootEntry(
                            EventBlackStarWedgeReward,
                            blackStarMemberId,
                            LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BlackStarWedge),
                            1,
                            "黑星楔钉"
                        )
                    );
                }
            }

            StringName bloodDebtMemberId = FindFirstBloodDebtCandidateId(input);
            if (
                bloodDebtMemberId != ""
                && MarkMetaFlagIfFirst(BuildEventMetaFlagId(EventBloodDebtShawlReward))
            )
            {
                result.AddTriggeredEventId(EventBloodDebtShawlReward);
                result.AddLootEntry(
                    BuildFixedItemLootEntry(
                        EventBloodDebtShawlReward,
                        bloodDebtMemberId,
                        LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.BloodDebtShawl),
                        1,
                        "血债披肩"
                    )
                );
            }

            StringName lanternMemberId = FindFirstAliveMemberIdInBattle(
                IntersectMemberIds(hardshipMembers, criticalFailMembers),
                input
            );
            if (
                lanternMemberId != ""
                && MarkMetaFlagIfFirst(BuildEventMetaFlagId(EventDeadRoadLanternReward))
            )
            {
                result.AddTriggeredEventId(EventDeadRoadLanternReward);
                result.AddLootEntry(
                    BuildFixedItemLootEntry(
                        EventDeadRoadLanternReward,
                        lanternMemberId,
                        LowLuckRelicRules.ToStringName(LowLuckRelicItemKind.DeadRoadLantern),
                        1,
                        "亡途灯笼"
                    )
                );
            }
        }

        ClearBattleTracking(battleId);
        return result;
    }

    public LowLuckEventResult HandleSettlementAction(LowLuckSettlementActionInput input)
    {
        LowLuckEventResult result = new();
        input ??= LowLuckSettlementActionInput.Empty;
        if (!IsLampWithoutWitnessContext(input))
            return result;
        if (!MarkMetaFlagIfFirst(BuildEventMetaFlagId(EventLampWithoutWitness)))
            return result;

        PartyState partyState = GetPartyState();
        StringName memberId = FindFirstLowLuckMemberId(partyState);
        if (memberId == "")
        {
            ClearMetaFlag(BuildEventMetaFlagId(EventLampWithoutWitness));
            return result;
        }

        PendingCharacterReward reward = BuildPendingReward(
            memberId,
            EventLampWithoutWitness,
            "灯下无人",
            BuildRewardEntry(
                PendingCharacterRewardContentRules.ToStringName(PendingCharacterRewardEntryKind.KnowledgeUnlock),
                KnowledgeLampWithoutWitness,
                1,
                "黑市知识",
                "灯下空出来的位置，让这名角色先一步看懂了黑市留下的暗号。"
            ),
            "神龛 / 旅舍 / 赌坊的休整没有带来安慰，却留下了固定线索。"
        );
        if (reward == null)
        {
            ClearMetaFlag(BuildEventMetaFlagId(EventLampWithoutWitness));
            return result;
        }

        result.AddTriggeredEventId(EventLampWithoutWitness);
        result.AddPendingCharacterReward(reward);
        return result;
    }

    internal void Dispose()
    {
        _characterGateway = null;
        _hardshipSurvivalByBattleId.Clear();
        _criticalFailByBattleId.Clear();
    }

    private void TrackHardshipSurvival(LowLuckFateEventPayload eventPayload)
    {
        StringName battleId = eventPayload.BattleId;
        StringName memberId = eventPayload.AttackerMemberId;
        if (battleId == "" || memberId == "")
            return;
        if (!eventPayload.AttackerLowHpHardship)
            return;
        if (eventPayload.AttackerStrongAttackDebuffIds.Count == 0)
            return;
        if (!IsLowLuckMemberPayload(memberId, eventPayload.HiddenLuckAtBirth))
            return;
        MarkBattleMember(_hardshipSurvivalByBattleId, battleId, memberId);
    }

    private void TrackCriticalFail(LowLuckFateEventPayload eventPayload)
    {
        StringName battleId = eventPayload.BattleId;
        StringName memberId = eventPayload.AttackerMemberId;
        if (battleId == "" || memberId == "")
            return;
        if (!IsLowLuckMemberPayload(memberId, eventPayload.HiddenLuckAtBirth))
            return;
        MarkBattleMember(_criticalFailByBattleId, battleId, memberId);
    }

    private bool IsLowLuckMemberPayload(StringName memberId, int? hiddenLuckAtBirth)
    {
        if (hiddenLuckAtBirth.HasValue && hiddenLuckAtBirth.Value <= LowLuckThreshold)
            return true;

        PartyMemberState memberState = GetMemberState(memberId);
        return memberState != null && memberState.GetHiddenLuckAtBirth() <= LowLuckThreshold;
    }

    private bool IsLampWithoutWitnessContext(LowLuckSettlementActionInput context)
    {
        string actionId = context.ActionId.ToLowerInvariant();
        string interactionScriptId = context.InteractionScriptId.ToLowerInvariant();
        string facilityId = context.FacilityId.ToLowerInvariant();
        string haystack = $"{context.FacilityName} {context.ServiceType}".ToLowerInvariant();

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
        foreach (string keyword in RestFacilityKeywords)
        {
            if (haystack.Contains(keyword))
                return true;
        }
        return false;
    }

    private PendingCharacterReward BuildPendingReward(
        StringName memberId,
        StringName eventId,
        string sourceLabel,
        PendingCharacterRewardEntry rewardEntry,
        string summaryText
    )
    {
        PartyMemberState memberState = GetMemberState(memberId);
        if (memberState == null || rewardEntry == null || rewardEntry.IsEmpty())
            return null;

        PendingCharacterReward reward = new()
        {
            reward_id = BuildRewardId(eventId, memberId),
            member_id = memberId,
            member_name = !string.IsNullOrEmpty(memberState.display_name)
                ? memberState.display_name
                : memberId.ToString(),
            source_type = SourceTypeStoryEvent,
            source_id = eventId,
            source_label = sourceLabel ?? "",
            summary_text = summaryText ?? "",
        };
        reward.entries.Add(rewardEntry);
        return reward.IsEmpty() ? null : reward;
    }

    private static PendingCharacterRewardEntry BuildRewardEntry(
        StringName entryType,
        StringName targetId,
        int amount,
        string targetLabel,
        string reasonText
    )
    {
        StringName normalizedEntryType = ProgressionDataUtils.to_string_name(entryType);
        StringName normalizedTargetId = ProgressionDataUtils.to_string_name(targetId);
        if (
            normalizedEntryType == ""
            || normalizedTargetId == ""
            || amount == 0
            || !PendingCharacterRewardContentRules.IsSupportedEntryType(normalizedEntryType)
        )
            return null;
        if (
            PendingCharacterRewardContentRules.IsAttributeProgressEntry(normalizedEntryType)
            && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(
                normalizedTargetId
            )
        )
            return null;

        return new PendingCharacterRewardEntry
        {
            entry_type = normalizedEntryType,
            target_id = normalizedTargetId,
            target_label = targetLabel ?? "",
            amount = amount,
            reason_text = reasonText ?? "",
        };
    }

    private static LowLuckLootEntry BuildFixedItemLootEntry(
        StringName eventId,
        StringName memberId,
        StringName itemId,
        int quantity,
        string sourceLabel
    )
    {
        return new LowLuckLootEntry(
            BattleLootDropKind.Item,
            BattleLootSourceKind.LowLuckEvent,
            eventId,
            sourceLabel ?? "",
            $"{eventId}:{memberId}",
            itemId,
            Math.Max(quantity, 0)
        );
    }

    private void MarkBattleMember(
        Dictionary<StringName, HashSet<StringName>> store,
        StringName battleId,
        StringName memberId
    )
    {
        if (battleId == "" || memberId == "")
            return;
        if (!store.TryGetValue(battleId, out HashSet<StringName> battleMembers))
        {
            battleMembers = new HashSet<StringName>();
            store[battleId] = battleMembers;
        }
        battleMembers.Add(memberId);
    }

    private static List<StringName> GetBattleMemberIds(
        Dictionary<StringName, HashSet<StringName>> store,
        StringName battleId
    )
    {
        List<StringName> memberIds = new();
        if (battleId == "")
            return memberIds;
        if (!store.TryGetValue(battleId, out HashSet<StringName> battleMembers))
            return memberIds;
        foreach (StringName memberKey in battleMembers)
        {
            StringName memberId = ProgressionDataUtils.to_string_name(memberKey);
            if (memberId != "")
                memberIds.Add(memberId);
        }
        memberIds.Sort((a, b) => a.ToString().CompareTo(b.ToString()));
        return memberIds;
    }

    private void ClearBattleTracking(StringName battleId)
    {
        if (battleId == "")
            return;
        _hardshipSurvivalByBattleId.Remove(battleId);
        _criticalFailByBattleId.Remove(battleId);
    }

    private StringName FindFirstLowLuckMemberId(PartyState partyState)
    {
        if (partyState == null)
            return "";
        foreach (StringName memberId in BuildOrderedMemberIds(partyState))
        {
            PartyMemberState memberState = partyState.GetMemberState(memberId);
            if (memberState == null || memberState.is_dead)
                continue;
            if (memberState.GetHiddenLuckAtBirth() <= LowLuckThreshold)
                return memberId;
        }
        return "";
    }

    private static List<StringName> BuildOrderedMemberIds(PartyState partyState)
    {
        List<StringName> orderedMemberIds = new();
        if (partyState == null)
            return orderedMemberIds;
        AppendUniqueMemberIds(orderedMemberIds, partyState.active_member_ids);
        AppendUniqueMemberIds(orderedMemberIds, partyState.reserve_member_ids);
        foreach (PartyMemberState memberState in partyState.GetMemberStates())
            AppendUniqueMemberId(orderedMemberIds, memberState?.member_id ?? "");
        return orderedMemberIds;
    }

    private static void AppendUniqueMemberIds(
        List<StringName> target,
        IEnumerable<StringName> values
    )
    {
        if (values == null)
            return;
        foreach (StringName value in values)
            AppendUniqueMemberId(target, value);
    }

    private static void AppendUniqueMemberId(List<StringName> target, StringName value)
    {
        StringName normalizedValue = ProgressionDataUtils.to_string_name(value);
        if (normalizedValue == "" || target.Contains(normalizedValue))
            return;
        target.Add(normalizedValue);
    }

    private bool IsBattleMemberAlive(LowLuckBattleResolutionInput input, StringName memberId)
    {
        if (input == null || memberId == "")
            return false;
        foreach (LowLuckBattleUnitSnapshot unit in input.Units)
        {
            if (unit.SourceMemberId == memberId)
                return unit.IsAlive;
        }
        PartyMemberState memberState = GetMemberState(memberId);
        return memberState != null && !memberState.is_dead;
    }

    private StringName FindFirstAliveMemberIdInBattle(
        List<StringName> memberIds,
        LowLuckBattleResolutionInput input
    )
    {
        foreach (StringName memberId in memberIds)
        {
            if (IsBattleMemberAlive(input, memberId))
                return memberId;
        }
        return "";
    }

    private static List<StringName> IntersectMemberIds(
        List<StringName> first,
        List<StringName> second
    )
    {
        List<StringName> intersected = new();
        foreach (StringName memberId in first)
        {
            if (memberId == "" || intersected.Contains(memberId) || !second.Contains(memberId))
                continue;
            intersected.Add(memberId);
        }
        return intersected;
    }

    private static bool BattleHasEliteOrBossEnemy(LowLuckBattleResolutionInput input)
    {
        if (input == null)
            return false;
        foreach (LowLuckBattleUnitSnapshot unit in input.Units)
        {
            if (unit.IsEnemy && unit.IsEliteOrBoss)
                return true;
        }
        return false;
    }

    private StringName FindFirstBloodDebtCandidateId(LowLuckBattleResolutionInput input)
    {
        PartyState partyState = GetPartyState();
        if (input == null || partyState == null)
            return "";
        foreach (StringName memberId in BuildOrderedMemberIds(partyState))
        {
            PartyMemberState memberState = partyState.GetMemberState(memberId);
            if (memberState == null || memberState.GetHiddenLuckAtBirth() > LowLuckThreshold)
                continue;
            if (!IsBattleMemberAlive(input, memberId))
                continue;
            if (BattleHasFallenPlayerAlly(input, memberId))
                return memberId;
        }
        return "";
    }

    private static bool BattleHasFallenPlayerAlly(
        LowLuckBattleResolutionInput input,
        StringName survivingMemberId
    )
    {
        if (input == null)
            return false;
        foreach (LowLuckBattleUnitSnapshot unit in input.Units)
        {
            if (unit.FactionId != "player")
                continue;
            StringName memberId = ProgressionDataUtils.to_string_name(unit.SourceMemberId);
            if (memberId == "" || memberId == survivingMemberId)
                continue;
            if (!unit.IsAlive)
                return true;
        }
        return false;
    }

    private PartyState GetPartyState()
    {
        return _characterGateway?.GetPartyState();
    }

    private PartyMemberState GetMemberState(StringName memberId)
    {
        if (_characterGateway == null || memberId == "")
            return null;
        return _characterGateway.GetMemberState(memberId);
    }

    private bool MarkMetaFlagIfFirst(StringName flagId)
    {
        PartyState partyState = GetPartyState();
        if (partyState == null || flagId == "")
            return false;
        if (partyState.HasMetaFlag(flagId))
            return false;
        partyState.SetMetaFlag(flagId, true);
        return true;
    }

    private void ClearMetaFlag(StringName flagId)
    {
        PartyState partyState = GetPartyState();
        if (partyState == null || flagId == "")
            return;
        partyState.ClearMetaFlag(flagId);
    }

    private static StringName BuildEventMetaFlagId(StringName eventId, StringName memberId = null)
    {
        if (eventId == "")
            return "";
        if (memberId == null || memberId == "")
            return ProgressionDataUtils.to_string_name($"{MetaFlagPrefix}{eventId}");
        return ProgressionDataUtils.to_string_name($"{MetaFlagPrefix}{eventId}:{memberId}");
    }

    private static StringName BuildRewardId(StringName eventId, StringName memberId)
    {
        return ProgressionDataUtils.to_string_name($"{MetaFlagPrefix}reward:{eventId}:{memberId}");
    }
}
