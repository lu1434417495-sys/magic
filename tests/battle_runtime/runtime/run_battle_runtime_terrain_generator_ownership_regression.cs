using Godot;

public partial class run_battle_runtime_terrain_generator_ownership_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestRuntimeDisposesDefaultTerrainGenerator();
        TestInjectedTerrainGeneratorRemainsCallerOwned();
        TestReplacingDefaultTerrainGeneratorDisposesOwnedPreviousInstance();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Battle runtime terrain generator ownership regression"));
    }

    private void TestRuntimeDisposesDefaultTerrainGenerator()
    {
        var runtime = new BattleRuntimeModule();
        BattleTerrainGenerator ownedGenerator = runtime.GetTerrainGenerator();

        runtime.dispose();

        _test.True(
            !GodotObject.IsInstanceValid(ownedGenerator),
            "BattleRuntimeModule 应释放自己创建的默认 terrain generator。"
        );
    }

    private void TestInjectedTerrainGeneratorRemainsCallerOwned()
    {
        var runtime = new BattleRuntimeModule();
        var injectedGenerator = new BattleTerrainGenerator();

        runtime.setup(terrain_generator: injectedGenerator);
        runtime.dispose();

        _test.True(
            GodotObject.IsInstanceValid(injectedGenerator),
            "外部注入的 terrain generator 应由 caller 释放，runtime dispose 不应释放它。"
        );
        injectedGenerator.Dispose();
    }

    private void TestReplacingDefaultTerrainGeneratorDisposesOwnedPreviousInstance()
    {
        var runtime = new BattleRuntimeModule();
        BattleTerrainGenerator ownedGenerator = runtime.GetTerrainGenerator();
        var injectedGenerator = new BattleTerrainGenerator();

        runtime.setup(terrain_generator: injectedGenerator);

        _test.True(
            !GodotObject.IsInstanceValid(ownedGenerator),
            "替换 caller-owned terrain generator 时，应先释放 runtime 自己创建的旧默认实例。"
        );

        runtime.dispose();

        _test.True(
            GodotObject.IsInstanceValid(injectedGenerator),
            "通过 setup 注入的 terrain generator 应保持 caller-owned。"
        );
        injectedGenerator.Dispose();
    }
}
