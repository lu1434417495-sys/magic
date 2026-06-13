using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class CharacterMasteryChangeFact
{
    public StringName SkillId { get; }
    public string SkillName { get; }
    public int MasteryAmount { get; }
    public StringName SourceType { get; }
    public string SourceLabel { get; }
    public string ReasonText { get; }

    public CharacterMasteryChangeFact(
        StringName skillId,
        string skillName,
        int masteryAmount,
        StringName sourceType,
        string sourceLabel,
        string reasonText
    )
    {
        SkillId = skillId;
        SkillName = skillName ?? "";
        MasteryAmount = masteryAmount;
        SourceType = sourceType;
        SourceLabel = sourceLabel ?? "";
        ReasonText = reasonText ?? "";
    }

    public GDictionary ToDictionary() =>
        new()
        {
            ["skill_id"] = SkillId,
            ["skill_name"] = SkillName,
            ["mastery_amount"] = MasteryAmount,
            ["source_type"] = SourceType,
            ["source_label"] = SourceLabel,
            ["reason_text"] = ReasonText,
        };

    public static CharacterMasteryChangeFact FromDictionary(GDictionary source)
    {
        if (source == null || source.Count == 0)
            return null;
        return new CharacterMasteryChangeFact(
            ReadStringName(source, "skill_id"),
            ReadString(source, "skill_name"),
            ReadInt(source, "mastery_amount"),
            ReadStringName(source, "source_type"),
            ReadString(source, "source_label"),
            ReadString(source, "reason_text")
        );
    }

    private static StringName ReadStringName(
        GDictionary source,
        string key,
        StringName fallback = default
    )
    {
        if (!TryRead(source, key, out Variant value))
            return fallback ?? "";
        return ProgressionDataUtils.to_string_name(value);
    }

    private static string ReadString(GDictionary source, string key, string fallback = "")
    {
        if (!TryRead(source, key, out Variant value))
            return fallback;
        string text = value.ToString();
        return string.IsNullOrEmpty(text) || text == "<null>" ? fallback : text;
    }

    private static int ReadInt(GDictionary source, string key, int fallback = 0)
    {
        if (!TryRead(source, key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool TryRead(GDictionary source, string key, out Variant value)
    {
        value = default;
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return false;
        value = source[key];
        return value.VariantType != Variant.Type.Nil;
    }
}
