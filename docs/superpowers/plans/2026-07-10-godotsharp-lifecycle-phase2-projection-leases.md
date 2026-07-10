# GodotSharp Lifecycle Phase 2 Projection and Native Leases Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace runtime suppress/quarantine ownership with deterministic native/projection leases for save, battle, AI, and UI boundaries while keeping process content ownership unchanged.

**Architecture:** Introduce `NativeLeaseScope` for exclusively-created pathless Godot wrappers and `GodotProjectionLease<T>` for short-lived collection graphs. Migrate one complete owner domain at a time, clearing borrowers before disposing owned wrappers. Convert dynamic AttributeModifier Resources and static Godot collections to typed managed definitions, then close battle/AI/UI borrower gaps.

**Tech Stack:** Godot 4.6.2 Mono, C#/.NET 8, headless C# regressions, Python regression runner.

## Global Constraints

- Phase 1 must be complete and its lifecycle-order gate green before starting.
- Preserve save version 12 and all golden payload/headless/AI fingerprints.
- Do not touch `ProcessContentHost` or content snapshot ownership in this phase.
- No reflection cleanup and no new `GC.SuppressFinalize`/quarantine call.
- Node and path-backed process content can be borrowed but never owned/disposed by a native/projection lease.
- Each migrated domain must remove its `RuntimeStateLifecycle.MarkValueGraphFinalizerless` calls before its commit.
- Preserve unrelated worktree edits; inspect `git status` before every task, especially `scripts/enemies/EnemyTemplateDef.cs`.
- Do not run battle simulation or balance runners.
- Treat each task's `Files` list as its staging manifest. Before every commit, inspect `git diff --cached --name-only`, `git diff --cached`, and `git diff --cached --check`; directory-level `git add` lines are scopes, not permission to stage unrelated changes.
- Keep the three current user-owned EnemyTemplate/AI regression paths unstaged unless explicitly modified; for overlap, stage only task hunks or stop if separation is impossible.
- Every new C# regression runner derives from `LifecycleTestSceneTree` and exits through `RequestTestExit(_test.Finish(label))`.

---

### Task 1: NativeLeaseScope and GodotProjectionLease

**Files:**
- Create: `scripts/utils/NativeLeaseScope.cs`
- Create: `scripts/utils/GodotProjectionLease.cs`
- Modify: `scripts/utils/GodotObjectOwnership.cs:460-732`
- Modify: `scripts/utils/RuntimeResourceFactories.cs`
- Create: `tests/runtime/validation/run_native_lease_scope_regression.cs`
- Create: `tests/runtime/validation/run_godot_projection_lease_regression.cs`

**Interfaces:**
- Consumes: Phase 1 `LifetimeDomain`/audit counters.
- Produces: deterministic native owner and explicitly-registered projection lease used by Tasks 2-6.

- [ ] **Step 1: Write failing ownership tests**

Cover Node rejection, path-backed Resource rejection, cross-owner rejection, reverse-order single disposal, transfer, closed `Value` access, explicit nested Array/Dictionary ownership, borrowed-child non-disposal, and owner/lease counters returning to baseline.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/validation/run_native_lease_scope_regression.cs
godot --headless -s res://tests/runtime/validation/run_godot_projection_lease_regression.cs
```

Expected: missing lease types.

- [ ] **Step 3: Implement the exact API**

```csharp
internal sealed class NativeLeaseScope : IDisposable
{
    internal NativeLeaseScope(string ownerId, LifetimeDomain domain);
    internal bool IsClosed { get; }

    internal T Own<T>(T wrapper, string reason)
        where T : class, IDisposable;

    internal T TransferTo<T>(NativeLeaseScope target, T wrapper, string reason)
        where T : class, IDisposable;

    public void Dispose();
}

internal sealed class GodotProjectionLease<T> : IDisposable
    where T : class, IDisposable
{
    internal static GodotProjectionLease<T> CreateOwnedRoot(
        T root,
        string ownerId,
        LifetimeDomain domain,
        string reason
    );

    internal T Value { get; }
    internal TOwned Own<TOwned>(TOwned wrapper, string reason)
        where TOwned : class, IDisposable;

    internal TBorrowed Borrow<TBorrowed>(TBorrowed wrapper, string reason)
        where TBorrowed : class;

    public void Dispose();
}
```

Projection builders call `CreateOwnedRoot` for the root and `Own` immediately for every nested container they create. `Borrow` records a child for audit only and never adds it to the dispose list. The lease performs no graph traversal, field/property inspection, or implicit child adoption. Every registration validates domain, path, existing owner, and closed state before mutating the lease; owned wrappers dispose in reverse explicit registration order exactly once.

- [ ] **Step 4: Run green and static guard**

```powershell
godot --headless -s res://tests/runtime/validation/run_native_lease_scope_regression.cs
godot --headless -s res://tests/runtime/validation/run_godot_projection_lease_regression.cs
rg -n "SuppressFinalize|GodotTypedResourceGraphWalker|VisitValueGraph|VisitWrappers" scripts/utils/NativeLeaseScope.cs scripts/utils/GodotProjectionLease.cs
dotnet build magic.csproj
```

Expected: tests PASS; rg has no output.

- [ ] **Step 5: Commit**

```powershell
git add scripts/utils tests/runtime/validation/run_native_lease_scope_regression.cs tests/runtime/validation/run_godot_projection_lease_regression.cs
git commit -m "feat: add native and projection leases"
```

---

### Task 2: Plain AttributeModifier definitions and static collection cleanup

**Files:**
- Create by moving the existing declaration: `scripts/player/progression/AttributeModifierDefinition.cs`
- Modify: `scripts/player/progression/AttributeModifier.cs`
- Modify: `scripts/player/progression/SkillDefinition.cs:277-369`
- Modify: `scripts/player/progression/DerivedAttributeRule.cs`
- Modify: `scripts/systems/attributes/AttributeSourceContext.cs`
- Modify: `scripts/systems/attributes/AttributeService.cs:150-203,542-709,963-1011`
- Modify: `scripts/systems/inventory/PartyEquipmentService.cs:214-232,556-576`
- Modify: `scripts/systems/progression/CharacterTraitService.cs:52-60,445-476`
- Modify: `scripts/systems/progression/CharacterManagementModule.cs:496-506`
- Modify: `scripts/enemies/EnemyTemplateDef.cs:11`
- Modify: `scripts/systems/settlement/SettlementForgeService.cs:9`
- Modify: `scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs:29`
- Modify: `scripts/systems/battle/core/special_profiles/BattleSpecialProfileManifestValidator.cs:12-55`
- Modify: `scripts/systems/progression/CharacterCreationService.cs:23`
- Test: `tests/progression/identity/run_attribute_source_context_regression.cs`
- Test: `tests/progression/core/run_attribute_trait_modifier_regression.cs`
- Test: `tests/progression/core/run_character_trait_service_regression.cs`
- Test: `tests/progression/core/run_character_management_trait_attribute_regression.cs`

**Interfaces:**
- Consumes: authored `AttributeModifier : Resource` at the resource boundary only.
- Produces: immutable `AttributeModifierDefinition` and CLR-backed `DerivedAttributeRule` for runtime services.

- [ ] **Step 1: Add failing plain-type assertions**

Assert runtime contexts/services expose `IReadOnlyList<AttributeModifierDefinition>`, all three dynamic construction paths return definitions, and static Godot collection source patterns are absent.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/progression/core/run_attribute_trait_modifier_regression.cs
rg -n "new\s+AttributeModifier\b" scripts --glob "*.cs"
```

Expected: existing dynamic Resource paths are reported.

- [ ] **Step 3: Add the definition and projection**

```csharp
public sealed class AttributeModifierDefinition
{
    public AttributeModifierDefinition(
        StringName attributeId,
        StringName mode,
        int value,
        int valuePerRank,
        StringName sourceType,
        StringName sourceId
    )
    {
        AttributeId = attributeId;
        Mode = mode;
        Value = value;
        ValuePerRank = valuePerRank;
        SourceType = sourceType;
        SourceId = sourceId;
    }

    public StringName AttributeId { get; }
    public StringName Mode { get; }
    public int Value { get; }
    public int ValuePerRank { get; }
    public StringName SourceType { get; }
    public StringName SourceId { get; }

    internal static AttributeModifierDefinition FromResource(AttributeModifier source) =>
        new(source.attribute_id, source.mode, source.value, source.value_per_rank,
            source.source_type, source.source_id);

    public int GetValueForRank(int rank) =>
        Value + ValuePerRank * (Math.Max(rank, 1) - 1);

    public bool IsPercent() =>
        AttributeModifier.ToMode(Mode) == AttributeModifierMode.Percent;

    public bool IsFlat() =>
        AttributeModifier.ToMode(Mode) == AttributeModifierMode.Flat;
}
```

Move the current `AttributeModifierDefinition` declaration out of `SkillDefinition.cs` into the new file in the same commit; do not leave a second declaration. Change `DerivedAttributeRule` to accept `IReadOnlyDictionary<StringName,int>` and evaluate `IReadOnlyDictionary`. Replace static Godot collections with arrays, HashSets, or read-only dictionaries; preserve exact ordering where tests depend on it.

- [ ] **Step 4: Run focused and static green**

```powershell
godot --headless -s res://tests/progression/identity/run_attribute_source_context_regression.cs
godot --headless -s res://tests/progression/core/run_attribute_trait_modifier_regression.cs
godot --headless -s res://tests/progression/core/run_character_trait_service_regression.cs
godot --headless -s res://tests/progression/core/run_character_management_trait_attribute_regression.cs
rg -n "new\s+AttributeModifier\b" scripts --glob "*.cs"
rg -n "private static readonly (Godot\.Collections\.(Array|Dictionary)|G(Array|Dictionary))" scripts --glob "*.cs"
dotnet build magic.csproj
```

Expected: tests/build PASS; both rg commands have no production matches.

- [ ] **Step 5: Commit**

```powershell
# Stage only the exact Task 2 Files manifest; patch-stage EnemyTemplateDef lifecycle hunks.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: use plain attribute modifier definitions"
```

---

### Task 3: Save payload and FileAccess transaction lease

**Files:**
- Modify: `scripts/systems/persistence/SaveSerializer.cs`
- Modify: `scripts/systems/persistence/SaveRepository.cs`
- Modify: `scripts/systems/persistence/FileIOCoordinator.cs:7-68`
- Modify: `scripts/systems/persistence/GameSession.cs:1367-1405,1532-1557,1660-1663`
- Modify: `scripts/systems/persistence/GameSession.SaveIndexAndFileIO.cs:26-210,280-349`
- Modify: `scripts/utils/RuntimePlainPayload.cs`
- Create: `tests/runtime/validation/run_save_projection_lease_regression.cs`
- Test: `tests/runtime/persistence/run_save_serializer_quest_round_trip_regression.cs`
- Test: `tests/runtime/persistence/run_invalid_save_graceful_regression.cs`
- Test: `tests/runtime/persistence/run_save_payload_string_minimization_regression.cs`

**Interfaces:**
- Consumes: Task 1 projection lease.
- Produces: lease-returning save/index projections and transaction-bound FileAccess write ownership.

- [ ] **Step 1: Write failure/exception/closed-access tests**

Assert successful write, injected write failure, and thrown exception all restore the projection counter; closed lease `Value` throws; save version/key/value shape remains identical.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/runtime/validation/run_save_projection_lease_regression.cs
```

Expected: raw payload methods leak outside a transaction.

- [ ] **Step 3: Change the boundary signatures**

```csharp
private GodotProjectionLease<GDictionary> BuildSavePayloadLease(
    int savedAtUnixTime
);
private GodotProjectionLease<GDictionary> BuildWorldStatePayloadLease();
public GodotProjectionLease<GDictionary> BuildSaveIndexPayloadLease(
    GDictionaryArray entries
);

internal int WriteSavePayloadAtomically(
    string savePath,
    GodotProjectionLease<GDictionary> payload
);
```

Callers use:

```csharp
using GodotProjectionLease<GDictionary> payload = BuildSavePayloadLease(now);
return WriteSavePayloadAtomically(path, payload);
```

Read-side `GetVar()` is normalized immediately into plain/typed data and no raw/duplicated `GDictionary` remains in session fields. Remove migrated `RuntimeStateLifecycle` calls.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/runtime/validation/run_save_projection_lease_regression.cs
godot --headless -s res://tests/runtime/persistence/run_save_serializer_quest_round_trip_regression.cs
godot --headless -s res://tests/runtime/persistence/run_invalid_save_graceful_regression.cs
godot --headless -s res://tests/runtime/persistence/run_save_payload_string_minimization_regression.cs
dotnet build magic.csproj
git add scripts/systems/persistence scripts/utils/RuntimePlainPayload.cs tests/runtime
git commit -m "refactor: lease save payload projections"
```

---

### Task 4: Battle event, preview, and AI trace leases

**Files:**
- Create: `scripts/systems/battle/core/BattleEventBatchProjection.cs`
- Create: `scripts/systems/battle/core/BattlePreviewProjection.cs`
- Modify: `scripts/systems/battle/core/BattleEventBatch.cs`
- Modify: `scripts/systems/battle/core/BattlePreview.cs`
- Modify: `scripts/systems/battle/core/BattleSaveBranchPreviewData.cs`
- Modify: `scripts/systems/battle/rules/BattleDamagePreviewProjection.cs`
- Modify: `scripts/systems/battle/rules/BattleDamagePreviewRangeProjection.cs`
- Modify: `scripts/systems/battle/ai/BattleAiTurnTracePayloadProjection.cs`
- Modify: `scripts/systems/battle/ai/BattleAiTurnTraceProjection.cs`
- Modify: `scripts/enemies/TraceDictionaryProjection.cs`
- Modify: `scripts/dev_tools/AiTraceRecorder.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.AiTrace.cs`
- Create: `tests/battle_runtime/runtime/run_battle_projection_lease_regression.cs`
- Create: `tests/battle_runtime/ai/run_battle_ai_trace_projection_lease_regression.cs`

**Interfaces:**
- Consumes: projection lease infrastructure.
- Produces: plain core views and lease-returning Godot boundary projections.

- [ ] **Step 1: Add failing lease-counter/fingerprint assertions**

Assert repeated preview/report/trace access does not create an unowned Godot collection, nested containers belong to one root lease, and AI/battle fingerprints remain unchanged.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_battle_projection_lease_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_trace_projection_lease_regression.cs
```

- [ ] **Step 3: Introduce explicit projection entry points**

```csharp
internal static GodotProjectionLease<GDictionary> BuildLease(BattlePreview preview);
internal static GodotProjectionLease<GDictionary> BuildLease(BattleEventBatch batch);
internal static GodotProjectionLease<GDictionary> BuildLease(
    BattleAiTurnTraceProjection trace
);
```

`BattleEventBatch.ReportEntriesTyped` becomes plain read-only data. Core state stores typed facts only; Godot containers are built once inside the lease. Remove migrated `RuntimeStateLifecycle` calls.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_battle_projection_lease_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_trace_projection_lease_regression.cs
python tests/run_regression_suite.py --pattern battle_runtime/ai --jobs 16 --finalizer-crash-retries 0
dotnet build magic.csproj
# Stage only the exact Task 4 Files manifest.
git diff --cached --name-only
git diff --cached --check
git commit -m "refactor: lease battle projections"
```

---

### Task 5: HUD typed snapshots and BattleBoard scene lease

**Files:**
- Create: `scripts/systems/battle/presentation/BattleHudSnapshot.cs`
- Create: `scripts/systems/battle/presentation/BattleHoverSnapshot.cs`
- Modify: `scripts/systems/battle/presentation/BattleHudAdapter.cs`
- Modify: `scripts/ui/BattleMapPanel.cs`
- Modify: `scripts/ui/BattleHoverPreviewOverlay.cs`
- Modify: `scripts/ui/BattleBoardRenderProfile.cs`
- Modify: `scripts/ui/BattleBoardController.cs:108-244,1309-1685`
- Modify: `scripts/ui/BattleBoard2D.cs:31-115,298-359`
- Create: `tests/battle_runtime/presentation/run_battle_hud_typed_projection_regression.cs`
- Create: `tests/battle_runtime/presentation/run_battle_board_native_lease_regression.cs`

**Interfaces:**
- Consumes: native/projection leases.
- Produces: plain HUD/hover view models and a controller-owned render-generation native lease.

- [ ] **Step 1: Add failing clear/rebind/exit tests**

Cover board `Clear() -> reconfigure`, double Dispose, path-backed texture borrow, pathless TileSet/ImageTexture ownership, scene exit counter baseline, and unchanged HUD snapshot/signature.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/battle_runtime/presentation/run_battle_hud_typed_projection_regression.cs
godot --headless -s res://tests/battle_runtime/presentation/run_battle_board_native_lease_regression.cs
```

- [ ] **Step 3: Apply the owner boundary**

```csharp
public sealed class BattleBoardController : IDisposable
{
    private NativeLeaseScope _renderLease;

    public void Clear()
    {
        _renderLease?.Dispose();
        _renderLease = null;
        ClearBorrowedFields();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Clear();
        _disposed = true;
    }
}
```

Use CLR lists for layer/coord collections. Own pathless TileSet/atlas/Image/ImageTexture in the render lease; borrow path-backed textures. `BattleBoard2D._ExitTree()` calls `Dispose()`. Remove `quarantineOnDrain: true` and its Phase 1 debt.

- [ ] **Step 4: Run green/static gate and commit**

```powershell
godot --headless -s res://tests/battle_runtime/presentation/run_battle_hud_typed_projection_regression.cs
godot --headless -s res://tests/battle_runtime/presentation/run_battle_board_native_lease_regression.cs
rg -n "quarantineOnDrain:\s*true" scripts
dotnet build magic.csproj
git add scripts/systems/battle/presentation scripts/ui tests/battle_runtime/presentation
git commit -m "refactor: own battle presentation wrappers"
```

Expected: rg has no production output.

---

### Task 6: AI decision borrower and BattleRuntime teardown

**Files:**
- Create: `scripts/systems/battle/ai/BattleAiDecisionResult.cs`
- Create: `scripts/systems/battle/ai/BattleAiMutationSnapshot.cs`
- Modify: `scripts/systems/battle/ai/BattleAiContext.cs:441-500`
- Modify: `scripts/systems/battle/ai/BattleAiService.cs:66-130`
- Modify: `scripts/systems/battle/ai/BattleAiScoreService.cs:18-50`
- Modify: `scripts/systems/battle/ai/BattleAiRuntimeActionPlan.cs:454-475`
- Modify: `scripts/systems/battle/ai/BattleAiMutationGuard.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeServices.cs:191-327`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs:945-1046,2573-2680`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.ContentSync.cs`
- Create: `tests/battle_runtime/ai/run_battle_ai_decision_lifetime_regression.cs`
- Create: `tests/battle_runtime/runtime/run_battle_runtime_borrower_teardown_regression.cs`
- Test: `tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs`

**Interfaces:**
- Consumes: lease infrastructure and plain trace projection.
- Produces: decision result copied before context clear and borrower-first battle teardown.

- [ ] **Step 1: Add success/wait/error/finally tests**

Every path must clear state/unit/plan/catalog/callback references. The mutation checkpoint must store CLR snapshots only, restore the same battle state/fingerprint, and return its owner/lease vector to baseline. Double Dispose must not change counters. Battle Dispose must clear `_skillCatalog` and all skill/trait/equipment/item/enemy/special-profile indexes.

- [ ] **Step 2: Run red**

```powershell
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_decision_lifetime_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_runtime_borrower_teardown_regression.cs
```

- [ ] **Step 3: Return copied decision data and clear context in finally**

```csharp
internal sealed class BattleAiDecisionResult
{
    internal BattleAiDecision Decision { get; init; }
    internal BattleAiTurnTraceProjection TurnTrace { get; init; }
}

internal BattleAiDecisionResult ChooseCommand(BattleAiContext context, bool captureTrace)
{
    try
    {
        return ChooseAndCopyResult(context, captureTrace);
    }
    finally
    {
        context.ClearRuntimeBindings();
        _scoreService.EndDecisionScope();
    }
}
```

`BattleAiMutationSnapshot` stores typed CLR unit/state/command facts. Replace `GDictionary`/`GArray` fields and `ToGodotDictionary`, `SetUnitsFromDictionary`, and `DuplicateStringNameArray` round-trips in `BattleAiMutationGuard`. Restore writes through typed battle-state APIs; any Godot API projection is created inside a short `GodotProjectionLease` and never stored in the checkpoint.

Teardown order: clear runtime bindings/result, dispose plans, AI and sidecars, clear all content borrowers/indexes, clear state/topology, close battle leases. `BattleAiScoreService` clears borrower fields before disposing its scope.

- [ ] **Step 4: Run green and commit**

```powershell
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_decision_lifetime_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_mutation_guard_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_battle_runtime_borrower_teardown_regression.cs
rg -n "GDictionary|GArray|ToGodotDictionary|SetUnitsFromDictionary|DuplicateStringNameArray" scripts/systems/battle/ai/BattleAiMutationGuard.cs
python tests/run_regression_suite.py --pattern battle_runtime/ai --jobs 16 --finalizer-crash-retries 0
dotnet build magic.csproj
# Stage only the exact Task 6 Files manifest.
git diff --cached --name-only
git diff --cached --check
git commit -m "fix: clear battle and AI borrowers before teardown"
```

---

### Task 7: Phase 2 cumulative gate and context map

**Files:**
- Modify: `tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs`
- Modify: Phase 1 lifecycle runner/config in `tests/run_regression_suite.py`
- Modify: `docs/design/project_context_units.md`

**Interfaces:**
- Consumes: Tasks 1-6.
- Produces: exact phase-2 static/runtime gates while leaving process content debt for Phase 3.

- [ ] **Step 1: Add cumulative assertions**

Assert migrated domains return owner/lease/scope counters to their pre-call vector, production quarantine is zero, exact source pattern `new\s+AttributeModifier\b` is zero, migrated files no longer call `RuntimeStateLifecycle`, and process-content static pool remains the only declared later-phase debt.

- [ ] **Step 2: Run the complete phase gate**

```powershell
$env:MAGIC_LIFECYCLE_STRICT='1'
godot --headless -s res://tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs
godot --headless -s res://tests/runtime/validation/run_save_projection_lease_regression.cs
godot --headless -s res://tests/battle_runtime/presentation/run_battle_board_native_lease_regression.cs
godot --headless -s res://tests/battle_runtime/ai/run_battle_ai_decision_lifetime_regression.cs
python tests/run_regression_suite.py --pattern battle_runtime/ai --jobs 16 --finalizer-crash-retries 0
dotnet build magic.csproj
git diff --check
```

Expected: all commands exit 0; no production quarantine; migrated lease counters return exactly to baseline.

- [ ] **Step 3: Update implemented boundaries and commit**

Update CU-14/CU-15/CU-16/CU-18/CU-19 without describing Phase 3 as implemented.

```powershell
git add tests/runtime/validation/run_runtime_lifecycle_boundary_regression.cs tests/run_regression_suite.py docs/design/project_context_units.md
git commit -m "test: enforce projection lease boundaries"
```
