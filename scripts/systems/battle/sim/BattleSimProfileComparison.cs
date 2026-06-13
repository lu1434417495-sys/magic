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

    internal Godot.Collections.Dictionary ToDictionary() =>
        new()
        {
            ["baseline_profile_id"] = BaselineProfileId,
            ["candidate_profile_id"] = CandidateProfileId,
            ["average_final_tu_delta"] = AverageFinalTuDelta,
            ["average_iterations_delta"] = AverageIterationsDelta,
            ["average_timeline_steps_delta"] = AverageTimelineStepsDelta,
            ["win_rate_delta"] = ToFloatDictionary(WinRateDelta),
            ["skill_usage_delta"] = ToIntDictionary(SkillUsageDelta),
            ["skill_attempt_delta"] = ToIntDictionary(SkillAttemptDelta),
            ["skill_failure_delta"] = ToIntDictionary(SkillFailureDelta),
            ["action_choice_delta"] = ToIntDictionary(ActionChoiceDelta),
        };

    private static Godot.Collections.Dictionary ToIntDictionary(
        IReadOnlyDictionary<string, int> source
    )
    {
        var payload = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, int> entry in source)
            payload[entry.Key] = entry.Value;
        return payload;
    }

    private static Godot.Collections.Dictionary ToFloatDictionary(
        IReadOnlyDictionary<string, float> source
    )
    {
        var payload = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, float> entry in source)
            payload[entry.Key] = entry.Value;
        return payload;
    }
}
