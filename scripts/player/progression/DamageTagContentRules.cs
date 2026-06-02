using Godot;
using System;
using System.Collections.Generic;

public static class DamageTagContentRules
{
    public static readonly StringName DAMAGE_TAG_PHYSICAL_SLASH = "physical_slash";

    public static readonly StringName DAMAGE_TAG_PHYSICAL_PIERCE = "physical_pierce";

    public static readonly StringName DAMAGE_TAG_PHYSICAL_BLUNT = "physical_blunt";

    private static readonly HashSet<StringName> VALID_DAMAGE_TAGS = new()
    {
        DAMAGE_TAG_PHYSICAL_SLASH,
        DAMAGE_TAG_PHYSICAL_PIERCE,
        DAMAGE_TAG_PHYSICAL_BLUNT,
        "fire",
        "freeze",
        "lightning",
        "negative_energy",
        "force",
        "psychic",
        "radiant",
        "thunder",
        "magic",
        "acid",
        "poison",
    };

    private static readonly HashSet<StringName> VALID_PHYSICAL_DAMAGE_TAGS = new()
    {
        DAMAGE_TAG_PHYSICAL_SLASH,
        DAMAGE_TAG_PHYSICAL_PIERCE,
        DAMAGE_TAG_PHYSICAL_BLUNT,
    };

    private static readonly HashSet<StringName> VALID_MITIGATION_TIERS = new()
    {
        "normal",
        "half",
        "double",
        "immune",
    };

    private static readonly HashSet<StringName> VALID_DAMAGE_CATEGORIES = new()
    {
        "physical",
        "spell",
        "magic",
        "energy",
    };

    public static StringName normalize_string_name(StringName value) => value;

    public static bool is_valid_damage_tag(StringName value)
    {
        return VALID_DAMAGE_TAGS.Contains(value);
    }

    public static bool is_valid_physical_damage_tag(StringName value)
    {
        return VALID_PHYSICAL_DAMAGE_TAGS.Contains(value);
    }

    public static bool is_valid_mitigation_tier(StringName value)
    {
        return VALID_MITIGATION_TIERS.Contains(value);
    }

    public static bool is_valid_damage_category(StringName value)
    {
        return VALID_DAMAGE_CATEGORIES.Contains(value);
    }

    public static string valid_damage_tag_label()
    {
        return _sorted_key_label(VALID_DAMAGE_TAGS);
    }

    public static string valid_mitigation_tier_label()
    {
        return _sorted_key_label(VALID_MITIGATION_TIERS);
    }

    public static string valid_damage_category_label()
    {
        return _sorted_key_label(VALID_DAMAGE_CATEGORIES);
    }

    private static string _sorted_key_label(HashSet<StringName> source)
    {
        var labels = new List<string>();

        foreach (var key in source)
            labels.Add(key.ToString());

        labels.Sort(StringComparer.Ordinal);

        return string.Join(", ", labels);
    }
}
