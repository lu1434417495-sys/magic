using Godot;
using System;
using System.Collections.Generic;

internal enum PendingCharacterRewardEntryKind
{
    Unknown = 0,
    KnowledgeUnlock,
    SkillUnlock,
    SkillMastery,
    AttributeDelta,
    AttributeProgress,
}

internal static class PendingCharacterRewardContentRules
{
    private static readonly StringName EntryKnowledgeUnlock = "knowledge_unlock";

    private static readonly StringName EntrySkillUnlock = "skill_unlock";

    private static readonly StringName EntrySkillMastery = "skill_mastery";

    private static readonly StringName EntryAttributeDelta = "attribute_delta";

    private static readonly StringName EntryAttributeProgress = "attribute_progress";

    internal static bool IsSupportedEntryType(StringName value) =>
        ToEntryKind(value) != PendingCharacterRewardEntryKind.Unknown;

    internal static bool RequiresSkillTarget(StringName value) =>
        ToEntryKind(value)
        is PendingCharacterRewardEntryKind.SkillUnlock
            or PendingCharacterRewardEntryKind.SkillMastery;

    internal static bool IsAttributeProgressEntry(StringName value) =>
        ToEntryKind(value) == PendingCharacterRewardEntryKind.AttributeProgress;

    internal static bool IsAttributeDeltaEntry(StringName value) =>
        ToEntryKind(value) == PendingCharacterRewardEntryKind.AttributeDelta;

    internal static bool IsValidAttributeProgressTarget(StringName value) =>
        AttributeGrowthContentRules.IsValidAttributeId(value);

    internal static string ValidEntryTypeLabel()
    {
        var labels = new List<string>
        {
            EntryAttributeDelta.ToString(),
            EntryAttributeProgress.ToString(),
            EntryKnowledgeUnlock.ToString(),
            EntrySkillMastery.ToString(),
            EntrySkillUnlock.ToString(),
        };

        labels.Sort(StringComparer.Ordinal);

        return string.Join(", ", labels);
    }

    internal static PendingCharacterRewardEntryKind ToEntryKind(StringName value)
    {
        if (value == EntryKnowledgeUnlock)
            return PendingCharacterRewardEntryKind.KnowledgeUnlock;
        if (value == EntrySkillUnlock)
            return PendingCharacterRewardEntryKind.SkillUnlock;
        if (value == EntrySkillMastery)
            return PendingCharacterRewardEntryKind.SkillMastery;
        if (value == EntryAttributeDelta)
            return PendingCharacterRewardEntryKind.AttributeDelta;
        if (value == EntryAttributeProgress)
            return PendingCharacterRewardEntryKind.AttributeProgress;
        return PendingCharacterRewardEntryKind.Unknown;
    }

    internal static StringName ToStringName(PendingCharacterRewardEntryKind value)
    {
        return value switch
        {
            PendingCharacterRewardEntryKind.KnowledgeUnlock => EntryKnowledgeUnlock,
            PendingCharacterRewardEntryKind.SkillUnlock => EntrySkillUnlock,
            PendingCharacterRewardEntryKind.SkillMastery => EntrySkillMastery,
            PendingCharacterRewardEntryKind.AttributeDelta => EntryAttributeDelta,
            PendingCharacterRewardEntryKind.AttributeProgress => EntryAttributeProgress,
            _ => "",
        };
    }
}
