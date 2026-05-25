using Godot;

[GlobalClass]
public partial class TraitTriggerContentRules : RefCounted
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

    private static readonly Godot.Collections.Dictionary VALID_TRIGGER_TYPES = new()
    {
        { TriggerPassive, true },
        { TriggerOnNaturalOne, true },
        { TriggerOnCrit, true },
        { TriggerOnFatalDamage, true },
        { TriggerOnBattleStart, true },
        { TriggerOnTurnStart, true },
    };

    private static readonly Godot.Collections.Dictionary DISPATCH_TRIGGER_TYPES = new()
    {
        { TraitHalflingLuck, new Godot.Collections.Dictionary { { TriggerOnNaturalOne, "_handle_halfling_luck" } } },
        { TraitSavageAttacks, new Godot.Collections.Dictionary { { TriggerOnCrit, "_handle_savage_attacks" } } },
        { TraitRelentlessEndurance, new Godot.Collections.Dictionary { { TriggerOnFatalDamage, "_handle_relentless_endurance" } } },
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

    public static Godot.Collections.Dictionary get_valid_trigger_types()
    {
        return VALID_TRIGGER_TYPES.Duplicate(true);
    }

    public static Godot.Collections.Dictionary get_dispatch_trigger_types()
    {
        return DISPATCH_TRIGGER_TYPES.Duplicate(true);
    }

    public static bool is_valid_trigger_type(StringName triggerType)
    {
        return VALID_TRIGGER_TYPES.ContainsKey(triggerType);
    }

    public static bool has_dispatch_for_trait_trigger(StringName traitId, StringName triggerType)
    {
        if (traitId == "" || triggerType == "")
            return false;
        if (!DISPATCH_TRIGGER_TYPES.ContainsKey(traitId))
            return false;
        var dispatchEntry = DISPATCH_TRIGGER_TYPES[traitId].AsGodotDictionary();
        return dispatchEntry.ContainsKey(triggerType);
    }

    public static string get_dispatch_method_name(StringName traitId, StringName triggerType)
    {
        if (traitId == "" || triggerType == "")
            return "";
        if (!DISPATCH_TRIGGER_TYPES.ContainsKey(traitId))
            return "";
        var dispatchEntry = DISPATCH_TRIGGER_TYPES[traitId].AsGodotDictionary();
        if (!dispatchEntry.ContainsKey(triggerType))
            return "";
        return (string)dispatchEntry[triggerType];
    }

    public static Godot.Collections.Array<StringName> get_dispatch_trait_ids()
    {
        var traitIds = new Godot.Collections.Array<StringName>();
        foreach (var traitIdVariant in DISPATCH_TRIGGER_TYPES.Keys)
        {
            var traitId = traitIdVariant.AsStringName();
            if (traitId != "")
                traitIds.Add(traitId);
        }
        traitIds.Sort();
        return traitIds;
    }
}
