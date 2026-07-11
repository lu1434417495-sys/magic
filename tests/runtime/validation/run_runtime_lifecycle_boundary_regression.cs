using System;
using System.Collections.Generic;
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
    private static readonly Regex ProductionQuarantinePattern =
        new(@"quarantineOnDrain\s*:\s*true", RegexOptions.Compiled);
    private static readonly Regex DeclaredDebtArgumentPattern =
        new(
            @"legacyDebt\s*:\s*GodotLifecycleLegacyDebtManifest\.BattleBoardControllerQuarantine\b",
            RegexOptions.Compiled
        );
    private static readonly Regex ProcessExitGodotApiPattern =
        new(
            @"\b(?:using\s+Godot|Godot\.|GD\s*\.|Engine\s*\.|GodotObject\s*\.|SceneTree\s*\.|GetTree\s*\(|GetNode(?:OrNull)?\s*\()",
            RegexOptions.Compiled
        );

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
        AssertExactLegacyDebt();
        AssertExactProductionQuarantine();
        AssertProductionQuitOwner();
        AssertTestAdapterHasNoQuit();
        AssertProcessExitDiagnosticsHaveNoGodotApi();
        AssertNormalSessionSuppressCountIsZero();
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

    private void AssertExactLegacyDebt()
    {
        IReadOnlyList<LifecycleLegacyDebtSnapshot> debt =
            LifecycleAuditRegistry.Shared.CaptureSnapshot().LegacyDebt;

        _test.Eq(debt.Count, 1, "phase 1 permits exactly one lifecycle legacy debt record");
        if (debt.Count != 1)
            return;

        _test.Eq(
            debt[0].DebtId,
            "battle-board-controller-quarantine",
            "legacy debt ID is exact"
        );
        _test.Eq(
            debt[0].Source,
            "scripts/ui/BattleBoardController.cs",
            "legacy debt source is exact"
        );
        _test.Eq(debt[0].OwnerDomain, "SceneTree", "legacy debt owner domain is exact");
        _test.Eq(debt[0].DeletePhase, 2, "legacy debt deletion phase is exact");
    }

    private void AssertExactProductionQuarantine()
    {
        string scriptsRoot = ProjectSettings.GlobalizePath("res://scripts");
        List<string> quarantineSites = FindSourceMatches(
            scriptsRoot,
            ProductionQuarantinePattern
        );

        _test.Eq(
            quarantineSites.Count,
            1,
            "phase 1 permits exactly one production quarantine call site"
        );
        if (quarantineSites.Count != 1)
            return;

        _test.Eq(
            quarantineSites[0],
            "ui/BattleBoardController.cs",
            "production quarantine remains confined to BattleBoardController"
        );

        string battleBoardSource = File.ReadAllText(
            Path.Combine(scriptsRoot, "ui", "BattleBoardController.cs")
        );
        _test.True(
            DeclaredDebtArgumentPattern.IsMatch(battleBoardSource),
            "the production quarantine call site names its exact legacy debt metadata"
        );
    }

    private void AssertProductionQuitOwner()
    {
        string scriptsRoot = ProjectSettings.GlobalizePath("res://scripts");
        List<string> quitSites = FindSourceMatches(scriptsRoot, DirectTestQuitPattern);

        _test.Eq(quitSites.Count, 1, "production C# has exactly one SceneTree quit call");
        if (quitSites.Count == 1)
        {
            _test.Eq(
                quitSites[0],
                "systems/lifecycle/ApplicationLifetimeCoordinator.cs",
                "ApplicationLifetimeCoordinator is the only production quit owner"
            );
        }
    }

    private void AssertTestAdapterHasNoQuit()
    {
        string source = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://tests/shared/TestExitCoordinator.cs")
        );
        _test.False(
            DirectTestQuitPattern.IsMatch(source),
            "TestExitCoordinator delegates exit and does not call the tree exit API"
        );
    }

    private void AssertProcessExitDiagnosticsHaveNoGodotApi()
    {
        string source = File.ReadAllText(
            ProjectSettings.GlobalizePath(
                "res://scripts/systems/lifecycle/ApplicationLifetimeDiagnostics.cs"
            )
        );
        _test.False(
            ProcessExitGodotApiPattern.IsMatch(source),
            "ProcessExit diagnostics must not import or call Godot APIs"
        );
    }

    private void AssertNormalSessionSuppressCountIsZero()
    {
        _test.Eq(
            LifecycleAuditRegistry.Shared.CaptureSnapshot().NormalPhaseSuppressCount,
            0L,
            "normal session paths do not suppress process content wrappers"
        );
    }

    private static List<string> FindSourceMatches(string root, Regex pattern)
    {
        var matches = new List<string>();
        foreach (string filePath in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(filePath);
            int matchCount = pattern.Matches(source).Count;
            string relativePath = Path.GetRelativePath(root, Path.GetFullPath(filePath))
                .Replace('\\', '/');
            for (int index = 0; index < matchCount; index++)
                matches.Add(relativePath);
        }
        return matches;
    }
}
