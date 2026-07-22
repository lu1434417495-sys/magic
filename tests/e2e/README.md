# Application E2E tests

This folder covers the application boundary that the ordinary service/runtime regressions do not: the project main scene, canonical autoloads, real Godot input propagation, and cross-scene UI flows.

## Rules

- Start every scenario through `E2eSceneTree.LoadProjectMainSceneAsync<T>()`. The path is read from `application/run/main_scene`; tests do not instantiate a substitute login scene.
- Drive user actions through `E2eInputDriver`. Keyboard and action events use `Input.ParseInputEvent`; pointer events use `Viewport.PushInput`. Do not call button callbacks, emit `Pressed`, or invoke gameplay/runtime commands from an E2E scenario.
- Every press and release is separated by at least one process frame. `E2eSceneTree` also releases any held synthetic input before requesting the shared lifecycle shutdown.
- Wait for observable state with `E2eWait.UntilAsync` or `UntilValueAsync`. The usual overload has a process-frame cap; the real-time overload stops at either a frame cap or a monotonic millisecond deadline for production behavior that intentionally depends on wall-clock time. All variants poll once per process frame; do not add fixed sleeps or timers.
- Let `E2eSceneTree` finish through `TestHarness`, `TestResourceOwnership`, `TestExitCoordinator`, and `ApplicationLifetimeCoordinator`. Scenarios must not call `Quit`, force GC/finalizers, or implement their own shutdown path.
- Run scenarios through the dedicated outer E2E runner. It must isolate `user://` with temporary platform data directories so tests never read or overwrite player saves or display settings.
- Save-writing/loading flows require the outer runner's isolation marker and absolute sandbox root; the C# base verifies `OS.GetUserDataDir()` is actually inside that root before continuing. Directly launching those scripts is intentionally rejected.
- Scenarios that declare a deterministic random seed configure `TrueRandomSeedService` before the production main scene starts. This controls only randomness; UI input, battle rules, objective evaluation, writeback, and persistence still use their normal owners.

## Shared API

- `E2eSceneTree`: scenario template, main-scene loading, current-scene waits, failure capture, input cleanup, and unified shutdown.
- `E2eSceneTree.CreateTestGameThroughUiAsync`: reusable login-to-world flow that refuses to write unless the outer runner has marked user data as isolated.
- `E2eWait`: next-frame, bounded frame waits, and bounded predicate/value polling, including a wall-clock-bounded variant for real-time production delays.
- `E2eInputDriver`: action taps, key taps, Unicode text input, control-center clicks, coordinate clicks inside a live control, and best-effort release cleanup.

## Registered scenarios

- `run_cold_boot_e2e.cs`: starts the configured main scene and verifies the canonical application owners plus the usable login surface.
- `run_new_game_e2e.cs` + `run_load_game_e2e.cs`: two separate Godot processes create a game and then cold-start against the same isolated sandbox to load it through the login UI.
- `run_world_save_mutation_e2e.cs` + `run_world_save_reload_e2e.cs`: two separate Godot processes move through the real world-map input path, let canonical shutdown persist the pending runtime state, and cold-load the same sandbox to verify the restored coordinate and world step.
- `run_enter_battle_e2e.cs`: enters a generated encounter through world-map input and waits for the battle surface to become ready.
- `run_battle_round_trip_e2e.cs`: enters a generated encounter, uses the real skill grid, battle-board coordinates, movement, and resolve button to play the encounter, confirms any required promotion/reward UI, and verifies formal encounter cleanup, save flush, save-lock release, and return to the world map. The declared deterministic seed fixes the production random stream for repeatability; it does not inject commands or force victory.

Run all five scenarios from the repository root:

```bash
dotnet build magic.csproj
# On a fresh checkout, import resources once before the first E2E run:
godot --headless --import --quit --path .
python tests/run_e2e_suite.py --fail-on-output-error
```

Select one scenario with `--scenario cold_boot`, `--scenario new_and_load`, `--scenario world_save_round_trip`, `--scenario enter_battle`, or `--scenario battle_round_trip`. Use `--list` to inspect the registered process steps. The ordinary `run_regression_suite.py` deliberately excludes this folder; application E2E is serialized and opt-in because it owns isolated process-level user data.

Headless runs prove semantic scene and UI behavior, not pixel output, native window behavior, DPI, or mouse hit geometry under a real display server. Rendered smoke coverage belongs in a separate non-headless mode.
