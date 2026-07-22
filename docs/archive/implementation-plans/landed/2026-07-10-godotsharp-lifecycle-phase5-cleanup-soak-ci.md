# GodotSharp Lifecycle Phase 5 Cleanup, Soak, and CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete every lifecycle stopgap left by Phases 1-4, make the regression runner permanently retry-free and strict about shutdown output, prove the complete lifecycle with the specified 110-cycle single-process soak, and pass the final ten-round AI and full-suite gates.

**Architecture:** The Phase 1 coordinator is the only application-shutdown/GC-barrier/Quit owner; Phase 2 scopes and leases are the only project owners of runtime-created Godot wrappers; the Phase 3-4 `ProcessContentHost` and immutable `ContentSnapshot` are the only authored-content boundary. Phase 5 removes graph-wide suppression, static strong-wrapper retention, test quarantine, reflection walkers, migration tooling, retry recovery, and broad log exemptions. A deterministic test-only composition root repeatedly opens and closes normal sessions against one process snapshot and samples lifecycle counters plus managed/private memory through the explicit test-only `LifecycleMeasurementBarrier`.

**Tech Stack:** Godot 4.6.2 Mono, C#/.NET 8, Python 3 `unittest` regression tooling, GitHub Actions, PowerShell acceptance loops.

## Global Constraints

- Phases 1-4 and their cumulative gates must be green before starting this plan.
- Do not add compatibility aliases, fallback suppress paths, retry recovery, raw Resource runtime branches, or broad output allowlists.
- `ApplicationLifetimeCoordinator` remains the unique `SceneTree.Quit` owner; normal session close must never invoke the process shutdown path.
- `ProcessContentHost` is loaded once for the soak process. Each cycle binds the same immutable snapshot, closes only session-owned state, and leaves process content at its stable baseline.
- A plain CLR class calling `GC.SuppressFinalize(this)` is not evidence of Godot wrapper suppression. Final source gates target calls that suppress/retain Godot wrappers or recursively walk arbitrary value graphs.
- Preserve save version 12, payload shape, text snapshots, AI fingerprints, and battle results.
- Run no battle simulation or balance entry points; the final full suite retains the repository's existing simulation exclusions.
- Preserve unrelated worktree edits, especially current EnemyTemplate and AI regression changes.
- Treat each task's `Files` list as its staging manifest. Before every commit, inspect `git diff --cached --name-only`, `git diff --cached`, and `git diff --cached --check`; directory-level `git add` lines are never permission to stage unrelated work.
- Keep the three current user-owned EnemyTemplate/AI regression paths unstaged throughout Phase 5; none of this plan's tasks needs to modify them.
- Every new C# regression runner derives from `LifecycleTestSceneTree` and exits through `RequestTestExit(_test.Finish(label))`.

---

### Task 1: Remove RuntimeStateLifecycle and graph-wide finalizer marking

**Files:**
- Delete: `scripts/utils/RuntimeStateLifecycle.cs`
- Modify: `scripts/utils/Vector2IList.cs`
- Modify: `scripts/utils/StringNameList.cs`
- Modify: `scripts/utils/StringList.cs`
- Modify: `scripts/utils/RuntimePlainPayload.cs`
- Modify: `scripts/utils/RuntimePayloadCopy.cs`
- Modify: `scripts/utils/GodotObjectLifecycle.cs`
- Modify: `scripts/dev_tools/AiTraceRecorder.cs`
- Modify: `scripts/player/progression/SkillDef.cs`
- Modify: `scripts/ui/PartyMemberOptionUtils.cs`
- Modify: `scripts/systems/settlement/NpcQuestOfferWindowData.cs`
- Modify: `scripts/systems/persistence/GameSession.SaveIndexAndFileIO.cs`
- Modify: `scripts/systems/persistence/GameSession.FinalizerSuppression.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify: `scripts/systems/battle/presentation/BattleHudAdapter.cs`
- Modify: `scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs`
- Modify: `scripts/systems/battle/terrain/BattleTerrainGenerator.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.Mitigation.cs`
- Modify: `scripts/systems/battle/rules/DamageResolutionContext.cs`
- Modify: `scripts/systems/battle/core/AttackEffectResolutionResult.cs`
- Modify: `scripts/systems/battle/core/BattleSaveBranchPreviewData.cs`
- Modify: `scripts/systems/battle/core/BattleState.cs`
- Modify: `scripts/systems/battle/core/meteor_swarm/MeteorSwarmProfile.cs`
- Modify: `scripts/systems/battle/runtime/BattleGroundEffectService.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/systems/battle/ai/BattleAiTurnTracePayloadProjection.cs`
- Modify: `tests/shared/BattleTestFixture.cs`
- Modify: `tests/battle_runtime/fate/run_low_luck_relic_regression.cs`
- Modify: `tests/battle_runtime/fate/run_fate_low_luck_tactical_skills_regression.cs`
- Modify: `tests/progression/fate/run_party_state_fate_regression.cs`
- Modify: `tests/progression/fate/run_misfortune_black_omen_regression.cs`
- Create: `tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs`

**Interfaces:**
- Consumes: Phase 2 projection/native lease ownership and Phase 3-4 plain definitions.
- Produces: zero `RuntimeStateLifecycle` call sites and no recursive “make this graph finalizerless” behavior.

- [ ] **Step 1: Add a failing source-boundary regression**

Make `run_runtime_lifecycle_cleanup_regression.cs` recursively scan project-owned C# sources and report every declaration or call containing:

```text
RuntimeStateLifecycle
MarkValueGraphFinalizerless
SuppressRuntimeStateGraphsForFinalizerDrain
```

The error must include the repository-relative path and line number. The scanner may exclude only the two lifecycle source-gate files that contain the forbidden tokens as test data; do not use a substring or directory allowlist for production/test callers.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs
```

Expected: the new gate lists the helper and any callers not already removed by Phases 2-4.

- [ ] **Step 3: Remove each remaining call for its actual ownership reason**

- Pure CLR copies/definitions: remove the marker with no replacement.
- Godot projection values: return or retain them only through the Phase 2 `GodotProjectionLease`/`NativeLeaseScope` owner.
- Test fixtures: close the owning battle/session scope; do not suppress the projected graph.
- Simulation helpers: keep them on the same typed snapshot/lease APIs as production despite their exclusion from routine regression.

Delete `RuntimeStateLifecycle.cs` only after the call-site scan is empty. Do not replace it with another reflection helper or `GC.SuppressFinalize` loop.

- [ ] **Step 4: Run green and commit**

```powershell
rg -n "RuntimeStateLifecycle|MarkValueGraphFinalizerless|SuppressRuntimeStateGraphsForFinalizerDrain" scripts tests --glob "*.cs" --glob "!tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs" --glob "!tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs"
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs
dotnet build magic.csproj
# Stage only the exact Task 1 Files manifest.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: remove runtime graph suppression"
```

Expected: `rg` returns no matches and the cleanup regression/build pass.

---

### Task 2: Delete static retention, quarantine, and reflection cleanup

**Files:**
- Delete: `scripts/utils/GodotTypedResourceGraphWalker.cs`
- Delete: `scripts/systems/persistence/GameSession.FinalizerSuppression.cs`
- Modify: `scripts/utils/GodotObjectOwnership.cs`
- Modify: `scripts/utils/GodotObjectLifecycle.cs`
- Modify: `scripts/utils/WorldMapContentValidator.cs`
- Modify: `scripts/systems/persistence/GameSession.cs`
- Modify: `scripts/systems/lifecycle/ApplicationLifetimeCoordinator.cs`
- Modify: `scripts/systems/lifecycle/LifecycleAuditRegistry.cs`
- Modify: `tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs`
- Modify: `tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs`

**Interfaces:**
- Consumes: explicit coordinator, content host, scene owner, native scope, and projection lease teardown.
- Produces: weak diagnostic registry only; no project-side strong wrapper sink or wrapper-wide finalizer suppression.

- [ ] **Step 1: Extend the cleanup regression and run red**

Reject declarations/calls for:

```text
GodotTypedResourceGraphWalker
StaticStrongWrappers
GodotTestRuntimeQuarantine
SuppressBorrowedContentForProcessExit
RetainStaticWrappersForProcessLifetime
PrepareForFinalizerDrain
GameSession.FinalizerSuppression
```

Also fail on `GC.SuppressFinalize(` when the argument is a `GodotObject`, Resource/Node wrapper, or an object produced by a wrapper graph walk. Do not reject plain CLR `GC.SuppressFinalize(this)` by text alone.

```powershell
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs
```

Expected: legacy retention/quarantine/walker files are reported.

- [ ] **Step 2: Shrink ownership infrastructure to auditable weak state**

Keep only weak-reference ownership records, counters, conflict detection, epoch/domain/role metadata, and structured snapshots in `GodotObjectOwnership.cs`/`LifecycleAuditRegistry.cs`. Remove:

- `GodotContentOwnership.StaticStrongWrappers` and its recursively retained wrapper graph;
- `GodotTestRuntimeQuarantine` and test-only wrapper retention;
- recursive graph traversal/suppression;
- content/process-exit suppression entry points;
- GameSession reflection cleanup.

Do not remove `ProcessContentHost` canonical content roots or `EngineAssetResolver` anchors here. They are the explicit process owner/borrow anchors and remain live until coordinator `ReleaseContentAsync` after the Phase 1 release gate succeeds.

`GodotObjectLifecycle` may expose the coordinator's one finalizer barrier operation, but it must only run `GC.Collect → GC.WaitForPendingFinalizers → GC.Collect` after the coordinator barrier gate succeeds. It must not inspect or mutate runtime/content graphs.

- [ ] **Step 3: Remove obsolete callers and preserve shutdown ordering**

Delete `WorldMapContentValidator.SuppressBorrowedContentForProcessExit()` and any GameSession partial-method hooks. Update `ApplicationLifetimeCoordinator` so `ReleaseContentAsync` first asserts that every external content borrower is closed, then releases the host's own snapshot anchor and clears canonical roots before the barrier; it must not call a legacy preparation bridge. Keep failure reporting and `FinalizerBarrierSkipped` behavior from Phase 1.

- [ ] **Step 4: Run focused ownership/lifecycle gates**

```powershell
rg -n "GodotTypedResourceGraphWalker|StaticStrongWrappers|GodotTestRuntimeQuarantine|SuppressBorrowedContentForProcessExit|PrepareForFinalizerDrain" scripts tests --glob "*.cs" --glob "!tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs" --glob "!tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs"
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs
python tests/run_regression_suite.py --pattern runtime/lifecycle --jobs 1 --stop-on-failure --finalizer-crash-retries 0 --fail-on-output-error --lifecycle-correctness
dotnet build magic.csproj
# Stage only the exact Task 2 Files manifest.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: delete lifecycle quarantine infrastructure"
```

Expected: source scans are empty; strict lifecycle tests pass without a suppress bridge.

---

### Task 3: Seal the test-exit migration and remove migration-only tooling

**Files:**
- Delete: `tools/migrate_test_exit_calls.py`
- Delete: `tests/tooling/test_migrate_test_exit_calls.py`
- Delete: `tests/tooling/test_exit_migration_manifest.txt`
- Delete: `tests/shared/GodotSharpCleanup.cs`
- Modify: `tests/shared/TestHarness.cs`
- Modify: `tests/shared/TestExitCoordinator.cs`
- Modify: `tests/shared/LifecycleTestSceneTree.cs`
- Modify: `tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs`
- Modify: `scripts/systems/lifecycle/ApplicationLifetimeCoordinator.cs`

**Interfaces:**
- Consumes: the completed Phase 1 migration of every C# SceneTree runner.
- Produces: one durable source gate proving all test/process exits still traverse the coordinator, with no migration compatibility layer.

- [ ] **Step 1: Make the boundary regression prove the final source shape**

Add exact allowlists and assertions:

- exactly one project call to `SceneTree.Quit`/`GetTree().Quit`, in `ApplicationLifetimeCoordinator.cs`;
- no direct Quit under `tests/`;
- no `GodotSharpCleanup`, test-local `CollectPendingFinalizers`, or `TryStartNoGCRegion`;
- every concrete C# `SceneTree` runner derives from `LifecycleTestSceneTree`;
- `TestExitCoordinator` submits `ShutdownRequest` and does not run GC/Quit itself;
- no migration marker or generated compatibility wrapper remains.

Allow direct GC calls only in production `GodotObjectLifecycle.cs` and test-only `tests/shared/LifecycleMeasurementBarrier.cs`. Session-recreate and soak runners call the helper rather than duplicating the sequence.

- [ ] **Step 2: Run red against migration-only files**

```powershell
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs
python -m unittest tests.tooling.test_migrate_test_exit_calls -v
```

Expected: the source-shape assertion fails while migration-only artifacts and `GodotSharpCleanup` remain; the codemod test still proves the Phase 1 migration is complete before deletion.

- [ ] **Step 3: Delete migration-only files**

Remove the codemod, its unit test/manifest, and the obsolete cleanup facade. Keep `TestHarness` as result aggregation only, `LifecycleTestSceneTree` as the runner base, and `TestExitCoordinator` as the adapter to the application coordinator. Do not retain obsolete overloads for old runner call shapes.

- [ ] **Step 4: Run green and commit**

```powershell
rg -n "GetTree\(\)\.Quit|SceneTree\.Quit|GodotSharpCleanup|TryStartNoGCRegion|CollectPendingFinalizers" tests scripts --glob "*.cs" --glob "!tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs" --glob "!tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs"
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs
dotnet build magic.csproj
git add tools tests/shared tests/tooling tests/runtime/validation scripts/systems/lifecycle
git commit -m "test: seal coordinator-owned exits"
```

Expected: only the exact coordinator/barrier allowlist remains; build and source gate pass.

---

### Task 4: Make the regression runner permanently retry-free and output-strict

**Files:**
- Modify: `tests/run_regression_suite.py`
- Modify: `tests/tooling/test_run_regression_suite.py`
- Modify: `.github/workflows/ci.yml`
- Modify: `tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs`

**Interfaces:**
- Consumes: deterministic coordinator shutdown with no expected project-owned leak output.
- Produces: one attempt per test process, fatal shutdown-marker classification, and CI with no retry or broad leak exemption.

- [ ] **Step 1: Rewrite Python tests first**

Assert:

- parser no longer accepts `--finalizer-crash-retries`;
- `run_single` invokes Godot exactly once for pass, nonzero exit, crash, timeout, and output failure;
- `RunResult` and summary contain no retry count;
- `Leaked unsafe reference to object`, `ObjectDB instances leaked at exit`, and `resources still in use at exit` are output failures even when Godot exits 0;
- finalizer markers `gchandle.is_released`, `GodotObject.Finalize`, `Handle is not initialized`, and `godotsharp_variant_destroy` are always failures;
- generic ERROR/SCRIPT ERROR/FATAL handling remains strict under `--fail-on-output-error`;
- workflow contains `--fail-on-output-error` and `--lifecycle-correctness`, with no retry argument.

- [ ] **Step 2: Run red**

```powershell
python -m unittest tests.tooling.test_run_regression_suite -v
```

Expected: existing retry fields/loop, workflow argument, and shutdown-only exemptions fail the new tests.

- [ ] **Step 3: Remove retry/exemption implementation**

Delete the retry CLI option, retry loop, retry counters/summaries, and borrowed-resource/ObjectDB whole-line exemptions. Preserve subprocess timeout/termination cleanup and parallel job scheduling. If an engine-originated baseline is ever proven later, it must be classified by exact type/owner in the lifecycle audit, not hidden in the Python output parser.

Update CI to:

```yaml
python tests/run_regression_suite.py \
  --jobs 16 \
  --test-timeout-seconds 180 \
  --fail-on-output-error \
  --lifecycle-correctness
```

- [ ] **Step 4: Run green and commit**

```powershell
python -m unittest tests.tooling.test_run_regression_suite -v
rg -n "finalizer-crash-retries|finalizer_retries|borrowed_resource_shutdown|ObjectDB.*exempt|suppressed .*leaked unsafe" tests/run_regression_suite.py .github/workflows/ci.yml
python tests/run_regression_suite.py --pattern runtime/lifecycle --jobs 1 --stop-on-failure --fail-on-output-error --lifecycle-correctness
git add tests/run_regression_suite.py tests/tooling/test_run_regression_suite.py tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs .github/workflows/ci.yml
git commit -m "test: enforce retry-free lifecycle failures"
```

Expected: unit/focused suites pass and `rg` has no retry/exemption hits.

---

### Task 5: Add the deterministic 110-cycle lifecycle soak

**Files:**
- Create: `scripts/systems/persistence/GameSessionPersistenceOptions.cs`
- Modify: `scripts/systems/persistence/GameSession.cs`
- Modify: `scripts/systems/persistence/GameSession.SaveIndexAndFileIO.cs`
- Modify: `scripts/systems/persistence/SaveRepository.cs`
- Create: `scripts/systems/game_runtime/IBattleSeedSource.cs`
- Create: `scripts/systems/game_runtime/TrueRandomBattleSeedSource.cs`
- Modify: `scripts/systems/game_runtime/BattleSessionFacade.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify: `scripts/systems/lifecycle/LifecycleAuditRegistry.cs`
- Modify: `tests/shared/GameSessionTestFactory.cs`
- Create: `tests/shared/FixedBattleSeedSource.cs`
- Create: `tests/runtime/lifecycle/LifecycleSoakCounterVector.cs`
- Create: `tests/runtime/lifecycle/LifecycleSoakActivityDelta.cs`
- Create: `tests/runtime/lifecycle/LifecycleSoakSample.cs`
- Create: `tests/runtime/lifecycle/LifecycleSoakStatistics.cs`
- Create: `tests/runtime/lifecycle/LifecycleSoakScenario.cs`
- Create: `tests/runtime/lifecycle/run_lifecycle_soak_statistics_regression.cs`
- Create: `tests/runtime/lifecycle/run_application_lifecycle_soak_regression.cs`
- Create: `tests/runtime/persistence/run_game_session_persistence_options_regression.cs`
- Test helper: `tests/shared/LifecycleMeasurementBarrier.cs`

**Interfaces:**
- Consumes: a sealed process host/snapshot, the normal-session open/close API, battle/AI/preview/save-load owners, lifecycle audit snapshots, and coordinator-owned final process exit.
- Produces: deterministic counter equality plus managed/private memory median and slope evidence over one 110-cycle process.

- [ ] **Step 1: Test statistics and thresholds before the scenario**

`run_lifecycle_soak_statistics_regression.cs` must cover odd/even median, least-squares slope, percentage/absolute threshold selection, negative/no-growth series, exact domain counter-vector equality, per-cycle created/closed balance, zero-violation enforcement, canonical root fingerprint changes, and threshold-boundary pass/fail cases. `run_game_session_persistence_options_regression.cs` must prove production path values remain `user://saves`/`user://saves/index.dat` without reading/deleting them, lifecycle-soak paths stay under one sanitized run root, invalid/traversal IDs fail, and clearing test root A leaves test root B intact.

All three new runners derive from `LifecycleTestSceneTree` and complete through `RequestTestExit(_test.Finish(label))`.

Use:

```csharp
managedAllowedGrowth = Math.Max(8L * 1024 * 1024, managedBaseline * 0.05);
privateAllowedGrowth = Math.Max(32L * 1024 * 1024, privateBaseline * 0.10);
managedMaxSlope = 64L * 1024;   // bytes per cycle
privateMaxSlope = 256L * 1024;  // bytes per cycle
```

Slope is ordinary least squares over cycle numbers 11-110 and their 100 samples:

```text
sum((x - meanX) * (y - meanY)) / sum((x - meanX)^2)
```

- [ ] **Step 2: Run statistics red**

```powershell
godot --headless -s res://tests/runtime/lifecycle/run_lifecycle_soak_statistics_regression.cs
godot --headless -s res://tests/runtime/persistence/run_game_session_persistence_options_regression.cs
```

Expected: helpers do not exist.

- [ ] **Step 3: Implement immutable samples/statistics**

Add explicit production/test composition seams:

```csharp
internal sealed record GameSessionPersistenceOptions(
    string SaveDirectory,
    string SaveIndexPath
)
{
    internal static GameSessionPersistenceOptions Production { get; } =
        new("user://saves", "user://saves/index.dat");

    internal static GameSessionPersistenceOptions ForLifecycleSoak(string runId)
    {
        if (
            string.IsNullOrWhiteSpace(runId)
            || !runId.All(value => char.IsLetterOrDigit(value) || value == '-')
        )
        {
            throw new ArgumentException("Invalid lifecycle soak run ID.", nameof(runId));
        }
        string directory = $"user://lifecycle_soak/{runId}";
        return new(directory, $"{directory}/index.dat");
    }
}

internal interface IBattleSeedSource
{
    int NextSeed(EncounterAnchorData encounterAnchor);
}

internal sealed class TrueRandomBattleSeedSource : IBattleSeedSource
{
    public int NextSeed(EncounterAnchorData encounterAnchor) =>
        (int)TrueRandomSeedService.GenerateSeed();
}
```

`GameSession` keeps its parameterless production constructor and adds an internal constructor accepting `GameSessionPersistenceOptions`; every save/index/cleanup path reads that immutable option. `SaveRepository` receives the same directory. `GameRuntimeFacade()` delegates to an internal `GameRuntimeFacade(IBattleSeedSource seedSource)` constructor; `BattleSessionFacade.BuildBattleSeed` calls the injected source. `FixedBattleSeedSource` returns `0x5A17_2026`. These seams change no save schema, seed algorithm in production, or gameplay behavior.

`LifecycleAuditRegistry.CaptureSnapshot()` returns active counts by exact domain plus monotonic activity totals. `LifecycleSoakCounterVector` captures the complete post-teardown active/violation state:

```csharp
internal sealed record LifecycleSoakCounterVector(
    int SessionOwners,
    int BattleOwners,
    int DecisionOwners,
    int RequestOwners,
    int SceneTreeOwners,
    int ContentBorrowers,
    int ActiveJobs,
    string NativeScopesByDomain,
    string ProjectionLeasesByDomain,
    long SnapshotEpoch,
    string ProcessContentRootFingerprint,
    int UnknownOwnershipViolations,
    int OwnerConflictViolations,
    int EscapedLeaseViolations,
    int CloseAfterUseViolations,
    int NormalSuppressions,
    int QuarantinedWrappers
);

internal sealed record LifecycleSoakActivityDelta(
    long OwnersRegistered,
    long OwnersClosed,
    long NativeWrappersOwned,
    long NativeWrappersDisposed,
    long ProjectionContainersOwned,
    long ProjectionContainersDisposed,
    long TransfersOut,
    long TransfersIn
);
```

Domain strings and `ProcessContentHost.GetCanonicalRootDiagnostics()` are ordinal-sorted `name=count`/`path|type|role` sequences before comparison. Every sample requires all four violation counters, normal suppressions, and quarantine to be zero. `LifecycleSoakActivityDelta` requires registered=closed, native-owned=disposed, projection-owned=disposed, and transfers-out=transfers-in for each cycle; monotonic totals themselves are not compared to the warm-up active vector.

`LifecycleSoakSample` stores cycle, active counters, activity delta, `GC.GetTotalMemory(false)`, and `Process.GetCurrentProcess().PrivateMemorySize64`. Statistics return a structured report including baselines, final medians, deltas, allowed deltas, slopes, and failing cycle/counter names.

- [ ] **Step 4: Add a deterministic single-process scenario**

Lock the test composition API:

```csharp
internal sealed class LifecycleSoakScenario
{
    internal LifecycleSoakScenario(
        SceneTree tree,
        ApplicationLifetimeCoordinator coordinator,
        ProcessContentHost contentHost,
        GameSessionPersistenceOptions persistenceOptions
    );

    internal ValueTask<LifecycleSoakSample> RunCycleAsync(int cycle);
}
```

The runner obtains the already-built/sealed `coordinator.ContentHost` (asserting `BuildAndSeal` occurred exactly once), closes the bootstrap `/root/GameSession` through `CloseSessionAsync`, awaits one `LifecycleMeasurementBarrier`, creates one run ID `$"{Process.GetCurrentProcess().Id}-{Guid.NewGuid():N}"` before warm-up, and constructs `GameSessionPersistenceOptions.ForLifecycleSoak(runId)`. Each `RunCycleAsync` performs:

1. capture pre-cycle monotonic audit totals;
2. create a fresh session with `GameSessionTestFactory.CreateBorrowingProcessSnapshot(tree, persistenceOptions)` and attach it to the coordinator;
3. call `CreateNewSave("res://data/configs/world_map/test_world_map_config.tres")` and assert save version 12;
4. create `GameRuntimeFacade(new FixedBattleSeedSource(0x5A17_2026))`, call `Setup(session)`, and start/confirm this formal encounter:

```csharp
new EncounterAnchorData
{
    entity_id = "lifecycle_soak_wolf",
    display_name = "Lifecycle Soak Wolf",
    world_coord = new Vector2I(3, 3),
    faction_id = "hostile",
    enemy_roster_template_id = "wolf_pack",
    encounter_kind = EncounterAnchorData.ToStringName(EncounterAnchorKind.Single),
};
```

5. enable typed AI trace, build a wait `BattleCommand` for the active unit, call `BattleRuntimeModule.PreviewCommand`, project it with the Phase 2 `BattlePreviewProjection.BuildLease`, read `lease.Value`, and close the lease;
6. for at most 64 iterations, issue `CommandBattleWaitOrResolveTyped` for a manual active unit or `CommandBattleTickTyped(1)` for an AI active unit; fail unless `GetAiTurnTracesTyped().Count >= 1`;
7. dispose `GameRuntimeFacade`, assert the battle save lock and all battle/decision/request leases are clear, call `SaveGameState()`, remember `GetActiveSaveId()`, and call `LoadSave(saveId)`;
8. in `finally`, dispose any remaining facade, call `ClearPersistedGame()` against the injected test root, close the session through `ApplicationLifetimeCoordinator.CloseSessionAsync`, await `LifecycleMeasurementBarrier.RunAsync(tree)`, and sample active counters/memory plus the pre/post activity delta.

The measurement barrier is test-only and does not request process shutdown. Cleanup is restricted to the injected lifecycle-soak root and happens through the normal save owner. The final test result still exits through `TestExitCoordinator`/`ApplicationLifetimeCoordinator`.

Use cycle 10's complete counter vector as the exact baseline for cycles 11-110. Use median cycles 11-20 as each memory baseline and median cycles 101-110 as the final window. Fail on any counter mismatch, managed delta above `max(8 MiB, 5%)`, private delta above `max(32 MiB, 10%)`, managed slope above 64 KiB/cycle, or private slope above 256 KiB/cycle.

- [ ] **Step 5: Run the soak and inspect structured output**

```powershell
godot --headless -s res://tests/runtime/lifecycle/run_lifecycle_soak_statistics_regression.cs
godot --headless -s res://tests/runtime/lifecycle/run_application_lifecycle_soak_regression.cs
```

Expected: exactly 110 cycle records, a `[LIFECYCLE-SOAK]` summary, unchanged post-warm-up counter vectors, and every memory delta/slope within contract.

- [ ] **Step 6: Run focused parity tests and commit**

```powershell
godot --headless -s res://tests/runtime/persistence/run_game_session_transaction_regression.cs
godot --headless -s res://tests/runtime/persistence/run_game_session_persistence_options_regression.cs
godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs
python tests/run_regression_suite.py --pattern battle_runtime/ai --jobs 16 --fail-on-output-error --lifecycle-correctness
dotnet build magic.csproj
git add scripts/systems/persistence/GameSessionPersistenceOptions.cs scripts/systems/persistence/GameSession.cs scripts/systems/persistence/GameSession.SaveIndexAndFileIO.cs scripts/systems/persistence/SaveRepository.cs scripts/systems/game_runtime/IBattleSeedSource.cs scripts/systems/game_runtime/TrueRandomBattleSeedSource.cs scripts/systems/game_runtime/BattleSessionFacade.cs scripts/systems/game_runtime/GameRuntimeFacade.cs scripts/systems/lifecycle/LifecycleAuditRegistry.cs tests/shared/GameSessionTestFactory.cs tests/shared/FixedBattleSeedSource.cs tests/runtime/lifecycle tests/runtime/persistence/run_game_session_persistence_options_regression.cs
git commit -m "test: add deterministic lifecycle soak"
```

---

### Task 6: Enforce the final architecture and complete the acceptance matrix

**Files:**
- Modify: `tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs`
- Modify: `tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs`
- Modify: `tests/tooling/test_run_regression_suite.py`
- Modify: `docs/design/project_context_units.md`
- Modify: `docs/design/platform/godotsharp_lifecycle.md`

**Interfaces:**
- Consumes: every Phase 1-5 contract.
- Produces: final static/behavior/stability proof and an updated repository owner/read-set map.

- [ ] **Step 1: Make final source gates cumulative**

The final validation must prove:

- zero `RuntimeStateLifecycle`, recursive graph suppression, reflection wrapper walker, static strong-wrapper sink, or quarantine;
- zero direct Godot-wrapper `GC.SuppressFinalize`;
- unique coordinator Quit and unique production GC barrier;
- zero test-local Quit, zero NoGCRegion, and zero direct test GC outside `LifecycleMeasurementBarrier.cs`; every runner uses `LifecycleTestSceneTree`;
- zero raw authored Resource types in runtime/service/state signatures;
- zero legacy Enemy/AI content catalog;
- no runner retry option/count/loop and no broad shutdown-log exemption;
- `ApplicationLifetimeCoordinator` is first in autoload order and `GameSession` binds the process snapshot.

Source gates may use exact authoring/projector allowlists established in Phases 3-4, never “all files under this directory” exemptions.

- [ ] **Step 2: Run build, static gates, focused behavior, and one soak**

```powershell
dotnet build magic.csproj
python -m unittest tests.tooling.test_run_regression_suite -v
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs
godot --headless -s res://tests/runtime/lifecycle/run_application_lifecycle_soak_regression.cs
python tests/run_regression_suite.py --pattern runtime/lifecycle --jobs 1 --stop-on-failure --fail-on-output-error --lifecycle-correctness
```

Expected: all pass with zero fatal/finalizer/unsafe/ObjectDB/resource-in-use markers.

- [ ] **Step 3: Run the AI subset ten times with 16 independent processes**

```powershell
1..10 | ForEach-Object {
    python tests/run_regression_suite.py --pattern battle_runtime/ai --jobs 16 --fail-on-output-error --lifecycle-correctness
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
```

Expected: all ten rounds pass on the first and only attempt for every test.

- [ ] **Step 4: Run the complete routine suite ten times**

```powershell
1..10 | ForEach-Object {
    python tests/run_regression_suite.py --jobs 16 --fail-on-output-error --lifecycle-correctness
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
```

The routine-suite discovery rules continue excluding numeric battle simulation/balance entry points. Do not manually add them. The lifecycle soak remains part of the runtime/lifecycle set and therefore participates in these final full-suite rounds.

Expected: ten clean rounds, no retry recovery, no output exemptions, and stable save/text/AI/battle goldens.

- [ ] **Step 5: Update current owner documentation**

Update `docs/design/project_context_units.md` only now that the architecture exists:

- CU-02: coordinator/process content owner and GameSession snapshot borrower;
- CU-06: coordinator-driven facade shutdown;
- CU-15/CU-16: BattleLifetime/AiDecisionLease ownership;
- CU-18: scene-owned UI projection leases;
- CU-19: `TestExitCoordinator` and lifecycle validation read set;
- CU-21: headless runners delegate to the common shutdown pipeline.

Mark the architecture spec implemented with the Phase 1-5 implementation commit hashes that already exist before the closure commit, plus exact acceptance commands/results. Do not try to record the not-yet-created closure commit's own hash; if that hash must be recorded, use a later documentation-only commit. Do not rewrite the chosen design or thresholds.

- [ ] **Step 6: Final diff audit and commit**

```powershell
rg -n "RuntimeStateLifecycle|GodotTypedResourceGraphWalker|StaticStrongWrappers|GodotTestRuntimeQuarantine|SuppressBorrowedContentForProcessExit|TryStartNoGCRegion" scripts tests --glob "*.cs" --glob "!tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs" --glob "!tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs"
rg -n "finalizer-crash-retries|finalizer_retries" tests/run_regression_suite.py .github/workflows/ci.yml
git diff --check
git status --short
git add tests/runtime/validation/run_runtime_lifecycle_cleanup_regression.cs tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs docs/design/project_context_units.md docs/design/platform/godotsharp_lifecycle.md
git commit -m "docs: close GodotSharp lifecycle migration"
```

Expected: both `rg` scans are empty, only intentional files are staged, and all unrelated user edits remain untouched.
