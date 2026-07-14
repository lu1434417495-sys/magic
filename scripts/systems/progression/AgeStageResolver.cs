using System.Collections.Generic;
using Godot;

public static class AgeStageResolver
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

    public static AgeStageResolution ResolveEffectiveStage(
        PartyMemberState member_state,
        AgeProfileDefinition age_profile,
        IEnumerable<StageAdvancementDefinition> stage_advancement_modifiers = null,
        BloodlineDefinition _bloodline_def = null,
        BloodlineStageDefinition _bloodline_stage_def = null,
        AscensionDefinition ascension_def = null,
        AscensionStageDefinition ascension_stage_def = null
    )
    {
        var base_stage_id = _resolve_base_stage_id(member_state);
        if (
            ascension_def != null
            && ascension_stage_def != null
            && ascension_def.ReplacesAgeGrowth
            && ascension_stage_def.StageId != ""
        )
        {
            return _build_result(
                ascension_stage_def.StageId,
                SourceTypeAscension,
                ascension_stage_def.StageId
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
                    modifier.ModifierId
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

    private static List<StringName> _collect_age_stage_order(AgeProfileDefinition age_profile)
    {
        var stage_order = new List<StringName>();
        if (age_profile == null)
            return stage_order;
        foreach (var stage_rule in age_profile.StageRules)
        {
            if (stage_rule == null || stage_rule.StageId == "")
                continue;
            if (!stage_rule.ReachableByAging)
                continue;
            if (stage_order.Contains(stage_rule.StageId))
                continue;
            stage_order.Add(stage_rule.StageId);
        }
        return stage_order;
    }

    private static StageCandidate _resolve_modifier_stage_result(
        StageAdvancementDefinition modifier,
        StringName base_stage_id,
        int base_stage_index,
        List<StringName> stage_order
    )
    {
        if (modifier == null || modifier.StageOffset <= 0)
            return new StageCandidate(base_stage_id, base_stage_index);
        if (_uses_identity_stage_axis(modifier.TargetAxisKind))
            return new StageCandidate(modifier.MaxStageId, -1);
        if (base_stage_index < 0 || stage_order.Count == 0)
            return new StageCandidate(
                modifier.MaxStageId != "" ? modifier.MaxStageId : base_stage_id,
                -1
            );

        var target_index = Mathf.Min(
            base_stage_index + modifier.StageOffset,
            stage_order.Count - 1
        );
        if (modifier.MaxStageId != "")
        {
            var max_stage_index = stage_order.IndexOf(modifier.MaxStageId);
            if (max_stage_index >= 0)
                target_index = Mathf.Min(target_index, max_stage_index);
        }
        return new StageCandidate(stage_order[target_index], target_index);
    }

    private static bool _uses_identity_stage_axis(StageAdvancementTargetAxis targetAxis)
    {
        return targetAxis
            is StageAdvancementTargetAxis.Bloodline or StageAdvancementTargetAxis.Divine;
    }

    private static bool _modifier_applies_to_member(
        StageAdvancementDefinition modifier,
        PartyMemberState member_state
    )
    {
        if (modifier == null || member_state == null)
            return false;

        if (
            modifier.AppliesToRaceIds.Count > 0
            && !_contains_id(modifier.AppliesToRaceIds, member_state.race_id)
        )
            return false;
        if (
            modifier.AppliesToSubraceIds.Count > 0
            && !_contains_id(modifier.AppliesToSubraceIds, member_state.subrace_id)
        )
            return false;
        if (
            modifier.AppliesToBloodlineIds.Count > 0
            && !_contains_id(modifier.AppliesToBloodlineIds, member_state.bloodline_id)
        )
            return false;
        if (
            modifier.AppliesToAscensionIds.Count > 0
            && !_contains_id(modifier.AppliesToAscensionIds, member_state.ascension_id)
        )
            return false;
        return true;
    }

    private static bool _contains_id(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values)
            if (value == expected)
                return true;
        return false;
    }

    private static AgeStageResolution _build_result(
        StringName stage_id,
        StringName source_type,
        StringName source_id
    ) =>
        new(stage_id, source_type, source_id);
}
