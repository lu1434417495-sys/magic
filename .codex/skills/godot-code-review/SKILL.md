---
name: godot-code-review
description: Review Godot 4 changes in this repository for runtime regressions, scene-script mismatches, state and serialization bugs, performance hazards, and missing headless tests. Use when the user asks for a review, PR review, regression check, safety audit, or asks whether changes touching `.gd`, `.tscn`, `project.godot`, data configs, or tests are safe.
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

## Review Procedure

1. Rebuild context around the diff.
- Read the changed files.
- For `.tscn` changes, also read the attached script and verify node paths, exported fields, and signals.
- For `scripts/systems/*`, read the nearby owner state, service, and regression tests.
- Read [references/review-checklist.md](references/review-checklist.md) when the affected area spans multiple subsystems.

2. Hunt for high-signal failures.
- Broken preload paths, missing classes, stale scene paths, and signal mismatches.
- State-transition bugs between world, battle, modal, and autoloaded session state.
- Preview-vs-execution divergence in battle commands.
- Save/load or dictionary-shape compatibility issues.
- Per-frame scans, allocations, or redraw patterns that will get expensive.
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
