using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiChargePathAoeActionEvaluator
{
    private static readonly StringName EmptyStringName = "";

    private readonly BattleAiTypedActionHelper _helper = new();
    private readonly BattleAiDecisionEngine _scoreOrdering = new();

    private readonly struct ChargeTargetInfo
    {
        public ChargeTargetInfo(bool valid, int distance = 0, Vector2I direction = default)
        {
            Valid = valid;
            Distance = distance;
            Direction = direction;
        }

        public bool Valid { get; }
        public int Distance { get; }
        public Vector2I Direction { get; }
    }

    private sealed class PathStepHitMetrics
    {
        public Vector2I ResolvedAnchorCoord = new(-1, -1);
        public int ResolvedMoveDistance;
        public int HitCount;
        public Dictionary<StringName, int> HitCountsByUnitId = new();

        public int UniqueTargetCount => HitCountsByUnitId.Count;

        public void AddHit(StringName unitId)
        {
            StringName normalizedUnitId = ProgressionDataUtils.to_string_name(unitId);
            if (normalizedUnitId == "")
                return;
            HitCountsByUnitId[normalizedUnitId] =
                HitCountsByUnitId.GetValueOrDefault(normalizedUnitId, 0) + 1;
            HitCount += 1;
        }

        public void ApplyToMetadata(IDictionary<string, object> metadata)
        {
            if (metadata == null)
                return;
            metadata["resolved_anchor_coord"] = ResolvedAnchorCoord;
            metadata["resolved_move_distance"] = ResolvedMoveDistance;
            metadata["path_step_hit_count"] = HitCount;
            metadata["path_step_unique_target_count"] = UniqueTargetCount;
            metadata["path_step_hit_counts_by_unit_id"] = BuildHitCountsMetadata();
        }

        private Dictionary<StringName, int> BuildHitCountsMetadata()
        {
            var result = new Dictionary<StringName, int>();
            foreach (KeyValuePair<StringName, int> entry in HitCountsByUnitId)
            {
                if (entry.Key != "" && entry.Value > 0)
                    result[entry.Key] = entry.Value;
            }
            return result;
        }
    }

    internal BattleAiDecision Evaluate(UseChargePathAoeAction action, BattleAiContext context)
    {
        return Evaluate(BattleAiChargePathAoeActionSpec.FromAction(action), context);
    }

    internal BattleAiDecision Evaluate(
        BattleAiChargePathAoeActionSpec action,
        BattleAiContext context
    )
    {
        if (action == null || context?.unit_state == null || context.state == null)
            return null;

        AiActionTrace actionTrace = BeginActionTrace(
            action,
            context,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action_kind"] = "charge_path_aoe",
                ["target_selector"] = action.TargetSelector.ToString(),
                ["minimum_hit_count"] = action.MinimumHitCount,
                ["desired_min_distance"] = action.DesiredMinDistance,
                ["desired_max_distance"] = action.DesiredMaxDistance,
            }
        );
        List<BattleUnitState> targets = _helper.SortTargetUnits(
            context,
            "enemy",
            action.TargetSelector
        );
        if (targets.Count == 0)
        {
            TraceAddBlockReason(actionTrace, "no_valid_targets");
            FinalizeActionTrace(context, actionTrace);
            return null;
        }

        BattleUnitState focusTarget = targets[0];
        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        BattleAiDecision fallbackDecision = null;
        BattleUnitState actor = context.unit_state;
        BattleState state = context.state;
        foreach (BattleAvailableSkillEntry skillEntry in _helper.ResolveAvailableSkillEntries(context, action.SkillIds))
        {
            StringName skillId = skillEntry.EntryRef.SkillId;
            TraceCountIncrement(actionTrace, "skill_considered_count", 1);
            SkillDefinition skillDefinition = _helper.GetSkillDefinition(context, skillEntry);
            if (
                skillDefinition?.CombatProfile == null
                || skillDefinition.CombatProfile.TargetModeKind != BattleTargetMode.Ground
            )
            {
                TraceAddBlockReason(
                    actionTrace,
                    skillDefinition == null ? "missing_skill_definition" : "non_ground_skill"
                );
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

            foreach (CombatCastVariantDefinition castVariant in GetGroundOptionDefinitions(
                context,
                skillDefinition,
                skillEntry.SkillLevel
            ))
            {
                if (castVariant == null || !IsChargeOption(castVariant))
                    continue;

                CombatEffectDefinition pathStepEffect = GetPathStepAoeEffect(castVariant);
                if (pathStepEffect == null)
                {
                    TraceAddBlockReason(actionTrace, "missing_path_step_aoe");
                    continue;
                }

                for (int y = 0; y < state.map_size.Y; y++)
                for (int x = 0; x < state.map_size.X; x++)
                {
                    TraceCountIncrement(actionTrace, "evaluation_count", 1);
                    Vector2I targetCoord = new(x, y);
                    ChargeTargetInfo chargeTargetInfo = ResolveChargeTargetInfo(
                        actor,
                        targetCoord
                    );
                    if (!chargeTargetInfo.Valid)
                        continue;

                    BattleCommand command = BuildGroundSkillCommand(
                        context,
                        skillEntry,
                        castVariant.VariantId,
                        new[] { targetCoord }
                    );
                    AiTraceRecorder.Enter("charge_path_aoe:formal_preview");
                    BattlePreview preview = context.PreviewCommand(command);
                    AiTraceRecorder.Exit("charge_path_aoe:formal_preview");
                    preview ??= BuildFastChargePathPreview(
                        actor,
                        command,
                        chargeTargetInfo,
                        targetCoord
                    );
                    if (preview?.allowed != true)
                    {
                        TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                        continue;
                    }

                    PathStepHitMetrics metrics = BuildPathStepHitMetrics(
                        context,
                        skillDefinition,
                        pathStepEffect,
                        preview.resolved_anchor_coord
                    );
                    int pathHitCount = metrics.HitCount;
                    if (pathHitCount < action.MinimumHitCount)
                    {
                        TraceAddBlockReason(actionTrace, "minimum_hit_count");
                        continue;
                    }

                    Vector2I resolvedAnchor =
                        metrics.ResolvedAnchorCoord != new Vector2I(-1, -1)
                            ? metrics.ResolvedAnchorCoord
                            : preview.resolved_anchor_coord;
                    int resolvedMoveDistance = metrics.ResolvedMoveDistance;
                    int chargeActionScore = 10 + Math.Max(resolvedMoveDistance - 1, 0) * 4;
                    string variantLabel = EnemyAiActionHelper.FormatSkillVariantLabel(
                        skillDefinition,
                        castVariant
                    );
                    var positionMetadata = new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["action_kind"] = "skill",
                        ["action_base_score"] = chargeActionScore,
                        ["position_target_unit_id"] = focusTarget.unit_id,
                        ["position_anchor_coord"] = resolvedAnchor,
                        ["desired_min_distance"] = action.DesiredMinDistance,
                        ["desired_max_distance"] = action.DesiredMaxDistance,
                        ["action_label"] = variantLabel,
                    };
                    metrics.ApplyToMetadata(positionMetadata);
                    BattleAiScoreInput scoreInput = BuildSkillScoreInput(
                        action,
                        context,
                        skillDefinition,
                        command,
                        preview,
                        castVariant.EffectDefinitions,
                        positionMetadata
                    );
                    TraceOfferCandidate(
                        actionTrace,
                        EnemyAiActionHelper.BuildCandidateSummary(
                            variantLabel,
                            command,
                            scoreInput,
                            new Dictionary<string, object>(StringComparer.Ordinal)
                            {
                                ["path_step_hit_count"] = pathHitCount,
                                ["path_step_unique_target_count"] = metrics.UniqueTargetCount,
                                ["resolved_anchor_coord"] = resolvedAnchor,
                                ["resolved_move_distance"] = resolvedMoveDistance,
                                ["skill_id"] = skillId.ToString(),
                            }
                        )
                    );

                    if (scoreInput == null)
                    {
                        fallbackDecision ??= EnemyAiActionHelper.CreateDecision(
                            action.ActionId,
                            action.ScoreBucketId,
                            command,
                            $"{actor.display_name} 准备用 {skillDefinition.DisplayName} 沿途命中 {pathHitCount} 次。"
                        );
                        continue;
                    }
                    if (!_scoreOrdering.IsBetterScoreInput(scoreInput, bestScoreInput))
                        continue;
                    bestScoreInput = scoreInput;
                    bestDecision = EnemyAiActionHelper.CreateScoredDecision(
                        action.ActionId,
                        action.ScoreBucketId,
                        command,
                        scoreInput,
                        $"{actor.display_name} 准备用 {skillDefinition.DisplayName} 沿途命中 {pathHitCount} 次（评分 {ScoreTotal(scoreInput)}）。"
                    );
                }
            }
        }

        BattleAiDecision resolvedDecision = bestDecision ?? fallbackDecision;
        FinalizeActionTrace(context, actionTrace, resolvedDecision);
        return resolvedDecision;
    }

    private static CombatEffectDefinition GetPathStepAoeEffect(CombatCastVariantDefinition castVariant)
    {
        if (castVariant == null)
            return null;
        foreach (CombatEffectDefinition effectDefinition in castVariant.EffectDefinitions)
        {
            if (
                effectDefinition != null
                && effectDefinition.EffectKind == BattleEffectKind.PathStepAoe
            )
            {
                return effectDefinition;
            }
        }
        return null;
    }

    private static ChargeTargetInfo ResolveChargeTargetInfo(
        BattleUnitState unitState,
        Vector2I targetCoord
    )
    {
        if (unitState == null)
            return new ChargeTargetInfo(false);
        unitState.RefreshFootprint();
        int minX = unitState.coord.X;
        int maxX = unitState.coord.X + unitState.footprint_size.X - 1;
        int minY = unitState.coord.Y;
        int maxY = unitState.coord.Y + unitState.footprint_size.Y - 1;
        if (targetCoord.Y >= minY && targetCoord.Y <= maxY)
        {
            if (targetCoord.X < minX)
                return new ChargeTargetInfo(true, minX - targetCoord.X, Vector2I.Left);
            if (targetCoord.X > maxX)
                return new ChargeTargetInfo(true, targetCoord.X - maxX, Vector2I.Right);
        }
        if (targetCoord.X >= minX && targetCoord.X <= maxX)
        {
            if (targetCoord.Y < minY)
                return new ChargeTargetInfo(true, minY - targetCoord.Y, Vector2I.Up);
            if (targetCoord.Y > maxY)
                return new ChargeTargetInfo(true, targetCoord.Y - maxY, Vector2I.Down);
        }
        return new ChargeTargetInfo(false);
    }

    private static BattlePreview BuildFastChargePathPreview(
        BattleUnitState actor,
        BattleCommand command,
        ChargeTargetInfo chargeInfo,
        Vector2I targetCoord
    )
    {
        Vector2I resolvedAnchor =
            actor != null && chargeInfo.Valid
                ? actor.coord + chargeInfo.Direction * chargeInfo.Distance
                : new Vector2I(-1, -1);
        var preview = new BattlePreview
        {
            allowed = command != null && resolvedAnchor != new Vector2I(-1, -1),
            resolved_anchor_coord = resolvedAnchor,
            move_cost = Math.Max(chargeInfo.Distance, 0),
        };
        if (preview.allowed)
            preview.AddTargetCoord(targetCoord);
        return preview;
    }

    private static PathStepHitMetrics BuildPathStepHitMetrics(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        CombatEffectDefinition pathStepEffect,
        Vector2I resolvedAnchorCoord
    )
    {
        var result = new PathStepHitMetrics { ResolvedAnchorCoord = resolvedAnchorCoord };
        if (
            context?.state == null
            || context.unit_state == null
            || context.grid_service == null
            || pathStepEffect == null
            || resolvedAnchorCoord == new Vector2I(-1, -1)
        )
        {
            return result;
        }

        BattleUnitState actor = context.unit_state;
        List<Vector2I> path = BuildResolvedAnchorPath(actor.coord, resolvedAnchorCoord);
        if (path.Count == 0)
            return result;

        bool allowRepeatHits = pathStepEffect.AllowRepeatHitsAcrossSteps;
        StringName targetFilter = BattleTargetTeamRules.ResolveEffectTargetFilter(
            skillDefinition,
            pathStepEffect
        );
        BattleState state = context.state;
        foreach (Vector2I anchorCoord in path)
        {
            List<Vector2I> effectCoords = BuildPathStepEffectCoords(
                context,
                anchorCoord,
                pathStepEffect
            );
            if (effectCoords.Count == 0)
                continue;

            var stepUnitIds = new HashSet<StringName>();
            foreach (BattleUnitState targetUnit in state.GetUnitsTyped())
            {
                if (targetUnit == null || !targetUnit.is_alive)
                    continue;
                if (!BattleTargetTeamRules.IsUnitValidForFilter(actor, targetUnit, targetFilter))
                    continue;
                if (!UnitIntersectsCoords(targetUnit, effectCoords))
                    continue;
                if (!allowRepeatHits && result.HitCountsByUnitId.ContainsKey(targetUnit.unit_id))
                    continue;
                if (stepUnitIds.Contains(targetUnit.unit_id))
                    continue;
                stepUnitIds.Add(targetUnit.unit_id);
            }
            foreach (StringName unitId in stepUnitIds)
                result.AddHit(unitId);
        }
        result.ResolvedMoveDistance = path.Count;
        return result;
    }

    private static List<Vector2I> BuildResolvedAnchorPath(Vector2I sourceCoord, Vector2I resolvedAnchorCoord)
    {
        var path = new List<Vector2I>();
        Vector2I delta = resolvedAnchorCoord - sourceCoord;
        Vector2I direction = Vector2I.Zero;
        int distance = 0;
        if (delta.Y == 0 && delta.X != 0)
        {
            direction = delta.X > 0 ? Vector2I.Right : Vector2I.Left;
            distance = Math.Abs(delta.X);
        }
        else if (delta.X == 0 && delta.Y != 0)
        {
            direction = delta.Y > 0 ? Vector2I.Down : Vector2I.Up;
            distance = Math.Abs(delta.Y);
        }
        if (direction == Vector2I.Zero || distance <= 0)
            return path;

        Vector2I anchorCoord = sourceCoord;
        for (int index = 0; index < distance; index++)
        {
            anchorCoord += direction;
            path.Add(anchorCoord);
        }
        return path;
    }

    private static List<Vector2I> BuildPathStepEffectCoords(
        BattleAiContext context,
        Vector2I anchorCoord,
        CombatEffectDefinition pathStepEffect
    )
    {
        var result = new List<Vector2I>();
        if (context?.state == null || context.grid_service == null || pathStepEffect == null)
            return result;

        BattleUnitState actor = context.unit_state;
        BattleGridService grid = context.grid_service;
        BattleState state = context.state;
        StringName stepShape = ReadStringNameParameter(pathStepEffect, "step_shape", "diamond");
        int stepRadius = Math.Max(ReadIntParameter(pathStepEffect, "step_radius", 1), 0);
        var coordSet = new HashSet<Vector2I>();
        foreach (Vector2I occupiedCoord in grid.GetUnitTargetCoords(actor, anchorCoord))
        foreach (
            Vector2I effectCoord in grid.GetAreaCoords(
                state,
                occupiedCoord,
                stepShape,
                stepRadius,
                Vector2I.Zero
            )
        )
        {
            coordSet.Add(effectCoord);
        }
        result.AddRange(coordSet);
        SortCoordsInPlace(result);
        return result;
    }

    private static bool UnitIntersectsCoords(
        BattleUnitState unitState,
        IReadOnlyCollection<Vector2I> coords
    )
    {
        if (unitState == null || coords == null || coords.Count == 0)
            return false;
        var coordSet = new HashSet<Vector2I>(coords);
        unitState.RefreshFootprint();
        foreach (Vector2I occupiedCoord in unitState.occupied_coords)
        {
            if (coordSet.Contains(occupiedCoord))
                return true;
        }
        return false;
    }

    private static List<CombatCastVariantDefinition> GetGroundOptionDefinitions(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        int skillLevel
    )
    {
        var options = new List<CombatCastVariantDefinition>();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null || combatProfile.TargetModeKind != BattleTargetMode.Ground)
            return options;
        if (combatProfile.CastVariants.Count == 0)
        {
            options.Add(BuildImplicitGroundOptionDefinition(skillDefinition));
            return options;
        }

        SkillEffectiveCombatDefinition effectiveDefinition =
            context?.skill_catalog?.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
            ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        foreach (CombatCastVariantDefinition castVariant in effectiveDefinition.UnlockedCastVariants)
        {
            if (castVariant != null)
                options.Add(castVariant);
        }
        return options;
    }

    private static CombatCastVariantDefinition BuildImplicitGroundOptionDefinition(
        SkillDefinition skillDefinition
    )
    {
        CombatSkillDefinition profile = skillDefinition?.CombatProfile;
        return new CombatCastVariantDefinition(
            "",
            "",
            "",
            0,
            BattleTypedNames.ToStringName(BattleTargetMode.Ground),
            CombatSkillTargetingContentRules.ToFootprintPatternId(
                CombatCastFootprintPattern.Single
            ),
            1,
            Array.Empty<StringName>(),
            profile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>(),
            null
        );
    }

    private static bool IsChargeOption(CombatCastVariantDefinition castVariant)
    {
        if (castVariant == null)
            return false;
        foreach (
            CombatEffectDefinition effectDefinition in castVariant.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effectDefinition != null && effectDefinition.EffectKind == BattleEffectKind.Charge)
                return true;
        }
        return false;
    }

    private static BattleCommand BuildGroundSkillCommand(
        BattleAiContext context,
        BattleAvailableSkillEntry skillEntry,
        StringName skillVariantId,
        IEnumerable<Vector2I> targetCoords
    )
    {
        if (context?.unit_state == null || skillEntry == null)
            return null;
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = context.unit_state.unit_id,
            skill_entry_id = skillEntry.EntryRef.SkillEntryId,
            skill_id = skillEntry.EntryRef.SkillId,
            skill_variant_id = skillVariantId,
        };
        foreach (Vector2I coord in targetCoords ?? Array.Empty<Vector2I>())
        {
            command.AddTargetCoord(coord);
            if (command.target_coord == new Vector2I(-1, -1))
                command.target_coord = coord;
        }
        return command;
    }

    private static BattleAiScoreInput BuildSkillScoreInput(
        BattleAiChargePathAoeActionSpec action,
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        if (context == null || skillDefinition == null)
            return null;

        Dictionary<string, object> scoringMetadata = CloneMetadata(metadata);
        scoringMetadata["score_bucket_id"] = action.ScoreBucketId;
        scoringMetadata["action_kind"] = ReadMetadataStringName(
            scoringMetadata,
            "action_kind",
            new StringName("skill")
        );
        scoringMetadata["action_intent"] = ResolveMetadataActionIntent(
            scoringMetadata,
            ResolveDefaultSkillActionIntent(action, skillDefinition, effectDefinitions)
        );
        scoringMetadata["action_label"] = ReadMetadataString(
            scoringMetadata,
            "action_label",
            !string.IsNullOrEmpty(skillDefinition.DisplayName)
                ? skillDefinition.DisplayName
                : action.ActionId.ToString()
        );
        scoringMetadata = context.MergeCurrentActionMetadataTyped(scoringMetadata);
        scoringMetadata["score_bucket_id"] = ReadMetadataStringName(
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

    private static StringName ResolveDefaultSkillActionIntent(
        BattleAiChargePathAoeActionSpec action,
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions
    )
    {
        if (
            BattleAiActionIntent.IsValid(action.ActionIntent)
            && action.ActionIntent != BattleAiActionIntent.Positioning
        )
        {
            return action.ActionIntent;
        }
        return BattleAiActionIntent.InferForSkill(skillDefinition, effectDefinitions);
    }

    private static StringName ResolveMetadataActionIntent(
        IReadOnlyDictionary<string, object> metadata,
        StringName fallback
    )
    {
        StringName metadataIntent = ReadMetadataStringName(metadata, "action_intent", "");
        if (BattleAiActionIntent.IsValid(metadataIntent))
            return metadataIntent;
        return BattleAiActionIntent.IsValid(fallback) ? fallback : "";
    }

    private static StringName ReadStringNameParameter(
        CombatEffectDefinition effectDefinition,
        string key,
        StringName fallback = default
    )
    {
        if (effectDefinition == null || string.IsNullOrEmpty(key))
            return fallback;
        StringName value = effectDefinition.GetStringNameParamTyped(key, fallback);
        return value == "" ? fallback : value;
    }

    private static int ReadIntParameter(
        CombatEffectDefinition effectDefinition,
        string key,
        int fallback = 0
    )
    {
        return effectDefinition == null || string.IsNullOrEmpty(key)
            ? fallback
            : effectDefinition.GetIntParamTyped(key, fallback);
    }

    private static int GetSkillLevel(BattleUnitState unitState, StringName skillId)
    {
        if (unitState == null || skillId == "")
            return 0;
        int knownSkillLevel = unitState.GetKnownSkillLevelTyped(skillId);
        return knownSkillLevel > 0
            ? knownSkillLevel
            : unitState.known_active_skill_ids.Contains(skillId)
                ? 1
                : 0;
    }

    private static void SortCoordsInPlace(List<Vector2I> coords)
    {
        coords.Sort(
            (left, right) =>
            {
                int yComparison = left.Y.CompareTo(right.Y);
                return yComparison != 0 ? yComparison : left.X.CompareTo(right.X);
            }
        );
    }

    private static AiActionTrace BeginActionTrace(
        BattleAiChargePathAoeActionSpec action,
        BattleAiContext context,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        Dictionary<string, object> traceMetadata =
            context != null ? context.MergeCurrentActionMetadataTyped(metadata) : CloneMetadata(metadata);
        StringName scoreBucketId = ReadMetadataStringName(
            traceMetadata,
            "score_bucket_id",
            action?.ScoreBucketId ?? EmptyStringName
        );
        return EnemyAiActionHelper.BeginActionTrace(
            action?.ActionId ?? EmptyStringName,
            scoreBucketId,
            context,
            traceMetadata
        );
    }

    private static void TraceCountIncrement(
        AiActionTrace actionTrace,
        string key,
        int amount = 1
    ) => EnemyAiActionHelper.TraceCountIncrement(actionTrace, key, amount);

    private static void TraceAddBlockReason(AiActionTrace actionTrace, string reasonKey) =>
        EnemyAiActionHelper.TraceAddBlockReason(actionTrace, reasonKey);

    private static void TraceOfferCandidate(
        AiActionTrace actionTrace,
        AiCandidateSummary candidateSummary
    ) => EnemyAiActionHelper.TraceOfferCandidate(actionTrace, candidateSummary, 5);

    private static StringName FinalizeActionTrace(
        BattleAiContext context,
        AiActionTrace actionTrace,
        BattleAiDecision bestDecision = null
    ) => EnemyAiActionHelper.FinalizeActionTrace(context, actionTrace, bestDecision);

    private static Dictionary<string, object> CloneMetadata(
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

    private static StringName ReadMetadataStringName(
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
            return fallback;
        }
        return value switch
        {
            StringName stringName => stringName,
            string text when !string.IsNullOrEmpty(text) => new StringName(text),
            Variant variant when variant.VariantType == Variant.Type.StringName =>
                variant.AsStringName(),
            Variant variant when variant.VariantType == Variant.Type.String =>
                new StringName(variant.AsString()),
            _ => fallback,
        };
    }

    private static string ReadMetadataString(
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

    private static int ScoreTotal(BattleAiScoreInput scoreInput) => scoreInput?.total_score ?? 0;
}
