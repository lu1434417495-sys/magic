using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class BattleAiActionIntent : RefCounted
{
    private static readonly StringName IntentOffense = "offense";
    private static readonly StringName IntentControl = "control";
    private static readonly StringName IntentSurvival = "survival";
    private static readonly StringName IntentPositioning = "positioning";
    private static readonly StringName IntentEscape = "escape";
    private static readonly StringName IntentWait = "wait";

    private static readonly HashSet<string> ValidIntents = new()
    {
        "offense",
        "control",
        "survival",
        "positioning",
        "escape",
        "wait",
    };

    private static readonly Dictionary<string, StringName> SlotRoleDefaultIntent = new()
    {
        ["offense"] = IntentOffense,
        ["control"] = IntentControl,
        ["survival"] = IntentSurvival,
        ["positioning"] = IntentPositioning,
    };

    public static StringName INTENT_OFFENSE() => IntentOffense;
    public static StringName INTENT_CONTROL() => IntentControl;
    public static StringName INTENT_SURVIVAL() => IntentSurvival;
    public static StringName INTENT_POSITIONING() => IntentPositioning;
    public static StringName INTENT_ESCAPE() => IntentEscape;
    public static StringName INTENT_WAIT() => IntentWait;

    public static bool is_valid(StringName intent)
    {
        return intent != null && ValidIntents.Contains(intent.ToString());
    }

    public static StringName default_from_slot_role(StringName slot_role)
    {
        if (slot_role == null)
        {
            return "";
        }
        return SlotRoleDefaultIntent.TryGetValue(slot_role.ToString(), out StringName intent) ? intent : "";
    }
}
