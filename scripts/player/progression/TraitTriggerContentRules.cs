using Godot;
using System.Collections.Generic;

internal enum TraitTriggerKind
{
    Unknown = 0,
    Passive,
    OnNaturalOne,
    OnCrit,
    OnFatalDamage,
    OnBattleStart,
    OnTurnStart,
}

internal readonly struct TraitTriggerDispatchRule
{
    public readonly TraitEffectKind TraitKind;
    public readonly TraitTriggerKind TriggerKind;
    public readonly string DispatchKey;

    public TraitTriggerDispatchRule(
        TraitEffectKind traitKind,
        TraitTriggerKind triggerKind,
        string dispatchKey
    )
    {
        TraitKind = traitKind;
        TriggerKind = triggerKind;
        DispatchKey = dispatchKey ?? "";
    }
}

public static class TraitTriggerContentRules
{
    private static readonly StringName TriggerPassive = "passive";

    private static readonly StringName TriggerOnNaturalOne = "on_natural_one";

    private static readonly StringName TriggerOnCrit = "on_crit";

    private static readonly StringName TriggerOnFatalDamage = "on_fatal_damage";

    private static readonly StringName TriggerOnBattleStart = "on_battle_start";

    private static readonly StringName TriggerOnTurnStart = "on_turn_start";

    private static readonly StringName TraitHalflingLuck = "halfling_luck";

    private static readonly StringName TraitSavageAttacks = "savage_attacks";

    private static readonly StringName TraitRelentlessEndurance = "relentless_endurance";

    private const string DispatchHalflingLuck = "halfling_luck";

    private const string DispatchSavageAttacks = "savage_attacks";

    private const string DispatchRelentlessEndurance = "relentless_endurance";

    private static readonly TraitTriggerDispatchRule[] DISPATCH_TRIGGER_RULES =
    {
        new(TraitEffectKind.HalflingLuck, TraitTriggerKind.OnNaturalOne, DispatchHalflingLuck),
        new(TraitEffectKind.SavageAttacks, TraitTriggerKind.OnCrit, DispatchSavageAttacks),
        new(
            TraitEffectKind.RelentlessEndurance,
            TraitTriggerKind.OnFatalDamage,
            DispatchRelentlessEndurance
        ),
    };

    internal static IReadOnlyList<TraitTriggerDispatchRule> GetDispatchTriggerRules() =>
        DISPATCH_TRIGGER_RULES;

    public static bool HasDispatchForTraitTrigger(StringName traitId, StringName triggerType)
    {
        if (traitId == "" || triggerType == "")
            return false;

        TraitEffectKind traitKind = TraitContentRules.ToEffectKind(traitId);
        TraitTriggerKind triggerKind = ToTriggerKind(triggerType);
        if (traitKind == TraitEffectKind.Unknown || triggerKind == TraitTriggerKind.Unknown)
            return false;

        return GetDispatchKey(traitKind, triggerKind) != "";
    }

    public static string GetDispatchKey(StringName traitId, StringName triggerType)
    {
        if (traitId == "" || triggerType == "")
            return "";

        TraitEffectKind traitKind = TraitContentRules.ToEffectKind(traitId);
        TraitTriggerKind triggerKind = ToTriggerKind(triggerType);
        if (traitKind == TraitEffectKind.Unknown || triggerKind == TraitTriggerKind.Unknown)
            return "";

        return GetDispatchKey(traitKind, triggerKind);
    }

    public static IReadOnlyList<StringName> GetDispatchTraitIds()
    {
        var traitIds = new List<StringName>();

        foreach (TraitTriggerDispatchRule rule in DISPATCH_TRIGGER_RULES)
        {
            StringName traitId = TraitContentRules.ToStringName(rule.TraitKind);
            if (traitId != "" && !ContainsTraitId(traitIds, traitId))
                traitIds.Add(traitId);
        }

        traitIds.Sort((left, right) =>
            string.CompareOrdinal(left.ToString(), right.ToString())
        );

        return traitIds;
    }

    private static string GetDispatchKey(TraitEffectKind traitKind, TraitTriggerKind triggerKind)
    {
        foreach (TraitTriggerDispatchRule rule in DISPATCH_TRIGGER_RULES)
        {
            if (rule.TraitKind == traitKind && rule.TriggerKind == triggerKind)
                return rule.DispatchKey;
        }

        return "";
    }

    private static bool ContainsTraitId(List<StringName> traitIds, StringName traitId)
    {
        foreach (StringName existing in traitIds)
        {
            if (existing == traitId)
                return true;
        }

        return false;
    }

    internal static TraitTriggerKind ToTriggerKind(StringName value)
    {
        if (value == TriggerPassive)
            return TraitTriggerKind.Passive;
        if (value == TriggerOnNaturalOne)
            return TraitTriggerKind.OnNaturalOne;
        if (value == TriggerOnCrit)
            return TraitTriggerKind.OnCrit;
        if (value == TriggerOnFatalDamage)
            return TraitTriggerKind.OnFatalDamage;
        if (value == TriggerOnBattleStart)
            return TraitTriggerKind.OnBattleStart;
        if (value == TriggerOnTurnStart)
            return TraitTriggerKind.OnTurnStart;
        return TraitTriggerKind.Unknown;
    }

    internal static StringName ToStringName(TraitTriggerKind value)
    {
        return value switch
        {
            TraitTriggerKind.Passive => TriggerPassive,
            TraitTriggerKind.OnNaturalOne => TriggerOnNaturalOne,
            TraitTriggerKind.OnCrit => TriggerOnCrit,
            TraitTriggerKind.OnFatalDamage => TriggerOnFatalDamage,
            TraitTriggerKind.OnBattleStart => TriggerOnBattleStart,
            TraitTriggerKind.OnTurnStart => TriggerOnTurnStart,
            _ => "",
        };
    }
}
