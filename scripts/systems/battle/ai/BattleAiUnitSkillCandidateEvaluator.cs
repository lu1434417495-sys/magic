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
    private readonly BattleAiDecisionEngine _scoreOrdering = new();

    internal BattleAiDecision Evaluate(UseUnitSkillAction action, BattleAiContext context)
    {
        return Evaluate(BattleAiUnitSkillActionSpec.FromAction(action), context);
    }

    internal BattleAiDecision Evaluate(BattleAiUnitSkillActionSpec action, BattleAiContext context)
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
        AiActionTrace actionTrace = BeginActionTrace(
            action,
            context,
            traceEnabled,
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
        );

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
                    TraceCountIncrement(actionTrace, "evaluation_count", 1);
                    BattleCommand command = _helper.BuildUnitSkillCommand(
                        context,
                        skillEntry,
                        target,
                        optionId
                    );
                    BattlePreview preview = BuildFastUnitSkillPreview(
                        context,
                        skillDefinition,
                        command,
                        target,
                        out string previewRejectCounterKey
                    );
                    if (preview == null || !preview.allowed)
                    {
                        TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                        TraceCountIncrement(actionTrace, previewRejectCounterKey, 1);
                        continue;
                    }

                    Dictionary<string, object> positionMetadata = _helper.BuildPositionMetadata(
                        action,
                        context,
                        target,
                        skillDefinition
                    );
                    positionMetadata["action_label"] = optionLabel;
                    List<CombatEffectDefinition> effectDefinitions = _helper.CollectUnitSkillEffectDefinitions(
                        skillDefinition,
                        castVariant,
                        skillEntry.SkillLevel
                    );
                    BattleAiScoreInput scoreInput = BuildSkillScoreInput(
                        action,
                        context,
                        skillDefinition,
                        command,
                        preview,
                        effectDefinitions,
                        positionMetadata
                    );
                    Dictionary<string, object> candidateExtra = BuildCandidateExtra(
                        skillId,
                        optionId,
                        skillDefinition,
                        castVariant,
                        target
                    );

                    if (scoreInput == null)
                    {
                        fallbackDecision ??= _helper.CreateDecision(
                            action,
                            command,
                            $"{actorDisplayName} 选择对 {target.display_name} 使用 {optionLabel}。"
                        );
                        OfferCandidate(
                            actionTrace,
                            traceEnabled,
                            optionLabel,
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
                        traceEnabled,
                        optionLabel,
                        target,
                        command,
                        scoreInput,
                        candidateExtra
                    );
                    if (!_scoreOrdering.IsBetterScoreInput(scoreInput, bestScoreInput))
                        continue;

                    bestScoreInput = scoreInput;
                    bestDecision = _helper.CreateScoredDecision(
                        action,
                        command,
                        scoreInput,
                        $"{actorDisplayName} 选择对 {target.display_name} 使用 {optionLabel}（评分 {scoreInput.total_score}）。"
                    );
                }
            }
        }

        BattleAiDecision resolvedDecision = bestDecision ?? fallbackDecision;
        FinalizeActionTrace(context, actionTrace, resolvedDecision, traceEnabled);
        return resolvedDecision;
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
        BattleAiUnitSkillActionSpec action,
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<string, object> metadata
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
            scoringMetadata
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
        BattleAiUnitSkillActionSpec action,
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

    private static BattlePreview BuildFastUnitSkillPreview(
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
        if (target == null || !target.is_alive)
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
        target.RefreshFootprint();
        foreach (Vector2I coord in target.occupied_coords)
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
        BattleAiUnitSkillActionSpec action,
        BattleAiContext context,
        bool traceEnabled,
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
        StringName traceId = context != null ? context.NextActionTraceId(actionId) : actionId;
        return new AiActionTrace(
            traceId,
            actionId.ToString(),
            scoreBucketId.ToString(),
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
        bool traceEnabled,
        string optionLabel,
        BattleUnitState target,
        BattleCommand command,
        BattleAiScoreInput scoreInput,
        IReadOnlyDictionary<string, object> candidateExtra
    )
    {
        if (actionTrace == null)
            return;
        if (!traceEnabled)
        {
            actionTrace.Increment("candidate_count", 1);
            return;
        }

        AiCandidateSummary candidateSummary = AiCandidateSummary.Create(
            $"{optionLabel}->{target?.display_name ?? ""}",
            command,
            scoreInput,
            candidateExtra
        );
        actionTrace.OfferCandidate(candidateSummary, 5);
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
        BattleAiDecision bestDecision,
        bool traceEnabled
    )
    {
        if (actionTrace == null || actionTrace.IsEmpty())
            return;
        if (bestDecision != null)
            bestDecision.action_trace_id = actionTrace.TraceId;
        if (!traceEnabled)
            return;

        actionTrace.ApplyBestDecision(bestDecision);
        context?.RecordActionTrace(actionTrace);
    }

}
