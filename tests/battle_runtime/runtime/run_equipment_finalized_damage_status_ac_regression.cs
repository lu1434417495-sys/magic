using System;
using System.Collections.Generic;
using Godot;

public partial class run_equipment_finalized_damage_status_ac_regression
    : LifecycleTestSceneTree
{
    private static readonly StringName BindingId = "binding.test.finalized_damage_status_ac";
    private static readonly StringName BurningArmorStatusId = "test_burning_armor";
    private static readonly StringName RawOnlySentinelStatusId = "test_raw_only_sentinel";
    private static readonly StringName FireShieldStatusId = "test_fire_shield";
    private static readonly StringName TriggeredSkillId = "test_low_hp_fire_burst";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestApplyStatusArmorClassAuthoringProjection();
            using Fixture fixture = Fixture.Create();
            TestRawFireFactsAndOriginFilters(fixture);
            TestArmorClassBonusUsesAllActiveStatusStacks(fixture);
            TestLowHpCrossingTriggersSkillOnceAndPreviewDoesNotMutate(fixture);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(
            _test.Finish("Equipment finalized damage status AC regression")
        );
    }

    private void TestApplyStatusArmorClassAuthoringProjection()
    {
        using ApplyStatusActionPayloadDef payload = new()
        {
            target_selector = "source",
            status_id = BurningArmorStatusId,
            armor_class_bonus_per_stack = 3,
        };
        using EquipmentAbilityActionDef action = new()
        {
            action_id = "action.test.status_ac_projection",
            kind = "apply_status",
            payload = payload,
        };
        using EquipmentAbilityReactionDef reaction = new()
        {
            reaction_id = "reaction.test.status_ac_projection",
            trigger = "on_damage_taken_finalized",
            timing = "after_damage",
        };
        reaction.actions.Add(action);
        using EquipmentAbilityBindingDef binding = new()
        {
            binding_id = "binding.test.status_ac_projection",
        };
        binding.reactions.Add(reaction);

        EquipmentAbilityBindingDefinition projected =
            EquipmentAbilityDefinitionProjection.ProjectBinding(binding);
        ApplyStatusActionPayloadDefinition projectedPayload =
            projected.Reactions[0].Actions[0].PayloadDefinition
                as ApplyStatusActionPayloadDefinition;
        _test.Eq(
            projectedPayload?.ArmorClassBonusPerStack ?? -1,
            3,
            "apply_status projection should preserve armor_class_bonus_per_stack."
        );

        var validErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateApplyStatusPayload(
            payload,
            BuildValidationContext(),
            "test.valid_status_ac",
            validErrors
        );
        _test.Eq(
            validErrors.Count,
            0,
            $"positive status AC should validate. errors={string.Join(" | ", validErrors)}"
        );

        payload.armor_class_bonus_per_stack = -1;
        var invalidErrors = new List<string>();
        EquipmentAbilityPayloadValidators.ValidateApplyStatusPayload(
            payload,
            BuildValidationContext(),
            "test.invalid_status_ac",
            invalidErrors
        );
        _test.True(
            ContainsErrorCode(invalidErrors, "EQA_STATUS_ARMOR_CLASS_BONUS_INVALID"),
            "negative status AC per stack should fail closed."
        );
    }

    private void TestRawFireFactsAndOriginFilters(Fixture fixture)
    {
        fixture.Wearer.SetDamageResistanceTyped("fire", "immune");

        AttackEffectResolutionResult immuneFire = ResolveDamage(
            fixture,
            fixture.Attacker,
            fixture.Wearer,
            "fire",
            10
        );
        _test.Eq(
            immuneFire.DamageEvents[0].RolledDamage,
            10,
            "immune fire should retain the pre-mitigation rolled damage."
        );
        bool directFactChanged = fixture.Runtime
            .GetEquipmentAbilityRuntimeService()
            .ResolveDamageTakenFinalized(
                new BattleEquipmentAbilityDamageAppliedContext
                {
                    SourceUnit = fixture.Attacker,
                    TargetUnit = fixture.Wearer,
                    BattleState = fixture.State,
                    RawDamage = 10,
                    HpDamage = 0,
                    HpBefore = 100,
                    DamageTag = "fire",
                }
            );
        _test.True(
            directFactChanged,
            "the finalized-damage runtime should consume an explicit raw fire fact."
        );
        AssertStatusStacks(
            fixture.Attacker,
            BurningArmorStatusId,
            1,
            "the direct fact probe should apply the same typed status."
        );
        fixture.Attacker.EraseStatusEffect(BurningArmorStatusId);
        _test.Eq(
            fixture.Wearer.GetCurrentHp(),
            100,
            "immune fire should preserve HP while retaining the positive raw event."
        );
        AssertStatusStacks(
            fixture.Wearer,
            BurningArmorStatusId,
            1,
            "external immune fire should add one burning-armor stack."
        );
        _test.False(
            fixture.Wearer.HasStatusEffect(RawOnlySentinelStatusId),
            "a zero-HP-damage event must not run a reaction that does not explicitly reference raw_damage."
        );

        ResolveDamage(fixture, fixture.Attacker, fixture.Wearer, "force", 4);
        AssertStatusStacks(
            fixture.Wearer,
            BurningArmorStatusId,
            1,
            "non-fire damage must not add burning-armor stacks."
        );
        fixture.Wearer.SetCurrentHp(100);

        ResolveDamage(
            fixture,
            fixture.Attacker,
            fixture.Wearer,
            "fire",
            10,
            BattleEffectOrigin.EquipmentAbility()
        );
        AssertStatusStacks(
            fixture.Wearer,
            BurningArmorStatusId,
            1,
            "equipment-generated fire must not recursively add burning-armor stacks."
        );

        ResolveDamage(fixture, fixture.Wearer, fixture.Wearer, "fire", 10);
        AssertStatusStacks(
            fixture.Wearer,
            BurningArmorStatusId,
            1,
            "self fire must not add burning-armor stacks."
        );

        ResolveDamage(fixture, fixture.Attacker, fixture.Wearer, "fire", 10);
        ResolveDamage(fixture, fixture.Attacker, fixture.Wearer, "fire", 10);
        ResolveDamage(fixture, fixture.Attacker, fixture.Wearer, "fire", 10);
        AssertStatusStacks(
            fixture.Wearer,
            BurningArmorStatusId,
            3,
            "burning-armor stacks should cap at the authored stack limit."
        );

        BattleStatusEffectState burning =
            fixture.Wearer.GetStatusEffect(BurningArmorStatusId);
        _test.Eq(
            burning?.armor_class_bonus_per_stack ?? -1,
            1,
            "the applied status should carry its typed AC-per-stack field."
        );
        _test.Eq(
            burning?.DuplicateState().armor_class_bonus_per_stack ?? -1,
            1,
            "status duplication should preserve AC-per-stack."
        );
        using GodotProjectionLease<Godot.Collections.Dictionary> lease =
            burning.ToDictionaryLease();
        BattleStatusEffectState restored =
            BattleStatusEffectState.FromDictionary(lease.Value);
        _test.Eq(
            restored?.armor_class_bonus_per_stack ?? -1,
            1,
            "status snapshot round-trip should preserve AC-per-stack."
        );
    }

    private void TestArmorClassBonusUsesAllActiveStatusStacks(Fixture fixture)
    {
        using var resolver = new BattleHitResolver();
        AttackCheckInput burningCheck = resolver.BuildSkillAttackCheck(
            fixture.Attacker,
            fixture.Wearer,
            null
        );
        _test.Eq(
            burningCheck.TargetArmorClass,
            13,
            "three burning-armor stacks at +1 each should raise base AC 10 to 13."
        );

        fixture.Wearer.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = FireShieldStatusId,
                source_unit_id = fixture.Wearer.unit_id,
                stacks = 1,
                duration = 60,
                armor_class_bonus_per_stack = 3,
            }
        );
        AttackCheckInput combinedCheck = resolver.BuildSkillAttackCheck(
            fixture.Attacker,
            fixture.Wearer,
            null
        );
        _test.Eq(
            combinedCheck.TargetArmorClass,
            16,
            "all active typed status AC bonuses should add across distinct statuses."
        );

        fixture.Wearer.GetStatusEffect(FireShieldStatusId).stacks = 0;
        AttackCheckInput inactiveCheck = resolver.BuildSkillAttackCheck(
            fixture.Attacker,
            fixture.Wearer,
            null
        );
        _test.Eq(
            inactiveCheck.TargetArmorClass,
            13,
            "a zero-stack status should not contribute AC."
        );
        fixture.Wearer.EraseStatusEffect(FireShieldStatusId);
    }

    private void TestLowHpCrossingTriggersSkillOnceAndPreviewDoesNotMutate(
        Fixture fixture
    )
    {
        fixture.Wearer.SetCurrentHp(60);
        fixture.Attacker.SetCurrentHp(100);

        ResolveDamage(fixture, fixture.Attacker, fixture.Wearer, "force", 15);
        _test.Eq(
            fixture.Wearer.GetCurrentHp(),
            45,
            "fixture damage should cross from above 50% to at most 50%."
        );
        _test.Eq(
            fixture.Attacker.GetCurrentHp(),
            93,
            "the finalized-damage trigger_skill action should execute its 7 fire damage."
        );
        _test.False(
            fixture.Attacker.HasStatusEffect(BurningArmorStatusId),
            "trigger_skill damage should be marked equipment-generated and must not recurse into the plate reaction."
        );

        ResolveDamage(fixture, fixture.Attacker, fixture.Wearer, "force", 5);
        _test.Eq(
            fixture.Attacker.GetCurrentHp(),
            93,
            "remaining below 50% should not retrigger without another downward crossing."
        );

        fixture.Wearer.SetCurrentHp(45);
        int wearerHpBeforePreview = fixture.Wearer.GetCurrentHp();
        int attackerHpBeforePreview = fixture.Attacker.GetCurrentHp();
        int wearerStatusCountBeforePreview = fixture.Wearer.GetStatusEffectsTyped().Count;
        int attackerStatusCountBeforePreview = fixture.Attacker.GetStatusEffectsTyped().Count;
        bool previewChanged = fixture.Runtime
            .GetEquipmentAbilityRuntimeService()
            .ResolveDamageTakenFinalized(
                new BattleEquipmentAbilityDamageAppliedContext
                {
                    SourceUnit = fixture.Wearer,
                    TargetUnit = fixture.Attacker,
                    BattleState = fixture.State,
                    RawDamage = 15,
                    HpDamage = 15,
                    HpBefore = 60,
                    DamageTag = "force",
                    IsPreview = true,
                }
            );
        _test.False(
            previewChanged,
            "preview finalized-damage trigger_skill should report no canonical mutation."
        );
        _test.Eq(
            fixture.Wearer.GetCurrentHp(),
            wearerHpBeforePreview,
            "preview should not mutate canonical wearer HP."
        );
        _test.Eq(
            fixture.Attacker.GetCurrentHp(),
            attackerHpBeforePreview,
            "preview should not execute trigger_skill against the canonical target."
        );
        _test.Eq(
            fixture.Wearer.GetStatusEffectsTyped().Count,
            wearerStatusCountBeforePreview,
            "preview should not add canonical wearer statuses."
        );
        _test.Eq(
            fixture.Attacker.GetStatusEffectsTyped().Count,
            attackerStatusCountBeforePreview,
            "preview should not add canonical target statuses."
        );
    }

    private static AttackEffectResolutionResult ResolveDamage(
        Fixture fixture,
        BattleUnitState source,
        BattleUnitState target,
        StringName damageTag,
        int power,
        BattleEffectOrigin origin = null
    )
    {
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            power: power,
            damageTag: damageTag
        );
        DamageResolutionContext context = DamageResolutionContext
            .Create(
                criticalHit: false,
                attackSuccess: true,
                secondaryHitSuccess: false,
                skillId: "test_external_damage"
            )
            .WithBattleState(fixture.State);
        if (origin != null)
        {
            context = context.WithDamageApplicationHookContext(
                batch: null,
                origin
            );
        }
        return fixture.Runtime.GetDamageResolver().ResolveEffects(
            source,
            target,
            new[] { effect },
            context
        );
    }

    private static EquipmentAbilityBindingDefinition BuildBinding()
    {
        EquipmentConditionGroupDefinition externalFireRawConditions = All(
            IntFact("raw_damage", "gt", 0),
            StringFact("damage_tag", "eq", "fire"),
            IntFact("is_equipment_generated", "eq", 0),
            IntFact("is_self_damage", "eq", 0)
        );
        EquipmentConditionGroupDefinition crossingConditions = All(
            IntFact("raw_damage", "gt", 0),
            IntFact("hp_before_percent_bp", "gt", 5000, "source"),
            IntFact("hp_percent_bp", "lte", 5000, "source"),
            IntFact("is_equipment_generated", "eq", 0),
            IntFact("is_self_damage", "eq", 0)
        );
        return new EquipmentAbilityBindingDefinition
        {
            BindingId = BindingId,
            TraitId = "trait.test.finalized_damage_status_ac",
            Reactions = new[]
            {
                new EquipmentAbilityReactionDefinition
                {
                    ReactionId = "reaction.test.external_fire_raw",
                    Trigger = EquipmentAbilityTriggerKind.OnDamageTakenFinalized,
                    Timing = EquipmentAbilityTimingKind.AfterDamage,
                    ConditionGroup = externalFireRawConditions,
                    Actions = new[]
                    {
                        new EquipmentAbilityActionDefinition
                        {
                            ActionId = "action.test.add_burning_armor",
                            Kind = "apply_status",
                            PayloadDefinition = new ApplyStatusActionPayloadDefinition
                            {
                                TargetSelector = "source",
                                StatusId = BurningArmorStatusId,
                                DurationTu = 60,
                                StackDelta = 1,
                                StackBehavior = "add",
                                StackLimit = 3,
                                ArmorClassBonusPerStack = 1,
                            },
                        },
                    },
                },
                new EquipmentAbilityReactionDefinition
                {
                    ReactionId = "reaction.test.no_raw_sentinel",
                    Trigger = EquipmentAbilityTriggerKind.OnDamageTakenFinalized,
                    Timing = EquipmentAbilityTimingKind.AfterDamage,
                    ConditionGroup = All(StringFact("damage_tag", "eq", "fire")),
                    Actions = new[]
                    {
                        new EquipmentAbilityActionDefinition
                        {
                            ActionId = "action.test.no_raw_sentinel",
                            Kind = "apply_status",
                            PayloadDefinition = new ApplyStatusActionPayloadDefinition
                            {
                                TargetSelector = "source",
                                StatusId = RawOnlySentinelStatusId,
                                DurationTu = 60,
                                StackDelta = 1,
                                StackBehavior = "refresh",
                            },
                        },
                    },
                },
                new EquipmentAbilityReactionDefinition
                {
                    ReactionId = "reaction.test.low_hp_crossing",
                    Trigger = EquipmentAbilityTriggerKind.OnDamageTakenFinalized,
                    Timing = EquipmentAbilityTimingKind.AfterDamage,
                    ConditionGroup = crossingConditions,
                    Actions = new[]
                    {
                        new EquipmentAbilityActionDefinition
                        {
                            ActionId = "action.test.low_hp_fire_burst",
                            Kind = "trigger_skill",
                            PayloadDefinition = new TriggerSkillActionPayloadDefinition
                            {
                                SkillId = TriggeredSkillId,
                                SkillLevel = 1,
                                TargetSelector = "target",
                            },
                        },
                    },
                },
            },
        };
    }

    private static SkillDefinition BuildTriggeredSkill()
    {
        CombatEffectDefinition damage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            power: 7,
            damageTag: "fire"
        );
        CombatSkillDefinition profile = TestSkillDefinitionProjection.BuildCombatProfile(
            TriggeredSkillId,
            effects: new[] { damage },
            targetMode: "unit",
            targetTeamFilter: "enemy",
            rangeValue: 99
        );
        return TestSkillDefinitionProjection.BuildSkill(
            TriggeredSkillId,
            combatProfile: profile
        );
    }

    private static EquipmentConditionGroupDefinition All(
        params EquipmentAbilityConditionDefinition[] conditions
    ) => new()
    {
        Mode = "all",
        Conditions = conditions ?? Array.Empty<EquipmentAbilityConditionDefinition>(),
    };

    private static EquipmentAbilityConditionDefinition IntFact(
        StringName factId,
        StringName compare,
        int value,
        StringName subject = default
    ) => new()
    {
        ConditionId = $"condition.test.{factId}.{compare}.{value}",
        Kind = "compare_fact",
        PayloadDefinition = new CompareFactConditionPayloadDefinition
        {
            Left = new EquipmentAbilityFactQueryDefinition
            {
                QueryKind = "fact",
                FactId = factId,
                Subject = subject,
            },
            Compare = compare,
            Right = new EquipmentAbilityFactQueryDefinition
            {
                QueryKind = "literal",
                IntLiteral = value,
            },
        },
    };

    private static EquipmentAbilityConditionDefinition StringFact(
        StringName factId,
        StringName compare,
        StringName value
    ) => new()
    {
        ConditionId = $"condition.test.{factId}.{compare}.{value}",
        Kind = "compare_fact",
        PayloadDefinition = new CompareFactConditionPayloadDefinition
        {
            Left = new EquipmentAbilityFactQueryDefinition
            {
                QueryKind = "fact",
                FactId = factId,
            },
            Compare = compare,
            Right = new EquipmentAbilityFactQueryDefinition
            {
                QueryKind = "literal",
                StringNameLiteral = value,
            },
        },
    };

    private void AssertStatusStacks(
        BattleUnitState unit,
        StringName statusId,
        int expected,
        string message
    )
    {
        _test.Eq(unit.GetStatusEffect(statusId)?.stacks ?? 0, expected, message);
    }

    private static EquipmentAbilityContentValidationContext BuildValidationContext() =>
        new()
        {
            KnownTraitIds = new HashSet<StringName>(),
            KnownSkillIds = new HashSet<StringName>(),
            WindupSkillIds = new HashSet<StringName>(),
            KnownStatusIds = new HashSet<StringName> { BurningArmorStatusId },
        };

    private static bool ContainsErrorCode(IEnumerable<string> errors, string code)
    {
        foreach (string error in errors ?? Array.Empty<string>())
        {
            if (error?.StartsWith(code + " ", StringComparison.Ordinal) == true)
                return true;
        }
        return false;
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture(
            BattleRuntimeModule runtime,
            BattleState state,
            BattleUnitState wearer,
            BattleUnitState attacker
        )
        {
            Runtime = runtime;
            State = state;
            Wearer = wearer;
            Attacker = attacker;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }
        internal BattleUnitState Wearer { get; }
        internal BattleUnitState Attacker { get; }

        internal static Fixture Create()
        {
            EquipmentAbilityBindingDefinition binding = BuildBinding();
            SkillDefinition triggeredSkill = BuildTriggeredSkill();
            var runtime = new BattleRuntimeModule();
            runtime.setup(
                skill_definitions: new Dictionary<StringName, SkillDefinition>
                {
                    [TriggeredSkillId] = triggeredSkill,
                },
                equipment_ability_bindings:
                    new Dictionary<StringName, EquipmentAbilityBindingDefinition>
                    {
                        [BindingId] = binding,
                    }
            );

            BattleUnitState wearer = Unit("test_status_ac_wearer", "heroes");
            BattleUnitState attacker = Unit("test_status_ac_attacker", "enemies");
            AttachBinding(wearer, "wearer");
            AttachBinding(attacker, "attacker");
            wearer.SetAnchorCoord(Vector2I.Zero);
            attacker.SetAnchorCoord(new Vector2I(1, 0));

            var state = new BattleState
            {
                battle_id = "equipment_finalized_damage_status_ac_test",
            };
            state.SetUnit(wearer);
            state.SetUnit(attacker);
            return new Fixture(runtime, state, wearer, attacker);
        }

        private static BattleUnitState Unit(StringName unitId, StringName factionId)
        {
            BattleUnitState unit = new BattleUnitState
            {
                unit_id = unitId,
                display_name = unitId.ToString(),
                faction_id = factionId,
            }.WithCombatResourcesForTest(hp: 100, isAlive: true);
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
            unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
            return unit;
        }

        private static void AttachBinding(BattleUnitState unit, StringName suffix)
        {
            StringName instanceId = $"eq_test_status_ac_{suffix}";
            unit.ReplaceEquipmentAbilityProjectionTyped(
                new[]
                {
                    new BattleEquipmentAbilitySourceState
                    {
                        EffectiveInstanceKey = $"projected:{instanceId}",
                        EquipmentDefId = "item.test.finalized_damage_status_ac",
                        SourceEquipmentInstanceId = instanceId,
                        SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                        AbilityIds = new List<StringName> { BindingId },
                    },
                },
                temporalProgressModifiers: null
            );
        }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
        }
    }
}
