using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_starting_area_challenge_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private static readonly WorldStartingAreaDefinition Area = new(new Vector2I(200, 200), 2);
    private ContentSnapshot _snapshot;
    private EncounterChallengeCatalog _challenges;

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            _snapshot = GameSessionTestFactory.GetProcessSnapshot();
            _challenges = new(_snapshot.BattleEncounters, _snapshot.EncounterRosters, _snapshot.EnemyTemplates);
            TestBoundaries();
            TestIndependentRatingAndGeneration();
            TestOfficialWorldAndGrowth();
            TestQuestPlacementAndInsertion();
        }
        catch (Exception exception)
        {
            _test.True(false, exception.ToString());
        }
        finally
        {
            RequestTestExit(_test.Finish("Starting area challenge regression"));
        }
    }

    private void TestBoundaries()
    {
        var rules = new WorldStartingAreaRules(Area, new Vector2I(500, 500), _challenges);
        _test.Eq(rules.Bounds.Size, new Vector2I(200, 200), "出生区域必须恰好为 200 x 200。");
        foreach (Vector2I corner in new[] { new Vector2I(400, 400), new Vector2I(599, 400), new Vector2I(400, 599), new Vector2I(599, 599) })
        {
            _test.True(rules.Contains(corner), "方形四角也必须受限制，不能用曼哈顿距离代替。");
            _test.False(rules.Allows(corner, "mist_hollow", 0), "角落不能生成高挑战雾兽。");
        }
        foreach (Vector2I outside in new[] { new Vector2I(399, 500), new Vector2I(600, 500), new Vector2I(500, 399), new Vector2I(500, 600) })
            _test.True(rules.Allows(outside, "mist_hollow", 0), "边界外不受出生区上限约束。");
        _test.False(rules.Allows(new Vector2I(500, 500), "missing", 0), "未知敌方配置不得作为低挑战怪放行。");
        _test.True(rules.Allows(new Vector2I(500, 500), "wolf_wilds", 2), "等于上限 2 的座狼应允许。");
    }

    private void TestIndependentRatingAndGeneration()
    {
        var templates = new Dictionary<StringName, EnemyTemplateDefinition>
        {
            ["gentle"] = new TestEnemyTemplateDefinitionBuilder { TemplateId = "gentle", CreatureLevel = 20, ChallengeRating = 2 }.Build(),
            ["dangerous"] = new TestEnemyTemplateDefinitionBuilder { TemplateId = "dangerous", CreatureLevel = 0, ChallengeRating = 3 }.Build(),
        };
        var rosters = new Dictionary<StringName, WildEncounterRosterDefinition>();
        var encounters = new Dictionary<StringName, BattleEncounterDefinition>();
        foreach (StringName id in templates.Keys)
        {
            rosters[id] = TestEnemyDefinitionFactory.Roster(id, new[] {
                TestEnemyDefinitionFactory.RosterStage(0, TestEnemyDefinitionFactory.RosterUnit(id, 3))
            });
            encounters[id] = new BattleEncounterDefinition(id, id.ToString(), id,
                BattleEliminationObjectiveDefinition.Instance,
                new BattleEncounterWorldResolutionDefinition(BattleWorldResolutionMode.Clear,
                    BattleWorldResolutionMode.Preserve, BattleWorldResolutionMode.Preserve, 0));
        }
        var challenges = new EncounterChallengeCatalog(encounters, rosters, templates);
        var rules = new WorldStartingAreaRules(Area, new Vector2I(200, 200), challenges);
        _test.True(rules.Allows(new Vector2I(200, 200), "gentle", 0), "生物等级 20、挑战等级 2 应允许；数量不应累加为单怪挑战等级。");
        _test.False(rules.Allows(new Vector2I(200, 200), "dangerous", 0), "生物等级 0、挑战等级 3 必须排除。");
        var chunks = new List<Vector2I>();
        for (int y = 0; y < 4; y++)
        for (int x = 0; x < 4; x++)
            chunks.Add(new Vector2I(x, y));
        var distributions = templates.Keys.Select(id => new WildSpawnRuleDefinition(
            id.ToString(), WorldVerticalBandKind.North, id.ToString(), id, "", "",
            4, 0, 1, chunks
        )).ToArray();
        WorldGenerationDefinition definition = TestWorldGenerationDefinitionFactory.Create(
            worldSizeInChunks: new Vector2I(4, 4), chunkSize: new Vector2I(100, 100),
            playerStartCoord: new Vector2I(200, 200), wildMonsterDistribution: distributions,
            startingArea: Area
        );
        var grid = new WorldMapGridSystem();
        grid.Setup(definition.WorldSizeInChunks, definition.ChunkSize);
        var world = new WorldMapSpawnSystem().BuildWorldTyped(definition, grid, challenges);
        int inside = 0;
        bool dangerousOutside = false;
        foreach (var anchor in world.EncounterAnchors)
        {
            if (rules.Contains(anchor.world_coord))
            {
                inside++;
                _test.Eq(anchor.encounter_profile_id, new StringName("gentle"), "区域内应保留合法非狼遭遇，并替换超限遭遇。");
            }
            else if (anchor.encounter_profile_id == "dangerous")
                dangerousOutside = true;
        }
        _test.True(inside > 0, "夹具必须实际覆盖出生区域内生成。");
        _test.True(dangerousOutside, "区域外必须保留高挑战怪。");
        _test.Eq(world.EncounterAnchors.Count, 128, "有合法替代项时不得删掉遇敌密度。");
    }

    private void TestOfficialWorldAndGrowth()
    {
        foreach (string id in new[] { "test", "small", "medium", "giant" })
        {
            _test.Eq(_snapshot.WorldGenerations[id].StartingArea.Size, Area.Size, "标准世界都要配置出生区域。");
            _test.Eq(_snapshot.WorldGenerations[id].StartingArea.MaxChallengeRating, 2d, "标准世界上限应为 2。");
        }
        WorldGenerationDefinition definition = _snapshot.WorldGenerations["small"];
        var grid = new WorldMapGridSystem();
        grid.Setup(definition.WorldSizeInChunks, definition.ChunkSize);
        var world = new WorldMapSpawnSystem().BuildWorldTyped(definition, grid, _challenges);
        var rules = new WorldStartingAreaRules(definition.StartingArea, world.PlayerStartCoord, _challenges);
        _test.True(world.EncounterAnchors.Any(a => rules.Contains(a.world_coord)), "正式 small 新档出生区域需要实际遭遇。");
        foreach (var anchor in world.EncounterAnchors)
            _test.True(rules.Allows(anchor.world_coord, anchor.encounter_profile_id, anchor.growth_stage), "正式生成不得包含区域内超限怪。");
        var inside = new EncounterAnchorData { encounter_profile_id = "wolf_den", encounter_kind = "settlement", world_coord = world.PlayerStartCoord };
        var outside = inside.DuplicateState();
        outside.world_coord = rules.Bounds.End;
        var anchors = new[] { inside, outside };
        var growth = new WildEncounterGrowthSystem();
        growth.ApplyStepAdvance(anchors, 0, 100, _snapshot.BattleEncounters, _snapshot.EncounterRosters, rules);
        _test.Eq(inside.growth_stage, 2, "出生区狼巢不能长出挑战等级大于 2 的狼王与萨满。");
        _test.Eq(outside.growth_stage, 4, "区外狼巢应正常成长。");
        _test.False(growth.ApplyStepAdvance(new[] { inside }, 100, 200, _snapshot.BattleEncounters, _snapshot.EncounterRosters, rules), "达到出生区上限后继续走路不得突破限制。");
    }

    private void TestQuestPlacementAndInsertion()
    {
        var spawn = new Vector2I(500, 500);
        var rules = new WorldStartingAreaRules(Area, spawn, _challenges);
        var grid = new WorldMapGridSystem();
        grid.Setup(new Vector2I(10, 10), new Vector2I(100, 100));
        _test.True(QuestAcceptEncounterPlacement.TryFindAvailableCoord(grid, spawn,
            coord => rules.Allows(coord, "wolf_den", 3), out Vector2I highCoord, 208), "高挑战任务应能选择出生区外落点。");
        _test.False(rules.Contains(highCoord), "高挑战任务不得刷在出生区域内。");
        _test.True(QuestAcceptEncounterPlacement.TryFindAvailableCoord(grid, spawn,
            coord => rules.Allows(coord, "wolf_wilds", 0), out Vector2I lowCoord), "低挑战任务仍应在发布者附近生成。");
        _test.True(rules.Contains(lowCoord), "低挑战任务不应被无故移出出生区。");
        var context = new WorldMapDataContext();
        using var lease = WorldMapSpawnProjection.ProjectLease(
            new WorldMapSpawnSystem.WorldBuildData { MapSeed = 1, PlayerStartCoord = spawn }, "starting_area_test"
        );
        context.BindRootWorldData(lease.Value);
        context.active_generation_definition = TestWorldGenerationDefinitionFactory.Create(startingArea: Area);
        context.ConfigureEncounterChallenges(_challenges);
        try
        {
            _test.False(context.TryAddEncounterAnchor(new EncounterAnchorData {
                entity_id = "high", encounter_profile_id = "wolf_den", encounter_kind = "single", growth_stage = 3, world_coord = spawn
            }), "世界数据写入口也要拒绝超限任务，不能只靠候选坐标筛选。");
            _test.True(context.TryAddEncounterAnchor(new EncounterAnchorData {
                entity_id = "low", display_name = "低挑战", encounter_profile_id = "wolf_wilds", encounter_kind = "single", world_coord = spawn
            }), "出生区写入口应接受合法怪物。");
        }
        finally { context.Dispose(); }
    }
}
