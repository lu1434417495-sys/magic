---
name: battle-sim-analysis
description: Run and analyze battle simulations for this Godot repository with a low-token workflow. Use when the user asks to run a battle simulation, inspect simulation reports or AI traces, compare balance profiles, diagnose whether a result comes from skill numbers, AI action parameters, or AI scoring, prepare a compact model handoff, or operate the GPU-surrogate AI score tuning and promotion loop. Do not use numeric simulation as routine regression coverage.
---

# Battle Simulation Analysis

Use compact artifacts and aggregate evidence. Keep simulation/balance work separate from correctness regression and from battle AI implementation.

## Select A Mode

- **Run or protect the canonical baseline**: read [references/baseline-contract.md](references/baseline-contract.md).
- **Analyze reports or compare a candidate**: read [references/manual-analysis.md](references/manual-analysis.md).
- **Tune many AI score weights with the surrogate loop**: read [references/gpu-surrogate-tuning.md](references/gpu-surrogate-tuning.md) and the current canonical tuner runbook.

## Load Context

1. Read `docs/design/project_context_units.md`.
2. Start with CU-15 and CU-16. Add CU-17 for terrain/roster, CU-19 for runners, CU-20 for authored AI/BattleSim content, and CU-21 only for text/headless surfaces.
3. Read `docs/design/battle/balance_simulation.md`.
4. Reopen the current scenario, runner, report schema, and owner code. Do not rely on roster counts, parameter counts, paths, or thresholds remembered by this skill.

## Common Workflow

1. Confirm the requested mode, scenario, profile, sample scale, and output location.
2. Build C# before running a C# Godot runner. Stop when the build fails.
3. Run only the requested opt-in simulation or tuning command.
4. Verify exit status, completion count, unfinished runs, timeouts, and report path before interpreting outcomes.
5. Build or read the compact packet before opening full reports and full trace dumps.
6. Classify the likely axis:
   - `skill numbers`
   - `AI action parameters`
   - `AI scoring`
7. Change or recommend one axis at a time.
8. Compare aggregates at the same workload and sample scale.
9. Run focused non-simulation regressions for correctness before calling an implementation complete.

## Evidence Rules

- Treat small samples as directional. Use the current runbook's required sample and promotion thresholds.
- Exclude or separately report unfinished, invalid, stalled, or iteration-budget-exhausted runs.
- Distinguish setup seed from combat randomness by reading the current seed owners. Do not promise replayability unless current source proves it.
- Treat AI estimates as estimates; cross-check realized damage, kills, deaths, skill results, and completed outcomes.
- Use outlier traces to explain variance, not as the main balance conclusion.
- `manual_policy=wait` or another scripted policy is a controlled probe, not proof of player-facing intelligence.
- Do not edit a canonical baseline fixture to make a run pass or improve a matchup.
- Do not add battle simulation, balance, benchmark, or tuner entry points to the routine regression suite.

## Output

Report:

- checkout/HEAD/dirty state and build result
- scenario, profiles, run count, completion/timeout status, and report paths
- aggregate deltas and sample limitations
- classified root-cause axis
- exact next fields or owner to inspect
- focused correctness tests still required

Update `docs/design/project_context_units.md` only when ownership or recommended read sets change, not for balance values or simulation findings.

## References

- `references/baseline-contract.md`: immutable baseline, invocation, randomness, and completion checks.
- `references/manual-analysis.md`: compact packet, aggregate comparison, and one-axis iteration.
- `references/gpu-surrogate-tuning.md`: scenario validation, sampling, GPU search, real-battle gate, and anti-surrogate-gaming rules.
