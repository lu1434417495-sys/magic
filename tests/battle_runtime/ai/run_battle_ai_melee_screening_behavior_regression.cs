using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_melee_screening_behavior_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        try
        {
            TestMeleeCloseInPrefersScreeningRangedAllyWhenHealthy();
            TestMeleeScreeningPrefersActualPathCostBlock();
            TestMeleeScreeningIgnoresGeometricLineWithoutPressure();
            TestMeleeScreeningMoveToRangeHonorsLockedMovePoints();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle AI melee screening behavior regression"));
    }

    private void TestMeleeCloseInPrefersScreeningRangedAllyWhenHealthy()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(7, 8));
        runtime.SetupStateForTests(state);
        BattleUnitState wolf = BuildAiUnit(
            "screening_wolf",
            "占位战士",
            "hostile",
            new Vector2I(1, 4),
            "melee_aggressor",
            "engage",
            new[] { "charge", "basic_attack" },
            28,
            2
        );
        wolf.current_move_points = 2;
        wolf.current_stamina = 80;
        wolf.attribute_snapshot.SetValue("stamina_max", 80);
        BattleUnitState archer = BuildAiUnit(
            "screening_archer",
            "后排弓手",
            "hostile",
            new Vector2I(3, 6),
            "ranged_archer",
            "pressure",
            new[] { "archer_aimed_shot" },
            28,
            2
        );
        ApplyTestBowWeapon(archer, 6);
        BattleUnitState player = BuildManualUnit(
            "screening_threat",
            "近战威胁",
            "player",
            new Vector2I(3, 3),
            new[] { "basic_attack" }
        );
        AddUnitToState(runtime, state, wolf, isEnemy: true);
        AddUnitToState(runtime, state, archer, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);

        MoveToRangeActionDefinition action = BuildScreeningAction("screening_close_in_probe");
        BattleAiContext aiContext = BuildAiContext(runtime, wolf);
        BattleAiDecision decision = Evaluate(action, aiContext);
        _test.Eq(
            decision?.command?.target_coord ?? new Vector2I(-1, -1),
            new Vector2I(3, 4),
            "健康近战接敌时，应优先选择仍能贴敌且位于敌方近战到己方弓手最短路上的占位格。"
        );

        wolf.current_hp = 8;
        BattleAiDecision lowHpDecision = Evaluate(action, aiContext);
        _test.Eq(
            lowHpDecision?.command?.target_coord ?? new Vector2I(-1, -1),
            new Vector2I(2, 3),
            "低血且无防御技能时，接敌动作不应继续为了保护后排偏向占位格。"
        );
    }

    private void TestMeleeScreeningPrefersActualPathCostBlock()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(5, 4));
        foreach (Vector2I blockedCoord in new[]
        {
            new Vector2I(2, 0),
            new Vector2I(3, 0),
            new Vector2I(2, 1),
            new Vector2I(3, 1),
        })
        {
            if (!state.ContainsCell(blockedCoord))
            {
                continue;
            }
            BattleCellState cell = state.GetCell(blockedCoord);
            cell.base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.DeepWater);
            cell.RecalculateRuntimeValues();
        }
        state.RebuildCellColumns();
        runtime.SetupStateForTests(state);

        BattleUnitState wolf = BuildAiUnit(
            "path_cost_screening_wolf",
            "占位战士",
            "hostile",
            new Vector2I(0, 2),
            "melee_aggressor",
            "engage",
            new[] { "charge", "basic_attack" },
            28,
            2
        );
        wolf.current_move_points = 1;
        BattleUnitState archer = BuildAiUnit(
            "path_cost_screening_archer",
            "后排弓手",
            "hostile",
            new Vector2I(4, 1),
            "ranged_archer",
            "pressure",
            new[] { "archer_aimed_shot" },
            28,
            2
        );
        ApplyTestBowWeapon(archer, 6);
        BattleUnitState player = BuildManualUnit(
            "path_cost_screening_threat",
            "近战威胁",
            "player",
            new Vector2I(1, 1),
            new[] { "basic_attack" }
        );
        player.current_move_points = 6;
        AddUnitToState(runtime, state, wolf, isEnemy: true);
        AddUnitToState(runtime, state, archer, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);

        MoveToRangeActionDefinition action = BuildScreeningAction(
            "path_cost_screening_probe",
            screeningPathBonus: 300
        );
        BattleAiDecision decision = Evaluate(action, BuildAiContext(runtime, wolf));
        _test.Eq(
            decision?.command?.target_coord ?? new Vector2I(-1, -1),
            new Vector2I(1, 2),
            "候选格不在几何最短路但实际封住敌方到弓手的路径时，接敌动作应选择该守线格。"
        );
        _test.True(
            runtime.PreviewCommand(decision?.command)?.allowed == true,
            "路径成本守线格应产出可执行移动指令。"
        );
    }

    private void TestMeleeScreeningIgnoresGeometricLineWithoutPressure()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(6, 8));
        runtime.SetupStateForTests(state);
        BattleUnitState wolf = BuildAiUnit(
            "geometric_screening_wolf",
            "几何占位战士",
            "hostile",
            new Vector2I(0, 5),
            "melee_aggressor",
            "engage",
            new[] { "charge", "basic_attack" },
            28,
            2
        );
        wolf.current_move_points = 3;
        BattleUnitState archer = BuildAiUnit(
            "geometric_screening_archer",
            "后排弓手",
            "hostile",
            new Vector2I(3, 6),
            "ranged_archer",
            "pressure",
            new[] { "archer_aimed_shot" },
            28,
            2
        );
        ApplyTestBowWeapon(archer, 6);
        BattleUnitState player = BuildManualUnit(
            "geometric_screening_threat",
            "近战威胁",
            "player",
            new Vector2I(2, 3),
            new[] { "basic_attack" }
        );
        player.current_move_points = 3;
        AddUnitToState(runtime, state, wolf, isEnemy: true);
        AddUnitToState(runtime, state, archer, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);

        BattleAiDecision decision = Evaluate(
            BuildScreeningAction("geometric_screening_probe"),
            BuildAiContext(runtime, wolf)
        );
        _test.True(
            decision?.command?.target_coord != new Vector2I(3, 5),
            "仅处于几何最短路但不增加路径成本、也不能贴身/反击的格子不应成为守线偏好的移动目标。"
        );
        _test.True(
            runtime.PreviewCommand(decision?.command)?.allowed == true,
            "几何最短路被忽略后仍应产出可执行接敌移动指令。"
        );
    }

    private void TestMeleeScreeningMoveToRangeHonorsLockedMovePoints()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(5, 5));
        runtime.SetupStateForTests(state);
        BattleUnitState wolf = BuildAiUnit(
            "locked_screening_wolf",
            "锁定移动力战士",
            "hostile",
            new Vector2I(0, 2),
            "melee_aggressor",
            "engage",
            new[] { "charge", "basic_attack" },
            28,
            2
        );
        wolf.current_move_points = 1;
        wolf.has_moved_this_turn = true;
        wolf.can_use_locked_move_points_this_turn = false;
        BattleUnitState archer = BuildAiUnit(
            "locked_screening_archer",
            "锁定测试弓手",
            "hostile",
            new Vector2I(4, 4),
            "ranged_archer",
            "pressure",
            new[] { "archer_aimed_shot" },
            28,
            2
        );
        ApplyTestBowWeapon(archer, 6);
        BattleUnitState player = BuildManualUnit(
            "locked_screening_threat",
            "锁定测试威胁",
            "player",
            new Vector2I(3, 2),
            new[] { "basic_attack" }
        );
        AddUnitToState(runtime, state, wolf, isEnemy: true);
        AddUnitToState(runtime, state, archer, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);

        MoveToRangeActionDefinition action = BuildScreeningAction("locked_screening_probe");
        BattleAiDecision lockedDecision = Evaluate(action, BuildAiContext(runtime, wolf));
        _test.True(
            lockedDecision == null,
            "已移动且未获准使用锁定移动力时，screening move_to_range 不应继续产出移动指令。"
        );

        wolf.can_use_locked_move_points_this_turn = true;
        BattleAiDecision allowedDecision = Evaluate(action, BuildAiContext(runtime, wolf));
        _test.True(
            allowedDecision?.command?.IsMove() == true,
            "获得锁定移动力许可时，screening move_to_range 仍应能产出移动指令。"
        );
        _test.True(
            runtime.PreviewCommand(allowedDecision?.command)?.allowed == true,
            "获得锁定移动力许可时，产出的移动指令应通过 runtime 预览。"
        );
    }

    private static MoveToRangeActionDefinition BuildScreeningAction(
        StringName actionId,
        int screeningPathBonus = 45
    )
    {
        return new MoveToRangeActionDefinition(
            actionId,
            "",
            "positioning",
            "inline_decide",
            "nearest_enemy",
            1,
            1,
            Array.Empty<StringName>(),
            BattleAiMoveToRangeActionEvaluator.ToStringName(
                BattleAiMoveToRangeActionEvaluator.MoveToRangeScreeningMode.RangedAlly
            ),
            true,
            2,
            140,
            220,
            1000,
            4000,
            4,
            2,
            2,
            screeningPathBonus
        );
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

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_melee_screening_behavior_regression",
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
        runtime._ai_action_plans_by_unit_id.TryGetValue(
            unitState.unit_id,
            out BattleAiRuntimeActionPlan actionPlan
        );
        var context = new BattleAiContext
        {
            state = runtime._state,
            unit_state = unitState,
            grid_service = runtime._grid_service,
            move_cost_callback = (unit, targetCoord) =>
                runtime._get_ai_move_query_cost(unit.unit_id, unit.coord, targetCoord),
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
            current_hp = currentHp,
            current_mp = 120,
            current_stamina = 8,
            current_ap = currentAp,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        SeedBaseAttributesAndArmorClass(unit, Math.Max(currentHp, 24), 8, 12);
        unit.attribute_snapshot.SetValue("mp_max", 120);
        unit.attribute_snapshot.SetValue("action_points", Math.Max(currentAp, 2));
        foreach (string rawSkillId in skillIds)
        {
            StringName skillId = rawSkillId;
            unit.known_active_skill_ids.Add(skillId);
            unit.known_skill_level_map[skillId] = 1;
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
            current_hp = 30,
            current_ap = 2,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        SeedBaseAttributesAndArmorClass(unit, 30, 8, 6);
        unit.attribute_snapshot.SetValue("action_points", 2);
        foreach (string rawSkillId in skillIds)
        {
            StringName skillId = rawSkillId;
            unit.known_active_skill_ids.Add(skillId);
            unit.known_skill_level_map[skillId] = 1;
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
            runtime._grid_service.PlaceUnit(state, unit, unit.coord, true),
            $"测试单位 {unit.unit_id} 应能放入测试战场。"
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

    private static void ApplyTestBowWeapon(BattleUnitState unit, int attackRange)
    {
        unit?.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
                weapon_item_id = "ai_screening_test_longbow",
                weapon_profile_type_id = "longbow",
                weapon_family = "bow",
                weapon_current_grip = BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded),
                weapon_attack_range = attackRange,
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 8,
                    flat_bonus = 0,
                },
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = "physical_pierce",
            }
        );
    }

    private static BattleAiDecision Evaluate(
        MoveToRangeActionDefinition action,
        BattleAiContext context
    ) =>
        new BattleAiMoveToRangeActionEvaluator().Evaluate(action, context);

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
