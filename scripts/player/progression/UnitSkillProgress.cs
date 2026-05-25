using Godot;

[GlobalClass]
public partial class UnitSkillProgress : RefCounted
{
    public static readonly StringName GRANTED_SOURCE_PLAYER = "player";
    public static readonly StringName GRANTED_SOURCE_PROFESSION = "profession";
    public static readonly StringName GRANTED_SOURCE_RACE = "race";
    public static readonly StringName GRANTED_SOURCE_SUBRACE = "subrace";
    public static readonly StringName GRANTED_SOURCE_ASCENSION = "ascension";
    public static readonly StringName GRANTED_SOURCE_BLOODLINE = "bloodline";

    private static readonly Godot.Collections.Dictionary VALID_GRANTED_SOURCE_TYPES = new()
    {
        { GRANTED_SOURCE_PLAYER, true }, { GRANTED_SOURCE_PROFESSION, true }, { GRANTED_SOURCE_RACE, true },
        { GRANTED_SOURCE_SUBRACE, true }, { GRANTED_SOURCE_ASCENSION, true }, { GRANTED_SOURCE_BLOODLINE, true },
    };

    private static readonly Godot.Collections.Array<string> TO_DICT_FIELDS = new()
    {
        "skill_id", "is_learned", "skill_level", "current_mastery", "total_mastery_earned", "is_core",
        "assigned_profession_id", "merged_from_skill_ids", "mastery_from_training", "mastery_from_battle",
        "profession_granted_by", "granted_source_type", "granted_source_id", "core_max_growth_claimed",
        "is_level_trigger_active", "is_level_trigger_locked", "bonus_to_hit_from_lock",
    };

    public StringName skill_id = "";
    public bool is_learned;
    public int skill_level;
    public int current_mastery;
    public int total_mastery_earned;
    public bool is_core;
    public StringName assigned_profession_id = "";
    public Godot.Collections.Array<StringName> merged_from_skill_ids = new();
    public int mastery_from_training;
    public int mastery_from_battle;
    public StringName profession_granted_by = "";
    public StringName granted_source_type = GRANTED_SOURCE_PLAYER;
    public StringName granted_source_id = "";
    public bool core_max_growth_claimed;
    public bool is_level_trigger_active;
    public bool is_level_trigger_locked;
    public int bonus_to_hit_from_lock = 1;

    public bool is_max_level(int maxLevel) => skill_level >= maxLevel;
    public void clear_profession_assignment() => assigned_profession_id = "";

    public Godot.Collections.Dictionary to_dict()
    {
        return new Godot.Collections.Dictionary
        {
            { "skill_id", (string)skill_id }, { "is_learned", is_learned }, { "skill_level", skill_level },
            { "current_mastery", current_mastery }, { "total_mastery_earned", total_mastery_earned }, { "is_core", is_core },
            { "assigned_profession_id", (string)assigned_profession_id },
            { "merged_from_skill_ids", ProgressionDataUtils.string_name_array_to_string_array(merged_from_skill_ids) },
            { "mastery_from_training", mastery_from_training }, { "mastery_from_battle", mastery_from_battle },
            { "profession_granted_by", (string)profession_granted_by }, { "granted_source_type", (string)granted_source_type },
            { "granted_source_id", (string)granted_source_id }, { "core_max_growth_claimed", core_max_growth_claimed },
            { "is_level_trigger_active", is_level_trigger_active }, { "is_level_trigger_locked", is_level_trigger_locked },
            { "bonus_to_hit_from_lock", bonus_to_hit_from_lock },
        };
    }

    public static UnitSkillProgress from_dict(Godot.Collections.Dictionary data)
    {
        if (!_has_exact_fields(data, TO_DICT_FIELDS)) return null;
        if (data["merged_from_skill_ids"].VariantType != Variant.Type.Array) return null;
        var skillId = _parse_string_name_field(data["skill_id"], false, out bool ok1);
        if (!ok1) return null;
        foreach (string bf in new[] { "is_learned", "is_core", "core_max_growth_claimed", "is_level_trigger_active", "is_level_trigger_locked" })
            if (data[bf].VariantType != Variant.Type.Bool) return null;
        foreach (string inf in new[] { "skill_level", "current_mastery", "total_mastery_earned", "mastery_from_training", "mastery_from_battle", "bonus_to_hit_from_lock" })
        {
            var iv = data[inf];
            if (iv.VariantType != Variant.Type.Int || iv.AsInt32() < 0) return null;
        }
        var assignedProf = _parse_string_name_field(data["assigned_profession_id"], true, out bool ok2);
        if (!ok2) return null;
        var profGrantedBy = _parse_string_name_field(data["profession_granted_by"], true, out bool ok3);
        if (!ok3) return null;
        var grantSourceType = _parse_string_name_field(data["granted_source_type"], false, out bool ok4);
        if (!ok4 || !VALID_GRANTED_SOURCE_TYPES.ContainsKey(grantSourceType)) return null;
        var grantSourceId = _parse_string_name_field(data["granted_source_id"], grantSourceType == GRANTED_SOURCE_PLAYER, out bool ok5);
        if (!ok5) return null;
        var mergedIds = _parse_unique_string_name_array(data["merged_from_skill_ids"].AsGodotArray());
        if (mergedIds == null) return null;

        bool isLearned = data["is_learned"].AsBool();
        if (!isLearned)
        {
            if (data["skill_level"].AsInt32() != 0) return null;
            if (data["current_mastery"].AsInt32() != 0) return null;
            if (data["total_mastery_earned"].AsInt32() != 0) return null;
            if (data["mastery_from_training"].AsInt32() != 0) return null;
            if (data["mastery_from_battle"].AsInt32() != 0) return null;
            if (data["is_core"].AsBool()) return null;
            if (assignedProf != "") return null;
            if (mergedIds.Count > 0) return null;
            if (profGrantedBy != "") return null;
            if (data["core_max_growth_claimed"].AsBool()) return null;
        }

        int curMastery = data["current_mastery"].AsInt32();
        int totalMastery = data["total_mastery_earned"].AsInt32();
        int mTraining = data["mastery_from_training"].AsInt32();
        int mBattle = data["mastery_from_battle"].AsInt32();
        if (curMastery > totalMastery) return null;
        if (mTraining + mBattle > totalMastery) return null;

        return new UnitSkillProgress
        {
            skill_id = skillId, is_learned = isLearned, skill_level = data["skill_level"].AsInt32(),
            current_mastery = curMastery, total_mastery_earned = totalMastery, is_core = data["is_core"].AsBool(),
            assigned_profession_id = assignedProf, merged_from_skill_ids = mergedIds,
            mastery_from_training = mTraining, mastery_from_battle = mBattle, profession_granted_by = profGrantedBy,
            granted_source_type = grantSourceType, granted_source_id = grantSourceId,
            core_max_growth_claimed = data["core_max_growth_claimed"].AsBool(),
            is_level_trigger_active = data["is_level_trigger_active"].AsBool(),
            is_level_trigger_locked = data["is_level_trigger_locked"].AsBool(),
            bonus_to_hit_from_lock = data["bonus_to_hit_from_lock"].AsInt32(),
        };
    }

    private static StringName _parse_string_name_field(Variant value, bool allowEmpty, out bool ok)
    { ok = false; if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName) return new StringName(""); var p = ProgressionDataUtils.to_string_name(value); if (p == "" && !allowEmpty) return new StringName(""); ok = true; return p; }
    private static Godot.Collections.Array<StringName> _parse_unique_string_name_array(Godot.Collections.Array values) { var r = new Godot.Collections.Array<StringName>(); var s = new Godot.Collections.Dictionary(); foreach (var raw in values) { var p = _parse_string_name_field(raw, false, out bool o); if (!o || s.ContainsKey(p)) return null; s[p] = true; r.Add(p); } return r; }
    private static bool _has_exact_fields(Godot.Collections.Dictionary data, Godot.Collections.Array<string> expected) { if (data.Count != expected.Count) return false; var el = new Godot.Collections.Dictionary(); foreach (string fn in expected) el[fn] = true; foreach (var key in data.Keys) { if (key.VariantType != Variant.Type.String && key.VariantType != Variant.Type.StringName) return false; if (!el.ContainsKey(key.AsString())) return false; if (!el[key.AsString()].AsBool()) return false; el[key.AsString()] = false; } return true; }
}
