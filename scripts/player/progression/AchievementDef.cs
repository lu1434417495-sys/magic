using Godot;

[GlobalClass]
public partial class AchievementDef : RefCounted
{
    public StringName achievement_id = "";
    public string display_name = "";
    public string description = "";
    public StringName event_type = "";
    public StringName subject_id = "";
    public int threshold;
    public Godot.Collections.Array<AchievementRewardDef> rewards = new();

    public bool MatchesEvent(StringName target_event_type, StringName target_subject_id = default)
    {
        if ((string)achievement_id == "" || (string)event_type == "" || threshold <= 0)
            return false;
        if (event_type != target_event_type)
            return false;
        return (string)subject_id == "" || subject_id == target_subject_id;
    }

    public bool IsEmpty() =>
        (string)achievement_id == "" || (string)event_type == "" || threshold <= 0;

    public Godot.Collections.Dictionary ToDictionary()
    {
        var rd = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var r in rewards)
            if (r != null)
                rd.Add(r.ToDictionary());
        return new Godot.Collections.Dictionary
        {
            { "achievement_id", (string)achievement_id },
            { "display_name", display_name },
            { "description", description },
            { "event_type", (string)event_type },
            { "subject_id", (string)subject_id },
            { "threshold", threshold },
            { "rewards", rd },
        };
    }

    public static AchievementDef FromDictionary(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            return null;
        if (!_has_exact_serialized_fields(payload))
            return null;
        var aid = _parse_string_name_field(payload["achievement_id"], false);
        var et = _parse_string_name_field(payload["event_type"], false);
        var si = _parse_string_name_field(payload["subject_id"], true);
        if (aid == null || et == null || si == null)
            return null;
        if (
            payload["display_name"].VariantType != Variant.Type.String
            || payload["description"].VariantType != Variant.Type.String
        )
            return null;
        if (
            payload["threshold"].VariantType != Variant.Type.Int
            || payload["threshold"].AsInt32() <= 0
        )
            return null;
        if (payload["rewards"].VariantType != Variant.Type.Array)
            return null;
        var def = new AchievementDef
        {
            achievement_id = aid,
            display_name = payload["display_name"].AsString(),
            description = payload["description"].AsString(),
            event_type = et,
            subject_id = si,
            threshold = payload["threshold"].AsInt32(),
        };
        foreach (var rv in payload["rewards"].AsGodotArray())
        {
            if (rv.VariantType != Variant.Type.Dictionary)
                return null;
            var reward = AchievementRewardDef.FromDictionary(rv.AsGodotDictionary());
            if (reward == null)
                return null;
            def.rewards.Add(reward);
        }
        return def;
    }

    private static bool _has_exact_serialized_fields(Godot.Collections.Dictionary payload) =>
        _hfs(
            payload,
            new Godot.Collections.Array<string>
            {
                "achievement_id",
                "display_name",
                "description",
                "event_type",
                "subject_id",
                "threshold",
                "rewards",
            }
        );

    private static bool _hfs(Godot.Collections.Dictionary d, Godot.Collections.Array<string> f)
    {
        if (d.Count != f.Count)
            return false;
        foreach (string n in f)
            if (!d.ContainsKey(n))
                return false;
        return true;
    }

    private static StringName _parse_string_name_field(object rawValue, bool allow_empty)
    {
        if (rawValue is Variant value)
        {
            var vt = value.VariantType;
            if (vt != Variant.Type.String && vt != Variant.Type.StringName)
                return null;
        }
        else if (rawValue is not string && rawValue is not StringName)
        {
            return null;
        }
        var pv = ProgressionDataUtils.to_string_name(rawValue);
        if ((string)pv == "" && !allow_empty)
            return null;
        return pv;
    }
}
