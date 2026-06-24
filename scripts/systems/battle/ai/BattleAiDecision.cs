using Godot;
using System.Collections.Generic;

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
        var disposed = new HashSet<GodotObject>();
        DisposeScoreInput(score_input, disposed);
        if (!ReferenceEquals(skill_score_input, score_input))
            DisposeScoreInput(skill_score_input, disposed);
        DisposeCommand(command, disposed);
        command = null;
        score_input = null;
        skill_score_input = null;
        StatePatch = null;
    }

    private static void DisposeScoreInput(
        BattleAiScoreInput scoreInput,
        HashSet<GodotObject> disposed
    )
    {
        if (scoreInput == null)
            return;
        DisposeCommand(scoreInput.command, disposed);
        DisposePreview(scoreInput.preview, disposed);
        scoreInput.command = null;
        scoreInput.preview = null;
        scoreInput.skill_def = null;
    }

    private static void DisposeCommand(BattleCommand command, HashSet<GodotObject> disposed)
    {
        if (command == null)
            return;
        DisposeGodotObject(command.equipment_instance, disposed);
        command.equipment_instance = null;
    }

    private static void DisposePreview(BattlePreview preview, HashSet<GodotObject> disposed)
    {
        if (preview == null)
            return;
        DisposeGodotObject(preview.hit_preview, disposed);
        preview.hit_preview = null;
    }

    private static void DisposeGodotObject(GodotObject owned, HashSet<GodotObject> disposed)
    {
        if (owned == null || disposed == null || !disposed.Add(owned))
            return;
        GodotObjectLifecycle.DisposeGodotObject(owned);
    }
}
