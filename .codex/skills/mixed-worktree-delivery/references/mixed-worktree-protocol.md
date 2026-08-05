# Mixed Worktree Protocol

## Evidence surfaces

Keep these facts separate:

1. **Checkout**: resolved repository root, worktree Git directory, branch, HEAD, and upstream.
2. **Working tree**: unstaged tracked edits, untracked files, deletions, and generated output.
3. **Index**: the exact staged patch, unmerged entries, staged conflict markers, and staged whitespace errors.
4. **Local validation**: command, checkout, HEAD, dirty-state caveat, and result.
5. **Remote validation**: pushed commit and CI result. Never present local success as remote CI evidence.

Run the inventory from the checkout that will receive the commit. A result from another worktree is historical context, not current evidence.

## Safe inventory

- Set `GIT_OPTIONAL_LOCKS=0` or use `git --no-optional-locks` for diagnostic Git commands.
- Inspect `git status --short --branch`, staged and unstaged name-status, untracked files, unmerged entries, and both forms of `git diff --check`.
- Treat `.godot/`, local save files, coverage output, simulation reports, temporary capture directories, and editor artifacts as generated until repository ownership proves otherwise.
- Scan changed files for `<<<<<<<`, `=======`, and `>>>>>>>`; index cleanliness does not detect markers already embedded in ordinary file content.
- Inspect referenced untracked `.cs`, `.gd`, JavaScript, and TypeScript source. Search both tracked and untracked text owners: an untracked HTML report can depend on untracked vendored JavaScript. SDK-style C# projects may compile an untracked `.cs` file even when it has no explicit text reference.

## Ownership rules

- Preserve all pre-existing and parallel edits.
- Do not rewrite a shared file merely to make staging easier.
- When requested and unrelated behavior share a file, stage only the requested hunks and verify both staged and unstaged projections afterward.
- Do not assume an untracked file belongs to the current task. Establish its producer and runtime dependency first.
- Do not delete generated or temporary files unless deletion is explicitly authorized and the resolved path is verified.

## Git metadata stop conditions

Stop Git write operations when any of these occurs:

- `.git/index.lock` or the worktree-specific index lock exists.
- Git reports `Permission denied`, `Access is denied`, or inability to create `index.lock`.
- The repository root or Git metadata directory cannot be resolved or read.

Report the exact checkout and error. Do not retry `add`, `commit`, `merge`, `rebase`, `reset`, or `clean`; retries can obscure an active Git process or a checkout permission problem. Read-only source inspection may continue if it does not require inaccessible Git metadata.

An absent `index.lock` and successful read-only commands establish readability only. Do not report Git metadata as writable until an authorized Git write actually succeeds; never perform a probe write merely to test permission.

## Validation claims

Record the precise command and result. If a build or test reads unstaged or untracked code, say so; it validates the current filesystem, not necessarily the staged commit. For commit-level evidence, validate a clean materialization of the commit or explain why the local result is broader.
