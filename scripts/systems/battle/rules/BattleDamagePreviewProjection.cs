using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleDamagePreviewProjection
{
    internal static GodotProjectionLease<GDictionary> BuildLease(
        BattleDamagePreviewSaveEstimate estimate
    ) => BuildLease(root => WriteInto(root.Lease, root.Value, estimate), "save-estimate");

    internal static GodotProjectionLease<GDictionary> BuildLease(
        BattleDamagePreviewResult preview
    ) => BuildLease(root => WriteInto(root.Lease, root.Value, preview), "result");

    internal static GDictionary WriteOwned<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleDamagePreviewSaveEstimate estimate,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        WriteInto(lease, result, estimate);
        return result;
    }

    internal static GDictionary WriteOwned<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleDamagePreviewResult preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        WriteInto(lease, result, preview);
        return result;
    }

    private static GodotProjectionLease<GDictionary> BuildLease(
        Action<ProjectionRoot> writer,
        string suffix
    )
    {
        GDictionary root = new();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                $"battle-damage-preview-{suffix}",
                LifetimeDomain.Request,
                $"BattleDamagePreviewProjection.{suffix}.root"
            );
        try
        {
            writer(new ProjectionRoot(lease, root));
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static void WriteInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        BattleDamagePreviewSaveEstimate estimate
    )
        where TLeaseRoot : class, IDisposable
    {
        if (estimate == null)
            return;

        target["has_save"] = estimate.HasSave;
        target["damage_before_save"] = estimate.DamageBeforeSave;
        target["damage_after_save"] = estimate.DamageAfterSave;
        target["damage_after_save_estimate"] = estimate.DamageAfterSaveEstimate;
        target["damage_after_save_worst"] = estimate.DamageAfterSaveWorst;
        if (!estimate.HasSave)
            return;

        target["damage_on_save_failure"] = estimate.DamageOnSaveFailure;
        target["damage_on_save_success"] = estimate.DamageOnSaveSuccess;
        target["save_partial_on_success"] = estimate.SavePartialOnSuccess;
        target["save_success_probability_basis_points"] =
            estimate.SaveSuccessProbabilityBasisPoints;
        target["save_success_rate_percent"] = estimate.SaveSuccessRatePercent;
        target["save_failure_probability_basis_points"] =
            estimate.SaveFailureProbabilityBasisPoints;
        target["dc"] = estimate.Dc;
        target["ability"] = estimate.Ability;
        target["save_tag"] = estimate.SaveTag;
        target["advantage_state"] = estimate.AdvantageState;
        target["ability_value"] = estimate.AbilityValue;
        target["ability_modifier"] = estimate.AbilityModifier;
        target["bonus"] = estimate.Bonus;
        target["immune"] = estimate.Immune;
        target["sources"] = WriteSaveSources(
            lease,
            estimate.Sources,
            "BattleDamagePreviewProjection.save_estimate.sources"
        );
    }

    private static void WriteInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        BattleDamagePreviewResult preview
    )
        where TLeaseRoot : class, IDisposable
    {
        if (preview == null)
            return;

        target["applied"] = preview.Applied;
        target["pre_save_damage"] = preview.PreSaveDamage;
        target["post_save_damage"] = preview.PostSaveDamage;
        target["damage"] = preview.Damage;
        target["hp_damage"] = preview.HpDamage;
        target["healing"] = 0;
        target["incoming_budget_damage"] = preview.IncomingBudgetDamage;
        target["shield_absorbed"] = preview.ShieldAbsorbed;
        target["shield_broken"] = preview.ShieldBroken;
        target["shield_hp_before"] = preview.ShieldHpBefore;
        target["shield_hp_after"] = preview.ShieldHpAfter;
        target["damage_events"] = TraceDictionaryProjection.WriteArray(
            lease,
            preview.DamageEvents,
            "BattleDamagePreviewProjection.damage_events"
        );
        target["equipment_durability_events"] = EmptyArray(
            lease,
            "BattleDamagePreviewProjection.equipment_durability_events"
        );
        target["dispel_events"] = EmptyArray(
            lease,
            "BattleDamagePreviewProjection.dispel_events"
        );
        target["damage_dice_high_total_roll"] = false;
        target["skill_damage_dice_is_max"] = false;
        target["weapon_damage_dice_is_max"] = false;
        target["status_effect_ids"] = EmptyArray(
            lease,
            "BattleDamagePreviewProjection.status_effect_ids"
        );
        target["removed_status_effect_ids"] = EmptyArray(
            lease,
            "BattleDamagePreviewProjection.removed_status_effect_ids"
        );
        target["source_status_effect_ids"] = EmptyArray(
            lease,
            "BattleDamagePreviewProjection.source_status_effect_ids"
        );
        target["terrain_effect_ids"] = EmptyArray(
            lease,
            "BattleDamagePreviewProjection.terrain_effect_ids"
        );
        target["height_delta"] = 0;
        target["diagnostics"] = TraceDictionaryProjection.WriteArray(
            lease,
            preview.Diagnostics,
            "BattleDamagePreviewProjection.diagnostics"
        );
        target["save_estimates"] = WriteSaveEstimates(
            lease,
            preview.SaveEstimates,
            "BattleDamagePreviewProjection.save_estimates"
        );
        target["stable_lethal"] = preview.StableLethal;
        target["lethal_probability_basis_points"] = preview.LethalProbabilityBasisPoints;

        if (preview.RollMode != default)
            target["roll_mode"] = preview.RollMode.ToString();
        if (preview.SaveMode != default)
            target["save_mode"] = preview.SaveMode.ToString();
        if (preview.DamageOutcome.Count > 0)
            target["damage_outcome"] = TraceDictionaryProjection.WriteDictionary(
                lease,
                preview.DamageOutcome,
                "BattleDamagePreviewProjection.damage_outcome"
            );
        if (preview.DamageResult.Count > 0)
            target["damage_result"] = TraceDictionaryProjection.WriteDictionary(
                lease,
                preview.DamageResult,
                "BattleDamagePreviewProjection.damage_result"
            );
        if (preview.SaveEstimate != null)
            target["save_estimate"] = WriteOwned(
                lease,
                preview.SaveEstimate,
                "BattleDamagePreviewProjection.save_estimate"
            );
        if (!string.IsNullOrEmpty(preview.ErrorCode))
            target["error_code"] = preview.ErrorCode;
        if (preview.SourcePreviewAfter != null)
            target["source_preview_after"] = BattleUnitStatePlainSnapshot.WriteOwned(
                lease,
                preview.SourcePreviewAfter,
                "BattleDamagePreviewProjection.source_preview_after"
            );
        if (preview.TargetPreviewAfter != null)
            target["target_preview_after"] = BattleUnitStatePlainSnapshot.WriteOwned(
                lease,
                preview.TargetPreviewAfter,
                "BattleDamagePreviewProjection.target_preview_after"
            );
    }

    private static GArray WriteSaveEstimates<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<BattleDamagePreviewSaveEstimate> estimates,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (estimates == null)
            return result;
        for (int index = 0; index < estimates.Count; index++)
        {
            BattleDamagePreviewSaveEstimate estimate = estimates[index];
            if (estimate != null && estimate.HasSave)
                result.Add(WriteOwned(lease, estimate, $"{reason}[{index}]"));
        }
        return result;
    }

    private static GArray WriteSaveSources<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<BattleSaveSource> sources,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (sources == null)
            return result;
        for (int index = 0; index < sources.Count; index++)
        {
            BattleSaveSource source = sources[index];
            GDictionary payload = lease.Own(new GDictionary(), $"{reason}[{index}]");
            payload["source_id"] = source.SourceId.ToString();
            payload["type"] = source.Type ?? "";
            payload["tag"] = source.Tag.ToString();
            payload["mode"] = source.Mode.ToString();
            result.Add(payload);
        }
        return result;
    }

    private static GArray EmptyArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        string reason
    )
        where TLeaseRoot : class, IDisposable => lease.Own(new GArray(), reason);

    private readonly record struct ProjectionRoot(
        GodotProjectionLease<GDictionary> Lease,
        GDictionary Value
    );
}
