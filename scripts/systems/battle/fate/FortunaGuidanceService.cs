using System;
using System.Collections.Generic;
using Godot;

public sealed class FortunaGuidanceEventInput
{
    public StringName EventType { get; init; } = "";
    public StringName BattleId { get; init; } = "";
    public StringName AttackerMemberId { get; init; } = "";
    public bool DefenderIsEliteOrBoss { get; init; }
    public bool AttackerLowHpHardship { get; init; }
    public IReadOnlyList<StringName> AttackerStrongAttackDebuffIds { get; init; } =
        Array.Empty<StringName>();
}

public sealed class FortunaChapterCompletionInput
{
    public IReadOnlyList<StringName> MemberIds { get; init; } = Array.Empty<StringName>();
    public bool HadPermanentDeath { get; init; }
}

public class FortunaGuidanceService
{
    public static readonly StringName AchievementGuidanceTrue = "fortuna_guidance_true";
    public static readonly StringName AchievementGuidanceDevout = "fortuna_guidance_devout";
    public static readonly StringName AchievementGuidanceExalted = "fortuna_guidance_exalted";
    public static readonly StringName AchievementGuidanceBlessed = "fortuna_guidance_blessed";

    private static readonly StringName EventCriticalSuccessUnderDisadvantage =
        "critical_success_under_disadvantage";
    private static readonly StringName EventHighThreatCriticalHit = "high_threat_critical_hit";
    private static readonly StringName EventHardshipSurvival = "hardship_survival";
    private static readonly StringName FortuneMarkedStatId = "fortune_marked";

    private const string ChapterEventFlagPrefix = "fortuna_guidance_chapter_seen:";
    private const string DevoutBattleFlagPrefix = "fortuna_guidance_devout_battle:";

    private IBattleRuntimeCharacterGateway _characterGateway;

    public void Setup(IBattleRuntimeCharacterGateway characterGateway = null)
    {
        _characterGateway = characterGateway;
    }

    public void Dispose() => dispose();

    public void dispose()
    {
        _characterGateway = null;
    }

    public void HandleFateEvent(FortunaGuidanceEventInput payload)
    {
        if (payload == null || payload.EventType == "")
            return;
        if (payload.EventType == EventCriticalSuccessUnderDisadvantage)
            HandleCriticalSuccessUnderDisadvantage(payload);
        else if (payload.EventType == EventHighThreatCriticalHit)
            HandleHighThreatCriticalHit(payload);
        else if (payload.EventType == EventHardshipSurvival)
            HandleHardshipSurvival(payload);
    }

    public List<StringName> HandleBattleResolution(
        BattleState battleState,
        BattleResolutionResult battleResolutionResult
    )
    {
        var unlockedIds = new List<StringName>();
        var partyState = GetPartyState();
        if (partyState == null || battleState == null || battleResolutionResult == null)
            return unlockedIds;
        var battleId =
            battleResolutionResult.battle_id != ""
                ? battleResolutionResult.battle_id
                : battleState.battle_id;
        if (battleId == "")
            return unlockedIds;
        bool playerWon = battleResolutionResult.winner_faction_id == "player";
        foreach (StringName allyUnitId in battleState.get_ally_unit_ids_typed())
        {
            if (!battleState.TryGetUnitTyped(allyUnitId, out BattleUnitState unitState))
                continue;
            if (unitState == null || unitState.source_member_id == "")
                continue;

            var flagId = BuildDevoutBattleFlagId(battleId, unitState.source_member_id);
            if (!partyState.has_fate_run_flag(flagId))
                continue;
            if (
                playerWon
                && unitState.is_alive
                && UnlockAchievement(unitState.source_member_id, AchievementGuidanceDevout)
            )
                AppendUniqueStringName(unlockedIds, AchievementGuidanceDevout);
            partyState.clear_fate_run_flag(flagId);
        }
        return unlockedIds;
    }

    public List<StringName> HandleChapterCompleted(FortunaChapterCompletionInput input = null)
    {
        input ??= new FortunaChapterCompletionInput();
        var unlockedIds = new List<StringName>();
        var partyState = GetPartyState();
        if (partyState == null)
            return unlockedIds;
        List<StringName> memberIds = ResolveChapterMemberIds(input, partyState);
        if (memberIds.Count == 0)
            return unlockedIds;
        foreach (var memberId in memberIds)
        {
            var flagId = BuildChapterEventFlagId(memberId);
            bool shouldUnlock =
                !input.HadPermanentDeath
                && partyState.has_fate_run_flag(flagId)
                && IsFortunaDevotee(GetMemberState(memberId));
            if (shouldUnlock && UnlockAchievement(memberId, AchievementGuidanceBlessed))
                AppendUniqueStringName(unlockedIds, AchievementGuidanceBlessed);
            partyState.clear_fate_run_flag(flagId);
        }
        return unlockedIds;
    }

    private void HandleCriticalSuccessUnderDisadvantage(FortunaGuidanceEventInput payload)
    {
        if (!payload.DefenderIsEliteOrBoss)
            return;
        var memberId = ProgressionDataUtils.to_string_name(payload.AttackerMemberId);
        if (memberId == "")
            return;
        MarkChapterEventSeen(memberId);
        if (IsFortunaMarked(GetMemberState(memberId)))
            UnlockAchievement(memberId, AchievementGuidanceTrue);
    }

    private void HandleHighThreatCriticalHit(FortunaGuidanceEventInput payload)
    {
        if (!payload.DefenderIsEliteOrBoss)
            return;
        var memberId = ProgressionDataUtils.to_string_name(payload.AttackerMemberId);
        if (memberId == "")
            return;
        var memberState = GetMemberState(memberId);
        if (!IsFortunaDevotee(memberState))
            return;
        MarkChapterEventSeen(memberId);
        UnlockAchievement(memberId, AchievementGuidanceExalted);
    }

    private void HandleHardshipSurvival(FortunaGuidanceEventInput payload)
    {
        var battleId = ProgressionDataUtils.to_string_name(payload.BattleId);
        var memberId = ProgressionDataUtils.to_string_name(payload.AttackerMemberId);
        if (battleId == "" || memberId == "")
            return;
        if (!IsFortunaDevotee(GetMemberState(memberId)))
            return;
        if (!payload.AttackerLowHpHardship)
            return;
        if (!HasAnyStringName(payload.AttackerStrongAttackDebuffIds))
            return;
        MarkChapterEventSeen(memberId);
        var partyState = GetPartyState();
        partyState?.set_fate_run_flag(BuildDevoutBattleFlagId(battleId, memberId), true);
    }

    private void MarkChapterEventSeen(StringName memberId)
    {
        var partyState = GetPartyState();
        if (partyState != null && memberId != "")
            partyState.set_fate_run_flag(BuildChapterEventFlagId(memberId), true);
    }

    private bool UnlockAchievement(StringName memberId, StringName achievementId)
    {
        if (_characterGateway == null || memberId == "" || achievementId == "")
            return false;
        return (_characterGateway as CharacterManagementModule)?.UnlockAchievement(
            memberId,
            achievementId,
            BuildSummaryText(achievementId)
        ) ?? false;
    }

    private static bool IsFortunaMarked(PartyMemberState memberState)
    {
        if (memberState?.progression?.unit_base_attributes == null)
            return false;
        return memberState.progression.unit_base_attributes.get_attribute_value(FortuneMarkedStatId)
            > 0;
    }

    private static bool IsFortunaDevotee(PartyMemberState memberState) =>
        memberState != null && memberState.get_faith_luck_bonus() > 0;

    private static string BuildSummaryText(StringName achievementId)
    {
        if (achievementId == AchievementGuidanceTrue)
            return "Fortuna 再次看见了这名角色。";
        if (achievementId == AchievementGuidanceDevout)
            return "逆境中的胜利让 Fortuna 的怜悯有了回应。";
        if (achievementId == AchievementGuidanceExalted)
            return "好运不再只是门骰，而是被抬进了真正的高位威胁区间。";
        if (achievementId == AchievementGuidanceBlessed)
            return "整章旅程都被 Fortuna 的影子护住了。";
        return "";
    }

    private PartyState GetPartyState() => _characterGateway?.get_party_state();

    private PartyMemberState GetMemberState(StringName memberId) =>
        _characterGateway != null && memberId != ""
            ? _characterGateway.get_member_state(memberId)
            : null;

    private static StringName BuildChapterEventFlagId(StringName memberId) =>
        ProgressionDataUtils.to_string_name($"{ChapterEventFlagPrefix}{memberId}");

    private static StringName BuildDevoutBattleFlagId(StringName battleId, StringName memberId) =>
        ProgressionDataUtils.to_string_name($"{DevoutBattleFlagPrefix}{battleId}:{memberId}");

    private static void AppendUniqueStringName(List<StringName> values, StringName value)
    {
        if (value != "" && !values.Contains(value))
            values.Add(value);
    }

    private static bool HasAnyStringName(IReadOnlyList<StringName> values)
    {
        if (values == null)
            return false;
        foreach (var value in values)
        {
            if (ProgressionDataUtils.to_string_name(value) != "")
                return true;
        }
        return false;
    }

    private static List<StringName> ResolveChapterMemberIds(
        FortunaChapterCompletionInput input,
        PartyState partyState
    )
    {
        var result = new List<StringName>();
        if (input?.MemberIds != null)
        {
            foreach (var rawMemberId in input.MemberIds)
            {
                var memberId = ProgressionDataUtils.to_string_name(rawMemberId);
                if (memberId != "" && !result.Contains(memberId))
                    result.Add(memberId);
            }
        }
        if (result.Count > 0 || partyState == null)
            return result;

        foreach (var memberState in partyState.get_member_states())
        {
            var memberId = ProgressionDataUtils.to_string_name(memberState?.member_id);
            if (memberId != "" && !result.Contains(memberId))
                result.Add(memberId);
        }
        return result;
    }
}
