using Godot;

[GlobalClass]
public partial class FaithService : RefCounted
{
    private static readonly GDScript PartyStateScript = GD.Load<GDScript>("res://scripts/player/progression/party_state.gd");
    private GodotObject _party_state;

    public FaithService() { _party_state = PartyStateScript.Call("new").AsGodotObject(); }
    public void setup(GodotObject partyState) { _party_state = partyState ?? PartyStateScript.Call("new").AsGodotObject(); }
    public GodotObject get_party_state() => _party_state;

    public Godot.Collections.Dictionary get_faith_status(StringName memberId, Godot.Collections.Dictionary deityDefs = null)
    {
        deityDefs ??= new Godot.Collections.Dictionary();
        var r = new Godot.Collections.Dictionary { {"member_id",memberId},{"has_deity",false},{"deity_id",new StringName("")},{"deity_name",""},{"current_rank",0},{"max_rank",0},{"progress_stat_id",new StringName("")},{"progress_value",0} };
        if (_party_state == null || memberId == "") return r;
        var ms = _party_state.Call("get_member_state", memberId).AsGodotObject();
        if (ms == null || ms.Get("progression").AsGodotObject() == null) return r;
        var deityId = ms.Get("faith_deity_id").AsStringName();
        if (deityId == "" || !deityDefs.ContainsKey(deityId)) return r;
        var deity = deityDefs[deityId].AsGodotObject() as FaithDeityDef;
        if (deity == null) return r;
        r["has_deity"] = true; r["deity_id"] = deityId; r["deity_name"] = deity.display_name; r["progress_stat_id"] = deity.rank_progress_stat_id;
        int rank = ms.Get("faith_rank").AsInt32(); r["current_rank"] = rank; r["max_rank"] = deity.get_max_rank();
        var uba = ms.Get("progression").AsGodotObject()?.Get("unit_base_attributes").AsGodotObject();
        r["progress_value"] = uba?.Call("get_attribute_value", deity.rank_progress_stat_id).AsInt32() ?? 0;
        return r;
    }

    public Godot.Collections.Dictionary get_available_ranks(StringName memberId, Godot.Collections.Dictionary deityDefs = null)
    {
        deityDefs ??= new Godot.Collections.Dictionary();
        var r = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        var fs = get_faith_status(memberId, deityDefs);
        if (!fs["has_deity"].AsBool()) return new Godot.Collections.Dictionary { {"ranks",r} };
        var deityId = fs["deity_id"].AsStringName();
        var deity = deityDefs.ContainsKey(deityId) ? deityDefs[deityId].AsGodotObject() as FaithDeityDef : null;
        if (deity == null) return new Godot.Collections.Dictionary { {"ranks",r} };
        int currentRank = fs["current_rank"].AsInt32();
        for (int i = 1; i <= deity.get_max_rank(); i++) {
            var rd = deity.get_rank_def(i); if (rd == null) continue;
            var ar = new Godot.Collections.Dictionary { {"rank_index",i},{"rank_name",rd.rank_name},{"required_gold",rd.required_gold},{"required_level",rd.required_level},{"required_custom_stat_id",rd.required_custom_stat_id},{"required_custom_stat_min_value",rd.required_custom_stat_min_value},{"required_achievement_id",rd.required_achievement_id},{"is_attained",i<=currentRank},{"is_next",i==currentRank+1},{"reward_entries",rd.reward_entries} };
            r.Add(ar);
        }
        return new Godot.Collections.Dictionary { {"ranks",r} };
    }

    public Godot.Collections.Dictionary promote_rank(StringName memberId, int targetRank, Godot.Collections.Dictionary deityDefs = null)
    {
        deityDefs ??= new Godot.Collections.Dictionary();
        var result = new Godot.Collections.Dictionary { {"ok",false},{"member_id",memberId},{"target_rank",targetRank} };
        if (_party_state == null || memberId == "" || targetRank <= 0) { result["error"] = "invalid_request"; return result; }
        var ms = _party_state.Call("get_member_state", memberId).AsGodotObject();
        if (ms == null || ms.Get("progression").AsGodotObject() == null) { result["error"] = "member_not_found"; return result; }
        var deityId = ms.Get("faith_deity_id").AsStringName();
        if (deityId == "" || !deityDefs.ContainsKey(deityId)) { result["error"] = "no_deity"; return result; }
        var deity = deityDefs[deityId].AsGodotObject() as FaithDeityDef;
        if (deity == null) { result["error"] = "deity_not_found"; return result; }
        int currentRank = ms.Get("faith_rank").AsInt32();
        if (targetRank != currentRank + 1) { result["error"] = "invalid_rank_progression"; return result; }
        var rd = deity.get_rank_def(targetRank); if (rd == null) { result["error"] = "rank_def_not_found"; return result; }
        if (!_party_state.Call("can_afford", rd.required_gold).AsBool()) { result["error"] = "insufficient_gold"; return result; }
        int charLevel = ms.Get("progression").AsGodotObject()?.Get("character_level").AsInt32() ?? 0;
        if (charLevel < rd.required_level) { result["error"] = "level_too_low"; return result; }
        var uba = ms.Get("progression").AsGodotObject()?.Get("unit_base_attributes").AsGodotObject();
        if (rd.required_custom_stat_id != "" && uba != null) { int statVal = uba.Call("get_attribute_value", rd.required_custom_stat_id).AsInt32(); if (statVal < rd.required_custom_stat_min_value) { result["error"] = "stat_too_low"; return result; } }
        _party_state.Call("spend_gold", rd.required_gold);
        ms.Set("faith_rank", targetRank);
        result["ok"] = true; result["reward_entries"] = rd.reward_entries;
        return result;
    }
}
