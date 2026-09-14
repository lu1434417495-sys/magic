using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class FixedSuccessFailedSecondarySaveOneDamageResolver
    : FixedSuccessOneDamageResolver
{
    public FixedSuccessFailedSecondarySaveOneDamageResolver()
    {
        SetHitResolver(new FixedHitFailedSecondaryRollResolver());
    }

    internal new BattleFateEventBus GetFateEventBus() => base.GetFateEventBus();

    private sealed class FixedHitFailedSecondaryRollResolver : FixedHitResolver
    {
        public override int RollAttackDie(
            int dieSize,
            bool isDisadvantage,
            AttackContext attackContext
        )
        {
            attackContext?.BattleState?.NextAttackRollNonce();
            return Math.Clamp(1, 1, Math.Max(dieSize, 1));
        }
    }
}
