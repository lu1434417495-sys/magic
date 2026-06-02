using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed class AscensionApplyService
{
    private Dictionary<StringName, AscensionDef> _ascensionDefs = new();
    private Dictionary<StringName, AscensionStageDef> _ascensionStageDefs = new();

    public void setup(GDictionary contentBundle = null)
    {
        _ascensionDefs = ProgressionContentBundleAdapter.ReadDefMap<AscensionDef>(
            contentBundle,
            "ascension_defs",
            "ascension"
        );
        _ascensionStageDefs = ProgressionContentBundleAdapter.ReadDefMap<AscensionStageDef>(
            contentBundle,
            "ascension_stage_defs",
            "ascension_stage"
        );
    }

    public bool apply_ascension(
        PartyMemberState memberState,
        StringName ascensionId,
        StringName ascensionStageId,
        int currentWorldStep
    )
    {
        if (
            memberState == null
            || ascensionId == ""
            || ascensionStageId == ""
            || currentWorldStep < 0
        )
            return false;
        _ascensionDefs.TryGetValue(ascensionId, out AscensionDef ascensionDef);
        _ascensionStageDefs.TryGetValue(ascensionStageId, out AscensionStageDef stageDef);
        if (!IsValidAscensionStagePair(ascensionDef, stageDef, ascensionId, ascensionStageId))
            return false;
        if (!MemberMatchesAllowedIdentity(memberState, ascensionDef))
            return false;
        if (memberState.original_race_id_before_ascension == "")
            memberState.original_race_id_before_ascension = memberState.race_id;
        memberState.ascension_id = ascensionId;
        memberState.ascension_stage_id = ascensionStageId;
        memberState.ascension_started_at_world_step = currentWorldStep;
        return true;
    }

    public bool revoke_ascension(PartyMemberState memberState) => revoke_ascension(memberState, true);

    public bool revoke_ascension(PartyMemberState memberState, bool restoreOriginalRace)
    {
        if (memberState == null)
            return false;
        if (
            memberState.ascension_id == ""
            && memberState.ascension_stage_id == ""
            && memberState.ascension_started_at_world_step == -1
            && memberState.original_race_id_before_ascension == ""
        )
            return false;
        if (restoreOriginalRace && memberState.original_race_id_before_ascension != "")
            memberState.race_id = memberState.original_race_id_before_ascension;
        memberState.ascension_id = "";
        memberState.ascension_stage_id = "";
        memberState.ascension_started_at_world_step = -1;
        memberState.original_race_id_before_ascension = "";
        return true;
    }

    private static bool IsValidAscensionStagePair(
        AscensionDef ascensionDef,
        AscensionStageDef stageDef,
        StringName ascensionId,
        StringName ascensionStageId
    )
    {
        if (ascensionDef == null || stageDef == null)
            return false;
        if (ascensionDef.ascension_id != ascensionId)
            return false;
        if (stageDef.stage_id != ascensionStageId)
            return false;
        if (stageDef.ascension_id != ascensionId)
            return false;
        return ascensionDef.stage_ids.Contains(ascensionStageId);
    }

    private static bool MemberMatchesAllowedIdentity(
        PartyMemberState memberState,
        AscensionDef ascensionDef
    )
    {
        if (ascensionDef == null || memberState == null)
            return false;
        if (
            ascensionDef.allowed_race_ids.Count > 0
            && !ascensionDef.allowed_race_ids.Contains(memberState.race_id)
        )
            return false;
        if (
            ascensionDef.allowed_subrace_ids.Count > 0
            && !ascensionDef.allowed_subrace_ids.Contains(memberState.subrace_id)
        )
            return false;
        if (
            ascensionDef.allowed_bloodline_ids.Count > 0
            && !ascensionDef.allowed_bloodline_ids.Contains(memberState.bloodline_id)
        )
            return false;
        return true;
    }
}
