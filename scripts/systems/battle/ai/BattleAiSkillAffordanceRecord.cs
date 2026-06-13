using System.Collections.Generic;
using Godot;

internal sealed class BattleAiSkillAffordanceRecord
{
    public StringName skill_id = "";
    public bool is_generatable;
    public string skip_reason = "";
    public StringName team_intent = "";
    public BattleTargetMode target_mode = BattleTargetMode.Unknown;
    public BattleTargetFilter target_filter = BattleTargetFilter.Unknown;
    public BattleTargetSelectionMode selection_mode = BattleTargetSelectionMode.Unknown;
    public List<StringName> effect_roles = new();
    public List<StringName> affordances = new();
    public List<StringName> action_families = new();
    public bool requires_positioning_action;
    public List<StringName> variant_ids = new();
    public string blocked_reason = "";

    public static BattleAiSkillAffordanceRecord Empty(StringName skillId)
    {
        return new BattleAiSkillAffordanceRecord
        {
            skill_id = ProgressionDataUtils.to_string_name(skillId),
        };
    }

    public BattleAiSkillAffordanceRecord Clone()
    {
        return new BattleAiSkillAffordanceRecord
        {
            skill_id = skill_id,
            is_generatable = is_generatable,
            skip_reason = skip_reason,
            team_intent = team_intent,
            target_mode = target_mode,
            target_filter = target_filter,
            selection_mode = selection_mode,
            effect_roles = new List<StringName>(effect_roles),
            affordances = new List<StringName>(affordances),
            action_families = new List<StringName>(action_families),
            requires_positioning_action = requires_positioning_action,
            variant_ids = new List<StringName>(variant_ids),
            blocked_reason = blocked_reason,
        };
    }

    public void AddEffectRole(StringName value) => AddUnique(effect_roles, value);

    public void AddAffordance(StringName value) => AddUnique(affordances, value);

    public void AddActionFamily(StringName value) => AddUnique(action_families, value);

    public void AddVariantId(StringName value) => AddUnique(variant_ids, value);

    public bool HasActionFamily(StringName value)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        return normalized != "" && action_families.Contains(normalized);
    }

    public bool HasAnyActionFamily(IEnumerable<StringName> values)
    {
        if (values == null)
        {
            return false;
        }
        foreach (StringName value in values)
        {
            if (HasActionFamily(value))
            {
                return true;
            }
        }
        return false;
    }

    private static void AddUnique(List<StringName> target, StringName value)
    {
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        if (normalized == "" || target.Contains(normalized))
        {
            return;
        }
        target.Add(normalized);
    }
}
