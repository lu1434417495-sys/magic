using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_ai_enemy_template_runtime_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        try
        {
            TestTemplateStartBattleStableIds();
            TestWolfTemplatesSpawnWithPositiveStaminaPool();
            TestUnitFactoryDoesNotBuildFallbackEnemy();
            TestFormalEnemyTemplatesHaveRealPressureSkillAction();
            TestDepletedRangedTemplatesCloseForBasicAttackFallback();
            TestEnemyTemplateUsesCanonicalTemplateId();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Battle AI enemy template runtime regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle AI enemy template runtime regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestTemplateStartBattleStableIds()
    {
        AssertTemplateStartBattle(
            "encounter_wolf",
            "wolf_pack",
            "荒狼群",
            expectedEnemyCount: 2,
            expectedBrainId: "melee_aggressor",
            expectedStateId: "engage",
            requiredSkills: new[] { "charge", "basic_attack" }
        );
        AssertTemplateStartBattle(
            "encounter_vanguard",
            "wolf_vanguard",
            "荒狼先锋",
            expectedEnemyCount: 1,
            expectedBrainId: "frontline_bulwark",
            expectedStateId: "engage",
            requiredSkills: new[] { "charge", "warrior_guard" }
        );
        AssertTemplateStartBattle(
            "encounter_harrier",
            "mist_harrier",
            "雾沼猎压者",
            expectedEnemyCount: 1,
            expectedBrainId: "ranged_suppressor",
            expectedStateId: "pressure",
            requiredSkills: new[] { "archer_suppressive_fire", "archer_pinning_shot" }
        );
        AssertTemplateStartBattle(
            "encounter_weaver",
            "mist_weaver",
            "雾沼织咒者",
            expectedEnemyCount: 1,
            expectedBrainId: "healer_controller",
            expectedStateId: "pressure",
            requiredSkills: new[] { "mage_temporal_rewind", "mage_glacial_prison" }
        );
    }

    private void TestWolfTemplatesSpawnWithPositiveStaminaPool()
    {
        var cases = new[]
        {
            ("wolf_pack", "encounter_wolf_pack_stamina", "荒狼群"),
            ("wolf_raider", "encounter_wolf_raider_stamina", "荒狼袭掠者"),
            ("wolf_alpha", "encounter_wolf_alpha_stamina", "荒狼首领"),
            ("wolf_vanguard", "encounter_wolf_vanguard_stamina", "荒狼先锋"),
        };
        foreach ((string templateId, string encounterId, string displayName) in cases)
        {
            using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
            BattleRuntimeModule runtime = runtimeScope.Runtime;
            BattleState state = runtime.start_battle(
                BuildEncounterAnchor(encounterId, templateId, displayName),
                106,
                BuildBattleStartContext("ally_a", "ally_b")
            );
            AssertTrue(IsStartedState(state), $"{templateId} 模板应能正式生成战斗状态。");
            if (!IsStartedState(state))
            {
                continue;
            }
            AssertTrue(state.enemy_unit_ids.Count > 0, $"{templateId} 模板应至少生成一个敌方单位。");
            foreach (StringName enemyUnitId in state.enemy_unit_ids)
            {
                AssertTrue(state.TryGetUnitTyped(enemyUnitId, out BattleUnitState enemyUnit), $"{templateId} 模板生成的敌方单位应存在于 battle state 中。");
                if (enemyUnit == null)
                {
                    continue;
                }
                AssertTrue(
                    enemyUnit.attribute_snapshot.get_value("stamina_max") > 0,
                    $"{templateId} 模板生成的敌方单位 stamina_max 应为正值。"
                );
                AssertTrue(
                    enemyUnit.current_stamina > 0,
                    $"{templateId} 模板生成的敌方单位 current_stamina 应为正值，避免技能链因资源池为 0 直接失效。"
                );
            }
        }
    }

    private void TestUnitFactoryDoesNotBuildFallbackEnemy()
    {
        using var gameSession = new GameSessionScope();
        var runtime = new BattleRuntimeModule();
        try
        {
            runtime.setup(
                null,
                gameSession.Session.get_skill_defs(),
                new GDictionary(),
                new GDictionary(),
                null
            );
            GArray enemyUnits = runtime._unit_factory.build_enemy_units(
                BuildEncounterAnchor(
                    "runtime_factory_fallback_affordability",
                    "missing_runtime_factory_template",
                    "工厂 fallback 敌人"
                ),
                new GDictionary
                {
                    ["default_enemy_stamina"] = 0,
                    ["enemy_unit_count"] = 1,
                }
            );
            AssertTrue(
                enemyUnits.Count == 0,
                "BattleUnitFactory 不应再构建 fallback enemy；敌人必须来自显式 payload 或正式模板。"
            );
        }
        finally
        {
            runtime.dispose();
        }
    }

    private void TestFormalEnemyTemplatesHaveRealPressureSkillAction()
    {
        foreach (StringName templateId in GetFormalEnemyTemplateIds())
        {
            using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
            BattleRuntimeModule runtime = runtimeScope.Runtime;
            BattleUnitState enemyUnit = BuildFormalTemplateProbeUnit(runtime, templateId);
            AssertTrue(enemyUnit != null, $"{templateId} 正式模板应能构建真实战斗单位。");
            if (enemyUnit == null)
            {
                continue;
            }

            if (enemyUnit.ai_brain_id == (StringName)"melee_aggressor")
            {
                continue;
            }

            int targetDistance = ResolveProbeTargetDistance(runtime, enemyUnit);
            BattleState state = BuildFlatState(new Vector2I(10, 5));
            runtime._state = state;
            enemyUnit.set_anchor_coord(new Vector2I(1, 2));
            enemyUnit.ai_state_id = "pressure";
            enemyUnit.current_move_points = 2;
            var player = BuildManualUnit(
                "pressure_probe_target",
                "压力目标",
                "player",
                new Vector2I(1 + targetDistance, 2),
                new[] { "basic_attack" }
            );
            var secondPlayer = BuildManualUnit(
                "pressure_probe_cluster",
                "压力副目标",
                "player",
                new Vector2I(1 + targetDistance, 3),
                new[] { "basic_attack" }
            );
            AddUnitToState(runtime, state, enemyUnit, isEnemy: true);
            AddUnitToState(runtime, state, player, isEnemy: false);
            AddUnitToState(runtime, state, secondPlayer, isEnemy: false);

            BattleAiDecision decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, enemyUnit));
            AssertTrue(decision != null && decision.command != null, $"{templateId} pressure probe 应产出正式 AI 指令。");
            if (decision?.command == null)
            {
                continue;
            }
            AssertEq(
                decision.command.command_type,
                BattleCommand.TYPE_SKILL(),
                $"{templateId} 在真实 stamina/weapon 投影下应至少有一个合法 pressure 技能动作，不应只 move/wait。"
            );
            BattlePreview preview = runtime.preview_command(decision.command);
            AssertTrue(preview != null && preview.allowed, $"{templateId} pressure 技能动作必须通过 runtime preview。");
        }
    }

    private void TestDepletedRangedTemplatesCloseForBasicAttackFallback()
    {
        foreach (StringName templateId in new StringName[] { "mist_beast", "mist_harrier", "mist_weaver", "wolf_shaman" })
        {
            using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
            BattleRuntimeModule runtime = runtimeScope.Runtime;
            BattleUnitState enemyUnit = BuildFormalTemplateProbeUnit(runtime, templateId);
            AssertTrue(enemyUnit != null, $"{templateId} depleted fallback probe 应能构建真实敌方单位。");
            if (enemyUnit == null)
            {
                continue;
            }

            int basicStaminaCost = ResolveBasicAttackStaminaCost(runtime);
            int targetDistance = Math.Max(ResolveProbeTargetDistance(runtime, enemyUnit), 3);
            BattleState state = BuildFlatState(new Vector2I(10, 5));
            runtime._state = state;
            enemyUnit.set_anchor_coord(new Vector2I(1, 2));
            enemyUnit.ai_state_id = "pressure";
            enemyUnit.current_mp = 0;
            enemyUnit.current_aura = 0;
            enemyUnit.current_stamina = basicStaminaCost;
            enemyUnit.attribute_snapshot.set_value("stamina_max", basicStaminaCost);
            enemyUnit.current_move_points = 2;
            BlockNonBasicSkills(enemyUnit);
            BattleUnitState player = BuildManualUnit(
                "depleted_range_target",
                "耗竭远距目标",
                "player",
                new Vector2I(1 + targetDistance, 2),
                new[] { "basic_attack" }
            );
            AddUnitToState(runtime, state, enemyUnit, isEnemy: true);
            AddUnitToState(runtime, state, player, isEnemy: false);
            BattleAiDecision moveDecision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, enemyUnit));
            AssertTrue(moveDecision != null && moveDecision.command != null, $"{templateId} 法力耗尽时仍应产出 fallback 指令。");
            if (moveDecision?.command != null)
            {
                bool isMoveOrBasicAttack =
                    moveDecision.command.command_type == BattleCommand.TYPE_MOVE()
                    || (
                        moveDecision.command.command_type == BattleCommand.TYPE_SKILL()
                        && moveDecision.command.skill_id == (StringName)"basic_attack"
                    );
                AssertTrue(
                    isMoveOrBasicAttack,
                    $"{templateId} 高阶动作不可用且处于远程距离带时，应推进到 basic_attack 距离或直接使用可达 basic_attack，而不是待机。"
                );
            }

            BattleState adjacentState = BuildFlatState(new Vector2I(5, 3));
            runtime._state = adjacentState;
            enemyUnit.set_anchor_coord(new Vector2I(1, 1));
            enemyUnit.ai_state_id = "pressure";
            enemyUnit.current_mp = 0;
            enemyUnit.current_aura = 0;
            enemyUnit.current_stamina = basicStaminaCost;
            enemyUnit.current_move_points = 2;
            BlockNonBasicSkills(enemyUnit);
            BattleUnitState adjacentPlayer = BuildManualUnit(
                "depleted_adjacent_target",
                "耗竭近距目标",
                "player",
                new Vector2I(2, 1),
                new[] { "basic_attack" }
            );
            AddUnitToState(runtime, adjacentState, enemyUnit, isEnemy: true);
            AddUnitToState(runtime, adjacentState, adjacentPlayer, isEnemy: false);
            BattleAiDecision attackDecision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, enemyUnit));
            AssertTrue(attackDecision != null && attackDecision.command != null, $"{templateId} 近身 depleted fallback 应产出基础攻击。");
            if (attackDecision?.command != null)
            {
                AssertEq(
                    attackDecision.command.skill_id,
                    new StringName("basic_attack"),
                    $"{templateId} 高阶资源耗尽且已近身时，应使用 basic_attack fallback。"
                );
            }
        }
    }

    private void TestEnemyTemplateUsesCanonicalTemplateId()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleState state = runtimeScope.Runtime.start_battle(
            BuildEncounterAnchor("encounter_wolf_pack_canonical", "wolf_pack", "荒狼群"),
            102,
            BuildBattleStartContext("ally_a")
        );
        AssertTrue(IsStartedState(state), "wolf_pack 正式 template_id 应能创建战斗状态。");
        if (!IsStartedState(state))
        {
            return;
        }
        AssertTrue(state.TryGetUnitTyped(state.enemy_unit_ids[0], out BattleUnitState enemyUnit), "wolf_pack battle state 应能取到敌方单位。");
        if (enemyUnit == null)
        {
            return;
        }
        AssertEq(enemyUnit.enemy_template_id, new StringName("wolf_pack"), "敌方单位应保留正式 wolf_pack template_id。");
        AssertEq(enemyUnit.ai_brain_id, new StringName("melee_aggressor"), "wolf_pack 正式模板应解析到 melee_aggressor AI。");
    }

    private void AssertTemplateStartBattle(
        StringName encounterId,
        StringName templateId,
        string displayName,
        int expectedEnemyCount,
        StringName expectedBrainId,
        StringName expectedStateId,
        IReadOnlyList<string> requiredSkills
    )
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleState state = runtimeScope.Runtime.start_battle(
            BuildEncounterAnchor(encounterId, templateId, displayName),
            101,
            BuildBattleStartContext("ally_a", "ally_b")
        );
        AssertTrue(IsStartedState(state), $"{templateId} 正式 battle start 应能创建基于敌方模板的战斗状态。");
        if (!IsStartedState(state))
        {
            return;
        }
        AssertEq(state.enemy_unit_ids.Count, expectedEnemyCount, $"{templateId} 模板应构建 {expectedEnemyCount} 个敌方单位。");
        AssertTrue(state.TryGetUnitTyped(state.enemy_unit_ids[0], out BattleUnitState enemyUnit), $"{templateId} battle state 应能取到敌方单位。");
        if (enemyUnit == null)
        {
            return;
        }
        AssertEq(enemyUnit.ai_brain_id, expectedBrainId, $"{templateId} 应绑定 {expectedBrainId} brain。");
        AssertEq(enemyUnit.ai_state_id, expectedStateId, $"{templateId} 应写入 {expectedStateId} 初始状态。");
        foreach (string rawSkillId in requiredSkills)
        {
            StringName skillId = rawSkillId;
            AssertTrue(enemyUnit.known_active_skill_ids.Contains(skillId), $"{templateId} 应携带 {skillId}。");
        }
    }

    private static BattleRuntimeScope BuildRuntimeWithEnemyContent()
    {
        var gameSession = new GameSession();
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            gameSession.get_skill_defs(),
            gameSession.get_enemy_templates(),
            gameSession.get_enemy_ai_brains(),
            null
        );
        runtime.configure_hit_resolver_for_tests(new FixedHitResolver(10));
        var damageResolver = new FixedSuccessOneDamageResolver();
        damageResolver.set_skill_defs(runtime.get_skill_defs());
        runtime.configure_damage_resolver_for_tests(damageResolver);
        gameSession.Free();
        return new BattleRuntimeScope(runtime);
    }

    private static EncounterAnchorData BuildEncounterAnchor(
        StringName entityId,
        StringName templateId,
        string displayName
    )
    {
        return new EncounterAnchorData
        {
            entity_id = entityId,
            display_name = displayName,
            enemy_roster_template_id = templateId,
            faction_id = "hostile",
            world_coord = Vector2I.Zero,
            region_tag = "default",
        };
    }

    private static GDictionary BuildBattleStartContext(params StringName[] allyMemberIds)
    {
        var allies = new GStringNameArray();
        foreach (StringName allyMemberId in allyMemberIds)
        {
            allies.Add(allyMemberId);
        }
        return new GDictionary
        {
            ["ally_member_ids"] = allies,
            ["default_active_skill_ids"] = new GStringNameArray { "warrior_heavy_strike" },
            ["validate_spawn_reachability"] = false,
        };
    }

    private static bool IsStartedState(BattleState state) => state != null && !state.is_empty();

    private static List<StringName> GetFormalEnemyTemplateIds()
    {
        using var gameSession = new GameSessionScope();
        var results = new List<StringName>();
        foreach (Variant key in gameSession.Session.get_enemy_templates().Keys)
        {
            results.Add(ProgressionDataUtils.to_string_name(key));
        }
        results.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return results;
    }

    private static BattleUnitState BuildFormalTemplateProbeUnit(BattleRuntimeModule runtime, StringName templateId)
    {
        BattleState state = runtime.start_battle(
            BuildEncounterAnchor($"probe_{templateId}", templateId, templateId.ToString()),
            1701,
            BuildBattleStartContext("ally_probe")
        );
        if (!IsStartedState(state) || state.enemy_unit_ids.Count == 0)
        {
            return null;
        }
        state.TryGetUnitTyped(state.enemy_unit_ids[0], out BattleUnitState enemyUnit);
        return enemyUnit;
    }

    private static int ResolveProbeTargetDistance(BattleRuntimeModule runtime, BattleUnitState enemyUnit)
    {
        if (runtime == null || enemyUnit == null)
        {
            return 1;
        }
        if (
            !runtime._enemy_ai_brains.ContainsKey(enemyUnit.ai_brain_id)
            || runtime._enemy_ai_brains[enemyUnit.ai_brain_id].As<EnemyAiBrainDef>() is not EnemyAiBrainDef brain
        )
        {
            return 1;
        }
        foreach (EnemyAiTransitionRuleDef rule in brain.transition_rules)
        {
            if (rule == null || rule.rule_id != (StringName)"pressure_enter")
            {
                continue;
            }
            foreach (EnemyAiTransitionConditionDef condition in rule.GetTypedConditions())
            {
                if (condition != null && condition.predicate == (StringName)"nearest_enemy_distance_at_or_below")
                {
                    return Math.Clamp(condition.max_distance, 1, 7);
                }
            }
        }
        return 1;
    }

    private static int ResolveBasicAttackStaminaCost(BattleRuntimeModule runtime)
    {
        if (
            runtime == null
            || !runtime._skill_defs.ContainsKey("basic_attack")
            || runtime._skill_defs["basic_attack"].As<SkillDef>() is not SkillDef skillDef
            || skillDef.combat_profile == null
        )
        {
            return 5;
        }
        GDictionary costs = skillDef.combat_profile.get_effective_resource_costs(1);
        return Math.Max(ReadInt(costs, "stamina_cost", skillDef.combat_profile.stamina_cost), 0);
    }

    private static BattleState BuildFlatState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "ai_enemy_template_runtime_regression",
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
                    base_terrain = BattleCellState.TERRAIN_LAND(),
                    base_height = 4,
                    height_offset = 0,
                };
                cell.recalculate_runtime_values();
                state.cells[cell.coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
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
            skill_defs = runtime._skill_defs,
            move_cost_callback = (unit, targetCoord) =>
                runtime._get_ai_move_query_cost(unit.unit_id, unit.coord, targetCoord),
            runtime_action_plan = actionPlan,
        };
        runtime._bind_ai_helper_services_for_decision(unitState, context);
        return context;
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
        unit.set_anchor_coord(coord);
        SeedBaseAttributesAndArmorClass(unit, hpMax: 30, staminaMax: 8, attackBonus: 6);
        unit.attribute_snapshot.set_value("action_points", 2);
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
        AssertTrue(
            runtime._grid_service.place_unit(state, unit, unit.coord, true),
            $"测试单位 {unit.unit_id} 应能放入测试战场。"
        );
    }

    private static void BlockNonBasicSkills(BattleUnitState unitState)
    {
        if (unitState == null)
        {
            return;
        }
        foreach (StringName skillId in unitState.known_active_skill_ids)
        {
            if (skillId == (StringName)"basic_attack")
            {
                continue;
            }
            unitState.current_mp = 0;
            unitState.current_aura = 0;
            unitState.cooldowns[skillId] = 30;
        }
    }

    private static void SeedBaseAttributesAndArmorClass(
        BattleUnitState unit,
        int hpMax,
        int staminaMax,
        int attackBonus
    )
    {
        foreach (StringName attributeId in UnitBaseAttributes.BASE_ATTRIBUTE_IDS())
        {
            if (!unit.attribute_snapshot.has_value(attributeId))
            {
                unit.attribute_snapshot.set_value(attributeId, 10);
            }
        }
        unit.attribute_snapshot.set_value("hp_max", hpMax);
        unit.attribute_snapshot.set_value("stamina_max", staminaMax);
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), attackBonus);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 10);
    }

    private static int ReadInt(GDictionary source, StringName key, int fallback)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return fallback;
        }
        return source[key].AsInt32();
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
        {
            _failures.Add($"{message} | actual={actual} expected={expected}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
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

    private sealed class GameSessionScope : IDisposable
    {
        internal GameSessionScope()
        {
            Session = new GameSession();
        }

        internal GameSession Session { get; }

        public void Dispose()
        {
            Session?.Free();
        }
    }
}
