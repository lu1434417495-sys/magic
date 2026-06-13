using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class CharacterKnowledgeChangeFact
{
    public StringName KnowledgeId { get; }
    public string KnowledgeLabel { get; }
    public string ReasonText { get; }

    public CharacterKnowledgeChangeFact(
        StringName knowledgeId,
        string knowledgeLabel,
        string reasonText
    )
    {
        KnowledgeId = knowledgeId;
        KnowledgeLabel = knowledgeLabel ?? "";
        ReasonText = reasonText ?? "";
    }

    public GDictionary ToDictionary() =>
        new()
        {
            ["knowledge_id"] = KnowledgeId,
            ["knowledge_label"] = KnowledgeLabel,
            ["reason_text"] = ReasonText,
        };

    public static CharacterKnowledgeChangeFact FromDictionary(GDictionary source)
    {
        if (source == null || source.Count == 0)
            return null;
        return new CharacterKnowledgeChangeFact(
            ReadStringName(source, "knowledge_id"),
            ReadString(source, "knowledge_label"),
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

    private static bool TryRead(GDictionary source, string key, out Variant value)
    {
        value = default;
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
            return false;
        value = source[key];
        return value.VariantType != Variant.Type.Nil;
    }
}
