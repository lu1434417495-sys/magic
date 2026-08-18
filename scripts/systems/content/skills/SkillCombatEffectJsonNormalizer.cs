#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

internal static partial class SkillJsonImportParser
{
    private delegate bool TryClosedValue<T>(string? value, out T result) where T : struct;

    private static CombatEffectImportModel? NormalizeFullCombatEffect(
        JsonContentEntryContext context,
        CombatEffectJsonDto? dto,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics,
        bool validateNumericRanges
    )
    {
        if (dto == null)
        {
            diagnostics.Add(Required(context, pointer));
            return null;
        }

        int before = diagnostics.Count;
        if (!SkillJsonImportValueRules.TryParseEffectKind(dto.EffectType, out CombatEffectImportKind kind))
        {
            diagnostics.Add(Diagnostic(
                SkillJsonImportRules.UnknownEffectKind,
                "Combat effect type is not registered by the closed import contract.",
                context,
                $"{pointer}/effect_type"
            ));
            return null;
        }

        ICombatEffectPayloadImportModel? payload = NormalizeFullEffectPayload(
            context,
            dto,
            kind,
            pointer,
            diagnostics
        );
        if (payload == null)
            return null;

        int minSkillLevel = dto.MinSkillLevel ?? 0;
        int maxSkillLevel = dto.MaxSkillLevel ?? -1;
        int power = dto.Power ?? 0;
        int durationTu = dto.DurationTu ?? 0;
        if (validateNumericRanges)
        {
            ValidateNonNegative(minSkillLevel, context, $"{pointer}/min_skill_level", diagnostics);
            if (maxSkillLevel < -1)
                AddRangeDiagnostic(context, $"{pointer}/max_skill_level", diagnostics);
            ValidateNonNegative(power, context, $"{pointer}/power", diagnostics);
            ValidateNonNegative(durationTu, context, $"{pointer}/duration_tu", diagnostics);
        }
        ValidateFinite(dto.PreResistanceDamageMultiplier ?? 1.0, context, $"{pointer}/pre_resistance_damage_multiplier", diagnostics);
        ValidateFinite(dto.JumpStrScale, context, $"{pointer}/jump_str_scale", diagnostics);
        ValidateFinite(dto.JumpArcRatio, context, $"{pointer}/jump_arc_ratio", diagnostics);

        CombatTickEffectImportKind tickEffect = Closed<CombatTickEffectImportKind>(
            dto.TickEffectType ?? "",
            SkillCombatEffectValueRules.TryTickEffect,
            context,
            $"{pointer}/tick_effect_type",
            diagnostics
        );
        CombatEffectLifetimeImportKind lifetime = Closed<CombatEffectLifetimeImportKind>(
            dto.LifetimePolicy ?? "timed",
            SkillCombatEffectValueRules.TryLifetime,
            context,
            $"{pointer}/lifetime_policy",
            diagnostics
        );
        CombatPathStepAreaPatternImportKind pathStepArea = Closed<CombatPathStepAreaPatternImportKind>(
            dto.PathStepAreaPattern ?? "diamond",
            SkillCombatEffectValueRules.TryPathStepAreaPattern,
            context,
            $"{pointer}/path_step_area_pattern",
            diagnostics
        );
        CombatStackBehaviorImportKind stackBehavior = Closed<CombatStackBehaviorImportKind>(
            dto.StackBehavior ?? "refresh",
            SkillCombatEffectValueRules.TryStackBehavior,
            context,
            $"{pointer}/stack_behavior",
            diagnostics
        );
        CombatSaveDcModeImportKind saveDcMode = Closed<CombatSaveDcModeImportKind>(
            dto.SaveDcMode ?? "static",
            SkillCombatEffectValueRules.TrySaveDcMode,
            context,
            $"{pointer}/save_dc_mode",
            diagnostics
        );

        CombatEffectImportModel model = new(kind, payload)
        {
            MinSkillLevel = minSkillLevel,
            MaxSkillLevel = maxSkillLevel,
            Power = power,
            DurationTu = durationTu,
            TickEffectType = tickEffect,
            LifetimePolicy = lifetime,
            HealToHpPercentFloor = dto.HealToHpPercentFloor,
            HealMissingHpPercent = dto.HealMissingHpPercent,
            MoveCostDelta = dto.MoveCostDelta,
            RenderOverlayId = Name(dto.RenderOverlayId, context, $"{pointer}/render_overlay_id", diagnostics),
            OverlayPriority = dto.OverlayPriority,
            DisplayName = dto.DisplayName ?? "",
            DoesNotStackWithStatusId = Name(dto.DoesNotStackWithStatusId, context, $"{pointer}/does_not_stack_with_status_id", diagnostics),
            DoesNotStackWithStatusIds = Names(dto.DoesNotStackWithStatusIds, context, $"{pointer}/does_not_stack_with_status_ids", diagnostics),
            DamageRatioPercent = dto.DamageRatioPercent ?? 100,
            PreResistanceDamageMultiplier = dto.PreResistanceDamageMultiplier ?? 1.0,
            WeaponDiceMultiplier = dto.WeaponDiceMultiplier ?? 1,
            BonusWeaponDiceMultiplier = dto.BonusWeaponDiceMultiplier,
            DamageTag = OptionalClosed<DamageTagImportKind>(dto.DamageTag, SkillRootCombatImportValueRules.TryDamageTag, context, $"{pointer}/damage_tag", diagnostics),
            DamageTags = ClosedValues<DamageTagImportKind>(dto.DamageTags, SkillRootCombatImportValueRules.TryDamageTag, context, $"{pointer}/damage_tags", diagnostics),
            MitigationBypassDamageTags = ClosedValues<DamageTagImportKind>(dto.MitigationBypassDamageTags, SkillRootCombatImportValueRules.TryDamageTag, context, $"{pointer}/mitigation_bypass_damage_tags", diagnostics),
            MitigationBypassTiers = ClosedValues<DamageMitigationTierImportKind>(dto.MitigationBypassTiers, SkillCombatEffectValueRules.TryMitigationTier, context, $"{pointer}/mitigation_bypass_tiers", diagnostics),
            DamageCategory = OptionalClosed<DamageCategoryImportKind>(dto.DamageCategory, SkillCombatEffectValueRules.TryDamageCategory, context, $"{pointer}/damage_category", diagnostics),
            DrBypassTag = Name(dto.DrBypassTag, context, $"{pointer}/dr_bypass_tag", diagnostics),
            HpRatioThresholdPercent = dto.HpRatioThresholdPercent,
            DiceCount = dto.DiceCount,
            DiceSides = dto.DiceSides,
            DiceBonus = dto.DiceBonus,
            DiceSidesBase = dto.DiceSidesBase,
            DiceSidesPerConstitutionMod = dto.DiceSidesPerConstitutionMod,
            DiceSidesPerWillpowerMod = dto.DiceSidesPerWillpowerMod,
            ShieldFamily = Name(dto.ShieldFamily, context, $"{pointer}/shield_family", diagnostics),
            ShieldAttributeModifierId = OptionalClosed<ShieldAttributeModifierImportKind>(dto.ShieldAttributeModifierId, SkillCombatEffectValueRules.TryShieldAttribute, context, $"{pointer}/shield_attribute_modifier_id", diagnostics),
            ShieldRollPerTarget = dto.ShieldRollPerTarget,
            BonusDamageDiceCount = dto.BonusDamageDiceCount,
            BonusDamageDiceSides = dto.BonusDamageDiceSides,
            BonusDamageDiceBonus = dto.BonusDamageDiceBonus,
            BonusDamageSeparateEvent = dto.BonusDamageSeparateEvent,
            SourceBoundWeaponBonusDamageDiceCount = dto.SourceBoundWeaponBonusDamageDiceCount,
            SourceBoundWeaponBonusDamageDiceSides = dto.SourceBoundWeaponBonusDamageDiceSides,
            SourceBoundWeaponBonusDamageDiceBonus = dto.SourceBoundWeaponBonusDamageDiceBonus,
            AddWeaponDice = dto.AddWeaponDice,
            RequiresWeapon = dto.RequiresWeapon,
            UseWeaponPhysicalDamageTag = dto.UseWeaponPhysicalDamageTag,
            ResolveAsWeaponAttack = dto.ResolveAsWeaponAttack,
            AllowRepeatHitsAcrossSteps = dto.AllowRepeatHitsAcrossSteps,
            PathStepAreaPattern = pathStepArea,
            PathStepRadius = dto.PathStepRadius ?? 1,
            PathStepLogLabel = dto.PathStepLogLabel ?? "",
            RepeatHitStatusId = Name(dto.RepeatHitStatusId, context, $"{pointer}/repeat_hit_status_id", diagnostics),
            RepeatHitStatusThreshold = dto.RepeatHitStatusThreshold,
            RepeatHitStatusMinSkillLevel = dto.RepeatHitStatusMinSkillLevel,
            RepeatHitStatusPower = dto.RepeatHitStatusPower ?? 1,
            RepeatHitStatusDurationTu = dto.RepeatHitStatusDurationTu,
            RepeatHitStatusLogTemplate = dto.RepeatHitStatusLogTemplate ?? "",
            PreventRepeatTarget = dto.PreventRepeatTarget ?? true,
            ChainBaseHopRange = dto.ChainBaseHopRange,
            ChainConductiveHopRange = dto.ChainConductiveHopRange,
            ChainMaxTotalTargets = dto.ChainMaxTotalTargets,
            ChainConductiveStatusIds = Names(dto.ChainConductiveStatusIds, context, $"{pointer}/chain_conductive_status_ids", diagnostics),
            ChainConductiveTerrainEffectIds = Names(dto.ChainConductiveTerrainEffectIds, context, $"{pointer}/chain_conductive_terrain_effect_ids", diagnostics),
            ChainBacklashHopRangeBonus = dto.ChainBacklashHopRangeBonus,
            StopOnMiss = dto.StopOnMiss ?? true,
            StopOnTargetDown = dto.StopOnTargetDown ?? true,
            FixedAttackCount = dto.FixedAttackCount,
            FollowUpDamageMultiplierPercent = dto.FollowUpDamageMultiplierPercent ?? 100,
            FollowUpAttackRollBonusCurve = dto.FollowUpAttackRollBonusCurve,
            RemoveHarmful = dto.RemoveHarmful,
            RemoveHarmfulFromAllies = dto.RemoveHarmfulFromAllies ?? true,
            RemoveBeneficial = dto.RemoveBeneficial,
            RemoveBeneficialFromEnemies = dto.RemoveBeneficialFromEnemies ?? true,
            RequireDamageApplied = dto.RequireDamageApplied,
            MaxStatusRemoved = dto.MaxStatusRemoved,
            MinHpAfterDamage = dto.MinHpAfterDamage ?? 1,
            DeathPreventionPriority = dto.DeathPreventionPriority,
            ThresholdBaseValue = dto.ThresholdBaseValue,
            ThresholdLevelAnchor = dto.ThresholdLevelAnchor ?? 17,
            ThresholdLevelBonusPerDelta = dto.ThresholdLevelBonusPerDelta ?? 5,
            ThresholdMaxHpRatioPercent = dto.ThresholdMaxHpRatioPercent ?? 20,
            ThresholdCapMaxHpRatioPercent = dto.ThresholdCapMaxHpRatioPercent ?? 50,
            SoulFractureDurationTu = dto.SoulFractureDurationTu,
            HealMultiplierPercent = dto.HealMultiplierPercent ?? 100,
            ShieldGainMultiplierPercent = dto.ShieldGainMultiplierPercent ?? 100,
            AttackRollPenalty = dto.AttackRollPenalty ?? -1,
            AttackRollBonus = dto.AttackRollBonus,
            AttackRollAdvantage = dto.AttackRollAdvantage,
            ConsumeOnNextAttackCheck = dto.ConsumeOnNextAttackCheck,
            ConsumeOnNextSave = dto.ConsumeOnNextSave,
            Undispellable = dto.Undispellable,
            DispellableMagic = dto.DispellableMagic,
            DispellableHarmfulMagic = dto.DispellableHarmfulMagic,
            DispellableBeneficialMagic = dto.DispellableBeneficialMagic,
            MitigationTier = OptionalClosed<DamageMitigationTierImportKind>(dto.MitigationTier, SkillCombatEffectValueRules.TryMitigationTier, context, $"{pointer}/mitigation_tier", diagnostics),
            SecondaryHitDcBase = dto.SecondaryHitDcBase ?? 10,
            DebuffCountThreshold = dto.DebuffCountThreshold ?? 3,
            BaseHeal = dto.BaseHeal ?? 8,
            HealPerLevel = dto.HealPerLevel ?? 4,
            ConModBase = dto.ConModBase ?? 2,
            ConModPer2Levels = dto.ConModPer2Levels ?? 1,
            EffectCategories = Names(dto.EffectCategories, context, $"{pointer}/effect_categories", diagnostics),
            EffectTargetTeamFilter = OptionalClosed<CombatEffectTargetTeamFilterImportKind>(dto.EffectTargetTeamFilter, SkillCombatEffectValueRules.TryEffectTargetTeamFilter, context, $"{pointer}/effect_target_team_filter", diagnostics),
            MaxAffectedTargets = dto.MaxAffectedTargets,
            ExcludeSource = dto.ExcludeSource,
            TargetOrder = OptionalClosed<CombatEffectTargetOrderImportKind>(dto.TargetOrder, SkillCombatEffectValueRules.TryTargetOrder, context, $"{pointer}/target_order", diagnostics),
            RequiredTargetCreatureTypeTag = Name(dto.RequiredTargetCreatureTypeTag, context, $"{pointer}/required_target_creature_type_tag", diagnostics),
            RequiredTargetMinCognition = OptionalClosed<CombatCognitionImportKind>(dto.RequiredTargetMinCognition, SkillCombatEffectValueRules.TryCognition, context, $"{pointer}/required_target_min_cognition", diagnostics),
            StatusId = Name(dto.StatusId, context, $"{pointer}/status_id", diagnostics),
            AppliedStatusDurationTu = dto.AppliedStatusDurationTu,
            TerrainEffectId = Name(dto.TerrainEffectId, context, $"{pointer}/terrain_effect_id", diagnostics),
            TerrainContactMode = OptionalClosed<CombatTerrainContactImportKind>(dto.TerrainContactMode, SkillCombatEffectValueRules.TryTerrainContact, context, $"{pointer}/terrain_contact_mode", diagnostics),
            TerrainEffectiveTriggerCount = dto.TerrainEffectiveTriggerCount,
            TerrainRequiresGroundContact = dto.TerrainRequiresGroundContact,
            TerrainRecheckFromInside = dto.TerrainRecheckFromInside,
            TerrainMaxActiveInstancesPerSource = dto.TerrainMaxActiveInstancesPerSource,
            TerrainReplaceExistingFromSource = dto.TerrainReplaceExistingFromSource,
            TerrainReplaceTo = Name(dto.TerrainReplaceTo, context, $"{pointer}/terrain_replace_to", diagnostics),
            HeightDelta = dto.HeightDelta,
            BodySizeCategory = OptionalClosed<CombatBodySizeImportKind>(dto.BodySizeCategory, SkillCombatEffectValueRules.TryBodySize, context, $"{pointer}/body_size_category", diagnostics),
            ForcedMoveMode = OptionalClosed<CombatForcedMoveImportKind>(dto.ForcedMoveMode, SkillCombatEffectValueRules.TryForcedMove, context, $"{pointer}/forced_move_mode", diagnostics),
            ForcedMoveDistance = dto.ForcedMoveDistance,
            ForcedMoveMaxTargetBodySize = dto.ForcedMoveMaxTargetBodySize,
            GrappleMaxHeightGain = dto.GrappleMaxHeightGain,
            SourceRetreatDistance = dto.SourceRetreatDistance,
            ChargeTrapImmunityMinSkillLevel = dto.ChargeTrapImmunityMinSkillLevel ?? -1,
            JumpBaseBudget = dto.JumpBaseBudget,
            JumpStrScale = dto.JumpStrScale,
            JumpArcRatio = dto.JumpArcRatio,
            JumpRangeMultiplier = dto.JumpRangeMultiplier ?? 1,
            TickIntervalTu = dto.TickIntervalTu,
            StackBehavior = stackBehavior,
            StackLimit = dto.StackLimit,
            BonusCondition = OptionalClosed<CombatDamageBonusConditionImportKind>(dto.BonusCondition, SkillCombatEffectValueRules.TryBonusCondition, context, $"{pointer}/bonus_condition", diagnostics),
            BonusConditionCreatureTypeTag = Name(dto.BonusConditionCreatureTypeTag, context, $"{pointer}/bonus_condition_creature_type_tag", diagnostics),
            TriggerEvent = OptionalClosed<CombatEffectTriggerEventImportKind>(dto.TriggerEvent, SkillCombatEffectValueRules.TryTriggerEvent, context, $"{pointer}/trigger_event", diagnostics),
            TriggerCondition = OptionalClosed<CombatEffectTriggerConditionImportKind>(dto.TriggerCondition, SkillCombatEffectValueRules.TryTriggerCondition, context, $"{pointer}/trigger_condition", diagnostics),
            TriggerStatusId = Name(dto.TriggerStatusId, context, $"{pointer}/trigger_status_id", diagnostics),
            SaveDc = dto.SaveDc,
            SaveDcBonus = dto.SaveDcBonus,
            SaveDcMode = saveDcMode,
            SaveDcSourceAbility = OptionalClosed<CombatSaveAbilityImportKind>(dto.SaveDcSourceAbility, SkillRootCombatImportValueRules.TrySaveAbility, context, $"{pointer}/save_dc_source_ability", diagnostics),
            SaveAbility = OptionalClosed<CombatSaveAbilityImportKind>(dto.SaveAbility, SkillRootCombatImportValueRules.TrySaveAbility, context, $"{pointer}/save_ability", diagnostics),
            SaveFailureStatusId = Name(dto.SaveFailureStatusId, context, $"{pointer}/save_failure_status_id", diagnostics),
            SaveFailureStatusOutcomes = NormalizeWeightedOutcomes(context, dto.SaveFailureStatusOutcomes, $"{pointer}/save_failure_status_outcomes", diagnostics, validateNumericRanges),
            SavePartialOnSuccess = dto.SavePartialOnSuccess,
            SaveTag = OptionalClosed<CombatSaveTagImportKind>(dto.SaveTag, SkillCombatEffectValueRules.TrySaveTag, context, $"{pointer}/save_tag", diagnostics),
            ConsumedStatusId = Name(dto.ConsumedStatusId, context, $"{pointer}/consumed_status_id", diagnostics),
            RequiredTargetStatusId = Name(dto.RequiredTargetStatusId, context, $"{pointer}/required_target_status_id", diagnostics),
            RequiredTargetStatusMinStacks = dto.RequiredTargetStatusMinStacks,
            RequiredTargetStatusSourceSelector = OptionalClosed<CombatStatusSourceSelectorImportKind>(dto.RequiredTargetStatusSourceSelector, SkillCombatEffectValueRules.TryStatusSourceSelector, context, $"{pointer}/required_target_status_source_selector", diagnostics),
            DicePerConsumedStack = dto.DicePerConsumedStack,
            DiceSidesPerStack = dto.DiceSidesPerStack,
            ApGain = dto.ApGain,
            FreeMovePointsGain = dto.FreeMovePointsGain,
            CountsAsDebuffOverride = dto.CountsAsDebuffOverride,
            CountsAsDebuff = dto.CountsAsDebuff,
            LockCounterattack = dto.LockCounterattack,
            LockGuard = dto.LockGuard,
            LockDodgeBonus = dto.LockDodgeBonus,
            LockCrit = dto.LockCrit,
            SkipTurn = dto.SkipTurn,
            BreakOnPositiveDamage = dto.BreakOnPositiveDamage,
            OnRemovedStatusId = Name(dto.OnRemovedStatusId, context, $"{pointer}/on_removed_status_id", diagnostics),
            OnRemovedStatusSaveImmunityTags = ClosedValues<CombatSaveTagImportKind>(dto.OnRemovedStatusSaveImmunityTags, SkillCombatEffectValueRules.TrySaveTag, context, $"{pointer}/on_removed_status_save_immunity_tags", diagnostics),
            OnRemovedStatusUndispellable = dto.OnRemovedStatusUndispellable,
            OnRemovedStatusConsumeAfterNormalTurn = dto.OnRemovedStatusConsumeAfterNormalTurn,
            SaveBonus = dto.SaveBonus,
            ControlSaveBonus = dto.ControlSaveBonus,
            PassiveReduction = dto.PassiveReduction,
            ContentDr = dto.ContentDr,
            GuardBlock = dto.GuardBlock,
            RangeBonus = dto.RangeBonus,
            MainSkillLockOtherDebuffCount = dto.MainSkillLockOtherDebuffCount,
            MeleeComboStackGainBonus = dto.MeleeComboStackGainBonus,
            ComboAttackBonusStatusId = Name(dto.ComboAttackBonusStatusId, context, $"{pointer}/combo_attack_bonus_status_id", diagnostics),
            ComboAttackBonusStackDivisor = dto.ComboAttackBonusStackDivisor,
            UpkeepResource = OptionalClosed<CombatResourceImportKind>(dto.UpkeepResource, SkillCombatEffectValueRules.TryResource, context, $"{pointer}/upkeep_resource", diagnostics),
            UpkeepIntervalTu = dto.UpkeepIntervalTu,
            UpkeepBaseCost = dto.UpkeepBaseCost,
            UpkeepEscalationIntervalTu = dto.UpkeepEscalationIntervalTu,
            UpkeepCostMultiplier = dto.UpkeepCostMultiplier ?? 1,
            BreakOnHardControl = dto.BreakOnHardControl,
            TerminationStatusId = Name(dto.TerminationStatusId, context, $"{pointer}/termination_status_id", diagnostics),
            TerminationStatusDurationTu = dto.TerminationStatusDurationTu,
            TerminationAttackRollPenalty = dto.TerminationAttackRollPenalty,
            TerminationCooldownTu = dto.TerminationCooldownTu,
            SaveAdvantageTags = ClosedValues<CombatSaveTagImportKind>(dto.SaveAdvantageTags, SkillCombatEffectValueRules.TrySaveTag, context, $"{pointer}/save_advantage_tags", diagnostics),
            SaveDisadvantageTags = ClosedValues<CombatSaveTagImportKind>(dto.SaveDisadvantageTags, SkillCombatEffectValueRules.TrySaveTag, context, $"{pointer}/save_disadvantage_tags", diagnostics),
            SaveImmunityTags = ClosedValues<CombatSaveTagImportKind>(dto.SaveImmunityTags, SkillCombatEffectValueRules.TrySaveTag, context, $"{pointer}/save_immunity_tags", diagnostics),
            EffectTags = Names(dto.EffectTags, context, $"{pointer}/effect_tags", diagnostics),
            EquipmentDurabilitySlotWeights = NormalizeSlotWeights(context, dto.EquipmentDurabilitySlotWeights, $"{pointer}/equipment_durability_slot_weights", diagnostics),
            ExtraDamageSegments = NormalizeDamageSegments(context, dto.ExtraDamageSegments, $"{pointer}/extra_damage_segments", diagnostics),
            TargetDamageMultiplierRules = NormalizeTargetDamageMultiplierRules(context, dto.TargetDamageMultiplierRules, $"{pointer}/target_damage_multiplier_rules", diagnostics),
        };

        return diagnostics.Count == before && payload != null ? model : null;
    }

    private static ICombatEffectPayloadImportModel? NormalizeFullEffectPayload(
        JsonContentEntryContext context,
        CombatEffectJsonDto dto,
        CombatEffectImportKind kind,
        string effectPointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        string pointer = $"{effectPointer}/payload";
        if (dto.Payload is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Diagnostic(SkillJsonImportRules.InvalidEffectPayload, "Combat effect payload must be a JSON object.", context, pointer));
            return null;
        }

        return SkillFullCombatEffectClosedSpec.GetPayloadShape(kind) switch
        {
            CombatEffectPayloadShape.Empty => ParseEmptyPayload(context, element, pointer, diagnostics),
            CombatEffectPayloadShape.Status => NormalizeStatusPayload(context, element, pointer, diagnostics),
            CombatEffectPayloadShape.Heal => NormalizeHealPayload(context, element, pointer, diagnostics),
            CombatEffectPayloadShape.EquipmentDurabilityDamage => NormalizeEquipmentPayload(context, element, pointer, diagnostics),
            CombatEffectPayloadShape.RepeatAttackUntilFail => NormalizeRepeatPayload(context, element, pointer, diagnostics),
            CombatEffectPayloadShape.LayeredBarrier => NormalizeLayeredBarrierPayload(context, element, pointer, diagnostics),
            CombatEffectPayloadShape.GradedSaveExecute => NormalizeGradedPayload(context, element, pointer, diagnostics),
            CombatEffectPayloadShape.DispelMagic => NormalizeDispelPayload(context, element, pointer, diagnostics),
            CombatEffectPayloadShape.OnKillGainResources => NormalizeOnKillPayload(context, element, pointer, diagnostics),
            _ => throw new InvalidOperationException("Unregistered combat effect payload shape."),
        };
    }

    private static EmptyCombatEffectPayloadImportModel? ParseEmptyPayload(JsonContentEntryContext context, JsonElement element, string pointer, List<ContentJsonDiagnostic> diagnostics) =>
        ParsePayload(context, element, pointer, SkillJsonImportSerializerContext.Default.EmptyCombatEffectPayloadJsonDto, diagnostics) != null
            ? EmptyCombatEffectPayloadImportModel.Instance
            : null;

    private static StatusEffectPayloadImportModel? NormalizeStatusPayload(JsonContentEntryContext context, JsonElement element, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        StatusEffectPayloadJsonDto? dto = ParsePayload(context, element, pointer, SkillJsonImportSerializerContext.Default.StatusEffectPayloadJsonDto, diagnostics);
        return dto == null ? null : new(Name(dto.BreaksBarrierLayer, context, $"{pointer}/breaks_barrier_layer", diagnostics), Name(dto.SourceSkillId, context, $"{pointer}/source_skill_id", diagnostics));
    }

    private static HealEffectPayloadImportModel? NormalizeHealPayload(JsonContentEntryContext context, JsonElement element, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        HealEffectPayloadJsonDto? dto = ParsePayload(context, element, pointer, SkillJsonImportSerializerContext.Default.HealEffectPayloadJsonDto, diagnostics);
        return dto == null ? null : new(dto.ConModHeal);
    }

    private static EquipmentDurabilityDamageEffectPayloadImportModel? NormalizeEquipmentPayload(JsonContentEntryContext context, JsonElement element, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        EquipmentDurabilityDamageEffectPayloadJsonDto? dto = ParsePayload(context, element, pointer, SkillJsonImportSerializerContext.Default.EquipmentDurabilityDamageEffectPayloadJsonDto, diagnostics);
        int maxDamagedItems = element.TryGetProperty("max_damaged_items", out _)
            ? dto?.MaxDamagedItems ?? 1
            : 1;
        return dto == null ? null : new(maxDamagedItems, ClosedValues<CombatEquipmentSlotImportKind>(dto.TargetSlots, SkillCombatEffectValueRules.TryEquipmentSlot, context, $"{pointer}/target_slots", diagnostics));
    }

    private static RepeatAttackUntilFailEffectPayloadImportModel? NormalizeRepeatPayload(JsonContentEntryContext context, JsonElement element, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        RepeatAttackUntilFailEffectPayloadJsonDto? dto = ParsePayload(context, element, pointer, SkillJsonImportSerializerContext.Default.RepeatAttackUntilFailEffectPayloadJsonDto, diagnostics);
        if (dto == null)
            return null;
        CombatResourceImportKind resource = Closed<CombatResourceImportKind>(dto.CostResource ?? "aura", SkillCombatEffectValueRules.TryResource, context, $"{pointer}/cost_resource", diagnostics);
        double followUpCostMultiplier = element.TryGetProperty("follow_up_cost_multiplier", out _)
            ? dto.FollowUpCostMultiplier
            : 1.0;
        ValidateFinite(followUpCostMultiplier, context, $"{pointer}/follow_up_cost_multiplier", diagnostics);
        var map = new List<KeyValuePair<int, int>>();
        foreach (KeyValuePair<string, int> pair in dto.PenaltyFreeStagesByLevel ?? EmptyStringIntMap)
        {
            string keyPointer = $"{pointer}/penalty_free_stages_by_level/{EscapePointerToken(pair.Key)}";
            if (!TryParseCanonicalNonNegativeInt(pair.Key, out int level))
                diagnostics.Add(Diagnostic(SkillJsonImportRules.InvalidId, "Penalty-free stage level key must be a canonical nonnegative integer.", context, keyPointer));
            else
                map.Add(new(level, pair.Value));
        }
        return new(dto.BaseAttackBonus, resource, dto.FollowUpCostAddition, followUpCostMultiplier, dto.FollowUpAttackPenalty, map, dto.SameTargetOnly, dto.FollowUpFixedCost, dto.ExponentialPenalty, dto.StopOnInsufficientResource);
    }

    private static GradedSaveExecuteEffectPayloadImportModel? NormalizeGradedPayload(JsonContentEntryContext context, JsonElement element, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        GradedSaveExecuteEffectPayloadJsonDto? dto = ParsePayload(context, element, pointer, SkillJsonImportSerializerContext.Default.GradedSaveExecuteEffectPayloadJsonDto, diagnostics);
        if (dto == null)
            return null;
        TryIdentifier(dto.ProfileId, context, $"{pointer}/profile_id", diagnostics, out SkillImportIdentifier profileId);
        return new(dto.CriticalFailureDamageDiceCount, dto.CriticalFailureDamageDiceSides, dto.CriticalFailureExecuteThresholdMaxHpPercent, dto.CriticalFailureFrightenedDurationTu, dto.CriticalFailureStunnedDurationTu, dto.FailureDamageDiceCount, dto.FailureDamageDiceSides, dto.FailureExecuteThresholdFixed, dto.FailureExecuteThresholdMaxHpPercent, dto.FailureFrightenedDurationTu, dto.FailureReactionLockDurationTu, profileId, dto.SuccessAftershockDurationTu);
    }

    private static DispelMagicEffectPayloadImportModel? NormalizeDispelPayload(JsonContentEntryContext context, JsonElement element, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        DispelMagicEffectPayloadJsonDto? dto = ParsePayload(context, element, pointer, SkillJsonImportSerializerContext.Default.DispelMagicEffectPayloadJsonDto, diagnostics);
        return dto == null ? null : new(Name(dto.BreaksBarrierLayer, context, $"{pointer}/breaks_barrier_layer", diagnostics));
    }

    private static OnKillGainResourcesEffectPayloadImportModel? NormalizeOnKillPayload(JsonContentEntryContext context, JsonElement element, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        OnKillGainResourcesEffectPayloadJsonDto? dto = ParsePayload(context, element, pointer, SkillJsonImportSerializerContext.Default.OnKillGainResourcesEffectPayloadJsonDto, diagnostics);
        return dto == null ? null : new(Closed<CombatOnKillGrantScopeImportKind>(dto.GrantScope ?? "", SkillCombatEffectValueRules.TryGrantScope, context, $"{pointer}/grant_scope", diagnostics), dto.RequireTargetDefeatedBySameSkill, dto.StackOnMultipleKills);
    }

    private static T? ParsePayload<T>(JsonContentEntryContext context, JsonElement element, string pointer, JsonTypeInfo<T> typeInfo, List<ContentJsonDiagnostic> diagnostics) where T : class
    {
        var payloadContext = new JsonContentEntryContext(context.DomainId, context.EntryId, context.SourceLabel, $"{context.JsonPointer}{pointer}");
        ContentImportStageResult<T> parsed = ContentJsonStrictDtoParser.Parse(payloadContext, element.GetRawText(), typeInfo, SkillJsonImportRules.InvalidEffectPayload);
        if (parsed.HasValue)
            return parsed.Value;
        diagnostics.AddRange(parsed.Diagnostics);
        return null;
    }

    private static IReadOnlyList<CombatWeightedStatusOutcomeImportModel> NormalizeWeightedOutcomes(JsonContentEntryContext context, IReadOnlyList<CombatWeightedStatusOutcomeJsonDto>? values, string pointer, List<ContentJsonDiagnostic> diagnostics, bool validateNumericRanges)
    {
        values ??= Array.Empty<CombatWeightedStatusOutcomeJsonDto>();
        var result = new List<CombatWeightedStatusOutcomeImportModel>(values.Count);
        for (int index = 0; index < values.Count; index += 1)
        {
            CombatWeightedStatusOutcomeJsonDto? value = values[index];
            string itemPointer = $"{pointer}/{index}";
            if (value == null || value.StatusEffect == null)
            {
                diagnostics.Add(Required(context, value == null ? itemPointer : $"{itemPointer}/status_effect"));
                continue;
            }
            CombatEffectImportModel? effect = NormalizeFullCombatEffect(context, value.StatusEffect, $"{itemPointer}/status_effect", diagnostics, validateNumericRanges);
            if (effect != null)
                result.Add(new(Name(value.OutcomeId, context, $"{itemPointer}/outcome_id", diagnostics), value.Weight ?? 1, effect));
        }
        return result;
    }

    private static IReadOnlyList<CombatEffectSlotWeightImportModel> NormalizeSlotWeights(JsonContentEntryContext context, IReadOnlyList<CombatEffectSlotWeightJsonDto>? values, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        values ??= Array.Empty<CombatEffectSlotWeightJsonDto>();
        var result = new List<CombatEffectSlotWeightImportModel>(values.Count);
        for (int index = 0; index < values.Count; index += 1)
        {
            CombatEffectSlotWeightJsonDto value = values[index];
            result.Add(new(Closed<CombatEquipmentSlotImportKind>(value.SlotId, SkillCombatEffectValueRules.TryEquipmentSlot, context, $"{pointer}/{index}/slot_id", diagnostics), value.Weight));
        }
        return result;
    }

    private static IReadOnlyList<CombatDamageSegmentImportModel> NormalizeDamageSegments(JsonContentEntryContext context, IReadOnlyList<CombatDamageSegmentJsonDto>? values, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        values ??= Array.Empty<CombatDamageSegmentJsonDto>();
        var result = new List<CombatDamageSegmentImportModel>(values.Count);
        for (int index = 0; index < values.Count; index += 1)
        {
            CombatDamageSegmentJsonDto value = values[index];
            string item = $"{pointer}/{index}";
            ValidateFinite(value.PreResistanceDamageMultiplier ?? 1.0, context, $"{item}/pre_resistance_damage_multiplier", diagnostics);
            result.Add(new(
                OptionalClosed<DamageTagImportKind>(value.DamageTag, SkillRootCombatImportValueRules.TryDamageTag, context, $"{item}/damage_tag", diagnostics),
                ClosedValues<DamageTagImportKind>(value.DamageTags, SkillRootCombatImportValueRules.TryDamageTag, context, $"{item}/damage_tags", diagnostics),
                ClosedValues<DamageTagImportKind>(value.MitigationBypassDamageTags, SkillRootCombatImportValueRules.TryDamageTag, context, $"{item}/mitigation_bypass_damage_tags", diagnostics),
                ClosedValues<DamageMitigationTierImportKind>(value.MitigationBypassTiers, SkillCombatEffectValueRules.TryMitigationTier, context, $"{item}/mitigation_bypass_tiers", diagnostics),
                value.Power, value.DiceCount, value.DiceSides, value.DiceBonus, value.DoubleDiceOnCritical, value.PreResistanceDamageMultiplier ?? 1.0
            ));
        }
        return result;
    }

    private static IReadOnlyList<CombatTargetDamageMultiplierRuleImportModel> NormalizeTargetDamageMultiplierRules(JsonContentEntryContext context, IReadOnlyList<CombatTargetDamageMultiplierRuleJsonDto>? values, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        values ??= Array.Empty<CombatTargetDamageMultiplierRuleJsonDto>();
        var result = new List<CombatTargetDamageMultiplierRuleImportModel>(values.Count);
        for (int index = 0; index < values.Count; index += 1)
        {
            CombatTargetDamageMultiplierRuleJsonDto value = values[index];
            string item = $"{pointer}/{index}";
            result.Add(new(Names(value.AnyCreatureTypeTags, context, $"{item}/any_creature_type_tags", diagnostics), Names(value.AllCreatureTypeTags, context, $"{item}/all_creature_type_tags", diagnostics), Names(value.ExcludedCreatureTypeTags, context, $"{item}/excluded_creature_type_tags", diagnostics), value.MultiplierPercent ?? 100));
        }
        return result;
    }

    private static SkillImportStringName Name(string? value, JsonContentEntryContext context, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        if (SkillImportStringName.TryCreate(value ?? "", out SkillImportStringName result))
            return result;
        diagnostics.Add(Diagnostic(SkillJsonImportRules.InvalidId, "StringName value must be empty or a canonical snake_case identifier.", context, pointer));
        return default;
    }

    private static IReadOnlyList<SkillImportStringName> Names(IEnumerable<string>? values, JsonContentEntryContext context, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        var result = new List<SkillImportStringName>();
        int index = 0;
        foreach (string value in values ?? Array.Empty<string>())
        {
            result.Add(Name(value, context, $"{pointer}/{index}", diagnostics));
            index += 1;
        }
        return result;
    }

    private static T Closed<T>(string? value, TryClosedValue<T> tryParse, JsonContentEntryContext context, string pointer, List<ContentJsonDiagnostic> diagnostics) where T : struct
    {
        if (tryParse(value, out T result))
            return result;
        diagnostics.Add(Diagnostic(SkillJsonImportRules.InvalidEffectPayload, "Value is not registered by the closed combat effect contract.", context, pointer));
        return default;
    }

    private static T? OptionalClosed<T>(string? value, TryClosedValue<T> tryParse, JsonContentEntryContext context, string pointer, List<ContentJsonDiagnostic> diagnostics) where T : struct =>
        string.IsNullOrEmpty(value) ? null : Closed(value, tryParse, context, pointer, diagnostics);

    private static IReadOnlyList<T> ClosedValues<T>(IEnumerable<string>? values, TryClosedValue<T> tryParse, JsonContentEntryContext context, string pointer, List<ContentJsonDiagnostic> diagnostics) where T : struct
    {
        var result = new List<T>();
        int index = 0;
        foreach (string value in values ?? Array.Empty<string>())
        {
            result.Add(Closed(value, tryParse, context, $"{pointer}/{index}", diagnostics));
            index += 1;
        }
        return result;
    }

    private static void ValidateFinite(double value, JsonContentEntryContext context, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        if (!double.IsFinite(value))
            AddRangeDiagnostic(context, pointer, diagnostics);
    }

    private static IReadOnlyDictionary<string, int> EmptyStringIntMap { get; } =
        new Dictionary<string, int>();
}
