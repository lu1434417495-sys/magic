using Godot;

[GlobalClass]
public partial class UnitProfessionProgress : RefCounted
{
    private static readonly Godot.Collections.Array<string> TO_DICT_FIELDS = new() { "profession_id", "rank", "is_active", "is_hidden", "core_skill_ids", "granted_skill_ids", "promotion_history", "inactive_reason" };

    public StringName profession_id = "";
    public int rank;
    public bool is_active = true;
    public bool is_hidden;
    public Godot.Collections.Array<StringName> core_skill_ids = new();
    public Godot.Collections.Array<StringName> granted_skill_ids = new();
    public Godot.Collections.Array<ProfessionPromotionRecord> promotion_history = new();
    public StringName inactive_reason = "";

    public void add_core_skill(StringName skillId) { if (skillId != "" && !core_skill_ids.Contains(skillId)) core_skill_ids.Add(skillId); }
    public void remove_core_skill(StringName skillId) => core_skill_ids.Remove(skillId);
    public void add_granted_skill(StringName skillId) { if (skillId != "" && !granted_skill_ids.Contains(skillId)) granted_skill_ids.Add(skillId); }
    public void add_promotion_record(ProfessionPromotionRecord record) { if (record != null) promotion_history.Add(record); }

    public Godot.Collections.Dictionary to_dict()
    {
        var promoData = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var r in promotion_history) { if (r != null) promoData.Add(r.to_dict()); }
        return new Godot.Collections.Dictionary { { "profession_id", (string)profession_id }, { "rank", rank }, { "is_active", is_active }, { "is_hidden", is_hidden }, { "core_skill_ids", ProgressionDataUtils.string_name_array_to_string_array(core_skill_ids) }, { "granted_skill_ids", ProgressionDataUtils.string_name_array_to_string_array(granted_skill_ids) }, { "promotion_history", promoData }, { "inactive_reason", (string)inactive_reason } };
    }

    public static UnitProfessionProgress from_dict(Godot.Collections.Dictionary data)
    {
        if (!_has_exact_fields(data, TO_DICT_FIELDS)) return null;
        if (data["core_skill_ids"].VariantType != Variant.Type.Array) return null;
        if (data["granted_skill_ids"].VariantType != Variant.Type.Array) return null;
        if (data["promotion_history"].VariantType != Variant.Type.Array) return null;
        var profId = _parse_string_name_field(data["profession_id"], false, out bool ok1);
        if (!ok1) return null;
        var rankVar = data["rank"];
        if (rankVar.VariantType != Variant.Type.Int || rankVar.AsInt32() < 0) return null;
        if (data["is_active"].VariantType != Variant.Type.Bool || data["is_hidden"].VariantType != Variant.Type.Bool) return null;
        bool isActive = data["is_active"].AsBool();
        string inactiveReasonText = data.ContainsKey("inactive_reason") ? data["inactive_reason"].AsString() : "";
        if (rankVar.AsInt32() <= 0 && isActive) return null;
        if (isActive && inactiveReasonText != "") return null;
        var coreIds = _parse_unique_string_name_array(data["core_skill_ids"].AsGodotArray());
        if (coreIds == null) return null;
        var grantedIds = _parse_unique_string_name_array(data["granted_skill_ids"].AsGodotArray());
        if (grantedIds == null) return null;
        var inactiveReason = _parse_string_name_field(data["inactive_reason"], true, out bool ok2);
        if (!ok2) return null;

        var progress = new UnitProfessionProgress { profession_id = profId, rank = rankVar.AsInt32(), is_active = isActive, is_hidden = data["is_hidden"].AsBool(), core_skill_ids = coreIds, granted_skill_ids = grantedIds, inactive_reason = inactiveReason };
        foreach (var recordData in data["promotion_history"].AsGodotArray())
        {
            if (recordData.VariantType != Variant.Type.Dictionary) return null;
            var promoRecord = ProfessionPromotionRecord.from_dict(recordData.AsGodotDictionary());
            if (promoRecord == null) return null;
            progress.promotion_history.Add(promoRecord);
        }
        return progress;
    }

    private static bool _has_exact_fields(Godot.Collections.Dictionary data, Godot.Collections.Array<string> expected) { if (data.Count != expected.Count) return false; foreach (string fn in expected) { if (!data.ContainsKey(fn)) return false; } return true; }
    private static StringName _parse_string_name_field(Variant value, bool allowEmpty, out bool ok) { ok = false; if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName) return new StringName(""); var parsed = ProgressionDataUtils.to_string_name(value); if (parsed == "" && !allowEmpty) return new StringName(""); ok = true; return parsed; }
    private static Godot.Collections.Array<StringName> _parse_unique_string_name_array(Godot.Collections.Array values) { var result = new Godot.Collections.Array<StringName>(); var seen = new Godot.Collections.Dictionary(); foreach (var raw in values) { var parsed = _parse_string_name_field(raw, false, out bool ok); if (!ok || seen.ContainsKey(parsed)) return null; seen[parsed] = true; result.Add(parsed); } return result; }
}
