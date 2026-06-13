using Godot;

public sealed class BattleAiDecision
{
    public BattleCommand command { get; set; }
    public StringName brain_id { get; set; } = "";
    public StringName state_id { get; set; } = "";
    public StringName action_id { get; set; } = "";
    public string reason_text { get; set; } = "";
    public StringName score_bucket_id { get; set; } = "";
    public StringName action_trace_id { get; set; } = "";
    public BattleAiScoreInput skill_score_input { get; set; }
    public BattleAiScoreInput score_input { get; set; }
    internal BattleAiStateResolver.TransitionResult Transition { get; set; }

    internal BattleAiDecisionCommitter.DecisionStatePatch StatePatch { get; set; }
}
