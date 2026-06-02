using Godot;
using System;
using System.Collections.Generic;

public static class CombatSkillTargetingContentRules
{
    public static readonly StringName TARGET_MODE_UNIT = "unit";

    public static readonly StringName TARGET_MODE_GROUND = "ground";

    private static readonly HashSet<StringName> VALID_COMBAT_TARGET_MODES = new()
    {
        TARGET_MODE_UNIT,
        TARGET_MODE_GROUND,
    };

    private static readonly HashSet<StringName> VALID_CAST_OPTION_TARGET_MODES = new()
    {
        TARGET_MODE_UNIT,
        TARGET_MODE_GROUND,
    };

    private static readonly HashSet<StringName> VALID_TARGET_SELECTION_MODES = new()
    {
        "single_unit",
        "multi_unit",
        "random_chain",
        "self",
        "single_coord",
        "coord_pair",
    };

    private static readonly HashSet<StringName> VALID_SELECTION_ORDER_MODES = new()
    {
        "stable",
        "manual",
    };

    private static readonly HashSet<StringName> VALID_AREA_PATTERNS = new()
    {
        "single",
        "self",
        "diamond",
        "square",
        "radius",
        "cross",
        "line",
        "cone",
        "narrow_cone",
        "front_arc",
    };

    private static readonly HashSet<StringName> VALID_FOOTPRINT_PATTERNS = new()
    {
        "single",
        "line2",
        "square2",
        "unordered",
    };

    public static bool is_valid_combat_target_mode(StringName value)
    {
        return VALID_COMBAT_TARGET_MODES.Contains(value);
    }

    public static bool is_valid_cast_variant_target_mode(StringName value)
    {
        return VALID_CAST_OPTION_TARGET_MODES.Contains(value);
    }

    public static bool is_valid_target_selection_mode(StringName value)
    {
        return VALID_TARGET_SELECTION_MODES.Contains(value);
    }

    public static bool is_valid_selection_order_mode(StringName value)
    {
        return VALID_SELECTION_ORDER_MODES.Contains(value);
    }

    public static bool is_valid_area_pattern(StringName value)
    {
        return VALID_AREA_PATTERNS.Contains(value);
    }

    public static bool is_valid_footprint_pattern(StringName value)
    {
        return VALID_FOOTPRINT_PATTERNS.Contains(value);
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

    private static string _sorted_key_label(HashSet<StringName> source)
    {
        var labels = new List<string>();

        foreach (var key in source)
            labels.Add(key.ToString());

        labels.Sort(StringComparer.Ordinal);

        return string.Join(", ", labels);
    }
}
