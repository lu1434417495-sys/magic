using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_meteor_swarm_ai_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestMeteorSwarmAiUsesSpecialScoreFields();
        TestMeteorSwarmUseCasesAndHighPriorityTrace();
        TestMeteorSwarmFriendlyFireSoftAndProtectedPaths();

        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Meteor swarm AI regression"));
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
        Fixture setup = null;
        BattleCommand command = null;
        BattlePreview preview = null;
        BattleAiScoreInput scoreInput = null;
        try
        {
            setup = BuildRuntimeFixture(new Vector2I(9, 9), new[] { enemyCenter, enemyOuter, allyInner });
            SkillDef skillDef = GetSkill(setup.SkillDefs, "mage_meteor_swarm");

            var assembler = new BattleAiActionAssembler();
            _test.True(
                assembler.IsOffensiveOrEnemySkill(skillDef),
                "AI action assembler 应把 effectless meteor special profile 识别为进攻技能。"
            );

            command = BuildCommand(setup.Caster, new Vector2I(4, 4));
            preview = setup.Runtime.PreviewCommand(command);
            _test.True(preview != null && preview.allowed, "AI regression 前置：陨星雨 preview 应可用。");
            var aiContext = new BattleAiContext
            {
                state = setup.Runtime.GetState(),
                unit_state = setup.Caster,
                grid_service = setup.Runtime.GetGridService(),
            };
            aiContext.SetSkillDefs(setup.SkillDefIndex);
            var scoreService = new BattleAiScoreService();
            scoreInput = scoreService.BuildSkillScoreInput(
                aiContext,
                skillDef,
                command,
                preview,
                Array.Empty<CombatEffectDef>(),
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["action_kind"] = "ground_skill",
                    ["action_label"] = "陨星雨",
                }
            );
            _test.True(scoreInput != null, "AI 应能构造 meteor special score input。");
            if (scoreInput == null)
            {
                return;
            }

            _test.Eq(scoreInput.enemy_target_count, 2, "AI 应识别两个敌方目标。");
            _test.Eq(scoreInput.estimated_friendly_fire_target_count, 1, "AI 应从 numeric summary 识别一个友伤目标。");
            _test.True(scoreInput.estimated_enemy_damage > 0, "AI 应估算敌方伤害。");
            _test.True(scoreInput.estimated_terrain_effect_count >= 49, "AI 应估算陨星雨地形收益。");
            _test.True(scoreInput.attack_roll_modifier_breakdown.Count >= 1, "AI trace 应携带尘土命中修正 breakdown。");
            _test.True(!string.IsNullOrEmpty(scoreInput.friendly_fire_reject_reason), "AI 应标记友伤 hard reject reason。");
            _test.Eq(scoreInput.meteor_use_case, new StringName("unsafe_friendly_fire"), "友伤 hard reject 时 meteor_use_case 应进入 unsafe。");

            var action = new UseGroundSkillAction
            {
                maximum_friendly_fire_target_count = 99,
                allow_friendly_lethal = true,
            };
            _test.True(
                !action.PassesFriendlyFireLimits(scoreInput),
                "UseGroundSkillAction 应优先遵守 meteor hard reject，而不是粗略友伤数量。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleAiScoreInput(scoreInput);
            BattleTestFixture.DisposeBattlePreview(preview);
            GodotSharpCleanup.DisposeGodotObject(command);
            setup?.Dispose();
        }
    }

    private void TestMeteorSwarmUseCasesAndHighPriorityTrace()
    {
        BattleUnitState clusterA = BuildUnit("meteor_cluster_a", "集群敌A", "enemy", new Vector2I(5, 4), 300);
        BattleUnitState clusterB = BuildUnit("meteor_cluster_b", "集群敌B", "enemy", new Vector2I(6, 4), 300);
        BattleUnitState clusterC = BuildUnit("meteor_cluster_c", "集群敌C", "enemy", new Vector2I(7, 4), 300);
        Fixture clusterSetup = null;
        Fixture decapSetup = null;
        Fixture zoneSetup = null;
        BattleAiScoreInput clusterScore = null;
        BattleAiScoreInput decapScore = null;
        BattleAiScoreInput zoneScore = null;
        try
        {
            clusterSetup = BuildRuntimeFixture(
                new Vector2I(10, 10),
                new[] { clusterA, clusterB, clusterC }
            );
            clusterScore = BuildMeteorScoreInput(clusterSetup, new Vector2I(4, 4));
            _test.True(clusterScore != null, "cluster 用例应能构造 score input。");
            if (clusterScore != null)
            {
                _test.Eq(clusterScore.meteor_use_case, new StringName("cluster"), "3 个有效敌方目标应进入 cluster use-case。");
            }

            BattleUnitState eliteCenter = BuildUnit("meteor_decap_elite", "中心精英", "enemy", new Vector2I(4, 4), 1000);
            eliteCenter.attribute_snapshot.SetValue("fortune_mark_target", 1);
            decapSetup = BuildRuntimeFixture(new Vector2I(9, 9), new[] { eliteCenter });
            decapScore = BuildMeteorScoreInput(decapSetup, new Vector2I(4, 4));
            _test.True(decapScore != null, "decapitation 用例应能构造 score input。");
            if (decapScore != null)
            {
                _test.Eq(decapScore.meteor_use_case, new StringName("decapitation"), "中心直击 high-priority target 应进入 decapitation use-case。");
                _test.True(decapScore.high_priority_target_ids.Contains(eliteCenter.unit_id), "AI trace 应输出 high_priority_target_ids。");
                List<string> reasons = GetHighPriorityReasons(decapScore.high_priority_reasons, eliteCenter.unit_id);
                _test.True(
                    reasons.Contains("elite_or_boss"),
                    "high priority trace 应记录 elite/boss reason。"
                );
                GDictionary trace = BattleAiScoreProjection.Project(decapScore);
                _test.True(
                    trace.GetValueOrDefault("high_priority_target_ids", new GArray()).AsGodotArray().Contains(eliteCenter.unit_id),
                    "to_dict trace 应序列化 high_priority_target_ids。"
                );
                _test.True(trace.ContainsKey("high_priority_reasons"), "to_dict trace 应序列化 high_priority_reasons。");
                _test.True(trace.ContainsKey("low_value_penalty_reason"), "to_dict trace 应序列化 low_value_penalty_reason。");
            }

            BattleUnitState zoneEnemy = BuildUnit("meteor_zone_enemy", "压制敌人", "enemy", new Vector2I(6, 4), 1000);
            zoneSetup = BuildRuntimeFixture(new Vector2I(9, 9), new[] { zoneEnemy });
            zoneScore = BuildMeteorScoreInput(zoneSetup, new Vector2I(4, 4));
            _test.True(zoneScore != null, "zone_denial 用例应能构造 score input。");
            if (zoneScore != null)
            {
                _test.Eq(zoneScore.meteor_use_case, new StringName("zone_denial"), "无 cluster/decapitation 但地形压住敌人时应进入 zone_denial。");
            }
        }
        finally
        {
            BattleTestFixture.DisposeBattleAiScoreInput(clusterScore);
            BattleTestFixture.DisposeBattleAiScoreInput(decapScore);
            BattleTestFixture.DisposeBattleAiScoreInput(zoneScore);
            clusterSetup?.Dispose();
            decapSetup?.Dispose();
            zoneSetup?.Dispose();
        }
    }

    private void TestMeteorSwarmFriendlyFireSoftAndProtectedPaths()
    {
        BattleUnitState enemy = BuildUnit("meteor_soft_enemy", "软友伤敌人", "enemy", new Vector2I(4, 4), 1000);
        BattleUnitState sturdyAlly = BuildUnit("meteor_soft_ally", "高血友军", "player", new Vector2I(7, 7), 3000);
        Fixture softSetup = null;
        Fixture protectedSetup = null;
        BattleAiScoreInput softScore = null;
        BattleAiScoreInput protectedScore = null;
        try
        {
            softSetup = BuildRuntimeFixture(new Vector2I(10, 10), new[] { enemy, sturdyAlly });
            softScore = BuildMeteorScoreInput(softSetup, new Vector2I(4, 4));
            _test.True(softScore != null, "soft 友伤用例应能构造 score input。");
            if (softScore != null)
            {
                _test.Eq(softScore.estimated_friendly_fire_target_count, 1, "soft 友伤前置：应识别一个友军波及目标。");
                _test.Eq(softScore.friendly_fire_reject_reason, "", "低比例友伤应进入 soft penalty 而非 hard reject。");
                var defaultAction = new UseGroundSkillAction();
                _test.True(
                    defaultAction.PassesFriendlyFireLimits(softScore),
                    "Meteor soft 友伤不应被默认 friendly_fire_target_count=0 的通用上限挡掉。"
                );
            }

            BattleUnitState protectedAlly = BuildUnit("meteor_protected_ally", "受保护友军", "player", new Vector2I(7, 7), 3000);
            protectedAlly.ai_blackboard.SetBool("protected_ally", true);
            protectedSetup = BuildRuntimeFixture(new Vector2I(10, 10), new[] { enemy, protectedAlly });
            protectedScore = BuildMeteorScoreInput(protectedSetup, new Vector2I(4, 4));
            _test.True(protectedScore != null, "protected ally 用例应能构造 score input。");
            if (protectedScore != null)
            {
                _test.True(
                    protectedScore.friendly_fire_reject_reason.StartsWith("meteor_swarm_protected_ally", StringComparison.Ordinal),
                    $"protected ally 任意非零后果应 hard reject。 actual={protectedScore.friendly_fire_reject_reason}"
                );
                _test.Eq(protectedScore.meteor_use_case, new StringName("unsafe_friendly_fire"), "protected ally hard reject 应进入 unsafe use-case。");
            }
        }
        finally
        {
            BattleTestFixture.DisposeBattleAiScoreInput(softScore);
            BattleTestFixture.DisposeBattleAiScoreInput(protectedScore);
            softSetup?.Dispose();
            protectedSetup?.Dispose();
        }
    }

    private Fixture BuildRuntimeFixture(Vector2I mapSize, BattleUnitState[] extraUnits)
    {
        var progressionRegistry = new ProgressionContentRegistry();
        IReadOnlyDictionary<StringName, SkillDef> typedSkillDefs =
            progressionRegistry.GetSkillDefsTyped();
        var specialRegistry = new BattleSpecialProfileRegistry();
        specialRegistry.Rebuild(typedSkillDefs);
        _test.True(specialRegistry.Validate().Count == 0, "正式 special profile registry 应可用于 meteor AI fixture。");

        var runtime = new BattleRuntimeModule();
        runtime.setup(
            null,
            typedSkillDefs,
            new Dictionary<StringName, EnemyTemplateDef>(),
            new Dictionary<StringName, EnemyAiBrainDef>(),
            null,
            null,
            new Dictionary<StringName, ItemDef>(),
            null,
            default,
            specialRegistry.GetSnapshot()
        );
        BattleTestFixture.ConfigureHitResolverForTests(runtime, new FixedHitResolver(10));

        BattleState state = BuildState(mapSize);
        BattleUnitState caster = BuildUnit("meteor_ai_caster", "陨星术者", "player", new Vector2I(4, 0), 180);
        caster.known_active_skill_ids.Add("mage_meteor_swarm");
        caster.known_skill_level_map["mage_meteor_swarm"] = 9;
        caster.current_ap = 4;
        caster.current_mp = 200;
        caster.current_aura = 3;
        caster.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        caster.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        state.SetUnit(caster);
        state.ally_unit_ids.Add(caster.unit_id);
        foreach (BattleUnitState unit in extraUnits)
        {
            if (unit == null)
            {
                continue;
            }
            state.SetUnit(unit);
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
        foreach (var rawUnitId in state.UnitIndex.Keys)
        {
            BattleUnitState unitState = state.GetUnit(rawUnitId);
            if (unitState == null)
            {
                continue;
            }
            _test.True(
                runtime.GetGridService().PlaceUnit(state, unitState, unitState.coord, true),
                $"单位应能放入 meteor AI 棋盘：{unitState?.unit_id}"
            );
        }
        runtime.SetupStateForTests(state);
        return new Fixture
        {
            Runtime = runtime,
            State = state,
            Caster = caster,
            ProgressionRegistry = progressionRegistry,
            SpecialRegistry = specialRegistry,
            SkillDefIndex = typedSkillDefs,
            SkillDefs = ProjectSkillDefs(typedSkillDefs),
        };
    }

    private static GDictionary ProjectSkillDefs(
        IReadOnlyDictionary<StringName, SkillDef> skillDefs
    )
    {
        GDictionary result = new();
        if (skillDefs == null)
            return result;
        foreach ((StringName skillId, SkillDef skillDef) in skillDefs)
        {
            if (skillId == "" || skillDef == null)
                continue;
            result[skillId] = skillDef;
        }
        return result;
    }

    private BattleAiScoreInput BuildMeteorScoreInput(Fixture setup, Vector2I anchorCoord)
    {
        SkillDef skillDef = GetSkill(setup.SkillDefs, "mage_meteor_swarm");
        BattleCommand command = BuildCommand(setup.Caster, anchorCoord);
        BattlePreview preview = setup.Runtime.PreviewCommand(command);
        _test.True(preview != null && preview.allowed, "meteor score input helper 前置：preview 应可用。");
        if (preview == null || !preview.allowed)
        {
            BattleTestFixture.DisposeBattlePreview(preview);
            GodotSharpCleanup.DisposeGodotObject(command);
            return null;
        }
        var aiContext = new BattleAiContext
        {
            state = setup.Runtime.GetState(),
            unit_state = setup.Caster,
            grid_service = setup.Runtime.GetGridService(),
        };
        aiContext.SetSkillDefs(setup.SkillDefIndex);
        var scoreService = new BattleAiScoreService();
        BattleAiScoreInput scoreInput = scoreService.BuildSkillScoreInput(
            aiContext,
            skillDef,
            command,
            preview,
            Array.Empty<CombatEffectDef>(),
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["action_kind"] = "ground_skill",
                ["action_label"] = "陨星雨",
            }
        );
        if (scoreInput == null)
        {
            BattleTestFixture.DisposeBattlePreview(preview);
            GodotSharpCleanup.DisposeGodotObject(command);
        }
        return scoreInput;
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
                state.SetCell(coord, cell);
            }
        }
        state.RebuildCellColumns();
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
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), hp);
        SeedBaseAttributesAndDeriveAc(unit);
        unit.RefreshFootprint();
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
            if (!unit.attribute_snapshot.HasValue(attributeId))
            {
                unit.attribute_snapshot.SetValue(attributeId, 10);
            }
        }
        if (!unit.attribute_snapshot.HasValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass)))
        {
            int agilityModifier = AttributeSnapshot.CalculateScoreModifier(
                unit.attribute_snapshot.GetValue("agility")
            );
            unit.attribute_snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.ArmorClass),
                Math.Clamp(AttributeService.BASE_ARMOR_CLASS + agilityModifier, 1, 99)
            );
        }
    }

    private static BattleCommand BuildCommand(BattleUnitState caster, Vector2I anchorCoord)
    {
        var command = new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.Skill),
            unit_id = caster.unit_id,
            skill_id = "mage_meteor_swarm",
            target_coord = anchorCoord,
        };
        command.AddTargetCoord(anchorCoord);
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

    private static List<string> GetHighPriorityReasons(
        Dictionary<StringName, List<string>> reasonsByTargetId,
        StringName targetUnitId
    )
    {
        if (
            reasonsByTargetId != null
            && reasonsByTargetId.TryGetValue(targetUnitId, out List<string> reasons)
        )
        {
            return reasons;
        }
        return new List<string>();
    }

    private sealed class Fixture : IDisposable
    {
        public BattleRuntimeModule Runtime;
        public BattleState State;
        public BattleUnitState Caster;
        public ProgressionContentRegistry ProgressionRegistry;
        public BattleSpecialProfileRegistry SpecialRegistry;
        public IReadOnlyDictionary<StringName, SkillDef> SkillDefIndex;
        public GDictionary SkillDefs;

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
            GodotSharpCleanup.DisposeGodotObject(SpecialRegistry);
            GodotSharpCleanup.DisposeGodotObject(ProgressionRegistry);
            Runtime = null;
            State = null;
            Caster = null;
            ProgressionRegistry = null;
            SpecialRegistry = null;
            SkillDefIndex = null;
            SkillDefs = null;
        }
    }
}
