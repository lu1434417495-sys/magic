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

    public static bool is_power_word_kill_execute(Variant context)
    {
        if (context.VariantType != Variant.Type.Dictionary)
        {
            return false;
        }
        GDictionary contextData = context.AsGodotDictionary();
        Variant sourceValue = GdInterop.TryGet(contextData, KEY_DEATH_SOURCE(), out Variant rawValue)
            ? rawValue
            : Variant.From("");
        return ToStringNameLikeProgressionDataUtils(sourceValue) == DEATH_SOURCE_POWER_WORD_KILL_EXECUTE();
    }

    private static StringName ToStringNameLikeProgressionDataUtils(Variant value)
    {
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
}
