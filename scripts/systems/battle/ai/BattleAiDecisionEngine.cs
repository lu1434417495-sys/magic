using Godot;
using static GdInterop;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleAiDecisionEngine : RefCounted
{
    private static readonly StringName ArcherSurvivalBucketId = "archer_survival";
    private static readonly GDScript BattleAiPayloadGuardScript = GD.Load<GDScript>("res://scripts/systems/battle/ai/battle_ai_payload_guard.gd");

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

    public GodotObject choose_command_impl(
        GodotObject context,
        GDictionary enemyAiBrains,
        GodotObject stateResolver,
        Callable waitDecisionFactory,
        GodotObject scoreService,
        Callable traceEnter = default,
        Callable traceExit = default,
        bool traceEnabled = false)
    {
        if (context == null)
        {
            return null;
        }

        GodotObject unitState = GetObject(context, "unit_state");
        if (unitState == null)
        {
            return null;
        }

        StringName unitBrainId = GetStringName(unitState, "ai_brain_id");
        GodotObject brain = ResolveBrain(enemyAiBrains, unitBrainId);
        if (brain == null)
        {
            GodotObject missingBrainDecision = BuildWaitDecision(
                waitDecisionFactory,
                context,
                new StringName(""),
                new StringName(""),
                new StringName("wait_missing_brain"),
                $"{GetString(unitState, "display_name")} 缺少正式 AI brain，改为待机。");
            AttachPatchAndMark(context, missingBrainDecision);
            return missingBrainDecision;
        }

        StringName brainId = GetStringName(brain, "brain_id");

        GDictionary transitionResult = stateResolver != null
            ? stateResolver.Call("resolve", context, brain).AsGodotDictionary()
            : new GDictionary();
        StringName nextStateId = GetStringName(transitionResult, "state_id", GetStringName(brain, "default_state_id"));
        GodotObject stateDef = brain.Call("get_state", nextStateId).AsGodotObject();
        if (stateDef == null)
        {
            GodotObject missingStateDecision = BuildWaitDecision(
                waitDecisionFactory,
                context,
                brainId,
                nextStateId,
                new StringName("wait_missing_state"),
                $"{GetString(unitState, "display_name")} 找不到 AI 状态 {nextStateId}，改为待机。");
            PrepareDecision(missingStateDecision, brainId, nextStateId, transitionResult, null);
            AttachPatchAndMark(context, missingStateDecision);
            return missingStateDecision;
        }

        GDictionary actionResolution = ResolveRuntimeActions(context, brain, nextStateId, stateDef);
        StringName waitActionId = GetStringName(actionResolution, "wait_action_id");
        if (!IsEmpty(waitActionId))
        {
            GodotObject runtimeWaitDecision = BuildWaitDecision(
                waitDecisionFactory,
                context,
                brainId,
                nextStateId,
                waitActionId,
                GetString(actionResolution, "wait_reason_text"));
            PrepareDecision(runtimeWaitDecision, brainId, nextStateId, transitionResult, null);
            AttachPatchAndMark(context, runtimeWaitDecision);
            return runtimeWaitDecision;
        }

        GodotObject bestScoredDecision = null;
        int bestScoredActionIndex = int.MaxValue;
        GodotObject fallbackDecision = null;
        GArray actions = GetArray(actionResolution, "actions");
        for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
        {
            GodotObject action = actions[actionIndex].AsGodotObject();
            if (action == null)
            {
                continue;
            }

            GDictionary actionMetadata = context.HasMethod("get_runtime_action_metadata")
                ? context.Call("get_runtime_action_metadata", action).AsGodotDictionary()
                : new GDictionary();
            if (context.HasMethod("push_action_metadata"))
            {
                context.Call("push_action_metadata", actionMetadata);
            }
            GodotObject decision = EvaluateAction(context, action);
            if (context.HasMethod("pop_action_metadata"))
            {
                context.Call("pop_action_metadata");
            }

            if (decision == null || GetObject(decision, "command") == null)
            {
                continue;
            }

            PrepareDecision(decision, brainId, nextStateId, transitionResult, null);
            ApplyActionMetadataToDecision(decision, actionMetadata, scoreService);

            GodotObject scoreInput = GetDecisionScoreInput(decision);
            if (scoreInput != null)
            {
                if (ShouldReplaceScoredDecision(decision, actionIndex, bestScoredDecision, bestScoredActionIndex))
                {
                    bestScoredDecision = decision;
                    bestScoredActionIndex = actionIndex;
                }
                continue;
            }

            fallbackDecision ??= decision;
        }

        GodotObject resolvedDecision = bestScoredDecision ?? fallbackDecision;
        if (resolvedDecision != null)
        {
            AttachPatchAndMark(context, resolvedDecision);
            return resolvedDecision;
        }

        GodotObject waitDecision = BuildWaitDecision(
            waitDecisionFactory,
            context,
            brainId,
            nextStateId,
            new StringName("wait_fallback"),
            $"{GetString(unitState, "display_name")} 在状态 {nextStateId} 下没有找到合法指令，改为待机。");
        PrepareDecision(waitDecision, brainId, nextStateId, transitionResult, null);
        AttachPatchAndMark(context, waitDecision);
        return waitDecision;
    }

    public bool is_better_score_input(GodotObject candidate, GodotObject bestCandidate)
    {
        return IsBetterScoreInput(candidate, bestCandidate);
    }

    private static GodotObject EvaluateAction(GodotObject context, GodotObject action)
    {
        if (action == null)
        {
            return null;
        }

        if (action.HasMethod("uses_candidate_request") && action.Call("uses_candidate_request").AsBool())
        {
            return EvaluateCandidateAction(context, action);
        }

        return DecideWithActionFallback(context, action);
    }

    private static GodotObject EvaluateCandidateAction(GodotObject context, GodotObject action)
    {
        if (!action.HasMethod("build_candidate_request"))
        {
            return FailCandidateAction(action, "candidate_request action is missing build_candidate_request().");
        }
        if (context == null || !context.HasMethod("get_ai_query_service"))
        {
            return FailCandidateAction(action, "candidate_request action requires BattleAiContext.get_ai_query_service().");
        }

        GodotObject query = context.Call("get_ai_query_service").AsGodotObject();
        if (query == null)
        {
            return FailCandidateAction(action, "candidate_request action requires a non-null AI query service.");
        }

        GodotObject request = action.Call("build_candidate_request", query).AsGodotObject();
        if (request == null)
        {
            return FailCandidateAction(action, "candidate_request action returned null request.");
        }
        if (!context.HasMethod("evaluate_candidate_request"))
        {
            return FailCandidateAction(action, "candidate_request action requires BattleAiContext.evaluate_candidate_request().");
        }

        return context.Call("evaluate_candidate_request", request).AsGodotObject();
    }

    private static GodotObject FailCandidateAction(GodotObject action, string message)
    {
        StringName actionId = GetStringName(action, "action_id", "anonymous_action");
        string fullMessage = $"AI action {actionId} failed: {message}";
        if (BattleAiPayloadGuardScript != null)
        {
            BattleAiPayloadGuardScript.Call(
                "fail_loud",
                fullMessage,
                new GDictionary
                {
                    ["source"] = "BattleAiDecisionEngine",
                    ["action_id"] = actionId,
                });
        }
        else
        {
            GD.PushError(fullMessage);
        }
        return null;
    }

    private static GodotObject DecideWithActionFallback(GodotObject context, GodotObject action)
    {
        if (action.HasMethod("decide"))
        {
            return action.Call("decide", context).AsGodotObject();
        }
        return null;
    }

    private static GDictionary ResolveRuntimeActions(GodotObject context, GodotObject brain, StringName stateId, GodotObject stateDef)
    {
        if (context == null)
        {
            return new GDictionary { ["actions"] = new GArray() };
        }

        GodotObject runtimeActionPlan = GetObject(context, "runtime_action_plan");
        if (runtimeActionPlan != null)
        {
            if (context.HasMethod("is_runtime_action_plan_stale") && context.Call("is_runtime_action_plan_stale", brain).AsBool())
            {
                return new GDictionary
                {
                    ["wait_action_id"] = new StringName("wait_stale_runtime_plan"),
                    ["wait_reason_text"] = $"{GetString(GetObject(context, "unit_state"), "display_name")} 的 AI runtime plan 已过期，改为待机。",
                };
            }
            if (!context.HasMethod("has_runtime_action_state") || !context.Call("has_runtime_action_state", stateId).AsBool())
            {
                return new GDictionary
                {
                    ["wait_action_id"] = new StringName("wait_missing_runtime_plan"),
                    ["wait_reason_text"] = $"{GetString(GetObject(context, "unit_state"), "display_name")} 缺少状态 {stateId} 的 AI runtime plan，改为待机。",
                };
            }
            GArray runtimeActions = context.HasMethod("get_runtime_actions")
                ? context.Call("get_runtime_actions", stateId).AsGodotArray()
                : new GArray();
            if (runtimeActions.Count == 0)
            {
                return new GDictionary
                {
                    ["wait_action_id"] = new StringName("wait_empty_runtime_state"),
                    ["wait_reason_text"] = $"{GetString(GetObject(context, "unit_state"), "display_name")} 的 AI runtime state {stateId} 没有可评估 action，改为待机。",
                };
            }
            return new GDictionary { ["actions"] = runtimeActions };
        }

        if (GetBool(context, "allow_authored_action_fallback_for_tests"))
        {
            GArray authoredActions = stateDef != null ? stateDef.Call("get_actions").AsGodotArray() : new GArray();
            return new GDictionary { ["actions"] = authoredActions };
        }

        return new GDictionary
        {
            ["wait_action_id"] = new StringName("wait_missing_runtime_plan"),
            ["wait_reason_text"] = $"{GetString(GetObject(context, "unit_state"), "display_name")} 缺少 AI runtime plan，改为待机。",
        };
    }

    private static GodotObject BuildWaitDecision(
        Callable waitDecisionFactory,
        GodotObject context,
        StringName brainId,
        StringName stateId,
        StringName actionId,
        string reasonText)
    {
        return waitDecisionFactory.Call(context, brainId, stateId, actionId, reasonText).AsGodotObject();
    }

    private static void PrepareDecision(
        GodotObject decision,
        StringName brainId,
        StringName stateId,
        GDictionary transitionResult,
        GodotObject scoreInputOverride)
    {
        if (decision == null)
        {
            return;
        }

        decision.Set("brain_id", brainId);
        decision.Set("state_id", stateId);
        decision.Set("transition", transitionResult?.Duplicate(true) ?? new GDictionary());
        if (IsEmpty(GetStringName(decision, "action_id")))
        {
            decision.Set("action_id", new StringName("anonymous_action"));
        }

        GodotObject scoreInput = scoreInputOverride ?? GetDecisionScoreInput(decision);
        if (IsEmpty(GetStringName(decision, "score_bucket_id")) && scoreInput != null)
        {
            decision.Set("score_bucket_id", GetStringName(scoreInput, "score_bucket_id"));
        }
    }

    private static void ApplyActionMetadataToDecision(GodotObject decision, GDictionary metadata, GodotObject scoreService)
    {
        if (decision == null || metadata == null || metadata.Count == 0)
        {
            return;
        }

        StringName metadataBucketId = GetStringName(metadata, "score_bucket_id");
        if (!IsEmpty(metadataBucketId))
        {
            decision.Set("score_bucket_id", metadataBucketId);
        }

        GodotObject scoreInput = GetDecisionScoreInput(decision);
        if (scoreInput == null)
        {
            return;
        }

        if (!IsEmpty(metadataBucketId))
        {
            scoreInput.Set("score_bucket_id", metadataBucketId);
            int priority = scoreService != null && scoreService.HasMethod("get_bucket_priority")
                ? scoreService.Call("get_bucket_priority", metadataBucketId).AsInt32()
                : 0;
            scoreInput.Set("score_bucket_priority", priority);
        }

        GDictionary currentRuntimeMetadata = GetDictionary(scoreInput, "runtime_action_metadata");
        GDictionary runtimeActionMetadata = GetDictionary(metadata, "runtime_action_metadata");
        if (currentRuntimeMetadata.Count == 0 && runtimeActionMetadata.Count > 0)
        {
            scoreInput.Set("runtime_action_metadata", runtimeActionMetadata.Duplicate(true));
        }
    }

    private static bool ShouldReplaceScoredDecision(GodotObject candidate, int candidateActionIndex, GodotObject bestCandidate, int bestActionIndex)
    {
        GodotObject candidateScore = GetDecisionScoreInput(candidate);
        if (candidateScore == null)
        {
            return false;
        }
        GodotObject bestScore = GetDecisionScoreInput(bestCandidate);
        if (bestScore == null)
        {
            return true;
        }
        if (IsBetterScoreInput(candidateScore, bestScore))
        {
            return true;
        }
        if (IsBetterScoreInput(bestScore, candidateScore))
        {
            return false;
        }
        return candidateActionIndex < bestActionIndex;
    }

    private static bool IsBetterScoreInput(GodotObject candidate, GodotObject bestCandidate)
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
        return IsBetterScoreInput(candidateFacts, bestFacts);
    }

    private static bool IsBetterScoreInput(ScoreInputFacts candidate, ScoreInputFacts bestCandidate)
    {
        if (candidate == null)
        {
            return false;
        }
        if (bestCandidate == null)
        {
            return true;
        }
        if (candidate.EstimatedFriendlyLethalTargetCount != bestCandidate.EstimatedFriendlyLethalTargetCount)
        {
            return candidate.EstimatedFriendlyLethalTargetCount < bestCandidate.EstimatedFriendlyLethalTargetCount;
        }
        if (candidate.EstimatedFriendlyFireTargetCount != bestCandidate.EstimatedFriendlyFireTargetCount)
        {
            return candidate.EstimatedFriendlyFireTargetCount < bestCandidate.EstimatedFriendlyFireTargetCount;
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
        if (candidate.EstimatedLethalThreatTargetCount != bestCandidate.EstimatedLethalThreatTargetCount)
        {
            return candidate.EstimatedLethalThreatTargetCount > bestCandidate.EstimatedLethalThreatTargetCount;
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

        if (candidate.EstimatedLethalTargetCount > 0 && bestCandidate.EstimatedLethalTargetCount > 0)
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
            int lethalNonfatalRiskComparison = CompareNonfatalPostActionSurvivalRisk(candidate, bestCandidate);
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

        int nonfatalRiskComparison = CompareNonfatalPostActionSurvivalRisk(candidate, bestCandidate);
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
            if (scoreInput.PreActionIsLethalSurvivalRisk && !scoreInput.PostActionIsLethalSurvivalRisk)
            {
                return true;
            }
            if (scoreInput.PreActionThreatExpectedDamage > scoreInput.PostActionRemainingThreatExpectedDamage
                && scoreInput.PostActionSurvivalMargin >= 0)
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

    private static int ComparePostActionSurvivalRisk(ScoreInputFacts candidate, ScoreInputFacts bestCandidate)
    {
        if (candidate == null || bestCandidate == null)
        {
            return 0;
        }
        if (!candidate.HasPostActionThreatProjection || !bestCandidate.HasPostActionThreatProjection)
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

    private static int CompareNonfatalPostActionSurvivalRisk(ScoreInputFacts candidate, ScoreInputFacts bestCandidate)
    {
        if (candidate == null || bestCandidate == null)
        {
            return 0;
        }
        if (!candidate.HasPostActionThreatProjection || !bestCandidate.HasPostActionThreatProjection)
        {
            return 0;
        }
        if (candidate.PostActionIsLethalSurvivalRisk || bestCandidate.PostActionIsLethalSurvivalRisk)
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

    private static ScoreInputFacts BuildScoreInputFacts(GodotObject scoreInput)
    {
        if (scoreInput == null)
        {
            return null;
        }
        return new ScoreInputFacts
        {
            ScoreBucketId = GetStringName(scoreInput, "score_bucket_id"),
            EstimatedFriendlyLethalTargetCount = GetInt(scoreInput, "estimated_friendly_lethal_target_count"),
            EstimatedFriendlyFireTargetCount = GetInt(scoreInput, "estimated_friendly_fire_target_count"),
            FriendlyFirePenaltyScore = GetInt(scoreInput, "friendly_fire_penalty_score"),
            EstimatedLethalThreatTargetCount = GetInt(scoreInput, "estimated_lethal_threat_target_count"),
            EstimatedLethalTargetCount = GetInt(scoreInput, "estimated_lethal_target_count"),
            TotalScore = GetInt(scoreInput, "total_score"),
            HitPayoffScore = GetInt(scoreInput, "hit_payoff_score"),
            EffectiveTargetCount = GetInt(scoreInput, "effective_target_count"),
            ResourceCostScore = GetInt(scoreInput, "resource_cost_score"),
            ScoreBucketPriority = GetInt(scoreInput, "score_bucket_priority"),
            TargetCount = GetInt(scoreInput, "target_count"),
            PositionObjectiveScore = GetInt(scoreInput, "position_objective_score"),
            EnemyTargetCount = GetInt(scoreInput, "enemy_target_count"),
            AllyTargetCount = GetInt(scoreInput, "ally_target_count"),
            EstimatedDamage = GetInt(scoreInput, "estimated_damage"),
            EstimatedControlCount = GetInt(scoreInput, "estimated_control_count"),
            PositionCurrentDistance = GetInt(scoreInput, "position_current_distance"),
            PositionSafeDistance = GetInt(scoreInput, "position_safe_distance"),
            DistanceToPrimaryCoord = GetInt(scoreInput, "distance_to_primary_coord"),
            HasPostActionThreatProjection = GetBool(scoreInput, "has_post_action_threat_projection"),
            PreActionIsLethalSurvivalRisk = GetBool(scoreInput, "pre_action_is_lethal_survival_risk"),
            PostActionIsLethalSurvivalRisk = GetBool(scoreInput, "post_action_is_lethal_survival_risk"),
            PreActionThreatExpectedDamage = GetInt(scoreInput, "pre_action_threat_expected_damage"),
            PostActionRemainingThreatExpectedDamage = GetInt(scoreInput, "post_action_remaining_threat_expected_damage"),
            PostActionSurvivalMargin = GetInt(scoreInput, "post_action_survival_margin"),
            PostActionRemainingThreatCount = GetInt(scoreInput, "post_action_remaining_threat_count"),
        };
    }

    private static void AttachPatchAndMark(GodotObject context, GodotObject decision)
    {
        AttachStatePatch(decision);
        if (context != null && decision != null && context.HasMethod("mark_action_trace_chosen"))
        {
            context.Call("mark_action_trace_chosen", GetStringName(decision, "action_trace_id"), decision);
        }
    }

    private static void AttachStatePatch(GodotObject decision)
    {
        if (decision == null)
        {
            return;
        }

        GDictionary blackboard = new GDictionary();
        blackboard["last_brain_id"] = GetStringName(decision, "brain_id").ToString();
        blackboard["last_state_id"] = GetStringName(decision, "state_id").ToString();
        blackboard["last_action_id"] = GetStringName(decision, "action_id").ToString();
        blackboard["last_reason_text"] = GetString(decision, "reason_text");

        GDictionary transition = GetDictionary(decision, "transition");
        if (transition.Count > 0)
        {
            blackboard["last_transition_previous_state_id"] = GetStringName(transition, "previous_state_id").ToString();
            blackboard["last_transition_state_id"] = GetStringName(transition, "state_id").ToString();
            blackboard["last_transition_rule_id"] = GetStringName(transition, "rule_id").ToString();
            blackboard["last_transition_reason"] = GetString(transition, "reason");
        }

        GDictionary patch = new GDictionary
        {
            ["blackboard_set"] = blackboard,
            ["blackboard_increment"] = new GDictionary
            {
                ["turn_decision_count"] = 1,
            },
        };
        StringName brainId = GetStringName(decision, "brain_id");
        if (!IsEmpty(brainId))
        {
            patch["ai_brain_id"] = brainId;
        }
        StringName stateId = GetStringName(decision, "state_id");
        if (!IsEmpty(stateId))
        {
            patch["ai_state_id"] = stateId;
        }
        decision.Set("state_patch", patch);
    }

    private static GodotObject GetDecisionScoreInput(GodotObject decision)
    {
        if (decision == null)
        {
            return null;
        }
        return GetObject(decision, "score_input") ?? GetObject(decision, "skill_score_input");
    }

    private static GodotObject ResolveBrain(GDictionary brains, StringName brainId)
    {
        if (brains == null || IsEmpty(brainId))
        {
            return null;
        }
        if (brains.ContainsKey(brainId))
        {
            return brains[brainId].AsGodotObject();
        }
        string brainText = brainId.ToString();
        return brains.ContainsKey(brainText) ? brains[brainText].AsGodotObject() : null;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

}
