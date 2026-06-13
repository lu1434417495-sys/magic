using Godot;

public readonly record struct DeathResolutionContext(StringName DeathSource, int DeathSourcePriority)
{
    public bool HasDeathSource =>
        DeathSource != default && !string.IsNullOrEmpty(DeathSource.ToString());
}

public static class BattleDeathResolutionRules
{
    internal const string DeathSourcePayloadKey = "death_source";
    internal const string DeathSourcePriorityPayloadKey = "death_source_priority";

    private static readonly StringName DeathSourceDamage = "damage";
    private static readonly StringName DeathSourcePowerWordKillExecute = "power_word_kill_execute";

    private const int DeathPriorityNormalFatal = 100;
    private const int DeathPriorityExecuteFatal = 900;

    internal static StringName DamageDeathSource => DeathSourceDamage;

    internal static StringName PowerWordKillExecuteDeathSource => DeathSourcePowerWordKillExecute;

    public static DeathResolutionContext NormalFatalContext() =>
        new(DeathSourceDamage, DeathPriorityNormalFatal);

    public static DeathResolutionContext PowerWordKillExecuteContext() =>
        new(DeathSourcePowerWordKillExecute, DeathPriorityExecuteFatal);

    public static bool IsPowerWordKillExecute(DeathResolutionContext context) =>
        context.DeathSource == DeathSourcePowerWordKillExecute;
}
