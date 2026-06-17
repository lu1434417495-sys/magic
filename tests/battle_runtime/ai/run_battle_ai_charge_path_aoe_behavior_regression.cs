using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_charge_path_aoe_behavior_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestAssemblerAddsWhirlwindChargePathAction();
            TestChargePathAoeScoresRepeatHits();
            TestRuntimePlanUsesAutoWhirlwindAction();
            Quit(_test.Finish("Battle AI charge path AOE behavior regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            Quit(1);
        }
    }

    private void TestAssemblerAddsWhirlwindChargePathAction()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        EnemyAiBrainDef brain = GetEnemyBrain(runtime, "melee_aggressor");
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
        BattleAiRuntimeActionPlan plan = assembler.BuildUnitActionPlan(
            spinner,
            brain,
            runtime.GetSkillDefIndexTyped()
        );
        bool foundPathAction = false;
        foreach (EnemyAiAction action in plan.GetActions("engage"))
        {
            if (action is not UseChargePathAoeAction chargePathAction)
            {
                continue;
            }
            foundPathAction = chargePathAction.skill_ids.Contains("warrior_whirlwind_slash");
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

        var action = new UseChargePathAoeAction
        {
            action_id = "whirlwind_path_aoe_probe",
            target_selector = "nearest_enemy",
            minimum_hit_count = 2,
        };
        action.skill_ids.Add("warrior_whirlwind_slash");

        BattleAiDecision decision = action.Decide(BuildAiContext(runtime, spinner));
        _test.True(decision?.command != null, "旋风斩路径 AOE Action 应能产出合法候选。");
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

        BattleAiDecision decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, spinner));
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

    private static EnemyAiBrainDef GetEnemyBrain(BattleRuntimeModule runtime, StringName brainId)
    {
        if (runtime?._enemy_ai_brains == null || !runtime._enemy_ai_brains.ContainsKey(brainId))
        {
            return null;
        }
        return runtime._enemy_ai_brains[brainId].As<EnemyAiBrainDef>();
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

    private static void PrepareTestWhirlwindUser(BattleUnitState unit)
    {
        if (unit == null)
        {
            return;
        }
        unit.current_stamina = 120;
        unit.current_aura = 140;
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        unit.attribute_snapshot.SetValue("stamina_max", 120);
        unit.attribute_snapshot.SetValue("aura_max", 140);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 30);
        unit.known_skill_level_map["warrior_whirlwind_slash"] = 9;
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
                weapon_item_id = "ai_test_whirlwind_blade",
                weapon_profile_type_id = "shortsword",
                weapon_family = "sword",
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
