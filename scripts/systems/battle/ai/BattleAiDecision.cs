using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiDecision : RefCounted
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
    public GDictionary transition { get; set; } = new();
    public GDictionary trace_counters { get; set; } = new();
    public GDictionary state_patch { get; set; } = new();

    internal BattleAiStateResolver.TransitionResult TypedTransition { get; set; }
    internal BattleAiDecisionCommitter.DecisionStatePatch TypedStatePatch { get; set; }
}
