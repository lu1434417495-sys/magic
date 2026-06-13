using System.Collections;
using System.Collections.Generic;
using Godot;

public static class BattleEffectCategoryResolver
{
    public static IReadOnlyList<StringName> ResolveCategories(
        SkillDef skillDef,
        IEnumerable<CombatEffectDef> effectDefs
    )
    {
        var categories = new List<StringName>();
        var seen = new HashSet<StringName>();

        CombatSkillDef combatProfile = skillDef?.combat_profile;
        if (combatProfile != null)
        {
            AppendCategories(categories, seen, combatProfile.delivery_categories);
        }

        if (effectDefs == null)
        {
            return categories;
        }

        foreach (CombatEffectDef effect in effectDefs)
        {
            if (effect == null)
            {
                continue;
            }
            AppendCategories(categories, seen, effect.effect_categories);
        }

        return categories;
    }

    private static void AppendCategories(
        List<StringName> categories,
        HashSet<StringName> seen,
        IEnumerable rawValues
    )
    {
        if (rawValues == null)
        {
            return;
        }

        foreach (object rawValue in rawValues)
        {
            StringName category = ProgressionDataUtils.to_string_name(rawValue);
            if (IsEmpty(category) || !seen.Add(category))
            {
                continue;
            }
            categories.Add(category);
        }
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
    }
}
