using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class
    run_battle_counterattack_execution_parity_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestCounterattackExecutesInsideOriginalRoot();
        TestCounterattackBusinessFailureIsPreserved();
        TestCounterattackWeaponRequirementFailureIsSideEffectFree();
        TestCriticalButUnappliedDoesNotGrantWeaponTraining();
        TestWeaponTrainingMappingAndRankAmounts();
        RequestTestExit(
            _test.Finish(
                "Battle counterattack execution parity regression"
            )
        );
    }

    private void TestCounterattackBusinessFailureIsPreserved()
    {
        SkillDefinition weaponAction = BuildWeaponAction();
        BattleUnitState attacker = BuildUnit(
            "counter_failure_attacker",
            "player",
            Vector2I.Zero
        );
        BattleUnitState defender = BuildUnit(
            "counter_failure_defender",
            "enemy",
            new Vector2I(1, 0)
        );
        defender.SetKnownSkillLevelTyped(
            weaponAction.SkillId,
            1
        );
        defender.ReplaceCounterattackCapabilitiesTyped(
            new[]
            {
                new BattleCounterattackCapability(
                    "counter_failure_capability",
                    BattleCounterattackTriggerKind
                        .MeleeHitReceived,
                    SelectionPriority: 1,
                    ChancePercent: 100,
                    AttackRollBonus: 0,
                    WeaponActionDefinitionId:
                        weaponAction.SkillId
                ),
            }
        );
        var state = BattleTestFixture.BuildFlatState(
            "counter_failure",
            new Vector2I(3, 2)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { attacker },
            new[] { defender }
        );
        using var runtime = new BattleRuntimeModule();
        runtime.setup(
            skill_definitions:
                new Dictionary<StringName, SkillDefinition>
                {
                    [weaponAction.SkillId] = weaponAction,
                }
        );
        runtime.SetupStateForTests(state);
        runtime.ConfigureDamageResolverForTests(
            new ThrowOnSecondAttackResolver()
        );

        InvalidOperationException captured = null;
        using (var batch = new BattleEventBatch())
        {
            try
            {
                BattleReactionRootTestHelper
                    .ExecuteInReactionRoot(
                        runtime,
                        batch,
                        () =>
                        {
                            using BattleLogicalAttackScope
                                logicalAttack =
                                    runtime.BeginLogicalAttack(
                                        BattleAttackDeliveryKind
                                            .MeleeWeapon
                                    );
                            runtime._damage_resolver
                                .ResolveAttackEffects(
                                    attacker,
                                    defender,
                                    weaponAction
                                        .CombatProfile
                                        .EffectDefinitions,
                                    new AttackCheckInput(
                                        skillId:
                                            weaponAction.SkillId
                                    ),
                                    new AttackContext
                                    {
                                        BattleState = state,
                                        SkillId =
                                            weaponAction.SkillId,
                                        EventBatch = batch,
                                        Action =
                                            logicalAttack.Context,
                                    }
                                );
                            logicalAttack.Complete();
                        }
                    );
            }
            catch (InvalidOperationException error)
            {
                captured = error;
            }
        }

        _test.Eq(
            captured?.Message,
            ThrowOnSecondAttackResolver.FailureMessage,
            "counterattack execution must preserve the original resolver failure."
        );
        _test.False(
            captured?.Message.Contains(
                "disposed without Complete",
                StringComparison.Ordinal
            ) == true,
            "nested reaction scopes must not overwrite a business failure."
        );
        using var nextBatch = new BattleEventBatch();
        BattleReactionRootTestHelper.ExecuteInReactionRoot(
            runtime,
            nextBatch,
            () => { }
        );
        _test.Eq(
            runtime._counterattackSystem.PendingCount,
            0,
            "a failed root must leave the next root with an empty queue."
        );
    }

    private void TestCounterattackExecutesInsideOriginalRoot()
    {
        SkillDefinition weaponAction = BuildWeaponAction();
        SkillDefinition basicAttack =
            BuildBasicAttackMasteryPolicy();
        SkillDefinition swordTraining =
            BuildWeaponTraining("sword_training");
        var gateway = new RecordingMasteryGateway();
        BattleUnitState attacker = BuildUnit(
            "counter_execution_attacker",
            "player",
            Vector2I.Zero
        );
        BattleUnitState defender = BuildUnit(
            "counter_execution_defender",
            "enemy",
            new Vector2I(1, 0)
        );
        defender.source_member_id =
            "counter_execution_member";
        defender.SetKnownSkillLevelTyped(
            weaponAction.SkillId,
            1
        );
        var state = BattleTestFixture.BuildFlatState(
            "counter_execution",
            new Vector2I(3, 2)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { attacker },
            new[] { defender }
        );
        using var runtime = new BattleRuntimeModule();
        runtime.setup(
            character_gateway: gateway,
            skill_definitions:
                new Dictionary<StringName, SkillDefinition>
                {
                    [weaponAction.SkillId] = weaponAction,
                    [basicAttack.SkillId] = basicAttack,
                    [swordTraining.SkillId] = swordTraining,
                }
        );
        runtime.SetupStateForTests(state);
        var resolver = new CountingFixedSuccessResolver();
        runtime.ConfigureDamageResolverForTests(resolver);
        defender.ReplaceCounterattackCapabilitiesTyped(
            new[]
            {
                new BattleCounterattackCapability(
                    "counter_execution_capability",
                    BattleCounterattackTriggerKind
                        .MeleeHitReceived,
                    SelectionPriority: 1,
                    ChancePercent: 100,
                    AttackRollBonus: 0,
                    WeaponActionDefinitionId:
                        weaponAction.SkillId
                ),
            }
        );

        int attackerHpBefore = attacker.GetCurrentHp();
        int defenderHpBefore = defender.GetCurrentHp();
        int defenderApBefore = defender.GetCurrentAp();
        int defenderStaminaBefore =
            defender.GetCurrentStamina();
        using var batch = new BattleEventBatch();
        AttackEffectResolutionResult originalResolution = default;
        BattleReactionRootTestHelper.ExecuteInReactionRoot(
            runtime,
            batch,
            () =>
            {
                using BattleLogicalAttackScope logicalAttack =
                    runtime.BeginLogicalAttack(
                        BattleAttackDeliveryKind.MeleeWeapon
                    );
                for (int stage = 0; stage < 2; stage++)
                {
                    originalResolution =
                        runtime._damage_resolver.ResolveAttackEffects(
                            attacker,
                            defender,
                            weaponAction.CombatProfile.EffectDefinitions,
                            new AttackCheckInput(
                                skillId: weaponAction.SkillId
                            ),
                            new AttackContext
                            {
                                BattleState = state,
                                SkillId = weaponAction.SkillId,
                                EventBatch = batch,
                                Action = logicalAttack.Context,
                            }
                        );
                }
                logicalAttack.Complete();
            }
        );

        _test.True(
            originalResolution.Applied,
            $"the original attack must be applied. damage={originalResolution.Damage} success={originalResolution.AttackSuccess}"
        );
        _test.True(
            defender.GetCurrentHp() < defenderHpBefore,
            $"the original weapon attack must resolve before drain. hp={defender.GetCurrentHp()}/{defenderHpBefore} damage={originalResolution.Damage} success={originalResolution.AttackSuccess} applied={originalResolution.Applied}"
        );
        _test.True(
            attacker.GetCurrentHp() < attackerHpBefore,
            $"the queued counterattack must execute before the root returns. hp={attacker.GetCurrentHp()}/{attackerHpBefore} queue={runtime._counterattackSystem.PendingCount} reports={batch.ReportEntriesTyped.Count}"
        );
        _test.Eq(
            defender.CaptureReactionRawTyped().ChargesRemaining,
            0,
            "a successful attempt must consume one reaction charge."
        );
        _test.Eq(
            defender.GetCurrentStamina(),
            defenderStaminaBefore - 2,
            "counterattack must spend the action definition stamina cost."
        );
        _test.Eq(
            defender.GetCurrentAp(),
            defenderApBefore,
            "counterattack must not consume AP."
        );
        _test.Eq(
            defender.GetCooldownTyped(weaponAction.SkillId),
            0,
            "counterattack must not set the action cooldown."
        );
        _test.Eq(
            runtime._counterattackSystem.PendingCount,
            0,
            "the outer boundary must drain the counterattack queue."
        );
        _test.Eq(
            runtime._counterattackSystem.DedupeCount,
            0,
            "successful drain must clear root-local dedupe state."
        );
        _test.Eq(
            resolver.CallCount,
            3,
            "two stages sharing one action id must enqueue exactly one counterattack."
        );
        AssertSingleWeaponTrainingMasteryChange(
            batch,
            defender.source_member_id,
            swordTraining.SkillId,
            1
        );
        _test.Eq(
            CountMasteryChanges(
                batch,
                defender.source_member_id,
                weaponAction.SkillId
            ),
            0,
            "counterattack action definition mastery must not be granted."
        );
        _test.Eq(
            gateway.GetTotalMastery(
                defender.source_member_id,
                swordTraining.SkillId
            ),
            1,
            "canonical mastery owner must receive the same sword-training amount."
        );
        _test.Eq(
            gateway.SkillUsedEvents,
            0,
            "counterattack must not publish skill_used achievements."
        );
    }

    private void
        TestCounterattackWeaponRequirementFailureIsSideEffectFree()
    {
        SkillDefinition weaponAction = BuildWeaponAction(
            requiredWeaponFamily: "hammer"
        );
        BattleUnitState attacker = BuildUnit(
            "counter_gate_attacker",
            "player",
            Vector2I.Zero
        );
        BattleUnitState defender = BuildUnit(
            "counter_gate_defender",
            "enemy",
            new Vector2I(1, 0)
        );
        defender.SetKnownSkillLevelTyped(
            weaponAction.SkillId,
            1
        );
        defender.ReplaceCounterattackCapabilitiesTyped(
            new[]
            {
                new BattleCounterattackCapability(
                    "counter_gate_capability",
                    BattleCounterattackTriggerKind
                        .MeleeHitReceived,
                    SelectionPriority: 1,
                    ChancePercent: 50,
                    AttackRollBonus: 0,
                    WeaponActionDefinitionId:
                        weaponAction.SkillId
                ),
            }
        );
        var state = BattleTestFixture.BuildFlatState(
            "counter_weapon_gate",
            new Vector2I(3, 2)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { attacker },
            new[] { defender }
        );
        var chanceRoller =
            new CountingCounterattackChanceRoller();
        using var runtime = new BattleRuntimeModule(
            chanceRoller
        );
        runtime.setup(
            skill_definitions:
                new Dictionary<StringName, SkillDefinition>
                {
                    [weaponAction.SkillId] = weaponAction,
                }
        );
        runtime.SetupStateForTests(state);
        var resolver = new CountingFixedSuccessResolver();
        runtime.ConfigureDamageResolverForTests(resolver);

        int attackerHpBefore = attacker.GetCurrentHp();
        int defenderHpBefore = defender.GetCurrentHp();
        int defenderStaminaBefore =
            defender.GetCurrentStamina();
        BattleUnitReactionSnapshot reactionBefore =
            defender.CaptureReactionRawTyped();
        using var batch = new BattleEventBatch();
        BattleReactionRootTestHelper.ExecuteInReactionRoot(
            runtime,
            batch,
            () =>
            {
                using BattleLogicalAttackScope logicalAttack =
                    runtime.BeginLogicalAttack(
                        BattleAttackDeliveryKind.MeleeWeapon
                    );
                runtime._damage_resolver.ResolveAttackEffects(
                    attacker,
                    defender,
                    weaponAction.CombatProfile
                        .EffectDefinitions,
                    new AttackCheckInput(
                        skillId: weaponAction.SkillId
                    ),
                    new AttackContext
                    {
                        BattleState = state,
                        SkillId = weaponAction.SkillId,
                        EventBatch = batch,
                        Action = logicalAttack.Context,
                    }
                );
                logicalAttack.Complete();
            }
        );

        _test.True(
            defender.GetCurrentHp() < defenderHpBefore,
            "原攻击必须先正常命中反击者。"
        );
        _test.Eq(
            attacker.GetCurrentHp(),
            attackerHpBefore,
            "反击动作要求锤但当前装备剑时不得造成反击伤害。"
        );
        _test.Eq(
            defender.CaptureReactionRawTyped(),
            reactionBefore,
            "武器门槛失败不得消耗反应次数。"
        );
        _test.Eq(
            defender.GetCurrentStamina(),
            defenderStaminaBefore,
            "武器门槛失败不得消耗反击动作体力。"
        );
        _test.Eq(
            chanceRoller.CallCount,
            0,
            "武器门槛失败必须发生在 chance RNG 之前。"
        );
        _test.Eq(
            resolver.CallCount,
            1,
            "武器门槛失败时只应执行原攻击，不得进入反击伤害解析。"
        );
        _test.Eq(
            runtime._counterattackSystem.PendingCount,
            0,
            "门槛失败在 drain 后不得残留反击队列。"
        );
        _test.Eq(
            runtime._counterattackSystem.DedupeCount,
            0,
            "门槛失败在 root 结束后不得残留去重状态。"
        );
    }

    private void
        TestCriticalButUnappliedDoesNotGrantWeaponTraining()
    {
        SkillDefinition basicAttack =
            TestSkillDefinitionProjection.BuildSkill(
                "basic_attack",
                combatProfile:
                    TestSkillDefinitionProjection
                        .BuildCombatProfile(
                            "basic_attack",
                            new[]
                            {
                                TestSkillDefinitionProjection
                                    .BuildEffect(
                                        "damage",
                                        addWeaponDice: true
                                    ),
                            },
                            masteryTriggerMode:
                                "weapon_attack_quality",
                            masteryAmountMode:
                                "per_target_rank"
                        )
            );
        SkillDefinition swordTraining =
            TestSkillDefinitionProjection.BuildSkill(
                "sword_training",
                tags:
                    new[]
                    {
                        new StringName("weapon_training"),
                    }
            );
        BattleUnitState source = BuildUnit(
            "counter_mastery_source",
            "player",
            Vector2I.Zero
        );
        source.source_member_id = "counter_mastery_member";
        BattleUnitState target = BuildUnit(
            "counter_mastery_target",
            "enemy",
            new Vector2I(1, 0)
        );
        var result = new AttackEffectResolutionResult
        {
            AttackSuccess = true,
            AttackResolution =
                AttackResolutionKind.CriticalHit,
            CriticalHit = true,
            Applied = false,
            WeaponDamageDiceIsMax = true,
        };
        using var service = new BattleSkillMasteryService();
        BattleSkillMasteryGrant grant =
            service
                .BuildCounterattackWeaponTrainingMasteryGrant(
                    source,
                    target,
                    "sword_training",
                    result,
                    new Dictionary<
                        StringName,
                        SkillDefinition
                    >
                    {
                        [basicAttack.SkillId] =
                            basicAttack,
                        [swordTraining.SkillId] =
                            swordTraining,
                    }
                );

        _test.True(
            grant == null,
            "critical and max weapon dice cannot grant training when Applied is false."
        );
    }

    private void TestWeaponTrainingMappingAndRankAmounts()
    {
        SkillDefinition basicAttack =
            BuildBasicAttackMasteryPolicy();
        SkillDefinition swordTraining =
            BuildWeaponTraining("sword_training");
        SkillDefinition bowTraining =
            BuildWeaponTraining("bow_training");
        SkillDefinition unarmedTraining =
            BuildWeaponTraining("unarmed_training");
        var definitions =
            new Dictionary<StringName, SkillDefinition>
            {
                [basicAttack.SkillId] = basicAttack,
                [swordTraining.SkillId] = swordTraining,
                [bowTraining.SkillId] = bowTraining,
                [unarmedTraining.SkillId] =
                    unarmedTraining,
            };
        var qualityResult =
            new AttackEffectResolutionResult
            {
                AttackSuccess = true,
                AttackResolution =
                    AttackResolutionKind.CriticalHit,
                CriticalHit = true,
                Applied = true,
            };
        using var service = new BattleSkillMasteryService();
        (
            StringName profileKind,
            StringName family,
            StringName rangeType,
            StringName expectedSkillId
        )[] weaponCases =
        [
            (
                "equipped",
                "sword",
                "melee",
                "sword_training"
            ),
            (
                "equipped",
                "bow",
                "ranged",
                "bow_training"
            ),
            (
                "unarmed",
                "unarmed",
                "melee",
                "unarmed_training"
            ),
        ];
        foreach (var weaponCase in weaponCases)
        {
            BattleUnitState source = BuildUnit(
                $"mapping_{weaponCase.family}",
                "player",
                Vector2I.Zero
            );
            source.source_member_id =
                $"member_{weaponCase.family}";
            SetWeaponProjection(
                source,
                weaponCase.profileKind,
                weaponCase.family,
                weaponCase.rangeType
            );
            StringName resolvedSkillId =
                service.ResolveWeaponTrainingSkillId(
                    source
                );
            _test.Eq(
                resolvedSkillId,
                weaponCase.expectedSkillId,
                $"{weaponCase.family} must map to its weapon-training skill."
            );
            for (int rank = 1; rank <= 3; rank++)
            {
                BattleUnitState target = BuildUnit(
                    $"rank_{rank}_{weaponCase.family}",
                    "enemy",
                    new Vector2I(1, 0)
                );
                target.attribute_snapshot.SetValue(
                    "fortune_mark_target",
                    rank - 1
                );
                BattleSkillMasteryGrant grant =
                    service
                        .BuildCounterattackWeaponTrainingMasteryGrant(
                            source,
                            target,
                            resolvedSkillId,
                            qualityResult,
                            definitions
                        );
                _test.True(
                    grant != null,
                    $"{weaponCase.family} rank {rank} quality counterattack must grant training."
                );
                _test.Eq(
                    grant?.Amount ?? 0,
                    rank,
                    $"{weaponCase.family} must preserve normal/elite/boss 1/2/3 mastery amounts."
                );
            }
        }
    }

    private CharacterMasteryChangeFact
        AssertSingleWeaponTrainingMasteryChange(
            BattleEventBatch batch,
            StringName memberId,
            StringName expectedSkillId,
            int expectedAmount
        )
    {
        CharacterMasteryChangeFact matched = null;
        int matchCount = 0;
        foreach (
            CharacterProgressionDelta delta
                in batch?.ProgressionDeltasTyped
                    ?? Array.Empty<CharacterProgressionDelta>()
        )
        {
            if (delta == null || delta.member_id != memberId)
                continue;
            foreach (
                CharacterMasteryChangeFact change
                    in delta.MasteryChangesTyped
            )
            {
                if (
                    change == null
                    || change.SkillId != expectedSkillId
                )
                {
                    continue;
                }
                matchCount++;
                matched = change;
            }
        }
        _test.Eq(
            matchCount,
            1,
            "counterattack must write exactly one weapon-training mastery change."
        );
        if (matched == null)
            return null;
        _test.Eq(
            matched.MasteryAmount,
            expectedAmount,
            "weapon-training amount must use the basic-attack per-target-rank policy."
        );
        _test.Eq(
            matched.SourceType,
            new StringName("battle"),
            "weapon-training mastery source must remain battle."
        );
        _test.Eq(
            matched.SourceLabel,
            "战斗",
            "weapon-training source label must retain the canonical value."
        );
        _test.Eq(
            matched.ReasonText,
            "反击：武器高质量攻击",
            "weapon-training reason must retain the typed counterattack value."
        );
        return matched;
    }

    private static int CountMasteryChanges(
        BattleEventBatch batch,
        StringName memberId,
        StringName skillId
    )
    {
        int count = 0;
        foreach (
            CharacterProgressionDelta delta
                in batch?.ProgressionDeltasTyped
                    ?? Array.Empty<CharacterProgressionDelta>()
        )
        {
            if (delta == null || delta.member_id != memberId)
                continue;
            foreach (
                CharacterMasteryChangeFact change
                    in delta.MasteryChangesTyped
            )
            {
                if (change?.SkillId == skillId)
                    count++;
            }
        }
        return count;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            factionId,
            coord,
            currentHp: 30
        );
        unit.SetCurrentStamina(10);
        unit.attribute_snapshot.SetValue(
            AttributeService.ARMOR_CLASS,
            10
        );
        unit.attribute_snapshot.SetValue(
            AttributeService.ATTACK_BONUS,
            20
        );
        unit.RestoreWeaponProjectionForMutationSnapshotExact(
            BattleUnitWeaponProjectionSnapshot.Present(
                new BattleWeaponProjectionValues(
                    "equipped",
                    "test_sword",
                    "test_sword_profile",
                    "melee",
                    "sword",
                    "one_handed",
                    1,
                    new BattleWeaponDiceValues(
                        true,
                        1,
                        6,
                        0
                    ),
                    BattleWeaponDiceValues.PresentEmpty,
                    false,
                    false,
                    "physical_slash"
                )
            )
        );
        return unit;
    }

    private static void SetWeaponProjection(
        BattleUnitState unit,
        StringName profileKind,
        StringName family,
        StringName rangeType
    )
    {
        unit.RestoreWeaponProjectionForMutationSnapshotExact(
            BattleUnitWeaponProjectionSnapshot.Present(
                new BattleWeaponProjectionValues(
                    profileKind,
                    $"test_{family}",
                    $"test_{family}_profile",
                    rangeType,
                    family,
                    "one_handed",
                    rangeType == new StringName("ranged")
                        ? 6
                        : 1,
                    new BattleWeaponDiceValues(
                        true,
                        1,
                        6,
                        0
                    ),
                    BattleWeaponDiceValues.PresentEmpty,
                    false,
                    false,
                    "physical_slash"
                )
            )
        );
    }

    private static SkillDefinition BuildWeaponAction(
        StringName requiredWeaponFamily = default
    )
    {
        StringName skillId = "test_counter_weapon_action";
        IReadOnlyList<StringName> requiredWeaponFamilies =
            requiredWeaponFamily == new StringName("")
                ? Array.Empty<StringName>()
                : new[] { requiredWeaponFamily };
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            combatProfile:
                TestSkillDefinitionProjection.BuildCombatProfile(
                    skillId,
                    new[]
                    {
                        TestSkillDefinitionProjection.BuildEffect(
                            "damage",
                            addWeaponDice: true,
                            useWeaponPhysicalDamageTag: true,
                            diceCount: 1,
                            diceSides: 1,
                            damageTag: "physical_slash"
                        ),
                    },
                    targetMode: "unit",
                    targetTeamFilter: "enemy",
                    rangeValue: 1,
                    staminaCost: 2,
                    requiredWeaponFamilies:
                        requiredWeaponFamilies
                )
        );
    }

    private static SkillDefinition
        BuildBasicAttackMasteryPolicy() =>
            TestSkillDefinitionProjection.BuildSkill(
                "basic_attack",
                combatProfile:
                    TestSkillDefinitionProjection
                        .BuildCombatProfile(
                            "basic_attack",
                            new[]
                            {
                                TestSkillDefinitionProjection
                                    .BuildEffect(
                                        "damage",
                                        addWeaponDice: true
                                    ),
                            },
                            masteryTriggerMode:
                                "weapon_attack_quality",
                            masteryAmountMode:
                                "per_target_rank"
                        )
            );

    private static SkillDefinition BuildWeaponTraining(
        StringName skillId
    ) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            tags:
                new[]
                {
                    new StringName("weapon_training"),
                }
        );

    private sealed partial class CountingFixedSuccessResolver
        : FixedSuccessOneDamageResolver
    {
        internal int CallCount { get; private set; }

        internal override AttackEffectResolutionResult
            ResolveAttackEffects(
                BattleUnitState source_unit,
                BattleUnitState target_unit,
                IEnumerable<CombatEffectDefinition>
                    effect_definitions,
                AttackCheckInput attack_check,
                AttackContext attack_context
            )
        {
            CallCount++;
            AttackEffectResolutionResult result =
                base.ResolveAttackEffects(
                source_unit,
                target_unit,
                effect_definitions,
                attack_check,
                attack_context
            );
            if (CallCount == 3)
            {
                result.Applied = true;
                result.AttackSuccess = true;
                result.AttackResolution =
                    AttackResolutionKind.CriticalHit;
                result.CriticalHit = true;
            }
            return result;
        }
    }

    private sealed class CountingCounterattackChanceRoller
        : IBattleCounterattackChanceRoller
    {
        internal int CallCount { get; private set; }

        public int RollInclusive1To100()
        {
            CallCount++;
            return 1;
        }
    }

    private sealed partial class ThrowOnSecondAttackResolver
        : FixedSuccessOneDamageResolver
    {
        internal const string FailureMessage =
            "counterattack resolver business failure";

        private int _callCount;

        internal override AttackEffectResolutionResult
            ResolveAttackEffects(
                BattleUnitState source_unit,
                BattleUnitState target_unit,
                IEnumerable<CombatEffectDefinition>
                    effect_definitions,
                AttackCheckInput attack_check,
                AttackContext attack_context
            )
        {
            _callCount++;
            if (_callCount == 2)
                throw new InvalidOperationException(FailureMessage);
            return base.ResolveAttackEffects(
                source_unit,
                target_unit,
                effect_definitions,
                attack_check,
                attack_context
            );
        }
    }

    private sealed class RecordingMasteryGateway
        : IBattleRuntimeCharacterGateway
    {
        private readonly Dictionary<
            (StringName MemberId, StringName SkillId),
            int
        > _masteryTotals = new();

        internal int SkillUsedEvents { get; private set; }

        internal int GetTotalMastery(
            StringName memberId,
            StringName skillId
        ) =>
            _masteryTotals.TryGetValue(
                (memberId, skillId),
                out int amount
            )
                ? amount
                : 0;

        public PartyState GetPartyState() => null;

        public IReadOnlyDictionary<StringName, ItemDefinition>
            GetItemDefsTyped() =>
                new Dictionary<StringName, ItemDefinition>();

        public bool HasItemDefCatalog() => false;

        public ItemDefinition GetItemDef(
            StringName item_id
        ) => null;

        public PartyMemberState GetMemberState(
            StringName member_id
        ) => null;

        public AttributeSnapshot
            GetMemberAttributeSnapshotForEquipmentView(
                StringName member_id,
                EquipmentState equipment_view
            ) => null;

        public WeaponProjection
            GetMemberWeaponProjectionForEquipmentViewTyped(
                StringName member_id,
                EquipmentState equipment_view
            ) => new();

        public BattleEffectiveTraitProjection
            BuildEffectiveTraitProjectionForEquipmentView(
                StringName member_id,
                EquipmentState equipment_view
            ) => BattleEffectiveTraitProjection.Empty;

        public PassiveSourceContext BuildPassiveSourceContext(
            StringName member_id,
            UnitProgress progression_state
        ) => null;

        public CharacterProgressionDelta PromoteProfession(
            StringName member_id,
            StringName profession_id,
            PromotionSelectionData selection
        ) => new() { member_id = member_id };

        public BattleResourceCommitResult
            CommitBattleResources(
                StringName member_id,
                int current_hp,
                int current_mp,
                int current_aura
            ) => BattleResourceCommitResult.Success(member_id);

        public ContingencyConsumedCommitResult
            ValidateContingencyConsumedSetups(
                StringName member_id,
                IReadOnlyCollection<StringName>
                    consumed_setup_ids
            ) =>
                ContingencyConsumedCommitResult.Success(
                    member_id,
                    consumed_setup_ids?.Count ?? 0
                );

        public ContingencyConsumedCommitResult
            CommitContingencyConsumedSetups(
                StringName member_id,
                IReadOnlyCollection<StringName>
                    consumed_setup_ids
            ) =>
                ContingencyConsumedCommitResult.Success(
                    member_id,
                    consumed_setup_ids?.Count ?? 0
                );

        public void CommitBattleDeath(
            StringName member_id
        ) { }

        public int FlushAfterBattle() => (int)Error.Ok;

        public CharacterProgressionDelta GrantBattleMastery(
            StringName member_id,
            StringName skill_id,
            int amount
        ) =>
            RecordGrant(
                member_id,
                skill_id,
                amount,
                "battle",
                "战斗",
                ""
            );

        public CharacterProgressionDelta
            GrantSkillMasteryFromSource(
                StringName member_id,
                StringName skill_id,
                int amount,
                StringName source_type,
                string source_label,
                string reason_text,
                bool emit_achievement_event
            ) =>
                RecordGrant(
                    member_id,
                    skill_id,
                    amount,
                    source_type,
                    source_label,
                    reason_text
                );

        public IReadOnlyList<StringName>
            RecordAchievementEvent(
                StringName member_id,
                StringName event_type,
                int amount
            ) => Array.Empty<StringName>();

        public IReadOnlyList<StringName>
            RecordAchievementEvent(
                StringName member_id,
                StringName event_type,
                int amount,
                StringName subject_id,
                GDictionary meta
            )
        {
            if (event_type == "skill_used")
                SkillUsedEvents += amount;
            return Array.Empty<StringName>();
        }

        public PendingCharacterReward
            BuildPendingSkillMasteryReward(
                StringName member_id,
                StringName source_type,
                string source_label,
                IEnumerable<PendingCharacterRewardEntry>
                    entry_options,
                string summary_text
            ) => null;

        private CharacterProgressionDelta RecordGrant(
            StringName memberId,
            StringName skillId,
            int amount,
            StringName sourceType,
            string sourceLabel,
            string reasonText
        )
        {
            var key = (memberId, skillId);
            _masteryTotals.TryGetValue(
                key,
                out int existing
            );
            _masteryTotals[key] = existing + amount;
            var change = new CharacterMasteryChangeFact(
                skillId,
                skillId.ToString(),
                amount,
                sourceType,
                sourceLabel,
                reasonText
            );
            var delta = new CharacterProgressionDelta
            {
                member_id = memberId,
            };
            delta.AddMasteryChange(change);
            return delta;
        }
    }
}
