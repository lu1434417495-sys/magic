using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_enemy_template_runtime_start_regression : SceneTree
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
            TestFormalTemplatesResolveStableIds();
            TestWolfTemplatesSpawnWithPositiveStaminaPool();
            TestBattleUnitFactoryDoesNotBuildFallbackEnemy();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (_failures.Count == 0)
        {
            GD.Print("Enemy template runtime start regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Enemy template runtime start regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestFormalTemplatesResolveStableIds()
    {
        AssertTemplateStart(
            "encounter_wolf",
            "wolf_pack",
            "荒狼群",
            expectedEnemyCount: 2,
            expectedBrainId: "melee_aggressor",
            expectedStateId: "engage",
            requiredSkillIds: new[] { "charge", "basic_attack" }
        );
        AssertTemplateStart(
            "encounter_vanguard",
            "wolf_vanguard",
            "荒狼先锋",
            expectedEnemyCount: 1,
            expectedBrainId: "frontline_bulwark",
            expectedStateId: "engage",
            requiredSkillIds: new[] { "charge", "warrior_guard" }
        );
        AssertTemplateStart(
            "encounter_harrier",
            "mist_harrier",
            "雾沼猎压者",
            expectedEnemyCount: 1,
            expectedBrainId: "ranged_suppressor",
            expectedStateId: "pressure",
            requiredSkillIds: new[] { "archer_suppressive_fire", "archer_pinning_shot" }
        );
        AssertTemplateStart(
            "encounter_weaver",
            "mist_weaver",
            "雾沼织咒者",
            expectedEnemyCount: 1,
            expectedBrainId: "healer_controller",
            expectedStateId: "pressure",
            requiredSkillIds: new[] { "mage_temporal_rewind", "mage_glacial_prison" }
        );
    }

    private void TestWolfTemplatesSpawnWithPositiveStaminaPool()
    {
        string[] templateIds =
        {
            "wolf_pack",
            "wolf_raider",
            "wolf_alpha",
            "wolf_vanguard",
        };
        foreach (string templateId in templateIds)
        {
            using BattleRuntimeModule runtime = BuildRuntimeWithEnemyContent();
            BattleState state = StartTemplateBattle(
                runtime,
                $"encounter_{templateId}_stamina",
                templateId,
                templateId,
                seed: 106
            );
            AssertTrue(
                state != null && !state.is_empty(),
                $"{templateId} 模板应能正式生成战斗状态。"
            );
            if (state == null || state.is_empty())
            {
                continue;
            }
            AssertTrue(
                state.enemy_unit_ids.Count > 0,
                $"{templateId} 模板应至少生成一个敌方单位。"
            );
            foreach (StringName enemyUnitId in state.enemy_unit_ids)
            {
                BattleUnitState enemyUnit = GetUnit(state, enemyUnitId);
                AssertTrue(
                    enemyUnit != null,
                    $"{templateId} 模板生成的敌方单位应存在于 battle state 中。"
                );
                if (enemyUnit == null)
                {
                    continue;
                }
                AssertTrue(
                    enemyUnit.attribute_snapshot.get_value(AttributeService.STAMINA_MAX_ID()) > 0,
                    $"{templateId} 模板生成的敌方单位 stamina_max 应为正值。"
                );
                AssertTrue(
                    enemyUnit.current_stamina > 0,
                    $"{templateId} 模板生成的敌方单位 current_stamina 应为正值，避免技能链因资源池为 0 直接失效。"
                );
            }
        }
    }

    private void TestBattleUnitFactoryDoesNotBuildFallbackEnemy()
    {
        using var gameSession = new GameSession();
        using var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            gameSession.get_skill_defs(),
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

    private void AssertTemplateStart(
        StringName encounterId,
        StringName templateId,
        string displayName,
        int expectedEnemyCount,
        StringName expectedBrainId,
        StringName expectedStateId,
        string[] requiredSkillIds
    )
    {
        using BattleRuntimeModule runtime = BuildRuntimeWithEnemyContent();
        BattleState state = StartTemplateBattle(runtime, encounterId, templateId, displayName, seed: 101);
        AssertTrue(
            state != null && !state.is_empty(),
            $"{templateId} 正式 battle start 应能创建基于敌方模板的战斗状态。"
        );
        if (state == null || state.is_empty())
        {
            return;
        }
        AssertEq(
            state.enemy_unit_ids.Count,
            expectedEnemyCount,
            $"{templateId} 模板生成的敌方单位数量应符合配置。"
        );
        if (state.enemy_unit_ids.Count == 0)
        {
            return;
        }
        BattleUnitState enemyUnit = GetUnit(state, state.enemy_unit_ids[0]);
        AssertTrue(
            enemyUnit != null,
            $"{templateId} 模板生成的首个敌方单位应存在于 battle state 中。"
        );
        if (enemyUnit == null)
        {
            return;
        }
        AssertEq(
            enemyUnit.ai_brain_id,
            expectedBrainId,
            $"{templateId} 应绑定 {expectedBrainId} brain，而不是回落到默认敌人。"
        );
        AssertEq(
            enemyUnit.ai_state_id,
            expectedStateId,
            $"{templateId} 应写入 {expectedStateId} 初始 AI 状态。"
        );
        foreach (string skillId in requiredSkillIds)
        {
            AssertTrue(
                enemyUnit.known_active_skill_ids.Contains(new StringName(skillId)),
                $"{templateId} 模板应为敌人注入 {skillId} 技能。"
            );
        }
    }

    private static BattleRuntimeModule BuildRuntimeWithEnemyContent()
    {
        using var gameSession = new GameSession();
        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            gameSession.get_skill_defs(),
            gameSession.get_enemy_templates(),
            gameSession.get_enemy_ai_brains(),
            null
        );
        runtime.configure_hit_resolver_for_tests(new FixedHitResolver(10));
        return runtime;
    }

    private static BattleState StartTemplateBattle(
        BattleRuntimeModule runtime,
        StringName encounterId,
        StringName templateId,
        string displayName,
        int seed
    )
    {
        return runtime.start_battle(
            BuildEncounterAnchor(encounterId, templateId, displayName),
            seed,
            new GDictionary
            {
                ["ally_member_ids"] = new GStringNameArray { "ally_a", "ally_b" },
                ["default_active_skill_ids"] = new GStringNameArray { "warrior_heavy_strike" },
                ["validate_spawn_reachability"] = false,
            }
        );
    }

    private static EncounterAnchorData BuildEncounterAnchor(
        StringName encounterId,
        StringName templateId,
        string displayName
    )
    {
        return new EncounterAnchorData
        {
            entity_id = encounterId,
            display_name = displayName,
            world_coord = Vector2I.Zero,
            faction_id = "hostile",
            enemy_roster_template_id = templateId,
            region_tag = "mistwood",
            vision_range = 4,
            encounter_kind = EncounterAnchorData.ENCOUNTER_KIND_SINGLE(),
            encounter_profile_id = "test_enemy_template_runtime_start",
        };
    }

    private static BattleUnitState GetUnit(BattleState state, StringName unitId)
    {
        return state != null && state.TryGetUnitTyped(unitId, out BattleUnitState unitState)
            ? unitState
            : null;
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} actual={actual} expected={expected}");
        }
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }
}
