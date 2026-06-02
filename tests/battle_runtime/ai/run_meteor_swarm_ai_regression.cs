using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_meteor_swarm_ai_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestMeteorSwarmAiUsesSpecialScoreFields();
        TestMeteorSwarmUseCasesAndHighPriorityTrace();
        TestMeteorSwarmFriendlyFireSoftAndProtectedPaths();

        if (_failures.Count == 0)
        {
            GD.Print("Meteor swarm AI regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Meteor swarm AI regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestMeteorSwarmAiUsesSpecialScoreFields()
    {
        BattleUnitState enemyCenter = BuildUnit(
            "meteor_ai_enemy_center",
            "中心敌人",
            "enemy",
            new Vector2I(4, 4),
            120
        );
        BattleUnitState enemyOuter = BuildUnit(
            "meteor_ai_enemy_outer",
            "外圈敌人",
            "enemy",
            new Vector2I(7, 7),
            160
        );
        BattleUnitState allyInner = BuildUnit(
            "meteor_ai_ally_inner",
            "内圈友军",
            "player",
            new Vector2I(5, 4),
            160
        );
        Fixture setup = BuildRuntimeFixture(new Vector2I(9, 9), new[] { enemyCenter, enemyOuter, allyInner });
        SkillDef skillDef = GetSkill(setup.SkillDefs, "mage_meteor_swarm");

        var assembler = new BattleAiActionAssembler();
        AssertTrue(
            assembler.IsOffensiveOrEnemySkill(skillDef),
            "AI action assembler 应把 effectless meteor special profile 识别为进攻技能。"
        );

        BattleCommand command = BuildCommand(setup.Caster, new Vector2I(4, 4));
        BattlePreview preview = setup.Runtime.preview_command(command);
        AssertTrue(preview != null && preview.allowed, "AI regression 前置：陨星雨 preview 应可用。");
        var aiContext = new BattleAiContext
        {
            state = setup.Runtime.get_state(),
            unit_state = setup.Caster,
            grid_service = setup.Runtime.get_grid_service(),
            skill_defs = setup.SkillDefs,
        };
        var scoreService = new BattleAiScoreService();
        BattleAiScoreInput scoreInput = scoreService.BuildSkillScoreInput(
            aiContext,
            skillDef,
            command,
            preview,
            Array.Empty<CombatEffectDef>(),
            new GDictionary
            {
                ["action_kind"] = "ground_skill",
                ["action_label"] = "陨星雨",
            }
        );
        AssertTrue(scoreInput != null, "AI 应能构造 meteor special score input。");
        if (scoreInput == null)
        {
            return;
        }

        AssertEq(scoreInput.enemy_target_count, 2, "AI 应识别两个敌方目标。");
        AssertEq(scoreInput.estimated_friendly_fire_target_count, 1, "AI 应从 numeric summary 识别一个友伤目标。");
        AssertTrue(scoreInput.estimated_enemy_damage > 0, "AI 应估算敌方伤害。");
        AssertTrue(scoreInput.estimated_terrain_effect_count >= 49, "AI 应估算陨星雨地形收益。");
        AssertTrue(scoreInput.attack_roll_modifier_breakdown.Count >= 1, "AI trace 应携带尘土命中修正 breakdown。");
        AssertTrue(!string.IsNullOrEmpty(scoreInput.friendly_fire_reject_reason), "AI 应标记友伤 hard reject reason。");
        AssertEq(scoreInput.meteor_use_case, new StringName("unsafe_friendly_fire"), "友伤 hard reject 时 meteor_use_case 应进入 unsafe。");

        var action = new UseGroundSkillAction
        {
            maximum_friendly_fire_target_count = 99,
            allow_friendly_lethal = true,
        };
        AssertTrue(
            !action._passes_friendly_fire_limits(scoreInput),
            "UseGroundSkillAction 应优先遵守 meteor hard reject，而不是粗略友伤数量。"
        );
    }

    private void TestMeteorSwarmUseCasesAndHighPriorityTrace()
    {
        BattleUnitState clusterA = BuildUnit("meteor_cluster_a", "集群敌A", "enemy", new Vector2I(5, 4), 300);
        BattleUnitState clusterB = BuildUnit("meteor_cluster_b", "集群敌B", "enemy", new Vector2I(6, 4), 300);
        BattleUnitState clusterC = BuildUnit("meteor_cluster_c", "集群敌C", "enemy", new Vector2I(7, 4), 300);
        Fixture clusterSetup = BuildRuntimeFixture(
            new Vector2I(10, 10),
            new[] { clusterA, clusterB, clusterC }
        );
        BattleAiScoreInput clusterScore = BuildMeteorScoreInput(clusterSetup, new Vector2I(4, 4));
        AssertTrue(clusterScore != null, "cluster 用例应能构造 score input。");
        if (clusterScore != null)
        {
            AssertEq(clusterScore.meteor_use_case, new StringName("cluster"), "3 个有效敌方目标应进入 cluster use-case。");
        }

        BattleUnitState eliteCenter = BuildUnit("meteor_decap_elite", "中心精英", "enemy", new Vector2I(4, 4), 1000);
        eliteCenter.attribute_snapshot.set_value("fortune_mark_target", 1);
        Fixture decapSetup = BuildRuntimeFixture(new Vector2I(9, 9), new[] { eliteCenter });
        BattleAiScoreInput decapScore = BuildMeteorScoreInput(decapSetup, new Vector2I(4, 4));
        AssertTrue(decapScore != null, "decapitation 用例应能构造 score input。");
        if (decapScore != null)
        {
            AssertEq(decapScore.meteor_use_case, new StringName("decapitation"), "中心直击 high-priority target 应进入 decapitation use-case。");
            AssertTrue(decapScore.high_priority_target_ids.Contains(eliteCenter.unit_id), "AI trace 应输出 high_priority_target_ids。");
            GArray reasons = DictArray(decapScore.high_priority_reasons, eliteCenter.unit_id.ToString());
            AssertTrue(
                reasons.Contains("elite_or_boss"),
                "high priority trace 应记录 elite/boss reason。"
            );
            GDictionary trace = decapScore.ToDictionary();
            AssertTrue(
                trace.GetValueOrDefault("high_priority_target_ids", new GArray()).AsGodotArray().Contains(eliteCenter.unit_id),
                "to_dict trace 应序列化 high_priority_target_ids。"
            );
            AssertTrue(trace.ContainsKey("high_priority_reasons"), "to_dict trace 应序列化 high_priority_reasons。");
            AssertTrue(trace.ContainsKey("low_value_penalty_reason"), "to_dict trace 应序列化 low_value_penalty_reason。");
        }

        BattleUnitState zoneEnemy = BuildUnit("meteor_zone_enemy", "压制敌人", "enemy", new Vector2I(6, 4), 1000);
        Fixture zoneSetup = BuildRuntimeFixture(new Vector2I(9, 9), new[] { zoneEnemy });
        BattleAiScoreInput zoneScore = BuildMeteorScoreInput(zoneSetup, new Vector2I(4, 4));
        AssertTrue(zoneScore != null, "zone_denial 用例应能构造 score input。");
        if (zoneScore != null)
        {
            AssertEq(zoneScore.meteor_use_case, new StringName("zone_denial"), "无 cluster/decapitation 但地形压住敌人时应进入 zone_denial。");
        }
    }

    private void TestMeteorSwarmFriendlyFireSoftAndProtectedPaths()
    {
        BattleUnitState enemy = BuildUnit("meteor_soft_enemy", "软友伤敌人", "enemy", new Vector2I(4, 4), 1000);
        BattleUnitState sturdyAlly = BuildUnit("meteor_soft_ally", "高血友军", "player", new Vector2I(7, 7), 3000);
        Fixture softSetup = BuildRuntimeFixture(new Vector2I(10, 10), new[] { enemy, sturdyAlly });
        BattleAiScoreInput softScore = BuildMeteorScoreInput(softSetup, new Vector2I(4, 4));
        AssertTrue(softScore != null, "soft 友伤用例应能构造 score input。");
        if (softScore != null)
        {
            AssertEq(softScore.estimated_friendly_fire_target_count, 1, "soft 友伤前置：应识别一个友军波及目标。");
            AssertEq(softScore.friendly_fire_reject_reason, "", "低比例友伤应进入 soft penalty 而非 hard reject。");
            var defaultAction = new UseGroundSkillAction();
            AssertTrue(
                defaultAction._passes_friendly_fire_limits(softScore),
                "Meteor soft 友伤不应被默认 friendly_fire_target_count=0 的通用上限挡掉。"
            );
        }

        BattleUnitState protectedAlly = BuildUnit("meteor_protected_ally", "受保护友军", "player", new Vector2I(7, 7), 3000);
        protectedAlly.ai_blackboard.set_bool("protected_ally", true);
        Fixture protectedSetup = BuildRuntimeFixture(new Vector2I(10, 10), new[] { enemy, protectedAlly });
        BattleAiScoreInput protectedScore = BuildMeteorScoreInput(protectedSetup, new Vector2I(4, 4));
        AssertTrue(protectedScore != null, "protected ally 用例应能构造 score input。");
        if (protectedScore != null)
        {
            AssertTrue(
                protectedScore.friendly_fire_reject_reason.StartsWith("meteor_swarm_protected_ally", StringComparison.Ordinal),
                $"protected ally 任意非零后果应 hard reject。 actual={protectedScore.friendly_fire_reject_reason}"
            );
            AssertEq(protectedScore.meteor_use_case, new StringName("unsafe_friendly_fire"), "protected ally hard reject 应进入 unsafe use-case。");
        }
    }

    private Fixture BuildRuntimeFixture(Vector2I mapSize, BattleUnitState[] extraUnits)
    {
        var progressionRegistry = new ProgressionContentRegistry();
        GDictionary skillDefs = progressionRegistry.get_skill_defs();
        var specialRegistry = new BattleSpecialProfileRegistry();
        specialRegistry.rebuild(skillDefs);
        AssertTrue(specialRegistry.validate().Count == 0, "正式 special profile registry 应可用于 meteor AI fixture。");

        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            skillDefs,
            new GDictionary(),
            new GDictionary(),
            null,
            null,
            new GDictionary(),
            null,
            default,
            specialRegistry.get_snapshot()
        );
        runtime.configure_hit_resolver_for_tests(new FixedHitResolver(10));

        BattleState state = BuildState(mapSize);
        BattleUnitState caster = BuildUnit("meteor_ai_caster", "陨星术者", "player", new Vector2I(4, 0), 180);
        caster.known_active_skill_ids.Add("mage_meteor_swarm");
        caster.known_skill_level_map["mage_meteor_swarm"] = 9;
        caster.current_ap = 4;
        caster.current_mp = 200;
        caster.current_aura = 3;
        caster.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP());
        caster.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA());
        state.units[caster.unit_id] = caster;
        state.ally_unit_ids.Add(caster.unit_id);
        foreach (BattleUnitState unit in extraUnits)
        {
            if (unit == null)
            {
                continue;
            }
            state.units[unit.unit_id] = unit;
            if (unit.faction_id == caster.faction_id)
            {
                state.ally_unit_ids.Add(unit.unit_id);
            }
            else
            {
                state.enemy_unit_ids.Add(unit.unit_id);
            }
        }
        state.active_unit_id = caster.unit_id;
        foreach (var rawUnitId in state.units.Keys)
        {
            BattleUnitState unitState = state.units[rawUnitId].As<BattleUnitState>();
            if (unitState == null)
            {
                continue;
            }
            AssertTrue(
                runtime.get_grid_service().place_unit(state, unitState, unitState.coord, true),
                $"单位应能放入 meteor AI 棋盘：{unitState?.unit_id}"
            );
        }
        runtime._state = state;
        return new Fixture
        {
            Runtime = runtime,
            Caster = caster,
            SkillDefs = skillDefs,
        };
    }

    private BattleAiScoreInput BuildMeteorScoreInput(Fixture setup, Vector2I anchorCoord)
    {
        SkillDef skillDef = GetSkill(setup.SkillDefs, "mage_meteor_swarm");
        BattleCommand command = BuildCommand(setup.Caster, anchorCoord);
        BattlePreview preview = setup.Runtime.preview_command(command);
        AssertTrue(preview != null && preview.allowed, "meteor score input helper 前置：preview 应可用。");
        if (preview == null || !preview.allowed)
        {
            return null;
        }
        var aiContext = new BattleAiContext
        {
            state = setup.Runtime.get_state(),
            unit_state = setup.Caster,
            grid_service = setup.Runtime.get_grid_service(),
            skill_defs = setup.SkillDefs,
        };
        var scoreService = new BattleAiScoreService();
        return scoreService.BuildSkillScoreInput(
            aiContext,
            skillDef,
            command,
            preview,
            Array.Empty<CombatEffectDef>(),
            new GDictionary
            {
                ["action_kind"] = "ground_skill",
                ["action_label"] = "陨星雨",
            }
        );
    }

    private static BattleState BuildState(Vector2I mapSize)
    {
        var state = new BattleState
        {
            battle_id = "meteor_swarm_ai_regression",
            phase = "unit_acting",
            map_size = mapSize,
            timeline = new BattleTimelineState(),
        };
        for (int y = 0; y < mapSize.Y; y++)
        {
            for (int x = 0; x < mapSize.X; x++)
            {
                Vector2I coord = new(x, y);
                var cell = new BattleCellState
                {
                    coord = coord,
                    passable = true,
                };
                state.cells[coord] = cell;
            }
        }
        state.cell_columns = BattleCellState.build_columns_from_surface_cells(state.cells);
        return state;
    }

    private static BattleUnitState BuildUnit(
        StringName unitId,
        string displayName,
        StringName factionId,
        Vector2I coord,
        int hp
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = displayName,
            faction_id = factionId,
            coord = coord,
            is_alive = true,
            current_hp = hp,
        };
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), hp);
        SeedBaseAttributesAndDeriveAc(unit);
        unit.refresh_footprint();
        return unit;
    }

    private static void SeedBaseAttributesAndDeriveAc(BattleUnitState unit)
    {
        StringName[] baseAttributes =
        {
            "strength",
            "agility",
            "constitution",
            "perception",
            "intelligence",
            "willpower",
        };
        foreach (StringName attributeId in baseAttributes)
        {
            if (!unit.attribute_snapshot.has_value(attributeId))
            {
                unit.attribute_snapshot.set_value(attributeId, 10);
            }
        }
        if (!unit.attribute_snapshot.has_value(AttributeService.ARMOR_CLASS_ID()))
        {
            int agilityModifier = AttributeSnapshot.calculate_score_modifier(
                unit.attribute_snapshot.get_value("agility")
            );
            unit.attribute_snapshot.set_value(
                AttributeService.ARMOR_CLASS_ID(),
                Math.Clamp(AttributeService.BASE_ARMOR_CLASS_VALUE() + agilityModifier, 1, 99)
            );
        }
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, Vector2I anchorCoord)
    {
        var command = new BattleCommand
        {
            command_type = BattleCommand.TYPE_SKILL(),
            unit_id = caster.unit_id,
            skill_id = "mage_meteor_swarm",
            target_coord = anchorCoord,
        };
        command.target_coords.Add(anchorCoord);
        return command;
    }

    private static SkillDef GetSkill(GDictionary skillDefs, StringName skillId)
    {
        if (skillDefs == null || !skillDefs.ContainsKey(skillId))
        {
            return null;
        }
        return skillDefs[skillId].As<SkillDef>();
    }

    private static GArray DictArray(GDictionary dictionary, string key)
    {
        if (dictionary == null || string.IsNullOrEmpty(key))
        {
            return new GArray();
        }
        if (dictionary.ContainsKey(key))
        {
            return dictionary[key].AsGodotArray();
        }
        StringName stringNameKey = new(key);
        return dictionary.ContainsKey(stringNameKey)
            ? dictionary[stringNameKey].AsGodotArray()
            : new GArray();
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

    private sealed class Fixture
    {
        public BattleRuntimeModule Runtime;
        public BattleUnitState Caster;
        public GDictionary SkillDefs;
    }
}
