using System.Collections.Generic;
using Godot;

public sealed class BloodlineApplyService
{
    private Dictionary<StringName, BloodlineDef> _bloodlineDefs = new();
    private Dictionary<StringName, BloodlineStageDef> _bloodlineStageDefs = new();

    public void Setup(ProgressionIdentityCatalogData identityCatalog)
    {
        identityCatalog ??= new ProgressionIdentityCatalogData();
        _bloodlineDefs = new Dictionary<StringName, BloodlineDef>(identityCatalog.BloodlineDefs);
        _bloodlineStageDefs = new Dictionary<StringName, BloodlineStageDef>(
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
        _bloodlineDefs.TryGetValue(bloodlineId, out BloodlineDef bloodlineDef);
        _bloodlineStageDefs.TryGetValue(bloodlineStageId, out BloodlineStageDef stageDef);
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
        BloodlineDef bloodlineDef,
        BloodlineStageDef stageDef,
        StringName bloodlineId,
        StringName bloodlineStageId
    )
    {
        if (bloodlineDef == null || stageDef == null)
            return false;
        if (bloodlineDef.bloodline_id != bloodlineId)
            return false;
        if (stageDef.stage_id != bloodlineStageId)
            return false;
        if (stageDef.bloodline_id != bloodlineId)
            return false;
        return bloodlineDef.stage_ids.Contains(bloodlineStageId);
    }
}
