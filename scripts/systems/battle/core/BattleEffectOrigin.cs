using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleEffectOrigin
{
    private BattleEffectOrigin(
        StringName originKind,
        bool canTriggerContingencies,
        StringName ownerMemberId = default,
        StringName setupId = default,
        StringName instanceId = default,
        StringName skillEntryId = default,
        StringName storedSkillId = default,
        StringName triggerType = default
    )
    {
        OriginKind = Normalize(originKind);
        CanTriggerContingencies = canTriggerContingencies;
        OwnerMemberId = Normalize(ownerMemberId);
        SetupId = Normalize(setupId);
        InstanceId = Normalize(instanceId);
        SkillEntryId = Normalize(skillEntryId);
        StoredSkillId = Normalize(storedSkillId);
        TriggerType = Normalize(triggerType);
    }

    internal StringName OriginKind { get; }
    internal bool CanTriggerContingencies { get; }
    internal StringName OwnerMemberId { get; }
    internal StringName SetupId { get; }
    internal StringName InstanceId { get; }
    internal StringName SkillEntryId { get; }
    internal StringName StoredSkillId { get; }
    internal StringName TriggerType { get; }

    internal static BattleEffectOrigin PlayerCommand() =>
        new("player_command", canTriggerContingencies: true);

    internal static BattleEffectOrigin AutoCast(AutoCastRequest request) =>
        new(
            "contingency_auto_cast",
            canTriggerContingencies: false,
            request?.OwnerMemberId ?? "",
            request?.SetupId ?? "",
            request?.InstanceId ?? "",
            request?.SkillEntryId ?? "",
            request?.StoredSkillId ?? "",
            request?.ReleaseContext?.TriggerType ?? ""
        );

    internal GDictionary ToDictionary() =>
        new()
        {
            ["origin_kind"] = OriginKind.ToString(),
            ["can_trigger_contingencies"] = CanTriggerContingencies,
            ["owner_member_id"] = OwnerMemberId.ToString(),
            ["setup_id"] = SetupId.ToString(),
            ["instance_id"] = InstanceId.ToString(),
            ["skill_entry_id"] = SkillEntryId.ToString(),
            ["stored_skill_id"] = StoredSkillId.ToString(),
            ["trigger_type"] = TriggerType.ToString(),
        };

    private static StringName Normalize(StringName value) =>
        ProgressionDataUtils.to_string_name(value);
}
