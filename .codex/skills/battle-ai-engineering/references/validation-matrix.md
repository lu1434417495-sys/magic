# Battle AI Validation Matrix

Choose the narrowest cells that cover the changed contract. Discover actual runner names with `rg --files tests`; do not guess paths.

| Change | Required validation |
|---|---|
| Action Resource/schema | Content/schema rejection plus valid Resource-to-definition projection. |
| Definition or dispatcher | Projection field parity, action-kind dispatch, incompatible-shape rejection. |
| Action assembler/plan | Entry construction, binding lifetime, unknown/missing definition failure. |
| Evaluator/target route | Positive behavior, no-target/no-path fallback, canonical legality parity. |
| Skill evaluator | Availability/entry identity, selected variant, canonical preview, execution-compatible command. |
| Score input/profile | Input facts, neutral defaults, breakdown/trace projection, stable ordering. |
| Objective behavior | Typed objective facts, preferred target/path, no-path fallback, mutation snapshot coverage. |
| Mutation/preview | Exact nested mutation detection and successful no-mutation decision. |
| Decision lifetime | Detached command/score/trace after decision scope teardown. |
| Path/cache | Correctness across key variants, invalidation/epoch, detached result, hit/miss diagnostics. |
| Performance | Same-provenance before/after benchmark after correctness tests pass. |

## Commands

Build before executing C# runners:

```powershell
dotnet build magic.csproj
godot --headless --script tests/battle_runtime/ai/run_<focused>_regression.cs
python tests/run_regression_suite.py --pattern tests/battle_runtime/ai
git diff --check
```

Use a focused runner first. A domain sweep is appropriate after multiple AI owners change.

Do not add BattleSim, balance, simulation, or benchmark runners to the routine sweep. Run them only when requested, and use `battle-sim-analysis` for simulation/tuning. Do not treat a benchmark pass as correctness coverage.

## Strict Zero-Write Audit

When the user prohibits all filesystem writes, do not run `dotnet build`, Godot, coverage, benchmark, or simulation commands: they can create or refresh `bin/`, `obj/`, `.godot/`, reports, captures, or user state. Limit verification to read-only Git inspection, source/Resource ownership, existing test assertions, and static helper output. Report tests as “present but not run”; do not reuse historical PASS results as current checkout evidence.

## Assertion Guidance

- Assert legal command/target/path and stable typed result fields.
- Assert failure/fallback codes or decisions, not incidental logs.
- For scores, assert component semantics and ordering. Avoid brittle total scores unless the total itself is the contract.
- Assert preview no-mutation and decision detachment explicitly.
- For caches, assert equal results with and without reuse plus invalidation behavior; timing alone does not prove correctness.
