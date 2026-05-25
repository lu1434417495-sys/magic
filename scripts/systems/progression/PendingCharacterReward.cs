using Godot;

[GlobalClass]
public partial class PendingCharacterReward : RefCounted
{
    private static readonly Godot.Collections.Array<string> TO_DICT_FIELDS = new() { "reward_id", "member_id", "member_name", "source_type", "source_id", "source_label", "summary_text", "entries" };

    public StringName reward_id = "";
    public StringName member_id = "";
    public string member_name = "";
    public StringName source_type = "";
    public StringName source_id = "";
    public string source_label = "";
    public string summary_text = "";
    public Godot.Collections.Array<PendingCharacterRewardEntry> entries = new();

    public bool is_empty()
    {
        if (reward_id == "" || member_id == "" || source_type == "" || source_id == "" || entries.Count == 0) return true;
        foreach (var entry in entries) { if (entry != null && !entry.is_empty()) return false; }
        return true;
    }

    public Godot.Collections.Dictionary to_dict()
    {
        var entryData = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var entry in entries) { if (entry != null) entryData.Add(entry.to_dict()); }
        return new Godot.Collections.Dictionary { { "reward_id", (string)reward_id }, { "member_id", (string)member_id }, { "member_name", member_name }, { "source_type", (string)source_type }, { "source_id", (string)source_id }, { "source_label", source_label }, { "summary_text", summary_text }, { "entries", entryData } };
    }

    public static PendingCharacterReward from_dict(Godot.Collections.Dictionary data)
    {
        if (!_has_exact_fields(data, TO_DICT_FIELDS)) return null;
        var rewardId = _parse_string_name_field(data["reward_id"], false, out bool ok1);
        var memId = _parse_string_name_field(data["member_id"], false, out bool ok2);
        var srcType = _parse_string_name_field(data["source_type"], false, out bool ok3);
        var srcId = _parse_string_name_field(data["source_id"], false, out bool ok4);
        if (!ok1 || !ok2 || !ok3 || !ok4) return null;
        foreach (string tf in new[] { "member_name", "source_label", "summary_text" })
            if (data[tf].VariantType != Variant.Type.String) return null;
        var entriesVar = data["entries"];
        if (entriesVar.VariantType != Variant.Type.Array) return null;

        var parsedEntries = new Godot.Collections.Array<PendingCharacterRewardEntry>();
        foreach (var entryData in entriesVar.AsGodotArray())
        {
            if (entryData.VariantType != Variant.Type.Dictionary) return null;
            var parsed = PendingCharacterRewardEntry.from_dict(entryData.AsGodotDictionary());
            if (parsed == null) return null;
            parsedEntries.Add(parsed);
        }
        if (parsedEntries.Count == 0) return null;

        return new PendingCharacterReward { reward_id = rewardId, member_id = memId, member_name = data["member_name"].AsString(), source_type = srcType, source_id = srcId, source_label = data["source_label"].AsString(), summary_text = data["summary_text"].AsString(), entries = parsedEntries };
    }

    private static StringName _parse_string_name_field(Variant value, bool allowEmpty, out bool ok)
    {
        ok = false;
        if (value.VariantType != Variant.Type.String && value.VariantType != Variant.Type.StringName) return new StringName("");
        var parsed = ProgressionDataUtils.to_string_name(value);
        if (parsed == "" && !allowEmpty) return new StringName("");
        ok = true; return parsed;
    }

    private static bool _has_exact_fields(Godot.Collections.Dictionary data, Godot.Collections.Array<string> expected)
    {
        if (data.Count != expected.Count) return false;
        foreach (string fn in expected) { if (!data.ContainsKey(fn)) return false; }
        return true;
    }

    public static PendingCharacterReward from_variant(Variant rewardVariant)
    {
        if (rewardVariant.VariantType == Variant.Type.Nil) return null;
        if (rewardVariant.VariantType == Variant.Type.Object && rewardVariant.AsGodotObject() is PendingCharacterReward typed)
            return from_dict(typed.to_dict());
        if (rewardVariant.VariantType == Variant.Type.Dictionary) return from_dict(rewardVariant.AsGodotDictionary());
        return null;
    }
}
