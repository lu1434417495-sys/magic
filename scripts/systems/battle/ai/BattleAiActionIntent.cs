using System.Collections.Generic;
using Godot;

public static class BattleAiActionIntent
{
    private static readonly StringName IntentOffense = "offense";
    private static readonly StringName IntentControl = "control";
    private static readonly StringName IntentSurvival = "survival";
    private static readonly StringName IntentPositioning = "positioning";
    private static readonly StringName IntentEscape = "escape";
    private static readonly StringName IntentWait = "wait";

    private static readonly HashSet<string> ValidIntents = new(System.StringComparer.Ordinal)
    {
        "offense",
        "control",
        "survival",
        "positioning",
        "escape",
        "wait",
    };

    private static readonly Dictionary<string, StringName> SlotRoleDefaultIntent =
        new(System.StringComparer.Ordinal)
    {
        ["offense"] = IntentOffense,
        ["control"] = IntentControl,
        ["survival"] = IntentSurvival,
        ["positioning"] = IntentPositioning,
    };

    public static StringName Offense => IntentOffense;

    public static StringName Control => IntentControl;

    public static StringName Survival => IntentSurvival;

    public static StringName Positioning => IntentPositioning;

    public static StringName Escape => IntentEscape;

    public static StringName Wait => IntentWait;

    public static bool IsValid(StringName intent)
    {
        return intent != null && ValidIntents.Contains(intent.ToString());
    }

    public static StringName DefaultFromSlotRole(StringName slotRole)
    {
        if (slotRole == null)
        {
            return "";
        }
        return SlotRoleDefaultIntent.TryGetValue(slotRole.ToString(), out StringName intent)
            ? intent
            : "";
    }
}
