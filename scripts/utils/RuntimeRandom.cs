using System;

internal sealed class RuntimeRandom
{
    private Random _random;

    internal RuntimeRandom(long seed = 1)
    {
        Reseed(seed);
    }

    internal void Reseed(long seed)
    {
        int normalizedSeed = unchecked((int)(seed ^ (seed >> 32)));
        if (normalizedSeed == 0)
            normalizedSeed = 1;
        _random = new Random(normalizedSeed);
    }

    internal int RandiRange(int minValue, int maxValue)
    {
        int lower = Math.Min(minValue, maxValue);
        int upper = Math.Max(minValue, maxValue);
        if (lower == upper)
            return lower;
        return (int)_random.NextInt64(lower, (long)upper + 1L);
    }

    internal long Randi()
    {
        return _random.NextInt64(1, long.MaxValue);
    }

    internal float Randf()
    {
        return (float)_random.NextDouble();
    }

    internal float RandfRange(float minValue, float maxValue)
    {
        float lower = Math.Min(minValue, maxValue);
        float upper = Math.Max(minValue, maxValue);
        if (Math.Abs(upper - lower) <= float.Epsilon)
            return lower;
        return lower + (upper - lower) * Randf();
    }
}
