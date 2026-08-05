using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using PlainArray = System.Collections.Generic.IReadOnlyList<object>;
using PlainDictionary = System.Collections.Generic.IReadOnlyDictionary<string, object>;

public partial class run_game_runtime_snapshot_builder_regression : LifecycleTestSceneTree
{
    private const string TestWorldConfig = "res://data/configs/world_map/test_world_map_config.tres";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(RunDeferred);
    }

    private void RunDeferred()
    {
        TestResult exitCode = Run();
        RequestTestExit(exitCode);
    }

    private TestResult Run()
    {
        TestSnapshotBuilderMatchesFacadeOutputs();
        TestHeadlessSnapshotLeaseLifecycle();
        TestSnapshotBuilderExposesBattleObjectiveProgress();
        TestGameTextCommandResultLifecycle();
        TestTextSnapshotRedactsHostLogPaths();
        TestSnapshotBuilderExposesPartyQuestSnapshot();
        TestSnapshotBuilderExposesPartyStandingSnapshot();
        TestSnapshotBuilderExposesMemberProgressionSnapshot();
        TestTextSnapshotRequiresExplicitQuestStageId();
        TestTextSnapshotRejectsStringNameQuestAndWindowFields();
        TestTextSnapshotRejectsLegacyWindowAndReportFields();
        TestSnapshotBuilderCrossReferencesQuestItemsInTextSnapshot();
        TestSnapshotBuilderExposesContractBoardModalSnapshot();
        TestSnapshotBuilderExposesNpcQuestOfferModalSnapshot();
        TestSnapshotBuilderExposesForgeModalSnapshot();
        TestSnapshotBuilderExposesGenericForgeModalSnapshot();
        TestSnapshotBuilderRequiresPanelKindForForgeModal();
        TestSnapshotBuilderExposesGameOverSnapshot();
        TestSnapshotBuilderExposesBattleLootSnapshot();
        TestSnapshotBuilderOmitsLootSectionWhenEmpty();

        return _test.Finish("Game runtime snapshot builder regression");
    }

    private void TestSnapshotBuilderExposesBattleObjectiveProgress()
    {
        TestBossObjectiveProgressSnapshot();
        TestEscapeObjectiveProgressSnapshot();
        TestInterceptObjectiveProgressSnapshot();
        TestDefenseObjectiveProgressSnapshot();
        TestNodeOperationObjectiveProgressSnapshot();
        TestControlObjectiveProgressSnapshot();
    }

    private void TestBossObjectiveProgressSnapshot()
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            "snapshot_boss_ally",
            "player",
            new Vector2I(1, 1)
        );
        ally.source_member_id = "snapshot_boss_member";
        BattleUnitState boss = BattleTestFixture.BuildUnit(
            "snapshot_boss_target",
            "enemy",
            new Vector2I(3, 1)
        );
        boss.encounter_actor_id = "red_dragon_boss";
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "snapshot_boss_battle",
            new Vector2I(5, 4),
            new[] { ally },
            new[] { boss }
        );
        _test.True(
            fixture.State.InitializeObjective(
                new BattleBossObjectiveDefinition("red_dragon_boss")
            ),
            "boss objective should initialize for runtime snapshot projection."
        );

        var runtime = new SnapshotTestRuntime
        {
            BattleState = fixture.State,
            BattleRuntime = fixture.Runtime,
            ActiveBattleEncounterId = "snapshot_boss_encounter",
            ActiveBattleEncounterName = "首领快照遭遇",
        };
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        try
        {
            PlainDictionary battle = Dict(
                builder.BuildHeadlessSnapshotPlain(),
                "battle"
            );
            PlainDictionary objective = Dict(battle, "objective");
            _test.Eq(StringValue(objective, "mode"), "boss", "boss 快照应暴露目标模式。");
            _test.Eq(
                StringValue(objective, "target_actor_id"),
                "red_dragon_boss",
                "boss 快照应暴露稳定 actor id。"
            );
            _test.Eq(
                StringValue(objective, "target_unit_id"),
                "snapshot_boss_target",
                "boss 快照应暴露本场解析后的 unit id。"
            );
            _test.True(BoolValue(objective, "target_alive"), "boss 快照应暴露首领存活状态。");
            _test.Eq(
                IntValue(objective, "alive_required_unit_count"),
                1,
                "boss 快照应暴露冻结队伍的存活进度。"
            );
            _test.True(
                HasTextLinePrefix(
                    TextSnapshotLines(builder.BuildTextSnapshot()),
                    "objective_progress=mode=boss | target_actor=red_dragon_boss"
                ),
                "文本快照应暴露 boss 目标过程事实。"
            );
        }
        finally
        {
            builder.Dispose();
        }
    }

    private void TestEscapeObjectiveProgressSnapshot()
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            "snapshot_escape_ally",
            "player",
            new Vector2I(3, 1)
        );
        ally.source_member_id = "snapshot_escape_member";
        BattleUnitState enemy = BattleTestFixture.BuildUnit(
            "snapshot_escape_enemy",
            "enemy",
            new Vector2I(1, 1)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "snapshot_escape_battle",
            new Vector2I(4, 3),
            new[] { ally },
            new[] { enemy }
        );
        _test.True(
            fixture.State.InitializeObjective(
                new BattleEscapeObjectiveDefinition(
                    "east_exit",
                    BattleMapEdge.Right,
                    1
                )
            ),
            "escape objective should initialize for runtime snapshot projection."
        );

        var runtime = new SnapshotTestRuntime
        {
            BattleState = fixture.State,
            BattleRuntime = fixture.Runtime,
            ActiveBattleEncounterId = "snapshot_escape_encounter",
            ActiveBattleEncounterName = "逃离快照遭遇",
        };
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        try
        {
            PlainDictionary battle = Dict(
                builder.BuildHeadlessSnapshotPlain(),
                "battle"
            );
            PlainDictionary objective = Dict(battle, "objective");
            _test.Eq(StringValue(objective, "mode"), "escape", "escape 快照应暴露目标模式。");
            _test.Eq(
                StringValue(objective, "exit_zone_id"),
                "east_exit",
                "escape 快照应暴露稳定出口区域 id。"
            );
            _test.Eq(StringValue(objective, "exit_edge"), "right", "escape 快照应暴露出口边缘。");
            _test.Eq(IntValue(objective, "exit_depth"), 1, "escape 快照应暴露出口纵深。");
            _test.Eq(
                IntValue(objective, "reached_exit_unit_count"),
                1,
                "escape 快照应暴露已完整进入出口的单位数。"
            );
            _test.Eq(
                ArrayValue(objective, "exit_coords").Count,
                3,
                "escape 快照应暴露初始化时冻结的出口坐标。"
            );
            _test.True(
                HasTextLinePrefix(
                    TextSnapshotLines(builder.BuildTextSnapshot()),
                    "objective_progress=mode=escape | exit_zone=east_exit | edge=right | depth=1 | reached=1/1"
                ),
                "文本快照应暴露 escape 目标过程事实。"
            );
        }
        finally
        {
            builder.Dispose();
        }
    }

    private void TestInterceptObjectiveProgressSnapshot()
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            "snapshot_intercept_ally",
            "player",
            new Vector2I(1, 1)
        );
        ally.source_member_id = "snapshot_intercept_member";
        BattleUnitState target = BattleTestFixture.BuildUnit(
            "snapshot_intercept_target",
            "enemy",
            new Vector2I(3, 1)
        );
        target.encounter_actor_id = "snapshot_intercept_actor";
        target.display_name = "迷雾信使";
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "snapshot_intercept_battle",
            new Vector2I(5, 3),
            new[] { ally },
            new[] { target }
        );
        _test.True(
            fixture.State.InitializeObjective(
                new BattleInterceptObjectiveDefinition(
                    "snapshot_intercept_actor",
                    "west_breakthrough",
                    BattleMapEdge.Left,
                    1
                )
            ),
            "intercept objective should initialize for runtime snapshot projection."
        );

        var runtime = new SnapshotTestRuntime
        {
            BattleState = fixture.State,
            BattleRuntime = fixture.Runtime,
            ActiveBattleEncounterId = "snapshot_intercept_encounter",
            ActiveBattleEncounterName = "截击快照遭遇",
        };
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        try
        {
            PlainDictionary battle = Dict(
                builder.BuildHeadlessSnapshotPlain(),
                "battle"
            );
            PlainDictionary objective = Dict(battle, "objective");
            _test.Eq(
                StringValue(objective, "mode"),
                "intercept",
                "intercept 快照应暴露目标模式。"
            );
            _test.Eq(
                StringValue(objective, "target_actor_id"),
                "snapshot_intercept_actor",
                "intercept 快照应暴露稳定 actor id。"
            );
            _test.Eq(
                StringValue(objective, "exit_zone_id"),
                "west_breakthrough",
                "intercept 快照应暴露逃脱区 id。"
            );
            _test.Eq(
                StringValue(objective, "exit_edge"),
                "left",
                "intercept 快照应暴露逃脱区边缘。"
            );
            _test.False(
                BoolValue(objective, "target_reached_exit"),
                "尚未逃脱的截击目标应投影为未到达。"
            );
            _test.True(
                HasTextLinePrefix(
                    TextSnapshotLines(builder.BuildTextSnapshot()),
                    "objective_progress=mode=intercept | target_actor=snapshot_intercept_actor"
                ),
                "文本快照应暴露 intercept 目标过程事实。"
            );
        }
        finally
        {
            builder.Dispose();
        }
    }

    private void TestDefenseObjectiveProgressSnapshot()
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            "snapshot_defense_ally",
            "player",
            Vector2I.Zero
        );
        ally.source_member_id = "snapshot_defense_member";
        BattleUnitState target = BattleTestFixture.BuildUnit(
            "snapshot_defense_target",
            "player",
            Vector2I.Right
        );
        target.encounter_actor_id = "snapshot_defense_actor";
        target.display_name = "迷雾守望者";
        BattleUnitState enemy = BattleTestFixture.BuildUnit(
            "snapshot_defense_enemy",
            "enemy",
            new Vector2I(3, 0)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "snapshot_defense_battle",
            new Vector2I(4, 2),
            new[] { ally, target },
            new[] { enemy }
        );
        fixture.State.timeline.current_tu = 40;
        _test.True(
            fixture.State.InitializeObjective(
                new BattleDefenseObjectiveDefinition(
                    "snapshot_defense_actor",
                    100
                )
            ),
            "defense objective should initialize for runtime snapshot projection."
        );

        var runtime = new SnapshotTestRuntime
        {
            BattleState = fixture.State,
            BattleRuntime = fixture.Runtime,
            ActiveBattleEncounterId = "snapshot_defense_encounter",
            ActiveBattleEncounterName = "防守快照遭遇",
        };
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        try
        {
            PlainDictionary battle = Dict(
                builder.BuildHeadlessSnapshotPlain(),
                "battle"
            );
            PlainDictionary objective = Dict(battle, "objective");
            _test.Eq(
                StringValue(objective, "mode"),
                "defense",
                "defense 快照应暴露目标模式。"
            );
            _test.Eq(
                StringValue(objective, "target_actor_id"),
                "snapshot_defense_actor",
                "defense 快照应暴露稳定 actor id。"
            );
            _test.Eq(
                IntValue(objective, "current_tu"),
                40,
                "defense 快照应暴露当前 TU。"
            );
            _test.Eq(
                IntValue(objective, "start_tu"),
                40,
                "defense 快照应暴露冻结的开始 TU。"
            );
            _test.Eq(
                IntValue(objective, "deadline_tu"),
                140,
                "defense 快照应暴露冻结的截止 TU。"
            );
            _test.Eq(
                IntValue(objective, "remaining_tu"),
                100,
                "defense 快照应暴露剩余 TU。"
            );
            _test.True(
                HasTextLinePrefix(
                    TextSnapshotLines(builder.BuildTextSnapshot()),
                    "objective_progress=mode=defense | target_actor=snapshot_defense_actor"
                ),
                "文本快照应暴露 defense 目标过程事实。"
            );
        }
        finally
        {
            builder.Dispose();
        }
    }

    private void TestSnapshotBuilderMatchesFacadeOutputs()
    {
        GameSession gameSession = CreateTestSession();
        if (gameSession == null)
            return;

        var facade = new GameRuntimeFacade();
        try
        {
            facade.Setup(gameSession);
            var builder = new GameRuntimeSnapshotBuilder();
            builder.Setup(facade);

            LifecycleAuditSnapshot plainBaseline =
                LifecycleAuditRegistry.Shared.CaptureSnapshot();
            PlainDictionary facadeSnapshot = facade.BuildHeadlessSnapshotPlain();
            PlainDictionary builderSnapshot = builder.BuildHeadlessSnapshotPlain();
            string facadeText = facade.BuildTextSnapshot();
            string builderText = builder.BuildTextSnapshot();
            PlainDictionary repeatedFacadeSnapshot = facade.BuildHeadlessSnapshotPlain();
            string repeatedFacadeText = facade.BuildTextSnapshot();

            AssertSafetyCounters(
                plainBaseline,
                LifecycleAuditRegistry.Shared.CaptureSnapshot(),
                "real facade repeated plain snapshot"
            );

            PlainDictionary settlementSnapshot = Dict(facadeSnapshot, "settlement");
            _test.True(
                settlementSnapshot.ContainsKey("country_id"),
                "真实 facade headless snapshot 应显式暴露据点 country_id。"
            );
            _test.Eq(
                StringValue(settlementSnapshot, "country_id"),
                "",
                "当前未归属国家的起始据点应输出空 country_id。"
            );
            _test.True(
                facadeText.Contains("country_id=", StringComparison.Ordinal),
                "文本快照应显式渲染据点 country_id。"
            );

            PlainArray services = ArrayValue(settlementSnapshot, "services");
            PlainArray repeatedServices = ArrayValue(
                Dict(repeatedFacadeSnapshot, "settlement"),
                "services"
            );
            _test.True(services.Count > 0, "真实 facade headless snapshot 应暴露起始据点服务。");
            _test.Eq(
                SnapshotServiceOrder(repeatedServices),
                SnapshotServiceOrder(services),
                "真实 facade 重复 snapshot 的 settlement service order golden 应稳定。"
            );
            foreach (object serviceValue in services)
            {
                _test.True(
                    serviceValue is PlainDictionary service
                    && string.Join(",", service.Keys)
                        == "action_id,facility_name,npc_name,service_type,interaction_script_id",
                    "headless settlement service facts 应保持严格五字段顺序。"
                );
            }

            string facadeJson;
            using (
                GodotProjectionLease<GDictionary> facadeSnapshotLease =
                    facade.BuildHeadlessSnapshotLease()
            )
            using (
                GodotProjectionLease<GDictionary> builderSnapshotLease =
                    builder.BuildHeadlessSnapshotLease()
            )
            {
                facadeJson = Json.Stringify(facadeSnapshotLease.Value);
                _test.Eq(
                    Json.Stringify(builderSnapshotLease.Value),
                    facadeJson,
                    "Snapshot builder 输出应与 facade.BuildHeadlessSnapshotPlain() 保持一致。"
                );
            }
            using (
                GodotProjectionLease<GDictionary> repeatedFacadeLease =
                    facade.BuildHeadlessSnapshotLease()
            )
            {
                _test.Eq(
                    Json.Stringify(repeatedFacadeLease.Value),
                    facadeJson,
                    "真实 facade 重复 headless JSON golden 应稳定。"
                );
            }
            _test.Eq(
                builderText,
                facadeText,
                "Snapshot builder 文本快照应与 facade.BuildTextSnapshot() 保持一致。"
            );
            _test.Eq(
                repeatedFacadeText,
                facadeText,
                "真实 facade 重复 headless text golden 应稳定。"
            );
            _test.True(!string.IsNullOrEmpty(builderText), "Snapshot builder 文本快照不应为空。");
            _test.True(builderSnapshot.ContainsKey("logs"), "运行时快照应包含日志段。");
            PlainDictionary logs = Dict(builderSnapshot, "logs");
            _test.Eq(StringValue(logs, "file_path"), "", "运行时快照默认不应暴露日志文件路径。");
            _test.False(BoolValue(logs, "file_output_enabled", true), "运行时日志文件输出默认应关闭。");
            _test.True(ArrayValue(logs, "entries").Count > 0, "运行时快照应包含最近日志条目。");

            builder.Dispose();
        }
        finally
        {
            facade.Dispose();
            CleanupTestSession(gameSession);
        }
    }

    private void TestNodeOperationObjectiveProgressSnapshot()
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            "snapshot_node_operation_ally",
            "player",
            Vector2I.Zero
        );
        ally.source_member_id = "snapshot_node_operation_member";
        BattleUnitState enemy = BattleTestFixture.BuildUnit(
            "snapshot_node_operation_enemy",
            "enemy",
            new Vector2I(4, 0)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "snapshot_node_operation_battle",
            new Vector2I(5, 1),
            new[] { ally },
            new[] { enemy }
        );
        _test.True(
            fixture.State.InitializeObjective(
                new BattleNodeOperationObjectiveDefinition(
                    new[]
                    {
                        new BattleOperationNodeDefinition(
                            "snapshot_node",
                            "快照节点",
                            "snapshot_zone",
                            BattleMapEdge.Left,
                            2
                        ),
                    }
                )
            ),
            "node operation objective should initialize for runtime snapshot projection."
        );

        var runtime = new SnapshotTestRuntime
        {
            BattleState = fixture.State,
            BattleRuntime = fixture.Runtime,
            ActiveBattleEncounterId = "snapshot_node_operation_encounter",
            ActiveBattleEncounterName = "节点作业快照遭遇",
        };
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        try
        {
            PlainDictionary battle = Dict(
                builder.BuildHeadlessSnapshotPlain(),
                "battle"
            );
            PlainDictionary objective = Dict(battle, "objective");
            _test.Eq(
                StringValue(objective, "mode"),
                "node_operation",
                "节点作业快照应暴露目标模式。"
            );
            _test.Eq(
                IntValue(objective, "operation_node_count"),
                1,
                "节点作业快照应暴露节点总数。"
            );
            _test.Eq(
                IntValue(objective, "completed_operation_node_count"),
                0,
                "节点作业快照应暴露已完成数量。"
            );
            _test.Eq(
                ArrayValue(objective, "operation_nodes").Count,
                1,
                "节点作业快照应暴露逐节点事实。"
            );
            _test.True(
                HasTextLinePrefix(
                    TextSnapshotLines(builder.BuildTextSnapshot()),
                    "objective_progress=mode=node_operation | completed=0/1"
                ),
                "文本快照应暴露 node_operation 目标过程事实。"
            );
        }
        finally
        {
            builder.Dispose();
        }
    }

    private void TestControlObjectiveProgressSnapshot()
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            "snapshot_control_ally",
            "player",
            Vector2I.Zero
        );
        ally.source_member_id = "snapshot_control_member";
        BattleUnitState enemy = BattleTestFixture.BuildUnit(
            "snapshot_control_enemy",
            "enemy",
            new Vector2I(4, 0)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "snapshot_control_battle",
            new Vector2I(5, 1),
            new[] { ally },
            new[] { enemy }
        );
        _test.True(
            fixture.State.InitializeObjective(
                new BattleControlObjectiveDefinition(
                    new[]
                    {
                        new BattleControlZoneDefinition(
                            "snapshot_control_zone",
                            "快照占领区",
                            BattleMapEdge.Left,
                            2
                        ),
                    },
                    100
                )
            ),
            "control objective should initialize for runtime snapshot projection."
        );

        var runtime = new SnapshotTestRuntime
        {
            BattleState = fixture.State,
            BattleRuntime = fixture.Runtime,
            ActiveBattleEncounterId = "snapshot_control_encounter",
            ActiveBattleEncounterName = "区域占领快照遭遇",
        };
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        try
        {
            PlainDictionary battle = Dict(
                builder.BuildHeadlessSnapshotPlain(),
                "battle"
            );
            PlainDictionary objective = Dict(battle, "objective");
            _test.Eq(
                StringValue(objective, "mode"),
                "control",
                "区域占领快照应暴露目标模式。"
            );
            _test.Eq(
                IntValue(objective, "control_zone_count"),
                1,
                "区域占领快照应暴露区域总数。"
            );
            _test.Eq(
                IntValue(objective, "player_control_score"),
                0,
                "区域占领快照应暴露我方分数。"
            );
            _test.Eq(
                IntValue(objective, "hostile_control_score"),
                0,
                "区域占领快照应暴露敌方分数。"
            );
            _test.Eq(
                IntValue(objective, "control_score_target"),
                100,
                "区域占领快照应暴露目标分数。"
            );
            PlainArray zones = ArrayValue(objective, "control_zones");
            _test.Eq(zones.Count, 1, "区域占领快照应暴露逐区域事实。");
            _test.Eq(
                StringValue((PlainDictionary)zones[0], "occupancy"),
                "player",
                "逐区域事实应暴露稳定归属值。"
            );
            _test.True(
                HasTextLinePrefix(
                    TextSnapshotLines(builder.BuildTextSnapshot()),
                    "objective_progress=mode=control | player_score=0/100"
                ),
                "文本快照应暴露 control 目标过程事实。"
            );
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static string SnapshotServiceOrder(PlainArray services)
    {
        var actionIds = new List<string>();
        foreach (object serviceValue in services ?? System.Array.Empty<object>())
        {
            if (serviceValue is PlainDictionary service)
                actionIds.Add(StringValue(service, "action_id"));
        }
        return string.Join(",", actionIds);
    }

    private void TestHeadlessSnapshotLeaseLifecycle()
    {
        var runtime = new SnapshotTestRuntime();
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        int expectedContainerCount = -1;
        string expectedProjectionFingerprint = null;
        string expectedTextFingerprint = null;

        PlainDictionary canonical = builder.BuildHeadlessSnapshotPlain();
        LifecycleAuditSnapshot afterCanonicalPlain =
            LifecycleAuditRegistry.Shared.CaptureSnapshot();
        AssertAuditBaseline(baseline, afterCanonicalPlain, "canonical plain snapshot");
        AssertSafetyCounters(baseline, afterCanonicalPlain, "canonical plain snapshot");
        _test.Eq(
            string.Join(",", canonical.Keys),
            "status,modal,logs,world,submap,game_over,party,settlement,contract_board,bounty_board,npc_quest_offer,shop,forge,stagecoach,character_info,warehouse,battle,loot,reward,promotion",
            "headless snapshot canonical root key order 不应漂移。"
        );
        _test.True(
            IsStrictPlainGraph(canonical),
            "headless snapshot canonical graph 不应包含 Variant、Godot collection/Object 或 HUD DTO。"
        );

        for (int iteration = 0; iteration < 3; iteration++)
        {
            GodotProjectionLease<GDictionary> lease = builder.BuildHeadlessSnapshotLease();
            LifecycleAuditSnapshot active = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            int containerCount = CountContainers(lease.Value);
            if (expectedContainerCount < 0)
                expectedContainerCount = containerCount;
            string projectionFingerprint = Json.Stringify(lease.Value);
            string textFingerprint = builder.BuildTextSnapshot();
            expectedProjectionFingerprint ??= projectionFingerprint;
            expectedTextFingerprint ??= textFingerprint;
            _test.Eq(
                containerCount,
                expectedContainerCount,
                "重复 headless snapshot projection 的递归容器数应保持稳定。"
            );
            _test.Eq(
                projectionFingerprint,
                expectedProjectionFingerprint,
                "重复 headless snapshot projection 的 JSON/key-order fingerprint 应保持稳定。"
            );
            _test.Eq(
                textFingerprint,
                expectedTextFingerprint,
                "重复 headless text snapshot fingerprint 应保持稳定。"
            );
            _test.Eq(
                active.ActiveOwnerCount - baseline.ActiveOwnerCount,
                containerCount,
                "headless snapshot lease 应精确拥有 root 与每个 nested container。"
            );
            _test.Eq(
                active.ActiveLeaseCount,
                baseline.ActiveLeaseCount + 1,
                "headless snapshot projection 应只登记一个 root lease。"
            );
            _test.Eq(
                active.ActiveScopeCount,
                baseline.ActiveScopeCount,
                "headless snapshot projection 不应额外登记 native scope。"
            );
            _test.Eq(
                active.ActiveContentBorrowerCount,
                baseline.ActiveContentBorrowerCount,
                "headless snapshot projection 不应登记 content borrower。"
            );

            lease.Dispose();
            _test.True(
                Throws<ObjectDisposedException>(() => _ = lease.Value),
                "关闭后的 headless snapshot lease.Value 应抛 ObjectDisposedException。"
            );
            AssertAuditBaseline(
                baseline,
                LifecycleAuditRegistry.Shared.CaptureSnapshot(),
                $"headless snapshot iteration {iteration}"
            );
            AssertSafetyCounters(
                baseline,
                LifecycleAuditRegistry.Shared.CaptureSnapshot(),
                $"headless snapshot iteration {iteration}"
            );
        }

        Dictionary<string, object> unsupported = RuntimePlainPayload.CloneDictionary(
            builder.BuildHeadlessSnapshotPlain()
        );
        unsupported["unsupported"] = new object();
        _test.True(
            Throws<InvalidOperationException>(
                () =>
                {
                    using GodotProjectionLease<GDictionary> rejected =
                        RuntimePlainPayload.ProjectDictionaryLease(
                            unsupported,
                            "headless-snapshot-unsupported-value",
                            LifetimeDomain.Request,
                            "run_game_runtime_snapshot_builder_regression.unsupported"
                        );
                }
            ),
            "headless root projection 遇到未知类型应抛错而不是字符串化。"
        );
        AssertAuditBaseline(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "headless snapshot unknown value"
        );
        AssertSafetyCounters(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "headless snapshot unknown value"
        );
        builder.Dispose();
    }

    private void TestGameTextCommandResultLifecycle()
    {
        var result = new GameTextCommandResult();
        result.SetSnapshot(
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["root"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["items"] = new List<object>
                    {
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["id"] = "entry_a",
                            ["count"] = 2,
                        },
                    },
                },
            }
        );
        result.AddAssertion(true, "ok", "summary", "actual", "expected");

        LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        PlainDictionary firstSnapshot = result.SnapshotTyped;
        PlainDictionary secondSnapshot = result.SnapshotTyped;
        IReadOnlyList<IReadOnlyDictionary<string, object>> firstAssertions =
            result.AssertionFactsTyped;
        IReadOnlyList<IReadOnlyDictionary<string, object>> secondAssertions =
            result.AssertionFactsTyped;
        _test.False(
            ReferenceEquals(firstSnapshot, secondSnapshot),
            "GameTextCommandResult 每次 SnapshotTyped 读取都应返回 fresh managed deep copy。"
        );
        _test.False(
            ReferenceEquals(firstAssertions, secondAssertions),
            "GameTextCommandResult 每次 AssertionFactsTyped 读取都应返回 fresh managed copy。"
        );
        result.AddAssertion(false, "later", "later summary", "later actual", "later expected");
        _test.Eq(
            firstAssertions.Count,
            1,
            "后续 AddAssertion 不应改变 caller 已取得的 assertion facts。"
        );
        _test.Eq(
            result.AssertionFactsTyped.Count,
            2,
            "后续 AddAssertion 应只出现在新的 assertion facts snapshot。"
        );
        var mutatedRoot = (Dictionary<string, object>)firstSnapshot["root"];
        var mutatedItems = (List<object>)mutatedRoot["items"];
        var mutatedEntry = (Dictionary<string, object>)mutatedItems[0];
        mutatedEntry["count"] = 99;
        mutatedItems.Add(
            new Dictionary<string, object>(StringComparer.Ordinal) { ["id"] = "caller_only" }
        );
        PlainDictionary isolatedSnapshot = result.SnapshotTyped;
        PlainDictionary isolatedRoot = Dict(isolatedSnapshot, "root");
        PlainArray isolatedItems = ArrayValue(isolatedRoot, "items");
        _test.Eq(
            isolatedItems.Count,
            1,
            "caller 修改 first SnapshotTyped nested list 不应污染 result canonical snapshot。"
        );
        _test.Eq(
            IntValue((PlainDictionary)isolatedItems[0], "count", -1),
            2,
            "caller 修改 first SnapshotTyped nested dictionary 不应污染后续读取。"
        );
        for (int iteration = 0; iteration < 3; iteration++)
        {
            _test.False(
                ReferenceEquals(secondSnapshot, result.SnapshotTyped),
                "GameTextCommandResult 重复读取 SnapshotTyped 应持续返回 fresh managed root。"
            );
            _test.False(
                ReferenceEquals(secondAssertions, result.AssertionFactsTyped),
                "GameTextCommandResult 重复读取 AssertionFactsTyped 应持续返回 fresh managed root。"
            );
            _test.True(
                IsStrictPlainGraph(result.SnapshotTyped),
                "GameTextCommandResult SnapshotTyped 应保持 strict plain graph。"
            );
            _test.True(
                IsStrictPlainGraph(result.AssertionFactsTyped),
                "GameTextCommandResult assertion facts 应保持 strict plain graph。"
            );
        }
        AssertAuditBaseline(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "game text command result repeated managed access"
        );
        AssertSafetyCounters(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "game text command result repeated managed access"
        );

        string expectedSnapshotFingerprint = null;
        string expectedAssertionFingerprint = null;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            GodotProjectionLease<GDictionary> snapshotLease = result.BuildSnapshotLease();
            int snapshotContainerCount = CountContainers(snapshotLease.Value);
            LifecycleAuditSnapshot activeSnapshot =
                LifecycleAuditRegistry.Shared.CaptureSnapshot();
            string snapshotFingerprint = Json.Stringify(snapshotLease.Value);
            expectedSnapshotFingerprint ??= snapshotFingerprint;
            _test.Eq(
                snapshotFingerprint,
                expectedSnapshotFingerprint,
                "GameTextCommandResult snapshot lease 重复投影 fingerprint 应稳定。"
            );
            _test.Eq(
                activeSnapshot.ActiveOwnerCount - baseline.ActiveOwnerCount,
                snapshotContainerCount,
                "GameTextCommandResult snapshot lease 应递归拥有全部容器。"
            );
            _test.Eq(
                activeSnapshot.ActiveLeaseCount,
                baseline.ActiveLeaseCount + 1,
                "GameTextCommandResult snapshot projection 应登记一个 root lease。"
            );
            snapshotLease.Dispose();
            _test.True(
                Throws<ObjectDisposedException>(() => _ = snapshotLease.Value),
                "关闭后的 GameTextCommandResult snapshot lease.Value 应抛错。"
            );
            AssertAuditBaseline(
                baseline,
                LifecycleAuditRegistry.Shared.CaptureSnapshot(),
                $"game text command result snapshot lease {iteration}"
            );

            GodotProjectionLease<GArray> assertionLease = result.BuildAssertionFactsLease();
            int assertionContainerCount = CountContainers(assertionLease.Value);
            LifecycleAuditSnapshot activeAssertions =
                LifecycleAuditRegistry.Shared.CaptureSnapshot();
            string assertionFingerprint = Json.Stringify(assertionLease.Value);
            expectedAssertionFingerprint ??= assertionFingerprint;
            _test.Eq(
                assertionFingerprint,
                expectedAssertionFingerprint,
                "GameTextCommandResult assertion lease 重复投影 fingerprint 应稳定。"
            );
            _test.Eq(
                activeAssertions.ActiveOwnerCount - baseline.ActiveOwnerCount,
                assertionContainerCount,
                "GameTextCommandResult assertion lease 应递归拥有全部容器。"
            );
            _test.Eq(
                activeAssertions.ActiveLeaseCount,
                baseline.ActiveLeaseCount + 1,
                "GameTextCommandResult assertion projection 应登记一个 root lease。"
            );
            assertionLease.Dispose();
            _test.True(
                Throws<ObjectDisposedException>(() => _ = assertionLease.Value),
                "关闭后的 GameTextCommandResult assertion lease.Value 应抛错。"
            );
            AssertAuditBaseline(
                baseline,
                LifecycleAuditRegistry.Shared.CaptureSnapshot(),
                $"game text command result assertion lease {iteration}"
            );
            AssertSafetyCounters(
                baseline,
                LifecycleAuditRegistry.Shared.CaptureSnapshot(),
                $"game text command result lease iteration {iteration}"
            );
        }

        PlainDictionary snapshotCapturedBeforeDispose = result.SnapshotTyped;
        IReadOnlyList<IReadOnlyDictionary<string, object>> assertionsCapturedBeforeDispose =
            result.AssertionFactsTyped;
        result.Dispose();
        _test.Eq(
            ArrayValue(Dict(snapshotCapturedBeforeDispose, "root"), "items").Count,
            1,
            "GameTextCommandResult.Dispose 不应清空 caller 已取得的 managed snapshot。"
        );
        _test.Eq(
            result.SnapshotTyped.Count,
            0,
            "GameTextCommandResult.Dispose 后新 snapshot 读取应为空。"
        );
        _test.Eq(
            assertionsCapturedBeforeDispose.Count,
            2,
            "GameTextCommandResult.Dispose 不应清空 caller 已取得的 assertion facts。"
        );
        _test.Eq(
            result.AssertionFactsTyped.Count,
            0,
            "GameTextCommandResult.Dispose 后新 assertion facts 读取应为空。"
        );
        AssertAuditBaseline(
            baseline,
            LifecycleAuditRegistry.Shared.CaptureSnapshot(),
            "game text command result dispose isolation"
        );
    }

    private void TestTextSnapshotRedactsHostLogPaths()
    {
        string memoryTextSnapshot = RenderRawSnapshot(
            new GDictionary
            {
                ["logs"] = new GDictionary
                {
                    ["file_path"] = "",
                    ["virtual_path"] = "",
                    ["entry_count"] = 1,
                    ["buffer_limit"] = 3,
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["seq"] = 1,
                            ["level"] = "info",
                            ["domain"] = "session",
                            ["event_id"] = "session.memory_only",
                            ["message"] = "memory only",
                        },
                    },
                },
            }
        );
        List<string> memoryLines = TextSnapshotLines(memoryTextSnapshot);
        _test.True(HasTextLine(memoryLines, "[LOG]"), "内存日志仍应渲染 LOG 分段。");
        _test.False(HasTextLinePrefix(memoryLines, "file_name="), "内存日志文本快照不应渲染文件名。");
        _test.False(HasTextLinePrefix(memoryLines, "file_path="), "文本快照不应继续渲染绝对 file_path 标签。");
        _test.False(HasTextLinePrefix(memoryLines, "virtual_path="), "文本快照不应继续渲染 virtual_path 标签。");

        const string filePath = "C:/tmp/magic/session_redaction.jsonl";
        const string virtualPath = "user://logs/session_redaction.jsonl";
        string fileTextSnapshot = RenderRawSnapshot(
            new GDictionary
            {
                ["logs"] = new GDictionary
                {
                    ["file_path"] = filePath,
                    ["virtual_path"] = virtualPath,
                    ["entry_count"] = 0,
                    ["buffer_limit"] = 3,
                    ["entries"] = new GArray(),
                },
            }
        );
        List<string> fileLines = TextSnapshotLines(fileTextSnapshot);
        _test.True(
            HasTextLine(fileLines, "file_name=session_redaction.jsonl"),
            "文本快照应只渲染稳定日志文件名。"
        );
        _test.False(HasTextLine(fileLines, filePath), "文本快照不应泄漏宿主绝对日志路径。");
        _test.False(HasTextLine(fileLines, virtualPath), "文本快照不应泄漏 session 级虚拟日志路径。");
        _test.False(HasTextLinePrefix(fileLines, "file_path="), "文本快照不应渲染宿主绝对日志路径字段。");
        _test.False(HasTextLinePrefix(fileLines, "virtual_path="), "文本快照不应渲染 session 级虚拟日志路径字段。");
    }

    private void TestSnapshotBuilderExposesPartyQuestSnapshot()
    {
        var runtime = new SnapshotTestRuntime();
        var partyState = new PartyState();
        var questState = new QuestState { quest_id = "contract_wolf_pack" };
        questState.MarkAccepted(12);
        questState.RecordObjectiveProgress(
            "defeat_wolves",
            2,
            3,
            QuestProgressContext.FromDictionary(
                new GDictionary { ["enemy_template_id"] = "wolf_raider" }
            )
        );
        questState.RecordObjectiveProgress(
            "defeat_wolves",
            2,
            3,
            QuestProgressContext.FromDictionary(
                new GDictionary { ["enemy_template_id"] = "wolf_raider" }
            )
        );
        questState.RecordObjectiveProgress(
            "report_back",
            1,
            1,
            QuestProgressContext.FromDictionary(
                new GDictionary { ["settlement_id"] = "spring_village_01" }
            )
        );
        var claimableQuest = new QuestState { quest_id = "contract_settlement_warehouse" };
        claimableQuest.MarkAccepted(9);
        claimableQuest.MarkCompleted(15);
        var failedQuest = new QuestState { quest_id = "contract_failed_patrol" };
        failedQuest.MarkAccepted(10);
        failedQuest.MarkFailed(17, "deadline_expired");
        partyState.SetActiveQuestState(questState);
        partyState.SetClaimableQuestState(claimableQuest);
        partyState.SetFailedQuestState(failedQuest);
        partyState.AddCompletedQuestId("contract_intro");
        runtime.PartyState = partyState;

        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        PlainDictionary snapshot = builder.BuildHeadlessSnapshotPlain();
        builder.Dispose();

        PlainDictionary questsSnapshot = Dict(Dict(snapshot, "party"), "quests");
        _test.False(
            BoolValue(Dict(snapshot, "world"), "player_visible_on_map", true),
            "快照应暴露世界地图人物显隐状态。"
        );
        _test.True(questsSnapshot.Count > 0, "当 PartyState 暴露 quest schema 时，headless snapshot 应在 party 段包含 quests。");
        AssertStringListEq(
            StringList(ArrayValue(questsSnapshot, "active_quest_ids")),
            new[] { "contract_wolf_pack" },
            "active_quest_ids 应稳定暴露当前激活任务 ID。"
        );
        AssertStringListEq(
            StringList(ArrayValue(questsSnapshot, "claimable_quest_ids")),
            new[] { "contract_settlement_warehouse" },
            "claimable_quest_ids 应稳定暴露待领奖励任务 ID。"
        );
        AssertStringListEq(
            StringList(ArrayValue(questsSnapshot, "completed_quest_ids")),
            new[] { "contract_intro" },
            "completed_quest_ids 应稳定暴露已完成任务 ID。"
        );
        AssertStringListEq(
            StringList(ArrayValue(questsSnapshot, "failed_quest_ids")),
            new[] { "contract_failed_patrol" },
            "failed_quest_ids 应稳定暴露失败任务 ID。"
        );

        PlainArray activeQuests = ArrayValue(questsSnapshot, "active_quests");
        PlainArray claimableQuests = ArrayValue(questsSnapshot, "claimable_quests");
        PlainArray failedQuests = ArrayValue(questsSnapshot, "failed_quests");
        _test.Eq(activeQuests.Count, 1, "active_quests 应保留当前任务详情。");
        _test.Eq(claimableQuests.Count, 1, "claimable_quests 应保留待领奖励任务详情。");
        _test.Eq(failedQuests.Count, 1, "failed_quests 应保留失败任务详情。");
        if (activeQuests.Count > 0)
        {
            PlainDictionary questEntry = activeQuests[0] as PlainDictionary;
            _test.Eq(StringValue(questEntry, "quest_id"), "contract_wolf_pack", "任务快照应保留 quest_id。");
            _test.Eq(StringValue(questEntry, "stage_id"), "active", "激活任务快照应标记 active stage。");
            _test.Eq(IntValue(questEntry, "accepted_at_world_step", -1), 12, "任务快照应保留接取时间。");
            _test.Eq(
                IntValue(Dict(questEntry, "objective_progress"), "defeat_wolves", 0),
                3,
                "任务快照应保留封顶后的目标进度。"
            );
            _test.Eq(
                StringValue(Dict(questEntry, "last_progress_context"), "settlement_id"),
                "spring_village_01",
                "任务快照应保留最近进度上下文。"
            );
        }
        if (claimableQuests.Count > 0)
        {
            PlainDictionary claimableEntry = claimableQuests[0] as PlainDictionary;
            _test.Eq(
                StringValue(claimableEntry, "quest_id"),
                "contract_settlement_warehouse",
                "待领奖励任务快照应保留 quest_id。"
            );
            _test.Eq(StringValue(claimableEntry, "stage_id"), "claimable", "待领奖励任务快照应标记 claimable stage。");
            _test.Eq(IntValue(claimableEntry, "completed_at_world_step", -1), 15, "待领奖励任务快照应保留完成时间。");
        }
        if (failedQuests.Count > 0)
        {
            PlainDictionary failedEntry = failedQuests[0] as PlainDictionary;
            _test.Eq(
                StringValue(failedEntry, "quest_id"),
                "contract_failed_patrol",
                "失败任务快照应保留 quest_id。"
            );
            _test.Eq(
                IntValue(failedEntry, "failed_at_world_step", -1),
                17,
                "失败任务快照应保留失败时间。"
            );
            _test.Eq(
                StringValue(failedEntry, "failure_reason_id"),
                "deadline_expired",
                "失败任务快照应保留失败原因。"
            );
        }
    }

    private void TestSnapshotBuilderExposesMemberProgressionSnapshot()
    {
        var partyState = new PartyState
        {
            leader_member_id = "player_sword_01",
            active_member_ids = new GStringNameArray { "player_sword_01" },
        };

        var memberState = new PartyMemberState
        {
            member_id = "player_sword_01",
            display_name = "剑士",
            current_aura = 2,
        };
        memberState.progression.unit_id = memberState.member_id;
        memberState.progression.unlocked_combat_resource_ids = new GStringNameArray
        {
            "hp",
            "stamina",
            "mp",
            "aura",
        };
        memberState.progression.active_level_trigger_core_skill_id = "warrior_heavy_strike";
        memberState.progression.locked_level_trigger_skill_ids = new GStringNameArray { "mage_blink" };
        memberState.progression.blocked_relearn_skill_ids = new GStringNameArray { "old_focus" };

        var coreSkill = new UnitSkillProgress
        {
            skill_id = "warrior_heavy_strike",
            is_learned = true,
            skill_level = 3,
            is_core = true,
            assigned_profession_id = "warrior",
            is_level_trigger_active = true,
        };
        memberState.progression.SetSkillProgress(coreSkill);

        var lockedSkill = new UnitSkillProgress
        {
            skill_id = "mage_blink",
            is_learned = true,
            skill_level = 1,
            is_level_trigger_locked = true,
            core_max_growth_claimed = true,
        };
        memberState.progression.SetSkillProgress(lockedSkill);

        var profession = new UnitProfessionProgress
        {
            profession_id = "warrior",
            rank = 2,
            is_active = true,
            core_skill_ids = new GStringNameArray { "warrior_heavy_strike" },
            granted_skill_ids = new GStringNameArray { "warrior_guard_break" },
        };
        memberState.progression.SetProfessionProgress(profession);
        partyState.SetMemberState(memberState);

        var runtime = new SnapshotTestRuntime { PartyState = partyState };
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        PlainDictionary snapshot = builder.BuildHeadlessSnapshotPlain();
        builder.Dispose();

        PlainDictionary memberSnapshot = FindMemberSnapshot(snapshot, "player_sword_01");
        _test.True(memberSnapshot.Count > 0, "member progression 回归前置：应能找到主角成员快照。");
        if (memberSnapshot.Count == 0)
            return;

        AssertStringListEq(
            StringList(ArrayValue(memberSnapshot, "unlocked_combat_resource_ids")),
            new[] { "aura", "hp", "mp", "stamina" },
            "成员快照应稳定暴露已解锁战斗资源。"
        );
        AssertStringListEq(
            StringList(ArrayValue(memberSnapshot, "active_core_skill_ids")),
            new[] { "warrior_heavy_strike" },
            "成员快照应暴露激活核心技能列表。"
        );
        _test.Eq(
            StringValue(memberSnapshot, "active_level_trigger_core_skill_id"),
            "warrior_heavy_strike",
            "成员快照应暴露 active level trigger。"
        );
        AssertStringListEq(
            StringList(ArrayValue(memberSnapshot, "locked_level_trigger_skill_ids")),
            new[] { "mage_blink" },
            "成员快照应暴露 locked trigger 技能。"
        );
        AssertStringListEq(
            StringList(ArrayValue(memberSnapshot, "blocked_relearn_skill_ids")),
            new[] { "old_focus" },
            "成员快照应暴露 blocked relearn 技能。"
        );
        _test.Eq(ArrayValue(memberSnapshot, "skill_entries").Count, 2, "成员快照应暴露 learned skill 详情。");
        _test.Eq(ArrayValue(memberSnapshot, "profession_entries").Count, 1, "成员快照应暴露 profession 详情。");
    }

    private void TestSnapshotBuilderExposesPartyStandingSnapshot()
    {
        var partyState = new PartyState();
        partyState.SetWorldRenown(64);
        partyState.SetCountryReputation("starfall_federation", 35);
        partyState.SetCountryReputation("frost_ash_empire", -20);

        var runtime = new SnapshotTestRuntime { PartyState = partyState };
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        PlainDictionary snapshot = builder.BuildHeadlessSnapshotPlain();
        builder.Dispose();

        PlainDictionary party = Dict(snapshot, "party");
        _test.Eq(
            IntValue(party, "world_renown", -1),
            64,
            "Party headless snapshot 应暴露世界名望。"
        );
        PlainDictionary countryReputations = Dict(party, "country_reputations");
        _test.Eq(
            IntValue(countryReputations, "frost_ash_empire", 999),
            -20,
            "Party headless snapshot 应暴露独立帝国声望。"
        );
        _test.Eq(
            IntValue(countryReputations, "starfall_federation", 999),
            35,
            "Party headless snapshot 应暴露独立联邦声望。"
        );
    }

    private void TestTextSnapshotRequiresExplicitQuestStageId()
    {
        var snapshot = new GDictionary
        {
            ["party"] = new GDictionary
            {
                ["quests"] = new GDictionary
                {
                    ["active_quest_ids"] = new GArray
                    {
                        "contract_missing_stage",
                        "contract_numeric_stage",
                        "contract_empty_stage",
                    },
                    ["claimable_quest_ids"] = new GArray { "contract_valid_stage" },
                    ["completed_quest_ids"] = new GArray(),
                    ["active_quests"] = new GArray
                    {
                        BuildQuestSnapshotPayload("contract_missing_stage", null),
                        BuildQuestSnapshotPayload("contract_numeric_stage", 1),
                        BuildQuestSnapshotPayload("contract_empty_stage", ""),
                    },
                    ["claimable_quests"] = new GArray
                    {
                        BuildQuestSnapshotPayload("contract_valid_stage", "claimable", "completed", 2),
                    },
                },
            },
        };
        List<string> lines = TextSnapshotLines(RenderRawSnapshot(snapshot));

        _test.True(
            HasTextLine(lines, "active_quest_ids=contract_missing_stage contract_numeric_stage contract_empty_stage"),
            "文本快照应保留 quest ID 汇总。"
        );
        _test.True(HasTextLinePrefix(lines, "quest=contract_valid_stage | stage=claimable"), "文本快照应渲染显式 stage_id 的任务明细。");
        _test.False(HasTextLinePrefix(lines, "quest=contract_missing_stage"), "缺 stage_id 的任务明细不应按 active 兜底渲染。");
        _test.False(HasTextLinePrefix(lines, "quest=contract_numeric_stage"), "非字符串 stage_id 的任务明细不应渲染。");
        _test.False(HasTextLinePrefix(lines, "quest=contract_empty_stage"), "空 stage_id 的任务明细不应渲染。");
    }

    private void TestTextSnapshotRejectsStringNameQuestAndWindowFields()
    {
        var snapshot = new GDictionary
        {
            ["party"] = new GDictionary
            {
                ["quests"] = new GDictionary
                {
                    ["active_quest_ids"] = new GArray { "contract_stringname_fields" },
                    ["claimable_quest_ids"] = new GArray(),
                    ["completed_quest_ids"] = new GArray(),
                    ["active_quests"] = new GArray
                    {
                        new GDictionary
                        {
                            ["quest_id"] = new StringName("contract_stringname_fields"),
                            ["stage_id"] = new StringName("active"),
                            ["status_id"] = new StringName("active"),
                            ["objective_progress"] = new GDictionary { [new StringName("deliver_dispatch")] = 1 },
                            ["accepted_at_world_step"] = 1,
                            ["completed_at_world_step"] = -1,
                            ["reward_claimed_at_world_step"] = -1,
                            ["last_progress_context"] = new GDictionary(),
                        },
                    },
                    ["claimable_quests"] = new GArray(),
                },
            },
            ["contract_board"] = new GDictionary
            {
                ["visible"] = true,
                ["window_data"] = new GDictionary
                {
                    ["title"] = new StringName("旧任务板标题"),
                    ["settlement_id"] = new StringName("spring_village_01"),
                    ["provider_interaction_id"] = new StringName("service_contract_board"),
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["display_name"] = new StringName("首轮狩猎"),
                            ["state_label"] = new StringName("状态：可查看"),
                            ["cost_label"] = new StringName("奖励：80 金"),
                        },
                    },
                },
            },
        };

        List<string> lines = TextSnapshotLines(RenderRawSnapshot(snapshot));

        _test.True(
            HasTextLine(lines, "active_quest_ids=contract_stringname_fields"),
            "文本快照应继续保留正式 quest id 汇总。"
        );
        _test.False(
            HasTextLinePrefix(lines, "quest=contract_stringname_fields"),
            "StringName quest 字段不应被文本快照当成正式 quest 明细渲染。"
        );
        _test.True(
            HasTextLine(lines, "provider_interaction_id="),
            "缺 formal string provider_interaction_id 时文本快照只应渲染空正式字段。"
        );
        _test.False(
            HasTextLine(lines, "provider_interaction_id=service_contract_board"),
            "StringName provider_interaction_id 不应被文本快照当成正式字符串渲染。"
        );
        _test.False(
            HasTextLinePrefix(lines, "entry=首轮狩猎"),
            "StringName contract-board 条目文案不应被文本快照当成正式字符串渲染。"
        );
    }

    private void TestTextSnapshotRejectsLegacyWindowAndReportFields()
    {
        var snapshot = new GDictionary
        {
            ["shop"] = new GDictionary
            {
                ["visible"] = true,
                ["window_data"] = new GDictionary
                {
                    ["title"] = "旧商店字段",
                    ["settlement_id"] = "settlement_legacy_schema",
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["entry_id"] = "old_shop_entry_id",
                            ["state_label"] = "状态：旧字段",
                            ["cost_label"] = "价格：旧字段",
                        },
                    },
                },
            },
            ["contract_board"] = new GDictionary
            {
                ["visible"] = true,
                ["window_data"] = new GDictionary
                {
                    ["title"] = "旧任务板字段",
                    ["settlement_id"] = "settlement_legacy_schema",
                    ["interaction_script_id"] = "old_contract_provider",
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["entry_id"] = "old_contract_entry_id",
                            ["quest_id"] = "contract_current_id",
                            ["state_label"] = "状态：旧字段",
                            ["cost_label"] = "奖励：旧字段",
                        },
                    },
                },
            },
            ["stagecoach"] = new GDictionary
            {
                ["visible"] = true,
                ["window_data"] = new GDictionary
                {
                    ["title"] = "旧驿站字段",
                    ["settlement_id"] = "settlement_legacy_schema",
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["entry_id"] = "old_stagecoach_entry_id",
                            ["state_label"] = "状态：旧字段",
                            ["cost_label"] = "车费：旧字段",
                        },
                    },
                },
            },
            ["forge"] = new GDictionary
            {
                ["visible"] = true,
                ["window_data"] = new GDictionary
                {
                    ["title"] = "旧锻造字段",
                    ["settlement_id"] = "settlement_legacy_schema",
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["entry_id"] = "old_forge_entry_id",
                            ["state_label"] = "状态：旧字段",
                            ["cost_label"] = "材料：旧字段",
                        },
                    },
                },
            },
            ["battle"] = new GDictionary
            {
                ["active"] = true,
                ["report_entry_count"] = 1,
                ["report_entries"] = new GArray
                {
                    new GDictionary
                    {
                        ["type"] = "change_equipment",
                        ["ok"] = true,
                        ["operation"] = "equip",
                        ["unit_id"] = "player_sword_01",
                        ["target_unit_id"] = "player_sword_01",
                        ["slot_id"] = "head",
                        ["item_id"] = "leather_cap",
                        ["instance_id"] = "eq_formal_schema",
                        ["ap_before"] = 4,
                        ["ap_after"] = 2,
                        ["text"] = "formal change equipment report",
                    },
                },
            },
            ["reward"] = new GDictionary
            {
                ["visible"] = true,
                ["remaining_count"] = 1,
                ["reward"] = new GDictionary
                {
                    ["reward_id"] = "reward_legacy_schema",
                    ["member_id"] = "player_sword_01",
                    ["member_name"] = "剑士",
                    ["source_label"] = "测试",
                    ["summary_text"] = "旧奖励字段",
                    ["entries"] = new GArray
                    {
                        new GDictionary
                        {
                            ["entry_type"] = "attribute_delta",
                            ["target_label"] = "old_reward_target_label",
                            ["amount"] = 1,
                            ["reason_text"] = "旧字段奖励",
                        },
                    },
                },
            },
        };
        List<string> lines = TextSnapshotLines(RenderRawSnapshot(snapshot));

        _test.True(HasTextLine(lines, "provider_interaction_id="), "缺 provider_interaction_id 时文本快照只应渲染空正式字段。");
        _test.True(HasTextLine(lines, "entry= | state=状态：旧字段 | cost=价格：旧字段"), "shop 条目缺 display_name 时应渲染空正式 label 字段。");
        _test.True(HasTextLine(lines, "entry=old_contract_entry_id"), "contract board 条目应渲染 entry_id。");
        _test.True(HasTextLine(lines, "  display_name="), "contract board 条目缺 display_name 时应渲染空正式 label 字段。");
        _test.True(HasTextLine(lines, "  provider_kind="), "contract board 条目缺 provider_kind 时应渲染空正式字段。");
        _test.True(HasTextLine(lines, "  listing_channels="), "contract board 条目缺 listing_channels 时应渲染空正式字段。");
        _test.True(HasTextLine(lines, "  state_label=状态：旧字段"), "contract board 条目应渲染 state_label。");
        _test.True(HasTextLine(lines, "  cost_label=奖励：旧字段"), "contract board 条目应渲染 cost_label。");
        _test.True(HasTextLine(lines, "  is_enabled=false"), "contract board 条目缺 is_enabled 时应渲染 false 默认值。");
        _test.True(HasTextLine(lines, "  disabled_reason="), "contract board 条目缺 disabled_reason 时应渲染空正式字段。");
        _test.True(HasTextLine(lines, "  accept_dialogue_text="), "contract board 条目缺 accept_dialogue_text 时应渲染空正式字段。");
        _test.True(HasTextLine(lines, "route= | state=状态：旧字段 | cost=车费：旧字段"), "stagecoach 条目缺 display_name 时应渲染空正式 label 字段。");
        _test.True(HasTextLine(lines, "entry= | state=状态：旧字段 | cost=材料：旧字段"), "forge 条目缺 display_name 时应渲染空正式 label 字段。");
        _test.True(
            HasTextLinePrefix(
                lines,
                "report=change_equipment | ok=true | error= | op=equip | unit=player_sword_01 | target=player_sword_01 | slot=head | item=leather_cap | instance=eq_formal_schema | ap=4>2"
            ),
            "change_equipment report 应只按正式字段渲染 operation 和 ap_after。"
        );
        _test.True(
            HasTextLinePrefix(lines, "entry=attribute_delta |  | amount=1"),
            "奖励条目缺 target_id 时应渲染空正式目标字段。"
        );
        _test.False(HasTextLinePrefix(lines, "provider_interaction_id=old_contract_provider"), "旧 interaction_script_id 不应回填 provider_interaction_id。");
        _test.False(
            HasTextLinePrefix(lines, "entry=attribute_delta | old_reward_target_label"),
            "奖励条目缺 target_id 时不应回退 target_label。"
        );
    }

    private void TestSnapshotBuilderCrossReferencesQuestItemsInTextSnapshot()
    {
        var runtime = new SnapshotTestRuntime();
        var partyState = new PartyState();
        var questState = new QuestState { quest_id = "contract_archive_delivery" };
        questState.MarkAccepted(7);
        questState.RecordObjectiveProgress(
            "deliver_dispatch",
            1,
            1,
            QuestProgressContext.FromDictionary(
                new GDictionary { ["item_id"] = "sealed_dispatch", ["submitted_quantity"] = 1 }
            )
        );
        partyState.SetActiveQuestState(questState);
        runtime.PartyState = partyState;
        runtime.ActiveModalKind = RuntimeModalKind.Warehouse;
        runtime.WarehouseWindowData = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["title"] = "共享仓库",
            ["entries"] = new List<object>
            {
                BuildWarehouseEntry("sealed_dispatch", 1),
                BuildWarehouseEntry("bandit_insignia", 3),
                BuildWarehouseEntry("moonfern_sample", 2),
            },
        };

        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        PlainDictionary snapshot = builder.BuildHeadlessSnapshotPlain();
        builder.Dispose();

        PlainDictionary questsSnapshot = Dict(Dict(snapshot, "party"), "quests");
        PlainArray activeQuests = ArrayValue(questsSnapshot, "active_quests");
        PlainDictionary warehouseSnapshot = Dict(snapshot, "warehouse");
        List<string> warehouseEntryIds = ExtractWindowEntryValueStrings(
            ArrayValue(Dict(warehouseSnapshot, "window_data"), "entries"),
            "item_id"
        );

        _test.Eq(activeQuests.Count, 1, "任务物品交叉引用回归中任务快照应保留 active quest。");
        if (activeQuests.Count > 0)
        {
            PlainDictionary questEntry = activeQuests[0] as PlainDictionary;
            PlainDictionary context = Dict(questEntry, "last_progress_context");
            _test.Eq(StringValue(context, "item_id"), "sealed_dispatch", "任务快照应保留任务物品上下文 item_id。");
            _test.Eq(IntValue(context, "submitted_quantity"), 1, "任务快照应保留任务物品提交数量。");
        }
        _test.True(BoolValue(warehouseSnapshot, "visible"), "任务物品交叉引用回归中仓库快照应保持可见。");
        AssertStringListEq(
            warehouseEntryIds,
            new[] { "sealed_dispatch", "bandit_insignia", "moonfern_sample" },
            "仓库快照应稳定暴露正式任务物品条目。"
        );
    }

    private void TestSnapshotBuilderExposesContractBoardModalSnapshot()
    {
        var runtime = new SnapshotTestRuntime
        {
            ActiveModalKind = RuntimeModalKind.ContractBoard,
            ContractBoardWindowData = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["title"] = "春泉村 · 任务板",
                ["settlement_id"] = "spring_village_01",
                ["provider_interaction_id"] = "service_contract_board",
                ["state_summary_text"] = "当前有 2 则契约可查看。",
                ["entries"] = new List<object>
                {
                    BuildWindowEntry(
                        "contract_first_hunt",
                        "contract_first_hunt",
                        "首轮狩猎",
                        "状态：可查看",
                        "奖励：80 金",
                        providerKind: "guild",
                        listingChannels: new[] { "board", "rumor" },
                        isEnabled: true,
                        disabledReason: "",
                        acceptDialogueText: "接受这份狩猎契约？"
                    ),
                    BuildWindowEntry(
                        "contract_manual_drill",
                        "contract_manual_drill",
                        "训练记录",
                        "状态：已锁定",
                        "奖励：30 金",
                        providerKind: "military",
                        listingChannels: new[] { "board" },
                        isEnabled: false,
                        disabledReason: "需要先完成前置契约。",
                        acceptDialogueText: ""
                    ),
                },
            },
        };

        PlainDictionary snapshot = BuildSnapshot(runtime, out string textSnapshot);
        PlainDictionary contractBoardSnapshot = Dict(snapshot, "contract_board");
        List<string> entryIds = ExtractWindowEntryValueStrings(
            ArrayValue(Dict(contractBoardSnapshot, "window_data"), "entries"),
            "quest_id"
        );
        List<string> lines = TextSnapshotLines(textSnapshot);

        _test.True(BoolValue(contractBoardSnapshot, "visible"), "contract board modal 激活时快照应暴露 contract_board.visible。");
        _test.Eq(
            StringValue(Dict(contractBoardSnapshot, "window_data"), "provider_interaction_id"),
            "service_contract_board",
            "contract board 快照应保留当前 provider_interaction_id。"
        );
        AssertStringListEq(
            entryIds,
            new[] { "contract_first_hunt", "contract_manual_drill" },
            "contract board 快照应稳定暴露当前任务板条目列表。"
        );
        _test.True(
            HasTextLine(lines, "state_summary_text=当前有 2 则契约可查看。"),
            "contract board 文本快照应渲染 state_summary_text。"
        );
        _test.True(
            HasTextLine(lines, "  provider_kind=guild"),
            "contract board 文本快照应渲染条目的 provider_kind。"
        );
        _test.True(
            HasTextLine(lines, "  listing_channels=board rumor"),
            "contract board 文本快照应渲染条目的 listing_channels。"
        );
        _test.True(
            HasTextLine(lines, "  is_enabled=true"),
            "contract board 文本快照应渲染条目的 is_enabled。"
        );
        _test.True(
            HasTextLine(lines, "  disabled_reason="),
            "contract board 文本快照应渲染条目的 disabled_reason（可为空）。"
        );
        _test.True(
            HasTextLine(lines, "  accept_dialogue_text=接受这份狩猎契约？"),
            "contract board 文本快照应渲染条目的 accept_dialogue_text。"
        );
        _test.True(
            HasTextLine(lines, "  provider_kind=military"),
            "contract board 文本快照应渲染第二个条目的 provider_kind。"
        );
        _test.True(
            HasTextLine(lines, "  listing_channels=board"),
            "contract board 文本快照应渲染第二个条目的 listing_channels。"
        );
        _test.True(
            HasTextLine(lines, "  is_enabled=false"),
            "contract board 文本快照应渲染第二个条目的 is_enabled。"
        );
        _test.True(
            HasTextLine(lines, "  disabled_reason=需要先完成前置契约。"),
            "contract board 文本快照应渲染第二个条目的 disabled_reason。"
        );
    }

    private void TestSnapshotBuilderExposesNpcQuestOfferModalSnapshot()
    {
        var runtime = new SnapshotTestRuntime
        {
            ActiveModalKind = RuntimeModalKind.NpcQuestOffer,
            NpcQuestOfferWindowData = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["npc_name"] = "blacksmith hrothgar",
                ["npc_interaction_id"] = "npc_blacksmith_hrothgar",
                ["selected_quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                ["feedback_text"] = "",
                ["entries"] = new List<object>
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["quest_id"] = "npc_blacksmith_hrothgar_cave_beasts",
                        ["display_name"] = "洞穴野兽",
                        ["is_enabled"] = true,
                        ["disabled_reason"] = "",
                        ["accept_dialogue_text"] = "峡谷北面的野兽又在骚扰商队，帮我清理一下。",
                    },
                },
            },
        };

        _test.Eq(
            runtime.GetActiveModalId(),
            "npc_quest_offer",
            "RuntimeModalKind.NpcQuestOffer 应映射为 npc_quest_offer。"
        );
        _test.False(
            RuntimeModalKinds.IsSettlementServiceModal(RuntimeModalKind.NpcQuestOffer),
            "NpcQuestOffer 不应被归类为据点服务面板。"
        );

        PlainDictionary windowData = RuntimePlainPayload.CloneDictionary(
            runtime.GetNpcQuestOfferWindowDataSnapshotPlain()
        );
        _test.Eq(
            StringValue(windowData, "npc_name"),
            "blacksmith hrothgar",
            "GetNpcQuestOfferWindowData() 应暴露 NPC 名称。"
        );
        _test.Eq(
            StringValue(windowData, "selected_quest_id"),
            "npc_blacksmith_hrothgar_cave_beasts",
            "GetActiveNpcQuestOfferContext() 应暴露当前选中的 quest_id。"
        );

        PlainDictionary snapshot = BuildSnapshot(runtime, out string textSnapshot);
        List<string> lines = TextSnapshotLines(textSnapshot);
        _test.Eq(
            StringValue(Dict(snapshot, "modal"), "id"),
            "npc_quest_offer",
            "快照应暴露当前 modal id 为 npc_quest_offer。"
        );
        _test.True(
            BoolValue(Dict(snapshot, "npc_quest_offer"), "visible", false),
            "NpcQuestOffer 激活时快照应暴露 npc_quest_offer.visible=true。"
        );
        _test.Eq(
            StringValue(Dict(Dict(snapshot, "npc_quest_offer"), "window_data"), "npc_name"),
            "blacksmith hrothgar",
            "npc_quest_offer 快照应包含 window_data.npc_name。"
        );
        _test.False(
            BoolValue(Dict(snapshot, "contract_board"), "visible", true),
            "NpcQuestOffer 激活时不应暴露 contract_board.visible。"
        );
        _test.False(
            BoolValue(Dict(snapshot, "shop"), "visible", true),
            "NpcQuestOffer 激活时不应暴露 shop.visible。"
        );
        _test.False(
            BoolValue(Dict(snapshot, "forge"), "visible", true),
            "NpcQuestOffer 激活时不应暴露 forge.visible。"
        );
        _test.True(
            HasTextLine(lines, "[NPC_QUEST_OFFER]"),
            "文本快照应渲染 [NPC_QUEST_OFFER] 区块。"
        );
        _test.True(
            HasTextLine(lines, "  display_name=洞穴野兽"),
            "文本快照应渲染 NPC offer 条目 display_name。"
        );
    }

    private void TestSnapshotBuilderExposesForgeModalSnapshot()
    {
        var runtime = new SnapshotTestRuntime
        {
            ActiveModalKind = RuntimeModalKind.Forge,
            ForgeWindowData = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["title"] = "灰烬镇 · 大师重铸",
                ["settlement_id"] = "forge_town",
                ["entries"] = new List<object>
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["display_name"] = "大师重铸：铁制大剑",
                        ["state_label"] = "状态：可重铸",
                        ["cost_label"] = "材料：1 件 青铜短剑、2 件 铁矿石",
                    },
                },
            },
        };

        PlainDictionary snapshot = BuildSnapshot(runtime, out _);
        _test.True(BoolValue(Dict(snapshot, "forge"), "visible"), "forge modal 激活时快照应暴露 forge.visible。");
        _test.Eq(
            StringValue(Dict(Dict(snapshot, "forge"), "window_data"), "title"),
            "灰烬镇 · 大师重铸",
            "forge 快照应保留窗口标题。"
        );
        _test.Eq(
            ArrayValue(Dict(Dict(snapshot, "forge"), "window_data"), "entries").Count,
            1,
            "forge 快照应保留配方条目。"
        );
    }

    private void TestSnapshotBuilderExposesGenericForgeModalSnapshot()
    {
        var runtime = new SnapshotTestRuntime
        {
            ActiveModalKind = RuntimeModalKind.Forge,
            ActiveShopContext = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["title"] = "灰烬镇 · 熔炉整备",
                ["settlement_id"] = "forge_town",
                ["panel_kind"] = "forge",
                ["submission_source"] = "forge",
                ["entries"] = new List<object>
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["entry_id"] = "forge:temper_edge",
                        ["display_name"] = "刃口淬火",
                        ["state_label"] = "状态：可执行",
                        ["cost_label"] = "材料：1 件 铁矿石、1 件 皮革护衣",
                    },
                },
            },
        };

        PlainDictionary snapshot = BuildSnapshot(runtime, out _);
        _test.Eq(
            StringValue(Dict(Dict(snapshot, "forge"), "window_data"), "title"),
            "灰烬镇 · 熔炉整备",
            "通用 forge modal 应从共享窗口上下文进入 forge 快照。"
        );
        _test.Eq(
            ArrayValue(Dict(Dict(snapshot, "forge"), "window_data"), "entries").Count,
            1,
            "通用 forge modal 应保留 forge 条目。"
        );
        _test.Eq(Dict(Dict(snapshot, "shop"), "window_data").Count, 0, "forge panel_kind 不应继续出现在 shop 快照中。");
    }

    private void TestSnapshotBuilderRequiresPanelKindForForgeModal()
    {
        var runtime = new SnapshotTestRuntime
        {
            ActiveModalKind = RuntimeModalKind.Forge,
            ActiveShopContext = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["title"] = "旧 forge 来源",
                ["settlement_id"] = "forge_town",
                ["submission_source"] = "forge",
                ["entries"] = new List<object>(),
            },
        };

        PlainDictionary snapshot = BuildSnapshot(runtime, out _);
        _test.Eq(
            Dict(Dict(snapshot, "forge"), "window_data").Count,
            0,
            "只有 submission_source=forge 的旧窗口上下文不应再被识别为 forge modal。"
        );
    }

    private void TestSnapshotBuilderExposesGameOverSnapshot()
    {
        var runtime = new SnapshotTestRuntime
        {
            ActiveModalKind = RuntimeModalKind.GameOver,
            GameOverContext = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["title"] = "Game Over",
                ["description"] = "主角已阵亡，本次旅程结束。",
                ["confirm_text"] = "返回标题",
                ["main_character_member_id"] = "player_sword_01",
                ["main_character_name"] = "剑士",
                ["main_character_dead"] = true,
            },
        };

        PlainDictionary snapshot = BuildSnapshot(runtime, out _);
        PlainDictionary gameOverSnapshot = Dict(snapshot, "game_over");
        _test.Eq(StringValue(gameOverSnapshot, "title"), "Game Over", "game_over 快照应暴露标题。");
        _test.Eq(StringValue(gameOverSnapshot, "main_character_member_id"), "player_sword_01", "game_over 快照应暴露主角成员 ID。");
        _test.True(BoolValue(gameOverSnapshot, "main_character_dead"), "game_over 快照应标记主角死亡。");
    }

    private void TestSnapshotBuilderExposesBattleLootSnapshot()
    {
        var runtime = new SnapshotTestRuntime
        {
            LastBattleLootSnapshot = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["battle_name"] = "荒狼巢穴",
                ["winner_faction_id"] = "player",
                ["loot_entries"] = new List<object>
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["item_id"] = "beast_hide",
                        ["quantity"] = 2,
                    },
                },
                ["loot_entry_count"] = 1,
                ["loot_summary_text"] = "兽皮 x2",
                ["overflow_entries"] = new List<object>
                {
                    new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["item_id"] = "beast_hide",
                        ["quantity"] = 1,
                    },
                },
                ["overflow_entry_count"] = 1,
                ["overflow_summary_text"] = "兽皮 x1",
            },
        };

        PlainDictionary snapshot = BuildSnapshot(runtime, out _);
        PlainDictionary lootSnapshot = Dict(snapshot, "loot");
        _test.Eq(StringValue(lootSnapshot, "battle_name"), "荒狼巢穴", "loot 快照应保留最近一次战斗名称。");
        _test.Eq(IntValue(lootSnapshot, "loot_entry_count"), 1, "loot 快照应暴露 loot 条目数。");
        _test.Eq(IntValue(lootSnapshot, "overflow_entry_count"), 1, "loot 快照应暴露 overflow 条目数。");
        _test.True(
            HasItemQuantity(ArrayValue(lootSnapshot, "loot_entries"), "beast_hide", 2),
            "loot 快照应保留 loot item/quantity。"
        );
        _test.True(
            HasItemQuantity(ArrayValue(lootSnapshot, "overflow_entries"), "beast_hide", 1),
            "loot 快照应保留 overflow item/quantity。"
        );
    }

    private void TestSnapshotBuilderOmitsLootSectionWhenEmpty()
    {
        PlainDictionary snapshot = BuildSnapshot(new SnapshotTestRuntime(), out _);
        _test.Eq(Dict(snapshot, "loot").Count, 0, "没有最近掉落时 headless snapshot 不应强行生成 loot 段。");
    }

    private GameSession CreateTestSession()
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        int createError = gameSession.CreateNewSave(TestWorldConfig);
        _test.Eq(createError, (int)Error.Ok, "GameSession 应能基于测试世界配置创建新存档。");
        if (createError != (int)Error.Ok)
        {
            CleanupTestSession(gameSession);
            return null;
        }
        return gameSession;
    }

    private static void CleanupTestSession(GameSession gameSession)
    {
        if (gameSession == null)
            return;
        gameSession.ClearPersistedGame();
        gameSession.Free();
    }

    private static PlainDictionary BuildSnapshot(
        IGameRuntimeSnapshotSource runtime,
        out string textSnapshot
    )
    {
        var builder = new GameRuntimeSnapshotBuilder();
        builder.Setup(runtime);
        PlainDictionary snapshot = builder.BuildHeadlessSnapshotPlain();
        textSnapshot = builder.BuildTextSnapshot();
        builder.Dispose();
        return snapshot;
    }

    private static PlainDictionary BuildPartyQuestSnapshot(PartyState partyState)
    {
        var runtime = new SnapshotTestRuntime { PartyState = partyState };
        PlainDictionary snapshot = BuildSnapshot(runtime, out _);
        return Dict(Dict(snapshot, "party"), "quests");
    }

    private static PlainDictionary FindMemberSnapshot(PlainDictionary snapshot, string memberId)
    {
        foreach (object memberValue in ArrayValue(Dict(snapshot, "party"), "members"))
        {
            if (memberValue is not PlainDictionary member)
                continue;
            if (StringValue(member, "member_id") == memberId)
                return member;
        }
        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static GDictionary BuildQuestSnapshotPayload(
        string questId,
        object stageId,
        string statusId = "active",
        int completedAtWorldStep = -1
    )
    {
        var payload = new GDictionary
        {
            ["quest_id"] = questId,
            ["status_id"] = statusId,
            ["objective_progress"] = new GDictionary(),
            ["accepted_at_world_step"] = 1,
            ["completed_at_world_step"] = completedAtWorldStep,
            ["reward_claimed_at_world_step"] = -1,
            ["failed_at_world_step"] = -1,
            ["failure_reason_id"] = "",
            ["last_progress_context"] = new GDictionary(),
        };
        if (stageId is string stageString)
            payload["stage_id"] = stageString;
        else if (stageId is int stageInt)
            payload["stage_id"] = stageInt;
        else if (stageId is StringName stageStringName)
            payload["stage_id"] = stageStringName;
        return payload;
    }

    private static Dictionary<string, object> BuildWarehouseEntry(string itemId, int quantity)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["item_id"] = itemId,
            ["quantity"] = quantity,
            ["total_quantity"] = quantity,
            ["is_stackable"] = true,
            ["stack_limit"] = 20,
            ["storage_mode"] = "stack",
        };
    }

    private static Dictionary<string, object> BuildWindowEntry(
        string entryId,
        string questId,
        string displayName,
        string stateLabel,
        string costLabel,
        string providerKind = "",
        string[] listingChannels = null,
        bool isEnabled = true,
        string disabledReason = "",
        string acceptDialogueText = ""
    )
    {
        var channels = new List<object>();
        if (listingChannels != null)
        {
            foreach (string channel in listingChannels)
            {
                channels.Add(channel);
            }
        }
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["entry_id"] = entryId,
            ["quest_id"] = questId,
            ["display_name"] = displayName,
            ["state_label"] = stateLabel,
            ["cost_label"] = costLabel,
            ["provider_kind"] = providerKind,
            ["listing_channels"] = channels,
            ["is_enabled"] = isEnabled,
            ["disabled_reason"] = disabledReason,
            ["accept_dialogue_text"] = acceptDialogueText,
        };
    }

    private static List<string> ExtractWindowEntryValueStrings(GArray entries, string key)
    {
        var result = new List<string>();
        foreach (Variant entryValue in entries)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;
            result.Add(StringValue(entryValue.AsGodotDictionary(), key));
        }
        return result;
    }

    private static List<string> ExtractWindowEntryValueStrings(
        PlainArray entries,
        string key
    )
    {
        var result = new List<string>();
        if (entries == null)
            return result;
        foreach (object entryValue in entries)
        {
            if (entryValue is PlainDictionary entry)
                result.Add(StringValue(entry, key));
        }
        return result;
    }

    private static bool HasItemQuantity(GArray entries, string itemId, int quantity)
    {
        foreach (Variant entryValue in entries)
        {
            if (entryValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary entry = entryValue.AsGodotDictionary();
            if (StringValue(entry, "item_id") == itemId && IntValue(entry, "quantity") == quantity)
                return true;
        }
        return false;
    }

    private static bool HasItemQuantity(PlainArray entries, string itemId, int quantity)
    {
        if (entries == null)
            return false;
        foreach (object entryValue in entries)
        {
            if (
                entryValue is PlainDictionary entry
                && StringValue(entry, "item_id") == itemId
                && IntValue(entry, "quantity") == quantity
            )
                return true;
        }
        return false;
    }

    private static List<string> TextSnapshotLines(string text)
    {
        var lines = new List<string>();
        string normalized = (text ?? "").Replace("\r", "");
        foreach (string line in normalized.Split('\n'))
        {
            if (line.Length > 0)
                lines.Add(line);
        }
        return lines;
    }

    private static bool HasTextLine(IEnumerable<string> lines, string expected)
    {
        foreach (string line in lines)
        {
            if (line == expected)
                return true;
        }
        return false;
    }

    private static bool HasTextLinePrefix(IEnumerable<string> lines, string prefix)
    {
        foreach (string line in lines)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static List<string> StringList(GArray values)
    {
        var result = new List<string>();
        foreach (Variant value in values)
            result.Add(value.AsString());
        return result;
    }

    private static List<string> StringList(PlainArray values)
    {
        var result = new List<string>();
        if (values == null)
            return result;
        foreach (object value in values)
        {
            if (value is string text)
                result.Add(text);
            else if (value is StringName name)
                result.Add(name.ToString());
        }
        return result;
    }

    private static PlainDictionary Dict(PlainDictionary source, string key)
    {
        return source != null
            && source.TryGetValue(key, out object rawValue)
            && rawValue is PlainDictionary dictionary
            ? dictionary
            : new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static PlainArray ArrayValue(PlainDictionary source, string key)
    {
        return source != null
            && source.TryGetValue(key, out object rawValue)
            && rawValue is PlainArray array
            ? array
            : new List<object>();
    }

    private static string StringValue(
        PlainDictionary source,
        string key,
        string fallback = ""
    )
    {
        if (source == null || !source.TryGetValue(key, out object rawValue))
            return fallback;
        return rawValue switch
        {
            string text => text,
            StringName name => name.ToString(),
            _ => fallback,
        };
    }

    private static int IntValue(PlainDictionary source, string key, int fallback = 0)
    {
        if (source == null || !source.TryGetValue(key, out object rawValue))
            return fallback;
        return rawValue switch
        {
            int value => value,
            long value when value is >= int.MinValue and <= int.MaxValue => (int)value,
            _ => fallback,
        };
    }

    private static bool BoolValue(
        PlainDictionary source,
        string key,
        bool fallback = false
    )
    {
        return source != null
            && source.TryGetValue(key, out object rawValue)
            && rawValue is bool value
            ? value
            : fallback;
    }

    private static string RenderRawSnapshot(GDictionary snapshot)
    {
        return GameTextSnapshotRenderer.RenderFullSnapshot(
            RuntimePlainPayload.NormalizeDictionaryStrict(
                snapshot,
                "run_game_runtime_snapshot_builder_regression.renderer_input"
            )
        );
    }

    private static GDictionary Dict(GDictionary source, string key)
    {
        if (
            source != null
            && source.ContainsKey(key)
            && source[key].VariantType == Variant.Type.Dictionary
        )
            return source[key].AsGodotDictionary();
        return new GDictionary();
    }

    private static GArray ArrayValue(GDictionary source, string key)
    {
        if (source != null && source.ContainsKey(key) && source[key].VariantType == Variant.Type.Array)
            return source[key].AsGodotArray();
        return new GArray();
    }

    private static string StringValue(GDictionary source, string key, string fallback = "")
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        if (value.VariantType == Variant.Type.Nil)
            return fallback;
        return value.AsString();
    }

    private static int IntValue(GDictionary source, string key, int fallback = 0)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
    }

    private static bool BoolValue(GDictionary source, string key, bool fallback = false)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
    }

    private void AssertAuditBaseline(
        LifecycleAuditSnapshot expected,
        LifecycleAuditSnapshot actual,
        string label
    )
    {
        _test.Eq(actual.ActiveOwnerCount, expected.ActiveOwnerCount, $"{label}: owner baseline");
        _test.Eq(actual.ActiveLeaseCount, expected.ActiveLeaseCount, $"{label}: lease baseline");
        _test.Eq(actual.ActiveScopeCount, expected.ActiveScopeCount, $"{label}: scope baseline");
        _test.Eq(
            actual.ActiveContentBorrowerCount,
            expected.ActiveContentBorrowerCount,
            $"{label}: borrower baseline"
        );
    }

    private void AssertSafetyCounters(
        LifecycleAuditSnapshot expected,
        LifecycleAuditSnapshot actual,
        string label
    )
    {
        _test.Eq(actual.EscapedCount, expected.EscapedCount, $"{label}: escaped baseline");
        _test.Eq(actual.UnknownCount, expected.UnknownCount, $"{label}: unknown baseline");
        _test.Eq(actual.ViolationCount, expected.ViolationCount, $"{label}: violation baseline");
        _test.Eq(
            actual.NormalPhaseSuppressCount,
            expected.NormalPhaseSuppressCount,
            $"{label}: suppress baseline"
        );
        _test.Eq(
            actual.QuarantineCount,
            expected.QuarantineCount,
            $"{label}: quarantine baseline"
        );
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static int CountContainers(GDictionary dictionary)
    {
        int count = 1;
        foreach (Variant key in dictionary.Keys)
        {
            Variant value = dictionary[key];
            if (value.VariantType == Variant.Type.Dictionary)
            {
                using GDictionary nested = value.AsGodotDictionary();
                count += CountContainers(nested);
            }
            else if (value.VariantType == Variant.Type.Array)
            {
                using GArray nested = value.AsGodotArray();
                count += CountContainers(nested);
            }
        }
        return count;
    }

    private static int CountContainers(GArray array)
    {
        int count = 1;
        for (int index = 0; index < array.Count; index++)
        {
            Variant value = array[index];
            if (value.VariantType == Variant.Type.Dictionary)
            {
                using GDictionary nested = value.AsGodotDictionary();
                count += CountContainers(nested);
            }
            else if (value.VariantType == Variant.Type.Array)
            {
                using GArray nested = value.AsGodotArray();
                count += CountContainers(nested);
            }
        }
        return count;
    }

    private static bool IsStrictPlainGraph(object value)
    {
        if (value == null)
            return true;
        if (value is PlainDictionary dictionary)
        {
            foreach ((string key, object child) in dictionary)
            {
                if (string.IsNullOrEmpty(key) || !IsStrictPlainGraph(child))
                    return false;
            }
            return true;
        }
        if (value is PlainArray array)
        {
            foreach (object child in array)
            {
                if (!IsStrictPlainGraph(child))
                    return false;
            }
            return true;
        }
        return value
            is string
                or StringName
                or bool
                or byte
                or short
                or int
                or long
                or float
                or double
                or Vector2I
                or Vector2
                or Vector3I
                or Vector3
                or Color;
    }

    private void AssertStringListEq(
        IEnumerable<string> actual,
        IEnumerable<string> expected,
        string message
    )
    {
        string actualText = string.Join(",", actual);
        string expectedText = string.Join(",", expected);
        if (actualText != expectedText)
            _test.Fail($"{message} | actual=[{actualText}] expected=[{expectedText}]");
    }

}
