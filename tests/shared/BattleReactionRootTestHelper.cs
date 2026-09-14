using System;
using System.Collections.Generic;

internal static class BattleReactionRootTestHelper
{
    internal static T ExecuteInReactionRoot<T>(
        BattleRuntimeModule runtime,
        BattleEventBatch batch,
        Func<T> body
    )
    {
        T result = default;
        ExecuteInReactionRoot(runtime, batch, () => { result = body(); });
        return result;
    }

    internal static T ExecuteLogicalAttack<T>(
        BattleRuntimeModule runtime,
        BattleEventBatch batch,
        BattleUnitState source,
        IReadOnlyList<CombatEffectDefinition> effects,
        Func<BattleAttackActionContext, T> body
    )
    {
        T result = default;
        ExecuteLogicalAttack(runtime, batch, source, effects, context => { result = body(context); });
        return result;
    }

    internal static void ExecuteLogicalAttack(
        BattleRuntimeModule runtime,
        BattleEventBatch batch,
        BattleUnitState source,
        IReadOnlyList<CombatEffectDefinition> effects,
        Action<BattleAttackActionContext> body
    )
    {
        ExecuteInReactionRoot(runtime, batch, () =>
        {
            using BattleLogicalAttackScope attack = runtime.BeginLogicalAttack(
                BattleAttackDeliveryRules.Resolve(effects, source.GetWeaponProjectionReadViewTyped())
            );
            try
            {
                body(attack.Context);
                attack.Complete();
            }
            catch
            {
                runtime.AbortActiveReactionBoundary();
                throw;
            }
        });
    }

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
        try
        {
            body();
            boundary.Complete();
        }
        catch
        {
            runtime.AbortActiveReactionBoundary();
            throw;
        }
    }
}
