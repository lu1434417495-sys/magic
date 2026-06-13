using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
public sealed partial class BattleAiScoreService
{
    private static readonly StringName BonusConditionTargetLowHp = "target_low_hp";
    private static readonly StringName PathStepAoeEffectType = "path_step_aoe";
    private static readonly StringName ChainDamageEffectType = "chain_damage";
    private static readonly StringName MeteorSwarmProfileId = "meteor_swarm";
    private static readonly StringName FortuneMarkTargetStatId = "fortune_mark_target";
    private static readonly StringName BossTargetStatId = "boss_target";

    private const int ThreatMultiplierBasisPointsDenominator = 10000;
    private const int MinRangedThreatRange = 3;
    private const int FriendlyLethalMinProbabilityThreshold = 15;

    private BattleAiScoreProfile _scoreProfile = new();
    private BattleDamageResolver _damageResolver;
    private readonly Dictionary<StringName, ThreatProfile> _threatProfileCache = new();
    private readonly Dictionary<StringName, int> _targetRoleThreatMultiplierCache = new();
    private readonly Dictionary<ThreatProjectionCacheKey, ThreatProjection> _threatProjectionCache =
        new();
    private readonly Dictionary<StringName, List<BattleUnitState>> _hostileUnitsByActorFactionCache =
        new();
    private readonly Dictionary<TargetEffectMetricsCacheKey, TargetEffectMetrics> _targetEffectMetricsCache =
        new();
    private readonly Dictionary<AnchorDistanceCacheKey, int> _anchorDistanceCache = new();
    private bool _decisionScopeActive;

    private readonly record struct ThreatProjectionCacheKey(
        StringName ActorUnitId,
        Vector2I ActorAnchorCoord,
        long SuppressedThreatSignature
    );

    private readonly record struct TargetEffectMetricsCacheKey(
        StringName SkillId,
        StringName SourceUnitId,
        StringName TargetUnitId,
        int HitCount,
        int EffectSignature,
        int SourceSignature,
        int TargetSignature
    );

    private readonly record struct AnchorDistanceCacheKey(
        StringName ActorUnitId,
        Vector2I AnchorCoord,
        Vector2I ActorFootprintSize,
        StringName TargetUnitId
    );

    private sealed class ScoreBuildMetadata
    {
        public StringName ActionKind = "skill";
        public string ActionLabel = "";
        public StringName ScoreBucketId = "";
        public BattleAiScoreRuntimeMetadata RuntimeActionMetadata = new();
        public int MoveCost;
        public int TargetCountWeight;
        public bool HasActionBaseScore;
        public int ActionBaseScore;
        public ScoreRandomChainMetadata RandomChain = new();
        public ScorePositionMetadata Position = new();
        public ScorePathStepAoeMetadata PathStepAoe = new();

        public static ScoreBuildMetadata FromMetadata(
            IReadOnlyDictionary<string, object> source,
            StringName defaultActionKind,
            string defaultActionLabel,
            StringName defaultScoreBucketId,
            int defaultMoveCost
        )
        {
            bool hasActionBaseScore = HasMetadataKey(source, "action_base_score");
            return new ScoreBuildMetadata
            {
                ActionKind = MetadataStringName(source, "action_kind", defaultActionKind),
                ActionLabel = MetadataString(source, "action_label", defaultActionLabel ?? ""),
                ScoreBucketId = MetadataStringName(source, "score_bucket_id", defaultScoreBucketId),
                RuntimeActionMetadata = BattleAiScoreRuntimeMetadata.FromMetadata(source),
                MoveCost = MetadataInt(source, "move_cost", defaultMoveCost),
                TargetCountWeight = MetadataInt(source, "target_count_weight", 0),
                HasActionBaseScore = hasActionBaseScore,
                ActionBaseScore = hasActionBaseScore ? MetadataInt(source, "action_base_score", 0) : 0,
                RandomChain = ScoreRandomChainMetadata.FromMetadata(source),
                Position = ScorePositionMetadata.FromMetadata(source),
                PathStepAoe = ScorePathStepAoeMetadata.FromMetadata(source),
            };
        }
    }

    private sealed class ScoreRandomChainMetadata
    {
        public List<StringName> CandidatePoolUnitIds = new();
        public int? MaxHitsPerTarget;
        public int? MaxAttemptCount;
        public StringName SelectionPolicy = "random_from_living_pool";
        public StringName PoolRefreshPolicy = "before_each_attempt";
        public StringName ScoreEstimatePolicy = "expected_value";

        public static ScoreRandomChainMetadata FromMetadata(IReadOnlyDictionary<string, object> source)
        {
            var result = new ScoreRandomChainMetadata
            {
                CandidatePoolUnitIds = ReadMetadataStringNameList(source, "candidate_pool_unit_ids"),
                SelectionPolicy = MetadataStringName(
                    source,
                    "random_chain_selection_policy",
                    "random_from_living_pool"
                ),
                PoolRefreshPolicy = MetadataStringName(
                    source,
                    "random_chain_pool_refresh_policy",
                    "before_each_attempt"
                ),
                ScoreEstimatePolicy = MetadataStringName(
                    source,
                    "random_chain_score_estimate_policy",
                    "expected_value"
                ),
            };
            if (HasMetadataKey(source, "random_chain_max_hits_per_target"))
            {
                result.MaxHitsPerTarget = MetadataInt(source, "random_chain_max_hits_per_target", 1);
            }
            if (HasMetadataKey(source, "random_chain_max_attempt_count"))
            {
                result.MaxAttemptCount = MetadataInt(source, "random_chain_max_attempt_count", 1);
            }
            return result;
        }
    }

    private sealed class ScorePositionMetadata
    {
        public int DesiredMinDistance = -1;
        public int DesiredMaxDistance = -1;
        public int CurrentDistance = -1;
        public int SafeDistance = -1;
        public StringName ObjectiveKind = "";
        public StringName TargetUnitId = "";
        public Vector2I AnchorCoord = new(-1, -1);

        public static ScorePositionMetadata FromMetadata(IReadOnlyDictionary<string, object> source)
        {
            int desiredMinDistance = MetadataInt(source, "desired_min_distance", -1);
            StringName targetUnitId = MetadataStringName(source, "position_target_unit_id", "");
            if (IsEmpty(targetUnitId))
            {
                targetUnitId = MetadataStringName(source, "focus_target_unit_id", "");
            }
            return new ScorePositionMetadata
            {
                DesiredMinDistance = desiredMinDistance,
                DesiredMaxDistance = MetadataInt(
                    source,
                    "desired_max_distance",
                    desiredMinDistance
                ),
                CurrentDistance = MetadataInt(source, "position_current_distance", -1),
                SafeDistance = MetadataInt(source, "position_safe_distance", -1),
                ObjectiveKind = MetadataStringName(source, "position_objective_kind", ""),
                TargetUnitId = targetUnitId,
                AnchorCoord = MetadataVector2I(
                    source,
                    "position_anchor_coord",
                    new Vector2I(-1, -1)
                ),
            };
        }
    }

    private sealed class ScorePathStepAoeMetadata
    {
        public List<PathStepHitCountEntry> HitCounts = new();
        public CombatEffectDef Effect;

        public static ScorePathStepAoeMetadata FromMetadata(IReadOnlyDictionary<string, object> source)
        {
            return new ScorePathStepAoeMetadata
            {
                HitCounts = ReadPathStepHitCountEntries(source),
                Effect = MetadataCombatEffectDef(source, "path_step_aoe_effect"),
            };
        }
    }

    internal void Setup(BattleDamageResolver damageResolver = null)
    {
        _damageResolver = damageResolver;
        ClearDecisionCaches();
    }

    internal void SetProfile(BattleAiScoreProfile profile)
    {
        _scoreProfile = profile ?? new BattleAiScoreProfile();
        ClearDecisionCaches();
    }

    internal void BeginDecisionScope(
        BattleState _state,
        BattleUnitState _actorUnitState,
        IReadOnlyDictionary<StringName, SkillDef> _skillDefs
    )
    {
        _decisionScopeActive = true;
        ClearDecisionCaches();
    }

    internal void EndDecisionScope()
    {
        _decisionScopeActive = false;
        ClearDecisionCaches();
    }

    internal BattleAiScoreProfile GetProfile()
    {
        return _scoreProfile;
    }

    internal int GetBucketPriority(StringName bucketId)
    {
        return _scoreProfile != null ? _scoreProfile.GetBucketPriority(bucketId) : 0;
    }

    internal BattleAiScoreInput BuildSkillScoreInput(
        IBattleAiScoreContext context,
        SkillDef skillDef,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDef> effectDefs = null,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        AiTraceRecorder.Enter("build_skill_score_input");
        AiTraceRecorder.Enter("score_input:metadata");
        ScoreBuildMetadata scoreMetadata = ScoreBuildMetadata.FromMetadata(
            metadata,
            "skill",
            skillDef != null ? skillDef.display_name : "",
            "",
            0
        );
        AiTraceRecorder.Exit("score_input:metadata");

        var scoreInput = new BattleAiScoreInput
        {
            command = command,
            skill_def = skillDef,
            preview = preview,
            action_kind = scoreMetadata.ActionKind,
            action_label = scoreMetadata.ActionLabel,
            score_bucket_id = scoreMetadata.ScoreBucketId,
        };
        scoreInput.score_bucket_priority = GetBucketPriority(scoreInput.score_bucket_id);
        scoreInput.runtime_action_metadata = CloneRuntimeActionMetadata(
            scoreMetadata.RuntimeActionMetadata
        );
        scoreInput.primary_coord = ResolvePrimaryCoord(command, preview);
        scoreInput.target_unit_ids = CopyTargetUnitIds(preview);
        scoreInput.target_coords = CopyTargetCoords(preview);
        scoreInput.target_count = scoreInput.target_unit_ids.Count;

        AiTraceRecorder.Enter("score_input:filter_effects");
        List<CombatEffectDef> effectiveEffectDefs = FilterEffectDefsForContext(
            effectDefs,
            context,
            skillDef
        );
        AiTraceRecorder.Exit("score_input:filter_effects");
        PopulateHitMetrics(scoreInput, context, effectiveEffectDefs);
        AiTraceRecorder.Enter("score_input:ground_control");
        PopulateGroundControlMetrics(scoreInput, effectiveEffectDefs);
        AiTraceRecorder.Exit("score_input:ground_control");
        PopulateRandomChainMetrics(scoreInput, context, effectiveEffectDefs, scoreMetadata.RandomChain);
        PopulateSpecialProfileMetrics(scoreInput, context);
        PopulatePathStepAoeMetrics(scoreInput, context, effectiveEffectDefs, scoreMetadata.PathStepAoe);
        AiTraceRecorder.Enter("score_input:resource_cost");
        PopulateResourceCostMetrics(scoreInput, skillDef, context);
        AiTraceRecorder.Exit("score_input:resource_cost");
        AiTraceRecorder.Enter("score_input:position");
        PopulatePositionMetrics(scoreInput, context, scoreMetadata.Position);
        AiTraceRecorder.Exit("score_input:position");
        AiTraceRecorder.Enter("score_input:post_threat_projection");
        PopulatePostActionThreatProjection(scoreInput, context, scoreMetadata.Position);
        AiTraceRecorder.Exit("score_input:post_threat_projection");
        scoreInput.total_score =
            ResolveActionBaseScore(scoreInput.action_kind, scoreMetadata)
            + scoreInput.hit_payoff_score
            + scoreInput.effective_target_count * _scoreProfile.target_count_weight
            - scoreInput.resource_cost_score
            + scoreInput.position_objective_score;
        AiTraceRecorder.Exit("build_skill_score_input");
        return scoreInput;
    }

    internal BattleAiScoreInput BuildSkillScoreInput(
        BattleAiContext context,
        SkillDef skillDef,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyList<CombatEffectDef> effectDefs = null,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        return BuildSkillScoreInput(
            (IBattleAiScoreContext)context,
            skillDef,
            command,
            preview,
            effectDefs,
            metadata
        );
    }

    internal BattleAiScoreInput BuildActionScoreInput(
        IBattleAiScoreContext context,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        AiTraceRecorder.Enter("build_action_score_input");
        AiTraceRecorder.Enter("score_input:metadata");
        ScoreBuildMetadata scoreMetadata = ScoreBuildMetadata.FromMetadata(
            metadata,
            actionKind,
            actionLabel,
            scoreBucketId,
            preview != null ? preview.move_cost : 0
        );
        AiTraceRecorder.Exit("score_input:metadata");

        var scoreInput = new BattleAiScoreInput
        {
            command = command,
            preview = preview,
            action_kind = scoreMetadata.ActionKind,
            action_label = scoreMetadata.ActionLabel,
            score_bucket_id = scoreMetadata.ScoreBucketId,
        };
        scoreInput.score_bucket_priority = GetBucketPriority(scoreInput.score_bucket_id);
        scoreInput.runtime_action_metadata = CloneRuntimeActionMetadata(
            scoreMetadata.RuntimeActionMetadata
        );
        scoreInput.primary_coord = ResolvePrimaryCoord(command, preview);
        scoreInput.target_unit_ids = CopyTargetUnitIds(preview);
        scoreInput.target_coords = CopyTargetCoords(preview);
        scoreInput.target_count = ResolveActionTargetCount(scoreInput);
        scoreInput.move_cost = scoreMetadata.MoveCost;
        AiTraceRecorder.Enter("score_input:position");
        PopulatePositionMetrics(scoreInput, context, scoreMetadata.Position);
        AiTraceRecorder.Exit("score_input:position");
        AiTraceRecorder.Enter("score_input:post_threat_projection");
        PopulatePostActionThreatProjection(scoreInput, context, scoreMetadata.Position);
        AiTraceRecorder.Exit("score_input:post_threat_projection");
        scoreInput.resource_cost_score =
            Math.Max(scoreInput.move_cost, 0) * _scoreProfile.movement_cost_weight;
        scoreInput.total_score =
            ResolveActionBaseScore(scoreInput.action_kind, scoreMetadata)
            + scoreInput.position_objective_score
            + scoreInput.target_count * scoreMetadata.TargetCountWeight
            - scoreInput.resource_cost_score;
        AiTraceRecorder.Exit("build_action_score_input");
        return scoreInput;
    }

    internal BattleAiScoreInput BuildActionScoreInput(
        BattleAiContext context,
        StringName actionKind,
        string actionLabel,
        StringName scoreBucketId,
        BattleCommand command,
        BattlePreview preview,
        IReadOnlyDictionary<string, object> metadata = null
    )
    {
        return BuildActionScoreInput(
            (IBattleAiScoreContext)context,
            actionKind,
            actionLabel,
            scoreBucketId,
            command,
            preview,
            metadata
        );
    }

    private static Vector2I ResolvePrimaryCoord(BattleCommand command, BattlePreview preview)
    {
        if (command != null && command.target_coord != new Vector2I(-1, -1))
        {
            return command.target_coord;
        }
        if (preview != null && preview.TargetCoordsTyped.Count > 0)
        {
            return preview.TargetCoordsTyped[0];
        }
        return new Vector2I(-1, -1);
    }

    private static BattleAiScoreRuntimeMetadata CloneRuntimeActionMetadata(
        BattleAiScoreRuntimeMetadata metadata
    )
    {
        return metadata?.Clone() ?? new BattleAiScoreRuntimeMetadata();
    }

    private void ClearDecisionCaches()
    {
        _threatProfileCache.Clear();
        _targetRoleThreatMultiplierCache.Clear();
        _threatProjectionCache.Clear();
        _hostileUnitsByActorFactionCache.Clear();
        _targetEffectMetricsCache.Clear();
        _anchorDistanceCache.Clear();
    }

    private static List<StringName> CopyTargetUnitIds(BattlePreview preview)
    {
        var targetUnitIds = new List<StringName>();
        if (preview == null)
        {
            return targetUnitIds;
        }
        foreach (StringName unitId in preview.TargetUnitIdsTyped)
        {
            targetUnitIds.Add(ProgressionDataUtils.to_string_name(unitId));
        }
        return targetUnitIds;
    }

    private static List<Vector2I> CopyTargetCoords(BattlePreview preview)
    {
        var targetCoords = new List<Vector2I>();
        if (preview == null)
        {
            return targetCoords;
        }
        foreach (Vector2I coord in preview.TargetCoordsTyped)
        {
            targetCoords.Add(coord);
        }
        return targetCoords;
    }

    private void PopulateGroundControlMetrics(
        BattleAiScoreInput scoreInput,
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        if (scoreInput == null || scoreInput.target_coords.Count == 0)
        {
            return;
        }
        int perCellScore = EstimateGroundControlScorePerCell(effectDefs);
        if (perCellScore <= 0)
        {
            return;
        }
        int cellCount = CountUniqueTargetCoords(scoreInput.target_coords);
        if (cellCount <= 0)
        {
            return;
        }
        scoreInput.estimated_ground_control_cell_count = cellCount;
        scoreInput.ground_control_score = cellCount * perCellScore;
        scoreInput.hit_payoff_score += scoreInput.ground_control_score;
    }

    private static int CountUniqueTargetCoords(IEnumerable<Vector2I> targetCoords)
    {
        var seen = new HashSet<Vector2I>();
        foreach (Vector2I coord in targetCoords ?? System.Array.Empty<Vector2I>())
        {
            seen.Add(coord);
        }
        return seen.Count;
    }

    private void PopulateRandomChainMetrics(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        IReadOnlyList<CombatEffectDef> effectDefs,
        ScoreRandomChainMetadata metadata
    )
    {
        if (
            scoreInput == null
            || scoreInput.skill_def is not SkillDef skillDef
            || skillDef.combat_profile == null
        )
        {
            return;
        }
        if (
            skillDef.combat_profile.TargetSelectionModeKind
            != BattleTargetSelectionMode.RandomChain
        )
        {
            return;
        }

        List<StringName> candidateUnitIds = DuplicateStringNameArray(
            metadata?.CandidatePoolUnitIds
        );
        if (candidateUnitIds.Count == 0 && scoreInput.preview != null)
        {
            candidateUnitIds = DuplicateStringNameArray(
                scoreInput.preview.RandomChainCandidateUnitIdsTyped
            );
        }
        scoreInput.random_chain_candidate_unit_ids = candidateUnitIds;
        scoreInput.random_chain_candidate_pool_count = candidateUnitIds.Count;
        scoreInput.random_chain_max_hits_per_target = Math.Max(
            metadata?.MaxHitsPerTarget ?? skillDef.combat_profile.max_hits_per_target,
            1
        );
        scoreInput.random_chain_max_attempt_count = Math.Max(
            metadata?.MaxAttemptCount
                ?? candidateUnitIds.Count * scoreInput.random_chain_max_hits_per_target,
            1
        );
        scoreInput.random_chain_selection_policy =
            metadata?.SelectionPolicy ?? "random_from_living_pool";
        scoreInput.random_chain_pool_refresh_policy =
            metadata?.PoolRefreshPolicy ?? "before_each_attempt";
        scoreInput.random_chain_score_estimate_policy =
            metadata?.ScoreEstimatePolicy ?? "expected_value";

        BattleState state = ContextState(context);
        if (state == null)
        {
            return;
        }
        foreach (StringName candidateUnitId in candidateUnitIds)
        {
            BattleUnitState candidateUnit = GetUnit(state, candidateUnitId);
            if (candidateUnit == null)
            {
                continue;
            }
            PopulateTargetEffectMetrics(
                scoreInput,
                context,
                candidateUnit,
                effectDefs,
                scoreInput.random_chain_max_hits_per_target
            );
        }
    }

    private void PopulateHitMetrics(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        IReadOnlyList<CombatEffectDef> effectDefs
    )
    {
        AiTraceRecorder.Enter("_populate_hit_metrics");
        PopulateHitMetricsImpl(scoreInput, context, effectDefs);
        AiTraceRecorder.Exit("_populate_hit_metrics");
    }

    private void PopulateHitMetricsImpl(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        IReadOnlyList<CombatEffectDef> effectDefs
    )
    {
        if (scoreInput == null)
        {
            return;
        }
        scoreInput.estimated_hit_rate_percent = ResolveEstimatedHitRatePercent(scoreInput.preview?.hit_preview);
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (state == null || actor == null)
        {
            return;
        }
        foreach (StringName targetUnitId in scoreInput.target_unit_ids)
        {
            BattleUnitState targetUnit = GetUnit(state, targetUnitId);
            if (targetUnit == null)
            {
                continue;
            }
            PopulateTargetEffectMetrics(scoreInput, context, targetUnit, effectDefs);
        }
        PopulateChainDamageMetrics(scoreInput, context, effectDefs);
        int healingPayoff =
            (scoreInput.estimated_ally_healing - scoreInput.estimated_enemy_healing)
            * _scoreProfile.heal_weight;
        int damagePayoff = scoreInput.hit_payoff_score - healingPayoff;
        scoreInput.hit_payoff_score = RoundToInt(
            (double)damagePayoff * scoreInput.estimated_hit_rate_percent / 100.0
        ) + healingPayoff;
        scoreInput.target_priority_score = RoundToInt(
            (double)scoreInput.target_priority_score * scoreInput.estimated_hit_rate_percent / 100.0
        );
    }

    private void PopulateSpecialProfileMetrics(BattleAiScoreInput scoreInput, IBattleAiScoreContext context)
    {
        AiTraceRecorder.Enter("_populate_special_profile_metrics");
        PopulateSpecialProfileMetricsImpl(scoreInput, context);
        AiTraceRecorder.Exit("_populate_special_profile_metrics");
    }

    private void PopulateSpecialProfileMetricsImpl(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context
    )
    {
        if (
            scoreInput == null
            || scoreInput.preview == null
            || scoreInput.preview.special_profile_preview_facts == null
        )
        {
            return;
        }

        BattleSpecialProfilePreviewFacts facts = scoreInput.preview.special_profile_preview_facts;
        List<MeteorSwarmNumericSummary> targetSummaries = ReadTargetNumericSummaries(facts);
        scoreInput.special_profile_preview_facts = facts;
        scoreInput.target_numeric_summary = new List<MeteorSwarmNumericSummary>(targetSummaries);
        if (facts is MeteorSwarmPreviewFacts meteorFacts)
        {
            scoreInput.friendly_fire_numeric_summary = new List<MeteorSwarmNumericSummary>(
                meteorFacts.GetFriendlyFireNumericSummariesTyped()
            );
        }
        else
        {
            scoreInput.friendly_fire_numeric_summary = new List<MeteorSwarmNumericSummary>();
        }
        scoreInput.attack_roll_modifier_breakdown = CloneModifierSpecList(
            facts.GetAttackRollModifierBreakdown()
        );
        scoreInput.estimated_terrain_effect_count += Math.Max(
            facts.GetExpectedTerrainEffectCount(),
            0
        );
        if (scoreInput.estimated_terrain_effect_count > 0)
        {
            scoreInput.hit_payoff_score +=
                scoreInput.estimated_terrain_effect_count * _scoreProfile.terrain_weight;
        }

        if (ContextState(context) == null || ContextUnitState(context) == null)
        {
            scoreInput.meteor_use_case = ResolveMeteorUseCase(scoreInput, targetSummaries);
            return;
        }

        if (targetSummaries.Count == 0)
        {
            PopulateSpecialProfileTargetCountsWithoutNumericSummary(scoreInput, context);
        }
        else
        {
            foreach (MeteorSwarmNumericSummary summary in targetSummaries)
            {
                PopulateSpecialProfileTargetSummary(scoreInput, context, summary);
            }
        }
        scoreInput.meteor_use_case = ResolveMeteorUseCase(scoreInput, targetSummaries);
    }

    private void PopulateSpecialProfileTargetSummary(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context,
        MeteorSwarmNumericSummary summary
    )
    {
        StringName targetUnitId = summary.TargetUnitId;
        if (IsEmpty(targetUnitId))
        {
            return;
        }
        BattleUnitState actor = ContextUnitState(context);
        BattleUnitState targetUnit = GetUnit(ContextState(context), targetUnitId);
        if (actor == null || targetUnit == null)
        {
            return;
        }

        int estimatedDamage = summary.ComponentExpectedDamage;
        int worstCaseDamage = Math.Max(summary.ComponentWorstCaseDamage, estimatedDamage);
        int statusCount = summary.StatusEffectCount;
        bool isAlly = targetUnit.faction_id == actor.faction_id;
        scoreInput.estimated_damage += estimatedDamage;
        scoreInput.estimated_status_count += statusCount;
        scoreInput.estimated_control_count += statusCount;
        if (isAlly)
        {
            scoreInput.ally_target_count += 1;
            scoreInput.estimated_ally_damage += estimatedDamage;
            PopulateSpecialProfileAllyRisk(
                scoreInput,
                targetUnit,
                summary,
                estimatedDamage,
                worstCaseDamage,
                statusCount
            );
            return;
        }

        scoreInput.enemy_target_count += 1;
        scoreInput.estimated_enemy_damage += estimatedDamage;
        if (estimatedDamage > 0 || statusCount > 0 || scoreInput.estimated_terrain_effect_count > 0)
        {
            scoreInput.effective_target_count += 1;
        }
        scoreInput.hit_payoff_score += estimatedDamage * _scoreProfile.damage_weight;
        scoreInput.hit_payoff_score += statusCount * _scoreProfile.status_weight;
        int targetPriorityBonus = ResolveTargetRoleThreatBonus(
            context,
            targetUnit,
            estimatedDamage,
            statusCount,
            scoreInput.estimated_terrain_effect_count,
            0
        );
        scoreInput.target_priority_score += targetPriorityBonus;
        scoreInput.hit_payoff_score += targetPriorityBonus;
        int lethalBasis = Math.Max(
            estimatedDamage,
            summary.LethalProbabilityPercent > 0 ? worstCaseDamage : estimatedDamage
        );
        int lethalBonus = ResolveLethalTargetBonus(scoreInput, context, targetUnit, lethalBasis);
        scoreInput.target_priority_score += lethalBonus;
        scoreInput.hit_payoff_score += lethalBonus;
        RecordMeteorHighPriorityTarget(
            scoreInput,
            context,
            targetUnit,
            summary,
            targetPriorityBonus + lethalBonus
        );
        if (statusCount > 0)
        {
            AppendUniqueStringName(scoreInput.estimated_control_target_ids, targetUnit.unit_id);
            if (IsPriorityThreatTarget(context, targetUnit))
            {
                AppendUniqueStringName(
                    scoreInput.estimated_control_threat_target_ids,
                    targetUnit.unit_id
                );
            }
        }
    }

    private static List<BattleAttackRollModifierSpec> CloneModifierSpecList(
        IEnumerable<BattleAttackRollModifierSpec> values
    )
    {
        var result = new List<BattleAttackRollModifierSpec>();
        if (values == null)
        {
            return result;
        }
        foreach (BattleAttackRollModifierSpec value in values)
        {
            if (value != null)
            {
                result.Add(value.Clone());
            }
        }
        return result;
    }

    private void PopulateSpecialProfileAllyRisk(
        BattleAiScoreInput scoreInput,
        BattleUnitState targetUnit,
        MeteorSwarmNumericSummary summary,
        int estimatedDamage,
        int worstCaseDamage,
        int statusCount
    )
    {
        if (estimatedDamage <= 0 && worstCaseDamage <= 0 && statusCount <= 0)
        {
            return;
        }
        scoreInput.estimated_friendly_fire_target_count += 1;
        scoreInput.estimated_friendly_fire_damage += estimatedDamage;
        if (statusCount > 0)
        {
            scoreInput.estimated_friendly_control_target_count += 1;
        }
        bool isLethal =
            worstCaseDamage >= Math.Max(targetUnit.current_hp, 1)
            || summary.LethalProbabilityPercent >= FriendlyLethalMinProbabilityThreshold;
        int penalty =
            estimatedDamage * _scoreProfile.friendly_fire_damage_weight
            + _scoreProfile.friendly_fire_target_weight
            + statusCount * _scoreProfile.friendly_control_target_weight;
        if (isLethal)
        {
            scoreInput.estimated_friendly_lethal_target_count += 1;
            penalty += _scoreProfile.friendly_lethal_target_weight;
        }

        string rejectReason = ResolveMeteorFriendlyFireRejectReason(
            targetUnit,
            summary,
            estimatedDamage,
            worstCaseDamage,
            statusCount
        );
        if (
            !string.IsNullOrEmpty(rejectReason)
            && string.IsNullOrEmpty(scoreInput.friendly_fire_reject_reason)
        )
        {
            scoreInput.friendly_fire_reject_reason = rejectReason;
        }
        scoreInput.friendly_fire_penalty_score += penalty;
        scoreInput.hit_payoff_score -= penalty;
    }

    private static void PopulateSpecialProfileTargetCountsWithoutNumericSummary(
        BattleAiScoreInput scoreInput,
        IBattleAiScoreContext context
    )
    {
        BattleState state = ContextState(context);
        BattleUnitState actor = ContextUnitState(context);
        if (scoreInput == null || state == null || actor == null)
        {
            return;
        }
        foreach (StringName targetUnitId in scoreInput.target_unit_ids)
        {
            BattleUnitState targetUnit = GetUnit(state, targetUnitId);
            if (targetUnit == null)
            {
                continue;
            }
            if (targetUnit.faction_id == actor.faction_id)
            {
                scoreInput.ally_target_count += 1;
            }
            else
            {
                scoreInput.enemy_target_count += 1;
                scoreInput.effective_target_count += 1;
            }
        }
    }
}
