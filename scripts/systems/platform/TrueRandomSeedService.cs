using System;
using System.Threading;
using CryptoRandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator;

public static class TrueRandomSeedService
{
    private const int SeedByteCount = 7;
    private const long MaxCryptoValue = 72057594037927936L;
    private static readonly object DeterministicTestLock = new();
    private static int _deterministicTestEnabled;
    private static ulong _deterministicTestState;

    internal static void ConfigureDeterministicForTests(long seed)
    {
        if (seed <= 0)
            throw new ArgumentOutOfRangeException(nameof(seed), seed, "Test seed must be positive.");

        lock (DeterministicTestLock)
        {
            _deterministicTestState = (ulong)seed;
            Volatile.Write(ref _deterministicTestEnabled, 1);
        }
    }

    internal static void ClearDeterministicForTests()
    {
        lock (DeterministicTestLock)
        {
            Volatile.Write(ref _deterministicTestEnabled, 0);
            _deterministicTestState = 0;
        }
    }

    public static long GenerateSeed()
    {
        if (TryNextDeterministicForTests(out ulong deterministicValue))
            return (long)(deterministicValue % (ulong)(MaxCryptoValue - 1L)) + 1L;

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

        if (TryNextDeterministicForTests(out ulong deterministicValue))
            return (int)(lower + (long)(deterministicValue % (ulong)span));

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

    private static bool TryNextDeterministicForTests(out ulong value)
    {
        if (Volatile.Read(ref _deterministicTestEnabled) == 0)
        {
            value = 0;
            return false;
        }

        lock (DeterministicTestLock)
        {
            if (Volatile.Read(ref _deterministicTestEnabled) == 0)
            {
                value = 0;
                return false;
            }

            ulong state = _deterministicTestState;
            value = NextSplitMix64(ref state);
            _deterministicTestState = state;
            return true;
        }
    }

    private static ulong NextSplitMix64(ref ulong state)
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong value = state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    private static long SeedFromCryptoBytes()
    {
        byte[] bytes = new byte[SeedByteCount];
        CryptoRandomNumberGenerator.Fill(bytes);

        long seed = 0;
        foreach (byte byteValue in bytes)
        {
            seed = (seed << 8) | byteValue;
        }
        return seed;
    }

    private static long SeedFromFallbackRng()
    {
        return Math.Max((long)Random.Shared.Next(), 1L);
    }

    private static int FallbackRngRange(int minValue, int maxValue)
    {
        return (int)Random.Shared.NextInt64(minValue, (long)maxValue + 1L);
    }
}
