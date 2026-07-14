using System.Collections.Generic;
using Godot;

public sealed class AscensionApplyService
{
    private Dictionary<StringName, AscensionDefinition> _ascensionDefs = new();
    private Dictionary<StringName, AscensionStageDefinition> _ascensionStageDefs = new();

    public void Setup(ProgressionIdentityCatalogData identityCatalog)
    {
        identityCatalog ??= new ProgressionIdentityCatalogData();
        _ascensionDefs = new Dictionary<StringName, AscensionDefinition>(identityCatalog.AscensionDefs);
        _ascensionStageDefs = new Dictionary<StringName, AscensionStageDefinition>(
            identityCatalog.AscensionStageDefs
        );
    }

    public bool ApplyAscension(
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
        _ascensionDefs.TryGetValue(ascensionId, out AscensionDefinition ascensionDef);
        _ascensionStageDefs.TryGetValue(ascensionStageId, out AscensionStageDefinition stageDef);
        if (!IsValidAscensionStagePair(ascensionDef, stageDef, ascensionId, ascensionStageId))
            return false;
        if (!MemberMatchesAllowedIdentity(memberState, ascensionDef))
            return false;
        StringName originalRaceId = memberState.original_race_id_before_ascension == ""
            ? memberState.race_id
            : memberState.original_race_id_before_ascension;
        memberState.SetAscension(ascensionId, ascensionStageId, currentWorldStep, originalRaceId);
        return true;
    }

    public bool RevokeAscension(PartyMemberState memberState) => RevokeAscension(memberState, true);

    public bool RevokeAscension(PartyMemberState memberState, bool restoreOriginalRace)
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
            memberState.SetIdentity(memberState.original_race_id_before_ascension, memberState.subrace_id);
        memberState.ClearAscension();
        return true;
    }

    private static bool IsValidAscensionStagePair(
        AscensionDefinition ascensionDef,
        AscensionStageDefinition stageDef,
        StringName ascensionId,
        StringName ascensionStageId
    )
    {
        if (ascensionDef == null || stageDef == null)
            return false;
        if (ascensionDef.AscensionId != ascensionId)
            return false;
        if (stageDef.StageId != ascensionStageId)
            return false;
        if (stageDef.AscensionId != ascensionId)
            return false;
        return ContainsId(ascensionDef.StageIds, ascensionStageId);
    }

    private static bool MemberMatchesAllowedIdentity(
        PartyMemberState memberState,
        AscensionDefinition ascensionDef
    )
    {
        if (ascensionDef == null || memberState == null)
            return false;
        if (
            ascensionDef.AllowedRaceIds.Count > 0
            && !ContainsId(ascensionDef.AllowedRaceIds, memberState.race_id)
        )
            return false;
        if (
            ascensionDef.AllowedSubraceIds.Count > 0
            && !ContainsId(ascensionDef.AllowedSubraceIds, memberState.subrace_id)
        )
            return false;
        if (
            ascensionDef.AllowedBloodlineIds.Count > 0
            && !ContainsId(ascensionDef.AllowedBloodlineIds, memberState.bloodline_id)
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
