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

    internal void DisposeOwnedGodotObjects()
    {
        DisposeScoreInput(score_input);
        if (!ReferenceEquals(skill_score_input, score_input))
            DisposeScoreInput(skill_score_input);
        DisposeCommand(command);
        command = null;
        score_input = null;
        skill_score_input = null;
        StatePatch = null;
    }

    private static void DisposeScoreInput(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
            return;
        DisposeCommand(scoreInput.command);
        DisposePreview(scoreInput.preview);
        scoreInput.command = null;
        scoreInput.preview = null;
    }

    private static void DisposeCommand(BattleCommand command)
    {
        if (command == null)
            return;
        command.equipment_instance = null;
    }

    private static void DisposePreview(BattlePreview preview)
    {
        if (preview == null)
            return;
        preview.hit_preview = null;
    }
}
