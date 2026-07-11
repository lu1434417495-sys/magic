using System.Collections.Generic;
using Godot;

public sealed class StageAdvancementApplyService
{
    private Dictionary<StringName, StageAdvancementDefinition> _stageAdvancementDefs = new();

    public void Setup(ProgressionIdentityCatalogData identityCatalog)
    {
        identityCatalog ??= new ProgressionIdentityCatalogData();
        _stageAdvancementDefs = new Dictionary<StringName, StageAdvancementDefinition>(
            identityCatalog.StageAdvancementDefs
        );
    }

    public bool AddStageAdvancementModifier(PartyMemberState memberState, StringName modifierId)
    {
        if (memberState == null || modifierId == "")
            return false;
        _stageAdvancementDefs.TryGetValue(modifierId, out StageAdvancementDefinition modifier);
        if (modifier == null || modifier.ModifierId != modifierId)
            return false;
        if (!ModifierAppliesToMember(modifier, memberState))
            return false;
        var activeIds = memberState.active_stage_advancement_modifier_ids;
        if (activeIds.Contains(modifierId))
            return false;
        activeIds.Add(modifierId);
        return true;
    }

    public bool RemoveStageAdvancementModifier(PartyMemberState memberState, StringName modifierId)
    {
        if (memberState == null || modifierId == "")
            return false;
        var activeIds = memberState.active_stage_advancement_modifier_ids;
        if (!activeIds.Contains(modifierId))
            return false;
        activeIds.Remove(modifierId);
        return true;
    }

    private static bool ModifierAppliesToMember(
        StageAdvancementDefinition modifier,
        PartyMemberState memberState
    )
    {
        if (modifier == null || memberState == null)
            return false;
        if (
            modifier.AppliesToRaceIds.Count > 0
            && !ContainsId(modifier.AppliesToRaceIds, memberState.race_id)
        )
            return false;
        if (
            modifier.AppliesToSubraceIds.Count > 0
            && !ContainsId(modifier.AppliesToSubraceIds, memberState.subrace_id)
        )
            return false;
        if (
            modifier.AppliesToBloodlineIds.Count > 0
            && !ContainsId(modifier.AppliesToBloodlineIds, memberState.bloodline_id)
        )
            return false;
        if (
            modifier.AppliesToAscensionIds.Count > 0
            && !ContainsId(modifier.AppliesToAscensionIds, memberState.ascension_id)
        )
            return false;
        return true;
    }

    private static bool ContainsId(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values)
            if (value == expected)
                return true;
        return false;
    }
}
