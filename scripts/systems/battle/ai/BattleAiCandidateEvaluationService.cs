using System;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiCandidateEvaluationService : RefCounted
{
    private static readonly StringName FamilyMoveToRange = "move_to_range";

    private readonly System.Collections.Generic.Dictionary<
        StringName,
        Func<BattleAiCandidateRequest, BattleAiQueryService, BattleAiDecision>
    > _evaluators = new();
    private readonly BattleAiMoveToRangeCandidateEvaluator _moveToRangeEvaluator = new();

    public void setup(BattleAiScoreService score_service)
    {
        if (score_service == null)
        {
            GameLog.Error("BattleAiCandidateEvaluationService.setup requires BattleAiScoreService.", "ai.eval.missing_score_service", "ai");
        }
    }

    public void register_evaluator(
        StringName family_id,
        Func<BattleAiCandidateRequest, BattleAiQueryService, BattleAiDecision> evaluator
    )
    {
        if (family_id == FamilyMoveToRange)
        {
            GameLog.Error("Evaluator for built-in family move_to_range must not be overridden.", "ai.eval.builtin_override", "ai");
            return;
        }
        if (_evaluators.ContainsKey(family_id))
        {
            GameLog.Error($"Evaluator for family {family_id} is already registered.", "ai.eval.duplicate_family", "ai");
            return;
        }
        if (evaluator == null)
        {
            GameLog.Error($"Evaluator for family {family_id} must be a valid delegate.", "ai.eval.invalid_delegate", "ai");
            return;
        }
        _evaluators[family_id] = evaluator;
    }

    public BattleAiDecision evaluate(BattleAiCandidateRequest request, BattleAiQueryService query)
    {
        if (request == null)
        {
            GameLog.Error(
                "BattleAiCandidateEvaluationService.evaluate requires BattleAiCandidateRequest.",
                "ai.eval.missing_request",
                "ai"
            );
            return null;
        }

        StringName familyId = request.FamilyId;
        if (_evaluators.TryGetValue(familyId, out var evaluator))
        {
            return evaluator.Invoke(request, query);
        }
        if (familyId == FamilyMoveToRange)
        {
            return evaluate_move_to_range_request(request, query);
        }

        GameLog.Error($"Unsupported candidate family_id {familyId}.", "ai.eval.unsupported_family", "ai");
        return null;
    }

    public BattleAiDecision evaluate_move_to_range_request(
        BattleAiCandidateRequest request,
        BattleAiQueryService query
    )
    {
        return _moveToRangeEvaluator.evaluate_move_to_range_request(
            request,
            query,
            BuildMoveToRangeCommand,
            BuildMoveToRangeDecision
        );
    }

    public BattleCommand BuildMoveToRangeCommand(StringName actor_unit_id, Vector2I target_coord)
    {
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_MOVE(),
            unit_id = actor_unit_id,
            target_coord = target_coord,
        };
    }

    public BattleAiDecision BuildMoveToRangeDecision(
        BattleAiCandidateRequest request,
        BattleCommand command,
        BattleAiScoreInput score_input,
        string target_display_name,
        int path_cost
    )
    {
        if (score_input != null && !score_input.is_sealed())
        {
            score_input.seal();
        }
        string targetLabel = string.IsNullOrEmpty(target_display_name)
            ? "目标"
            : target_display_name;
        return new BattleAiDecision
        {
            command = command,
            action_id = request.ActionId,
            score_bucket_id = request.ScoreBucketId,
            score_input = score_input,
            skill_score_input = score_input,
            reason_text =
                $"{request.ActorUnitId} 调整到距离 {targetLabel} 的战术位置（移动消耗 {path_cost}）。",
        };
    }

    public string _trim_reason(string value)
    {
        return value?.StripEdges() ?? "";
    }

}
