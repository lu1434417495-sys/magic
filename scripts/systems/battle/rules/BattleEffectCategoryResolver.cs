using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class BattleEffectCategoryResolver : RefCounted
{
    public Godot.Collections.Array<StringName> ResolveCategories(
        SkillDef skill_def,
        GArray effect_defs
    )
    {
        var categories = new Godot.Collections.Array<StringName>();
        var seen = new HashSet<StringName>();

        CombatSkillDef combatProfile = skill_def?.combat_profile;
        if (combatProfile != null)
        {
            AppendCategories(categories, seen, combatProfile.delivery_categories);
        }

        if (effect_defs == null)
        {
            return categories;
        }

        foreach (var effectValue in effect_defs)
        {
            CombatEffectDef effect = effectValue.As<CombatEffectDef>();
            if (effect == null)
            {
                continue;
            }
            AppendCategories(categories, seen, effect.effect_categories);
        }

        return categories;
    }

    private static void AppendCategories(
        Godot.Collections.Array<StringName> categories,
        HashSet<StringName> seen,
        Godot.Collections.Array<StringName> rawValues
    )
    {
        if (rawValues == null)
        {
            return;
        }

        foreach (StringName category in rawValues)
        {
            if (IsEmpty(category) || seen.Contains(category))
            {
                continue;
            }
            seen.Add(category);
            categories.Add(category);
        }
    }

    private static void AppendCategories(
        Godot.Collections.Array<StringName> categories,
        HashSet<StringName> seen,
        GArray rawValues
    )
    {
        if (rawValues == null)
        {
            return;
        }

        foreach (var rawValue in rawValues)
        {
            StringName category = ToStringName(rawValue);
            if (IsEmpty(category) || seen.Contains(category))
            {
                continue;
            }
            seen.Add(category);
            categories.Add(category);
        }
    }

    private static StringName ToStringName<TValue>(TValue value) =>
        ProgressionDataUtils.to_string_name(value);

    private static bool IsEmpty(StringName value)
    {
        return value == null || value == "";
    }
}
