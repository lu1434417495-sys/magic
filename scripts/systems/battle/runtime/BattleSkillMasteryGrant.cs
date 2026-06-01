using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BattleSkillMasteryGrant
{
    public StringName MemberId = "";
    public StringName SkillId = "";
    public int Amount;
    public StringName SourceType = "";
    public string SourceLabel = "";
    public string ReasonText = "";
    public bool AllowUnlocks = true;
    public bool RecordNearDeathUnbrokenManual;

    public bool IsValid =>
        MemberId != ""
        && SkillId != ""
        && SourceType != ""
        && Amount > 0;

    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["member_id"] = MemberId,
            ["skill_id"] = SkillId,
            ["amount"] = Amount,
            ["source_type"] = SourceType,
            ["source_label"] = SourceLabel,
            ["reason_text"] = ReasonText,
            ["allow_unlocks"] = AllowUnlocks,
            ["record_near_death_unbroken_manual"] = RecordNearDeathUnbrokenManual,
        };
    }

    public static BattleSkillMasteryGrant FromDictionary(GDictionary source)
    {
        if (source == null || source.Count == 0)
        {
            return new BattleSkillMasteryGrant();
        }
        return new BattleSkillMasteryGrant
        {
            MemberId = ReadStringName(source, "member_id"),
            SkillId = ReadStringName(source, "skill_id"),
            Amount = ReadInt(source, "amount"),
            SourceType = ReadStringName(source, "source_type"),
            SourceLabel = ReadString(source, "source_label"),
            ReasonText = ReadString(source, "reason_text"),
            AllowUnlocks = ReadBool(source, "allow_unlocks", true),
            RecordNearDeathUnbrokenManual = ReadBool(
                source,
                "record_near_death_unbroken_manual"
            ),
        };
    }

    private static StringName ReadStringName(
        GDictionary source,
        string key,
        StringName fallback = default
    )
    {
        if (!HasKey(source, key))
            return fallback ?? "";
        if (source.ContainsKey(key))
            return ProgressionDataUtils.to_string_name(source[key]);
        return ProgressionDataUtils.to_string_name(source[new StringName(key)]);
    }

    private static string ReadString(GDictionary source, string key, string fallback = "")
    {
        if (!HasKey(source, key))
            return fallback;
        string text = source.ContainsKey(key)
            ? source[key].ToString()
            : source[new StringName(key)].ToString();
        return string.IsNullOrEmpty(text) || text == "<null>" ? fallback : text;
    }

    private static int ReadInt(GDictionary source, string key, int fallback = 0)
    {
        if (!HasKey(source, key))
            return fallback;
        return source.ContainsKey(key)
            ? source[key].AsInt32()
            : source[new StringName(key)].AsInt32();
    }

    private static bool ReadBool(GDictionary source, string key, bool fallback = false)
    {
        if (!HasKey(source, key))
            return fallback;
        return source.ContainsKey(key)
            ? source[key].AsBool()
            : source[new StringName(key)].AsBool();
    }

    private static bool HasKey(GDictionary source, string key)
    {
        if (source == null || key == null)
            return false;
        return source.ContainsKey(key) || source.ContainsKey(new StringName(key));
    }
}
