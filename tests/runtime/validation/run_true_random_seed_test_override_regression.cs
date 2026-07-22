using System;

public partial class run_true_random_seed_test_override_regression : LifecycleTestSceneTree
{
    private const long TestSeed = 23_071_991L;

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            RandomSample first = CaptureSample(TestSeed);
            RandomSample repeated = CaptureSample(TestSeed);

            _test.Eq(
                repeated.GeneratedSeed,
                first.GeneratedSeed,
                "Resetting the E2E random seed should reproduce generated runtime seeds."
            );
            _test.Eq(
                repeated.RangeValue,
                first.RangeValue,
                "Resetting the E2E random seed should reproduce bounded runtime rolls."
            );
            _test.Eq(
                repeated.NextGeneratedSeed,
                first.NextGeneratedSeed,
                "The deterministic test override should preserve call-order reproducibility."
            );
            _test.True(first.GeneratedSeed > 0, "Generated runtime seeds must stay positive.");
            _test.True(
                first.RangeValue >= -3 && first.RangeValue <= 3,
                "Deterministic bounded rolls must honor the requested inclusive range."
            );
            _test.True(
                Throws<ArgumentOutOfRangeException>(
                    () => TrueRandomSeedService.ConfigureDeterministicForTests(0)
                ),
                "The deterministic test override should reject non-positive seeds."
            );
        }
        finally
        {
            TrueRandomSeedService.ClearDeterministicForTests();
        }

        RequestTestExit(_test.Finish("True random seed test override regression"));
    }

    private static RandomSample CaptureSample(long seed)
    {
        TrueRandomSeedService.ConfigureDeterministicForTests(seed);
        return new RandomSample(
            TrueRandomSeedService.GenerateSeed(),
            TrueRandomSeedService.RandiRange(-3, 3),
            TrueRandomSeedService.GenerateSeed()
        );
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private readonly record struct RandomSample(
        long GeneratedSeed,
        int RangeValue,
        long NextGeneratedSeed
    );
}
