using System.Collections.Generic;
using Godot;

internal static class BattleDamagePreviewProjection
{
    internal static Godot.Collections.Dictionary Project(BattleDamagePreviewSaveEstimate estimate)
    {
        if (estimate == null)
            return new Godot.Collections.Dictionary();

        if (!estimate.HasSave)
        {
            return new Godot.Collections.Dictionary
            {
                ["has_save"] = false,
                ["damage_before_save"] = estimate.DamageBeforeSave,
                ["damage_after_save"] = estimate.DamageAfterSave,
                ["damage_after_save_estimate"] = estimate.DamageAfterSaveEstimate,
                ["damage_after_save_worst"] = estimate.DamageAfterSaveWorst,
            };
        }

        return new Godot.Collections.Dictionary
        {
            ["has_save"] = true,
            ["damage_before_save"] = estimate.DamageBeforeSave,
            ["damage_after_save"] = estimate.DamageAfterSave,
            ["damage_after_save_estimate"] = estimate.DamageAfterSaveEstimate,
            ["damage_after_save_worst"] = estimate.DamageAfterSaveWorst,
            ["damage_on_save_failure"] = estimate.DamageOnSaveFailure,
            ["damage_on_save_success"] = estimate.DamageOnSaveSuccess,
            ["save_partial_on_success"] = estimate.SavePartialOnSuccess,
            ["save_success_probability_basis_points"] =
                estimate.SaveSuccessProbabilityBasisPoints,
            ["save_success_rate_percent"] = estimate.SaveSuccessRatePercent,
            ["save_failure_probability_basis_points"] =
                estimate.SaveFailureProbabilityBasisPoints,
            ["dc"] = estimate.Dc,
            ["ability"] = estimate.Ability,
            ["save_tag"] = estimate.SaveTag,
            ["advantage_state"] = estimate.AdvantageState,
            ["ability_value"] = estimate.AbilityValue,
            ["ability_modifier"] = estimate.AbilityModifier,
            ["bonus"] = estimate.Bonus,
            ["immune"] = estimate.Immune,
            ["sources"] = BuildSaveSourceArray(estimate.Sources),
        };
    }

    internal static Godot.Collections.Dictionary Project(BattleDamagePreviewResult preview)
    {
        if (preview == null)
            return new Godot.Collections.Dictionary();

        var result = new Godot.Collections.Dictionary
        {
            ["applied"] = preview.Applied,
            ["pre_save_damage"] = preview.PreSaveDamage,
            ["post_save_damage"] = preview.PostSaveDamage,
            ["damage"] = preview.Damage,
            ["hp_damage"] = preview.HpDamage,
            ["healing"] = 0,
            ["incoming_budget_damage"] = preview.IncomingBudgetDamage,
            ["shield_absorbed"] = preview.ShieldAbsorbed,
            ["shield_broken"] = preview.ShieldBroken,
            ["shield_hp_before"] = preview.ShieldHpBefore,
            ["shield_hp_after"] = preview.ShieldHpAfter,
            ["damage_events"] = TraceDictionaryProjection.ToArray(preview.DamageEvents),
            ["equipment_durability_events"] = new Godot.Collections.Array(),
            ["dispel_events"] = new Godot.Collections.Array(),
            ["damage_dice_high_total_roll"] = false,
            ["skill_damage_dice_is_max"] = false,
            ["weapon_damage_dice_is_max"] = false,
            ["status_effect_ids"] = new Godot.Collections.Array<StringName>(),
            ["removed_status_effect_ids"] = new Godot.Collections.Array<StringName>(),
            ["source_status_effect_ids"] = new Godot.Collections.Array<StringName>(),
            ["terrain_effect_ids"] = new Godot.Collections.Array<StringName>(),
            ["height_delta"] = 0,
            ["diagnostics"] = TraceDictionaryProjection.ToArray(preview.Diagnostics),
            ["save_estimates"] = BuildSaveEstimateArray(preview.SaveEstimates),
            ["stable_lethal"] = preview.StableLethal,
            ["lethal_probability_basis_points"] = preview.LethalProbabilityBasisPoints,
        };
        if (preview.RollMode != default)
            result["roll_mode"] = preview.RollMode.ToString();
        if (preview.SaveMode != default)
            result["save_mode"] = preview.SaveMode.ToString();
        if (preview.DamageOutcome.Count > 0)
            result["damage_outcome"] = TraceDictionaryProjection.ToDictionary(preview.DamageOutcome);
        if (preview.DamageResult.Count > 0)
            result["damage_result"] = TraceDictionaryProjection.ToDictionary(preview.DamageResult);
        if (preview.SaveEstimate != null)
            result["save_estimate"] = Project(preview.SaveEstimate);
        if (!string.IsNullOrEmpty(preview.ErrorCode))
            result["error_code"] = preview.ErrorCode;
        if (preview.SourcePreviewAfter != null)
            result["source_preview_after"] = preview.SourcePreviewAfter;
        if (preview.TargetPreviewAfter != null)
            result["target_preview_after"] = preview.TargetPreviewAfter;
        return result;
    }

    private static Godot.Collections.Array BuildSaveEstimateArray(
        IReadOnlyList<BattleDamagePreviewSaveEstimate> estimates
    )
    {
        var result = new Godot.Collections.Array();
        if (estimates == null)
            return result;
        foreach (BattleDamagePreviewSaveEstimate estimate in estimates)
        {
            if (estimate != null && estimate.HasSave)
                result.Add(Project(estimate));
        }
        return result;
    }

    private static Godot.Collections.Array BuildSaveSourceArray(
        IReadOnlyList<BattleSaveSource> sources
    )
    {
        var result = new Godot.Collections.Array();
        if (sources == null)
            return result;
        foreach (BattleSaveSource source in sources)
        {
            result.Add(BattleSaveResultProjection.Project(source));
        }
        return result;
    }
}
