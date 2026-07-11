using System;
using System.Collections.Generic;

internal sealed record LifecycleSoakMemoryStatistics(
    double BaselineMedianBytes,
    double FinalMedianBytes,
    double DeltaBytes,
    double AllowedDeltaBytes,
    double SlopeBytesPerCycle,
    double MaximumSlopeBytesPerCycle
);

internal sealed record LifecycleSoakFailure(int Cycle, string CounterName, string Message);

internal sealed class LifecycleSoakStatisticsReport
{
    private readonly IReadOnlyList<LifecycleSoakFailure> _failures;

    internal LifecycleSoakStatisticsReport(
        int sampleCount,
        LifecycleSoakMemoryStatistics managedMemory,
        LifecycleSoakMemoryStatistics privateMemory,
        IEnumerable<LifecycleSoakFailure> failures
    )
    {
        SampleCount = sampleCount;
        ManagedMemory = managedMemory;
        PrivateMemory = privateMemory;
        _failures = new List<LifecycleSoakFailure>(failures).AsReadOnly();
    }

    internal int SampleCount { get; }

    internal LifecycleSoakMemoryStatistics ManagedMemory { get; }

    internal LifecycleSoakMemoryStatistics PrivateMemory { get; }

    internal IReadOnlyList<LifecycleSoakFailure> Failures => _failures;

    internal bool Passed => _failures.Count == 0;
}

internal static class LifecycleSoakStatistics
{
    internal const int TotalCycleCount = 110;
    internal const int CounterBaselineCycle = 10;
    internal const int FirstMeasuredCycle = 11;
    internal const int LastMeasuredCycle = 110;

    internal const long ManagedAbsoluteAllowedGrowthBytes = 8L * 1024 * 1024;
    internal const double ManagedAllowedGrowthFraction = 0.05;
    internal const long PrivateAbsoluteAllowedGrowthBytes = 32L * 1024 * 1024;
    internal const double PrivateAllowedGrowthFraction = 0.10;
    internal const long ManagedMaximumSlopeBytesPerCycle = 64L * 1024;
    internal const long PrivateMaximumSlopeBytesPerCycle = 256L * 1024;

    private const int BaselineWindowEndCycle = 20;
    private const int FinalWindowStartCycle = 101;

    internal static double Median(IReadOnlyList<long> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new ArgumentException("Median requires at least one value.", nameof(values));

        var ordered = new long[values.Count];
        for (int index = 0; index < values.Count; index++)
            ordered[index] = values[index];
        Array.Sort(ordered);

        int middle = ordered.Length / 2;
        if ((ordered.Length & 1) == 1)
            return ordered[middle];

        return ordered[middle - 1] / 2.0 + ordered[middle] / 2.0;
    }

    internal static double LeastSquaresSlope(
        IReadOnlyList<LifecycleSoakSample> samples,
        Func<LifecycleSoakSample, long> valueSelector
    )
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(valueSelector);
        if (samples.Count < 2)
        {
            throw new ArgumentException(
                "Least-squares slope requires at least two samples.",
                nameof(samples)
            );
        }

        double meanCycle = 0;
        double meanValue = 0;
        for (int index = 0; index < samples.Count; index++)
        {
            LifecycleSoakSample sample = samples[index]
                ?? throw new ArgumentException("Samples cannot contain null values.", nameof(samples));
            meanCycle += sample.Cycle;
            meanValue += valueSelector(sample);
        }
        meanCycle /= samples.Count;
        meanValue /= samples.Count;

        double numerator = 0;
        double denominator = 0;
        for (int index = 0; index < samples.Count; index++)
        {
            LifecycleSoakSample sample = samples[index];
            double centeredCycle = sample.Cycle - meanCycle;
            numerator += centeredCycle * (valueSelector(sample) - meanValue);
            denominator += centeredCycle * centeredCycle;
        }

        if (denominator == 0)
            throw new ArgumentException("Sample cycles must not all be equal.", nameof(samples));

        return numerator / denominator;
    }

    internal static double CalculateAllowedGrowth(
        double baselineBytes,
        long absoluteMinimumBytes,
        double allowedFraction
    )
    {
        if (baselineBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(baselineBytes));
        if (absoluteMinimumBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteMinimumBytes));
        if (allowedFraction < 0)
            throw new ArgumentOutOfRangeException(nameof(allowedFraction));

        return Math.Max(absoluteMinimumBytes, baselineBytes * allowedFraction);
    }

    internal static LifecycleSoakStatisticsReport Evaluate(
        IReadOnlyList<LifecycleSoakSample> samples
    )
    {
        LifecycleSoakSample[] ordered = OrderAndValidateSamples(samples);
        var failures = new FailureCollector();

        for (int index = 0; index < ordered.Length; index++)
        {
            LifecycleSoakSample sample = ordered[index];
            ValidateZeroCounters(sample, failures);
            ValidateActivityBalance(sample, failures);
        }

        LifecycleSoakCounterVector baseline = ordered[CounterBaselineCycle - 1].ActiveCounters;
        for (int cycle = FirstMeasuredCycle; cycle <= LastMeasuredCycle; cycle++)
            CompareCounterVectors(cycle, baseline, ordered[cycle - 1].ActiveCounters, failures);

        LifecycleSoakMemoryStatistics managed = AnalyzeMemory(
            ordered,
            sample => sample.ManagedMemoryBytes,
            ManagedAbsoluteAllowedGrowthBytes,
            ManagedAllowedGrowthFraction,
            ManagedMaximumSlopeBytesPerCycle,
            "ManagedMemory"
        );
        AddMemoryFailures(managed, "ManagedMemory", failures);

        LifecycleSoakMemoryStatistics privateMemory = AnalyzeMemory(
            ordered,
            sample => sample.PrivateMemoryBytes,
            PrivateAbsoluteAllowedGrowthBytes,
            PrivateAllowedGrowthFraction,
            PrivateMaximumSlopeBytesPerCycle,
            "PrivateMemory"
        );
        AddMemoryFailures(privateMemory, "PrivateMemory", failures);

        return new LifecycleSoakStatisticsReport(
            ordered.Length,
            managed,
            privateMemory,
            failures.Items
        );
    }

    private static LifecycleSoakSample[] OrderAndValidateSamples(
        IReadOnlyList<LifecycleSoakSample> samples
    )
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count != TotalCycleCount)
        {
            throw new ArgumentException(
                $"Lifecycle soak requires exactly {TotalCycleCount} samples.",
                nameof(samples)
            );
        }

        var ordered = new LifecycleSoakSample[TotalCycleCount];
        for (int index = 0; index < samples.Count; index++)
        {
            LifecycleSoakSample sample = samples[index]
                ?? throw new ArgumentException("Samples cannot contain null values.", nameof(samples));
            if (sample.Cycle < 1 || sample.Cycle > TotalCycleCount)
            {
                throw new ArgumentException(
                    $"Sample cycle {sample.Cycle} is outside 1-{TotalCycleCount}.",
                    nameof(samples)
                );
            }
            if (ordered[sample.Cycle - 1] != null)
            {
                throw new ArgumentException(
                    $"Sample cycle {sample.Cycle} appears more than once.",
                    nameof(samples)
                );
            }
            if (sample.ActiveCounters == null || sample.ActivityDelta == null)
            {
                throw new ArgumentException(
                    $"Sample cycle {sample.Cycle} has incomplete lifecycle data.",
                    nameof(samples)
                );
            }

            ordered[sample.Cycle - 1] = sample;
        }

        return ordered;
    }

    private static LifecycleSoakMemoryStatistics AnalyzeMemory(
        LifecycleSoakSample[] samples,
        Func<LifecycleSoakSample, long> valueSelector,
        long absoluteMinimumBytes,
        double allowedFraction,
        long maximumSlopeBytesPerCycle,
        string counterPrefix
    )
    {
        var baselineValues = new long[BaselineWindowEndCycle - FirstMeasuredCycle + 1];
        for (int cycle = FirstMeasuredCycle; cycle <= BaselineWindowEndCycle; cycle++)
            baselineValues[cycle - FirstMeasuredCycle] = valueSelector(samples[cycle - 1]);

        var finalValues = new long[LastMeasuredCycle - FinalWindowStartCycle + 1];
        for (int cycle = FinalWindowStartCycle; cycle <= LastMeasuredCycle; cycle++)
            finalValues[cycle - FinalWindowStartCycle] = valueSelector(samples[cycle - 1]);

        var measuredSamples = new LifecycleSoakSample[
            LastMeasuredCycle - FirstMeasuredCycle + 1
        ];
        for (int cycle = FirstMeasuredCycle; cycle <= LastMeasuredCycle; cycle++)
            measuredSamples[cycle - FirstMeasuredCycle] = samples[cycle - 1];

        double baselineMedian = Median(baselineValues);
        double finalMedian = Median(finalValues);
        return new LifecycleSoakMemoryStatistics(
            baselineMedian,
            finalMedian,
            finalMedian - baselineMedian,
            CalculateAllowedGrowth(baselineMedian, absoluteMinimumBytes, allowedFraction),
            LeastSquaresSlope(measuredSamples, valueSelector),
            maximumSlopeBytesPerCycle
        );
    }

    private static void AddMemoryFailures(
        LifecycleSoakMemoryStatistics statistics,
        string counterPrefix,
        FailureCollector failures
    )
    {
        if (statistics.DeltaBytes > statistics.AllowedDeltaBytes)
        {
            failures.Add(
                LastMeasuredCycle,
                $"{counterPrefix}DeltaBytes",
                $"delta {statistics.DeltaBytes} exceeds allowed {statistics.AllowedDeltaBytes}."
            );
        }
        if (statistics.SlopeBytesPerCycle > statistics.MaximumSlopeBytesPerCycle)
        {
            failures.Add(
                LastMeasuredCycle,
                $"{counterPrefix}SlopeBytesPerCycle",
                $"slope {statistics.SlopeBytesPerCycle} exceeds allowed {statistics.MaximumSlopeBytesPerCycle}."
            );
        }
    }

    private static void ValidateZeroCounters(
        LifecycleSoakSample sample,
        FailureCollector failures
    )
    {
        LifecycleSoakCounterVector counters = sample.ActiveCounters;
        AddZeroFailure(
            sample.Cycle,
            nameof(LifecycleSoakCounterVector.UnknownOwnershipViolations),
            counters.UnknownOwnershipViolations,
            failures
        );
        AddZeroFailure(
            sample.Cycle,
            nameof(LifecycleSoakCounterVector.OwnerConflictViolations),
            counters.OwnerConflictViolations,
            failures
        );
        AddZeroFailure(
            sample.Cycle,
            nameof(LifecycleSoakCounterVector.EscapedLeaseViolations),
            counters.EscapedLeaseViolations,
            failures
        );
        AddZeroFailure(
            sample.Cycle,
            nameof(LifecycleSoakCounterVector.CloseAfterUseViolations),
            counters.CloseAfterUseViolations,
            failures
        );
        AddZeroFailure(
            sample.Cycle,
            nameof(LifecycleSoakCounterVector.NormalSuppressions),
            counters.NormalSuppressions,
            failures
        );
        AddZeroFailure(
            sample.Cycle,
            nameof(LifecycleSoakCounterVector.QuarantinedWrappers),
            counters.QuarantinedWrappers,
            failures
        );
    }

    private static void AddZeroFailure(
        int cycle,
        string counterName,
        int value,
        FailureCollector failures
    )
    {
        if (value != 0)
            failures.Add(cycle, counterName, $"expected zero but was {value}.");
    }

    private static void ValidateActivityBalance(
        LifecycleSoakSample sample,
        FailureCollector failures
    )
    {
        LifecycleSoakActivityDelta activity = sample.ActivityDelta;
        AddBalanceFailure(
            sample.Cycle,
            "OwnersRegistered/OwnersClosed",
            activity.OwnersRegistered,
            activity.OwnersClosed,
            failures
        );
        AddBalanceFailure(
            sample.Cycle,
            "NativeWrappersOwned/NativeWrappersDisposed",
            activity.NativeWrappersOwned,
            activity.NativeWrappersDisposed,
            failures
        );
        AddBalanceFailure(
            sample.Cycle,
            "ProjectionContainersOwned/ProjectionContainersDisposed",
            activity.ProjectionContainersOwned,
            activity.ProjectionContainersDisposed,
            failures
        );
        AddBalanceFailure(
            sample.Cycle,
            "TransfersOut/TransfersIn",
            activity.TransfersOut,
            activity.TransfersIn,
            failures
        );
    }

    private static void AddBalanceFailure(
        int cycle,
        string counterName,
        long created,
        long closed,
        FailureCollector failures
    )
    {
        if (created != closed)
            failures.Add(cycle, counterName, $"created {created} does not match closed {closed}.");
    }

    private static void CompareCounterVectors(
        int cycle,
        LifecycleSoakCounterVector expected,
        LifecycleSoakCounterVector actual,
        FailureCollector failures
    )
    {
        if (expected == actual)
            return;

        AddDifference(cycle, nameof(expected.SessionOwners), expected.SessionOwners, actual.SessionOwners, failures);
        AddDifference(cycle, nameof(expected.BattleOwners), expected.BattleOwners, actual.BattleOwners, failures);
        AddDifference(cycle, nameof(expected.DecisionOwners), expected.DecisionOwners, actual.DecisionOwners, failures);
        AddDifference(cycle, nameof(expected.RequestOwners), expected.RequestOwners, actual.RequestOwners, failures);
        AddDifference(cycle, nameof(expected.SceneTreeOwners), expected.SceneTreeOwners, actual.SceneTreeOwners, failures);
        AddDifference(cycle, nameof(expected.ContentBorrowers), expected.ContentBorrowers, actual.ContentBorrowers, failures);
        AddDifference(cycle, nameof(expected.ActiveJobs), expected.ActiveJobs, actual.ActiveJobs, failures);
        AddOrdinalStringDifference(cycle, nameof(expected.NativeScopesByDomain), expected.NativeScopesByDomain, actual.NativeScopesByDomain, failures);
        AddOrdinalStringDifference(cycle, nameof(expected.ProjectionLeasesByDomain), expected.ProjectionLeasesByDomain, actual.ProjectionLeasesByDomain, failures);
        AddDifference(cycle, nameof(expected.SnapshotEpoch), expected.SnapshotEpoch, actual.SnapshotEpoch, failures);
        AddOrdinalStringDifference(cycle, nameof(expected.ProcessContentRootFingerprint), expected.ProcessContentRootFingerprint, actual.ProcessContentRootFingerprint, failures);
        AddDifference(cycle, nameof(expected.UnknownOwnershipViolations), expected.UnknownOwnershipViolations, actual.UnknownOwnershipViolations, failures);
        AddDifference(cycle, nameof(expected.OwnerConflictViolations), expected.OwnerConflictViolations, actual.OwnerConflictViolations, failures);
        AddDifference(cycle, nameof(expected.EscapedLeaseViolations), expected.EscapedLeaseViolations, actual.EscapedLeaseViolations, failures);
        AddDifference(cycle, nameof(expected.CloseAfterUseViolations), expected.CloseAfterUseViolations, actual.CloseAfterUseViolations, failures);
        AddDifference(cycle, nameof(expected.NormalSuppressions), expected.NormalSuppressions, actual.NormalSuppressions, failures);
        AddDifference(cycle, nameof(expected.QuarantinedWrappers), expected.QuarantinedWrappers, actual.QuarantinedWrappers, failures);
    }

    private static void AddDifference<T>(
        int cycle,
        string counterName,
        T expected,
        T actual,
        FailureCollector failures
    )
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            failures.Add(cycle, counterName, $"expected {expected} but was {actual}.");
    }

    private static void AddOrdinalStringDifference(
        int cycle,
        string counterName,
        string expected,
        string actual,
        FailureCollector failures
    )
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            failures.Add(cycle, counterName, $"expected '{expected}' but was '{actual}'.");
    }

    private sealed class FailureCollector
    {
        private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
        private readonly List<LifecycleSoakFailure> _items = new();

        internal IReadOnlyList<LifecycleSoakFailure> Items => _items;

        internal void Add(int cycle, string counterName, string message)
        {
            if (_keys.Add($"{cycle}:{counterName}"))
                _items.Add(new LifecycleSoakFailure(cycle, counterName, message));
        }
    }
}
