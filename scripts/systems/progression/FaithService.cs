using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class FaithDevotionResult
{
    public bool Success { get; internal set; }
    public string ErrorCode { get; internal set; } = "";
    public StringName MemberId { get; internal set; } = "";
    public StringName DeityId { get; internal set; } = "";
    public int CurrentRank { get; internal set; }
    public int TargetRank { get; internal set; }
    public int GoldSpent { get; internal set; }
    public PendingCharacterReward PendingReward { get; internal set; }
    public StringName MissingCustomStatId { get; internal set; } = "";
    public StringName MissingAchievementId { get; internal set; } = "";
}

public class FaithService
{
    internal static readonly StringName SourceTypeFaithRankReward = "faith_rank_reward";
    internal static readonly StringName FaithLuckBonusStatId = "faith_luck_bonus";

    private static readonly IReadOnlyDictionary<StringName, FaithDeityDefinition> EmptyDefinitions =
        new ReadOnlyDictionary<StringName, FaithDeityDefinition>(
            new Dictionary<StringName, FaithDeityDefinition>()
        );

    private IReadOnlyDictionary<StringName, FaithDeityDefinition> _faithDeityDefs =
        EmptyDefinitions;

    public FaithService(
        IReadOnlyDictionary<StringName, FaithDeityDefinition> faithDeityDefinitions
    )
    {
        Setup(faithDeityDefinitions);
    }

    public void Setup(
        IReadOnlyDictionary<StringName, FaithDeityDefinition> faithDeityDefinitions
    )
    {
        ArgumentNullException.ThrowIfNull(faithDeityDefinitions);
        _faithDeityDefs = faithDeityDefinitions;
    }

    public IReadOnlyDictionary<StringName, FaithDeityDefinition> GetFaithDeityDefs()
    {
        return _faithDeityDefs;
    }

    public FaithDeityDefinition GetFaithDeityDef(StringName deityId)
    {
        return _faithDeityDefs.TryGetValue(deityId, out FaithDeityDefinition deityDef)
            ? deityDef
            : null;
    }

    public FaithDevotionResult ExecuteDevotion(
        PartyState partyState,
        StringName memberId,
        StringName deityId
    )
    {
        var result = new FaithDevotionResult
        {
            MemberId = memberId,
            DeityId = deityId,
        };

        if (partyState == null || IsEmpty(memberId) || IsEmpty(deityId))
        {
            result.ErrorCode = "invalid_request";
            return result;
        }

        PartyMemberState memberState = partyState.GetMemberState(memberId);
        UnitProgress progress = GetProgress(memberState);
        UnitBaseAttributes attributes = progress?.unit_base_attributes;
        if (memberState == null || progress == null || attributes == null)
        {
            result.ErrorCode = "member_not_found";
            return result;
        }

        FaithDeityDefinition deityDef = GetFaithDeityDef(deityId);
        if (deityDef == null)
        {
            result.ErrorCode = "deity_not_found";
            return result;
        }

        int currentRank = GetCurrentRank(partyState, memberId, deityId, deityDef);
        result.CurrentRank = currentRank;
        if (currentRank >= deityDef.GetMaxRank())
        {
            result.ErrorCode = "max_rank_reached";
            return result;
        }

        FaithRankDefinition nextRank = deityDef.GetRankDefinition(currentRank + 1);
        if (nextRank == null)
        {
            result.ErrorCode = "missing_rank_def";
            return result;
        }
        result.TargetRank = nextRank.RankIndex;

        if (!partyState.CanAfford(nextRank.RequiredGold))
        {
            result.ErrorCode = "insufficient_gold";
            return result;
        }
        if (progress.character_level < nextRank.RequiredLevel)
        {
            result.ErrorCode = "level_too_low";
            return result;
        }
        if (!MeetsPlaceholderRequirements(memberState, nextRank, result))
            return result;

        if (!partyState.SpendGold(nextRank.RequiredGold))
        {
            result.ErrorCode = "insufficient_gold";
            return result;
        }

        EnsureWritableRewardAttributeSeeds(memberState, nextRank);
        PendingCharacterReward reward = BuildRankReward(memberState, deityDef, nextRank);
        if (reward == null || reward.IsEmpty())
        {
            partyState.AddGold(nextRank.RequiredGold);
            result.ErrorCode = "invalid_rank_reward";
            return result;
        }

        partyState.EnqueuePendingCharacterReward(reward);
        result.Success = true;
        result.GoldSpent = nextRank.RequiredGold;
        result.PendingReward = reward;
        return result;
    }

    public int GetCurrentRank(
        PartyState partyState,
        StringName memberId,
        StringName deityId,
        FaithDeityDefinition deityDef = null
    )
    {
        deityDef ??= GetFaithDeityDef(deityId);
        if (deityDef == null || partyState == null)
            return 0;

        PartyMemberState memberState = partyState.GetMemberState(memberId);
        if (memberState == null)
            return 0;

        StringName rankProgressStatId = ResolveRankProgressStatId(deityDef);
        int appliedRank = Mathf.Max(GetCustomStatValue(memberState, rankProgressStatId), 0);
        int pendingRank = CountPendingRankRewards(
            partyState,
            memberId,
            deityId,
            rankProgressStatId
        );
        return Mathf.Clamp(appliedRank + pendingRank, 0, deityDef.GetMaxRank());
    }

    public void Dispose()
    {
        _faithDeityDefs = EmptyDefinitions;
    }

    private static bool MeetsPlaceholderRequirements(
        PartyMemberState memberState,
        FaithRankDefinition rankDef,
        FaithDevotionResult result
    )
    {
        if (rankDef.HasCustomStatRequirement())
        {
            int currentValue = GetCustomStatValue(memberState, rankDef.RequiredCustomStatId);
            if (currentValue < rankDef.RequiredCustomStatMinValue)
            {
                result.ErrorCode = "custom_stat_requirement_unmet";
                result.MissingCustomStatId = rankDef.RequiredCustomStatId;
                return false;
            }
        }
        if (rankDef.HasAchievementRequirement())
        {
            if (!IsAchievementUnlocked(memberState, rankDef.RequiredAchievementId))
            {
                result.ErrorCode = "achievement_requirement_unmet";
                result.MissingAchievementId = rankDef.RequiredAchievementId;
                return false;
            }
        }
        return true;
    }

    private static int GetCustomStatValue(PartyMemberState memberState, StringName statId)
    {
        UnitBaseAttributes attributes = GetProgress(memberState)?.unit_base_attributes;
        return attributes?.GetAttributeValue(statId) ?? 0;
    }

    private static bool IsAchievementUnlocked(
        PartyMemberState memberState,
        StringName achievementId
    )
    {
        UnitProgress progress = GetProgress(memberState);
        if (IsEmpty(achievementId) || progress == null)
            return false;
        AchievementProgressState progressState = progress.GetAchievementProgressState(
            achievementId
        );
        return progressState != null && progressState.is_unlocked;
    }

    private static int CountPendingRankRewards(
        PartyState partyState,
        StringName memberId,
        StringName deityId,
        StringName rankProgressStatId
    )
    {
        if (
            partyState == null
            || IsEmpty(memberId)
            || IsEmpty(deityId)
            || IsEmpty(rankProgressStatId)
        )
            return 0;

        int pendingBonus = 0;
        foreach (PendingCharacterReward reward in partyState.pending_character_rewards)
        {
            if (reward == null)
                continue;
            if (reward.member_id != memberId)
                continue;
            if (reward.source_type != SourceTypeFaithRankReward || reward.source_id != deityId)
                continue;
            foreach (PendingCharacterRewardEntry entry in reward.entries)
            {
                if (entry == null)
                    continue;
                if (
                    entry.EntryKind == PendingCharacterRewardEntryKind.AttributeDelta
                    && entry.target_id == rankProgressStatId
                )
                    pendingBonus += entry.amount;
            }
        }
        return pendingBonus;
    }

    private static void EnsureWritableRewardAttributeSeeds(
        PartyMemberState memberState,
        FaithRankDefinition rankDef
    )
    {
        if (memberState == null || rankDef == null)
            return;
        foreach (FaithRankRewardEntryDefinition rewardEntry in rankDef.RewardEntries)
        {
            if (rewardEntry == null || rewardEntry.EntryType != "attribute_delta")
                continue;
            EnsureWritableCustomStatSeed(memberState, rewardEntry.TargetId);
        }
    }

    private static void EnsureWritableCustomStatSeed(
        PartyMemberState memberState,
        StringName statId
    )
    {
        if (IsEmpty(statId) || IsBaseAttributeId(statId))
            return;
        UnitBaseAttributes attributes = GetProgress(memberState)?.unit_base_attributes;
        if (attributes == null || attributes.custom_stats.ContainsKey(statId))
            return;
        attributes.custom_stats[statId] = attributes.GetAttributeValue(statId);
    }

    private static StringName ResolveRankProgressStatId(FaithDeityDefinition deityDef)
    {
        if (deityDef == null || deityDef.RankProgressStatId == "")
            return FaithLuckBonusStatId;
        return deityDef.RankProgressStatId;
    }

    private static PendingCharacterReward BuildRankReward(
        PartyMemberState memberState,
        FaithDeityDefinition deityDef,
        FaithRankDefinition rankDef
    )
    {
        if (memberState == null || deityDef == null || rankDef == null)
            return null;

        string sourceLabel = !string.IsNullOrEmpty(deityDef.DisplayName)
            ? deityDef.DisplayName
            : deityDef.DeityId.ToString();
        var reward = new PendingCharacterReward
        {
            reward_id = BuildRewardId(memberState.member_id, deityDef.DeityId, rankDef.RankIndex),
            member_id = memberState.member_id,
            member_name = !string.IsNullOrEmpty(memberState.display_name)
                ? memberState.display_name
                : memberState.member_id.ToString(),
            source_type = SourceTypeFaithRankReward,
            source_id = deityDef.DeityId,
            source_label = sourceLabel,
            summary_text = $"{sourceLabel} 晋升为 {rankDef.RankName}",
        };

        reward.entries = new();
        foreach (FaithRankRewardEntryDefinition rewardSpec in rankDef.RewardEntries)
        {
            PendingCharacterRewardEntry rewardEntry = BuildRewardEntry(
                rewardSpec,
                reward.summary_text
            );
            if (rewardEntry == null)
                return null;
            if (rewardEntry.IsEmpty())
                continue;
            reward.entries.Add(rewardEntry);
        }
        return reward.IsEmpty() ? null : reward;
    }

    private static PendingCharacterRewardEntry BuildRewardEntry(
        FaithRankRewardEntryDefinition rewardSpec,
        string defaultReasonText
    )
    {
        if (
            rewardSpec == null
            || rewardSpec.EntryType == ""
            || rewardSpec.TargetId == ""
            || rewardSpec.Amount == 0
        )
            return null;
        if (!PendingCharacterRewardContentRules.IsSupportedEntryType(rewardSpec.EntryType))
            return null;
        if (
            PendingCharacterRewardContentRules.IsAttributeProgressEntry(rewardSpec.EntryType)
            && !PendingCharacterRewardContentRules.IsValidAttributeProgressTarget(
                rewardSpec.TargetId
            )
        )
            return null;

        return new PendingCharacterRewardEntry
        {
            entry_type = rewardSpec.EntryType,
            target_id = rewardSpec.TargetId,
            amount = rewardSpec.Amount,
            target_label = !string.IsNullOrEmpty(rewardSpec.TargetLabel)
                ? rewardSpec.TargetLabel
                : rewardSpec.TargetId.ToString(),
            reason_text = !string.IsNullOrEmpty(rewardSpec.ReasonText)
                ? rewardSpec.ReasonText
                : defaultReasonText,
        };
    }

    private static StringName BuildRewardId(StringName memberId, StringName deityId, int rankIndex)
    {
        return new StringName($"{memberId}_{deityId}_rank_{rankIndex}_{Time.GetTicksUsec()}");
    }

    private static UnitProgress GetProgress(PartyMemberState memberState)
    {
        return memberState?.progression as UnitProgress;
    }

    private static bool IsBaseAttributeId(StringName statId)
    {
        return statId == "strength"
            || statId == "agility"
            || statId == "constitution"
            || statId == "perception"
            || statId == "intelligence"
            || statId == "willpower";
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
    }
}
