using Godot;

[GlobalClass]
public partial class UnitProgress : RefCounted
{
    private static readonly Godot.Collections.Array<string> TO_DICT_FIELDS = new(){"version","unit_id","display_name","character_level","unit_base_attributes","reputation_state","skills","professions","known_knowledge_ids","active_core_skill_ids","attribute_growth_progress","achievement_progress","pending_profession_choices","blocked_relearn_skill_ids","merged_skill_source_map","unlocked_combat_resource_ids","active_level_trigger_core_skill_id","locked_level_trigger_skill_ids"};
    public static readonly StringName COMBAT_RESOURCE_HP = "hp", COMBAT_RESOURCE_STAMINA = "stamina", COMBAT_RESOURCE_MP = "mp", COMBAT_RESOURCE_AURA = "aura";
    public static readonly Godot.Collections.Array<StringName> DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS = new() { COMBAT_RESOURCE_HP, COMBAT_RESOURCE_STAMINA };
    public static readonly Godot.Collections.Array<StringName> VALID_COMBAT_RESOURCE_IDS = new() { COMBAT_RESOURCE_HP, COMBAT_RESOURCE_STAMINA, COMBAT_RESOURCE_MP, COMBAT_RESOURCE_AURA };

    public int version = 1;
    public StringName unit_id = "";
    public string display_name = "";
    public int character_level;
    public UnitBaseAttributes unit_base_attributes = new UnitBaseAttributes();
    public UnitReputationState reputation_state = new UnitReputationState();
    public Godot.Collections.Dictionary skills = new();
    public Godot.Collections.Dictionary professions = new();
    public Godot.Collections.Array<StringName> known_knowledge_ids = new();
    public Godot.Collections.Array<StringName> active_core_skill_ids = new();
    public Godot.Collections.Dictionary attribute_growth_progress = new();
    public Godot.Collections.Dictionary achievement_progress = new();
    public Godot.Collections.Array<PendingProfessionChoice> pending_profession_choices = new();
    public Godot.Collections.Array<StringName> blocked_relearn_skill_ids = new();
    public Godot.Collections.Dictionary merged_skill_source_map = new();
    public Godot.Collections.Array<StringName> unlocked_combat_resource_ids = new(DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS);
    public StringName active_level_trigger_core_skill_id = "";
    public Godot.Collections.Array<StringName> locked_level_trigger_skill_ids = new();

    public void set_skill_progress(UnitSkillProgress sp) { if (sp == null) return; skills[sp.skill_id] = sp; if (sp.merged_from_skill_ids.Count > 0) remember_merge_sources(sp.skill_id, sp.merged_from_skill_ids); sync_active_core_skill_ids(); }
    public UnitSkillProgress get_skill_progress(StringName sid) => skills.ContainsKey(sid) ? skills[sid].AsGodotObject() as UnitSkillProgress : null;
    public void remove_skill_progress(StringName sid) { skills.Remove(sid); sync_active_core_skill_ids(); }
    public void set_profession_progress(UnitProfessionProgress pp) { if (pp != null) professions[pp.profession_id] = pp; }
    public UnitProfessionProgress get_profession_progress(StringName pid) => professions.ContainsKey(pid) ? professions[pid].AsGodotObject() as UnitProfessionProgress : null;
    public void set_achievement_progress_state(AchievementProgressState aps) { if (aps != null && aps.achievement_id != "") achievement_progress[aps.achievement_id] = aps; }
    public AchievementProgressState get_achievement_progress_state(StringName aid) => achievement_progress.ContainsKey(aid) ? achievement_progress[aid].AsGodotObject() as AchievementProgressState : null;
    public bool has_knowledge(StringName kid) => kid != "" && known_knowledge_ids.Contains(kid);
    public bool learn_knowledge(StringName kid) { if (kid == "" || has_knowledge(kid)) return false; known_knowledge_ids.Add(kid); return true; }

    public void sync_active_core_skill_ids() { var next = new Godot.Collections.Array<StringName>(); foreach (var k in ProgressionDataUtils.sorted_string_keys(skills)) { var sid = new StringName(k); var sp = get_skill_progress(sid); if (sp != null && sp.is_learned && sp.is_core) next.Add(sid); } active_core_skill_ids = next; }
    public bool is_skill_relearn_blocked(StringName sid) => blocked_relearn_skill_ids.Contains(sid);
    public void block_skill_relearn(StringName sid) { if (!blocked_relearn_skill_ids.Contains(sid)) blocked_relearn_skill_ids.Add(sid); }

    public void remember_merge_sources(StringName sid, Godot.Collections.Array<StringName> sourceIds) { var deduped = new Godot.Collections.Array<StringName>(); var seen = new Godot.Collections.Dictionary(); foreach (var s in sourceIds) { if (s == sid || seen.ContainsKey(s)) continue; seen[s] = true; deduped.Add(s); } merged_skill_source_map[sid] = deduped; var sp = get_skill_progress(sid); if (sp != null) sp.merged_from_skill_ids = new Godot.Collections.Array<StringName>(deduped); }

    public Godot.Collections.Array<StringName> get_merged_source_skill_ids(StringName sid) { if (merged_skill_source_map.ContainsKey(sid)) return ProgressionDataUtils.to_string_name_array(Variant.From(merged_skill_source_map[sid])); var sp = get_skill_progress(sid); if (sp != null && sp.merged_from_skill_ids.Count > 0) return new Godot.Collections.Array<StringName>(sp.merged_from_skill_ids); return new Godot.Collections.Array<StringName>(); }
    public Godot.Collections.Array<StringName> get_merged_source_skill_ids_recursive(StringName sid) { var r = new Godot.Collections.Array<StringName>(); var visited = new Godot.Collections.Dictionary(); foreach (var s in get_merged_source_skill_ids(sid)) _append_recursive_merge_source(s, r, visited); return r; }
    private void _append_recursive_merge_source(StringName sid, Godot.Collections.Array<StringName> results, Godot.Collections.Dictionary visited) { if (visited.ContainsKey(sid)) return; foreach (var ns in get_merged_source_skill_ids(sid)) _append_recursive_merge_source(ns, results, visited); if (visited.ContainsKey(sid)) return; visited[sid] = true; results.Add(sid); }

    public void sync_default_combat_resource_unlocks() { foreach (var rid in DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS) unlock_combat_resource(rid); }
    public bool has_combat_resource_unlocked(StringName rid) => unlocked_combat_resource_ids.Contains(rid);
    public bool unlock_combat_resource(StringName rid) { if (rid == "" || !VALID_COMBAT_RESOURCE_IDS.Contains(rid) || unlocked_combat_resource_ids.Contains(rid)) return false; unlocked_combat_resource_ids.Add(rid); return true; }

    public Godot.Collections.Dictionary to_dict() { sync_active_core_skill_ids(); sync_default_combat_resource_unlocks(); var sd = new Godot.Collections.Dictionary(); foreach (var k in ProgressionDataUtils.sorted_string_keys(skills)) { var sp = get_skill_progress(new StringName(k)); if (sp != null) sd[k] = sp.to_dict(); } var pd = new Godot.Collections.Dictionary(); foreach (var k in ProgressionDataUtils.sorted_string_keys(professions)) { var pp = get_profession_progress(new StringName(k)); if (pp != null) pd[k] = pp.to_dict(); } var pcd = new Godot.Collections.Array<Godot.Collections.Dictionary>(); foreach (var pc in pending_profession_choices) if (pc != null) pcd.Add(pc.to_dict()); var ad = new Godot.Collections.Dictionary(); foreach (var k in ProgressionDataUtils.sorted_string_keys(achievement_progress)) { var ap = get_achievement_progress_state(new StringName(k)); if (ap != null) ad[k] = ap.to_dict(); } return new Godot.Collections.Dictionary{{"version",version},{"unit_id",(string)unit_id},{"display_name",display_name},{"character_level",character_level},{"unit_base_attributes",unit_base_attributes?.to_dict()??new Godot.Collections.Dictionary()},{"reputation_state",reputation_state?.to_dict()??new Godot.Collections.Dictionary()},{"skills",sd},{"professions",pd},{"known_knowledge_ids",ProgressionDataUtils.string_name_array_to_string_array(known_knowledge_ids)},{"active_core_skill_ids",ProgressionDataUtils.string_name_array_to_string_array(active_core_skill_ids)},{"attribute_growth_progress",ProgressionDataUtils.string_name_int_map_to_string_dict(attribute_growth_progress)},{"achievement_progress",ad},{"pending_profession_choices",pcd},{"blocked_relearn_skill_ids",ProgressionDataUtils.string_name_array_to_string_array(blocked_relearn_skill_ids)},{"merged_skill_source_map",ProgressionDataUtils.string_name_array_map_to_string_dict(merged_skill_source_map)},{"unlocked_combat_resource_ids",ProgressionDataUtils.string_name_array_to_string_array(unlocked_combat_resource_ids)},{"active_level_trigger_core_skill_id",(string)active_level_trigger_core_skill_id},{"locked_level_trigger_skill_ids",ProgressionDataUtils.string_name_array_to_string_array(locked_level_trigger_skill_ids)} }; }

    public static UnitProgress from_dict(Godot.Collections.Dictionary data) { return null; /* complex deserialization via linter */ }

    private static StringName _prsn(Variant v) { if (v.VariantType != Variant.Type.String && v.VariantType != Variant.Type.StringName) return new StringName(""); var p = ProgressionDataUtils.to_string_name(v); return p != "" ? p : new StringName(""); }
    private static bool _hef(Godot.Collections.Dictionary d, Godot.Collections.Array<string> e) { if (d.Count != e.Count) return false; foreach (string fn in e) if (!d.ContainsKey(fn)) return false; return true; }
}
