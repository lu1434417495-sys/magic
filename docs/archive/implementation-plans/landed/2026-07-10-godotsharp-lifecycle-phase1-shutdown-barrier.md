# GodotSharp Lifecycle Phase 1 Shutdown Barrier Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish one idempotent application/test shutdown owner, separate normal session close from process shutdown, and add a retries-zero lifecycle-order CI lane without claiming wrapper leaks are solved.

**Architecture:** Add a pure shutdown state machine plus an autoload `ApplicationLifetimeCoordinator` created before `GameSession`. All production and test exits submit `ShutdownRequest`; only the coordinator tears down runtime/SceneTree owners, evaluates the barrier gate, runs the pre-quit GC barrier when safe, emits the final result, and calls `SceneTree.Quit`. Existing suppress/quarantine mechanisms remain only as exact `LegacyDebt` records for later phases.

**Tech Stack:** Godot 4.6.2 Mono, C#/.NET 8, Python 3 regression runner, GitHub Actions, headless Godot regression scripts.

## Global Constraints

- Read `docs/design/platform/godotsharp_lifecycle.md` before editing.
- Preserve save version 12, serialized payload shape, gameplay behavior, AI fingerprint, and headless snapshot shape.
- Do not introduce content hot reload, offline `.tres` compilation, compatibility aliases, or new suppress/quarantine fallbacks.
- Do not add `GC.SuppressFinalize` calls for GodotObject/Godot collection wrappers.
- `ApplicationLifetimeCoordinator` is the only production/test owner allowed to run the final GC barrier or call `SceneTree.Quit`.
- Phase 1 proves shutdown ordering/idempotency and zero finalizer fatal with retries 0. Existing wrapper/resource counts remain baselines, not success claims.
- Do not run battle simulation or balance runners.
- Treat each task's `Files` list as its staging manifest. Before every commit, inspect `git diff --cached --name-only`, `git diff --cached`, and `git diff --cached --check`; never blindly execute a directory-wide `git add` from a command block when unrelated changes exist.
- Current user-owned paths `scripts/enemies/EnemyTemplateDef.cs`, `tests/battle_runtime/ai/run_enemy_template_runtime_start_regression.cs`, and `tests/battle_runtime/ai/run_enemy_template_schema_boundary_regression.cs` stay unstaged unless the task explicitly changes them. For an overlapping task, stage only lifecycle hunks; if they cannot be separated, stop before committing.

---

### Task 1: Shutdown contracts, state machine, and report

**Files:**
- Create: `scripts/systems/lifecycle/ApplicationShutdownPhase.cs`
- Create: `scripts/systems/lifecycle/ShutdownRequest.cs`
- Create: `scripts/systems/lifecycle/ShutdownReport.cs`
- Create: `scripts/systems/lifecycle/ApplicationShutdownStateMachine.cs`
- Create: `scripts/systems/lifecycle/LifecycleAuditRegistry.cs`
- Create: `tests/runtime/lifecycle/run_application_shutdown_contract_regression.cs`

**Interfaces:**
- Consumes: current `LifecycleViolation.Report` strict behavior.
- Produces: shutdown phase/request/report types and the audit/debt counters used by later tasks.

- [ ] **Step 1: Write the failing contract regression**

```csharp
var machine = new ApplicationShutdownStateMachine();
_test.Eq(machine.Phase, ApplicationShutdownPhase.Running, "starts running");
_test.True(machine.TryAdvance(ApplicationShutdownPhase.Quiescing), "enter quiescing");
_test.False(machine.TryAdvance(ApplicationShutdownPhase.SceneDrained), "cannot skip runtime");
_test.True(machine.TryAdvance(ApplicationShutdownPhase.RuntimeDrained), "runtime drained");
_test.True(machine.TryAdvance(ApplicationShutdownPhase.SceneDrained), "scene drained");
_test.True(machine.TryAdvance(ApplicationShutdownPhase.ContentReleased), "content released");
_test.True(machine.TryAdvance(ApplicationShutdownPhase.FinalizersDrained), "finalizers drained");
_test.True(machine.TryAdvance(ApplicationShutdownPhase.QuitRequested), "quit requested");

var failed = new ApplicationShutdownStateMachine();
_test.True(failed.TryAdvance(ApplicationShutdownPhase.Quiescing), "failure quiesces");
_test.True(
    failed.TryAdvance(ApplicationShutdownPhase.FinalizerBarrierSkipped),
    "unsafe pre-content state enters explicit failure"
);
_test.True(
    failed.TryAdvance(ApplicationShutdownPhase.QuitRequested),
    "failed shutdown still requests quit"
);
_test.False(
    failed.TryAdvance(ApplicationShutdownPhase.ContentReleased),
    "failed shutdown cannot claim content release"
);

var request = new ShutdownRequest(
    0,
    ShutdownReason.TestComplete,
    new ShutdownCallerResult("contract", false)
);
var report = new ShutdownReport(request);
_test.Eq(report.EffectiveExitCode, 1, "failed caller forces nonzero exit");
```

- [ ] **Step 2: Run red**

```powershell
dotnet build magic.csproj
godot --headless -s res://tests/runtime/lifecycle/run_application_shutdown_contract_regression.cs
```

Expected: missing lifecycle types, not an unrelated parse error.

- [ ] **Step 3: Add the exact contracts**

```csharp
internal enum ApplicationShutdownPhase
{
    Running = 0,
    Quiescing,
    RuntimeDrained,
    SceneDrained,
    ContentReleased,
    FinalizersDrained,
    FinalizerBarrierSkipped,
    QuitRequested,
}

internal enum ShutdownReason
{
    WindowClose = 0,
    RequestedExit,
    TestComplete,
}

internal sealed record ShutdownCallerResult(string Label, bool Passed);

internal sealed record ShutdownRequest(
    int RequestedExitCode,
    ShutdownReason Reason,
    ShutdownCallerResult CallerResult = null
);
```

Implement only these success/failure phase branches:

```text
Running -> Quiescing -> RuntimeDrained -> SceneDrained -> ContentReleased
ContentReleased -> FinalizersDrained -> QuitRequested
Quiescing | RuntimeDrained | SceneDrained | ContentReleased
    -> FinalizerBarrierSkipped -> QuitRequested
```

`ShutdownReport` stores the first request, requested/effective code, duplicate request diagnostics, phase history, failures, barrier-skipped state, and exact `LegacyDebt` snapshots. `MergeRequest` never changes the first reason, but any later nonzero requested code or failed caller raises the effective code to nonzero and can never lower it. `LifecycleAuditRegistry` stores counters and weak diagnostics only.

- [ ] **Step 4: Run green**

```powershell
dotnet build magic.csproj
$env:MAGIC_LIFECYCLE_STRICT='1'
godot --headless -s res://tests/runtime/lifecycle/run_application_shutdown_contract_regression.cs
```

Expected: build exits 0 and the test prints `PASS`.

- [ ] **Step 5: Commit**

```powershell
git add scripts/systems/lifecycle tests/runtime/lifecycle/run_application_shutdown_contract_regression.cs
git commit -m "feat: add application shutdown contracts"
```

---

### Task 2: Pure shutdown pipeline and barrier failure

**Files:**
- Create: `scripts/systems/lifecycle/IApplicationShutdownHooks.cs`
- Create: `scripts/systems/lifecycle/ApplicationShutdownPipeline.cs`
- Create: `tests/runtime/lifecycle/run_application_shutdown_pipeline_regression.cs`

**Interfaces:**
- Consumes: Task 1 contracts.
- Produces: Godot-independent `ValueTask<ShutdownReport> ApplicationShutdownPipeline.RunAsync(ShutdownReport report)` shared by the autoload and fake test hooks.

- [ ] **Step 1: Write fake-hook order tests**

The successful fake records `quiesce, runtime, scene, release-gate, content, barrier-gate, barrier`. Add fakes for quiesce/runtime/scene exceptions, a false release gate, content-release exception, a false finalizer gate, and finalizer-barrier exception. Any failure before safe content release must leave `ContentCalled == false`; any failure after/before the finalizer gate must leave `BarrierCalled == false`.

The false release-gate case asserts:

```csharp
_test.True(report.FinalizerBarrierSkipped, "unsafe barrier is skipped");
_test.Eq(report.FinalPhase, ApplicationShutdownPhase.QuitRequested, "failure reaches quit");
_test.Eq(report.EffectiveExitCode, 1, "skipped barrier forces failure");
_test.False(fake.ContentCalled, "content roots remain while borrowers may be live");
_test.False(fake.BarrierCalled, "barrier is not forced with active owners");
```

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/lifecycle/run_application_shutdown_pipeline_regression.cs
```

Expected: missing pipeline/hook types.

- [ ] **Step 3: Implement the hook contract**

```csharp
internal interface IApplicationShutdownHooks
{
    ValueTask QuiesceAsync(ShutdownReport report);
    ValueTask DrainRuntimeAsync(ShutdownReport report);
    ValueTask DrainSceneAsync(ShutdownReport report);
    bool CanReleaseProcessContent(ShutdownReport report, out string failure);
    ValueTask ReleaseContentAsync(ShutdownReport report);
    bool CanRunFinalizerBarrier(ShutdownReport report, out string failure);
    void RunFinalizerBarrier(ShutdownReport report);
}
```

`RunAsync` catches and records each hook failure and continues only teardown that is safe after that failure. `CanReleaseProcessContent` runs after runtime/scene drain and rejects if either hook failed or any non-terminal owner/content borrower/scope/lease/job is active. On rejection, do not call `ReleaseContentAsync`; record `FinalizerBarrierSkipped` and a nonzero effective code. After release, `CanRunFinalizerBarrier` additionally requires zero canonical roots and no new lifecycle violation. Never record `ContentReleased` unless release completed, and never record `FinalizersDrained` unless the barrier returned normally.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/runtime/lifecycle/run_application_shutdown_pipeline_regression.cs
dotnet build magic.csproj
git add scripts/systems/lifecycle tests/runtime/lifecycle/run_application_shutdown_pipeline_regression.cs
git commit -m "feat: add application shutdown pipeline"
```

---

### Task 3: Normal GameSession close and late ProcessExit diagnostics

**Files:**
- Modify: `scripts/systems/persistence/GameSession.cs:257-329`
- Modify: `scripts/systems/persistence/GameSession.FinalizerSuppression.cs:145-208`
- Modify: `scripts/systems/content/GameRoot.cs:20-30`
- Modify: `scripts/utils/GodotObjectLifecycle.cs:4-69`
- Create: `scripts/systems/lifecycle/ApplicationLifetimeDiagnostics.cs`
- Create: `tests/shared/LifecycleMeasurementBarrier.cs`
- Create: `tests/runtime/lifecycle/run_game_session_close_lifecycle_regression.cs`

**Interfaces:**
- Consumes: Task 1 audit contracts.
- Produces: idempotent normal close with zero process-content suppression and a ProcessExit callback that calls no Godot API.

- [ ] **Step 1: Write session A/B failure coverage**

Create session A, query a known skill/item, call `Dispose()` twice, assert catalog revision changes once, await `LifecycleMeasurementBarrier.RunAsync(this)` while Godot lives, create session B, use the same content, and assert normal-close process suppress count is unchanged.

- [ ] **Step 2: Run red**

```powershell
$env:MAGIC_LIFECYCLE_STRICT='1'
godot --headless -s res://tests/runtime/lifecycle/run_game_session_close_lifecycle_regression.cs
```

Expected: current `_ExitTree` process-suppression path violates the assertion.

- [ ] **Step 3: Implement one normal-close path**

```csharp
internal void CloseNormal()
{
    if (_disposed)
        return;
    _disposed = true;
    DisposePartyStateGraph(_party_state);
    _party_state = null;
    DisposeOwnedRuntimeResources();
    RemoveLogSink();
}

public override void _ExitTree() => CloseNormal();
```

`Dispose()` calls `CloseNormal()` before `Free()`/`base.Dispose()`. Remove the suppression boolean from normal owner cleanup. Make `GameRoot.Dispose()` idempotent.

Replace active ProcessExit cleanup with `ApplicationLifetimeDiagnostics.RecordProcessExit(phase)` using `Console.Error`. It must not access `Engine.GetMainLoop`, traverse nodes, suppress wrappers, or collect GC.

`LifecycleMeasurementBarrier.RunAsync(SceneTree tree)` is a test-only correctness helper: await two process frames, run `GC.Collect → GC.WaitForPendingFinalizers → GC.Collect`, then await one process frame. It does not inspect owners or request shutdown. This is the only durable test-side location for direct GC calls.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/runtime/lifecycle/run_game_session_close_lifecycle_regression.cs
godot --headless -s res://tests/runtime/validation/run_game_root_content_catalog_regression.cs
dotnet build magic.csproj
git add scripts/systems/persistence scripts/systems/content/GameRoot.cs scripts/utils/GodotObjectLifecycle.cs scripts/systems/lifecycle/ApplicationLifetimeDiagnostics.cs tests/shared/LifecycleMeasurementBarrier.cs tests/runtime/lifecycle/run_game_session_close_lifecycle_regression.cs
git commit -m "fix: separate session close from process shutdown"
```

---

### Task 4: ApplicationLifetimeCoordinator autoload and unique Quit owner

**Files:**
- Create: `scripts/systems/lifecycle/ApplicationShutdownParticipantStage.cs`
- Create: `scripts/systems/lifecycle/IApplicationShutdownParticipant.cs`
- Create: `scripts/systems/lifecycle/ApplicationLifetimeCoordinator.cs`
- Modify: `project.godot:18-20`
- Modify: `scripts/systems/game_runtime/WorldMapSystem.cs:135-183`
- Modify: `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs`
- Modify: `scripts/systems/persistence/GameSession.cs`
- Modify: `scripts/utils/GodotObjectLifecycle.cs`
- Create: `tests/runtime/lifecycle/run_application_lifetime_coordinator_regression.cs`

**Interfaces:**
- Consumes: Tasks 1-3.
- Produces: window-close interception, participant teardown, safe barrier gate, and the only actual `SceneTree.Quit` call.

- [ ] **Step 1: Write the failing autoload regression**

Assert the coordinator exists before GameSession, `AutoAcceptQuit == false`, duplicate requests share one completion/report, a later failed/nonzero request can only raise the cached report's effective code, non-main-thread requests fail before touching Godot, participant ordering is deterministic, duplicate IDs/late registration fail, unregister is idempotent, participant exceptions are recorded, legal success/skipped histories are emitted, and window close submits the same request path.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/lifecycle/run_application_lifetime_coordinator_regression.cs
```

Expected: missing autoload/coordinator.

- [ ] **Step 3: Implement the Node adapter**

```csharp
internal enum ApplicationShutdownParticipantStage
{
    Runtime = 0,
    Session = 1,
}

internal interface IApplicationShutdownParticipant
{
    string ShutdownParticipantId { get; }
    ApplicationShutdownParticipantStage ShutdownStage { get; }
    int ShutdownOrder { get; }
    ValueTask CloseForApplicationShutdownAsync(ShutdownReport report);
}

public partial class ApplicationLifetimeCoordinator : Node, IApplicationShutdownHooks
{
    private readonly object _shutdownSync = new();
    private ApplicationShutdownPipeline _pipeline;
    private Task<ShutdownReport> _completion;
    private ShutdownReport _report;
    private int _mainThreadId;

    public override void _Ready()
    {
        _mainThreadId = Environment.CurrentManagedThreadId;
        GetTree().AutoAcceptQuit = false;
        _pipeline = new ApplicationShutdownPipeline(this, LifecycleAuditRegistry.Shared);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
            _ = RequestShutdownAsync(new ShutdownRequest(0, ShutdownReason.WindowClose));
    }

    internal ValueTask<ShutdownReport> RequestShutdownAsync(ShutdownRequest request)
    {
        EnsureMainThread();
        lock (_shutdownSync)
        {
            if (_completion == null)
            {
                _report = new ShutdownReport(request);
                _completion = _pipeline.RunAsync(_report).AsTask();
            }
            else
            {
                _report.MergeRequest(request);
            }
            return new ValueTask<ShutdownReport>(_completion);
        }
    }

    internal void RegisterParticipant(IApplicationShutdownParticipant participant);
    internal void UnregisterParticipant(IApplicationShutdownParticipant participant);
}
```

The participant registry stores weak references and is not an owner. It rejects duplicate IDs and registration after `Quiescing`; unregister is idempotent. Runtime then Session participants close in `ShutdownStage`/`ShutdownOrder`/ordinal ID order. `WorldMapSystem` owns/disposes its `GameRuntimeFacade` as the Runtime participant; `HeadlessGameTestSession` is the headless Runtime participant; `GameSession` is the Session participant. Child battle/services do not register separately because their top-level owner closes them.

`EnsureMainThread` compares `Environment.CurrentManagedThreadId` with the ID captured in `_Ready`. The first request supplies reason/requested code; later requests append diagnostics and can raise, never lower, the effective exit code through `ShutdownReport.MergeRequest`. Hooks reject new work after Quiescing, close participants, free/await the current scene and GameSession, invoke the recorded phase-1 legacy preparation bridge, evaluate non-terminal counters, run `GodotObjectLifecycle.CollectPendingFinalizers()` only when safe, print the pre-quit report, then call `GetTree().Quit(report.EffectiveExitCode)` once.

Move `WorldMapSystem` teardown into idempotent `CloseForApplicationShutdown()`; `_ExitTree` calls the same method.

Autoload order:

```ini
[autoload]

ApplicationLifetimeCoordinator="*res://scripts/systems/lifecycle/ApplicationLifetimeCoordinator.cs"
GameSession="*res://scripts/systems/persistence/GameSession.cs"
```

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/runtime/lifecycle/run_application_lifetime_coordinator_regression.cs
rg -n "GetTree\(\)\.Quit|SceneTree\.Quit" scripts --glob "*.cs"
dotnet build magic.csproj
git add project.godot scripts/systems/lifecycle scripts/systems/game_runtime/WorldMapSystem.cs scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs scripts/systems/persistence/GameSession.cs scripts/utils/GodotObjectLifecycle.cs tests/runtime/lifecycle/run_application_lifetime_coordinator_regression.cs
git commit -m "feat: coordinate application shutdown"
```

Expected: the scan reports only the coordinator.

---

### Task 5: TestResult adapter and all C# SceneTree exits

**Files:**
- Create: `tests/shared/TestResult.cs`
- Create: `tests/shared/TestExitCoordinator.cs`
- Create: `tests/shared/LifecycleTestSceneTree.cs`
- Create: `tools/migrate_test_exit_calls.py`
- Create: `tests/tooling/test_migrate_test_exit_calls.py`
- Create: `tests/tooling/test_exit_migration_manifest.txt`
- Modify: `tests/shared/TestHarness.cs:1-118`
- Modify: `tests/shared/GodotSharpCleanup.cs`
- Modify: the exact runner paths recorded in `tests/tooling/test_exit_migration_manifest.txt` by the codemod
- Modify: `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs:602-630`
- Modify: `tests/text_runtime/headless/run_headless_game_test_session_regression.cs:1-35`
- Modify: `tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs`

**Interfaces:**
- Consumes: Task 4 coordinator/request.
- Produces: `TestHarness.Finish -> TestResult`, one adapter/base class, and zero direct test GC/Quit calls.

- [ ] **Step 1: Add codemod unit tests**

Cover direct Finish, Finish with exit code, stored exit code followed by Quit, and direct `Quit(1)`. Unknown shapes must fail check mode; do not add implicit `TestResult -> int` conversion.

- [ ] **Step 2: Run red**

```powershell
python -m unittest tests.tooling.test_migrate_test_exit_calls -v
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs
```

Expected: missing codemod and direct-exit violations.

- [ ] **Step 3: Implement the shared API**

```csharp
internal sealed record TestResult(
    string Label,
    bool Passed,
    int ExitCode,
    IReadOnlyList<string> Failures
);

internal abstract partial class LifecycleTestSceneTree : SceneTree
{
    protected void RequestTestExit(TestResult result) =>
        TestExitCoordinator.Complete(this, result);
}

internal static class TestExitCoordinator
{
    internal static async void Complete(SceneTree tree, TestResult result)
    {
        var coordinator = tree.Root.GetNode<ApplicationLifetimeCoordinator>(
            "ApplicationLifetimeCoordinator"
        );
        await coordinator.RequestShutdownAsync(
            new ShutdownRequest(
                result.ExitCode,
                ShutdownReason.TestComplete,
                new ShutdownCallerResult(result.Label, result.Passed)
            )
        );
    }
}
```

`TestHarness.Finish` atomically snapshots failures and returns `TestResult`. Remove printing, test-local drain, and GC. Remove `HeadlessGameTestSession.PrepareForFinalizerDrain`, `GodotSharpCleanup.CollectPendingFinalizers`, and the 512 MiB `TryStartNoGCRegion` initializer.

- [ ] **Step 4: Generate and review the exact migration manifest**

```powershell
python tools/migrate_test_exit_calls.py --check --root tests --manifest tests/tooling/test_exit_migration_manifest.txt
Get-Content tests/tooling/test_exit_migration_manifest.txt
python tools/migrate_test_exit_calls.py --apply --root tests --manifest tests/tooling/test_exit_migration_manifest.txt
```

The codemod refuses unknown shapes. Convert every reported outlier explicitly.

- [ ] **Step 5: Run static gates**

```powershell
rg -n "TryStartNoGCRegion|CollectPendingFinalizers" tests --glob "*.cs"
rg -n ":\s*SceneTree" tests --glob "*.cs"
rg -n "\bQuit\s*\(" tests --glob "*.cs"
```

Expected: first and third scans have no output; SceneTree inheritance exists only through `LifecycleTestSceneTree`.

- [ ] **Step 6: Run representative tests and commit the mechanical migration**

```powershell
python -m unittest tests.tooling.test_migrate_test_exit_calls -v
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs
godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs
dotnet build magic.csproj
# Stage only paths in tests/tooling/test_exit_migration_manifest.txt plus the shared/tool files listed above; patch-stage the two protected AI test hunks.
git diff --cached --name-only
git diff --cached --check
git commit -m "test: route regressions through lifecycle shutdown"
```

---

### Task 6: Exact LegacyDebt gate

**Files:**
- Modify: `scripts/utils/GodotObjectOwnership.cs:6-747`
- Modify: `scripts/ui/BattleBoardController.cs:120-140`
- Modify: `tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs`
- Modify: `tests/runtime/lifecycle/run_application_lifetime_coordinator_regression.cs`

**Interfaces:**
- Consumes: Task 1 audit/report.
- Produces: exact debt metadata for the one production quarantine and static guards rejecting any additional production use.

- [ ] **Step 1: Add the failing debt assertion**

Allow exactly:

```text
debt_id=battle-board-controller-quarantine
source=scripts/ui/BattleBoardController.cs
delete_phase=2
```

Also assert normal session suppress count is zero, production Quit is coordinator-only, test adapter has no Quit, and ProcessExit diagnostics call no Godot API.

- [ ] **Step 2: Run red, add metadata, run green, and commit**

```powershell
$env:MAGIC_LIFECYCLE_STRICT='1'
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs
godot --headless -s res://tests/runtime/lifecycle/run_application_lifetime_coordinator_regression.cs
git add scripts/utils/GodotObjectOwnership.cs scripts/ui/BattleBoardController.cs tests/runtime
git commit -m "test: track lifecycle legacy debt"
```

---

### Task 7: Lifecycle-order runner profile and CI

**Files:**
- Modify: `tests/run_regression_suite.py:60-210,279-410`
- Modify: `tests/tooling/test_run_regression_suite.py`
- Modify: `.github/workflows/ci.yml`
- Modify: `docs/design/project_context_units.md`

**Interfaces:**
- Consumes: lifecycle tests from Tasks 1-6.
- Produces: reusable `--lifecycle-correctness` strict execution mode with strict/trace, retries 0, retained stage-1 baselines, and a dedicated lifecycle CI step.

- [ ] **Step 1: Write failing Python tests**

Prove the mode rejects retries other than 0, sets strict/trace, fails on finalizer markers even with process exit 0, retains baseline lines, does not rewrite the caller's pattern/jobs selection, and appears in CI before the existing full suite. The workflow test must assert that the dedicated step runs both the `runtime/lifecycle` runner pattern and `run_runtime_lifecycle_boundary_regression.cs`.

- [ ] **Step 2: Run red**

```powershell
python -m unittest tests.tooling.test_run_regression_suite -v
```

Expected: missing profile failures.

- [ ] **Step 3: Implement the profile**

The mode forces retries 0/strict/trace and never deletes unsafe/finalizer lines. It must not change discovery, `--pattern`, or `--jobs` so Phase 5 can reuse the same strict semantics for AI and the full suite. The dedicated Phase 1 CI step first runs the runner with `--pattern runtime/lifecycle --jobs 1`, then invokes `godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs` in the same strict environment. Keep the existing full-suite CI retry at 1 until Phase 5.

- [ ] **Step 4: Run the phase gate**

```powershell
python -m unittest tests.tooling.test_run_regression_suite -v
python tests/run_regression_suite.py --pattern runtime/lifecycle --jobs 1 --stop-on-failure --finalizer-crash-retries 0 --test-timeout-seconds 180 --fail-on-output-error --lifecycle-correctness
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs
godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs
dotnet build magic.csproj
git diff --check
```

Expected: all commands exit 0 without retry/finalizer fatal; remaining wrapper/resource counts are printed as exact baselines.

- [ ] **Step 5: Update current ownership docs and commit**

Update CU-02/CU-19/CU-21 only after the code exists, then:

```powershell
git add tests/run_regression_suite.py tests/tooling/test_run_regression_suite.py tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs .github/workflows/ci.yml docs/design/project_context_units.md
git diff --cached --name-only
git diff --cached --check
git commit -m "ci: add lifecycle correctness gate"
```
