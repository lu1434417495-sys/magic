using Godot;
using System;
using System.Collections.Generic;

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

    private static readonly HashSet<StringName> VALID_TRIGGER_TYPES = new()
    {
        TriggerPassive,
        TriggerOnNaturalOne,
        TriggerOnCrit,
        TriggerOnFatalDamage,
        TriggerOnBattleStart,
        TriggerOnTurnStart,
    };

    private static readonly IReadOnlyDictionary<StringName, IReadOnlyDictionary<StringName, string>>
        DISPATCH_TRIGGER_TYPES =
            new Dictionary<StringName, IReadOnlyDictionary<StringName, string>>
    {
        {
            TraitHalflingLuck,
            new Dictionary<StringName, string> { { TriggerOnNaturalOne, "_handle_halfling_luck" } }
        },
        {
            TraitSavageAttacks,
            new Dictionary<StringName, string> { { TriggerOnCrit, "_handle_savage_attacks" } }
        },
        {
            TraitRelentlessEndurance,
            new Dictionary<StringName, string>
            {
                { TriggerOnFatalDamage, "_handle_relentless_endurance" },
            }
        },
    };

    public static StringName TRIGGER_PASSIVE() => TriggerPassive;

    public static StringName TRIGGER_ON_NATURAL_ONE() => TriggerOnNaturalOne;

    public static StringName TRIGGER_ON_CRIT() => TriggerOnCrit;

    public static StringName TRIGGER_ON_FATAL_DAMAGE() => TriggerOnFatalDamage;

    public static StringName TRIGGER_ON_BATTLE_START() => TriggerOnBattleStart;

    public static StringName TRIGGER_ON_TURN_START() => TriggerOnTurnStart;

    public static StringName TRAIT_HALFLING_LUCK() => TraitHalflingLuck;

    public static StringName TRAIT_SAVAGE_ATTACKS() => TraitSavageAttacks;

    public static StringName TRAIT_RELENTLESS_ENDURANCE() => TraitRelentlessEndurance;

    public static IReadOnlySet<StringName> get_valid_trigger_types() => VALID_TRIGGER_TYPES;

    public static IReadOnlyDictionary<StringName, IReadOnlyDictionary<StringName, string>>
        get_dispatch_trigger_types() => DISPATCH_TRIGGER_TYPES;

    public static bool is_valid_trigger_type(StringName triggerType)
    {
        return VALID_TRIGGER_TYPES.Contains(triggerType);
    }

    public static bool has_dispatch_for_trait_trigger(StringName traitId, StringName triggerType)
    {
        if (traitId == "" || triggerType == "")
            return false;

        if (!DISPATCH_TRIGGER_TYPES.TryGetValue(traitId, out var dispatchEntry))
            return false;

        return dispatchEntry.ContainsKey(triggerType);
    }

    public static string get_dispatch_method_name(StringName traitId, StringName triggerType)
    {
        if (traitId == "" || triggerType == "")
            return "";

        if (!DISPATCH_TRIGGER_TYPES.TryGetValue(traitId, out var dispatchEntry))
            return "";

        if (!dispatchEntry.TryGetValue(triggerType, out string methodName))
            return "";

        return methodName;
    }

    public static IReadOnlyList<StringName> get_dispatch_trait_ids()
    {
        var traitIds = new List<StringName>();

        foreach (StringName traitId in DISPATCH_TRIGGER_TYPES.Keys)
        {
            if (traitId != "")
                traitIds.Add(traitId);
        }

        traitIds.Sort((left, right) =>
            string.Compare(left.ToString(), right.ToString(), StringComparison.Ordinal)
        );

        return traitIds;
    }
}
