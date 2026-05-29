using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedMissResolver : BattleHitResolver
{
    private const int NaturalMissRoll = 1;
    private const int NaturalHitRoll = 20;
    private static readonly StringName AttackResolutionMiss = "miss";
    private static readonly StringName RollDispositionNaturalAutoMiss = "natural_1_auto_miss";

    public new GDictionary resolve_attack_metadata(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GDictionary attack_check,
        GDictionary attack_context
    )
    {
        int requiredRoll = GetInt(attack_check, "required_roll", NaturalHitRoll);
        return new GDictionary
        {
            ["attack_resolution"] = AttackResolutionMiss,
            ["attack_success"] = false,
            ["critical_hit"] = false,
            ["critical_fail"] = false,
            ["ordinary_miss"] = true,
            ["is_disadvantage"] = GetBool(attack_context, "is_disadvantage", false),
            ["hidden_luck_at_birth"] = 0,
            ["faith_luck_bonus"] = 0,
            ["effective_luck"] = 0,
            ["crit_locked"] = false,
            ["crit_gate_die"] = 20,
            ["crit_gate_roll"] = 0,
            ["hit_roll"] = NaturalMissRoll,
            ["fumble_low_end"] = 1,
            ["crit_threshold"] = NaturalHitRoll,
            ["required_roll"] = requiredRoll,
            ["display_required_roll"] = GetInt(
                attack_check,
                "display_required_roll",
                Math.Clamp(requiredRoll, 2, NaturalHitRoll)
            ),
            ["hit_rate_percent"] = 0,
            ["success_rate_percent"] = 0,
            ["trait_trigger_results"] = new Godot.Collections.Array(),
        };
    }

    public new GDictionary resolve_spell_control_metadata(
        BattleUnitState source_unit,
        GDictionary attack_context
    )
    {
        return new GDictionary
        {
            ["attack_resolution"] = AttackResolutionMiss,
            ["spell_control_resolution"] = new StringName("miss"),
            ["attack_success"] = false,
            ["critical_hit"] = false,
            ["critical_fail"] = false,
            ["ordinary_miss"] = true,
            ["hit_roll"] = NaturalMissRoll,
        };
    }

    public new GDictionary roll_attack_check(BattleState battle_state, GDictionary attack_check)
    {
        if (battle_state != null)
            battle_state.attack_roll_nonce = Math.Max((int)battle_state.attack_roll_nonce, 0) + 1;
        GDictionary result = attack_check?.Duplicate(true) ?? new GDictionary();
        result["roll"] = NaturalMissRoll;
        result["roll_disposition"] = RollDispositionNaturalAutoMiss;
        result["success"] = false;
        result["hit_rate_percent"] = 0;
        result["success_rate_percent"] = 0;
        result["preview_text"] = "0%（测试固定未命中）";
        result["resolution_text"] = "0%（测试固定未命中），d20=1";
        return result;
    }

    public new int roll_attack_die(int die_size, bool is_disadvantage, GDictionary attack_context)
    {
        return Math.Clamp(NaturalMissRoll, 1, Math.Max(die_size, 1));
    }

    public new int _roll_true_random_attack_range(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
            battle_state.attack_roll_nonce = Math.Max((int)battle_state.attack_roll_nonce, 0) + 1;
        return Math.Min(min_value, max_value);
    }

    private static int GetInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsInt32();
    }

    private static bool GetBool(GDictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsBool();
    }
}
