using System;
using System.Collections.Generic;

/// 拆卸步骤的容错执行：跑完每一步，累计所有异常。
///
/// 原本只存在于 <c>BattleRuntimeModule.RunTeardownStep</c>，但它是纯工具、不碰任何运行时状态，
/// 而 <c>battle_runtime_isolated</c> 层的服务同样要用它拆自己的子服务。为了不让隔离层为一个
/// try/catch 去依赖 hub，下移到这里；hub 上那份保留为转发，69 个既有调用点无需改动。
///
/// 早期实现是 <c>accumulatedFailure ??= exception</c>，只留第一个异常。BattleRuntimeModule
/// 的拆卸串了约 45 步，第 5 步和第 30 步同时抛时，后者永久不可见。改为与
/// <see cref="NativeLeaseScope"/>、<c>GodotObjectOwnership</c>、<c>ApplicationLifetimeCoordinator</c>
/// 一致的聚合语义：单个失败原样上抛（保留原始栈），多个失败合成 AggregateException。
internal static class BattleTeardown
{
    internal static void RunStep(ref Exception accumulatedFailure, Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception exception)
        {
            accumulatedFailure = Combine(accumulatedFailure, exception);
        }
    }

    private static Exception Combine(Exception accumulated, Exception next)
    {
        if (accumulated == null)
            return next;
        if (accumulated is AggregateException aggregate)
        {
            var inner = new List<Exception>(aggregate.InnerExceptions) { next };
            return new AggregateException(aggregate.Message, inner);
        }
        return new AggregateException(
            "Teardown steps failed.",
            new[] { accumulated, next }
        );
    }
}
