using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class CharacterAttributeChangeFact
{
    public StringName AttributeId { get; }
    public string AttributeLabel { get; }
    public int Delta { get; }
    public string ReasonText { get; }
    public int? ProgressDelta { get; }
    public int? ProgressBefore { get; }
    public int? ProgressAfter { get; }
    public int? AttributeBefore { get; }
    public int? AttributeAfter { get; }

    public CharacterAttributeChangeFact(
        StringName attributeId,
        string attributeLabel,
        int delta,
        string reasonText,
        int? progressDelta = null,
        int? progressBefore = null,
        int? progressAfter = null,
        int? attributeBefore = null,
        int? attributeAfter = null
    )
    {
        AttributeId = attributeId;
        AttributeLabel = attributeLabel ?? "";
        Delta = delta;
        ReasonText = reasonText ?? "";
        ProgressDelta = progressDelta;
        ProgressBefore = progressBefore;
        ProgressAfter = progressAfter;
        AttributeBefore = attributeBefore;
        AttributeAfter = attributeAfter;
    }

    public static CharacterAttributeChangeFact PermanentDelta(
        StringName attributeId,
        string attributeLabel,
        int delta,
        string reasonText
    ) => new(attributeId, attributeLabel, delta, reasonText);

    public static CharacterAttributeChangeFact GrowthResult(
        string attributeLabel,
        AttributeGrowthResult growthResult
    )
    {
        if (growthResult == null)
            return null;
        return new CharacterAttributeChangeFact(
            growthResult.AttributeId,
            attributeLabel,
            growthResult.AttributeDelta,
            growthResult.ReasonText,
            growthResult.ProgressDelta,
            growthResult.ProgressBefore,
            growthResult.ProgressAfter,
            growthResult.AttributeBefore,
            growthResult.AttributeAfter
        );
    }

    public GDictionary ToDictionary()
    {
        var result = new GDictionary
        {
            ["attribute_id"] = AttributeId,
            ["attribute_label"] = AttributeLabel,
            ["delta"] = Delta,
            ["reason_text"] = ReasonText,
        };
        if (ProgressDelta.HasValue)
            result["progress_delta"] = ProgressDelta.Value;
        if (ProgressBefore.HasValue)
            result["progress_before"] = ProgressBefore.Value;
        if (ProgressAfter.HasValue)
            result["progress_after"] = ProgressAfter.Value;
        if (AttributeBefore.HasValue)
            result["attribute_before"] = AttributeBefore.Value;
        if (AttributeAfter.HasValue)
            result["attribute_after"] = AttributeAfter.Value;
        return result;
    }

    public static CharacterAttributeChangeFact FromDictionary(GDictionary source)
    {
        if (source == null || source.Count == 0)
            return null;
        return new CharacterAttributeChangeFact(
            ReadStringName(source, "attribute_id"),
            ReadString(source, "attribute_label"),
            ReadInt(source, "delta"),
            ReadString(source, "reason_text"),
            ReadOptionalInt(source, "progress_delta"),
            ReadOptionalInt(source, "progress_before"),
            ReadOptionalInt(source, "progress_after"),
            ReadOptionalInt(source, "attribute_before"),
            ReadOptionalInt(source, "attribute_after")
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

    private static int? ReadOptionalInt(GDictionary source, string key)
    {
        if (!TryRead(source, key, out Variant value))
            return null;
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : null;
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
