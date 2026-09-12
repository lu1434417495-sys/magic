using System;
using System.Collections.Generic;
using Godot;

public readonly struct BattleRepeatAttackStageSpec
{
    public readonly int stage_index;
    public readonly int stage_count;
    public readonly int skill_level;
    public readonly int stage_base_attack_bonus;
    public readonly int follow_up_attack_penalty;
    public readonly int penalty_free_stages;
    public readonly bool exponential_penalty;
    public readonly CombatResourceKind cost_resource_kind;
    public readonly int base_resource_cost;
    public readonly int stage_resource_cost;
    public readonly int follow_up_fixed_cost;
    public readonly int follow_up_cost_addition;
    public readonly double follow_up_cost_multiplier;
    public readonly int stage_damage_multiplier_percent;
    public readonly bool stop_on_miss;
    public readonly bool fate_aware;
    public readonly StringName stage_label;

    private BattleRepeatAttackStageSpec(
        int stageIndex,
        int stageCount,
        int skillLevel,
        int stageBaseAttackBonus,
        int followUpAttackPenalty,
        int penaltyFreeStages,
        bool exponentialPenalty,
        CombatResourceKind costResourceKind,
        int baseResourceCost,
        int stageResourceCost,
        int followUpFixedCost,
        int followUpCostAddition,
        double followUpCostMultiplier,
        int stageDamageMultiplierPercent,
        bool stopOnMiss,
        bool fateAware,
        StringName stageLabel
    )
    {
        stage_index = Mathf.Max(stageIndex, 0);
        stage_count = Mathf.Max(stageCount, 0);
        skill_level = Mathf.Max(skillLevel, 0);
        stage_base_attack_bonus = stageBaseAttackBonus;
        follow_up_attack_penalty = Mathf.Max(followUpAttackPenalty, 0);
        penalty_free_stages = Mathf.Max(penaltyFreeStages, 0);
        exponential_penalty = exponentialPenalty;
        cost_resource_kind =
            costResourceKind == CombatResourceKind.None
                ? CombatResourceKind.Aura
                : costResourceKind;
        base_resource_cost = Mathf.Max(baseResourceCost, 0);
        stage_resource_cost = Mathf.Max(stageResourceCost, 0);
        follow_up_fixed_cost = Mathf.Max(followUpFixedCost, 0);
        follow_up_cost_addition = Mathf.Max(followUpCostAddition, 0);
        follow_up_cost_multiplier = Math.Max(followUpCostMultiplier, 1.0);
        stage_damage_multiplier_percent = Mathf.Max(stageDamageMultiplierPercent, 1);
        stop_on_miss = stopOnMiss;
        fate_aware = fateAware;
        stage_label = stageLabel ?? new StringName("");
    }

    public static BattleRepeatAttackStageSpec FromRepeatAttackEffect(
        CombatEffectDefinition repeat_attack_effect,
        int stage_index_value,
        int stage_count_value,
        int skill_level_value,
        bool fate_aware_value = false
    )
    {
        int stageIndex = Mathf.Max(stage_index_value, 0);
        if (repeat_attack_effect == null)
        {
            return new BattleRepeatAttackStageSpec(
                stageIndex,
                stage_count_value,
                skill_level_value,
                0,
                0,
                0,
                false,
                CombatResourceKind.Aura,
                0,
                0,
                0,
                0,
                1.0,
                100,
                true,
                fate_aware_value,
                new StringName($"repeat_stage_{stageIndex}")
            );
        }

        RepeatAttackUntilFailEffectPayloadDefinition payload =
            repeat_attack_effect.Payload as RepeatAttackUntilFailEffectPayloadDefinition;

        return new BattleRepeatAttackStageSpec(
            stageIndex,
            stage_count_value,
            skill_level_value,
            (payload?.BaseAttackBonus ?? 0)
                + (stageIndex > 0
                    ? repeat_attack_effect.GetFollowUpAttackRollBonus(skill_level_value)
                    : 0),
            payload?.FollowUpAttackPenalty ?? 0,
            ResolvePenaltyFreeStages(payload?.PenaltyFreeStagesByLevel, skill_level_value),
            payload?.ExponentialPenalty ?? false,
            payload?.CostResourceKind ?? CombatResourceKind.Aura,
            0,
            0,
            payload?.FollowUpFixedCost ?? 0,
            payload?.FollowUpCostAddition ?? 0,
            payload?.FollowUpCostMultiplier ?? 1.0,
            ResolveStageDamageMultiplierPercent(
                repeat_attack_effect.FollowUpDamageMultiplierPercent,
                stageIndex
            ),
            repeat_attack_effect.StopOnMiss,
            fate_aware_value,
            new StringName($"repeat_stage_{stageIndex}")
        );
    }

    public int ResolveStageAttackPenalty()
    {
        if (stage_index < penalty_free_stages)
        {
            return 0;
        }
        int penalizedStageIndex = Mathf.Max(stage_index - penalty_free_stages, 0);
        if (exponential_penalty)
        {
            return (int)Mathf.Pow(2, penalizedStageIndex) * follow_up_attack_penalty;
        }
        return (penalizedStageIndex + 1) * follow_up_attack_penalty;
    }

    public BattleRepeatAttackStageSpec WithBaseResourceCost(int value)
    {
        int normalizedBaseCost = Mathf.Max(value, 0);
        return new BattleRepeatAttackStageSpec(
            stage_index,
            stage_count,
            skill_level,
            stage_base_attack_bonus,
            follow_up_attack_penalty,
            penalty_free_stages,
            exponential_penalty,
            cost_resource_kind,
            normalizedBaseCost,
            ResolveResourceCostForStage(stage_index, normalizedBaseCost),
            follow_up_fixed_cost,
            follow_up_cost_addition,
            follow_up_cost_multiplier,
            stage_damage_multiplier_percent,
            stop_on_miss,
            fate_aware,
            stage_label
        );
    }

    public BattleRepeatAttackStageSpec WithFateAware(bool value)
    {
        return new BattleRepeatAttackStageSpec(
            stage_index,
            stage_count,
            skill_level,
            stage_base_attack_bonus,
            follow_up_attack_penalty,
            penalty_free_stages,
            exponential_penalty,
            cost_resource_kind,
            base_resource_cost,
            stage_resource_cost,
            follow_up_fixed_cost,
            follow_up_cost_addition,
            follow_up_cost_multiplier,
            stage_damage_multiplier_percent,
            stop_on_miss,
            value,
            stage_label
        );
    }

    public int ResolveResourceCostForStage(int stage_index_value)
    {
        return ResolveResourceCostForStage(stage_index_value, base_resource_cost);
    }

    private int ResolveResourceCostForStage(int stage_index_value, int base_cost)
    {
        int normalizedStageIndex = Mathf.Max(stage_index_value, 0);
        int normalizedBaseCost = Mathf.Max(base_cost, 0);
        if (normalizedStageIndex <= 0)
        {
            return normalizedBaseCost;
        }
        if (follow_up_fixed_cost > 0)
        {
            return follow_up_fixed_cost;
        }
        if (follow_up_cost_addition > 0)
        {
            return Mathf.Max(normalizedBaseCost + normalizedStageIndex * follow_up_cost_addition, 0);
        }
        return Mathf.Max(
            (int)Math.Round(
                normalizedBaseCost
                * Math.Pow(follow_up_cost_multiplier, normalizedStageIndex)
            ),
            0
        );
    }

    private static int ResolvePenaltyFreeStages(
        IReadOnlyDictionary<int, int> levelStagesMap,
        int skillLevel
    )
    {
        if (levelStagesMap == null || levelStagesMap.Count == 0)
        {
            return 0;
        }

        int resolvedStages = 0;
        int bestLevel = -1;
        foreach (KeyValuePair<int, int> entry in levelStagesMap)
        {
            int levelValue = entry.Key;
            if (levelValue <= skillLevel && levelValue > bestLevel)
            {
                bestLevel = levelValue;
                resolvedStages = entry.Value;
            }
        }
        return Mathf.Max(resolvedStages, 0);
    }

    private static int ResolveStageDamageMultiplierPercent(
        int followUpMultiplierPercent,
        int stageIndex
    )
    {
        int percent = 100;
        int normalizedMultiplier = Mathf.Max(followUpMultiplierPercent, 1);
        for (int stage = 0; stage < stageIndex; stage++)
        {
            percent = (int)Math.Clamp(
                (long)percent * normalizedMultiplier / 100L,
                1L,
                int.MaxValue
            );
        }
        return percent;
    }

}
