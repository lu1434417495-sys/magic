using Godot;

[GlobalClass]
public partial class QuestProgressService : RefCounted
{
    private static readonly GDScript PartyStateScript = GD.Load<GDScript>("res://scripts/player/progression/party_state.gd");
    public static readonly StringName EVENT_ACCEPT = "accept", EVENT_PROGRESS = "progress", EVENT_COMPLETE = "complete";

    private GodotObject _party_state;
    private Godot.Collections.Dictionary _quest_defs = new();

    public QuestProgressService() { _party_state = PartyStateScript.Call("new").AsGodotObject(); }

    public void setup(GodotObject partyState, Godot.Collections.Dictionary questDefs = null) { _party_state = partyState ?? PartyStateScript.Call("new").AsGodotObject(); _quest_defs = questDefs ?? new Godot.Collections.Dictionary(); }
    public void set_party_state(GodotObject partyState, Godot.Collections.Dictionary questDefs = null) => setup(partyState, questDefs ?? _quest_defs);
    public GodotObject get_party_state() => _party_state;
    public Godot.Collections.Dictionary get_quest_defs() => _quest_defs;

    public Godot.Collections.Array<QuestState> get_active_quests() => _party_state?.Call("get_active_quests").AsGodotArray<QuestState>() ?? new Godot.Collections.Array<QuestState>();
    public Godot.Collections.Array<QuestState> get_claimable_quests() => _party_state?.Call("get_claimable_quests").AsGodotArray<QuestState>() ?? new Godot.Collections.Array<QuestState>();
    public Godot.Collections.Array<StringName> get_claimable_quest_ids() => _party_state?.Call("get_claimable_quest_ids").AsGodotArray<StringName>() ?? new Godot.Collections.Array<StringName>();
    public Godot.Collections.Array<StringName> get_completed_quest_ids() => _party_state?.Call("get_completed_quest_ids").AsGodotArray<StringName>() ?? new Godot.Collections.Array<StringName>();

    public bool accept_quest(StringName questId, int worldStep = -1, bool allowReaccept = false)
    {
        if (_party_state == null || questId == "") return false;
        var qd = _quest_defs.ContainsKey(questId) ? _quest_defs[questId].AsGodotObject() : null;
        if (qd == null) return false;
        var qs = _party_state.Call("get_quest_state", questId).AsGodotObject();
        if (qs != null && qs.Get("status_id").AsStringName() != "inactive" && !allowReaccept) return false;
        if (qs == null) { qs = QuestStateScript.Call("new").AsGodotObject(); qs.Set("quest_id", questId); _party_state.Call("set_quest_state", questId, qs); }
        qs.Call("mark_accepted", worldStep);
        return true;
    }

    private static readonly GDScript QuestStateScript = GD.Load<GDScript>("res://scripts/player/progression/quest_state.gd");
    static QuestProgressService() { } // QuestState is now C#, use direct type

    public bool record_progress(StringName questId, StringName objectiveId, int delta, int targetValue = 0, Godot.Collections.Dictionary context = null)
    {
        if (_party_state == null || questId == "" || objectiveId == "") return false;
        var qs = _party_state.Call("get_quest_state", questId).AsGodotObject() as QuestState;
        if (qs == null || !qs.is_active()) return false;
        qs.record_objective_progress(objectiveId, delta, targetValue, context);
        var qd = _quest_defs.ContainsKey(questId) ? _quest_defs[questId].AsGodotObject() : null;
        if (qd != null && qs.has_completed_all_objectives(qd)) qs.mark_completed(_get_world_step());
        return true;
    }

    public bool mark_completed(StringName questId) { if (_party_state == null || questId == "") return false; var qs = _party_state.Call("get_quest_state", questId).AsGodotObject() as QuestState; if (qs == null || !qs.is_active()) return false; qs.mark_completed(_get_world_step()); return true; }

    public bool claim_reward(StringName questId, Godot.Collections.Dictionary claimContext = null)
    {
        if (_party_state == null || questId == "") return false;
        var qs = _party_state.Call("get_quest_state", questId).AsGodotObject() as QuestState;
        if (qs == null || !qs.is_completed() || qs.status_id == "rewarded") return false;
        var qd = _quest_defs.ContainsKey(questId) ? _quest_defs[questId].AsGodotObject() : null;
        qs.mark_reward_claimed(_get_world_step());
        return true;
    }

    public Godot.Collections.Array<Godot.Collections.Dictionary> get_quest_progress_events(StringName questId) { if (_party_state == null) return new Godot.Collections.Array<Godot.Collections.Dictionary>(); var qs = _party_state.Call("get_quest_state", questId).AsGodotObject() as QuestState; return qs != null ? new Godot.Collections.Array<Godot.Collections.Dictionary> { qs.to_dict() } : new Godot.Collections.Array<Godot.Collections.Dictionary>(); }

    private int _get_world_step() => _party_state?.Call("get_world_step").AsInt32() ?? 0;
}
