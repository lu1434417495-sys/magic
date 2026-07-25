using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_warrior_repeat_attack_mastery_bonus_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private ContentSnapshot _contentSnapshot;

    public override void _Initialize()
    {
        ProcessFrame += RunOnFirstProcessFrame;
    }

    private void RunOnFirstProcessFrame()
    {
        ProcessFrame -= RunOnFirstProcessFrame;
        _contentSnapshot = GameSessionTestFactory.GetProcessSnapshot();
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        TestRepeatAttackResolverUsesTypedResourceCosts();
        TestRepeatAttackMasteryBonusStartsOnFifthStageEntry();
        TestWeaponAttackQualityReadsWeaponDiceMaxReasonFromResultPayload();
        TestGuardMasteryGrantReadsSkillDefFromTypedDictionaryKey();
        TestGuardLevelProgressionMatchesDescription();

        return _test.Finish("Warrior repeat attack mastery bonus regression");
    }

    private void TestRepeatAttackResolverUsesTypedResourceCosts()
    {
        using RepeatAttackFixture fixture = BuildRepeatAttackFixture(new[] { true });
        SkillDefinition skillDefinition = BuildRepeatAttackSkillDefinition(
            "combo_mastery_stage_test",
            apCost: 2,
            mpCost: 3,
            staminaCost: 4,
            auraCost: 5
        );

        CombatSkillResourceCosts costs = fixture.Resolver._resolve_effective_skill_costs(
            fixture.ActiveUnit,
            skillDefinition
        );
        _test.Eq(costs.ApCost, 2, "repeat attack typed costs 应保留 AP。");
        _test.Eq(costs.MpCost, 3, "repeat attack typed costs 应保留 MP。");
        _test.Eq(costs.StaminaCost, 4, "repeat attack typed costs 应保留 Stamina。");
        _test.Eq(costs.AuraCost, 5, "repeat attack typed costs 应保留 Aura。");

        _test.Eq(
            fixture.Resolver._get_repeat_attack_base_resource_cost(
                fixture.ActiveUnit,
                skillDefinition,
                CombatResourceKind.Aura
            ),
            5,
            "repeat attack base resource cost 应直接来自 typed Aura cost。"
        );
    }

    private void TestRepeatAttackMasteryBonusStartsOnFifthStageEntry()
    {
        using RepeatAttackFixture missFixture = BuildRepeatAttackFixture(
            new[] { true, true, true, true, false }
        );
        using var missBatch = new BattleEventBatch();
        bool missExecuted = missFixture.Resolver.ApplyRepeatAttackSkillResult(
            missFixture.ActiveUnit,
            missFixture.TargetUnit,
            missFixture.SkillDefinition,
            missFixture.EffectDefinitions,
            missFixture.RepeatEffectDefinition,
            missBatch
        );
        _test.True(missExecuted, "连击段数熟练度回归前置：应至少执行到第五段。");
        _test.Eq(
            missFixture.DamageResolver.call_count,
            5,
            "连击段数熟练度回归应固定进入第五段后 miss。"
        );
        _test.Eq(
            missFixture.MasteryService.ResolveActiveSkillMasteryAmount(),
            0,
            "连击熟练度 bonus 必须在对应段命中后发放，第五段 miss 不应给 bonus。"
        );

        using RepeatAttackFixture hitFixture = BuildRepeatAttackFixture(
            new[] { true, true, true, true, true, false }
        );
        using var hitBatch = new BattleEventBatch();
        bool hitExecuted = hitFixture.Resolver.ApplyRepeatAttackSkillResult(
            hitFixture.ActiveUnit,
            hitFixture.TargetUnit,
            hitFixture.SkillDefinition,
            hitFixture.EffectDefinitions,
            hitFixture.RepeatEffectDefinition,
            hitBatch
        );
        _test.True(hitExecuted, "连击段数熟练度回归前置：命中夹具应执行。");
        _test.Eq(
            hitFixture.DamageResolver.call_count,
            6,
            "命中夹具应在第五段命中后继续进入第六段 miss。"
        );
        _test.Eq(
            hitFixture.MasteryService.ResolveActiveSkillMasteryAmount(),
            1,
            "第五段命中后应发放 1 点连击段数 bonus。"
        );
    }

    private RepeatAttackFixture BuildRepeatAttackFixture(bool[] stageSuccesses)
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();
        var damageResolver = new StageOutcomeDamageResolver();
        foreach (bool stageSuccess in stageSuccesses)
        {
            damageResolver.stage_successes.Add(stageSuccess);
        }
        runtime.ConfigureDamageResolverForTests(damageResolver);

        var masteryService = new BattleSkillMasteryService();
        var resolver = new BattleRepeatAttackResolver();
        resolver.Setup(runtime, masteryService);

        BattleUnitState activeUnit = BuildUnit("combo_mastery_user", new Vector2I(1, 1), 2);
        activeUnit.source_member_id = "hero";
        activeUnit.SetCurrentAura(99);
        activeUnit.SetKnownActiveSkillIds(
            new[] { new StringName("combo_mastery_stage_test") }
        );
        activeUnit.SetKnownSkillLevelTyped("combo_mastery_stage_test", 1);

        BattleUnitState targetUnit = BuildUnit("combo_mastery_target", new Vector2I(2, 1), 2);
        targetUnit.faction_id = "enemy";
        targetUnit.SetCurrentHp(999);
        targetUnit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 999);

        SkillDefinition skillDefinition = BuildRepeatAttackSkillDefinition(
            "combo_mastery_stage_test"
        );
        CombatEffectDefinition repeatEffectDefinition = skillDefinition.CombatProfile.EffectDefinitions[1];

        return new RepeatAttackFixture
        {
            Runtime = runtime,
            DamageResolver = damageResolver,
            MasteryService = masteryService,
            Resolver = resolver,
            ActiveUnit = activeUnit,
            TargetUnit = targetUnit,
            SkillDefinition = skillDefinition,
            EffectDefinitions = skillDefinition.CombatProfile.EffectDefinitions,
            RepeatEffectDefinition = repeatEffectDefinition,
        };
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord, int currentAp)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
        }.WithCombatResourcesForTest(
            hp: 40,
            mp: 4,
            stamina: 60,
            aura: 0,
            ap: currentAp,
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 40);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 60);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 8);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), Mathf.Max(currentAp, 1));
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 80);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 5);
        return unit;
    }

    private void TestWeaponAttackQualityReadsWeaponDiceMaxReasonFromResultPayload()
    {
        var service = new BattleSkillMasteryService();
        BattleUnitState source = BuildMasteryUnit("mastery_weapon_source", "player", "hero");
        BattleUnitState target = BuildMasteryUnit("mastery_weapon_target", "enemy");
        SkillDefinition skillDefinition = BuildMasterySkill(
            "weapon_quality_contract",
            "weapon_attack_quality"
        );

        var result = new GDictionary
        {
            ["attack_success"] = true,
            ["damage"] = 5,
            ["damage_events"] = new Godot.Collections.Array
            {
                new GDictionary
                {
                    ["weapon_damage_dice_is_max"] = true,
                    ["weapon_damage_dice_is_max_reason"] = "weapon_dice_max",
                    ["hp_damage"] = 5,
                },
            },
        };

        service.RecordTargetResult(source, target, skillDefinition, result);

        _test.Eq(
            service.ResolveActiveSkillMasteryAmount(),
            1,
            "weapon_attack_quality 应继续通过 payload 中的 weapon_dice_max reason 触发熟练度。"
        );
    }

    private void TestGuardMasteryGrantReadsSkillDefFromTypedDictionaryKey()
    {
        var service = new BattleSkillMasteryService();
        BattleUnitState attacker = BuildMasteryUnit("guard_attacker", "enemy");
        BattleUnitState target = BuildMasteryUnit("guard_target", "player", "hero");
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "guarding",
            }
        );

        var fullyMitigatedHitResult = new AttackEffectResolutionResult
        {
            AttackSuccess = true,
            Damage = 0,
        };
        var effectDefs = new List<CombatEffectDefinition>
        {
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                damageTag: "physical_slash"
            ),
        };
        var skillDefinitions = new Dictionary<StringName, SkillDefinition>
        {
            [new StringName("warrior_guard")] = BuildMasterySkill(
                "warrior_guard",
                "incoming_physical_hit"
            ),
        };

        BattleSkillMasteryGrant grant = service.BuildGuardMasteryGrantFromIncomingHitTyped(
            attacker,
            target,
            effectDefs,
            fullyMitigatedHitResult,
            skillDefinitions
        );

        _test.True(
            grant != null,
            "成功物理命中即使被完全减伤，也应从 typed skill dictionary 授予格挡精通。"
        );
        if (grant != null)
        {
            _test.Eq(
                grant.SkillId.ToString(),
                "warrior_guard",
                "guard mastery grant 应保留 warrior_guard skill id。"
            );
            _test.Eq(grant.Amount, 1, "普通敌方命中 guarding 目标时应给予 1 点熟练度。");
            _test.Eq(grant.MemberId.ToString(), "hero", "guard mastery grant 应归属给被保护的成员。");
        }

        var missedResult = new AttackEffectResolutionResult
        {
            AttackSuccess = false,
            Damage = 0,
        };
        _test.True(
            service.BuildGuardMasteryGrantFromIncomingHitTyped(
                attacker,
                target,
                effectDefs,
                missedResult,
                skillDefinitions
            ) == null,
            "未命中的攻击不应授予格挡精通。"
        );

        var magicalEffectDefs = new List<CombatEffectDefinition>
        {
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                damageTag: "force"
            ),
        };
        _test.True(
            service.BuildGuardMasteryGrantFromIncomingHitTyped(
                attacker,
                target,
                magicalEffectDefs,
                fullyMitigatedHitResult,
                skillDefinitions
            ) == null,
            "非物理命中不应授予格挡精通。"
        );
    }

    private void TestGuardLevelProgressionMatchesDescription()
    {
        _contentSnapshot.Skills.TryGetValue(
            "warrior_guard",
            out SkillDefinition guardDefinition
        );
        _test.True(guardDefinition?.CombatProfile != null, "格挡正式技能配置应可加载。");
        if (guardDefinition?.CombatProfile == null)
        {
            return;
        }

        _test.True(
            guardDefinition.LevelDescriptionTemplate.StartsWith("格挡姿态："),
            "格挡等级模板应使用中文姿态名称。"
        );
        _test.True(
            !guardDefinition.LevelDescriptionTemplate.Contains("guarding"),
            "格挡等级模板不应残留内部状态 id。"
        );

        int[] expectedGuardPower = { 1, 1, 1, 2, 2, 3 };
        int[] expectedDurationTu = { 40, 50, 60, 60, 60, 60 };
        int[] expectedStaminaCost = { 50, 50, 40, 40, 35, 30 };
        for (int level = 0; level <= 5; level++)
        {
            CombatEffectDefinition guarding = FindActiveStatusEffect(
                guardDefinition.CombatProfile.EffectDefinitions,
                "guarding",
                level
            );
            _test.True(guarding != null, $"格挡 {level}级 应有 guarding 效果。");
            if (guarding != null)
            {
                _test.Eq(
                    guarding.Power,
                    expectedGuardPower[level],
                    $"格挡 {level}级 减伤强度应匹配等级说明。"
                );
                _test.Eq(
                    guarding.DurationTu,
                    expectedDurationTu[level],
                    $"格挡 {level}级 持续时间应匹配等级说明。"
                );
            }

            CombatEffectDefinition slow = FindActiveStatusEffect(
                guardDefinition.CombatProfile.EffectDefinitions,
                "slow",
                level
            );
            _test.Eq(
                slow != null,
                level <= 3,
                $"格挡 {level}级 移动力惩罚启用状态应匹配等级说明。"
            );
            if (slow != null)
            {
                _test.Eq(slow.Power, 1, $"格挡 {level}级 移动力惩罚应为1。");
                _test.Eq(
                    slow.DurationTu,
                    expectedDurationTu[level],
                    $"格挡 {level}级 移动力惩罚持续时间应跟随姿态。"
                );
            }

            SkillEffectiveCombatDefinition effectiveDefinition =
                SkillEffectiveCombatDefinition.BuildUncached(guardDefinition, level);
            _test.Eq(
                effectiveDefinition.ResourceCosts.StaminaCost,
                expectedStaminaCost[level],
                $"格挡 {level}级 体力消耗应匹配等级说明。"
            );
            _test.Eq(
                guardDefinition.CombatProfile.CooldownTu,
                120,
                $"格挡 {level}级 冷却应保持120TU。"
            );
        }
    }

    private static CombatEffectDefinition FindActiveStatusEffect(
        IReadOnlyList<CombatEffectDefinition> effectDefinitions,
        StringName statusId,
        int skillLevel
    )
    {
        foreach (CombatEffectDefinition effectDefinition in effectDefinitions)
        {
            if (
                effectDefinition?.EffectType == "status"
                && effectDefinition.StatusId == statusId
                && skillLevel >= Mathf.Max(effectDefinition.MinSkillLevel, 0)
                && (
                    effectDefinition.MaxSkillLevel < 0
                    || skillLevel <= effectDefinition.MaxSkillLevel
                )
            )
            {
                return effectDefinition;
            }
        }
        return null;
    }

    private static BattleUnitState BuildMasteryUnit(
        StringName unitId,
        StringName factionId,
        StringName memberId = default
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            source_member_id = memberId,
        }.WithCombatResourcesForTest(
            hp: 30,
            isAlive: true
        );
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        return unit;
    }

    private static SkillDefinition BuildRepeatAttackSkillDefinition(
        StringName skillId,
        int apCost = 0,
        int mpCost = 0,
        int staminaCost = 0,
        int auraCost = 0
    )
    {
        CombatEffectDefinition damageEffect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            power: 0
        );
        CombatEffectDefinition repeatEffect = TestSkillDefinitionProjection.BuildEffect(
            "repeat_attack_until_fail",
            parameters: new Dictionary<string, object>
            {
                ["cost_resource"] = "aura",
                ["follow_up_fixed_cost"] = 0,
                ["follow_up_attack_penalty"] = 0,
            }
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "连击熟练度段数测试",
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { damageEffect, repeatEffect },
                apCost: apCost,
                mpCost: mpCost,
                staminaCost: staminaCost,
                auraCost: auraCost,
                masteryTriggerMode: "damage_dealt",
                masteryAmountMode: "per_target_rank"
            )
        );
    }

    private static SkillDefinition BuildMasterySkill(StringName skillId, string masteryTriggerMode)
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId.ToString(),
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                masteryTriggerMode: masteryTriggerMode,
                masteryAmountMode: "per_target_rank"
            )
        );
    }

    private sealed class RepeatAttackFixture : System.IDisposable
    {
        private bool _disposed;

        public BattleRuntimeModule Runtime;
        public StageOutcomeDamageResolver DamageResolver;
        internal BattleSkillMasteryService MasteryService;
        public BattleRepeatAttackResolver Resolver;
        public BattleUnitState ActiveUnit;
        public BattleUnitState TargetUnit;
        public SkillDefinition SkillDefinition;
        public IReadOnlyList<CombatEffectDefinition> EffectDefinitions;
        public CombatEffectDefinition RepeatEffectDefinition;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            Resolver?.Setup(null, null);
            MasteryService?.Dispose();
            BattleTestFixture.DisposeBattleFixture(
                Runtime,
                null,
                ActiveUnit,
                TargetUnit
            );

            Runtime = null;
            DamageResolver = null;
            MasteryService = null;
            Resolver = null;
            ActiveUnit = null;
            TargetUnit = null;
            SkillDefinition = null;
            EffectDefinitions = null;
            RepeatEffectDefinition = null;
        }
    }
}
