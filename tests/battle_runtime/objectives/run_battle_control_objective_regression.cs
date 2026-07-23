using System;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_control_objective_regression
    : LifecycleTestSceneTree
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
            TestDefinitionRejectsInvalidZonesAndScoreTarget();
            TestInitializationFreezesNonOverlappingZones();
            TestExclusivePlayerAndHostileScoringReachTheirOutcomes();
            TestContestedAndNeutralZonesDoNotScore();
            TestPartialFootprintDoesNotControlZone();
            TestSimultaneousScoreThresholdIsDraw();
            TestPartyDefeatBeforeThresholdFails();
            TestEnemyDefeatDoesNotCompleteObjective();
            TestScoreCompletionWinsOverPartyDefeatInSameMutation();
            TestProgressAndHudSnapshotsExposeControlFacts();
            TestDuplicateStatePreservesDetachedControlProgress();
            TestFormalControlEncounterStartsWithBoundZones();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unhandled battle control objective regression exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Battle control objective regression"));
    }

    private void TestDefinitionRejectsInvalidZonesAndScoreTarget()
    {
        _test.True(
            Throws<ArgumentException>(
                () =>
                    _ = new BattleControlObjectiveDefinition(
                        Array.Empty<BattleControlZoneDefinition>(),
                        100
                    )
            ),
            "区域占领定义必须拒绝空区域集合。"
        );
        _test.True(
            Throws<ArgumentException>(
                () =>
                    _ = new BattleControlObjectiveDefinition(
                        new[]
                        {
                            BuildZone("duplicate_zone", BattleMapEdge.Left, 1),
                            BuildZone("duplicate_zone", BattleMapEdge.Right, 1),
                        },
                        100
                    )
            ),
            "区域占领定义必须拒绝重复 zone_id。"
        );
        _test.True(
            Throws<ArgumentOutOfRangeException>(
                () =>
                    _ = new BattleControlObjectiveDefinition(
                        new[] { BuildZone("valid_zone", BattleMapEdge.Left, 1) },
                        7
                    )
            ),
            "占领分数目标必须是正的 5 TU 倍数。"
        );
    }

    private void TestInitializationFreezesNonOverlappingZones()
    {
        using BattleTestFixture fixture = CreateControlBattle(
            "control_binding",
            new Vector2I(5, 1),
            Vector2I.Zero,
            new Vector2I(4, 0),
            new BattleControlObjectiveDefinition(
                new[]
                {
                    BuildZone("west_zone", BattleMapEdge.Left, 2),
                    BuildZone("east_zone", BattleMapEdge.Right, 2),
                },
                100
            )
        );
        var objective = fixture.State.ObjectiveRuntimeState
            as BattleControlObjectiveRuntimeState;

        _test.True(objective != null, "区域占领应生成独立运行时状态。");
        _test.Eq(
            objective?.ControlZones.Count ?? 0,
            2,
            "运行时必须冻结全部声明区域。"
        );
        _test.Eq(
            objective?.ControlZones.SelectMany(zone => zone.Coords).Distinct().Count()
                ?? 0,
            objective?.ControlZones.Sum(zone => zone.Coords.Count) ?? -1,
            "运行时区域坐标不得重叠。"
        );

        BattleUnitState ally = BuildPersistentAlly(
            "control_overlap_ally",
            Vector2I.Zero
        );
        BattleUnitState enemy = BuildEnemy(
            "control_overlap_enemy",
            new Vector2I(4, 0)
        );
        using BattleTestFixture overlapFixture = BattleTestFixture.CreateFlatBattle(
            "control_overlap",
            new Vector2I(5, 1),
            new[] { ally },
            new[] { enemy }
        );
        _test.False(
            overlapFixture.Runtime.InitializeBattleObjective(
                new BattleControlObjectiveDefinition(
                    new[]
                    {
                        BuildZone("wide_west", BattleMapEdge.Left, 3),
                        BuildZone("wide_east", BattleMapEdge.Right, 3),
                    },
                    100
                )
            ),
            "实际地图上的占领区域重叠时必须拒绝绑定。"
        );
    }

    private void TestExclusivePlayerAndHostileScoringReachTheirOutcomes()
    {
        using (
            BattleTestFixture playerFixture = CreateControlBattle(
                "control_player_score",
                new Vector2I(5, 1),
                Vector2I.Zero,
                new Vector2I(4, 0),
                BuildSingleZoneDefinition(
                    "west_zone",
                    BattleMapEdge.Left,
                    2,
                    10
                )
            )
        )
        using (BattleEventBatch batch = playerFixture.Runtime.advance(2))
        {
            var objective = (BattleControlObjectiveRuntimeState)
                playerFixture.State.ObjectiveRuntimeState;
            _test.Eq(objective.PlayerScore, 10, "我方独占区域应按 TU 累积分数。");
            _test.Eq(objective.HostileScore, 0, "敌方未占领时不得获得分数。");
            _test.True(batch.battle_ended, "我方达到分数目标应立即结束战斗。");
            AssertDecision(
                playerFixture.State.FinalDecision,
                BattleOutcomeKind.PlayerSuccess,
                BattleEndReasonKind.ControlPlayerScoreReached,
                "我方占领达标"
            );
        }

        using (
            BattleTestFixture hostileFixture = CreateControlBattle(
                "control_hostile_score",
                new Vector2I(5, 1),
                Vector2I.Zero,
                new Vector2I(4, 0),
                BuildSingleZoneDefinition(
                    "east_zone",
                    BattleMapEdge.Right,
                    2,
                    10
                )
            )
        )
        using (BattleEventBatch batch = hostileFixture.Runtime.advance(2))
        {
            var objective = (BattleControlObjectiveRuntimeState)
                hostileFixture.State.ObjectiveRuntimeState;
            _test.Eq(objective.PlayerScore, 0, "我方未占领时不得获得分数。");
            _test.Eq(objective.HostileScore, 10, "敌方独占区域应按 TU 累积分数。");
            _test.True(batch.battle_ended, "敌方达到分数目标应立即结束战斗。");
            AssertDecision(
                hostileFixture.State.FinalDecision,
                BattleOutcomeKind.PlayerFailure,
                BattleEndReasonKind.ControlHostileScoreReached,
                "敌方占领达标"
            );
        }
    }

    private void TestContestedAndNeutralZonesDoNotScore()
    {
        using (
            BattleTestFixture contestedFixture = CreateControlBattle(
                "control_contested",
                new Vector2I(5, 1),
                Vector2I.Zero,
                Vector2I.Right,
                BuildSingleZoneDefinition(
                    "west_zone",
                    BattleMapEdge.Left,
                    2,
                    100
                )
            )
        )
        using (BattleEventBatch batch = contestedFixture.Runtime.advance(1))
        {
            var objective = (BattleControlObjectiveRuntimeState)
                contestedFixture.State.ObjectiveRuntimeState;
            _test.Eq(objective.PlayerScore, 0, "争夺区域不得给我方计分。");
            _test.Eq(objective.HostileScore, 0, "争夺区域不得给敌方计分。");
            _test.False(batch.battle_ended, "争夺状态不应结束战斗。");
        }

        using (
            BattleTestFixture neutralFixture = CreateControlBattle(
                "control_neutral",
                new Vector2I(5, 1),
                new Vector2I(2, 0),
                new Vector2I(4, 0),
                BuildSingleZoneDefinition(
                    "west_zone",
                    BattleMapEdge.Left,
                    1,
                    100
                )
            )
        )
        using (BattleEventBatch batch = neutralFixture.Runtime.advance(1))
        {
            var objective = (BattleControlObjectiveRuntimeState)
                neutralFixture.State.ObjectiveRuntimeState;
            _test.Eq(objective.PlayerScore, 0, "中立区域不得给我方计分。");
            _test.Eq(objective.HostileScore, 0, "中立区域不得给敌方计分。");
            _test.False(batch.battle_ended, "中立状态不应结束战斗。");
        }
    }

    private void TestPartialFootprintDoesNotControlZone()
    {
        BattleUnitState largeAlly = BuildPersistentAlly(
            "control_partial_footprint_ally",
            new Vector2I(1, 0)
        );
        _test.True(
            largeAlly.SetBodySizeCategory("large"),
            "占领 footprint 回归应成功构建 2×2 单位。"
        );
        BattleUnitState enemy = BuildEnemy(
            "control_partial_footprint_enemy",
            new Vector2I(5, 0)
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "control_partial_footprint",
            new Vector2I(6, 2),
            new[] { largeAlly },
            new[] { enemy }
        );
        foreach (BattleUnitState unit in fixture.State.GetUnitsTyped())
            unit.action_threshold = 1_000_000;
        fixture.State.PhaseKind = BattlePhaseKind.TimelineRunning;
        fixture.State.active_unit_id = "";
        _test.True(
            fixture.Runtime.InitializeBattleObjective(
                BuildSingleZoneDefinition(
                    "west_zone",
                    BattleMapEdge.Left,
                    2,
                    100
                )
            ),
            "部分 footprint 场景应成功初始化区域占领目标。"
        );
        using BattleEventBatch batch = fixture.Runtime.advance(1);

        var objective = (BattleControlObjectiveRuntimeState)
            fixture.State.ObjectiveRuntimeState;
        _test.Eq(
            BattleControlObjectiveRules.ResolveOccupancy(
                fixture.State,
                objective.ControlZones[0]
            ),
            BattleControlZoneOccupancyKind.Neutral,
            "单位 footprint 只有部分进入区域时不得形成占领。"
        );
        _test.Eq(
            objective.PlayerScore,
            0,
            "部分 footprint 进入区域不得给我方计分。"
        );
    }

    private void TestSimultaneousScoreThresholdIsDraw()
    {
        using BattleTestFixture fixture = CreateControlBattle(
            "control_tied_scores",
            new Vector2I(5, 1),
            Vector2I.Zero,
            new Vector2I(4, 0),
            new BattleControlObjectiveDefinition(
                new[]
                {
                    BuildZone("west_zone", BattleMapEdge.Left, 2),
                    BuildZone("east_zone", BattleMapEdge.Right, 2),
                },
                5
            )
        );
        using BattleEventBatch batch = fixture.Runtime.advance(1);

        var objective = (BattleControlObjectiveRuntimeState)
            fixture.State.ObjectiveRuntimeState;
        _test.Eq(objective.PlayerScore, 5, "同一时间步我方应获得占领分。");
        _test.Eq(objective.HostileScore, 5, "同一时间步敌方应获得占领分。");
        _test.True(batch.battle_ended, "双方同刻达标应立即结束战斗。");
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.Draw,
            BattleEndReasonKind.ControlScoresTied,
            "双方占领分同刻达标"
        );
    }

    private void TestPartyDefeatBeforeThresholdFails()
    {
        using BattleTestFixture fixture = CreateControlBattle(
            "control_party_defeat",
            new Vector2I(5, 1),
            new Vector2I(2, 0),
            new Vector2I(4, 0),
            BuildSingleZoneDefinition(
                "west_zone",
                BattleMapEdge.Left,
                1,
                100
            )
        );
        using BattleEventBatch batch = new();

        DefeatUnitAtomically(fixture.Runtime, fixture.Allies[0], batch);

        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.ControlPartyDefeated,
            "占领达标前队伍覆灭"
        );
    }

    private void TestEnemyDefeatDoesNotCompleteObjective()
    {
        using BattleTestFixture fixture = CreateControlBattle(
            "control_enemy_defeat",
            new Vector2I(5, 1),
            Vector2I.Zero,
            new Vector2I(4, 0),
            BuildSingleZoneDefinition(
                "west_zone",
                BattleMapEdge.Left,
                2,
                100
            )
        );
        using BattleEventBatch batch = new();

        DefeatUnitAtomically(fixture.Runtime, fixture.Enemies[0], batch);

        _test.True(
            fixture.State.FinalDecision == null,
            "敌军全灭不得替代区域占领达标条件。"
        );
        _test.True(
            fixture.State.PhaseKind != BattlePhaseKind.BattleEnded,
            "敌军全灭后区域占领战仍应继续。"
        );
    }

    private void TestScoreCompletionWinsOverPartyDefeatInSameMutation()
    {
        using BattleTestFixture fixture = CreateControlBattle(
            "control_atomic_score_priority",
            new Vector2I(5, 1),
            Vector2I.Zero,
            new Vector2I(4, 0),
            BuildSingleZoneDefinition(
                "west_zone",
                BattleMapEdge.Left,
                2,
                5
            )
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult result;
        try
        {
            fixture.Runtime.AdvanceControlObjectiveProgress(5, batch);
            fixture.Allies[0].MarkDead();
            fixture.Runtime.HandleUnitDefeatedByRuntimeEffect(
                fixture.Allies[0],
                null,
                batch,
                "",
                new BattleDefeatHandlingOptions(collectLoot: false)
            );
        }
        finally
        {
            result = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            result,
            BattleOutcomeFlushResult.Completed,
            "同批占领达标与队伍覆灭应完成一次原子结算。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.ControlPlayerScoreReached,
            "同批占领达标与队伍覆灭"
        );
    }

    private void TestProgressAndHudSnapshotsExposeControlFacts()
    {
        using BattleTestFixture fixture = CreateControlBattle(
            "control_snapshot",
            new Vector2I(5, 1),
            Vector2I.Zero,
            new Vector2I(4, 0),
            BuildSingleZoneDefinition(
                "west_zone",
                BattleMapEdge.Left,
                2,
                100
            )
        );
        using BattleEventBatch batch = fixture.Runtime.advance(1);

        BattleObjectiveProgressSnapshot progress =
            BattleObjectiveProgressSnapshot.Capture(fixture.State);
        _test.Eq(progress.Mode, BattleObjectiveMode.Control, "快照应保留模式。");
        _test.Eq(progress.ControlZoneCount, 1, "快照应投影占领区数量。");
        _test.Eq(progress.PlayerControlScore, 5, "快照应投影我方占领分。");
        _test.Eq(progress.HostileControlScore, 0, "快照应投影敌方占领分。");
        _test.Eq(progress.ControlScoreTarget, 100, "快照应投影目标分数。");
        _test.Eq(
            progress.ControlZones[0].Occupancy,
            BattleControlZoneOccupancyKind.Player,
            "快照应投影区域归属。"
        );

        BattleHudObjectiveProgressSnapshot hud = new(progress);
        _test.Eq(hud.Title, "区域占领", "HUD 应显示区域占领专用标题。");
        _test.True(
            hud.ProgressText.Contains("我方 5/100", StringComparison.Ordinal)
                && hud.ProgressText.Contains("我方1", StringComparison.Ordinal),
            "HUD 应显示双方分数和区域归属摘要。"
        );
        _test.Eq(hud.ControlZones[0].Occupancy, "player", "HUD 应使用稳定归属值。");
    }

    private void TestDuplicateStatePreservesDetachedControlProgress()
    {
        using BattleTestFixture fixture = CreateControlBattle(
            "control_duplicate",
            new Vector2I(5, 1),
            Vector2I.Zero,
            new Vector2I(4, 0),
            BuildSingleZoneDefinition(
                "west_zone",
                BattleMapEdge.Left,
                2,
                100
            )
        );
        using BattleEventBatch batch = fixture.Runtime.advance(1);
        var original = (BattleControlObjectiveRuntimeState)
            fixture.State.ObjectiveRuntimeState;
        var duplicate = (BattleControlObjectiveRuntimeState)
            original.DuplicateState();

        _test.Eq(duplicate.PlayerScore, 5, "复制运行态应保留我方占领分。");
        _test.Eq(duplicate.HostileScore, 0, "复制运行态应保留敌方占领分。");
        _test.False(
            ReferenceEquals(original.ControlZones[0], duplicate.ControlZones[0]),
            "复制运行态必须深拷贝区域运行事实。"
        );
        _test.True(
            duplicate.TryAdvanceScores(1, 0, 5),
            "复制运行态应能独立推进分数。"
        );
        _test.Eq(original.PlayerScore, 5, "推进复制体不得污染原运行态。");
        _test.Eq(duplicate.PlayerScore, 10, "复制体应保留自身独立进度。");
    }

    private void TestFormalControlEncounterStartsWithBoundZones()
    {
        using GameSession gameSession =
            GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        using EncounterRosterBuilder builder = new();
        builder.Setup(
            gameSession.GetBattleEncounterDefinitions(),
            gameSession.GetEncounterRosterDefinitions(),
            gameSession.GetEnemyTemplateDefinitions()
        );
        var runtime = new BattleRuntimeModule();
        BattleState state = null;
        try
        {
            GameContentCatalog catalog = gameSession.GetContentCatalogTyped();
            runtime.setup(
                null,
                gameSession.GetSkillDefinitionsTyped(),
                gameSession.GetEnemyTemplateDefinitions(),
                gameSession.GetEnemyAiBrainDefinitions(),
                builder,
                item_defs: gameSession.GetItemDefsTyped(),
                skill_catalog: catalog.GetSkillCatalogTyped(),
                trait_defs: catalog.GetTraitDefsTyped(),
                equipment_ability_bindings:
                    catalog.GetEquipmentAbilityBindingDefinitionsTyped()
            );
            BattleEncounterDefinition encounter =
                gameSession.GetBattleEncounterDefinitions()["mist_hollow_control"];
            var anchor = new EncounterAnchorData
            {
                entity_id = "formal_control_start",
                display_name = "正式区域占领开战",
                world_coord = Vector2I.Zero,
                faction_id = "hostile",
                region_tag = "mistwood",
                encounter_profile_id = "mist_hollow_control",
                growth_stage = 0,
            };
            state = runtime.StartBattle(
                anchor,
                240728,
                encounter.Objective,
                new GDictionary
                {
                    ["ally_member_ids"] = new GStringNameArray
                    {
                        "formal_control_ally",
                    },
                    ["validate_spawn_reachability"] = false,
                }
            );

            _test.True(
                state != null && !state.IsEmpty(),
                "正式区域占领遭遇应成功完成开战装配。"
            );
            _test.True(
                state?.ObjectiveRuntimeState
                    is BattleControlObjectiveRuntimeState objective
                    && objective.ControlZones.Count == 2
                    && objective.ScoreTarget == 100
                    && objective.PlayerScore == 0
                    && objective.HostileScore == 0,
                "正式区域占领应冻结两个区域及 100 分目标。"
            );
        }
        finally
        {
            runtime.SetupStateForTests(null);
            BattleTestFixture.DisposeBattleState(state);
            runtime.Dispose();
        }
    }

    private BattleTestFixture CreateControlBattle(
        StringName battleId,
        Vector2I mapSize,
        Vector2I allyCoord,
        Vector2I enemyCoord,
        BattleControlObjectiveDefinition definition
    )
    {
        BattleUnitState ally = BuildPersistentAlly(
            $"{battleId}_ally",
            allyCoord
        );
        BattleUnitState enemy = BuildEnemy(
            $"{battleId}_enemy",
            enemyCoord
        );
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            mapSize,
            new[] { ally },
            new[] { enemy }
        );
        foreach (BattleUnitState unit in fixture.State.GetUnitsTyped())
            unit.action_threshold = 1_000_000;
        fixture.State.PhaseKind = BattlePhaseKind.TimelineRunning;
        fixture.State.active_unit_id = "";
        _test.True(
            fixture.Runtime.InitializeBattleObjective(definition),
            $"{battleId} 应成功初始化区域占领目标。"
        );
        return fixture;
    }

    private static BattleControlObjectiveDefinition BuildSingleZoneDefinition(
        StringName zoneId,
        BattleMapEdge edge,
        int depth,
        int scoreTarget
    ) =>
        new(
            new[] { BuildZone(zoneId, edge, depth) },
            scoreTarget
        );

    private static BattleControlZoneDefinition BuildZone(
        StringName zoneId,
        BattleMapEdge edge,
        int depth
    ) =>
        new(zoneId, zoneId.ToString(), edge, depth);

    private static BattleUnitState BuildPersistentAlly(
        StringName unitId,
        Vector2I coord
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            "player",
            coord,
            currentHp: 20
        );
        unit.source_member_id = $"{unitId}_member";
        return unit;
    }

    private static BattleUnitState BuildEnemy(
        StringName unitId,
        Vector2I coord
    ) =>
        BattleTestFixture.BuildUnit(
            unitId,
            "enemy",
            coord,
            currentHp: 20
        );

    private static void DefeatUnitAtomically(
        BattleRuntimeModule runtime,
        BattleUnitState unit,
        BattleEventBatch batch
    )
    {
        runtime.BeginObjectiveMutation();
        try
        {
            unit.MarkDead();
            runtime.HandleUnitDefeatedByRuntimeEffect(
                unit,
                null,
                batch,
                "",
                new BattleDefeatHandlingOptions(collectLoot: false)
            );
        }
        finally
        {
            runtime.EndObjectiveMutation(batch);
        }
    }

    private void AssertDecision(
        BattleFinalDecision decision,
        BattleOutcomeKind expectedOutcome,
        BattleEndReasonKind expectedReason,
        string context
    )
    {
        _test.True(decision != null, $"{context}应锁存终局决定。");
        if (decision == null)
            return;
        _test.Eq(decision.ObjectiveMode, BattleObjectiveMode.Control, context);
        _test.Eq(decision.Outcome, expectedOutcome, context);
        _test.Eq(decision.EndReason, expectedReason, context);
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
}
