using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiChargeActionEvaluator
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName ActionKindCharge = "charge";
    private static readonly Vector2I[] ChargeDirections =
    {
        Vector2I.Left,
        Vector2I.Right,
        Vector2I.Up,
        Vector2I.Down,
    };

    private readonly BattleAiTypedActionHelper _helper = new();
    private readonly BattleAiDecisionEngine _scoreOrdering = new();

    private readonly struct ChargeTargetInfo
    {
        public readonly bool Valid;
        public readonly int Distance;
        public readonly Vector2I Direction;
        public readonly Vector2I PredictedAnchor;

        public ChargeTargetInfo(
            bool valid,
            int distance,
            Vector2I direction,
            Vector2I predictedAnchor
        )
        {
            Valid = valid;
            Distance = distance;
            Direction = direction;
            PredictedAnchor = predictedAnchor;
        }
    }

    private readonly struct ChargeDistanceBreakpoint
    {
        public readonly int Level;
        public readonly int Distance;

        public ChargeDistanceBreakpoint(int level, int distance)
        {
            Level = level;
            Distance = distance;
        }
    }

    internal BattleAiDecision Evaluate(UseChargeAction action, BattleAiContext context)
    {
        return Evaluate(BattleAiChargeActionSpec.FromAction(action), context);
    }

    internal BattleAiDecision Evaluate(BattleAiChargeActionSpec action, BattleAiContext context)
    {
        if (action == null || context?.unit_state == null)
            return null;

        AiActionTrace actionTrace = BeginActionTrace(
            action,
            context,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action_kind"] = ActionKindCharge.ToString(),
                ["target_selector"] = action.TargetSelector.ToString(),
            }
        );

        SkillDefinition skillDefinition = _helper.GetSkillDefinition(context, action.SkillId);
        if (
            skillDefinition?.CombatProfile == null
            || skillDefinition.CombatProfile.TargetModeKind != BattleTargetMode.Ground
        )
        {
            TraceAddBlockReason(actionTrace, "invalid_charge_skill");
            FinalizeActionTrace(context, actionTrace);
            return null;
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
            FinalizeActionTrace(context, actionTrace);
            return null;
        }

        AiTraceRecorder.Enter("charge:sort_targets");
        List<BattleUnitState> targets = _helper.SortTargetUnits(
            context,
            "enemy",
            action.TargetSelector
        );
        AiTraceRecorder.Exit("charge:sort_targets");
        if (targets.Count == 0)
        {
            TraceAddBlockReason(actionTrace, "no_valid_targets");
            FinalizeActionTrace(context, actionTrace);
            return null;
        }

        BattleUnitState focusTarget = targets[0];
        BattleUnitState actor = context.unit_state;
        int focusTargetDistance = DistanceBetweenUnits(context, actor, focusTarget);
        actionTrace.Metadata["focus_target_distance"] = focusTargetDistance;
        actionTrace.Metadata["minimum_charge_move_distance"] =
            action.MinimumChargeMoveDistance;
        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        int bestFallbackScore = -999999;
        var chargeInfoCache = new Dictionary<Vector2I, ChargeTargetInfo>();
        var focusDistanceByAnchor = new Dictionary<Vector2I, int>();

        ChargeTargetInfo ResolveChargeInfo(Vector2I targetCoord)
        {
            if (chargeInfoCache.TryGetValue(targetCoord, out ChargeTargetInfo cachedInfo))
                return cachedInfo;
            ChargeTargetInfo resolvedInfo = ResolveChargeTargetInfo(actor, targetCoord);
            chargeInfoCache[targetCoord] = resolvedInfo;
            return resolvedInfo;
        }

        int DistanceFromAnchorToFocus(Vector2I anchorCoord)
        {
            if (focusDistanceByAnchor.TryGetValue(anchorCoord, out int cachedDistance))
                return cachedDistance;
            int distance = DistanceFromAnchorToUnit(context, actor, anchorCoord, focusTarget);
            focusDistanceByAnchor[anchorCoord] = distance;
            return distance;
        }

        AiTraceRecorder.Enter("charge:get_ground_options");
        List<CombatCastVariantDefinition> groundOptions = GetGroundOptionDefinitions(
            context,
            skillDefinition
        );
        AiTraceRecorder.Exit("charge:get_ground_options");
        foreach (CombatCastVariantDefinition castVariant in groundOptions)
        {
            if (castVariant == null || !IsChargeOption(castVariant))
                continue;

            string variantLabel = EnemyAiActionHelper.FormatSkillVariantLabel(
                skillDefinition,
                castVariant
            );
            AiTraceRecorder.Enter("charge:evaluate_variant");
            foreach (Vector2I targetCoord in EnumerateChargeTargetCoords(
                context,
                actor,
                castVariant
            ))
            {
                TraceCountIncrement(actionTrace, "evaluation_count", 1);
                ChargeTargetInfo chargeInfo = ResolveChargeInfo(targetCoord);
                if (!chargeInfo.Valid)
                    continue;

                int predictedDistance = DistanceFromAnchorToFocus(chargeInfo.PredictedAnchor);
                if (predictedDistance >= focusTargetDistance)
                {
                    TraceAddBlockReason(actionTrace, "charge_does_not_close_focus_target");
                    continue;
                }

                string shortBlock = ResolveShortChargeBlockReason(
                    action,
                    context,
                    chargeInfo.PredictedAnchor,
                    chargeInfo.Distance,
                    focusTargetDistance
                );
                if (shortBlock.Length > 0)
                {
                    TraceAddBlockReason(actionTrace, shortBlock);
                    continue;
                }

                BattleCommand command = BuildGroundSkillCommand(
                    context,
                    action.SkillId,
                    castVariant.VariantId,
                    new[] { targetCoord }
                );
                AiTraceRecorder.Enter("charge:formal_preview");
                BattlePreview preview = context.PreviewCommand(command);
                AiTraceRecorder.Exit("charge:formal_preview");
                preview ??= BuildFastChargePreview(command, chargeInfo, targetCoord);
                if (preview?.allowed != true)
                {
                    TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                    continue;
                }

                Vector2I resolvedAnchor = preview.resolved_anchor_coord;
                if (resolvedAnchor == new Vector2I(-1, -1))
                    resolvedAnchor = actor.coord;
                int resolvedDistance = DistanceFromAnchorToFocus(resolvedAnchor);
                int resolvedMoveDistance =
                    context.grid_service?.GetDistance(actor.coord, resolvedAnchor) ?? 0;
                string resolvedShortBlock = ResolveShortChargeBlockReason(
                    action,
                    context,
                    resolvedAnchor,
                    resolvedMoveDistance,
                    focusTargetDistance
                );
                if (resolvedShortBlock.Length > 0)
                {
                    TraceAddBlockReason(actionTrace, resolvedShortBlock);
                    continue;
                }

                AiTraceRecorder.Enter("charge:formal_score_input");
                BattleAiScoreInput scoreInput = BuildChargeScoreInput(
                    action,
                    context,
                    skillDefinition,
                    command,
                    preview,
                    castVariant,
                    focusTarget,
                    resolvedAnchor,
                    resolvedMoveDistance,
                    variantLabel
                );
                AiTraceRecorder.Exit("charge:formal_score_input");
                TraceOfferCandidate(
                    actionTrace,
                    EnemyAiActionHelper.BuildCandidateSummary(
                        $"{variantLabel}->{focusTarget.display_name}",
                        command,
                        scoreInput,
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["resolved_anchor_coord"] = resolvedAnchor,
                            ["resolved_distance"] = resolvedDistance,
                            ["resolved_move_distance"] = resolvedMoveDistance,
                        }
                    )
                );

                if (scoreInput != null)
                {
                    if (!_scoreOrdering.IsBetterScoreInput(scoreInput, bestScoreInput))
                        continue;
                    bestScoreInput = scoreInput;
                    bestDecision = EnemyAiActionHelper.CreateScoredDecision(
                        action.ActionId,
                        action.ScoreBucketId,
                        command,
                        scoreInput,
                        $"{actor.display_name} 准备用冲锋逼近 {focusTarget.display_name}（评分 {ScoreTotal(scoreInput)}）。"
                    );
                    continue;
                }

                int movedDistance =
                    context.grid_service?.GetDistance(actor.coord, resolvedAnchor) ?? 0;
                int fallbackScore = 1000 - resolvedDistance * 100 + movedDistance;
                if (fallbackScore <= bestFallbackScore)
                    continue;
                bestFallbackScore = fallbackScore;
                bestDecision = EnemyAiActionHelper.CreateDecision(
                    action.ActionId,
                    action.ScoreBucketId,
                    command,
                    $"{actor.display_name} 准备用冲锋逼近 {focusTarget.display_name}。"
                );
            }
            AiTraceRecorder.Exit("charge:evaluate_variant");
        }

        FinalizeActionTrace(context, actionTrace, bestDecision);
        return bestDecision;
    }

    private static IEnumerable<Vector2I> EnumerateChargeTargetCoords(
        BattleAiContext context,
        BattleUnitState unitState,
        CombatCastVariantDefinition castVariant
    )
    {
        if (context?.grid_service == null || context.state == null || unitState == null || castVariant == null)
            yield break;

        unitState.RefreshFootprint();
        int maxDistance = ResolveChargeMaxDistance(unitState, castVariant);
        if (maxDistance <= 0)
            yield break;

        int minX = unitState.coord.X;
        int maxX = unitState.coord.X + unitState.footprint_size.X - 1;
        int minY = unitState.coord.Y;
        int maxY = unitState.coord.Y + unitState.footprint_size.Y - 1;
        int anchorX = unitState.coord.X;
        int anchorY = unitState.coord.Y;

        foreach (Vector2I direction in ChargeDirections)
        {
            for (int distance = 1; distance <= maxDistance; distance += 1)
            {
                Vector2I targetCoord = direction == Vector2I.Left
                    ? new Vector2I(minX - distance, anchorY)
                    : direction == Vector2I.Right
                        ? new Vector2I(maxX + distance, anchorY)
                        : direction == Vector2I.Up
                            ? new Vector2I(anchorX, minY - distance)
                            : new Vector2I(anchorX, maxY + distance);
                if (context.grid_service.IsInside(context.state, targetCoord))
                    yield return targetCoord;
            }
        }
    }

    private static int ResolveChargeMaxDistance(
        BattleUnitState unitState,
        CombatCastVariantDefinition castVariant
    )
    {
        CombatEffectDefinition chargeEffect = null;
        foreach (CombatEffectDefinition effectDefinition in castVariant.EffectDefinitions)
        {
            if (effectDefinition != null && effectDefinition.EffectKind == BattleEffectKind.Charge)
            {
                chargeEffect = effectDefinition;
                break;
            }
        }
        if (chargeEffect == null)
            return 0;

        int maxDistance = Math.Max(ReadInt(chargeEffect.Parameters, "base_distance", 3), 0);
        int skillLevel = GetSkillLevel(
            unitState,
            ReadStringName(chargeEffect.Parameters, "skill_id", "charge")
        );
        foreach (ChargeDistanceBreakpoint breakpoint in ReadDistanceBreakpoints(chargeEffect))
        {
            if (skillLevel >= breakpoint.Level)
                maxDistance = Math.Max(maxDistance, breakpoint.Distance);
        }
        return maxDistance;
    }

    private static List<ChargeDistanceBreakpoint> ReadDistanceBreakpoints(
        CombatEffectDefinition chargeEffect
    )
    {
        var result = new List<ChargeDistanceBreakpoint>();
        Godot.Collections.Dictionary distanceByLevel = ReadDictionary(
            chargeEffect?.Parameters,
            "distance_by_level"
        );
        foreach (Variant rawKey in distanceByLevel.Keys)
        {
            if (!TryReadLevelBreakpoint(rawKey, out int levelBreakpoint))
                continue;
            int distance = ReadInt(distanceByLevel, rawKey, -1);
            if (distance < 0)
                continue;
            result.Add(new ChargeDistanceBreakpoint(levelBreakpoint, distance));
        }
        result.Sort((left, right) => left.Level.CompareTo(right.Level));
        return result;
    }

    private static bool TryReadLevelBreakpoint(Variant rawKey, out int levelBreakpoint)
    {
        levelBreakpoint = 0;
        if (rawKey.VariantType == Variant.Type.Int)
        {
            levelBreakpoint = rawKey.AsInt32();
            return true;
        }
        if (rawKey.VariantType != Variant.Type.String && rawKey.VariantType != Variant.Type.StringName)
            return false;
        return int.TryParse(rawKey.AsString(), out levelBreakpoint);
    }

    private static ChargeTargetInfo ResolveChargeTargetInfo(BattleUnitState unitState, Vector2I targetCoord)
    {
        if (unitState == null)
            return new ChargeTargetInfo(false, 0, Vector2I.Zero, new Vector2I(-1, -1));

        unitState.RefreshFootprint();
        int minX = unitState.coord.X;
        int maxX = unitState.coord.X + unitState.footprint_size.X - 1;
        int minY = unitState.coord.Y;
        int maxY = unitState.coord.Y + unitState.footprint_size.Y - 1;
        if (targetCoord.Y >= minY && targetCoord.Y <= maxY)
        {
            if (targetCoord.X < minX)
            {
                int leftDistance = minX - targetCoord.X;
                return new ChargeTargetInfo(
                    true,
                    leftDistance,
                    Vector2I.Left,
                    unitState.coord + Vector2I.Left * leftDistance
                );
            }
            if (targetCoord.X > maxX)
            {
                int rightDistance = targetCoord.X - maxX;
                return new ChargeTargetInfo(
                    true,
                    rightDistance,
                    Vector2I.Right,
                    unitState.coord + Vector2I.Right * rightDistance
                );
            }
        }
        if (targetCoord.X >= minX && targetCoord.X <= maxX)
        {
            if (targetCoord.Y < minY)
            {
                int upDistance = minY - targetCoord.Y;
                return new ChargeTargetInfo(
                    true,
                    upDistance,
                    Vector2I.Up,
                    unitState.coord + Vector2I.Up * upDistance
                );
            }
            if (targetCoord.Y > maxY)
            {
                int downDistance = targetCoord.Y - maxY;
                return new ChargeTargetInfo(
                    true,
                    downDistance,
                    Vector2I.Down,
                    unitState.coord + Vector2I.Down * downDistance
                );
            }
        }
        return new ChargeTargetInfo(false, 0, Vector2I.Zero, new Vector2I(-1, -1));
    }

    private static BattlePreview BuildFastChargePreview(
        BattleCommand command,
        ChargeTargetInfo chargeInfo,
        Vector2I targetCoord
    )
    {
        var preview = new BattlePreview
        {
            allowed = command != null
                && chargeInfo.Valid
                && chargeInfo.PredictedAnchor != new Vector2I(-1, -1),
            resolved_anchor_coord = chargeInfo.PredictedAnchor,
            move_cost = Math.Max(chargeInfo.Distance, 0),
        };
        if (preview.allowed)
            preview.AddTargetCoord(targetCoord);
        return preview;
    }

    private static BattleAiScoreInput BuildChargeScoreInput(
        BattleAiChargeActionSpec action,
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        CombatCastVariantDefinition castVariant,
        BattleUnitState focusTarget,
        Vector2I resolvedAnchor,
        int resolvedMoveDistance,
        string variantLabel
    )
    {
        int chargeBaseScore = 20 + Math.Max(resolvedMoveDistance - 1, 0) * 8;
        return BuildSkillScoreInput(
            action,
            context,
            skillDefinition,
            command,
            preview,
            castVariant?.EffectDefinitions,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action_kind"] = "move",
                ["action_base_score"] = chargeBaseScore,
                ["position_target_unit_id"] = focusTarget?.unit_id ?? EmptyStringName,
                ["position_anchor_coord"] = resolvedAnchor,
                ["desired_min_distance"] = 1,
                ["desired_max_distance"] = 1,
                ["action_label"] = variantLabel ?? "",
            }
        );
    }

    private static string ResolveShortChargeBlockReason(
        BattleAiChargeActionSpec action,
        BattleAiContext context,
        Vector2I resolvedAnchor,
        int resolvedMoveDistance,
        int focusTargetDistance
    )
    {
        if (action.MinimumChargeMoveDistance <= 1)
            return "";
        if (focusTargetDistance <= action.MinimumChargeMoveDistance)
            return "target_distance_below_minimum_charge";
        if (resolvedMoveDistance > action.MinimumChargeMoveDistance)
            return "";
        BattleUnitState actor = context?.unit_state;
        if (actor == null || resolvedAnchor == actor.coord)
            return "";
        return "short_charge_below_minimum";
    }

    private static List<CombatCastVariantDefinition> GetGroundOptionDefinitions(
        BattleAiContext context,
        SkillDefinition skillDefinition
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

        int skillLevel = GetSkillLevel(context?.unit_state, skillDefinition.SkillId);
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
        StringName skillId = ProgressionDataUtils.to_string_name(
            skillDefinition?.SkillId ?? EmptyStringName
        );
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
        StringName skillId,
        StringName skillVariantId,
        IEnumerable<Vector2I> targetCoords
    )
    {
        if (context?.unit_state == null)
            return null;
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = context.unit_state.unit_id,
            skill_id = skillId,
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
        BattleAiChargeActionSpec action,
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IEnumerable<CombatEffectDefinition> effectDefinitions = null,
        IReadOnlyDictionary<string, object> metadata = null
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
        BattleAiChargeActionSpec action,
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

    private static int DistanceBetweenUnits(
        BattleAiContext context,
        BattleUnitState firstUnit,
        BattleUnitState secondUnit
    )
    {
        return context?.grid_service != null
            ? context.grid_service.GetDistanceBetweenUnits(firstUnit, secondUnit)
            : 999999;
    }

    private static int DistanceFromAnchorToUnit(
        BattleAiContext context,
        BattleUnitState actor,
        Vector2I anchor,
        BattleUnitState target
    )
    {
        if (context?.grid_service == null || actor == null || target == null)
            return 999999;
        BattleGridService grid = context.grid_service;
        actor.RefreshFootprint();
        target.RefreshFootprint();
        int bestDistance = 999999;
        foreach (Vector2I sourceCoord in grid.GetFootprintCoords(anchor, actor.footprint_size))
        foreach (Vector2I targetCoord in target.occupied_coords)
            bestDistance = Math.Min(bestDistance, grid.GetDistance(sourceCoord, targetCoord));
        return bestDistance;
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

    private static AiActionTrace BeginActionTrace(
        BattleAiChargeActionSpec action,
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

    private static Godot.Collections.Dictionary ReadDictionary(
        IReadOnlyDictionary<string, Variant> data,
        string key
    )
    {
        Variant value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Godot.Collections.Dictionary();
    }

    private static Godot.Collections.Dictionary ReadDictionary(
        Godot.Collections.Dictionary data,
        string key
    )
    {
        Variant value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Godot.Collections.Dictionary();
    }

    private static int ReadInt(IReadOnlyDictionary<string, Variant> data, string key, int fallback = 0)
    {
        Variant value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static int ReadInt(Godot.Collections.Dictionary data, object key, int fallback = 0)
    {
        Variant value = ReadValue(data, key);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static StringName ReadStringName(
        IReadOnlyDictionary<string, Variant> data,
        string key,
        StringName fallback = default
    )
    {
        Variant value = ReadValue(data, key);
        if (value.VariantType == Variant.Type.StringName)
            return value.AsStringName();
        if (value.VariantType == Variant.Type.String)
            return new StringName(value.AsString());
        return fallback ?? EmptyStringName;
    }

    private static Variant ReadValue(Godot.Collections.Dictionary data, object key)
    {
        if (data == null || key == null)
            return default;
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            StringName stringNameKey => stringNameKey,
            string stringKey => stringKey,
            int intKey => intKey,
            long longKey => longKey,
            _ => default,
        };
        if (variantKey.VariantType == Variant.Type.Nil)
            return default;
        return data.ContainsKey(variantKey) ? data[variantKey] : default;
    }

    private static Variant ReadValue(IReadOnlyDictionary<string, Variant> data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key))
            return default;
        return data.TryGetValue(key, out Variant value) ? value : default;
    }

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
