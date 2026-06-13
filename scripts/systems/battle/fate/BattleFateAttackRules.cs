using Godot;

public static class BattleFateAttackRules
{
    private const int NATURAL_HIT_ROLL = 20;

    private static readonly StringName STATUS_BLACK_STAR_BRAND_ELITE = "black_star_brand_elite";

    private static readonly StringName STATUS_CROWN_BREAK_BROKEN_FANG = "crown_break_broken_fang";

    public static bool DoesAttackRollHit(int hitRoll, AttackCheckInput attackCheck)
    {
        if (attackCheck.NaturalOneAutoMiss && hitRoll <= 1)
            return false;

        if (attackCheck.NaturalTwentyAutoHit && hitRoll >= NATURAL_HIT_ROLL)
            return true;

        return hitRoll >= attackCheck.RequiredRoll;
    }

    public static bool DoesGateDieCrit(int critGateRoll, int critGateDie, bool critLocked)
    {
        return !critLocked && critGateDie > NATURAL_HIT_ROLL && critGateRoll == critGateDie;
    }

    public static bool IsHighThreatCritRoll(
        int hitRoll,
        bool critLocked,
        int critGateDie,
        int critThreshold
    )
    {
        return !critLocked && critGateDie == NATURAL_HIT_ROLL && hitRoll >= critThreshold;
    }

    public static bool IsAttackCritLocked(BattleUnitState unitState)
    {
        return unitState != null
            && (
                unitState.HasStatusEffect(STATUS_BLACK_STAR_BRAND_ELITE)
                || unitState.HasStatusEffect(STATUS_CROWN_BREAK_BROKEN_FANG)
                || UnitHasCritLockStatus(unitState)
            );
    }

    private static bool UnitHasCritLockStatus(BattleUnitState unitState)
    {
        if (unitState == null)
            return false;

        foreach (StringName statusId in unitState.GetSortedStatusEffectIdsTyped())
        {
            var statusEntry = unitState.GetStatusEffect(statusId);
            if (statusEntry == null)
                continue;

            if (statusEntry.lock_crit)
                return true;
        }

        return false;
    }
}
