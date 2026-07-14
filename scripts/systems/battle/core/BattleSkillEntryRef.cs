using Godot;

internal enum BattleSkillEntrySourceKind
{
    KnownSkill,
    EquipmentSkill,
    ScopedAutoCast,
}

internal readonly record struct BattleSkillEntryRef(
    StringName SkillEntryId,
    StringName SkillId,
    BattleSkillEntrySourceKind SourceKind,
    StringName SourceEquipmentInstanceId,
    StringName SourceEquipmentEffectiveInstanceKey = default
);

internal static class BattleSkillEntryIds
{
    internal static StringName KnownSkill(StringName skillId)
    {
        string normalizedSkillId = ToText(skillId);
        return string.IsNullOrEmpty(normalizedSkillId)
            ? new StringName("")
            : new StringName($"known_skill:{normalizedSkillId}");
    }

    internal static StringName EquipmentSkill(
        StringName bindingId,
        StringName sourceEquipmentInstanceId,
        StringName effectiveInstanceKey,
        StringName grantedActionId,
        StringName skillId
    )
    {
        return new StringName(
            $"equipment_skill:{ToText(bindingId)}:{ToText(sourceEquipmentInstanceId)}:{ToText(effectiveInstanceKey)}:{ToText(grantedActionId)}:{ToText(skillId)}"
        );
    }

    internal static StringName ScopedAutoCast(StringName scopeId, StringName skillId)
    {
        return new StringName($"scoped_auto:{ToText(scopeId)}:{ToText(skillId)}");
    }

    private static string ToText(StringName value) =>
        ProgressionDataUtils.to_string_name(value).ToString();
}
