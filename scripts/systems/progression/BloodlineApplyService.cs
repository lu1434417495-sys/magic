using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class BloodlineApplyService
{
    private Dictionary<StringName, BloodlineDef> _bloodlineDefs = new();
    private Dictionary<StringName, BloodlineStageDef> _bloodlineStageDefs = new();

    public void setup(GDictionary contentBundle = null)
    {
        _bloodlineDefs = ProgressionContentBundleAdapter.ReadDefMap<BloodlineDef>(
            contentBundle,
            "bloodline_defs",
            "bloodline"
        );
        _bloodlineStageDefs = ProgressionContentBundleAdapter.ReadDefMap<BloodlineStageDef>(
            contentBundle,
            "bloodline_stage_defs",
            "bloodline_stage"
        );
    }

    public bool apply_bloodline(
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
        memberState.bloodline_id = bloodlineId;
        memberState.bloodline_stage_id = bloodlineStageId;
        return true;
    }

    public bool revoke_bloodline(PartyMemberState memberState)
    {
        if (memberState == null)
            return false;
        if (memberState.bloodline_id == "" && memberState.bloodline_stage_id == "")
            return false;
        memberState.bloodline_id = "";
        memberState.bloodline_stage_id = "";
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
