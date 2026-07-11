using System.Collections.Generic;
using Godot;
using System;

internal sealed class BattleAiDecisionEngine
{
    private static readonly StringName ArcherSurvivalBucketId = "archer_survival";

    private readonly BattleAiUnitSkillCandidateEvaluator _unitSkill = new();
    private readonly BattleAiGroundSkillActionEvaluator _groundSkill = new();
    private readonly BattleAiMultiUnitSkillEvaluator _multiUnit = new();
    private readonly BattleAiMoveToMultiUnitSkillPositionEvaluator _multiUnitMove = new();
    private readonly BattleAiRandomChainSkillEvaluator _randomChain = new();
    private readonly BattleAiChargeActionEvaluator _charge = new();
    private readonly BattleAiChargePathAoeActionEvaluator _chargePathAoe = new();
    private readonly BattleAiMoveToRangeActionEvaluator _moveToRange = new();
    private readonly BattleAiMoveToAdvantageActionEvaluator _advantage = new();
    private readonly BattleAiGroundRepositionActionEvaluator _groundReposition = new();
    private readonly BattleAiRetreatActionEvaluator _retreat = new();
    private readonly BattleAiWaitActionEvaluator _wait = new();

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
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> enemyAiBrains,
        BattleAiStateResolver stateResolver,
        System.Func<BattleAiContext, StringName, StringName, StringName, string, BattleAiDecision> waitDecisionFactory,
        BattleAiScoreService scoreService
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
        EnemyAiBrainDefinition brain = ResolveBrain(enemyAiBrains, unitBrainId);
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

        StringName brainId = brain.BrainId;

        BattleAiStateResolver.TransitionResult transitionResult =
            stateResolver != null
                ? stateResolver.ResolveTyped(context, brain)
                : BattleAiStateResolver.TransitionResult.Empty();
        StringName nextStateId =
            transitionResult != null && !IsEmpty(transitionResult.StateId)
                ? transitionResult.StateId
                : brain.DefaultStateId;
        brain.TryGetState(nextStateId, out EnemyAiStateDefinition stateDef);
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
            nextStateId
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
        IReadOnlyList<BattleAiRuntimeActionEntry> actions = actionResolution.Actions;
        for (int actionIndex = 0; actionIndex < actions.Count; actionIndex++)
        {
            BattleAiRuntimeActionEntry actionEntry = actions[actionIndex];
            if (actionEntry == null)
            {
                continue;
            }

            BattleAiRuntimeActionPlan.RuntimeActionMetadata actionMetadata =
                actionEntry.Metadata?.Clone() ?? new BattleAiRuntimeActionPlan.RuntimeActionMetadata();
            context.PushActionMetadata(actionMetadata);
            BattleAiDecision decision;
            try
            {
                decision = EvaluateEntry(context, actionEntry);
            }
            finally
            {
                context.PopActionMetadata();
            }

            if (decision == null || decision.command == null)
            {
                decision?.ClearOwnedRuntimeReferences();
                continue;
            }

            PrepareDecision(decision, brainId, nextStateId, transitionResult, null);
            ApplyActionMetadataToDecision(decision, actionMetadata, scoreService);

            BattleAiScoreInput scoreInput = GetDecisionScoreInput(decision);
            if (scoreInput != null)
            {
                if (!BattleAiSafetyGate.IsEligible(scoreInput))
                {
                    decision.ClearOwnedRuntimeReferences();
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
                    if (
                        bestScoredDecision != null
                        && !ReferenceEquals(bestScoredDecision, decision)
                    )
                        bestScoredDecision.ClearOwnedRuntimeReferences();
                    bestScoredDecision = decision;
                    bestScoredActionIndex = actionIndex;
                }
                else
                {
                    decision.ClearOwnedRuntimeReferences();
                }
                continue;
            }

            if (fallbackDecision == null)
                fallbackDecision = decision;
            else
                decision.ClearOwnedRuntimeReferences();
        }

        BattleAiDecision resolvedDecision = bestScoredDecision ?? fallbackDecision;
        if (resolvedDecision != null)
        {
            if (
                bestScoredDecision != null
                && fallbackDecision != null
                && !ReferenceEquals(bestScoredDecision, fallbackDecision)
            )
                fallbackDecision.ClearOwnedRuntimeReferences();
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

    internal static bool IsBetterScoreInputTyped(
        BattleAiScoreInput candidate,
        BattleAiScoreInput bestCandidate
    ) => CompareScoreInput(candidate, bestCandidate);

    internal BattleAiDecision EvaluateEntry(
        BattleAiContext context,
        BattleAiRuntimeActionEntry entry
    )
    {
        if (entry?.Action == null || context == null)
            return null;

        return entry.Action.Kind switch
        {
            EnemyAiActionKind.UseUnitSkill => _unitSkill.Evaluate(
                (UseUnitSkillActionDefinition)entry.Action,
                context
            ),
            EnemyAiActionKind.UseGroundSkill => _groundSkill.Evaluate(
                (UseGroundSkillActionDefinition)entry.Action,
                context
            ),
            EnemyAiActionKind.UseMultiUnitSkill => _multiUnit.Evaluate(
                (UseMultiUnitSkillActionDefinition)entry.Action,
                context
            ),
            EnemyAiActionKind.MoveToMultiUnitSkillPosition => _multiUnitMove.Evaluate(
                (MoveToMultiUnitSkillPositionActionDefinition)entry.Action,
                context
            ),
            EnemyAiActionKind.UseRandomChainSkill => _randomChain.Evaluate(
                (UseRandomChainSkillActionDefinition)entry.Action,
                context
            ),
            EnemyAiActionKind.UseCharge => _charge.Evaluate(
                (UseChargeActionDefinition)entry.Action,
                context
            ),
            EnemyAiActionKind.UseChargePathAoe => _chargePathAoe.Evaluate(
                (UseChargePathAoeActionDefinition)entry.Action,
                context
            ),
            EnemyAiActionKind.MoveToRange => _moveToRange.Evaluate(
                (MoveToRangeActionDefinition)entry.Action,
                context,
                entry.Metadata?.force_candidate_request_evaluation == true
            ),
            EnemyAiActionKind.MoveToAdvantagePosition => _advantage.Evaluate(
                (MoveToAdvantagePositionActionDefinition)entry.Action,
                context
            ),
            EnemyAiActionKind.UseGroundRepositionSkill => _groundReposition.Evaluate(
                (UseGroundRepositionSkillActionDefinition)entry.Action,
                context
            ),
            EnemyAiActionKind.Retreat => _retreat.Evaluate(
                (RetreatActionDefinition)entry.Action,
                context
            ),
            EnemyAiActionKind.Wait => _wait.Evaluate(
                (WaitActionDefinition)entry.Action,
                context
            ),
            _ => throw new InvalidOperationException(
                $"Unsupported action kind {entry.Action.Kind}"
            ),
        };
    }

    private static RuntimeActionResolution ResolveRuntimeActions(
        BattleAiContext context,
        EnemyAiBrainDefinition brain,
        StringName stateId
    )
    {
        if (context == null)
        {
            return RuntimeActionResolution.ForActions(System.Array.Empty<BattleAiRuntimeActionEntry>());
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
            IReadOnlyList<BattleAiRuntimeActionEntry> runtimeActions =
                context.GetRuntimeActionEntriesTyped(stateId);
            if (runtimeActions.Count == 0)
            {
                return RuntimeActionResolution.ForWait(
                    "wait_empty_runtime_state",
                    $"{context.unit_state.display_name} 的 AI runtime state {stateId} 没有可评估 action，改为待机。"
                );
            }
            return RuntimeActionResolution.ForActions(runtimeActions);
        }

        return RuntimeActionResolution.ForWait(
            "wait_missing_runtime_plan",
            $"{context.unit_state.display_name} 缺少 AI runtime plan，改为待机。"
        );
    }

    private sealed class RuntimeActionResolution
    {
        public IReadOnlyList<BattleAiRuntimeActionEntry> Actions =
            System.Array.Empty<BattleAiRuntimeActionEntry>();
        public StringName WaitActionId = "";
        public string WaitReasonText = "";

        public static RuntimeActionResolution ForActions(
            IReadOnlyList<BattleAiRuntimeActionEntry> actions
        )
        {
            return new RuntimeActionResolution
            {
                Actions = actions ?? System.Array.Empty<BattleAiRuntimeActionEntry>(),
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

    private static EnemyAiBrainDefinition ResolveBrain(
        IReadOnlyDictionary<StringName, EnemyAiBrainDefinition> brains,
        StringName brainId
    )
    {
        if (brains == null || IsEmpty(brainId))
        {
            return null;
        }
        return brains.TryGetValue(brainId, out EnemyAiBrainDefinition brain) ? brain : null;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
