using System;
using System.Collections.Generic;
using Godot;

internal sealed class BattleEffectOrigin
{
    private BattleEffectOrigin(
        StringName originKind,
        bool canTriggerContingencies,
        bool canTriggerReactions,
        StringName ownerMemberId = default,
        StringName setupId = default,
        StringName instanceId = default,
        StringName skillEntryId = default,
        StringName storedSkillId = default,
        StringName triggerType = default,
        long triggeringAttackActionId = 0
    )
    {
        OriginKind = Normalize(originKind);
        if (OriginKind == new StringName(""))
            throw new ArgumentException("origin kind is required");
        if (triggeringAttackActionId < 0)
            throw new ArgumentOutOfRangeException(
                nameof(triggeringAttackActionId)
            );
        CanTriggerContingencies = canTriggerContingencies;
        CanTriggerReactions = canTriggerReactions;
        OwnerMemberId = Normalize(ownerMemberId);
        SetupId = Normalize(setupId);
        InstanceId = Normalize(instanceId);
        SkillEntryId = Normalize(skillEntryId);
        StoredSkillId = Normalize(storedSkillId);
        TriggerType = Normalize(triggerType);
        TriggeringAttackActionId = triggeringAttackActionId;
    }

    internal StringName OriginKind { get; }
    internal bool CanTriggerContingencies { get; }
    internal bool IsEquipmentGenerated =>
        OriginKind == "equipment_ability" || OriginKind == "equipment_direct_reaction";
    internal bool IsEquipmentDirectReaction => OriginKind == "equipment_direct_reaction";
    internal bool CanTriggerReactions { get; }
    internal StringName OwnerMemberId { get; }
    internal StringName SetupId { get; }
    internal StringName InstanceId { get; }
    internal StringName SkillEntryId { get; }
    internal StringName StoredSkillId { get; }
    internal StringName TriggerType { get; }
    internal long TriggeringAttackActionId { get; }

    internal static BattleEffectOrigin PlayerCommand() =>
        new(
            "player_command",
            canTriggerContingencies: true,
            canTriggerReactions: true
        );

    internal static BattleEffectOrigin EquipmentAbility() =>
        new("equipment_ability", canTriggerContingencies: false);

    internal static BattleEffectOrigin EquipmentDirectReaction() =>
        new("equipment_direct_reaction", canTriggerContingencies: false);

    internal static BattleEffectOrigin AutoCast(AutoCastRequest request) =>
        new(
            "contingency_auto_cast",
            canTriggerContingencies: false,
            canTriggerReactions: true,
            ownerMemberId: request?.OwnerMemberId ?? "",
            setupId: request?.SetupId ?? "",
            instanceId: request?.InstanceId ?? "",
            skillEntryId: request?.SkillEntryId ?? "",
            storedSkillId: request?.StoredSkillId ?? "",
            triggerType: request?.ReleaseContext?.TriggerType ?? ""
        );

    internal static BattleEffectOrigin Timeline(StringName segmentKind)
    {
        if (segmentKind == new StringName(""))
            throw new ArgumentException("timeline segment kind is required");
        return new BattleEffectOrigin(
            "timeline",
            canTriggerContingencies: true,
            canTriggerReactions: true,
            triggerType: segmentKind
        );
    }

    internal static BattleEffectOrigin Counterattack(
        BattleAttackActionId triggeringActionId,
        StringName capabilityInstanceId
    )
    {
        if (!triggeringActionId.IsValid)
            throw new ArgumentException("triggering action id is invalid");
        if (capabilityInstanceId == new StringName(""))
            throw new ArgumentException("capability instance id is required");
        return new BattleEffectOrigin(
            "counterattack",
            canTriggerContingencies: true,
            canTriggerReactions: false,
            instanceId: capabilityInstanceId,
            triggerType: "counterattack",
            triggeringAttackActionId: triggeringActionId.Value
        );
    }

    internal Dictionary<string, object> ToPlainDictionary() =>
        new(StringComparer.Ordinal)
        {
            ["origin_kind"] = OriginKind.ToString(),
            ["can_trigger_contingencies"] = CanTriggerContingencies,
            ["can_trigger_reactions"] = CanTriggerReactions,
            ["owner_member_id"] = OwnerMemberId.ToString(),
            ["setup_id"] = SetupId.ToString(),
            ["instance_id"] = InstanceId.ToString(),
            ["skill_entry_id"] = SkillEntryId.ToString(),
            ["stored_skill_id"] = StoredSkillId.ToString(),
            ["trigger_type"] = TriggerType.ToString(),
            ["triggering_attack_action_id"] = TriggeringAttackActionId,
        };

    private static StringName Normalize(StringName value) =>
        ProgressionDataUtils.to_string_name(value);
}
