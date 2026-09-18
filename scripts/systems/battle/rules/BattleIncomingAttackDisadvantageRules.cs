using Godot;

internal static class BattleIncomingAttackDisadvantageRules
{
    // Source-side overrides retain their meaning; incoming protection is a separate contribution.
    internal static StringName ResolveContributingStatus(
        BattleUnitReadView defender,
        AttackCheckInput check,
        bool sourceDisadvantage,
        AttackContext context = null
    )
    {
        if (!defender.IsValid || !defender.IsAlive || sourceDisadvantage || check.Invalid
            || check.ForceHitNoCrit || context?.ForceHitNoCrit == true
            || context?.ForceHitAllowCrit == true)
            return new StringName("");
        foreach (StringName id in defender.GetSortedStatusEffectIds())
        {
            BattleStatusReadView status = defender.GetStatus(id);
            if (status.IncomingAttackRollDisadvantage && status.DurationTu != 0)
                return id;
        }
        return new StringName("");
    }
}
