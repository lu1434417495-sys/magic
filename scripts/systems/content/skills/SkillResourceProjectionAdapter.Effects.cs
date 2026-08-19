#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using GdDictionary = Godot.Collections.Dictionary;

internal static partial class SkillResourceProjectionAdapter
{
    private static IReadOnlyList<CombatEffectJsonDto> Effects(
        JsonContentEntryContext context,
        Godot.Collections.Array<CombatEffectDef>? values,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var result = new List<CombatEffectJsonDto>();
        if (values == null)
            return result;
        for (int index = 0; index < values.Count; index++)
        {
            CombatEffectDef? value = values[index];
            if (value == null)
            {
                AddInvalid(context, $"{pointer}/{index}", "Combat effect must not be null.", diagnostics);
                continue;
            }
            CombatEffectJsonDto? dto = Effect(
                context,
                value,
                $"{pointer}/{index}",
                diagnostics,
                new HashSet<CombatEffectDef>(ReferenceEqualityComparer.Instance)
            );
            if (dto != null)
                result.Add(dto);
        }
        return result;
    }

    private static CombatEffectJsonDto? Effect(
        JsonContentEntryContext context,
        CombatEffectDef value,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics,
        HashSet<CombatEffectDef> ancestors
    )
    {
        if (!ancestors.Add(value))
        {
            AddInvalid(context, pointer, "Recursive combat effect Resource graph contains a reference cycle.", diagnostics);
            return null;
        }
        try
        {
        string effectType = Text(value.effect_type);
        if (!SkillJsonImportValueRules.TryParseEffectKind(effectType, out CombatEffectImportKind kind))
        {
            diagnostics.Add(new ContentJsonDiagnostic(
                SkillJsonImportRules.UnknownEffectKind,
                "Combat effect type is not registered by the closed import contract.",
                context.SourceLabel,
                context.JsonPointer + pointer + "/effect_type"
            ));
            return null;
        }

        JsonElement? payload = EffectPayload(context, kind, value.@params, pointer + "/payload", diagnostics);
        if (!payload.HasValue)
            return null;

        return new CombatEffectJsonDto
        {
            EffectType = effectType,
            MinSkillLevel = value.min_skill_level,
            MaxSkillLevel = value.max_skill_level,
            Power = value.power,
            DurationTu = value.duration_tu,
            Payload = payload.Value,
            TickEffectType = Text(value.tick_effect_type),
            LifetimePolicy = Text(value.lifetime_policy),
            HealToHpPercentFloor = value.heal_to_hp_percent_floor,
            HealMissingHpPercent = value.heal_missing_hp_percent,
            MoveCostDelta = value.move_cost_delta,
            RenderOverlayId = Text(value.render_overlay_id),
            OverlayPriority = value.overlay_priority,
            DisplayName = value.display_name ?? "",
            DoesNotStackWithStatusId = Text(value.does_not_stack_with_status_id),
            DoesNotStackWithStatusIds = Texts(value.does_not_stack_with_status_ids),
            DamageRatioPercent = value.damage_ratio_percent,
            PreResistanceDamageMultiplier = value.pre_resistance_damage_multiplier,
            WeaponDiceMultiplier = value.weapon_dice_multiplier,
            BonusWeaponDiceMultiplier = value.bonus_weapon_dice_multiplier,
            DamageTag = Text(value.damage_tag),
            DamageTags = Texts(value.damage_tags),
            MitigationBypassDamageTags = Texts(value.mitigation_bypass_damage_tags),
            MitigationBypassTiers = Texts(value.mitigation_bypass_tiers),
            DamageCategory = Text(value.damage_category),
            DrBypassTag = Text(value.dr_bypass_tag),
            HpRatioThresholdPercent = value.hp_ratio_threshold_percent,
            DiceCount = value.dice_count,
            DiceSides = value.dice_sides,
            DiceBonus = value.dice_bonus,
            DiceSidesBase = value.dice_sides_base,
            DiceSidesPerConstitutionMod = value.dice_sides_per_constitution_mod,
            DiceSidesPerWillpowerMod = value.dice_sides_per_willpower_mod,
            ShieldFamily = Text(value.shield_family),
            ShieldAttributeModifierId = Text(value.shield_attribute_modifier_id),
            ShieldRollPerTarget = value.shield_roll_per_target,
            BonusDamageDiceCount = value.bonus_damage_dice_count,
            BonusDamageDiceSides = value.bonus_damage_dice_sides,
            BonusDamageDiceBonus = value.bonus_damage_dice_bonus,
            BonusDamageSeparateEvent = value.bonus_damage_separate_event,
            SourceBoundWeaponBonusDamageDiceCount = value.source_bound_weapon_bonus_damage_dice_count,
            SourceBoundWeaponBonusDamageDiceSides = value.source_bound_weapon_bonus_damage_dice_sides,
            SourceBoundWeaponBonusDamageDiceBonus = value.source_bound_weapon_bonus_damage_dice_bonus,
            AddWeaponDice = value.add_weapon_dice,
            RequiresWeapon = value.requires_weapon,
            UseWeaponPhysicalDamageTag = value.use_weapon_physical_damage_tag,
            ResolveAsWeaponAttack = value.resolve_as_weapon_attack,
            AllowRepeatHitsAcrossSteps = value.allow_repeat_hits_across_steps,
            PathStepAreaPattern = Text(value.path_step_area_pattern),
            PathStepRadius = value.path_step_radius,
            PathStepLogLabel = value.path_step_log_label ?? "",
            RepeatHitStatusId = Text(value.repeat_hit_status_id),
            RepeatHitStatusThreshold = value.repeat_hit_status_threshold,
            RepeatHitStatusMinSkillLevel = value.repeat_hit_status_min_skill_level,
            RepeatHitStatusPower = value.repeat_hit_status_power,
            RepeatHitStatusDurationTu = value.repeat_hit_status_duration_tu,
            RepeatHitStatusLogTemplate = value.repeat_hit_status_log_template ?? "",
            PreventRepeatTarget = value.prevent_repeat_target,
            ChainBaseHopRange = value.chain_base_hop_range,
            ChainConductiveHopRange = value.chain_conductive_hop_range,
            ChainMaxTotalTargets = value.chain_max_total_targets,
            ChainConductiveStatusIds = Texts(value.chain_conductive_status_ids),
            ChainConductiveTerrainEffectIds = Texts(value.chain_conductive_terrain_effect_ids),
            ChainBacklashHopRangeBonus = value.chain_backlash_hop_range_bonus,
            StopOnMiss = value.stop_on_miss,
            StopOnTargetDown = value.stop_on_target_down,
            FixedAttackCount = value.fixed_attack_count,
            FollowUpDamageMultiplierPercent = value.follow_up_damage_multiplier_percent,
            FollowUpAttackRollBonusCurve = Copy(value.follow_up_attack_roll_bonus_curve),
            RemoveHarmful = value.remove_harmful,
            RemoveHarmfulFromAllies = value.remove_harmful_from_allies,
            RemoveBeneficial = value.remove_beneficial,
            RemoveBeneficialFromEnemies = value.remove_beneficial_from_enemies,
            RequireDamageApplied = value.require_damage_applied,
            MaxStatusRemoved = value.max_status_removed,
            MinHpAfterDamage = value.min_hp_after_damage,
            DeathPreventionPriority = value.death_prevention_priority,
            ThresholdBaseValue = value.threshold_base_value,
            ThresholdLevelAnchor = value.threshold_level_anchor,
            ThresholdLevelBonusPerDelta = value.threshold_level_bonus_per_delta,
            ThresholdMaxHpRatioPercent = value.threshold_max_hp_ratio_percent,
            ThresholdCapMaxHpRatioPercent = value.threshold_cap_max_hp_ratio_percent,
            SoulFractureDurationTu = value.soul_fracture_duration_tu,
            HealMultiplierPercent = value.heal_multiplier_percent,
            ShieldGainMultiplierPercent = value.shield_gain_multiplier_percent,
            AttackRollPenalty = value.attack_roll_penalty,
            AttackRollBonus = value.attack_roll_bonus,
            AttackRollAdvantage = value.attack_roll_advantage,
            ConsumeOnNextAttackCheck = value.consume_on_next_attack_check,
            ConsumeOnNextSave = value.consume_on_next_save,
            Undispellable = value.undispellable,
            DispellableMagic = value.dispellable_magic,
            DispellableHarmfulMagic = value.dispellable_harmful_magic,
            DispellableBeneficialMagic = value.dispellable_beneficial_magic,
            MitigationTier = Text(value.mitigation_tier),
            SecondaryHitDcBase = value.secondary_hit_dc_base,
            DebuffCountThreshold = value.debuff_count_threshold,
            BaseHeal = value.base_heal,
            HealPerLevel = value.heal_per_level,
            ConModBase = value.con_mod_base,
            ConModPer2Levels = value.con_mod_per_2_levels,
            EffectCategories = Texts(value.effect_categories),
            EffectTargetTeamFilter = Text(value.effect_target_team_filter),
            MaxAffectedTargets = value.max_affected_targets,
            ExcludeSource = value.exclude_source,
            TargetOrder = Text(value.target_order),
            RequiredTargetCreatureTypeTag = Text(value.required_target_creature_type_tag),
            RequiredTargetMinCognition = Text(value.required_target_min_cognition),
            StatusId = Text(value.status_id),
            AppliedStatusDurationTu = value.applied_status_duration_tu,
            TerrainEffectId = Text(value.terrain_effect_id),
            TerrainContactMode = Text(value.terrain_contact_mode),
            TerrainEffectiveTriggerCount = value.terrain_effective_trigger_count,
            TerrainRequiresGroundContact = value.terrain_requires_ground_contact,
            TerrainRecheckFromInside = value.terrain_recheck_from_inside,
            TerrainMaxActiveInstancesPerSource = value.terrain_max_active_instances_per_source,
            TerrainReplaceExistingFromSource = value.terrain_replace_existing_from_source,
            TerrainReplaceTo = Text(value.terrain_replace_to),
            HeightDelta = value.height_delta,
            BodySizeCategory = Text(value.body_size_category),
            ForcedMoveMode = Text(value.forced_move_mode),
            ForcedMoveDistance = value.forced_move_distance,
            ForcedMoveMaxTargetBodySize = value.forced_move_max_target_body_size,
            GrappleMaxHeightGain = value.grapple_max_height_gain,
            SourceRetreatDistance = value.source_retreat_distance,
            ChargeTrapImmunityMinSkillLevel = value.charge_trap_immunity_min_skill_level,
            JumpBaseBudget = value.jump_base_budget,
            JumpStrScale = value.jump_str_scale,
            JumpArcRatio = value.jump_arc_ratio,
            JumpRangeMultiplier = value.jump_range_multiplier,
            TickIntervalTu = value.tick_interval_tu,
            StackBehavior = Text(value.stack_behavior),
            StackLimit = value.stack_limit,
            BonusCondition = Text(value.bonus_condition),
            BonusConditionCreatureTypeTag = Text(value.bonus_condition_creature_type_tag),
            TriggerEvent = Text(value.trigger_event),
            TriggerCondition = Text(value.trigger_condition),
            TriggerStatusId = Text(value.trigger_status_id),
            SaveDc = value.save_dc,
            SaveDcBonus = value.save_dc_bonus,
            SaveDcMode = Text(value.save_dc_mode),
            SaveDcSourceAbility = Text(value.save_dc_source_ability),
            SaveAbility = Text(value.save_ability),
            SaveFailureStatusId = Text(value.save_failure_status_id),
            SaveFailureStatusOutcomes = WeightedOutcomes(context, value.save_failure_status_outcomes, pointer + "/save_failure_status_outcomes", diagnostics, ancestors),
            SavePartialOnSuccess = value.save_partial_on_success,
            SaveTag = Text(value.save_tag),
            ConsumedStatusId = Text(value.consumed_status_id),
            RequiredTargetStatusId = Text(value.required_target_status_id),
            RequiredTargetStatusMinStacks = value.required_target_status_min_stacks,
            RequiredTargetStatusSourceSelector = Text(value.required_target_status_source_selector),
            DicePerConsumedStack = value.dice_per_consumed_stack,
            DiceSidesPerStack = value.dice_sides_per_stack,
            ApGain = value.ap_gain,
            FreeMovePointsGain = value.free_move_points_gain,
            CountsAsDebuffOverride = value.counts_as_debuff_override,
            CountsAsDebuff = value.counts_as_debuff,
            LockCounterattack = value.lock_counterattack,
            LockGuard = value.lock_guard,
            LockDodgeBonus = value.lock_dodge_bonus,
            LockCrit = value.lock_crit,
            SkipTurn = value.skip_turn,
            BreakOnPositiveDamage = value.break_on_positive_damage,
            OnRemovedStatusId = Text(value.on_removed_status_id),
            OnRemovedStatusSaveImmunityTags = Texts(value.on_removed_status_save_immunity_tags),
            OnRemovedStatusUndispellable = value.on_removed_status_undispellable,
            OnRemovedStatusConsumeAfterNormalTurn = value.on_removed_status_consume_after_normal_turn,
            SaveBonus = value.save_bonus,
            ControlSaveBonus = value.control_save_bonus,
            PassiveReduction = value.passive_reduction,
            ContentDr = value.content_dr,
            GuardBlock = value.guard_block,
            RangeBonus = value.range_bonus,
            MainSkillLockOtherDebuffCount = value.main_skill_lock_other_debuff_count,
            MeleeComboStackGainBonus = value.melee_combo_stack_gain_bonus,
            ComboAttackBonusStatusId = Text(value.combo_attack_bonus_status_id),
            ComboAttackBonusStackDivisor = value.combo_attack_bonus_stack_divisor,
            UpkeepResource = Text(value.upkeep_resource),
            UpkeepIntervalTu = value.upkeep_interval_tu,
            UpkeepBaseCost = value.upkeep_base_cost,
            UpkeepEscalationIntervalTu = value.upkeep_escalation_interval_tu,
            UpkeepCostMultiplier = value.upkeep_cost_multiplier,
            BreakOnHardControl = value.break_on_hard_control,
            TerminationStatusId = Text(value.termination_status_id),
            TerminationStatusDurationTu = value.termination_status_duration_tu,
            TerminationAttackRollPenalty = value.termination_attack_roll_penalty,
            TerminationCooldownTu = value.termination_cooldown_tu,
            SaveAdvantageTags = Texts(value.save_advantage_tags),
            SaveDisadvantageTags = Texts(value.save_disadvantage_tags),
            SaveImmunityTags = Texts(value.save_immunity_tags),
            EffectTags = Texts(value.effect_tags),
            EquipmentDurabilitySlotWeights = SlotWeights(context, value.equipment_durability_slot_weights, pointer + "/equipment_durability_slot_weights", diagnostics),
            ExtraDamageSegments = DamageSegments(context, value.extra_damage_segments, pointer + "/extra_damage_segments", diagnostics),
            TargetDamageMultiplierRules = DamageMultiplierRules(context, value.target_damage_multiplier_rules, pointer + "/target_damage_multiplier_rules", diagnostics),
        };
        }
        finally
        {
            ancestors.Remove(value);
        }
    }

    private static IReadOnlyList<CombatWeightedStatusOutcomeJsonDto> WeightedOutcomes(JsonContentEntryContext context, Godot.Collections.Array<CombatWeightedStatusOutcomeDef>? values, string pointer, List<ContentJsonDiagnostic> diagnostics, HashSet<CombatEffectDef> ancestors)
    {
        var result = new List<CombatWeightedStatusOutcomeJsonDto>();
        if (values == null) return result;
        for (int index = 0; index < values.Count; index++)
        {
            CombatWeightedStatusOutcomeDef? value = values[index];
            if (value?.status_effect == null)
            {
                AddInvalid(context, $"{pointer}/{index}/status_effect", "Weighted outcome and status_effect must not be null.", diagnostics);
                continue;
            }
            CombatEffectJsonDto? effect = Effect(context, value.status_effect, $"{pointer}/{index}/status_effect", diagnostics, ancestors);
            if (effect != null) result.Add(new CombatWeightedStatusOutcomeJsonDto { OutcomeId = Text(value.outcome_id), Weight = value.weight, StatusEffect = effect });
        }
        return result;
    }

    private static IReadOnlyList<CombatEffectSlotWeightJsonDto> SlotWeights(JsonContentEntryContext context, Godot.Collections.Array<CombatEffectSlotWeightDef>? values, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        var result = new List<CombatEffectSlotWeightJsonDto>();
        if (values == null) return result;
        for (int index = 0; index < values.Count; index++)
        {
            CombatEffectSlotWeightDef? value = values[index];
            if (value == null) { AddInvalid(context, $"{pointer}/{index}", "Slot weight must not be null.", diagnostics); continue; }
            result.Add(new CombatEffectSlotWeightJsonDto { SlotId = Text(value.slot_id), Weight = value.weight });
        }
        return result;
    }

    private static IReadOnlyList<CombatDamageSegmentJsonDto> DamageSegments(JsonContentEntryContext context, Godot.Collections.Array<CombatDamageSegmentDef>? values, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        var result = new List<CombatDamageSegmentJsonDto>();
        if (values == null) return result;
        for (int index = 0; index < values.Count; index++)
        {
            CombatDamageSegmentDef? value = values[index];
            if (value == null) { AddInvalid(context, $"{pointer}/{index}", "Damage segment must not be null.", diagnostics); continue; }
            result.Add(new CombatDamageSegmentJsonDto
            {
                DamageTag = Text(value.damage_tag), DamageTags = Texts(value.damage_tags), MitigationBypassDamageTags = Texts(value.mitigation_bypass_damage_tags),
                MitigationBypassTiers = Texts(value.mitigation_bypass_tiers), Power = value.power, DiceCount = value.dice_count,
                DiceSides = value.dice_sides, DiceBonus = value.dice_bonus, DoubleDiceOnCritical = value.double_dice_on_critical,
                PreResistanceDamageMultiplier = value.pre_resistance_damage_multiplier,
            });
        }
        return result;
    }

    private static IReadOnlyList<CombatTargetDamageMultiplierRuleJsonDto> DamageMultiplierRules(JsonContentEntryContext context, Godot.Collections.Array<CombatTargetDamageMultiplierRuleDef>? values, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        var result = new List<CombatTargetDamageMultiplierRuleJsonDto>();
        if (values == null) return result;
        for (int index = 0; index < values.Count; index++)
        {
            CombatTargetDamageMultiplierRuleDef? value = values[index];
            if (value == null) { AddInvalid(context, $"{pointer}/{index}", "Damage multiplier rule must not be null.", diagnostics); continue; }
            result.Add(new CombatTargetDamageMultiplierRuleJsonDto
            {
                AnyCreatureTypeTags = Texts(value.any_creature_type_tags), AllCreatureTypeTags = Texts(value.all_creature_type_tags),
                ExcludedCreatureTypeTags = Texts(value.excluded_creature_type_tags), MultiplierPercent = value.multiplier_percent,
            });
        }
        return result;
    }

    private static JsonElement? EffectPayload(JsonContentEntryContext context, CombatEffectImportKind kind, GdDictionary? raw, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        var values = new StrictParams(context, raw, pointer, diagnostics);
        switch (SkillFullCombatEffectClosedSpec.GetPayloadShape(kind))
        {
            case CombatEffectPayloadShape.Empty:
                values.Allow();
                return values.IsValid ? JsonSerializer.SerializeToElement(new EmptyCombatEffectPayloadJsonDto(), SkillJsonImportSerializerContext.Default.EmptyCombatEffectPayloadJsonDto) : null;
            case CombatEffectPayloadShape.Status:
                values.Allow("breaks_barrier_layer", "source_skill_id");
                return values.IsValid ? JsonSerializer.SerializeToElement(new StatusEffectPayloadJsonDto { BreaksBarrierLayer = values.String("breaks_barrier_layer"), SourceSkillId = values.String("source_skill_id") }, SkillJsonImportSerializerContext.Default.StatusEffectPayloadJsonDto) : null;
            case CombatEffectPayloadShape.Heal:
                values.Allow("con_mod_heal");
                return values.IsValid ? JsonSerializer.SerializeToElement(new HealEffectPayloadJsonDto { ConModHeal = values.Bool("con_mod_heal") }, SkillJsonImportSerializerContext.Default.HealEffectPayloadJsonDto) : null;
            case CombatEffectPayloadShape.EquipmentDurabilityDamage:
                values.Allow("max_damaged_items", "target_slots");
                return values.IsValid ? JsonSerializer.SerializeToElement(new EquipmentDurabilityDamageEffectPayloadJsonDto { MaxDamagedItems = values.Int("max_damaged_items", 1), TargetSlots = values.StringArray("target_slots") }, SkillJsonImportSerializerContext.Default.EquipmentDurabilityDamageEffectPayloadJsonDto) : null;
            case CombatEffectPayloadShape.RepeatAttackUntilFail:
                values.Allow("base_attack_bonus", "cost_resource", "follow_up_cost_addition", "follow_up_cost_multiplier", "follow_up_attack_penalty", "penalty_free_stages_by_level", "same_target_only", "follow_up_fixed_cost", "exponential_penalty", "stop_on_insufficient_resource");
                return values.IsValid ? JsonSerializer.SerializeToElement(new RepeatAttackUntilFailEffectPayloadJsonDto
                {
                    BaseAttackBonus = values.Int("base_attack_bonus"), CostResource = values.String("cost_resource", "aura"), FollowUpCostAddition = values.Int("follow_up_cost_addition"),
                    FollowUpCostMultiplier = values.Double("follow_up_cost_multiplier", 1.0), FollowUpAttackPenalty = values.Int("follow_up_attack_penalty"),
                    PenaltyFreeStagesByLevel = values.IntMap("penalty_free_stages_by_level"), SameTargetOnly = values.Bool("same_target_only"),
                    FollowUpFixedCost = values.Int("follow_up_fixed_cost"), ExponentialPenalty = values.Bool("exponential_penalty"), StopOnInsufficientResource = values.Bool("stop_on_insufficient_resource"),
                }, SkillJsonImportSerializerContext.Default.RepeatAttackUntilFailEffectPayloadJsonDto) : null;
            case CombatEffectPayloadShape.LayeredBarrier:
                values.Allow("area_pattern", "profile_id", "radius_cells", "save_dc");
                return values.IsValid ? JsonSerializer.SerializeToElement(new LayeredBarrierEffectPayloadJsonDto
                {
                    AreaPattern = values.RequiredString("area_pattern"), ProfileId = values.RequiredString("profile_id"), RadiusCells = values.RequiredInt("radius_cells"), SaveDc = values.RequiredInt("save_dc"),
                }, SkillJsonImportSerializerContext.Default.LayeredBarrierEffectPayloadJsonDto) : null;
            case CombatEffectPayloadShape.GradedSaveExecute:
                values.Allow("critical_failure_damage_dice_count", "critical_failure_damage_dice_sides", "critical_failure_execute_threshold_max_hp_percent", "critical_failure_frightened_duration_tu", "critical_failure_stunned_duration_tu", "failure_damage_dice_count", "failure_damage_dice_sides", "failure_execute_threshold_fixed", "failure_execute_threshold_max_hp_percent", "failure_frightened_duration_tu", "failure_reaction_lock_duration_tu", "profile_id", "success_aftershock_duration_tu");
                return values.IsValid ? JsonSerializer.SerializeToElement(new GradedSaveExecuteEffectPayloadJsonDto
                {
                    CriticalFailureDamageDiceCount = values.RequiredInt("critical_failure_damage_dice_count"), CriticalFailureDamageDiceSides = values.RequiredInt("critical_failure_damage_dice_sides"),
                    CriticalFailureExecuteThresholdMaxHpPercent = values.RequiredInt("critical_failure_execute_threshold_max_hp_percent"), CriticalFailureFrightenedDurationTu = values.RequiredInt("critical_failure_frightened_duration_tu"),
                    CriticalFailureStunnedDurationTu = values.RequiredInt("critical_failure_stunned_duration_tu"), FailureDamageDiceCount = values.RequiredInt("failure_damage_dice_count"),
                    FailureDamageDiceSides = values.RequiredInt("failure_damage_dice_sides"), FailureExecuteThresholdFixed = values.RequiredInt("failure_execute_threshold_fixed"),
                    FailureExecuteThresholdMaxHpPercent = values.RequiredInt("failure_execute_threshold_max_hp_percent"), FailureFrightenedDurationTu = values.RequiredInt("failure_frightened_duration_tu"),
                    FailureReactionLockDurationTu = values.RequiredInt("failure_reaction_lock_duration_tu"), ProfileId = values.RequiredString("profile_id"),
                    SuccessAftershockDurationTu = values.RequiredInt("success_aftershock_duration_tu"),
                }, SkillJsonImportSerializerContext.Default.GradedSaveExecuteEffectPayloadJsonDto) : null;
            case CombatEffectPayloadShape.DispelMagic:
                values.Allow("breaks_barrier_layer");
                return values.IsValid ? JsonSerializer.SerializeToElement(new DispelMagicEffectPayloadJsonDto { BreaksBarrierLayer = values.String("breaks_barrier_layer") }, SkillJsonImportSerializerContext.Default.DispelMagicEffectPayloadJsonDto) : null;
            case CombatEffectPayloadShape.OnKillGainResources:
                values.Allow("grant_scope", "require_target_defeated_by_same_skill", "stack_on_multiple_kills");
                return values.IsValid ? JsonSerializer.SerializeToElement(new OnKillGainResourcesEffectPayloadJsonDto { GrantScope = values.String("grant_scope"), RequireTargetDefeatedBySameSkill = values.Bool("require_target_defeated_by_same_skill"), StackOnMultipleKills = values.Bool("stack_on_multiple_kills") }, SkillJsonImportSerializerContext.Default.OnKillGainResourcesEffectPayloadJsonDto) : null;
            default:
                throw new InvalidOperationException("Unregistered combat effect payload shape.");
        }
    }

    private sealed class StrictParams
    {
        private readonly JsonContentEntryContext _context;
        private readonly string _pointer;
        private readonly List<ContentJsonDiagnostic> _diagnostics;
        private readonly Dictionary<string, Variant> _values = new(StringComparer.Ordinal);
        private int _start;

        internal StrictParams(JsonContentEntryContext context, GdDictionary? values, string pointer, List<ContentJsonDiagnostic> diagnostics)
        {
            _context = context; _pointer = pointer; _diagnostics = diagnostics; _start = diagnostics.Count;
            if (values == null) { Invalid(pointer, "Effect params dictionary must not be null."); return; }
            foreach (Variant rawKey in values.Keys)
            {
                if (!TryStrictText(rawKey, out string key)) { Invalid(pointer, "Effect param names must be strings."); continue; }
                if (!_values.TryAdd(key, values[rawKey])) Invalid(pointer + "/" + Escape(key), "Duplicate effect param name.");
            }
        }

        internal bool IsValid => _diagnostics.Count == _start;
        internal void Allow(params string[] names)
        {
            var allowed = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (string key in _values.Keys) if (!allowed.Contains(key)) Invalid(_pointer + "/" + Escape(key), "Unknown effect payload member.");
        }
        internal int Int(string key, int fallback = 0)
        {
            if (!_values.TryGetValue(key, out Variant value)) return fallback;
            if (TryInt32(value, out int result)) return result;
            Invalid(_pointer + "/" + key, "Effect payload member must be an Int32 integer."); return fallback;
        }
        internal int RequiredInt(string key) { if (!_values.ContainsKey(key)) { Invalid(_pointer + "/" + key, "Required effect payload member is missing."); return 0; } return Int(key); }
        internal bool Bool(string key, bool fallback = false) => Try(key, Variant.Type.Bool, out Variant value) ? value.AsBool() : fallback;
        internal double Double(string key, double fallback = 0)
        {
            if (!_values.TryGetValue(key, out Variant value)) return fallback;
            if (value.VariantType is not (Variant.Type.Float or Variant.Type.Int)) { Invalid(_pointer + "/" + key, "Effect payload member must be a finite number."); return fallback; }
            double result = value.VariantType == Variant.Type.Int ? value.AsInt64() : value.AsDouble();
            if (!double.IsFinite(result)) { Invalid(_pointer + "/" + key, "Effect payload member must be a finite number."); return fallback; }
            return result;
        }
        internal string String(string key, string fallback = "")
        {
            if (!_values.TryGetValue(key, out Variant value)) return fallback;
            if (!TryStrictText(value, out string result)) { Invalid(_pointer + "/" + key, "Effect payload member must be a string token."); return fallback; }
            return result;
        }
        internal string RequiredString(string key) { if (!_values.ContainsKey(key)) { Invalid(_pointer + "/" + key, "Required effect payload member is missing."); return ""; } return String(key); }
        internal IReadOnlyList<string> StringArray(string key)
        {
            var result = new List<string>();
            if (!_values.TryGetValue(key, out Variant value)) return result;
            if (value.VariantType != Variant.Type.Array) { Invalid(_pointer + "/" + key, "Effect payload member must be a string array."); return result; }
            Godot.Collections.Array array = value.AsGodotArray();
            for (int index = 0; index < array.Count; index++)
            {
                if (!TryStrictText(array[index], out string item)) Invalid($"{_pointer}/{key}/{index}", "Effect payload array item must be a string token.");
                else result.Add(item);
            }
            return result;
        }
        internal IReadOnlyDictionary<string, int> IntMap(string key)
        {
            var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
            if (!_values.TryGetValue(key, out Variant value)) return result;
            if (value.VariantType != Variant.Type.Dictionary) { Invalid(_pointer + "/" + key, "Effect payload member must be an integer map."); return result; }
            GdDictionary map = value.AsGodotDictionary();
            foreach (Variant rawKey in map.Keys)
            {
                string mapKey;
                if (TryInt32(rawKey, out int numericKey)) mapKey = numericKey.ToString(System.Globalization.CultureInfo.InvariantCulture);
                else if (!TryStrictText(rawKey, out mapKey))
                {
                    Invalid(_pointer + "/" + key, "Effect payload map keys must be Int32 integers or strings.");
                    continue;
                }
                if (!TryInt32(map[rawKey], out int intValue))
                    Invalid(_pointer + "/" + key, "Effect payload map must use level keys and integer values.");
                else if (!result.TryAdd(mapKey, intValue)) Invalid(_pointer + "/" + key + "/" + Escape(mapKey), "Duplicate canonical level key.");
            }
            return result;
        }
        private bool Try(string key, Variant.Type type, out Variant value)
        {
            if (!_values.TryGetValue(key, out value)) return false;
            if (value.VariantType == type) return true;
            Invalid(_pointer + "/" + key, "Effect payload member has the wrong type."); return false;
        }
        private void Invalid(string pointer, string message) => AddInvalid(_context, pointer, message, _diagnostics);
    }
}
