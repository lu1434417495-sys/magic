using Godot;

[GlobalClass]
public partial class CombatSkillTargetingContentRules : RefCounted
{
    public static readonly StringName TARGET_MODE_UNIT = "unit";

    public static readonly StringName TARGET_MODE_GROUND = "ground";

    private static readonly Godot.Collections.Dictionary VALID_COMBAT_TARGET_MODES = new()
    {
        { TARGET_MODE_UNIT, true },
        { TARGET_MODE_GROUND, true },
    };

    private static readonly Godot.Collections.Dictionary VALID_CAST_OPTION_TARGET_MODES = new()
    {
        { TARGET_MODE_UNIT, true },
        { TARGET_MODE_GROUND, true },
    };

    private static readonly Godot.Collections.Dictionary VALID_TARGET_SELECTION_MODES = new()
    {
        { "single_unit", true },
        { "multi_unit", true },
        { "random_chain", true },
        { "self", true },
        { "single_coord", true },
        { "coord_pair", true },
    };

    private static readonly Godot.Collections.Dictionary VALID_SELECTION_ORDER_MODES = new()
    {
        { "stable", true },
        { "manual", true },
    };

    private static readonly Godot.Collections.Dictionary VALID_AREA_PATTERNS = new()
    {
        { "single", true },
        { "self", true },
        { "diamond", true },
        { "square", true },
        { "radius", true },
        { "cross", true },
        { "line", true },
        { "cone", true },
        { "narrow_cone", true },
        { "front_arc", true },
    };

    private static readonly Godot.Collections.Dictionary VALID_FOOTPRINT_PATTERNS = new()
    {
        { "single", true },
        { "line2", true },
        { "square2", true },
        { "unordered", true },
    };

    public static bool is_valid_combat_target_mode(StringName value)
    {
        return VALID_COMBAT_TARGET_MODES.ContainsKey(value);
    }

    public static bool is_valid_cast_variant_target_mode(StringName value)
    {
        return VALID_CAST_OPTION_TARGET_MODES.ContainsKey(value);
    }

    public static bool is_valid_target_selection_mode(StringName value)
    {
        return VALID_TARGET_SELECTION_MODES.ContainsKey(value);
    }

    public static bool is_valid_selection_order_mode(StringName value)
    {
        return VALID_SELECTION_ORDER_MODES.ContainsKey(value);
    }

    public static bool is_valid_area_pattern(StringName value)
    {
        return VALID_AREA_PATTERNS.ContainsKey(value);
    }

    public static bool is_valid_footprint_pattern(StringName value)
    {
        return VALID_FOOTPRINT_PATTERNS.ContainsKey(value);
    }

    public static string valid_combat_target_mode_label()
    {
        return _sorted_key_label(VALID_COMBAT_TARGET_MODES);
    }

    public static string valid_cast_variant_target_mode_label()
    {
        return _sorted_key_label(VALID_CAST_OPTION_TARGET_MODES);
    }

    public static string valid_target_selection_mode_label()
    {
        return _sorted_key_label(VALID_TARGET_SELECTION_MODES);
    }

    public static string valid_selection_order_mode_label()
    {
        return _sorted_key_label(VALID_SELECTION_ORDER_MODES);
    }

    public static string valid_area_pattern_label()
    {
        return _sorted_key_label(VALID_AREA_PATTERNS);
    }

    public static string valid_footprint_pattern_label()
    {
        return _sorted_key_label(VALID_FOOTPRINT_PATTERNS);
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
