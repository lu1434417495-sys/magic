using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_ai_enemy_template_runtime_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestTemplateStartBattleStableIds();
            TestWolfTemplatesSpawnWithPositiveStaminaPool();
            TestUnitFactoryDoesNotBuildFallbackEnemy();
            TestFormalEnemyTemplatesHaveRealPressureSkillAction();
            TestDepletedRangedTemplatesCloseForBasicAttackFallback();
            TestEnemyTemplateUsesCanonicalTemplateId();
            RequestTestExit(_test.Finish("Battle AI enemy template runtime regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Battle AI enemy template runtime regression", 1));
        }
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
            requiredSkills: new[] { "basic_attack" }
        );
        AssertTemplateStartBattle(
            "encounter_vanguard",
            "wolf_vanguard",
            "荒狼先锋",
            expectedEnemyCount: 1,
            expectedBrainId: "frontline_bulwark",
            expectedStateId: "engage",
            requiredSkills: new[] { "warrior_heavy_strike", "basic_attack" }
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
        AssertTemplateStartBattle(
            "encounter_red_dragon",
            "red_dragon",
            "红龙",
            expectedEnemyCount: 1,
            expectedBrainId: "dragon_tyrant",
            expectedStateId: "engage",
            requiredSkills: new[] { "dragon_breath_fire_cone", "dragon_breath_fire_line", "basic_attack" }
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
            BattleState state = runtime.StartBattle(
                BuildEncounterAnchor(encounterId, templateId, displayName),
                106,
                BuildBattleStartContext("ally_a", "ally_b")
            );
            _test.True(IsStartedState(state), $"{templateId} 模板应能正式生成战斗状态。");
            if (!IsStartedState(state))
            {
                continue;
            }
            _test.True(state.enemy_unit_ids.Count > 0, $"{templateId} 模板应至少生成一个敌方单位。");
            foreach (StringName enemyUnitId in state.enemy_unit_ids)
            {
                _test.True(state.TryGetUnitTyped(enemyUnitId, out BattleUnitState enemyUnit), $"{templateId} 模板生成的敌方单位应存在于 battle state 中。");
                if (enemyUnit == null)
                {
                    continue;
                }
                _test.True(
                    enemyUnit.attribute_snapshot.GetValue("stamina_max") > 0,
                    $"{templateId} 模板生成的敌方单位 stamina_max 应为正值。"
                );
                _test.True(
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
                gameSession.Session.GetSkillDefinitionsTyped(),
                new Dictionary<StringName, EnemyTemplateDef>(),
                new Dictionary<StringName, EnemyAiBrainDef>(),
                null
            );
            var enemyUnits = runtime._unit_factory.BuildEnemyUnits(
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
            _test.True(
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
            _test.True(enemyUnit != null, $"{templateId} 正式模板应能构建真实战斗单位。");
            if (enemyUnit == null)
            {
                continue;
            }

            if (enemyUnit.ai_brain_id == (StringName)"melee_aggressor")
            {
                BattleTestFixture.DisposeBattleUnit(enemyUnit);
                continue;
            }

            int targetDistance = ResolveProbeTargetDistance(runtime, enemyUnit);
            BattleState state = BuildFlatState(new Vector2I(10, 5));
            runtime.SetupStateForTests(state);
            enemyUnit.SetAnchorCoord(new Vector2I(1, 2));
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

            BattleAiDecision decision = null;
            BattlePreview preview = null;
            try
            {
                decision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, enemyUnit));
                _test.True(decision != null && decision.command != null, $"{templateId} pressure probe 应产出正式 AI 指令。");
                if (decision?.command == null)
                {
                    continue;
                }
                _test.Eq(
                    decision.command.command_type,
                    BattleTypedNames.ToStringName(BattleCommandKind.Skill),
                    $"{templateId} 在真实 stamina/weapon 投影下应至少有一个合法 pressure 技能动作，不应只 move/wait。"
                );
                preview = runtime.PreviewCommand(decision.command);
                _test.True(preview != null && preview.allowed, $"{templateId} pressure 技能动作必须通过 runtime preview。");
            }
            finally
            {
                BattleTestFixture.DisposeBattlePreview(preview);
                DisposeDecision(decision);
            }
        }
    }

    private void TestDepletedRangedTemplatesCloseForBasicAttackFallback()
    {
        foreach (StringName templateId in new StringName[] { "mist_beast", "mist_harrier", "mist_weaver", "wolf_shaman" })
        {
            using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
            BattleRuntimeModule runtime = runtimeScope.Runtime;
            BattleUnitState enemyUnit = BuildFormalTemplateProbeUnit(runtime, templateId);
            _test.True(enemyUnit != null, $"{templateId} depleted fallback probe 应能构建真实敌方单位。");
            if (enemyUnit == null)
            {
                continue;
            }

            int basicStaminaCost = ResolveBasicAttackStaminaCost(runtime);
            int targetDistance = Math.Max(ResolveProbeTargetDistance(runtime, enemyUnit), 3);
            BattleState state = BuildFlatState(new Vector2I(10, 5));
            runtime.SetupStateForTests(state);
            enemyUnit.SetAnchorCoord(new Vector2I(1, 2));
            enemyUnit.ai_state_id = "pressure";
            enemyUnit.current_mp = 0;
            enemyUnit.current_aura = 0;
            enemyUnit.current_stamina = basicStaminaCost;
            enemyUnit.attribute_snapshot.SetValue("stamina_max", basicStaminaCost);
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
            BattleAiDecision moveDecision = null;
            try
            {
                moveDecision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, enemyUnit));
                _test.True(moveDecision != null && moveDecision.command != null, $"{templateId} 法力耗尽时仍应产出 fallback 指令。");
                if (moveDecision?.command != null)
                {
                    bool isMoveOrBasicAttack =
                        moveDecision.command.command_type == BattleTypedNames.ToStringName(BattleCommandKind.Move)
                        || (
                            moveDecision.command.command_type == BattleTypedNames.ToStringName(BattleCommandKind.Skill)
                            && moveDecision.command.skill_id == (StringName)"basic_attack"
                        );
                    _test.True(
                        isMoveOrBasicAttack,
                        $"{templateId} 高阶动作不可用且处于远程距离带时，应推进到 basic_attack 距离或直接使用可达 basic_attack，而不是待机。"
                    );
                }
            }
            finally
            {
                DisposeDecision(moveDecision);
            }

            runtime.SetupStateForTests(null);
            BattleTestFixture.DisposeBattleState(state);

            enemyUnit = BuildFormalTemplateProbeUnit(runtime, templateId);
            BattleState adjacentState = BuildFlatState(new Vector2I(5, 3));
            runtime.SetupStateForTests(adjacentState);
            enemyUnit.SetAnchorCoord(new Vector2I(1, 1));
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
            BattleAiDecision attackDecision = null;
            try
            {
                attackDecision = runtime._ai_service.ChooseCommand(BuildAiContext(runtime, enemyUnit));
                _test.True(attackDecision != null && attackDecision.command != null, $"{templateId} 近身 depleted fallback 应产出基础攻击。");
                if (attackDecision?.command != null)
                {
                    _test.Eq(
                        attackDecision.command.skill_id,
                        new StringName("basic_attack"),
                        $"{templateId} 高阶资源耗尽且已近身时，应使用 basic_attack fallback。"
                    );
                }
            }
            finally
            {
                DisposeDecision(attackDecision);
            }
        }
    }

    private void TestEnemyTemplateUsesCanonicalTemplateId()
    {
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent();
        BattleState state = runtimeScope.Runtime.StartBattle(
            BuildEncounterAnchor("encounter_wolf_pack_canonical", "wolf_pack", "荒狼群"),
            102,
            BuildBattleStartContext("ally_a")
        );
        _test.True(IsStartedState(state), "wolf_pack 正式 template_id 应能创建战斗状态。");
        if (!IsStartedState(state))
        {
            return;
        }
        _test.True(state.TryGetUnitTyped(state.enemy_unit_ids[0], out BattleUnitState enemyUnit), "wolf_pack battle state 应能取到敌方单位。");
        if (enemyUnit == null)
        {
            return;
        }
        _test.Eq(enemyUnit.enemy_template_id, new StringName("wolf_pack"), "敌方单位应保留正式 wolf_pack template_id。");
        _test.Eq(enemyUnit.ai_brain_id, new StringName("melee_aggressor"), "wolf_pack 正式模板应解析到 melee_aggressor AI。");
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
        BattleState state = runtimeScope.Runtime.StartBattle(
            BuildEncounterAnchor(encounterId, templateId, displayName),
            101,
            BuildBattleStartContext("ally_a", "ally_b")
        );
        _test.True(IsStartedState(state), $"{templateId} 正式 battle start 应能创建基于敌方模板的战斗状态。");
        if (!IsStartedState(state))
        {
            return;
        }
        _test.Eq(state.enemy_unit_ids.Count, expectedEnemyCount, $"{templateId} 模板应构建 {expectedEnemyCount} 个敌方单位。");
        _test.True(state.TryGetUnitTyped(state.enemy_unit_ids[0], out BattleUnitState enemyUnit), $"{templateId} battle state 应能取到敌方单位。");
        if (enemyUnit == null)
        {
            return;
        }
        _test.Eq(enemyUnit.ai_brain_id, expectedBrainId, $"{templateId} 应绑定 {expectedBrainId} brain。");
        _test.Eq(enemyUnit.ai_state_id, expectedStateId, $"{templateId} 应写入 {expectedStateId} 初始状态。");
        foreach (string rawSkillId in requiredSkills)
        {
            StringName skillId = rawSkillId;
            _test.True(enemyUnit.known_active_skill_ids.Contains(skillId), $"{templateId} 应携带 {skillId}。");
        }
    }

    private static BattleRuntimeScope BuildRuntimeWithEnemyContent()
    {
        var gameSession = new GameSession();
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            gameSession.GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplatesTyped(),
            gameSession.GetEnemyAiBrainsTyped(),
            null
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        var damageResolver = new FixedSuccessOneDamageResolver();
        damageResolver.SetSkillDefinitions(runtime.GetSkillDefinitionIndexTyped());
        runtime.ConfigureDamageResolverForTests(damageResolver);
        return new BattleRuntimeScope(runtime, gameSession);
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

    private static bool IsStartedState(BattleState state) => state != null && !state.IsEmpty();

    private static List<StringName> GetFormalEnemyTemplateIds()
    {
        using var gameSession = new GameSessionScope();
        var results = new List<StringName>();
        results.AddRange(gameSession.Session.GetEnemyTemplatesTyped().Keys);
        results.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return results;
    }

    private static BattleUnitState BuildFormalTemplateProbeUnit(BattleRuntimeModule runtime, StringName templateId)
    {
        BattleState state = null;
        try
        {
            state = runtime.StartBattle(
                BuildEncounterAnchor($"probe_{templateId}", templateId, templateId.ToString()),
                1701,
                BuildBattleStartContext("ally_probe")
            );
            if (!IsStartedState(state) || state.enemy_unit_ids.Count == 0)
            {
                return null;
            }
            state.TryGetUnitTyped(state.enemy_unit_ids[0], out BattleUnitState enemyUnit);
            DetachUnitFromState(state, enemyUnit);
            return enemyUnit;
        }
        finally
        {
            runtime.SetupStateForTests(null);
            BattleTestFixture.DisposeBattleState(state);
        }
    }

    private static void DetachUnitFromState(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
        {
            return;
        }
        state.RemoveUnit(unit.unit_id);
        state.ally_unit_ids.Remove(unit.unit_id);
        state.enemy_unit_ids.Remove(unit.unit_id);
    }

    private static int ResolveProbeTargetDistance(BattleRuntimeModule runtime, BattleUnitState enemyUnit)
    {
        if (runtime == null || enemyUnit == null)
        {
            return 1;
        }
        if (
            !runtime.GetEnemyAiBrainIndexTyped().TryGetValue(enemyUnit.ai_brain_id, out EnemyAiBrainDef brain)
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
            || !runtime
                .GetSkillDefinitionIndexTyped()
                .TryGetValue("basic_attack", out SkillDefinition skillDefinition)
            || skillDefinition.CombatProfile == null
        )
        {
            return 5;
        }
        CombatSkillResourceCosts costs =
            skillDefinition.CombatProfile.GetEffectiveResourceCostValues(1);
        return Math.Max(costs.StaminaCost, 0);
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
        SeedBaseAttributesAndArmorClass(unit, hpMax: 30, staminaMax: 8, attackBonus: 6);
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

    private static int ReadInt(GDictionary source, StringName key, int fallback)
    {
        if (source == null || !source.ContainsKey(key))
        {
            return fallback;
        }
        return source[key].AsInt32();
    }

    private static void DisposeDecision(BattleAiDecision decision)
    {
        GodotSharpCleanup.ClearRuntimeReferences(decision?.command);
        BattleTestFixture.DisposeBattleAiScoreInput(decision?.score_input);
        BattleTestFixture.DisposeBattleAiScoreInput(decision?.skill_score_input);
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

    private sealed class GameSessionScope : IDisposable
    {
        internal GameSessionScope()
        {
            Session = new GameSession();
        }

        internal GameSession Session { get; }

        public void Dispose()
        {
            Session?.Dispose();
        }
    }
}
