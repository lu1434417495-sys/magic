using Godot;

public sealed class BattleSimOutputFiles
{
    public string ReportJson { get; set; } = "";

    public string TurnTraceJsonl { get; set; } = "";

    public string TraceSummaryJson { get; set; } = "";

    internal Godot.Collections.Dictionary ToDictionary()
    {
        var payload = new Godot.Collections.Dictionary
        {
            ["report_json"] = ReportJson,
            ["turn_trace_jsonl"] = TurnTraceJsonl,
        };
        if (!string.IsNullOrEmpty(TraceSummaryJson))
            payload["trace_summary_json"] = TraceSummaryJson;
        return payload;
    }
}
