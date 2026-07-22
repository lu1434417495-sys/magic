using System.Collections;
using System.Collections.Generic;
using Godot;

public static class BattleEffectCategoryResolver
{
    public static IReadOnlyList<StringName> ResolveCategories(
        SkillDefinition skillDefinition,
        IEnumerable<CombatEffectDefinition> effectDefinitions,
        IEnumerable<StringName> additionalCategories = null
    )
    {
        var categories = new List<StringName>();
        var seen = new HashSet<StringName>();

        CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
        if (combatProfile != null)
        {
            AppendCategories(categories, seen, combatProfile.DeliveryCategories);
        }

        if (effectDefinitions != null)
        {
            foreach (CombatEffectDefinition effect in effectDefinitions)
            {
                if (effect == null)
                {
                    continue;
                }
                AppendCategories(categories, seen, effect.EffectCategories);
            }
        }

        AppendCategories(categories, seen, additionalCategories);

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
