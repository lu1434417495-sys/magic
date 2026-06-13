using Godot;

internal enum BattleAiIntent
{
    Unknown,
    Offense,
    Control,
    Survival,
    Positioning,
    Escape,
    Wait,
}

internal static class BattleAiActionIntent
{
    private static readonly StringName IntentOffense = "offense";
    private static readonly StringName IntentControl = "control";
    private static readonly StringName IntentSurvival = "survival";
    private static readonly StringName IntentPositioning = "positioning";
    private static readonly StringName IntentEscape = "escape";
    private static readonly StringName IntentWait = "wait";

    internal static StringName Offense => IntentOffense;
    internal static StringName Control => IntentControl;
    internal static StringName Survival => IntentSurvival;
    internal static StringName Positioning => IntentPositioning;
    internal static StringName Escape => IntentEscape;
    internal static StringName Wait => IntentWait;

    internal static BattleAiIntent ToKind(StringName intent)
    {
        return intent.ToString() switch
        {
            "offense" => BattleAiIntent.Offense,
            "control" => BattleAiIntent.Control,
            "survival" => BattleAiIntent.Survival,
            "positioning" => BattleAiIntent.Positioning,
            "escape" => BattleAiIntent.Escape,
            "wait" => BattleAiIntent.Wait,
            _ => BattleAiIntent.Unknown,
        };
    }

    internal static StringName ToStringName(BattleAiIntent intent) =>
        intent switch
        {
            BattleAiIntent.Offense => IntentOffense,
            BattleAiIntent.Control => IntentControl,
            BattleAiIntent.Survival => IntentSurvival,
            BattleAiIntent.Positioning => IntentPositioning,
            BattleAiIntent.Escape => IntentEscape,
            BattleAiIntent.Wait => IntentWait,
            _ => "",
        };

    internal static bool IsValid(StringName intent) => ToKind(intent) != BattleAiIntent.Unknown;

    internal static StringName DefaultFromSlotRole(StringName slotRole)
    {
        return EnemyAiGenerationSlotDef.ToSlotRole(slotRole) switch
        {
            EnemyAiGenerationSlotRole.Offense => IntentOffense,
            EnemyAiGenerationSlotRole.Control => IntentControl,
            EnemyAiGenerationSlotRole.Survival => IntentSurvival,
            EnemyAiGenerationSlotRole.Positioning => IntentPositioning,
            _ => "",
        };
    }
}
