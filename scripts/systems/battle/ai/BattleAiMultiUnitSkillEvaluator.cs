using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiMultiUnitSkillEvaluator
{
    private static readonly StringName EmptyStringName = "";

    private readonly BattleAiTypedActionHelper _helper = new();
    private readonly BattleAiDecisionEngine _scoreOrdering = new();

    internal BattleAiDecision Evaluate(UseMultiUnitSkillAction action, BattleAiContext context)
    {
        return Evaluate(BattleAiMultiUnitSkillActionSpec.FromAction(action), context);
    }

    internal BattleAiDecision Evaluate(BattleAiMultiUnitSkillActionSpec action, BattleAiContext context)
    {
        if (action == null || context == null || !HasExplicitDistanceContract(action))
            return null;

        BattleUnitState actor = context.unit_state;
        if (actor == null || context.state == null)
            return null;

        AiActionTrace actionTrace = BeginActionTrace(
            action,
            context,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action_kind"] = "multi_unit_skill",
                ["target_selector"] = action.TargetSelector.ToString(),
                ["distance_reference"] = action.DistanceReference.ToString(),
                ["desired_min_distance"] = action.DesiredMinDistance,
                ["desired_max_distance"] = action.DesiredMaxDistance,
                ["candidate_pool_limit"] = action.CandidatePoolLimit,
                ["candidate_group_limit"] = action.CandidateGroupLimit,
            }
        );

        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        BattleAiDecision fallbackDecision = null;
        foreach (StringName skillId in _helper.ResolveKnownSkillIds(context, action.SkillIds))
        {
            TraceCountIncrement(actionTrace, "skill_considered_count", 1);
            SkillDefinition skillDefinition = _helper.GetSkillDefinition(context, skillId);
            if (!IsMultiUnitSkill(skillDefinition))
            {
                TraceAddBlockReason(
                    actionTrace,
                    skillDefinition == null ? "missing_skill_definition" : "non_multi_unit_skill"
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

            List<BattleUnitState> sortedTargets = _helper.SortTargetUnits(
                context,
                skillDefinition.CombatProfile.TargetTeamFilter,
                action.TargetSelector
            );
            if (sortedTargets.Count == 0)
            {
                TraceAddBlockReason(actionTrace, "no_valid_targets");
                continue;
            }

            foreach (CombatCastVariantDefinition castVariant in GetMultiUnitCastVariants(
                context,
                skillDefinition
            ))
            {
                if (castVariant != null && IsChargeOption(castVariant))
                    continue;

                List<List<BattleUnitState>> targetGroups = BuildTargetGroups(
                    context,
                    action,
                    skillDefinition,
                    castVariant,
                    sortedTargets
                );
                if (targetGroups.Count == 0)
                {
                    TraceAddBlockReason(actionTrace, "no_valid_target_groups");
                    continue;
                }

                foreach (List<BattleUnitState> targetGroup in targetGroups)
                {
                    TraceCountIncrement(actionTrace, "evaluation_count", 1);
                    BattleCommand command = BuildMultiUnitSkillCommand(
                        context,
                        skillId,
                        castVariant,
                        targetGroup
                    );
                    BattlePreview preview = BuildFastUnitSkillPreview(
                        context,
                        skillDefinition,
                        command
                    );
                    if (preview?.allowed != true)
                    {
                        TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                        continue;
                    }

                    Dictionary<string, object> positionMetadata = BuildPositionMetadata(
                        context,
                        action,
                        targetGroup,
                        skillDefinition
                    );
                    string optionLabel = EnemyAiActionHelper.FormatSkillVariantLabel(
                        skillDefinition,
                        castVariant
                    );
                    positionMetadata["action_label"] = optionLabel;
                    BattleAiScoreInput scoreInput = BuildSkillScoreInput(
                        action,
                        context,
                        skillDefinition,
                        command,
                        preview,
                        CollectMultiUnitEffectDefinitions(skillDefinition, castVariant),
                        positionMetadata
                    );
                    int targetCount = command.TargetUnitIdsTyped.Count;
                    Dictionary<string, object> candidateExtra =
                        new(StringComparer.Ordinal)
                        {
                            ["skill_id"] = skillId.ToString(),
                            ["target_count"] = targetCount,
                        };

                    if (scoreInput == null)
                    {
                        fallbackDecision ??= EnemyAiActionHelper.CreateDecision(
                            action.ActionId,
                            action.ScoreBucketId,
                            command,
                            $"{actor.display_name} 准备用 {skillDefinition.DisplayName} 锁定 {targetCount} 个单位。"
                        );
                        TraceOfferCandidate(
                            actionTrace,
                            EnemyAiActionHelper.BuildCandidateSummary(
                                optionLabel,
                                command,
                                null,
                                candidateExtra
                            )
                        );
                        continue;
                    }

                    TraceOfferCandidate(
                        actionTrace,
                        EnemyAiActionHelper.BuildCandidateSummary(
                            optionLabel,
                            command,
                            scoreInput,
                            candidateExtra
                        )
                    );
                    if (!_scoreOrdering.IsBetterScoreInput(scoreInput, bestScoreInput))
                        continue;

                    bestScoreInput = scoreInput;
                    bestDecision = EnemyAiActionHelper.CreateScoredDecision(
                        action.ActionId,
                        action.ScoreBucketId,
                        command,
                        scoreInput,
                        $"{actor.display_name} 准备用 {skillDefinition.DisplayName} 锁定 {targetCount} 个单位（评分 {scoreInput.total_score}）。"
                    );
                }
            }
        }

        BattleAiDecision resolvedDecision = bestDecision ?? fallbackDecision;
        EnemyAiActionHelper.FinalizeActionTrace(context, actionTrace, resolvedDecision);
        return resolvedDecision;
    }

    private static bool IsMultiUnitSkill(SkillDefinition skillDefinition) =>
        skillDefinition?.CombatProfile != null
        && skillDefinition.CombatProfile.TargetSelectionModeKind
            == BattleTargetSelectionMode.MultiUnit;

    private static bool HasExplicitDistanceContract(BattleAiMultiUnitSkillActionSpec action)
    {
        return action.DesiredMinDistance >= 0
            && action.DesiredMaxDistance >= action.DesiredMinDistance
            && (
                action.DistanceReferenceKind == EnemyAiDistanceReference.TargetUnit
                || action.DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline
            );
    }

    private static bool IsChargeOption(CombatCastVariantDefinition castVariant)
    {
        if (castVariant == null)
            return false;
        foreach (CombatEffectDefinition effect in castVariant.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>())
        {
            if (effect != null && effect.EffectKind == BattleEffectKind.Charge)
                return true;
        }
        return false;
    }

    private static List<CombatCastVariantDefinition> GetMultiUnitCastVariants(
        BattleAiContext context,
        SkillDefinition skillDefinition
    )
    {
        var result = new List<CombatCastVariantDefinition>();
        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile == null)
            return result;
        if (combatProfile.CastVariants.Count == 0)
        {
            result.Add(null);
            return result;
        }
        BattleUnitState actor = context?.unit_state;
        int skillLevel = actor != null ? GetSkillLevel(actor, skillDefinition.SkillId) : 0;
        SkillEffectiveCombatDefinition effectiveDefinition =
            context?.skill_catalog?.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
            ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        foreach (CombatCastVariantDefinition castVariant in effectiveDefinition.UnlockedCastVariants)
        {
            if (castVariant != null)
                result.Add(castVariant);
        }
        return result;
    }

    private List<List<BattleUnitState>> BuildTargetGroups(
        BattleAiContext context,
        BattleAiMultiUnitSkillActionSpec action,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<BattleUnitState> sortedTargets
    )
    {
        var groups = new List<List<BattleUnitState>>();
        List<BattleUnitState> pool = BuildCandidatePool(
            context,
            action,
            skillDefinition,
            castVariant,
            sortedTargets
        );
        if (pool.Count == 0)
            return groups;

        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        int skillLevel = GetSkillLevel(context.unit_state, skillDefinition.SkillId);
        int minCount = Mathf.Max(combatProfile.MinTargetCount, 1);
        SkillEffectiveCombatDefinition effectiveDefinition =
            context?.skill_catalog?.GetEffectiveCombatDefinition(skillDefinition.SkillId, skillLevel)
            ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        int maxCount = Mathf.Max(effectiveDefinition.MaxTargetCount, minCount);
        maxCount = Mathf.Min(maxCount, pool.Count);
        if (pool.Count < minCount)
            return groups;

        var seen = new HashSet<string>();
        for (int count = maxCount; count >= minCount; count--)
        {
            if (count == 1)
            {
                foreach (BattleUnitState target in pool)
                {
                    AppendTargetGroup(groups, seen, new List<BattleUnitState> { target });
                    if (groups.Count >= action.CandidateGroupLimit)
                        return groups;
                }
                continue;
            }
            for (int startIndex = 0; startIndex <= pool.Count - count; startIndex++)
            {
                var targetGroup = new List<BattleUnitState>();
                for (int offset = 0; offset < count; offset++)
                    targetGroup.Add(pool[startIndex + offset]);
                AppendTargetGroup(groups, seen, targetGroup);
                if (groups.Count >= action.CandidateGroupLimit)
                    return groups;
            }
        }
        return groups;
    }

    private List<BattleUnitState> BuildCandidatePool(
        BattleAiContext context,
        BattleAiMultiUnitSkillActionSpec action,
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant,
        IEnumerable<BattleUnitState> sortedTargets
    )
    {
        var pool = new List<BattleUnitState>();
        int minCount = Mathf.Max(skillDefinition.CombatProfile.MinTargetCount, 1);
        foreach (BattleUnitState target in sortedTargets ?? Array.Empty<BattleUnitState>())
        {
            if (target == null || pool.Count >= action.CandidatePoolLimit)
                break;
            if (minCount <= 1)
            {
                BattleCommand singleCommand = BuildMultiUnitSkillCommand(
                    context,
                    skillDefinition.SkillId,
                    castVariant,
                    new List<BattleUnitState> { target }
                );
                BattlePreview singlePreview = BuildFastUnitSkillPreview(
                    context,
                    skillDefinition,
                    singleCommand,
                    target
                );
                if (singlePreview?.allowed != true)
                    continue;
            }
            pool.Add(target);
        }
        return pool;
    }

    private static void AppendTargetGroup(
        List<List<BattleUnitState>> groups,
        HashSet<string> seen,
        List<BattleUnitState> targetGroup
    )
    {
        if (targetGroup.Count == 0)
            return;
        string key = TargetGroupKey(targetGroup);
        if (key.Length == 0 || !seen.Add(key))
            return;
        groups.Add(targetGroup);
    }

    private static string TargetGroupKey(IReadOnlyList<BattleUnitState> targetGroup)
    {
        var parts = new List<string>();
        foreach (BattleUnitState target in targetGroup)
        {
            if (target != null)
                parts.Add(target.unit_id.ToString());
        }
        return string.Join("|", parts);
    }

    private static BattleCommand BuildMultiUnitSkillCommand(
        BattleAiContext context,
        StringName skillId,
        CombatCastVariantDefinition castVariant,
        IReadOnlyList<BattleUnitState> targetGroup
    )
    {
        if (context?.unit_state == null)
            return null;
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = context.unit_state.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
            skill_variant_id = castVariant?.VariantId ?? EmptyStringName,
        };
        foreach (BattleUnitState target in targetGroup ?? Array.Empty<BattleUnitState>())
        {
            if (target == null)
                continue;
            command.AddTargetUnitId(target.unit_id);
            if (command.target_coord == new Vector2I(-1, -1))
                command.target_coord = target.coord;
        }
        return command;
    }

    private static BattlePreview BuildFastUnitSkillPreview(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattleUnitState targetUnit = null
    )
    {
        var preview = new BattlePreview();
        BattleUnitState actor = context?.unit_state;
        BattleState state = context?.state;
        if (actor == null || state == null || skillDefinition?.CombatProfile == null || command == null)
            return preview;

        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        var targetIds = new List<StringName>();
        AddUniqueTargetId(targetIds, targetUnit?.unit_id ?? "");
        AddUniqueTargetId(targetIds, command.target_unit_id);
        foreach (StringName id in command.TargetUnitIdsTyped)
            AddUniqueTargetId(targetIds, id);
        if (targetIds.Count == 0)
            return preview;

        bool isMultiTarget =
            combatProfile.TargetSelectionModeKind == BattleTargetSelectionMode.MultiUnit;
        if (!isMultiTarget && targetIds.Count != 1)
            return preview;

        foreach (StringName targetId in targetIds)
        {
            BattleUnitState candidate = state.TryGetUnitTyped(targetId, out BattleUnitState found)
                ? found
                : null;
            if (
                candidate == null
                || !candidate.is_alive
                || !MatchesTargetFilter(context, candidate, combatProfile.TargetTeamFilter)
                || !IsFastUnitSkillTargetInRange(context, actor, candidate, skillDefinition)
            )
            {
                return preview;
            }
            preview.AddTargetUnitId(candidate.unit_id);
            candidate.RefreshFootprint();
            foreach (Vector2I coord in candidate.occupied_coords)
            {
                if (!preview.ContainsTargetCoord(coord))
                    preview.AddTargetCoord(coord);
            }
        }

        preview.allowed = preview.TargetUnitIdsTyped.Count > 0;
        preview.resolved_anchor_coord =
            preview.TargetCoordsTyped.Count > 0
                ? preview.TargetCoordsTyped[0]
                : new Vector2I(-1, -1);
        return preview;
    }

    private Dictionary<string, object> BuildPositionMetadata(
        BattleAiContext context,
        BattleAiMultiUnitSkillActionSpec action,
        IReadOnlyList<BattleUnitState> targetGroup,
        SkillDefinition skillDefinition
    )
    {
        Dictionary<string, object> metadata = ResolveDesiredDistanceContract(
            context,
            action,
            skillDefinition
        );
        if (action.DistanceReferenceKind == EnemyAiDistanceReference.TargetUnit)
        {
            BattleUnitState primaryTarget = targetGroup.Count > 0 ? targetGroup[0] : null;
            if (primaryTarget != null)
                metadata["position_target_unit_id"] = primaryTarget.unit_id;
            else
                metadata["position_objective_kind"] = "none";
        }
        else if (action.DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline)
        {
            BattleUnitState frontline = ResolveEnemyFrontlineUnit(context);
            if (frontline != null)
                metadata["position_target_unit_id"] = frontline.unit_id;
            else
                metadata["position_objective_kind"] = "none";
        }
        else
        {
            metadata["position_objective_kind"] = "none";
        }
        return metadata;
    }

    private static Dictionary<string, object> ResolveDesiredDistanceContract(
        BattleAiContext context,
        BattleAiMultiUnitSkillActionSpec action,
        SkillDefinition skillDefinition
    )
    {
        int configuredMinDistance = action?.DesiredMinDistance ?? 0;
        int configuredMaxDistance = action?.DesiredMaxDistance ?? 0;
        int effectiveAttackRange = ResolveEffectiveAttackRange(context, skillDefinition);
        int resolvedMaxDistance =
            effectiveAttackRange >= 0 ? effectiveAttackRange : configuredMaxDistance;
        int resolvedMinDistance = configuredMinDistance;
        if (resolvedMaxDistance >= 0 && resolvedMinDistance > resolvedMaxDistance)
            resolvedMinDistance = resolvedMaxDistance;
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["desired_min_distance"] = resolvedMinDistance,
            ["desired_max_distance"] = Mathf.Max(resolvedMaxDistance, resolvedMinDistance),
            ["configured_desired_min_distance"] = configuredMinDistance,
            ["configured_desired_max_distance"] = configuredMaxDistance,
            ["effective_attack_range"] = effectiveAttackRange,
        };
    }

    private BattleUnitState ResolveEnemyFrontlineUnit(BattleAiContext context)
    {
        List<BattleUnitState> targets = _helper.SortTargetUnits(context, "enemy", "nearest_enemy");
        return targets.Count > 0 ? targets[0] : null;
    }

    private static int ResolveEffectiveAttackRange(BattleAiContext context, SkillDefinition skillDefinition)
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || skillDefinition == null)
            return -1;
        return BattleRangeService.GetEffectiveSkillDistanceContractRange(
            actor,
            skillDefinition,
            context.skill_catalog
        );
    }

    private static List<CombatEffectDefinition> CollectMultiUnitEffectDefinitions(
        SkillDefinition skillDefinition,
        CombatCastVariantDefinition castVariant
    )
    {
        var result = new List<CombatEffectDefinition>();
        if (skillDefinition?.CombatProfile != null)
        {
            foreach (CombatEffectDefinition effect in skillDefinition.CombatProfile.EffectDefinitions)
            {
                if (effect != null)
                    result.Add(effect);
            }
        }
        if (castVariant != null)
        {
            foreach (CombatEffectDefinition effect in castVariant.EffectDefinitions)
            {
                if (effect != null)
                    result.Add(effect);
            }
        }
        return result;
    }

    private static BattleAiScoreInput BuildSkillScoreInput(
        BattleAiMultiUnitSkillActionSpec action,
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
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
        StringName defaultActionIntent =
            BattleAiActionIntent.IsValid(action.ActionIntent)
            && action.ActionIntent != BattleAiActionIntent.Positioning
                ? action.ActionIntent
                : BattleAiActionIntent.InferForSkill(skillDefinition, effectDefinitions);
        scoringMetadata["action_intent"] = ReadMetadataStringName(
            scoringMetadata,
            "action_intent",
            defaultActionIntent
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

    private static bool MatchesTargetFilter(
        BattleAiContext context,
        BattleUnitState targetUnit,
        StringName targetFilter
    )
    {
        BattleUnitState actor = context?.unit_state;
        if (actor == null || targetUnit == null)
            return false;
        return BattleTargetTeamRules.IsUnitValidForFilter(
            actor,
            targetUnit,
            targetFilter,
            new BattleTargetTeamRules.TargetFilterOptions(
                MadnessTargetAnyTeam: actor.ai_blackboard?.madness_target_any_team == true
            )
        );
    }

    private static bool IsFastUnitSkillTargetInRange(
        BattleAiContext context,
        BattleUnitState actor,
        BattleUnitState targetUnit,
        SkillDefinition skillDefinition
    )
    {
        if (context?.grid_service == null || actor == null || targetUnit == null || skillDefinition == null)
            return false;
        int effectiveRange = BattleRangeService.GetEffectiveSkillRange(
            actor,
            skillDefinition,
            context.skill_catalog
        );
        return context.grid_service.GetDistanceBetweenUnits(actor, targetUnit) <= effectiveRange;
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

    private static void AddUniqueTargetId(List<StringName> targetIds, StringName unitId)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(unitId);
        if (targetIds == null || normalized == "" || targetIds.Contains(normalized))
            return;
        targetIds.Add(normalized);
    }

    private static AiActionTrace BeginActionTrace(
        BattleAiMultiUnitSkillActionSpec action,
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
}
