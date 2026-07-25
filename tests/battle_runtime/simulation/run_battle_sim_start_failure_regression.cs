using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_sim_start_failure_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestInvalidUnitsAreReportedAsInvalidRuntime();
            TestPlacementExhaustionIsReportedAsInvalidRuntime();
            TestSuccessfulRetryClearsTransientStartFailure();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled battle sim start failure regression exception: {exception}");
        }

        RequestTestExit(_test.Finish("Battle sim start failure regression"));
    }

    private void TestInvalidUnitsAreReportedAsInvalidRuntime()
    {
        BattleSimScenarioReport report = RunScenario(BuildEmptyRosterScenario());
        AssertStartFailureReport(
            report,
            "invalid_start_units",
            expectedAllyUnitCount: 0,
            expectedEnemyUnitCount: 0,
            label: "empty roster"
        );
    }

    private void TestPlacementExhaustionIsReportedAsInvalidRuntime()
    {
        using var terrainGenerator = new EmptyTerrainGenerator();
        BattleSimScenarioReport report = RunScenario(
            BuildValidRosterScenario(),
            terrainGenerator
        );
        AssertStartFailureReport(
            report,
            "placement_exhausted",
            expectedAllyUnitCount: -1,
            expectedEnemyUnitCount: -1,
            label: "placement exhaustion"
        );

        BattleSimRunReport run = GetOnlyRun(report);
        _test.True(
            (run?.StartFailure?.PlacementAttempts ?? -1) > 0,
            "placement exhaustion 应保留已耗尽的布阵尝试数。"
        );
    }

    private void TestSuccessfulRetryClearsTransientStartFailure()
    {
        var contentProvider = new BattleSimContentProvider(
            GameSessionTestFactory.GetProcessSnapshot()
        );
        using var terrainGenerator = new RetryReachabilityTerrainGenerator();
        using var runtime = new BattleRuntimeModule();
        runtime.setup(
            character_gateway: null,
            skill_definitions: contentProvider.GetSkillDefinitionsTyped(),
            enemy_templates: contentProvider.GetEnemyTemplatesTyped(),
            enemy_ai_brains: contentProvider.GetEnemyAiBrainsTyped(),
            terrain_generator: terrainGenerator,
            barrier_profile_definitions: contentProvider.GetBarrierProfileDefinitionsTyped()
        );

        BattleSimScenarioDefinition scenario = BuildValidRosterScenario();
        using GodotProjectionLease<GDictionary> contextLease =
            scenario.BuildStartContextLease();
        contextLease.Value["validate_spawn_reachability"] = true;
        contextLease.Value["validate_bidirectional_spawn_reachability"] = true;
        var encounterAnchor = new EncounterAnchorData
        {
            entity_id = "battle_sim_start_retry_regression",
            display_name = "Battle Sim Start Retry Regression",
            faction_id = "hostile",
            region_tag = "simulation",
        };

        BattleState state = runtime.StartBattleBorrowingContext(
            encounterAnchor,
            303,
            BattleEliminationObjectiveDefinition.Instance,
            contextLease.Value
        );

        _test.Eq(
            terrainGenerator.GenerateCallCount,
            2,
            "出生可达性首轮失败后应使用下一轮地形重试。"
        );
        _test.True(
            ReferenceEquals(state, runtime.GetState()) && !state.IsEmpty(),
            "后续布阵成功时应返回 runtime 正式持有的战斗 state。"
        );
        _test.True(
            runtime.GetLastStartFailureSnapshot().IsEmpty,
            "成功重试后不应残留首轮 spawn_reachability 失败快照。"
        );
    }

    private BattleSimScenarioReport RunScenario(
        BattleSimScenarioDefinition scenario,
        BattleTerrainGenerator terrainGenerator = null
    )
    {
        var contentProvider = new BattleSimContentProvider(
            GameSessionTestFactory.GetProcessSnapshot()
        );
        var runner = new BattleSimRunner(contentProvider);
        if (terrainGenerator != null)
            runner.Setup(contentProvider, terrainGenerator);
        BattleSimScenarioReport report = runner.RunScenario(
            scenario,
            new List<BattleSimProfileDefinition> { BuildBaselineProfile() }
        );
        CleanupOutputFiles(report.OutputFiles);
        return report;
    }

    private void CleanupOutputFiles(BattleSimOutputFiles outputFiles)
    {
        if (outputFiles == null)
            return;
        RemoveOutputFile(outputFiles.ReportJson, "report_json");
        RemoveOutputFile(outputFiles.TurnTraceJsonl, "turn_trace_jsonl");
        RemoveOutputFile(outputFiles.TraceSummaryJson, "trace_summary_json");
    }

    private void RemoveOutputFile(string path, string label)
    {
        if (string.IsNullOrEmpty(path))
            return;
        Error error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        _test.True(
            error is Error.Ok or Error.FileNotFound,
            $"Battle sim start failure 回归应清理 {label} 测试产物：{error}。"
        );
    }

    private void AssertStartFailureReport(
        BattleSimScenarioReport report,
        string expectedReason,
        int expectedAllyUnitCount,
        int expectedEnemyUnitCount,
        string label
    )
    {
        BattleSimRunReport run = GetOnlyRun(report);
        _test.True(run != null, $"{label} 应保留单局诊断。");
        if (run == null)
            return;

        _test.Eq(
            run.TerminationKind,
            BattleSimTerminationKind.InvalidRuntime,
            $"{label} 应归类为 invalid_runtime。"
        );
        _test.False(run.Stalled, $"{label} 不应归类为 idle stall。");
        _test.Eq(run.Iterations, 0, $"{label} 不应进入执行循环。");
        _test.Eq(run.IdleLoops, 0, $"{label} 不应累计 idle loop。");
        _test.Eq(run.TimelineSteps, 0, $"{label} 不应推进时间轴。");
        _test.Eq(
            run.StartFailure?.Reason ?? "",
            expectedReason,
            $"{label} 应保留启动失败原因。"
        );
        if (expectedAllyUnitCount >= 0)
            _test.Eq(
                run.StartFailure?.AllyUnitCount ?? -1,
                expectedAllyUnitCount,
                $"{label} 应保留 ally 数量。"
            );
        if (expectedEnemyUnitCount >= 0)
            _test.Eq(
                run.StartFailure?.EnemyUnitCount ?? -1,
                expectedEnemyUnitCount,
                $"{label} 应保留 enemy 数量。"
            );

        _test.Eq(report.RunCount, 1, $"{label} 应计入一次尝试。");
        _test.Eq(report.InvalidRuntimeRunCount, 1, $"{label} 应计入 invalid runtime。");
        _test.Eq(report.StalledRunCount, 0, $"{label} 不应污染 stalled 统计。");
        _test.Eq(
            report.ProfileEntries[0].Summary.InvalidRuntimeRunCount,
            1,
            $"{label} profile summary 应计入 invalid runtime。"
        );
        _test.Eq(
            report.ProfileEntries[0].Summary.StalledRunCount,
            0,
            $"{label} profile summary 不应计入 stalled。"
        );

        using (
            GodotProjectionLease<GDictionary> projectionLease =
                BattleSimReportProjection.BuildLease(report)
        )
        {
            AssertProjectedStartFailure(
                projectionLease.Value,
                expectedReason,
                $"{label} Godot report projection"
            );
        }
        using (
            GodotProjectionLease<GDictionary> fileLease =
                BattleSimFilePayloadProjection.BuildReportLease(report)
        )
        {
            AssertProjectedStartFailure(
                fileLease.Value,
                expectedReason,
                $"{label} file report projection"
            );
        }
        using (
            GodotProjectionLease<GDictionary> traceSummaryLease =
                new BattleSimTraceSummaryBuilder().BuildLease(report)
        )
        {
            AssertProjectedStartFailure(
                traceSummaryLease.Value,
                expectedReason,
                $"{label} trace summary projection",
                compactTraceShape: true
            );
        }
    }

    private void AssertProjectedStartFailure(
        GDictionary root,
        string expectedReason,
        string label,
        bool compactTraceShape = false
    )
    {
        using GArray runs = GetProjectedRuns(root, compactTraceShape);
        _test.Eq(runs.Count, 1, $"{label} 应保留单局条目。");
        if (runs.Count == 0)
            return;
        using GDictionary run = runs[0].AsGodotDictionary();
        using GDictionary startFailure = run["start_failure"].AsGodotDictionary();
        _test.Eq(
            startFailure["reason"].AsString(),
            expectedReason,
            $"{label} 应保留 start_failure.reason。"
        );
    }

    private static GArray GetProjectedRuns(GDictionary root, bool compactTraceShape)
    {
        if (compactTraceShape)
            return root["runs"].AsGodotArray();
        using GArray profileEntries = root["profile_entries"].AsGodotArray();
        using GDictionary profileEntry = profileEntries[0].AsGodotDictionary();
        return profileEntry["runs"].AsGodotArray();
    }

    private BattleSimRunReport GetOnlyRun(BattleSimScenarioReport report)
    {
        _test.True(report != null, "Battle sim start failure 应产出 report。");
        _test.Eq(report?.ProfileEntries.Count ?? 0, 1, "应产出单 profile entry。");
        if (report?.ProfileEntries.Count != 1)
            return null;
        _test.Eq(report.ProfileEntries[0].Runs.Count, 1, "应产出单 run entry。");
        return report.ProfileEntries[0].Runs.Count == 1
            ? report.ProfileEntries[0].Runs[0]
            : null;
    }

    private static BattleSimScenarioDefinition BuildEmptyRosterScenario()
    {
        BattleSimScenarioDef scenario = TestResourceOwnership.Own(
            new BattleSimScenarioDef
            {
                scenario_id = "battle_sim_invalid_start_units_regression",
                display_name = "Battle Sim Invalid Start Units Regression",
                map_size = new Vector2I(3, 3),
                max_iterations = 10,
                seeds = new[] { 101 },
            },
            "BattleSimStartFailure.empty-roster-scenario"
        );
        return scenario.ToDefinition();
    }

    private static BattleSimScenarioDefinition BuildValidRosterScenario()
    {
        BattleSimScenarioDef scenario = TestResourceOwnership.Own(
            new BattleSimScenarioDef
            {
                scenario_id = "battle_sim_placement_exhausted_regression",
                display_name = "Battle Sim Placement Exhausted Regression",
                map_size = new Vector2I(3, 3),
                max_iterations = 10,
                seeds = new[] { 202 },
            },
            "BattleSimStartFailure.placement-scenario"
        );
        scenario.ally_units.Add(BuildUnit("placement_ally", "player", new Vector2I(2, 1)));
        scenario.enemy_units.Add(BuildUnit("placement_enemy", "hostile", new Vector2I(0, 1)));
        return scenario.ToDefinition();
    }

    private static BattleSimUnitSpec BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord
    )
    {
        return TestResourceOwnership.Own(
            new BattleSimUnitSpec
            {
                unit_id = unitId,
                display_name = unitId.ToString(),
                faction_id = factionId,
                control_mode = "manual",
                coord = coord,
                current_hp = 30,
                current_ap = 1,
                skill_ids = new GArray { "basic_attack" },
                skill_level_map = new GDictionary { ["basic_attack"] = 0 },
                base_attributes = new GDictionary
                {
                    ["strength"] = 10,
                    ["agility"] = 10,
                    ["constitution"] = 10,
                    ["perception"] = 10,
                    ["intelligence"] = 10,
                    ["willpower"] = 10,
                },
                attribute_overrides = new GDictionary
                {
                    ["hp_max"] = 30,
                    ["action_points"] = 1,
                    ["armor_ac_bonus"] = 4,
                },
            },
            $"BattleSimStartFailure.unit.{unitId}"
        );
    }

    private static BattleSimProfileDefinition BuildBaselineProfile() =>
        new(
            "baseline",
            "Baseline",
            "",
            BattleAiScoreProfileDefinition.Default,
            Array.Empty<BattleSimOverridePatchDefinition>()
        );

    private sealed class EmptyTerrainGenerator : BattleTerrainGenerator
    {
        internal override BattleTerrainLayout GenerateTyped(
            EncounterAnchorData encounterAnchor,
            long seed,
            GDictionary context
        ) => new();
    }

    private sealed class RetryReachabilityTerrainGenerator : BattleTerrainGenerator
    {
        internal int GenerateCallCount { get; private set; }

        internal override BattleTerrainLayout GenerateTyped(
            EncounterAnchorData encounterAnchor,
            long seed,
            GDictionary context
        )
        {
            GenerateCallCount++;
            bool blockMiddleColumn = GenerateCallCount == 1;
            Vector2I mapSize = new(3, 2);
            var cells = new Dictionary<Vector2I, BattleCellState>();
            for (int y = 0; y < mapSize.Y; y++)
            {
                for (int x = 0; x < mapSize.X; x++)
                {
                    var cell = new BattleCellState
                    {
                        coord = new Vector2I(x, y),
                        base_terrain = blockMiddleColumn && x == 1
                            ? "deep_water"
                            : "land",
                        base_height = 4,
                        height_offset = 0,
                    };
                    cell.RecalculateRuntimeValues();
                    cells[cell.coord] = cell;
                }
            }
            return new BattleTerrainLayout(
                mapSize,
                cells,
                new[] { new Vector2I(2, 1) },
                new[] { new Vector2I(0, 1) },
                new Vector2I(2, 1),
                new Vector2I(0, 1),
                "default"
            );
        }
    }
}
