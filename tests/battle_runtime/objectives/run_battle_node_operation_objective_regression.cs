using System;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_node_operation_objective_regression
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
            TestDefinitionRejectsEmptyAndDuplicateNodes();
            TestInitializationFreezesUniqueUnoccupiedNodeCoords();
            TestAdjacentPartyCompletesNodesAndSpendsAp();
            TestDistantAndCompletedNodeInteractionsAreRejected();
            TestPartyDefeatBeforeCompletionFails();
            TestEnemyDefeatDoesNotCompleteObjective();
            TestAtomicLastNodeCompletionWinsOverPartyDefeat();
            TestFormalNodeOperationEncounterStartsWithBoundNodes();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unhandled battle node operation objective regression exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Battle node operation objective regression"));
    }

    private void TestDefinitionRejectsEmptyAndDuplicateNodes()
    {
        _test.True(
            Throws<ArgumentException>(
                () =>
                    _ = new BattleNodeOperationObjectiveDefinition(
                        Array.Empty<BattleOperationNodeDefinition>()
                    )
            ),
            "节点作业定义必须拒绝空节点集合。"
        );
        _test.True(
            Throws<ArgumentException>(
                () =>
                    _ = new BattleNodeOperationObjectiveDefinition(
                        new[]
                        {
                            BuildNodeDefinition(
                                "duplicate_node",
                                BattleMapEdge.Left,
                                2
                            ),
                            BuildNodeDefinition(
                                "duplicate_node",
                                BattleMapEdge.Right,
                                2
                            ),
                        }
                    )
            ),
            "节点作业定义必须拒绝重复 node_id。"
        );
        _test.True(
            Throws<ArgumentException>(
                () =>
                    _ = new BattleOperationNodeDefinition(
                        "",
                        "无效节点",
                        "invalid_zone",
                        BattleMapEdge.Left,
                        1
                    )
            ),
            "节点定义必须拒绝空 node_id。"
        );
    }

    private void TestInitializationFreezesUniqueUnoccupiedNodeCoords()
    {
        using BattleTestFixture fixture = CreateNodeBattle(
            "node_operation_binding"
        );
        var objective = fixture.State.ObjectiveRuntimeState
            as BattleNodeOperationObjectiveRuntimeState;

        _test.True(objective != null, "节点作业应生成独立运行时状态。");
        _test.Eq(
            objective?.OperationNodes.Count ?? 0,
            2,
            "运行时必须冻结全部声明节点。"
        );
        _test.Eq(
            objective?.OperationNodes.Select(node => node.Coord).Distinct().Count()
                ?? 0,
            2,
            "多个节点必须绑定唯一坐标。"
        );
        _test.True(
            objective != null
                && objective.OperationNodes.All(
                    node =>
                        fixture.State.GetCell(node.Coord)?.occupant_unit_id == ""
                ),
            "初始化节点不得覆盖已占用战斗格。"
        );
        BattleObjectiveProgressSnapshot progress =
            BattleObjectiveProgressSnapshot.Capture(fixture.State);
        _test.Eq(progress.OperationNodeCount, 2, "进度快照应投影节点总数。");
        _test.Eq(
            progress.CompletedOperationNodeCount,
            0,
            "初始节点均应为未完成。"
        );
        BattleHudObjectiveProgressSnapshot hud = new(progress);
        _test.Eq(hud.Title, "节点作业", "HUD 应显示节点作业专用标题。");
    }

    private void TestAdjacentPartyCompletesNodesAndSpendsAp()
    {
        using BattleTestFixture fixture = CreateNodeBattle(
            "node_operation_interaction"
        );
        BattleUnitState ally = fixture.Allies[0];
        var objective = (BattleNodeOperationObjectiveRuntimeState)
            fixture.State.ObjectiveRuntimeState;
        BattleOperationNodeRuntimeState[] nodes = objective.OperationNodes
            .OrderBy(node => node.Coord.X)
            .ToArray();
        BattleOperationNodeRuntimeState first = nodes[0];
        BattleOperationNodeRuntimeState second = nodes[1];

        using BattleEventBatch firstBatch = fixture.Runtime.IssueCommand(
            BuildInteractCommand(ally, first.Coord)
        );

        _test.True(first.IsCompleted, "相邻交互应完成目标节点。");
        _test.Eq(ally.GetCurrentAp(), 0, "完成节点应消耗 1 AP。");
        _test.False(firstBatch.battle_ended, "仍有节点未完成时战斗不得结束。");

        ally.SetCurrentAp(1);
        ally.SetAnchorCoord(second.Coord + Vector2I.Left);
        fixture.State.active_unit_id = ally.unit_id;
        fixture.State.PhaseKind = BattlePhaseKind.UnitActing;
        using BattleEventBatch secondBatch = fixture.Runtime.IssueCommand(
            BuildInteractCommand(ally, second.Coord)
        );

        _test.True(secondBatch.battle_ended, "完成最后一个节点应立即结束战斗。");
        _test.True(fixture.Enemies[0].IsAlive(), "节点作业成功不要求歼灭敌军。");
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.NodeOperationAllNodesCompleted,
            "完成全部节点"
        );
    }

    private void TestDistantAndCompletedNodeInteractionsAreRejected()
    {
        using BattleTestFixture fixture = CreateNodeBattle(
            "node_operation_rejection"
        );
        BattleUnitState ally = fixture.Allies[0];
        var objective = (BattleNodeOperationObjectiveRuntimeState)
            fixture.State.ObjectiveRuntimeState;
        BattleOperationNodeRuntimeState[] nodes = objective.OperationNodes
            .OrderBy(node => node.Coord.X)
            .ToArray();
        BattleOperationNodeRuntimeState first = nodes[0];
        BattleOperationNodeRuntimeState second = nodes[1];

        BattlePreview distantPreview = fixture.Runtime.PreviewCommand(
            BuildInteractCommand(ally, second.Coord)
        );
        _test.False(
            distantPreview?.allowed == true,
            "距离超过一格时不得执行节点作业。"
        );

        using BattleEventBatch firstBatch = fixture.Runtime.IssueCommand(
            BuildInteractCommand(ally, first.Coord)
        );
        ally.SetCurrentAp(1);
        BattlePreview repeatedPreview = fixture.Runtime.PreviewCommand(
            BuildInteractCommand(ally, first.Coord)
        );
        _test.False(
            repeatedPreview?.allowed == true,
            "已完成节点不得重复操作。"
        );
        _test.Eq(ally.GetCurrentAp(), 1, "拒绝重复操作不得额外扣除 AP。");
    }

    private void TestPartyDefeatBeforeCompletionFails()
    {
        using BattleTestFixture fixture = CreateNodeBattle(
            "node_operation_party_defeat"
        );
        using BattleEventBatch batch = new();

        DefeatUnitAtomically(fixture.Runtime, fixture.Allies[0], batch);

        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.NodeOperationPartyDefeated,
            "节点未完成时队伍覆灭"
        );
    }

    private void TestEnemyDefeatDoesNotCompleteObjective()
    {
        using BattleTestFixture fixture = CreateNodeBattle(
            "node_operation_enemy_defeat"
        );
        using BattleEventBatch batch = new();

        DefeatUnitAtomically(fixture.Runtime, fixture.Enemies[0], batch);

        _test.True(
            fixture.State.FinalDecision == null,
            "敌军全灭不得替代节点作业完成条件。"
        );
        _test.True(
            fixture.State.PhaseKind != BattlePhaseKind.BattleEnded,
            "敌军全灭后节点作业战仍应继续。"
        );
    }

    private void TestAtomicLastNodeCompletionWinsOverPartyDefeat()
    {
        using BattleTestFixture fixture = CreateNodeBattle(
            "node_operation_atomic_success"
        );
        var objective = (BattleNodeOperationObjectiveRuntimeState)
            fixture.State.ObjectiveRuntimeState;
        foreach (BattleOperationNodeRuntimeState node in objective.OperationNodes)
            objective.TryCompleteNode(node.NodeId);
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult result;
        try
        {
            fixture.Allies[0].MarkDead();
            fixture.Runtime.HandleUnitDefeatedByRuntimeEffect(
                fixture.Allies[0],
                null,
                batch,
                "",
                new BattleDefeatHandlingOptions(collectLoot: false)
            );
            fixture.Runtime.MarkObjectiveEvaluationDirty();
        }
        finally
        {
            result = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            result,
            BattleOutcomeFlushResult.Completed,
            "最后节点完成与队伍覆灭同批发生时应完成结算。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.NodeOperationAllNodesCompleted,
            "最后节点与队伍覆灭同批"
        );
    }

    private void TestFormalNodeOperationEncounterStartsWithBoundNodes()
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
                gameSession.GetBattleEncounterDefinitions()[
                    "mist_hollow_node_operation"
                ];
            var anchor = new EncounterAnchorData
            {
                entity_id = "formal_node_operation_start",
                display_name = "正式节点作业开战",
                world_coord = Vector2I.Zero,
                faction_id = "hostile",
                region_tag = "mistwood",
                encounter_profile_id = "mist_hollow_node_operation",
                growth_stage = 0,
            };
            state = runtime.StartBattle(
                anchor,
                240727,
                encounter.Objective,
                new GDictionary
                {
                    ["ally_member_ids"] = new GStringNameArray
                    {
                        "formal_node_operation_ally",
                    },
                    ["validate_spawn_reachability"] = false,
                }
            );

            _test.True(
                state != null && !state.IsEmpty(),
                "正式节点作业遭遇应成功完成开战装配。"
            );
            _test.True(
                state?.ObjectiveRuntimeState
                    is BattleNodeOperationObjectiveRuntimeState objective
                    && objective.OperationNodes.Count == 2
                    && objective.OperationNodes.All(node => !node.IsCompleted),
                "正式节点作业应冻结两个未完成节点。"
            );
        }
        finally
        {
            runtime.SetupStateForTests(null);
            BattleTestFixture.DisposeBattleState(state);
            runtime.Dispose();
        }
    }

    private BattleTestFixture CreateNodeBattle(StringName battleId)
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            $"{battleId}_ally",
            "player",
            Vector2I.Zero,
            currentAp: 1,
            currentHp: 20
        );
        ally.source_member_id = $"{battleId}_member";
        BattleUnitState enemy = BattleTestFixture.BuildUnit(
            $"{battleId}_enemy",
            "enemy",
            new Vector2I(4, 0),
            currentHp: 20
        );
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(5, 1),
            new[] { ally },
            new[] { enemy }
        );
        _test.True(
            fixture.Runtime.InitializeBattleObjective(BuildDefinition()),
            $"{battleId} 应成功初始化节点作业目标。"
        );
        return fixture;
    }

    private static BattleNodeOperationObjectiveDefinition BuildDefinition() =>
        new(
            new[]
            {
                BuildNodeDefinition(
                    "west_operation_node",
                    BattleMapEdge.Left,
                    2
                ),
                BuildNodeDefinition(
                    "east_operation_node",
                    BattleMapEdge.Right,
                    2
                ),
            }
        );

    private static BattleOperationNodeDefinition BuildNodeDefinition(
        StringName nodeId,
        BattleMapEdge edge,
        int depth
    ) =>
        new(nodeId, nodeId.ToString(), $"{nodeId}_zone", edge, depth);

    private static BattleCommand BuildInteractCommand(
        BattleUnitState unit,
        Vector2I coord
    ) =>
        new()
        {
            CommandKind = BattleCommandKind.Interact,
            unit_id = unit.unit_id,
            target_coord = coord,
        };

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
        _test.Eq(decision.ObjectiveMode, BattleObjectiveMode.NodeOperation, context);
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
