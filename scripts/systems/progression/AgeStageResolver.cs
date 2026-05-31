using Godot;

[GlobalClass]
public partial class AgeStageResolver : RefCounted
{
    private static readonly StringName SourceTypeAscension = "ascension";
    private static readonly StringName SourceTypeStageAdvancement = "stage_advancement";

    private readonly struct StageCandidate
    {
        public readonly StringName StageId;
        public readonly int StageIndex;

        public StageCandidate(StringName stageId, int stageIndex)
        {
            StageId = stageId;
            StageIndex = stageIndex;
        }
    }

    public static AgeStageResolution resolve_effective_stage(
        PartyMemberState member_state,
        AgeProfileDef age_profile,
        Godot.Collections.Array<StageAdvancementModifier> stage_advancement_modifiers = null,
        BloodlineDef _bloodline_def = null,
        BloodlineStageDef _bloodline_stage_def = null,
        AscensionDef ascension_def = null,
        AscensionStageDef ascension_stage_def = null
    )
    {
        var base_stage_id = _resolve_base_stage_id(member_state);
        if (
            ascension_def != null
            && ascension_stage_def != null
            && (bool)ascension_def.replaces_age_growth
            && ascension_stage_def.stage_id != ""
        )
        {
            return _build_result(
                ascension_stage_def.stage_id,
                SourceTypeAscension,
                ascension_stage_def.stage_id
            );
        }

        var resolved_result = _build_result(base_stage_id, "", "");
        var stage_order = _collect_age_stage_order(age_profile);
        var base_stage_index = stage_order.IndexOf(base_stage_id);
        var best_stage_index = base_stage_index;
        if (stage_advancement_modifiers != null)
        {
            foreach (var modifier in stage_advancement_modifiers)
            {
                if (modifier == null)
                    continue;
                if (!_modifier_applies_to_member(modifier, member_state))
                    continue;
                var modifier_result = _resolve_modifier_stage_result(
                    modifier,
                    base_stage_id,
                    base_stage_index,
                    stage_order
                );
                var modifier_stage_id = modifier_result.StageId;
                if (modifier_stage_id == "" || modifier_stage_id == base_stage_id)
                    continue;
                var modifier_stage_index = modifier_result.StageIndex;
                if (
                    modifier_stage_index >= 0
                    && best_stage_index >= 0
                    && modifier_stage_index < best_stage_index
                )
                    continue;
                best_stage_index = modifier_stage_index;
                resolved_result = _build_result(
                    modifier_stage_id,
                    SourceTypeStageAdvancement,
                    modifier.modifier_id
                );
            }
        }

        return resolved_result;
    }

    private static StringName _resolve_base_stage_id(PartyMemberState member_state)
    {
        if (member_state == null)
            return "adult";
        if (member_state.natural_age_stage_id != "")
            return member_state.natural_age_stage_id;
        return member_state.effective_age_stage_id != ""
            ? member_state.effective_age_stage_id
            : "adult";
    }

    private static Godot.Collections.Array<StringName> _collect_age_stage_order(
        AgeProfileDef age_profile
    )
    {
        var stage_order = new Godot.Collections.Array<StringName>();
        if (age_profile == null)
            return stage_order;
        foreach (var stage_rule in age_profile.stage_rules)
        {
            if (stage_rule == null || stage_rule.stage_id == "")
                continue;
            if (!(bool)stage_rule.reachable_by_aging)
                continue;
            if (stage_order.Contains(stage_rule.stage_id))
                continue;
            stage_order.Add(stage_rule.stage_id);
        }
        return stage_order;
    }

    private static StageCandidate _resolve_modifier_stage_result(
        StageAdvancementModifier modifier,
        StringName base_stage_id,
        int base_stage_index,
        Godot.Collections.Array<StringName> stage_order
    )
    {
        if (modifier == null || (int)modifier.stage_offset <= 0)
            return new StageCandidate(base_stage_id, base_stage_index);
        if (_uses_identity_stage_axis(modifier.target_axis))
            return new StageCandidate(modifier.max_stage_id, -1);
        if (base_stage_index < 0 || stage_order.Count == 0)
            return new StageCandidate(
                modifier.max_stage_id != "" ? modifier.max_stage_id : base_stage_id,
                -1
            );

        var target_index = Mathf.Min(
            base_stage_index + (int)modifier.stage_offset,
            stage_order.Count - 1
        );
        if (modifier.max_stage_id != "")
        {
            var max_stage_index = stage_order.IndexOf(modifier.max_stage_id);
            if (max_stage_index >= 0)
                target_index = Mathf.Min(target_index, max_stage_index);
        }
        return new StageCandidate(stage_order[target_index], target_index);
    }

    private static bool _uses_identity_stage_axis(StringName target_axis)
    {
        return target_axis == "bloodline" || target_axis == "divine";
    }

    private static bool _modifier_applies_to_member(
        StageAdvancementModifier modifier,
        PartyMemberState member_state
    )
    {
        if (modifier == null || member_state == null)
            return false;

        if (
            modifier.applies_to_race_ids.Count > 0
            && !modifier.applies_to_race_ids.Contains(member_state.race_id)
        )
            return false;
        if (
            modifier.applies_to_subrace_ids.Count > 0
            && !modifier.applies_to_subrace_ids.Contains(member_state.subrace_id)
        )
            return false;
        if (
            modifier.applies_to_bloodline_ids.Count > 0
            && !modifier.applies_to_bloodline_ids.Contains(member_state.bloodline_id)
        )
            return false;
        if (
            modifier.applies_to_ascension_ids.Count > 0
            && !modifier.applies_to_ascension_ids.Contains(member_state.ascension_id)
        )
            return false;
        return true;
    }

    private static AgeStageResolution _build_result(
        StringName stage_id,
        StringName source_type,
        StringName source_id
    ) =>
        new(stage_id, source_type, source_id);
}
