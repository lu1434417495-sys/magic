using System.Collections.Generic;
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
    private const string ConfigDirectory = "res://data/configs/faith";

    internal static readonly StringName SourceTypeFaithRankReward = "faith_rank_reward";
    internal static readonly StringName FaithLuckBonusStatId = "faith_luck_bonus";

    private readonly Dictionary<StringName, FaithDeityDef> _faithDeityDefs = new();
    private readonly List<string> _validationErrors = new();

    public FaithService()
    {
        Rebuild();
    }

    public void Setup(IEnumerable<FaithDeityDef> faithDeityDefs = null)
    {
        _faithDeityDefs.Clear();
        _validationErrors.Clear();

        if (faithDeityDefs != null)
        {
            foreach (FaithDeityDef deityDef in faithDeityDefs)
            {
                if (deityDef == null || deityDef.deity_id == "")
                    continue;
                _faithDeityDefs[deityDef.deity_id] = deityDef;
            }
        }

        CollectValidationErrorsInto(_validationErrors);
    }

    public void Rebuild()
    {
        _faithDeityDefs.Clear();
        _validationErrors.Clear();
        ScanDirectory(ConfigDirectory);
        CollectValidationErrorsInto(_validationErrors);
    }

    public IReadOnlyDictionary<StringName, FaithDeityDef> GetFaithDeityDefs()
    {
        return _faithDeityDefs;
    }

    public FaithDeityDef GetFaithDeityDef(StringName deityId)
    {
        return _faithDeityDefs.TryGetValue(deityId, out FaithDeityDef deityDef)
            ? deityDef
            : null;
    }

    public IReadOnlyList<string> Validate()
    {
        return new List<string>(_validationErrors);
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

        FaithDeityDef deityDef = GetFaithDeityDef(deityId);
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

        FaithRankDef nextRank = deityDef.GetRankDef(currentRank + 1);
        if (nextRank == null)
        {
            result.ErrorCode = "missing_rank_def";
            return result;
        }
        result.TargetRank = nextRank.rank_index;

        if (!partyState.CanAfford(nextRank.required_gold))
        {
            result.ErrorCode = "insufficient_gold";
            return result;
        }
        if (progress.character_level < nextRank.required_level)
        {
            result.ErrorCode = "level_too_low";
            return result;
        }
        if (!MeetsPlaceholderRequirements(memberState, nextRank, result))
            return result;

        if (!partyState.SpendGold(nextRank.required_gold))
        {
            result.ErrorCode = "insufficient_gold";
            return result;
        }

        EnsureWritableRewardAttributeSeeds(memberState, nextRank);
        PendingCharacterReward reward = BuildRankReward(memberState, deityDef, nextRank);
        if (reward == null || reward.IsEmpty())
        {
            partyState.AddGold(nextRank.required_gold);
            result.ErrorCode = "invalid_rank_reward";
            return result;
        }

        partyState.EnqueuePendingCharacterReward(reward);
        result.Success = true;
        result.GoldSpent = nextRank.required_gold;
        result.PendingReward = reward;
        return result;
    }

    public int GetCurrentRank(
        PartyState partyState,
        StringName memberId,
        StringName deityId,
        FaithDeityDef deityDef = null
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
        _faithDeityDefs.Clear();
        _validationErrors.Clear();
    }

    private void ScanDirectory(string directoryPath)
    {
        DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"FaithService could not open {directoryPath}.");
            return;
        }

        try
        {
            directory.ListDirBegin();
            while (true)
            {
                string entryName = directory.GetNext();
                if (string.IsNullOrEmpty(entryName))
                    break;
                if (entryName == "." || entryName == "..")
                    continue;

                string entryPath = $"{directoryPath}/{entryName}";
                if (directory.CurrentIsDir())
                {
                    ScanDirectory(entryPath);
                    continue;
                }
                if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                    continue;
                RegisterDeityResource(entryPath);
            }
            directory.ListDirEnd();
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }
    }

    private void RegisterDeityResource(string resourcePath)
    {
        Resource resource = ResourceLoader.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"Failed to load faith config {resourcePath}.");
            return;
        }
        if (resource is not FaithDeityDef deityDef)
        {
            _validationErrors.Add($"Faith config {resourcePath} failed to cast to FaithDeityDef.");
            return;
        }
        if (deityDef.deity_id == "")
        {
            _validationErrors.Add($"Faith config {resourcePath} is missing deity_id.");
            return;
        }
        if (_faithDeityDefs.ContainsKey(deityDef.deity_id))
        {
            _validationErrors.Add($"Duplicate faith deity_id registered: {deityDef.deity_id}");
            return;
        }

        GodotContentOwnership.RegisterBorrowedContent(deityDef, resourcePath);
        _faithDeityDefs[deityDef.deity_id] = deityDef;
    }

    private void CollectValidationErrorsInto(List<string> errors)
    {
        var sortedIds = new List<string>();
        foreach (StringName deityId in _faithDeityDefs.Keys)
            sortedIds.Add(deityId.ToString());
        sortedIds.Sort();

        foreach (string deityIdText in sortedIds)
        {
            FaithDeityDef deityDef = GetFaithDeityDef(deityIdText);
            if (deityDef == null)
                continue;
            foreach (string error in deityDef.Validate())
                errors.Add(error);
        }
    }

    private static bool MeetsPlaceholderRequirements(
        PartyMemberState memberState,
        FaithRankDef rankDef,
        FaithDevotionResult result
    )
    {
        if (rankDef.HasCustomStatRequirement())
        {
            int currentValue = GetCustomStatValue(memberState, rankDef.required_custom_stat_id);
            if (currentValue < rankDef.required_custom_stat_min_value)
            {
                result.ErrorCode = "custom_stat_requirement_unmet";
                result.MissingCustomStatId = rankDef.required_custom_stat_id;
                return false;
            }
        }
        if (rankDef.HasAchievementRequirement())
        {
            if (!IsAchievementUnlocked(memberState, rankDef.required_achievement_id))
            {
                result.ErrorCode = "achievement_requirement_unmet";
                result.MissingAchievementId = rankDef.required_achievement_id;
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
        FaithRankDef rankDef
    )
    {
        if (memberState == null || rankDef == null)
            return;
        foreach (FaithRankRewardEntrySpec rewardEntry in rankDef.GetRewardEntrySpecs())
        {
            if (rewardEntry.EntryType != "attribute_delta")
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

    private static StringName ResolveRankProgressStatId(FaithDeityDef deityDef)
    {
        if (deityDef == null || deityDef.rank_progress_stat_id == "")
            return FaithLuckBonusStatId;
        return deityDef.rank_progress_stat_id;
    }

    private static PendingCharacterReward BuildRankReward(
        PartyMemberState memberState,
        FaithDeityDef deityDef,
        FaithRankDef rankDef
    )
    {
        if (memberState == null || deityDef == null || rankDef == null)
            return null;

        string sourceLabel = !string.IsNullOrEmpty(deityDef.display_name)
            ? deityDef.display_name
            : deityDef.deity_id.ToString();
        var reward = new PendingCharacterReward
        {
            reward_id = BuildRewardId(memberState.member_id, deityDef.deity_id, rankDef.rank_index),
            member_id = memberState.member_id,
            member_name = !string.IsNullOrEmpty(memberState.display_name)
                ? memberState.display_name
                : memberState.member_id.ToString(),
            source_type = SourceTypeFaithRankReward,
            source_id = deityDef.deity_id,
            source_label = sourceLabel,
            summary_text = $"{sourceLabel} 晋升为 {rankDef.rank_name}",
        };

        reward.entries = new();
        foreach (FaithRankRewardEntrySpec rewardSpec in rankDef.GetRewardEntrySpecs())
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
        FaithRankRewardEntrySpec rewardSpec,
        string defaultReasonText
    )
    {
        if (rewardSpec.IsEmpty)
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
