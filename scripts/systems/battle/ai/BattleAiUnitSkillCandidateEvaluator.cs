using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

internal sealed class BattleAiUnitSkillCandidateEvaluator
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName TargetModeUnit = "unit";
    private static readonly Vector2I InvalidCoord = new(-1, -1);

    private readonly BattleAiTypedActionHelper _helper = new();
    private readonly BattleAiDecisionEngine _scoreOrdering = new();

    internal BattleAiDecision Evaluate(UseUnitSkillAction action, BattleAiContext context)
    {
        if (action == null || context == null)
            return null;

        EnemyAiDistanceReference distanceReference = action.DistanceReferenceKind;
        int desiredMinDistance = action.desired_min_distance;
        int desiredMaxDistance = action.desired_max_distance;
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
                ["target_selector"] = action.target_selector.ToString(),
                ["minimum_effective_target_count"] = action.minimum_effective_target_count,
                ["maximum_friendly_fire_target_count"] =
                    action.maximum_friendly_fire_target_count,
                ["allow_friendly_lethal"] = action.allow_friendly_lethal,
                ["distance_reference"] = action.distance_reference.ToString(),
                ["desired_min_distance"] = desiredMinDistance,
                ["desired_max_distance"] = desiredMaxDistance,
            }
        );

        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        BattleAiDecision fallbackDecision = null;
        List<StringName> knownSkillIds = _helper.ResolveKnownSkillIds(context, action.skill_ids);

        foreach (StringName skillId in knownSkillIds)
        {
            TraceCountIncrement(actionTrace, "skill_considered_count", 1);
            SkillDef skillDef = _helper.GetSkillDef(context, skillId);
            CombatSkillDef combatProfile = skillDef?.combat_profile;
            if (skillDef == null || combatProfile == null)
            {
                TraceAddBlockReason(actionTrace, "missing_skill_def");
                continue;
            }
            if (combatProfile.TargetModeKind != BattleTargetMode.Unit)
            {
                TraceAddBlockReason(actionTrace, "non_unit_skill");
                continue;
            }

            BattleSkillCastBlockReasonKind blockReason = _helper.GetSkillCastBlockReason(
                context,
                skillDef
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
                combatProfile.target_team_filter,
                action.target_selector
            );
            if (targets.Count == 0)
            {
                TraceAddBlockReason(actionTrace, "no_valid_targets");
                continue;
            }

            List<CombatCastVariantDef> castVariants = _helper.GetUnitCastVariants(
                context,
                skillDef
            );
            if (castVariants.Count == 0)
            {
                TraceAddBlockReason(actionTrace, "no_unlocked_unit_options");
                continue;
            }

            foreach (CombatCastVariantDef castVariant in castVariants)
            {
                StringName optionId = castVariant?.variant_id ?? EmptyStringName;
                string optionLabel = FormatSkillVariantLabel(skillDef, castVariant);

                foreach (BattleUnitState target in targets)
                {
                    if (target == null)
                        continue;
                    TraceCountIncrement(actionTrace, "evaluation_count", 1);
                    BattleCommand command = _helper.BuildUnitSkillCommand(
                        context,
                        skillId,
                        target,
                        optionId
                    );
                    BattlePreview preview = BuildFastUnitSkillPreview(
                        context,
                        skillDef,
                        combatProfile,
                        command,
                        target
                    );
                    if (preview == null || !preview.allowed)
                    {
                        TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                        continue;
                    }

                    Dictionary<string, object> positionMetadata = _helper.BuildPositionMetadata(
                        action,
                        context,
                        target,
                        skillDef
                    );
                    positionMetadata["action_label"] = optionLabel;
                    List<CombatEffectDef> effectDefs = _helper.CollectUnitSkillEffectDefs(
                        skillDef,
                        castVariant,
                        actor
                    );
                    BattleAiScoreInput scoreInput = BuildSkillScoreInput(
                        action,
                        context,
                        skillDef,
                        command,
                        preview,
                        effectDefs,
                        positionMetadata
                    );
                    Dictionary<string, object> candidateExtra = BuildCandidateExtra(
                        skillId,
                        optionId,
                        skillDef,
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

                    if (scoreInput.effective_target_count < action.minimum_effective_target_count)
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
        SkillDef skillDef,
        CombatCastVariantDef castVariant
    )
    {
        if (skillDef == null)
            return "";
        string optionName = castVariant?.display_name ?? "";
        return string.IsNullOrEmpty(optionName)
            ? skillDef.display_name
            : $"{skillDef.display_name}·{optionName}";
    }

    private static BattleAiScoreInput BuildSkillScoreInput(
        UseUnitSkillAction action,
        BattleAiContext context,
        SkillDef skillDef,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDef> effectDefs,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        if (context == null)
            return null;

        Dictionary<string, object> scoringMetadata = CloneTraceMetadata(metadata);
        scoringMetadata["score_bucket_id"] = action.score_bucket_id;
        scoringMetadata["action_kind"] = ReadTraceStringName(
            scoringMetadata,
            "action_kind",
            new StringName("skill")
        );
        StringName defaultActionIntent =
            BattleAiActionIntent.IsValid(action.action_intent)
            && action.action_intent != BattleAiActionIntent.Positioning
                ? action.action_intent
                : BattleAiActionIntent.InferForSkill(skillDef, effectDefs);
        scoringMetadata["action_intent"] = ReadTraceStringName(
            scoringMetadata,
            "action_intent",
            defaultActionIntent
        );
        scoringMetadata["action_label"] = ReadTraceString(
            scoringMetadata,
            "action_label",
            !string.IsNullOrEmpty(skillDef?.display_name)
                ? skillDef.display_name
                : action.action_id.ToString()
        );
        scoringMetadata = context.MergeCurrentActionMetadataTyped(scoringMetadata);
        scoringMetadata["score_bucket_id"] = ReadTraceStringName(
            scoringMetadata,
            "score_bucket_id",
            action.score_bucket_id
        );
        return context.BuildSkillScoreInputTyped(
            skillDef,
            command,
            preview,
            effectDefs,
            scoringMetadata
        );
    }

    private Dictionary<string, object> BuildCandidateExtra(
        StringName skillId,
        StringName optionId,
        SkillDef skillDef,
        CombatCastVariantDef castVariant,
        BattleUnitState target
    )
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["skill_id"] = skillId.ToString(),
            ["skill_variant_id"] = optionId.ToString(),
            ["skill_variant_target_mode"] = BattleTypedNames
                .ToStringName(_helper.GetCastVariantTargetModeKind(skillDef, castVariant))
                .ToString(),
            ["target_unit_id"] = target?.unit_id.ToString() ?? "",
        };
    }

    private static bool PassesFriendlyFireLimits(
        UseUnitSkillAction action,
        BattleAiScoreInput scoreInput
    )
    {
        if (scoreInput == null || action == null)
            return false;
        if (
            scoreInput.estimated_friendly_fire_target_count
            > action.maximum_friendly_fire_target_count
        )
        {
            return false;
        }
        return action.allow_friendly_lethal
            || scoreInput.estimated_friendly_lethal_target_count <= 0;
    }

    private static BattlePreview BuildFastUnitSkillPreview(
        BattleAiContext context,
        SkillDef skillDef,
        CombatSkillDef combatProfile,
        BattleCommand command,
        BattleUnitState target
    )
    {
        var preview = new BattlePreview();
        BattleUnitState actor = context?.unit_state;
        BattleGridService grid = context?.grid_service;
        if (
            actor == null
            || grid == null
            || skillDef == null
            || combatProfile == null
            || command == null
            || target == null
            || !target.is_alive
        )
        {
            return preview;
        }
        if (
            !BattleTargetTeamRules.IsUnitValidForFilter(
                actor,
                target,
                combatProfile.target_team_filter,
                new BattleTargetTeamRules.TargetFilterOptions(
                    MadnessTargetAnyTeam: actor.ai_blackboard?.madness_target_any_team == true
                )
            )
        )
        {
            return preview;
        }
        int effectiveRange = BattleRangeService.GetEffectiveSkillRange(actor, skillDef);
        if (grid.GetDistanceBetweenUnits(actor, target) > effectiveRange)
        {
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
        UseUnitSkillAction action,
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
            action?.score_bucket_id ?? EmptyStringName
        );
        StringName actionId = action?.action_id ?? EmptyStringName;
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
