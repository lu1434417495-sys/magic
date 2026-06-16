---
name: godot-test-writing
description: Write and revise focused regression tests for this Godot 4.6 C# repository. Use when Codex needs to add, update, or evaluate tests under `tests/`, choose between service/runtime/headless/text/snapshot coverage, decide what assertions belong in a test, reuse project shared test fixtures, or verify that a change has appropriate Godot headless regression coverage.
---

# Godot Test Writing

Use this skill to add narrow, maintainable regressions that protect runtime contracts without turning tests into implementation mirrors.

## Load Context

1. Read `docs/design/project_context_units.md` first.
2. Map the requested change to the owning context unit and always include CU-19. Include CU-21 when the test drives `HeadlessGameTestSession`, `GameTextCommandRunner`, text snapshots, or command scripts.
3. Read existing tests in the nearest `tests/<domain>/` folder before writing a new runner. Use `rg` or `find` to discover actual filenames; do not guess test paths.
4. Read the owner code and nearby helpers that the existing tests use. Prefer matching local patterns over introducing a new test style.

## Choose Test Layer

Choose the narrowest layer that proves the behavior:

- **Pure service/rule test**: Instantiate the service, DTO, validator, or rules class directly when the bug is local and does not require scene or session state.
- **Runtime/facade test**: Use `GameRuntimeFacade`, `WorldMapRuntimeProxy`, or owner modules when the behavior is a cross-system command, modal, reward, warehouse, battle, or world transition.
- **Headless session test**: Use `HeadlessGameTestSession` when the behavior requires `GameSession + GameRuntimeFacade` lifecycle, save locks, content catalog setup, or world loading without UI.
- **Text command/snapshot test**: Use `GameTextCommandRunner` for automation-facing command behavior and snapshot fields. Follow `tests/text_runtime/README.md`.
- **Scene/UI schema test**: Test scene-facing payload shape, signal/callable contracts, and stable UI data. Do not use UI tests for rule logic that belongs in services.
- **Battle simulation or balance test**: Do not include routine numeric simulation, balance, benchmark, or analysis runners unless the user explicitly asks for simulation or balance work. Use `battle-sim-analysis` for that workflow.

## Reuse Shared Framework

Prefer existing project fixtures:

- Use `tests/shared/TestHarness.cs` for assertions and final status. Avoid ad hoc `GD.PushError` assertion frameworks in new tests.
- Use `GodotSharpCleanup.CollectPendingFinalizers()` before `Quit(...)` when a runner creates/disposes Godot C# objects, touches many `Variant` boundaries, or has native wrapper lifecycle risk.
- Use `SnapshotTestRuntime` for snapshot renderer tests instead of rebuilding a fake runtime source from scratch.
- Reuse deterministic damage resolvers in `tests/shared/` and domain helpers in `tests/battle_runtime/helpers/` before adding new battle test doubles.
- Use `HeadlessGameTestSession.GetGameSessionTyped()` / `GetRuntimeFacadeTyped()` and typed runtime APIs. Do not route formal test setup through `GodotObject.Call(...)` or string method names when typed APIs exist.
- Use `GameTextCommandRunner` for text automation flows and call `Dispose(true)` when finished.
- Put repeated helpers in `tests/shared/` or the nearest domain helper folder only after at least two tests need them. Keep one-off builders local to the runner.

## Write The Runner

1. Place the test beside its domain: `tests/warehouse`, `tests/equipment`, `tests/progression`, `tests/runtime`, `tests/world_map`, `tests/text_runtime`, or `tests/battle_runtime`.
2. Name C# runners `run_<behavior>_regression.cs`. Do not manually create or edit `.uid` files.
3. Use a `public partial class run_<behavior>_regression : SceneTree` with a private `TestHarness`.
4. In `_Initialize()`, use `CallDeferred(nameof(Run))` when the test needs autoloads, scene tree readiness, async waits, or Godot resources that should settle before assertions. Direct `Run()` is acceptable for isolated pure logic runners.
5. Split assertions into small private methods named for the contract being protected.
6. Build the smallest fixture that exercises the behavior. Prefer typed setup APIs and formal content injection helpers already present in the codebase.
7. Dispose owned sessions, runtimes, registries, windows, resources, and services in `finally` blocks when cleanup affects later assertions or finalizers.
8. If the runtime relationship, ownership boundary, or recommended read set changed, update `docs/design/project_context_units.md` after the code change.

## Assert These Things

Assert stable contracts and failure modes:

- Command success/failure, typed result code, and stable error code.
- Public typed API output, owner-visible state transition, and mutation/no-mutation behavior.
- Snapshot fields and short stable text fragments that are part of the automation surface.
- Payload boundary normalization, rejection, defensive copy behavior, lifecycle cleanup, save locks, and content catalog binding when those are the intended contracts.
- Cross-table validation errors by domain and count when the validator surface defines those counts.

Private fields are allowed only when the existing project exposes no cleaner setup seam and the test is explicitly an architecture or ownership contract regression. Keep the assertion tied to the observable contract being protected, not incidental storage shape.

## Do Not Assert These Things

Avoid brittle or misleading coverage:

- Do not assert private cache identity, backing collection type, helper call order, or internal field layout unless the change is specifically an ownership, defensive-copy, or stale-reference contract.
- Do not assert full log dumps, full text snapshots, full rendered UI text, or incidental Chinese/English phrasing unless the text surface itself is the contract. Prefer fields, stable event IDs, error codes, and short fragments.
- Do not assert exact RNG outcomes, AI score totals, target ordering, benchmark timing, or battle balance numbers in routine regressions.
- Do not duplicate production enum validation, legal-value `HashSet`s, schema allowlists, or rule tables inside tests. Test the production owner API instead.
- Do not drive formal runtime behavior through `GDictionary` options, public Godot dictionary projections, string-key fallback, or `GodotObject.Call(...)` when the repo has typed APIs for that path.
- Do not add compatibility tests for old payloads, legacy aliases, fallback migrations, or old schema support without explicit user confirmation.
- Do not make tests depend on `.godot/`, `.uid`, generated cache files, local save artifacts, benchmark output, or personal capture artifacts.

## Validate

Run the narrowest relevant command first:

```bash
godot --headless --script tests/<domain>/run_<behavior>_regression.cs
```

When C# compile surface changed, also run:

```bash
dotnet build magic.csproj
```

For a domain sweep, use:

```bash
python tests/run_regression_suite.py --pattern tests/<domain>
```

Do not pass `--include-simulation` or `--include-benchmarks` unless the user explicitly requested those classes of tests.

## Keep This Skill Current

After using this skill to write, revise, or review tests, compare the actual workflow against these instructions before the final response. If the task revealed a reusable process improvement, missing shared helper rule, assertion boundary, validation command, or anti-pattern, update `.codex/skills/godot-test-writing/SKILL.md` in the same work before finishing.

Do not update the skill for one-off facts that only apply to a single bug or temporary local artifact. Update it when the lesson should guide future test-writing tasks in this repository.
