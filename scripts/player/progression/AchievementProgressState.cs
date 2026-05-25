using Godot;

[GlobalClass]
public partial class AchievementProgressState : RefCounted
{
    public StringName achievement_id = "";
    public int current_value;
    public bool is_unlocked;
    public int unlocked_at_unix_time;

    public Godot.Collections.Dictionary to_dict() => new()
    {
        {"achievement_id", (string)achievement_id},
        {"current_value", current_value},
        {"is_unlocked", is_unlocked},
        {"unlocked_at_unix_time", unlocked_at_unix_time},
    };

    public static AchievementProgressState from_dict(Godot.Collections.Dictionary data)
    {
        if (!_has_exact_fields(data, new Godot.Collections.Array<string> { "achievement_id", "current_value", "is_unlocked", "unlocked_at_unix_time" }))
            return null;
        var aid = ProgressionDataUtils.to_string_name(data["achievement_id"]);
        if ((string)aid == "") return null;
        var av = data["achievement_id"];
        if (av.VariantType != Variant.Type.String && av.VariantType != Variant.Type.StringName) return null;
        var cv = data["current_value"];
        if (cv.VariantType != Variant.Type.Int || cv.AsInt32() < 0) return null;
        var iu = data["is_unlocked"];
        if (iu.VariantType != Variant.Type.Bool) return null;
        var ua = data["unlocked_at_unix_time"];
        if (ua.VariantType != Variant.Type.Int || ua.AsInt32() < 0) return null;
        return new AchievementProgressState
        {
            achievement_id = aid,
            current_value = cv.AsInt32(),
            is_unlocked = iu.AsBool(),
            unlocked_at_unix_time = ua.AsInt32(),
        };
    }

    private static bool _has_exact_fields(Godot.Collections.Dictionary data, Godot.Collections.Array<string> expected)
    {
        if (data.Count != expected.Count) return false;
        foreach (string fn in expected)
            if (!data.ContainsKey(fn)) return false;
        return true;
    }
}
