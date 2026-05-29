using Godot;

[GlobalClass]
public partial class BloodlineApplyService : RefCounted
{
    private Godot.Collections.Dictionary _content_bundle = new();

    public void setup(Godot.Collections.Dictionary contentBundle = null)
    {
        _content_bundle = contentBundle ?? new Godot.Collections.Dictionary();
    }

    public bool apply_bloodline(
        PartyMemberState memberState,
        StringName bloodlineId,
        StringName bloodlineStageId
    )
    {
        if (memberState == null || bloodlineId == "" || bloodlineStageId == "")
            return false;
        var bloodlineDef =
            _get_content_def("bloodline_defs", "bloodline", bloodlineId) as BloodlineDef;
        var stageDef =
            _get_content_def("bloodline_stage_defs", "bloodline_stage", bloodlineStageId)
            as BloodlineStageDef;
        if (!_is_valid_bloodline_stage_pair(bloodlineDef, stageDef, bloodlineId, bloodlineStageId))
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

    private bool _is_valid_bloodline_stage_pair(
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

    private GodotObject _get_content_def(
        string primaryBucket,
        string aliasBucket,
        StringName entryId
    )
    {
        if (entryId == "")
            return null;
        var bucket = _get_content_bucket(primaryBucket, aliasBucket);
        return bucket != null && bucket.ContainsKey(entryId)
            ? bucket[entryId].AsGodotObject()
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
