using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class PartyState : RefCounted
{
    private static readonly Godot.Collections.Array<string> TO_DICT_FIELDS = new()
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
        "completed_quest_ids",
        "warehouse_state",
    };

    public int version = 3;
    public int gold;
    public StringName leader_member_id = "",
        main_character_member_id = "";
    public Dictionary<StringName, bool> fate_run_flags { get; private set; } = new();
    public Dictionary<StringName, bool> meta_flags { get; private set; } = new();
    public Godot.Collections.Array<StringName> active_member_ids = new(),
        reserve_member_ids = new();
    public Godot.Collections.Dictionary member_states = new();
    public Godot.Collections.Array<PendingCharacterReward> pending_character_rewards = new();
    public Godot.Collections.Array<QuestState> active_quests = new(),
        claimable_quests = new();
    public Godot.Collections.Array<StringName> completed_quest_ids = new();
    public WarehouseState warehouse_state = new WarehouseState();

    public PartyMemberState GetMemberState(StringName id)
    {
        StringName normalizedId = ProgressionDataUtils.to_string_name(id);
        if (normalizedId == "")
            return null;

        if (member_states.ContainsKey(normalizedId))
            return member_states[normalizedId].AsGodotObject() as PartyMemberState;

        foreach (Variant key in member_states.Keys)
        {
            if (ProgressionDataUtils.to_string_name(key) != normalizedId)
                continue;
            return member_states[key].AsGodotObject() as PartyMemberState;
        }

        return null;
    }

    public bool HasMemberState(StringName id) => GetMemberState(id) != null;

    public List<PartyMemberState> GetMemberStates()
    {
        var result = new List<PartyMemberState>();
        foreach (string key in ProgressionDataUtils.sorted_string_keys(member_states))
        {
            PartyMemberState memberState = GetMemberState(new StringName(key));
            if (memberState != null)
            {
                result.Add(memberState);
            }
        }
        return result;
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
        active_member_ids = ProgressionDataUtils.to_string_name_array(
            Variant.From(active_member_ids)
        );
        reserve_member_ids = ProgressionDataUtils.to_string_name_array(
            Variant.From(reserve_member_ids)
        );
        active_member_ids.Remove(id);
        reserve_member_ids.Remove(id);
        if (leader_member_id == id)
            leader_member_id =
                active_member_ids.Count > 0 ? active_member_ids[0] : new StringName("");
    }

    public List<QuestState> GetActiveQuestsTyped() => new(active_quests);

    public List<QuestState> GetClaimableQuestsTyped() => new(claimable_quests);

    public List<StringName> GetCompletedQuestIdsTyped() => new(completed_quest_ids);

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
            active_member_ids = DuplicateStringNameArray(active_member_ids),
            reserve_member_ids = DuplicateStringNameArray(reserve_member_ids),
            member_states = DuplicateMemberStates(member_states),
            pending_character_rewards = DuplicatePendingCharacterRewards(pending_character_rewards),
            active_quests = DuplicateQuestStates(active_quests),
            claimable_quests = DuplicateQuestStates(claimable_quests),
            completed_quest_ids = DuplicateStringNameArray(completed_quest_ids),
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
            member_states[ms.member_id] = ms;
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
        foreach (var q in active_quests)
            if (q != null && q.quest_id == qid)
                return q;
        return null;
    }

    public bool HasActiveQuest(StringName qid) => GetActiveQuestState(qid) != null;

    public QuestState GetClaimableQuestState(StringName qid)
    {
        foreach (var q in claimable_quests)
            if (q != null && q.quest_id == qid)
                return q;
        return null;
    }

    public bool HasClaimableQuest(StringName qid) => GetClaimableQuestState(qid) != null;

    public QuestState GetQuestState(StringName qid)
    {
        var activeQuest = GetActiveQuestState(qid);
        if (activeQuest != null)
            return activeQuest;
        return GetClaimableQuestState(qid);
    }

    public void SetQuestState(StringName qid, QuestState q)
    {
        if (q == null)
            return;
        if (q.quest_id == "")
            q.quest_id = qid;
        if (q.quest_id == "")
            return;
        if (q.status_id == QuestState.ToStringName(QuestStatusKind.Completed))
            SetClaimableQuestState(q);
        else if (q.status_id == QuestState.ToStringName(QuestStatusKind.Rewarded))
            AddCompletedQuestId(q.quest_id);
        else
            SetActiveQuestState(q);
    }

    public void SetActiveQuestState(QuestState q)
    {
        if (q == null || q.quest_id == "")
            return;
        RemoveClaimableQuest(q.quest_id);
        completed_quest_ids.Remove(q.quest_id);
        for (int i = 0; i < active_quests.Count; i++)
        {
            if (active_quests[i] != null && active_quests[i].quest_id == q.quest_id)
            {
                active_quests[i] = q;
                return;
            }
        }
        active_quests.Add(q);
    }

    public void SetClaimableQuestState(QuestState q)
    {
        if (q == null || q.quest_id == "")
            return;
        RemoveActiveQuest(q.quest_id);
        completed_quest_ids.Remove(q.quest_id);
        for (int i = 0; i < claimable_quests.Count; i++)
        {
            if (claimable_quests[i] != null && claimable_quests[i].quest_id == q.quest_id)
            {
                claimable_quests[i] = q;
                return;
            }
        }
        claimable_quests.Add(q);
    }

    public bool RemoveActiveQuest(StringName qid)
    {
        for (int i = 0; i < active_quests.Count; i++)
        {
            if (active_quests[i] != null && active_quests[i].quest_id == qid)
            {
                active_quests.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public bool RemoveClaimableQuest(StringName qid)
    {
        for (int i = 0; i < claimable_quests.Count; i++)
        {
            if (claimable_quests[i] != null && claimable_quests[i].quest_id == qid)
            {
                claimable_quests.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public List<StringName> GetActiveQuestIdsTyped()
    {
        var r = new List<StringName>();
        foreach (var q in active_quests)
            if (q != null && q.quest_id != "")
                r.Add(q.quest_id);
        return r;
    }

    public List<StringName> GetClaimableQuestIdsTyped()
    {
        var r = new List<StringName>();
        foreach (var q in claimable_quests)
            if (q != null && q.quest_id != "")
                r.Add(q.quest_id);
        return r;
    }

    public bool HasCompletedQuest(StringName qid) => completed_quest_ids.Contains(qid);

    public void AddCompletedQuestId(StringName qid)
    {
        if (qid == "" || completed_quest_ids.Contains(qid))
            return;
        RemoveActiveQuest(qid);
        RemoveClaimableQuest(qid);
        completed_quest_ids.Add(qid);
    }

    public bool MarkQuestClaimable(StringName qid, int ws = -1)
    {
        var q = GetActiveQuestState(qid);
        if (q == null)
            return false;
        q.MarkCompleted(ws);
        RemoveActiveQuest(qid);
        SetClaimableQuestState(q);
        return true;
    }

    public bool MarkQuestCompleted(StringName qid, int ws = -1) => MarkQuestClaimable(qid, ws);

    public bool MarkQuestRewardClaimed(StringName qid, int ws = -1)
    {
        var q = GetClaimableQuestState(qid);
        if (q == null)
            return false;
        q.MarkRewardClaimed(ws);
        RemoveClaimableQuest(qid);
        AddCompletedQuestId(qid);
        return true;
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        var msd = new Godot.Collections.Dictionary();
        foreach (var k in ProgressionDataUtils.sorted_string_keys(member_states))
        {
            var m = GetMemberState(new StringName(k));
            if (m != null)
                msd[k] = m.ToDictionary();
        }
        var prd = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var r in pending_character_rewards)
            if (r != null)
                prd.Add(PendingCharacterRewardPayload.Project(r));
        var aqd = _serialize_quest_state_array(active_quests);
        var cqd = _serialize_quest_state_array(claimable_quests);
        return new Godot.Collections.Dictionary
        {
            { "version", version },
            { "gold", GetGold() },
            { "leader_member_id", (string)leader_member_id },
            { "main_character_member_id", (string)main_character_member_id },
            { "fate_run_flags", _serialize_flags(fate_run_flags) },
            { "meta_flags", _serialize_flags(meta_flags) },
            {
                "active_member_ids",
                ProgressionDataUtils.string_name_array_to_string_array(
                    ProgressionDataUtils.to_string_name_array(Variant.From(active_member_ids))
                )
            },
            {
                "reserve_member_ids",
                ProgressionDataUtils.string_name_array_to_string_array(
                    ProgressionDataUtils.to_string_name_array(Variant.From(reserve_member_ids))
                )
            },
            { "member_states", msd },
            { "pending_character_rewards", prd },
            { "active_quests", aqd },
            { "claimable_quests", cqd },
            {
                "completed_quest_ids",
                ProgressionDataUtils.string_name_array_to_string_array(_nusna(completed_quest_ids))
            },
            {
                "warehouse_state",
                warehouse_state?.ToDictionary() ?? new Godot.Collections.Dictionary()
            },
        };
    }

    public static PartyState FromDictionary(Godot.Collections.Dictionary data)
    {
        if (data.Count == 0)
            return null;
        if (!_has_exact_fields(data, TO_DICT_FIELDS))
            return null;
        if (data["version"].VariantType != Variant.Type.Int || data["version"].AsInt32() != 3)
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
            warehouse_state = warehouseState,
        };

        var memberStatesData = data["member_states"].AsGodotDictionary();
        foreach (var key in memberStatesData.Keys)
        {
            var serializedMemberId = ProgressionDataUtils.to_string_name(key);
            if (serializedMemberId == "")
                return null;

            var memberStatePayload = memberStatesData[key];
            if (memberStatePayload.VariantType != Variant.Type.Dictionary)
                return null;

            var memberState = PartyMemberState.FromDictionary(memberStatePayload.AsGodotDictionary());
            if (memberState == null || memberState.member_id != serializedMemberId)
                return null;
            if (partyState.member_states.ContainsKey(memberState.member_id))
                return null;
            partyState.member_states[memberState.member_id] = memberState;
        }

        if (!_has_unique_equipment_instance_ids(partyState))
            return null;
        if (
            partyState.leader_member_id == ""
            || !partyState.HasMemberState(partyState.leader_member_id)
        )
            return null;

        var rosterSeenIds = new Godot.Collections.Dictionary();
        foreach (var memberId in partyState.active_member_ids)
        {
            if (!partyState.HasMemberState(memberId))
                return null;
            rosterSeenIds[memberId] = true;
        }
        foreach (var memberId in partyState.reserve_member_ids)
        {
            if (rosterSeenIds.ContainsKey(memberId) || !partyState.HasMemberState(memberId))
                return null;
            rosterSeenIds[memberId] = true;
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

        foreach (var questValue in data["active_quests"].AsGodotArray())
        {
            if (questValue.VariantType != Variant.Type.Dictionary)
                return null;
            var questState = QuestState.FromDictionary(questValue.AsGodotDictionary());
            if (
                questState == null
                || questState.quest_id == ""
                || partyState.HasActiveQuest(questState.quest_id)
            )
                return null;
            if (questState.status_id != QuestState.ToStringName(QuestStatusKind.Active))
                return null;
            partyState.active_quests.Add(questState);
        }

        foreach (var questValue in data["claimable_quests"].AsGodotArray())
        {
            if (questValue.VariantType != Variant.Type.Dictionary)
                return null;
            var questState = QuestState.FromDictionary(questValue.AsGodotDictionary());
            if (
                questState == null
                || questState.quest_id == ""
                || partyState.HasClaimableQuest(questState.quest_id)
            )
                return null;
            if (questState.status_id != QuestState.ToStringName(QuestStatusKind.Completed))
                return null;
            partyState.claimable_quests.Add(questState);
        }

        var parsedCompletedQuestIds = _parse_completed_quest_ids(
            data["completed_quest_ids"].AsGodotArray()
        );
        if (parsedCompletedQuestIds == null)
            return null;
        partyState.completed_quest_ids = parsedCompletedQuestIds;

        var activeQuestIds = partyState.GetActiveQuestIdsTyped();
        var claimableQuestIds = partyState.GetClaimableQuestIdsTyped();
        foreach (var questId in activeQuestIds)
        {
            if (
                claimableQuestIds.Contains(questId)
                || partyState.completed_quest_ids.Contains(questId)
            )
                return null;
        }
        foreach (var questId in claimableQuestIds)
        {
            if (partyState.completed_quest_ids.Contains(questId))
                return null;
        }
        if (
            partyState.main_character_member_id == ""
            || !partyState.HasMemberState(partyState.main_character_member_id)
        )
            return null;

        return partyState;
    }

    private static Godot.Collections.Array<Godot.Collections.Dictionary> _serialize_quest_state_array(
        Godot.Collections.Array<QuestState> qs
    )
    {
        var e = new System.Collections.Generic.List<(string, Godot.Collections.Dictionary)>();
        foreach (var q in qs)
            if (q != null && q.quest_id != "")
                e.Add(((string)q.quest_id, q.ToDictionary()));
        e.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
        var r = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var (_, d) in e)
            r.Add(d.Duplicate(true));
        return r;
    }

    private static Dictionary<StringName, bool> DuplicateBoolMap(
        Dictionary<StringName, bool> values
    )
    {
        return values != null ? new Dictionary<StringName, bool>(values) : new Dictionary<StringName, bool>();
    }

    private static Godot.Collections.Array<StringName> DuplicateStringNameArray(
        Godot.Collections.Array<StringName> values
    )
    {
        return values != null
            ? new Godot.Collections.Array<StringName>(values)
            : new Godot.Collections.Array<StringName>();
    }

    private static Godot.Collections.Dictionary DuplicateMemberStates(
        Godot.Collections.Dictionary values
    )
    {
        var result = new Godot.Collections.Dictionary();
        if (values == null)
            return result;

        foreach (var rawKey in values.Keys)
        {
            var memberId = ProgressionDataUtils.to_string_name(rawKey);
            if (memberId == "")
                continue;
            var memberState = values[rawKey].AsGodotObject() as PartyMemberState;
            var duplicate = memberState?.DuplicateState();
            if (duplicate != null)
                result[memberId] = duplicate;
        }
        return result;
    }

    private static Godot.Collections.Array<PendingCharacterReward> DuplicatePendingCharacterRewards(
        Godot.Collections.Array<PendingCharacterReward> values
    )
    {
        var result = new Godot.Collections.Array<PendingCharacterReward>();
        if (values == null)
            return result;
        foreach (var reward in values)
            if (reward != null)
                result.Add(reward.DuplicateState());
        return result;
    }

    private static Godot.Collections.Array<QuestState> DuplicateQuestStates(
        Godot.Collections.Array<QuestState> values
    )
    {
        var result = new Godot.Collections.Array<QuestState>();
        if (values == null)
            return result;
        foreach (var questState in values)
            if (questState != null)
                result.Add(questState.DuplicateState());
        return result;
    }

    private static Godot.Collections.Array<StringName> _nusna(Godot.Collections.Array<StringName> v)
    {
        var r = new Godot.Collections.Array<StringName>();
        var s = new Godot.Collections.Dictionary();
        foreach (var n in v)
        {
            if (n != "" && !s.ContainsKey(n))
            {
                s[n] = true;
                r.Add(n);
            }
        }
        return r;
    }

    private static bool _has_exact_fields(
        Godot.Collections.Dictionary data,
        Godot.Collections.Array<string> expectedFields
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

    private static Godot.Collections.Array<StringName> _parse_unique_string_name_array(
        Godot.Collections.Array values
    )
    {
        var parsedValues = new Godot.Collections.Array<StringName>();
        var seenValues = new Godot.Collections.Dictionary();
        foreach (var rawValue in values)
        {
            var parsedValue = ProgressionDataUtils.to_string_name(rawValue);
            if (parsedValue == "" || seenValues.ContainsKey(parsedValue))
                return null;
            seenValues[parsedValue] = true;
            parsedValues.Add(parsedValue);
        }
        return parsedValues;
    }

    private static Godot.Collections.Array<StringName> _parse_completed_quest_ids(
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

        var seenInstanceIds = new Godot.Collections.Dictionary();
        if (partyState.warehouse_state != null)
        {
            foreach (var instance in partyState.warehouse_state.GetNonEmptyEquipmentInstancesTyped())
            {
                if (instance == null)
                    continue;
                var instanceId = ProgressionDataUtils.to_string_name(instance.instance_id);
                if (instanceId == "")
                    continue;
                if (seenInstanceIds.ContainsKey(instanceId))
                    return false;
                seenInstanceIds[instanceId] = true;
            }
        }

        foreach (var memberValue in partyState.member_states.Values)
        {
            var memberState = memberValue.AsGodotObject() as PartyMemberState;
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
                if (seenInstanceIds.ContainsKey(instanceId))
                    return false;
                seenInstanceIds[instanceId] = true;
            }
        }
        return true;
    }

    private static Godot.Collections.Dictionary _serialize_flags(Dictionary<StringName, bool> values)
    {
        var r = new Godot.Collections.Dictionary();
        var sorted = new List<StringName>(values.Keys);
        sorted.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
        foreach (var fid in sorted)
        {
            if (fid != "")
                r[(string)fid] = values[fid];
        }
        return r;
    }
}
