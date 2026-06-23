# Chain Contingency Task 6 Report

## RED Evidence

- `godot --headless -s res://tests/text_runtime/commands/run_contingency_text_commands_regression.cs`
  - RED: failed before production code with `未知 party 子命令 contingency。` / missing `party.contingency_last_result` and `party.contingency_status_by_member` surfaces.
- `godot --headless -s res://tests/world_map/ui/run_contingency_setup_window_regression.cs`
  - RED: failed before production code because `ContingencySetupWindow` scene/script did not exist.

## GREEN / Build Evidence

- `godot --headless -s res://tests/text_runtime/commands/run_contingency_text_commands_regression.cs`
  - PASS: `Contingency text commands regression: PASS`
- `godot --headless -s res://tests/world_map/ui/run_contingency_setup_window_regression.cs`
  - PASS: `Contingency setup window regression: PASS`
- `dotnet build magic.csproj`
  - PASS: `0 Warning(s), 0 Error(s)`

## Commit Hash

- Final commit hash is recorded in the final task response. The exact hash cannot be embedded in this committed file without changing the commit hash.

## Changed Files

- `.superpowers/sdd/chain-contingency-task-6-report.md`
- `docs/design/project_context_units.md`
- `scenes/ui/contingency_setup_window.tscn`
- `scenes/ui/party_management_window.tscn`
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`
- `scripts/systems/game_runtime/IGameRuntimeSnapshotSource.cs`
- `scripts/systems/game_runtime/WorldMapSystem.cs`
- `scripts/systems/game_runtime/headless/GameTextCommandRunner.cs`
- `scripts/ui/ContingencySetupWindow.cs`
- `scripts/ui/PartyManagementWindow.cs`
- `scripts/utils/GameTextSnapshotRenderer.cs`
- `tests/shared/SnapshotTestRuntime.cs`
- `tests/text_runtime/commands/run_contingency_text_commands_regression.cs`
- `tests/world_map/ui/run_contingency_setup_window_regression.cs`

## Signals Changed

- Added `PartyManagementWindow.contingency_setup_requested(member_id)`.
- Added `ContingencySetupWindow.save_requested(member_id, setup_payload_name)`.
- Added `ContingencySetupWindow.charge_requested(member_id, setup_id)`.
- Added `ContingencySetupWindow.clear_charge_requested(member_id, setup_id)`.
- Added `ContingencySetupWindow.closed()`.

## Manual Godot Editor Checks

- Not run in the interactive Godot editor.
- Covered by headless UI scene regression and C# build.

## Concerns

- `special_contingency_gem` remains test-local content for the headless regression; production content can add a real item definition later.
- Exact commit hash is reported outside this committed report because a file cannot self-contain its final commit hash.
