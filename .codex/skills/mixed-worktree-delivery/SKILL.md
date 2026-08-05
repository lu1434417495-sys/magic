---
name: mixed-worktree-delivery
description: Inspect and deliver changes safely from a dirty or shared Git worktree in this Godot repository. Use when the user asks to commit local changes, commit by behavior or theme, separate unrelated edits, stage selected hunks, audit untracked source dependencies, verify a mixed worktree, or diagnose worktree, merge, conflict-marker, index-lock, or checkout-specific Git state.
---

# Mixed Worktree Delivery

Preserve parallel work while turning only the user-authorized behavior into verified commits. Treat checkout, working tree, index, local validation, and remote CI as separate evidence surfaces.

## Workflow

1. Establish the exact checkout.
   - Read repository instructions and run `scripts/inventory_worktree.ps1`.
   - Record repository root, branch, HEAD, upstream, staged changes, unstaged changes, untracked files, unmerged paths, and diff-check results.
   - Do not infer repository state from another worktree or from a clean index alone.

2. Classify every relevant path.
   - Separate requested behavior, unrelated tracked edits, required untracked dependencies, generated output, and ambiguous overlap.
   - Treat generated-looking classifications as heuristics. Tracked reports, HTML exports, and vendored assets still require producer, license, and intentional-ownership review.
   - Run `scripts/scan_conflict_markers.ps1`; a clean index does not prove that edited files lack embedded conflict markers.
   - Run `scripts/find_referenced_untracked_sources.ps1` before staging or committing code.
   - Read [references/mixed-worktree-protocol.md](references/mixed-worktree-protocol.md) when the worktree is dirty, shared, conflicted, or blocked by Git metadata.

3. Define commit themes before writing the index.
   - Map each requested behavior to its implementation, tests, data, and current documentation.
   - Keep unrelated edits unstaged. Use hunk staging when one file contains multiple themes.
   - Read [references/content-based-commits.md](references/content-based-commits.md) when the user requests content- or behavior-based commits.

4. Stage only after commit scope is authorized.
   - Use explicit paths or interactive hunk staging.
   - Never use broad staging as a shortcut in a mixed worktree.
   - Never discard, reset, clean, move, or rewrite unrelated changes without explicit approval.

5. Verify the exact staged snapshot.
   - Run `scripts/verify_staged_theme.ps1` with the theme and allowed path families.
   - Inspect `git diff --cached`, `git diff --cached --check`, staged conflict markers, unmerged paths, and staged/unstaged overlap.
   - Overlap is informational because intentional hunk staging can leave both projections in one file. Inspect both patches; use `-FailOnOverlap` only when the theme requires whole-file staging.
   - Build and run the narrowest tests that validate the staged behavior. State when validation necessarily includes unstaged or untracked code.

6. Commit and re-inventory.
   - Commit one coherent theme at a time with a Conventional Commit subject.
   - Verify the resulting commit contents, then rerun the inventory before preparing the next theme.
   - Report commit hashes, files included, tests run, remaining local changes, and any unverified remote CI separately.

## Read-Only Utilities

Resolve the repository root first so these commands work from any directory inside it:

```powershell
$repoRoot = (git --no-optional-locks rev-parse --show-toplevel).Trim()
$toolRoot = Join-Path $repoRoot ".codex/skills/mixed-worktree-delivery/scripts"
& (Join-Path $toolRoot "inventory_worktree.ps1") -RepoPath $repoRoot
& (Join-Path $toolRoot "scan_conflict_markers.ps1") -RepoPath $repoRoot
& (Join-Path $toolRoot "find_referenced_untracked_sources.ps1") -RepoPath $repoRoot
& (Join-Path $toolRoot "verify_staged_theme.ps1") -RepoPath $repoRoot `
  -Theme "battle preview isolation" `
  -AllowedPath "scripts/systems/battle/**","tests/battle_runtime/**"
```

The utilities do not modify files, the index, refs, or commits. A readable Git directory and absent index lock do not prove metadata writability. If a Git command reports index-lock or metadata permission failure, stop Git write operations; do not retry staging, committing, merging, rebasing, resetting, or cleaning in that checkout.
