using System;
using System.Collections.Generic;
using Godot;

public sealed class BattleAiCandidateEvaluationService
{
    private static readonly StringName FamilyMoveToRange = BattleAiCandidateRequest.FamilyMoveToRange;

    private readonly System.Collections.Generic.Dictionary<
        StringName,
        Func<BattleAiCandidateRequest, BattleAiQueryService, BattleAiDecision>
    > _evaluators = new();
    private readonly BattleAiMoveToRangeCandidateEvaluator _moveToRangeEvaluator = new();

    public void Setup(BattleAiScoreService scoreService)
    {
        if (scoreService == null)
        {
            BattleAiFailurePolicy.ReportContractError(
                "BattleAiCandidateEvaluationService.Setup requires BattleAiScoreService.",
                SourceMetadata()
            );
        }
    }

    public void RegisterEvaluator(
        StringName familyId,
        Func<BattleAiCandidateRequest, BattleAiQueryService, BattleAiDecision> evaluator
    )
    {
        if (IsEmpty(familyId))
        {
            BattleAiFailurePolicy.ReportContractError(
                "Evaluator family_id must not be empty.",
                SourceMetadata()
            );
            return;
        }
        if (familyId == FamilyMoveToRange)
        {
            BattleAiFailurePolicy.ReportContractError(
                "Evaluator for built-in family move_to_range must not be overridden.",
                SourceMetadata()
            );
            return;
        }
        if (_evaluators.ContainsKey(familyId))
        {
            BattleAiFailurePolicy.ReportContractError(
                $"Evaluator for family {familyId} is already registered.",
                SourceMetadata()
            );
            return;
        }
        if (evaluator == null)
        {
            BattleAiFailurePolicy.ReportContractError(
                $"Evaluator for family {familyId} must be a valid delegate.",
                SourceMetadata()
            );
            return;
        }
        _evaluators[familyId] = evaluator;
    }

    public BattleAiDecision Evaluate(BattleAiCandidateRequest request, BattleAiQueryService query)
    {
        if (request == null)
        {
            return Fail("BattleAiCandidateEvaluationService.Evaluate requires BattleAiCandidateRequest.");
        }
        if (!request.TryValidateCommon(out string error))
        {
            return Fail(error);
        }

        StringName familyId = request.FamilyId;
        if (_evaluators.TryGetValue(familyId, out var evaluator))
        {
            return evaluator.Invoke(request, query);
        }
        if (familyId == FamilyMoveToRange)
        {
            if (!request.TryValidateMoveToRange(out error))
            {
                return Fail(error);
            }
            return EvaluateMoveToRangeRequest(request, query);
        }

        return Fail($"Unsupported candidate family_id {familyId}.");
    }

    public BattleAiDecision EvaluateMoveToRangeRequest(
        BattleAiCandidateRequest request,
        BattleAiQueryService query
    )
    {
        return _moveToRangeEvaluator.EvaluateMoveToRangeRequest(
            request,
            query,
            BuildMoveToRangeCommand,
            BuildMoveToRangeDecision
        );
    }

    private static BattleCommand BuildMoveToRangeCommand(StringName actorUnitId, Vector2I targetCoord)
    {
        return new BattleCommand
        {
            command_type = BattleCommand.TYPE_MOVE(),
            unit_id = actorUnitId,
            target_coord = targetCoord,
        };
    }

    private static BattleAiDecision BuildMoveToRangeDecision(
        BattleAiCandidateRequest request,
        BattleCommand command,
        BattleAiScoreInput scoreInput,
        string targetDisplayName,
        int pathCost
    )
    {
        if (scoreInput != null && !scoreInput.IsSealed())
        {
            scoreInput.Seal();
        }
        string targetLabel = string.IsNullOrEmpty(targetDisplayName)
            ? "目标"
            : targetDisplayName;
        return new BattleAiDecision
        {
            command = command,
            action_id = request.ActionId,
            score_bucket_id = request.ScoreBucketId,
            score_input = scoreInput,
            skill_score_input = scoreInput,
            reason_text =
                $"{request.ActorUnitId} 调整到距离 {targetLabel} 的战术位置（移动消耗 {pathCost}）。",
        };
    }

    private static BattleAiDecision Fail(string message)
    {
        BattleAiFailurePolicy.ReportContractError(message, SourceMetadata());
        return null;
    }

    private static Dictionary<string, string> SourceMetadata() =>
        new(StringComparer.Ordinal)
        {
            ["source"] = "BattleAiCandidateEvaluationService",
        };

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
