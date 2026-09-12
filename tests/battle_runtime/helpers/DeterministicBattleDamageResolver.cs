using System;
using Godot;

public partial class DeterministicBattleDamageResolver : BattleDamageResolver
{
    private readonly DeterministicBattleHitResolver _deterministicHitResolver = new();

    public DeterministicBattleDamageResolver()
    {
        SetHitResolver(_deterministicHitResolver);
    }

    internal BattleHitResolver GetHitResolver() => _deterministicHitResolver;

    private sealed class DeterministicBattleHitResolver : BattleHitResolver
    {
        protected override int RollTrueRandomAttackRange(
            int minValue,
            int maxValue,
            BattleState battleState
        )
        {
            int lower = Math.Min(minValue, maxValue);
            int upper = Math.Max(minValue, maxValue);
            if (battleState == null)
                return lower;

            int nonce = Math.Max((int)battleState.attack_roll_nonce, 0);
            string rollSeedSource = $"{battleState.battle_id}:{battleState.seed}:{nonce}";
            var rng = new RuntimeRandom(unchecked((long)StringExtensions.Hash(rollSeedSource)));
            battleState.NextAttackRollNonce();
            return rng.RandiRange(lower, upper);
        }
    }
}
