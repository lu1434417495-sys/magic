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
                    typeof(HasStatusConditionPayloadDef),
                    typeof(HasStatusConditionPayloadDefinition)
                ),
                ["compare_fact"] = Condition(
                    "compare_fact",
                    typeof(CompareFactConditionPayloadDef),
                    typeof(CompareFactConditionPayloadDefinition)
                ),
                ["has_equipment_tag"] = Condition(
                    "has_equipment_tag",
                    typeof(HasEquipmentTagConditionPayloadDef),
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
                    typeof(AddDamageDiceActionPayloadDef),
                    typeof(AddDamageDiceActionPayloadDefinition)
                ),
                ["attack_roll_bonus"] = Action(
                    "attack_roll_bonus",
                    typeof(AttackRollBonusActionPayloadDef),
                    typeof(AttackRollBonusActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["attack_roll_advantage"] = Action(
                    "attack_roll_advantage",
                    typeof(AttackRollAdvantageActionPayloadDef),
                    typeof(AttackRollAdvantageActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["damage_roll_mode_override"] = Action(
                    "damage_roll_mode_override",
                    typeof(DamageRollModeOverrideActionPayloadDef),
                    typeof(DamageRollModeOverrideActionPayloadDefinition),
                    consumerSupport: ConsumerSupport(includePreview: true)
                ),
                ["loot_quantity_multiplier"] = Action(
                    "loot_quantity_multiplier",
                    typeof(LootQuantityMultiplierActionPayloadDef),
                    typeof(LootQuantityMultiplierActionPayloadDefinition)
                ),
                ["apply_status"] = Action(
                    "apply_status",
                    typeof(ApplyStatusActionPayloadDef),
                    typeof(ApplyStatusActionPayloadDefinition)
                ),
                ["schedule_area_effect"] = Action(
                    "schedule_area_effect",
                    typeof(ScheduleAreaEffectActionPayloadDef),
                    typeof(ScheduleAreaEffectActionPayloadDefinition)
                ),
                ["modify_ability_state"] = Action(
                    "modify_ability_state",
                    typeof(ModifyAbilityStateActionPayloadDef),
                    typeof(ModifyAbilityStateActionPayloadDefinition),
                    stateAccess: WritesDeclaredBindingState()
                ),
                ["mark_target"] = Action(
                    "mark_target",
                    typeof(MarkTargetActionPayloadDef),
                    typeof(MarkTargetActionPayloadDefinition),
                    stateAccess: WritesDeclaredTargetMark()
                ),
                ["grant_skill"] = Action(
                    "grant_skill",
                    typeof(GrantSkillActionPayloadDef),
                    typeof(GrantSkillActionPayloadDefinition)
                ),
                ["equipment_durability_damage"] = Action(
                    "equipment_durability_damage",
                    typeof(EquipmentDurabilityDamageActionPayloadDef),
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
                [EquipmentAbilityTriggerKind.OnBattleEnd] = new()
                {
                    Trigger = EquipmentAbilityTriggerKind.OnBattleEnd,
                    AllowedTimings = EquipmentAbilityReadOnlySet<EquipmentAbilityTimingKind>.From(
                        new[] { EquipmentAbilityTimingKind.AfterBattle }
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
            }
        );
    }

    private static EquipmentAbilityHandlerSpec Condition(
        StringName handlerId,
        System.Type payloadResourceType,
        System.Type payloadDefinitionType
    )
    {
        return new EquipmentAbilityHandlerSpec
        {
            HandlerId = handlerId,
            HandlerKind = EquipmentAbilityHandlerKind.Condition,
            Origin = EquipmentAbilityHandlerOriginKind.Builtin,
            PayloadResourceType = payloadResourceType,
            PayloadDefinitionType = payloadDefinitionType,
            MutationPolicy = EquipmentAbilityMutationPolicyKind.None,
            ConsumerSupport = ConsumerSupport(includePreview: true),
            StateAccess = EquipmentAbilityStateAccessSpec.Empty,
        };
    }

    private static EquipmentAbilityHandlerSpec Action(
        StringName handlerId,
        System.Type payloadResourceType,
        System.Type payloadDefinitionType,
        EquipmentAbilityStateAccessSpec stateAccess = null,
        IReadOnlyList<EquipmentAbilityConsumerSupportSpec> consumerSupport = null
    )
    {
        return new EquipmentAbilityHandlerSpec
        {
            HandlerId = handlerId,
            HandlerKind = EquipmentAbilityHandlerKind.Action,
            Origin = EquipmentAbilityHandlerOriginKind.Builtin,
            PayloadResourceType = payloadResourceType,
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
