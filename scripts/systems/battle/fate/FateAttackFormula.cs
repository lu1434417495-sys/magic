using System;
using Godot;

public static class FateAttackFormula
{
    private const int D20Size = 20;
    private const int CombatLuckScoreMax = 4;

    public interface IRollSource
    {
        int RandiRange(int minValue, int maxValue);
    }

    public static int CalcCritGateDieSize(int effectiveLuck, bool isDisadvantage)
    {
        int growthSteps = Math.Max(0, -effectiveLuck - 3);
        if (isDisadvantage && effectiveLuck <= -5 && growthSteps > 0)
            growthSteps -= 1;
        return D20Size << growthSteps;
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
            CombatLuckScoreMax,
            positiveHiddenLuck + (int)(positiveFaithLuck / 2.0)
        );
    }

    public static int CalcCritThreshold(int hiddenLuckAtBirth, int faithLuckBonus)
    {
        return D20Size - CalcCombatLuckScore(hiddenLuckAtBirth, faithLuckBonus);
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
        GodotRandomRollSource ownedRng = rng == null ? new GodotRandomRollSource(null) : null;
        IRollSource resolvedRng = rng ?? ownedRng;
        try
        {
            int firstRoll = resolvedRng.RandiRange(1, normalizedDieSize);
            if (!isDisadvantage)
                return firstRoll;
            int secondRoll = resolvedRng.RandiRange(1, normalizedDieSize);
            return Math.Min(firstRoll, secondRoll);
        }
        finally
        {
            ownedRng?.Dispose();
        }
    }

    private sealed class GodotRandomRollSource : IRollSource, IDisposable
    {
        private readonly GodotTransientResourceScope _scope;
        private readonly RandomNumberGenerator _rng;

        public GodotRandomRollSource(RandomNumberGenerator rng)
        {
            if (rng != null)
            {
                _rng = rng;
                return;
            }
            _scope = new GodotTransientResourceScope("FateAttackFormula.GodotRandomRollSource");
            _rng = _scope.OwnWrapper(new RandomNumberGenerator(), "rng");
            _rng.Randomize();
        }

        public int RandiRange(int minValue, int maxValue)
        {
            return _rng.RandiRange(minValue, maxValue);
        }

        public void Dispose()
        {
            _scope?.Dispose();
        }
    }
}
