using Godot;

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
    public Godot.Collections.Dictionary fate_run_flags = new(),
        meta_flags = new();
    public Godot.Collections.Array active_member_ids = new(),
        reserve_member_ids = new();
    public Godot.Collections.Dictionary member_states = new();
    public Godot.Collections.Array<PendingCharacterReward> pending_character_rewards = new();
    public Godot.Collections.Array<QuestState> active_quests = new(),
        claimable_quests = new();
    public Godot.Collections.Array<StringName> completed_quest_ids = new();
    public WarehouseState warehouse_state = new WarehouseState();

    public PartyMemberState get_member_state(StringName id) =>
        member_states.ContainsKey(id)
            ? member_states[id].AsGodotObject() as PartyMemberState
            : null;

    public bool has_member_state(StringName id) => get_member_state(id) != null;

    public bool is_member_dead(StringName id)
    {
        var m = get_member_state(id);
        return m != null && m.is_dead;
    }

    public StringName get_resolved_main_character_member_id() =>
        main_character_member_id != "" && has_member_state(main_character_member_id)
            ? main_character_member_id
            : new StringName("");

    public Godot.Collections.Dictionary get_fate_run_flags() =>
        _normalize_fate_run_flags(Variant.From(fate_run_flags));

    public void set_fate_run_flags(Godot.Collections.Dictionary v) =>
        fate_run_flags = _normalize_fate_run_flags(Variant.From(v));

    public bool get_fate_run_flag(StringName id, bool defVal = false)
    {
        if (id == "")
            return defVal;
        var n = _normalize_fate_run_flags(Variant.From(fate_run_flags));
        return n.ContainsKey(id) && (bool)n[id];
    }

    public bool has_fate_run_flag(StringName id) => get_fate_run_flag(id);

    public void set_fate_run_flag(StringName id, bool en = true)
    {
        if (id != "")
            fate_run_flags[id] = en;
    }

    public void clear_fate_run_flag(StringName id)
    {
        if (id != "")
            fate_run_flags.Remove(id);
    }

    public Godot.Collections.Dictionary get_meta_flags() =>
        _normalize_meta_flags(Variant.From(meta_flags));

    public void set_meta_flags(Godot.Collections.Dictionary v) =>
        meta_flags = _normalize_meta_flags(Variant.From(v));

    public bool get_meta_flag(StringName id, bool defVal = false)
    {
        if (id == "")
            return defVal;
        var n = _normalize_meta_flags(Variant.From(meta_flags));
        return n.ContainsKey(id) && (bool)n[id];
    }

    public bool has_meta_flag(StringName id) => get_meta_flag(id);

    public void set_meta_flag(StringName id, bool en = true)
    {
        if (id != "")
            meta_flags[id] = en;
    }

    public void clear_meta_flag(StringName id)
    {
        if (id != "")
            meta_flags.Remove(id);
    }

    public void remove_member_from_rosters(StringName id)
    {
        if (id == "")
            return;
        active_member_ids = Variant
            .From(ProgressionDataUtils.to_string_name_array(Variant.From(active_member_ids)))
            .AsGodotArray();
        reserve_member_ids = Variant
            .From(ProgressionDataUtils.to_string_name_array(Variant.From(reserve_member_ids)))
            .AsGodotArray();
        active_member_ids.Remove(id);
        reserve_member_ids.Remove(id);
        if (leader_member_id == id)
            leader_member_id =
                active_member_ids.Count > 0
                    ? active_member_ids[0].AsStringName()
                    : new StringName("");
    }

    public int get_gold() => Mathf.Max(gold, 0);

    public void set_gold(int v) => gold = Mathf.Max(v, 0);

    public int add_gold(int a)
    {
        set_gold(get_gold() + a);
        return gold;
    }

    public bool can_afford(int amount) => get_gold() >= Mathf.Max(amount, 0);

    public bool spend_gold(int amount)
    {
        int cost = Mathf.Max(amount, 0);
        if (cost == 0)
            return true;
        if (!can_afford(cost))
            return false;
        set_gold(get_gold() - cost);
        return true;
    }

    public void set_member_state(PartyMemberState ms)
    {
        if (ms != null && ms.member_id != "")
            member_states[ms.member_id] = ms;
    }

    public void remove_member_state(StringName id) => member_states.Remove(id);

    public void enqueue_pending_character_reward(PendingCharacterReward r)
    {
        if (r != null && !r.is_empty())
            pending_character_rewards.Add(r);
    }

    public PendingCharacterReward get_pending_character_reward(StringName rid)
    {
        foreach (var r in pending_character_rewards)
            if (r != null && r.reward_id == rid)
                return r;
        return null;
    }

    public PendingCharacterReward get_next_pending_character_reward() =>
        pending_character_rewards.Count > 0 ? pending_character_rewards[0] : null;

    public bool remove_pending_character_reward(StringName rid)
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

    public QuestState get_active_quest_state(StringName qid)
    {
        foreach (var q in active_quests)
            if (q != null && q.quest_id == qid)
                return q;
        return null;
    }

    public bool has_active_quest(StringName qid) => get_active_quest_state(qid) != null;

    public QuestState get_claimable_quest_state(StringName qid)
    {
        foreach (var q in claimable_quests)
            if (q != null && q.quest_id == qid)
                return q;
        return null;
    }

    public bool has_claimable_quest(StringName qid) => get_claimable_quest_state(qid) != null;

    public void set_active_quest_state(QuestState q)
    {
        if (q == null || q.quest_id == "")
            return;
        remove_claimable_quest(q.quest_id);
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

    public void set_claimable_quest_state(QuestState q)
    {
        if (q == null || q.quest_id == "")
            return;
        remove_active_quest(q.quest_id);
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

    public bool remove_active_quest(StringName qid)
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

    public bool remove_claimable_quest(StringName qid)
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

    public Godot.Collections.Array<StringName> get_active_quest_ids()
    {
        var r = new Godot.Collections.Array<StringName>();
        foreach (var q in active_quests)
            if (q != null && q.quest_id != "")
                r.Add(q.quest_id);
        return r;
    }

    public Godot.Collections.Array<StringName> get_claimable_quest_ids()
    {
        var r = new Godot.Collections.Array<StringName>();
        foreach (var q in claimable_quests)
            if (q != null && q.quest_id != "")
                r.Add(q.quest_id);
        return r;
    }

    public bool has_completed_quest(StringName qid) => completed_quest_ids.Contains(qid);

    public void add_completed_quest_id(StringName qid)
    {
        if (qid == "" || completed_quest_ids.Contains(qid))
            return;
        remove_active_quest(qid);
        remove_claimable_quest(qid);
        completed_quest_ids.Add(qid);
    }

    public bool mark_quest_claimable(StringName qid, int ws = -1)
    {
        var q = get_active_quest_state(qid);
        if (q == null)
            return false;
        q.mark_completed(ws);
        remove_active_quest(qid);
        set_claimable_quest_state(q);
        return true;
    }

    public bool mark_quest_completed(StringName qid, int ws = -1) => mark_quest_claimable(qid, ws);

    public bool mark_quest_reward_claimed(StringName qid, int ws = -1)
    {
        var q = get_claimable_quest_state(qid);
        if (q == null)
            return false;
        q.mark_reward_claimed(ws);
        remove_claimable_quest(qid);
        add_completed_quest_id(qid);
        return true;
    }

    public Godot.Collections.Dictionary to_dict()
    {
        var msd = new Godot.Collections.Dictionary();
        foreach (var k in ProgressionDataUtils.sorted_string_keys(member_states))
        {
            var m = get_member_state(new StringName(k));
            if (m != null)
                msd[k] = m.to_dict();
        }
        var prd = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var r in pending_character_rewards)
            if (r != null)
                prd.Add(r.to_dict());
        var aqd = _serialize_quest_state_array(active_quests);
        var cqd = _serialize_quest_state_array(claimable_quests);
        return new Godot.Collections.Dictionary
        {
            { "version", version },
            { "gold", get_gold() },
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
            { "warehouse_state", warehouse_state?.to_dict() ?? new Godot.Collections.Dictionary() },
        };
    }

    public static PartyState from_dict(Godot.Collections.Dictionary data)
    {
        return null; /* complex deserialization — linter expansion pending */
    }

    private static Godot.Collections.Array<Godot.Collections.Dictionary> _serialize_quest_state_array(
        Godot.Collections.Array<QuestState> qs
    )
    {
        var e = new System.Collections.Generic.List<(string, Godot.Collections.Dictionary)>();
        foreach (var q in qs)
            if (q != null && q.quest_id != "")
                e.Add(((string)q.quest_id, q.to_dict()));
        e.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
        var r = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var (_, d) in e)
            r.Add(d.Duplicate(true));
        return r;
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

    private static Godot.Collections.Dictionary _normalize_fate_run_flags(Variant v)
    {
        var r = new Godot.Collections.Dictionary();
        if (v.VariantType != Variant.Type.Dictionary)
            return r;
        foreach (var rk in v.AsGodotDictionary().Keys)
        {
            var fid = ProgressionDataUtils.to_string_name(rk);
            if (fid != "")
                r[fid] = v.AsGodotDictionary()[rk].AsBool();
        }
        return r;
    }

    private static Godot.Collections.Dictionary _normalize_meta_flags(Variant v)
    {
        var r = new Godot.Collections.Dictionary();
        if (v.VariantType != Variant.Type.Dictionary)
            return r;
        foreach (var rk in v.AsGodotDictionary().Keys)
        {
            var fid = ProgressionDataUtils.to_string_name(rk);
            if (fid != "")
                r[fid] = v.AsGodotDictionary()[rk].AsBool();
        }
        return r;
    }

    private static Godot.Collections.Dictionary _serialize_flags(Godot.Collections.Dictionary v)
    {
        var r = new Godot.Collections.Dictionary();
        foreach (var k in ProgressionDataUtils.sorted_string_keys(v))
        {
            var fid = ProgressionDataUtils.to_string_name(k);
            if (fid != "")
                r[(string)fid] = v.ContainsKey(fid)
                    ? v[fid].AsBool()
                    : (v.ContainsKey(k) ? v[k].AsBool() : false);
        }
        return r;
    }
}

