using System;
using System.Collections.Generic;

public partial class run_lifecycle_soak_statistics_regression : LifecycleTestSceneTree
{
    private const long MiB = 1024L * 1024L;
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestMedianAndLeastSquaresSlope();
            TestAbsoluteAndPercentageThresholdSelection();
            TestThresholdBoundariesPass();
            TestNegativeAndNoGrowthSeriesPass();
            TestExactCounterAndFingerprintFailures();
            TestViolationAndActivityBalanceFailures();
            TestMemoryDeltaAndSlopeFailures();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Lifecycle soak statistics regression"));
    }

    private void TestMedianAndLeastSquaresSlope()
    {
        _test.Eq(
            LifecycleSoakStatistics.Median(new long[] { 9, 1, 3 }),
            3.0,
            "odd median should select the sorted middle value"
        );
        _test.Eq(
            LifecycleSoakStatistics.Median(new long[] { 4, 1, 3, 2 }),
            2.5,
            "even median should average the two sorted middle values"
        );

        IReadOnlyList<LifecycleSoakSample> increasing = new[]
        {
            Sample(11, managed: 100, privateBytes: 0),
            Sample(12, managed: 110, privateBytes: 0),
            Sample(13, managed: 120, privateBytes: 0),
        };
        _test.Eq(
            LifecycleSoakStatistics.LeastSquaresSlope(
                increasing,
                sample => sample.ManagedMemoryBytes
            ),
            10.0,
            "least-squares slope should use cycle numbers as x values"
        );

        IReadOnlyList<LifecycleSoakSample> decreasing = new[]
        {
            Sample(20, managed: 120, privateBytes: 0),
            Sample(21, managed: 110, privateBytes: 0),
            Sample(22, managed: 100, privateBytes: 0),
        };
        _test.Eq(
            LifecycleSoakStatistics.LeastSquaresSlope(
                decreasing,
                sample => sample.ManagedMemoryBytes
            ),
            -10.0,
            "negative memory slope should remain negative"
        );
    }

    private void TestAbsoluteAndPercentageThresholdSelection()
    {
        _test.Eq(
            LifecycleSoakStatistics.CalculateAllowedGrowth(100 * MiB, 8 * MiB, 0.05),
            8.0 * MiB,
            "managed growth should use the absolute floor when five percent is smaller"
        );
        _test.Eq(
            LifecycleSoakStatistics.CalculateAllowedGrowth(1_000 * MiB, 8 * MiB, 0.05),
            50.0 * MiB,
            "managed growth should use the percentage threshold when it is larger"
        );
        _test.Eq(
            LifecycleSoakStatistics.CalculateAllowedGrowth(300 * MiB, 32 * MiB, 0.10),
            32.0 * MiB,
            "private growth should preserve the 32 MiB absolute floor"
        );
    }

    private void TestThresholdBoundariesPass()
    {
        List<LifecycleSoakSample> samples = BuildSamples(
            cycle =>
                100 * MiB
                + Math.Max(cycle - LifecycleSoakStatistics.FirstMeasuredCycle, 0)
                    * LifecycleSoakStatistics.ManagedMaximumSlopeBytesPerCycle,
            cycle =>
                300 * MiB
                + Math.Max(cycle - LifecycleSoakStatistics.FirstMeasuredCycle, 0)
                    * LifecycleSoakStatistics.PrivateMaximumSlopeBytesPerCycle
        );

        LifecycleSoakStatisticsReport report = LifecycleSoakStatistics.Evaluate(samples);
        _test.True(
            report.Passed,
            $"exact slope boundaries and sub-threshold deltas should pass: {FormatFailures(report)}"
        );
        _test.Eq(
            report.ManagedMemory.SlopeBytesPerCycle,
            (double)LifecycleSoakStatistics.ManagedMaximumSlopeBytesPerCycle,
            "managed slope equality should be accepted"
        );
        _test.Eq(
            report.PrivateMemory.SlopeBytesPerCycle,
            (double)LifecycleSoakStatistics.PrivateMaximumSlopeBytesPerCycle,
            "private slope equality should be accepted"
        );
    }

    private void TestNegativeAndNoGrowthSeriesPass()
    {
        List<LifecycleSoakSample> samples = BuildSamples(
            cycle => 120 * MiB - cycle * 1024L,
            _ => 300 * MiB
        );
        LifecycleSoakStatisticsReport report = LifecycleSoakStatistics.Evaluate(samples);
        _test.True(
            report.Passed,
            $"negative/no-growth memory series should pass: {FormatFailures(report)}"
        );
        _test.True(
            report.ManagedMemory.SlopeBytesPerCycle < 0,
            "negative managed growth should not be clamped to zero"
        );
        _test.Eq(
            report.PrivateMemory.SlopeBytesPerCycle,
            0.0,
            "constant private memory should have zero slope"
        );
    }

    private void TestExactCounterAndFingerprintFailures()
    {
        List<LifecycleSoakSample> counterMismatch = BuildSamples();
        counterMismatch[41] = counterMismatch[41] with
        {
            ActiveCounters = counterMismatch[41].ActiveCounters with { BattleOwners = 1 },
        };
        counterMismatch[42] = counterMismatch[42] with
        {
            ActiveCounters = counterMismatch[42].ActiveCounters with
            {
                NativeScopesByDomain = "Request=0,Battle=0",
            },
        };
        counterMismatch[43] = counterMismatch[43] with
        {
            ActiveCounters = counterMismatch[43].ActiveCounters with
            {
                ProjectionLeasesByDomain = "Battle=0,Request=1",
            },
        };
        counterMismatch[44] = counterMismatch[44] with
        {
            ActiveCounters = counterMismatch[44].ActiveCounters with
            {
                SnapshotEpoch = 2,
            },
        };
        LifecycleSoakStatisticsReport counterReport = LifecycleSoakStatistics.Evaluate(
            counterMismatch
        );
        _test.True(
            HasFailure(counterReport, 42, nameof(LifecycleSoakCounterVector.BattleOwners)),
            "post-warm-up active counters must exactly match cycle 10"
        );
        _test.True(
            HasFailure(
                counterReport,
                43,
                nameof(LifecycleSoakCounterVector.NativeScopesByDomain)
            ),
            "native-scope domain strings must match ordinally, including domain order"
        );
        _test.True(
            HasFailure(
                counterReport,
                44,
                nameof(LifecycleSoakCounterVector.ProjectionLeasesByDomain)
            ),
            "projection-lease domain strings must match exact domain counts"
        );
        _test.True(
            HasFailure(
                counterReport,
                45,
                nameof(LifecycleSoakCounterVector.SnapshotEpoch)
            ),
            "process snapshot epoch must remain identical after warm-up"
        );

        List<LifecycleSoakSample> fingerprintMismatch = BuildSamples();
        fingerprintMismatch[49] = fingerprintMismatch[49] with
        {
            ActiveCounters = fingerprintMismatch[49].ActiveCounters with
            {
                ProcessContentRootFingerprint = "changed-root",
            },
        };
        LifecycleSoakStatisticsReport fingerprintReport = LifecycleSoakStatistics.Evaluate(
            fingerprintMismatch
        );
        _test.True(
            HasFailure(
                fingerprintReport,
                50,
                nameof(LifecycleSoakCounterVector.ProcessContentRootFingerprint)
            ),
            "canonical process-content root fingerprint changes must fail"
        );
    }

    private void TestViolationAndActivityBalanceFailures()
    {
        List<LifecycleSoakSample> samples = BuildSamples();
        samples[24] = samples[24] with
        {
            ActiveCounters = samples[24].ActiveCounters with
            {
                UnknownOwnershipViolations = 1,
                OwnerConflictViolations = 1,
                EscapedLeaseViolations = 1,
                CloseAfterUseViolations = 1,
                NormalSuppressions = 1,
                QuarantinedWrappers = 1,
            },
            ActivityDelta = new LifecycleSoakActivityDelta(2, 1, 3, 2, 4, 3, 5, 4),
        };

        LifecycleSoakStatisticsReport report = LifecycleSoakStatistics.Evaluate(samples);
        _test.True(
            HasFailure(
                report,
                25,
                nameof(LifecycleSoakCounterVector.UnknownOwnershipViolations)
            ),
            "unknown ownership violations must be zero"
        );
        _test.True(
            HasFailure(
                report,
                25,
                nameof(LifecycleSoakCounterVector.OwnerConflictViolations)
            ),
            "owner conflict violations must be zero"
        );
        _test.True(
            HasFailure(
                report,
                25,
                nameof(LifecycleSoakCounterVector.EscapedLeaseViolations)
            ),
            "escaped lease violations must be zero"
        );
        _test.True(
            HasFailure(
                report,
                25,
                nameof(LifecycleSoakCounterVector.CloseAfterUseViolations)
            ),
            "close-after-use violations must be zero"
        );
        _test.True(
            HasFailure(report, 25, nameof(LifecycleSoakCounterVector.NormalSuppressions)),
            "normal-phase suppressions must be zero"
        );
        _test.True(
            HasFailure(report, 25, nameof(LifecycleSoakCounterVector.QuarantinedWrappers)),
            "quarantined wrappers must be zero"
        );
        _test.True(
            HasFailure(report, 25, "OwnersRegistered/OwnersClosed"),
            "owner creation and closure totals must balance per cycle"
        );
        _test.True(
            HasFailure(report, 25, "NativeWrappersOwned/NativeWrappersDisposed"),
            "native wrapper ownership totals must balance per cycle"
        );
        _test.True(
            HasFailure(
                report,
                25,
                "ProjectionContainersOwned/ProjectionContainersDisposed"
            ),
            "projection ownership totals must balance per cycle"
        );
        _test.True(
            HasFailure(report, 25, "TransfersOut/TransfersIn"),
            "lease transfer totals must balance per cycle"
        );
    }

    private void TestMemoryDeltaAndSlopeFailures()
    {
        List<LifecycleSoakSample> deltaSamples = BuildSamples(
            cycle => cycle >= 101 ? 109 * MiB : 100 * MiB,
            cycle => cycle >= 101 ? 334 * MiB : 300 * MiB
        );
        LifecycleSoakStatisticsReport deltaReport = LifecycleSoakStatistics.Evaluate(
            deltaSamples
        );
        _test.True(
            HasFailure(deltaReport, 110, "ManagedMemoryDeltaBytes"),
            "managed median growth above max(8 MiB, five percent) must fail"
        );
        _test.True(
            HasFailure(deltaReport, 110, "PrivateMemoryDeltaBytes"),
            "private median growth above max(32 MiB, ten percent) must fail"
        );

        List<LifecycleSoakSample> slopeSamples = BuildSamples(
            cycle =>
                100 * MiB
                + Math.Max(cycle - LifecycleSoakStatistics.FirstMeasuredCycle, 0)
                    * (LifecycleSoakStatistics.ManagedMaximumSlopeBytesPerCycle + 1),
            cycle =>
                300 * MiB
                + Math.Max(cycle - LifecycleSoakStatistics.FirstMeasuredCycle, 0)
                    * (LifecycleSoakStatistics.PrivateMaximumSlopeBytesPerCycle + 1)
        );
        LifecycleSoakStatisticsReport slopeReport = LifecycleSoakStatistics.Evaluate(
            slopeSamples
        );
        _test.True(
            HasFailure(slopeReport, 110, "ManagedMemorySlopeBytesPerCycle"),
            "managed slope above 64 KiB per cycle must fail"
        );
        _test.True(
            HasFailure(slopeReport, 110, "PrivateMemorySlopeBytesPerCycle"),
            "private slope above 256 KiB per cycle must fail"
        );
    }

    private static List<LifecycleSoakSample> BuildSamples(
        Func<int, long> managed = null,
        Func<int, long> privateBytes = null
    )
    {
        managed ??= _ => 100 * MiB;
        privateBytes ??= _ => 300 * MiB;
        var result = new List<LifecycleSoakSample>();
        for (int cycle = 1; cycle <= LifecycleSoakStatistics.TotalCycleCount; cycle++)
            result.Add(Sample(cycle, managed(cycle), privateBytes(cycle)));
        return result;
    }

    private static LifecycleSoakSample Sample(int cycle, long managed, long privateBytes) =>
        new(
            cycle,
            StableCounters(),
            new LifecycleSoakActivityDelta(2, 2, 3, 3, 4, 4, 1, 1),
            managed,
            privateBytes
        );

    private static LifecycleSoakCounterVector StableCounters() =>
        new(
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            "Battle=0,Request=0",
            "Battle=0,Request=0",
            1,
            "root|type|Borrowed",
            0,
            0,
            0,
            0,
            0,
            0
        );

    private static bool HasFailure(
        LifecycleSoakStatisticsReport report,
        int cycle,
        string counterName
    )
    {
        foreach (LifecycleSoakFailure failure in report.Failures)
        {
            if (failure.Cycle == cycle && failure.CounterName == counterName)
                return true;
        }
        return false;
    }

    private static string FormatFailures(LifecycleSoakStatisticsReport report)
    {
        var values = new List<string>();
        foreach (LifecycleSoakFailure failure in report.Failures)
            values.Add($"cycle={failure.Cycle} counter={failure.CounterName}: {failure.Message}");
        return string.Join(" | ", values);
    }
}
