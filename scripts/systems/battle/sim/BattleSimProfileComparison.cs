using System.Collections.Generic;

public sealed class BattleSimProfileComparison
{
    public string BaselineProfileId { get; set; } = "";

    public string CandidateProfileId { get; set; } = "";

    public float AverageFinalTuDelta { get; set; }

    public float AverageIterationsDelta { get; set; }

    public float AverageTimelineStepsDelta { get; set; }

    public Dictionary<string, float> WinRateDelta { get; } = new(System.StringComparer.Ordinal);

    public Dictionary<string, int> SkillUsageDelta { get; } = new(System.StringComparer.Ordinal);

    public Dictionary<string, int> SkillAttemptDelta { get; } = new(System.StringComparer.Ordinal);

    public Dictionary<string, int> SkillFailureDelta { get; } = new(System.StringComparer.Ordinal);

    public Dictionary<string, int> ActionChoiceDelta { get; } = new(System.StringComparer.Ordinal);

}
