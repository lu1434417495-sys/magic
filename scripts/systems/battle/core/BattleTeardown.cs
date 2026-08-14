using System;

/// 拆卸步骤的容错执行：跑完每一步，只记住第一个异常。
///
/// 原本只存在于 <c>BattleRuntimeModule.RunTeardownStep</c>，但它是纯工具、不碰任何运行时状态，
/// 而 <c>battle_runtime_isolated</c> 层的服务同样要用它拆自己的子服务。为了不让隔离层为一个
/// try/catch 去依赖 hub，下移到这里；hub 上那份保留为转发，69 个既有调用点无需改动。
internal static class BattleTeardown
{
    internal static void RunStep(ref Exception firstFailure, Action action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception exception)
        {
            firstFailure ??= exception;
        }
    }
}
