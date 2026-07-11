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
    private static readonly Regex DirectQuarantineRetainPattern =
        new(
            @"\bGodotTestRuntimeQuarantine\s*\.\s*R" + @"etain\s*\(",
            RegexOptions.Compiled
        );
    private static readonly Regex DynamicAttributeModifierConstructionPattern =
        new(@"\bnew\s+AttributeModifier\b", RegexOptions.Compiled);
    private static readonly Regex RuntimeStateLifecycleCallPattern =
        new(@"\bRuntimeStateLifecycle\s*\.", RegexOptions.Compiled);
    private static readonly Regex DeferredProcessContentPoolPattern =
        new(@"\bStaticStrongWrappers\b", RegexOptions.Compiled);
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

    // These files have a complete Phase 2 owner/surface migration. Shared files with
    // unrelated Phase 5 cleanup work (RuntimePlainPayload, AiTraceRecorder,
    // BattleState, and BattleSimFormalCombatFixture) are intentionally not listed.
    private static readonly string[] PhaseTwoRuntimeStateFreeSurfaces =
    {
        "scripts/utils/NativeLeaseScope.cs",
        "scripts/utils/GodotProjectionLease.cs",
        "scripts/utils/RuntimeResourceFactories.cs",
        "scripts/player/progression/AttributeModifierDefinition.cs",
        "scripts/player/progression/AttributeModifier.cs",
        "scripts/player/progression/SkillDefinition.cs",
        "scripts/player/progression/DerivedAttributeRule.cs",
        "scripts/systems/attributes/AttributeSourceContext.cs",
        "scripts/systems/attributes/AttributeService.cs",
        "scripts/systems/inventory/PartyEquipmentService.cs",
        "scripts/systems/progression/CharacterTraitService.cs",
        "scripts/systems/progression/CharacterManagementModule.cs",
        "scripts/systems/settlement/SettlementForgeService.cs",
        "scripts/systems/battle/core/special_profiles/BattleSpecialProfileManifestValidator.cs",
        "scripts/systems/progression/CharacterCreationService.cs",
        "scripts/enemies/EnemyTemplateDef.cs",
        "scripts/systems/persistence/SaveSerializer.cs",
        "scripts/systems/persistence/SaveRepository.cs",
        "scripts/systems/persistence/FileIOCoordinator.cs",
        "scripts/systems/persistence/GameSession.cs",
        "scripts/systems/persistence/GameSession.SaveIndexAndFileIO.cs",
        "scripts/systems/battle/core/BattleEventBatchProjection.cs",
        "scripts/systems/battle/core/BattlePreviewProjection.cs",
        "scripts/systems/battle/core/BattleEventBatch.cs",
        "scripts/systems/battle/core/BattlePreview.cs",
        "scripts/systems/battle/core/BattleSaveBranchPreviewData.cs",
        "scripts/systems/battle/rules/BattleDamagePreviewProjection.cs",
        "scripts/systems/battle/rules/BattleDamagePreviewRangeProjection.cs",
        "scripts/systems/battle/ai/BattleAiTurnTracePayloadProjection.cs",
        "scripts/systems/battle/ai/BattleAiTurnTraceProjection.cs",
        "scripts/enemies/TraceDictionaryProjection.cs",
        "scripts/systems/battle/runtime/BattleRuntimeModule.AiTrace.cs",
        "scripts/systems/battle/presentation/BattleHudSnapshot.cs",
        "scripts/systems/battle/presentation/BattleHoverSnapshot.cs",
        "scripts/systems/battle/presentation/BattleHudAdapter.cs",
        "scripts/ui/BattleMapPanel.cs",
        "scripts/ui/BattleHoverPreviewOverlay.cs",
        "scripts/ui/BattleBoardRenderProfile.cs",
        "scripts/ui/BattleBoardController.cs",
        "scripts/ui/BattleBoard2D.cs",
        "scripts/systems/battle/ai/BattleAiDecisionResult.cs",
        "scripts/systems/battle/ai/BattleAiMutationSnapshot.cs",
        "scripts/systems/battle/ai/BattleAiContext.cs",
        "scripts/systems/battle/ai/BattleAiService.cs",
        "scripts/systems/battle/ai/BattleAiScoreService.cs",
        "scripts/systems/battle/ai/BattleAiRuntimeActionPlan.cs",
        "scripts/systems/battle/ai/BattleAiMutationGuard.cs",
        "scripts/systems/battle/runtime/BattleRuntimeServices.cs",
        "scripts/systems/battle/runtime/BattleRuntimeModule.cs",
        "scripts/systems/battle/runtime/BattleRuntimeModule.ContentSync.cs",
    };

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
        AssertPhaseTwoLeaseDomainsReturnToBaseline();
        AssertNoDynamicAttributeModifierConstruction();
        AssertPhaseTwoRuntimeStateFreeSurfaces();
        AssertExactLegacyDebt();
        AssertExactProductionQuarantine();
        AssertDeferredProcessContentPoolIsIsolated();
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

        _test.Eq(debt.Count, 0, "Phase 2 permits no declared lifecycle legacy debt");
        _test.False(
            GodotLifecycleLegacyDebtManifest.IsDeclared(
                new LifecycleLegacyDebtSnapshot(
                    "undeclared-probe",
                    "tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs",
                    "Request",
                    5
                )
            ),
            "the legacy debt manifest cannot authorize a new quarantine"
        );
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
            0,
            "Phase 2 permits no production legacy quarantine call site"
        );

        _test.Eq(
            FindSourceMatches(scriptsRoot, ProductionTestQuarantineFactoryPattern).Count,
            0,
            "production cannot invoke the test quarantine factory"
        );

        List<string> directRetainSites = FindSourceMatches(
            scriptsRoot,
            DirectQuarantineRetainPattern
        );
        _test.Eq(
            directRetainSites.Count,
            1,
            "production contains exactly one direct quarantine retain call"
        );
        if (directRetainSites.Count == 1)
        {
            _test.Eq(
                directRetainSites[0],
                "utils/GodotObjectOwnership.cs",
                "the ownership bridge contains the only direct quarantine retain call"
            );
        }

        _test.Eq(
            LifecycleAuditRegistry.Shared.CaptureSnapshot().QuarantineCount,
            0L,
            "Phase 2 production execution does not retain any runtime quarantine"
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

    private void AssertPhaseTwoRuntimeStateFreeSurfaces()
    {
        string projectRoot = ProjectSettings.GlobalizePath("res://");
        foreach (string relativePath in PhaseTwoRuntimeStateFreeSurfaces)
        {
            string filePath = Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)
            );
            _test.True(File.Exists(filePath), $"Phase 2 surface exists: {relativePath}");
            if (!File.Exists(filePath))
                continue;

            _test.False(
                RuntimeStateLifecycleCallPattern.IsMatch(File.ReadAllText(filePath)),
                $"fully migrated Phase 2 surface cannot call RuntimeStateLifecycle: {relativePath}"
            );
        }
    }

    private void AssertDeferredProcessContentPoolIsIsolated()
    {
        string scriptsRoot = ProjectSettings.GlobalizePath("res://scripts");
        List<string> matches = FindSourceMatches(
            scriptsRoot,
            DeferredProcessContentPoolPattern
        );
        _test.True(
            matches.Count > 0,
            "the deferred process-content static pool remains visible until the content phases"
        );
        foreach (string match in matches)
        {
            _test.Eq(
                match,
                "utils/GodotObjectOwnership.cs",
                "deferred StaticStrongWrappers retention is isolated to process-content ownership"
            );
        }
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
