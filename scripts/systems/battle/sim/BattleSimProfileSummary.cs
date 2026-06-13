using System.Collections.Generic;

public sealed class BattleSimProfileSummary
{
    public string ProfileId { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public int RunCount { get; set; }

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

    internal Godot.Collections.Dictionary ToDictionary()
    {
        var factionMetricTotals = new Godot.Collections.Dictionary();
        foreach (KeyValuePair<string, BattleSimFactionMetricSummary> entry in FactionMetricTotals)
            factionMetricTotals[entry.Key] = entry.Value?.ToDictionary() ?? new Godot.Collections.Dictionary();

        return new Godot.Collections.Dictionary
        {
            ["profile_id"] = ProfileId,
            ["display_name"] = DisplayName,
            ["run_count"] = RunCount,
            ["wins_by_faction"] = ToIntDictionary(WinsByFaction),
            ["win_rate_by_faction"] = ToFloatDictionary(WinRateByFaction),
            ["average_final_tu"] = AverageFinalTu,
            ["average_iterations"] = AverageIterations,
            ["average_timeline_steps"] = AverageTimelineSteps,
            ["skill_attempt_totals"] = ToIntDictionary(SkillAttemptTotals),
            ["skill_usage_totals"] = ToIntDictionary(SkillUsageTotals),
            ["skill_failure_totals"] = ToIntDictionary(SkillFailureTotals),
            ["action_choice_counts"] = ToIntDictionary(ActionChoiceCounts),
            ["faction_metric_totals"] = factionMetricTotals,
        };
    }

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
