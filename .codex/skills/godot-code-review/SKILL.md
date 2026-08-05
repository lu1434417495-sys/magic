---
name: godot-code-review
description: Review Godot 4.6 C# diffs, commits, branches, PRs, historical findings, and large refactors for runtime regressions, scene-script mismatches, GodotSharp interop bugs, state and serialization bugs, behavior-equivalence breaks, performance hazards, and missing headless tests. Use when the user asks for a code review, PR review, current-owner verification, regression or safety audit, large-file split review, or whether changes touching `.cs`, `.tscn`, `project.godot`, `.tres`, legacy `.gd`, or tests are safe.
---

# Godot Code Review

Use a findings-first review. Prioritize bugs, regressions, broken contracts, and test gaps over style commentary.

## Select a Mode

Use the user's scope. Otherwise establish one of:

1. Uncommitted changes: `git diff HEAD`
2. Staged changes: `git diff --cached`
3. Current branch vs base
4. Specific commit
5. Commit range
6. Historical finding, review report, or PR thread against current owners
7. Large refactor, physical split, owner extraction, or architecture campaign

For mode 6, read [references/current-owner-verification.md](references/current-owner-verification.md). For mode 7, read [references/large-refactor-equivalence.md](references/large-refactor-equivalence.md) and instantiate [references/coverage-ledger-template.md](references/coverage-ledger-template.md) in working notes.

Then inspect only the relevant diff plus the owner files around it.

Ignore ordinary source changes under `tools/` by default. Remove normal `tools/**` paths from the review set before rebuilding context, do not read owner files for those paths, and do not report findings for them unless the user explicitly asks to include `tools/`. Also ignore `.ralph/prd.json` by default unless the user explicitly asks to include it. Exception: keep generated Python cache artifacts in scope, especially `tools/__pycache__/**`, `tools/**/*.pyc`, and `tools/**/*.pyo`; these should be reported because they do not belong in the repository. If nothing remains after filtering, say so and stop.

## Review Procedure

1. Rebuild context around the filtered diff.
- Read `docs/design/project_context_units.md` first as the repo's architecture loading index.
- Use it to map the diff to the owning context units and decide which adjacent owner files are actually in scope.
- Record the actual checkout, HEAD, comparison base, staged/unstaged boundary, and PR state. Do not mix local evidence with remote CI or a different worktree.
- Read the changed files that remain after excluding ordinary `tools/**` paths and `.ralph/prd.json`.
- If the remaining path is only a generated cache artifact under `tools/`, report it directly without loading surrounding tool implementation files.
- For `.tscn` changes, also read the attached C# script, or legacy `.gd` script if still present, and verify node paths, exported fields/properties, callable names, and signals.
- For C# runtime changes under `scripts/systems/*`, `scripts/player/*`, `scripts/enemies/*`, or `scripts/ui/*`, read the nearby owner state, service, scene, and regression tests.
- Read [references/review-checklist.md](references/review-checklist.md) when the affected area spans multiple subsystems.
- Run `git diff --check` and scan the filtered changed files for embedded `<<<<<<<`, `=======`, and `>>>>>>>` markers; a clean index does not prove their absence.

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
- Keep review and repair separate. Do not edit code, resolve findings, or expand compatibility unless the user explicitly authorizes that work.

## Output

Return findings first, ordered by severity.

For each finding, use:

`[severity] path:line - issue and why it can fail`

After findings, include:

- `Open questions / assumptions`
- `Residual risks / test gaps`

If no issues are found, say so explicitly and still mention any remaining test gap or unverified area.
