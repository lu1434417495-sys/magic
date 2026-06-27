using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedSuccessFailedSecondarySaveOneDamageResolver
    : FixedSuccessOneDamageResolver
{
    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

    public new int _roll_true_random_attack_range(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        if (battle_state != null)
        {
            battle_state.NextAttackRollNonce();
        }
        return Math.Min(min_value, max_value);
    }
}
