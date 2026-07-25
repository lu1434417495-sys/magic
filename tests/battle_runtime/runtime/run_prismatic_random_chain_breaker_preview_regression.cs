using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_prismatic_random_chain_breaker_preview_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestResult exitCode = null;
        try
        {
            AssertRandomChainBreakerPreview(maxHitsPerTarget: 2, expectFollowUpImpact: true);
            AssertRandomChainBreakerPreview(maxHitsPerTarget: 1, expectFollowUpImpact: false);
            exitCode = _test.Finish("Prismatic random-chain breaker preview regression");
        }
        finally
        {
            RequestTestExit(
                exitCode
                    ?? _test.Finish(
                        "Prismatic random-chain breaker preview regression",
                        1
                    )
            );
        }
    }

    private void AssertRandomChainBreakerPreview(
        int maxHitsPerTarget,
        bool expectFollowUpImpact
    )
    {
        SkillDefinition randomBreaker = BuildRandomChainMagicMissile(maxHitsPerTarget);
        var skillDefinitions = new Dictionary<StringName, SkillDefinition>
        {
            [randomBreaker.SkillId] = randomBreaker,
        };
        var runtime = new BattleRuntimeModule();
        BattleState state = null;
        BattleCommand command = null;
        BattlePreview firstPreview = null;
        BattlePreview secondPreview = null;
        BattleEventBatch executionBatch = null;
        try
        {
            runtime.setup(
                skill_definitions: skillDefinitions,
                barrier_profile_definitions: BarrierDefinitionTestContent.LoadValidated()
            );
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            state = BattleTestFixture.BuildFlatState(
                $"prismatic_random_chain_breaker_preview_regression_{maxHitsPerTarget}",
                new Vector2I(7, 5)
            );
            BattleUnitState sphereOwner = BuildUnit(
                "sphere_owner",
                "法球施法者",
                "player",
                new Vector2I(2, 2)
            );
            BattleUnitState source = BuildUnit(
                "random_breaker_source",
                "随机破层者",
                "enemy",
                new Vector2I(5, 2)
            );
            LearnSkill(source, randomBreaker.SkillId);
            BattleTestFixture.InstallUnits(
                state,
                new[] { sphereOwner },
                new[] { source }
            );
            state.active_unit_id = source.unit_id;
            runtime.SetupStateForTests(state);

            BattleLayeredBarrierApplyResult applyResult =
                runtime._layered_barrier_service.ApplyLayeredBarrierEffectResult(
                    sphereOwner,
                    sphereOwner,
                    BuildSphereSkill(),
                    BuildSphereEffect(),
                    batch: null
                );
            _test.True(applyResult.Applied, "测试法球应成功建立。");
            SetOnlyRemainingLayer(state, applyResult.BarrierInstanceId, "blue");

            BattleStateReadView stateView = state.AsReadView();
            BattleBarrierInteractionResult directPreview =
                runtime._layered_barrier_service.PreviewSkillBarrierInteractionResult(
                    stateView.GetUnit(source.unit_id),
                    stateView.GetUnit(sphereOwner.unit_id),
                    randomBreaker,
                    randomBreaker.CombatProfile.EffectDefinitions
                );
            _test.True(
                directPreview.Blocked && directPreview.WouldBreakLayer,
                "随机链首次跨界命中应以结构化事实报告会破解蓝层且本次被挡。"
            );
            _test.Eq(
                ActiveLayerId(state, applyResult.BarrierInstanceId),
                new StringName("blue"),
                "只读屏障判定不得修改正式蓝层。"
            );

            command = new BattleCommand
            {
                CommandKind = BattleCommandKind.Skill,
                unit_id = source.unit_id,
                skill_entry_id = BattleSkillEntryIds.KnownSkill(randomBreaker.SkillId),
                skill_id = randomBreaker.SkillId,
            };
            firstPreview = runtime.PreviewCommand(command);
            _test.True(firstPreview?.allowed == true, "随机破层链应保持可施放。");
            _test.True(
                firstPreview?.RandomChainCandidateUnitIdsTyped.Contains(
                    sphereOwner.unit_id
                ) == true,
                "随机链必须保留真实抽样候选池。"
            );
            _test.Eq(
                firstPreview?.TargetUnitIdsTyped.Count ?? -1,
                0,
                "随机链预览不得伪造已确定的目标。"
            );
            bool hasImpactCandidate =
                firstPreview?.RandomChainImpactCandidateUnitIdsTyped.Contains(
                    sphereOwner.unit_id
                ) == true;
            if (expectFollowUpImpact)
            {
                _test.True(
                    hasImpactCandidate && firstPreview.DamagePreviewTyped.HasValue,
                    "首击破蓝层后，同一随机链的后续命中应把目标保留为可能受影响候选。"
                );
            }
            else
            {
                _test.True(
                    !hasImpactCandidate && !firstPreview.DamagePreviewTyped.HasValue,
                    "只有一个候选且每目标只命中一次时，破层首击后没有后续命中，不应伪造 impact candidate 或伤害预览。"
                );
            }
            _test.Eq(
                ActiveLayerId(state, applyResult.BarrierInstanceId),
                new StringName("blue"),
                "随机链预览模拟破层不得写入正式屏障状态。"
            );

            secondPreview = runtime.PreviewCommand(command);
            _test.Eq(
                secondPreview?.RandomChainImpactCandidateUnitIdsTyped.Contains(
                    sphereOwner.unit_id
                ) == true,
                expectFollowUpImpact,
                "重复预览应从正式状态重新模拟并保持同一后续可影响判断。"
            );
            _test.Eq(
                ActiveLayerId(state, applyResult.BarrierInstanceId),
                new StringName("blue"),
                "重复预览不得累积破层副作用。"
            );

            int hpBefore = sphereOwner.GetCurrentHp();
            executionBatch = runtime.IssueCommand(command);
            _test.Eq(
                ActiveLayerId(state, applyResult.BarrierInstanceId),
                new StringName(""),
                "正式随机链的首个跨界命中应实际破解蓝层。"
            );
            if (expectFollowUpImpact)
            {
                _test.True(
                    sphereOwner.GetCurrentHp() < hpBefore,
                    "每目标允许两次命中时，正式随机链应在首击破层后继续并造成伤害。"
                );
            }
            else
            {
                _test.Eq(
                    sphereOwner.GetCurrentHp(),
                    hpBefore,
                    "每目标只允许一次命中时，正式随机链应止于被屏障消耗的破层首击。"
                );
            }
        }
        finally
        {
            executionBatch?.Dispose();
            BattleTestFixture.DisposeBattlePreview(firstPreview);
            BattleTestFixture.DisposeBattlePreview(secondPreview);
            BattleTestFixture.DisposeBattleCommand(command);
            BattleTestFixture.DisposeBattleFixture(runtime, state);
        }
    }

    private static SkillDefinition BuildRandomChainMagicMissile(int maxHitsPerTarget)
    {
        StringName skillId = "mage_arcane_missile";
        CombatEffectDefinition damage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 10,
            damageTag: "force"
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "随机奥术飞弹",
            tags: new[] { new StringName("magic"), new StringName("test") },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { damage },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangePattern: "fixed",
                rangeValue: 10,
                targetSelectionMode: "random_chain",
                maxHitsPerTarget: maxHitsPerTarget,
                projectileKind: "magical"
            )
        );
    }

    private static SkillDefinition BuildSphereSkill() =>
        TestSkillDefinitionProjection.BuildSkill(
            "mage_prismatic_sphere",
            displayName: "虹光法球",
            tags: new[] { new StringName("magic") }
        );

    private static CombatEffectDefinition BuildSphereEffect() =>
        TestSkillDefinitionProjection.BuildEffect(
            "layered_barrier",
            durationTu: 120,
            saveDc: 15,
            saveDcMode: "static",
            saveAbility: "willpower",
            saveTag: "magic",
            parameters: new Dictionary<string, object>
            {
                ["area_pattern"] = "diamond",
                ["profile_id"] = "prismatic_sphere",
                ["radius_cells"] = 2,
            }
        );

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            factionId,
            coord,
            currentAp: 2,
            currentHp: 120
        );
        unit.display_name = displayName;
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ArmorClass),
            AttributeService.BASE_ARMOR_CLASS
        );
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        return unit;
    }

    private static void LearnSkill(BattleUnitState unitState, StringName skillId)
    {
        unitState.AddKnownActiveSkill(skillId);
        unitState.SetKnownSkillLevelTyped(skillId, 1);
    }

    private static void SetOnlyRemainingLayer(
        BattleState state,
        StringName barrierKey,
        StringName remainingLayerId
    )
    {
        if (
            state?.TryGetLayeredBarrierField(
                barrierKey,
                out BattleBarrierInstanceState barrier
            ) != true
        )
        {
            return;
        }
        List<BattleBarrierLayerState> layers = barrier.GetLayersTyped();
        foreach (BattleBarrierLayerState layer in layers)
        {
            if (layer != null)
                layer.Broken = layer.LayerId != remainingLayerId;
        }
        barrier.SetLayers(layers);
        state.PutLayeredBarrierField(barrierKey, barrier);
    }

    private static StringName ActiveLayerId(BattleState state, StringName barrierKey)
    {
        if (
            state?.TryGetLayeredBarrierField(
                barrierKey,
                out BattleBarrierInstanceState barrier
            ) != true
        )
        {
            return "";
        }
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer != null && !layer.Broken)
                return layer.LayerId;
        }
        return "";
    }
}
