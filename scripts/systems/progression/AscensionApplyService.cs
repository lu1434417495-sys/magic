using Godot;

[GlobalClass]
public partial class AscensionApplyService : RefCounted
{
    private Godot.Collections.Dictionary _content_bundle = new();

    public void setup(Godot.Collections.Dictionary contentBundle = null)
    {
        _content_bundle = contentBundle ?? new Godot.Collections.Dictionary();
    }

    public bool apply_ascension(GodotObject memberState, StringName ascensionId, StringName ascensionStageId, int currentWorldStep)
    {
        if (memberState == null || ascensionId == "" || ascensionStageId == "" || currentWorldStep < 0)
            return false;
        var ascensionDef = _get_content_def("ascension_defs", "ascension", ascensionId) as AscensionDef;
        var stageDef = _get_content_def("ascension_stage_defs", "ascension_stage", ascensionStageId) as AscensionStageDef;
        if (!_is_valid_ascension_stage_pair(ascensionDef, stageDef, ascensionId, ascensionStageId))
            return false;
        if (!_member_matches_allowed_identity(memberState, ascensionDef))
            return false;
        if (memberState.Get("original_race_id_before_ascension").AsStringName() == "")
            memberState.Set("original_race_id_before_ascension", memberState.Get("race_id").AsStringName());
        memberState.Set("ascension_id", ascensionId);
        memberState.Set("ascension_stage_id", ascensionStageId);
        memberState.Set("ascension_started_at_world_step", currentWorldStep);
        return true;
    }

    public bool revoke_ascension(GodotObject memberState, bool restoreOriginalRace = true)
    {
        if (memberState == null)
            return false;
        if (memberState.Get("ascension_id").AsStringName() == ""
            && memberState.Get("ascension_stage_id").AsStringName() == ""
            && memberState.Get("ascension_started_at_world_step").AsInt32() == -1
            && memberState.Get("original_race_id_before_ascension").AsStringName() == "")
            return false;
        if (restoreOriginalRace && memberState.Get("original_race_id_before_ascension").AsStringName() != "")
            memberState.Set("race_id", memberState.Get("original_race_id_before_ascension").AsStringName());
        memberState.Set("ascension_id", "");
        memberState.Set("ascension_stage_id", "");
        memberState.Set("ascension_started_at_world_step", -1);
        memberState.Set("original_race_id_before_ascension", "");
        return true;
    }

    private bool _is_valid_ascension_stage_pair(AscensionDef ascensionDef, AscensionStageDef stageDef, StringName ascensionId, StringName ascensionStageId)
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

    private bool _member_matches_allowed_identity(GodotObject memberState, AscensionDef ascensionDef)
    {
        if (ascensionDef == null || memberState == null)
            return false;
        if (ascensionDef.allowed_race_ids.Count > 0 && !ascensionDef.allowed_race_ids.Contains(memberState.Get("race_id").AsStringName()))
            return false;
        if (ascensionDef.allowed_subrace_ids.Count > 0 && !ascensionDef.allowed_subrace_ids.Contains(memberState.Get("subrace_id").AsStringName()))
            return false;
        if (ascensionDef.allowed_bloodline_ids.Count > 0 && !ascensionDef.allowed_bloodline_ids.Contains(memberState.Get("bloodline_id").AsStringName()))
            return false;
        return true;
    }

    private GodotObject _get_content_def(string primaryBucket, string aliasBucket, StringName entryId)
    {
        if (entryId == "")
            return null;
        var bucket = _get_content_bucket(primaryBucket, aliasBucket);
        return bucket != null && bucket.ContainsKey(entryId) ? bucket[entryId].AsGodotObject() : null;
    }

    private Godot.Collections.Dictionary _get_content_bucket(string primaryBucket, string aliasBucket)
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
