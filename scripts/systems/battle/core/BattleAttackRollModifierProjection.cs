using System.Collections.Generic;
using Godot;

internal static class BattleAttackRollModifierProjection
{
    internal static Godot.Collections.Dictionary ProjectBundle(
        BattleAttackRollModifierBundle bundle
    )
    {
        if (bundle == null)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["total_bonus"] = bundle.TotalBonus,
            ["total_penalty"] = bundle.TotalPenalty,
            ["effective_modifier_delta"] = bundle.GetEffectiveModifierDelta(),
            ["breakdown"] = ProjectBreakdown(bundle.Breakdown),
        };
    }

    internal static Godot.Collections.Array<Godot.Collections.Dictionary> ProjectBreakdown(
        IEnumerable<BattleAttackRollModifierSpec> specs
    )
    {
        var payloads = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (BattleAttackRollModifierSpec spec in specs ?? System.Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (spec == null)
                continue;
            payloads.Add(ProjectSpecWithEffectiveModifierDelta(spec, spec.modifier_delta));
        }
        return payloads;
    }

    internal static Godot.Collections.Dictionary ProjectSpec(BattleAttackRollModifierSpec spec)
    {
        if (spec == null)
            return new Godot.Collections.Dictionary();
        return ProjectSpecWithEffectiveModifierDelta(spec, spec.modifier_delta);
    }

    internal static Godot.Collections.Dictionary ProjectPartialSpec(
        BattleAttackRollModifierSpec spec
    )
    {
        if (spec == null)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["source_domain"] = spec.source_domain.ToString(),
            ["source_id"] = spec.source_id.ToString(),
            ["source_instance_id"] = spec.source_instance_id,
            ["label"] = spec.label,
            ["modifier_delta"] = spec.modifier_delta,
            ["stack_key"] = spec.stack_key.ToString(),
            ["stack_mode"] = spec.stack_mode.ToString(),
            ["roll_kind_filter"] = spec.roll_kind_filter.ToString(),
            ["endpoint_mode"] = spec.endpoint_mode.ToString(),
            ["distance_min_exclusive"] = spec.distance_min_exclusive,
            ["distance_max_inclusive"] = spec.distance_max_inclusive,
            ["target_team_filter"] = spec.target_team_filter.ToString(),
            ["footprint_mode"] = spec.footprint_mode.ToString(),
            ["applies_to"] = spec.applies_to.ToString(),
        };
    }

    internal static Godot.Collections.Dictionary ProjectSpecWithEffectiveModifierDelta(
        BattleAttackRollModifierSpec spec,
        int effectiveModifierDelta
    )
    {
        if (spec == null)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["source_domain"] = spec.source_domain.ToString(),
            ["source_id"] = spec.source_id.ToString(),
            ["source_instance_id"] = spec.source_instance_id,
            ["label"] = spec.label,
            ["modifier_delta"] = spec.modifier_delta,
            ["effective_modifier_delta"] = effectiveModifierDelta,
            ["stack_key"] = spec.stack_key.ToString(),
            ["stack_mode"] = spec.stack_mode.ToString(),
            ["roll_kind_filter"] = spec.roll_kind_filter.ToString(),
            ["endpoint_mode"] = spec.endpoint_mode.ToString(),
            ["distance_min_exclusive"] = spec.distance_min_exclusive,
            ["distance_max_inclusive"] = spec.distance_max_inclusive,
            ["target_team_filter"] = spec.target_team_filter.ToString(),
            ["footprint_mode"] = spec.footprint_mode.ToString(),
            ["applies_to"] = spec.applies_to.ToString(),
        };
    }
}
