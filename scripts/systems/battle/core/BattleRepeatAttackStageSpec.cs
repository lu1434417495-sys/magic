using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class BattleRepeatAttackStageSpec : RefCounted
{
    public int stage_index { get; set; }
    public int stage_count { get; set; }
    public int skill_level { get; set; }
    public int stage_base_attack_bonus { get; set; }
    public int follow_up_attack_penalty { get; set; }
    public int penalty_free_stages { get; set; }
    public bool exponential_penalty { get; set; }
    public bool fate_aware { get; set; }
    public StringName stage_label { get; set; } = "";

    public static BattleRepeatAttackStageSpec from_repeat_attack_effect(
        CombatEffectDef repeat_attack_effect,
        int stage_index_value,
        int stage_count_value,
        int skill_level_value,
        bool fate_aware_value = false
    )
    {
        var spec = new BattleRepeatAttackStageSpec
        {
            stage_index = Mathf.Max(stage_index_value, 0),
            stage_count = Mathf.Max(stage_count_value, 0),
            skill_level = Mathf.Max(skill_level_value, 0),
            fate_aware = fate_aware_value,
        };
        spec.stage_label = new StringName($"repeat_stage_{spec.stage_index}");

        GDictionary parameters = repeat_attack_effect?.@params ?? new GDictionary();
        if (repeat_attack_effect == null || parameters == null || parameters.Count == 0)
        {
            return spec;
        }

        spec.stage_base_attack_bonus = GdInterop.GetInt(parameters, "base_attack_bonus", 0);
        spec.follow_up_attack_penalty = Mathf.Max(
            GdInterop.GetInt(parameters, "follow_up_attack_penalty", 0),
            0
        );
        spec.exponential_penalty = GdInterop.GetBool(parameters, "exponential_penalty", false);
        spec.penalty_free_stages = ResolvePenaltyFreeStages(parameters, spec.skill_level);
        return spec;
    }

    public int resolve_stage_attack_penalty()
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

    private static int ResolvePenaltyFreeStages(GDictionary parameters, int skillLevel)
    {
        GDictionary levelStagesMap = GdInterop.GetDictionary(
            parameters,
            "penalty_free_stages_by_level"
        );
        if (levelStagesMap.Count == 0)
        {
            return 0;
        }

        int resolvedStages = 0;
        int bestLevel = -1;
        foreach (var levelKey in levelStagesMap.Keys)
        {
            int levelValue = levelKey.AsInt32();
            if (levelValue <= skillLevel && levelValue > bestLevel)
            {
                bestLevel = levelValue;
                resolvedStages = GdInterop.GetInt(levelStagesMap, levelKey, 0);
            }
        }
        return Mathf.Max(resolvedStages, 0);
    }
}
