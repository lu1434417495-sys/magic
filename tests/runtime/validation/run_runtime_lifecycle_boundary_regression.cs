using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
    private static readonly Regex ProductionLegacyQuarantineFactoryPattern =
        new(
            @"\bGodotTransientResourceScope\s*\.\s*CreateLegacyQuarantine\s*\(",
            RegexOptions.Compiled
        );
    private static readonly Regex ProductionTestQuarantineFactoryPattern =
        new(
            @"\bGodotTransientResourceScope\s*\.\s*CreateTestQuarantine\s*\(",
            RegexOptions.Compiled
        );
    private static readonly Regex BattleBoardLegacyFactoryPattern =
        new(
            @"GodotTransientResourceScope\.CreateLegacyQuarantine\s*\(\s*""BattleBoardController""\s*,\s*GodotLifecycleLegacyDebtManifest\.BattleBoardControllerQuarantine\s*\)",
            RegexOptions.Compiled | RegexOptions.Singleline
        );
    private static readonly Regex DirectQuarantineRetainPattern =
        new(
            @"\bGodotTestRuntimeQuarantine\s*\.\s*Retain\s*\(",
            RegexOptions.Compiled
        );
    private static readonly Regex ProductionSceneTreeQuitPattern =
        new(
            @"\bGetTree\s*\(\s*\)\s*\.\s*Q" + @"uit\s*\(",
            RegexOptions.Compiled
        );
    private static readonly Regex ProcessExitGodotApiPattern =
        new(
            @"\b(?:using\s+Godot|Godot\.|GD\s*\.|Engine\s*\.|GodotObject\s*\.|SceneTree\s*\.|GetTree\s*\(|GetNode(?:OrNull)?\s*\()",
            RegexOptions.Compiled
        );
    private static readonly Regex NormalSuppressRunningGatePattern =
        new(
            @"ApplicationLifetimeDiagnostics\.CurrentPhase\s*==\s*ApplicationShutdownPhase\.Running",
            RegexOptions.Compiled
        );
    private static readonly Regex NormalSuppressRecordPattern =
        new(
            @"LifecycleAuditRegistry\.Shared\.RecordNormalPhaseSuppress\s*\(",
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
        AssertQuarantineFactorySurface();
        AssertProductionQuitOwner();
        AssertTestAdapterHasNoQuit();
        AssertProcessExitDiagnosticsHaveNoGodotApi();
        AssertNormalSuppressInstrumentation();
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
            ProductionLegacyQuarantineFactoryPattern
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
            BattleBoardLegacyFactoryPattern.IsMatch(battleBoardSource),
            "the production quarantine factory receives its exact legacy debt metadata"
        );

        _test.Eq(
            FindSourceMatches(scriptsRoot, ProductionTestQuarantineFactoryPattern).Count,
            0,
            "production cannot invoke the test quarantine factory"
        );

        string ownershipSource = File.ReadAllText(
            Path.Combine(scriptsRoot, "utils", "GodotObjectOwnership.cs")
        );
        _test.Eq(
            DirectQuarantineRetainPattern.Matches(ownershipSource).Count,
            1,
            "the ownership bridge contains the only direct quarantine retain call"
        );
    }

    private void AssertQuarantineFactorySurface()
    {
        ConstructorInfo[] constructors = typeof(GodotTransientResourceScope).GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );
        int callableConstructorCount = 0;
        foreach (ConstructorInfo constructor in constructors)
        {
            if (constructor.IsPrivate)
                continue;

            callableConstructorCount++;
            ParameterInfo[] parameters = constructor.GetParameters();
            _test.True(
                parameters.Length == 1 && parameters[0].ParameterType == typeof(string),
                "the only callable transient scope constructor is the normal one-argument path"
            );
        }
        _test.Eq(
            callableConstructorCount,
            1,
            "quarantine cannot be enabled through a positional constructor argument"
        );
    }

    private void AssertProductionQuitOwner()
    {
        string scriptsRoot = ProjectSettings.GlobalizePath("res://scripts");
        List<string> quitSites = FindSourceMatches(
            scriptsRoot,
            ProductionSceneTreeQuitPattern
        );

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
        string callbackBody = ExtractMethodBody(
            source,
            "internal static void RecordProcessExit"
        );
        _test.True(
            callbackBody.Length > 0,
            "ProcessExit diagnostics callback remains discoverable by the boundary gate"
        );
        _test.False(
            ProcessExitGodotApiPattern.IsMatch(callbackBody),
            "ProcessExit diagnostics must not import or call Godot APIs"
        );
    }

    private void AssertNormalSuppressInstrumentation()
    {
        string source = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://scripts/utils/GodotObjectOwnership.cs")
        );
        string methodBody = ExtractMethodBody(
            source,
            "internal static void SuppressBorrowedContentForProcessExit"
        );
        _test.True(
            NormalSuppressRunningGatePattern.IsMatch(methodBody),
            "process-only content suppression detects an unexpected Running-phase call"
        );
        _test.Eq(
            NormalSuppressRecordPattern.Matches(methodBody).Count,
            1,
            "unexpected normal-phase process suppression records exactly one audit event"
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

    private static string ExtractMethodBody(string source, string signature)
    {
        int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        if (signatureIndex < 0)
            return string.Empty;

        int openingBrace = source.IndexOf('{', signatureIndex);
        if (openingBrace < 0)
            return string.Empty;

        int depth = 0;
        for (int index = openingBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}')
                depth--;

            if (depth == 0)
                return source.Substring(openingBrace, index - openingBrace + 1);
        }
        return string.Empty;
    }
}
