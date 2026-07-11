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
        new(@":\s*(?:Godot\s*\.\s*)?S" + @"ceneTree\b", RegexOptions.Compiled);
    private static readonly Regex DirectGcBarrierCallPattern =
        new(
            @"\b(?:System\s*\.\s*)?GC\s*\.\s*(?:Collect|WaitForPendingFinalizers|TryStartNoGCRegion|EndNoGCRegion)\s*\(",
            RegexOptions.Compiled
        );
    private static readonly Regex MigrationCompatibilityPattern =
        new(
            @"\b(?:GodotSharpCleanup|migrate_test_exit_calls|test_exit_migration_manifest)\b",
            RegexOptions.Compiled
        );
    private static readonly Regex DynamicAttributeModifierConstructionPattern =
        new(@"\bnew\s+AttributeModifier\b", RegexOptions.Compiled);
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
    private static readonly Regex LegacyEnemyBorrowerMarkerPattern =
        new(
            @"\b(?:ILegacyEnemyContentCatalog|EnemyContentSeed|Enemy[A-Za-z0-9_]*Def|EnemyAiAction|BattleAiScoreProfile|WildEncounterRoster[A-Za-z0-9_]*Def|DropEntryDef|BattleSimProfileDef)\b",
            RegexOptions.Compiled
        );

    private static readonly LifetimeDomain[] PhaseTwoLeaseDomains =
    {
        LifetimeDomain.Request,
        LifetimeDomain.Battle,
        LifetimeDomain.SceneTree,
    };

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
        AssertFinalizerBarrierCallersAreExact();
        AssertMigrationArtifactsAreDeleted();
        AssertTestExitComponentsAreNarrow();
        AssertPhaseTwoLeaseDomainsReturnToBaseline();
        AssertNoDynamicAttributeModifierConstruction();
        AssertExactLegacyDebt();
        AssertLifecycleStopgapCountersAreZero();
        AssertRawEnemyAuthoringInventory();
        AssertProductionQuitOwner();
        AssertTestAdapterHasNoQuit();
        AssertProcessExitDiagnosticsHaveNoGodotApi();
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

    private void AssertFinalizerBarrierCallersAreExact()
    {
        string projectRoot = ProjectSettings.GlobalizePath("res://");
        var allowedPaths = new HashSet<string>(
            new[]
            {
                "scripts/utils/GodotObjectLifecycle.cs",
                "tests/shared/LifecycleMeasurementBarrier.cs",
            },
            StringComparer.Ordinal
        );
        var discoveredPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (string sourceRoot in new[] { "scripts", "tests" })
        {
            string absoluteRoot = Path.Combine(projectRoot, sourceRoot);
            foreach (string filePath in Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(projectRoot, Path.GetFullPath(filePath))
                    .Replace('\\', '/');
                if (
                    relativePath
                    is "tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs"
                        or "tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs"
                )
                {
                    continue;
                }

                if (!DirectGcBarrierCallPattern.IsMatch(File.ReadAllText(filePath)))
                    continue;
                discoveredPaths.Add(relativePath);
                _test.True(
                    allowedPaths.Contains(relativePath),
                    $"direct GC barrier control is forbidden outside the two declared owners: {relativePath}"
                );
            }
        }

        _test.Eq(
            discoveredPaths.Count,
            allowedPaths.Count,
            "production shutdown and test measurement are the only direct GC barrier owners"
        );
        foreach (string allowedPath in allowedPaths)
        {
            _test.True(
                discoveredPaths.Contains(allowedPath),
                $"declared GC barrier owner remains active: {allowedPath}"
            );
        }
    }

    private void AssertMigrationArtifactsAreDeleted()
    {
        string projectRoot = ProjectSettings.GlobalizePath("res://");
        string[] deletedArtifacts =
        {
            "tools/migrate_test_exit_calls.py",
            "tests/tooling/test_migrate_test_exit_calls.py",
            "tests/tooling/test_exit_migration_manifest.txt",
            "tests/shared/GodotSharpCleanup.cs",
        };
        foreach (string relativePath in deletedArtifacts)
        {
            _test.False(
                File.Exists(Path.Combine(projectRoot, relativePath)),
                $"migration-only artifact must stay deleted: {relativePath}"
            );
        }

        foreach (string sourceRoot in new[] { "scripts", "tests", "tools" })
        {
            string absoluteRoot = Path.Combine(projectRoot, sourceRoot);
            if (!Directory.Exists(absoluteRoot))
                continue;
            foreach (string filePath in Directory.GetFiles(absoluteRoot, "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(filePath);
                if (
                    extension is not ".cs"
                    && extension is not ".py"
                    && extension is not ".txt"
                )
                {
                    continue;
                }
                string relativePath = Path.GetRelativePath(projectRoot, Path.GetFullPath(filePath))
                    .Replace('\\', '/');
                if (
                    relativePath
                    is "tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs"
                        or "tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs"
                )
                {
                    continue;
                }
                if (MigrationCompatibilityPattern.IsMatch(File.ReadAllText(filePath)))
                {
                    _test.Fail(
                        $"migration compatibility marker remains in project source: {relativePath}"
                    );
                }
            }
        }
    }

    private void AssertTestExitComponentsAreNarrow()
    {
        string adapterSource = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://tests/shared/TestExitCoordinator.cs")
        );
        _test.True(
            adapterSource.Contains("new ShutdownRequest(", StringComparison.Ordinal),
            "TestExitCoordinator maps results to a production ShutdownRequest"
        );
        _test.False(
            DirectGcBarrierCallPattern.IsMatch(adapterSource),
            "TestExitCoordinator does not run a finalizer barrier"
        );
        _test.False(
            DirectTestQuitPattern.IsMatch(adapterSource),
            "TestExitCoordinator does not call the tree exit API"
        );

        string harnessSource = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://tests/shared/TestHarness.cs")
        );
        _test.False(
            harnessSource.Contains("GodotTransientResourceScope", StringComparison.Ordinal)
                || harnessSource.Contains("TestResourceOwnership", StringComparison.Ordinal)
                || DirectGcBarrierCallPattern.IsMatch(harnessSource)
                || DirectTestQuitPattern.IsMatch(harnessSource),
            "TestHarness remains result aggregation only"
        );
    }

    private void AssertExactLegacyDebt()
    {
        IReadOnlyList<LifecycleLegacyDebtSnapshot> debt =
            LifecycleAuditRegistry.Shared.CaptureSnapshot().LegacyDebt;
        _test.Eq(debt.Count, 0, "Phase 5 permits no lifecycle legacy debt");
    }

    private void AssertLifecycleStopgapCountersAreZero()
    {
        LifecycleAuditSnapshot audit = LifecycleAuditRegistry.Shared.CaptureSnapshot();
        _test.Eq(
            audit.NormalPhaseSuppressCount,
            0L,
            "normal runtime paths perform no finalizer suppression"
        );
        _test.Eq(
            audit.QuarantineCount,
            0L,
            "runtime paths retain no quarantined wrapper graphs"
        );
    }

    private void AssertRawEnemyAuthoringInventory()
    {
        var borrowerOwners = new HashSet<string>(
            Array.Empty<string>(),
            StringComparer.Ordinal
        );
        var authoringAndProjectionOwners = new HashSet<string>(
            new[]
            {
                "scripts/systems/battle/ai/BattleAiScoreProfile.cs",
                "scripts/systems/battle/sim/BattleSimProfileDef.cs",
            },
            StringComparer.Ordinal
        );
        _test.Eq(
            borrowerOwners.Count,
            0,
            "runtime Enemy/AI borrower inventory is empty"
        );
        _test.Eq(
            authoringAndProjectionOwners.Count,
            2,
            "raw Enemy/AI authoring/projector inventory contains no duplicates"
        );
        foreach (string borrowerPath in borrowerOwners)
        {
            _test.False(
                authoringAndProjectionOwners.Contains(borrowerPath),
                $"raw content owner has exactly one debt role: {borrowerPath}"
            );
        }

        string projectRoot = ProjectSettings.GlobalizePath("res://");
        string scriptsRoot = ProjectSettings.GlobalizePath("res://scripts/systems");
        var discoveredRawOwners = new HashSet<string>(StringComparer.Ordinal);
        foreach (string filePath in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(filePath);
            if (!LegacyEnemyBorrowerMarkerPattern.IsMatch(source))
                continue;

            string relativePath = Path.GetRelativePath(projectRoot, Path.GetFullPath(filePath))
                .Replace('\\', '/');
            discoveredRawOwners.Add(relativePath);
            _test.True(
                borrowerOwners.Contains(relativePath)
                    || authoringAndProjectionOwners.Contains(relativePath),
                $"raw Enemy/AI runtime reference must be an exact Phase 4 authoring owner: {relativePath}"
            );
        }

        foreach (string borrowerPath in borrowerOwners)
        {
            _test.True(
                discoveredRawOwners.Contains(borrowerPath),
                $"declared runtime borrower still owns a raw Enemy/AI reference: {borrowerPath}"
            );
        }
        foreach (string ownerPath in authoringAndProjectionOwners)
        {
            _test.True(
                discoveredRawOwners.Contains(ownerPath),
                $"declared authoring/projector owner still belongs to the raw graph: {ownerPath}"
            );
        }
        _test.Eq(
            discoveredRawOwners.Count,
            borrowerOwners.Count + authoringAndProjectionOwners.Count,
            "reverse scan covers every raw Enemy/AI owner exactly once"
        );

    }

    private void AssertPhaseTwoLeaseDomainsReturnToBaseline()
    {
        foreach (LifetimeDomain domain in PhaseTwoLeaseDomains)
        {
            LifecycleAuditSnapshot projectionBaseline =
                LifecycleAuditRegistry.Shared.CaptureSnapshot();
            using (
                GodotProjectionLease<Godot.Collections.Dictionary> lease =
                    RuntimePlainPayload.ProjectDictionaryLease(
                        new Dictionary<string, object>
                        {
                            ["domain"] = domain.ToString(),
                            ["nested"] = new List<object>
                            {
                                1L,
                                new Dictionary<string, object> { ["value"] = true },
                            },
                        },
                        $"phase-two-boundary-{domain}",
                        domain,
                        "cumulative projection vector"
                    )
            )
            {
                LifecycleAuditSnapshot active =
                    LifecycleAuditRegistry.Shared.CaptureSnapshot();
                _test.Eq(
                    active.ActiveLeaseCount,
                    projectionBaseline.ActiveLeaseCount + 1,
                    $"{domain} projection opens exactly one lease"
                );
                _test.True(
                    active.ActiveOwnerCount > projectionBaseline.ActiveOwnerCount,
                    $"{domain} projection explicitly owns its container graph"
                );
                _test.Eq(
                    active.ActiveScopeCount,
                    projectionBaseline.ActiveScopeCount,
                    $"{domain} projection does not expose its internal owner as a native scope"
                );
            }
            AssertActiveVector(
                projectionBaseline,
                LifecycleAuditRegistry.Shared.CaptureSnapshot(),
                $"{domain} projection close"
            );

            LifecycleAuditSnapshot scopeBaseline =
                LifecycleAuditRegistry.Shared.CaptureSnapshot();
            using (var scope = new NativeLeaseScope($"phase-two-boundary-{domain}", domain))
            {
                scope.Own(
                    new Godot.Collections.Dictionary(),
                    "cumulative native owner vector"
                );
                LifecycleAuditSnapshot active =
                    LifecycleAuditRegistry.Shared.CaptureSnapshot();
                _test.Eq(
                    active.ActiveOwnerCount,
                    scopeBaseline.ActiveOwnerCount + 1,
                    $"{domain} native scope registers one explicit owner"
                );
                _test.Eq(
                    active.ActiveScopeCount,
                    scopeBaseline.ActiveScopeCount + 1,
                    $"{domain} native scope registers one scope"
                );
            }
            AssertActiveVector(
                scopeBaseline,
                LifecycleAuditRegistry.Shared.CaptureSnapshot(),
                $"{domain} native scope close"
            );
        }
    }

    private void AssertNoDynamicAttributeModifierConstruction()
    {
        string scriptsRoot = ProjectSettings.GlobalizePath("res://scripts");
        List<string> matches = FindSourceMatches(
            scriptsRoot,
            DynamicAttributeModifierConstructionPattern
        );
        _test.Eq(
            matches.Count,
            0,
            matches.Count == 0
                ? "production constructs no runtime AttributeModifier Resource"
                : $"production constructs runtime AttributeModifier Resource in {string.Join(", ", matches)}"
        );
    }

    private void AssertActiveVector(
        LifecycleAuditSnapshot expected,
        LifecycleAuditSnapshot actual,
        string label
    )
    {
        _test.Eq(actual.ActiveContentBorrowerCount, expected.ActiveContentBorrowerCount, $"{label}: borrowers");
        _test.Eq(actual.ActiveOwnerCount, expected.ActiveOwnerCount, $"{label}: owners");
        _test.Eq(actual.ActiveLeaseCount, expected.ActiveLeaseCount, $"{label}: leases");
        _test.Eq(actual.ActiveScopeCount, expected.ActiveScopeCount, $"{label}: scopes");
        _test.Eq(actual.ActiveJobCount, expected.ActiveJobCount, $"{label}: jobs");
        _test.Eq(actual.ViolationCount, expected.ViolationCount, $"{label}: violations");
        _test.Eq(
            actual.NormalPhaseSuppressCount,
            expected.NormalPhaseSuppressCount,
            $"{label}: normal suppressions"
        );
        _test.Eq(actual.QuarantineCount, expected.QuarantineCount, $"{label}: quarantine");
        _test.Eq(
            actual.ActiveCountsByDomain.Count,
            expected.ActiveCountsByDomain.Count,
            $"{label}: active domain count"
        );
        foreach (KeyValuePair<string, int> entry in expected.ActiveCountsByDomain)
        {
            _test.True(
                actual.ActiveCountsByDomain.TryGetValue(entry.Key, out int actualCount),
                $"{label}: active domain remains present: {entry.Key}"
            );
            if (actual.ActiveCountsByDomain.TryGetValue(entry.Key, out actualCount))
                _test.Eq(actualCount, entry.Value, $"{label}: active domain {entry.Key}");
        }
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
