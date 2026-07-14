using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedHitMaxDamageResolver : BattleDamageResolver
{
    public FixedHitMaxDamageResolver()
    {
        SetHitResolver(new FixedHitResolver());
    }

    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

    public override int _roll_damage_die(int dice_sides)
    {
        return Math.Max(dice_sides, 1);
    }

    public int _roll_true_random_attack_range(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
        {
            battle_state.NextAttackRollNonce();
        }
        return Math.Clamp(10, Math.Min(min_value, max_value), Math.Max(min_value, max_value));
    }
}
