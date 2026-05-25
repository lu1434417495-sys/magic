using Godot;

[GlobalClass]
public partial class CharacterCreationService : RefCounted
{
    private static readonly GDScript UnitProgressScript = GD.Load<GDScript>("res://scripts/player/progression/unit_progress.gd");
    private static readonly GDScript AttributeServiceScript = GD.Load<GDScript>("res://scripts/systems/attributes/attribute_service.gd");
    private static readonly GDScript ProgressionServiceScript = GD.Load<GDScript>("res://scripts/systems/progression/progression_service.gd");
    private const int HIDDEN_LUCK_AT_BIRTH_MAX = 2, HIDDEN_LUCK_AT_BIRTH_MIN = -6, INITIAL_HP_BASE = 14, MAXIMUM_REROLL_TIER_MINIMUM = 10_000_000;
    private static readonly StringName DEFAULT_SOURCE_ID = "birth_roll";
    private const string CREATION_OPTION_BAKE_REROLL_LUCK = "bake_reroll_luck";
    private static readonly Godot.Collections.Array<string> IDENTITY_BODY_SIZE_SOURCE_FIELDS = new() { "race_id","subrace_id","bloodline_id","bloodline_stage_id","ascension_id","ascension_stage_id","body_size","body_size_category" };

    public static int calculate_initial_hp_max(int constitutionValue) => Mathf.Max(1, INITIAL_HP_BASE + ProgressionServiceScript.Call("calculate_constitution_modifier", constitutionValue).AsInt32() * 2);

    public static PartyMemberState create_member_from_character_creation_payload(StringName memberId, Godot.Collections.Dictionary payload, Variant progressionContentSource = default, Godot.Collections.Dictionary options = null)
    {
        options ??= new Godot.Collections.Dictionary();
        var ms = new PartyMemberState { member_id = memberId };
        ms.progression = UnitProgressScript.Call("new").AsGodotObject(); ms.progression.Set("unit_id", memberId); ms.progression.Set("unit_base_attributes", new UnitBaseAttributes());
        return apply_character_creation_payload_to_member(ms, payload, progressionContentSource, options) ? ms : null;
    }

    public static bool apply_character_creation_payload_to_member(PartyMemberState memberState, Godot.Collections.Dictionary payload, Variant progressionContentSource = default, Godot.Collections.Dictionary options = null)
    {
        options ??= new Godot.Collections.Dictionary();
        if (memberState == null || payload == null || payload.Count == 0) return false;
        if (!_validate_payload_identity_before_mutation(memberState, payload, progressionContentSource)) return false;
        if (memberState.progression == null) { memberState.progression = UnitProgressScript.Call("new").AsGodotObject(); }
        if (memberState.progression.Get("unit_id").AsStringName() == "") memberState.progression.Set("unit_id", memberState.member_id);
        if (memberState.progression.Get("unit_base_attributes").AsGodotObject() == null) memberState.progression.Set("unit_base_attributes", new UnitBaseAttributes());
        string dn = payload.ContainsKey("display_name") ? payload["display_name"].AsString().StripEdges() : memberState.display_name;
        if (dn.Length > 0) { memberState.display_name = dn; memberState.progression.Set("display_name", dn); }
        var ba = memberState.progression.Get("unit_base_attributes").AsGodotObject();
        foreach (string aid in new[] { "strength","agility","constitution","perception","intelligence","willpower" }) if (payload.ContainsKey(aid)) ba.Call("set_attribute_value", new StringName(aid), payload[aid].AsInt32());
        if (!_apply_identity_payload_to_member(memberState, payload, progressionContentSource)) return false;
        var at = AttributeServiceScript.Get("ACTION_THRESHOLD"); if (payload.ContainsKey((string)at)) ba.Call("set_attribute_value", new StringName((string)at), payload[(string)at].AsInt32());
        if (options.ContainsKey(CREATION_OPTION_BAKE_REROLL_LUCK) && options[CREATION_OPTION_BAKE_REROLL_LUCK].AsBool()) { var asv = AttributeServiceScript.Call("new").AsGodotObject(); asv.Call("setup", memberState.progression); var cc = new CharacterCreationService(); cc.bake_hidden_luck_at_birth(asv, payload.ContainsKey("reroll_count") ? payload["reroll_count"] : Variant.From(0)); }
        int con = ba.Call("get_attribute_value", new StringName("constitution")).AsInt32(); int ihp = calculate_initial_hp_max(con);
        ba.Call("set_attribute_value", AttributeServiceScript.Get("HP_MAX"), ihp); memberState.current_hp = ihp; return true;
    }

    public static int map_reroll_count_to_hidden_luck_at_birth(Variant rerollCount) { switch (rerollCount.VariantType) { case Variant.Type.Int: return _map_integer_reroll_count(rerollCount.AsInt32()); case Variant.Type.Float: return _map_float_reroll_count(rerollCount.AsDouble()); case Variant.Type.String: case Variant.Type.StringName: return _map_string_reroll_count(rerollCount.AsString()); default: return HIDDEN_LUCK_AT_BIRTH_MAX; } }

    public bool bake_hidden_luck_at_birth(GodotObject attributeService, Variant rerollCount, StringName sourceId = default) { if (attributeService == null) return false; sourceId = sourceId == "" ? DEFAULT_SOURCE_ID : sourceId; int targetHL = map_reroll_count_to_hidden_luck_at_birth(rerollCount); int currentHL = attributeService.Call("get_base_value", new StringName("hidden_luck_at_birth")).AsInt32(); int delta = targetHL - currentHL; if (delta == 0) return true; return attributeService.Call("apply_permanent_attribute_change", new StringName("hidden_luck_at_birth"), delta, new Godot.Collections.Dictionary { {"source_type", AttributeServiceScript.Get("PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION")}, {"source_id", sourceId} }).AsBool(); }

    private static int _map_integer_reroll_count(int rc) { if (rc <= 0) return HIDDEN_LUCK_AT_BIRTH_MAX; if (rc >= MAXIMUM_REROLL_TIER_MINIMUM) return HIDDEN_LUCK_AT_BIRTH_MIN; return 2 - rc.ToString().Length; }
    private static int _map_float_reroll_count(double rc) { if (double.IsNaN(rc)) return HIDDEN_LUCK_AT_BIRTH_MIN; if (rc <= 0.0) return HIDDEN_LUCK_AT_BIRTH_MAX; if (rc >= MAXIMUM_REROLL_TIER_MINIMUM) return HIDDEN_LUCK_AT_BIRTH_MIN; return _map_integer_reroll_count((int)System.Math.Floor(rc)); }
    private static int _map_string_reroll_count(string rct) { var nt = rct.StripEdges(); if (nt.Length == 0) return HIDDEN_LUCK_AT_BIRTH_MAX; if (nt.StartsWith("-")) return HIDDEN_LUCK_AT_BIRTH_MAX; if (nt.StartsWith("+")) nt = nt.Substring(1); if (nt.Length == 0) return HIDDEN_LUCK_AT_BIRTH_MAX; int fnz = -1; for (int i = 0; i < nt.Length; i++) { if (nt[i] < '0' || nt[i] > '9') return HIDDEN_LUCK_AT_BIRTH_MAX; if (nt[i] != '0' && fnz == -1) fnz = i; } if (fnz == -1) return HIDDEN_LUCK_AT_BIRTH_MAX; int dc = nt.Length - fnz; if (dc >= 8) return HIDDEN_LUCK_AT_BIRTH_MIN; return 2 - dc; }

    private static bool _apply_identity_payload_to_member(PartyMemberState ms, Godot.Collections.Dictionary payload, Variant pcs) { bool srf = _payload_requires_body_size_identity_source(payload); ms.race_id = _rpsn(payload, "race_id", ms.race_id, false); ms.subrace_id = _rpsn(payload, "subrace_id", ms.subrace_id, false); ms.age_years = _rpnni(payload, "age_years", ms.age_years); ms.birth_at_world_step = _rpnni(payload, "birth_at_world_step", ms.birth_at_world_step); ms.age_profile_id = _rpsn(payload, "age_profile_id", ms.age_profile_id, false); ms.natural_age_stage_id = _rpsn(payload, "natural_age_stage_id", ms.natural_age_stage_id, false); ms.effective_age_stage_id = _rpsn(payload, "effective_age_stage_id", ms.effective_age_stage_id, false); ms.effective_age_stage_source_type = _rpsn(payload, "effective_age_stage_source_type", ms.effective_age_stage_source_type, true); ms.effective_age_stage_source_id = _rpsn(payload, "effective_age_stage_source_id", ms.effective_age_stage_source_id, true); ms.versatility_pick = _rpsn(payload, "versatility_pick", ms.versatility_pick, true); if (payload.ContainsKey("active_stage_advancement_modifier_ids") && payload["active_stage_advancement_modifier_ids"].VariantType == Variant.Type.Array) ms.active_stage_advancement_modifier_ids = ProgressionDataUtils.to_string_name_array(payload["active_stage_advancement_modifier_ids"]); ms.bloodline_id = _rpsn(payload, "bloodline_id", ms.bloodline_id, true); ms.bloodline_stage_id = _rpsn(payload, "bloodline_stage_id", ms.bloodline_stage_id, true); ms.ascension_id = _rpsn(payload, "ascension_id", ms.ascension_id, true); ms.ascension_stage_id = _rpsn(payload, "ascension_stage_id", ms.ascension_stage_id, true); if (payload.ContainsKey("ascension_started_at_world_step") && payload["ascension_started_at_world_step"].VariantType == Variant.Type.Int) ms.ascension_started_at_world_step = Mathf.Max(payload["ascension_started_at_world_step"].AsInt32(), -1); ms.original_race_id_before_ascension = _rpsn(payload, "original_race_id_before_ascension", ms.original_race_id_before_ascension, true); ms.biological_age_years = _rpnni(payload, "biological_age_years", ms.biological_age_years); ms.astral_memory_years = _rpnni(payload, "astral_memory_years", ms.astral_memory_years); if (srf) return IdentityPayloadValidator.refresh_member_body_size_from_identity(ms, pcs); return true; }

    private static bool _validate_payload_identity_before_mutation(PartyMemberState ms, Godot.Collections.Dictionary payload, Variant pcs) { if (!_payload_requires_body_size_identity_source(payload)) return true; if (pcs.VariantType == Variant.Type.Nil) return false; var c = _build_identity_candidate_from_payload(ms, payload); var errors = IdentityPayloadValidator.validate_member_identity(c, pcs); if (errors.Count > 0) return false; return IdentityPayloadValidator.resolve_body_size_category_for_member(c, pcs) != ""; }

    private static PartyMemberState _build_identity_candidate_from_payload(PartyMemberState ms, Godot.Collections.Dictionary payload) { var c = new PartyMemberState { member_id = ms.member_id }; c.race_id = _rpsn(payload, "race_id", ms.race_id, false); c.subrace_id = _rpsn(payload, "subrace_id", ms.subrace_id, false); c.bloodline_id = _rpsn(payload, "bloodline_id", ms.bloodline_id, true); c.bloodline_stage_id = _rpsn(payload, "bloodline_stage_id", ms.bloodline_stage_id, true); c.ascension_id = _rpsn(payload, "ascension_id", ms.ascension_id, true); c.ascension_stage_id = _rpsn(payload, "ascension_stage_id", ms.ascension_stage_id, true); return c; }

    private static bool _payload_requires_body_size_identity_source(Godot.Collections.Dictionary payload) { foreach (string fn in IDENTITY_BODY_SIZE_SOURCE_FIELDS) if (payload.ContainsKey(fn)) return true; return false; }
    private static StringName _rpsn(Godot.Collections.Dictionary payload, string fn, StringName fb, bool ae) { if (!payload.ContainsKey(fn)) return fb; var v = payload[fn]; if (v.VariantType != Variant.Type.String && v.VariantType != Variant.Type.StringName) return fb; var p = ProgressionDataUtils.to_string_name(v); if (p == "" && !ae) return fb; return p; }
    private static int _rpnni(Godot.Collections.Dictionary payload, string fn, int fb) { if (!payload.ContainsKey(fn) || payload[fn].VariantType != Variant.Type.Int) return fb; return Mathf.Max(payload[fn].AsInt32(), 0); }
}
