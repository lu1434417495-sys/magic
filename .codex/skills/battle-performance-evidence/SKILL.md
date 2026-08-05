---
name: battle-performance-evidence
description: Analyze battle-runtime performance, benchmark evidence, hot paths, and C# line or branch coverage for this Godot 4.6 repository. Use when the user asks for performance-only analysis, profiling, benchmark comparison, allocation or pathfinding cost review, coverage percentages, Cobertura recovery, current dirty-worktree coverage, or whether a runner PASS proves production coverage. Keep analysis read-only unless implementation is explicitly authorized.
---

# Battle Performance Evidence

Build an evidence chain before proposing optimization or reporting coverage. Never present a historical artifact, another worktree, or a passing runner as current coverage.

## Workflow

1. Establish provenance.
- Read `docs/design/project_context_units.md` and select the owning battle context units, normally CU-15, CU-16, and CU-19.
- Capture the actual checkout path, HEAD, branch, dirty state, and relevant changed files.
- Distinguish source checkout, compiled assembly, benchmark process, coverage instrumented source, report path, and remote CI.
- When the user says performance-only, remain read-only. Ask before editing even when an optimization looks straightforward.

2. Choose the evidence lane.
- Use a focused regression for correctness and behavioral parity.
- Use an opt-in benchmark or performance baseline for timing, allocation, path-count, or per-turn cost.
- Use formal battle simulation only when explicitly requested; it is not routine performance validation.
- Use Cobertura or another instrumented report for line/branch coverage. A runner PASS is not coverage.

3. Establish a baseline before diagnosing.
- Record command, configuration, sample size, warmup, process lifetime, scenario, iteration limit, and whether the run completed normally.
- Measure the full user-visible cost and the suspected layer metric. Do not declare an improvement from a narrow layer metric when total AI-turn or command cost regresses.
- Separate correctness-required isolation from optional micro-optimization.
- Read [references/performance-investigation.md](references/performance-investigation.md) for hot-path and parity gates.

4. Audit coverage provenance.
- Search the current checkout, Git history, sibling worktrees, and existing artifacts before concluding that no coverage workflow exists.
- Re-instrument the current source when the user asks for current dirty-worktree coverage.
- Parse a Cobertura report with:

```powershell
python .codex/skills/battle-performance-evidence/scripts/summarize_cobertura.py <report.xml> --repo-root . --include-prefix scripts/systems/battle/
```

- Read [references/coverage-provenance.md](references/coverage-provenance.md) before reporting percentages.

5. Compare like with like.
- Keep scenario, workload, build configuration, diagnostic flags, and sample scale fixed.
- Preserve algorithm ordering and tie-breaking unless the intended behavior change is explicit.
- Treat bounded diagnostics, timed-out runs, unfinished simulations, and moving-source binaries as separate evidence classes.
- Add or run a parity regression for any optimization that changes caching, copying, pathfinding, preview, ordering, or lifecycle.

6. Report the result.
- State the inspected checkout and source state.
- Separate confirmed bottlenecks, inferred bottlenecks, rejected hypotheses, and unmeasured areas.
- Report benchmark evidence, correctness evidence, and coverage evidence independently.
- Name the exact authorization still needed before implementation when the task was analysis-only.

## Guardrails

- Do not optimize preview by sharing mutable live state.
- Do not infer production relevance from a file's coverage percentage without checking current callers.
- Do not report an aggregate percentage without numerator, denominator, filters, and report provenance.
- Do not use a historical Cobertura report as a current dirty-worktree measurement.
- Do not add benchmark or battle-simulation runners to the routine full regression suite.
- Do not update `docs/design/project_context_units.md` for a measurement-only finding or a micro-optimization that leaves ownership/read sets unchanged.

## Resources

- `scripts/summarize_cobertura.py`: read-only filtered Cobertura summary with report hash and optional checkout facts.
- `references/performance-investigation.md`: baseline, hot-path, equivalence, and measurement checklist.
- `references/coverage-provenance.md`: coverage source classification and reporting contract.
