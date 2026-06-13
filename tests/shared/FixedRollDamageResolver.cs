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

    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

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
            battle_state.NextAttackRollNonce();
        }
        if (_attackRolls.Count == 0)
        {
            return upper;
        }
        return Math.Clamp(_attackRolls.Dequeue(), lower, upper);
    }
}
