using Godot;

[GlobalClass]
public partial class DamageTagContentRules : RefCounted
{
    public static readonly StringName DAMAGE_TAG_PHYSICAL_SLASH = "physical_slash";

    public static readonly StringName DAMAGE_TAG_PHYSICAL_PIERCE = "physical_pierce";

    public static readonly StringName DAMAGE_TAG_PHYSICAL_BLUNT = "physical_blunt";

    private static readonly Godot.Collections.Dictionary VALID_DAMAGE_TAGS = new()
    {
        { DAMAGE_TAG_PHYSICAL_SLASH, true },
        { DAMAGE_TAG_PHYSICAL_PIERCE, true },
        { DAMAGE_TAG_PHYSICAL_BLUNT, true },
        { "fire", true },
        { "freeze", true },
        { "lightning", true },
        { "negative_energy", true },
        { "force", true },
        { "psychic", true },
        { "radiant", true },
        { "thunder", true },
        { "magic", true },
        { "acid", true },
        { "poison", true },
    };

    private static readonly Godot.Collections.Dictionary VALID_PHYSICAL_DAMAGE_TAGS = new()
    {
        { DAMAGE_TAG_PHYSICAL_SLASH, true },
        { DAMAGE_TAG_PHYSICAL_PIERCE, true },
        { DAMAGE_TAG_PHYSICAL_BLUNT, true },
    };

    private static readonly Godot.Collections.Dictionary VALID_MITIGATION_TIERS = new()
    {
        { "normal", true },
        { "half", true },
        { "double", true },
        { "immune", true },
    };

    private static readonly Godot.Collections.Dictionary VALID_DAMAGE_CATEGORIES = new()
    {
        { "physical", true },
        { "spell", true },
        { "magic", true },
        { "energy", true },
    };

    public static StringName normalize_string_name(StringName value) => value;

    public static bool is_valid_damage_tag(StringName value)
    {
        return VALID_DAMAGE_TAGS.ContainsKey(value);
    }

    public static bool is_valid_physical_damage_tag(StringName value)
    {
        return VALID_PHYSICAL_DAMAGE_TAGS.ContainsKey(value);
    }

    public static bool is_valid_mitigation_tier(StringName value)
    {
        return VALID_MITIGATION_TIERS.ContainsKey(value);
    }

    public static bool is_valid_damage_category(StringName value)
    {
        return VALID_DAMAGE_CATEGORIES.ContainsKey(value);
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

    private static string _sorted_key_label(Godot.Collections.Dictionary source)
    {
        var labels = new Godot.Collections.Array<string>();

        foreach (var key in source.Keys)
            labels.Add(key.AsString());

        labels.Sort();

        return string.Join(", ", labels);
    }
}
