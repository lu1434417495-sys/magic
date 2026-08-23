using System;
using System.Collections.Generic;
using Godot;

// §8.6：通用 attack-hit reaction（on_attack_hit / after_hit）。
// 覆盖：武器命中触发；非武器 attack spell 命中触发（旧 on_hit 不触发、新
// on_attack_hit 触发）；miss 不触发；save-only 无攻击检定不触发；多目标每目标一次；
// 目标完全减伤与使用者满血仍消费；preview/AI 不消费；has_status + consume_status_stacks
// + heal 组合模拟龙血沸腾链路（3 层 charge、命中消费并治疗、第 4 次不触发）；
// authoring 校验与 trigger/timing closed domain。
public partial class run_equipment_attack_hit_reaction_regression : LifecycleTestSceneTree
{
    private static readonly StringName AttackHitBindingId = "binding.test.attack_hit_boil";
    private static readonly StringName LegacyOnHitBindingId = "binding.test.legacy_on_hit";
    private static readonly StringName BoilStatusId = "test_dragon_blood_boil";
    private static readonly StringName LegacyMarkerStatusId = "test_legacy_on_hit_marker";
    private static readonly StringName TestSkillId = "test_attack_hit_skill";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestTriggerTimingClosedDomain();
        TestContentValidationAcceptsAndRejects();
        TestWeaponHitTriggersBothTriggers();
        TestNonWeaponAttackSpellHitTriggersOnlyAttackHit();
        TestMissDoesNotTrigger();
        TestSaveOnlyDamageDoesNotTrigger();
        TestMultiTargetFiresOncePerTarget();
        TestFullyMitigatedHitStillConsumes();
        TestFullHealthHitStillConsumes();
        TestChargesExhaustAfterThreeHits();
        TestPreviewAndAiDoNotConsume();

        RequestTestExit(_test.Finish("Equipment attack-hit reaction regression"));
    }

    private void TestTriggerTimingClosedDomain()
    {
        using var registry = new EquipmentAbilityContentRegistry();
        IReadOnlyDictionary<EquipmentAbilityTriggerKind, EquipmentAbilityTriggerTimingSpec> specs =
            registry.GetTriggerTimingSpecsTyped();
        _test.True(
            specs.ContainsKey(EquipmentAbilityTriggerKind.OnAttackHit),
            "trigger timing specs should register on_attack_hit."
        );
        _test.True(
            specs[EquipmentAbilityTriggerKind.OnAttackHit].AllowedTimings.Contains(
                EquipmentAbilityTimingKind.AfterHit
            ),
            "on_attack_hit should allow the after_hit timing."
        );
        _test.False(
            specs[EquipmentAbilityTriggerKind.OnAttackHit].AllowedTimings.Contains(
                EquipmentAbilityTimingKind.BeforeHit
            )
                || specs[EquipmentAbilityTriggerKind.OnAttackHit].AllowedTimings.Contains(
                    EquipmentAbilityTimingKind.AfterKill
                ),
            "on_attack_hit should reject before_hit/after_kill timings."
        );
        _test.False(
            specs[EquipmentAbilityTriggerKind.OnHit].AllowedTimings.Contains(
                EquipmentAbilityTimingKind.AfterAttackCheck
            ),
            "the legacy on_hit trigger metadata should stay unchanged."
        );
    }

    private void TestContentValidationAcceptsAndRejects()
    {
        using var registry = new EquipmentAbilityContentRegistry();

        EquipmentAbilityRegistryBuildResult accepted = registry.Rebuild(
            new[] { BuildBoilAuthoringPack("on_attack_hit", "after_hit") },
            BuildValidationContext()
        );
        _test.True(
            accepted.Success,
            $"an on_attack_hit/after_hit pack should build: {FormatErrors(accepted.Errors)}"
        );
        EquipmentAbilityBindingDefinition definition =
            registry.GetBindingDefinitionsTyped()[AttackHitBindingId];
        _test.Eq(
            definition?.Reactions?[0]?.Trigger,
            EquipmentAbilityTriggerKind.OnAttackHit,
            "the projected reaction should carry the OnAttackHit trigger kind."
        );
        _test.Eq(
            definition?.Reactions?[0]?.Timing,
            EquipmentAbilityTimingKind.AfterHit,
            "the projected reaction should carry the AfterHit timing kind."
        );

        EquipmentAbilityRegistryBuildResult badTiming = registry.Rebuild(
            new[] { BuildBoilAuthoringPack("on_attack_hit", "after_kill") },
            BuildValidationContext()
        );
        _test.False(badTiming.Success, "on_attack_hit with after_kill timing should be rejected.");
        AssertErrorContains(badTiming.Errors, "EQA_TRIGGER_TIMING_UNSUPPORTED", "timing");

        EquipmentAbilityRegistryBuildResult legacyStillValid = registry.Rebuild(
            new[] { BuildBoilAuthoringPack("on_hit", "after_hit") },
            BuildValidationContext()
        );
        _test.True(
            legacyStillValid.Success,
            $"the legacy on_hit trigger should keep building: {FormatErrors(legacyStillValid.Errors)}"
        );
    }

    private void TestWeaponHitTriggersBothTriggers()
    {
        using AttackHitFixture fixture = AttackHitFixture.Create();
        fixture.EquipHolderWeapon();
        fixture.ApplyBoilStacks(3);
        fixture.Holder.SetCurrentHp(50);

        fixture.ResolveAttackHit(fixture.BuildWeaponEffect());
        _test.Eq(
            fixture.BoilStacks(),
            2,
            "a weapon hit should consume one boil charge through on_attack_hit."
        );
        _test.Eq(
            fixture.Holder.GetCurrentHp(),
            56,
            "a weapon hit should heal the holder 1D6 (fixed max 6) through on_attack_hit."
        );
        _test.True(
            fixture.Holder.HasStatusEffect(LegacyMarkerStatusId),
            "a weapon hit should still fire the legacy on_hit reaction."
        );
    }

    private void TestNonWeaponAttackSpellHitTriggersOnlyAttackHit()
    {
        using AttackHitFixture fixture = AttackHitFixture.Create();
        fixture.ApplyBoilStacks(3);
        fixture.Holder.SetCurrentHp(50);

        fixture.ResolveAttackHit(fixture.BuildSpellEffect());
        _test.Eq(
            fixture.BoilStacks(),
            2,
            "a non-weapon attack spell hit should consume one boil charge."
        );
        _test.Eq(
            fixture.Holder.GetCurrentHp(),
            56,
            "a non-weapon attack spell hit should heal the holder 1D6."
        );
        _test.False(
            fixture.Holder.HasStatusEffect(LegacyMarkerStatusId),
            "the legacy on_hit reaction must stay weapon-gated and not fire on a spell hit."
        );
    }

    private void TestMissDoesNotTrigger()
    {
        using AttackHitFixture fixture = AttackHitFixture.Create();
        fixture.ApplyBoilStacks(3);
        fixture.Holder.SetCurrentHp(50);

        fixture.ResolveAttackMiss(fixture.BuildSpellEffect());
        _test.Eq(fixture.BoilStacks(), 3, "a miss must not consume boil charges.");
        _test.Eq(fixture.Holder.GetCurrentHp(), 50, "a miss must not heal the holder.");
        _test.False(
            fixture.Holder.HasStatusEffect(LegacyMarkerStatusId),
            "a miss must not fire the legacy on_hit reaction."
        );
    }

    private void TestSaveOnlyDamageDoesNotTrigger()
    {
        using AttackHitFixture fixture = AttackHitFixture.Create();
        fixture.ApplyBoilStacks(3);
        fixture.Holder.SetCurrentHp(50);

        // 豁免型主直接伤害没有攻击检定：走 ResolveEffects，不经过 attack-hit sink。
        fixture.Resolver.ResolveEffects(
            fixture.Holder,
            fixture.Target,
            new[]
            {
                TestSkillDefinitionProjection.BuildEffect(
                    "damage",
                    damageTag: "fire",
                    power: 8,
                    saveDc: 10,
                    saveAbility: "agility",
                    saveTag: "magic",
                    savePartialOnSuccess: true
                ),
            },
            DamageResolutionContext
                .ForSkill(TestSkillId)
                .WithBattleState(fixture.State)
        );
        _test.Eq(fixture.BoilStacks(), 3, "save-only damage without an attack check must not consume.");
        _test.Eq(fixture.Holder.GetCurrentHp(), 50, "save-only damage must not heal the holder.");
    }

    private void TestMultiTargetFiresOncePerTarget()
    {
        using AttackHitFixture fixture = AttackHitFixture.Create();
        fixture.ApplyBoilStacks(3);
        fixture.Holder.SetCurrentHp(40);
        BattleUnitState secondTarget = AttackHitFixture.AddTarget(fixture, "attack_hit_target_b");

        fixture.ResolveAttackHit(fixture.BuildSpellEffect());
        _test.Eq(fixture.BoilStacks(), 2, "the first target hit should consume exactly one charge.");
        _test.Eq(fixture.Holder.GetCurrentHp(), 46, "the first target hit should heal once.");

        fixture.ResolveAttackHit(fixture.BuildSpellEffect(), secondTarget);
        _test.Eq(fixture.BoilStacks(), 1, "the second target hit should consume one more charge.");
        _test.Eq(fixture.Holder.GetCurrentHp(), 52, "the second target hit should heal once more.");
    }

    private void TestFullyMitigatedHitStillConsumes()
    {
        using AttackHitFixture fixture = AttackHitFixture.Create();
        fixture.ApplyBoilStacks(3);
        fixture.Holder.SetCurrentHp(50);
        fixture.Target.SetDamageResistanceTyped("fire", "immune");

        AttackEffectResolutionResult result = fixture.ResolveAttackHit(fixture.BuildSpellEffect());
        _test.Eq(result.Damage, 0, "an immune target should take zero damage.");
        _test.Eq(
            fixture.BoilStacks(),
            2,
            "a fully mitigated hit should still consume one boil charge."
        );
        _test.Eq(
            fixture.Holder.GetCurrentHp(),
            56,
            "a fully mitigated hit should still heal the holder."
        );
    }

    private void TestFullHealthHitStillConsumes()
    {
        using AttackHitFixture fixture = AttackHitFixture.Create();
        fixture.ApplyBoilStacks(3);

        fixture.ResolveAttackHit(fixture.BuildSpellEffect());
        _test.Eq(
            fixture.BoilStacks(),
            2,
            "a hit at full health should still consume one boil charge."
        );
        _test.Eq(
            fixture.Holder.GetCurrentHp(),
            100,
            "a hit at full health should not overheal the holder."
        );
    }

    private void TestChargesExhaustAfterThreeHits()
    {
        using AttackHitFixture fixture = AttackHitFixture.Create();
        fixture.ApplyBoilStacks(3);
        fixture.Holder.SetCurrentHp(10);

        for (int hit = 1; hit <= 3; hit++)
        {
            fixture.ResolveAttackHit(fixture.BuildSpellEffect());
            _test.Eq(
                fixture.Holder.GetCurrentHp(),
                10 + hit * 6,
                $"hit {hit} should consume one charge and heal 1D6."
            );
        }
        _test.False(
            fixture.Holder.HasStatusEffect(BoilStatusId),
            "the boil status should be gone after the third charge is consumed."
        );

        fixture.ResolveAttackHit(fixture.BuildSpellEffect());
        _test.Eq(
            fixture.Holder.GetCurrentHp(),
            28,
            "the fourth hit must not trigger: all charges are exhausted."
        );
    }

    private void TestPreviewAndAiDoNotConsume()
    {
        using AttackHitFixture fixture = AttackHitFixture.Create();
        fixture.ApplyBoilStacks(3);
        fixture.Holder.SetCurrentHp(50);
        CombatEffectDefinition effect = fixture.BuildSpellEffect();
        DamageResolutionContext context = DamageResolutionContext
            .Create(
                criticalHit: false,
                attackSuccess: true,
                secondaryHitSuccess: false,
                skillId: TestSkillId
            )
            .WithBattleState(fixture.State)
            .WithAttackCheck();

        BattleDamagePreviewResult preview = fixture.Resolver.PreviewDamageEffectTyped(
            fixture.Holder,
            fixture.Target,
            effect,
            context,
            BattleDamagePreviewRollMode.Average,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.True(preview != null, "canonical preview should return a result.");
        _test.Eq(fixture.BoilStacks(), 3, "preview must not consume boil charges.");
        _test.Eq(fixture.Holder.GetCurrentHp(), 50, "preview must not heal the holder.");

        // AI lane：与 BattleAiScoreService 相同的 detached working set 评分路径。
        BattleDamagePreviewWorkingSet workingSet = BattleDamagePreviewWorkingSet.CreateDetached(
            fixture.Holder,
            fixture.Target,
            fixture.State
        );
        BattleDamagePreviewScoreResult score = fixture.Resolver.PreviewDamageScoreOnWorkingSetTyped(
            workingSet,
            effect,
            context,
            BattleDamagePreviewRollMode.Average,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.True(score != null, "the AI score lane should return a result.");
        _test.Eq(fixture.BoilStacks(), 3, "AI scoring must not consume boil charges.");
        _test.Eq(fixture.Holder.GetCurrentHp(), 50, "AI scoring must not heal the holder.");
    }

    private static EquipmentAbilityContentPackImportModel BuildBoilAuthoringPack(
        StringName trigger,
        StringName timing
    )
    {
        return new EquipmentAbilityContentPackImportModel
        {
            pack_id = "pack.test.attack_hit_boil",
            schema_version = 1,
            load_order = 10,
            bindings = new[]
            {
                new EquipmentAbilityBindingImportModel
                {
                    binding_id = AttackHitBindingId.ToString(),
                    trait_id = "trait.test.attack_hit_boil",
                    override_mode = "add",
                    allowed_source_kinds = new[] { "equipment_fixed" },
                    reactions = new[]
                    {
                        new EquipmentAbilityReactionImportModel
                        {
                            reaction_id = "reaction.attack_hit_boil",
                            trigger = trigger.ToString(),
                            timing = timing.ToString(),
                            condition_group = new EquipmentAbilityConditionGroupImportModel
                            {
                                conditions = new[]
                                {
                                    new EquipmentAbilityConditionImportModel
                                    {
                                        condition_id = "condition.has_boil",
                                        kind = "has_status",
                                        payload = new HasStatusConditionPayloadImportModel
                                        {
                                            subject = "source",
                                            status_id = BoilStatusId.ToString(),
                                        },
                                    },
                                },
                            },
                            actions = new[]
                            {
                                new EquipmentAbilityActionImportModel
                                {
                                    action_id = "action.consume_boil",
                                    kind = "consume_status_stacks",
                                    payload = new ConsumeStatusStacksActionPayloadImportModel
                                    {
                                        target_selector = "source",
                                        status_id = BoilStatusId.ToString(),
                                        count = 1,
                                    },
                                },
                                new EquipmentAbilityActionImportModel
                                {
                                    action_id = "action.boil_heal",
                                    kind = "heal",
                                    payload = new HealActionPayloadImportModel
                                    {
                                        target_selector = "source",
                                        dice = new DiceExpressionImportModel
                                        {
                                            terms = new[]
                                            {
                                                new DiceExpressionTermImportModel
                                                {
                                                    dice_count = 1,
                                                    dice_sides = 6,
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };
    }

    private static EquipmentAbilityContentValidationContext BuildValidationContext()
    {
        return new EquipmentAbilityContentValidationContext
        {
            KnownTraitIds = new HashSet<StringName> { "trait.test.attack_hit_boil" },
            KnownSkillIds = new HashSet<StringName> { "known_skill" },
            KnownStatusIds = new HashSet<StringName> { BoilStatusId },
        };
    }

    private void AssertErrorContains(
        IReadOnlyList<string> errors,
        string code,
        string pathFragment
    )
    {
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(code) && (error ?? "").Contains(pathFragment))
                return;
        }
        _test.Fail(
            $"Expected error containing code={code} path={pathFragment}. errors={FormatErrors(errors)}"
        );
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors ?? Array.Empty<string>())
            values.Add(error ?? "");
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private sealed class AttackHitFixture : IDisposable
    {
        private AttackHitFixture(
            BattleRuntimeModule runtime,
            FixedRollDamageResolver resolver,
            BattleState state,
            BattleUnitState holder,
            BattleUnitState target
        )
        {
            Runtime = runtime;
            Resolver = resolver;
            State = state;
            Holder = holder;
            Target = target;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal FixedRollDamageResolver Resolver { get; }
        internal BattleState State { get; }
        internal BattleUnitState Holder { get; }
        internal BattleUnitState Target { get; }

        internal static AttackHitFixture Create()
        {
            var bindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                [AttackHitBindingId] = BuildAttackHitBinding(),
                [LegacyOnHitBindingId] = BuildLegacyOnHitBinding(),
            };
            var runtime = new BattleRuntimeModule();
            runtime.setup(equipment_ability_bindings: bindings);
            // ConfigureDamageResolverForTests 内部经 BindEquipmentRulePorts 重新绑定
            // query/sink ports 并重接 equipment service 的 damage resolver，
            // 因此 after-hit 反应（含 heal）都走同一 fixed-roll seam。
            var resolver = new FixedRollDamageResolver();
            BattleTestFixture.ConfigureDamageResolverForTests(runtime, resolver);

            BattleUnitState holder = Unit("attack_hit_holder", "heroes");
            BattleUnitState target = Unit("attack_hit_target", "enemies");
            BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
                "attack_hit_reaction_test",
                holder,
                target
            );
            runtime.SetupStateForTests(state);
            Attach(holder, "main", AttackHitBindingId, LegacyOnHitBindingId);
            return new AttackHitFixture(runtime, resolver, state, holder, target);
        }

        internal static BattleUnitState AddTarget(AttackHitFixture fixture, StringName unitId)
        {
            BattleUnitState target = Unit(unitId, "enemies");
            fixture.State.SetUnit(target);
            fixture.State.enemy_unit_ids.Add(target.unit_id);
            return target;
        }

        internal void EquipHolderWeapon()
        {
            Holder.SetUnarmedWeaponProjectionTyped(
                "physical_slash",
                new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                    flat_bonus = 0,
                },
                1
            );
        }

        internal void ApplyBoilStacks(int stacks)
        {
            Holder.SetStatusEffect(
                new BattleStatusEffectState
                {
                    status_id = BoilStatusId,
                    source_unit_id = Holder.unit_id,
                    stacks = stacks,
                    duration = 180,
                }
            );
        }

        internal int BoilStacks() => Holder.GetStatusEffect(BoilStatusId)?.stacks ?? 0;

        internal CombatEffectDefinition BuildSpellEffect() =>
            TestSkillDefinitionProjection.BuildEffect("damage", damageTag: "fire", power: 8);

        internal CombatEffectDefinition BuildWeaponEffect() =>
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                damageTag: "physical_slash",
                power: 4,
                addWeaponDice: true
            );

        internal AttackEffectResolutionResult ResolveAttackHit(
            CombatEffectDefinition effect,
            BattleUnitState target = null
        )
        {
            return Resolver.ResolveAttackEffects(
                Holder,
                target ?? Target,
                new[] { effect },
                new AttackCheckInput(forceHitNoCrit: true, skillId: TestSkillId),
                new AttackContext
                {
                    BattleState = State,
                    SkillId = TestSkillId,
                }
            );
        }

        internal AttackEffectResolutionResult ResolveAttackMiss(CombatEffectDefinition effect)
        {
            // requiredRoll 21 且关闭自然 20 自动命中：任意 D20 结果都是普通 miss。
            return Resolver.ResolveAttackEffects(
                Holder,
                Target,
                new[] { effect },
                new AttackCheckInput(
                    requiredRoll: 21,
                    naturalOneAutoMiss: true,
                    naturalTwentyAutoHit: false,
                    skillId: TestSkillId
                ),
                new AttackContext
                {
                    BattleState = State,
                    SkillId = TestSkillId,
                }
            );
        }

        private static void Attach(
            BattleUnitState unit,
            StringName suffix,
            params StringName[] bindingIds
        )
        {
            StringName instanceId = $"eq_attack_hit_{suffix}";
            unit.ReplaceEquipmentAbilityProjectionTyped(
                new[]
                {
                    new BattleEquipmentAbilitySourceState
                    {
                        EffectiveInstanceKey = $"projected:{instanceId}",
                        EquipmentDefId = "item.test.attack_hit",
                        SourceEquipmentInstanceId = instanceId,
                        SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                        AbilityIds = new List<StringName>(bindingIds),
                    },
                },
                temporalProgressModifiers: null
            );
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
            unit.attribute_snapshot.SetValue("agility", 10);
            return unit;
        }

        // 龙血沸腾链路（§3.4/§8.6）：has_status → 消耗 1 层自身 status → 自身治疗 1D6。
        private static EquipmentAbilityBindingDefinition BuildAttackHitBinding()
        {
            return new EquipmentAbilityBindingDefinition
            {
                BindingId = AttackHitBindingId,
                TraitId = "trait.test.attack_hit_boil",
                Reactions = new[]
                {
                    new EquipmentAbilityReactionDefinition
                    {
                        ReactionId = "reaction.attack_hit_boil",
                        Trigger = EquipmentAbilityTriggerKind.OnAttackHit,
                        Timing = EquipmentAbilityTimingKind.AfterHit,
                        ConditionGroup = new EquipmentConditionGroupDefinition
                        {
                            Conditions = new[]
                            {
                                new EquipmentAbilityConditionDefinition
                                {
                                    ConditionId = "condition.has_boil",
                                    Kind = "has_status",
                                    PayloadDefinition = new HasStatusConditionPayloadDefinition
                                    {
                                        Subject = "source",
                                        StatusId = BoilStatusId,
                                    },
                                },
                            },
                        },
                        Actions = new EquipmentAbilityActionDefinition[]
                        {
                            new()
                            {
                                ActionId = "action.consume_boil",
                                Kind = "consume_status_stacks",
                                PayloadDefinition = new ConsumeStatusStacksActionPayloadDefinition
                                {
                                    TargetSelector = "source",
                                    StatusId = BoilStatusId,
                                    Count = 1,
                                    RequireSourceUnitMatch = true,
                                },
                            },
                            new()
                            {
                                ActionId = "action.boil_heal",
                                Kind = "heal",
                                PayloadDefinition = new HealActionPayloadDefinition
                                {
                                    TargetSelector = "source",
                                    Dice = new DiceExpressionDefinition
                                    {
                                        Terms = new[]
                                        {
                                            new DiceExpressionTermDefinition
                                            {
                                                DiceCount = 1,
                                                DiceSides = 6,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        // 旧 on_hit 保持 weapon-hit 语义：命中后给持有者施加 marker status，
        // 用于证明非武器 attack spell 命中不会触发它。
        private static EquipmentAbilityBindingDefinition BuildLegacyOnHitBinding()
        {
            return new EquipmentAbilityBindingDefinition
            {
                BindingId = LegacyOnHitBindingId,
                TraitId = "trait.test.attack_hit_boil",
                Reactions = new[]
                {
                    new EquipmentAbilityReactionDefinition
                    {
                        ReactionId = "reaction.legacy_on_hit",
                        Trigger = EquipmentAbilityTriggerKind.OnHit,
                        Timing = EquipmentAbilityTimingKind.AfterHit,
                        Actions = new EquipmentAbilityActionDefinition[]
                        {
                            new()
                            {
                                ActionId = "action.legacy_on_hit_marker",
                                Kind = "apply_status",
                                PayloadDefinition = new ApplyStatusActionPayloadDefinition
                                {
                                    TargetSelector = "source",
                                    StatusId = LegacyMarkerStatusId,
                                    DurationTu = 60,
                                    StackDelta = 1,
                                },
                            },
                        },
                    },
                },
            };
        }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
        }
    }
}
