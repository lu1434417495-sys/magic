using Godot;

[GlobalClass]
public partial class PendingCharacterRewardContentRules : RefCounted
{
    public static readonly StringName ENTRY_KNOWLEDGE_UNLOCK = "knowledge_unlock";

    public static readonly StringName ENTRY_SKILL_UNLOCK = "skill_unlock";

    public static readonly StringName ENTRY_SKILL_MASTERY = "skill_mastery";

    public static readonly StringName ENTRY_ATTRIBUTE_DELTA = "attribute_delta";

    public static readonly StringName ENTRY_ATTRIBUTE_PROGRESS = "attribute_progress";

    private static readonly Godot.Collections.Dictionary SUPPORTED_ENTRY_TYPES = new()
    {
        { ENTRY_KNOWLEDGE_UNLOCK, true },
        { ENTRY_SKILL_UNLOCK, true },
        { ENTRY_SKILL_MASTERY, true },
        { ENTRY_ATTRIBUTE_DELTA, true },
        { ENTRY_ATTRIBUTE_PROGRESS, true },
    };

    private static readonly Godot.Collections.Dictionary SKILL_TARGET_ENTRY_TYPES = new()
    {
        { ENTRY_SKILL_UNLOCK, true },
        { ENTRY_SKILL_MASTERY, true },
    };

    public static StringName normalize_string_name(StringName value) => value;

    public static bool is_supported_entry_type(StringName value) =>
        SUPPORTED_ENTRY_TYPES.ContainsKey(value);

    public static bool requires_skill_target(StringName value) =>
        SKILL_TARGET_ENTRY_TYPES.ContainsKey(value);

    public static bool is_attribute_progress_entry(StringName value) =>
        value == ENTRY_ATTRIBUTE_PROGRESS;

    public static bool is_attribute_delta_entry(StringName value) => value == ENTRY_ATTRIBUTE_DELTA;

    public static bool is_valid_attribute_progress_target(StringName value) =>
        AttributeGrowthContentRules.is_valid_attribute_id(value);

    public static string valid_entry_type_label()
    {
        var labels = new Godot.Collections.Array<string>();

        foreach (var entryType in SUPPORTED_ENTRY_TYPES.Keys)
            labels.Add(entryType.AsString());

        labels.Sort();

        return string.Join(", ", labels);
    }
}
