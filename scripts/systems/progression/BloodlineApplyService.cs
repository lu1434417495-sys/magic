using System.Collections.Generic;
using Godot;

public sealed class BloodlineApplyService
{
    private Dictionary<StringName, BloodlineDefinition> _bloodlineDefs = new();
    private Dictionary<StringName, BloodlineStageDefinition> _bloodlineStageDefs = new();

    public void Setup(ProgressionIdentityCatalogData identityCatalog)
    {
        identityCatalog ??= new ProgressionIdentityCatalogData();
        _bloodlineDefs = new Dictionary<StringName, BloodlineDefinition>(identityCatalog.BloodlineDefs);
        _bloodlineStageDefs = new Dictionary<StringName, BloodlineStageDefinition>(
            identityCatalog.BloodlineStageDefs
        );
    }

    public bool ApplyBloodline(
        PartyMemberState memberState,
        StringName bloodlineId,
        StringName bloodlineStageId
    )
    {
        if (memberState == null || bloodlineId == "" || bloodlineStageId == "")
            return false;
        _bloodlineDefs.TryGetValue(bloodlineId, out BloodlineDefinition bloodlineDef);
        _bloodlineStageDefs.TryGetValue(bloodlineStageId, out BloodlineStageDefinition stageDef);
        if (!IsValidBloodlineStagePair(bloodlineDef, stageDef, bloodlineId, bloodlineStageId))
            return false;
        memberState.SetBloodline(bloodlineId, bloodlineStageId);
        return true;
    }

    public bool RevokeBloodline(PartyMemberState memberState)
    {
        if (memberState == null)
            return false;
        if (memberState.bloodline_id == "" && memberState.bloodline_stage_id == "")
            return false;
        memberState.ClearBloodline();
        return true;
    }

    private static bool IsValidBloodlineStagePair(
        BloodlineDefinition bloodlineDef,
        BloodlineStageDefinition stageDef,
        StringName bloodlineId,
        StringName bloodlineStageId
    )
    {
        if (bloodlineDef == null || stageDef == null)
            return false;
        if (bloodlineDef.BloodlineId != bloodlineId)
            return false;
        if (stageDef.StageId != bloodlineStageId)
            return false;
        if (stageDef.BloodlineId != bloodlineId)
            return false;
        return ContainsId(bloodlineDef.StageIds, bloodlineStageId);
    }

    private static bool ContainsId(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values)
            if (value == expected)
                return true;
        return false;
    }
}
