using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using System;

internal delegate void BattleAiActionMutationCheckpoint(
    BattleAiContext context,
    EnemyAiAction action,
    int actionIndex,
    string stage
);

internal sealed class BattleAiDecisionEngine
{
    private static readonly StringName ArcherSurvivalBucketId = "archer_survival";

    private sealed class ScoreInputFacts
    {
        public StringName ScoreBucketId = "";
        public int EstimatedFriendlyLethalTargetCount;
        public int EstimatedFriendlyFireTargetCount;
        public int FriendlyFirePenaltyScore;
        public int EstimatedLethalThreatTargetCount;
        public int EstimatedLethalTargetCount;
        public int TotalScore;
        public int HitPayoffScore;
        public int EffectiveTargetCount;
        public int ResourceCostScore;
        public int ScoreBucketPriority;
        public int TargetCount;
        public int PositionObjectiveScore;
        public int EnemyTargetCount;
        public int AllyTargetCount;
        public int EstimatedDamage;
        public int EstimatedControlCount;
        public int PositionCurrentDistance;
        public int PositionSafeDistance;
        public int DistanceToPrimaryCoord;
        public bool HasPostActionThreatProjection;
        public bool PreActionIsLethalSurvivalRisk;
        public bool PostActionIsLethalSurvivalRisk;
        public int PreActionThreatExpectedDamage;
        public int PostActionRemainingThreatExpectedDamage;
        public int PostActionSurvivalMargin;
        public int PostActionRemainingThreatCount;
    }

    internal BattleAiDecision ChooseCommandImpl(
        BattleAiContext context,
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> enemyAiBrains,
        BattleAiStateResolver stateResolver,
        System.Func<BattleAiContext, StringName, StringName, StringName, string, BattleAiDecision> waitDecisionFactory,
        BattleAiScoreService scoreService,
        BattleAiActionMutationCheckpoint mutationCheckpoint = null
    )
    {
        if (context == null)
        {
            return null;
        }

        BattleUnitState unitState = context.unit_state;
        if (unitState == null)
        {
            return null;
        }

        StringName unitBrainId = unitState.ai_brain_id;
        EnemyAiBrainDef brain = ResolveBrain(enemyAiBrains, unitBrainId);
        if (brain == null)
        {
            BattleAiDecision missingBrainDecision = BuildWaitDecision(
                waitDecisionFactory,
                context,
                new StringName(""),
                new StringName(""),
                new StringName("wait_missing_brain"),
                $"{unitState.display_name} 缺少正式 AI brain，改为待机。"
            );
            AttachPatchAndMark(context, missingBrainDecision);
            return missingBrainDecision;
        }

        StringName brainId = brain.brain_id;

        BattleAiStateResolver.TransitionResult transitionResult =
            stateResolver != null
                ? stateResolver.ResolveTyped(context, brain)
                : BattleAiStateResolver.TransitionResult.Empty();
        StringName nextStateId =
            transitionResult != null && !IsEmpty(transitionResult.StateId)
                ? transitionResult.StateId
                : brain.default_state_id;
        EnemyAiStateDef stateDef = brain.GetState(nextStateId);
        if (stateDef == null)
        {
            BattleAiDecision missingStateDecision = BuildWaitDecision(
                waitDecisionFactory,
                context,
                brainId,
                nextStateId,
                new StringName("wait_missing_state"),
                $"{unitState.display_name} 找不到 AI 状态 {nextStateId}，改为待机。"
            );
            PrepareDecision(missingStateDecision, brainId, nextStateId, transitionResult, null);
            AttachPatchAndMark(context, missingStateDecision);
            return missingStateDecision;
        }

        RuntimeActionResolution actionResolution = ResolveRuntimeActions(
            context,
            brain,
            nextStateId,
            stateDef
        );
        StringName waitActionId = actionResolution.WaitActionId;
        if (!IsEmpty(waitActionId))
        {
            BattleAiDecision runtimeWaitDecision = BuildWaitDecision(
                waitDecisionFactory,
                context,
                brainId,
                nextStateId,
                waitActionId,
                actionResolution.WaitReasonText
            );
            PrepareDecision(runtimeWaitDecision, brainId, nextStateId, transitionResult, null);
            AttachPatchAndMark(context, runtimeWaitDecision);
            return runtimeWaitDecision;
        }

        BattleAiDecision bestScoredDecision = null;
        int bestScoredActionIndex = int.MaxValue;
        BattleAiDecision fallbackDecision = null;
        IReadOnlyList<EnemyAiAction> actions = actionResolution.Actions;
        for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
        {
            EnemyAiAction action = actions[actionIndex];
            if (action == null)
            {
                continue;
            }

            BattleAiRuntimeActionPlan.RuntimeActionMetadata actionMetadata =
                context.GetRuntimeActionMetadataTyped(action);
            context.PushActionMetadata(actionMetadata);
            BattleAiDecision decision;
            try
            {
                mutationCheckpoint?.Invoke(context, action, actionIndex, "before_action");
                try
                {
                    decision = EvaluateAction(context, action);
                }
                finally
                {
                    mutationCheckpoint?.Invoke(context, action, actionIndex, "after_action");
                }
            }
            finally
            {
                context.PopActionMetadata();
            }

            if (decision == null || decision.command == null)
            {
                continue;
            }

            PrepareDecision(decision, brainId, nextStateId, transitionResult, null);
            ApplyActionMetadataToDecision(decision, actionMetadata, scoreService);

            BattleAiScoreInput scoreInput = GetDecisionScoreInput(decision);
            if (scoreInput != null)
            {
                if (!BattleAiSafetyGate.IsEligible(scoreInput))
                {
                    continue;
                }
                if (
                    ShouldReplaceScoredDecision(
                        decision,
                        actionIndex,
                        bestScoredDecision,
                        bestScoredActionIndex
                    )
                )
                {
                    bestScoredDecision = decision;
                    bestScoredActionIndex = actionIndex;
                }
                continue;
            }

            fallbackDecision ??= decision;
        }

        BattleAiDecision resolvedDecision = bestScoredDecision ?? fallbackDecision;
        if (resolvedDecision != null)
        {
            AttachPatchAndMark(context, resolvedDecision);
            return resolvedDecision;
        }

        BattleAiDecision waitDecision = BuildWaitDecision(
            waitDecisionFactory,
            context,
            brainId,
            nextStateId,
            new StringName("wait_fallback"),
            $"{unitState.display_name} 在状态 {nextStateId} 下没有找到合法指令，改为待机。"
        );
        PrepareDecision(waitDecision, brainId, nextStateId, transitionResult, null);
        AttachPatchAndMark(context, waitDecision);
        return waitDecision;
    }

    public bool IsBetterScoreInput(BattleAiScoreInput candidate, BattleAiScoreInput bestCandidate)
    {
        return CompareScoreInput(candidate, bestCandidate);
    }

    private static BattleAiDecision EvaluateAction(BattleAiContext context, EnemyAiAction action)
    {
        if (action == null)
        {
            return null;
        }

        if (action.UsesCandidateRequest())
        {
            return EvaluateCandidateAction(context, action);
        }

        return DecideWithActionFallback(context, action);
    }

    private static BattleAiDecision EvaluateCandidateAction(BattleAiContext context, EnemyAiAction action)
    {
        if (context == null)
        {
            return FailCandidateAction(
                action,
                "candidate_request action requires BattleAiContext.GetAiQueryService()."
            );
        }

        BattleAiQueryService query = context.GetAiQueryService();
        if (query == null)
        {
            return FailCandidateAction(
                action,
                "candidate_request action requires a non-null AI query service."
            );
        }

        BattleAiCandidateRequest request = action.BuildCandidateRequest(query);
        if (request == null)
        {
            return FailCandidateAction(action, "candidate_request action returned null request.");
        }

        return context.EvaluateCandidateRequest(request);
    }

    private static BattleAiDecision FailCandidateAction(EnemyAiAction action, string message)
    {
        StringName actionId = !IsEmpty(action.action_id)
            ? action.action_id
            : "anonymous_action";
        string fullMessage = $"AI action {actionId} failed: {message}";
        GameLog.Error(fullMessage, "ai.decision.no_candidates", "ai");
        return null;
    }

    private static BattleAiDecision DecideWithActionFallback(
        BattleAiContext context,
        EnemyAiAction action
    )
    {
        return action?.Decide(context);
    }

    private static RuntimeActionResolution ResolveRuntimeActions(
        BattleAiContext context,
        EnemyAiBrainDef brain,
        StringName stateId,
        EnemyAiStateDef stateDef
    )
    {
        if (context == null)
        {
            return RuntimeActionResolution.ForActions(System.Array.Empty<EnemyAiAction>());
        }

        BattleAiRuntimeActionPlan runtimeActionPlan = context.runtime_action_plan;
        if (runtimeActionPlan != null)
        {
            if (context.IsRuntimeActionPlanStale(brain))
            {
                return RuntimeActionResolution.ForWait(
                    "wait_stale_runtime_plan",
                    $"{context.unit_state.display_name} 的 AI runtime plan 已过期，改为待机。"
                );
            }
            if (!context.HasRuntimeActionState(stateId))
            {
                return RuntimeActionResolution.ForWait(
                    "wait_missing_runtime_plan",
                    $"{context.unit_state.display_name} 缺少状态 {stateId} 的 AI runtime plan，改为待机。"
                );
            }
            IReadOnlyList<EnemyAiAction> runtimeActions = context.GetRuntimeActionsTyped(stateId);
            if (runtimeActions.Count == 0)
            {
                return RuntimeActionResolution.ForWait(
                    "wait_empty_runtime_state",
                    $"{context.unit_state.display_name} 的 AI runtime state {stateId} 没有可评估 action，改为待机。"
                );
            }
            return RuntimeActionResolution.ForActions(runtimeActions);
        }

        if (context.allow_authored_action_fallback_for_tests)
        {
            return RuntimeActionResolution.ForActions(
                stateDef != null ? stateDef.GetTypedActions() : new List<EnemyAiAction>()
            );
        }

        return RuntimeActionResolution.ForWait(
            "wait_missing_runtime_plan",
            $"{context.unit_state.display_name} 缺少 AI runtime plan，改为待机。"
        );
    }

    private sealed class RuntimeActionResolution
    {
        public IReadOnlyList<EnemyAiAction> Actions = System.Array.Empty<EnemyAiAction>();
        public StringName WaitActionId = "";
        public string WaitReasonText = "";

        public static RuntimeActionResolution ForActions(IReadOnlyList<EnemyAiAction> actions)
        {
            return new RuntimeActionResolution
            {
                Actions = actions ?? System.Array.Empty<EnemyAiAction>(),
            };
        }

        public static RuntimeActionResolution ForWait(StringName waitActionId, string reasonText)
        {
            return new RuntimeActionResolution
            {
                WaitActionId = waitActionId,
                WaitReasonText = reasonText ?? "",
            };
        }
    }

    private static BattleAiDecision BuildWaitDecision(
        System.Func<BattleAiContext, StringName, StringName, StringName, string, BattleAiDecision> waitDecisionFactory,
        BattleAiContext context,
        StringName brainId,
        StringName stateId,
        StringName actionId,
        string reasonText
    )
    {
        return waitDecisionFactory?.Invoke(context, brainId, stateId, actionId, reasonText);
    }

    private static void PrepareDecision(
        BattleAiDecision decision,
        StringName brainId,
        StringName stateId,
        BattleAiStateResolver.TransitionResult transitionResult,
        BattleAiScoreInput scoreInputOverride
    )
    {
        if (decision == null)
        {
            return;
        }

        decision.brain_id = brainId;
        decision.state_id = stateId;
        decision.Transition = transitionResult;
        if (IsEmpty(decision.action_id))
        {
            decision.action_id = new StringName("anonymous_action");
        }

        BattleAiScoreInput scoreInput = scoreInputOverride ?? GetDecisionScoreInput(decision);
        if (IsEmpty(decision.score_bucket_id) && scoreInput != null)
        {
            decision.score_bucket_id = scoreInput.score_bucket_id;
        }
    }

    private static void ApplyActionMetadataToDecision(
        BattleAiDecision decision,
        BattleAiRuntimeActionPlan.RuntimeActionMetadata metadata,
        BattleAiScoreService scoreService
    )
    {
        if (decision == null || metadata == null)
        {
            return;
        }

        StringName metadataBucketId = metadata.score_bucket_id;
        if (!IsEmpty(metadataBucketId))
        {
            decision.score_bucket_id = metadataBucketId;
        }

        BattleAiScoreInput scoreInput = GetDecisionScoreInput(decision);
        if (scoreInput == null)
        {
            return;
        }

        if (!IsEmpty(metadataBucketId))
        {
            scoreInput.score_bucket_id = metadataBucketId;
            int priority =
                scoreService != null ? scoreService.GetBucketPriority(metadataBucketId) : 0;
            scoreInput.score_bucket_priority = priority;
        }

        BattleAiScoreRuntimeMetadata currentRuntimeMetadata =
            scoreInput.runtime_action_metadata ?? new BattleAiScoreRuntimeMetadata();
        BattleAiScoreRuntimeMetadata runtimeActionMetadata =
            BattleAiScoreRuntimeMetadata.FromRuntimeActionExportMetadata(
                metadata.runtime_action_metadata
            );
        if (currentRuntimeMetadata.IsEmpty() && !runtimeActionMetadata.IsEmpty())
        {
            scoreInput.runtime_action_metadata = runtimeActionMetadata.Clone();
        }
    }

    private static bool ShouldReplaceScoredDecision(
        BattleAiDecision candidate,
        int candidateActionIndex,
        BattleAiDecision bestCandidate,
        int bestActionIndex
    )
    {
        BattleAiScoreInput candidateScore = GetDecisionScoreInput(candidate);
        if (candidateScore == null)
        {
            return false;
        }
        BattleAiScoreInput bestScore = GetDecisionScoreInput(bestCandidate);
        if (bestScore == null)
        {
            return true;
        }
        if (CompareScoreInput(candidateScore, bestScore))
        {
            return true;
        }
        if (CompareScoreInput(bestScore, candidateScore))
        {
            return false;
        }
        return candidateActionIndex < bestActionIndex;
    }

    private static bool CompareScoreInput(
        BattleAiScoreInput candidate,
        BattleAiScoreInput bestCandidate
    )
    {
        if (candidate == null)
        {
            return false;
        }
        if (bestCandidate == null)
        {
            return true;
        }
        ScoreInputFacts candidateFacts = BuildScoreInputFacts(candidate);
        ScoreInputFacts bestFacts = BuildScoreInputFacts(bestCandidate);
        return CompareScoreInput(candidateFacts, bestFacts);
    }

    private static bool CompareScoreInput(ScoreInputFacts candidate, ScoreInputFacts bestCandidate)
    {
        if (candidate == null)
        {
            return false;
        }
        if (bestCandidate == null)
        {
            return true;
        }
        if (
            candidate.EstimatedFriendlyLethalTargetCount
            != bestCandidate.EstimatedFriendlyLethalTargetCount
        )
        {
            return candidate.EstimatedFriendlyLethalTargetCount
                < bestCandidate.EstimatedFriendlyLethalTargetCount;
        }
        if (
            candidate.EstimatedFriendlyFireTargetCount
            != bestCandidate.EstimatedFriendlyFireTargetCount
        )
        {
            return candidate.EstimatedFriendlyFireTargetCount
                < bestCandidate.EstimatedFriendlyFireTargetCount;
        }
        if (candidate.FriendlyFirePenaltyScore != bestCandidate.FriendlyFirePenaltyScore)
        {
            return candidate.FriendlyFirePenaltyScore < bestCandidate.FriendlyFirePenaltyScore;
        }

        int survivalRiskComparison = ComparePostActionSurvivalRisk(candidate, bestCandidate);
        if (survivalRiskComparison != 0)
        {
            return survivalRiskComparison > 0;
        }
        if (
            candidate.EstimatedLethalThreatTargetCount
            != bestCandidate.EstimatedLethalThreatTargetCount
        )
        {
            return candidate.EstimatedLethalThreatTargetCount
                > bestCandidate.EstimatedLethalThreatTargetCount;
        }
        if (candidate.EstimatedLethalTargetCount != bestCandidate.EstimatedLethalTargetCount)
        {
            return candidate.EstimatedLethalTargetCount > bestCandidate.EstimatedLethalTargetCount;
        }

        bool candidateEmergency = IsEmergencySurvivalScoreInput(candidate);
        bool bestEmergency = IsEmergencySurvivalScoreInput(bestCandidate);
        if (candidateEmergency != bestEmergency)
        {
            return candidateEmergency;
        }

        if (
            candidate.EstimatedLethalTargetCount > 0
            && bestCandidate.EstimatedLethalTargetCount > 0
        )
        {
            if (candidate.TotalScore != bestCandidate.TotalScore)
            {
                return candidate.TotalScore > bestCandidate.TotalScore;
            }
            if (candidate.HitPayoffScore != bestCandidate.HitPayoffScore)
            {
                return candidate.HitPayoffScore > bestCandidate.HitPayoffScore;
            }
            if (candidate.EffectiveTargetCount != bestCandidate.EffectiveTargetCount)
            {
                return candidate.EffectiveTargetCount > bestCandidate.EffectiveTargetCount;
            }
            int lethalNonfatalRiskComparison = CompareNonfatalPostActionSurvivalRisk(
                candidate,
                bestCandidate
            );
            if (lethalNonfatalRiskComparison != 0)
            {
                return lethalNonfatalRiskComparison > 0;
            }
            if (candidate.ResourceCostScore != bestCandidate.ResourceCostScore)
            {
                return candidate.ResourceCostScore < bestCandidate.ResourceCostScore;
            }
        }

        if (candidate.ScoreBucketPriority != bestCandidate.ScoreBucketPriority)
        {
            return candidate.ScoreBucketPriority > bestCandidate.ScoreBucketPriority;
        }
        if (candidate.TotalScore != bestCandidate.TotalScore)
        {
            return candidate.TotalScore > bestCandidate.TotalScore;
        }
        if (candidate.HitPayoffScore != bestCandidate.HitPayoffScore)
        {
            return candidate.HitPayoffScore > bestCandidate.HitPayoffScore;
        }
        if (candidate.EffectiveTargetCount != bestCandidate.EffectiveTargetCount)
        {
            return candidate.EffectiveTargetCount > bestCandidate.EffectiveTargetCount;
        }
        if (candidate.TargetCount != bestCandidate.TargetCount)
        {
            return candidate.TargetCount > bestCandidate.TargetCount;
        }

        int nonfatalRiskComparison = CompareNonfatalPostActionSurvivalRisk(
            candidate,
            bestCandidate
        );
        if (nonfatalRiskComparison != 0)
        {
            return nonfatalRiskComparison > 0;
        }
        if (candidate.PositionObjectiveScore != bestCandidate.PositionObjectiveScore)
        {
            return candidate.PositionObjectiveScore > bestCandidate.PositionObjectiveScore;
        }
        return candidate.ResourceCostScore < bestCandidate.ResourceCostScore;
    }

    private static bool IsEmergencySurvivalScoreInput(ScoreInputFacts scoreInput)
    {
        if (scoreInput == null)
        {
            return false;
        }
        if (scoreInput.ScoreBucketId != ArcherSurvivalBucketId)
        {
            return false;
        }
        if (scoreInput.HasPostActionThreatProjection)
        {
            if (
                scoreInput.PreActionIsLethalSurvivalRisk
                && !scoreInput.PostActionIsLethalSurvivalRisk
            )
            {
                return true;
            }
            if (
                scoreInput.PreActionThreatExpectedDamage
                    > scoreInput.PostActionRemainingThreatExpectedDamage
                && scoreInput.PostActionSurvivalMargin >= 0
            )
            {
                return true;
            }
        }
        if (scoreInput.TargetCount > 0 || scoreInput.EffectiveTargetCount > 0)
        {
            return false;
        }
        if (scoreInput.EnemyTargetCount > 0 || scoreInput.AllyTargetCount > 0)
        {
            return false;
        }
        if (scoreInput.EstimatedDamage != 0 || scoreInput.EstimatedControlCount != 0)
        {
            return false;
        }
        if (scoreInput.PositionCurrentDistance >= 0 && scoreInput.PositionSafeDistance > 0)
        {
            int currentGap = scoreInput.PositionSafeDistance - scoreInput.PositionCurrentDistance;
            if (currentGap < 2)
            {
                return false;
            }
            if (scoreInput.DistanceToPrimaryCoord >= 0)
            {
                return scoreInput.DistanceToPrimaryCoord >= scoreInput.PositionSafeDistance;
            }
            return scoreInput.PositionObjectiveScore > 0;
        }
        return scoreInput.PositionObjectiveScore > 0;
    }

    private static int ComparePostActionSurvivalRisk(
        ScoreInputFacts candidate,
        ScoreInputFacts bestCandidate
    )
    {
        if (candidate == null || bestCandidate == null)
        {
            return 0;
        }
        if (
            !candidate.HasPostActionThreatProjection || !bestCandidate.HasPostActionThreatProjection
        )
        {
            return 0;
        }
        bool candidateFatal = candidate.PostActionIsLethalSurvivalRisk;
        bool bestFatal = bestCandidate.PostActionIsLethalSurvivalRisk;
        if (candidateFatal != bestFatal)
        {
            return candidateFatal ? -1 : 1;
        }
        return 0;
    }

    private static int CompareNonfatalPostActionSurvivalRisk(
        ScoreInputFacts candidate,
        ScoreInputFacts bestCandidate
    )
    {
        if (candidate == null || bestCandidate == null)
        {
            return 0;
        }
        if (
            !candidate.HasPostActionThreatProjection || !bestCandidate.HasPostActionThreatProjection
        )
        {
            return 0;
        }
        if (
            candidate.PostActionIsLethalSurvivalRisk || bestCandidate.PostActionIsLethalSurvivalRisk
        )
        {
            return 0;
        }

        bool candidateThreatFree = candidate.PostActionRemainingThreatCount <= 0;
        bool bestThreatFree = bestCandidate.PostActionRemainingThreatCount <= 0;
        if (candidateThreatFree != bestThreatFree)
        {
            return candidateThreatFree ? 1 : -1;
        }

        int candidateDamage = candidate.PostActionRemainingThreatExpectedDamage;
        int bestDamage = bestCandidate.PostActionRemainingThreatExpectedDamage;
        if (candidateDamage != bestDamage)
        {
            return candidateDamage < bestDamage ? 1 : -1;
        }

        int candidateCount = candidate.PostActionRemainingThreatCount;
        int bestCount = bestCandidate.PostActionRemainingThreatCount;
        if (candidateCount != bestCount)
        {
            return candidateCount < bestCount ? 1 : -1;
        }

        int candidateMargin = candidate.PostActionSurvivalMargin;
        int bestMargin = bestCandidate.PostActionSurvivalMargin;
        if (candidateMargin != bestMargin)
        {
            return candidateMargin > bestMargin ? 1 : -1;
        }
        return 0;
    }

    private static ScoreInputFacts BuildScoreInputFacts(BattleAiScoreInput scoreInput)
    {
        if (scoreInput == null)
        {
            return null;
        }
        return new ScoreInputFacts
        {
            ScoreBucketId = scoreInput.score_bucket_id,
            EstimatedFriendlyLethalTargetCount = scoreInput.estimated_friendly_lethal_target_count,
            EstimatedFriendlyFireTargetCount = scoreInput.estimated_friendly_fire_target_count,
            FriendlyFirePenaltyScore = scoreInput.friendly_fire_penalty_score,
            EstimatedLethalThreatTargetCount = scoreInput.estimated_lethal_threat_target_count,
            EstimatedLethalTargetCount = scoreInput.estimated_lethal_target_count,
            TotalScore = scoreInput.total_score,
            HitPayoffScore = scoreInput.hit_payoff_score,
            EffectiveTargetCount = scoreInput.effective_target_count,
            ResourceCostScore = scoreInput.resource_cost_score,
            ScoreBucketPriority = scoreInput.score_bucket_priority,
            TargetCount = scoreInput.target_count,
            PositionObjectiveScore = scoreInput.position_objective_score,
            EnemyTargetCount = scoreInput.enemy_target_count,
            AllyTargetCount = scoreInput.ally_target_count,
            EstimatedDamage = scoreInput.estimated_damage,
            EstimatedControlCount = scoreInput.estimated_control_count,
            PositionCurrentDistance = scoreInput.position_current_distance,
            PositionSafeDistance = scoreInput.position_safe_distance,
            DistanceToPrimaryCoord = scoreInput.distance_to_primary_coord,
            HasPostActionThreatProjection = scoreInput.has_post_action_threat_projection,
            PreActionIsLethalSurvivalRisk = scoreInput.pre_action_is_lethal_survival_risk,
            PostActionIsLethalSurvivalRisk = scoreInput.post_action_is_lethal_survival_risk,
            PreActionThreatExpectedDamage = scoreInput.pre_action_threat_expected_damage,
            PostActionRemainingThreatExpectedDamage =
                scoreInput.post_action_remaining_threat_expected_damage,
            PostActionSurvivalMargin = scoreInput.post_action_survival_margin,
            PostActionRemainingThreatCount = scoreInput.post_action_remaining_threat_count,
        };
    }

    private static void AttachPatchAndMark(BattleAiContext context, BattleAiDecision decision)
    {
        AttachStatePatch(decision);
        if (context != null && decision != null)
            context.MarkActionTraceChosen(decision.action_trace_id, decision);
    }

    private static void AttachStatePatch(BattleAiDecision decision)
    {
        if (decision == null)
        {
            return;
        }

        BattleAiDecisionCommitter.AttachStatePatch(decision);
    }

    private static BattleAiScoreInput GetDecisionScoreInput(BattleAiDecision decision)
    {
        if (decision == null)
        {
            return null;
        }
        return decision.score_input ?? decision.skill_score_input;
    }

    private static EnemyAiBrainDef ResolveBrain(
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> brains,
        StringName brainId
    )
    {
        if (brains == null || IsEmpty(brainId))
        {
            return null;
        }
        return brains.TryGetValue(brainId, out EnemyAiBrainDef brain) ? brain : null;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
