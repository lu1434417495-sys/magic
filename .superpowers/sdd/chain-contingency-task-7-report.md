# Chain Contingency Task 7 Report

## RED

Command:

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs
```

Failure reason:

- The new lifecycle regression could not instantiate because the production API did not exist yet.
- Follow-up compile command:

```powershell
dotnet build magic.csproj
```

showed the expected missing API failures: `BattleUnitState` had no `MarkContingencySetupConsumed` method and `BattleEndResult` did not exist.

## GREEN

Command:

```powershell
dotnet build magic.csproj
```

PASS summary:

- Build succeeded.
- 0 warnings.
- 0 errors.

Command:

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs
```

PASS summary:

- `Contingency battle lifecycle regression: PASS`

Command:

```powershell
git diff --check
```

PASS summary:

- No whitespace errors reported.

## Files Changed

- `scripts/systems/battle/core/BattleUnitState.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/progression/CharacterBattleWritebackService.cs`
- `scripts/systems/progression/CharacterManagementModule.cs`
- `tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs`
- `.superpowers/sdd/chain-contingency-task-7-report.md`

## Context Map

`docs/design/project_context_units.md` did not need an update. The affected ownership boundaries and recommended read sets remain the same: battle runtime settles into the progression writeback boundary, and the finalization transaction remains owned by the game runtime facade.

## Notes And Deferred Scope

- Added an in-memory consumed setup ID list on `BattleUnitState` so future battle runtime code can supply zero-or-more consumed setup IDs without persisting battle-time overlay state into saves.
- Added progression writeback that clears charged consumed setups without refunding materials and without increasing current MP as part of the setup clear.
- Added battle end result plumbing so finalization can detect contingency settlement and flush failures.
- Added finalization rollback around local writeback, loot, contingency settlement, persistence, and flush failure paths.
- Deferred Task 8 behavior remains intentionally unimplemented: no `BattleContingencySystem`, trigger matching, release queue, suppression rules, overlay owner, or activation logic was added here.

## Review Fix Report

### RED: Battle-Sidecar And Resolution Rollback

Command:

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs
```

Failure reason:

- Added `TestLowLuckSidecarRollbackSurvivesLateFlushFailure`.
- The test failed because a forced late flush failure left low-luck loot merged into the in-memory `BattleResolutionResult`.
- Failure summary: `Rollback after late failure should restore battle resolution loot mutations. | actual=1 expected=0`.

### RED: Resource Commit Failure Channel

Command:

```powershell
dotnet build magic.csproj
```

Failure reason:

- Added resource commit failure coverage using the desired typed result channel.
- Build failed because `BattleResourceCommitResult` did not exist and `IBattleRuntimeCharacterGateway.CommitBattleResources(...)` still returned `void`.
- Failure summary: missing `BattleResourceCommitResult` and interface return-type mismatch for the test fake.

### GREEN

Command:

```powershell
dotnet build magic.csproj
```

PASS summary:

- Build succeeded.
- 0 warnings.
- 0 errors.

Command:

```powershell
godot --headless -s res://tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs
```

PASS summary:

- `Contingency battle lifecycle regression: PASS`

Command:

```powershell
git diff --check
```

PASS summary:

- No whitespace errors reported.

### Fix Files Changed

- `scripts/systems/battle/core/BattleResolutionResult.cs`
- `scripts/systems/battle/fate/FateRuntimeModule.cs`
- `scripts/systems/battle/fate/LowLuckEventService.cs`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `scripts/systems/battle/runtime/IBattleRatingCharacterGateway.cs`
- `scripts/systems/battle/sim/BattleSimFormalCombatFixture.cs`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/progression/CharacterBattleWritebackService.cs`
- `scripts/systems/progression/CharacterManagementModule.cs`
- `tests/battle_runtime/benchmarks/run_longsword_3v3_mastery_analysis.cs`
- `tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs`
- `tests/battle_runtime/skills/run_battle_weapon_dice_regression.cs`
- `.superpowers/sdd/chain-contingency-task-7-report.md`

### Context Map

`docs/design/project_context_units.md` did not need an update. The fix stays within the existing battle runtime / fate sidecar / progression writeback / finalization rollback boundaries, and the recommended read sets did not change.

### Notes And Deferred Scope

- Added battle-local rollback snapshots for low-luck event memory and in-memory `BattleResolutionResult` loot/overflow mutations.
- Added `BattleResourceCommitResult` as the typed resource writeback result channel on `IBattleRuntimeCharacterGateway`.
- `EndBattle()` now returns `battle_resource_commit_failed` and skips later flush when a resource commit fails.
- `FinalizeBattleResolution()` now rolls back the same pre-finalization memory snapshot on resource commit failure.
- Task 8 behavior remains deferred: no contingency trigger matching, release queue, suppression, overlay owner, or `BattleContingencySystem` was added.
