using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal static class EquipmentAbilityDefinitionProjection
{
    internal static EquipmentAbilityContentPackDefinition ProjectPack(
        EquipmentAbilityContentPackImportModel source,
        IReadOnlyList<EquipmentAbilityBindingDefinition> bindings
    )
    {
        return new EquipmentAbilityContentPackDefinition
        {
            PackId = source.pack_id,
            SchemaVersion = source.schema_version,
            LoadOrder = source.load_order,
            Dependencies = CopyStringNames(source.dependencies),
            Bindings = new ReadOnlyCollection<EquipmentAbilityBindingDefinition>(
                new List<EquipmentAbilityBindingDefinition>(bindings)
            ),
        };
    }

    internal static EquipmentAbilityBindingDefinition ProjectBinding(EquipmentAbilityBindingImportModel source)
    {
        return new EquipmentAbilityBindingDefinition
        {
            BindingId = source.binding_id,
            TraitId = source.trait_id,
            OverrideMode = TryParseOverrideMode(source.override_mode, out var mode)
                ? mode
                : EquipmentAbilityBindingOverrideMode.Add,
            ReplacesBindingId = source.replaces_binding_id,
            AllowedSourceKinds = ProjectSourceKinds(source.allowed_source_kinds),
            RequiredTraitCategories = CopyStringNameSet(source.required_trait_categories),
            RequiredEffectiveTraitIds = CopyStringNameSet(source.required_effective_trait_ids),
            RequiredItemTags = CopyStringNameSet(source.required_item_tags),
            SupportedEquipmentTypeIds = CopyStringNameSet(source.supported_equipment_type_ids),
            ActivationStatusId = source.activation_status_id,
            StateSchemas = ProjectStateSchemas(source.state_schemas),
            Reactions = ProjectReactions(source.reactions),
            FatalIntercepts = ProjectFatalIntercepts(source.fatal_intercepts),
            MitigationAuras = ProjectMitigationAuras(source.mitigation_auras),
            MovementTrails = ProjectMovementTrails(source.movement_trails),
            GrantedActions = ProjectGrantedActions(source.granted_actions),
            TemporalProgressModifiers = ProjectTemporalProgressModifiers(
                source.binding_id,
                source.temporal_progress_modifiers
            ),
            CognitionCeilingModifiers =
                ProjectCognitionCeilingModifiers(
                    source.binding_id,
                    source.cognition_ceiling_modifiers
                ),
            WeaponProfileOverlays = ProjectWeaponProfileOverlays(source.weapon_profile_overlays),
            WorldEffects = ProjectWorldEffects(source.world_effects),
        };
    }

    private static IReadOnlyList<EquipmentFatalInterceptDefinition> ProjectFatalIntercepts(
        IReadOnlyList<EquipmentFatalInterceptImportModel> values
    )
    {
        if (values == null || values.Count == 0)
            return Array.Empty<EquipmentFatalInterceptDefinition>();

        var result = new List<EquipmentFatalInterceptDefinition>();
        foreach (EquipmentFatalInterceptImportModel value in values)
        {
            if (value == null)
                continue;
            EquipmentAbilityUsagePeriodKinds.TryParse(
                value.usage_period_kind,
                out EquipmentAbilityUsagePeriodKind usagePeriodKind
            );
            result.Add(
                new EquipmentFatalInterceptDefinition
                {
                    InterceptId = value.intercept_id,
                    ResolutionOrder = value.resolution_order,
                    ProtectionPriority = value.protection_priority,
                    UsagePeriodKind = usagePeriodKind,
                    MaxAttemptsPerPeriod = value.max_attempts_per_period,
                    ConsumeOnAttempt = value.consume_on_attempt,
                    RollGate = ProjectRollGate(value.roll_gate),
                    RecoveryKind = TryParseFatalInterceptRecoveryKind(
                        value.recovery_kind,
                        out EquipmentFatalInterceptRecoveryKind recoveryKind
                    )
                        ? recoveryKind
                        : EquipmentFatalInterceptRecoveryKind.HpDice,
                    RecoveryDice = ProjectDice(value.recovery_dice),
                    RecoveryPercentBasisPoints = value.recovery_percent_basis_points,
                    SuccessActions = ProjectActions(value.success_actions),
                }
            );
        }
        return result.Count > 0
            ? new ReadOnlyCollection<EquipmentFatalInterceptDefinition>(result)
            : Array.Empty<EquipmentFatalInterceptDefinition>();
    }

    private static IReadOnlyList<EquipmentMitigationAuraDefinition> ProjectMitigationAuras(
        IReadOnlyList<EquipmentMitigationAuraImportModel> values
    )
    {
        if (values == null || values.Count == 0)
            return Array.Empty<EquipmentMitigationAuraDefinition>();
        var result = new List<EquipmentMitigationAuraDefinition>();
        foreach (EquipmentMitigationAuraImportModel value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentMitigationAuraDefinition
                {
                    AuraId = value.aura_id,
                    Radius = Math.Max(value.radius, 0),
                    TargetTeamFilter = value.target_team_filter,
                    DamageTag = value.damage_tag,
                    MitigationTier = value.mitigation_tier,
                    Label = value.label ?? "",
                }
            );
        }
        return result.Count == 0
            ? Array.Empty<EquipmentMitigationAuraDefinition>()
            : new ReadOnlyCollection<EquipmentMitigationAuraDefinition>(result);
    }

    private static IReadOnlyList<EquipmentMovementTrailDefinition> ProjectMovementTrails(
        IReadOnlyList<EquipmentMovementTrailImportModel> values
    )
    {
        if (values == null || values.Count == 0)
            return Array.Empty<EquipmentMovementTrailDefinition>();
        var result = new List<EquipmentMovementTrailDefinition>();
        foreach (EquipmentMovementTrailImportModel value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentMovementTrailDefinition
                {
                    TrailId = value.trail_id,
                    ReplacementGroupId = value.replacement_group_id,
                    Priority = value.priority,
                    RequiredSkillId = value.required_skill_id,
                    DurationTu = value.duration_tu,
                    TargetTeamFilter = value.target_team_filter,
                    DamageDice = ProjectDice(value.damage_dice),
                    DamageTag = value.damage_tag,
                    DamageTags = CopyStringNames(value.damage_tags),
                    DisplayName = value.display_name ?? "",
                }
            );
        }
        return result.Count == 0
            ? Array.Empty<EquipmentMovementTrailDefinition>()
            : new ReadOnlyCollection<EquipmentMovementTrailDefinition>(result);
    }

    private static IReadOnlyList<EquipmentTemporalProgressModifierDefinition> ProjectTemporalProgressModifiers(
        StringName bindingId,
        IReadOnlyList<EquipmentTemporalProgressModifierImportModel> values
    )
    {
        if (values == null || values.Count == 0)
            return Array.Empty<EquipmentTemporalProgressModifierDefinition>();
        var result = new List<EquipmentTemporalProgressModifierDefinition>();
        foreach (EquipmentTemporalProgressModifierImportModel value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentTemporalProgressModifierDefinition
                {
                    ModifierId = value.modifier_id,
                    BindingId = bindingId,
                    AppliesToActionProgress = value.applies_to_action_progress,
                    AppliesToCastProgress = value.applies_to_cast_progress,
                    SaveDc = Math.Max(value.save_dc, 0),
                    AttributeModifierId = value.attribute_modifier_id,
                    SuccessRatePercent = Math.Max(value.success_rate_percent, 0),
                    FailureRatePercent = Math.Max(value.failure_rate_percent, 0),
                    Label = value.label ?? "",
                }
            );
        }
        return result.Count > 0
            ? new ReadOnlyCollection<EquipmentTemporalProgressModifierDefinition>(result)
            : Array.Empty<EquipmentTemporalProgressModifierDefinition>();
    }

    private static
        IReadOnlyList<EquipmentCognitionCeilingModifierDefinition>
            ProjectCognitionCeilingModifiers(
                StringName bindingId,
                IReadOnlyList<
                    EquipmentCognitionCeilingModifierImportModel
                > values
            )
    {
        if (values == null || values.Count == 0)
        {
            return Array.Empty<
                EquipmentCognitionCeilingModifierDefinition
            >();
        }
        var result =
            new List<
                EquipmentCognitionCeilingModifierDefinition
            >();
        foreach (
            EquipmentCognitionCeilingModifierImportModel value in values
        )
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentCognitionCeilingModifierDefinition
                {
                    ModifierId = value.modifier_id,
                    BindingId = bindingId,
                    CognitionCeiling =
                        BattleCognitionContentRules.ToKind(
                            value.cognition_ceiling
                        ),
                }
            );
        }
        return new ReadOnlyCollection<
            EquipmentCognitionCeilingModifierDefinition
        >(result);
    }

    private static IReadOnlySet<StringName> ProjectSourceKinds(
        IReadOnlyList<string> values
    )
    {
        var result = new HashSet<StringName>();
        foreach (string value in values)
        {
            TraitSourceKind kind = TraitContentRules.ToSourceKind(value);
            if (
                kind == TraitSourceKind.EquipmentFixed
                || kind == TraitSourceKind.EquipmentRoll
                || kind == TraitSourceKind.GearSetThreshold
            )
                result.Add(TraitContentRules.ToStringName(kind));
        }
        return EquipmentAbilityReadOnlySet<StringName>.From(result);
    }

    private static IReadOnlyList<EquipmentAbilityStateSchemaDefinition> ProjectStateSchemas(
        IReadOnlyList<EquipmentAbilityStateSchemaImportModel> values
    )
    {
        var result = new List<EquipmentAbilityStateSchemaDefinition>();
        foreach (EquipmentAbilityStateSchemaImportModel value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentAbilityStateSchemaDefinition
                {
                    StateKey = value.state_key,
                    OwnerScope = value.owner_scope,
                    ValueKind = value.value_kind,
                    InitialIntValue = value.initial_int_value,
                    MaxIntValue = value.max_int_value,
                    ResetTiming = value.reset_timing,
                    PersistOutsideBattle = value.persist_outside_battle,
                    VisibleToUi = value.visible_to_ui,
                    SyncSourceStateKey = value.sync_source_state_key,
                    SyncAggregation = value.sync_aggregation,
                    SyncIntLiteral = value.sync_int_literal,
                }
            );
        }
        return new ReadOnlyCollection<EquipmentAbilityStateSchemaDefinition>(result);
    }

    private static IReadOnlyList<EquipmentAbilityReactionDefinition> ProjectReactions(
        IReadOnlyList<EquipmentAbilityReactionImportModel> values
    )
    {
        var result = new List<EquipmentAbilityReactionDefinition>();
        foreach (EquipmentAbilityReactionImportModel value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentAbilityReactionDefinition
                {
                    ReactionId = value.reaction_id,
                    Trigger = TryParseTrigger(value.trigger, out var trigger)
                        ? trigger
                        : EquipmentAbilityTriggerKind.OnHit,
                    Timing = TryParseTiming(value.timing, out var timing)
                        ? timing
                        : EquipmentAbilityTimingKind.AfterHit,
                    Priority = value.priority,
                    OnceScope = value.once_scope,
                    RequiresPlayerConfirmation = value.requires_player_confirmation,
                    ConditionGroup = ProjectConditionGroup(value.condition_group),
                    RollGate = ProjectRollGate(value.roll_gate),
                    OutcomeTable = ProjectOutcomeTable(value.outcome_table),
                    ProjectedEffectCategories = CopyStringNames(value.projected_effect_categories),
                    Actions = ProjectActions(value.actions),
                }
            );
        }
        return new ReadOnlyCollection<EquipmentAbilityReactionDefinition>(result);
    }

    private static EquipmentConditionGroupDefinition ProjectConditionGroup(
        EquipmentAbilityConditionGroupImportModel value
    )
    {
        if (value == null)
            return null;
        var conditions = new List<EquipmentAbilityConditionDefinition>();
        foreach (EquipmentAbilityConditionImportModel condition in value.conditions)
        {
            if (condition == null)
                continue;
            conditions.Add(
                new EquipmentAbilityConditionDefinition
                {
                    ConditionId = condition.condition_id,
                    Kind = condition.kind,
                    PayloadDefinition = ProjectConditionPayload(condition.payload),
                }
            );
        }
        var groups = new List<EquipmentConditionGroupDefinition>();
        foreach (EquipmentAbilityConditionGroupImportModel group in value.groups)
        {
            if (group == null)
                continue;
            EquipmentConditionGroupDefinition projected = ProjectConditionGroup(group);
            if (projected != null)
                groups.Add(projected);
        }
        return new EquipmentConditionGroupDefinition
        {
            Mode = value.mode,
            Negate = value.negate,
            Conditions = new ReadOnlyCollection<EquipmentAbilityConditionDefinition>(conditions),
            Groups = new ReadOnlyCollection<EquipmentConditionGroupDefinition>(groups),
        };
    }

    private static EquipmentAbilityConditionPayloadDefinition ProjectConditionPayload(IEquipmentAbilityConditionPayloadImportModel payload)
    {
        return payload switch
        {
            HasStatusConditionPayloadImportModel status => new HasStatusConditionPayloadDefinition
            {
                Subject = status.subject,
                StatusId = status.status_id,
            },
            CompareFactConditionPayloadImportModel compare => new CompareFactConditionPayloadDefinition
            {
                Left = ProjectFactQuery(compare.left),
                Compare = compare.compare,
                Right = ProjectFactQuery(compare.right),
            },
            HasEquipmentTagConditionPayloadImportModel tags => new HasEquipmentTagConditionPayloadDefinition
            {
                Subject = tags.subject,
                EquipmentSelector = tags.equipment_selector,
                AllTags = CopyStringNames(tags.all_tags),
                AnyTags = CopyStringNames(tags.any_tags),
            },
            _ => null,
        };
    }

    private static IReadOnlyList<EquipmentAbilityActionDefinition> ProjectActions(
        IReadOnlyList<EquipmentAbilityActionImportModel> values
    )
    {
        var result = new List<EquipmentAbilityActionDefinition>();
        foreach (EquipmentAbilityActionImportModel value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentAbilityActionDefinition
                {
                    ActionId = value.action_id,
                    Kind = value.kind,
                    PayloadDefinition = ProjectActionPayload(value.payload),
                    ConditionGroup = ProjectConditionGroup(value.condition_group),
                    RollGate = ProjectRollGate(value.roll_gate),
                }
            );
        }
        return new ReadOnlyCollection<EquipmentAbilityActionDefinition>(result);
    }

    private static EquipmentAbilityActionPayloadDefinition ProjectActionPayload(IEquipmentAbilityActionPayloadImportModel payload)
    {
        return payload switch
        {
            AddDamageDiceActionPayloadImportModel damage => new AddDamageDiceActionPayloadDefinition
            {
                TargetSelector = damage.target_selector,
                Dice = ProjectDice(damage.dice),
                DamageType = damage.damage_type,
                DamageTypeMode = EquipmentAbilityDamageTypeModeContentRules.ToStringName(
                    EquipmentAbilityDamageTypeModeContentRules.ToKind(damage.damage_type_mode)
                ),
                RequireWeaponDamage = damage.require_weapon_damage,
                Subtract = damage.subtract,
                ReplacementGroupId = damage.replacement_group_id,
                ReplacementPriority = damage.replacement_priority,
                DamageTags = CopyStringNames(damage.damage_tags),
                MitigationBypassDamageTags = CopyStringNames(
                    damage.mitigation_bypass_damage_tags
                ),
                MitigationBypassTiers = CopyStringNames(damage.mitigation_bypass_tiers),
            },
            ImmediateWeaponAttackActionPayloadImportModel weaponAttack => new ImmediateWeaponAttackActionPayloadDefinition
            {
                AnchorSelector = weaponAttack.anchor_selector,
                TargetTeamFilter = weaponAttack.target_team_filter,
                Radius = Math.Max(weaponAttack.radius, 0),
                MaxAttacks = Math.Max(weaponAttack.max_attacks, 0),
                SkillId = weaponAttack.skill_id,
                RequireWeaponRange = weaponAttack.require_weapon_range,
            },
            DealDamageActionPayloadImportModel directDamage => new DealDamageActionPayloadDefinition
            {
                TargetSelector = directDamage.target_selector,
                Dice = ProjectDice(directDamage.dice),
                DamageType = directDamage.damage_type,
                DamageTags = CopyStringNames(directDamage.damage_tags),
                MitigationBypassDamageTags = CopyStringNames(
                    directDamage.mitigation_bypass_damage_tags
                ),
                MitigationBypassTiers = CopyStringNames(directDamage.mitigation_bypass_tiers),
            },
            HealActionPayloadImportModel heal => new HealActionPayloadDefinition
            {
                TargetSelector = heal.target_selector,
                Dice = ProjectDice(heal.dice),
            },
            HealFromFactActionPayloadImportModel healFromFact => new HealFromFactActionPayloadDefinition
            {
                TargetSelector = healFromFact.target_selector,
                AmountFact = ProjectFactQuery(healFromFact.amount_fact),
                MultiplierPercent = healFromFact.multiplier_percent,
                MaxAmount = healFromFact.max_amount,
            },
            AttackRollBonusActionPayloadImportModel attackRoll => new AttackRollBonusActionPayloadDefinition
            {
                TargetSelector = attackRoll.target_selector,
                Bonus = attackRoll.bonus,
                AttributeModifierId = attackRoll.attribute_modifier_id,
                StackMode = attackRoll.stack_mode,
                Label = attackRoll.label ?? "",
                RequireWeaponDamage = attackRoll.require_weapon_damage,
            },
            AttackRollAdvantageActionPayloadImportModel attackAdvantage => new AttackRollAdvantageActionPayloadDefinition
            {
                TargetSelector = attackAdvantage.target_selector,
                Mode = attackAdvantage.mode,
                StackMode = attackAdvantage.stack_mode,
                Label = attackAdvantage.label ?? "",
            },
            CriticalHitOverrideActionPayloadImportModel critical => new CriticalHitOverrideActionPayloadDefinition
            {
                TargetSelector = critical.target_selector,
                RequireWeaponDamage = critical.require_weapon_damage,
                Label = critical.label ?? "",
            },
            EquipmentAttackDefenseModifierImportModel defense => new EquipmentAttackDefenseModifierDefinition
            {
                ModifierId = defense.modifier_id,
                IgnoredAcComponents = CopyStringNames(defense.ignored_ac_components),
                AcComponentMultipliers = ProjectAcComponentMultipliers(defense.ac_component_multipliers),
                LockDodgeBonus = defense.lock_dodge_bonus,
                RequiredTargetEquipmentSelector = defense.required_target_equipment_selector,
                RequiredTargetItemTags = CopyStringNames(defense.required_target_item_tags),
                RequiredTargetEquipmentTypeIds = CopyStringNames(
                    defense.required_target_equipment_type_ids
                ),
                CoverPolicy = defense.cover_policy,
                ProjectileObstaclePolicy = defense.projectile_obstacle_policy,
                TraceLabel = defense.trace_label,
            },
            DamageRollModeOverrideActionPayloadImportModel damageRollMode =>
                new DamageRollModeOverrideActionPayloadDefinition
                {
                    TargetSelector = damageRollMode.target_selector,
                    RollMode = damageRollMode.roll_mode,
                    StackMode = damageRollMode.stack_mode,
                    Label = damageRollMode.label ?? "",
                },
            DamageReductionActionPayloadImportModel damageReduction =>
                new DamageReductionActionPayloadDefinition
                {
                    TargetSelector = damageReduction.target_selector,
                    Amount = damageReduction.amount,
                    DamageTags = CopyStringNames(damageReduction.damage_tags),
                    Label = damageReduction.label ?? "",
                },
            GrantMitigationTierActionPayloadImportModel grantMitigationTier =>
                new GrantMitigationTierActionPayloadDefinition
                {
                    TargetSelector = grantMitigationTier.target_selector,
                    MitigationTier = grantMitigationTier.mitigation_tier,
                    DamageTags = CopyStringNames(grantMitigationTier.damage_tags),
                    Label = grantMitigationTier.label ?? "",
                },
            LootQuantityMultiplierActionPayloadImportModel loot => new LootQuantityMultiplierActionPayloadDefinition
            {
                TargetSelector = loot.target_selector,
                MultiplierPercent = loot.multiplier_percent,
                AffectedDropKinds = CopyStringNames(loot.affected_drop_kinds),
                AnyItemTags = CopyStringNames(loot.any_item_tags),
            },
            ApplyStatusActionPayloadImportModel status => new ApplyStatusActionPayloadDefinition
            {
                TargetSelector = status.target_selector,
                StatusId = status.status_id,
                DurationTurns = status.duration_turns,
                DurationTu = status.duration_tu,
                StackDelta = status.stack_delta,
                StackBehavior = status.stack_behavior,
                StackLimit = status.stack_limit,
                DisplayLabel = status.display_label ?? "",
                AttackRollPenalty = status.attack_roll_penalty,
                ArmorClassBonusPerStack = status.armor_class_bonus_per_stack,
                SourceBoundAttackRollPenalty = status.source_bound_attack_roll_penalty,
                SourceBoundAttackRollPenaltyMinStacks =
                    status.source_bound_attack_roll_penalty_min_stacks,
                SourceBoundIncomingAttackRollBonusPerStack =
                    status.source_bound_incoming_attack_roll_bonus_per_stack,
                SourceBoundIncomingAttackRollBonusMinStacks =
                    status.source_bound_incoming_attack_roll_bonus_min_stacks,
                OverrideHealMultiplierPercent = status.override_heal_multiplier_percent,
                HealMultiplierPercent = status.heal_multiplier_percent,
                MovePointCapacityDelta = status.move_point_capacity_delta,
                ForcedMoveImmune = status.forced_move_immune,
                DamageTag = status.damage_tag,
                DamageTags = CopyStringNames(status.damage_tags),
                MitigationTier = status.mitigation_tier,
                CountsAsDebuffOverride = status.counts_as_debuff_override,
                CountsAsDebuff = status.counts_as_debuff,
                Undispellable = status.undispellable,
                DispellableMagic = status.dispellable_magic,
                DispellableHarmfulMagic = status.dispellable_harmful_magic,
                DispellableBeneficialMagic = status.dispellable_beneficial_magic,
                LockCounterattack = status.lock_counterattack,
                LockGuard = status.lock_guard,
                LockDodgeBonus = status.lock_dodge_bonus,
                TickIntervalTu = status.tick_interval_tu,
                TimelineDamageDiceCount = status.timeline_damage_dice_count,
                TimelineDamageDiceSides = status.timeline_damage_dice_sides,
                TimelineDamageFlatBonus = status.timeline_damage_flat_bonus,
                SaveDc = status.save_dc,
                SaveAbility = status.save_ability,
                SaveTag = status.save_tag,
                ApplyOnSaveFailure = status.apply_on_save_failure,
                RemoveOnSourceDeactivated = status.remove_on_source_deactivated,
            },
            ModifyActionPointsActionPayloadImportModel actionPoints => new ModifyActionPointsActionPayloadDefinition
            {
                TargetSelector = actionPoints.target_selector,
                Mode = actionPoints.mode,
                Amount = actionPoints.amount,
                StatusId = actionPoints.status_id,
                DisplayLabel = actionPoints.display_label ?? "",
            },
            ScheduleAreaEffectActionPayloadImportModel schedule => new ScheduleAreaEffectActionPayloadDefinition
            {
                AnchorSelector = schedule.anchor_selector,
                DelayTu = schedule.delay_tu,
                TerrainEffectId = schedule.terrain_effect_id,
                AreaPattern = schedule.area_pattern,
                AreaValue = schedule.area_value,
                LifetimePolicy = schedule.lifetime_policy,
                EffectType = schedule.effect_type,
                TargetTeamFilter = schedule.target_team_filter,
                StackBehavior = schedule.stack_behavior,
                DisplayName = schedule.display_name ?? "",
                RenderOverlayId = schedule.render_overlay_id,
                OverlayPriority = schedule.overlay_priority,
                ContactStatusId = schedule.contact_status_id,
                ContactStatusDurationTu = schedule.contact_status_duration_tu,
                ContactStackBehavior = schedule.contact_stack_behavior,
                ContactStackLimit = schedule.contact_stack_limit,
                ContactStatusDisplayLabel = schedule.contact_status_display_label ?? "",
                ContactCountsAsDebuffOverride = schedule.contact_counts_as_debuff_override,
                ContactCountsAsDebuff = schedule.contact_counts_as_debuff,
                ContactUndispellable = schedule.contact_undispellable,
                ContactDispellableMagic = schedule.contact_dispellable_magic,
                ContactDispellableHarmfulMagic = schedule.contact_dispellable_harmful_magic,
                ContactDispellableBeneficialMagic = schedule.contact_dispellable_beneficial_magic,
                ContactSaveDc = schedule.contact_save_dc,
                ContactSaveAbility = schedule.contact_save_ability,
                ContactSaveTag = schedule.contact_save_tag,
                ContactApplyOnSaveFailure = schedule.contact_apply_on_save_failure,
                ContactTickIntervalTu = schedule.contact_tick_interval_tu,
                ContactTimelineDamageDiceCount = schedule.contact_timeline_damage_dice_count,
                ContactTimelineDamageDiceSides = schedule.contact_timeline_damage_dice_sides,
                ContactTimelineDamageFlatBonus = schedule.contact_timeline_damage_flat_bonus,
                ContactBlockedByTraitId = schedule.contact_blocked_by_trait_id,
            },
            ApplyBattleTerrainEffectAfterCheckActionPayloadImportModel terrainCheck =>
                new ApplyBattleTerrainEffectAfterCheckActionPayloadDefinition
                {
                    AnchorSelector = terrainCheck.anchor_selector,
                    TerrainEffectId = terrainCheck.terrain_effect_id,
                    MoveCostDelta = terrainCheck.move_cost_delta,
                    TargetTeamFilter = terrainCheck.target_team_filter,
                    StackBehavior = terrainCheck.stack_behavior,
                    DisplayName = terrainCheck.display_name ?? "",
                    RenderOverlayId = terrainCheck.render_overlay_id,
                    OverlayPriority = terrainCheck.overlay_priority,
                    CheckAttributeModifierId = terrainCheck.check_attribute_modifier_id,
                    CheckCompare = terrainCheck.check_compare,
                    CheckThreshold = terrainCheck.check_threshold,
                    NaturalTwentyAutoSuccess = terrainCheck.natural_twenty_auto_success,
                    NaturalOneAutoFailure = terrainCheck.natural_one_auto_failure,
                },
            ApplyEdgeFeatureActionPayloadImportModel edgeFeature => new ApplyEdgeFeatureActionPayloadDefinition
            {
                FromSelector = edgeFeature.from_selector,
                ToSelector = edgeFeature.to_selector,
                DurationTu = edgeFeature.duration_tu,
                MaxActiveEdges = edgeFeature.max_active_edges,
                RefreshExisting = edgeFeature.refresh_existing,
                RequireAdjacent = edgeFeature.require_adjacent,
                FeatureKind = edgeFeature.feature_kind,
                RenderKind = edgeFeature.render_kind,
                RenderLayers = edgeFeature.render_layers,
                BlocksMove = edgeFeature.blocks_move,
                BlocksOccupancy = edgeFeature.blocks_occupancy,
                BlocksLos = edgeFeature.blocks_los,
                InteractionKind = edgeFeature.interaction_kind,
                StateTag = edgeFeature.state_tag,
            },
            ModifyAbilityStateActionPayloadImportModel state => new ModifyAbilityStateActionPayloadDefinition
            {
                TargetSelector = state.target_selector,
                BindingId = state.binding_id,
                StateKey = state.state_key,
                Operation = state.operation,
                IntDelta = state.int_delta,
            },
            MarkTargetActionPayloadImportModel mark => new MarkTargetActionPayloadDefinition
            {
                TargetSelector = mark.target_selector,
                StateKey = mark.state_key,
                StackDelta = mark.stack_delta,
                RemoveOnSourceMissing = mark.remove_on_source_missing,
                RemoveOnTargetDefeated = mark.remove_on_target_defeated,
                UniquePerSource = mark.unique_per_source,
                MirrorStatusId = mark.mirror_status_id,
                MirrorStatusDurationTu = mark.mirror_status_duration_tu,
                MirrorStatusStackBehavior = mark.mirror_status_stack_behavior,
                MirrorStatusStackLimit = mark.mirror_status_stack_limit,
                MirrorStatusDisplayLabel = mark.mirror_status_display_label ?? "",
                ClearStatusIdsOnReplace = CopyStringNames(mark.clear_status_ids_on_replace),
            },
            ClearStatusActionPayloadImportModel clear => new ClearStatusActionPayloadDefinition
            {
                TargetSelector = clear.target_selector,
                StatusId = clear.status_id,
                MarkBindingId = clear.mark_binding_id,
                MarkStateKey = clear.mark_state_key,
                RequireSourceUnitMatch = clear.require_source_unit_match,
                ClearTargetMark = clear.clear_target_mark,
            },
            TriggerSkillActionPayloadImportModel triggerSkill => new TriggerSkillActionPayloadDefinition
            {
                SkillId = triggerSkill.skill_id,
                SkillLevel = Math.Max(triggerSkill.skill_level, 1),
                TargetSelector = triggerSkill.target_selector,
                MergeIntoParentResult = triggerSkill.merge_into_parent_result,
                HandleTargetDefeat = triggerSkill.handle_target_defeat,
                ActivationLog = triggerSkill.activation_log ?? "",
                SaveLogLabel = triggerSkill.save_log_label ?? "",
            },
            SummonUnitsActionPayloadImportModel summon => new SummonUnitsActionPayloadDefinition
            {
                AnchorSelector = summon.anchor_selector,
                StateKey = summon.state_key,
                CountDice = ProjectDice(summon.count_dice),
                MaxLivingUnits = summon.max_living_units,
                DurationTu = summon.duration_tu,
                SpawnRadius = summon.spawn_radius,
                UnitIdPrefix = summon.unit_id_prefix,
                UnitDisplayName = summon.unit_display_name ?? "",
                BodySizeCategory = summon.body_size_category,
                ControlMode = summon.control_mode,
                AiBrainId = summon.ai_brain_id,
                AiStateId = summon.ai_state_id,
                CognitionKind =
                    BattleCognitionContentRules.ToKind(
                        summon.cognition_kind
                    ),
                HpMax = summon.hp_max,
                ArmorClass = summon.armor_class,
                AttackBonus = summon.attack_bonus,
                BaseAttackBonus = summon.base_attack_bonus,
                ActionPoints = summon.action_points,
                MovePoints = summon.move_points,
                KnownActiveSkillIds = CopyStringNames(summon.known_active_skill_ids),
                NaturalWeaponProfileTypeId = summon.natural_weapon_profile_type_id,
                NaturalWeaponDamageTag = summon.natural_weapon_damage_tag,
                NaturalWeaponAttackRange = summon.natural_weapon_attack_range,
                NaturalWeaponDamageDice = ProjectDice(summon.natural_weapon_damage_dice),
                NaturalWeaponFamily = summon.natural_weapon_family,
                CreatureTypeTags = CopyStringNames(summon.creature_type_tags),
                MovementTags = CopyStringNames(summon.movement_tags),
            },
            ConsumeSummonedUnitsActionPayloadImportModel consume =>
                new ConsumeSummonedUnitsActionPayloadDefinition
                {
                    SourceBindingId = consume.source_binding_id,
                    StateKey = consume.state_key,
                    Count = consume.count,
                    SelectionMode = consume.selection_mode,
                },
            ConsumeStatusStacksActionPayloadImportModel consumeStacks =>
                new ConsumeStatusStacksActionPayloadDefinition
                {
                    TargetSelector = consumeStacks.target_selector,
                    StatusId = consumeStacks.status_id,
                    Count = consumeStacks.count,
                    RequireSourceUnitMatch = consumeStacks.require_source_unit_match,
                    SelectionMode = consumeStacks.selection_mode,
                },
            SummonedUnitAttackRollModifierActionPayloadImportModel summonedModifier =>
                new SummonedUnitAttackRollModifierActionPayloadDefinition
                {
                    TargetSelector = summonedModifier.target_selector,
                    SourceBindingId = summonedModifier.source_binding_id,
                    StateKey = summonedModifier.state_key,
                    Radius = summonedModifier.radius,
                    BonusPerUnit = summonedModifier.bonus_per_unit,
                    MaxAbsoluteBonus = summonedModifier.max_absolute_bonus,
                    MinUnits = summonedModifier.min_units,
                    StackMode = summonedModifier.stack_mode,
                    Label = summonedModifier.label ?? "",
                },
            EquipmentDurabilityDamageActionPayloadImportModel durability =>
                new EquipmentDurabilityDamageActionPayloadDefinition
                {
                    TargetSelector = durability.target_selector,
                    TargetSlots = CopyStringNames(durability.target_slots),
                    SlotWeights = ProjectSlotWeights(durability.slot_weights),
                    RequiredItemTags = CopyStringNames(durability.required_item_tags),
                    RequiredEquipmentTypeIds = CopyStringNames(durability.required_equipment_type_ids),
                    DurabilityLoss = durability.durability_loss,
                    SaveTag = durability.save_tag,
                    SaveDc = durability.save_dc,
                    RequireAttackSuccess = durability.require_attack_success,
                    MaxDamagedItems = durability.max_damaged_items,
                    MaxTargetRarity = durability.max_target_rarity,
                },
            _ => null,
        };
    }

    private static IReadOnlyList<EquipmentAcComponentMultiplierDefinition> ProjectAcComponentMultipliers(
        IReadOnlyList<EquipmentAcComponentMultiplierImportModel> values
    )
    {
        if (values == null || values.Count == 0)
            return Array.Empty<EquipmentAcComponentMultiplierDefinition>();
        var result = new List<EquipmentAcComponentMultiplierDefinition>();
        foreach (EquipmentAcComponentMultiplierImportModel value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentAcComponentMultiplierDefinition
                {
                    AcComponentId = value.ac_component_id,
                    MultiplierPercent = value.multiplier_percent,
                    StackMode = value.stack_mode,
                }
            );
        }
        return result.Count > 0
            ? new ReadOnlyCollection<EquipmentAcComponentMultiplierDefinition>(result)
            : Array.Empty<EquipmentAcComponentMultiplierDefinition>();
    }

    private static DiceExpressionDefinition ProjectDice(DiceExpressionImportModel value)
    {
        if (value == null)
            return null;
        var terms = new List<DiceExpressionTermDefinition>();
        foreach (DiceExpressionTermImportModel term in value.terms)
        {
            if (term == null)
                continue;
            terms.Add(
                new DiceExpressionTermDefinition
                {
                    DiceCount = term.dice_count,
                    DiceSides = term.dice_sides,
                    CountBonusFact = ProjectFactQuery(term.count_bonus_fact),
                    CountBonusMultiplier = term.count_bonus_multiplier,
                    MaxDiceCount = term.max_dice_count,
                }
            );
        }
        return new DiceExpressionDefinition
        {
            Terms = new ReadOnlyCollection<DiceExpressionTermDefinition>(terms),
            FlatBonus = value.flat_bonus,
            PreviewPolicy = value.preview_policy,
        };
    }

    private static EquipmentAbilityFactQueryDefinition ProjectFactQuery(EquipmentAbilityFactQueryImportModel value)
    {
        if (value == null)
            return null;
        return new EquipmentAbilityFactQueryDefinition
        {
            QueryKind = value.query_kind,
            FactId = value.fact_id,
            Subject = value.subject,
            BindingId = value.binding_id,
            StateKey = value.state_key,
            StatusId = value.status_id,
            RequireSourceUnitMatch = value.require_source_unit_match,
            AttributeId = value.attribute_id,
            Aggregation = value.aggregation,
            ValueKind = value.value_kind,
            BoolLiteral = value.bool_literal,
            IntLiteral = value.int_literal,
            FloatLiteral = value.float_literal,
            StringNameLiteral = value.string_name_literal,
        };
    }

    private static EquipmentRollGateDefinition ProjectRollGate(EquipmentRollGateImportModel value)
    {
        return value == null
            ? null
            : new EquipmentRollGateDefinition
            {
                RngStream = value.rng_stream,
                Roll = ProjectDice(value.roll),
                Compare = value.compare,
                Threshold = value.threshold,
            };
    }

    private static EquipmentOutcomeTableDefinition ProjectOutcomeTable(EquipmentOutcomeTableImportModel value)
    {
        if (value == null)
            return null;
        var entries = new List<EquipmentOutcomeEntryDefinition>();
        foreach (EquipmentOutcomeEntryImportModel entry in value.entries)
        {
            if (entry == null)
                continue;
            entries.Add(
                new EquipmentOutcomeEntryDefinition
                {
                    MinRoll = entry.min_roll,
                    MaxRoll = entry.max_roll,
                    Actions = ProjectActions(entry.actions),
                }
            );
        }
        return new EquipmentOutcomeTableDefinition
        {
            TableId = value.table_id,
            Roll = ProjectDice(value.roll),
            Entries = new ReadOnlyCollection<EquipmentOutcomeEntryDefinition>(entries),
        };
    }

    private static IReadOnlyList<EquipmentGrantedActionDefinition> ProjectGrantedActions(
        IReadOnlyList<EquipmentGrantedActionImportModel> values
    )
    {
        var result = new List<EquipmentGrantedActionDefinition>();
        foreach (EquipmentGrantedActionImportModel value in values)
        {
            if (value == null)
                continue;
            EquipmentAbilityUsagePeriodKinds.TryParse(
                value.usage_period_kind,
                out EquipmentAbilityUsagePeriodKind usagePeriodKind
            );
            result.Add(
                new EquipmentGrantedActionDefinition
                {
                    GrantedActionId = value.granted_action_id,
                    GrantedKind = TryParseGrantedKind(value.granted_kind, out var grantedKind)
                        ? grantedKind
                        : EquipmentGrantedActionKind.Skill,
                    SkillId = value.skill_id,
                    SkillLevel = value.skill_level,
                    UsagePeriodKind = usagePeriodKind,
                    MaxUsesPerPeriod = value.max_uses_per_period,
                    DisplayCategory = value.display_category,
                    DisplayPriority = value.display_priority,
                    AvailabilityConditions = ProjectConditionGroup(value.availability_conditions),
                }
            );
        }
        return new ReadOnlyCollection<EquipmentGrantedActionDefinition>(result);
    }

    private static IReadOnlyList<EquipmentWeaponProfileOverlayDefinition> ProjectWeaponProfileOverlays(
        IReadOnlyList<EquipmentWeaponProfileOverlayImportModel> values
    )
    {
        var result = new List<EquipmentWeaponProfileOverlayDefinition>();
        foreach (EquipmentWeaponProfileOverlayImportModel value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentWeaponProfileOverlayDefinition
                {
                    OverlayId = value.overlay_id,
                    Priority = value.priority,
                    ConditionGroup = ProjectConditionGroup(value.condition_group),
                    RequireEquippedWeapon = value.require_equipped_weapon,
                    RequiredWeaponFamilies = CopyStringNameSet(value.required_weapon_families),
                    RequiredWeaponTypeIds = CopyStringNameSet(value.required_weapon_type_ids),
                    AttackRangeDelta = value.attack_range_delta,
                    MinAttackRange = value.min_attack_range,
                    MaxAttackRange = value.max_attack_range,
                    OneHandedDiceOverlay = ProjectWeaponDiceOverlay(value.one_handed_dice_overlay),
                    TwoHandedDiceOverlay = ProjectWeaponDiceOverlay(value.two_handed_dice_overlay),
                    PhysicalDamageTagOverride = value.physical_damage_tag_override,
                    GripOverride = value.grip_override,
                    UsesTwoHandsOverride = value.uses_two_hands_override,
                    IsVersatileOverride = value.is_versatile_override,
                }
            );
        }
        return new ReadOnlyCollection<EquipmentWeaponProfileOverlayDefinition>(result);
    }

    private static EquipmentWeaponDiceOverlayDefinition ProjectWeaponDiceOverlay(
        EquipmentWeaponDiceOverlayImportModel value
    )
    {
        return value == null
            ? null
            : new EquipmentWeaponDiceOverlayDefinition
            {
                Mode = TryParseWeaponDiceOverlayMode(value.mode, out var mode)
                    ? mode
                    : EquipmentWeaponDiceOverlayModeKind.None,
                DiceCountDelta = value.dice_count_delta,
                DiceSidesOverride = value.dice_sides_override,
                FlatBonusDelta = value.flat_bonus_delta,
                DiceOverride = ProjectDice(value.dice_override),
            };
    }

    internal static bool TryParseWeaponDiceOverlayMode(
        StringName value,
        out EquipmentWeaponDiceOverlayModeKind mode
    )
    {
        if (value == "" || value == "none")
        {
            mode = EquipmentWeaponDiceOverlayModeKind.None;
            return true;
        }
        if (value == "add")
        {
            mode = EquipmentWeaponDiceOverlayModeKind.Add;
            return true;
        }
        if (value == "override")
        {
            mode = EquipmentWeaponDiceOverlayModeKind.Override;
            return true;
        }
        mode = EquipmentWeaponDiceOverlayModeKind.None;
        return false;
    }

    private static IReadOnlyList<EquipmentWorldEffectDefinition> ProjectWorldEffects(
        IReadOnlyList<EquipmentWorldEffectImportModel> values
    )
    {
        var result = new List<EquipmentWorldEffectDefinition>();
        foreach (EquipmentWorldEffectImportModel value in values)
        {
            if (value == null)
                continue;
            result.Add(
                new EquipmentWorldEffectDefinition
                {
                    WorldEffectId = value.world_effect_id,
                    Trigger = TryParseTrigger(value.trigger, out var trigger)
                        ? trigger
                        : EquipmentAbilityTriggerKind.OnHit,
                    Timing = TryParseTiming(value.timing, out var timing)
                        ? timing
                        : EquipmentAbilityTimingKind.AfterHit,
                    ConditionGroup = ProjectConditionGroup(value.condition_group),
                    Actions = ProjectActions(value.actions),
                }
            );
        }
        return new ReadOnlyCollection<EquipmentWorldEffectDefinition>(result);
    }

    private static IReadOnlyList<StringName> CopyStringNames(
        IReadOnlyList<string> values
    )
    {
        if (values == null || values.Count == 0)
            return Array.Empty<StringName>();
        var result = new List<StringName>();
        foreach (string value in values)
        {
            if (value != "")
                result.Add(value);
        }
        return result.Count > 0 ? new ReadOnlyCollection<StringName>(result) : Array.Empty<StringName>();
    }

    private static IReadOnlyList<EquipmentSlotWeightDefinition> ProjectSlotWeights(
        IReadOnlyList<EquipmentSlotWeightImportModel> values
    )
    {
        if (values == null || values.Count == 0)
            return Array.Empty<EquipmentSlotWeightDefinition>();
        var result = new List<EquipmentSlotWeightDefinition>();
        HashSet<StringName> seen = new();
        foreach (EquipmentSlotWeightImportModel value in values)
        {
            if (
                value == null
                || value.slot_id == ""
                || value.weight <= 0
                || !seen.Add(value.slot_id)
            )
            {
                continue;
            }
            result.Add(
                new EquipmentSlotWeightDefinition
                {
                    SlotId = value.slot_id,
                    Weight = value.weight,
                }
            );
        }
        return result.Count > 0
            ? new ReadOnlyCollection<EquipmentSlotWeightDefinition>(result)
            : Array.Empty<EquipmentSlotWeightDefinition>();
    }

    private static IReadOnlySet<StringName> CopyStringNameSet(
        IReadOnlyList<string> values
    )
    {
        var result = new HashSet<StringName>();
        if (values == null)
            return EquipmentAbilityReadOnlySet<StringName>.Empty;
        foreach (string value in values)
        {
            if (value != "")
                result.Add(value);
        }
        return EquipmentAbilityReadOnlySet<StringName>.From(result);
    }

    internal static bool TryParseOverrideMode(
        StringName value,
        out EquipmentAbilityBindingOverrideMode mode
    )
    {
        if (value == "" || value == "add")
        {
            mode = EquipmentAbilityBindingOverrideMode.Add;
            return true;
        }
        if (value == "replace_binding")
        {
            mode = EquipmentAbilityBindingOverrideMode.ReplaceBinding;
            return true;
        }
        mode = EquipmentAbilityBindingOverrideMode.Add;
        return false;
    }

    internal static bool TryParseTrigger(StringName value, out EquipmentAbilityTriggerKind trigger)
    {
        if (value == "on_hit")
        {
            trigger = EquipmentAbilityTriggerKind.OnHit;
            return true;
        }
        if (value == "on_attack_hit")
        {
            trigger = EquipmentAbilityTriggerKind.OnAttackHit;
            return true;
        }
        if (value == "on_kill")
        {
            trigger = EquipmentAbilityTriggerKind.OnKill;
            return true;
        }
        if (value == "on_granted_skill_used")
        {
            trigger = EquipmentAbilityTriggerKind.OnGrantedSkillUsed;
            return true;
        }
        if (value == "on_turn_end")
        {
            trigger = EquipmentAbilityTriggerKind.OnTurnEnd;
            return true;
        }
        if (value == "on_damage_roll")
        {
            trigger = EquipmentAbilityTriggerKind.OnDamageRoll;
            return true;
        }
        if (value == "on_damage_applied")
        {
            trigger = EquipmentAbilityTriggerKind.OnDamageApplied;
            return true;
        }
        if (value == "on_damage_taken_finalized")
        {
            trigger = EquipmentAbilityTriggerKind.OnDamageTakenFinalized;
            return true;
        }
        if (value == "on_hit_received")
        {
            trigger = EquipmentAbilityTriggerKind.OnHitReceived;
            return true;
        }
        if (value == "on_attack_check")
        {
            trigger = EquipmentAbilityTriggerKind.OnAttackCheck;
            return true;
        }
        if (value == "on_target_mark_expired")
        {
            trigger = EquipmentAbilityTriggerKind.OnTargetMarkExpired;
            return true;
        }
        trigger = EquipmentAbilityTriggerKind.OnHit;
        return false;
    }

    internal static bool TryParseTiming(StringName value, out EquipmentAbilityTimingKind timing)
    {
        if (value == "before_hit")
        {
            timing = EquipmentAbilityTimingKind.BeforeHit;
            return true;
        }
        if (value == "" || value == "after_hit")
        {
            timing = EquipmentAbilityTimingKind.AfterHit;
            return true;
        }
        if (value == "after_kill")
        {
            timing = EquipmentAbilityTimingKind.AfterKill;
            return true;
        }
        if (value == "after_skill")
        {
            timing = EquipmentAbilityTimingKind.AfterSkill;
            return true;
        }
        if (value == "after_turn")
        {
            timing = EquipmentAbilityTimingKind.AfterTurn;
            return true;
        }
        if (value == "before_damage")
        {
            timing = EquipmentAbilityTimingKind.BeforeDamage;
            return true;
        }
        if (value == "after_damage")
        {
            timing = EquipmentAbilityTimingKind.AfterDamage;
            return true;
        }
        if (value == "after_hit_received")
        {
            timing = EquipmentAbilityTimingKind.AfterHitReceived;
            return true;
        }
        if (value == "after_attack_check")
        {
            timing = EquipmentAbilityTimingKind.AfterAttackCheck;
            return true;
        }
        if (value == "after_status_expired")
        {
            timing = EquipmentAbilityTimingKind.AfterStatusExpired;
            return true;
        }
        timing = EquipmentAbilityTimingKind.AfterHit;
        return false;
    }

    internal static bool TryParseGrantedKind(
        StringName value,
        out EquipmentGrantedActionKind grantedKind
    )
    {
        if (value == "skill")
        {
            grantedKind = EquipmentGrantedActionKind.Skill;
            return true;
        }
        grantedKind = EquipmentGrantedActionKind.Skill;
        return false;
    }

    internal static bool TryParseFatalInterceptRecoveryKind(
        StringName value,
        out EquipmentFatalInterceptRecoveryKind recoveryKind
    )
    {
        StringName normalized = ProgressionDataUtils.to_string_name(value);
        if (normalized == "hp_dice")
        {
            recoveryKind = EquipmentFatalInterceptRecoveryKind.HpDice;
            return true;
        }
        if (normalized == "max_hp_percent")
        {
            recoveryKind = EquipmentFatalInterceptRecoveryKind.MaxHpPercent;
            return true;
        }
        recoveryKind = EquipmentFatalInterceptRecoveryKind.HpDice;
        return false;
    }
}
