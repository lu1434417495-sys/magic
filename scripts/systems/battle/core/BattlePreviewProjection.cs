using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattlePreviewProjection
{
    internal static GodotProjectionLease<GDictionary> BuildLease(BattlePreview preview)
    {
        GDictionary root = new();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                "battle-preview",
                LifetimeDomain.Request,
                "BattlePreviewProjection.root"
            );
        try
        {
            WriteInto(lease, root, preview);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static GodotProjectionLease<GDictionary> BuildSaveBranchLease(
        BattleSaveBranchPreviewData preview
    )
    {
        GDictionary root = new();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                "battle-save-branch-preview",
                LifetimeDomain.Request,
                "BattlePreviewProjection.save_branch_preview.root"
            );
        try
        {
            WriteSaveBranchInto(lease, root, preview, "BattlePreviewProjection.save_branch_preview");
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static void WriteInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        BattlePreview preview
    )
        where TLeaseRoot : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(target);
        preview ??= new BattlePreview();

        target["allowed"] = preview.allowed;
        target["log_lines"] = WriteArray(
            lease,
            preview.LogLinesTyped,
            "BattlePreviewProjection.log_lines"
        );
        target["target_unit_ids"] = WriteArray(
            lease,
            preview.TargetUnitIdsTyped,
            "BattlePreviewProjection.target_unit_ids"
        );
        target["target_coords"] = WriteArray(
            lease,
            preview.TargetCoordsTyped,
            "BattlePreviewProjection.target_coords"
        );
        target["random_chain_candidate_unit_ids"] = WriteArray(
            lease,
            preview.RandomChainCandidateUnitIdsTyped,
            "BattlePreviewProjection.random_chain_candidate_unit_ids"
        );
        target["resolved_anchor_coord"] = preview.resolved_anchor_coord;
        target["move_cost"] = preview.move_cost;
        target["hit_preview"] = WriteAttackPreview(
            lease,
            preview.hit_preview,
            "BattlePreviewProjection.hit_preview"
        );
        target["damage_preview"] = BattleDamagePreviewRangeProjection.WriteOwned(
            lease,
            preview.DamagePreviewTyped,
            "BattlePreviewProjection.damage_preview"
        );
        target["fate_preview"] = WriteFatePreview(
            lease,
            preview.FatePreviewTyped,
            "BattlePreviewProjection.fate_preview"
        );
        target["save_branch_preview"] = WriteSaveBranchPreview(
            lease,
            preview.SaveBranchPreviewTyped,
            "BattlePreviewProjection.save_branch_preview"
        );
        target["special_profile_gate_result"] = WriteSpecialProfileGate(
            lease,
            preview.special_profile_gate_result,
            "BattlePreviewProjection.special_profile_gate_result"
        );
        target["special_profile_preview_facts"] = WriteSpecialProfileFacts(
            lease,
            preview.special_profile_preview_facts,
            "BattlePreviewProjection.special_profile_preview_facts"
        );
    }

    private static GDictionary WriteAttackPreview<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        AttackPreviewData preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview == null)
            return result;
        result["summary_text"] = preview.SummaryText ?? "";
        result["source"] = preview.Source ?? "";
        result["hit_rate_percent"] = preview.HitRatePercent;
        result["success_rate_percent"] = preview.SuccessRatePercent;
        result["base_hit_rate_percent"] = preview.BaseHitRatePercent;
        result["force_hit_no_crit"] = preview.ForceHitNoCrit;
        result["force_critical_on_hit"] = preview.ForceCriticalOnHit;
        result["crit_locked"] = preview.CritLocked;
        result["stage_hit_rates"] = WriteStageValues(
            lease,
            preview.Stages,
            static stage => stage.HitRatePercent,
            $"{reason}.stage_hit_rates"
        );
        result["stage_success_rates"] = WriteStageValues(
            lease,
            preview.Stages,
            static stage => stage.SuccessRatePercent,
            $"{reason}.stage_success_rates"
        );
        result["stage_base_hit_rates"] = WriteStageValues(
            lease,
            preview.Stages,
            static stage => stage.BaseHitRatePercent,
            $"{reason}.stage_base_hit_rates"
        );
        result["stage_required_rolls"] = WriteStageValues(
            lease,
            preview.Stages,
            static stage => stage.DisplayRequiredRoll,
            $"{reason}.stage_required_rolls"
        );
        result["stage_preview_texts"] = WriteStageValues(
            lease,
            preview.Stages,
            static stage => stage.PreviewText ?? "",
            $"{reason}.stage_preview_texts"
        );
        result["attack_roll_modifier_breakdown"] = WriteModifierBreakdown(
            lease,
            preview.AttackRollModifierBreakdownTyped,
            $"{reason}.attack_roll_modifier_breakdown"
        );
        return result;
    }

    private static GDictionary WriteFatePreview<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleFatePreviewData preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview == null)
            return result;
        result["uses_fate_attack"] = preview.UsesFateAttack;
        result["force_hit_no_crit"] = preview.ForceHitNoCrit;
        result["force_critical_on_hit"] = preview.ForceCriticalOnHit;
        result["is_disadvantage"] = preview.IsDisadvantage;
        result["effective_luck"] = preview.EffectiveLuck;
        result["crit_gate_die"] = preview.CritGateDie;
        result["fumble_low_end"] = preview.FumbleLowEnd;
        result["crit_threshold"] = preview.CritThreshold;
        result["crit_locked"] = preview.CritLocked;
        result["mercy_active"] = preview.MercyActive;
        return result;
    }

    internal static GDictionary WriteSaveBranchPreview<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSaveBranchPreviewData preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        WriteSaveBranchInto(lease, result, preview, reason);
        return result;
    }

    private static void WriteSaveBranchInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary result,
        BattleSaveBranchPreviewData preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        if (preview == null || preview.IsEmpty)
            return;
        if (preview.HasKnownPayload())
        {
            result["kind"] = preview.Kind;
            result["branch"] = preview.Branch;
            result["save_tag"] = preview.SaveTag;
            result["save_ability"] = preview.SaveAbility;
            result["save_dc"] = preview.SaveDc;
            result["save_advantage_state"] = preview.SaveAdvantageState;
            result["save_success_chance_basis_points"] =
                preview.SaveSuccessChanceBasisPoints;
            result["hit_chance_basis_points"] = preview.HitChanceBasisPoints;
            result["threshold"] = preview.Threshold;
            result["current_hp"] = preview.CurrentHp;
            result["max_hp"] = preview.MaxHp;
            result["failure_branch_text"] = preview.FailureBranchText ?? "";
            result["success_branch_text"] = preview.SuccessBranchText ?? "";
            result["summary_text"] = preview.SummaryText ?? "";
        }

        var residual = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (
            KeyValuePair<string, object> entry in
            preview.ResidualValues
                ?? new Dictionary<string, object>(StringComparer.Ordinal)
        )
        {
            if (!string.IsNullOrEmpty(entry.Key) && !BattleSaveBranchPreviewData.IsKnownKey(entry.Key))
                residual[entry.Key] = entry.Value;
        }
        TraceDictionaryProjection.WriteInto(lease, result, residual, $"{reason}.residual");
    }

    private static GDictionary WriteSpecialProfileGate<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSpecialProfileGateResult gate,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (gate == null)
            return result;
        result["allowed"] = gate.Allowed;
        result["profile_id"] = gate.ProfileId;
        result["skill_id"] = gate.SkillId;
        result["block_code"] = gate.BlockCode;
        result["player_message"] = gate.PlayerMessage ?? "";
        result["debug_details"] = TraceDictionaryProjection.WriteDictionary(
            lease,
            gate.DebugDetails,
            $"{reason}.debug_details"
        );
        return result;
    }

    private static GDictionary WriteSpecialProfileFacts<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleSpecialProfilePreviewFacts facts,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        if (facts == null)
            return lease.Own(new GDictionary(), reason);
        return TraceDictionaryProjection.WriteDictionary(
            lease,
            facts.ToTraceDictionary(),
            reason
        );
    }

    private static GArray WriteModifierBreakdown<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<BattleAttackRollModifierSpec> specs,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (specs == null)
            return result;
        for (int index = 0; index < specs.Count; index++)
        {
            BattleAttackRollModifierSpec spec = specs[index];
            if (spec != null)
                result.Add(
                    TraceDictionaryProjection.WriteDictionary(
                        lease,
                        spec.ToTraceDictionary(),
                        $"{reason}[{index}]"
                    )
                );
        }
        return result;
    }

    private static GArray WriteStageValues<TLeaseRoot, TValue>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<AttackPreviewStage> stages,
        Func<AttackPreviewStage, TValue> selector,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (stages == null)
            return result;
        foreach (AttackPreviewStage stage in stages)
            result.Add(ToVariant(selector(stage), reason));
        return result;
    }

    private static GArray WriteArray<TLeaseRoot, TValue>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<TValue> values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (values == null)
            return result;
        foreach (TValue value in values)
            result.Add(ToVariant(value, reason));
        return result;
    }

    private static Variant ToVariant<TValue>(TValue value, string reason)
    {
        object boxed = value;
        return boxed switch
        {
            null => Variant.From(""),
            string text => Variant.From(text),
            StringName name => Variant.From(name),
            Vector2I coord => Variant.From(coord),
            int number => Variant.From(number),
            bool flag => Variant.From(flag),
            _ => throw new InvalidOperationException(
                $"Unsupported BattlePreview projection value type: {boxed.GetType().FullName} ({reason})."
            ),
        };
    }
}
