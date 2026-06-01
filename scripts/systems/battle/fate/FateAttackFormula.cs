using System;
using Godot;

public static class FateAttackFormula
{
    public const int D20_SIZE = 20;
    public const int COMBAT_LUCK_SCORE_MAX = 4;

    public interface IRollSource
    {
        int RandiRange(int minValue, int maxValue);
    }

    public static int CalcCritGateDieSize(int effectiveLuck, bool isDisadvantage)
    {
        int growthSteps = Math.Max(0, -effectiveLuck - 3);
        if (isDisadvantage && effectiveLuck <= -5 && growthSteps > 0)
            growthSteps -= 1;
        return D20_SIZE << growthSteps;
    }

    public static int CalcFumbleLowEnd(int effectiveLuck)
    {
        return 1 + Math.Clamp(-effectiveLuck - 4, 0, 2);
    }

    public static int CalcCombatLuckScore(int hiddenLuckAtBirth, int faithLuckBonus)
    {
        int positiveHiddenLuck = Math.Max(0, hiddenLuckAtBirth);
        int positiveFaithLuck = Math.Max(0, faithLuckBonus);
        return Math.Min(
            COMBAT_LUCK_SCORE_MAX,
            positiveHiddenLuck + (int)(positiveFaithLuck / 2.0)
        );
    }

    public static int CalcCritThreshold(int hiddenLuckAtBirth, int faithLuckBonus)
    {
        return D20_SIZE - CalcCombatLuckScore(hiddenLuckAtBirth, faithLuckBonus);
    }

    public static int RollDieWithDisadvantageRule(int dieSize, bool isDisadvantage)
    {
        return RollDieWithDisadvantageRule(dieSize, isDisadvantage, (IRollSource)null);
    }

    public static int RollDieWithDisadvantageRule(
        int dieSize,
        bool isDisadvantage,
        RandomNumberGenerator rng
    )
    {
        return RollDieWithDisadvantageRule(
            dieSize,
            isDisadvantage,
            new GodotRandomRollSource(rng)
        );
    }

    public static int RollDieWithDisadvantageRule(
        int dieSize,
        bool isDisadvantage,
        IRollSource rng
    )
    {
        int normalizedDieSize = Math.Max(dieSize, 1);
        IRollSource resolvedRng = rng ?? new GodotRandomRollSource(null);
        int firstRoll = resolvedRng.RandiRange(1, normalizedDieSize);
        if (!isDisadvantage)
            return firstRoll;
        int secondRoll = resolvedRng.RandiRange(1, normalizedDieSize);
        return Math.Min(firstRoll, secondRoll);
    }

    private sealed class GodotRandomRollSource : IRollSource
    {
        private readonly RandomNumberGenerator _rng;

        public GodotRandomRollSource(RandomNumberGenerator rng)
        {
            _rng = rng ?? CreateRandomizedRng();
        }

        public int RandiRange(int minValue, int maxValue)
        {
            return _rng.RandiRange(minValue, maxValue);
        }
    }

    private static RandomNumberGenerator CreateRandomizedRng()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        return rng;
    }
}
