using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        new(@"\.\s*Q" + @"uit\s*\(", RegexOptions.Compiled);
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
    private static readonly Regex LegacyEnemyCatalogPattern =
        new(
            @"\b(?:ILegacyEnemyContentCatalog|LegacyEnemyContentRegistry)\b|ProcessContentHost\s*\.\s*LegacyEnemyContentRegistry",
            RegexOptions.Compiled
        );
    private static readonly Regex ProductionTypeDeclarationPattern =
        new(
            @"\b(?:class|struct|interface|record(?:\s+(?:class|struct))?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled
        );

    // These types exist only inside the synchronous content-build scope. The classification
    // permits authored Resource method signatures; their fields are still scanned and require
    // an exact member exception below.
    private static readonly HashSet<string> SynchronousAuthoringOwnerAllowlist =
        new(
            new[]
            {
                "AgeContentRegistry",
                "AscensionContentRegistry",
                "BarrierContentRegistry",
                "BattleEncounterContentRegistry",
                "BattleSpecialProfileManifestValidator",
                "BattleSpecialProfileRegistry",
                "BloodlineContentRegistry",
                "ContentValidationRunner",
                "ContingencyTemplateContentRegistry",
                "EnemyContentRegistry",
                "EquipmentAbilityContentRegistry",
                "EquipmentAbilityBindingValidator",
                "EquipmentAbilityDefinitionProjection",
                "EquipmentAbilityPayloadValidators",
                "EquipmentAbilityStatusDeclarationCatalog",
                "FaithContentRegistry",
                "IdentityContentRegistryBase",
                "IdentityDefinitionProjection",
                "ItemContentRegistry",
                "ProfessionContentRegistry",
                "ProgressionContentRegistry",
                "QuestContentRegistry",
                "RaceContentRegistry",
                "RecipeContentRegistry",
                "SkillCombatProfileValidator",
                "SkillContentRegistry",
                "SkillDamageEffectValidator",
                "SkillExecuteEffectValidator",
                "SkillLevelDescriptionContentRules",
                "StageAdvancementContentRegistry",
                "SubraceContentRegistry",
                "TraitContentRegistry",
            },
            StringComparer.Ordinal
        );

    private static readonly HashSet<string> RuntimeDefinitionProjectionMemberAllowlist =
        new(
            new[]
            {
                "AgeProfileDefinition.FromResource",
                "AgeStageRuleDefinition.FromResource",
                "AscensionDefinition.FromResource",
                "AscensionStageDefinition.FromResource",
                "AttributeModifierDefinition.FromResource",
                "AttributeRequirementDefinition.FromResource",
                "BarrierLayerDefinition.FromResource",
                "BarrierOutcomeDefinition.FromResource",
                "BarrierProfileDefinition.FromResource",
                "BattleAiScoreProfileDefinition.FromResource",
                "BloodlineDefinition.FromResource",
                "BloodlineStageDefinition.FromResource",
                "CombatCastVariantDefinition.CopyEffectDefinitions",
                "CombatCastVariantDefinition.FromResource",
                "CombatDamageSegmentDefinition.FromResource",
                "CombatDamageSegmentDefinition.ProjectArray",
                "CombatEffectDefinition.FromResource",
                "CombatEffectDefinition.ProjectEquipmentDurabilitySlotWeights",
                "CombatSkillDefinition.FromResource",
                "CombatSkillDefinition.ProjectCastVariants",
                "CombatSkillDefinition.ProjectEffectDefinitions",
                "CombatTargetDamageMultiplierRuleDefinition.FromResource",
                "CombatTargetDamageMultiplierRuleDefinition.ProjectArray",
                "ContingencyAutomationDefinition.FromResource",
                "ContingencySetupTemplateDefinition.FromResource",
                "EnemyAiActionDefinition.FromResource",
                "EnemyTemplateDefinition.FromResource",
                "EquipmentAttributeRequirementDefinition.FromResource",
                "EquipmentRequirementDefinition.FromResource",
                "FacilityDefinition.FromResource",
                "FacilityNpcDefinition.FromResource",
                "FacilitySlotDefinition.FromResource",
                "FaithDeityDefinition.FromResource",
                "FaithRankDefinition.FromResource",
                "ItemDefinition.FromResource",
                "MeteorSwarmImpactComponentData.FromResource",
                "MeteorSwarmProfileData.FromResource",
                "MountedSubmapDefinition.FromResource",
                "ProfessionActiveConditionDefinition.FromResource",
                "ProfessionDefinition.FromResource",
                "ProfessionGrantedSkillDefinition.FromResource",
                "ProfessionPromotionRequirementDefinition.FromResource",
                "ProfessionRankGateDefinition.FromResource",
                "ProfessionRankRequirementDefinition.FromResource",
                "WorldGenerationDefinition+ProjectionContext.LoadRequired",
                "WorldGenerationDefinition+ProjectionContext.ProjectGeneration",
                "WorldGenerationDefinition+ProjectionContext.ProjectMountedSubmaps",
                "QuestDefinition.FromResource",
                "RaceDefinition.FromResource",
                "RacialGrantedSkillDefinition.FromResource",
                "RecipeDefinition.FromResource",
                "ReputationRequirementDefinition.FromResource",
                "SettlementDefinition.FromResource",
                "SettlementDistributionDefinition.FromResource",
                "SkillDefinition.FromResource",
                "SkillDefinition.ProjectAttributeModifiers",
                "SkillDefinition.ProjectIndex",
                "StageAdvancementDefinition.FromResource",
                "SubraceDefinition.FromResource",
                "TagRequirementDefinition.FromResource",
                "TraitDamageResistanceEntryDefinition.FromResource",
                "TraitDefinition.FromResource",
                "TraitPassiveStatusEffectDefinition.FromResource",
                "TraitRollGroupDefinition.FromResource",
                "TraitRollGroupEntryDefinition.FromResource",
                "TraitRollValueSchemaEntryDefinition.FromResource",
                "TraitSaveBonusEntryDefinition.FromResource",
                "WeaponDamageDiceDefinition.FromResource",
                "WeaponProfileDefinition.FromResource",
                "WeightedFacilityDefinition.FromResource",
                "WildSpawnRuleDefinition.FromResource",
                "WorldEventDefinition.FromResource",
                "WorldGenerationDefinition.FromResource",
                "WorldMapSettlementBundleDefinition.FromResource",
                "WorldMapSettlementNamePoolDefinition.FromResource",
                "WorldMapWildSpawnBundleDefinition.FromResource",
            },
            StringComparer.Ordinal
        );

    private static readonly HashSet<string> ExplicitRawBoundaryMemberAllowlist =
        new(
            new[]
            {
                "BattleBoardController.OwnRenderResource",
                "BattleBoardController.OwnsRenderResource",
                "BattleEncounterContentRegistry._authored",
                "BattleSpecialProfileRegistry._manifestsByProfileId",
                "BattleSpecialProfileRuntimeView.ForMeteorSwarm",
                "EnemyContentRegistry._enemy_ai_brains",
                "EnemyContentRegistry._enemy_templates",
                "EnemyContentRegistry._wild_encounter_rosters",
                "EngineAssetAccess.ResolveBorrowed",
                "EngineAssetResolver._assets",
                "EngineAssetResolver.ResolveBorrowed",
                "GodotContentOwnership.IsBorrowedContent",
                "GodotContentOwnership.RegisterBorrowedContent",
                "GodotContentOwnership.RegisterDerivedContent",
                "GodotObjectOwnershipRegistry.AssertBorrowedOrOwnedKnown",
                "GodotRuntimeResourceOwnership.MarkOwnedTransient",
                "GodotTransientResourceScope.Own",
                "GodotWrapperOwnershipRegistry.AssertBorrowedOrOwnedKnown",
                "IContentResourceLoader.LoadCanonical",
                "ProcessContentHost._roots",
                "ProcessContentHost.LoadCanonical",
                "WorldDefinitionProjection.ProjectResources",
            },
            StringComparer.Ordinal
        );

    private static readonly HashSet<string> ExplicitOpaqueStorageMemberAllowlist =
        new(
            new[]
            {
                "ApplicationLifetimeCoordinator._shutdownSync",
                "BattleSimOverridePatchDefinition._value",
                "GameLog._lock",
                "GameLogService._sync",
                "GodotContentOwnership.StaticContentOwner",
                "GodotObjectOwnershipRegistry.Sync",
                "GodotWrapperOwnershipRegistry.Sync",
                "LifecycleAuditRegistry._sync",
                "NativeLeaseScope.OwnershipSync",
                "ProcessContentHost.ProcessHostSync",
                "BattleDamagePreviewProjection+ProjectionRoot.Lease",
                "BattleDamagePreviewProjection+ProjectionRoot.Value",
                "ProgressionContentRegistry._skillDefs",
                "ProgressionContentRegistry._validationErrors",
                "SettlementServiceResultPayloadEntry._value",
                "ShutdownReport._sync",
                "SkillContentRegistry._skill_defs",
                "TrueRandomSeedService.DeterministicTestLock",
            },
            StringComparer.Ordinal
        );

    private static readonly IReadOnlyDictionary<string, int> RawBoundaryExpectedOverloadCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["EngineAssetAccess.ResolveBorrowed"] = 2,
            ["TraitRollGroupDefinition.FromResource"] = 2,
            ["TraitRollGroupEntryDefinition.FromResource"] = 2,
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
        RunAfterProcessStartup(Run);
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
        AssertNoLegacyEnemyContentCatalog();
        AssertNoRawAuthoredResourceRuntimeSignatures();
        AssertRetryFreeStrictRunnerSource();
        AssertAutoloadOrderAndProcessSnapshotBinding();
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

    private void AssertNoRawAuthoredResourceRuntimeSignatures()
    {
        Assembly assembly = typeof(GameSession).Assembly;
        HashSet<string> productionTypeNames = DiscoverProductionTypeNames();
        var violations = new HashSet<string>(StringComparer.Ordinal);
        foreach (Type type in GetLoadableTypes(assembly))
        {
            Type topLevelType = GetTopLevelType(type);
            if (
                type == null
                || typeof(Resource).IsAssignableFrom(type)
                || typeof(Resource).IsAssignableFrom(topLevelType)
                || !IsProductionType(type, productionTypeNames)
                || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
            )
            {
                continue;
            }

            const BindingFlags flags =
                BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly;
            foreach (FieldInfo field in type.GetFields(flags))
            {
                AddStoredRuntimeSignatureViolation(
                    violations,
                    type,
                    field,
                    field.FieldType,
                    "field"
                );
            }
            foreach (ConstructorInfo constructor in type.GetConstructors(flags))
            {
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    AddRawBoundarySignatureViolation(
                        violations,
                        type,
                        constructor,
                        $"constructor parameter {parameter.Name}",
                        parameter.ParameterType
                    );
                }
            }
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (
                    method.IsSpecialName
                    || method.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                )
                {
                    continue;
                }
                AddRawBoundarySignatureViolation(
                    violations,
                    type,
                    method,
                    $"method {method.Name} return",
                    method.ReturnType
                );
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    AddRawBoundarySignatureViolation(
                        violations,
                        type,
                        method,
                        $"method {method.Name} parameter {parameter.Name}",
                        parameter.ParameterType
                    );
                }
            }
        }

        foreach (string violation in violations)
            _test.Fail($"raw authored Resource or opaque Godot carrier escaped into runtime storage: {violation}");
    }

    private void AssertNoLegacyEnemyContentCatalog()
    {
        string scriptsRoot = ProjectSettings.GlobalizePath("res://scripts");
        List<string> matches = FindSourceMatches(scriptsRoot, LegacyEnemyCatalogPattern);
        _test.Eq(
            matches.Count,
            0,
            matches.Count == 0
                ? "legacy Enemy/AI content catalog remains deleted"
                : $"legacy Enemy/AI content catalog markers remain in {string.Join(", ", matches)}"
        );
    }

    private void AssertRetryFreeStrictRunnerSource()
    {
        string runnerSource = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://tests/run_regression_suite.py")
        );
        string workflowSource = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://.github/workflows/ci.yml")
        );
        foreach (
            string forbidden in new[]
            {
                "--finalizer-crash-retries",
                "finalizer_crash_retries",
                "finalizer_retries",
                "borrowed_resource_shutdown",
                "ObjectDB_leak_exempt",
                "suppressed_leaked_unsafe",
            }
        )
        {
            _test.False(
                runnerSource.Contains(forbidden, StringComparison.Ordinal)
                    || workflowSource.Contains(forbidden, StringComparison.Ordinal),
                $"regression runner/workflow contains no retry or shutdown exemption marker: {forbidden}"
            );
        }

        foreach (
            string required in new[]
            {
                "LEAKED_UNSAFE_REFERENCE_PREFIX",
                "OBJECTDB_LEAK_PREFIX",
                "RESOURCE_LEAK_PATTERN",
                "LIFECYCLE_FATAL_MARKERS",
            }
        )
        {
            _test.True(
                runnerSource.Contains(required, StringComparison.Ordinal),
                $"strict runner retains fatal shutdown classification: {required}"
            );
        }
        _test.True(
            workflowSource.Contains("--fail-on-output-error", StringComparison.Ordinal)
                && workflowSource.Contains("--lifecycle-correctness", StringComparison.Ordinal),
            "CI runs the full suite with strict output and lifecycle correctness"
        );
    }

    private void AssertAutoloadOrderAndProcessSnapshotBinding()
    {
        string projectSource = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://project.godot")
        );
        string autoloadSection = ExtractIniSection(projectSource, "autoload");
        var autoloadEntries = new List<string>();
        using (var reader = new StringReader(autoloadSection))
        {
            while (reader.ReadLine() is string line)
            {
                string normalized = line.Trim();
                if (normalized.Length != 0 && !normalized.StartsWith(';'))
                    autoloadEntries.Add(normalized);
            }
        }
        _test.True(autoloadEntries.Count >= 2, "project declares coordinator and session autoloads");
        if (autoloadEntries.Count >= 2)
        {
            _test.Eq(
                autoloadEntries[0],
                "ApplicationLifetimeCoordinator=\"*res://scripts/systems/lifecycle/ApplicationLifetimeCoordinator.cs\"",
                "ApplicationLifetimeCoordinator is first in autoload order"
            );
            _test.Eq(
                autoloadEntries[1],
                "GameSession=\"*res://scripts/systems/persistence/GameSession.cs\"",
                "GameSession follows the process lifetime owner"
            );
        }

        FieldInfo snapshotField = typeof(GameSession).GetField(
            "_contentSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        _test.Eq(
            snapshotField?.FieldType,
            typeof(ContentSnapshot),
            "GameSession stores the borrowed typed process snapshot"
        );
        MethodInfo bindContent = typeof(GameSession).GetMethod(
            "BindContent",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(ContentSnapshot) },
            modifiers: null
        );
        _test.True(bindContent != null, "GameSession exposes only the typed snapshot bind boundary");

        ApplicationLifetimeCoordinator coordinator =
            Root.GetNodeOrNull<ApplicationLifetimeCoordinator>("ApplicationLifetimeCoordinator");
        GameSession session = Root.GetNodeOrNull<GameSession>("GameSession");
        ContentSnapshot boundSnapshot = snapshotField?.GetValue(session) as ContentSnapshot;
        ContentSnapshot publishedSnapshot = coordinator?.ContentHost?.GetSnapshot();
        _test.True(
            coordinator != null && session != null,
            "coordinator and canonical GameSession autoloads are active"
        );
        _test.True(
            boundSnapshot != null && ReferenceEquals(boundSnapshot, publishedSnapshot),
            "canonical GameSession borrows the published process snapshot"
        );
        _test.True(
            coordinator?.ContentHost?.GetSnapshotBorrowerDiagnostics().Count > 0,
            "process content host records the attached session borrower"
        );
    }

    private static string ExtractIniSection(string source, string sectionName)
    {
        string header = $"[{sectionName}]";
        int start = source.IndexOf(header, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;
        start += header.Length;
        int nextSection = source.IndexOf("\n[", start, StringComparison.Ordinal);
        return nextSection >= 0
            ? source.Substring(start, nextSection - start)
            : source.Substring(start);
    }

    private static HashSet<string> DiscoverProductionTypeNames()
    {
        string scriptsRoot = ProjectSettings.GlobalizePath("res://scripts");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (string filePath in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(filePath);
            foreach (Match declaration in ProductionTypeDeclarationPattern.Matches(source))
                names.Add(declaration.Groups["name"].Value);
        }
        return names;
    }

    private static bool IsProductionType(Type type, HashSet<string> productionTypeNames)
    {
        Type topLevelType = GetTopLevelType(type);
        return productionTypeNames.Contains(WithoutGenericArity(topLevelType.Name));
    }

    private static Type GetTopLevelType(Type type)
    {
        Type topLevelType = type;
        while (topLevelType?.DeclaringType != null)
            topLevelType = topLevelType.DeclaringType;
        return topLevelType;
    }

    private static void AddStoredRuntimeSignatureViolation(
        HashSet<string> violations,
        Type ownerType,
        MemberInfo member,
        Type signatureType,
        string memberKind
    )
    {
        string memberKey = BuildMemberKey(ownerType, member.Name);
        if (!ExplicitRawBoundaryMemberAllowlist.Contains(memberKey))
        {
            AddRawResourceTypes(
                violations,
                ownerType,
                $"{memberKind} {member.Name}",
                signatureType
            );
        }

        if (
            ExplicitOpaqueStorageMemberAllowlist.Contains(memberKey)
            || ExplicitRawBoundaryMemberAllowlist.Contains(memberKey)
        )
        {
            return;
        }

        Type normalized = NormalizeSignatureType(signatureType);
        foreach (Type candidate in EnumerateSignatureTypes(signatureType))
        {
            bool directOpaqueObject = candidate == normalized && candidate == typeof(object);
            if (!directOpaqueObject && !IsOpaqueGodotCarrierType(candidate))
                continue;
            violations.Add(
                $"{ownerType.FullName ?? ownerType.Name}.{memberKind} {member.Name} -> opaque {candidate.FullName ?? candidate.Name}"
            );
        }
    }

    private static void AddRawBoundarySignatureViolation(
        HashSet<string> violations,
        Type ownerType,
        MemberInfo member,
        string memberLabel,
        Type signatureType
    )
    {
        if (IsAllowedSynchronousRawBoundary(ownerType, member))
            return;
        AddRawResourceTypes(violations, ownerType, memberLabel, signatureType);
    }

    private static void AddRawResourceTypes(
        HashSet<string> violations,
        Type ownerType,
        string memberLabel,
        Type signatureType
    )
    {
        foreach (Type rawType in EnumerateSignatureTypes(signatureType))
        {
            if (!IsProjectAuthoredResourceType(rawType))
                continue;
            violations.Add(
                $"{ownerType.FullName ?? ownerType.Name}.{memberLabel} -> {rawType.FullName ?? rawType.Name}"
            );
        }
    }

    private static bool IsAllowedSynchronousRawBoundary(Type ownerType, MemberInfo member)
    {
        string memberKey = BuildMemberKey(ownerType, member.Name);
        if (
            ExplicitRawBoundaryMemberAllowlist.Contains(memberKey)
            || RuntimeDefinitionProjectionMemberAllowlist.Contains(memberKey)
        )
            return HasExpectedBoundaryMemberShape(ownerType, member, memberKey);

        string ownerName = WithoutGenericArity(GetTopLevelType(ownerType).Name);
        if (SynchronousAuthoringOwnerAllowlist.Contains(ownerName))
            return member is ConstructorInfo || member is MethodInfo;

        return false;
    }

    private static bool HasExpectedBoundaryMemberShape(
        Type ownerType,
        MemberInfo member,
        string memberKey
    )
    {
        if (member is not MethodInfo method)
            return true;
        int expectedCount = RawBoundaryExpectedOverloadCounts.TryGetValue(
            memberKey,
            out int declaredCount
        )
            ? declaredCount
            : 1;
        const BindingFlags flags =
            BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;
        int actualCount = 0;
        foreach (MethodInfo candidate in ownerType.GetMethods(flags))
            if (string.Equals(candidate.Name, method.Name, StringComparison.Ordinal))
                actualCount++;
        return actualCount == expectedCount;
    }

    private static bool IsProjectAuthoredResourceType(Type type)
    {
        if (type == null)
            return false;
        if (type == typeof(Resource))
            return true;
        if (type.IsGenericParameter)
        {
            foreach (Type constraint in type.GetGenericParameterConstraints())
                if (typeof(Resource).IsAssignableFrom(constraint))
                    return true;
            return false;
        }
        return type.Assembly == typeof(GameSession).Assembly
            && typeof(Resource).IsAssignableFrom(type);
    }

    private static bool IsOpaqueGodotCarrierType(Type type) =>
        type == typeof(Variant)
        || type == typeof(GodotObject)
        || (
            string.Equals(type.Namespace, "Godot.Collections", StringComparison.Ordinal)
            && (
                string.Equals(
                    WithoutGenericArity(type.Name),
                    "Array",
                    StringComparison.Ordinal
                )
                || string.Equals(
                    WithoutGenericArity(type.Name),
                    "Dictionary",
                    StringComparison.Ordinal
                )
            )
        );

    private static Type NormalizeSignatureType(Type type)
    {
        Type normalized = type;
        while (normalized?.HasElementType == true)
            normalized = normalized.GetElementType();
        return normalized;
    }

    private static string BuildMemberKey(Type ownerType, string memberName)
    {
        string normalizedMemberName = memberName ?? string.Empty;
        if (
            normalizedMemberName.StartsWith("<", StringComparison.Ordinal)
            && normalizedMemberName.EndsWith(">k__BackingField", StringComparison.Ordinal)
        )
        {
            normalizedMemberName = normalizedMemberName.Substring(
                1,
                normalizedMemberName.Length - ">k__BackingField".Length - 1
            );
        }
        string ownerName = ownerType?.FullName ?? ownerType?.Name ?? string.Empty;
        return $"{WithoutGenericArity(ownerName)}.{normalizedMemberName}";
    }

    private static IEnumerable<Type> EnumerateSignatureTypes(Type type)
    {
        if (type == null)
            yield break;

        Type normalized = NormalizeSignatureType(type);
        if (normalized == null)
            yield break;

        yield return normalized;
        if (!normalized.IsGenericType)
            yield break;
        foreach (Type argument in normalized.GetGenericArguments())
        {
            foreach (Type nested in EnumerateSignatureTypes(argument))
                yield return nested;
        }
    }

    private static string WithoutGenericArity(string typeName)
    {
        int separator = typeName?.IndexOf('`') ?? -1;
        return separator >= 0 ? typeName.Substring(0, separator) : typeName ?? string.Empty;
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

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            var types = new List<Type>();
            foreach (Type type in exception.Types)
            {
                if (type != null)
                    types.Add(type);
            }
            return types;
        }
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
