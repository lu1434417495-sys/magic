using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Godot;

public partial class run_runtime_lifecycle_boundary_regression : SceneTree
{
    private static readonly Regex LocalFinalizerDrainCallPattern =
        new(
            @"(?m)^[\t ]*(?:GodotSharpCleanup|GodotObjectLifecycle)"
                + @"\.CollectPendingFinalizers\(\);",
            RegexOptions.Compiled
        );
    private static readonly Regex DirectSuccessfulQuitPattern =
        new(@"(?m)^[\t ]*Quit\(0\);", RegexOptions.Compiled);

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

        AssertFinalizerDrainOnlyUsesCentralizedExitPath();
        AssertSuccessfulQuitUsesCentralizedExitPath();
        Quit(_test.Finish("Runtime lifecycle boundary regression"));
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

    private void AssertFinalizerDrainOnlyUsesCentralizedExitPath()
    {
        string testsRoot = ProjectSettings.GlobalizePath("res://tests");
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(Path.Combine(testsRoot, "shared", "GodotSharpCleanup.cs")),
            Path.GetFullPath(Path.Combine(testsRoot, "shared", "TestHarness.cs")),
        };

        foreach (string filePath in Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(filePath);
            if (allowedFiles.Contains(fullPath))
                continue;

            string source = File.ReadAllText(fullPath);
            if (LocalFinalizerDrainCallPattern.IsMatch(source))
            {
                _test.Fail(
                    $"Finalizer drain must stay centralized in TestHarness.Finish, but {Path.GetRelativePath(testsRoot, fullPath)} calls CollectPendingFinalizers()."
                );
            }
        }
    }

    private void AssertSuccessfulQuitUsesCentralizedExitPath()
    {
        string testsRoot = ProjectSettings.GlobalizePath("res://tests");

        foreach (string filePath in Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(filePath);
            string source = File.ReadAllText(fullPath);
            if (!DirectSuccessfulQuitPattern.IsMatch(source))
                continue;

            _test.Fail(
                $"Successful regression exit must go through TestHarness.Finish, but {Path.GetRelativePath(testsRoot, fullPath)} calls Quit(0)."
            );
        }
    }
}
