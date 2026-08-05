# Content-Based Commits

## Build the theme map

Define each commit by one externally meaningful behavior, not by file type or by everything currently modified. For each theme, list:

- production owners;
- schema or data resources;
- focused regressions;
- current implementation documentation;
- required untracked dependencies;
- shared files that need hunk staging.

Keep mechanical cleanup with the behavior only when it is required for that behavior to build or remain understandable.

## Stage safely

1. Inventory the full worktree.
2. Stage explicit whole files that contain only the theme.
3. Use `git add -p -- <path>` for shared files.
4. Re-read `git diff --cached` as the proposed commit.
5. Re-read `git diff` for accidentally omitted companion changes.
6. Audit untracked source files before deciding the theme is self-contained.

Avoid broad `git add -A` or directory staging in a mixed worktree. Do not stage generated Godot state, local saves, reports, or temporary simulation output unless the task intentionally owns a tracked fixture.

## Verify cohesion

Use `verify_staged_theme.ps1` with allowed and forbidden path families. Then check:

- every staged hunk supports the named behavior;
- implementation and regression assertions agree;
- shared-file partial staging leaves both projections syntactically coherent;
- no unmerged entry or embedded conflict marker remains;
- `git diff --cached --check` passes;
- no required untracked source is missing;
- tests exercise the staged behavior rather than only adjacent current-worktree behavior.

If validation consumes unstaged companion code, report that limitation and do not claim the commit itself is independently validated.

## Commit and handoff

Use a short Conventional Commit subject. After committing, inspect the commit rather than trusting the pre-commit index:

```powershell
git show --stat --oneline --decorate --no-renames HEAD
git show --format=fuller --no-ext-diff --no-renames HEAD
```

Report the hash, theme, included files, validation commands, remaining worktree state, and remote CI status separately.
