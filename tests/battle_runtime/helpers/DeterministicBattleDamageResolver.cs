using System;
using Godot;

public partial class DeterministicBattleDamageResolver : BattleDamageResolver
{
    public int _roll_true_random_attack_range(
        int min_value,
        int max_value,
        BattleState battle_state
    )
    {
        int lower = Math.Min(min_value, max_value);
        int upper = Math.Max(min_value, max_value);
        if (battle_state == null)
        {
            return lower;
        }

        int nonce = Math.Max((int)battle_state.attack_roll_nonce, 0);
        string rollSeedSource = $"{battle_state.battle_id}:{battle_state.seed}:{nonce}";
        var rng = new RuntimeRandom(unchecked((long)StringExtensions.Hash(rollSeedSource)));
        battle_state.NextAttackRollNonce();
        return rng.RandiRange(lower, upper);
    }
}
