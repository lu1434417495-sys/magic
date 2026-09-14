using System.Collections.Generic;
using Godot;
using System;

internal sealed class BattleAiDecisionEngine
{
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
    private readonly BattleAiObjectiveActionEvaluator _objective = new();

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

        BattleAiDecision objectiveDecision = _objective.Evaluate(context);
        if (objectiveDecision != null)
        {
            AttachPatchAndMark(context, objectiveDecision);
            return objectiveDecision;
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
    ) => BattleAiCandidateOrdering.IsBetter(candidate, bestCandidate);

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
