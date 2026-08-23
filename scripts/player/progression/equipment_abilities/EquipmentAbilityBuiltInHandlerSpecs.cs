using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

internal static class EquipmentAbilityBuiltInHandlerSpecs
{
    internal static IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> BuildConditionSpecs()
    {
        return ReadOnly(
            new Dictionary<StringName, EquipmentAbilityHandlerSpec>
            {
                ["has_status"] = Condition(
                    "has_status",
                    typeof(HasStatusConditionPayloadDefinition)
                ),
                ["compare_fact"] = Condition(
                    "compare_fact",
                    typeof(CompareFactConditionPayloadDefinition)
                ),
                ["has_equipment_tag"] = Condition(
                    "has_equipment_tag",
                    typeof(HasEquipmentTagConditionPayloadDefinition)
                ),
            }
        );
    }

    internal static IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> BuildActionSpecs()
    {
        return ReadOnly(
            new Dictionary<StringName, EquipmentAbilityHandlerSpec>
            {
                ["add_damage_dice"] = Action(
                    "add_damage_dice",
                    typeof(AddDamageDiceActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["immediate_weapon_attack"] = Action(
                    "immediate_weapon_attack",
                    typeof(ImmediateWeaponAttackActionPayloadDefinition)
                ),
                ["deal_damage"] = Action(
                    "deal_damage",
                    typeof(DealDamageActionPayloadDefinition)
                ),
                ["heal"] = Action(
                    "heal",
                    typeof(HealActionPayloadDefinition)
                ),
                ["heal_from_fact"] = Action(
                    "heal_from_fact",
                    typeof(HealFromFactActionPayloadDefinition)
                ),
                ["attack_roll_bonus"] = Action(
                    "attack_roll_bonus",
                    typeof(AttackRollBonusActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["attack_roll_advantage"] = Action(
                    "attack_roll_advantage",
                    typeof(AttackRollAdvantageActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["critical_hit_override"] = Action(
                    "critical_hit_override",
                    typeof(CriticalHitOverrideActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["attack_defense_modifier"] = Action(
                    "attack_defense_modifier",
                    typeof(EquipmentAttackDefenseModifierDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["damage_roll_mode_override"] = Action(
                    "damage_roll_mode_override",
                    typeof(DamageRollModeOverrideActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["damage_reduction"] = Action(
                    "damage_reduction",
                    typeof(DamageReductionActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["grant_mitigation_tier"] = Action(
                    "grant_mitigation_tier",
                    typeof(GrantMitigationTierActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["loot_quantity_multiplier"] = Action(
                    "loot_quantity_multiplier",
                    typeof(LootQuantityMultiplierActionPayloadDefinition)
                ),
                ["apply_status"] = Action(
                    "apply_status",
                    typeof(ApplyStatusActionPayloadDefinition)
                ),
                ["modify_action_points"] = Action(
                    "modify_action_points",
                    typeof(ModifyActionPointsActionPayloadDefinition)
                ),
                ["schedule_area_effect"] = Action(
                    "schedule_area_effect",
                    typeof(ScheduleAreaEffectActionPayloadDefinition)
                ),
                ["apply_battle_terrain_effect_after_check"] = Action(
                    "apply_battle_terrain_effect_after_check",
                    typeof(ApplyBattleTerrainEffectAfterCheckActionPayloadDefinition)
                ),
                ["apply_edge_feature"] = Action(
                    "apply_edge_feature",
                    typeof(ApplyEdgeFeatureActionPayloadDefinition)
                ),
                ["modify_ability_state"] = Action(
                    "modify_ability_state",
                    typeof(ModifyAbilityStateActionPayloadDefinition),
                    stateAccess: WritesDeclaredBindingState()
                ),
                ["mark_target"] = Action(
                    "mark_target",
                    typeof(MarkTargetActionPayloadDefinition),
                    stateAccess: WritesDeclaredTargetMark()
                ),
                ["clear_status"] = Action(
                    "clear_status",
                    typeof(ClearStatusActionPayloadDefinition)
                ),
                ["trigger_skill"] = Action(
                    "trigger_skill",
                    typeof(TriggerSkillActionPayloadDefinition)
                ),
                ["summon_units"] = Action(
                    "summon_units",
                    typeof(SummonUnitsActionPayloadDefinition)
                ),
                ["consume_summoned_units"] = Action(
                    "consume_summoned_units",
                    typeof(ConsumeSummonedUnitsActionPayloadDefinition)
                ),
                ["consume_status_stacks"] = Action(
                    "consume_status_stacks",
                    typeof(ConsumeStatusStacksActionPayloadDefinition)
                ),
                ["summoned_unit_attack_roll_modifier"] = Action(
                    "summoned_unit_attack_roll_modifier",
                    typeof(SummonedUnitAttackRollModifierActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["equipment_durability_damage"] = Action(
                    "equipment_durability_damage",
                    typeof(EquipmentDurabilityDamageActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
            }
        );
    }

    internal static IReadOnlyDictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec> BuildTriggerTimingSpecs()
    {
        return ReadOnly(
            new Dictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec>
            {
                [EquipmentAbilityTriggerKind.OnHit] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnHit,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[]
                        {
                            EquipmentAbilityTimingKind.BeforeHit,
                            EquipmentAbilityTimingKind.AfterHit,
                        }
                    ),
                },
                [EquipmentAbilityTriggerKind.OnKill] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnKill,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[] { EquipmentAbilityTimingKind.AfterKill }
                    ),
                },
                [EquipmentAbilityTriggerKind.OnGrantedSkillUsed] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnGrantedSkillUsed,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[] { EquipmentAbilityTimingKind.AfterSkill }
                    ),
                },
                [EquipmentAbilityTriggerKind.OnTurnEnd] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnTurnEnd,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[] { EquipmentAbilityTimingKind.AfterTurn }
                    ),
                },
                [EquipmentAbilityTriggerKind.OnDamageRoll] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnDamageRoll,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[] { EquipmentAbilityTimingKind.BeforeDamage }
                    ),
                },
                [EquipmentAbilityTriggerKind.OnDamageApplied] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnDamageApplied,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[] { EquipmentAbilityTimingKind.AfterDamage }
                    ),
                },
                [EquipmentAbilityTriggerKind.OnDamageTakenFinalized] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnDamageTakenFinalized,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[] { EquipmentAbilityTimingKind.AfterDamage }
                    ),
                },
                [EquipmentAbilityTriggerKind.OnHitReceived] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnHitReceived,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[] { EquipmentAbilityTimingKind.AfterHitReceived }
                    ),
                },
                [EquipmentAbilityTriggerKind.OnAttackCheck] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnAttackCheck,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[] { EquipmentAbilityTimingKind.AfterAttackCheck }
                    ),
                },
                [EquipmentAbilityTriggerKind.OnAttackHit] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnAttackHit,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[] { EquipmentAbilityTimingKind.AfterHit }
                    ),
                },
                [EquipmentAbilityTriggerKind.OnTargetMarkExpired] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnTargetMarkExpired,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[] { EquipmentAbilityTimingKind.AfterStatusExpired }
                    ),
                },
            }
        );
    }

    private static EquipmentAbilityHandlerSpec Condition(
        StringName handlerId,
        System.Type payloadDefinitionType
    )
    {
        if (
            !EquipmentAbilityPayloadKindCatalog.TryGetCondition(
                handlerId.ToString(),
                out EquipmentAbilityPayloadKindSpec payloadSpec
            )
        )
        {
            throw new InvalidOperationException($"Condition handler {handlerId} is not aligned with the canonical payload kind catalog.");
        }
        return new EquipmentAbilityHandlerSpec
        {
            HandlerId = handlerId,
            HandlerKind = EquipmentAbilityHandlerKind.Condition,
            Origin = EquipmentAbilityHandlerOriginKind.Builtin,
            PayloadJsonDtoType = payloadSpec.JsonDtoType,
            PayloadImportModelType = payloadSpec.ImportModelType,
            PayloadDefinitionType = payloadDefinitionType,
            MutationPolicy = EquipmentAbilityMutationPolicyKind.None,
            ConsumerSupport = ConsumerSupport(includePreview: true),
            StateAccess = EquipmentAbilityStateAccessSpec.Empty,
        };
    }

    private static EquipmentAbilityHandlerSpec Action(
        StringName handlerId,
        System.Type payloadDefinitionType,
        EquipmentAbilityStateAccessSpec stateAccess = null,
        IReadOnlyList<EquipmentAbilityConsumerSupportSpec> consumerSupport = null
    )
    {
        if (
            !EquipmentAbilityPayloadKindCatalog.TryGetAction(
                handlerId.ToString(),
                out EquipmentAbilityPayloadKindSpec payloadSpec
            )
        )
        {
            throw new InvalidOperationException($"Action handler {handlerId} is not aligned with the canonical payload kind catalog.");
        }
        return new EquipmentAbilityHandlerSpec
        {
            HandlerId = handlerId,
            HandlerKind = EquipmentAbilityHandlerKind.Action,
            Origin = EquipmentAbilityHandlerOriginKind.Builtin,
            PayloadJsonDtoType = payloadSpec.JsonDtoType,
            PayloadImportModelType = payloadSpec.ImportModelType,
            PayloadDefinitionType = payloadDefinitionType,
            MutationPolicy = EquipmentAbilityMutationPolicyKind.Mutating,
            ConsumerSupport = consumerSupport ?? ConsumerSupport(includePreview: false),
            StateAccess = stateAccess ?? EquipmentAbilityStateAccessSpec.Empty,
        };
    }

    private static EquipmentAbilityStateAccessSpec WritesDeclaredBindingState()
    {
        return new EquipmentAbilityStateAccessSpec
        {
            Writes = new[]
            {
                new EquipmentAbilityStateContract
                {
                    OwnerKind = EquipmentAbilityStateOwnerKind.BindingState,
                    ValueKind = EquipmentAbilityStateValueKind.Int,
                    LifetimeKind = EquipmentAbilityStateLifetimeKind.Battle,
                    StateKeyPayloadMemberName = "state_key",
                    StateKeyMustBeDeclaredInBinding = true,
                    SourceLifecycleCleanupRequired = true,
                },
            },
        };
    }

    private static EquipmentAbilityStateAccessSpec WritesDeclaredTargetMark()
    {
        return new EquipmentAbilityStateAccessSpec
        {
            Writes = new[]
            {
                new EquipmentAbilityStateContract
                {
                    OwnerKind = EquipmentAbilityStateOwnerKind.TargetMark,
                    ValueKind = EquipmentAbilityStateValueKind.Int,
                    LifetimeKind = EquipmentAbilityStateLifetimeKind.Battle,
                    StateKeyPayloadMemberName = "state_key",
                    StateKeyMustBeDeclaredInBinding = true,
                    SourceLifecycleCleanupRequired = true,
                },
            },
        };
    }

    private static IReadOnlyList<EquipmentAbilityConsumerSupportSpec> ConsumerSupport(
        bool includePreview
    )
    {
        var support = new List<EquipmentAbilityConsumerSupportSpec>
        {
            Support(
                EquipmentAbilityConsumerKind.Execution,
                EquipmentAbilityConsumerSupportKind.Exact
            ),
            Support(
                EquipmentAbilityConsumerKind.Trace,
                EquipmentAbilityConsumerSupportKind.TraceOnly
            ),
        };
        if (includePreview)
        {
            support.Add(
                Support(
                    EquipmentAbilityConsumerKind.Preview,
                    EquipmentAbilityConsumerSupportKind.Approximate,
                    EquipmentAbilityPreviewRollPolicyKind.ExpectedValue
                )
            );
            support.Add(
                Support(
                    EquipmentAbilityConsumerKind.AiScoring,
                    EquipmentAbilityConsumerSupportKind.Approximate,
                    EquipmentAbilityPreviewRollPolicyKind.ExpectedValue
                )
            );
            support.Add(
                Support(
                    EquipmentAbilityConsumerKind.Snapshot,
                    EquipmentAbilityConsumerSupportKind.TraceOnly
                )
            );
        }
        return new ReadOnlyCollection<EquipmentAbilityConsumerSupportSpec>(support);
    }

    private static EquipmentAbilityConsumerSupportSpec Support(
        EquipmentAbilityConsumerKind consumer,
        EquipmentAbilityConsumerSupportKind supportKind,
        EquipmentAbilityPreviewRollPolicyKind rollPolicy = EquipmentAbilityPreviewRollPolicyKind.None
    )
    {
        return new EquipmentAbilityConsumerSupportSpec
        {
            Consumer = consumer,
            SupportKind = supportKind,
            RollPolicy = rollPolicy,
            UnsupportedPolicy = supportKind == EquipmentAbilityConsumerSupportKind.UnsupportedBlocking
                ? EquipmentAbilityUnsupportedConsumerPolicyKind.RejectContent
                : EquipmentAbilityUnsupportedConsumerPolicyKind.Ignore,
        };
    }

    private static IReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec> ReadOnly(
        Dictionary<StringName, EquipmentAbilityHandlerSpec> source
    )
    {
        return new ReadOnlyDictionary<StringName, EquipmentAbilityHandlerSpec>(source);
    }

    private static IReadOnlyDictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec> ReadOnly(
        Dictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec> source
    )
    {
        return new ReadOnlyDictionary<
            EquipmentAbilityTriggerKind,
            EquipmentAbilityTriggerTimingSpec
        >(source);
    }
}
