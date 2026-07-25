using System;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_rescue_escort_objective_regression
    : LifecycleTestSceneTree
{
    private static readonly StringName RescueActorId = "rescue_target";
    private static readonly StringName EscortActorId = "escort_target";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestAdjacentPersistentPartyMemberCanRescueTarget();
            TestDistantRescueInteractionIsRejected();
            TestRescueTargetDefeatFailsObjective();
            TestPersistentPartyDefeatFailsRescue();
            TestEscortArrivalSucceedsWhileEnemiesSurvive();
            TestEscortTargetDefeatFailsObjective();
            TestPersistentPartyDefeatFailsEscort();
            TestFormalEscortEncounterStartsWithBattleOnlyScenarioActor();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unhandled rescue/escort objective regression exception: {exception}"
            );
        }
        RequestTestExit(_test.Finish("Battle rescue and escort objective regression"));
    }

    private void TestFormalEscortEncounterStartsWithBattleOnlyScenarioActor()
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
                gameSession.GetBattleEncounterDefinitions()["mist_hollow_escort"];
            var anchor = new EncounterAnchorData
            {
                entity_id = "formal_escort_start",
                display_name = "正式护送开战",
                world_coord = Vector2I.Zero,
                faction_id = "hostile",
                region_tag = "mistwood",
                encounter_profile_id = "mist_hollow_escort",
                growth_stage = 0,
            };
            state = runtime.StartBattle(
                anchor,
                240724,
                encounter.Objective,
                new GDictionary
                {
                    ["ally_member_ids"] = new GStringNameArray
                    {
                        "formal_escort_ally",
                    },
                    ["validate_spawn_reachability"] = false,
                }
            );

            _test.True(
                state != null && !state.IsEmpty(),
                "正式护送遭遇应成功完成开战装配。"
            );
            BattleUnitState scenarioActor = state
                ?.GetUnitsTyped()
                .SingleOrDefault(
                    unit => unit?.encounter_actor_id == (StringName)"refugee_guide"
                );
            _test.True(scenarioActor != null, "正式护送开战应生成目标场景 NPC。");
            _test.Eq(
                scenarioActor?.source_member_id.ToString() ?? "<missing>",
                "",
                "正式护送 NPC 不得带入队伍成员写回键。"
            );
            _test.True(
                scenarioActor != null
                    && state.GetAllyUnitIdsTyped().Contains(scenarioActor.unit_id),
                "正式护送 NPC 应进入友方战斗索引。"
            );
            _test.True(
                state?.ObjectiveRuntimeState
                    is BattleEscortObjectiveRuntimeState escortObjective
                    && escortObjective.TargetUnitId == scenarioActor?.unit_id,
                "正式护送目标应绑定生成后的场景 NPC unit id。"
            );
        }
        finally
        {
            runtime.SetupStateForTests(null);
            BattleTestFixture.DisposeBattleState(state);
            runtime.Dispose();
        }
    }

    private void TestAdjacentPersistentPartyMemberCanRescueTarget()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "rescue_interactor",
            Vector2I.Zero
        );
        BattleUnitState target = BuildScenarioActor(
            "rescue_captive",
            Vector2I.Right,
            RescueActorId
        );
        BattleUnitState enemy = BuildEnemy(
            "rescue_guard",
            new Vector2I(4, 0)
        );
        using BattleTestFixture fixture = CreateRescueBattle(
            "rescue_interaction",
            ally,
            target,
            enemy
        );
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Interact,
            unit_id = ally.unit_id,
            target_unit_id = target.unit_id,
            target_coord = target.GetAnchorCoord(),
        };

        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "相邻持久队员应能预览解救交互。");
        BattleHudObjectiveProgressSnapshot pendingHud = new(
            BattleObjectiveProgressSnapshot.Capture(fixture.State)
        );
        _test.Eq(pendingHud.Title, "拯救目标", "救援 HUD 应显示专用目标标题。");
        _test.False(
            pendingHud.TargetSecured,
            "交互前 HUD 不应把目标标为已获救。"
        );
        using BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.Eq(ally.GetCurrentAp(), 0, "解救交互应消耗 1 点行动力。");
        _test.True(batch.battle_ended, "成功解救应在同一原子命令结束战斗。");
        _test.True(enemy.IsAlive(), "解救成功不要求消灭守卫。");
        _test.True(
            BattleObjectiveProgressSnapshot.Capture(fixture.State).TargetSecured,
            "成功交互后 detached objective snapshot 应报告已获救。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleObjectiveMode.Rescue,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.RescueTargetSecured,
            "相邻交互解救"
        );
    }

    private void TestDistantRescueInteractionIsRejected()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "rescue_distant_ally",
            Vector2I.Zero
        );
        BattleUnitState target = BuildScenarioActor(
            "rescue_distant_target",
            new Vector2I(3, 0),
            RescueActorId
        );
        using BattleTestFixture fixture = CreateRescueBattle(
            "rescue_distant",
            ally,
            target,
            BuildEnemy("rescue_distant_enemy", new Vector2I(4, 0))
        );
        var command = new BattleCommand
        {
            CommandKind = BattleCommandKind.Interact,
            unit_id = ally.unit_id,
            target_unit_id = target.unit_id,
        };

        BattlePreview preview = fixture.Runtime.PreviewCommand(command);

        _test.False(preview?.allowed == true, "远距离不应允许解救交互。");
        _test.True(
            preview?.LogLinesTyped.Count > 0
                && preview.LogLinesTyped[0].Contains("相邻位置"),
            "远距离解救预览应给出相邻位置提示。"
        );
    }

    private void TestRescueTargetDefeatFailsObjective()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "rescue_target_death_ally",
            Vector2I.Zero
        );
        BattleUnitState target = BuildScenarioActor(
            "rescue_target_death_target",
            Vector2I.Right,
            RescueActorId
        );
        using BattleTestFixture fixture = CreateRescueBattle(
            "rescue_target_death",
            ally,
            target,
            BuildEnemy("rescue_target_death_enemy", new Vector2I(4, 0))
        );
        using BattleEventBatch batch = new();

        DefeatUnitAtomically(fixture.Runtime, target, batch);

        AssertDecision(
            fixture.State.FinalDecision,
            BattleObjectiveMode.Rescue,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.RescueTargetDefeated,
            "救援目标倒下"
        );
    }

    private void TestPersistentPartyDefeatFailsRescue()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "rescue_party_death_ally",
            Vector2I.Zero
        );
        using BattleTestFixture fixture = CreateRescueBattle(
            "rescue_party_death",
            ally,
            BuildScenarioActor(
                "rescue_party_death_target",
                new Vector2I(2, 0),
                RescueActorId
            ),
            BuildEnemy("rescue_party_death_enemy", new Vector2I(4, 0))
        );
        using BattleEventBatch batch = new();

        DefeatUnitAtomically(fixture.Runtime, ally, batch);

        AssertDecision(
            fixture.State.FinalDecision,
            BattleObjectiveMode.Rescue,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.RescuePartyDefeated,
            "救援队伍覆灭"
        );
    }

    private void TestEscortArrivalSucceedsWhileEnemiesSurvive()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "escort_success_ally",
            Vector2I.Zero
        );
        BattleUnitState escort = BuildScenarioActor(
            "escort_success_target",
            new Vector2I(4, 0),
            EscortActorId
        );
        BattleUnitState enemy = BuildEnemy(
            "escort_success_enemy",
            new Vector2I(2, 0)
        );
        using BattleTestFixture fixture = CreateEscortBattle(
            "escort_success",
            ally,
            escort,
            enemy
        );
        using BattleEventBatch batch = new();

        BattleOutcomeFlushResult result =
            fixture.Runtime.FlushBattleOutcomeEvaluation(batch);

        _test.Eq(
            result,
            BattleOutcomeFlushResult.Completed,
            "护送目标完整进入出口后应完成战斗。"
        );
        _test.True(enemy.IsAlive(), "护送成功不要求消灭敌军。");
        BattleHudObjectiveProgressSnapshot escortHud = new(
            BattleObjectiveProgressSnapshot.Capture(fixture.State)
        );
        _test.Eq(escortHud.Title, "护送目标", "护送 HUD 应显示专用目标标题。");
        _test.True(
            escortHud.TargetReachedExit,
            "护送 HUD 应报告目标已经抵达出口。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleObjectiveMode.Escort,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.EscortTargetReachedExit,
            "护送目标抵达"
        );
    }

    private void TestEscortTargetDefeatFailsObjective()
    {
        BattleUnitState escort = BuildScenarioActor(
            "escort_target_death_target",
            new Vector2I(2, 0),
            EscortActorId
        );
        using BattleTestFixture fixture = CreateEscortBattle(
            "escort_target_death",
            BuildPersistentAlly("escort_target_death_ally", Vector2I.Zero),
            escort,
            BuildEnemy("escort_target_death_enemy", new Vector2I(4, 0))
        );
        using BattleEventBatch batch = new();

        DefeatUnitAtomically(fixture.Runtime, escort, batch);

        AssertDecision(
            fixture.State.FinalDecision,
            BattleObjectiveMode.Escort,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.EscortTargetDefeated,
            "护送目标倒下"
        );
    }

    private void TestPersistentPartyDefeatFailsEscort()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "escort_party_death_ally",
            Vector2I.Zero
        );
        using BattleTestFixture fixture = CreateEscortBattle(
            "escort_party_death",
            ally,
            BuildScenarioActor(
                "escort_party_death_target",
                new Vector2I(2, 0),
                EscortActorId
            ),
            BuildEnemy("escort_party_death_enemy", new Vector2I(4, 0))
        );
        using BattleEventBatch batch = new();

        DefeatUnitAtomically(fixture.Runtime, ally, batch);

        AssertDecision(
            fixture.State.FinalDecision,
            BattleObjectiveMode.Escort,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.EscortPartyDefeated,
            "护送队伍覆灭"
        );
    }

    private BattleTestFixture CreateRescueBattle(
        StringName battleId,
        BattleUnitState ally,
        BattleUnitState target,
        BattleUnitState enemy
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(5, 1),
            new[] { ally, target },
            new[] { enemy }
        );
        _test.True(
            fixture.Runtime.InitializeBattleObjective(
                new BattleRescueObjectiveDefinition(RescueActorId)
            ),
            $"{battleId} 应成功初始化救援目标。"
        );
        return fixture;
    }

    private BattleTestFixture CreateEscortBattle(
        StringName battleId,
        BattleUnitState ally,
        BattleUnitState target,
        BattleUnitState enemy
    )
    {
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(5, 1),
            new[] { ally, target },
            new[] { enemy }
        );
        _test.True(
            fixture.Runtime.InitializeBattleObjective(
                new BattleEscortObjectiveDefinition(
                    EscortActorId,
                    "east_exit",
                    BattleMapEdge.Right,
                    1
                )
            ),
            $"{battleId} 应成功初始化护送目标。"
        );
        return fixture;
    }

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
        unit.ControlModeKind = BattleUnitControlMode.Manual;
        return unit;
    }

    private static BattleUnitState BuildScenarioActor(
        StringName unitId,
        Vector2I coord,
        StringName actorId
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            "player",
            coord,
            currentHp: 20
        );
        unit.source_member_id = "";
        unit.encounter_actor_id = actorId;
        unit.ControlModeKind = BattleUnitControlMode.Ai;
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
        BattleObjectiveMode expectedMode,
        BattleOutcomeKind expectedOutcome,
        BattleEndReasonKind expectedReason,
        string context
    )
    {
        _test.True(decision != null, $"{context}应锁存终局决定。");
        if (decision == null)
            return;
        _test.Eq(decision.ObjectiveMode, expectedMode, $"{context}模式不正确。");
        _test.Eq(decision.Outcome, expectedOutcome, $"{context}结果不正确。");
        _test.Eq(decision.EndReason, expectedReason, $"{context}原因不正确。");
    }
}
