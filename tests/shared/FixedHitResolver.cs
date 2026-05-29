using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedHitResolver : BattleHitResolver
{
    protected const int NaturalMissRoll = 1;
    protected const int NaturalHitRoll = 20;
    protected static readonly StringName AttackResolutionHit = "hit";
    protected static readonly StringName AttackResolutionCriticalHit = "critical_hit";
    protected static readonly StringName AttackResolutionCriticalFail = "critical_fail";
    protected static readonly StringName RollDispositionThresholdHit = "threshold_hit";

    public int fixed_roll = 10;

    public FixedHitResolver() { }

    public FixedHitResolver(int pFixedRoll)
    {
        fixed_roll = Math.Clamp(pFixedRoll, NaturalMissRoll, NaturalHitRoll);
    }

    public new GDictionary resolve_attack_metadata(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        GDictionary attack_check,
        GDictionary attack_context
    )
    {
        return BuildFixedAttackMetadata(
            attack_check,
            attack_context,
            AttackResolutionHit,
            true,
            false,
            false
        );
    }

    public new GDictionary resolve_spell_control_metadata(
        BattleUnitState source_unit,
        GDictionary attack_context
    )
    {
        StringName attackResolution = AttackResolutionHit;
        StringName spellControlResolution = "normal";
        bool attackSuccess = true;
        bool criticalHit = false;
        bool criticalFail = false;
        if (fixed_roll <= NaturalMissRoll)
        {
            attackResolution = AttackResolutionCriticalFail;
            spellControlResolution = "critical_fail";
            attackSuccess = false;
            criticalFail = true;
        }
        else if (fixed_roll >= NaturalHitRoll)
        {
            attackResolution = AttackResolutionCriticalHit;
            spellControlResolution = "critical_success";
            criticalHit = true;
        }
        return new GDictionary
        {
            ["attack_resolution"] = attackResolution,
            ["spell_control_resolution"] = spellControlResolution,
            ["attack_success"] = attackSuccess,
            ["critical_hit"] = criticalHit,
            ["critical_fail"] = criticalFail,
            ["ordinary_miss"] = false,
            ["hit_roll"] = fixed_roll,
        };
    }

    public new GDictionary roll_attack_check(BattleState battle_state, GDictionary attack_check)
    {
        if (battle_state != null)
            battle_state.attack_roll_nonce = Math.Max((int)battle_state.attack_roll_nonce, 0) + 1;
        GDictionary result = attack_check?.Duplicate(true) ?? new GDictionary();
        result["roll"] = fixed_roll;
        result["roll_disposition"] = RollDispositionThresholdHit;
        result["success"] = true;
        result["hit_rate_percent"] = 100;
        result["success_rate_percent"] = 100;
        result["preview_text"] = "100%（测试固定命中）";
        result["resolution_text"] = $"100%（测试固定命中），d20={fixed_roll}";
        return result;
    }

    public new GDictionary roll_hit_rate(BattleState battle_state, int hit_rate_percent)
    {
        return roll_attack_check(
            battle_state,
            new GDictionary
            {
                ["required_roll"] = fixed_roll,
                ["display_required_roll"] = fixed_roll,
                ["natural_one_auto_miss"] = false,
                ["natural_twenty_auto_hit"] = false,
            }
        );
    }

    public new int roll_attack_die(int die_size, bool is_disadvantage, GDictionary attack_context)
    {
        return Math.Clamp(fixed_roll, 1, Math.Max(die_size, 1));
    }

    public new int _roll_true_random_attack_range(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
            battle_state.attack_roll_nonce = Math.Max((int)battle_state.attack_roll_nonce, 0) + 1;
        return Math.Clamp(
            fixed_roll,
            Math.Min(min_value, max_value),
            Math.Max(min_value, max_value)
        );
    }

    protected GDictionary BuildFixedAttackMetadata(
        GDictionary attackCheck,
        GDictionary attackContext,
        StringName attackResolution,
        bool attackSuccess,
        bool criticalHit,
        bool ordinaryMiss
    )
    {
        int requiredRoll = GetInt(attackCheck, "required_roll", fixed_roll);
        return new GDictionary
        {
            ["attack_resolution"] = attackResolution,
            ["attack_success"] = attackSuccess,
            ["critical_hit"] = criticalHit,
            ["critical_fail"] = false,
            ["ordinary_miss"] = ordinaryMiss,
            ["is_disadvantage"] = GetBool(attackContext, "is_disadvantage", false),
            ["hidden_luck_at_birth"] = 0,
            ["faith_luck_bonus"] = 0,
            ["effective_luck"] = 0,
            ["crit_locked"] = GetBool(attackContext, "force_hit_no_crit", false),
            ["crit_gate_die"] = 20,
            ["crit_gate_roll"] = 0,
            ["hit_roll"] = fixed_roll,
            ["fumble_low_end"] = 1,
            ["crit_threshold"] = NaturalHitRoll,
            ["required_roll"] = requiredRoll,
            ["display_required_roll"] = GetInt(
                attackCheck,
                "display_required_roll",
                Math.Clamp(requiredRoll, 2, NaturalHitRoll)
            ),
            ["hit_rate_percent"] = attackSuccess ? 100 : 0,
            ["success_rate_percent"] = attackSuccess ? 100 : 0,
            ["trait_trigger_results"] = new Godot.Collections.Array(),
        };
    }

    protected static int GetInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsInt32();
    }

    protected static bool GetBool(GDictionary dictionary, string key, bool fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key].AsBool();
    }
}
