using Godot;

public partial class run_battle_runtime_terrain_generator_ownership_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestRuntimeDisposesDefaultTerrainGenerator();
        TestInjectedTerrainGeneratorRemainsCallerOwned();
        TestInjectedTerrainGeneratorCanTransferOwnership();
        TestReplacingDefaultTerrainGeneratorDisposesOwnedPreviousInstance();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Battle runtime terrain generator ownership regression"));
    }

    private void TestRuntimeDisposesDefaultTerrainGenerator()
    {
        var runtime = new BattleRuntimeModule();
        BattleTerrainGenerator ownedGenerator = runtime.GetTerrainGenerator();

        _test.True(ownedGenerator.HasLiveOwnedRngForTests, "默认 terrain generator 应持有 live owned RNG。");
        runtime.dispose();

        _test.True(ownedGenerator.IsDisposed, "BattleRuntimeModule 应释放自己创建的默认 terrain generator。");
        _test.True(!ownedGenerator.HasLiveOwnedRngForTests, "释放默认 terrain generator 时应清理 owned RNG。");
    }

    private void TestInjectedTerrainGeneratorRemainsCallerOwned()
    {
        var runtime = new BattleRuntimeModule();
        var injectedGenerator = new BattleTerrainGenerator();

        runtime.setup(terrain_generator: injectedGenerator);
        runtime.dispose();

        _test.True(!injectedGenerator.IsDisposed, "外部注入的 terrain generator 应由 caller 释放，runtime dispose 不应释放它。");
        _test.True(injectedGenerator.HasLiveOwnedRngForTests, "runtime 不拥有外部注入 generator 的 RNG。");
        injectedGenerator.Dispose();
        _test.True(!injectedGenerator.HasLiveOwnedRngForTests, "caller 释放注入 generator 时应清理 owned RNG。");
    }

    private void TestReplacingDefaultTerrainGeneratorDisposesOwnedPreviousInstance()
    {
        var runtime = new BattleRuntimeModule();
        BattleTerrainGenerator ownedGenerator = runtime.GetTerrainGenerator();
        var injectedGenerator = new BattleTerrainGenerator();

        runtime.setup(terrain_generator: injectedGenerator);

        _test.True(ownedGenerator.IsDisposed, "替换 caller-owned terrain generator 时，应先释放 runtime 自己创建的旧默认实例。");
        _test.True(!ownedGenerator.HasLiveOwnedRngForTests, "替换 runtime-owned generator 时应清理旧实例 RNG。");

        runtime.dispose();

        _test.True(!injectedGenerator.IsDisposed, "通过 setup 注入的 terrain generator 应保持 caller-owned。");
        _test.True(injectedGenerator.HasLiveOwnedRngForTests, "dispose runtime 后注入 generator 的 RNG 仍应由 caller 持有。");
        injectedGenerator.Dispose();
        _test.True(!injectedGenerator.HasLiveOwnedRngForTests, "caller 最终释放注入 generator 时应清理 RNG。");
    }

    private void TestInjectedTerrainGeneratorCanTransferOwnership()
    {
        var runtime = new BattleRuntimeModule();
        var injectedGenerator = new BattleTerrainGenerator();

        runtime.setup(
            terrain_generator: injectedGenerator,
            owns_terrain_generator: true
        );
        runtime.dispose();

        _test.True(injectedGenerator.IsDisposed, "owns_terrain_generator=true 时 runtime dispose 应释放注入 generator。");
        _test.True(!injectedGenerator.HasLiveOwnedRngForTests, "runtime 释放 transferred generator 时应清理 owned RNG。");
    }
}
