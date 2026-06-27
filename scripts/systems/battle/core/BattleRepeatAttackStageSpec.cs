using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

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
        IReadOnlyDictionary<string, Variant> parameters = repeat_attack_effect?.Parameters;
        if (repeat_attack_effect == null || parameters == null || parameters.Count == 0)
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
                fate_aware_value,
                new StringName($"repeat_stage_{stageIndex}")
            );
        }

        return new BattleRepeatAttackStageSpec(
            stageIndex,
            stage_count_value,
            skill_level_value,
            ReadInt(parameters, "base_attack_bonus"),
            ReadInt(parameters, "follow_up_attack_penalty"),
            ResolvePenaltyFreeStages(parameters, skill_level_value),
            ReadBool(parameters, "exponential_penalty"),
            CombatResourceKindUtils.FromStringName(
                ReadStringName(parameters, "cost_resource", "aura"),
                CombatResourceKind.Aura
            ),
            0,
            0,
            ReadInt(parameters, "follow_up_fixed_cost"),
            ReadInt(parameters, "follow_up_cost_addition"),
            ReadFloat(parameters, "follow_up_cost_multiplier", 1.0),
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
        if (exponential_penalty)
        {
            return (int)Mathf.Pow(2, stage_index) * follow_up_attack_penalty;
        }
        return Mathf.Max(stage_index, 0) * follow_up_attack_penalty;
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
        IReadOnlyDictionary<string, Variant> parameters,
        int skillLevel
    )
    {
        GDictionary levelStagesMap = ReadDictionary(parameters, "penalty_free_stages_by_level");
        if (levelStagesMap.Count == 0)
        {
            return 0;
        }

        int resolvedStages = 0;
        int bestLevel = -1;
        foreach (var levelKey in levelStagesMap.Keys)
        {
            int levelValue = ReadInt(levelKey, -1);
            if (levelValue <= skillLevel && levelValue > bestLevel)
            {
                bestLevel = levelValue;
                resolvedStages = ReadInt(levelStagesMap, levelKey);
            }
        }
        return Mathf.Max(resolvedStages, 0);
    }

    private static GDictionary ReadDictionary(
        IReadOnlyDictionary<string, Variant> data,
        string key
    )
    {
        if (data == null || !data.TryGetValue(key, out Variant value))
            return new GDictionary();
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static int ReadInt(GDictionary data, int key, int fallback = 0)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        return data[key].AsInt32();
    }

    private static int ReadInt(GDictionary data, Variant key, int fallback = 0)
    {
        if (data == null || !data.ContainsKey(key))
            return fallback;
        return ReadInt(data[key], fallback);
    }

    private static int ReadInt(Variant value, int fallback = 0)
    {
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => Mathf.RoundToInt((float)value.AsDouble()),
            Variant.Type.String => int.TryParse(value.AsString(), out int parsed)
                ? parsed
                : fallback,
            Variant.Type.StringName => int.TryParse(value.AsStringName().ToString(), out int parsed)
                ? parsed
                : fallback,
            _ => fallback,
        };
    }

    private static int ReadInt(
        IReadOnlyDictionary<string, Variant> data,
        string key,
        int fallback = 0
    )
    {
        if (data == null || !data.TryGetValue(key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt32(),
            Variant.Type.Float => Mathf.RoundToInt((float)value.AsDouble()),
            _ => fallback,
        };
    }

    private static double ReadFloat(
        IReadOnlyDictionary<string, Variant> data,
        string key,
        double fallback = 0.0
    )
    {
        if (data == null || !data.TryGetValue(key, out Variant value))
            return fallback;
        return value.VariantType switch
        {
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            _ => fallback,
        };
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, Variant> data,
        string key,
        bool fallback = false
    )
    {
        if (data == null || !data.TryGetValue(key, out Variant value))
            return fallback;
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static StringName ReadStringName(
        IReadOnlyDictionary<string, Variant> data,
        string key,
        StringName fallback = default
    )
    {
        if (data == null)
            return fallback ?? new StringName("");
        if (data.TryGetValue(key, out Variant value))
            return ProgressionDataUtils.to_string_name(value);
        return fallback ?? new StringName("");
    }
}
