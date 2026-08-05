using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

internal sealed class BattleAiUnitSkillCandidateEvaluator
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName TargetModeUnit = "unit";
    private static readonly Vector2I InvalidCoord = new(-1, -1);
    private const string FastPreviewRejectMissingContext = "fast_preview_reject_missing_context";
    private const string FastPreviewRejectInvalidTarget = "fast_preview_reject_invalid_target";
    private const string FastPreviewRejectTargetFilter = "fast_preview_reject_target_filter";
    private const string FastPreviewRejectOutOfRange = "fast_preview_reject_out_of_range";

    private readonly BattleAiTypedActionHelper _helper = new();

    internal BattleAiDecision Evaluate(
        UseUnitSkillActionDefinition action,
        BattleAiContext context
    )
    {
        if (action == null || context == null)
            return null;

        EnemyAiDistanceReference distanceReference = action.DistanceReferenceKind;
        int desiredMinDistance = action.DesiredMinDistance;
        int desiredMaxDistance = action.DesiredMaxDistance;
        if (!HasExplicitDistanceContract(distanceReference, desiredMinDistance, desiredMaxDistance))
            return null;

        BattleUnitState actor = context.unit_state;
        if (actor == null || context.state == null)
            return null;

        string actorDisplayName = actor.display_name;
        bool traceEnabled = context.trace_enabled;
        AiActionTrace actionTrace = traceEnabled
            ? BeginActionTrace(
                action,
                context,
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["action_kind"] = "unit_skill",
                    ["target_selector"] = action.TargetSelector.ToString(),
                    ["minimum_effective_target_count"] = action.MinimumEffectiveTargetCount,
                    ["maximum_friendly_fire_target_count"] =
                        action.MaximumFriendlyFireTargetCount,
                    ["allow_friendly_lethal"] = action.AllowFriendlyLethal,
                    ["distance_reference"] = action.DistanceReference.ToString(),
                    ["desired_min_distance"] = desiredMinDistance,
                    ["desired_max_distance"] = desiredMaxDistance,
                }
            )
            : null;

        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        BattleAiDecision fallbackDecision = null;
        List<BattleAvailableSkillEntry> skillEntries = _helper.ResolveAvailableSkillEntries(
            context,
            action.SkillIds
        );

        foreach (BattleAvailableSkillEntry skillEntry in skillEntries)
        {
            StringName skillId = skillEntry.EntryRef.SkillId;
            TraceCountIncrement(actionTrace, "skill_considered_count", 1);
            SkillDefinition skillDefinition = _helper.GetSkillDefinition(context, skillEntry);
            if (skillDefinition?.CombatProfile == null)
            {
                TraceAddBlockReason(actionTrace, "missing_skill_definition");
                continue;
            }
            if (skillDefinition.CombatProfile.TargetModeKind != BattleTargetMode.Unit)
            {
                TraceAddBlockReason(actionTrace, "non_unit_skill");
                continue;
            }

            BattleSkillCastBlockReasonKind blockReason = _helper.GetSkillCastBlockReason(
                context,
                skillDefinition
            );
            if (BattleSkillCastBlockReasonKinds.IsBlocked(blockReason))
            {
                TraceAddBlockReason(
                    actionTrace,
                    BattleSkillCastBlockReasonKinds.ToTraceKey(blockReason)
                );
                continue;
            }

            List<BattleUnitState> targets = _helper.SortTargetUnits(
                context,
                skillDefinition.CombatProfile.TargetTeamFilter,
                action.TargetSelector
            );
            if (targets.Count == 0)
            {
                TraceAddBlockReason(actionTrace, "no_valid_targets");
                continue;
            }

            List<CombatCastVariantDefinition> castVariants = _helper.GetUnitCastVariantDefinitions(
                context,
                skillDefinition,
                skillEntry.SkillLevel
            );
            if (castVariants.Count == 0)
            {
                TraceAddBlockReason(actionTrace, "no_unlocked_unit_options");
                continue;
            }

            foreach (CombatCastVariantDefinition castVariant in castVariants)
            {
                StringName optionId = castVariant?.VariantId ?? EmptyStringName;
                string optionLabel = FormatSkillVariantLabel(skillDefinition, castVariant);

                foreach (BattleUnitState target in targets)
                {
                    if (target == null)
                        continue;
                    int windupTierCount = skillDefinition.CombatProfile.Windup != null
                        ? BattleWindupRules.GetMaxTier(actor, skillDefinition)
                        : 1;
                    for (int windupOption = 0; windupOption < windupTierCount; windupOption++)
                    {
                        IReadOnlyList<Vector2I> sourceRetreatDirections =
                            BuildSourceRetreatDirectionOptions(
                                skillDefinition,
                                castVariant,
                                skillEntry.SkillLevel,
                                actor,
                                target
                            );
                        foreach (Vector2I sourceRetreatDirection in sourceRetreatDirections)
                        {
                            TraceCountIncrement(actionTrace, "evaluation_count", 1);
                            BattleCommand command = _helper.BuildUnitSkillCommand(
                                context,
                                skillEntry,
                                target,
                                optionId
                            );
                            command.source_retreat_direction = sourceRetreatDirection;
                            if (skillDefinition.CombatProfile.Windup != null)
                                command.windup_tier = windupOption + 1;
                            string candidateLabel = skillDefinition.CombatProfile.Windup != null
                                ? $"{optionLabel}（{command.windup_tier} 挡）"
                                : optionLabel;
                            if (sourceRetreatDirection != Vector2I.Zero)
                                candidateLabel =
                                    $"{candidateLabel}（后撤{FormatDirection(sourceRetreatDirection)}）";
                            BattlePreview fastPreview = BuildFastUnitSkillPreview(
                                context,
                                skillDefinition,
                                command,
                                target,
                                out string previewRejectCounterKey
                            );
                            BattlePreview preview = _helper.ResolveBarrierAwareUnitSkillPreview(
                                context,
                                command,
                                fastPreview
                            );
                            if (preview == null || !preview.allowed)
                            {
                                TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                                if (!string.IsNullOrEmpty(previewRejectCounterKey))
                                    TraceCountIncrement(actionTrace, previewRejectCounterKey, 1);
                                else
                                    TraceAddBlockReason(actionTrace, "canonical_preview_reject");
                                continue;
                            }
                            if (!preview.ContainsTargetUnitId(target.unit_id))
                            {
                                TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                                TraceAddBlockReason(actionTrace, "barrier_blocked_all_targets");
                                continue;
                            }

                            Dictionary<string, object> positionMetadata = _helper.BuildPositionMetadata(
                                action,
                                context,
                                target,
                                skillDefinition
                            );
                            positionMetadata["action_label"] = candidateLabel;
                            List<CombatEffectDefinition> effectDefinitions = _helper.CollectUnitSkillEffectDefinitions(
                                skillDefinition,
                                castVariant,
                                skillEntry.SkillLevel
                            );
                            BattleWindupQuote? windupQuote = null;
                            BattleAiSkillCandidateScoreFacts? candidateScoreFacts = null;
                            if (skillDefinition.CombatProfile.Windup != null)
                            {
                                if (
                                    !BattleWindupRules.TryBuildQuote(
                                        actor,
                                        skillDefinition,
                                        command.windup_tier,
                                        out BattleWindupQuote resolvedWindupQuote,
                                        out _
                                    )
                                )
                                {
                                    TraceAddBlockReason(
                                        actionTrace,
                                        "windup_quote_invariant_reject"
                                    );
                                    continue;
                                }
                                windupQuote = resolvedWindupQuote;
                                candidateScoreFacts = new BattleAiSkillCandidateScoreFacts(
                                    resolvedWindupQuote.TotalStaminaCost,
                                    resolvedWindupQuote.TotalWindupTu
                                );
                                effectDefinitions = new List<CombatEffectDefinition>(
                                    BattleWindupRules.ApplyWeaponDiceMultiplier(
                                        effectDefinitions,
                                        resolvedWindupQuote.WeaponDiceMultiplier
                                    )
                                );
                            }
                            BattleAiScoreInput scoreInput = BuildSkillScoreInput(
                                action,
                                context,
                                skillDefinition,
                                command,
                                preview,
                                effectDefinitions,
                                positionMetadata,
                                candidateScoreFacts
                            );
                            Dictionary<string, object> candidateExtra = actionTrace != null
                                ? BuildCandidateExtra(
                                    skillId,
                                    optionId,
                                    skillDefinition,
                                    castVariant,
                                    target
                                )
                                : null;
                            if (candidateExtra != null && windupQuote != null)
                                candidateExtra["windup_tier"] = command.windup_tier;
                            if (candidateExtra != null && sourceRetreatDirection != Vector2I.Zero)
                            {
                                candidateExtra["source_retreat_direction"] =
                                    sourceRetreatDirection;
                                candidateExtra["source_retreat_final_coord"] =
                                    preview.resolved_anchor_coord;
                                candidateExtra["source_retreat_path_length"] =
                                    preview.SourceRetreatPathTyped.Count;
                            }

                            if (scoreInput == null)
                            {
                                fallbackDecision ??= _helper.CreateDecision(
                                    action,
                                    command,
                                    $"{actorDisplayName} 选择对 {target.display_name} 使用 {candidateLabel}。"
                                );
                                OfferCandidate(
                                    actionTrace,
                                    candidateLabel,
                                    target,
                                    command,
                                    null,
                                    candidateExtra
                                );
                                continue;
                            }

                            if (scoreInput.effective_target_count < action.MinimumEffectiveTargetCount)
                            {
                                TraceAddBlockReason(actionTrace, "minimum_effective_target_count");
                                continue;
                            }
                            if (!PassesFriendlyFireLimits(action, scoreInput))
                            {
                                TraceAddBlockReason(actionTrace, "friendly_fire_limit");
                                continue;
                            }

                            OfferCandidate(
                                actionTrace,
                                candidateLabel,
                                target,
                                command,
                                scoreInput,
                                candidateExtra
                            );
                            if (!BattleAiDecisionEngine.IsBetterScoreInputTyped(scoreInput, bestScoreInput))
                                continue;

                            bestScoreInput = scoreInput;
                            bestDecision = _helper.CreateScoredDecision(
                                action,
                                command,
                                scoreInput,
                                $"{actorDisplayName} 选择对 {target.display_name} 使用 {candidateLabel}（评分 {scoreInput.total_score}）。"
                            );
                        }
                    }
                }
            }
        }

        BattleAiDecision resolvedDecision = bestDecision ?? fallbackDecision;
        FinalizeActionTrace(context, actionTrace, resolvedDecision);
        return resolvedDecision;
    }

    private IReadOnlyList<Vector2I> BuildSourceRetreatDirectionOptions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        int skillLevel,
        BattleUnitState actor,
        BattleUnitState target
    )
    {
        IReadOnlyList<CombatEffectDefinition> effectDefinitions =
            _helper.CollectUnitSkillEffectDefinitions(
                skillDefinition,
                castVariant,
                skillLevel
            );
        if (!BattleSourceRetreatRules.HasEffect(effectDefinitions))
            return new[] { Vector2I.Zero };
        if (actor == null || target == null)
            return Array.Empty<Vector2I>();

        var result = new List<Vector2I>();
        foreach (Vector2I direction in BattleSourceRetreatRules.CardinalDirections)
        {
            if (
                BattleSourceRetreatRules.IncreasesDistanceFromTarget(
                    actor.GetAnchorCoord(),
                    target.GetAnchorCoord(),
                    direction
                )
            )
            {
                result.Add(direction);
            }
        }
        return result;
    }

    private static string FormatDirection(Vector2I direction)
    {
        if (direction == Vector2I.Up)
            return "上";
        if (direction == Vector2I.Right)
            return "右";
        if (direction == Vector2I.Down)
            return "下";
        if (direction == Vector2I.Left)
            return "左";
        return "";
    }

    private static bool HasExplicitDistanceContract(
        EnemyAiDistanceReference distanceReference,
        int desiredMinDistance,
        int desiredMaxDistance
    )
    {
        return desiredMinDistance >= 0
            && desiredMaxDistance >= desiredMinDistance
            && (
                distanceReference == EnemyAiDistanceReference.TargetUnit
                || distanceReference == EnemyAiDistanceReference.EnemyFrontline
            );
    }

    private static string FormatSkillVariantLabel(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        if (skillDefinition == null)
            return "";
        string optionName = castVariant?.DisplayName ?? "";
        return string.IsNullOrEmpty(optionName)
            ? skillDefinition.DisplayName
            : $"{skillDefinition.DisplayName}·{optionName}";
    }

    private static BattleAiScoreInput BuildSkillScoreInput(
        UseUnitSkillActionDefinition action,
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<string, object> metadata,
        BattleAiSkillCandidateScoreFacts? candidateScoreFacts
    )
    {
        if (context == null)
            return null;

        Dictionary<string, object> scoringMetadata = CloneTraceMetadata(metadata);
        scoringMetadata["score_bucket_id"] = action.ScoreBucketId;
        scoringMetadata["action_kind"] = ReadTraceStringName(
            scoringMetadata,
            "action_kind",
            new StringName("skill")
        );
        StringName defaultActionIntent =
            BattleAiActionIntent.IsValid(action.ActionIntent)
            && action.ActionIntent != BattleAiActionIntent.Positioning
                ? action.ActionIntent
                : BattleAiActionIntent.InferForSkill(
                    skillDefinition,
                    effectDefinitions
                );
        scoringMetadata["action_intent"] = ReadTraceStringName(
            scoringMetadata,
            "action_intent",
            defaultActionIntent
        );
        scoringMetadata["action_label"] = ReadTraceString(
            scoringMetadata,
            "action_label",
            !string.IsNullOrEmpty(skillDefinition?.DisplayName)
                ? skillDefinition.DisplayName
                : action.ActionId.ToString()
        );
        scoringMetadata = context.MergeCurrentActionMetadataTyped(scoringMetadata);
        scoringMetadata["score_bucket_id"] = ReadTraceStringName(
            scoringMetadata,
            "score_bucket_id",
            action.ScoreBucketId
        );
        return context.BuildSkillScoreInputTyped(
            skillDefinition,
            command,
            preview,
            effectDefinitions,
            scoringMetadata,
            candidateScoreFacts
        );
    }

    private Dictionary<string, object> BuildCandidateExtra(
        StringName skillId,
        StringName optionId,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        BattleUnitState target
    )
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["skill_id"] = skillId.ToString(),
            ["skill_variant_id"] = optionId.ToString(),
            ["skill_variant_target_mode"] = BattleTypedNames
                .ToStringName(
                    _helper.GetCastVariantTargetModeKind(
                        skillDefinition?.CombatProfile,
                        castVariant
                    )
                )
                .ToString(),
            ["target_unit_id"] = target?.unit_id.ToString() ?? "",
        };
    }

    private static bool PassesFriendlyFireLimits(
        UseUnitSkillActionDefinition action,
        BattleAiScoreInput scoreInput
    )
    {
        if (scoreInput == null || action == null)
            return false;
        if (
            scoreInput.estimated_friendly_fire_target_count
            > action.MaximumFriendlyFireTargetCount
        )
        {
            return false;
        }
        return action.AllowFriendlyLethal
            || scoreInput.estimated_friendly_lethal_target_count <= 0;
    }

    internal static BattlePreview BuildFastUnitSkillPreview(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattleUnitState target,
        out string rejectCounterKey
    )
    {
        rejectCounterKey = "";
        var preview = new BattlePreview();
        BattleUnitState actor = context?.unit_state;
        BattleGridService grid = context?.grid_service;
        if (
            actor == null
            || grid == null
            || skillDefinition?.CombatProfile == null
            || command == null
        )
        {
            rejectCounterKey = FastPreviewRejectMissingContext;
            return preview;
        }
        if (target == null || !target.IsAlive())
        {
            rejectCounterKey = FastPreviewRejectInvalidTarget;
            return preview;
        }
        if (
            !BattleTargetTeamRules.IsUnitValidForFilter(
                actor,
                target,
                skillDefinition.CombatProfile.TargetTeamFilter,
                new BattleTargetTeamRules.TargetFilterOptions(
                    MadnessTargetAnyTeam: actor.ai_blackboard?.madness_target_any_team == true
                )
            )
        )
        {
            rejectCounterKey = FastPreviewRejectTargetFilter;
            return preview;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillRange(actor, skillDefinition);
        if (grid.GetDistanceBetweenUnits(actor, target) > effectiveRange)
        {
            rejectCounterKey = FastPreviewRejectOutOfRange;
            return preview;
        }

        preview.allowed = true;
        preview.AddTargetUnitId(target.unit_id);
        foreach (Vector2I coord in target.GetOccupiedCoordsReadViewTyped())
        {
            if (!preview.ContainsTargetCoord(coord))
            {
                preview.AddTargetCoord(coord);
            }
        }
        preview.resolved_anchor_coord =
            preview.TargetCoordsTyped.Count > 0
                ? preview.TargetCoordsTyped[0]
                : new Vector2I(-1, -1);
        return preview;
    }

    private static AiActionTrace BeginActionTrace(
        UseUnitSkillActionDefinition action,
        BattleAiContext context,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        Dictionary<string, object> traceMetadata =
            context != null
                ? context.MergeCurrentActionMetadataTyped(metadata)
                : CloneTraceMetadata(metadata);
        StringName scoreBucketId = ReadTraceStringName(
            traceMetadata,
            "score_bucket_id",
            action?.ScoreBucketId ?? EmptyStringName
        );
        StringName actionId = action?.ActionId ?? EmptyStringName;
        return EnemyAiActionHelper.BeginActionTrace(
            actionId,
            scoreBucketId,
            context,
            traceMetadata
        );
    }

    private static void TraceCountIncrement(AiActionTrace actionTrace, string key, int amount = 1)
    {
        actionTrace?.Increment(key, amount);
    }

    private static void TraceAddBlockReason(AiActionTrace actionTrace, string reasonKey)
    {
        actionTrace?.AddBlockReason(reasonKey);
    }

    private static void OfferCandidate(
        AiActionTrace actionTrace,
        string optionLabel,
        BattleUnitState target,
        BattleCommand command,
        BattleAiScoreInput scoreInput,
        IReadOnlyDictionary<string, object> candidateExtra
    )
    {
        if (actionTrace == null)
            return;
        EnemyAiActionHelper.TraceOfferCandidate(
            actionTrace,
            EnemyAiActionHelper.BuildCandidateSummary(
                $"{optionLabel}->{target?.display_name ?? ""}",
                command,
                scoreInput,
                candidateExtra
            )
        );
    }

    private static Dictionary<string, object> CloneTraceMetadata(
        IReadOnlyDictionary<string, object> source
    )
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        if (source == null)
            return result;

        foreach (KeyValuePair<string, object> entry in source)
        {
            if (!string.IsNullOrEmpty(entry.Key))
                result[entry.Key] = entry.Value;
        }
        return result;
    }

    private static StringName ReadTraceStringName(
        IReadOnlyDictionary<string, object> source,
        string key,
        StringName fallback = default
    )
    {
        if (
            source == null
            || string.IsNullOrEmpty(key)
            || !source.TryGetValue(key, out object value)
            || value == null
        )
        {
            return fallback ?? EmptyStringName;
        }

        return value switch
        {
            StringName stringName => stringName,
            string text when !string.IsNullOrEmpty(text) => new StringName(text),
            Variant variant when variant.VariantType == Variant.Type.StringName =>
                variant.AsStringName(),
            Variant variant when variant.VariantType == Variant.Type.String =>
                new StringName(variant.AsString()),
            _ => fallback ?? EmptyStringName,
        };
    }

    private static string ReadTraceString(
        IReadOnlyDictionary<string, object> source,
        string key,
        string fallback = ""
    )
    {
        if (
            source == null
            || string.IsNullOrEmpty(key)
            || !source.TryGetValue(key, out object value)
            || value == null
        )
        {
            return fallback;
        }

        return value switch
        {
            string text => text,
            StringName stringName => stringName.ToString(),
            Variant variant when variant.VariantType == Variant.Type.String => variant.AsString(),
            Variant variant when variant.VariantType == Variant.Type.StringName =>
                variant.AsStringName().ToString(),
            _ => value.ToString() ?? fallback,
        };
    }

    private static void FinalizeActionTrace(
        BattleAiContext context,
        AiActionTrace actionTrace,
        BattleAiDecision bestDecision
    ) => EnemyAiActionHelper.FinalizeActionTrace(context, actionTrace, bestDecision);

}
