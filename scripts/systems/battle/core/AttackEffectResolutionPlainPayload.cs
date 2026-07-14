using System;
using System.Collections.Generic;
using Godot;

internal static class AttackEffectResolutionPlainPayload
{
    internal static Dictionary<string, object> Build(AttackEffectResolutionResult result)
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["applied"] = result.Applied,
            ["damage"] = result.Damage,
            ["hp_damage"] = result.HpDamage,
            ["healing"] = result.Healing,
            ["shield_absorbed"] = result.ShieldAbsorbed,
            ["shield_broken"] = result.ShieldBroken,
            ["attack_success"] = result.AttackSuccess,
            ["attack_resolution"] = AttackEffectResolutionResultReader
                .AttackResolutionToStringName(result.AttackResolution)
                .ToString(),
            ["crit_locked"] = result.AttackCheck.CritLocked,
            ["critical_hit"] = result.CriticalHit,
            ["critical_fail"] = result.CriticalFail,
            ["secondary_hit_success"] = result.SecondaryHitSuccess,
            ["critical_source"] = AttackEffectResolutionResultReader
                .CriticalSourceToStringName(result.CriticalSource)
                .ToString(),
            ["reverse_fate_downgraded"] = result.ReverseFateDowngraded,
            ["hit_roll"] = result.HitRoll,
            ["reroll_die"] = result.RerollDie,
            ["rerolled_roll"] = result.RerolledRoll,
            ["crit_gate_die"] = result.CritGateDie,
            ["crit_gate_roll"] = result.CritGateRoll,
            ["required_roll"] = result.RequiredRoll,
            ["display_required_roll"] = result.DisplayRequiredRoll,
            ["hit_rate_percent"] = result.HitRatePercent,
            ["success_rate_percent"] = result.SuccessRatePercent,
            ["resolution_text"] = result.ResolutionText ?? "",
            ["skill_id"] = (result.SkillId ?? new StringName("")).ToString(),
            ["status_effect_ids"] = BuildStringNames(result.StatusEffectIds),
            ["removed_status_effect_ids"] = BuildStringNames(result.RemovedStatusEffectIds),
            ["source_status_effect_ids"] = BuildStringNames(result.SourceStatusEffectIds),
            ["terrain_effect_ids"] = BuildStringNames(result.TerrainEffectIds),
            ["height_delta"] = result.HeightDelta,
            ["execute_stage"] = result.ExecuteStage,
            ["execute_outcome"] = AttackEffectResolutionResultReader
                .ExecuteOutcomeToStringName(result.ExecuteOutcome)
                .ToString(),
            ["error_code"] = result.ErrorCode ?? "",
            ["blocked_reason"] = result.BlockedReason ?? "",
            ["damage_events"] = BuildDamageEvents(result.DamageEvents),
            ["equipment_durability_events"] = BuildEquipmentDurabilityEvents(
                result.EquipmentDurabilityEvents
            ),
            ["dispel_events"] = BuildDispelEvents(result.DispelEvents),
            ["save_results"] = BuildSaveResults(result.SaveResults),
            ["diagnostics"] = BuildDiagnostics(result.Diagnostics),
            ["trait_trigger_results"] = BuildTraitTriggerResults(result.TraitTriggerResults),
            ["damage_dice_high_total_roll"] = result.DamageDiceHighTotalRoll,
            ["skill_damage_dice_is_max"] = result.SkillDamageDiceIsMax,
            ["weapon_damage_dice_is_max"] = result.WeaponDamageDiceIsMax,
        };
        if (result.HasReportEntry)
        {
            payload["report_entry"] = BattleReportEntryPayload.BuildPlainPayload(
                result.ReportEntry
            );
        }
        return payload;
    }

    private static List<string> BuildStringNames(IEnumerable<StringName> values)
    {
        var result = new List<string>();
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            result.Add(value.ToString());
        }
        return result;
    }

    private static List<Dictionary<string, object>> BuildDamageEvents(
        DamageEventResult[] values
    )
    {
        var result = new List<Dictionary<string, object>>();
        foreach (DamageEventResult value in values ?? Array.Empty<DamageEventResult>())
        {
            result.Add(BuildDamageEvent(value));
        }
        return result;
    }

    internal static Dictionary<string, object> BuildDamageEvent(DamageEventResult value)
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["damage_tag"] = (value.DamageTag ?? new StringName("")).ToString(),
            ["damage"] = value.Damage,
            ["hp_damage"] = value.HpDamage,
            ["shield_absorbed"] = value.ShieldAbsorbed,
            ["shield_broken"] = value.ShieldBroken,
            ["bypass_shield"] = value.BypassShield,
            ["bypass_death_prevention"] = value.BypassDeathPrevention,
            ["shield_absorption_percent"] = value.ShieldAbsorptionPercent,
            ["min_hp_after_damage"] = value.MinHpAfterDamage,
            ["mitigation_tier"] = AttackEffectResolutionResultReader
                .MitigationTierToStringName(value.MitigationTier)
                .ToString(),
            ["base_damage"] = value.BaseDamage,
            ["critical_hit"] = value.CriticalHit,
            ["add_weapon_dice"] = value.AddWeaponDice,
            ["bonus_condition_met"] = value.BonusConditionMet,
            ["offense_multiplier"] = value.OffenseMultiplier,
            ["rolled_damage"] = value.RolledDamage,
            ["tier_adjusted_damage"] = value.TierAdjustedDamage,
            ["resolved_damage"] = value.ResolvedDamage,
            ["buff_reduction"] = value.BuffReduction,
            ["stance_reduction"] = value.StanceReduction,
            ["passive_reduction"] = value.PassiveReduction,
            ["content_dr"] = value.ContentDr,
            ["guard_block"] = value.GuardBlock,
            ["guard_ignore_applied"] = value.GuardIgnoreApplied,
            ["fixed_mitigation_total"] = value.FixedMitigationTotal,
            ["fully_absorbed_by_mitigation"] = value.FullyAbsorbedByMitigation,
            ["fully_absorbed_by_shield"] = value.FullyAbsorbedByShield,
            ["low_luck_black_star_wedge_triggered"] =
                value.LowLuckBlackStarWedgeTriggered,
            ["damage_dice_high_total_roll"] = value.DamageDiceHighTotalRoll,
            ["damage_dice_high_total_roll_reason"] =
                (value.DamageDiceHighTotalRollReason ?? new StringName("")).ToString(),
            ["skill_damage_dice_is_max"] = value.SkillDamageDiceIsMax,
            ["skill_damage_dice_is_max_reason"] = AttackEffectResolutionResultReader
                .DamageDiceMaxReasonToStringName(value.SkillDamageDiceIsMaxReason)
                .ToString(),
            ["weapon_damage_dice_is_max"] = value.WeaponDamageDiceIsMax,
            ["weapon_damage_dice_is_max_reason"] = AttackEffectResolutionResultReader
                .DamageDiceMaxReasonToStringName(value.WeaponDamageDiceIsMaxReason)
                .ToString(),
            ["save_success"] = value.SaveSuccess,
            ["save_immune"] = value.SaveImmune,
            ["save_partial_applied"] = value.SavePartialApplied,
            ["pre_save_damage"] = value.PreSaveDamage,
            ["save_adjusted_damage"] = value.SaveAdjustedDamage,
            ["save_success_probability_basis_points"] =
                value.SaveSuccessProbabilityBasisPoints,
            ["save_failure_probability_basis_points"] =
                value.SaveFailureProbabilityBasisPoints,
            ["fully_absorbed_by_save"] = value.FullyAbsorbedBySave,
            ["mitigation_sources"] =
                value.MitigationSources != null && value.MitigationSources.Length > 0
                    ? BuildMitigationSources(value.MitigationSources)
                    : BuildMitigationSources(
                        value.HalfSourceLabels,
                        value.DoubleSourceLabels,
                        value.ImmuneSourceLabels
                    ),
            ["fixed_mitigation_sources"] = BuildFixedMitigationSources(
                value.FixedMitigationSourceLabels
            ),
            ["trait_trigger_results"] = BuildTraitTriggerResults(value.TraitTriggerResults),
            [BattleDeathResolutionRules.DeathSourcePayloadKey] =
                (value.DeathSource ?? new StringName("")).ToString(),
            [BattleDeathResolutionRules.DeathSourcePriorityPayloadKey] =
                value.DeathSourcePriority,
        };
        AppendDice(payload, "damage_dice", value.DamageDice, includeBonus: true);
        AppendDice(payload, "bonus_damage_dice", value.BonusDamageDice, includeBonus: true);
        AppendDice(payload, "weapon_damage_dice", value.WeaponDamageDice, includeBonus: true);
        AppendDice(
            payload,
            "critical_extra_damage_dice",
            value.CriticalExtraDamageDice,
            includeBonus: false
        );
        AppendDice(
            payload,
            "critical_extra_bonus_damage_dice",
            value.CriticalExtraBonusDamageDice,
            includeBonus: false
        );
        AppendDice(
            payload,
            "critical_extra_weapon_damage_dice",
            value.CriticalExtraWeaponDamageDice,
            includeBonus: false
        );
        AppendDice(
            payload,
            "trait_extra_weapon_damage_dice",
            value.TraitExtraWeaponDamageDice,
            includeBonus: false
        );
        if (
            value.SourceBoundWeaponBonusSkillIds != null
            && value.SourceBoundWeaponBonusSkillIds.Length > 0
        )
        {
            payload["source_bound_weapon_bonus_skill_ids"] = BuildStringNames(
                value.SourceBoundWeaponBonusSkillIds
            );
        }
        payload["damage_dice_high_total_roll"] = value.DamageDiceHighTotalRoll;
        payload["damage_dice_high_total_roll_reason"] =
            (value.DamageDiceHighTotalRollReason ?? new StringName("")).ToString();
        payload["skill_damage_dice_is_max"] = value.SkillDamageDiceIsMax;
        payload["skill_damage_dice_is_max_reason"] = AttackEffectResolutionResultReader
            .DamageDiceMaxReasonToStringName(value.SkillDamageDiceIsMaxReason)
            .ToString();
        payload["weapon_damage_dice_is_max"] = value.WeaponDamageDiceIsMax;
        payload["weapon_damage_dice_is_max_reason"] = AttackEffectResolutionResultReader
            .DamageDiceMaxReasonToStringName(value.WeaponDamageDiceIsMaxReason)
            .ToString();
        if (value.SaveResult.HasSave || value.SaveResult.Dc > 0)
        {
            payload["save_result"] = BuildSaveResult(value.SaveResult);
        }
        return payload;
    }

    private static void AppendDice(
        Dictionary<string, object> payload,
        string prefix,
        DamageDiceRollDetail value,
        bool includeBonus
    )
    {
        payload[$"{prefix}_count"] = value.Count;
        payload[$"{prefix}_sides"] = value.Sides;
        var rolls = new List<object>();
        foreach (int roll in value.Rolls ?? Array.Empty<int>())
            rolls.Add(roll);
        payload[$"{prefix}_rolls"] = rolls;
        payload[$"{prefix}_total"] = value.Total;
        if (includeBonus)
        {
            payload[$"{prefix}_bonus"] = value.Bonus;
            payload[$"{prefix}_is_max"] = value.IsMax;
        }
        payload[$"{prefix}_max_total"] = value.MaxTotal;
    }

    private static List<Dictionary<string, object>> BuildEquipmentDurabilityEvents(
        EquipmentDurabilityEventResult[] values
    )
    {
        var result = new List<Dictionary<string, object>>();
        foreach (
            EquipmentDurabilityEventResult value
            in values ?? Array.Empty<EquipmentDurabilityEventResult>()
        )
        {
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["effect_type"] = (value.EffectType ?? new StringName("")).ToString(),
                    ["target_unit_id"] =
                        (value.TargetUnitId ?? new StringName("")).ToString(),
                    ["entry_slot_id"] =
                        (value.EntrySlotId ?? new StringName("")).ToString(),
                    ["slot_id"] = (value.SlotId ?? new StringName("")).ToString(),
                    ["item_id"] = (value.ItemId ?? new StringName("")).ToString(),
                    ["instance_id"] =
                        (value.EquipmentInstanceId ?? new StringName("")).ToString(),
                    ["equipment_instance_id"] =
                        (value.EquipmentInstanceId ?? new StringName("")).ToString(),
                    ["rarity"] = value.Rarity,
                    ["durability_loss"] = value.DurabilityLoss,
                    ["durability_before"] = value.DurabilityBefore,
                    ["durability_after"] = value.DurabilityAfter,
                    ["destroyed"] = value.Destroyed,
                    ["save_result"] = BuildSaveResult(value.SaveResult),
                }
            );
        }
        return result;
    }

    private static List<Dictionary<string, object>> BuildDispelEvents(DispelEventResult[] values)
    {
        var result = new List<Dictionary<string, object>>();
        foreach (DispelEventResult value in values ?? Array.Empty<DispelEventResult>())
        {
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["effect_type"] = (value.EffectType ?? new StringName("")).ToString(),
                    ["target_unit_id"] =
                        (value.TargetUnitId ?? new StringName("")).ToString(),
                    ["mode"] = value.Mode ?? "",
                    ["max_status_removed"] = value.MaxStatusRemoved,
                    ["removed_status_ids"] = BuildStringNames(value.RemovedStatusIds),
                }
            );
        }
        return result;
    }

    private static List<Dictionary<string, object>> BuildSaveResults(SaveResolutionResult[] values)
    {
        var result = new List<Dictionary<string, object>>();
        foreach (SaveResolutionResult value in values ?? Array.Empty<SaveResolutionResult>())
        {
            result.Add(BuildSaveResult(value));
        }
        return result;
    }

    private static Dictionary<string, object> BuildSaveResult(SaveResolutionResult value)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["has_save"] = value.HasSave,
            ["success"] = value.Success,
            ["immune"] = value.Immune,
            ["roll"] = value.Roll > 0 ? value.Roll : value.NaturalRoll,
            ["total"] = value.Total > 0 ? value.Total : value.RollTotal,
            ["natural_roll"] = value.NaturalRoll > 0 ? value.NaturalRoll : value.Roll,
            ["roll_total"] = value.RollTotal > 0 ? value.RollTotal : value.Total,
            ["dc"] = value.Dc,
            ["save_kind"] = (value.SaveKind != "" ? value.SaveKind : value.SaveTag).ToString(),
            ["ability"] = (value.Ability ?? new StringName("")).ToString(),
            ["save_tag"] = (value.SaveTag != "" ? value.SaveTag : value.SaveKind).ToString(),
            ["advantage_state"] =
                (value.AdvantageState ?? new StringName("")).ToString(),
            ["ability_value"] = value.AbilityValue,
            ["ability_modifier"] = value.AbilityModifier,
            ["bonus"] = value.Bonus,
            ["degree"] = value.Degree ?? "",
            ["sources"] = BuildSaveSources(value.Sources),
            ["damage_before_save"] = value.DamageBeforeSave,
            ["damage_after_save"] = value.DamageAfterSave,
            ["damage_after_save_estimate"] = value.DamageAfterSaveEstimate,
            ["damage_after_save_worst"] = value.DamageAfterSaveWorst,
            ["damage_on_save_failure"] = value.DamageOnSaveFailure,
            ["damage_on_save_success"] = value.DamageOnSaveSuccess,
            ["save_partial_on_success"] = value.SavePartialOnSuccess,
            ["save_success_probability_basis_points"] =
                value.SaveSuccessProbabilityBasisPoints,
            ["save_success_rate_percent"] = value.SaveSuccessRatePercent,
            ["save_failure_probability_basis_points"] =
                value.SaveFailureProbabilityBasisPoints,
            ["equipment_rarity_bonus"] = value.EquipmentRarityBonus,
            ["status_save_bonus"] = value.StatusSaveBonus,
        };
    }

    private static List<Dictionary<string, object>> BuildSaveSources(BattleSaveSource[] values)
    {
        var result = new List<Dictionary<string, object>>();
        foreach (BattleSaveSource value in values ?? Array.Empty<BattleSaveSource>())
        {
            result.Add(value.ToTraceDictionary());
        }
        return result;
    }

    private static List<Dictionary<string, object>> BuildTraitTriggerResults(
        TraitTriggerEventResult[] values
    )
    {
        var result = new List<Dictionary<string, object>>();
        foreach (TraitTriggerEventResult value in values ?? Array.Empty<TraitTriggerEventResult>())
        {
            if (!value.Triggered)
            {
                continue;
            }
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["triggered"] = value.Triggered,
                    ["event"] = (value.Event ?? new StringName("")).ToString(),
                    ["trait_id"] = (value.TraitId ?? new StringName("")).ToString(),
                    ["effect_type"] =
                        (value.EffectType ?? new StringName("")).ToString(),
                    ["original_roll"] = value.OriginalRoll,
                    ["reroll_die"] = value.RerollDie,
                    ["rerolled_roll"] = value.RerolledRoll,
                    ["die_size"] = value.DieSize,
                    ["extra_weapon_dice_count"] = value.ExtraWeaponDiceCount,
                    ["extra_weapon_dice_sides"] = value.ExtraWeaponDiceSides,
                    ["clamp_to_hp"] = value.ClampToHp,
                    ["projected_hp"] = value.ProjectedHp,
                    ["hp_damage"] = value.HpDamage,
                    ["charge_key"] = (value.ChargeKey ?? new StringName("")).ToString(),
                    ["charges_remaining"] = value.ChargesRemaining,
                }
            );
        }
        return result;
    }

    private static List<Dictionary<string, object>> BuildDiagnostics(
        ResolutionDiagnostic[] values
    )
    {
        var result = new List<Dictionary<string, object>>();
        foreach (ResolutionDiagnostic value in values ?? Array.Empty<ResolutionDiagnostic>())
        {
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["error_code"] = value.ErrorCode ?? "",
                    ["message"] = value.Message ?? "",
                }
            );
        }
        return result;
    }

    private static List<Dictionary<string, object>> BuildMitigationSources(
        MitigationSourceResult[] values
    )
    {
        var result = new List<Dictionary<string, object>>();
        foreach (MitigationSourceResult value in values ?? Array.Empty<MitigationSourceResult>())
        {
            if (string.IsNullOrEmpty(value.StatusId))
            {
                continue;
            }
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["status_id"] = value.StatusId,
                    ["type"] = value.Type ?? "",
                    ["value"] = value.Value,
                    ["tier"] = AttackEffectResolutionResultReader
                        .MitigationTierToStringName(value.Tier)
                        .ToString(),
                }
            );
        }
        return result;
    }

    private static List<Dictionary<string, object>> BuildMitigationSources(
        string[] halfSources,
        string[] doubleSources,
        string[] immuneSources
    )
    {
        var result = new List<Dictionary<string, object>>();
        AppendMitigationSources(result, halfSources, MitigationTierKind.Half);
        AppendMitigationSources(result, doubleSources, MitigationTierKind.Double);
        AppendMitigationSources(result, immuneSources, MitigationTierKind.Immune);
        return result;
    }

    private static void AppendMitigationSources(
        List<Dictionary<string, object>> result,
        string[] labels,
        MitigationTierKind tier
    )
    {
        foreach (string label in labels ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(label))
            {
                continue;
            }
            result.Add(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["tier"] = AttackEffectResolutionResultReader
                        .MitigationTierToStringName(tier)
                        .ToString(),
                    ["status_id"] = label,
                }
            );
        }
    }

    private static List<Dictionary<string, object>> BuildFixedMitigationSources(string[] labels)
    {
        var result = new List<Dictionary<string, object>>();
        foreach (string label in labels ?? Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(label))
            {
                result.Add(
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["status_id"] = label,
                    }
                );
            }
        }
        return result;
    }
}
