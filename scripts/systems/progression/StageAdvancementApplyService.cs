using Godot;

[GlobalClass]
public partial class StageAdvancementApplyService : RefCounted
{
    private Godot.Collections.Dictionary _content_bundle = new();

    public void setup(Godot.Collections.Dictionary contentBundle = null)
    {
        _content_bundle = contentBundle ?? new Godot.Collections.Dictionary();
    }

    public bool add_stage_advancement_modifier(PartyMemberState memberState, StringName modifierId)
    {
        if (memberState == null || modifierId == "")
            return false;
        StageAdvancementModifier modifier = _get_content_def<StageAdvancementModifier>(
            "stage_advancement_defs",
            "stage_advancement",
            modifierId
        );
        if (modifier == null || modifier.modifier_id != modifierId)
            return false;
        if (!_modifier_applies_to_member(modifier, memberState))
            return false;
        var activeIds = memberState.active_stage_advancement_modifier_ids;
        if (activeIds.Contains(modifierId))
            return false;
        activeIds.Add(modifierId);
        return true;
    }

    public bool remove_stage_advancement_modifier(PartyMemberState memberState, StringName modifierId)
    {
        if (memberState == null || modifierId == "")
            return false;
        var activeIds = memberState.active_stage_advancement_modifier_ids;
        if (!activeIds.Contains(modifierId))
            return false;
        activeIds.Remove(modifierId);
        return true;
    }

    private bool _modifier_applies_to_member(
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
