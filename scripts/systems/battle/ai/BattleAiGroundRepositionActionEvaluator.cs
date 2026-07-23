using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiGroundRepositionActionEvaluator
{
    private readonly BattleAiTypedActionHelper _helper = new();
    private readonly BattleAiGroundSkillActionEvaluator _ground = new();

    internal BattleAiDecision Evaluate(
        UseGroundRepositionSkillActionDefinition action,
        BattleAiContext context
    )
    {
        if (action == null)
            return null;
        AiTraceRecorder.Enter("decide:ground_reposition_skill");
        try
        {
            return EvaluateImpl(action, context);
        }
        finally
        {
            AiTraceRecorder.Exit("decide:ground_reposition_skill");
        }
    }

    private BattleAiDecision EvaluateImpl(
        UseGroundRepositionSkillActionDefinition action,
        BattleAiContext context
    )
    {
        AiActionTrace trace = context?.trace_enabled == true
            ? EnemyAiActionHelper.BeginActionTrace(
                action.ActionId,
                action.ScoreBucketId,
                context,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["action_kind"] = "ground_reposition_skill",
                    ["target_selector"] = action.TargetSelector.ToString(),
                    ["minimum_safe_distance"] = action.MinimumSafeDistance,
                    ["safe_distance_margin"] = action.SafeDistanceMargin,
                    ["desired_max_distance_bonus"] = action.DesiredMaxDistanceBonus,
                    ["action_base_score"] = action.ActionBaseScore,
                }
            )
            : null;
        if (
            context?.state == null
            || context.unit_state == null
            || context.grid_service == null
        )
        {
            return Fail(context, trace, "missing_context");
        }

        List<BattleUnitState> targets = _helper.SortTargetUnits(
            context,
            "enemy",
            action.TargetSelector
        );
        if (targets.Count == 0)
            return Fail(context, trace, "no_valid_targets");

        BattleUnitState actor = context.unit_state;
        int resolvedSafeDistance = Mathf.Max(
            action.MinimumSafeDistance + action.SafeDistanceMargin,
            1
        );
        foreach (BattleUnitState focusTarget in targets)
        {
            if (focusTarget == null)
                continue;
            int currentDistance = BattleAiActionEvaluatorUtilities.DistanceFromAnchorToUnit(
                context,
                actor,
                actor.coord,
                focusTarget
            );
            if (trace != null)
            {
                trace.Metadata["focus_target_unit_id"] = focusTarget.unit_id.ToString();
                trace.Metadata["current_distance"] = currentDistance;
                trace.Metadata["resolved_safe_distance"] = resolvedSafeDistance;
            }
            if (currentDistance >= resolvedSafeDistance)
            {
                EnemyAiActionHelper.TraceAddBlockReason(trace, "already_safe");
                continue;
            }

            BattleAiDecision bestDecision = null;
            BattleAiScoreInput bestScoreInput = null;
            foreach (
                BattleAvailableSkillEntry skillEntry in _helper.ResolveAvailableSkillEntries(
                    context,
                    action.SkillIds
                )
            )
            {
                StringName skillId = skillEntry?.EntryRef.SkillId ?? "";
                if (skillId == "")
                    continue;
                EnemyAiActionHelper.TraceCountIncrement(trace, "skill_considered_count");
                SkillDefinition skill = _helper.GetSkillDefinition(context, skillEntry);
                if (
                    skill?.CombatProfile == null
                    || skill.CombatProfile.TargetModeKind != BattleTargetMode.Ground
                )
                {
                    EnemyAiActionHelper.TraceAddBlockReason(
                        trace,
                        skill == null ? "missing_skill_definition" : "non_ground_skill"
                    );
                    continue;
                }
                BattleSkillCastBlockReasonKind blockReason = _helper.GetSkillCastBlockReason(
                    context,
                    skill
                );
                if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
                {
                    EnemyAiActionHelper.TraceAddBlockReason(
                        trace,
                        BattleSkillCastBlockReasonKinds.ToTraceKey(blockReason)
                    );
                    continue;
                }
                int effectiveRange = BattleRangeService.GetEffectiveSkillRange(
                    actor,
                    skill,
                    context.skill_catalog
                );
                foreach (
                    CombatCastVariantDefinition castVariant in _ground.GetGroundOptionDefinitions(
                        context,
                        skill,
                        skillEntry.SkillLevel
                    )
                )
                {
                    if (
                        castVariant == null
                        || BattleAiGroundSkillActionEvaluator.IsChargeOption(castVariant)
                    )
                    {
                        continue;
                    }
                    if (!HasRepositionEffect(castVariant.EffectDefinitions))
                    {
                        EnemyAiActionHelper.TraceAddBlockReason(trace, "missing_reposition_effect");
                        continue;
                    }
                    foreach (
                        List<Vector2I> targetCoords in _ground.EnumerateGroundTargetCoordSetsTyped(
                            context,
                            castVariant
                        )
                    )
                    {
                        if (targetCoords.Count != 1)
                            continue;
                        Vector2I landingCoord = targetCoords[0];
                        int castDistance = context.grid_service.GetDistanceFromUnitToCoord(
                            actor,
                            landingCoord
                        );
                        if (effectiveRange >= 0 && castDistance > effectiveRange)
                            continue;
                        int landingDistance = BattleAiActionEvaluatorUtilities.DistanceFromAnchorToUnit(
                            context,
                            actor,
                            landingCoord,
                            focusTarget
                        );
                        if (landingDistance <= currentDistance)
                        {
                            EnemyAiActionHelper.TraceAddBlockReason(
                                trace,
                                "does_not_improve_safety"
                            );
                            continue;
                        }
                        EnemyAiActionHelper.TraceCountIncrement(trace, "evaluation_count");
                        BattleCommand command =
                            BattleAiGroundSkillActionEvaluator.BuildTypedGroundSkillCommand(
                                context,
                                skillEntry,
                                castVariant.VariantId,
                                new[] { landingCoord }
                            );
                        BattlePreview preview =
                            BattleAiGroundSkillActionEvaluator.BuildFastGroundSkillPreview(
                                context,
                                command,
                                new[] { landingCoord },
                                Array.Empty<StringName>()
                            );
                        if (preview?.allowed != true)
                        {
                            EnemyAiActionHelper.TraceCountIncrement(trace, "preview_reject_count");
                            continue;
                        }
                        string label = EnemyAiActionHelper.FormatSkillVariantLabel(
                            skill,
                            castVariant
                        );
                        BattleAiScoreInput scoreInput =
                            BattleAiActionEvaluatorUtilities.BuildSkillScoreInput(
                                action,
                                context,
                                skill,
                                command,
                                preview,
                                castVariant.EffectDefinitions,
                                new Dictionary<string, object>(StringComparer.Ordinal)
                                {
                                    ["action_label"] = label,
                                    ["action_base_score"] = action.ActionBaseScore,
                                    ["position_target_unit_id"] = focusTarget.unit_id,
                                    ["position_anchor_coord"] = landingCoord,
                                    ["position_current_distance"] = currentDistance,
                                    ["position_safe_distance"] = resolvedSafeDistance,
                                    ["desired_min_distance"] = resolvedSafeDistance,
                                    ["desired_max_distance"] =
                                        resolvedSafeDistance
                                        + Mathf.Max(action.DesiredMaxDistanceBonus, 0),
                                    ["position_objective_kind"] = "distance_band_progress",
                                }
                            );
                        if (
                            BattleAiActionEvaluatorUtilities.IsUnthreatenedReposition(
                                scoreInput,
                                action.MinSurvivalMarginGainToEscape
                            )
                        )
                        {
                            EnemyAiActionHelper.TraceAddBlockReason(trace, "no_survival_gain");
                            continue;
                        }
                        if (trace != null)
                        {
                            EnemyAiActionHelper.TraceOfferCandidate(
                                trace,
                                EnemyAiActionHelper.BuildCandidateSummary(
                                    $"{label}_to_{landingCoord.X}_{landingCoord.Y}",
                                    command,
                                    scoreInput,
                                    new Dictionary<string, object>(StringComparer.Ordinal)
                                    {
                                        ["skill_id"] = skillId.ToString(),
                                        ["landing_distance"] = landingDistance,
                                        ["resolved_safe_distance"] = resolvedSafeDistance,
                                    }
                                )
                            );
                        }
                        if (!BattleAiDecisionEngine.IsBetterScoreInputTyped(scoreInput, bestScoreInput))
                            continue;
                        bestScoreInput = scoreInput;
                        bestDecision = EnemyAiActionHelper.CreateScoredDecision(
                            action.ActionId,
                            action.ScoreBucketId,
                            command,
                            scoreInput,
                            $"{actor.display_name} 准备用 {skill.DisplayName} 拉开到 {landingDistance} 格（评分 {BattleAiActionEvaluatorUtilities.ScoreTotal(scoreInput)}）。"
                        );
                    }
                }
            }
            if (bestDecision != null)
            {
                EnemyAiActionHelper.FinalizeActionTrace(context, trace, bestDecision);
                return bestDecision;
            }
        }

        EnemyAiActionHelper.FinalizeActionTrace(context, trace);
        return null;
    }

    private static bool HasRepositionEffect(
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        foreach (
            CombatEffectDefinition effect in effectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effect?.EffectKind == BattleEffectKind.ForcedMove
                && (
                    effect.ForcedMoveModeKind == BattleForcedMoveMode.Blink
                    || effect.ForcedMoveModeKind == BattleForcedMoveMode.Jump
                )
            )
            {
                return true;
            }
        }
        return false;
    }

    private static BattleAiDecision Fail(
        BattleAiContext context,
        AiActionTrace trace,
        string reason
    )
    {
        EnemyAiActionHelper.TraceAddBlockReason(trace, reason);
        EnemyAiActionHelper.FinalizeActionTrace(context, trace);
        return null;
    }
}
