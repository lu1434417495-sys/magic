using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleDeathResolutionRules : RefCounted
{
    public static string KEY_DEATH_SOURCE() => "death_source";

    public static string KEY_DEATH_SOURCE_PRIORITY() => "death_source_priority";

    public static StringName DEATH_SOURCE_DAMAGE() => "damage";

    public static StringName DEATH_SOURCE_POWER_WORD_KILL_EXECUTE() => "power_word_kill_execute";

    public static int DEATH_PRIORITY_NORMAL_FATAL() => 100;

    public static int DEATH_PRIORITY_EXECUTE_FATAL() => 900;

    public static bool is_power_word_kill_execute(GDictionary context)
    {
        if (context == null)
        {
            return false;
        }
        var sourceValue = TryGetValue(context, KEY_DEATH_SOURCE(), out Variant rawValue)
            ? rawValue
            : Variant.From("");
        return ToStringNameLikeProgressionDataUtils(sourceValue) == DEATH_SOURCE_POWER_WORD_KILL_EXECUTE();
    }

    public static bool is_power_word_kill_execute_without_context() => false;

    private static StringName ToStringNameLikeProgressionDataUtils(object rawValue)
    {
        if (rawValue is not Variant value)
        {
            string rawText = rawValue?.ToString()?.Trim() ?? "";
            return string.IsNullOrEmpty(rawText) || rawText == "<null>" ? "" : new StringName(rawText);
        }
        if (value.VariantType == Variant.Type.Nil)
        {
            return "";
        }

        string text = value.ToString();
        if (text == "<null>")
        {
            return "";
        }

        string trimmed = text.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return "";
        }

        return new StringName(trimmed);
    }

    private static bool TryGetValue(GDictionary source, object key, out Variant value)
    {
        if (source == null)
        {
            value = default;
            return false;
        }
        Variant variantKey = key switch
        {
            Variant variant => variant,
            StringName stringName => Variant.From(stringName),
            string text => Variant.From(text),
            _ => Variant.From(key?.ToString() ?? ""),
        };
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return true;
        }
        if (key is string stringKey)
        {
            var stringNameKey = new StringName(stringKey);
            if (source.ContainsKey(stringNameKey))
            {
                value = source[stringNameKey];
                return true;
            }
        }
        else if (key is StringName stringNameKey)
        {
            string keyText = stringNameKey.ToString();
            if (source.ContainsKey(keyText))
            {
                value = source[keyText];
                return true;
            }
        }
        value = default;
        return false;
    }
}
