using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal abstract class EnemyAiActionDefinition
{
    protected EnemyAiActionDefinition(
        StringName actionId,
        StringName scoreBucketId,
        StringName actionIntent,
        EnemyAiActionKind kind,
        IReadOnlyList<StringName> declaredSkillIds
    )
    {
        ActionId = actionId;
        ScoreBucketId = scoreBucketId;
        ActionIntent = actionIntent;
        Kind = kind;
        DeclaredSkillIds = EnemyDefinitionCollections.FreezeList(declaredSkillIds);
    }

    internal StringName ActionId { get; }
    internal StringName ScoreBucketId { get; }
    internal StringName ActionIntent { get; }
    internal EnemyAiActionKind Kind { get; }
    internal IReadOnlyList<StringName> DeclaredSkillIds { get; }

    internal virtual string BuildSignature() =>
        $"{Kind}|{ActionId}|{ScoreBucketId}|{ActionIntent}|skills={string.Join(",", DeclaredSkillIds)}";

    internal static EnemyAiActionDefinition FromResource(EnemyAiAction source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source switch
        {
            MoveToMultiUnitSkillPositionAction action =>
                new MoveToMultiUnitSkillPositionActionDefinition(
                    action.action_id,
                    action.score_bucket_id,
                    action.action_intent,
                    Copy(action.skill_ids),
                    action.target_selector,
                    action.desired_min_distance,
                    action.desired_max_distance,
                    action.distance_reference,
                    action.candidate_pool_limit,
                    action.candidate_group_limit,
                    action.target_count_weight
                ),
            UseUnitSkillAction action => new UseUnitSkillActionDefinition(
                action.action_id,
                action.score_bucket_id,
                action.action_intent,
                Copy(action.skill_ids),
                action.target_selector,
                action.minimum_effective_target_count,
                action.maximum_friendly_fire_target_count,
                action.allow_friendly_lethal,
                action.desired_min_distance,
                action.desired_max_distance,
                action.distance_reference
            ),
            UseGroundSkillAction action => new UseGroundSkillActionDefinition(
                action.action_id,
                action.score_bucket_id,
                action.action_intent,
                Copy(action.skill_ids),
                action.minimum_hit_count,
                action.allow_empty_ground_control,
                action.allow_ground_control_supplement_partial_hits,
                action.minimum_ground_control_score,
                action.minimum_ally_threat_hit_count,
                action.maximum_friendly_fire_target_count,
                action.allow_friendly_lethal,
                action.threat_minimum_safe_distance,
                action.threat_safe_distance_margin,
                action.desired_min_distance,
                action.desired_max_distance,
                action.distance_reference
            ),
            UseMultiUnitSkillAction action => new UseMultiUnitSkillActionDefinition(
                action.action_id,
                action.score_bucket_id,
                action.action_intent,
                Copy(action.skill_ids),
                action.target_selector,
                action.desired_min_distance,
                action.desired_max_distance,
                action.distance_reference,
                action.candidate_pool_limit,
                action.candidate_group_limit
            ),
            UseRandomChainSkillAction action => new UseRandomChainSkillActionDefinition(
                action.action_id,
                action.score_bucket_id,
                action.action_intent,
                Copy(action.skill_ids),
                action.target_selector,
                action.desired_min_distance,
                action.desired_max_distance,
                action.distance_reference,
                action.minimum_candidate_count
            ),
            UseChargeAction action => new UseChargeActionDefinition(
                action.action_id,
                action.score_bucket_id,
                action.action_intent,
                action.skill_id,
                action.target_selector,
                action.minimum_charge_move_distance
            ),
            UseChargePathAoeAction action => new UseChargePathAoeActionDefinition(
                action.action_id,
                action.score_bucket_id,
                action.action_intent,
                Copy(action.skill_ids),
                action.target_selector,
                action.minimum_hit_count,
                action.desired_min_distance,
                action.desired_max_distance
            ),
            MoveToRangeAction action => new MoveToRangeActionDefinition(
                action.action_id,
                action.score_bucket_id,
                action.action_intent,
                action.ai_evaluation_mode,
                action.target_selector,
                action.desired_min_distance,
                action.desired_max_distance,
                Copy(action.range_skill_ids),
                action.screening_mode,
                action.enable_aoe_setup_positioning,
                action.aoe_setup_min_target_count,
                action.aoe_setup_target_count_weight,
                action.aoe_setup_improvement_weight,
                action.aoe_setup_friendly_fire_penalty,
                action.screening_min_hp_basis_points,
                action.screening_ally_min_attack_range,
                action.screening_enemy_max_contact_range,
                action.screening_threat_distance_buffer,
                action.screening_path_bonus
            ),
            MoveToAdvantagePositionAction action =>
                new MoveToAdvantagePositionActionDefinition(
                    action.action_id,
                    action.score_bucket_id,
                    action.action_intent,
                    action.target_selector,
                    action.desired_min_distance,
                    action.desired_max_distance,
                    Copy(action.range_skill_ids),
                    action.minimum_safe_distance,
                    action.safe_distance_margin,
                    action.min_survival_margin_gain_to_escape,
                    action.min_distance_progress_when_beyond_band,
                    action.positioning_mode,
                    action.high_ground_weight,
                    action.safety_weight,
                    action.distance_band_weight,
                    action.candidate_limit
                ),
            UseGroundRepositionSkillAction action =>
                new UseGroundRepositionSkillActionDefinition(
                    action.action_id,
                    action.score_bucket_id,
                    action.action_intent,
                    Copy(action.skill_ids),
                    action.target_selector,
                    action.minimum_safe_distance,
                    action.safe_distance_margin,
                    action.desired_max_distance_bonus,
                    action.action_base_score,
                    action.min_survival_margin_gain_to_escape
                ),
            RetreatAction action => new RetreatActionDefinition(
                action.action_id,
                action.score_bucket_id,
                action.action_intent,
                action.target_selector,
                action.minimum_safe_distance,
                action.use_dynamic_threat_safe_distance,
                action.safe_distance_margin
            ),
            WaitAction action => new WaitActionDefinition(
                action.action_id,
                action.score_bucket_id,
                action.action_intent,
                action.active_rest_action_base_score,
                action.active_rest_min_stamina_residue
            ),
            _ => throw new InvalidOperationException(
                $"Unsupported EnemyAiAction authoring type {source.GetType().Name}."
            ),
        };
    }

    private static IReadOnlyList<StringName> Copy(IEnumerable<StringName> source) =>
        source == null ? Array.Empty<StringName>() : new List<StringName>(source);
}

internal static class EnemyDefinitionCollections
{
    internal static IReadOnlyList<T> FreezeList<T>(IEnumerable<T> source)
    {
        return new ReadOnlyCollection<T>(
            source == null ? new List<T>() : new List<T>(source)
        );
    }

    internal static IReadOnlyDictionary<TKey, TValue> FreezeDictionary<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        IEqualityComparer<TKey> comparer = null
    )
    {
        var copy = comparer == null
            ? new Dictionary<TKey, TValue>()
            : new Dictionary<TKey, TValue>(comparer);
        if (source != null)
        {
            foreach ((TKey key, TValue value) in source)
                copy.Add(key, value);
        }
        return new ReadOnlyDictionary<TKey, TValue>(copy);
    }

    internal static long ResolveResourceUid(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ResourceUid.InvalidId;
        long uid = ResourceLoader.GetResourceUid(path);
        return uid == ResourceUid.InvalidId ? ResourceUid.InvalidId : uid;
    }
}
