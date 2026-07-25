using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_charge_path_aoe_behavior_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestAssemblerAddsWhirlwindChargePathAction();
            TestChargePathAoeScoresRepeatHits();
            TestChargePathAoeTraceBalancesWhenPreviewThrows();
            TestRuntimePlanUsesAutoWhirlwindAction();
            TestWhirlwindMissDoesNotGainChargeDistanceMastery();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI charge path AOE behavior regression"));
    }

    private void TestAssemblerAddsWhirlwindChargePathAction()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        EnemyAiBrainDefinition brain = GetEnemyBrain(runtime, "melee_aggressor");
        BattleUnitState spinner = BuildAiUnit(
            "whirlwind_assembler",
            "自动旋风狼",
            "hostile",
            new Vector2I(1, 2),
            "melee_aggressor",
            "engage",
            new[] { "warrior_whirlwind_slash" },
            36,
            2
        );
        PrepareTestWhirlwindUser(spinner);

        var assembler = new BattleAiActionAssembler();
        using BattleAiRuntimeActionPlan plan = assembler.BuildUnitActionPlan(
            spinner,
            brain,
            runtime.GetSkillDefinitionIndexTyped()
        );
        bool foundPathAction = false;
        foreach (BattleAiRuntimeActionEntry entry in plan.GetActionEntries("engage"))
        {
            UseChargePathAoeActionDefinition chargePathAction =
                entry?.Action as UseChargePathAoeActionDefinition;
            if (chargePathAction == null)
            {
                continue;
            }
            foundPathAction = ContainsSkillId(
                chargePathAction.DeclaredSkillIds,
                "warrior_whirlwind_slash"
            );
            if (foundPathAction)
            {
                break;
            }
        }

        _test.True(
            foundPathAction,
            "AI 自动装配器应为 warrior_whirlwind_slash 生成 charge + path_step_aoe Action。"
        );
    }

    private static bool ContainsSkillId(IReadOnlyList<StringName> skillIds, StringName skillId)
    {
        foreach (StringName candidate in skillIds ?? Array.Empty<StringName>())
        {
            if (candidate == skillId)
            {
                return true;
            }
        }
        return false;
    }

    private void TestChargePathAoeScoresRepeatHits()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(8, 5));
        BattleUnitState spinner = BuildAiUnit(
            "whirlwind_scorer",
            "旋风评分狼",
            "hostile",
            new Vector2I(1, 2),
            "melee_aggressor",
            "engage",
            new[] { "warrior_whirlwind_slash" },
            36,
            2
        );
        PrepareTestWhirlwindUser(spinner);
        BattleUnitState largeTarget = BuildManualUnit(
            "whirlwind_large_target",
            "大型目标",
            "player",
            new Vector2I(2, 0),
            new[] { "warrior_heavy_strike" }
        );
        largeTarget.SetBodySizeCategory("large");
        AddUnitToState(runtime, state, spinner, isEnemy: true);
        AddUnitToState(runtime, state, largeTarget, isEnemy: false);
        runtime.SetupStateForTests(state);

        var action = TestResourceOwnership.Own(
            new UseChargePathAoeAction
        {
            action_id = "whirlwind_path_aoe_probe",
            target_selector = "nearest_enemy",
            minimum_hit_count = 2,
            },
            "battle_ai_charge_path_aoe.action"
        );
        action.skill_ids.Add("warrior_whirlwind_slash");

        BattleAiContext context = BuildAiContext(runtime, spinner);
        context.trace_enabled = true;
        SkillDefinition whirlwind =
            runtime.GetSkillDefinitionIndexTyped()["warrior_whirlwind_slash"];
        _test.True(
            BattleRangeService.UnitHasMeleeWeapon(spinner),
            "旋风斩 AI 夹具应投影为有效近战武器。"
        );
        _test.Eq(
            new BattleAiTypedActionHelper().GetSkillCastBlockReason(context, whirlwind),
            BattleSkillCastBlockReasonKind.None,
            "旋风斩 AI 夹具不应被正式技能施放门槛阻挡。"
        );
        BattleAiDecision decision = new BattleAiChargePathAoeActionEvaluator().Evaluate(
            (UseChargePathAoeActionDefinition)action.ToDefinition(),
            context
        );
        AiActionTrace trace =
            context.GetActionTracesTyped().Count > 0
                ? context.GetActionTracesTyped()[0]
                : null;
        string traceSummary =
            trace == null
                ? "no trace"
                : $"evaluated={trace.EvaluationCount}, preview_reject={trace.PreviewRejectCount}, candidates={trace.CandidateCount}, blocks={string.Join(",", trace.BlockReasons)}";
        _test.True(
            decision?.command != null,
            $"旋风斩路径 AOE Action 应能产出合法候选。{traceSummary}"
        );
        _test.True(
            trace != null && trace.EvaluationCount < state.map_size.X * state.map_size.Y,
            "旋风斩 AI 应只枚举四向有效距离，不应扫描整张地图。"
        );
        _test.True(
            decision?.score_input != null && decision.score_input.path_step_hit_count >= 2,
            "路径 AOE 评分应统计同一大型目标被沿途多次命中的收益。"
        );
        _test.True(
            decision?.score_input != null && decision.score_input.path_step_payoff_score > 0,
            "路径 AOE 评分应把沿途命中转成正向 hit payoff。"
        );
        _test.True(
            runtime.PreviewCommand(decision?.command)?.allowed == true,
            "旋风斩路径 AOE Action 生成的命令必须通过 preview_command。"
        );
    }

    private void TestChargePathAoeTraceBalancesWhenPreviewThrows()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(8, 5));
        BattleUnitState spinner = BuildAiUnit(
            "whirlwind_trace_scorer",
            "旋风追踪狼",
            "hostile",
            new Vector2I(1, 2),
            "melee_aggressor",
            "engage",
            new[] { "warrior_whirlwind_slash" },
            36,
            2
        );
        PrepareTestWhirlwindUser(spinner);
        BattleUnitState largeTarget = BuildManualUnit(
            "whirlwind_trace_target",
            "旋风追踪大型目标",
            "player",
            new Vector2I(2, 0),
            new[] { "warrior_heavy_strike" }
        );
        largeTarget.SetBodySizeCategory("large");
        AddUnitToState(runtime, state, spinner, isEnemy: true);
        AddUnitToState(runtime, state, largeTarget, isEnemy: false);
        runtime.SetupStateForTests(state);

        var action = TestResourceOwnership.Own(
            new UseChargePathAoeAction
            {
                action_id = "whirlwind_trace_exception",
                target_selector = "nearest_enemy",
                minimum_hit_count = 2,
            },
            "battle_ai_charge_path_aoe.trace_exception_action"
        );
        action.skill_ids.Add("warrior_whirlwind_slash");
        BattleAiContext context = BuildAiContext(runtime, spinner);
        UseChargePathAoeActionDefinition definition =
            (UseChargePathAoeActionDefinition)action.ToDefinition();

        BattleAiTraceExceptionProbe.AssertPreservedAndBalanced(
            _test,
            "charge path AOE preview failure",
            expectedFailure =>
            {
                context.preview_command_callback = _ => throw expectedFailure;
                new BattleAiChargePathAoeActionEvaluator().Evaluate(definition, context);
            },
            "charge_path_aoe:formal_preview"
        );
    }

    private void TestRuntimePlanUsesAutoWhirlwindAction()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(8, 5));
        BattleUnitState spinner = BuildAiUnit(
            "whirlwind_auto_runtime",
            "自动旋风运行时",
            "hostile",
            new Vector2I(1, 2),
            "melee_aggressor",
            "engage",
            new[] { "warrior_whirlwind_slash" },
            36,
            2
        );
        PrepareTestWhirlwindUser(spinner);
        BattleUnitState largeTarget = BuildManualUnit(
            "whirlwind_runtime_target",
            "运行时大型目标",
            "player",
            new Vector2I(2, 0),
            new[] { "warrior_heavy_strike" }
        );
        largeTarget.SetBodySizeCategory("large");
        AddUnitToState(runtime, state, spinner, isEnemy: true);
        AddUnitToState(runtime, state, largeTarget, isEnemy: false);
        runtime.SetupStateForTests(state);
        runtime._build_ai_action_plans();

        BattleAiDecision decision = runtime._ai_service
            .ChooseCommand(BuildAiContext(runtime, spinner), captureTrace: false)
            ?.Decision;
        _test.True(decision?.command != null, "运行时自动 Action plan 应能产出 AI 指令。");
        _test.Eq(
            decision?.command?.skill_id ?? (StringName)"",
            (StringName)"warrior_whirlwind_slash",
            "未在 brain .tres 手写列出的 warrior_whirlwind_slash 应通过自动装配参与决策。"
        );
        _test.True(
            decision?.score_input != null && decision.score_input.path_step_hit_count >= 2,
            "运行时选择旋风斩时应携带路径 AOE 评分指标。"
        );
    }

    private void TestWhirlwindMissDoesNotGainChargeDistanceMastery()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        var missResolver = new FixedMissOneDamageResolver();
        missResolver.SetSkillDefinitions(runtime.GetSkillDefinitionIndexTyped());
        runtime.ConfigureDamageResolverForTests(missResolver);

        BattleState state = BuildFlatState(new Vector2I(8, 5));
        BattleUnitState spinner = BuildAiUnit(
            "whirlwind_mastery_miss",
            "旋风熟练度测试者",
            "hostile",
            new Vector2I(1, 2),
            "melee_aggressor",
            "engage",
            new[] { "warrior_whirlwind_slash" },
            36,
            2
        );
        PrepareTestWhirlwindUser(spinner);
        spinner.source_member_id = "whirlwind_mastery_member";
        BattleUnitState target = BuildManualUnit(
            "whirlwind_mastery_target",
            "旋风熟练度目标",
            "player",
            new Vector2I(2, 1),
            new[] { "warrior_heavy_strike" }
        );
        AddUnitToState(runtime, state, spinner, isEnemy: true);
        AddUnitToState(runtime, state, target, isEnemy: false);
        runtime.SetupStateForTests(state);

        SkillDefinition whirlwind =
            runtime.GetSkillDefinitionIndexTyped()["warrior_whirlwind_slash"];
        CombatCastVariantDefinition variant = whirlwind.CombatProfile.CastVariants[0];
        using var masteryService = new BattleSkillMasteryService();
        var chargeResolver = new BattleChargeResolver();
        chargeResolver.Setup(runtime, masteryService);
        using var batch = new BattleEventBatch();

        bool executed = chargeResolver.handle_charge_skill_command_result(
            spinner,
            whirlwind,
            variant,
            BattleGroundSkillValidationResult.AllowedResult(
                "可施放。",
                new[] { new Vector2I(3, 2) },
                direction: Vector2I.Right,
                distance: 2,
                resolvedAnchorCoord: new Vector2I(3, 2)
            ),
            batch
        );

        _test.True(executed, "旋风斩熟练度回归应成功执行两格路径冲锋。");
        _test.Eq(spinner.GetAnchorCoord(), new Vector2I(3, 2), "旋风斩熟练度回归应实际移动两格。");
        _test.Eq(
            masteryService.ResolveActiveSkillMasteryAmount(),
            0,
            "路径武器攻击全部未命中时，不应再按冲锋移动距离追加熟练度。"
        );

        masteryService.Clear();
        _test.True(
            runtime._grid_service.MoveUnit(state, spinner, new Vector2I(1, 2)),
            "旋风斩熟练度命中夹具应能把施放者复位。"
        );
        var maxDamageResolver = new FixedHitMaxDamageResolver();
        maxDamageResolver.SetSkillDefinitions(runtime.GetSkillDefinitionIndexTyped());
        runtime.ConfigureDamageResolverForTests(maxDamageResolver);
        using var hitBatch = new BattleEventBatch();
        bool hitExecuted = chargeResolver.handle_charge_skill_command_result(
            spinner,
            whirlwind,
            variant,
            BattleGroundSkillValidationResult.AllowedResult(
                "可施放。",
                new[] { new Vector2I(3, 2) },
                direction: Vector2I.Right,
                distance: 2,
                resolvedAnchorCoord: new Vector2I(3, 2)
            ),
            hitBatch
        );
        _test.True(hitExecuted, "旋风斩熟练度命中夹具应成功执行。");
        _test.True(
            masteryService.ResolveActiveSkillMasteryAmount() > 0,
            "路径武器攻击满足 weapon_attack_quality 时，应按真实攻击结果获得熟练度。"
        );
        chargeResolver.DisposeRuntime();
    }

    private static BattleRuntimeScope BuildRuntimeWithEnemyContent()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            gameSession.GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplateDefinitions(),
            gameSession.GetEnemyAiBrainDefinitions(),
            null
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        var damageResolver = new FixedSuccessOneDamageResolver();
        damageResolver.SetSkillDefinitions(runtime.GetSkillDefinitionIndexTyped());
        runtime.ConfigureDamageResolverForTests(damageResolver);
        return new BattleRuntimeScope(runtime, gameSession);
    }

    private static EnemyAiBrainDefinition GetEnemyBrain(
        BattleRuntimeModule runtime,
        StringName brainId
    )
    {
        if (
            runtime == null
            || !runtime.GetEnemyAiBrainIndexTyped().TryGetValue(
                brainId,
                out EnemyAiBrainDefinition brain
            )
        )
        {
            return null;
        }
        return brain;
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_charge_path_aoe_behavior_regression",
            phase = "timeline_running",
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
                    height_offset = 0,
                };
                cell.RecalculateRuntimeValues();
                state.SetCell(cell.coord, cell);
            }
        }
        state.RebuildCellColumns();
        return state;
    }

    private static BattleAiContext BuildAiContext(BattleRuntimeModule runtime, BattleUnitState unitState)
    {
        runtime._ensure_ai_action_plan_for_unit(unitState);
        runtime.TryGetAiActionPlanForUnit(
            unitState.unit_id,
            out BattleAiRuntimeActionPlan actionPlan
        );
        var context = new BattleAiContext
        {
            state = runtime._state,
            unit_state = unitState,
            grid_service = runtime._grid_service,
            move_cost_callback = (unit, targetCoord) =>
                runtime._get_ai_move_query_cost(unit.unit_id, unit.GetAnchorCoord(), targetCoord),
            runtime_action_plan = actionPlan,
        };
        context.SetSkillDefinitions(runtime.GetSkillDefinitionIndexTyped());
        runtime._bind_ai_helper_services_for_decision(unitState, context);
        return context;
    }

    private static BattleUnitState BuildAiUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        StringName brainId,
        StringName stateId,
        IReadOnlyList<string> skillIds,
        int currentHp,
        int currentAp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = "ai",
            ai_brain_id = brainId,
            ai_state_id = stateId,
        }.WithCombatResourcesForTest(
            hp: currentHp,
            mp: 120,
            stamina: 8,
            ap: currentAp,
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        SeedBaseAttributesAndArmorClass(unit, Math.Max(currentHp, 24), 8, 12);
        unit.attribute_snapshot.SetValue("mp_max", 120);
        unit.attribute_snapshot.SetValue("action_points", Math.Max(currentAp, 2));
        foreach (string rawSkillId in skillIds)
        {
            StringName skillId = rawSkillId;
            unit.AddKnownActiveSkill(skillId);
            unit.SetKnownSkillLevelTyped(
                skillId,
                skillId.ToString().StartsWith("mage_", StringComparison.Ordinal) ? 3 : 1
            );
        }
        return unit;
    }

    private static BattleUnitState BuildManualUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        IReadOnlyList<string> skillIds
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            control_mode = "manual",
        }.WithCombatResourcesForTest(
            hp: 30,
            ap: 2,
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        SeedBaseAttributesAndArmorClass(unit, 30, 8, 6);
        unit.attribute_snapshot.SetValue("action_points", 2);
        foreach (string rawSkillId in skillIds)
        {
            StringName skillId = rawSkillId;
            unit.AddKnownActiveSkill(skillId);
            unit.SetKnownSkillLevelTyped(
                skillId,
                skillId.ToString().StartsWith("mage_", StringComparison.Ordinal) ? 3 : 1
            );
        }
        return unit;
    }

    private void AddUnitToState(
        BattleRuntimeModule runtime,
        BattleState state,
        BattleUnitState unit,
        bool isEnemy
    )
    {
        state.SetUnit(unit);
        if (isEnemy)
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
        else
        {
            state.ally_unit_ids.Add(unit.unit_id);
        }
        _test.True(
            runtime._grid_service.PlaceUnit(state, unit, unit.GetAnchorCoord(), true),
            $"测试单位 {unit.unit_id} 应能放入测试战场。"
        );
    }

    private static void PrepareTestWhirlwindUser(BattleUnitState unit)
    {
        if (unit == null)
        {
            return;
        }
        unit.SetCurrentStamina(120);
        unit.SetCurrentAura(140);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        unit.attribute_snapshot.SetValue("stamina_max", 120);
        unit.attribute_snapshot.SetValue("aura_max", 140);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 30);
        unit.SetKnownSkillLevelTyped("warrior_whirlwind_slash", 9);
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
                weapon_item_id = "ai_test_whirlwind_blade",
                weapon_profile_type_id = "shortsword",
                weapon_family = "sword",
                weapon_range_type = "melee",
                weapon_current_grip = BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded),
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                    flat_bonus = 0,
                },
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }

    private static void SeedBaseAttributesAndArmorClass(
        BattleUnitState unit,
        int hpMax,
        int staminaMax,
        int attackBonus
    )
    {
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
        {
            if (!unit.attribute_snapshot.HasValue(attributeId))
            {
                unit.attribute_snapshot.SetValue(attributeId, 10);
            }
        }
        unit.attribute_snapshot.SetValue("hp_max", hpMax);
        unit.attribute_snapshot.SetValue("stamina_max", staminaMax);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), attackBonus);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
    }

    private sealed class BattleRuntimeScope : IDisposable
    {
        private readonly GameSession _gameSession;

        internal BattleRuntimeScope(BattleRuntimeModule runtime, GameSession gameSession)
        {
            Runtime = runtime;
            _gameSession = gameSession;
        }

        internal BattleRuntimeModule Runtime { get; }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, Runtime?._state);
            _gameSession?.Dispose();
        }
    }
}
