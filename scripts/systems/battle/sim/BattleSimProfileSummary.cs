using System.Collections.Generic;

public sealed class BattleSimProfileSummary
{
    public string ProfileId { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public int RunCount { get; set; }

    public int CompletedRunCount { get; set; }

    public int UnfinishedRunCount { get; set; }

    public int StalledRunCount { get; set; }

    public int IterationBudgetExhaustedRunCount { get; set; }

    public int InvalidRuntimeRunCount { get; set; }

    public bool HasUnfinishedRuns => UnfinishedRunCount > 0;

    public bool IsComplete => RunCount > 0 && !HasUnfinishedRuns;

    public float AverageFinalTu { get; set; }

    public float AverageIterations { get; set; }

    public float AverageTimelineSteps { get; set; }

    public Dictionary<string, int> WinsByFaction { get; } = new(System.StringComparer.Ordinal);

    public Dictionary<string, float> WinRateByFaction { get; } = new(
        System.StringComparer.Ordinal
    );

    public Dictionary<string, int> SkillAttemptTotals { get; } = new(
        System.StringComparer.Ordinal
    );

    public Dictionary<string, int> SkillUsageTotals { get; } = new(
        System.StringComparer.Ordinal
    );

    public Dictionary<string, int> SkillFailureTotals { get; } = new(
        System.StringComparer.Ordinal
    );

    public Dictionary<string, int> ActionChoiceCounts { get; } = new(
        System.StringComparer.Ordinal
    );

    public Dictionary<string, BattleSimFactionMetricSummary> FactionMetricTotals { get; } = new(
        System.StringComparer.Ordinal
    );

}
