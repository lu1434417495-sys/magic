using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedRollDamageResolver : BattleDamageResolver
{
    private readonly Queue<int> _damageRolls = new();
    private readonly FixedQueueHitResolver _fixedHitResolver = new();

    public FixedRollDamageResolver()
    {
        SetHitResolver(_fixedHitResolver);
    }

    public FixedRollDamageResolver(GArray damageRolls)
        : this()
    {
        SetRolls(damageRolls, null);
    }

    public FixedRollDamageResolver(GArray damageRolls, GArray attackRolls)
        : this()
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

        _fixedHitResolver.SetRolls(attackRolls);
    }

    internal BattleHitResolver GetHitResolver() => _fixedHitResolver;

    public override int _roll_damage_die(int dice_sides)
    {
        int normalizedSides = Math.Max(dice_sides, 1);
        if (_damageRolls.Count == 0)
        {
            return normalizedSides;
        }
        return Math.Clamp(_damageRolls.Dequeue(), 1, normalizedSides);
    }

    private sealed class FixedQueueHitResolver : BattleHitResolver
    {
        private readonly Queue<int> _attackRolls = new();

        internal void SetRolls(GArray attackRolls)
        {
            _attackRolls.Clear();
            if (attackRolls == null)
                return;
            foreach (Variant roll in attackRolls)
                _attackRolls.Enqueue(roll.AsInt32());
        }

        protected override int RollTrueRandomAttackRange(
            int minValue,
            int maxValue,
            BattleState battleState
        )
        {
            int lower = Math.Min(minValue, maxValue);
            int upper = Math.Max(minValue, maxValue);
            battleState?.NextAttackRollNonce();
            return _attackRolls.Count == 0
                ? upper
                : Math.Clamp(_attackRolls.Dequeue(), lower, upper);
        }
    }
}
