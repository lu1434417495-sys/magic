using System;
using Godot;

[GlobalClass]
public partial class TrueRandomSeedService : RefCounted
{
    public const int SEED_BYTE_COUNT = 7;
    public const long MAX_CRYPTO_VALUE = 72057594037927936L;

    public static long generate_seed()
    {
        long seed = SeedFromCryptoBytes();
        if (seed > 0)
        {
            return seed;
        }
        return SeedFromFallbackRng();
    }

    public static int randi_range(int min_value, int max_value)
    {
        int lower = Math.Min(min_value, max_value);
        int upper = Math.Max(min_value, max_value);
        long span = (long)upper - lower + 1L;
        if (span <= 1)
        {
            return lower;
        }

        long limit = MAX_CRYPTO_VALUE - (MAX_CRYPTO_VALUE % span);
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

    public static long GenerateSeed() => generate_seed();

    public static int RandiRange(int minValue, int maxValue) => randi_range(minValue, maxValue);

    private static long SeedFromCryptoBytes()
    {
        var crypto = new Crypto();
        byte[] bytes = crypto.GenerateRandomBytes(SEED_BYTE_COUNT);
        if (bytes.Length < SEED_BYTE_COUNT)
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
