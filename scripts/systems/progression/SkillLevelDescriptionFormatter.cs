using Godot;
using System.Text.RegularExpressions;

[GlobalClass]
public partial class SkillLevelDescriptionFormatter : RefCounted
{
    public static string build_level_description(SkillDef skillDef, int level, Godot.Collections.Dictionary runtimeContext = null)
    {
        if (skillDef == null || skillDef.level_description_template.Length == 0) return "";
        var config = new Godot.Collections.Dictionary();
        var rawConfig = skillDef.level_description_configs.ContainsKey(level.ToString()) ? skillDef.level_description_configs[level.ToString()] : default(Variant);
        if (rawConfig.VariantType == Variant.Type.Dictionary) config = rawConfig.AsGodotDictionary().Duplicate();
        _merge_matching_effect_params(config, skillDef, level);
        _merge_matching_effect_typed_fields(config, skillDef, level);
        _merge_level_overrides(config, skillDef, level);
        _resolve_charge_distance(config, level);
        if (runtimeContext != null) foreach (var ck in runtimeContext.Keys) config[ck] = runtimeContext[ck];
        _apply_description_derived_fields(config);
        if (config.Count == 0) return "";
        return render_template(skillDef.level_description_template, config);
    }

    public static string render_template(string template, Godot.Collections.Dictionary config)
    {
        string result = template;
        var condRegex = new Regex(@"\{\{\?([^}]+)\}\}(.*?)\{\{/\1\}\}", RegexOptions.Singleline);
        while (true) { var m = condRegex.Match(result); if (!m.Success) break; string key = m.Groups[1].Value.Trim(); string inner = m.Groups[2].Value; result = config.ContainsKey(key) && _is_optional_value_visible(config[key]) ? result.Substring(0, m.Index) + inner + result.Substring(m.Index + m.Length) : result.Substring(0, m.Index) + result.Substring(m.Index + m.Length); }
        var exprRegex = new Regex(@"\{=([^}]+)\}");
        while (true) { var m = exprRegex.Match(result); if (!m.Success) break; result = result.Substring(0, m.Index) + _eval_expression(m.Groups[1].Value.Trim(), config) + result.Substring(m.Index + m.Length); }
        var varRegex = new Regex(@"\{([^}]+)\}");
        while (true) { var m = varRegex.Match(result); if (!m.Success) break; string key = m.Groups[1].Value.Trim(); string value = config.ContainsKey(key) ? config[key].AsString() : ""; result = result.Substring(0, m.Index) + value + result.Substring(m.Index + m.Length); }
        return result;
    }

    private static bool _is_optional_value_visible(Variant v) { var t = v.VariantType; if (t == Variant.Type.Nil) return false; if (t == Variant.Type.Bool) return v.AsBool(); if (t == Variant.Type.Int) return v.AsInt32() != 0; if (t == Variant.Type.Float) { double f = v.AsDouble(); return !double.IsNaN(f) && !Mathf.IsEqualApprox((float)f, 0f); } if (t == Variant.Type.String || t == Variant.Type.StringName) return v.AsString().StripEdges().Length > 0; return v.AsString().StripEdges().Length > 0; }

    private static void _merge_matching_effect_params(Godot.Collections.Dictionary config, SkillDef skillDef, int level) { foreach (var ed in _collect_level_effect_defs(skillDef, level)) { if (ed?.@params == null) continue; foreach (var pk in ed.@params.Keys) { if (!config.ContainsKey(pk)) config[pk] = ed.@params[pk]; } } }

    private static void _merge_matching_effect_typed_fields(Godot.Collections.Dictionary config, SkillDef skillDef, int level) { foreach (var ed in _collect_level_effect_defs(skillDef, level)) { if (ed == null) continue; var et = ed.effect_type; if (et == "damage") _merge_damage_effect_typed_fields(config, ed); else if (et == "status" || et == "apply_status") _merge_status_effect_typed_fields(config, ed); else if (et == "forced_move") { if (ed.forced_move_mode != "") _set_if_missing(config, "forced_move_mode", (string)ed.forced_move_mode); if (ed.forced_move_distance > 0) _set_if_missing(config, "forced_move_distance", ed.forced_move_distance); } } }

    private static Godot.Collections.Array<CombatEffectDef> _collect_level_effect_defs(SkillDef skillDef, int level) { var r = new Godot.Collections.Array<CombatEffectDef>(); if (skillDef?.combat_profile == null) return r; _append_level_effect_defs(r, ((CombatSkillDef)skillDef.combat_profile).effect_defs, level); foreach (var cv in skillDef.combat_profile.get_unlocked_cast_variants(level)) { if (cv != null) _append_level_effect_defs(r, ToCE(cv.effect_defs), level); } return r; }

    private static void _append_level_effect_defs(Godot.Collections.Array<CombatEffectDef> output, Godot.Collections.Array<CombatEffectDef> effectDefs, int level) { foreach (var ed in effectDefs) { if (ed != null && _effect_unlocked_at_level(ed, level)) output.Add(ed); } }

    private static bool _effect_unlocked_at_level(CombatEffectDef ed, int level) { if (ed == null) return false; if (level < Mathf.Max(ed.min_skill_level, 0)) return false; return ed.max_skill_level < 0 || level <= ed.max_skill_level; }

    private static void _merge_damage_effect_typed_fields(Godot.Collections.Dictionary config, CombatEffectDef ed) { if (ed.power != 0) _set_if_missing(config, "damage_power", ed.power); if (ed.damage_ratio_percent != 100) _set_if_missing(config, "damage_ratio_percent", ed.damage_ratio_percent); if (ed.damage_tag != "") _set_if_missing(config, "damage_tag", (string)ed.damage_tag); _merge_save_fields(config, "damage", ed); }

    private static void _merge_status_effect_typed_fields(Godot.Collections.Dictionary config, CombatEffectDef ed) { string statusId = (string)ed.status_id; if (statusId.Length == 0) return; string label = _format_status_label(ed.status_id); _set_if_missing(config, "status_id", statusId); _set_if_missing(config, "status_display_name", label); if (ed.duration_tu > 0) _set_if_missing(config, "status_duration_tu", ed.duration_tu); if (ed.power != 0) _set_if_missing(config, "status_power", ed.power); _set_if_missing(config, $"{statusId}_status_id", statusId); _set_if_missing(config, $"{statusId}_display_name", label); if (ed.duration_tu > 0) _set_if_missing(config, $"{statusId}_duration_tu", ed.duration_tu); if (ed.power != 0) _set_if_missing(config, $"{statusId}_power", ed.power); _merge_save_fields(config, "status", ed); _merge_save_fields(config, statusId, ed); }

    private static void _merge_save_fields(Godot.Collections.Dictionary config, string prefix, CombatEffectDef ed) { if (prefix.Length == 0 || ed?.save_ability == "") return; string sa = (string)ed.save_ability; string sl = _format_attribute_label(ed.save_ability); _set_if_missing(config, $"{prefix}_save_ability", sa); _set_if_missing(config, $"{prefix}_save_ability_label", sl); _set_if_missing(config, $"{prefix}_save_text", _format_save_text(ed, sl)); }

    private static string _format_save_text(CombatEffectDef ed, string saveLabel) { if (ed == null) return ""; if (ed.effect_type == "damage" && ed.save_partial_on_success) return $"{saveLabel}豁免成功时伤害减半"; if ((ed.effect_type == "status" || ed.effect_type == "apply_status") && ed.status_id != "") return $"{saveLabel}豁免失败时附加{_format_status_label(ed.status_id)}"; return $"{saveLabel}豁免"; }

    private static string _format_attribute_label(StringName attrId) { string a = (string)attrId; if (a == "strength") return "力量"; if (a == "agility") return "敏捷"; if (a == "constitution") return "体质"; if (a == "perception") return "感知"; if (a == "intelligence") return "智力"; if (a == "willpower") return "意志"; return a; }

    private static string _format_status_label(StringName sid) { string s = (string)sid; if (s == "shocked") return "感电"; if (s == "burning") return "燃烧"; if (s == "frozen") return "冻结"; if (s == "slow") return "迟缓"; if (s == "blind" || s == "blinded") return "失明"; if (s == "rooted") return "定身"; if (s == "staggered") return "踉跄"; return s; }

    private static void _set_if_missing(Godot.Collections.Dictionary config, string key, Variant value) { if (!config.ContainsKey(key)) config[key] = value; }
    private static void _set_if_missing(Godot.Collections.Dictionary config, string key, int value) { if (!config.ContainsKey(key)) config[key] = value; }
    private static void _set_if_missing(Godot.Collections.Dictionary config, string key, string value) { if (!config.ContainsKey(key)) config[key] = value; }

    private static void _merge_level_overrides(Godot.Collections.Dictionary config, SkillDef skillDef, int level) { if (skillDef?.combat_profile == null) return; var p = skillDef.combat_profile; var o = p.get_level_override(level); var fields = new Godot.Collections.Dictionary { {"ap_cost", p.ap_cost},{"mp_cost", p.mp_cost},{"stamina_cost", p.stamina_cost},{"cooldown_tu", p.cooldown_tu},{"attack_roll_bonus", p.attack_roll_bonus},{"aura_cost", p.aura_cost},{"range_value", p.range_value},{"area_value", p.area_value} }; foreach (var fk in fields.Keys) { if (!config.ContainsKey(fk)) { string ks = fk.AsString(); config[ks] = o.ContainsKey(ks) ? o[ks] : fields[fk]; } } }

    private static void _resolve_charge_distance(Godot.Collections.Dictionary config, int level) { if (config.ContainsKey("distance")) return; if (!config.ContainsKey("base_distance") && !config.ContainsKey("distance_by_level")) return; int baseDist = config.ContainsKey("base_distance") ? config["base_distance"].AsInt32() : 0; var dbl = config.ContainsKey("distance_by_level") ? config["distance_by_level"] : default(Variant); if (dbl.VariantType != Variant.Type.Dictionary) { config["distance"] = baseDist; return; } int dist = baseDist; var keys = new Godot.Collections.Array<int>(); foreach (var k in dbl.AsGodotDictionary().Keys) keys.Add(k.AsString().ToInt()); keys.Sort(); foreach (int k in keys) { if (k > level) break; dist = dbl.AsGodotDictionary()[k.ToString()].AsInt32(); } config["distance"] = dist; }

    private static void _apply_description_derived_fields(Godot.Collections.Dictionary config) { if (config.ContainsKey("base_sides") && config.ContainsKey("con_mod_sides") && config.ContainsKey("will_mod_sides")) { int bs = config["base_sides"].AsInt32(); int cm = config.ContainsKey("con_mod") ? config["con_mod"].AsInt32() : 0; int wm = config.ContainsKey("will_mod") ? config["will_mod"].AsInt32() : 0; int cms = config["con_mod_sides"].AsInt32(); int wms = config["will_mod_sides"].AsInt32(); config["dice_sides"] = Mathf.Max(bs + cm * cms + wm * wms, 4); } }

    private static Godot.Collections.Array<CombatEffectDef> ToCE(Godot.Collections.Array<Resource> src) { var r = new Godot.Collections.Array<CombatEffectDef>(); foreach (var item in src) { if (item is CombatEffectDef ce) r.Add(ce); } return r; }

    private static string _eval_expression(string exprStr, Godot.Collections.Dictionary variables) { var expr = new Godot.Expression(); var inputNames = new System.Collections.Generic.List<string>(); var inputValues = new Godot.Collections.Array(); foreach (var k in variables.Keys) { string ks = k.AsString(); inputNames.Add(ks); var v = variables[k]; if (v.VariantType == Variant.Type.String) { string vs = v.AsString(); inputValues.Add(vs.IsValidInt() ? Variant.From(vs.ToInt()) : (vs.IsValidFloat() ? Variant.From(vs.ToFloat()) : v)); } else inputValues.Add(v); } if (expr.Parse(exprStr, inputNames.ToArray()) != Error.Ok) return "{=" + exprStr + "}"; var er = expr.Execute(inputValues); if (expr.HasExecuteFailed()) return "{=" + exprStr + "}"; if (er.VariantType == Variant.Type.Float && er.AsDouble() == System.Math.Floor(er.AsDouble())) return ((int)er.AsDouble()).ToString(); return er.AsString(); }
}
