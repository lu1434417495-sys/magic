using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleAiRandomChainSkillEvaluator
{
    private static readonly StringName EmptyStringName = "";
    private static readonly StringName ActionKindRandomChainSkill = "random_chain_skill";
    private static readonly StringName TargetSelectionModeRandomChain = "random_chain";
    private static readonly StringName SelectionPolicyRandomFromLivingPool =
        "random_from_living_pool";
    private static readonly StringName PoolRefreshPolicyBeforeEachAttempt =
        "before_each_attempt";
    private static readonly StringName ScoreEstimatePolicyExpectedValue = "expected_value";

    private readonly BattleAiTypedActionHelper _helper = new();
    private readonly BattleAiDecisionEngine _scoreOrdering = new();

    internal BattleAiDecision Evaluate(UseRandomChainSkillAction action, BattleAiContext context)
    {
        return Evaluate(BattleAiRandomChainSkillActionSpec.FromAction(action), context);
    }

    internal BattleAiDecision Evaluate(
        BattleAiRandomChainSkillActionSpec action,
        BattleAiContext context
    )
    {
        if (action == null || context == null)
            return null;
        if (!HasExplicitDistanceContract(action))
            return null;

        BattleUnitState actor = context.unit_state;
        if (actor == null || context.state == null)
            return null;

        AiActionTrace actionTrace = BeginActionTrace(
            action,
            context,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action_kind"] = ActionKindRandomChainSkill.ToString(),
                ["target_selection_mode"] = TargetSelectionModeRandomChain.ToString(),
                ["target_selector"] = action.TargetSelector.ToString(),
                ["distance_reference"] = action.DistanceReference.ToString(),
                ["desired_min_distance"] = action.DesiredMinDistance,
                ["desired_max_distance"] = action.DesiredMaxDistance,
                ["selection_policy"] = SelectionPolicyRandomFromLivingPool.ToString(),
                ["pool_refresh_policy"] = PoolRefreshPolicyBeforeEachAttempt.ToString(),
                ["score_estimate_policy"] = ScoreEstimatePolicyExpectedValue.ToString(),
                ["minimum_candidate_count"] = action.MinimumCandidateCount,
            }
        );

        BattleAiDecision bestDecision = null;
        BattleAiScoreInput bestScoreInput = null;
        BattleAiDecision fallbackDecision = null;
        foreach (StringName skillId in _helper.ResolveKnownSkillIds(context, action.SkillIds))
        {
            TraceCountIncrement(actionTrace, "skill_considered_count", 1);
            SkillDefinition skillDefinition = _helper.GetSkillDefinition(context, skillId);
            if (!IsRandomChainSkill(skillDefinition))
            {
                TraceAddBlockReason(
                    actionTrace,
                    skillDefinition == null
                        ? "missing_skill_definition"
                        : "non_random_chain_skill"
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

            foreach (CombatCastVariantDefinition castVariant in GetRandomChainCastVariants(
                context,
                skillDefinition
            ))
            {
                TraceCountIncrement(actionTrace, "evaluation_count", 1);
                BattleCommand command = BuildRandomChainSkillCommand(context, skillId, castVariant);
                BattlePreview preview = BuildFastRandomChainSkillPreview(
                    context,
                    skillDefinition,
                    command
                );
                if (preview?.allowed != true)
                {
                    TraceCountIncrement(actionTrace, "preview_reject_count", 1);
                    continue;
                }

                List<BattleUnitState> candidateUnits = ResolveCandidateUnits(
                    context,
                    action,
                    preview,
                    skillDefinition
                );
                if (candidateUnits.Count == 0)
                {
                    TraceAddBlockReason(actionTrace, "no_random_chain_candidates");
                    continue;
                }
                if (candidateUnits.Count < Mathf.Max(action.MinimumCandidateCount, 1))
                {
                    TraceAddBlockReason(actionTrace, "minimum_random_chain_candidate_count");
                    continue;
                }

                string optionLabel = EnemyAiActionHelper.FormatSkillVariantLabel(
                    skillDefinition,
                    castVariant
                );
                RandomChainScoreMetadata scoreMetadata = BuildRandomChainScoreMetadata(
                    context,
                    action,
                    candidateUnits,
                    skillDefinition,
                    optionLabel
                );
                scoreMetadata.ApplyToTrace(actionTrace);
                BattleAiScoreInput scoreInput = BuildSkillScoreInput(
                    action,
                    context,
                    skillDefinition,
                    command,
                    preview,
                    CollectRandomChainEffectDefinitions(skillDefinition, castVariant),
                    scoreMetadata.ToScoreMetadata()
                );

                if (scoreInput == null)
                {
                    fallbackDecision ??= EnemyAiActionHelper.CreateDecision(
                        action.ActionId,
                        action.ScoreBucketId,
                        command,
                        $"{actor.display_name} 准备发动 {skillDefinition.DisplayName}，候选池 {scoreMetadata.CandidatePoolUnitIds.Count} 个单位。"
                    );
                    TraceOfferCandidate(
                        actionTrace,
                        EnemyAiActionHelper.BuildCandidateSummary(
                            optionLabel,
                            command,
                            null,
                            scoreMetadata.ToCandidateSummaryMetadata(skillId)
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
                        scoreMetadata.ToCandidateSummaryMetadata(skillId)
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
                    $"{actor.display_name} 准备发动 {skillDefinition.DisplayName}，候选池 {scoreMetadata.CandidatePoolUnitIds.Count} 个单位（评分 {scoreInput.total_score}）。"
                );
            }
        }

        BattleAiDecision resolvedDecision = bestDecision ?? fallbackDecision;
        EnemyAiActionHelper.FinalizeActionTrace(context, actionTrace, resolvedDecision);
        return resolvedDecision;
    }

    private static bool HasExplicitDistanceContract(BattleAiRandomChainSkillActionSpec action)
    {
        return action.DesiredMinDistance >= 0
            && action.DesiredMaxDistance >= action.DesiredMinDistance
            && (
                action.DistanceReferenceKind == EnemyAiDistanceReference.CandidatePool
                || action.DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline
            );
    }

    private static bool IsRandomChainSkill(SkillDefinition skillDefinition) =>
        skillDefinition?.CombatProfile != null
        && skillDefinition.CombatProfile.TargetModeKind == BattleTargetMode.Unit
        && skillDefinition.CombatProfile.TargetSelectionModeKind
            == BattleTargetSelectionMode.RandomChain;

    private static List<CombatCastVariantDefinition> GetRandomChainCastVariants(
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
            context?.skill_catalog?.GetEffectiveCombatDefinition(
                skillDefinition.SkillId,
                skillLevel
            ) ?? SkillEffectiveCombatDefinition.BuildUncached(skillDefinition, skillLevel);
        foreach (CombatCastVariantDefinition castVariant in effectiveDefinition.UnlockedCastVariants)
        {
            if (castVariant != null)
                result.Add(castVariant);
        }
        return result;
    }

    private static BattleCommand BuildRandomChainSkillCommand(
        BattleAiContext context,
        StringName skillId,
        CombatCastVariantDefinition castVariant
    )
    {
        if (context?.unit_state == null)
            return null;
        return new BattleCommand
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = context.unit_state.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
            skill_variant_id = castVariant?.VariantId ?? EmptyStringName,
        };
    }

    private static BattlePreview BuildFastRandomChainSkillPreview(
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command
    )
    {
        var preview = new BattlePreview();
        BattleUnitState actor = context?.unit_state;
        BattleState state = context?.state;
        if (
            actor == null
            || state == null
            || command == null
            || skillDefinition?.CombatProfile == null
        )
        {
            return preview;
        }

        CombatSkillDefinition combatProfile = skillDefinition.CombatProfile;
        foreach (BattleUnitState candidate in state.GetUnitsTyped())
        {
            if (
                candidate == null
                || candidate == actor
                || !candidate.is_alive
                || !MatchesTargetFilter(context, candidate, combatProfile.TargetTeamFilter)
                || !IsFastUnitSkillTargetInRange(context, actor, candidate, skillDefinition)
            )
            {
                continue;
            }
            preview.AddRandomChainCandidateUnitId(candidate.unit_id);
        }
        preview.allowed = preview.RandomChainCandidateUnitIdsTyped.Count > 0;
        return preview;
    }

    private List<BattleUnitState> ResolveCandidateUnits(
        BattleAiContext context,
        BattleAiRandomChainSkillActionSpec action,
        BattlePreview preview,
        SkillDefinition skillDefinition
    )
    {
        var candidateIds = new HashSet<StringName>();
        if (preview != null)
        {
            foreach (StringName rawUnitId in preview.RandomChainCandidateUnitIdsTyped)
            {
                StringName unitId = ProgressionDataUtils.to_string_name(rawUnitId);
                if (unitId != "")
                    candidateIds.Add(unitId);
            }
        }
        if (candidateIds.Count == 0)
            return new List<BattleUnitState>();

        List<BattleUnitState> sortedUnits = _helper.SortTargetUnits(
            context,
            skillDefinition.CombatProfile.TargetTeamFilter,
            action.TargetSelector
        );
        var result = new List<BattleUnitState>();
        foreach (BattleUnitState unit in sortedUnits)
        {
            if (unit != null && candidateIds.Contains(unit.unit_id))
                result.Add(unit);
        }
        return result;
    }

    private static List<StringName> CandidateUnitIds(IEnumerable<BattleUnitState> candidates)
    {
        var result = new List<StringName>();
        foreach (BattleUnitState candidate in candidates ?? Array.Empty<BattleUnitState>())
        {
            if (candidate != null)
                result.Add(candidate.unit_id);
        }
        return result;
    }

    private static List<CombatEffectDefinition> CollectRandomChainEffectDefinitions(
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

    private RandomChainScoreMetadata BuildRandomChainScoreMetadata(
        BattleAiContext context,
        BattleAiRandomChainSkillActionSpec action,
        IReadOnlyList<BattleUnitState> candidates,
        SkillDefinition skillDefinition,
        string actionLabel
    )
    {
        RandomChainDistanceContract distanceContract = ResolveRandomChainDistanceContract(
            context,
            action,
            skillDefinition
        );
        var metadata = new RandomChainScoreMetadata
        {
            ActionLabel = actionLabel ?? "",
            CandidatePoolUnitIds = CandidateUnitIds(candidates),
            DesiredMinDistance = distanceContract.DesiredMinDistance,
            DesiredMaxDistance = distanceContract.DesiredMaxDistance,
            ConfiguredDesiredMinDistance = distanceContract.ConfiguredDesiredMinDistance,
            ConfiguredDesiredMaxDistance = distanceContract.ConfiguredDesiredMaxDistance,
            EffectiveAttackRange = distanceContract.EffectiveAttackRange,
            MaxHitsPerTarget = Mathf.Max(
                skillDefinition?.CombatProfile?.MaxHitsPerTarget ?? 1,
                1
            ),
        };
        metadata.MaxAttemptCount = Mathf.Max(
            metadata.CandidatePoolUnitIds.Count * metadata.MaxHitsPerTarget,
            1
        );
        if (action.DistanceReferenceKind == EnemyAiDistanceReference.CandidatePool)
        {
            BattleUnitState primaryCandidate = candidates.Count > 0 ? candidates[0] : null;
            if (primaryCandidate != null)
                metadata.PositionTargetUnitId = primaryCandidate.unit_id;
            else
                metadata.PositionObjectiveKind = "none";
        }
        else if (action.DistanceReferenceKind == EnemyAiDistanceReference.EnemyFrontline)
        {
            BattleUnitState frontline = ResolveEnemyFrontlineUnit(context);
            if (frontline != null)
                metadata.PositionTargetUnitId = frontline.unit_id;
            else
                metadata.PositionObjectiveKind = "none";
        }
        else
        {
            metadata.PositionObjectiveKind = "none";
        }
        return metadata;
    }

    private RandomChainDistanceContract ResolveRandomChainDistanceContract(
        BattleAiContext context,
        BattleAiRandomChainSkillActionSpec action,
        SkillDefinition skillDefinition
    )
    {
        int configuredMinDistance = action.DesiredMinDistance;
        int configuredMaxDistance = action.DesiredMaxDistance;
        int effectiveAttackRange = ResolveEffectiveAttackRange(context, skillDefinition);
        int resolvedMaxDistance =
            effectiveAttackRange >= 0 ? effectiveAttackRange : configuredMaxDistance;
        int resolvedMinDistance = configuredMinDistance;
        if (resolvedMaxDistance >= 0 && resolvedMinDistance > resolvedMaxDistance)
            resolvedMinDistance = resolvedMaxDistance;
        return new RandomChainDistanceContract
        {
            DesiredMinDistance = resolvedMinDistance,
            DesiredMaxDistance = Mathf.Max(resolvedMaxDistance, resolvedMinDistance),
            ConfiguredDesiredMinDistance = configuredMinDistance,
            ConfiguredDesiredMaxDistance = configuredMaxDistance,
            EffectiveAttackRange = effectiveAttackRange,
        };
    }

    private BattleUnitState ResolveEnemyFrontlineUnit(BattleAiContext context)
    {
        List<BattleUnitState> targets = _helper.SortTargetUnits(
            context,
            "enemy",
            "nearest_enemy"
        );
        return targets.Count > 0 ? targets[0] : null;
    }

    private static int ResolveEffectiveAttackRange(
        BattleAiContext context,
        SkillDefinition skillDefinition
    )
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
        if (
            context?.grid_service == null
            || actor == null
            || targetUnit == null
            || skillDefinition == null
        )
        {
            return false;
        }
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

    private static BattleAiScoreInput BuildSkillScoreInput(
        BattleAiRandomChainSkillActionSpec action,
        BattleAiContext context,
        SkillDefinition skillDefinition,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        IReadOnlyDictionary<string, object> metadata
    )
    {
        if (action == null || context == null || skillDefinition == null)
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
                : BattleAiActionIntent.InferForSkill(skillDefinition, effectDefinitions);
        scoringMetadata["action_intent"] = ReadTraceStringName(
            scoringMetadata,
            "action_intent",
            defaultActionIntent
        );
        scoringMetadata["action_label"] = ReadTraceString(
            scoringMetadata,
            "action_label",
            !string.IsNullOrEmpty(skillDefinition.DisplayName)
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

    private static AiActionTrace BeginActionTrace(
        BattleAiRandomChainSkillActionSpec action,
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

    private sealed class RandomChainDistanceContract
    {
        public int DesiredMinDistance = -1;
        public int DesiredMaxDistance = -1;
        public int ConfiguredDesiredMinDistance = -1;
        public int ConfiguredDesiredMaxDistance = -1;
        public int EffectiveAttackRange = -1;
    }

    private sealed class RandomChainScoreMetadata
    {
        public string ActionLabel = "";
        public List<StringName> CandidatePoolUnitIds = new();
        public int DesiredMinDistance = -1;
        public int DesiredMaxDistance = -1;
        public int ConfiguredDesiredMinDistance = -1;
        public int ConfiguredDesiredMaxDistance = -1;
        public int EffectiveAttackRange = -1;
        public StringName PositionTargetUnitId = "";
        public StringName PositionObjectiveKind = "";
        public int MaxHitsPerTarget = 1;
        public int MaxAttemptCount = 1;

        public Dictionary<string, object> ToScoreMetadata()
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action_kind"] = ActionKindRandomChainSkill,
                ["target_selection_mode"] = TargetSelectionModeRandomChain,
                ["action_label"] = ActionLabel ?? "",
                ["candidate_pool_unit_ids"] = new List<StringName>(CandidatePoolUnitIds),
                ["candidate_pool_count"] = CandidatePoolUnitIds.Count,
                ["random_chain_max_hits_per_target"] = MaxHitsPerTarget,
                ["random_chain_max_attempt_count"] = MaxAttemptCount,
                ["random_chain_selection_policy"] = SelectionPolicyRandomFromLivingPool,
                ["random_chain_pool_refresh_policy"] = PoolRefreshPolicyBeforeEachAttempt,
                ["random_chain_score_estimate_policy"] = ScoreEstimatePolicyExpectedValue,
                ["desired_min_distance"] = DesiredMinDistance,
                ["desired_max_distance"] = DesiredMaxDistance,
                ["configured_desired_min_distance"] = ConfiguredDesiredMinDistance,
                ["configured_desired_max_distance"] = ConfiguredDesiredMaxDistance,
                ["effective_attack_range"] = EffectiveAttackRange,
            };
            if (PositionTargetUnitId != "")
                result["position_target_unit_id"] = PositionTargetUnitId;
            if (PositionObjectiveKind != "")
                result["position_objective_kind"] = PositionObjectiveKind;
            return result;
        }

        public Dictionary<string, object> ToCandidateSummaryMetadata(StringName skillId)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["skill_id"] = skillId.ToString(),
                ["candidate_pool_count"] = CandidatePoolUnitIds.Count,
                ["candidate_pool_unit_ids"] = StringifyUnitIdList(CandidatePoolUnitIds),
            };
        }

        public void ApplyToTrace(AiActionTrace actionTrace)
        {
            if (actionTrace == null || actionTrace.IsEmpty())
                return;
            actionTrace.Metadata["candidate_pool_count"] = CandidatePoolUnitIds.Count;
            actionTrace.Metadata["candidate_pool_unit_ids"] = StringifyUnitIdList(
                CandidatePoolUnitIds
            );
            actionTrace.Metadata["max_hits_per_target"] = MaxHitsPerTarget;
            actionTrace.Metadata["max_attempt_count"] = MaxAttemptCount;
        }

        private static List<string> StringifyUnitIdList(IEnumerable<StringName> ids)
        {
            var result = new List<string>();
            foreach (StringName id in ids ?? Array.Empty<StringName>())
            {
                result.Add(id.ToString());
            }
            return result;
        }
    }
}
