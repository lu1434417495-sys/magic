using System;
using System.Collections.Generic;
using Godot;

public partial class run_prismatic_sphere_special_entry_regression : LifecycleTestSceneTree
{
    private const string ChargeVariantId = "charge_line";
    private readonly TestHarness _test = new();
    private IReadOnlyDictionary<StringName, BarrierProfileDefinition> _barrierProfileDefinitions;

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestResult exitCode = null;
        try
        {
            _barrierProfileDefinitions = BarrierDefinitionTestContent.LoadValidated();
            TestChargeMovementTriggersBarrierPassage();
            TestChargePushTriggersBarrierPassage();
            TestChargePathAoePreviewAndExecutionClipAtBarrier();
            TestRepeatAttackEntryPointsRespectBarrier();
            TestChainDamageChecksEachJumpAgainstBarrier();
            exitCode = _test.Finish("Prismatic sphere special entry regression");
        }
        finally
        {
            RequestTestExit(
                exitCode ?? _test.Finish("Prismatic sphere special entry regression", 1)
            );
        }
    }

    private void TestChargeMovementTriggersBarrierPassage()
    {
        SkillDefinition chargeSkill = BuildChargeSkill("test_prismatic_charge_move", false);
        using SpecialFixture fixture = BuildFixture(chargeSkill);
        MarkOnlyRedLayerActive(fixture.State);
        int hpBefore = fixture.Source.GetCurrentHp();
        BattleCommand command = BuildGroundCommand(
            fixture.Source,
            chargeSkill.SkillId,
            new Vector2I(3, 2),
            ChargeVariantId
        );
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(
            fixture.Source.GetAnchorCoord(),
            new Vector2I(3, 2),
            $"冲锋者通过色层后应继续完成允许的位移。 logs={string.Join(" | ", batch.LogLinesTyped)}"
        );
        _test.True(
            fixture.Source.GetCurrentHp() < hpBefore,
            "冲锋跨越虹光法球边界时应触发当前红层伤害，无论豁免是否将其减半。"
        );
        _test.True(
            LogsContain(batch.LogLinesTyped, "穿过虹光法球"),
            "冲锋跨界应记录统一的屏障穿越日志。"
        );
    }

    private void TestChargePushTriggersBarrierPassage()
    {
        SkillDefinition chargeSkill = BuildChargeSkill("test_prismatic_charge_push", false);
        using SpecialFixture fixture = BuildFixture(chargeSkill);
        MarkOnlyRedLayerActive(fixture.State);
        BattleUnitState pushed = fixture.AddUnit(
            BuildUnit("charge_pushed", "被顶开的单位", "player", new Vector2I(5, 2))
        );
        fixture.AddUnit(BuildUnit("charge_side_up", "上侧阻挡", "player", new Vector2I(5, 1)));
        fixture.AddUnit(BuildUnit("charge_side_down", "下侧阻挡", "player", new Vector2I(5, 3)));
        fixture.RebindState();
        int hpBefore = pushed.GetCurrentHp();
        BattleCommand command = BuildGroundCommand(
            fixture.Source,
            chargeSkill.SkillId,
            new Vector2I(5, 2),
            ChargeVariantId
        );
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(
            pushed.GetAnchorCoord(),
            new Vector2I(4, 2),
            $"冲锋前推应把目标推进法球边界格。 logs={string.Join(" | ", batch.LogLinesTyped)}"
        );
        _test.True(
            pushed.GetCurrentHp() < hpBefore,
            "被冲锋推过虹光法球边界的单位也应触发当前红层伤害，无论豁免是否将其减半。"
        );
        _test.True(
            LogsContain(batch.LogLinesTyped, "被顶开的单位 穿过虹光法球"),
            "被推单位的跨界应由同一屏障服务记录。"
        );
    }

    private void TestChargePathAoePreviewAndExecutionClipAtBarrier()
    {
        SkillDefinition chargeSkill = BuildChargeSkill("test_prismatic_charge_path_aoe", true);
        using SpecialFixture fixture = BuildFixture(chargeSkill);
        BattleUnitState insideTarget = fixture.AddUnit(
            BuildUnit("charge_path_inside", "路径内侧目标", "player", new Vector2I(4, 2))
        );
        fixture.RebindState();
        BattleCommand command = BuildGroundCommand(
            fixture.Source,
            chargeSkill.SkillId,
            new Vector2I(5, 2),
            ChargeVariantId
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.True(
            preview.allowed,
            $"路径步 AoE 冲锋预览应保持可施放。 logs={string.Join(" | ", preview.LogLinesTyped)}"
        );
        _test.False(
            preview.ContainsTargetCoord(new Vector2I(4, 2)),
            "路径步 AoE 预览应裁掉从当前路径锚点越过法球的地格。"
        );
        _test.Eq(
            ActiveLayerId(fixture.State),
            new StringName("red"),
            "路径步 AoE 只读预览不得改变当前色层。"
        );
        int hpBefore = insideTarget.GetCurrentHp();
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(
            insideTarget.GetCurrentHp(),
            hpBefore,
            "路径步 AoE 执行应在收集单位前裁掉法球内地格。"
        );
        _test.True(
            LogsContain(batch.LogLinesTyped, "阻挡了"),
            "路径步 AoE 被裁剪时应复用屏障阻挡日志。"
        );
    }

    private void TestRepeatAttackEntryPointsRespectBarrier()
    {
        AssertManualRepeatAttackBlocked();
        AssertAutoRepeatAttackBlocked();
        AssertPendingRepeatAttackBlocked();
        AssertRandomChainRepeatAttackBlocked();
    }

    private void AssertManualRepeatAttackBlocked()
    {
        SkillDefinition repeatSkill = BuildRepeatAttackSkill(
            "test_prismatic_repeat_manual",
            randomChain: false
        );
        using SpecialFixture fixture = BuildFixture(repeatSkill);
        BattleUnitState target = fixture.AddUnit(
            BuildUnit("repeat_manual_target", "手动连击目标", "player", new Vector2I(4, 2))
        );
        fixture.RebindState();
        int hpBefore = target.GetCurrentHp();
        BattleCommand command = BuildUnitCommand(fixture.Source, repeatSkill.SkillId, target);
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(target.GetCurrentHp(), hpBefore, "手动重复攻击不应绕过虹光法球。");
        _test.True(LogsContain(batch.LogLinesTyped, "被虹光法球"), "手动重复攻击应报告屏障阻挡。");
    }

    private void AssertAutoRepeatAttackBlocked()
    {
        SkillDefinition repeatSkill = BuildRepeatAttackSkill(
            "test_prismatic_repeat_auto",
            randomChain: false
        );
        using SpecialFixture fixture = BuildFixture(repeatSkill);
        BattleUnitState target = fixture.AddUnit(
            BuildUnit("repeat_auto_target", "自动连击目标", "player", new Vector2I(4, 2))
        );
        fixture.RebindState();
        int hpBefore = target.GetCurrentHp();
        using var batch = new BattleEventBatch();

        bool executed = fixture.Runtime._skill_orchestrator.ExecuteAutoCast(
            BuildAutoCastRequest(fixture.Source, repeatSkill.SkillId, target),
            batch
        );

        _test.True(executed, "自动重复攻击被屏障拦截仍应算作一次有效屏障交互。");
        _test.Eq(target.GetCurrentHp(), hpBefore, "自动重复攻击不应绕过虹光法球。");
    }

    private void AssertPendingRepeatAttackBlocked()
    {
        SkillDefinition repeatSkill = BuildRepeatAttackSkill(
            "test_prismatic_repeat_pending",
            randomChain: false,
            castingTimeTu: 10
        );
        using SpecialFixture fixture = BuildFixture(repeatSkill);
        BattleUnitState target = fixture.AddUnit(
            BuildUnit("repeat_pending_target", "读条连击目标", "player", new Vector2I(4, 2))
        );
        fixture.RebindState();
        var pendingCast = new BattlePendingCastState
        {
            SourceUnitId = fixture.Source.unit_id,
            SkillId = repeatSkill.SkillId,
            TargetMode = BattleTargetMode.Unit,
            BindingMode = PendingCastBindingModeKind.SoftAnchor,
            StartedCoord = fixture.Source.GetAnchorCoord(),
            StartedTu = fixture.State.timeline?.current_tu ?? 0,
            BaseCastingTimeTu = 10,
            RemainingCastProgress = 0,
            LastMaintenanceCheckpointHp = fixture.Source.GetCurrentHp(),
        };
        pendingCast.SetTargetUnitIds(new[] { target.unit_id });
        int hpBefore = target.GetCurrentHp();
        using var batch = new BattleEventBatch();

        bool resolved = fixture.Runtime._skill_orchestrator.ResolvePendingCast(
            fixture.Source,
            pendingCast,
            batch
        );

        _test.True(resolved, "读条重复攻击被屏障拦截仍应完成屏障交互。");
        _test.Eq(target.GetCurrentHp(), hpBefore, "读条完成的重复攻击不应绕过虹光法球。");
    }

    private void AssertRandomChainRepeatAttackBlocked()
    {
        SkillDefinition repeatSkill = BuildRepeatAttackSkill(
            "test_prismatic_repeat_random_chain",
            randomChain: true
        );
        using SpecialFixture fixture = BuildFixture(repeatSkill);
        BattleUnitState target = fixture.AddUnit(
            BuildUnit("repeat_random_target", "随机链目标", "player", new Vector2I(4, 2))
        );
        fixture.RebindState();
        int ownerHpBefore = fixture.SphereOwner.GetCurrentHp();
        int targetHpBefore = target.GetCurrentHp();
        BattleCommand command = BuildUnitCommand(
            fixture.Source,
            repeatSkill.SkillId,
            target: null
        );
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(
            fixture.SphereOwner.GetCurrentHp(),
            ownerHpBefore,
            "随机攻击链的重复攻击不应伤害法球内的屏障施放者。"
        );
        _test.Eq(target.GetCurrentHp(), targetHpBefore, "随机攻击链的重复攻击不应绕过虹光法球。");
        _test.True(LogsContain(batch.LogLinesTyped, "被虹光法球"), "随机攻击链应逐目标执行屏障检查。");
    }

    private void TestChainDamageChecksEachJumpAgainstBarrier()
    {
        SkillDefinition chainSkill = BuildChainDamageSkill();
        using SpecialFixture fixture = BuildFixture(chainSkill);
        BattleUnitState primary = fixture.AddUnit(
            BuildUnit("chain_primary", "连锁主目标", "player", new Vector2I(5, 2))
        );
        BattleUnitState secondary = fixture.AddUnit(
            BuildUnit("chain_secondary", "连锁次目标", "player", new Vector2I(4, 2))
        );
        fixture.RebindState();
        int primaryHpBefore = primary.GetCurrentHp();
        int secondaryHpBefore = secondary.GetCurrentHp();
        BattleCommand command = BuildUnitCommand(fixture.Source, chainSkill.SkillId, primary);
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.True(primary.GetCurrentHp() < primaryHpBefore, "同侧主目标应正常承受连锁技能的主伤害。");
        _test.Eq(
            secondary.GetCurrentHp(),
            secondaryHpBefore,
            "从主目标跳入法球的后续连锁伤害应在结算前被阻挡。"
        );
        _test.True(
            LogsContain(batch.LogLinesTyped, "连锁测试技能")
                && LogsContain(batch.LogLinesTyped, "被虹光法球"),
            "后续连锁跳跃应保留原施法者/技能归因并报告屏障阻挡。"
        );
    }

    private SpecialFixture BuildFixture(SkillDefinition skillDefinition)
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            skill_definitions: new Dictionary<StringName, SkillDefinition>
            {
                [skillDefinition.SkillId] = skillDefinition,
            },
            barrier_profile_definitions: _barrierProfileDefinitions
        );
        BattleTestFixture.ConfigureHitResolverForTests(runtime, new FixedHitResolver(10));
        BattleState state = BuildState(new Vector2I(8, 5));
        BattleUnitState sphereOwner = BuildUnit(
            "sphere_owner",
            "法球施放者",
            "player",
            new Vector2I(2, 2)
        );
        BattleUnitState source = BuildUnit(
            "special_source",
            "特殊入口使用者",
            "enemy",
            new Vector2I(6, 2)
        );
        var fixture = new SpecialFixture(runtime, state, sphereOwner, source, _test);
        fixture.AddUnit(sphereOwner);
        fixture.AddUnit(source);
        state.active_unit_id = source.unit_id;
        fixture.RebindState();
        LearnSkill(source, skillDefinition.SkillId);
        ApplyPrismaticSphere(runtime, sphereOwner);
        return fixture;
    }

    private static void ApplyPrismaticSphere(
        BattleRuntimeModule runtime,
        BattleUnitState sphereOwner
    )
    {
        SkillDefinition sphereSkill = TestSkillDefinitionProjection.BuildSkill(
            "mage_prismatic_sphere",
            displayName: "虹光法球",
            tags: new[] { new StringName("mage"), new StringName("magic") }
        );
        CombatEffectDefinition sphereEffect = TestSkillDefinitionProjection.BuildEffect(
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
        using var batch = new BattleEventBatch();
        runtime._layered_barrier_service.ApplyLayeredBarrierEffectResult(
            sphereOwner,
            sphereOwner,
            sphereSkill,
            sphereEffect,
            batch
        );
    }

    private static SkillDefinition BuildChargeSkill(StringName skillId, bool includePathAoe)
    {
        CombatEffectDefinition chargeEffect = TestSkillDefinitionProjection.BuildEffect(
            "charge"
        );
        var effects = new List<CombatEffectDefinition> { chargeEffect };
        if (includePathAoe)
        {
            effects.Add(
                TestSkillDefinitionProjection.BuildEffect(
                    "path_step_aoe",
                    effectTargetTeamFilter: "enemy",
                    power: 10,
                    damageTag: "force",
                    effectCategories: new[] { new StringName("force_effect") },
                    pathStepLogLabel: "路径测试攻击",
                    pathStepRadius: 1,
                    pathStepAreaPattern: "diamond"
                )
            );
        }
        CombatCastVariantDefinition variant = TestSkillDefinitionProjection.BuildCastVariant(
            ChargeVariantId,
            0,
            effects,
            targetMode: "ground",
            requiredCoordCount: 1
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: includePathAoe ? "路径冲锋测试" : "冲锋测试",
            tags: new[] { new StringName("melee"), new StringName("charge") },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                targetMode: "ground",
                targetTeamFilter: "any",
                rangeValue: 3,
                castVariants: new[] { variant }
            )
        );
    }

    private static SkillDefinition BuildRepeatAttackSkill(
        StringName skillId,
        bool randomChain,
        int castingTimeTu = 0
    )
    {
        CombatEffectDefinition damage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 5,
            damageTag: "force"
        );
        CombatEffectDefinition repeat = TestSkillDefinitionProjection.BuildEffect(
            "repeat_attack_until_fail",
            effectTargetTeamFilter: "enemy",
            parameters: new Dictionary<string, object>
            {
                ["follow_up_damage_multiplier_percent"] = 100,
            }
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "重复攻击测试",
            tags: new[] { new StringName("magic"), new StringName("test") },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { damage, repeat },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangePattern: "fixed",
                rangeValue: 10,
                castingTimeTu: castingTimeTu,
                targetSelectionMode: randomChain ? "random_chain" : "single_unit",
                maxHitsPerTarget: randomChain ? 1 : 0,
                deliveryCategories: new[] { new StringName("spell") }
            )
        );
    }

    private static SkillDefinition BuildChainDamageSkill()
    {
        StringName skillId = "test_prismatic_chain_damage";
        CombatEffectDefinition damage = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            effectTargetTeamFilter: "enemy",
            power: 10,
            damageTag: "lightning"
        );
        CombatEffectDefinition chain = TestSkillDefinitionProjection.BuildEffect(
            "chain_damage",
            effectTargetTeamFilter: "enemy",
            preventRepeatTarget: true,
            parameters: new Dictionary<string, object>
            {
                ["base_chain_radius"] = 1,
            }
        );
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: "连锁测试技能",
            tags: new[] { new StringName("magic"), new StringName("lightning") },
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                effects: new[] { damage, chain },
                targetMode: "unit",
                targetTeamFilter: "enemy",
                rangePattern: "fixed",
                rangeValue: 10,
                targetSelectionMode: "single_unit",
                deliveryCategories: new[] { new StringName("spell") }
            )
        );
    }

    private static BattleCommand BuildGroundCommand(
        BattleUnitState source,
        StringName skillId,
        Vector2I targetCoord,
        StringName variantId
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = source.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
            skill_variant_id = variantId,
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        return command;
    }

    private static BattleCommand BuildUnitCommand(
        BattleUnitState source,
        StringName skillId,
        BattleUnitState target
    )
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = source.unit_id,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(skillId),
            skill_id = skillId,
        };
        if (target != null)
        {
            command.target_coord = target.GetAnchorCoord();
            command.AddTargetUnitId(target.unit_id);
        }
        return command;
    }

    private static AutoCastRequest BuildAutoCastRequest(
        BattleUnitState source,
        StringName skillId,
        BattleUnitState target
    )
    {
        var releaseContext = new ContingencyReleaseContext
        {
            InstanceId = "test:repeat_auto",
            SetupId = "repeat_auto",
            OwnerMemberId = "test_owner",
            OwnerUnitId = source.unit_id,
            CasterUnitId = source.unit_id,
            TriggerType = "affected_by_spell",
        };
        return new AutoCastRequest
        {
            CasterUnitId = source.unit_id,
            OwnerMemberId = "test_owner",
            OwnerUnitId = source.unit_id,
            SetupId = "repeat_auto",
            InstanceId = "test:repeat_auto",
            SourceSkillId = "test_contingency_source",
            SourceSkillLevel = 1,
            SourceSkillGrantSourceType = UnitSkillGrantSourceType.Player,
            StoredSkillId = skillId,
            CastLevel = 1,
            TargetResolution = ContingencyTargetResolutionResult.UnitTarget(
                target.unit_id,
                target.GetAnchorCoord()
            ),
            ReleaseContext = releaseContext,
        };
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "prismatic_sphere_special_entry_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                var cell = new BattleCellState
                {
                    coord = new Vector2I(x, y),
                    base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.Land),
                    base_height = 4,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = "manual",
        }.WithCombatResourcesForTest(
            hp: 240,
            mp: 240,
            stamina: 240,
            aura: 240,
            ap: 10,
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 240);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 240);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 240);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("intelligence", 14);
        unit.attribute_snapshot.SetValue("constitution_modifier", 0);
        unit.attribute_snapshot.SetValue("willpower_modifier", 0);
        unit.attribute_snapshot.SetValue("intelligence_modifier", 2);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina));
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        return unit;
    }

    private static void LearnSkill(BattleUnitState unit, StringName skillId)
    {
        unit.AddKnownActiveSkill(skillId);
        unit.SetKnownSkillLevelTyped(skillId, 1);
    }

    private static void MarkOnlyRedLayerActive(BattleState state)
    {
        IReadOnlyList<StringName> keys = state.LayeredBarrierStore.SortedKeys();
        if (keys.Count == 0 || !state.LayeredBarrierStore.TryGet(keys[0], out BattleBarrierInstanceState barrier))
        {
            return;
        }
        var layers = new List<BattleBarrierLayerState>();
        foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
        {
            if (layer != null && layer.LayerId != new StringName("red"))
            {
                layer.Broken = true;
            }
            layers.Add(layer);
        }
        barrier.SetLayers(layers);
        state.LayeredBarrierStore.Put(keys[0], barrier);
    }

    private static StringName ActiveLayerId(BattleState state)
    {
        foreach (BattleBarrierInstanceState barrier in state.LayeredBarrierStore.ValuesSorted())
        {
            foreach (BattleBarrierLayerState layer in barrier.GetLayersTyped())
            {
                if (layer != null && !layer.Broken)
                {
                    return layer.LayerId;
                }
            }
        }
        return "";
    }

    private static bool LogsContain(IEnumerable<string> logLines, string fragment)
    {
        foreach (string line in logLines ?? Array.Empty<string>())
        {
            if (line?.Contains(fragment, StringComparison.Ordinal) == true)
            {
                return true;
            }
        }
        return false;
    }

    private sealed class SpecialFixture : IDisposable
    {
        private readonly List<BattleUnitState> _units = new();
        private readonly TestHarness _test;

        internal SpecialFixture(
            BattleRuntimeModule runtime,
            BattleState state,
            BattleUnitState sphereOwner,
            BattleUnitState source,
            TestHarness test
        )
        {
            Runtime = runtime;
            State = state;
            SphereOwner = sphereOwner;
            Source = source;
            _test = test;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }
        internal BattleUnitState SphereOwner { get; }
        internal BattleUnitState Source { get; }

        internal BattleUnitState AddUnit(BattleUnitState unit)
        {
            State.SetUnit(unit);
            if (unit.faction_id == SphereOwner.faction_id)
            {
                State.ally_unit_ids.Add(unit.unit_id);
            }
            else
            {
                State.enemy_unit_ids.Add(unit.unit_id);
            }
            _test.True(
                Runtime._grid_service.PlaceUnit(State, unit, unit.GetAnchorCoord(), true),
                $"测试单位应能放入棋盘：{unit.unit_id}"
            );
            _units.Add(unit);
            return unit;
        }

        internal void RebindState()
        {
            State.active_unit_id = Source.unit_id;
            Runtime.SetupStateForTests(State);
        }

        public void Dispose()
        {
            Runtime?.Dispose();
            foreach (BattleUnitState unit in _units)
            {
                BattleTestFixture.DisposeBattleUnit(unit);
            }
            BattleTestFixture.DisposeBattleState(State);
        }
    }
}
