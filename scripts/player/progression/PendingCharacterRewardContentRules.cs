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
        { ENTRY_KNOWLEDGE_UNLOCK, true }, { ENTRY_SKILL_UNLOCK, true }, { ENTRY_SKILL_MASTERY, true },
        { ENTRY_ATTRIBUTE_DELTA, true }, { ENTRY_ATTRIBUTE_PROGRESS, true },
    };

    private static readonly Godot.Collections.Dictionary SKILL_TARGET_ENTRY_TYPES = new()
    {
        { ENTRY_SKILL_UNLOCK, true }, { ENTRY_SKILL_MASTERY, true },
    };

    public static StringName normalize_string_name(Variant value)
    {
        if (value.VariantType == Variant.Type.StringName) return value.AsStringName();
        if (value.VariantType == Variant.Type.String)
        {
            string text = value.AsString().StripEdges();
            return text.Length > 0 ? new StringName(text) : new StringName("");
        }
        return new StringName("");
    }

    public static bool is_supported_entry_type(Variant value) => SUPPORTED_ENTRY_TYPES.ContainsKey(normalize_string_name(value));
    public static bool requires_skill_target(Variant value) => SKILL_TARGET_ENTRY_TYPES.ContainsKey(normalize_string_name(value));
    public static bool is_attribute_progress_entry(Variant value) => normalize_string_name(value) == ENTRY_ATTRIBUTE_PROGRESS;
    public static bool is_attribute_delta_entry(Variant value) => normalize_string_name(value) == ENTRY_ATTRIBUTE_DELTA;
    public static bool is_valid_attribute_progress_target(Variant value) => AttributeGrowthContentRules.is_valid_attribute_id(normalize_string_name(value));

    public static string valid_entry_type_label()
    {
        var labels = new Godot.Collections.Array<string>();
        foreach (var entryType in SUPPORTED_ENTRY_TYPES.Keys)
            labels.Add(entryType.AsString());
        labels.Sort();
        return string.Join(", ", labels);
    }
}
