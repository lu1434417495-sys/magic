using System;
using System.Collections.Generic;
using Godot;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_ai_melee_charge_behavior_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestNaturalWeaponMeleeAggressorFallsBackToBasicAttack();
            TestMeleeAggressorChargeDecisionMovesTowardTarget();
            TestFrontlineBulwarkChargeDecisionMovesTowardTarget();
            TestShortRegularMovePrefersCloseInOverCharge();
            TestChargeActionScoresWithResolvedStopAnchor();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        Quit(_test.Finish("Battle AI melee charge behavior regression"));
    }

    private void TestNaturalWeaponMeleeAggressorFallsBackToBasicAttack()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(3, 1));
        runtime._state = state;
        BattleUnitState wolf = BuildAiUnit(
            "natural_basic_wolf",
            "基础攻击荒狼",
            "hostile",
            new Vector2I(0, 0),
            "melee_aggressor",
            "pressure",
            new[] { "warrior_heavy_strike", "basic_attack" },
            28,
            2
        );
        wolf.SetNaturalWeaponProjectionTyped(
            "natural_weapon",
            "physical_pierce",
            1,
            new WeaponDice
            {
                dice_count = 1,
                dice_sides = 6,
                flat_bonus = 0,
            }
        );
        BattleUnitState player = BuildManualUnit(
            "basic_attack_target",
            "玩家",
            "player",
            new Vector2I(1, 0),
            new[] { "basic_attack" }
        );
        AddUnitToState(runtime, state, wolf, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);

        BattleAiDecision decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, wolf));
        _test.True(decision?.command != null, "天生武器单位在近身 pressure 状态下应能产出攻击指令。");
        _test.Eq(
            decision?.command?.skill_id ?? (StringName)"",
            (StringName)"basic_attack",
            "重击被装备武器门槛阻断后，天生武器单位应回退到基础攻击。"
        );
        BattlePreview preview = runtime.PreviewCommand(decision?.command);
        _test.True(preview?.allowed == true, "天生武器基础攻击应通过 runtime preview。");
    }

    private void TestMeleeAggressorChargeDecisionMovesTowardTarget()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(6, 3));
        runtime._state = state;
        BattleUnitState wolf = BuildAiUnit(
            "wolf_01",
            "荒狼",
            "hostile",
            new Vector2I(0, 1),
            "melee_aggressor",
            "engage",
            new[] { "charge", "warrior_heavy_strike" },
            36,
            2
        );
        wolf.current_move_points = 0;
        wolf.current_stamina = 80;
        wolf.attribute_snapshot.SetValue("stamina_max", 80);
        BattleUnitState player = BuildManualUnit(
            "player_01",
            "玩家",
            "player",
            new Vector2I(5, 1),
            new[] { "warrior_heavy_strike" }
        );
        AddUnitToState(runtime, state, wolf, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);
        state.phase = "unit_acting";
        state.active_unit_id = wolf.unit_id;

        BattleEventBatch batch = runtime.advance(0);
        _test.True(batch != null, "AI advance 应返回有效 batch。");
        _test.True(batch.log_lines.Count > 0, $"AI advance 应回传行动反馈。 log={batch.log_lines}");
        _test.True(
            wolf.coord != new Vector2I(0, 1),
            $"melee_aggressor 在 engage 状态下应优先用 charge 接敌。 coord={wolf.coord} log={batch.log_lines}"
        );
    }

    private void TestFrontlineBulwarkChargeDecisionMovesTowardTarget()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(7, 3));
        runtime._state = state;
        BattleUnitState vanguard = BuildAiUnit(
            "vanguard_01",
            "荒狼先锋",
            "hostile",
            new Vector2I(0, 1),
            "frontline_bulwark",
            "engage",
            new[] { "charge", "warrior_shield_bash", "warrior_taunt", "warrior_guard" },
            42,
            2
        );
        vanguard.current_move_points = 0;
        vanguard.current_stamina = 80;
        vanguard.attribute_snapshot.SetValue("stamina_max", 80);
        BattleUnitState player = BuildManualUnit(
            "player_01",
            "玩家",
            "player",
            new Vector2I(5, 1),
            new[] { "warrior_heavy_strike" }
        );
        AddUnitToState(runtime, state, vanguard, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);
        state.phase = "unit_acting";
        state.active_unit_id = vanguard.unit_id;

        BattleEventBatch batch = runtime.advance(0);
        _test.True(batch != null, "frontline_bulwark AI advance 应返回有效 batch。");
        _test.True(batch.log_lines.Count > 0, $"frontline_bulwark AI advance 应回传行动反馈。 log={batch.log_lines}");
        _test.True(
            vanguard.coord != new Vector2I(0, 1),
            $"frontline_bulwark 在 engage 状态下应优先用 charge 接敌。 coord={vanguard.coord} log={batch.log_lines}"
        );
    }

    private void TestShortRegularMovePrefersCloseInOverCharge()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(4, 3));
        runtime._state = state;
        BattleUnitState wolf = BuildAiUnit(
            "short_charge_wolf",
            "短距冲锋狼",
            "hostile",
            new Vector2I(0, 1),
            "melee_aggressor",
            "engage",
            new[] { "charge", "warrior_heavy_strike" },
            36,
            2
        );
        wolf.current_move_points = 2;
        wolf.current_stamina = 80;
        wolf.attribute_snapshot.SetValue("stamina_max", 80);
        BattleUnitState player = BuildManualUnit(
            "short_charge_target",
            "短距目标",
            "player",
            new Vector2I(2, 1),
            new[] { "warrior_heavy_strike" }
        );
        AddUnitToState(runtime, state, wolf, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);

        BattleAiDecision decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, wolf));
        _test.True(decision?.command != null, "短距离接敌应产出合法 AI 指令。");
        _test.Eq(
            decision?.action_id ?? (StringName)"",
            (StringName)"wolf_close_in",
            "短距离且普通移动可达时应走 close_in，而不是 charge_open。"
        );
        _test.Eq(
            decision?.command?.command_type ?? (StringName)"",
            BattleTypedNames.ToStringName(BattleCommandKind.Move),
            "短距离接敌应生成移动指令。"
        );
        _test.Eq(
            decision?.command?.target_coord ?? new Vector2I(-1, -1),
            new Vector2I(1, 1),
            "短距离接敌应移动到贴身格。"
        );
    }

    private void TestChargeActionScoresWithResolvedStopAnchor()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleState state = BuildFlatState(new Vector2I(6, 3));
        var blockedCoord = new Vector2I(2, 1);
        if (state.cells.ContainsKey(blockedCoord))
        {
            BattleCellState blockedCell = state.cells[blockedCoord].As<BattleCellState>();
            blockedCell.base_terrain = BattleTerrainRules.ToStringName(BattleTerrainKind.DeepWater);
            blockedCell.RecalculateRuntimeValues();
        }
        state.cell_columns = BattleCellState.BuildColumnsFromSurfaceCells(state.cells);
        runtime._state = state;
        BattleUnitState wolf = BuildAiUnit(
            "charge_score_wolf",
            "冲锋评分狼",
            "hostile",
            new Vector2I(0, 1),
            "melee_aggressor",
            "engage",
            new[] { "charge" },
            36,
            2
        );
        wolf.current_stamina = 80;
        wolf.attribute_snapshot.SetValue("stamina_max", 80);
        BattleUnitState player = BuildManualUnit(
            "charge_focus_target",
            "目标玩家",
            "player",
            new Vector2I(4, 1),
            new[] { "warrior_heavy_strike" }
        );
        AddUnitToState(runtime, state, wolf, isEnemy: true);
        AddUnitToState(runtime, state, player, isEnemy: false);

        var action = new UseChargeAction
        {
            action_id = "charge_resolved_stop_anchor",
            skill_id = "charge",
            target_selector = "nearest_enemy",
            minimum_charge_move_distance = 1,
        };
        BattleAiDecision decision = action.Decide(BuildAiContext(runtime, wolf));
        _test.True(decision?.command != null, "charge 评分回归应能产出合法冲锋指令。");
        _test.True(
            decision?.command?.target_coord != new Vector2I(-1, -1),
            "charge 评分回归应保留有效的声明目标格。"
        );
        BattlePreview preview = runtime.PreviewCommand(decision?.command);
        _test.True(preview?.allowed == true, "charge 评分回归中的正式 preview 必须允许该冲锋指令。");
        _test.Eq(
            preview?.resolved_anchor_coord ?? new Vector2I(-1, -1),
            new Vector2I(1, 1),
            "charge preview 应暴露与正式执行一致的 resolved_anchor_coord。"
        );
    }

    private static BattleRuntimeScope BuildRuntimeWithEnemyContent()
    {
        var gameSession = new GameSession();
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            gameSession.GetSkillDefsTyped(),
            gameSession.GetEnemyTemplatesTyped(),
            gameSession.GetEnemyAiBrainsTyped(),
            null
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        var damageResolver = new FixedSuccessOneDamageResolver();
        damageResolver.SetSkillDefs(runtime.GetSkillDefIndexTyped());
        runtime.ConfigureDamageResolverForTests(damageResolver);
        gameSession.Free();
        return new BattleRuntimeScope(runtime);
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_melee_charge_behavior_regression",
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
                state.cells[cell.coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.BuildColumnsFromSurfaceCells(state.cells);
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
        context.SetSkillDefs(runtime.GetSkillDefIndexTyped());
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
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina));
        SeedBaseAttributesAndArmorClass(unit, Math.Max(currentHp, 24), 8, 12);
        unit.attribute_snapshot.SetValue("mp_max", 120);
        unit.attribute_snapshot.SetValue("action_points", Math.Max(currentAp, 2));
        foreach (string rawSkillId in skillIds)
        {
            StringName skillId = rawSkillId;
            unit.known_active_skill_ids.Add(skillId);
            unit.known_skill_level_map[skillId] = skillId.ToString().StartsWith("mage_", StringComparison.Ordinal) ? 3 : 1;
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
            unit.known_skill_level_map[skillId] = skillId.ToString().StartsWith("mage_", StringComparison.Ordinal) ? 3 : 1;
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
        state.units[unit.unit_id] = unit;
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

    private sealed class BattleRuntimeScope : IDisposable
    {
        internal BattleRuntimeScope(BattleRuntimeModule runtime)
        {
            Runtime = runtime;
        }

        internal BattleRuntimeModule Runtime { get; }

        public void Dispose()
        {
            Runtime?.dispose();
        }
    }
}
