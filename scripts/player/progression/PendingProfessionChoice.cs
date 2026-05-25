using Godot;

[GlobalClass]
public partial class PendingProfessionChoice : RefCounted
{
    public Godot.Collections.Array<StringName> trigger_skill_ids = new();
    public Godot.Collections.Array<StringName> candidate_profession_ids = new();
    public Godot.Collections.Dictionary target_rank_map = new();
    public Godot.Collections.Array<StringName> qualifier_skill_pool_ids = new();
    public Godot.Collections.Array<StringName> assignable_skill_candidate_ids = new();
    public int required_qualifier_count;
    public int required_assigned_core_count;

    public void set_target_rank(StringName profession_id, int target_rank) => target_rank_map[profession_id] = target_rank;

    public Godot.Collections.Dictionary to_dict() => new()
    {
        {"trigger_skill_ids", ProgressionDataUtils.string_name_array_to_string_array(trigger_skill_ids)},
        {"candidate_profession_ids", ProgressionDataUtils.string_name_array_to_string_array(candidate_profession_ids)},
        {"target_rank_map", ProgressionDataUtils.string_name_int_map_to_string_dict(target_rank_map)},
        {"qualifier_skill_pool_ids", ProgressionDataUtils.string_name_array_to_string_array(qualifier_skill_pool_ids)},
        {"assignable_skill_candidate_ids", ProgressionDataUtils.string_name_array_to_string_array(assignable_skill_candidate_ids)},
        {"required_qualifier_count", required_qualifier_count},
        {"required_assigned_core_count", required_assigned_core_count},
    };

    public static PendingProfessionChoice from_dict(Godot.Collections.Dictionary data)
    {
        if (!_hfs(data, new Godot.Collections.Array<string> { "trigger_skill_ids", "candidate_profession_ids", "target_rank_map", "qualifier_skill_pool_ids", "assignable_skill_candidate_ids", "required_qualifier_count", "required_assigned_core_count" })) return null;
        var tsi = _pusna(data["trigger_skill_ids"].AsGodotArray()); if (tsi == null) return null;
        var cpi = _pusna(data["candidate_profession_ids"].AsGodotArray()); if (cpi == null) return null;
        var trm = _pnim(data["target_rank_map"].AsGodotDictionary()); if (trm == null) return null;
        var qsi = _pusna(data["qualifier_skill_pool_ids"].AsGodotArray()); if (qsi == null) return null;
        var asi = _pusna(data["assignable_skill_candidate_ids"].AsGodotArray()); if (asi == null) return null;
        if (data["required_qualifier_count"].VariantType != Variant.Type.Int || data["required_qualifier_count"].AsInt32() < 0) return null;
        if (data["required_assigned_core_count"].VariantType != Variant.Type.Int || data["required_assigned_core_count"].AsInt32() < 0) return null;
        return new PendingProfessionChoice { trigger_skill_ids = tsi, candidate_profession_ids = cpi, target_rank_map = trm, qualifier_skill_pool_ids = qsi, assignable_skill_candidate_ids = asi, required_qualifier_count = data["required_qualifier_count"].AsInt32(), required_assigned_core_count = data["required_assigned_core_count"].AsInt32() };
    }

    private static bool _hfs(Godot.Collections.Dictionary d, Godot.Collections.Array<string> f) { if (d.Count != f.Count) return false; foreach (string n in f) if (!d.ContainsKey(n)) return false; return true; }
    private static Godot.Collections.Array<StringName> _pusna(Godot.Collections.Array a) { var r = new Godot.Collections.Array<StringName>(); var s = new Godot.Collections.Dictionary(); foreach (var v in a) { var p = _psn(v); if (p == null || s.ContainsKey(p)) return null; s[p] = true; r.Add(p); } return r; }
    private static Godot.Collections.Dictionary _pnim(Godot.Collections.Dictionary v) { var p = new Godot.Collections.Dictionary(); var s = new Godot.Collections.Dictionary(); foreach (var rk in v.Keys) { var pk = _psn(rk); if (pk == null || s.ContainsKey(pk)) return null; var rv = v[rk]; if (rv.VariantType != Variant.Type.Int || rv.AsInt32() < 0) return null; s[pk] = true; p[pk] = rv.AsInt32(); } return p; }
    private static StringName _psn(Variant v) { var vt = v.VariantType; if (vt != Variant.Type.String && vt != Variant.Type.StringName) return null; var p = ProgressionDataUtils.to_string_name(v); return (string)p == "" ? null : (StringName?)p; }
}
