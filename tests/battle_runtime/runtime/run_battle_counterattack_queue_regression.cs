using System;
using Godot;

public partial class run_battle_counterattack_queue_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestOnlyOutermostBoundaryDrains();
        TestWorkGuardPreservesOriginalFailure();
        TestDepthGuardPreservesOriginalFailure();
        TestStaleGenerationHandlesAreNoOps();
        RequestTestExit(
            _test.Finish("Battle counterattack queue regression")
        );
    }

    private void TestOnlyOutermostBoundaryDrains()
    {
        var effects = new BattleEffectExecutionContextService();
        using var coordinator =
            new BattleAttackActionCoordinator(
                effects,
                new BattleReactionBoundaryLimits(4, 20)
            );
        var drain = new RecordingDrainOwner();
        coordinator.BindDrainOwner(drain);
        using var batch = new BattleEventBatch();
        using BattleReactionBoundaryScope outer =
            coordinator.BeginReactionBoundary(batch);
        using BattleReactionBoundaryScope inner =
            coordinator.BeginReactionBoundary(batch);
        inner.Complete();
        inner.Dispose();
        _test.Eq(
            drain.DrainCount,
            0,
            "nested boundary completion must not drain the root."
        );
        outer.Complete();
        _test.Eq(
            drain.DrainCount,
            1,
            "the outermost boundary must drain exactly once."
        );
        _test.True(
            ReferenceEquals(drain.LastBatch, batch),
            "drain must retain the root batch identity."
        );
    }

    private void TestWorkGuardPreservesOriginalFailure()
    {
        var effects = new BattleEffectExecutionContextService();
        using var coordinator =
            new BattleAttackActionCoordinator(
                effects,
                new BattleReactionBoundaryLimits(4, 1)
            );
        var drain = new RecordingDrainOwner();
        coordinator.BindDrainOwner(drain);
        using var batch = new BattleEventBatch();
        InvalidOperationException captured = null;
        using (
            BattleReactionBoundaryScope boundary =
                coordinator.BeginReactionBoundary(batch)
        )
        using (effects.Push(BattleEffectOrigin.PlayerCommand()))
        using (
            BattleLogicalAttackScope logical =
                coordinator.BeginLogicalAttack(
                    BattleAttackDeliveryKind.MeleeWeapon
                )
        )
        {
            try
            {
                coordinator.ConsumeWorkItem(
                    BattleReactionWorkItemKind.AttackFact
                );
            }
            catch (InvalidOperationException error)
            {
                captured = error;
            }
        }

        _test.True(
            captured != null,
            "work guard must throw its own root failure."
        );
        _test.True(
            captured?.Message.Contains(
                "depth=1",
                StringComparison.Ordinal
            ) == true
                && captured.Message.Contains(
                    "consumed=1",
                    StringComparison.Ordinal
                ),
            "work guard failure must retain root depth and consumed count."
        );
        _test.False(
            captured?.Message.Contains(
                "disposed without Complete",
                StringComparison.Ordinal
            ) == true,
            "scope cleanup must not overwrite the work-guard exception."
        );
        _test.True(
            drain.AbortCount > 0,
            "failed roots must abort queued reaction state."
        );
    }

    private void TestStaleGenerationHandlesAreNoOps()
    {
        var effects = new BattleEffectExecutionContextService();
        using var coordinator =
            new BattleAttackActionCoordinator(
                effects,
                new BattleReactionBoundaryLimits(4, 20)
            );
        var drain = new RecordingDrainOwner();
        coordinator.BindDrainOwner(drain);
        using var oldBatch = new BattleEventBatch();
        BattleReactionBoundaryScope oldBoundary =
            coordinator.BeginReactionBoundary(oldBatch);
        IDisposable oldOrigin =
            effects.Push(BattleEffectOrigin.PlayerCommand());
        BattleLogicalAttackScope oldLogical =
            coordinator.BeginLogicalAttack(
                BattleAttackDeliveryKind.MeleeWeapon
            );

        coordinator.StopAcceptingAndAbort();
        effects.Clear();
        coordinator.ResetForBattle();

        using var newBatch = new BattleEventBatch();
        using BattleReactionBoundaryScope newBoundary =
            coordinator.BeginReactionBoundary(newBatch);
        using IDisposable newOrigin =
            effects.Push(BattleEffectOrigin.PlayerCommand());
        using BattleLogicalAttackScope newLogical =
            coordinator.BeginLogicalAttack(
                BattleAttackDeliveryKind.MeleeWeapon
            );
        _test.Eq(
            newLogical.Context.ActionId.Value,
            1L,
            "new battle must reset the action allocator."
        );

        oldLogical.Complete();
        oldLogical.Dispose();
        oldBoundary.Complete();
        oldBoundary.Dispose();
        oldOrigin.Dispose();

        _test.Eq(
            newLogical.Context.ActionId.Value,
            1L,
            "stale handles must not mutate the new generation."
        );
        newLogical.Complete();
        newLogical.Dispose();
        newOrigin.Dispose();
        newBoundary.Complete();
    }

    private void TestDepthGuardPreservesOriginalFailure()
    {
        var effects =
            new BattleEffectExecutionContextService();
        using var coordinator =
            new BattleAttackActionCoordinator(
                effects,
                new BattleReactionBoundaryLimits(1, 20)
            );
        var drain = new RecordingDrainOwner();
        coordinator.BindDrainOwner(drain);
        using var batch = new BattleEventBatch();
        InvalidOperationException captured = null;
        using (
            BattleReactionBoundaryScope outer =
                coordinator.BeginReactionBoundary(batch)
        )
        {
            try
            {
                coordinator.BeginReactionBoundary(batch);
            }
            catch (InvalidOperationException error)
            {
                captured = error;
            }
        }
        _test.True(
            captured?.Message.Contains(
                "reaction boundary depth 2 exceeds 1",
                StringComparison.Ordinal
            ) == true,
            "depth guard must retain actual next depth and configured limit."
        );
        _test.False(
            captured?.Message.Contains(
                "disposed without Complete",
                StringComparison.Ordinal
            ) == true,
            "outer scope cleanup must not replace the depth-guard failure."
        );
        _test.True(
            drain.AbortCount > 0,
            "depth-guard failure must abort transient reaction state."
        );

        coordinator.ResetForBattle();
        using var nextBatch = new BattleEventBatch();
        using BattleReactionBoundaryScope next =
            coordinator.BeginReactionBoundary(nextBatch);
        next.Complete();
        _test.True(
            ReferenceEquals(drain.LastBatch, nextBatch),
            "the next root must start clean after a depth failure."
        );
    }

    private sealed class RecordingDrainOwner
        : IBattleReactionDrainOwner
    {
        internal int DrainCount { get; private set; }
        internal int AbortCount { get; private set; }
        internal BattleEventBatch LastBatch { get; private set; }

        public void Drain(BattleEventBatch batch)
        {
            DrainCount++;
            LastBatch = batch;
        }

        public void AbortBoundary()
        {
            AbortCount++;
        }
    }
}
