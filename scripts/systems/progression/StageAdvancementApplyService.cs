using System.Collections.Generic;
using Godot;

public sealed class StageAdvancementApplyService
{
    private Dictionary<StringName, StageAdvancementModifier> _stageAdvancementDefs = new();

    public void Setup(ProgressionIdentityCatalogData identityCatalog)
    {
        identityCatalog ??= new ProgressionIdentityCatalogData();
        _stageAdvancementDefs = new Dictionary<StringName, StageAdvancementModifier>(
            identityCatalog.StageAdvancementDefs
        );
    }

    public bool AddStageAdvancementModifier(PartyMemberState memberState, StringName modifierId)
    {
        if (memberState == null || modifierId == "")
            return false;
        _stageAdvancementDefs.TryGetValue(modifierId, out StageAdvancementModifier modifier);
        if (modifier == null || modifier.modifier_id != modifierId)
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
        StageAdvancementModifier modifier,
        PartyMemberState memberState
    )
    {
        if (modifier == null || memberState == null)
            return false;
        if (
            modifier.applies_to_race_ids.Count > 0
            && !modifier.applies_to_race_ids.Contains(memberState.race_id)
        )
            return false;
        if (
            modifier.applies_to_subrace_ids.Count > 0
            && !modifier.applies_to_subrace_ids.Contains(memberState.subrace_id)
        )
            return false;
        if (
            modifier.applies_to_bloodline_ids.Count > 0
            && !modifier.applies_to_bloodline_ids.Contains(memberState.bloodline_id)
        )
            return false;
        if (
            modifier.applies_to_ascension_ids.Count > 0
            && !modifier.applies_to_ascension_ids.Contains(memberState.ascension_id)
        )
            return false;
        return true;
    }
}
