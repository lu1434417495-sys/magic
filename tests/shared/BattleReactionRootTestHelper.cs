using System;

internal static class BattleReactionRootTestHelper
{
    internal static void ExecuteInReactionRoot(
        BattleRuntimeModule runtime,
        BattleEventBatch batch,
        Action body
    )
    {
        ExecuteInReactionRoot(
            runtime,
            batch,
            BattleEffectOrigin.PlayerCommand(),
            body
        );
    }

    internal static void ExecuteInReactionRoot(
        BattleRuntimeModule runtime,
        BattleEventBatch batch,
        BattleEffectOrigin origin,
        Action body
    )
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(body);
        using BattleReactionBoundaryScope boundary =
            runtime.BeginReactionBoundary(batch);
        using IDisposable originScope =
            runtime.EffectExecutionContext.Push(origin);
        body();
        boundary.Complete();
    }
}
