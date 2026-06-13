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

    private static readonly IReadOnlyDictionary<
        RaceTraitEffectKind,
        IReadOnlyDictionary<TraitTriggerKind, string>
    >
        DISPATCH_TRIGGER_TYPES =
            new Dictionary<RaceTraitEffectKind, IReadOnlyDictionary<TraitTriggerKind, string>>
    {
        {
            RaceTraitEffectKind.HalflingLuck,
            new Dictionary<TraitTriggerKind, string>
            {
                { TraitTriggerKind.OnNaturalOne, DispatchHalflingLuck },
            }
        },
        {
            RaceTraitEffectKind.SavageAttacks,
            new Dictionary<TraitTriggerKind, string>
            {
                { TraitTriggerKind.OnCrit, DispatchSavageAttacks },
            }
        },
        {
            RaceTraitEffectKind.RelentlessEndurance,
            new Dictionary<TraitTriggerKind, string>
            {
                { TraitTriggerKind.OnFatalDamage, DispatchRelentlessEndurance },
            }
        },
    };

    internal static IReadOnlyDictionary<
        RaceTraitEffectKind,
        IReadOnlyDictionary<TraitTriggerKind, string>
    >
        GetDispatchTriggerTypes() => DISPATCH_TRIGGER_TYPES;

    public static bool HasDispatchForTraitTrigger(StringName traitId, StringName triggerType)
    {
        if (traitId == "" || triggerType == "")
            return false;

        RaceTraitEffectKind traitKind = RaceTraitDef.ToEffectKind(traitId);
        TraitTriggerKind triggerKind = ToTriggerKind(triggerType);
        if (traitKind == RaceTraitEffectKind.Unknown || triggerKind == TraitTriggerKind.Unknown)
            return false;

        if (!DISPATCH_TRIGGER_TYPES.TryGetValue(traitKind, out var dispatchEntry))
            return false;

        return dispatchEntry.ContainsKey(triggerKind);
    }

    public static string GetDispatchKey(StringName traitId, StringName triggerType)
    {
        if (traitId == "" || triggerType == "")
            return "";

        RaceTraitEffectKind traitKind = RaceTraitDef.ToEffectKind(traitId);
        TraitTriggerKind triggerKind = ToTriggerKind(triggerType);
        if (traitKind == RaceTraitEffectKind.Unknown || triggerKind == TraitTriggerKind.Unknown)
            return "";

        if (!DISPATCH_TRIGGER_TYPES.TryGetValue(traitKind, out var dispatchEntry))
            return "";

        if (!dispatchEntry.TryGetValue(triggerKind, out string dispatchKey))
            return "";

        return dispatchKey;
    }

    public static IReadOnlyList<StringName> GetDispatchTraitIds()
    {
        var traitIds = new List<StringName>();

        foreach (RaceTraitEffectKind traitKind in DISPATCH_TRIGGER_TYPES.Keys)
        {
            StringName traitId = RaceTraitDef.ToStringName(traitKind);
            if (traitId != "")
                traitIds.Add(traitId);
        }

        traitIds.Sort((left, right) =>
            string.CompareOrdinal(left.ToString(), right.ToString())
        );

        return traitIds;
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
