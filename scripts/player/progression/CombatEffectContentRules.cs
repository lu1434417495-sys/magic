using Godot;

internal static class CombatEffectContentRules
{
    private static readonly StringName TriggerEventCriticalHit = "critical_hit";
    private static readonly StringName TriggerEventOrdinaryHit = "ordinary_hit";
    private static readonly StringName TriggerEventSecondaryHit = "secondary_hit";
    private static readonly StringName TriggerConditionBattleStart = "battle_start";
    private static readonly StringName TriggerConditionOnFatalDamage = "on_fatal_damage";
    private static readonly StringName LifetimePolicyTimed = "timed";
    private static readonly StringName LifetimePolicyBattle = "battle";

    internal const double MinJumpArcRatio = 0.15;

    internal static CombatEffectTriggerEvent ToTriggerEvent(StringName value)
    {
        if (value == "")
            return CombatEffectTriggerEvent.None;
        if (value == TriggerEventCriticalHit)
            return CombatEffectTriggerEvent.CriticalHit;
        if (value == TriggerEventOrdinaryHit)
            return CombatEffectTriggerEvent.OrdinaryHit;
        if (value == TriggerEventSecondaryHit)
            return CombatEffectTriggerEvent.SecondaryHit;
        return CombatEffectTriggerEvent.Unknown;
    }

    internal static CombatEffectTriggerCondition ToTriggerCondition(StringName value)
    {
        if (value == "")
            return CombatEffectTriggerCondition.None;
        if (value == TriggerConditionBattleStart)
            return CombatEffectTriggerCondition.BattleStart;
        if (value == TriggerConditionOnFatalDamage)
            return CombatEffectTriggerCondition.OnFatalDamage;
        return CombatEffectTriggerCondition.Unknown;
    }

    internal static CombatEffectLifetimePolicy ToLifetimePolicy(StringName value)
    {
        if (value == LifetimePolicyTimed)
            return CombatEffectLifetimePolicy.Timed;
        if (value == LifetimePolicyBattle)
            return CombatEffectLifetimePolicy.Battle;
        return CombatEffectLifetimePolicy.Unknown;
    }

    internal static StringName ToStringName(CombatEffectTriggerEvent triggerEvent)
    {
        return triggerEvent switch
        {
            CombatEffectTriggerEvent.None => "",
            CombatEffectTriggerEvent.CriticalHit => TriggerEventCriticalHit,
            CombatEffectTriggerEvent.OrdinaryHit => TriggerEventOrdinaryHit,
            CombatEffectTriggerEvent.SecondaryHit => TriggerEventSecondaryHit,
            _ => "",
        };
    }

    internal static StringName ToStringName(CombatEffectTriggerCondition triggerCondition)
    {
        return triggerCondition switch
        {
            CombatEffectTriggerCondition.None => "",
            CombatEffectTriggerCondition.BattleStart => TriggerConditionBattleStart,
            CombatEffectTriggerCondition.OnFatalDamage => TriggerConditionOnFatalDamage,
            _ => "",
        };
    }

    internal static StringName ToStringName(CombatEffectLifetimePolicy lifetimePolicy)
    {
        return lifetimePolicy switch
        {
            CombatEffectLifetimePolicy.Timed => LifetimePolicyTimed,
            CombatEffectLifetimePolicy.Battle => LifetimePolicyBattle,
            _ => "",
        };
    }
}
