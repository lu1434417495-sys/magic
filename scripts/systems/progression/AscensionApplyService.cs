using Godot;

[GlobalClass]
public partial class AscensionApplyService : RefCounted
{
    private Godot.Collections.Dictionary _content_bundle = new();

    public void setup(Godot.Collections.Dictionary contentBundle = null)
    {
        _content_bundle = contentBundle ?? new Godot.Collections.Dictionary();
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
        AscensionDef ascensionDef = _get_content_def<AscensionDef>(
            "ascension_defs",
            "ascension",
            ascensionId
        );
        AscensionStageDef stageDef = _get_content_def<AscensionStageDef>(
            "ascension_stage_defs",
            "ascension_stage",
            ascensionStageId
        );
        if (!_is_valid_ascension_stage_pair(ascensionDef, stageDef, ascensionId, ascensionStageId))
            return false;
        if (!_member_matches_allowed_identity(memberState, ascensionDef))
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

    private bool _is_valid_ascension_stage_pair(
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

    private bool _member_matches_allowed_identity(
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

    private T _get_content_def<T>(
        string primaryBucket,
        string aliasBucket,
        StringName entryId
    ) where T : class
    {
        if (entryId == "")
            return null;
        var bucket = _get_content_bucket(primaryBucket, aliasBucket);
        return bucket != null && bucket.ContainsKey(entryId)
            ? bucket[entryId].AsGodotObject() as T
            : null;
    }

    private Godot.Collections.Dictionary _get_content_bucket(
        string primaryBucket,
        string aliasBucket
    )
    {
        if (_content_bundle.ContainsKey(primaryBucket))
        {
            var bv = _content_bundle[primaryBucket];
            if (bv.VariantType == Variant.Type.Dictionary)
                return bv.AsGodotDictionary();
        }
        if (_content_bundle.ContainsKey(aliasBucket))
        {
            var bv = _content_bundle[aliasBucket];
            if (bv.VariantType == Variant.Type.Dictionary)
                return bv.AsGodotDictionary();
        }
        return null;
    }
}
