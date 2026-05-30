using Godot;

[GlobalClass]
public partial class FateAttackFormula : RefCounted
{
    public const int D20_SIZE = 20;
    public const int COMBAT_LUCK_SCORE_MAX = 4;

    public static int CalcCritGateDieSize(int effectiveLuck, bool isDisadvantage)
    {
        int growthSteps = Mathf.Max(0, -effectiveLuck - 3);
        if (isDisadvantage && effectiveLuck <= -5 && growthSteps > 0)
            growthSteps -= 1;
        return D20_SIZE << growthSteps;
    }

    public static int CalcFumbleLowEnd(int effectiveLuck)
    {
        return 1 + Mathf.Clamp(-effectiveLuck - 4, 0, 2);
    }

    public static int CalcCombatLuckScore(int hiddenLuckAtBirth, int faithLuckBonus)
    {
        int positiveHiddenLuck = Mathf.Max(0, hiddenLuckAtBirth);
        int positiveFaithLuck = Mathf.Max(0, faithLuckBonus);
        return Mathf.Min(
            COMBAT_LUCK_SCORE_MAX,
            positiveHiddenLuck + (int)(positiveFaithLuck / 2.0)
        );
    }

    public static int CalcCritThreshold(int hiddenLuckAtBirth, int faithLuckBonus)
    {
        return D20_SIZE - CalcCombatLuckScore(hiddenLuckAtBirth, faithLuckBonus);
    }

    public static int RollDieWithDisadvantageRule(
        int dieSize,
        bool isDisadvantage,
        RandomNumberGenerator rng = null
    )
    {
        int normalizedDieSize = Mathf.Max(dieSize, 1);
        var resolvedRng = _ResolveRng(rng);
        int firstRoll = resolvedRng.RandiRange(1, normalizedDieSize);
        if (!isDisadvantage)
            return firstRoll;
        int secondRoll = resolvedRng.RandiRange(1, normalizedDieSize);
        return Mathf.Min(firstRoll, secondRoll);
    }

    public static int roll_die_with_disadvantage_rule(
        int dieSize,
        bool isDisadvantage,
        RandomNumberGenerator rng = null
    )
    {
        return RollDieWithDisadvantageRule(dieSize, isDisadvantage, rng);
    }

    public static int calc_crit_gate_die_size(int effectiveLuck, bool isDisadvantage)
    {
        return CalcCritGateDieSize(effectiveLuck, isDisadvantage);
    }

    public static int calc_fumble_low_end(int effectiveLuck)
    {
        return CalcFumbleLowEnd(effectiveLuck);
    }

    public static int calc_crit_threshold(int hiddenLuckAtBirth, int faithLuckBonus)
    {
        return CalcCritThreshold(hiddenLuckAtBirth, faithLuckBonus);
    }

    private static RandomNumberGenerator _ResolveRng(RandomNumberGenerator rng)
    {
        if (rng != null)
            return rng;
        var fallbackRng = new RandomNumberGenerator();
        fallbackRng.Randomize();
        return fallbackRng;
    }
}
