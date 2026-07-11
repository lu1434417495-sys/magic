using System.Collections.Generic;
using Godot;

public sealed class AiCandidateSummary
{
    public string Label { get; set; } = "";
    public AiCommandSummary Command { get; set; }
    public int TotalScore { get; set; }
    public Dictionary<string, object> ScoreInput { get; } = new(System.StringComparer.Ordinal);
    public Dictionary<string, object> ExtraFields { get; } = new(System.StringComparer.Ordinal);

    public AiCandidateSummary()
    {
        Command = new AiCommandSummary();
    }

    public AiCandidateSummary(
        string p_label,
        AiCommandSummary p_command = null,
        int p_total_score = 0,
        IReadOnlyDictionary<string, object> p_score_input = null,
        IReadOnlyDictionary<string, object> p_extra_fields = null
    )
    {
        Label = p_label ?? "";
        Command = p_command ?? new AiCommandSummary();
        TotalScore = p_total_score;
        CopyDictionary(ScoreInput, p_score_input);
        CopyDictionary(ExtraFields, p_extra_fields);
    }

    internal AiCandidateSummary Clone()
    {
        return new AiCandidateSummary(
            Label,
            Command?.Clone(),
            TotalScore,
            RuntimePlainPayload.CloneDictionary(ScoreInput),
            RuntimePlainPayload.CloneDictionary(ExtraFields)
        );
    }

    internal static AiCandidateSummary Create(
        string label,
        BattleCommand command,
        BattleAiScoreInput scoreInput = null,
        IReadOnlyDictionary<string, object> extra = null
    )
    {
        int totalScore = scoreInput?.total_score ?? ReadInt(extra, "total_score", 0);
        var summary = new AiCandidateSummary(
            label,
            AiCommandSummary.FromCommand(command),
            totalScore,
            scoreInput != null ? BattleAiScoreProjection.BuildPlain(scoreInput) : null,
            extra
        );
        summary.ExtraFields.Remove("label");
        summary.ExtraFields.Remove("command");
        summary.ExtraFields.Remove("total_score");
        summary.ExtraFields.Remove("score_input");
        return summary;
    }

    internal Dictionary<string, object> ToTraceDictionary()
    {
        var result = new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["label"] = Label,
            ["command"] = Command ?? new AiCommandSummary(),
            ["total_score"] = TotalScore,
            ["score_input"] = new Dictionary<string, object>(ScoreInput, System.StringComparer.Ordinal),
        };
        foreach (KeyValuePair<string, object> entry in ExtraFields)
        {
            if (!string.IsNullOrEmpty(entry.Key))
            {
                result[entry.Key] = entry.Value;
            }
        }
        return result;
    }

    private static void CopyDictionary(
        Dictionary<string, object> target,
        IReadOnlyDictionary<string, object> source
    )
    {
        if (source == null)
        {
            return;
        }
        foreach (KeyValuePair<string, object> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
            {
                target[entry.Key] = entry.Value;
            }
        }
    }

    private static int ReadInt(IReadOnlyDictionary<string, object> source, string key, int fallback)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.TryGetValue(key, out object value))
        {
            return fallback;
        }
        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            float floatValue => (int)floatValue,
            double doubleValue => (int)doubleValue,
            _ => fallback,
        };
    }
}
