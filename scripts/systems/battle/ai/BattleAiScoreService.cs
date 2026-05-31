using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

[GlobalClass]
public partial class BattleAiScoreService : RefCounted
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

    private sealed class ScoreBuildMetadata
    {
        public StringName ActionKind = "skill";
        public string ActionLabel = "";
        public StringName ScoreBucketId = "";
        public GDictionary RuntimeActionMetadata = new();
        public int MoveCost;
        public int TargetCountWeight;
        public bool HasActionBaseScore;
        public int ActionBaseScore;
        public ScoreRandomChainMetadata RandomChain = new();
        public ScorePositionMetadata Position = new();
        public ScorePathStepAoeMetadata PathStepAoe = new();

        public static ScoreBuildMetadata FromDictionary(
            GDictionary source,
            StringName defaultActionKind,
            string defaultActionLabel,
            StringName defaultScoreBucketId,
            int defaultMoveCost
        )
        {
            source ??= new GDictionary();
            bool hasActionBaseScore = HasKey(source, "action_base_score");
            return new ScoreBuildMetadata
            {
                ActionKind = DictStringName(source, "action_kind", defaultActionKind),
                ActionLabel = DictString(source, "action_label", defaultActionLabel ?? ""),
                ScoreBucketId = DictStringName(source, "score_bucket_id", defaultScoreBucketId),
                RuntimeActionMetadata = CopyRuntimeActionMetadata(source),
                MoveCost = DictInt(source, "move_cost", defaultMoveCost),
                TargetCountWeight = DictInt(source, "target_count_weight", 0),
                HasActionBaseScore = hasActionBaseScore,
                ActionBaseScore = hasActionBaseScore ? DictInt(source, "action_base_score", 0) : 0,
                RandomChain = ScoreRandomChainMetadata.FromDictionary(source),
                Position = ScorePositionMetadata.FromDictionary(source),
                PathStepAoe = ScorePathStepAoeMetadata.FromDictionary(source),
            };
        }
    }

    private sealed class ScoreRandomChainMetadata
    {
        public GStringNameArray CandidatePoolUnitIds = new();
        public int? MaxHitsPerTarget;
        public int? MaxAttemptCount;
        public StringName SelectionPolicy = "random_from_living_pool";
        public StringName PoolRefreshPolicy = "before_each_attempt";
        public StringName ScoreEstimatePolicy = "expected_value";

        public static ScoreRandomChainMetadata FromDictionary(GDictionary source)
        {
            source ??= new GDictionary();
            var result = new ScoreRandomChainMetadata
            {
                CandidatePoolUnitIds = CopyStringNameArray(
                    DictArray(source, "candidate_pool_unit_ids", new GArray())
                ),
                SelectionPolicy = DictStringName(
                    source,
                    "random_chain_selection_policy",
                    "random_from_living_pool"
                ),
                PoolRefreshPolicy = DictStringName(
                    source,
                    "random_chain_pool_refresh_policy",
                    "before_each_attempt"
                ),
                ScoreEstimatePolicy = DictStringName(
                    source,
                    "random_chain_score_estimate_policy",
                    "expected_value"
                ),
            };
            if (HasKey(source, "random_chain_max_hits_per_target"))
            {
                result.MaxHitsPerTarget = DictInt(source, "random_chain_max_hits_per_target", 1);
            }
            if (HasKey(source, "random_chain_max_attempt_count"))
            {
                result.MaxAttemptCount = DictInt(source, "random_chain_max_attempt_count", 1);
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

        public static ScorePositionMetadata FromDictionary(GDictionary source)
        {
            source ??= new GDictionary();
            int desiredMinDistance = DictInt(source, "desired_min_distance", -1);
            StringName targetUnitId = DictStringName(source, "position_target_unit_id", "");
            if (IsEmpty(targetUnitId))
            {
                targetUnitId = DictStringName(source, "focus_target_unit_id", "");
            }
            return new ScorePositionMetadata
            {
                DesiredMinDistance = desiredMinDistance,
                DesiredMaxDistance = DictInt(
                    source,
                    "desired_max_distance",
                    desiredMinDistance
                ),
                CurrentDistance = DictInt(source, "position_current_distance", -1),
                SafeDistance = DictInt(source, "position_safe_distance", -1),
                ObjectiveKind = DictStringName(source, "position_objective_kind", ""),
                TargetUnitId = targetUnitId,
                AnchorCoord = DictVector2I(
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

        public static ScorePathStepAoeMetadata FromDictionary(GDictionary source)
        {
            source ??= new GDictionary();
            return new ScorePathStepAoeMetadata
            {
                HitCounts = ReadPathStepHitCountEntries(source),
                Effect = DictObject(source, "path_step_aoe_effect") as CombatEffectDef,
            };
        }
    }

    public void setup(BattleDamageResolver damage_resolver = null)
    {
        _damageResolver = damage_resolver;
    }

    public void set_profile(BattleAiScoreProfile profile)
    {
        _scoreProfile = profile ?? new BattleAiScoreProfile();
    }

    public BattleAiScoreProfile get_profile()
    {
        return _scoreProfile;
    }

    public int get_bucket_priority(StringName bucket_id)
    {
        return _scoreProfile != null ? _scoreProfile.get_bucket_priority(bucket_id) : 0;
    }

    public BattleAiScoreInput build_skill_score_input(
        IBattleAiScoreContext context,
        SkillDef skill_def,
        BattleCommand command,
        BattlePreview preview,
        GArray effect_defs = null,
        GDictionary metadata = null
    )
    {
        effect_defs ??= new GArray();
        metadata ??= new GDictionary();
        ScoreBuildMetadata scoreMetadata = ScoreBuildMetadata.FromDictionary(
            metadata,
            "skill",
            skill_def != null ? skill_def.display_name : "",
            "",
            0
        );

        var scoreInput = new BattleAiScoreInput
        {
            command = command,
            skill_def = skill_def,
            preview = preview,
            action_kind = scoreMetadata.ActionKind,
            action_label = scoreMetadata.ActionLabel,
            score_bucket_id = scoreMetadata.ScoreBucketId,
        };
        scoreInput.score_bucket_priority = get_bucket_priority(scoreInput.score_bucket_id);
        scoreInput.runtime_action_metadata = CloneRuntimeActionMetadata(
            scoreMetadata.RuntimeActionMetadata
        );
        scoreInput.primary_coord = ResolvePrimaryCoord(command, preview);
        scoreInput.target_unit_ids = CopyTargetUnitIds(preview);
        scoreInput.target_coords = CopyTargetCoords(preview);
        scoreInput.target_count = scoreInput.target_unit_ids.Count;

        List<CombatEffectDef> effectiveEffectDefs = FilterEffectDefsForContext(
            DecodeEffectDefs(effect_defs),
            context,
            skill_def
        );
        PopulateHitMetrics(scoreInput, context, effectiveEffectDefs);
        PopulateGroundControlMetrics(scoreInput, effectiveEffectDefs);
        PopulateRandomChainMetrics(scoreInput, context, effectiveEffectDefs, scoreMetadata.RandomChain);
        PopulateSpecialProfileMetrics(scoreInput, context);
        PopulatePathStepAoeMetrics(scoreInput, context, effectiveEffectDefs, scoreMetadata.PathStepAoe);
        PopulateResourceCostMetrics(scoreInput, skill_def, context);
        PopulatePositionMetrics(scoreInput, context, scoreMetadata.Position);
        PopulatePostActionThreatProjection(scoreInput, context, scoreMetadata.Position);
        scoreInput.total_score =
            ResolveActionBaseScore(scoreInput.action_kind, scoreMetadata)
            + scoreInput.hit_payoff_score
            + scoreInput.effective_target_count * _scoreProfile.target_count_weight
            - scoreInput.resource_cost_score
            + scoreInput.position_objective_score;
        return scoreInput;
    }

    public BattleAiScoreInput build_skill_score_input(
        BattleAiContext context,
        SkillDef skill_def,
        BattleCommand command,
        BattlePreview preview,
        GArray effect_defs = null,
        GDictionary metadata = null
    )
    {
        return build_skill_score_input(
            (IBattleAiScoreContext)context,
            skill_def,
            command,
            preview,
            effect_defs,
            metadata
        );
    }

    public BattleAiScoreInput build_action_score_input(
        IBattleAiScoreContext context,
        StringName action_kind,
        string action_label,
        StringName score_bucket_id,
        BattleCommand command,
        BattlePreview preview,
        GDictionary metadata = null
    )
    {
        metadata ??= new GDictionary();
        ScoreBuildMetadata scoreMetadata = ScoreBuildMetadata.FromDictionary(
            metadata,
            action_kind,
            action_label,
            score_bucket_id,
            preview != null ? preview.move_cost : 0
        );

        var scoreInput = new BattleAiScoreInput
        {
            command = command,
            preview = preview,
            action_kind = scoreMetadata.ActionKind,
            action_label = scoreMetadata.ActionLabel,
            score_bucket_id = scoreMetadata.ScoreBucketId,
        };
        scoreInput.score_bucket_priority = get_bucket_priority(scoreInput.score_bucket_id);
        scoreInput.runtime_action_metadata = CloneRuntimeActionMetadata(
            scoreMetadata.RuntimeActionMetadata
        );
        scoreInput.primary_coord = ResolvePrimaryCoord(command, preview);
        scoreInput.target_unit_ids = CopyTargetUnitIds(preview);
        scoreInput.target_coords = CopyTargetCoords(preview);
        scoreInput.target_count = ResolveActionTargetCount(scoreInput);
        scoreInput.move_cost = scoreMetadata.MoveCost;
        PopulatePositionMetrics(scoreInput, context, scoreMetadata.Position);
        PopulatePostActionThreatProjection(scoreInput, context, scoreMetadata.Position);
        scoreInput.resource_cost_score =
            Math.Max(scoreInput.move_cost, 0) * _scoreProfile.movement_cost_weight;
        scoreInput.total_score =
            ResolveActionBaseScore(scoreInput.action_kind, scoreMetadata)
            + scoreInput.position_objective_score
            + scoreInput.target_count * scoreMetadata.TargetCountWeight
            - scoreInput.resource_cost_score;
        return scoreInput;
    }

    public BattleAiScoreInput build_action_score_input(
        BattleAiContext context,
        StringName action_kind,
        string action_label,
        StringName score_bucket_id,
        BattleCommand command,
        BattlePreview preview,
        GDictionary metadata = null
    )
    {
        return build_action_score_input(
            (IBattleAiScoreContext)context,
            action_kind,
            action_label,
            score_bucket_id,
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
        if (preview != null && preview.target_coords.Count > 0)
        {
            return preview.target_coords[0];
        }
        return new Vector2I(-1, -1);
    }

    private static GDictionary CopyRuntimeActionMetadata(GDictionary metadata)
    {
        return CloneRuntimeActionMetadata(
            DictDictionary(metadata, "runtime_action_metadata", new GDictionary())
        );
    }

    private static GDictionary CloneRuntimeActionMetadata(GDictionary metadata)
    {
        return metadata != null && metadata.Count > 0 ? metadata.Duplicate(true) : new GDictionary();
    }

    private static GStringNameArray CopyTargetUnitIds(BattlePreview preview)
    {
        var targetUnitIds = new GStringNameArray();
        if (preview == null)
        {
            return targetUnitIds;
        }
        foreach (StringName unitId in preview.target_unit_ids)
        {
            targetUnitIds.Add(ProgressionDataUtils.to_string_name(unitId));
        }
        return targetUnitIds;
    }

    private static GVector2IArray CopyTargetCoords(BattlePreview preview)
    {
        var targetCoords = new GVector2IArray();
        if (preview == null)
        {
            return targetCoords;
        }
        foreach (Vector2I coord in preview.target_coords)
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

    private static int CountUniqueTargetCoords(GVector2IArray targetCoords)
    {
        var seen = new HashSet<Vector2I>();
        foreach (Vector2I coord in targetCoords)
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
            ProgressionDataUtils.to_string_name(skillDef.combat_profile.target_selection_mode)
            != "random_chain"
        )
        {
            return;
        }

        GStringNameArray candidateUnitIds = DuplicateStringNameArray(
            metadata?.CandidatePoolUnitIds
        );
        if (candidateUnitIds.Count == 0 && scoreInput.preview != null)
        {
            candidateUnitIds = DuplicateStringNameArray(
                scoreInput.preview.random_chain_candidate_unit_ids
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
        AiTraceRecorder.enter("_populate_hit_metrics");
        PopulateHitMetricsImpl(scoreInput, context, effectDefs);
        AiTraceRecorder.exit("_populate_hit_metrics");
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
        AiTraceRecorder.enter("_populate_special_profile_metrics");
        PopulateSpecialProfileMetricsImpl(scoreInput, context);
        AiTraceRecorder.exit("_populate_special_profile_metrics");
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
        GDictionary factsPayload = facts.ToDict();
        scoreInput.special_profile_preview_facts = factsPayload.Duplicate(true);
        scoreInput.friendly_fire_numeric_summary = ToUntypedArray(
            facts.GetFriendlyFireNumericSummary()
        );
        scoreInput.attack_roll_modifier_breakdown = ToUntypedArray(
            facts.attack_roll_modifier_breakdown
        );
        List<MeteorSwarmNumericSummary> targetSummaries = ReadTargetNumericSummaries(facts);
        scoreInput.target_numeric_summary = TargetNumericSummariesToArray(targetSummaries);

        scoreInput.estimated_terrain_effect_count += Math.Max(
            DictInt(factsPayload, "expected_terrain_effect_count", 0),
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
