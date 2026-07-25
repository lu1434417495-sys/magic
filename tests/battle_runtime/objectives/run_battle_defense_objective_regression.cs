using System;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_defense_objective_regression
    : LifecycleTestSceneTree
{
    private static readonly StringName TargetActorId = "defense_target";
    private const int DurationTu = 10;
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestTargetSurvivesUntilDeadline();
            TestTargetDefeatBeforeDeadlineFails();
            TestPersistentPartyDefeatBeforeDeadlineFails();
            TestAtomicTargetDefeatAtDeadlineFails();
            TestAtomicPartyDefeatAtDeadlineSucceeds();
            TestHostileEliminationBeforeDeadlineDoesNotComplete();
            TestDefinitionRejectsInvalidAuthoringValues();
            TestMissingAndDuplicateTargetBindingsAreRejected();
            TestFormalDefenseEncounterStartsWithBattleOnlyScenarioActor();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unhandled battle defense objective regression exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Battle defense objective regression"));
    }

    private void TestTargetSurvivesUntilDeadline()
    {
        using BattleTestFixture fixture = CreateDefenseBattle(
            "defense_deadline_success",
            startTu: 40
        );

        using BattleEventBatch pendingBatch = fixture.Runtime.advance(1);
        _test.False(
            pendingBatch.battle_ended,
            "截止 TU 前防守目标仍存活时不应提前结束战斗。"
        );
        _test.True(
            fixture.State.FinalDecision == null,
            "截止 TU 前不应锁存防守终局。"
        );

        using BattleEventBatch successBatch = fixture.Runtime.advance(1);

        _test.True(successBatch.battle_ended, "到达截止 TU 后应完成防守战斗。");
        _test.True(
            fixture.Enemies[0].IsAlive(),
            "防守成功不要求消灭敌军。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.DefenseDeadlineReached,
            "防守到时"
        );
    }

    private void TestTargetDefeatBeforeDeadlineFails()
    {
        using BattleTestFixture fixture = CreateDefenseBattle(
            "defense_target_defeated"
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult result;
        try
        {
            DefeatUnit(fixture.Runtime, fixture.Allies[1], batch);
        }
        finally
        {
            result = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            result,
            BattleOutcomeFlushResult.Completed,
            "防守目标在截止前倒下应立即完成失败结算。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.DefenseTargetDefeated,
            "防守目标倒下"
        );
    }

    private void TestPersistentPartyDefeatBeforeDeadlineFails()
    {
        using BattleTestFixture fixture = CreateDefenseBattle(
            "defense_party_defeated"
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult result;
        try
        {
            DefeatUnit(fixture.Runtime, fixture.Allies[0], batch);
        }
        finally
        {
            result = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            result,
            BattleOutcomeFlushResult.Completed,
            "初始持久队伍在截止前覆灭应立即完成失败结算。"
        );
        _test.True(
            fixture.Allies[1].IsAlive(),
            "队伍覆灭失败路径中防守目标应仍然存活。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.DefensePartyDefeated,
            "防守队伍覆灭"
        );
    }

    private void TestAtomicTargetDefeatAtDeadlineFails()
    {
        using BattleTestFixture fixture = CreateDefenseBattle(
            "defense_atomic_target_deadline",
            startTu: 20
        );
        var objective = (BattleDefenseObjectiveRuntimeState)
            fixture.State.ObjectiveRuntimeState;
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult result;
        try
        {
            fixture.State.timeline.current_tu = objective.DeadlineTu;
            DefeatUnit(fixture.Runtime, fixture.Allies[1], batch);
            using BattleEventBatch prematureBatch = new();
            _test.Eq(
                fixture.Runtime.FlushBattleOutcomeEvaluation(prematureBatch),
                BattleOutcomeFlushResult.NoChange,
                "同一原子变更内不得在目标死亡前提前锁存守时成功。"
            );
        }
        finally
        {
            result = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            result,
            BattleOutcomeFlushResult.Completed,
            "目标死亡与到时同批发生时应完成防守结算。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.DefenseTargetDefeated,
            "目标死亡与防守到时同批"
        );
    }

    private void TestAtomicPartyDefeatAtDeadlineSucceeds()
    {
        using BattleTestFixture fixture = CreateDefenseBattle(
            "defense_atomic_party_deadline",
            startTu: 20
        );
        var objective = (BattleDefenseObjectiveRuntimeState)
            fixture.State.ObjectiveRuntimeState;
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult result;
        try
        {
            fixture.State.timeline.current_tu = objective.DeadlineTu;
            DefeatUnit(fixture.Runtime, fixture.Allies[0], batch);
        }
        finally
        {
            result = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            result,
            BattleOutcomeFlushResult.Completed,
            "队伍覆灭与到时同批发生时应完成防守结算。"
        );
        _test.True(
            fixture.Allies[1].IsAlive(),
            "守时成功路径要求防守目标仍然存活。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.DefenseDeadlineReached,
            "队伍覆灭与防守到时同批"
        );
    }

    private void TestHostileEliminationBeforeDeadlineDoesNotComplete()
    {
        using BattleTestFixture fixture = CreateDefenseBattle(
            "defense_hostiles_eliminated"
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult result;
        try
        {
            DefeatUnit(fixture.Runtime, fixture.Enemies[0], batch);
        }
        finally
        {
            result = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            result,
            BattleOutcomeFlushResult.NoChange,
            "敌军提前全灭不应替代防守倒计时。"
        );
        _test.True(
            fixture.State.FinalDecision == null,
            "敌军提前全灭时防守战仍应继续。"
        );
    }

    private void TestDefinitionRejectsInvalidAuthoringValues()
    {
        _test.True(
            Throws<ArgumentException>(
                () => _ = new BattleDefenseObjectiveDefinition("", DurationTu)
            ),
            "防守定义必须拒绝空 target_actor_id。"
        );
        _test.True(
            Throws<ArgumentOutOfRangeException>(
                () => _ = new BattleDefenseObjectiveDefinition(TargetActorId, 0)
            ),
            "防守定义必须拒绝非正 duration_tu。"
        );
        _test.True(
            Throws<ArgumentOutOfRangeException>(
                () => _ = new BattleDefenseObjectiveDefinition(TargetActorId, 7)
            ),
            "防守定义必须拒绝未按 5 TU 对齐的 duration_tu。"
        );
    }

    private void TestMissingAndDuplicateTargetBindingsAreRejected()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "defense_binding_ally",
            Vector2I.Zero
        );
        using (
            BattleTestFixture missingFixture = BattleTestFixture.CreateFlatBattle(
                "defense_missing_binding",
                new Vector2I(4, 1),
                new[] { ally },
                new[] { BuildEnemy("defense_missing_enemy", new Vector2I(3, 0)) }
            )
        )
        {
            _test.False(
                missingFixture.Runtime.InitializeBattleObjective(
                    BuildDefinition()
                ),
                "没有友方场景 actor 绑定时应拒绝初始化防守目标。"
            );
        }

        BattleUnitState duplicateAlly = BuildPersistentAlly(
            "defense_duplicate_ally",
            Vector2I.Zero
        );
        using BattleTestFixture duplicateFixture =
            BattleTestFixture.CreateFlatBattle(
                "defense_duplicate_binding",
                new Vector2I(5, 2),
                new[]
                {
                    duplicateAlly,
                    BuildScenarioActor(
                        "defense_duplicate_first",
                        new Vector2I(1, 0),
                        TargetActorId
                    ),
                    BuildScenarioActor(
                        "defense_duplicate_second",
                        new Vector2I(1, 1),
                        TargetActorId
                    ),
                },
                new[] { BuildEnemy("defense_duplicate_enemy", new Vector2I(4, 0)) }
            );
        _test.False(
            duplicateFixture.Runtime.InitializeBattleObjective(BuildDefinition()),
            "多个场景单位绑定同一 actor 时应拒绝初始化防守目标。"
        );
    }

    private void TestFormalDefenseEncounterStartsWithBattleOnlyScenarioActor()
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
                gameSession.GetBattleEncounterDefinitions()["mist_hollow_defense"];
            var anchor = new EncounterAnchorData
            {
                entity_id = "formal_defense_start",
                display_name = "正式防守开战",
                world_coord = Vector2I.Zero,
                faction_id = "hostile",
                region_tag = "mistwood",
                encounter_profile_id = "mist_hollow_defense",
                growth_stage = 0,
            };
            state = runtime.StartBattle(
                anchor,
                240726,
                encounter.Objective,
                new GDictionary
                {
                    ["ally_member_ids"] = new GStringNameArray
                    {
                        "formal_defense_ally",
                    },
                    ["validate_spawn_reachability"] = false,
                }
            );

            _test.True(
                state != null && !state.IsEmpty(),
                "正式防守遭遇应成功完成开战装配。"
            );
            BattleUnitState target = state
                ?.GetUnitsTyped()
                .SingleOrDefault(
                    unit => unit?.encounter_actor_id == (StringName)"mist_warden"
                );
            _test.True(target != null, "正式防守遭遇应生成稳定 actor 目标。");
            _test.Eq(
                target?.source_member_id.ToString() ?? "<missing>",
                "",
                "正式防守目标不得带入队伍成员写回键。"
            );
            _test.True(
                target != null
                    && state.GetAllyUnitIdsTyped().Contains(target.unit_id),
                "正式防守目标必须属于友方 active index。"
            );
            _test.True(
                state?.ObjectiveRuntimeState
                    is BattleDefenseObjectiveRuntimeState objective
                    && objective.TargetUnitId == target?.unit_id
                    && objective.StartTu == 0
                    && objective.DeadlineTu == 200,
                "正式防守 objective 应绑定场景目标并冻结 200 TU 截止时间。"
            );
        }
        finally
        {
            runtime.SetupStateForTests(null);
            BattleTestFixture.DisposeBattleState(state);
            runtime.Dispose();
        }
    }

    private BattleTestFixture CreateDefenseBattle(
        StringName battleId,
        int startTu = 0
    )
    {
        BattleUnitState ally = BuildPersistentAlly(
            $"{battleId}_ally",
            Vector2I.Zero
        );
        BattleUnitState target = BuildScenarioActor(
            $"{battleId}_target",
            Vector2I.Right,
            TargetActorId
        );
        BattleUnitState enemy = BuildEnemy(
            $"{battleId}_enemy",
            new Vector2I(3, 0)
        );
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(4, 1),
            new[] { ally, target },
            new[] { enemy }
        );
        foreach (BattleUnitState unit in fixture.State.GetUnitsTyped())
            unit.SetActionThresholdTyped(1_000_000);
        fixture.State.timeline.current_tu = startTu;
        fixture.State.PhaseKind = BattlePhaseKind.TimelineRunning;
        fixture.State.active_unit_id = "";
        _test.True(
            fixture.Runtime.InitializeBattleObjective(BuildDefinition()),
            $"{battleId} 应成功初始化防守目标。"
        );
        return fixture;
    }

    private static BattleDefenseObjectiveDefinition BuildDefinition() =>
        new(TargetActorId, DurationTu);

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
        unit.encounter_actor_id = actorId;
        return unit;
    }

    private static BattleUnitState BuildEnemy(
        StringName unitId,
        Vector2I coord
    ) => BattleTestFixture.BuildUnit(
        unitId,
        "enemy",
        coord,
        currentHp: 20
    );

    private static void DefeatUnit(
        BattleRuntimeModule runtime,
        BattleUnitState unit,
        BattleEventBatch batch
    )
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
        _test.Eq(
            decision.ObjectiveMode,
            BattleObjectiveMode.Defense,
            $"{context}终局应属于防守目标。"
        );
        _test.Eq(
            decision.Outcome,
            expectedOutcome,
            $"{context}终局 outcome 不正确。"
        );
        _test.Eq(
            decision.EndReason,
            expectedReason,
            $"{context}终局原因不正确。"
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
}
