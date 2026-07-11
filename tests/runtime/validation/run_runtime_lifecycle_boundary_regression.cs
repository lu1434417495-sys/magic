using System;
using System.IO;
using System.Text.RegularExpressions;
using Godot;

public partial class run_runtime_lifecycle_boundary_regression : LifecycleTestSceneTree
{
    private static readonly Regex TestLocalFinalizerControlPattern =
        new(
            @"\b(?:TryStartNo" + @"GCRegion|CollectPending" + @"Finalizers)\s*\(",
            RegexOptions.Compiled
        );
    private static readonly Regex DirectTestQuitPattern =
        new(@"\bQ" + @"uit\s*\(", RegexOptions.Compiled);
    private static readonly Regex DirectSceneTreeBasePattern =
        new(@":\s*S" + @"ceneTree\b", RegexOptions.Compiled);

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        AssertPlainRuntimeService<GameRoot>();
        AssertPlainRuntimeService<GameRuntimeFacade>();
        AssertPlainRuntimeService<BattleSessionFacade>();
        AssertPlainRuntimeService<GameRuntimeBattleSelection>();
        AssertPlainRuntimeService<GameRuntimeSettlementCommandHandler>();
        AssertPlainRuntimeService<EncounterRosterBuilder>();
        AssertPlainRuntimeService<BattleRuntimeModule>();
        AssertPlainRuntimeService<BattleGridService>();
        AssertPlainRuntimeService<BattleTerrainGenerator>();
        AssertPlainRuntimeService<BattleDamageResolver>();
        AssertPlainRuntimeService<BattleHitResolver>();
        AssertPlainRuntimeService<BattleFateEventBus>();
        AssertPlainRuntimeService<BattleSimFormalCombatFixture>();
        AssertPlainRuntimeService<CharacterManagementModule>();
        AssertPlainRuntimeService<HeadlessGameTestSession>();
        AssertPlainRuntimeService<GameTextCommandRunner>();
        AssertPlainRuntimeService<GameTextCommandResult>();

        AssertNoTestLocalFinalizerControl();
        AssertNoDirectTestQuit();
        AssertConcreteRunnersUseLifecycleBase();
        RequestTestExit(_test.Finish("Runtime lifecycle boundary regression"));
    }

    private void AssertPlainRuntimeService<T>()
    {
        Type type = typeof(T);
        _test.True(
            typeof(IDisposable).IsAssignableFrom(type),
            $"{type.Name} should expose explicit CLR disposal."
        );
        _test.False(
            typeof(GodotObject).IsAssignableFrom(type),
            $"{type.Name} is a runtime service/helper and must not own a native Godot wrapper."
        );
    }

    private void AssertNoTestLocalFinalizerControl()
    {
        string testsRoot = ProjectSettings.GlobalizePath("res://tests");

        foreach (string filePath in Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(filePath);
            string source = File.ReadAllText(fullPath);
            if (TestLocalFinalizerControlPattern.IsMatch(source))
            {
                _test.Fail(
                    $"Tests must delegate finalizer control to the application shutdown coordinator, but {Path.GetRelativePath(testsRoot, fullPath)} contains a test-local control call."
                );
            }
        }
    }

    private void AssertNoDirectTestQuit()
    {
        string testsRoot = ProjectSettings.GlobalizePath("res://tests");

        foreach (string filePath in Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(filePath);
            string source = File.ReadAllText(fullPath);
            if (!DirectTestQuitPattern.IsMatch(source))
                continue;

            _test.Fail(
                $"Regression exits must go through the lifecycle coordinator, but {Path.GetRelativePath(testsRoot, fullPath)} calls the tree exit API directly."
            );
        }
    }

    private void AssertConcreteRunnersUseLifecycleBase()
    {
        string testsRoot = ProjectSettings.GlobalizePath("res://tests");
        string sharedBasePath = Path.GetFullPath(
            Path.Combine(testsRoot, "shared", "LifecycleTestSceneTree.cs")
        );

        foreach (string filePath in Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(filePath);
            if (string.Equals(fullPath, sharedBasePath, StringComparison.OrdinalIgnoreCase))
                continue;

            string source = File.ReadAllText(fullPath);
            if (!DirectSceneTreeBasePattern.IsMatch(source))
                continue;

            _test.Fail(
                $"Concrete C# runners must derive through LifecycleTestSceneTree, but {Path.GetRelativePath(testsRoot, fullPath)} derives from the engine tree directly."
            );
        }
    }
}
