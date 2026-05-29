using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedRollDamageResolver : BattleDamageResolver
{
    private readonly Queue<int> _damageRolls = new();
    private readonly Queue<int> _attackRolls = new();

    public FixedRollDamageResolver() { }

    public FixedRollDamageResolver(GArray damageRolls)
    {
        SetRolls(damageRolls, null);
    }

    public FixedRollDamageResolver(GArray damageRolls, GArray attackRolls)
    {
        SetRolls(damageRolls, attackRolls);
    }

    public void set_rolls(GArray damage_rolls, GArray attack_rolls = null)
    {
        SetRolls(damage_rolls, attack_rolls);
    }

    public new void set_skill_defs(GDictionary skill_defs) => base.set_skill_defs(skill_defs);

    public new void set_hit_resolver(GodotObject hit_resolver) =>
        base.set_hit_resolver(hit_resolver);

    public new BattleFateEventBus get_fate_event_bus() => base.get_fate_event_bus();

    public new GArray get_and_clear_last_stand_mastery_records() =>
        base.get_and_clear_last_stand_mastery_records();

    public GDictionary resolve_effects(
        RefCounted source_unit,
        RefCounted target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary damage_context = null
    )
    {
        return base.resolve_effects(
            source_unit as BattleUnitState,
            target_unit as BattleUnitState,
            effect_defs,
            damage_context
        );
    }

    public GDictionary resolve_effects(
        RefCounted source_unit,
        RefCounted target_unit,
        Godot.Collections.Array effect_defs
    )
    {
        return base.resolve_effects(
            source_unit as BattleUnitState,
            target_unit as BattleUnitState,
            effect_defs,
            new GDictionary()
        );
    }

    public GDictionary resolve_attack_effects(
        RefCounted source_unit,
        RefCounted target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary attack_check,
        GDictionary attack_context = null
    )
    {
        return base.resolve_attack_effects(
            source_unit as BattleUnitState,
            target_unit as BattleUnitState,
            effect_defs,
            attack_check,
            attack_context
        );
    }

    public GDictionary resolve_attack_effects(
        RefCounted source_unit,
        RefCounted target_unit,
        Godot.Collections.Array effect_defs,
        GDictionary attack_check
    )
    {
        return base.resolve_attack_effects(
            source_unit as BattleUnitState,
            target_unit as BattleUnitState,
            effect_defs,
            attack_check,
            new GDictionary()
        );
    }

    protected void SetRolls(GArray damageRolls, GArray attackRolls)
    {
        _damageRolls.Clear();
        if (damageRolls != null)
        {
            foreach (var roll in damageRolls)
            {
                _damageRolls.Enqueue(roll.AsInt32());
            }
        }

        _attackRolls.Clear();
        if (attackRolls != null)
        {
            foreach (var roll in attackRolls)
            {
                _attackRolls.Enqueue(roll.AsInt32());
            }
        }
    }

    public override int _roll_damage_die(int dice_sides)
    {
        int normalizedSides = Math.Max(dice_sides, 1);
        if (_damageRolls.Count == 0)
        {
            return normalizedSides;
        }
        return Math.Clamp(_damageRolls.Dequeue(), 1, normalizedSides);
    }

    public int _roll_true_random_attack_range(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        int lower = Math.Min(min_value, max_value);
        int upper = Math.Max(min_value, max_value);
        if (battle_state != null)
        {
            battle_state.attack_roll_nonce = Math.Max((int)battle_state.attack_roll_nonce, 0) + 1;
        }
        if (_attackRolls.Count == 0)
        {
            return upper;
        }
        return Math.Clamp(_attackRolls.Dequeue(), lower, upper);
    }
}
