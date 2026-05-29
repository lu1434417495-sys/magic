using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;

[GlobalClass]
public partial class BattleEffectCategoryResolver : RefCounted
{
    public Godot.Collections.Array<StringName> ResolveCategories(
        GodotObject skill_def,
        GArray effect_defs
    )
    {
        var categories = new Godot.Collections.Array<StringName>();
        var seen = new HashSet<StringName>();

        GodotObject combatProfile = GdInterop.GetObject(skill_def, "combat_profile");
        if (combatProfile != null)
        {
            AppendCategories(categories, seen, combatProfile.Get("delivery_categories"));
        }

        if (effect_defs == null)
        {
            return categories;
        }

        foreach (var effectValue in effect_defs)
        {
            GodotObject effect = effectValue.AsGodotObject();
            if (effect == null)
            {
                continue;
            }
            AppendCategories(categories, seen, effect.Get("effect_categories"));
        }

        return categories;
    }

    private static void AppendCategories(
        Godot.Collections.Array<StringName> categories,
        HashSet<StringName> seen,
        object rawValues
    )
    {
        if (rawValues is not Variant value || value.VariantType != Variant.Type.Array)
        {
            return;
        }

        foreach (var rawValue in value.AsGodotArray())
        {
            StringName category = GdInterop.ToStringName(rawValue);
            if (GdInterop.IsEmpty(category) || seen.Contains(category))
            {
                continue;
            }
            seen.Add(category);
            categories.Add(category);
        }
    }
}
