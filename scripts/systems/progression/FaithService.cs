using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class FaithService : RefCounted
{
    private const string ConfigDirectory = "res://data/configs/faith";
    private static readonly StringName SourceTypeFaithRankReward = "faith_rank_reward";
    private static readonly StringName FaithLuckBonusStatId = "faith_luck_bonus";

    private readonly GDictionary _faithDeityDefs = new();
    private readonly Godot.Collections.Array<string> _validationErrors = new();

    public FaithService()
    {
        rebuild();
    }

    public static StringName SOURCE_TYPE_FAITH_RANK_REWARD() => SourceTypeFaithRankReward;

    public static StringName FAITH_LUCK_BONUS_STAT_ID() => FaithLuckBonusStatId;

    public void setup(GDictionary faith_deity_defs = null)
    {
        _faithDeityDefs.Clear();
        _validationErrors.Clear();

        if (faith_deity_defs != null)
        {
            foreach (var key in faith_deity_defs.Keys)
            {
                FaithDeityDef deityDef = faith_deity_defs[key].AsGodotObject() as FaithDeityDef;
                if (deityDef == null || deityDef.deity_id == "")
                    continue;
                _faithDeityDefs[deityDef.deity_id] = deityDef;
            }
        }

        foreach (string error in CollectValidationErrors())
            _validationErrors.Add(error);
    }

    public void rebuild()
    {
        _faithDeityDefs.Clear();
        _validationErrors.Clear();
        ScanDirectory(ConfigDirectory);
        foreach (string error in CollectValidationErrors())
            _validationErrors.Add(error);
    }

    public GDictionary get_faith_deity_defs()
    {
        return _faithDeityDefs;
    }

    public FaithDeityDef get_faith_deity_def(StringName deity_id)
    {
        return _faithDeityDefs.ContainsKey(deity_id)
            ? _faithDeityDefs[deity_id].AsGodotObject() as FaithDeityDef
            : null;
    }

    public Godot.Collections.Array<string> validate()
    {
        return _validationErrors.Duplicate();
    }

    public GDictionary execute_devotion(
        PartyState party_state,
        StringName member_id,
        StringName deity_id
    )
    {
        var result = new GDictionary
        {
            ["ok"] = false,
            ["error_code"] = "",
            ["member_id"] = member_id.ToString(),
            ["deity_id"] = deity_id.ToString(),
            ["current_rank"] = 0,
            ["target_rank"] = 0,
            ["gold_spent"] = 0,
            ["pending_reward"] = new GDictionary(),
            ["missing_custom_stat_id"] = "",
            ["missing_achievement_id"] = "",
        };

        if (party_state == null || member_id == "" || deity_id == "")
        {
            result["error_code"] = "invalid_request";
            return result;
        }

        PartyMemberState memberState = party_state.get_member_state(member_id);
        UnitProgress progress = GetProgress(memberState);
        UnitBaseAttributes attributes = progress?.unit_base_attributes;
        if (memberState == null || progress == null || attributes == null)
        {
            result["error_code"] = "member_not_found";
            return result;
        }

        FaithDeityDef deityDef = get_faith_deity_def(deity_id);
        if (deityDef == null)
        {
            result["error_code"] = "deity_not_found";
            return result;
        }

        int currentRank = get_current_rank(party_state, member_id, deity_id, deityDef);
        result["current_rank"] = currentRank;
        if (currentRank >= deityDef.get_max_rank())
        {
            result["error_code"] = "max_rank_reached";
            return result;
        }

        FaithRankDef nextRank = deityDef.get_rank_def(currentRank + 1);
        if (nextRank == null)
        {
            result["error_code"] = "missing_rank_def";
            return result;
        }
        result["target_rank"] = nextRank.rank_index;

        if (!party_state.can_afford(nextRank.required_gold))
        {
            result["error_code"] = "insufficient_gold";
            return result;
        }
        if (progress.character_level < nextRank.required_level)
        {
            result["error_code"] = "level_too_low";
            return result;
        }
        if (!MeetsPlaceholderRequirements(memberState, nextRank, result))
            return result;

        if (!party_state.spend_gold(nextRank.required_gold))
        {
            result["error_code"] = "insufficient_gold";
            return result;
        }

        EnsureWritableRewardAttributeSeeds(memberState, nextRank);
        PendingCharacterReward reward = BuildRankReward(memberState, deityDef, nextRank);
        if (reward == null || reward.is_empty())
        {
            party_state.add_gold(nextRank.required_gold);
            result["error_code"] = "invalid_rank_reward";
            return result;
        }

        party_state.enqueue_pending_character_reward(reward);
        result["ok"] = true;
        result["gold_spent"] = nextRank.required_gold;
        result["pending_reward"] = reward.to_dict();
        return result;
    }

    public int get_current_rank(
        PartyState party_state,
        StringName member_id,
        StringName deity_id,
        FaithDeityDef deity_def = null
    )
    {
        deity_def ??= get_faith_deity_def(deity_id);
        if (deity_def == null || party_state == null)
            return 0;

        PartyMemberState memberState = party_state.get_member_state(member_id);
        if (memberState == null)
            return 0;

        StringName rankProgressStatId = ResolveRankProgressStatId(deity_def);
        int appliedRank = Mathf.Max(GetCustomStatValue(memberState, rankProgressStatId), 0);
        int pendingRank = CountPendingRankRewards(
            party_state,
            member_id,
            deity_id,
            rankProgressStatId
        );
        return Mathf.Clamp(appliedRank + pendingRank, 0, deity_def.get_max_rank());
    }

    private void ScanDirectory(string directoryPath)
    {
        DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"FaithService could not open {directoryPath}.");
            return;
        }

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

    private void RegisterDeityResource(string resourcePath)
    {
        Resource resource = ResourceLoader.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"Failed to load faith config {resourcePath}.");
            return;
        }
        FaithDeityDef deityDef = resource as FaithDeityDef;
        if (deityDef == null)
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

        _faithDeityDefs[deityDef.deity_id] = deityDef;
    }

    private Godot.Collections.Array<string> CollectValidationErrors()
    {
        var errors = new Godot.Collections.Array<string>();
        var sortedIds = new System.Collections.Generic.List<string>();
        foreach (var key in _faithDeityDefs.Keys)
            sortedIds.Add(ProgressionDataUtils.to_string_name(key).ToString());
        sortedIds.Sort();

        foreach (string deityIdText in sortedIds)
        {
            var deityId = new StringName(deityIdText);
            FaithDeityDef deityDef = get_faith_deity_def(deityId);
            if (deityDef == null)
                continue;
            foreach (string error in deityDef.validate())
                errors.Add(error);
        }
        return errors;
    }

    private bool MeetsPlaceholderRequirements(
        PartyMemberState memberState,
        FaithRankDef rankDef,
        GDictionary result
    )
    {
        if (rankDef.has_custom_stat_requirement())
        {
            int currentValue = GetCustomStatValue(memberState, rankDef.required_custom_stat_id);
            if (currentValue < rankDef.required_custom_stat_min_value)
            {
                result["error_code"] = "custom_stat_requirement_unmet";
                result["missing_custom_stat_id"] = rankDef.required_custom_stat_id.ToString();
                return false;
            }
        }
        if (rankDef.has_achievement_requirement())
        {
            if (!IsAchievementUnlocked(memberState, rankDef.required_achievement_id))
            {
                result["error_code"] = "achievement_requirement_unmet";
                result["missing_achievement_id"] = rankDef.required_achievement_id.ToString();
                return false;
            }
        }
        return true;
    }

    private static int GetCustomStatValue(PartyMemberState memberState, StringName statId)
    {
        UnitBaseAttributes attributes = GetProgress(memberState)?.unit_base_attributes;
        return attributes?.get_attribute_value(statId) ?? 0;
    }

    private static bool IsAchievementUnlocked(
        PartyMemberState memberState,
        StringName achievementId
    )
    {
        UnitProgress progress = GetProgress(memberState);
        if (achievementId == "" || progress == null)
            return false;
        AchievementProgressState progressState = progress.get_achievement_progress_state(
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
        if (partyState == null || memberId == "" || deityId == "" || rankProgressStatId == "")
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
                if (entry.entry_type == "attribute_delta" && entry.target_id == rankProgressStatId)
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
        foreach (GDictionary rewardData in rankDef.reward_entries)
        {
            if (rewardData == null)
                continue;
            if (ReadStringName(rewardData, "entry_type") != "attribute_delta")
                continue;
            StringName attributeId = ReadStringName(rewardData, "target_id");
            EnsureWritableCustomStatSeed(memberState, attributeId);
        }
    }

    private static void EnsureWritableCustomStatSeed(
        PartyMemberState memberState,
        StringName statId
    )
    {
        if (statId == "" || UnitBaseAttributes.BASE_ATTRIBUTE_IDS().Contains(statId))
            return;
        UnitBaseAttributes attributes = GetProgress(memberState)?.unit_base_attributes;
        if (attributes == null || attributes.custom_stats.ContainsKey(statId))
            return;
        attributes.custom_stats[statId] = attributes.get_attribute_value(statId);
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

        var normalizedEntries = new Godot.Collections.Array<PendingCharacterRewardEntry>();
        foreach (GDictionary rewardData in rankDef.reward_entries)
        {
            if (rewardData != null)
            {
                StringName entryType = ReadStringName(rewardData, "entry_type");
                StringName targetId = ReadStringName(rewardData, "target_id");
                if (
                    entryType != ""
                    && !PendingCharacterRewardContentRules.is_supported_entry_type(entryType)
                )
                    return null;
                if (
                    PendingCharacterRewardContentRules.is_attribute_progress_entry(entryType)
                    && !PendingCharacterRewardContentRules.is_valid_attribute_progress_target(
                        targetId
                    )
                )
                    return null;
            }

            PendingCharacterRewardEntry rewardEntry = PendingCharacterRewardEntry.from_dict(
                rewardData
            );
            if (rewardEntry == null || rewardEntry.is_empty())
                continue;
            if (string.IsNullOrEmpty(rewardEntry.reason_text))
                rewardEntry.reason_text = reward.summary_text;
            if (string.IsNullOrEmpty(rewardEntry.target_label))
                rewardEntry.target_label = rewardEntry.target_id.ToString();
            normalizedEntries.Add(rewardEntry);
        }

        reward.entries = normalizedEntries;
        return reward.is_empty() ? null : reward;
    }

    private static StringName BuildRewardId(StringName memberId, StringName deityId, int rankIndex)
    {
        return ProgressionDataUtils.to_string_name(
            $"{memberId}_{deityId}_rank_{rankIndex}_{Time.GetTicksUsec()}"
        );
    }

    private static UnitProgress GetProgress(PartyMemberState memberState)
    {
        return memberState?.progression as UnitProgress;
    }

    private static StringName ReadStringName(
        GDictionary data,
        string key,
        StringName fallback = default
    )
    {
        var value = ReadValue(data, key);
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName();
        if (value.VariantType == Variant.Type.String)
            return new StringName(value.AsString());
        return fallback ?? new StringName("");
    }

    private static Variant ReadValue(GDictionary data, string key)
    {
        if (data == null)
            return default;
        if (data.ContainsKey(key))
            return data[key];
        var stringNameKey = new StringName(key);
        if (data.ContainsKey(stringNameKey))
            return data[stringNameKey];
        return default;
    }
}
