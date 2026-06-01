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
        return ReadStringName(context, KEY_DEATH_SOURCE()) == DEATH_SOURCE_POWER_WORD_KILL_EXECUTE();
    }

    public static bool is_power_word_kill_execute_without_context() => false;

    private static StringName ReadStringName(GDictionary source, string key)
    {
        if (source == null || string.IsNullOrEmpty(key))
        {
            return "";
        }
        if (source.ContainsKey(key))
        {
            return ProgressionDataUtils.to_string_name(source[key]);
        }
        var stringNameKey = new StringName(key);
        if (source.ContainsKey(stringNameKey))
        {
            return ProgressionDataUtils.to_string_name(source[stringNameKey]);
        }
        return "";
    }
}
