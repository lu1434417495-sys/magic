using System;
using System.Collections.Generic;
using Godot;

internal static class BattleSkillResolutionPolicyProjection
{
    internal static Godot.Collections.Dictionary Project(BattleSkillResolutionPolicy policy)
    {
        if (policy == null)
            return new Godot.Collections.Dictionary();
        return new Godot.Collections.Dictionary
        {
            ["target_unit_ids"] = ToStringNameArray(policy.TargetUnitIds),
            ["unit_cast_variant_id"] = policy.UnitCastVariantDefinition?.VariantId ?? default,
            ["ground_cast_variant_id"] = policy.GroundCastVariantDefinition?.VariantId ?? default,
            ["command_cast_variant_id"] = policy.CommandCastVariantDefinition?.VariantId ?? default,
            ["unit_execution_cast_variant_id"] =
                policy.UnitExecutionCastVariantDefinition?.VariantId ?? default,
            ["execution_cast_variant_id"] = policy.ExecutionCastVariantDefinition?.VariantId ?? default,
            ["routes_to_unit_targeting"] = policy.RoutesToUnitTargeting,
            ["option_error_message"] = policy.OptionErrorMessage,
            ["option_allowed"] = policy.OptionAllowed,
            ["effect_count"] = policy.EffectDefinitions.Count,
            ["uses_fate_attack"] = policy.UsesFateAttack,
            ["force_hit_no_crit"] = policy.ForceHitNoCrit,
            ["fate_preview_mode"] = policy.FatePreviewMode,
        };
    }

    private static Godot.Collections.Array<StringName> ToStringNameArray(
        IEnumerable<StringName> values
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            result.Add(value);
        }
        return result;
    }

}
