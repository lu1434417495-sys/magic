using System.Collections.Generic;

public sealed class BattleSimProfileComparison
{
    public string BaselineProfileId { get; set; } = "";

    public string CandidateProfileId { get; set; } = "";

    public int BaselineRunCount { get; set; }

    public int BaselineCompletedRunCount { get; set; }

    public int CandidateRunCount { get; set; }

    public int CandidateCompletedRunCount { get; set; }

    public bool HasUnfinishedRuns =>
        BaselineCompletedRunCount < BaselineRunCount
        || CandidateCompletedRunCount < CandidateRunCount;

    public bool IsComplete =>
        BaselineCompletedRunCount > 0
        && CandidateCompletedRunCount > 0
        && !HasUnfinishedRuns;

    public float AverageFinalTuDelta { get; set; }

    public float AverageIterationsDelta { get; set; }

    public float AverageTimelineStepsDelta { get; set; }

    public Dictionary<string, float> WinRateDelta { get; } = new(System.StringComparer.Ordinal);

    public Dictionary<string, int> SkillUsageDelta { get; } = new(System.StringComparer.Ordinal);

    public Dictionary<string, int> SkillAttemptDelta { get; } = new(System.StringComparer.Ordinal);

    public Dictionary<string, int> SkillFailureDelta { get; } = new(System.StringComparer.Ordinal);

    public Dictionary<string, int> ActionChoiceDelta { get; } = new(System.StringComparer.Ordinal);

}
