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
        target["source_retreat_path"] = WriteArray(
            lease,
            preview.SourceRetreatPathTyped,
            "BattlePreviewProjection.source_retreat_path"
        );
        target["source_advance_path"] = WriteArray(
            lease,
            preview.SourceAdvancePathTyped,
            "BattlePreviewProjection.source_advance_path"
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
        target["status_contribution_previews"] = WriteStatusContributionPreviews(
            lease,
            preview.StatusContributionPreviewsTyped,
            "BattlePreviewProjection.status_contribution_previews"
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
        target["equipment_ability_preview"] = WriteEquipmentAbilityPreview(
            lease,
            preview.EquipmentAbilityPreviewTyped,
            "BattlePreviewProjection.equipment_ability_preview"
        );
        target["terrain_contact_preview"] = WriteTerrainContactPreview(
            lease,
            preview.TerrainContactPreviewTyped,
            "BattlePreviewProjection.terrain_contact_preview"
        );
        target["shield_preview"] = WriteShieldPreview(
            lease,
            preview.ShieldPreviewTyped,
            "BattlePreviewProjection.shield_preview"
        );
        target["equipment_durability_preview"] = WriteEquipmentDurabilityPreview(
            lease,
            preview.EquipmentDurabilityPreviewTyped,
            "BattlePreviewProjection.equipment_durability_preview"
        );
        target["forced_move_preview"] = WriteForcedMovePreview(
            lease,
            preview.ForcedMovePreviewTyped,
            "BattlePreviewProjection.forced_move_preview"
        );
        target["position_swap_preview"] = WritePositionSwapPreview(
            lease,
            preview.PositionSwapPreviewTyped,
            "BattlePreviewProjection.position_swap_preview"
        );
        target["ranged_weapon_reaction_preview"] = WriteRangedWeaponReactionPreview(
            lease,
            preview.RangedWeaponReactionPreviewTyped,
            "BattlePreviewProjection.ranged_weapon_reaction_preview"
        );
        target["chain_damage_preview"] = WriteChainDamagePreview(
            lease,
            preview.ChainDamagePreviewTyped,
            "BattlePreviewProjection.chain_damage_preview"
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

    private static GDictionary WriteChainDamagePreview<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleChainDamagePreviewData preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview == null)
            return result;
        result["primary_target_unit_id"] = preview.PrimaryTargetUnitId;
        result["normal_reached_target_count"] = preview.NormalReachedTargetCount;
        result["summary_text"] = preview.SummaryText;
        result["normal_hops"] = WriteChainDamageHops(
            lease,
            preview.NormalHops,
            $"{reason}.normal_hops"
        );
        result["backlash_hops"] = WriteChainDamageHops(
            lease,
            preview.BacklashHops,
            $"{reason}.backlash_hops"
        );
        return result;
    }

    private static GArray WriteChainDamageHops<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<BattleChainDamagePreviewHopData> hops,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        foreach (
            BattleChainDamagePreviewHopData hop in hops
                ?? Array.Empty<BattleChainDamagePreviewHopData>()
        )
        {
            GDictionary entry = lease.Own(
                new GDictionary(),
                $"{reason}[{result.Count}]"
            );
            entry["hop_index"] = hop.HopIndex;
            entry["origin_unit_id"] = hop.OriginUnitId;
            entry["origin_coord"] = hop.OriginCoord;
            entry["target_unit_id"] = hop.TargetUnitId;
            entry["target_coord"] = hop.TargetCoord;
            entry["distance"] = hop.Distance;
            entry["outgoing_range"] = hop.OutgoingRange;
            entry["origin_was_conductive"] = hop.OriginWasConductive;
            entry["blocked"] = hop.Blocked;
            result.Add(entry);
        }
        return result;
    }

    private static GDictionary WriteRangedWeaponReactionPreview<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleRangedWeaponReactionPreviewData preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview == null)
            return result;
        result["orb_count"] = preview.OrbCount;
        result["duration_tu"] = preview.DurationTu;
        result["consume_per_trigger"] = preview.ConsumePerTrigger;
        result["attack_roll_bonus"] = preview.AttackRollBonus;
        result["attack_defense_mode"] = preview.AttackDefenseMode;
        result["damage_tag"] = preview.DamageTag;
        result["trigger_on_hit"] = preview.TriggerOnHit;
        result["trigger_on_miss"] = preview.TriggerOnMiss;
        result["allow_critical"] = preview.AllowCritical;
        result["summary_text"] = preview.SummaryText;
        result["trigger_weapon_families"] = WriteArray(
            lease,
            preview.TriggerWeaponFamilies,
            $"{reason}.trigger_weapon_families"
        );
        return result;
    }

    private static GDictionary WriteEquipmentAbilityPreview<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleEquipmentAbilityCommandPreviewResult preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        return BattleEquipmentAbilityPreviewProjection.WriteCommand(
            lease,
            preview,
            reason
        );
    }

    private static GDictionary WriteTerrainContactPreview<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleTerrainContactPreviewData preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview == null)
        {
            return result;
        }
        result["contact_mode"] = preview.ContactMode;
        result["save_dc"] = preview.SaveDc;
        result["save_ability"] = preview.SaveAbility;
        result["save_ability"] = preview.SaveAbility;
        result["effective_trigger_count"] = preview.EffectiveTriggerCount;
        result["duration_tu"] = preview.DurationTu;
        result["rechecks_from_inside"] = preview.RechecksFromInside;
        result["requires_ground_contact"] = preview.RequiresGroundContact;
        return result;
    }

    private static GDictionary WriteShieldPreview<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleShieldPreviewData preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview == null || !preview.HasShield)
        {
            return result;
        }
        result["min_shield_hp"] = preview.MinShieldHp;
        result["max_shield_hp"] = preview.MaxShieldHp;
        result["expected_shield_hp_basis_points"] = preview.ExpectedShieldHpBasisPoints;
        result["attribute_modifier"] = preview.AttributeModifier;
        result["duration_tu"] = preview.DurationTu;
        result["target_count"] = preview.TargetCount;
        result["expected_benefiting_target_count"] = preview.ExpectedBenefitingTargetCount;
        result["expected_total_net_gain_basis_points"] =
            preview.ExpectedTotalNetGainBasisPoints;
        result["roll_per_target"] = preview.RollPerTarget;
        result["shield_family"] = preview.ShieldFamily;
        result["summary_text"] = preview.SummaryText;
        return result;
    }

    private static GDictionary WriteEquipmentDurabilityPreview<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleEquipmentDurabilityPreviewData preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview == null || !preview.HasEffect)
            return result;
        result["has_matching_equipment"] = preview.HasMatchingEquipment;
        result["target_unit_id"] = preview.TargetUnitId;
        result["entry_slot_id"] = preview.EntrySlotId;
        result["slot_id"] = preview.SlotId;
        result["item_id"] = preview.ItemId;
        result["item_display_name"] = preview.ItemDisplayName;
        result["rarity"] = preview.Rarity;
        result["current_durability"] = preview.CurrentDurability;
        result["maximum_durability"] = preview.MaximumDurability;
        result["durability_loss_on_failed_save"] = preview.DurabilityLossOnFailedSave;
        result["durability_after_failed_save"] = preview.DurabilityAfterFailedSave;
        result["save_dc"] = preview.SaveDc;
        result["equipment_rarity_save_bonus"] = preview.EquipmentRaritySaveBonus;
        result["save_success_probability_basis_points"] =
            preview.SaveSuccessProbabilityBasisPoints;
        result["save_failure_probability_basis_points"] =
            preview.SaveFailureProbabilityBasisPoints;
        result["expected_durability_loss_basis_points"] =
            preview.ExpectedDurabilityLossBasisPoints;
        result["destruction_probability_basis_points"] =
            preview.DestructionProbabilityBasisPoints;
        result["candidate_count"] = preview.CandidateCount;
        result["summary_text"] = preview.SummaryText;
        return result;
    }

    private static GDictionary WriteForcedMovePreview<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattleForcedMovePreviewData preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview == null)
            return result;
        result["mode"] = preview.Mode;
        result["target_unit_id"] = preview.TargetUnitId;
        result["source_coord"] = preview.SourceCoord;
        result["destination_coord"] = preview.DestinationCoord;
        result["distance"] = preview.Distance;
        result["maximum_distance"] = preview.MaximumDistance;
        result["target_body_size"] = preview.TargetBodySize;
        result["maximum_target_body_size"] = preview.MaximumTargetBodySize;
        result["ignores_intermediate_units"] = preview.IgnoresIntermediateUnits;
        result["ignores_height_difference"] = preview.IgnoresHeightDifference;
        result["applies_landing_contact"] = preview.AppliesLandingContact;
        result["applies_contact_per_entered_cell"] = preview.AppliesContactPerEnteredCell;
        result["summary_text"] = preview.SummaryText ?? "";
        GArray targetPreviews = lease.Own(new GArray(), $"{reason}.targets");
        for (int index = 0; index < preview.Targets.Count; index++)
        {
            BattleForcedMoveTargetPreviewData targetPreview = preview.Targets[index];
            if (targetPreview == null)
                continue;
            GDictionary targetPayload = lease.Own(
                new GDictionary(),
                $"{reason}.targets[{index}]"
            );
            targetPayload["target_unit_id"] = targetPreview.TargetUnitId;
            targetPayload["target_display_name"] = targetPreview.TargetDisplayName ?? "";
            targetPayload["source_coord"] = targetPreview.SourceCoord;
            targetPayload["destination_coord"] = targetPreview.DestinationCoord;
            targetPayload["distance"] = targetPreview.Distance;
            targetPayload["maximum_distance"] = targetPreview.MaximumDistance;
            targetPayload["target_body_size"] = targetPreview.TargetBodySize;
            targetPayload["maximum_target_body_size"] =
                targetPreview.MaximumTargetBodySize;
            targetPayload["save_dc"] = targetPreview.SaveDc;
            targetPayload["save_ability"] = targetPreview.SaveAbility;
            targetPayload["save_tag"] = targetPreview.SaveTag;
            targetPayload["save_success_probability_basis_points"] =
                targetPreview.SaveSuccessProbabilityBasisPoints;
            targetPayload["save_failure_probability_basis_points"] =
                targetPreview.SaveFailureProbabilityBasisPoints;
            targetPayload["can_move_on_failed_save"] = targetPreview.CanMoveOnFailedSave;
            targetPayload["block_reason"] = targetPreview.BlockReason ?? "";
            targetPreviews.Add(targetPayload);
        }
        result["targets"] = targetPreviews;
        return result;
    }

    private static GDictionary WritePositionSwapPreview<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        BattlePositionSwapPreviewData preview,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (preview == null)
            return result;
        result["source_unit_id"] = preview.SourceUnitId;
        result["target_unit_id"] = preview.TargetUnitId;
        result["source_from"] = preview.SourceFrom;
        result["source_to"] = preview.SourceTo;
        result["target_from"] = preview.TargetFrom;
        result["target_to"] = preview.TargetTo;
        result["requires_enemy_save"] = preview.RequiresEnemySave;
        result["save_dc"] = preview.SaveDc;
        result["save_ability"] = preview.SaveAbility;
        result["save_tag"] = preview.SaveTag;
        result["save_success_probability_basis_points"] =
            preview.SaveSuccessProbabilityBasisPoints;
        result["swap_probability_basis_points"] = preview.SwapProbabilityBasisPoints;
        result["summary_text"] = preview.SummaryText;
        return result;
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
        result["stage_reach_probability_basis_points"] = WriteStageValues(
            lease,
            preview.Stages,
            static stage => stage.ReachProbabilityBasisPoints,
            $"{reason}.stage_reach_probability_basis_points"
        );
        result["stage_damage_multiplier_percent"] = WriteStageValues(
            lease,
            preview.Stages,
            static stage => stage.DamageMultiplierPercent,
            $"{reason}.stage_damage_multiplier_percent"
        );
        result["repeat_attack_expected_damage_basis_points"] =
            preview.RepeatAttackExpectedDamageBasisPoints;
        result["repeat_attack_potential_damage_basis_points"] =
            preview.RepeatAttackPotentialDamageBasisPoints;
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

    private static GArray WriteStatusContributionPreviews<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<BattleStatusContributionPreviewData> previews,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (previews == null)
            return result;
        for (int index = 0; index < previews.Count; index++)
        {
            BattleStatusContributionPreviewData preview = previews[index];
            if (preview == null)
                continue;
            GDictionary payload = lease.Own(new GDictionary(), $"{reason}[{index}]");
            payload["target_unit_id"] = preview.TargetUnitId;
            payload["target_display_name"] = preview.TargetDisplayName ?? "";
            payload["status_id"] = preview.StatusId;
            payload["source_kind"] = preview.SourceKind;
            payload["source_definition_id"] = preview.SourceDefinitionId;
            payload["applies_on_save_failure"] = preview.AppliesOnSaveFailure;
            payload["adds_new_source"] = preview.AddsNewSource;
            payload["previous_source_stacks"] = preview.PreviousSourceStacks;
            payload["result_source_stacks"] = preview.ResultSourceStacks;
            payload["source_stack_limit"] = preview.SourceStackLimit;
            payload["result_aggregate_stacks"] = preview.ResultAggregateStacks;
            payload["result_source_count"] = preview.ResultSourceCount;
            payload["result_duration_tu"] = preview.ResultDurationTu;
            payload["tick_interval_tu"] = preview.TickIntervalTu;
            payload["status_display_name"] = preview.StatusDisplayName ?? "";
            if (preview.HealMultiplierPercent is int healMultiplierPercent)
                payload["heal_multiplier_percent"] = healMultiplierPercent;
            payload["summary_text"] = preview.SummaryText;
            result.Add(payload);
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
