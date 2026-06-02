using Godot;

public readonly record struct DeathResolutionContext(StringName DeathSource, int DeathSourcePriority)
{
    public bool HasDeathSource =>
        DeathSource != default && !string.IsNullOrEmpty(DeathSource.ToString());
}

public static class BattleDeathResolutionRules
{
    public const string DeathSourcePayloadKey = "death_source";
    public const string DeathSourcePriorityPayloadKey = "death_source_priority";

    private static readonly StringName DeathSourceDamage = "damage";
    private static readonly StringName DeathSourcePowerWordKillExecute = "power_word_kill_execute";

    public const int DeathPriorityNormalFatal = 100;
    public const int DeathPriorityExecuteFatal = 900;

    public static StringName DamageDeathSource => DeathSourceDamage;

    public static StringName PowerWordKillExecuteDeathSource => DeathSourcePowerWordKillExecute;

    public static DeathResolutionContext NormalFatalContext() =>
        new(DeathSourceDamage, DeathPriorityNormalFatal);

    public static DeathResolutionContext PowerWordKillExecuteContext() =>
        new(DeathSourcePowerWordKillExecute, DeathPriorityExecuteFatal);

    public static bool IsPowerWordKillExecute(DeathResolutionContext context) =>
        context.DeathSource == DeathSourcePowerWordKillExecute;
}
