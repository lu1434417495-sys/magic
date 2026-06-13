---
name: godot-code-review
description: Review Godot 4.6 C# changes in this repository for runtime regressions, scene-script mismatches, GodotSharp interop bugs, state and serialization bugs, performance hazards, and missing headless tests. Use when the user asks for a review, PR review, regression check, safety audit, or asks whether changes touching `.cs`, `.tscn`, `project.godot`, `.tres` data configs, legacy `.gd`, or tests are safe.
---

# Godot Code Review

Use a findings-first review. Prioritize bugs, regressions, broken contracts, and test gaps over style commentary.

## Scope

If the user already gave a scope, use it. Otherwise ask what to review:

1. Uncommitted changes: `git diff HEAD`
2. Staged changes: `git diff --cached`
3. Current branch vs base
4. Specific commit
5. Commit range

Then inspect only the relevant diff plus the owner files around it.

Ignore ordinary source changes under `tools/` by default. Remove normal `tools/**` paths from the review set before rebuilding context, do not read owner files for those paths, and do not report findings for them unless the user explicitly asks to include `tools/`. Also ignore `.ralph/prd.json` by default unless the user explicitly asks to include it. Exception: keep generated Python cache artifacts in scope, especially `tools/__pycache__/**`, `tools/**/*.pyc`, and `tools/**/*.pyo`; these should be reported because they do not belong in the repository. If nothing remains after filtering, say so and stop.

## Review Procedure

1. Rebuild context around the filtered diff.
- Read `docs/design/project_context_units.md` first as the repo's architecture loading index.
- Use it to map the diff to the owning context units and decide which adjacent owner files are actually in scope.
- Read the changed files that remain after excluding ordinary `tools/**` paths and `.ralph/prd.json`.
- If the remaining path is only a generated cache artifact under `tools/`, report it directly without loading surrounding tool implementation files.
- For `.tscn` changes, also read the attached C# script, or legacy `.gd` script if still present, and verify node paths, exported fields/properties, callable names, and signals.
- For C# runtime changes under `scripts/systems/*`, `scripts/player/*`, `scripts/enemies/*`, or `scripts/ui/*`, read the nearby owner state, service, scene, and regression tests.
- Read [references/review-checklist.md](references/review-checklist.md) when the affected area spans multiple subsystems.

2. Hunt for high-signal failures.
- Broken resource paths, missing C# partial classes, stale scene script paths, and signal/callable mismatches.
- State-transition bugs between world, battle, modal, and autoloaded session state.
- Preview-vs-execution divergence in battle commands.
- Save/load, content catalog, or serialization-shape issues. Follow the repository compatibility policy before recommending legacy payload support.
- Typed C# state leaking back into `Godot.Collections.Dictionary` / `Array` business logic instead of staying in CLR typed owners with projection only at Godot boundaries.
- New constraints represented by ad hoc strings, copied `HashSet<StringName>` whitelists, or public dictionary schemas when an enum, typed converter, typed rule utility, or DTO could own the contract.
- Per-frame scans, allocations, or redraw patterns that will get expensive.
- Runtime ownership or read-set changes that should have been reflected in `docs/design/project_context_units.md`.
- Missing or outdated regression tests.

3. Report only findings that matter.
- Lead with the most severe issue first.
- Use file and line references.
- Explain the concrete failure mode, not just the rule being violated.
- Skip cosmetic nits unless they hide a real maintenance or correctness problem.

## Output

Return findings first, ordered by severity.

For each finding, use:

`[severity] path:line - issue and why it can fail`

After findings, include:

- `Open questions / assumptions`
- `Residual risks / test gaps`

If no issues are found, say so explicitly and still mention any remaining test gap or unverified area.
