using Godot;
using System;
using System.Collections.Generic;

public partial class PartyState
{
    private static readonly string[] TO_DICT_FIELDS =
    {
        "version",
        "gold",
        "world_renown",
        "country_reputations",
        "leader_member_id",
        "main_character_member_id",
        "fate_run_flags",
        "meta_flags",
        "active_member_ids",
        "reserve_member_ids",
        "member_states",
        "pending_character_rewards",
        "active_quests",
        "claimable_quests",
        "failed_quests",
        "completed_quest_ids",
        "warehouse_state",
    };

    public int version = 9;
    public int gold;
    public int world_renown { get; private set; }
    public CountryReputationState country_reputations { get; private set; } = new();
    public StringName leader_member_id = "",
        main_character_member_id = "";
    public Dictionary<StringName, bool> fate_run_flags { get; private set; } = new();
    public Dictionary<StringName, bool> meta_flags { get; private set; } = new();
    public StringNameList active_member_ids = new(),
        reserve_member_ids = new();
    public PartyMemberStateCollection member_states = new();
    public List<PendingCharacterReward> pending_character_rewards = new();
    private QuestJournalState quest_journal = new();
    public WarehouseState warehouse_state = new WarehouseState();

    public PartyMemberState GetMemberState(StringName id)
    {
        StringName normalizedId = ProgressionDataUtils.to_string_name(id);
        if (normalizedId == "")
            return null;

        return member_states.Get(normalizedId);
    }

    public bool HasMemberState(StringName id) => GetMemberState(id) != null;

    public List<PartyMemberState> GetMemberStates()
    {
        return member_states.GetValuesTyped();
    }

    public bool IsMemberDead(StringName id)
    {
        var m = GetMemberState(id);
        return m != null && m.is_dead;
    }

    public StringName GetResolvedMainCharacterMemberId() =>
        main_character_member_id != "" && HasMemberState(main_character_member_id)
            ? main_character_member_id
            : new StringName("");

    public bool GetFateRunFlag(StringName id, bool defVal = false)
    {
        return id != "" && fate_run_flags.TryGetValue(id, out bool value) ? value : defVal;
    }

    public bool HasFateRunFlag(StringName id) => GetFateRunFlag(id);

    public void SetFateRunFlag(StringName id, bool en = true)
    {
        if (id != "")
            fate_run_flags[id] = en;
    }

    public void ClearFateRunFlag(StringName id)
    {
        if (id != "")
            fate_run_flags.Remove(id);
    }

    public Godot.Collections.Dictionary CaptureFateRunFlags()
    {
        var result = new Godot.Collections.Dictionary();
        foreach (var kv in fate_run_flags)
            result[kv.Key] = kv.Value;
        return result;
    }

    internal Dictionary<StringName, bool> CaptureFateRunFlagsTyped() =>
        new(fate_run_flags);

    public void ApplyFateRunFlags(Godot.Collections.Dictionary flags)
    {
        var next = new Dictionary<StringName, bool>();
        if (flags != null)
            foreach (var key in flags.Keys)
            {
                var flagId = ProgressionDataUtils.to_string_name(key);
                if (flagId == "")
                    continue;
                if (TryReadBool(flags, key, out bool value))
                    next[flagId] = value;
            }
        fate_run_flags = next;
    }

    internal void ApplyFateRunFlagsTyped(IReadOnlyDictionary<StringName, bool> flags)
    {
        var next = new Dictionary<StringName, bool>();
        if (flags != null)
            foreach (KeyValuePair<StringName, bool> entry in flags)
            {
                StringName flagId = ProgressionDataUtils.to_string_name(entry.Key);
                if (flagId != "")
                    next[flagId] = entry.Value;
            }
        fate_run_flags = next;
    }

    public bool GetMetaFlag(StringName id, bool defVal = false)
    {
        return id != "" && meta_flags.TryGetValue(id, out bool value) ? value : defVal;
    }

    public bool HasMetaFlag(StringName id) => GetMetaFlag(id);

    public void SetMetaFlag(StringName id, bool en = true)
    {
        if (id != "")
            meta_flags[id] = en;
    }

    public void ClearMetaFlag(StringName id)
    {
        if (id != "")
            meta_flags.Remove(id);
    }

    public void RemoveMemberFromRosters(StringName id)
    {
        if (id == "")
            return;
        active_member_ids.Remove(id);
        reserve_member_ids.Remove(id);
        if (leader_member_id == id)
            leader_member_id =
                active_member_ids.Count > 0 ? active_member_ids[0] : new StringName("");
    }

    public List<QuestState> GetActiveQuestsTyped() => quest_journal.GetActiveQuests();

    public List<QuestState> GetClaimableQuestsTyped() => quest_journal.GetClaimableQuests();

    public List<QuestState> GetFailedQuestsTyped() => quest_journal.GetFailedQuests();

    public List<StringName> GetCompletedQuestIdsTyped() =>
        quest_journal.GetRewardedQuestIds();

    public int GetGold() => Mathf.Max(gold, 0);

    public int GetWorldRenown() => world_renown;

    public int SetWorldRenown(int value)
    {
        world_renown = SocialStandingRules.ClampWorldRenown(value);
        return world_renown;
    }

    public int AddWorldRenown(int delta)
    {
        world_renown = SocialStandingRules.ClampWorldRenown((long)world_renown + delta);
        return world_renown;
    }

    public int GetCountryReputation(StringName countryId) =>
        country_reputations?.Get(countryId) ?? 0;

    public int SetCountryReputation(StringName countryId, int value)
    {
        country_reputations ??= new CountryReputationState();
        return country_reputations.Set(countryId, value);
    }

    public int AddCountryReputation(StringName countryId, int delta)
    {
        country_reputations ??= new CountryReputationState();
        return country_reputations.Add(countryId, delta);
    }

    public PartyState DuplicateState()
    {
        return new PartyState
        {
            version = version,
            gold = gold,
            world_renown = world_renown,
            country_reputations =
                country_reputations?.DuplicateState() ?? new CountryReputationState(),
            leader_member_id = leader_member_id,
            main_character_member_id = main_character_member_id,
            fate_run_flags = DuplicateBoolMap(fate_run_flags),
            meta_flags = DuplicateBoolMap(meta_flags),
            active_member_ids = active_member_ids?.Duplicate() ?? new StringNameList(),
            reserve_member_ids = reserve_member_ids?.Duplicate() ?? new StringNameList(),
            member_states = member_states?.DuplicateState() ?? new PartyMemberStateCollection(),
            pending_character_rewards = DuplicatePendingCharacterRewards(pending_character_rewards),
            quest_journal = quest_journal?.DuplicateState() ?? new QuestJournalState(),
            warehouse_state = warehouse_state?.DuplicateState() ?? new WarehouseState(),
        };
    }

    public void SetGold(int v) => gold = Mathf.Max(v, 0);

    public int AddGold(int a)
    {
        SetGold(GetGold() + a);
        return gold;
    }

    public bool CanAfford(int amount) => GetGold() >= Mathf.Max(amount, 0);

    public bool SpendGold(int amount)
    {
        int cost = Mathf.Max(amount, 0);
        if (cost == 0)
            return true;
        if (!CanAfford(cost))
            return false;
        SetGold(GetGold() - cost);
        return true;
    }

    public void SetMemberState(PartyMemberState ms)
    {
        if (ms != null && ms.member_id != "")
            member_states.Set(ms);
    }

    public void RemoveMemberState(StringName id) => member_states.Remove(id);

    public void EnqueuePendingCharacterReward(PendingCharacterReward r)
    {
        if (r != null && !r.IsEmpty())
            pending_character_rewards.Add(r);
    }

    public PendingCharacterReward GetPendingCharacterReward(StringName rid)
    {
        foreach (var r in pending_character_rewards)
            if (r != null && r.reward_id == rid)
                return r;
        return null;
    }

    public PendingCharacterReward GetNextPendingCharacterReward() =>
        pending_character_rewards.Count > 0 ? pending_character_rewards[0] : null;

    public bool RemovePendingCharacterReward(StringName rid)
    {
        for (int i = 0; i < pending_character_rewards.Count; i++)
        {
            if (
                pending_character_rewards[i] != null
                && pending_character_rewards[i].reward_id == rid
            )
            {
                pending_character_rewards.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public QuestState GetActiveQuestState(StringName qid)
    {
        return quest_journal.GetActiveQuest(qid);
    }

    public bool HasActiveQuest(StringName qid) => quest_journal.HasActiveQuest(qid);

    public QuestState GetClaimableQuestState(StringName qid)
    {
        return quest_journal.GetClaimableQuest(qid);
    }

    public bool HasClaimableQuest(StringName qid) =>
        quest_journal.HasClaimableQuest(qid);

    public QuestState GetFailedQuestState(StringName qid)
    {
        return quest_journal.GetFailedQuest(qid);
    }

    public bool HasFailedQuest(StringName qid) => quest_journal.HasFailedQuest(qid);

    public QuestState GetQuestState(StringName qid)
    {
        return quest_journal.GetQuest(qid);
    }

    internal bool SetQuestState(StringName qid, QuestState q)
    {
        if (q == null)
            return false;
        if (qid != "" && q.quest_id != "" && q.quest_id != qid)
            return false;
        if (q.quest_id == "")
            q.quest_id = qid;
        if (q.quest_id == "")
            return false;
        return quest_journal.SetState(q);
    }

    internal bool SetActiveQuestState(QuestState q)
    {
        return quest_journal.SetActiveQuest(q);
    }

    internal bool SetClaimableQuestState(QuestState q)
    {
        return quest_journal.SetClaimableQuest(q);
    }

    internal bool SetFailedQuestState(QuestState q)
    {
        return quest_journal.SetFailedQuest(q);
    }

    internal bool RemoveActiveQuest(StringName qid)
    {
        return quest_journal.RemoveActiveQuest(qid);
    }

    internal bool RemoveClaimableQuest(StringName qid)
    {
        return quest_journal.RemoveClaimableQuest(qid);
    }

    internal bool RemoveFailedQuest(StringName qid)
    {
        return quest_journal.RemoveFailedQuest(qid);
    }

    public List<StringName> GetActiveQuestIdsTyped() =>
        quest_journal.GetActiveQuestIds();

    public List<StringName> GetClaimableQuestIdsTyped() =>
        quest_journal.GetClaimableQuestIds();

    public List<StringName> GetFailedQuestIdsTyped() =>
        quest_journal.GetFailedQuestIds();

    public bool HasCompletedQuest(StringName qid) =>
        quest_journal.HasRewardedQuest(qid);

    internal bool AddCompletedQuestId(StringName qid)
    {
        return quest_journal.AddRewardedQuest(qid);
    }

    internal bool AcceptNewQuest(StringName qid, int worldStep)
    {
        return quest_journal.TryAcceptNewQuest(qid, worldStep);
    }

    internal bool RestartRewardedQuest(StringName qid, int worldStep)
    {
        return quest_journal.TryRestartRewardedQuest(qid, worldStep);
    }

    internal bool RestartFailedQuest(StringName qid, int worldStep)
    {
        return quest_journal.TryRestartFailedQuest(qid, worldStep);
    }

    internal bool RecordQuestObjectiveProgress(
        StringName qid,
        StringName objectiveId,
        int delta,
        int targetValue,
        QuestProgressContext context,
        out QuestState updatedState
    )
    {
        return quest_journal.TryRecordObjectiveProgress(
            qid,
            objectiveId,
            delta,
            targetValue,
            context,
            out updatedState
        );
    }

    public bool MarkQuestClaimable(StringName qid, int ws = -1)
    {
        return quest_journal.TryMarkClaimable(qid, ws);
    }

    public bool MarkQuestCompleted(StringName qid, int ws = -1) => MarkQuestClaimable(qid, ws);

    public bool MarkQuestRewardClaimed(StringName qid, int ws = -1)
    {
        return quest_journal.TryMarkRewarded(qid, ws);
    }

    internal bool MarkQuestFailed(
        StringName qid,
        int worldStep,
        StringName reasonId,
        QuestProgressContext context
    )
    {
        return quest_journal.TryMarkFailed(qid, worldStep, reasonId, context);
    }

    internal void ClearQuestJournal()
    {
        quest_journal.Clear();
    }

    internal GodotProjectionLease<Godot.Collections.Dictionary> ToDictionaryLease(
        string ownerId = "PartyState.ToDictionary"
    ) =>
        RuntimePlainPayload.ProjectDictionaryLease(
            BuildSaveSnapshotPlain(),
            ownerId,
            LifetimeDomain.Request,
            "PartyState.ToDictionary"
        );

    internal static bool TryReadPartyPayload(object rawValue, out PartyState value)
    {
        value = null;
        switch (rawValue)
        {
            case null:
                return false;
            case PartyState party:
                value = party;
                return value != null;
            case Variant variantValue when variantValue.VariantType == Variant.Type.Dictionary:
                value = FromDictionary(variantValue.AsGodotDictionary());
                return value != null;
            case Godot.Collections.Dictionary payload:
                value = FromDictionary(payload);
                return value != null;
            default:
                return false;
        }
    }

    public static PartyState FromDictionary(Godot.Collections.Dictionary data) =>
        FromDictionary(data, out _);

    /// <paramref name="failureReason"/> 说明是哪个字段让解码失败（成功时为空）。
    /// 只回 null 的话，读档失败时连"坏在 member_states 还是 active_quests"都查不出来。
    public static PartyState FromDictionary(
        Godot.Collections.Dictionary data,
        out string failureReason
    )
    {
        failureReason = DecodeInto(data, out PartyState result);
        return result;
    }

    /// 返回空字符串表示解码成功；否则返回失败字段的说明。
    private static string DecodeInto(
        Godot.Collections.Dictionary data,
        out PartyState result
    )
    {
        result = null;
        if (data.Count == 0)
            return "party_state: payload dictionary is empty";
        if (!_has_exact_fields(data, TO_DICT_FIELDS))
            return "party_state: field set does not match the current schema";
        if (data["version"].VariantType != Variant.Type.Int)
            return "version: expected Int, got " + data["version"].VariantType;
        // 版本不符单独成一条：它是开发期最常见的弃档原因，不该和字段损坏混在一起。
        if (data["version"].AsInt32() != 9)
            return $"version: expected 9, got {data["version"].AsInt32()}";
        if (data["world_renown"].VariantType != Variant.Type.Int)
            return "world_renown: expected Int, got " + data["world_renown"].VariantType;
        long parsedWorldRenown = data["world_renown"].AsInt64();
        if (!SocialStandingRules.IsValidWorldRenown(parsedWorldRenown))
            return $"world_renown: {parsedWorldRenown} is out of range";
        if (data["country_reputations"].VariantType != Variant.Type.Dictionary)
            return "country_reputations: expected Dictionary, got " + data["country_reputations"].VariantType;
        if (data["warehouse_state"].VariantType != Variant.Type.Dictionary)
            return "warehouse_state: expected Dictionary, got " + data["warehouse_state"].VariantType;
        if (data["member_states"].VariantType != Variant.Type.Dictionary)
            return "member_states: expected Dictionary, got " + data["member_states"].VariantType;
        if (data["pending_character_rewards"].VariantType != Variant.Type.Array)
            return "pending_character_rewards: expected Array, got " + data["pending_character_rewards"].VariantType;
        if (data["active_quests"].VariantType != Variant.Type.Array)
            return "active_quests: expected Array, got " + data["active_quests"].VariantType;
        if (data["claimable_quests"].VariantType != Variant.Type.Array)
            return "claimable_quests: expected Array, got " + data["claimable_quests"].VariantType;
        if (data["failed_quests"].VariantType != Variant.Type.Array)
            return "failed_quests: expected Array, got " + data["failed_quests"].VariantType;
        if (data["completed_quest_ids"].VariantType != Variant.Type.Array)
            return "completed_quest_ids: expected Array, got " + data["completed_quest_ids"].VariantType;
        if (data["fate_run_flags"].VariantType != Variant.Type.Dictionary)
            return "fate_run_flags: expected Dictionary, got " + data["fate_run_flags"].VariantType;
        if (data["meta_flags"].VariantType != Variant.Type.Dictionary)
            return "meta_flags: expected Dictionary, got " + data["meta_flags"].VariantType;
        if (data["gold"].VariantType != Variant.Type.Int)
            return "gold: expected Int, got " + data["gold"].VariantType;
        if (data["gold"].AsInt32() < 0)
            return $"gold: {data["gold"].AsInt32()} must not be negative";
        if (data["active_member_ids"].VariantType != Variant.Type.Array)
            return "active_member_ids: expected Array, got " + data["active_member_ids"].VariantType;
        if (data["reserve_member_ids"].VariantType != Variant.Type.Array)
            return "reserve_member_ids: expected Array, got " + data["reserve_member_ids"].VariantType;

        var leaderMemberId = _parse_required_string_name(
            data["leader_member_id"],
            out bool leaderOk
        );
        if (!leaderOk)
            return "leader_member_id: not a usable string name";
        var mainCharacterMemberId = _parse_required_string_name(
            data["main_character_member_id"],
            out bool mainOk
        );
        if (!mainOk)
            return "main_character_member_id: not a usable string name";

        var parsedFateRunFlags = _parse_boolean_flag_dict(
            data["fate_run_flags"].AsGodotDictionary()
        );
        if (parsedFateRunFlags == null)
            return "fate_run_flags: not a boolean flag map";
        var parsedMetaFlags = _parse_boolean_flag_dict(data["meta_flags"].AsGodotDictionary());
        if (parsedMetaFlags == null)
            return "meta_flags: not a boolean flag map";
        CountryReputationState parsedCountryReputations;
        try
        {
            parsedCountryReputations = CountryReputationState.FromDictionary(
                data["country_reputations"].AsGodotDictionary()
            );
        }
        catch (ArgumentException exception)
        {
            return $"country_reputations: {exception.Message}";
        }
        var parsedActiveMemberIds = _parse_unique_string_name_array(
            data["active_member_ids"].AsGodotArray()
        );
        if (parsedActiveMemberIds == null)
            return "active_member_ids: not a unique string-name array";
        var parsedReserveMemberIds = _parse_unique_string_name_array(
            data["reserve_member_ids"].AsGodotArray()
        );
        if (parsedReserveMemberIds == null)
            return "reserve_member_ids: not a unique string-name array";

        var warehouseState = WarehouseState.FromDictionary(
            data["warehouse_state"].AsGodotDictionary()
        );
        if (warehouseState == null)
            return "warehouse_state: decode failed";

        PartyMemberStateCollection parsedMemberStates;
        try
        {
            parsedMemberStates = PartyMemberStateCollection.FromSaveDictionary(
                data["member_states"].AsGodotDictionary()
            );
        }
        catch (ArgumentException exception)
        {
            return $"member_states: {exception.Message}";
        }

        var partyState = new PartyState
        {
            version = data["version"].AsInt32(),
            gold = data["gold"].AsInt32(),
            world_renown = (int)parsedWorldRenown,
            country_reputations = parsedCountryReputations,
            leader_member_id = leaderMemberId,
            main_character_member_id = mainCharacterMemberId,
            fate_run_flags = parsedFateRunFlags,
            meta_flags = parsedMetaFlags,
            active_member_ids = parsedActiveMemberIds,
            reserve_member_ids = parsedReserveMemberIds,
            member_states = parsedMemberStates,
            warehouse_state = warehouseState,
        };

        if (!_has_unique_equipment_instance_ids(partyState))
            return "member_states: equipment instance ids are not unique across the party";
        if (partyState.leader_member_id == "")
            return "leader_member_id: must not be empty";
        if (!partyState.HasMemberState(partyState.leader_member_id))
            return $"leader_member_id: '{partyState.leader_member_id}' has no member state";

        var rosterSeenIds = new HashSet<StringName>();
        foreach (var memberId in partyState.active_member_ids)
        {
            if (!partyState.HasMemberState(memberId))
                return $"active_member_ids: '{memberId}' has no member state";
            rosterSeenIds.Add(memberId);
        }
        foreach (var memberId in partyState.reserve_member_ids)
        {
            if (!rosterSeenIds.Add(memberId))
                return $"reserve_member_ids: '{memberId}' is already on the roster";
            if (!partyState.HasMemberState(memberId))
                return $"reserve_member_ids: '{memberId}' has no member state";
        }

        foreach (var rewardValue in data["pending_character_rewards"].AsGodotArray())
        {
            if (rewardValue.VariantType != Variant.Type.Dictionary)
                return "pending_character_rewards: entry is not a dictionary";
            var reward = PendingCharacterRewardPayload.ReadSavePayload(
                rewardValue.AsGodotDictionary()
            );
            if (reward == null || reward.IsEmpty())
                return "pending_character_rewards: entry decoded to an empty reward";
            partyState.pending_character_rewards.Add(reward);
        }

        var seenQuestIds = new HashSet<StringName>();
        foreach (var questValue in data["active_quests"].AsGodotArray())
        {
            if (questValue.VariantType != Variant.Type.Dictionary)
                return "active_quests: entry is not a dictionary";
            var questState = QuestState.FromDictionary(
                questValue.AsGodotDictionary(),
                out string questFailureActive
            );
            if (questState == null)
                return $"active_quests: {questFailureActive}";
            if (questState.quest_id == "")
                return "active_quests: entry has an empty quest_id";
            if (!seenQuestIds.Add(questState.quest_id))
                return $"active_quests: '{questState.quest_id}' duplicates another quest entry";
            if (questState.status_id != QuestState.ToStringName(QuestStatusKind.Active))
                return $"active_quests: '{questState.quest_id}' has status '{questState.status_id}'";
            if (!partyState.SetActiveQuestState(questState))
                return $"active_quests: '{questState.quest_id}' was rejected";
        }

        foreach (var questValue in data["claimable_quests"].AsGodotArray())
        {
            if (questValue.VariantType != Variant.Type.Dictionary)
                return "claimable_quests: entry is not a dictionary";
            var questState = QuestState.FromDictionary(
                questValue.AsGodotDictionary(),
                out string questFailureClaimable
            );
            if (questState == null)
                return $"claimable_quests: {questFailureClaimable}";
            if (questState.quest_id == "")
                return "claimable_quests: entry has an empty quest_id";
            if (!seenQuestIds.Add(questState.quest_id))
                return $"claimable_quests: '{questState.quest_id}' duplicates another quest entry";
            if (questState.status_id != QuestState.ToStringName(QuestStatusKind.Completed))
                return $"claimable_quests: '{questState.quest_id}' has status '{questState.status_id}'";
            if (!partyState.SetClaimableQuestState(questState))
                return $"claimable_quests: '{questState.quest_id}' was rejected";
        }

        foreach (var questValue in data["failed_quests"].AsGodotArray())
        {
            if (questValue.VariantType != Variant.Type.Dictionary)
                return "failed_quests: entry is not a dictionary";
            var questState = QuestState.FromDictionary(
                questValue.AsGodotDictionary(),
                out string questFailureFailed
            );
            if (questState == null)
                return $"failed_quests: {questFailureFailed}";
            if (questState.quest_id == "")
                return "failed_quests: entry has an empty quest_id";
            if (!seenQuestIds.Add(questState.quest_id))
                return $"failed_quests: '{questState.quest_id}' duplicates another quest entry";
            if (questState.status_id != QuestState.ToStringName(QuestStatusKind.Failed))
                return $"failed_quests: '{questState.quest_id}' has status '{questState.status_id}'";
            if (!partyState.SetFailedQuestState(questState))
                return $"failed_quests: '{questState.quest_id}' was rejected";
        }

        var parsedCompletedQuestIds = _parse_completed_quest_ids(
            data["completed_quest_ids"].AsGodotArray()
        );
        if (parsedCompletedQuestIds == null)
            return "completed_quest_ids: not a unique string-name array";
        foreach (StringName questId in parsedCompletedQuestIds)
        {
            if (!seenQuestIds.Add(questId))
                return $"completed_quest_ids: '{questId}' duplicates another quest entry";
            if (!partyState.AddCompletedQuestId(questId))
                return $"completed_quest_ids: '{questId}' was rejected";
        }
        if (partyState.main_character_member_id == "")
            return "main_character_member_id: must not be empty";
        if (!partyState.HasMemberState(partyState.main_character_member_id))
            return $"main_character_member_id: '{partyState.main_character_member_id}' has no member state";

        result = partyState;
        return "";
    }

    private static Dictionary<StringName, bool> DuplicateBoolMap(
        Dictionary<StringName, bool> values
    )
    {
        return values != null ? new Dictionary<StringName, bool>(values) : new Dictionary<StringName, bool>();
    }

    private static List<PendingCharacterReward> DuplicatePendingCharacterRewards(
        IEnumerable<PendingCharacterReward> values
    )
    {
        var result = new List<PendingCharacterReward>();
        if (values == null)
            return result;
        foreach (var reward in values)
            if (reward != null)
                result.Add(reward.DuplicateState());
        return result;
    }

    private static bool _has_exact_fields(
        Godot.Collections.Dictionary data,
        IReadOnlyCollection<string> expectedFields
    )
    {
        if (data.Count != expectedFields.Count)
            return false;
        foreach (var fieldName in expectedFields)
        {
            if (!data.ContainsKey(fieldName))
                return false;
        }
        return true;
    }

    private static StringName _parse_required_string_name(object rawValue, out bool ok)
    {
        ok = false;
        if (rawValue is Variant value)
        {
            if (
                value.VariantType != Variant.Type.String
                && value.VariantType != Variant.Type.StringName
            )
                return new StringName("");
        }
        else if (rawValue is not string && rawValue is not StringName)
        {
            return new StringName("");
        }

        var parsed = ProgressionDataUtils.to_string_name(rawValue);
        if (parsed == "")
            return new StringName("");

        ok = true;
        return parsed;
    }

    private static StringNameList _parse_unique_string_name_array(
        Godot.Collections.Array values
    )
    {
        var parsedValues = new StringNameList();
        var seenValues = new HashSet<StringName>();
        foreach (var rawValue in values)
        {
            var parsedValue = ProgressionDataUtils.to_string_name(rawValue);
            if (parsedValue == "" || !seenValues.Add(parsedValue))
                return null;
            parsedValues.Add(parsedValue);
        }
        return parsedValues;
    }

    private static StringNameList _parse_completed_quest_ids(
        Godot.Collections.Array values
    ) => _parse_unique_string_name_array(values);

    private static Dictionary<StringName, bool> _parse_boolean_flag_dict(
        Godot.Collections.Dictionary values
    )
    {
        var parsedFlags = new Dictionary<StringName, bool>();
        foreach (var rawKey in values.Keys)
        {
            var flagId = ProgressionDataUtils.to_string_name(rawKey);
            if (flagId == "" || parsedFlags.ContainsKey(flagId))
                return null;
            if (!TryReadBool(values, rawKey, out bool flagValue))
                return null;
            parsedFlags[flagId] = flagValue;
        }
        return parsedFlags;
    }

    private static bool TryReadBool(
        Godot.Collections.Dictionary data,
        Variant key,
        out bool result
    )
    {
        result = false;
        if (data == null || !data.ContainsKey(key))
            return false;
        Variant value = data[key];
        if (value.VariantType != Variant.Type.Bool)
            return false;
        result = value.AsBool();
        return true;
    }

    private static bool _has_unique_equipment_instance_ids(PartyState partyState)
    {
        if (partyState == null)
            return false;

        var seenInstanceIds = new HashSet<StringName>();
        if (partyState.warehouse_state != null)
        {
            foreach (var instance in partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped())
            {
                if (instance == null)
                    continue;
                var instanceId = ProgressionDataUtils.to_string_name(instance.instance_id);
                if (instanceId == "")
                    continue;
                if (!seenInstanceIds.Add(instanceId))
                    return false;
            }
        }

        foreach (PartyMemberState memberState in partyState.GetMemberStates())
        {
            var equipmentState = memberState?.equipment_state;
            if (equipmentState == null)
                continue;

            foreach (var entrySlotId in equipmentState.GetEntrySlotIdsTyped())
            {
                var instanceId = ProgressionDataUtils.to_string_name(
                    equipmentState.GetEquippedInstanceId(entrySlotId)
                );
                if (instanceId == "")
                    continue;
                if (!seenInstanceIds.Add(instanceId))
                    return false;
            }
        }
        return true;
    }

}
