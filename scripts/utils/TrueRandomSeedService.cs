using System;
using Godot;

public static class TrueRandomSeedService
{
    private const int SeedByteCount = 7;
    private const long MaxCryptoValue = 72057594037927936L;

    public static long GenerateSeed()
    {
        long seed = SeedFromCryptoBytes();
        if (seed > 0)
        {
            return seed;
        }
        return SeedFromFallbackRng();
    }

    public static int RandiRange(int min_value, int max_value)
    {
        int lower = Math.Min(min_value, max_value);
        int upper = Math.Max(min_value, max_value);
        long span = (long)upper - lower + 1L;
        if (span <= 1)
        {
            return lower;
        }

        long limit = MaxCryptoValue - (MaxCryptoValue % span);
        for (int attempt = 0; attempt < 16; attempt++)
        {
            long rawValue = SeedFromCryptoBytes();
            if (rawValue >= 0 && rawValue < limit)
            {
                return (int)(lower + (rawValue % span));
            }
        }
        return FallbackRngRange(lower, upper);
    }
    private static long SeedFromCryptoBytes()
    {
        var crypto = new Crypto();
        byte[] bytes = crypto.GenerateRandomBytes(SeedByteCount);
        if (bytes.Length < SeedByteCount)
        {
            return -1;
        }

        long seed = 0;
        foreach (byte byteValue in bytes)
        {
            seed = (seed << 8) | byteValue;
        }
        return seed;
    }

    private static long SeedFromFallbackRng()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        return Math.Max((long)rng.Randi(), 1L);
    }

    private static int FallbackRngRange(int minValue, int maxValue)
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        return rng.RandiRange(minValue, maxValue);
    }
}
