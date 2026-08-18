#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;

internal static class SkillRootCombatJsonNormalizer
{
    private delegate bool TryClosedValue<T>(string? value, out T result)
        where T : struct;

    internal delegate CombatEffectImportModel? EffectNormalizer(
        CombatEffectJsonDto? dto,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    );

    internal static SkillImportModel? NormalizeSkill(
        JsonContentEntryContext context,
        SkillJsonDto dto,
        SkillImportIdentifier skillId,
        string displayName,
        string description,
        SkillImportType skillType,
        int maxLevel,
        SkillImportLearnSource learnSource,
        IEnumerable<SkillImportIdentifier> tags,
        string levelDescriptionTemplate,
        IEnumerable<KeyValuePair<int, SkillDescriptionVariables>> levelDescriptionConfigs,
        CombatSkillImportModel? combatProfile,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        int start = diagnostics.Count;
        string rawIconId = dto.IconId ?? "";
        SkillImportAssetId iconId = SkillImportAssetId.FromResource(rawIconId);
        if (
            rawIconId.Length > 0
            && !SkillImportAssetId.TryCreate(rawIconId, out iconId)
        )
        {
            diagnostics.Add(
                Diagnostic(
                    "skill.dto.asset_id.invalid",
                    "A non-empty asset ID must be a stable token, not a path or whitespace.",
                    context,
                    "/icon_id"
                )
            );
        }

        SkillImportUnlockMode unlockMode = Closed<SkillImportUnlockMode>(
            dto.UnlockMode,
            SkillRootCombatImportValueRules.TryUnlockMode,
            "skill.dto.unlock_mode.unknown",
            context,
            "/unlock_mode",
            diagnostics
        );
        SkillImportCoreSkillTransitionMode coreTransitionMode = Closed<SkillImportCoreSkillTransitionMode>(
            dto.CoreSkillTransitionMode,
            SkillRootCombatImportValueRules.TryCoreTransition,
            "skill.dto.core_skill_transition_mode.unknown",
            context,
            "/core_skill_transition_mode",
            diagnostics
        );
        SkillImportProgressionTier growthTier = CanonicalClosed<SkillImportProgressionTier>(
            dto.GrowthTier,
            wasPresent: false,
            SkillRootCombatImportValueRules.TryTier,
            "skill.dto.growth_tier.unknown",
            context,
            "/growth_tier",
            diagnostics
        );
        SkillImportProgressionTier practiceTier = CanonicalClosed<SkillImportProgressionTier>(
            dto.PracticeTier,
            wasPresent: false,
            SkillRootCombatImportValueRules.TryTier,
            "skill.dto.practice_tier.unknown",
            context,
            "/practice_tier",
            diagnostics
        );

        List<SkillImportIdentifier> learnRequirements = Identifiers(
            dto.LearnRequirements,
            context,
            "/learn_requirements",
            diagnostics
        );
        List<SkillImportIdentifier> knowledgeRequirements = Identifiers(
            dto.KnowledgeRequirements,
            context,
            "/knowledge_requirements",
            diagnostics
        );
        List<KeyValuePair<SkillImportIdentifier, int>> skillLevelRequirements = IdentifierMap(
            dto.SkillLevelRequirements,
            context,
            "/skill_level_requirements",
            diagnostics
        );
        List<KeyValuePair<SkillImportIdentifier, int>> attributeRequirements = IdentifierMap(
            dto.AttributeRequirements,
            context,
            "/attribute_requirements",
            diagnostics
        );
        List<SkillImportIdentifier> achievementRequirements = Identifiers(
            dto.AchievementRequirements,
            context,
            "/achievement_requirements",
            diagnostics
        );
        List<SkillImportIdentifier> upgradeSourceSkillIds = Identifiers(
            dto.UpgradeSourceSkillIds,
            context,
            "/upgrade_source_skill_ids",
            diagnostics
        );
        List<SkillImportIdentifier> masterySources = Identifiers(
            dto.MasterySources,
            context,
            "/mastery_sources",
            diagnostics
        );
        List<KeyValuePair<SkillImportIdentifier, int>> attributeGrowthProgress = IdentifierMap(
            dto.AttributeGrowthProgress,
            context,
            "/attribute_growth_progress",
            diagnostics
        );

        var attributeModifiers = new List<AttributeModifierImportModel>();
        if (dto.AttributeModifiers == null)
        {
            diagnostics.Add(Required(context, "/attribute_modifiers"));
        }
        else
        {
            for (int index = 0; index < dto.AttributeModifiers.Count; index++)
            {
                AttributeModifierJsonDto? value = dto.AttributeModifiers[index];
                string pointer = $"/attribute_modifiers/{index}";
                if (value == null)
                {
                    diagnostics.Add(Required(context, pointer));
                    continue;
                }
                attributeModifiers.Add(
                    new AttributeModifierImportModel(
                        Name(value.AttributeId, context, $"{pointer}/attribute_id", diagnostics),
                        Closed<AttributeModifierImportMode>(
                            value.Mode,
                            SkillRootCombatImportValueRules.TryAttributeModifierMode,
                            "skill.dto.attribute_modifier.mode.unknown",
                            context,
                            $"{pointer}/mode",
                            diagnostics
                        ),
                        value.Value,
                        value.ValuePerRank,
                        Name(value.SourceType, context, $"{pointer}/source_type", diagnostics),
                        Name(value.SourceId, context, $"{pointer}/source_id", diagnostics)
                    )
                );
            }
        }

        ContingencyAutomationImportModel? contingency = null;
        if (dto.ContingencyAutomationProfile != null)
        {
            ContingencyAutomationJsonDto value = dto.ContingencyAutomationProfile;
            contingency = new ContingencyAutomationImportModel(
                value.CanBeStoredInContingency,
                value.MinContingencySkillLevel ?? 1,
                Name(value.EffectCategory, context, "/contingency_automation_profile/effect_category", diagnostics),
                Names(value.Tags, context, "/contingency_automation_profile/tags", diagnostics),
                value.ContingencyLoadOverride,
                Names(value.AllowedTargetResolvers, context, "/contingency_automation_profile/allowed_target_resolvers", diagnostics),
                value.RequiresManualTargeting,
                ParameterBindings(
                    value.AllowedParameterBindings,
                    context,
                    "/contingency_automation_profile/allowed_parameter_bindings",
                    diagnostics
                )
            );
        }

        if (dto.MasteryCurve == null)
            diagnostics.Add(Required(context, "/mastery_curve"));

        return diagnostics.Count == start
            ? new SkillImportModel(
                skillId,
                displayName,
                description,
                skillType,
                maxLevel,
                learnSource,
                tags,
                levelDescriptionTemplate,
                levelDescriptionConfigs,
                combatProfile,
                iconId,
                dto.NonCoreMaxLevel,
                StatId(dto.DynamicMaxLevelStatId, context, "/dynamic_max_level_stat_id", diagnostics),
                dto.DynamicMaxLevelBase,
                dto.DynamicMaxLevelPerStat,
                dto.MasteryCurve!,
                learnRequirements,
                unlockMode,
                knowledgeRequirements,
                skillLevelRequirements,
                attributeRequirements,
                achievementRequirements,
                upgradeSourceSkillIds,
                dto.RetainSourceSkillsOnUnlock ?? true,
                coreTransitionMode,
                masterySources,
                growthTier,
                attributeGrowthProgress,
                practiceTier,
                attributeModifiers,
                contingency
            )
            : null;
    }

    internal static CombatSkillImportModel? NormalizeCombatSkill(
        JsonContentEntryContext context,
        CombatSkillJsonDto dto,
        SkillImportIdentifier skillId,
        CombatSkillImportTargetMode targetMode,
        CombatSkillImportTargetTeamFilter targetTeamFilter,
        CombatSkillImportRangePattern rangePattern,
        int rangeValue,
        CombatSkillImportAreaPattern areaPattern,
        int apCost,
        int mpCost,
        int cooldownTu,
        IEnumerable<CombatEffectImportModel> effectDefs,
        IEnumerable<KeyValuePair<int, CombatSkillLevelOverrideImportModel>> levelOverrides,
        EffectNormalizer normalizeEffect,
        bool requireCanonicalSquare2Payload,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        int start = diagnostics.Count;
        var passiveEffects = new List<CombatEffectImportModel>();
        if (dto.PassiveEffectDefs == null)
        {
            diagnostics.Add(Required(context, "/combat_profile/passive_effect_defs"));
        }
        else
        {
            for (int index = 0; index < dto.PassiveEffectDefs.Count; index++)
            {
                CombatEffectImportModel? effect = normalizeEffect(
                    dto.PassiveEffectDefs[index],
                    $"/combat_profile/passive_effect_defs/{index}",
                    diagnostics
                );
                if (effect != null)
                    passiveEffects.Add(effect);
            }
        }

        var castVariants = new List<CombatCastVariantImportModel>();
        if (dto.CastVariants == null)
        {
            diagnostics.Add(Required(context, "/combat_profile/cast_variants"));
        }
        else
        {
            for (int index = 0; index < dto.CastVariants.Count; index++)
            {
                CombatCastVariantImportModel? variant = NormalizeCastVariant(
                    context,
                    dto.CastVariants[index],
                    index,
                    normalizeEffect,
                    requireCanonicalSquare2Payload,
                    diagnostics
                );
                if (variant != null)
                    castVariants.Add(variant);
            }
        }

        CombatWeaponRangePolicyImportKind weaponRangePolicy = CanonicalClosed<CombatWeaponRangePolicyImportKind>(
            dto.WeaponRangePolicy, wasPresent: false,
            SkillRootCombatImportValueRules.TryWeaponRangePolicy,
            "skill.dto.weapon_range_policy.unknown", context,
            "/combat_profile/weapon_range_policy", diagnostics
        );
        PendingCastBindingModeKind pendingCastBindingMode = Closed<PendingCastBindingModeKind>(
            dto.PendingCastBindingMode, SkillJsonImportValueRules.TryParsePendingCastBindingMode,
            "skill.dto.pending_cast_binding_mode.unknown", context,
            "/combat_profile/pending_cast_binding_mode", diagnostics
        );
        CombatSkillLevelOverrideAttackResolutionMode attackResolutionMode = CanonicalClosed<CombatSkillLevelOverrideAttackResolutionMode>(
            dto.AttackResolutionMode, wasPresent: false,
            SkillRootCombatImportValueRules.TryAttackResolution,
            "skill.dto.attack_resolution_mode.unknown", context,
            "/combat_profile/attack_resolution_mode", diagnostics
        );
        CombatSkillLevelOverrideAttackDefenseMode attackDefenseMode = Closed<CombatSkillLevelOverrideAttackDefenseMode>(
            dto.AttackDefenseMode, SkillJsonImportValueRules.TryParseLevelOverrideAttackDefenseMode,
            "skill.dto.attack_defense_mode.unknown", context,
            "/combat_profile/attack_defense_mode", diagnostics
        );
        CombatMasteryTriggerImportKind masteryTriggerMode = Closed<CombatMasteryTriggerImportKind>(
            dto.MasteryTriggerMode, SkillRootCombatImportValueRules.TryMasteryTrigger,
            "skill.dto.mastery_trigger_mode.unknown", context,
            "/combat_profile/mastery_trigger_mode", diagnostics
        );
        CombatMasteryAmountImportKind masteryAmountMode = Closed<CombatMasteryAmountImportKind>(
            dto.MasteryAmountMode, SkillRootCombatImportValueRules.TryMasteryAmount,
            "skill.dto.mastery_amount_mode.unknown", context,
            "/combat_profile/mastery_amount_mode", diagnostics
        );
        CombatSpellFateImportKind spellFateMode = CanonicalClosed<CombatSpellFateImportKind>(
            dto.SpellFateMode, wasPresent: false,
            SkillRootCombatImportValueRules.TrySpellFate,
            "skill.dto.spell_fate_mode.unknown", context,
            "/combat_profile/spell_fate_mode", diagnostics
        );
        CombatSpellCriticalImportKind spellCriticalMode = CanonicalClosed<CombatSpellCriticalImportKind>(
            dto.SpellCriticalMode, wasPresent: false,
            SkillRootCombatImportValueRules.TrySpellCritical,
            "skill.dto.spell_critical_mode.unknown", context,
            "/combat_profile/spell_critical_mode", diagnostics
        );
        CombatBacklashImportKind backlashMode = CanonicalClosed<CombatBacklashImportKind>(
            dto.BacklashMode, wasPresent: false,
            SkillRootCombatImportValueRules.TryBacklash,
            "skill.dto.backlash_mode.unknown", context,
            "/combat_profile/backlash_mode", diagnostics
        );
        CombatSkillImportTargetTeamFilter? backlashTargetFilter = null;
        if (dto.BacklashTargetFilter.Length > 0)
        {
            if (
                SkillJsonImportValueRules.TryParseTargetTeamFilter(
                    dto.BacklashTargetFilter,
                    out CombatSkillImportTargetTeamFilter parsedBacklashTargetFilter
                )
            )
            {
                backlashTargetFilter = parsedBacklashTargetFilter;
            }
            else
            {
                diagnostics.Add(
                    Diagnostic(
                        "skill.dto.backlash_target_filter.unknown",
                        "Backlash target filter must be empty or a registered target team filter.",
                        context,
                        "/combat_profile/backlash_target_filter"
                    )
                );
            }
        }
        CombatAreaOriginImportKind areaOriginMode = Closed<CombatAreaOriginImportKind>(
            dto.AreaOriginMode, SkillRootCombatImportValueRules.TryAreaOrigin,
            "skill.dto.area_origin_mode.unknown", context,
            "/combat_profile/area_origin_mode", diagnostics
        );
        CombatAreaDirectionImportKind areaDirectionMode = Closed<CombatAreaDirectionImportKind>(
            dto.AreaDirectionMode, SkillRootCombatImportValueRules.TryAreaDirection,
            "skill.dto.area_direction_mode.unknown", context,
            "/combat_profile/area_direction_mode", diagnostics
        );
        CombatBaseProjectileImportKind projectileKind = Closed<CombatBaseProjectileImportKind>(
            dto.ProjectileKind, SkillRootCombatImportValueRules.TryBaseProjectile,
            "skill.dto.projectile_kind.unknown", context,
            "/combat_profile/projectile_kind", diagnostics
        );
        CombatTargetSelectionImportKind targetSelectionMode = Closed<CombatTargetSelectionImportKind>(
            dto.TargetSelectionMode, SkillRootCombatImportValueRules.TryTargetSelection,
            "skill.dto.target_selection_mode.unknown", context,
            "/combat_profile/target_selection_mode", diagnostics
        );
        CombatUnitTargetResolutionImportKind unitTargetResolutionMode = Closed<CombatUnitTargetResolutionImportKind>(
            dto.UnitTargetResolutionMode, SkillRootCombatImportValueRules.TryUnitTargetResolution,
            "skill.dto.unit_target_resolution_mode.unknown", context,
            "/combat_profile/unit_target_resolution_mode", diagnostics
        );
        CombatSelectionOrderImportKind selectionOrderMode = Closed<CombatSelectionOrderImportKind>(
            dto.SelectionOrderMode, SkillRootCombatImportValueRules.TrySelectionOrder,
            "skill.dto.selection_order_mode.unknown", context,
            "/combat_profile/selection_order_mode", diagnostics
        );

        return diagnostics.Count == start
            ? new CombatSkillImportModel(
                skillId: skillId,
                targetMode: targetMode,
                targetTeamFilter: targetTeamFilter,
                rangePattern: rangePattern,
                rangeValue: rangeValue,
                areaPattern: areaPattern,
                apCost: apCost,
                mpCost: mpCost,
                cooldownTu: cooldownTu,
                effectDefs: effectDefs,
                levelOverrides: levelOverrides,
                excludedTargetCreatureTypeTags: Names(dto.ExcludedTargetCreatureTypeTags, context, "/combat_profile/excluded_target_creature_type_tags", diagnostics),
                rangeMovePointCapacityMultiplier: dto.RangeMovePointCapacityMultiplier,
                weaponRangePolicy: weaponRangePolicy,
                areaValue: dto.AreaValue,
                requiresLos: dto.RequiresLos,
                groundEffectRequireFullArea: dto.GroundEffectRequireFullArea,
                groundEffectRequireEmpty: dto.GroundEffectRequireEmpty,
                groundEffectRequireTraversable: dto.GroundEffectRequireTraversable,
                staminaCost: dto.StaminaCost,
                mpCostPerTargetSlot: dto.MpCostPerTargetSlot,
                staminaCostPerTargetSlot: dto.StaminaCostPerTargetSlot,
                castingTimeTu: dto.CastingTimeTu,
                castingMaintenanceDc: dto.CastingMaintenanceDc,
                castingSpellControlDc: dto.CastingSpellControlDc,
                windupProfile: Windup(dto.WindupProfile),
                directionalPiercingProfile: Directional(dto.DirectionalPiercingProfile),
                approachAttackProfile: dto.ApproachAttackProfile == null ? null : new CombatApproachAttackImportModel(dto.ApproachAttackProfile.MaximumPathHeightDeltaFromOrigin),
                lineThroughAttackProfile: LineThrough(dto.LineThroughAttackProfile),
                sequentialLineHitProfile: Sequential(dto.SequentialLineHitProfile),
                spellReactionProfile: SpellReaction(dto.SpellReactionProfile, context, diagnostics),
                rangedWeaponReactionProfile: RangedReaction(dto.RangedWeaponReactionProfile, context, diagnostics),
                pendingCastBindingMode: pendingCastBindingMode,
                attackRollBonus: dto.AttackRollBonus,
                attackResolutionMode: attackResolutionMode,
                attackDefenseMode: attackDefenseMode,
                auraCost: dto.AuraCost,
                masteryTriggerMode: masteryTriggerMode,
                masteryAmountMode: masteryAmountMode,
                masteryBaseAmount: dto.MasteryBaseAmount ?? 1,
                spellFateMode: spellFateMode,
                spellCriticalMode: spellCriticalMode,
                spellCriticalMpRefundPercent: dto.SpellCriticalMpRefundPercent,
                fumbleProtectionCurve: dto.FumbleProtectionCurve,
                fumbleProtectionExtraMpPercent: dto.FumbleProtectionExtraMpPercent ?? 100,
                backlashMode: backlashMode,
                backlashTargetFilter: backlashTargetFilter,
                backlashOffsetRadius: dto.BacklashOffsetRadius,
                areaOriginMode: areaOriginMode,
                areaDirectionMode: areaDirectionMode,
                aiTags: Names(dto.AiTags, context, "/combat_profile/ai_tags", diagnostics),
                deliveryCategories: Names(dto.DeliveryCategories, context, "/combat_profile/delivery_categories", diagnostics),
                attackRollBonusStatusId: Name(dto.AttackRollBonusStatusId, context, "/combat_profile/attack_roll_bonus_status_id", diagnostics),
                attackRollBonusStatusStackDivisor: dto.AttackRollBonusStatusStackDivisor,
                projectileKind: projectileKind,
                specialResolutionProfileId: Name(dto.SpecialResolutionProfileId, context, "/combat_profile/special_resolution_profile_id", diagnostics),
                targetSelectionMode: targetSelectionMode,
                minTargetCount: dto.MinTargetCount ?? 1,
                maxTargetCount: dto.MaxTargetCount ?? 1,
                allowRepeatTarget: dto.AllowRepeatTarget,
                unitTargetResolutionMode: unitTargetResolutionMode,
                maxHitsPerTarget: dto.MaxHitsPerTarget,
                randomChainAttackCount: dto.RandomChainAttackCount,
                randomChainContinueOnMiss: dto.RandomChainContinueOnMiss,
                selectionOrderMode: selectionOrderMode,
                passiveEffectDefs: passiveEffects,
                castVariants: castVariants,
                requiredWeaponFamilies: Names(dto.RequiredWeaponFamilies, context, "/combat_profile/required_weapon_families", diagnostics),
                allowsNaturalWeapon: dto.AllowsNaturalWeapon,
                requiresHeavyWeapon: dto.RequiresHeavyWeapon,
                requiredWeaponTypeIds: Names(dto.RequiredWeaponTypeIds, context, "/combat_profile/required_weapon_type_ids", diagnostics),
                excludedWeaponFamilies: Names(dto.ExcludedWeaponFamilies, context, "/combat_profile/excluded_weapon_families", diagnostics),
                excludedWeaponTypeIds: Names(dto.ExcludedWeaponTypeIds, context, "/combat_profile/excluded_weapon_type_ids", diagnostics),
                requiresEquippedShield: dto.RequiresEquippedShield,
                masteryLowHpBonusMultiplier: dto.MasteryLowHpBonusMultiplier ?? 1,
                masteryLowHpThresholdPercent: dto.MasteryLowHpThresholdPercent ?? 50
            )
            : null;
    }

    private static CombatCastVariantImportModel? NormalizeCastVariant(
        JsonContentEntryContext context,
        CombatCastVariantJsonDto? dto,
        int index,
        EffectNormalizer normalizeEffect,
        bool requireCanonicalSquare2Payload,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        string pointer = $"/combat_profile/cast_variants/{index}";
        int start = diagnostics.Count;
        if (dto == null)
        {
            diagnostics.Add(Required(context, pointer));
            return null;
        }
        TryIdentifier(dto.VariantId, context, $"{pointer}/variant_id", diagnostics, out SkillImportIdentifier variantId);
        CombatSkillImportTargetMode targetMode = Closed<CombatSkillImportTargetMode>(
            dto.TargetMode,
            SkillJsonImportValueRules.TryParseTargetMode,
            "skill.dto.cast_variant.target_mode.unknown",
            context,
            $"{pointer}/target_mode",
            diagnostics
        );
        CombatCastFootprintImportKind footprintPattern = Closed<CombatCastFootprintImportKind>(
            dto.FootprintPattern,
            SkillRootCombatImportValueRules.TryFootprint,
            "skill.dto.cast_variant.footprint_pattern.unknown",
            context,
            $"{pointer}/footprint_pattern",
            diagnostics
        );
        CombatProjectileImportKind projectileKindOverride = CanonicalClosed<CombatProjectileImportKind>(
            dto.ProjectileKindOverride,
            wasPresent: false,
            SkillRootCombatImportValueRules.TryProjectile,
            "skill.dto.cast_variant.projectile_kind_override.unknown",
            context,
            $"{pointer}/projectile_kind_override",
            diagnostics
        );
        var allowedBaseTerrains = new List<BattleTerrainImportKind>();
        for (int terrainIndex = 0; terrainIndex < dto.AllowedBaseTerrains.Count; terrainIndex++)
        {
            allowedBaseTerrains.Add(
                Closed<BattleTerrainImportKind>(
                    dto.AllowedBaseTerrains[terrainIndex],
                    SkillRootCombatImportValueRules.TryTerrain,
                    "skill.dto.cast_variant.allowed_base_terrain.unknown",
                    context,
                    $"{pointer}/allowed_base_terrains/{terrainIndex}",
                    diagnostics
                )
            );
        }
        var effects = new List<CombatEffectImportModel>();
        if (dto.EffectDefs == null)
            diagnostics.Add(Required(context, $"{pointer}/effect_defs"));
        else
        {
            for (int effectIndex = 0; effectIndex < dto.EffectDefs.Count; effectIndex++)
            {
                CombatEffectImportModel? effect = normalizeEffect(
                    dto.EffectDefs[effectIndex],
                    $"{pointer}/effect_defs/{effectIndex}",
                    diagnostics
                );
                if (effect != null)
                    effects.Add(effect);
            }
        }
        CombatCastVariantPayloadImportModel? payload = NormalizeCastPayload(
            context,
            dto.Payload,
            footprintPattern,
            requireCanonicalSquare2Payload,
            $"{pointer}/payload",
            diagnostics
        );
        return diagnostics.Count == start && payload != null
            ? new CombatCastVariantImportModel(
                variantId,
                dto.DisplayName,
                dto.Description,
                dto.MinSkillLevel,
                targetMode,
                footprintPattern,
                dto.RequiredCoordCount ?? 1,
                allowedBaseTerrains,
                projectileKindOverride,
                effects,
                payload
            )
            : null;
    }

    private static CombatCastVariantPayloadImportModel? NormalizeCastPayload(
        JsonContentEntryContext context,
        CombatCastVariantPayloadJsonDto? payload,
        CombatCastFootprintImportKind footprintPattern,
        bool requireCanonicalSquare2Payload,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (payload == null)
        {
            if (
                requireCanonicalSquare2Payload
                && footprintPattern == CombatCastFootprintImportKind.Square2
            )
            {
                diagnostics.Add(
                    Diagnostic(
                        "skill.dto.cast_payload.square2_corner.required",
                        "square2 cast variants require a typed square2_corner payload.",
                        context,
                        $"{pointer}/square2_corner"
                    )
                );
                return null;
            }
            return new CombatCastVariantPayloadImportModel(null);
        }
        CombatCastSquare2Corner? corner = null;
        if (payload.Square2Corner != null)
        {
            if (
                SkillRootCombatImportValueRules.TrySquare2Corner(
                    payload.Square2Corner,
                    out CombatCastSquare2Corner parsedCorner
                )
            )
            {
                corner = parsedCorner;
            }
            else
            {
                diagnostics.Add(Diagnostic("skill.dto.cast_payload.square2_corner.unknown", "square2_corner is not registered.", context, $"{pointer}/square2_corner"));
                return null;
            }
        }
        if (
            requireCanonicalSquare2Payload
            && footprintPattern == CombatCastFootprintImportKind.Square2
            && corner == null
        )
        {
            diagnostics.Add(
                Diagnostic(
                    "skill.dto.cast_payload.square2_corner.required",
                    "square2 cast variants require a typed square2_corner payload.",
                    context,
                    $"{pointer}/square2_corner"
                )
            );
            return null;
        }
        return new CombatCastVariantPayloadImportModel(corner);
    }

    private static CombatWindupImportModel? Windup(CombatWindupJsonDto? value) =>
        value == null ? null : new CombatWindupImportModel(value.StaminaCostPerTier ?? 6, value.WeaponDicePerTier ?? 1, SkillImportCollections.Freeze(value.SkillLevelTierCaps), SkillImportCollections.Freeze(value.BaseWeaponDiceMultipliers));

    private static CombatDirectionalPiercingImportModel? Directional(CombatDirectionalPiercingJsonDto? value) =>
        value == null ? null : new CombatDirectionalPiercingImportModel(SkillImportCollections.Freeze(value.BaseDamagePercentCurve), value.SuccessfulHitDecayPercent ?? 20, value.MinimumDamagePercent ?? 40, value.StaminaFlatBase ?? 32, value.StaminaRangeSquareCoefficient ?? 1, value.StaminaStrengthSquareScale ?? 100, value.MinimumStaminaCost ?? 1, value.MaximumHeightDelta ?? 1);

    private static CombatLineThroughAttackImportModel? LineThrough(CombatLineThroughAttackJsonDto? value) =>
        value == null ? null : new CombatLineThroughAttackImportModel(value.MaximumWeaponRange ?? 2, value.IntermediateWeaponDiceMultiplier ?? 1, SkillImportCollections.Freeze(value.PrimaryWeaponDiceMultiplierCurve), SkillImportCollections.Freeze(value.PrimaryAttackRollBonusCurve), value.SuccessfulIntermediateHitBonusWeaponDice ?? 1, value.SuccessfulIntermediateHitAttackRollBonus ?? 1, SkillImportCollections.Freeze(value.SuccessfulIntermediateHitBonusCapCurve));

    private static CombatSequentialLineHitImportModel? Sequential(CombatSequentialLineHitJsonDto? value) =>
        value == null ? null : new CombatSequentialLineHitImportModel(SkillImportCollections.Freeze(value.MinimumPrimaryDistanceCurve), SkillImportCollections.Freeze(value.ContinuationRangeCurve), SkillImportCollections.Freeze(value.FollowUpAttackPenaltyCurve));

    private static CombatSpellReactionImportModel? SpellReaction(
        CombatSpellReactionJsonDto? value,
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (value == null)
            return null;
        CombatSaveAbilityImportKind saveAbility = Closed<CombatSaveAbilityImportKind>(
            value.SaveAbility,
            SkillRootCombatImportValueRules.TrySaveAbility,
            "skill.dto.spell_reaction.save_ability.unknown",
            context,
            "/combat_profile/spell_reaction_profile/save_ability",
            diagnostics
        );
        return new CombatSpellReactionImportModel(
            Name(value.TriggerDeliveryCategory, context, "/combat_profile/spell_reaction_profile/trigger_delivery_category", diagnostics),
            Name(value.ReactionSkillId, context, "/combat_profile/spell_reaction_profile/reaction_skill_id", diagnostics),
            Name(value.ReadinessStatusId, context, "/combat_profile/spell_reaction_profile/readiness_status_id", diagnostics),
            Name(value.RequiredWeaponFamily, context, "/combat_profile/spell_reaction_profile/required_weapon_family", diagnostics),
            saveAbility,
            Name(value.SaveTag, context, "/combat_profile/spell_reaction_profile/save_tag", diagnostics),
            value.BaseSaveDc ?? 10,
            value.HpDamageDivisor ?? 2,
            value.AttackRollBonusBySkillLevel,
            value.SaveDcBonusBySkillLevel,
            value.RequireHpDamage ?? true,
            value.ConsumeOnTrigger ?? true,
            value.ExpireOnOwnerTurnStart ?? true
        );
    }

    private static CombatRangedWeaponReactionImportModel? RangedReaction(
        CombatRangedWeaponReactionJsonDto? value,
        JsonContentEntryContext context,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (value == null)
            return null;
        CombatSkillLevelOverrideAttackDefenseMode attackDefenseMode = Closed<CombatSkillLevelOverrideAttackDefenseMode>(
            value.AttackDefenseMode,
            SkillJsonImportValueRules.TryParseLevelOverrideAttackDefenseMode,
            "skill.dto.ranged_weapon_reaction.attack_defense_mode.unknown",
            context,
            "/combat_profile/ranged_weapon_reaction_profile/attack_defense_mode",
            diagnostics
        );
        DamageTagImportKind damageTag = Closed<DamageTagImportKind>(
            value.DamageTag,
            SkillRootCombatImportValueRules.TryDamageTag,
            "skill.dto.ranged_weapon_reaction.damage_tag.unknown",
            context,
            "/combat_profile/ranged_weapon_reaction_profile/damage_tag",
            diagnostics
        );
        return new CombatRangedWeaponReactionImportModel(
            Name(value.ReadinessStatusId, context, "/combat_profile/ranged_weapon_reaction_profile/readiness_status_id", diagnostics),
            Names(value.TriggerWeaponFamilies, context, "/combat_profile/ranged_weapon_reaction_profile/trigger_weapon_families", diagnostics),
            damageTag,
            attackDefenseMode,
            value.AttackRollBonusBySkillLevel,
            value.ConsumeStatusStacks ?? 1,
            value.TriggerOnHit ?? true,
            value.TriggerOnMiss ?? true,
            value.AllowCritical
        );
    }

    private static List<SkillImportIdentifier> Identifiers(IReadOnlyList<string>? values, JsonContentEntryContext context, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        var result = new List<SkillImportIdentifier>();
        if (values == null) { diagnostics.Add(Required(context, pointer)); return result; }
        for (int index = 0; index < values.Count; index++)
            if (TryIdentifier(values[index], context, $"{pointer}/{index}", diagnostics, out SkillImportIdentifier value)) result.Add(value);
        return result;
    }

    private static List<KeyValuePair<SkillImportIdentifier, int>> IdentifierMap(IReadOnlyDictionary<string, int>? values, JsonContentEntryContext context, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        var result = new List<KeyValuePair<SkillImportIdentifier, int>>();
        if (values == null) { diagnostics.Add(Required(context, pointer)); return result; }
        foreach ((string key, int value) in values)
            if (TryIdentifier(key, context, $"{pointer}/{Escape(key)}", diagnostics, out SkillImportIdentifier identifier)) result.Add(new(identifier, value));
        return result;
    }

    private static List<SkillImportStringName> Names(IReadOnlyList<string>? values, JsonContentEntryContext context, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        var result = new List<SkillImportStringName>();
        if (values == null) { diagnostics.Add(Required(context, pointer)); return result; }
        for (int index = 0; index < values.Count; index++) result.Add(Name(values[index], context, $"{pointer}/{index}", diagnostics));
        return result;
    }

    private static SkillImportStringName Name(string? value, JsonContentEntryContext context, string pointer, List<ContentJsonDiagnostic> diagnostics)
    {
        if (SkillImportStringName.TryCreate(value, out SkillImportStringName result))
            return result;
        diagnostics.Add(
            Diagnostic(
                "skill.dto.string_name.invalid",
                "StringName value must be empty or canonical lower snake_case ASCII.",
                context,
                pointer
            )
        );
        SkillImportStringName.TryCreate("", out result);
        return result;
    }

    private static SkillImportStatId StatId(
        string? value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        if (SkillImportStatId.TryCreate(value, out SkillImportStatId result))
            return result;
        diagnostics.Add(
            Diagnostic(
                "skill.dto.stat_id.invalid",
                "Stat ID must be empty, lower snake_case, or a namespace:name pair.",
                context,
                pointer
            )
        );
        SkillImportStatId.TryCreate("", out result);
        return result;
    }

    private static T Closed<T>(
        string? value,
        TryClosedValue<T> parser,
        string rule,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    ) where T : struct
    {
        if (parser(value, out T result))
            return result;
        diagnostics.Add(
            Diagnostic(
                rule,
                "Business string is not registered by this field's closed import rule.",
                context,
                pointer
            )
        );
        return default;
    }

    private static T CanonicalClosed<T>(
        string? value,
        bool wasPresent,
        TryClosedValue<T> parser,
        string rule,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    ) where T : struct
    {
        if (!wasPresent || !string.IsNullOrEmpty(value))
            return Closed(value, parser, rule, context, pointer, diagnostics);

        diagnostics.Add(
            Diagnostic(
                rule,
                "Business string must be omitted for its default or use a registered canonical value.",
                context,
                pointer
            )
        );
        return default;
    }

    private static List<KeyValuePair<SkillImportIdentifier, ContingencyParameterBindingImportValue>>
        ParameterBindings(
            IReadOnlyDictionary<string, JsonElement>? values,
            JsonContentEntryContext context,
            string pointer,
            List<ContentJsonDiagnostic> diagnostics
        )
    {
        var result = new List<KeyValuePair<SkillImportIdentifier, ContingencyParameterBindingImportValue>>();
        if (values == null)
            return result;
        foreach ((string key, JsonElement value) in values)
        {
            string valuePointer = $"{pointer}/{Escape(key)}";
            if (!TryIdentifier(key, context, valuePointer, diagnostics, out SkillImportIdentifier identifier))
                continue;
            int diagnosticCount = diagnostics.Count;
            ContingencyParameterBindingImportValue? normalized = value.ValueKind switch
            {
                JsonValueKind.True => new ContingencyBoolBindingImportValue(true),
                JsonValueKind.False => new ContingencyBoolBindingImportValue(false),
                JsonValueKind.String => new ContingencyStringBindingImportValue(value.GetString() ?? ""),
                JsonValueKind.Number when value.TryGetInt64(out long integer) =>
                    new ContingencyIntBindingImportValue(integer),
                JsonValueKind.Number
                    when value.TryGetDouble(out double number) && double.IsFinite(number) =>
                    new ContingencyFloatBindingImportValue(number),
                JsonValueKind.Array => NormalizeBindingStringList(
                    value,
                    context,
                    valuePointer,
                    diagnostics
                ),
                _ => null,
            };
            if (normalized == null)
            {
                if (diagnostics.Count == diagnosticCount)
                {
                    diagnostics.Add(
                        Diagnostic(
                            "skill.dto.contingency.parameter_binding_value.invalid",
                            "Parameter binding value must be bool, integer, finite number, string, or a string array.",
                            context,
                            valuePointer
                        )
                    );
                }
                continue;
            }
            result.Add(new(identifier, normalized));
        }
        return result;
    }

    private static ContingencyStringListBindingImportValue? NormalizeBindingStringList(
        JsonElement value,
        JsonContentEntryContext context,
        string pointer,
        List<ContentJsonDiagnostic> diagnostics
    )
    {
        var values = new List<string>();
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                diagnostics.Add(
                    Diagnostic(
                        "skill.dto.contingency.parameter_binding_array_item.invalid",
                        "Parameter binding array items must be strings.",
                        context,
                        $"{pointer}/{index}"
                    )
                );
                return null;
            }
            values.Add(item.GetString() ?? "");
            index += 1;
        }
        return new ContingencyStringListBindingImportValue(values);
    }

    private static bool TryIdentifier(string? value, JsonContentEntryContext context, string pointer, List<ContentJsonDiagnostic> diagnostics, out SkillImportIdentifier identifier)
    {
        if (SkillImportIdentifier.TryCreate(value, out identifier)) return true;
        diagnostics.Add(Diagnostic(SkillJsonImportRules.InvalidId, "Content ID must be canonical lower snake_case ASCII.", context, pointer));
        return false;
    }

    private static ContentJsonDiagnostic Required(JsonContentEntryContext context, string pointer) => Diagnostic(SkillJsonImportRules.RequiredMember, "Skill JSON member is null where a concrete value is required.", context, pointer);
    private static ContentJsonDiagnostic Diagnostic(string rule, string message, JsonContentEntryContext context, string pointer) => new(rule, message, context.SourceLabel, $"{context.JsonPointer}{pointer}");
    private static string Escape(string value) => (value ?? "").Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
}
