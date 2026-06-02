using System.Collections.Generic;
using Godot;

public sealed class AiActionTrace
{
    public StringName TraceId { get; set; } = "";
    public string ActionId { get; set; } = "";
    public string ScoreBucketId { get; set; } = "";
    public Dictionary<string, object> Metadata { get; } = new(System.StringComparer.Ordinal);
    public int EvaluationCount { get; set; }
    public int BlockedCount { get; set; }
    public int PreviewRejectCount { get; set; }
    public int CandidateCount { get; set; }
    public Dictionary<string, int> BlockReasons { get; } = new(System.StringComparer.Ordinal);
    public List<AiCandidateSummary> TopCandidates { get; } = new();
    public bool Chosen { get; set; }

    public string BestReasonText { get; set; } = "";
    public AiCommandSummary BestCommand { get; set; }
    public Dictionary<string, object> BestScoreInput { get; } = new(System.StringComparer.Ordinal);
    public string ChosenReasonText { get; set; } = "";
    public AiCommandSummary ChosenCommand { get; set; }
    public Dictionary<string, object> ChosenScoreInput { get; } = new(System.StringComparer.Ordinal);
    public Dictionary<string, int> CandidateTraceCounters { get; } = new(System.StringComparer.Ordinal);

    public bool GateRejected { get; set; }
    public string GateRejectionReason { get; set; } = "";

    public AiActionTrace()
    {
        BestCommand = new AiCommandSummary();
        ChosenCommand = new AiCommandSummary();
    }

    public AiActionTrace(
        StringName p_trace_id,
        string p_action_id = "",
        string p_score_bucket_id = "",
        IReadOnlyDictionary<string, object> p_metadata = null
    )
    {
        TraceId = p_trace_id;
        ActionId = p_action_id ?? "";
        ScoreBucketId = p_score_bucket_id ?? "";
        CopyObjectDictionary(Metadata, p_metadata);
        BestCommand = new AiCommandSummary();
        ChosenCommand = new AiCommandSummary();
    }

    public bool IsEmpty()
    {
        return TraceId == (StringName)"";
    }

    public void Increment(string key, int amount = 1)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }
        switch (key)
        {
            case "evaluation_count":
                EvaluationCount += amount;
                break;
            case "blocked_count":
                BlockedCount += amount;
                break;
            case "preview_reject_count":
                PreviewRejectCount += amount;
                break;
            case "candidate_count":
                CandidateCount += amount;
                break;
            default:
                CandidateTraceCounters[key] = CandidateTraceCounters.GetValueOrDefault(key, 0) + amount;
                break;
        }
    }

    public void AddBlockReason(string reasonKey)
    {
        if (string.IsNullOrEmpty(reasonKey))
        {
            return;
        }
        Increment("blocked_count", 1);
        BlockReasons[reasonKey] = BlockReasons.GetValueOrDefault(reasonKey, 0) + 1;
    }

    public void OfferCandidate(AiCandidateSummary candidate, int keepCount = 5)
    {
        if (candidate == null)
        {
            return;
        }
        Increment("candidate_count", 1);
        TopCandidates.Add(candidate.Clone());
        TopCandidates.Sort((left, right) => right.TotalScore.CompareTo(left.TotalScore));
        int limit = Mathf.Max(keepCount, 0);
        if (TopCandidates.Count > limit)
        {
            TopCandidates.RemoveRange(limit, TopCandidates.Count - limit);
        }
    }

    public void ApplyBestDecision(BattleAiDecision decision)
    {
        if (decision == null)
        {
            return;
        }
        BestReasonText = decision.reason_text ?? "";
        BestCommand = AiCommandSummary.FromCommand(decision.command);
        CopyScoreInput(BestScoreInput, decision.score_input ?? decision.skill_score_input);
        decision.action_trace_id = TraceId;
    }

    public void MarkChosen(BattleAiDecision decision)
    {
        Chosen = true;
        if (decision == null)
        {
            return;
        }
        ChosenReasonText = decision.reason_text ?? "";
        ChosenCommand = AiCommandSummary.FromCommand(decision.command);
        CopyScoreInput(ChosenScoreInput, decision.score_input ?? decision.skill_score_input);
    }

    internal Godot.Collections.Dictionary ToDictionary()
    {
        var top_candidate_dicts = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (var candidate in TopCandidates)
        {
            if (candidate != null)
                top_candidate_dicts.Add(candidate.ToDictionary());
        }

        return new Godot.Collections.Dictionary
        {
            ["trace_id"] = TraceId.ToString(),
            ["action_id"] = ActionId,
            ["score_bucket_id"] = ScoreBucketId,
            ["metadata"] = TraceDictionaryProjection.ToDictionary(Metadata),
            ["evaluation_count"] = EvaluationCount,
            ["blocked_count"] = BlockedCount,
            ["preview_reject_count"] = PreviewRejectCount,
            ["candidate_count"] = CandidateCount,
            ["block_reasons"] = TraceDictionaryProjection.ToDictionary(BlockReasons),
            ["top_candidates"] = top_candidate_dicts,
            ["chosen"] = Chosen,
            ["best_reason_text"] = BestReasonText,
            ["best_command"] = BestCommand != null ? BestCommand.ToDictionary() : new Godot.Collections.Dictionary(),
            ["best_score_input"] = TraceDictionaryProjection.ToDictionary(BestScoreInput),
            ["chosen_reason_text"] = ChosenReasonText,
            ["chosen_command"] = ChosenCommand != null ? ChosenCommand.ToDictionary() : new Godot.Collections.Dictionary(),
            ["chosen_score_input"] = TraceDictionaryProjection.ToDictionary(ChosenScoreInput),
            ["candidate_trace_counters"] = TraceDictionaryProjection.ToDictionary(CandidateTraceCounters),
            ["gate_rejected"] = GateRejected,
            ["gate_rejection_reason"] = GateRejectionReason,
        };
    }

    private static void CopyObjectDictionary(
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

    private static void CopyScoreInput(
        Dictionary<string, object> target,
        BattleAiScoreInput scoreInput
    )
    {
        target.Clear();
        if (scoreInput == null)
        {
            return;
        }
        CopyObjectDictionary(target, TraceDictionaryProjection.FromDictionary(scoreInput.ToDictionary()));
    }
}
