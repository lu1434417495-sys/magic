using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_ai_objective_behavior_regression
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
            TestEscapeAiUsesPathThatBeginsWithAnEqualDistanceDetour();
            TestEscapeAiFallsBackWhenExitPathIsBlocked();
            TestEscortAiMovesTowardExitAndWaitsWhenBlocked();
            TestInterceptTargetMovesTowardExitAndWaitsWhenBlocked();
            TestInterceptPartyAiPrioritizesTarget();
            TestDefenseTargetHoldsPosition();
            TestDefenseHostileAiPrioritizesTarget();
            TestRescueTargetWaitsForInteraction();
            TestNodeOperationAiInteractsAndMovesTowardNodes();
            TestControlAiMovesTowardHostileZoneAndFightsWhenContested();
            TestBossAiFallsBackToUsableMinionWhenBossIsUnavailable();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unhandled battle AI objective regression exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Battle AI objective behavior regression"));
    }

    private void TestEscortAiMovesTowardExitAndWaitsWhenBlocked()
    {
        EnemyAiBrainDefinition brain = BuildObjectiveProbeBrain();
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleUnitState party = BuildPersistentAiAlly(
            "escort_ai_party",
            new Vector2I(0, 1),
            brain.BrainId
        );
        BattleUnitState escort = BuildManualUnit(
            "escort_ai_target",
            "player",
            new Vector2I(1, 1)
        );
        escort.ControlModeKind = BattleUnitControlMode.Ai;
        escort.encounter_actor_id = "escort_actor";
        escort.SetCurrentMovePoints(1);
        BattleUnitState enemy = BuildManualUnit(
            "escort_ai_enemy",
            "enemy",
            new Vector2I(3, 1)
        );
        BattleState state = BattleTestFixture.BuildFlatState(
            "escort_ai_move",
            new Vector2I(5, 3)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { party, escort },
            new[] { enemy }
        );
        runtime.SetupStateForTests(state);
        _test.True(
            runtime.InitializeBattleObjective(
                new BattleEscortObjectiveDefinition(
                    "escort_actor",
                    "east_exit",
                    BattleMapEdge.Right,
                    1
                )
            ),
            "护送 AI 场景应成功初始化。"
        );

        BattleAiDecision moveDecision = ChooseAiDecision(runtime, escort);

        _test.True(
            moveDecision?.command?.IsMove() == true,
            "护送目标应优先沿路线向出口移动。"
        );
        _test.Eq(
            moveDecision?.action_id ?? (StringName)"",
            (StringName)"objective_escort_move",
            "护送移动应由目标 evaluator 产出。"
        );

        state.GetCell(new Vector2I(2, 1))?.SetBaseTerrain("deep_water");
        state.GetCell(new Vector2I(1, 0))?.SetBaseTerrain("deep_water");
        state.GetCell(new Vector2I(1, 2))?.SetBaseTerrain("deep_water");
        BattleAiDecision waitDecision = ChooseAiDecision(runtime, escort);

        _test.True(
            waitDecision?.command?.IsWait() == true,
            "护送路线受阻时 NPC 应等待重寻路，而不是转入普通攻击。"
        );
        _test.Eq(
            waitDecision?.action_id ?? (StringName)"",
            (StringName)"objective_escort_wait",
            "护送阻路应返回专用等待决定。"
        );
    }

    private void TestRescueTargetWaitsForInteraction()
    {
        EnemyAiBrainDefinition brain = BuildObjectiveProbeBrain();
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleUnitState party = BuildPersistentAiAlly(
            "rescue_ai_party",
            Vector2I.Zero,
            brain.BrainId
        );
        BattleUnitState target = BuildManualUnit(
            "rescue_ai_target",
            "player",
            Vector2I.Right
        );
        target.ControlModeKind = BattleUnitControlMode.Ai;
        target.encounter_actor_id = "rescue_actor";
        BattleState state = BattleTestFixture.BuildFlatState(
            "rescue_ai_wait",
            new Vector2I(4, 1)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { party, target },
            new[]
            {
                BuildManualUnit(
                    "rescue_ai_enemy",
                    "enemy",
                    new Vector2I(3, 0)
                ),
            }
        );
        runtime.SetupStateForTests(state);
        _test.True(
            runtime.InitializeBattleObjective(
                new BattleRescueObjectiveDefinition("rescue_actor")
            ),
            "救援 AI 场景应成功初始化。"
        );

        BattleAiDecision decision = ChooseAiDecision(runtime, target);

        _test.True(
            decision?.command?.IsWait() == true,
            "未获救的场景目标应等待玩家交互。"
        );
        _test.Eq(
            decision?.action_id ?? (StringName)"",
            (StringName)"objective_rescue_wait",
            "救援目标等待应由专用 objective evaluator 产出。"
        );
    }

    private void TestDefenseTargetHoldsPosition()
    {
        EnemyAiBrainDefinition brain = BuildObjectiveProbeBrain();
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleUnitState party = BuildPersistentAiAlly(
            "defense_ai_party",
            Vector2I.Zero,
            brain.BrainId
        );
        BattleUnitState target = BuildManualUnit(
            "defense_ai_target",
            "player",
            Vector2I.Right
        );
        ConfigureProbeAiUnit(target, brain.BrainId);
        target.encounter_actor_id = "defense_actor";
        BattleUnitState enemy = BuildManualUnit(
            "defense_ai_enemy",
            "enemy",
            new Vector2I(2, 0)
        );
        BattleState state = BattleTestFixture.BuildFlatState(
            "defense_ai_hold",
            new Vector2I(3, 1)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { party, target },
            new[] { enemy }
        );
        runtime.SetupStateForTests(state);
        _test.True(
            runtime.InitializeBattleObjective(
                new BattleDefenseObjectiveDefinition("defense_actor", 100)
            ),
            "防守 AI 场景应成功初始化。"
        );

        BattleAiDecision decision = ChooseAiDecision(runtime, target);

        _test.True(
            decision?.command?.IsWait() == true,
            "防守目标应原地等待，由玩家承担保护责任。"
        );
        _test.Eq(
            decision?.action_id ?? (StringName)"",
            (StringName)"objective_defense_hold",
            "防守目标等待应由专用 objective evaluator 产出。"
        );
    }

    private void TestNodeOperationAiInteractsAndMovesTowardNodes()
    {
        EnemyAiBrainDefinition brain = BuildObjectiveProbeBrain();
        using BattleRuntimeScope adjacentScope =
            BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule adjacentRuntime = adjacentScope.Runtime;
        BattleUnitState adjacentParty = BuildPersistentAiAlly(
            "node_operation_ai_adjacent",
            Vector2I.Zero,
            brain.BrainId
        );
        BattleState adjacentState = BattleTestFixture.BuildFlatState(
            "node_operation_ai_interact",
            new Vector2I(5, 1)
        );
        BattleTestFixture.InstallUnits(
            adjacentState,
            new[] { adjacentParty },
            new[]
            {
                BuildManualUnit(
                    "node_operation_ai_adjacent_enemy",
                    "enemy",
                    new Vector2I(4, 0)
                ),
            }
        );
        adjacentRuntime.SetupStateForTests(adjacentState);
        _test.True(
            adjacentRuntime.InitializeBattleObjective(
                new BattleNodeOperationObjectiveDefinition(
                    new[]
                    {
                        new BattleOperationNodeDefinition(
                            "near_node",
                            "近端节点",
                            "near_zone",
                            BattleMapEdge.Left,
                            2
                        ),
                    }
                )
            ),
            "相邻节点 AI 场景应成功初始化。"
        );

        BattleAiDecision interactDecision = ChooseAiDecision(
            adjacentRuntime,
            adjacentParty
        );

        _test.True(
            interactDecision?.command?.IsInteract() == true,
            "持久队员与未完成节点相邻时应优先执行交互。"
        );
        _test.Eq(
            interactDecision?.action_id ?? (StringName)"",
            (StringName)"objective_node_operation_interact",
            "节点交互应由专用 objective evaluator 产出。"
        );

        using BattleRuntimeScope distantScope =
            BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule distantRuntime = distantScope.Runtime;
        BattleUnitState distantParty = BuildPersistentAiAlly(
            "node_operation_ai_distant",
            Vector2I.Zero,
            brain.BrainId
        );
        distantParty.SetCurrentMovePoints(2);
        BattleState distantState = BattleTestFixture.BuildFlatState(
            "node_operation_ai_move",
            new Vector2I(6, 1)
        );
        BattleTestFixture.InstallUnits(
            distantState,
            new[] { distantParty },
            new[]
            {
                BuildManualUnit(
                    "node_operation_ai_distant_enemy",
                    "enemy",
                    new Vector2I(5, 0)
                ),
            }
        );
        distantRuntime.SetupStateForTests(distantState);
        _test.True(
            distantRuntime.InitializeBattleObjective(
                new BattleNodeOperationObjectiveDefinition(
                    new[]
                    {
                        new BattleOperationNodeDefinition(
                            "far_node",
                            "远端节点",
                            "far_zone",
                            BattleMapEdge.Right,
                            2
                        ),
                    }
                )
            ),
            "远端节点 AI 场景应成功初始化。"
        );

        BattleAiDecision moveDecision = ChooseAiDecision(
            distantRuntime,
            distantParty
        );

        _test.True(
            moveDecision?.command?.IsMove() == true,
            "持久队员距节点较远时应向节点移动。"
        );
        _test.Eq(
            moveDecision?.action_id ?? (StringName)"",
            (StringName)"objective_node_operation_move",
            "节点移动应由专用 objective evaluator 产出。"
        );
    }

    private void TestControlAiMovesTowardHostileZoneAndFightsWhenContested()
    {
        EnemyAiBrainDefinition brain = BuildObjectiveProbeBrain();
        using BattleRuntimeScope moveScope = BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule moveRuntime = moveScope.Runtime;
        BattleUnitState movingActor = BuildPersistentAiAlly(
            "control_ai_move_actor",
            Vector2I.Zero,
            brain.BrainId
        );
        BattleUnitState remoteEnemy = BuildManualUnit(
            "control_ai_move_enemy",
            "enemy",
            new Vector2I(5, 0)
        );
        BattleState moveState = BattleTestFixture.BuildFlatState(
            "control_ai_move",
            new Vector2I(6, 1)
        );
        BattleTestFixture.InstallUnits(
            moveState,
            new[] { movingActor },
            new[] { remoteEnemy }
        );
        moveRuntime.SetupStateForTests(moveState);
        _test.True(
            moveRuntime.InitializeBattleObjective(
                BuildControlDefinition()
            ),
            "占领移动 AI 场景应成功初始化。"
        );

        BattleAiDecision moveDecision = ChooseAiDecision(
            moveRuntime,
            movingActor
        );

        _test.True(
            moveDecision?.command?.IsMove() == true,
            "AI 应优先向敌方占领区移动。"
        );
        _test.Eq(
            moveDecision?.action_id ?? (StringName)"",
            (StringName)"objective_control_move",
            "占领移动应由专用 objective evaluator 产出。"
        );

        using BattleRuntimeScope contestedScope =
            BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule contestedRuntime = contestedScope.Runtime;
        BattleUnitState contestedActor = BuildPersistentAiAlly(
            "control_ai_contested_actor",
            new Vector2I(4, 0),
            brain.BrainId
        );
        BattleUnitState adjacentEnemy = BuildManualUnit(
            "control_ai_contested_enemy",
            "enemy",
            new Vector2I(5, 0)
        );
        BattleState contestedState = BattleTestFixture.BuildFlatState(
            "control_ai_contested",
            new Vector2I(6, 1)
        );
        BattleTestFixture.InstallUnits(
            contestedState,
            new[] { contestedActor },
            new[] { adjacentEnemy }
        );
        contestedRuntime.SetupStateForTests(contestedState);
        _test.True(
            contestedRuntime.InitializeBattleObjective(
                BuildControlDefinition()
            ),
            "占领争夺 AI 场景应成功初始化。"
        );

        BattleAiDecision attackDecision = ChooseAiDecision(
            contestedRuntime,
            contestedActor
        );

        _test.True(
            attackDecision?.command?.IsSkill() == true,
            "AI 身处争夺区时应回落到常规战斗行为。"
        );
        _test.Eq(
            attackDecision?.action_id ?? (StringName)"",
            (StringName)"objective_probe_basic_attack",
            "争夺区内应由正式基础攻击 action 处理相邻敌人。"
        );
        _test.Eq(
            attackDecision?.command?.target_unit_id ?? (StringName)"",
            adjacentEnemy.unit_id,
            "争夺区内的常规攻击应选择相邻敌人。"
        );
    }

    private void TestDefenseHostileAiPrioritizesTarget()
    {
        EnemyAiBrainDefinition brain = BuildObjectiveProbeBrain();
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleUnitState party = BuildManualUnit(
            "defense_priority_party",
            "player",
            new Vector2I(1, 0)
        );
        party.source_member_id = "defense_priority_member";
        BattleUnitState target = BuildManualUnit(
            "defense_priority_target",
            "player",
            new Vector2I(2, 1)
        );
        target.encounter_actor_id = "defense_priority_actor";
        BattleUnitState hostile = BuildManualUnit(
            "defense_priority_hostile",
            "enemy",
            new Vector2I(1, 1)
        );
        ConfigureProbeAiUnit(hostile, brain.BrainId);
        BattleState state = BattleTestFixture.BuildFlatState(
            "defense_ai_priority",
            new Vector2I(3, 2)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { party, target },
            new[] { hostile }
        );
        runtime.SetupStateForTests(state);
        _test.True(
            runtime.InitializeBattleObjective(
                new BattleDefenseObjectiveDefinition(
                    "defense_priority_actor",
                    100
                )
            ),
            "防守目标优先级场景应成功初始化。"
        );

        BattleAiDecision decision = ChooseAiDecision(runtime, hostile);

        _test.True(
            decision?.command?.IsSkill() == true,
            "敌人应能对相邻防守目标使用基础攻击。"
        );
        _test.Eq(
            decision?.command?.target_unit_id ?? (StringName)"",
            target.unit_id,
            "两个相邻玩家单位均可攻击时，敌人应优先选择防守目标。"
        );
    }

    private void TestInterceptTargetMovesTowardExitAndWaitsWhenBlocked()
    {
        EnemyAiBrainDefinition brain = BuildObjectiveProbeBrain();
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleUnitState party = BuildPersistentAiAlly(
            "intercept_ai_party",
            new Vector2I(4, 1),
            brain.BrainId
        );
        BattleUnitState target = BuildManualUnit(
            "intercept_ai_target",
            "enemy",
            new Vector2I(3, 1)
        );
        target.ControlModeKind = BattleUnitControlMode.Ai;
        target.ai_brain_id = brain.BrainId;
        target.encounter_actor_id = "intercept_actor";
        target.SetCurrentMovePoints(1);
        BattleState state = BattleTestFixture.BuildFlatState(
            "intercept_ai_move",
            new Vector2I(5, 3)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { party },
            new[] { target }
        );
        runtime.SetupStateForTests(state);
        _test.True(
            runtime.InitializeBattleObjective(
                new BattleInterceptObjectiveDefinition(
                    "intercept_actor",
                    "west_exit",
                    BattleMapEdge.Left,
                    1
                )
            ),
            "截击 AI 场景应成功初始化。"
        );

        BattleAiDecision moveDecision = ChooseAiDecision(runtime, target);

        _test.True(
            moveDecision?.command?.IsMove() == true,
            "截击目标应优先向逃脱区移动。"
        );
        _test.Eq(
            moveDecision?.action_id ?? (StringName)"",
            (StringName)"objective_intercept_move",
            "截击目标移动应由专用 objective evaluator 产出。"
        );

        state.GetCell(new Vector2I(2, 1))?.SetBaseTerrain("deep_water");
        state.GetCell(new Vector2I(3, 0))?.SetBaseTerrain("deep_water");
        state.GetCell(new Vector2I(3, 2))?.SetBaseTerrain("deep_water");
        BattleAiDecision waitDecision = ChooseAiDecision(runtime, target);

        _test.True(
            waitDecision?.command?.IsWait() == true,
            "逃脱路线受阻时截击目标应等待重寻路。"
        );
        _test.Eq(
            waitDecision?.action_id ?? (StringName)"",
            (StringName)"objective_intercept_wait",
            "截击阻路应返回专用等待决定。"
        );
    }

    private void TestInterceptPartyAiPrioritizesTarget()
    {
        EnemyAiBrainDefinition brain = BuildObjectiveProbeBrain();
        using BattleRuntimeScope runtimeScope = BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleUnitState party = BuildPersistentAiAlly(
            "intercept_priority_party",
            Vector2I.Zero,
            brain.BrainId
        );
        BattleUnitState guard = BuildManualUnit(
            "intercept_priority_guard",
            "enemy",
            Vector2I.Right
        );
        BattleUnitState target = BuildManualUnit(
            "intercept_priority_target",
            "enemy",
            Vector2I.Down
        );
        target.encounter_actor_id = "intercept_priority_actor";
        BattleState state = BattleTestFixture.BuildFlatState(
            "intercept_ai_priority",
            new Vector2I(3, 2)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { party },
            new[] { guard, target }
        );
        runtime.SetupStateForTests(state);
        _test.True(
            runtime.InitializeBattleObjective(
                new BattleInterceptObjectiveDefinition(
                    "intercept_priority_actor",
                    "east_exit",
                    BattleMapEdge.Right,
                    1
                )
            ),
            "截击目标优先级场景应成功初始化。"
        );

        BattleAiDecision decision = ChooseAiDecision(runtime, party);

        _test.True(
            decision?.command?.IsSkill() == true,
            "持久队员应能对相邻截击目标使用基础攻击。"
        );
        _test.Eq(
            decision?.command?.target_unit_id ?? (StringName)"",
            target.unit_id,
            "多个相邻敌人均可攻击时，AI 应优先选择截击目标。"
        );
    }

    private void TestEscapeAiUsesPathThatBeginsWithAnEqualDistanceDetour()
    {
        EnemyAiBrainDefinition brain = BuildObjectiveProbeBrain();
        using BattleRuntimeScope runtimeScope =
            BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleUnitState actor = BuildPersistentAiAlly(
            "escape_ai_detour_actor",
            new Vector2I(0, 1),
            brain.BrainId
        );
        actor.SetCurrentMovePoints(1);
        BattleUnitState enemy = BuildManualUnit(
            "escape_ai_detour_enemy",
            "enemy",
            new Vector2I(3, 1)
        );
        BattleState state = BattleTestFixture.BuildFlatState(
            "escape_ai_detour",
            new Vector2I(5, 3)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { actor },
            new[] { enemy }
        );
        state.GetCell(new Vector2I(1, 1))?.SetBaseTerrain("deep_water");
        runtime.SetupStateForTests(state);
        _test.True(
            runtime.InitializeBattleObjective(
                new BattleEscapeObjectiveDefinition(
                    "east_exit",
                    BattleMapEdge.Right,
                    1
                )
            ),
            "绕路场景应成功初始化逃离目标。"
        );

        BattleAiDecision decision = ChooseAiDecision(runtime, actor);

        _test.True(decision?.command?.IsMove() == true, "逃离 AI 应选择绕路移动，而不是等待。");
        _test.Eq(
            decision?.action_id ?? (StringName)"",
            (StringName)"objective_escape_move",
            "真实决策引擎应由逃离目标 evaluator 产出移动，而不是落入常规 brain。"
        );
        _test.Eq(
            decision?.command?.target_coord ?? new Vector2I(-1, -1),
            new Vector2I(0, 0),
            "直线路径受阻且本回合只有一步时，AI 应采用不缩短边缘距离的合法第一步。"
        );
    }

    private void TestEscapeAiFallsBackWhenExitPathIsBlocked()
    {
        EnemyAiBrainDefinition brain = BuildObjectiveProbeBrain();
        using BattleRuntimeScope runtimeScope =
            BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleUnitState actor = BuildPersistentAiAlly(
            "escape_ai_blocked_actor",
            new Vector2I(0, 0),
            brain.BrainId
        );
        BattleUnitState blocker = BuildManualUnit(
            "escape_ai_exit_blocker",
            "enemy",
            new Vector2I(1, 0)
        );
        BattleState state = BattleTestFixture.BuildFlatState(
            "escape_ai_blocked_exit",
            new Vector2I(2, 1)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { actor },
            new[] { blocker }
        );
        runtime.SetupStateForTests(state);
        _test.True(
            runtime.InitializeBattleObjective(
                new BattleEscapeObjectiveDefinition(
                    "east_exit",
                    BattleMapEdge.Right,
                    1
                )
            ),
            "出口被敌人暂时占据不应让目标初始化失败。"
        );

        BattleAiDecision decision = ChooseAiDecision(runtime, actor);

        _test.True(
            decision?.command?.IsSkill() == true,
            "出口无路时真实决策引擎应继续执行常规攻击，而不是返回空决策或等待。"
        );
        _test.Eq(
            decision?.action_id ?? (StringName)"",
            (StringName)"objective_probe_basic_attack",
            "出口无路时应落入正式 brain 的基础攻击 action。"
        );
        _test.Eq(
            decision?.command?.skill_id ?? (StringName)"",
            (StringName)"basic_attack",
            "出口阻挡者应由常规基础攻击处理。"
        );
        _test.Eq(
            decision?.command?.target_unit_id ?? (StringName)"",
            blocker.unit_id,
            "常规攻击应明确选择占据出口的敌人。"
        );
    }

    private void TestBossAiFallsBackToUsableMinionWhenBossIsUnavailable()
    {
        EnemyAiBrainDefinition brain = BuildObjectiveProbeBrain();
        using BattleRuntimeScope runtimeScope =
            BuildRuntimeWithEnemyContent(brain);
        BattleRuntimeModule runtime = runtimeScope.Runtime;
        BattleUnitState actor = BuildPersistentAiAlly(
            "boss_ai_actor",
            new Vector2I(0, 0),
            brain.BrainId
        );
        BattleUnitState minion = BuildManualUnit(
            "boss_ai_minion",
            "enemy",
            new Vector2I(1, 0)
        );
        BattleUnitState boss = BuildManualUnit(
            "boss_ai_target",
            "enemy",
            new Vector2I(4, 0)
        );
        boss.encounter_actor_id = "boss_actor";
        BattleState state = BattleTestFixture.BuildFlatState(
            "boss_ai_priority",
            new Vector2I(5, 1)
        );
        BattleTestFixture.InstallUnits(
            state,
            new[] { actor },
            new[] { minion, boss }
        );
        runtime.SetupStateForTests(state);
        _test.True(
            runtime.InitializeBattleObjective(
                new BattleBossObjectiveDefinition("boss_actor")
            ),
            "首领 AI 场景应成功初始化目标。"
        );

        BattleAiDecision decision = ChooseAiDecision(runtime, actor);

        _test.True(
            decision?.command?.IsSkill() == true,
            "首领超出基础攻击范围时，真实决策引擎仍应找到可用的杂兵攻击。"
        );
        _test.Eq(
            decision?.action_id ?? (StringName)"",
            (StringName)"objective_probe_basic_attack",
            "首领不可用时仍应由正式基础攻击 action 产出决定。"
        );
        _test.Eq(
            decision?.command?.target_unit_id ?? (StringName)"",
            minion.unit_id,
            "首领候选预览失败后，应继续尝试并选择相邻的可用杂兵。"
        );
    }

    private static BattleAiDecision ChooseAiDecision(
        BattleRuntimeModule runtime,
        BattleUnitState actor
    )
    {
        BattleAiContext context = BuildAiContext(runtime, actor);
        return runtime._ai_service
            .ChooseCommand(context, captureTrace: false)
            ?.Decision;
    }

    private static BattleAiContext BuildAiContext(
        BattleRuntimeModule runtime,
        BattleUnitState actor
    )
    {
        runtime._ensure_ai_action_plan_for_unit(actor);
        runtime.TryGetAiActionPlanForUnit(
            actor.unit_id,
            out BattleAiRuntimeActionPlan actionPlan
        );
        var context = new BattleAiContext
        {
            state = runtime._state,
            unit_state = actor,
            grid_service = runtime._grid_service,
            move_cost_callback = (unit, targetCoord) =>
                runtime._get_ai_move_query_cost(
                    unit.unit_id,
                    unit.GetAnchorCoord(),
                    targetCoord
                ),
            runtime_action_plan = actionPlan,
        };
        context.SetSkillDefinitions(runtime.GetSkillDefinitionIndexTyped());
        runtime._bind_ai_helper_services_for_decision(actor, context);
        return context;
    }

    private static EnemyAiBrainDefinition BuildObjectiveProbeBrain()
    {
        var basicAttack = new UseUnitSkillActionDefinition(
            actionId: "objective_probe_basic_attack",
            scoreBucketId: "",
            actionIntent: BattleAiActionIntent.Offense,
            skillIds: new[] { (StringName)"basic_attack" },
            targetSelector: "nearest_enemy",
            minimumEffectiveTargetCount: 1,
            maximumFriendlyFireTargetCount: 0,
            allowFriendlyLethal: false,
            desiredMinDistance: 1,
            desiredMaxDistance: 1,
            distanceReference: EnemyAiDistanceReferences.ToStringName(
                EnemyAiDistanceReference.TargetUnit
            )
        );
        var engageState = new EnemyAiStateDefinition(
            "engage",
            new EnemyAiActionDefinition[] { basicAttack },
            Array.Empty<EnemyAiGenerationSlotDefinition>()
        );
        return new EnemyAiBrainDefinition(
            "objective_probe_brain",
            "engage",
            BattleAiScoreProfileDefinition.Default,
            new[] { engageState },
            Array.Empty<EnemyAiTransitionRuleDefinition>()
        );
    }

    private static BattleControlObjectiveDefinition BuildControlDefinition() =>
        new(
            new[]
            {
                new BattleControlZoneDefinition(
                    "east_control_zone",
                    "东侧占领区",
                    BattleMapEdge.Right,
                    2
                ),
            },
            100
        );

    private static BattleRuntimeScope BuildRuntimeWithEnemyContent(
        params EnemyAiBrainDefinition[] extraBrains
    )
    {
        var gameSession = GameSessionTestFactory.CreateBorrowingProcessSnapshot();
        var runtime = new BattleRuntimeModule();
        var enemyAiBrains = new Dictionary<StringName, EnemyAiBrainDefinition>(
            gameSession.GetEnemyAiBrainDefinitions()
        );
        foreach (
            EnemyAiBrainDefinition brain in extraBrains
                ?? Array.Empty<EnemyAiBrainDefinition>()
        )
        {
            if (brain != null && brain.BrainId != (StringName)"")
                enemyAiBrains[brain.BrainId] = brain;
        }
        runtime.setup(
            null,
            gameSession.GetSkillDefinitionsTyped(),
            gameSession.GetEnemyTemplateDefinitions(),
            enemyAiBrains,
            null
        );
        runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        var damageResolver = new FixedSuccessOneDamageResolver();
        damageResolver.SetSkillDefinitions(
            runtime.GetSkillDefinitionIndexTyped()
        );
        runtime.ConfigureDamageResolverForTests(damageResolver);
        return new BattleRuntimeScope(runtime, gameSession);
    }

    private static BattleUnitState BuildPersistentAiAlly(
        StringName unitId,
        Vector2I coord,
        StringName brainId
    )
    {
        BattleUnitState unit = BuildUnit(
            unitId,
            "player",
            coord,
            controlMode: "ai"
        );
        unit.source_member_id = $"{unitId}_member";
        ConfigureProbeAiUnit(unit, brainId);
        return unit;
    }

    private static void ConfigureProbeAiUnit(
        BattleUnitState unit,
        StringName brainId
    )
    {
        unit.ai_brain_id = brainId;
        unit.ControlModeKind = BattleUnitControlMode.Ai;
        unit.ai_state_id = "engage";
        unit.AddKnownActiveSkill("basic_attack");
        unit.SetKnownSkillLevelTyped("basic_attack", 1);
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = BattleUnitState.ToStringName(
                    BattleWeaponProfileKind.Equipped
                ),
                weapon_item_id = "objective_probe_weapon",
                weapon_profile_type_id = "objective_probe_weapon",
                weapon_family = "test",
                weapon_current_grip = BattleUnitState.ToStringName(
                    BattleWeaponGripKind.OneHanded
                ),
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                },
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }

    private static BattleUnitState BuildManualUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord
    ) => BuildUnit(unitId, factionId, coord, controlMode: "manual");

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        StringName controlMode
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = controlMode,
        }.WithCombatResourcesForTest(
            hp: 30,
            mp: 120,
            stamina: 8,
            ap: 2,
            movePoints: 2,
            isAlive: true
        );
        unit.SetAnchorCoord(coord);
        unit.UnlockCombatResource(
            CombatResourceIds.ToStringName(CombatResourceIdKind.Mp)
        );
        unit.UnlockCombatResource(
            CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina)
        );
        foreach (
            StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped()
        )
        {
            unit.attribute_snapshot.SetValue(attributeId, 10);
        }
        unit.attribute_snapshot.SetValue("hp_max", 30);
        unit.attribute_snapshot.SetValue("mp_max", 120);
        unit.attribute_snapshot.SetValue("stamina_max", 8);
        unit.attribute_snapshot.SetValue("action_points", 2);
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.AttackBonus),
            12
        );
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ArmorClass),
            10
        );
        return unit;
    }

    private sealed class BattleRuntimeScope : IDisposable
    {
        private readonly GameSession _gameSession;

        internal BattleRuntimeScope(
            BattleRuntimeModule runtime,
            GameSession gameSession
        )
        {
            Runtime = runtime;
            _gameSession = gameSession;
        }

        internal BattleRuntimeModule Runtime { get; }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(
                Runtime,
                Runtime?._state
            );
            _gameSession?.Dispose();
        }
    }
}
