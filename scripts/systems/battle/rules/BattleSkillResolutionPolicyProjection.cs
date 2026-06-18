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
            ["unit_cast_variant"] = policy.UnitCastVariant,
            ["ground_cast_variant"] = policy.GroundCastVariant,
            ["command_cast_variant"] = policy.CommandCastVariant,
            ["unit_execution_cast_variant"] = policy.UnitExecutionCastVariant,
            ["execution_cast_variant"] = policy.ExecutionCastVariant,
            ["routes_to_unit_targeting"] = policy.RoutesToUnitTargeting,
            ["option_error_message"] = policy.OptionErrorMessage,
            ["option_allowed"] = policy.OptionAllowed,
            ["effect_defs"] = ToEffectArray(policy.EffectDefs),
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

    private static Godot.Collections.Array<CombatEffectDef> ToEffectArray(
        IEnumerable<CombatEffectDef> values
    )
    {
        var result = new Godot.Collections.Array<CombatEffectDef>();
        foreach (CombatEffectDef value in values ?? Array.Empty<CombatEffectDef>())
        {
            if (value != null)
                result.Add(value);
        }
        return result;
    }
}
