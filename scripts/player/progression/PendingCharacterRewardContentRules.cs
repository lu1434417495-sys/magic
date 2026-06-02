using Godot;
using System;
using System.Collections.Generic;

public static class PendingCharacterRewardContentRules
{
    public static readonly StringName ENTRY_KNOWLEDGE_UNLOCK = "knowledge_unlock";

    public static readonly StringName ENTRY_SKILL_UNLOCK = "skill_unlock";

    public static readonly StringName ENTRY_SKILL_MASTERY = "skill_mastery";

    public static readonly StringName ENTRY_ATTRIBUTE_DELTA = "attribute_delta";

    public static readonly StringName ENTRY_ATTRIBUTE_PROGRESS = "attribute_progress";

    private static readonly HashSet<StringName> SUPPORTED_ENTRY_TYPES = new()
    {
        ENTRY_KNOWLEDGE_UNLOCK,
        ENTRY_SKILL_UNLOCK,
        ENTRY_SKILL_MASTERY,
        ENTRY_ATTRIBUTE_DELTA,
        ENTRY_ATTRIBUTE_PROGRESS,
    };

    private static readonly HashSet<StringName> SKILL_TARGET_ENTRY_TYPES = new()
    {
        ENTRY_SKILL_UNLOCK,
        ENTRY_SKILL_MASTERY,
    };

    public static StringName normalize_string_name(StringName value) => value;

    public static bool is_supported_entry_type(StringName value) =>
        SUPPORTED_ENTRY_TYPES.Contains(value);

    public static bool requires_skill_target(StringName value) =>
        SKILL_TARGET_ENTRY_TYPES.Contains(value);

    public static bool is_attribute_progress_entry(StringName value) =>
        value == ENTRY_ATTRIBUTE_PROGRESS;

    public static bool is_attribute_delta_entry(StringName value) => value == ENTRY_ATTRIBUTE_DELTA;

    public static bool is_valid_attribute_progress_target(StringName value) =>
        AttributeGrowthContentRules.is_valid_attribute_id(value);

    public static string valid_entry_type_label()
    {
        var labels = new List<string>();

        foreach (var entryType in SUPPORTED_ENTRY_TYPES)
            labels.Add(entryType.ToString());

        labels.Sort(StringComparer.Ordinal);

        return string.Join(", ", labels);
    }
}
