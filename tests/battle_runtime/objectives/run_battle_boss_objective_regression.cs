using System;
using Godot;

public partial class run_battle_boss_objective_regression
    : LifecycleTestSceneTree
{
    private static readonly StringName BossActorId = "boss_actor";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestMinionDefeatDoesNotCompleteBattle();
            TestBossDefeatSucceedsWhileMinionSurvives();
            TestRequiredPartyDefeatFailsWhileSummonSurvives();
            TestAtomicBossAndPartyDefeatDraws();
            TestMissingAndDuplicateBossBindingsAreRejected();
            TestSummonOnlyPartyIsRejected();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unhandled battle boss objective regression exception: {exception}"
            );
        }

        RequestTestExit(_test.Finish("Battle boss objective regression"));
    }

    private void TestMinionDefeatDoesNotCompleteBattle()
    {
        using BattleTestFixture fixture = CreateBossBattle(
            "boss_minion_defeat",
            includeSummon: false
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult flushResult;
        try
        {
            DefeatUnit(fixture.Runtime, fixture.Enemies[1], batch);
        }
        finally
        {
            flushResult = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            flushResult,
            BattleOutcomeFlushResult.NoChange,
            "仅击败首领随从时，首领目标应保持进行中。"
        );
        _test.True(
            fixture.Enemies[0].is_alive,
            "仅击败随从不应改变首领的存活状态。"
        );
        _test.True(
            fixture.State.FinalDecision == null,
            "首领仍存活时不应锁存终局决定。"
        );
        _test.False(
            batch.battle_ended,
            "仅击败随从不应发布 battle_ended。"
        );
    }

    private void TestBossDefeatSucceedsWhileMinionSurvives()
    {
        using BattleTestFixture fixture = CreateBossBattle(
            "boss_target_defeat",
            includeSummon: false
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult flushResult;
        try
        {
            DefeatUnit(fixture.Runtime, fixture.Enemies[0], batch);
        }
        finally
        {
            flushResult = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            flushResult,
            BattleOutcomeFlushResult.Completed,
            "击败绑定首领后应立即完成首领目标。"
        );
        _test.True(
            fixture.Enemies[1].is_alive,
            "首领胜利不应要求同时歼灭随从。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerSuccess,
            BattleEndReasonKind.BossTargetDefeated,
            "击败首领"
        );
        _test.True(batch.battle_ended, "击败首领后应发布 battle_ended。");
    }

    private void TestRequiredPartyDefeatFailsWhileSummonSurvives()
    {
        using BattleTestFixture fixture = CreateBossBattle(
            "boss_party_defeat",
            includeSummon: true
        );
        using BattleEventBatch batch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult flushResult;
        try
        {
            DefeatUnit(fixture.Runtime, fixture.Allies[0], batch);
        }
        finally
        {
            flushResult = fixture.Runtime.EndObjectiveMutation(batch);
        }

        _test.Eq(
            flushResult,
            BattleOutcomeFlushResult.Completed,
            "初始持久队员全灭时应判定首领目标失败。"
        );
        _test.True(
            fixture.Allies[1].is_alive,
            "召唤物应保持存活，以证明它不能替代持久队员延续目标。"
        );
        _test.True(
            fixture.Enemies[0].is_alive,
            "队伍失败时首领应仍然存活。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.PlayerFailure,
            BattleEndReasonKind.BossPartyDefeated,
            "持久队伍全灭"
        );
    }

    private void TestAtomicBossAndPartyDefeatDraws()
    {
        using BattleTestFixture fixture = CreateBossBattle(
            "boss_mutual_defeat",
            includeSummon: true
        );
        using BattleEventBatch completionBatch = new();

        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult completionResult;
        try
        {
            DefeatUnit(fixture.Runtime, fixture.Enemies[0], completionBatch);
            DefeatUnit(fixture.Runtime, fixture.Allies[0], completionBatch);

            using BattleEventBatch prematureBatch = new();
            _test.Eq(
                fixture.Runtime.FlushBattleOutcomeEvaluation(prematureBatch),
                BattleOutcomeFlushResult.NoChange,
                "同一原子变更内的首领死亡不应提前锁存胜利。"
            );
            _test.True(
                fixture.State.FinalDecision == null,
                "原子变更结束前不应暴露中间终局。"
            );
        }
        finally
        {
            completionResult = fixture.Runtime.EndObjectiveMutation(
                completionBatch
            );
        }

        _test.Eq(
            completionResult,
            BattleOutcomeFlushResult.Completed,
            "首领与初始持久队员在同一原子变更内全灭后应完成结算。"
        );
        _test.True(
            fixture.Allies[1].is_alive,
            "召唤物存活不应把首领同归于尽改判为胜利。"
        );
        AssertDecision(
            fixture.State.FinalDecision,
            BattleOutcomeKind.Draw,
            BattleEndReasonKind.BossMutualDestruction,
            "首领同归于尽"
        );
    }

    private void TestMissingAndDuplicateBossBindingsAreRejected()
    {
        BattleUnitState missingAlly = BuildPersistentAlly(
            "boss_missing_ally",
            new Vector2I(0, 0)
        );
        BattleUnitState unboundEnemy = BuildEnemy(
            "boss_missing_enemy",
            new Vector2I(3, 0),
            ""
        );
        using (
            BattleTestFixture missingFixture = BattleTestFixture.CreateFlatBattle(
                "boss_missing_binding",
                new Vector2I(4, 1),
                new[] { missingAlly },
                new[] { unboundEnemy }
            )
        )
        {
            _test.False(
                missingFixture.Runtime.InitializeBattleObjective(
                    new BattleBossObjectiveDefinition(BossActorId)
                ),
                "没有任何敌方单位绑定目标 actor 时应拒绝初始化首领目标。"
            );
            _test.True(
                missingFixture.State.ObjectiveRuntimeState == null,
                "首领绑定缺失后不得保留旧目标运行态。"
            );
        }

        BattleUnitState duplicateAlly = BuildPersistentAlly(
            "boss_duplicate_ally",
            new Vector2I(0, 0)
        );
        BattleUnitState firstBoss = BuildEnemy(
            "boss_duplicate_first",
            new Vector2I(3, 0),
            BossActorId
        );
        BattleUnitState secondBoss = BuildEnemy(
            "boss_duplicate_second",
            new Vector2I(4, 0),
            BossActorId
        );
        using (
            BattleTestFixture duplicateFixture = BattleTestFixture.CreateFlatBattle(
                "boss_duplicate_binding",
                new Vector2I(5, 1),
                new[] { duplicateAlly },
                new[] { firstBoss, secondBoss }
            )
        )
        {
            _test.False(
                duplicateFixture.Runtime.InitializeBattleObjective(
                    new BattleBossObjectiveDefinition(BossActorId)
                ),
                "多个敌方单位绑定同一目标 actor 时应拒绝初始化首领目标。"
            );
            _test.True(
                duplicateFixture.State.ObjectiveRuntimeState == null,
                "首领绑定歧义后不得保留旧目标运行态。"
            );
        }
    }

    private void TestSummonOnlyPartyIsRejected()
    {
        BattleUnitState summon = BattleTestFixture.BuildUnit(
            "boss_summon_only",
            "player",
            new Vector2I(0, 0),
            currentHp: 20
        );
        BattleUnitState boss = BuildEnemy(
            "boss_summon_only_target",
            new Vector2I(2, 0),
            BossActorId
        );
        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "boss_summon_only_party",
            new Vector2I(3, 1),
            new[] { summon },
            new[] { boss }
        );

        _test.False(
            fixture.Runtime.InitializeBattleObjective(
                new BattleBossObjectiveDefinition(BossActorId)
            ),
            "只有非持久召唤物的友方阵容不得初始化首领目标。"
        );
    }

    private BattleTestFixture CreateBossBattle(
        StringName battleId,
        bool includeSummon
    )
    {
        BattleUnitState ally = BuildPersistentAlly(
            $"{battleId}_ally",
            new Vector2I(0, 0)
        );
        BattleUnitState boss = BuildEnemy(
            $"{battleId}_boss",
            new Vector2I(4, 0),
            BossActorId
        );
        BattleUnitState minion = BuildEnemy(
            $"{battleId}_minion",
            new Vector2I(4, 1),
            ""
        );
        BattleUnitState[] allies;
        if (includeSummon)
        {
            BattleUnitState summon = BattleTestFixture.BuildUnit(
                $"{battleId}_summon",
                "player",
                new Vector2I(0, 1),
                currentHp: 20
            );
            allies = new[] { ally, summon };
        }
        else
        {
            allies = new[] { ally };
        }
        BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(6, 2),
            allies,
            new[] { boss, minion }
        );
        _test.True(
            fixture.Runtime.InitializeBattleObjective(
                new BattleBossObjectiveDefinition(BossActorId)
            ),
            $"{battleId} 应成功初始化首领目标。"
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
            BattleObjectiveMode.Boss,
            $"{context}终局应属于首领目标。"
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
