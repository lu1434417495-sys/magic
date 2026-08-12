using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleEquipmentAbilityPreviewProjection
{
    internal static GDictionary WriteCommand<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleEquipmentAbilityCommandPreviewResult preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview == null)
            return result;
        result["triggered"] = preview.Triggered;
        result["source_unit_id"] = preview.SourceUnitId;
        result["actions"] = WriteActions(lease, preview.Actions, $"{reason}.actions");
        if (preview.SourceUnitAfter != null)
        {
            result["source_preview_after"] = BattleUnitStatePlainSnapshot.WriteOwned(
                lease,
                preview.SourceUnitAfter,
                $"{reason}.source_preview_after"
            );
        }
        return result;
    }

    internal static GDictionary WriteFatalIntercept<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleFatalInterceptPreviewResult preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview == null)
            return result;
        result["intercept_probability_basis_points"] =
            preview.InterceptProbabilityBasisPoints;
        result["guaranteed_intercept"] = preview.GuaranteedIntercept;
        result["expected_recovery_hp"] = preview.ExpectedRecoveryHp;
        result["expected_survival_hp"] = preview.ExpectedSurvivalHp;
        result["success_actions"] = WriteActions(
            lease,
            preview.SuccessActionPreviews,
            $"{reason}.success_actions"
        );

        GArray candidates = lease.Own(new GArray(), $"{reason}.candidates");
        int index = 0;
        foreach (
            BattleFatalInterceptCandidatePreview candidate
            in preview.Candidates ?? Array.Empty<BattleFatalInterceptCandidatePreview>()
        )
        {
            GDictionary entry = lease.Own(
                new GDictionary(),
                $"{reason}.candidates[{index}]"
            );
            entry["source_equipment_instance_id"] =
                candidate?.SourceEquipmentInstanceId ?? new StringName("");
            entry["binding_id"] = candidate?.BindingId ?? new StringName("");
            entry["intercept_id"] = candidate?.InterceptId ?? new StringName("");
            entry["resolution_order"] = candidate?.ResolutionOrder ?? 0;
            entry["protection_priority"] = candidate?.ProtectionPriority ?? 0;
            entry["available"] = candidate?.Available == true;
            entry["blocks_death_source"] = candidate?.BlocksDeathSource == true;
            entry["reach_probability_basis_points"] =
                candidate?.ReachProbabilityBasisPoints ?? 0;
            entry["success_probability_basis_points"] =
                candidate?.SuccessProbabilityBasisPoints ?? 0;
            entry["contribution_probability_basis_points"] =
                candidate?.ContributionProbabilityBasisPoints ?? 0;
            entry["expected_recovery_hp"] = candidate?.ExpectedRecoveryHp ?? 0;
            entry["success_actions"] = WriteActions(
                lease,
                candidate?.SuccessActionPreviews,
                $"{reason}.candidates[{index}].success_actions"
            );
            candidates.Add(entry);
            index++;
        }
        result["candidates"] = candidates;
        return result;
    }

    internal static GArray WriteActions<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<BattleEquipmentAbilityActionPreviewResult> actions,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        int index = 0;
        foreach (
            BattleEquipmentAbilityActionPreviewResult action
            in actions ?? Array.Empty<BattleEquipmentAbilityActionPreviewResult>()
        )
        {
            GDictionary entry = lease.Own(
                new GDictionary(),
                $"{reason}[{index}]"
            );
            entry["binding_id"] = action?.BindingId ?? new StringName("");
            entry["action_id"] = action?.ActionId ?? new StringName("");
            entry["action_kind"] = action?.ActionKind ?? new StringName("");
            entry["trigger_skill_id"] = action?.TriggerSkillId ?? new StringName("");
            entry["trigger_probability_basis_points"] =
                action?.TriggerProbabilityBasisPoints ?? 0;
            entry["guaranteed"] = action?.Guaranteed == true;
            entry["applied"] = action?.Applied == true;
            entry["conditional"] = action?.Conditional == true;
            entry["supported"] = action?.Supported == true;
            entry["unsupported_reason"] = action?.UnsupportedReason ?? "";
            GArray damagePreviews = lease.Own(
                new GArray(),
                $"{reason}[{index}].damage_previews"
            );
            int damageIndex = 0;
            foreach (
                BattleDamagePreviewResult damagePreview
                in action?.DamagePreviews ?? Array.Empty<BattleDamagePreviewResult>()
            )
            {
                damagePreviews.Add(
                    BattleDamagePreviewProjection.WriteOwned(
                        lease,
                        damagePreview,
                        $"{reason}[{index}].damage_previews[{damageIndex++}]"
                    )
                );
            }
            entry["damage_previews"] = damagePreviews;
            result.Add(entry);
            index++;
        }
        return result;
    }
}
