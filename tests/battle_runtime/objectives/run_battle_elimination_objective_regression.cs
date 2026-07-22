using System;
using Godot;

public partial class run_battle_elimination_objective_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestLivingSidesRemainInProgress();
            TestAtomicMutualDefeatCompletesOnceAsDraw();
            TestPromotionModalLatchesBeforeSingleCompletion();
            TestUnsupportedDecisionCombinationIsRejected();
            TestObjectiveRestoreRejectsDecisionWithoutRuntimeObjective();
        }
        catch (Exception exception)
        {
            _test.Fail(
                $"Unhandled battle elimination objective regression exception: {exception}"
            );
        }

        RequestTestExit(
            _test.Finish("Battle elimination objective regression")
        );
    }

    private void TestLivingSidesRemainInProgress()
    {
        using BattleTestFixture fixture = CreateDuel("objective_living_sides");
        using BattleEventBatch batch = new();

        BattleOutcomeFlushResult flushResult =
            fixture.Runtime.FlushBattleOutcomeEvaluation(batch);

        _test.Eq(
            flushResult,
            BattleOutcomeFlushResult.NoChange,
            "双方仍有存活单位时，歼灭目标应保持进行中。"
        );
        _test.True(
            fixture.State.FinalDecision == null,
            "进行中的歼灭目标不应锁存终局决定。"
        );
        _test.Eq(
            fixture.State.PhaseKind,
            BattlePhaseKind.UnitActing,
            "进行中的歼灭目标不应切换战斗阶段。"
        );
        _test.False(
            batch.battle_ended,
            "进行中的歼灭目标不应发布 battle_ended。"
        );
        _test.True(
            fixture.Runtime.GetBattleResolutionResult() == null,
            "进行中的歼灭目标不应生成战斗结果。"
        );
    }

    private void TestAtomicMutualDefeatCompletesOnceAsDraw()
    {
        using BattleTestFixture fixture = CreateDuel("objective_mutual_defeat");
        using BattleEventBatch completionBatch = new();
        fixture.Runtime.BeginObjectiveMutation();
        bool mutationCompleted = false;
        BattleOutcomeFlushResult completionResult;
        try
        {
            DefeatUnit(fixture.Runtime, fixture.Allies[0], completionBatch);
            DefeatUnit(fixture.Runtime, fixture.Enemies[0], completionBatch);

            using BattleEventBatch prematureBatch = new();
            _test.Eq(
                fixture.Runtime.FlushBattleOutcomeEvaluation(prematureBatch),
                BattleOutcomeFlushResult.NoChange,
                "同一原子变更尚未结束时，双方阵亡不应提前结算。"
            );
            _test.True(
                fixture.State.FinalDecision == null,
                "flush 被原子边界阻挡时不应锁存中间终局。"
            );
            _test.Eq(
                fixture.State.PhaseKind,
                BattlePhaseKind.UnitActing,
                "flush 被原子边界阻挡时战斗阶段应保持不变。"
            );
            _test.False(
                prematureBatch.battle_ended,
                "原子变更内的提前 flush 不应发布 battle_ended。"
            );

            mutationCompleted = true;
        }
        finally
        {
            completionResult = fixture.Runtime.EndObjectiveMutation(
                completionBatch,
                mutationCompleted
            );
        }

        _test.Eq(
            completionResult,
            BattleOutcomeFlushResult.Completed,
            "原子变更完成后应一次性结算双方阵亡。"
        );
        AssertDrawDecision(fixture.State.FinalDecision, "原子双方阵亡");
        _test.Eq(
            fixture.State.PhaseKind,
            BattlePhaseKind.BattleEnded,
            "原子双方阵亡结算后应进入 battle_ended。"
        );
        _test.True(
            completionBatch.battle_ended,
            "首次完成结算的 batch 应发布 battle_ended。"
        );
        _test.Eq(
            completionBatch.LogLinesTyped.Count,
            1,
            "首次完成结算的 batch 应只有一条终局日志。"
        );
        _test.Eq(
            fixture.State.log_entries.Count,
            1,
            "首次完成结算应只把一条终局日志写入状态。"
        );

        BattleResolutionResult firstResult =
            fixture.Runtime.ConsumeBattleResolutionResult();
        _test.True(firstResult != null, "首次完成结算应生成一个可消费结果。");
        if (firstResult != null)
        {
            _test.Eq(
                firstResult.objective_mode,
                BattleObjectiveMode.Elimination,
                "双方阵亡结果应保留歼灭目标模式。"
            );
            _test.Eq(
                firstResult.outcome,
                BattleOutcomeKind.Draw,
                "双方阵亡结果应为 Draw。"
            );
            _test.Eq(
                firstResult.end_reason,
                BattleEndReasonKind.EliminationMutualDestruction,
                "双方阵亡结果应为 MutualElimination。"
            );
        }

        using BattleEventBatch repeatedBatch = new();
        _test.Eq(
            fixture.Runtime.FlushBattleOutcomeEvaluation(repeatedBatch),
            BattleOutcomeFlushResult.AlreadyCompleted,
            "终局后的重复 flush 应保持幂等。"
        );
        _test.False(
            repeatedBatch.battle_ended,
            "重复 flush 不应再次发布 battle_ended。"
        );
        _test.Eq(
            repeatedBatch.LogLinesTyped.Count,
            0,
            "重复 flush 不应再次生成终局日志。"
        );
        _test.Eq(
            fixture.State.log_entries.Count,
            1,
            "重复 flush 不应向状态追加第二条终局日志。"
        );
        _test.True(
            fixture.Runtime.ConsumeBattleResolutionResult() == null,
            "终局结果只能消费一次。"
        );
    }

    private void TestPromotionModalLatchesBeforeSingleCompletion()
    {
        using BattleTestFixture fixture = CreateDuel("objective_promotion_modal");
        fixture.State.ModalStateKind = BattleModalStateKind.PromotionChoice;
        fixture.State.timeline.frozen = true;

        using BattleEventBatch defeatBatch = new();
        fixture.Runtime.BeginObjectiveMutation();
        bool mutationCompleted = false;
        BattleOutcomeFlushResult latchedResult;
        try
        {
            DefeatUnit(fixture.Runtime, fixture.Enemies[0], defeatBatch);
            mutationCompleted = true;
        }
        finally
        {
            latchedResult = fixture.Runtime.EndObjectiveMutation(
                defeatBatch,
                mutationCompleted
            );
        }

        _test.Eq(
            latchedResult,
            BattleOutcomeFlushResult.DecisionLatched,
            "晋升选择 modal 存在时应先锁存终局而不完成战斗。"
        );
        BattleFinalDecision latchedDecision = fixture.State.FinalDecision;
        _test.True(latchedDecision != null, "晋升 modal 下应锁存终局决定。");
        if (latchedDecision != null)
        {
            _test.Eq(
                latchedDecision.Outcome,
                BattleOutcomeKind.PlayerSuccess,
                "敌方全灭时锁存结果应为 PlayerSuccess。"
            );
            _test.Eq(
                latchedDecision.EndReason,
                BattleEndReasonKind.EliminationHostilesDefeated,
                "敌方全灭时锁存原因应为 HostilesDefeated。"
            );
        }
        _test.Eq(
            fixture.State.PhaseKind,
            BattlePhaseKind.UnitActing,
            "终局锁存期间不应提前进入 battle_ended。"
        );
        _test.False(
            defeatBatch.battle_ended,
            "终局锁存期间不应发布 battle_ended。"
        );
        _test.Eq(
            defeatBatch.LogLinesTyped.Count,
            0,
            "终局锁存期间不应生成终局日志。"
        );
        _test.True(
            fixture.Runtime.GetBattleResolutionResult() == null,
            "终局锁存期间不应生成可消费结果。"
        );

        using BattleEventBatch selectionBatch = new();
        fixture.Runtime.BeginObjectiveMutation();
        BattleOutcomeFlushResult selectionResult;
        try
        {
            fixture.State.ModalStateKind = BattleModalStateKind.None;
            fixture.State.timeline.frozen = false;
        }
        finally
        {
            selectionResult = fixture.Runtime.EndObjectiveMutation(selectionBatch);
        }

        _test.Eq(
            selectionResult,
            BattleOutcomeFlushResult.Completed,
            "晋升选择结束后应完成已锁存的终局。"
        );
        _test.True(
            ReferenceEquals(latchedDecision, fixture.State.FinalDecision),
            "完成已锁存终局时不应替换终局决定。"
        );
        _test.True(
            selectionBatch.battle_ended,
            "晋升选择结束后的完成 batch 应发布 battle_ended。"
        );
        _test.Eq(
            selectionBatch.LogLinesTyped.Count,
            1,
            "晋升选择结束后应只生成一条终局日志。"
        );
        _test.Eq(
            fixture.State.log_entries.Count,
            1,
            "晋升选择结束后状态中应只有一条终局日志。"
        );

        BattleResolutionResult firstResult =
            fixture.Runtime.ConsumeBattleResolutionResult();
        _test.True(firstResult != null, "晋升选择结束后应生成一个结果。");
        _test.Eq(
            firstResult?.outcome ?? BattleOutcomeKind.Unknown,
            BattleOutcomeKind.PlayerSuccess,
            "晋升选择结束后生成的结果应保留已锁存胜利。"
        );

        using BattleEventBatch repeatedBatch = new();
        _test.Eq(
            fixture.Runtime.FlushBattleOutcomeEvaluation(repeatedBatch),
            BattleOutcomeFlushResult.AlreadyCompleted,
            "晋升选择后的重复 flush 应保持幂等。"
        );
        _test.Eq(
            repeatedBatch.LogLinesTyped.Count,
            0,
            "晋升选择后的重复 flush 不应生成第二条终局日志。"
        );
        _test.True(
            fixture.Runtime.ConsumeBattleResolutionResult() == null,
            "晋升选择后的终局结果只能消费一次。"
        );
    }

    private static BattleTestFixture CreateDuel(StringName battleId)
    {
        BattleUnitState ally = BattleTestFixture.BuildUnit(
            $"{battleId}_ally",
            "player",
            new Vector2I(0, 0),
            currentHp: 20
        );
        BattleUnitState enemy = BattleTestFixture.BuildUnit(
            $"{battleId}_enemy",
            "enemy",
            new Vector2I(2, 0),
            currentHp: 20
        );
        return BattleTestFixture.CreateFlatBattle(
            battleId,
            new Vector2I(3, 1),
            new[] { ally },
            new[] { enemy }
        );
    }

    private void TestUnsupportedDecisionCombinationIsRejected()
    {
        _test.True(
            Throws<ArgumentException>(
                () =>
                    _ = new BattleFinalDecision(
                        BattleObjectiveMode.Boss,
                        BattleOutcomeKind.PlayerSuccess,
                        BattleEndReasonKind.EliminationHostilesDefeated,
                        0
                    )
            ),
            "尚未落地的目标模式不得借用歼灭原因构造伪终局。"
        );
    }

    private void TestObjectiveRestoreRejectsDecisionWithoutRuntimeObjective()
    {
        BattleState state = new();
        _test.True(
            Throws<InvalidOperationException>(
                () =>
                    state.RestoreObjectiveState(
                        null,
                        BattleObjectiveTestFactory.CreateEliminationDecision("player")
                    )
            ),
            "恢复目标快照时不得接受无 runtime objective 的终局决定。"
        );
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

    private void AssertDrawDecision(BattleFinalDecision decision, string context)
    {
        _test.True(decision != null, $"{context}应锁存终局决定。");
        if (decision == null)
            return;
        _test.Eq(
            decision.ObjectiveMode,
            BattleObjectiveMode.Elimination,
            $"{context}终局应属于歼灭目标。"
        );
        _test.Eq(
            decision.Outcome,
            BattleOutcomeKind.Draw,
            $"{context}终局应为 Draw。"
        );
        _test.Eq(
            decision.EndReason,
            BattleEndReasonKind.EliminationMutualDestruction,
            $"{context}终局原因应为 MutualElimination。"
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
