using System;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_battle_intercept_objective_regression
    : LifecycleTestSceneTree
{
    private static readonly StringName TargetActorId = "intercept_target";
    private static readonly StringName ExitZoneId = "west_breakthrough";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            TestTargetDefeatSucceedsWhileGuardsSurvive();
            TestTargetReachingExitFailsObjective();
            TestPersistentPartyDefeatFailsBeforeTargetEscapes();
            TestAtomicTargetAndPartyDefeatDraws();
            TestLargeTargetRequiresFullFootprintInsideExit();
            TestMissingAndDuplicateTargetBindingsAreRejected();
            TestFormalInterceptEncounterStartsWithBoundRosterTarget();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unhandled battle intercept objective regression exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Battle intercept objective regression"));
    }

    private void TestTargetDefeatSucceedsWhileGuardsSurvive()
    {
        using BattleTestFixture fixture = CreateInterceptBattle(
            "intercept_target_defeated"
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
            BattleOutcomeFlushResult.Completed,
            "截击目标被击败后应立即完成战斗。"
        );
        _test.True(
            fixture.Enemies[1].is_alive,
            "截击成功不应要求歼灭目标护卫。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.InterceptTargetDefeated,
            "截击目标被击败"
        );
    }

    private void TestTargetReachingExitFailsObjective()
    {
        using BattleTestFixture fixture = CreateInterceptBattle(
            "intercept_target_escaped"
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult result;
        try
        {
            _test.True(
                fixture.Runtime._grid_service.PlaceUnit(
                    fixture.State,
                    fixture.Enemies[0],
                    new Vector2I(0, 0),
                    ignore_height: true
                ),
                "截击目标应能移入左侧逃脱区。"
            );
        }
        finally
        {
            result = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            result,
            BattleOutcomeFlushResult.Completed,
            "目标存活进入逃脱区后应立即判定截击失败。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.InterceptTargetEscaped,
            "截击目标逃脱"
        );
    }

    private void TestPersistentPartyDefeatFailsBeforeTargetEscapes()
    {
        using BattleTestFixture fixture = CreateInterceptBattle(
            "intercept_party_defeated"
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
            "初始持久队伍覆灭后应判定截击失败。"
        );
        _test.True(
            fixture.Enemies[0].is_alive,
            "队伍覆灭失败路径中截击目标应仍然存活。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.InterceptPartyDefeated,
            "截击队伍覆灭"
        );
    }

    private void TestAtomicTargetAndPartyDefeatDraws()
    {
        using BattleTestFixture fixture = CreateInterceptBattle(
            "intercept_mutual_defeat"
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult result;
        try
        {
            DefeatUnit(fixture.Runtime, fixture.Enemies[0], batch);
            DefeatUnit(fixture.Runtime, fixture.Allies[0], batch);
            using BattleEventBatch prematureBatch = new();
            _test.Eq(
                fixture.Runtime.FlushBattleOutcomeEvaluation(prematureBatch),
                BattleOutcomeFlushResult.NoChange,
                "同一原子变更内不得提前锁存截击成功。"
            );
        }
        finally
        {
            result = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            result,
            BattleOutcomeFlushResult.Completed,
            "目标与持久队伍同灭后应完成截击结算。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.Draw,
            BattleEndReasonKind.InterceptMutualDestruction,
            "截击同归于尽"
        );
    }

    private void TestLargeTargetRequiresFullFootprintInsideExit()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "intercept_large_ally",
            new Vector2I(3, 0)
        );
        BattleUnitState target = BuildEnemy(
            "intercept_large_target",
            new Vector2I(1, 0),
            TargetActorId
        );
        _test.True(
            target.SetBodySizeCategory("large"),
            "大型截击目标应配置为 2x2 footprint。"
        );
        BattleUnitState guard = BuildEnemy(
            "intercept_large_guard",
            new Vector2I(4, 0),
            ""
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "intercept_large_footprint",
            new Vector2I(6, 3),
            new[] { ally },
            new[] { target, guard }
        );
        _test.True(
            fixture.Runtime.InitializeBattleObjective(
                new BattleInterceptObjectiveDefinition(
                    TargetActorId,
                    ExitZoneId,
                    BattleMapEdge.Left,
                    2
                )
            ),
            "大型截击目标应能绑定 2 格深逃脱区。"
        );
        using BattleEventBatch partialBatch = new();
        _test.Eq(
            fixture.Runtime.FlushBattleOutcomeEvaluation(partialBatch),
            BattleOutcomeFlushResult.NoChange,
            "只有一半 footprint 位于逃脱区时不得判定目标逃脱。"
        );

        using BattleEventBatch completionBatch = new();
        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult result;
        try
        {
            _test.True(
                fixture.Runtime._grid_service.PlaceUnit(
                    fixture.State,
                    target,
                    Vector2I.Zero,
                    ignore_height: true
                ),
                "大型截击目标应能完整移入逃脱区。"
            );
        }
        finally
        {
            result = fixture.Runtime.EndObjectiveMutation(completionBatch);
        }
        _test.Eq(
            result,
            BattleOutcomeFlushResult.Completed,
            "大型目标完整 footprint 进入逃脱区后应判定失败。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.InterceptTargetEscaped,
            "大型截击目标逃脱"
        );
    }

    private void TestMissingAndDuplicateTargetBindingsAreRejected()
    {
        BattleUnitState ally = BuildPersistentAlly(
            "intercept_binding_ally",
            new Vector2I(2, 0)
        );
        using (
            BattleTestFixture missingFixture = BattleTestFixture.CreateFlatBattle(
                "intercept_missing_binding",
                new Vector2I(5, 1),
                new[] { ally },
                new[] { BuildEnemy("intercept_unbound_enemy", new Vector2I(4, 0), "") }
            )
        )
        {
            _test.False(
                missingFixture.Runtime.InitializeBattleObjective(
                    BuildDefinition()
                ),
                "没有敌方 roster actor 绑定时应拒绝初始化截击目标。"
            );
        }

        BattleUnitState duplicateAlly = BuildPersistentAlly(
            "intercept_duplicate_ally",
            new Vector2I(2, 0)
        );
        using BattleTestFixture duplicateFixture =
            BattleTestFixture.CreateFlatBattle(
                "intercept_duplicate_binding",
                new Vector2I(6, 2),
                new[] { duplicateAlly },
                new[]
                {
                    BuildEnemy(
                        "intercept_duplicate_first",
                        new Vector2I(4, 0),
                        TargetActorId
                    ),
                    BuildEnemy(
                        "intercept_duplicate_second",
                        new Vector2I(5, 1),
                        TargetActorId
                    ),
                }
            );
        _test.False(
            duplicateFixture.Runtime.InitializeBattleObjective(BuildDefinition()),
            "多个敌人绑定同一 actor 时应拒绝初始化截击目标。"
        );
    }

    private void TestFormalInterceptEncounterStartsWithBoundRosterTarget()
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
                    "mist_hollow_intercept"
                ];
            var anchor = new EncounterAnchorData
            {
                entity_id = "formal_intercept_start",
                display_name = "正式截击开战",
                world_coord = Vector2I.Zero,
                faction_id = "hostile",
                region_tag = "mistwood",
                encounter_profile_id = "mist_hollow_intercept",
                growth_stage = 0,
            };
            state = runtime.StartBattle(
                anchor,
                240725,
                encounter.Objective,
                new GDictionary
                {
                    ["ally_member_ids"] = new GStringNameArray
                    {
                        "formal_intercept_ally",
                    },
                    ["validate_spawn_reachability"] = false,
                }
            );

            _test.True(
                state != null && !state.IsEmpty(),
                "正式截击遭遇应成功完成开战装配。"
            );
            BattleUnitState target = state
                ?.GetUnitsTyped()
                .SingleOrDefault(
                    unit => unit?.encounter_actor_id == (StringName)"mist_courier"
                );
            _test.True(target != null, "正式截击遭遇应生成稳定 actor 目标。");
            _test.True(
                target != null
                    && state.GetEnemyUnitIdsTyped().Contains(target.unit_id),
                "正式截击目标必须属于敌方 active index。"
            );
            _test.True(
                state?.ObjectiveRuntimeState
                    is BattleInterceptObjectiveRuntimeState objective
                    && objective.TargetUnitId == target?.unit_id,
                "正式截击 objective 应绑定 roster 生成后的目标 unit id。"
            );
        }
        finally
        {
            runtime.SetupStateForTests(null);
            runtime.Dispose();
        }
    }

    private BattleTestFixture CreateInterceptBattle(StringName battleId)
    {
        BattleUnitState ally = BuildPersistentAlly(
            $"{battleId}_ally",
            new Vector2I(2, 0)
        );
        BattleUnitState target = BuildEnemy(
            $"{battleId}_target",
            new Vector2I(4, 0),
            TargetActorId
        );
        BattleUnitState guard = BuildEnemy(
            $"{battleId}_guard",
            new Vector2I(5, 1),
            ""
        );
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(6, 2),
            new[] { ally },
            new[] { target, guard }
        );
        _test.True(
            fixture.Runtime.InitializeBattleObjective(BuildDefinition()),
            $"{battleId} 应成功初始化截击目标。"
        );
        return fixture;
    }

    private static BattleInterceptObjectiveDefinition BuildDefinition() =>
        new(
            TargetActorId,
            ExitZoneId,
            BattleMapEdge.Left,
            1
        );

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
        Vector2I coord,
        StringName actorId
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            "enemy",
            coord,
            currentHp: 20
        );
        unit.encounter_actor_id = actorId;
        return unit;
    }

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
            BattleObjectiveMode.Intercept,
            $"{context}终局应属于截击目标。"
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
}
