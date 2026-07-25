using Godot;
using System;
using System.Collections.Generic;

public partial class PartyState
{
    private static readonly string[] TO_DICT_FIELDS =
    {
        "version",
        "gold",
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

    public int version = 8;
    public int gold;
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

    public PartyState DuplicateState()
    {
        return new PartyState
        {
            version = version,
            gold = gold,
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

    public static PartyState FromDictionary(Godot.Collections.Dictionary data)
    {
        if (data.Count == 0)
            return null;
        if (!_has_exact_fields(data, TO_DICT_FIELDS))
            return null;
        if (data["version"].VariantType != Variant.Type.Int || data["version"].AsInt32() != 8)
            return null;
        if (data["warehouse_state"].VariantType != Variant.Type.Dictionary)
            return null;
        if (data["member_states"].VariantType != Variant.Type.Dictionary)
            return null;
        if (data["pending_character_rewards"].VariantType != Variant.Type.Array)
            return null;
        if (data["active_quests"].VariantType != Variant.Type.Array)
            return null;
        if (data["claimable_quests"].VariantType != Variant.Type.Array)
            return null;
        if (data["failed_quests"].VariantType != Variant.Type.Array)
            return null;
        if (data["completed_quest_ids"].VariantType != Variant.Type.Array)
            return null;
        if (data["fate_run_flags"].VariantType != Variant.Type.Dictionary)
            return null;
        if (data["meta_flags"].VariantType != Variant.Type.Dictionary)
            return null;
        if (data["gold"].VariantType != Variant.Type.Int || data["gold"].AsInt32() < 0)
            return null;
        if (data["active_member_ids"].VariantType != Variant.Type.Array)
            return null;
        if (data["reserve_member_ids"].VariantType != Variant.Type.Array)
            return null;

        var leaderMemberId = _parse_required_string_name(
            data["leader_member_id"],
            out bool leaderOk
        );
        if (!leaderOk)
            return null;
        var mainCharacterMemberId = _parse_required_string_name(
            data["main_character_member_id"],
            out bool mainOk
        );
        if (!mainOk)
            return null;

        var parsedFateRunFlags = _parse_boolean_flag_dict(
            data["fate_run_flags"].AsGodotDictionary()
        );
        if (parsedFateRunFlags == null)
            return null;
        var parsedMetaFlags = _parse_boolean_flag_dict(data["meta_flags"].AsGodotDictionary());
        if (parsedMetaFlags == null)
            return null;
        var parsedActiveMemberIds = _parse_unique_string_name_array(
            data["active_member_ids"].AsGodotArray()
        );
        if (parsedActiveMemberIds == null)
            return null;
        var parsedReserveMemberIds = _parse_unique_string_name_array(
            data["reserve_member_ids"].AsGodotArray()
        );
        if (parsedReserveMemberIds == null)
            return null;

        var warehouseState = WarehouseState.FromDictionary(
            data["warehouse_state"].AsGodotDictionary()
        );
        if (warehouseState == null)
            return null;

        PartyMemberStateCollection parsedMemberStates;
        try
        {
            parsedMemberStates = PartyMemberStateCollection.FromSaveDictionary(
                data["member_states"].AsGodotDictionary()
            );
        }
        catch (ArgumentException)
        {
            return null;
        }

        var partyState = new PartyState
        {
            version = data["version"].AsInt32(),
            gold = data["gold"].AsInt32(),
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
            return null;
        if (
            partyState.leader_member_id == ""
            || !partyState.HasMemberState(partyState.leader_member_id)
        )
            return null;

        var rosterSeenIds = new HashSet<StringName>();
        foreach (var memberId in partyState.active_member_ids)
        {
            if (!partyState.HasMemberState(memberId))
                return null;
            rosterSeenIds.Add(memberId);
        }
        foreach (var memberId in partyState.reserve_member_ids)
        {
            if (!rosterSeenIds.Add(memberId) || !partyState.HasMemberState(memberId))
                return null;
        }

        foreach (var rewardValue in data["pending_character_rewards"].AsGodotArray())
        {
            if (rewardValue.VariantType != Variant.Type.Dictionary)
                return null;
            var reward = PendingCharacterRewardPayload.ReadSavePayload(
                rewardValue.AsGodotDictionary()
            );
            if (reward == null || reward.IsEmpty())
                return null;
            partyState.pending_character_rewards.Add(reward);
        }

        var seenQuestIds = new HashSet<StringName>();
        foreach (var questValue in data["active_quests"].AsGodotArray())
        {
            if (questValue.VariantType != Variant.Type.Dictionary)
                return null;
            var questState = QuestState.FromDictionary(questValue.AsGodotDictionary());
            if (
                questState == null
                || questState.quest_id == ""
                || !seenQuestIds.Add(questState.quest_id)
            )
                return null;
            if (questState.status_id != QuestState.ToStringName(QuestStatusKind.Active))
                return null;
            if (!partyState.SetActiveQuestState(questState))
                return null;
        }

        foreach (var questValue in data["claimable_quests"].AsGodotArray())
        {
            if (questValue.VariantType != Variant.Type.Dictionary)
                return null;
            var questState = QuestState.FromDictionary(questValue.AsGodotDictionary());
            if (
                questState == null
                || questState.quest_id == ""
                || !seenQuestIds.Add(questState.quest_id)
            )
                return null;
            if (questState.status_id != QuestState.ToStringName(QuestStatusKind.Completed))
                return null;
            if (!partyState.SetClaimableQuestState(questState))
                return null;
        }

        foreach (var questValue in data["failed_quests"].AsGodotArray())
        {
            if (questValue.VariantType != Variant.Type.Dictionary)
                return null;
            var questState = QuestState.FromDictionary(questValue.AsGodotDictionary());
            if (
                questState == null
                || questState.quest_id == ""
                || !seenQuestIds.Add(questState.quest_id)
            )
                return null;
            if (questState.status_id != QuestState.ToStringName(QuestStatusKind.Failed))
                return null;
            if (!partyState.SetFailedQuestState(questState))
                return null;
        }

        var parsedCompletedQuestIds = _parse_completed_quest_ids(
            data["completed_quest_ids"].AsGodotArray()
        );
        if (parsedCompletedQuestIds == null)
            return null;
        foreach (StringName questId in parsedCompletedQuestIds)
        {
            if (!seenQuestIds.Add(questId) || !partyState.AddCompletedQuestId(questId))
                return null;
        }
        if (
            partyState.main_character_member_id == ""
            || !partyState.HasMemberState(partyState.main_character_member_id)
        )
            return null;

        return partyState;
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
