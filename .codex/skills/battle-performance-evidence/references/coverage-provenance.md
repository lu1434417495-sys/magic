# Coverage Provenance

## Evidence Classes

- **Runner result**: proves the selected tests exited successfully. It does not measure line or branch execution.
- **Current instrumented report**: produced after instrumenting the exact checkout and source state being reported.
- **Historical report**: valid evidence for its recorded source state only.
- **Sibling-worktree report**: evidence for that worktree, even when the branch names look related.
- **Remote CI report**: evidence for the remote commit and workflow configuration, not uncommitted local changes.

## Recovery Workflow

1. Search the current checkout for runsettings, coverage scripts, workflow commands, and report paths.
2. Search Git history and sibling worktrees when the current checkout lacks them.
3. Identify the report's originating checkout and commit when possible.
4. Re-instrument current dirty source before giving a current percentage.
5. Keep the recovered historical number labeled as historical when re-instrumentation is unavailable.

## Reporting Contract

For every percentage, include:

- covered and valid line counts
- covered and valid branch counts when available
- include/exclude path filters
- report absolute path, hash, and modification time
- originating checkout/commit if known
- current checkout/HEAD/dirty state as a separate fact
- whether generated, test, simulation, or benchmark code was excluded

Do not average per-file percentages. Sum covered and valid counts for the requested domain.

## Interpreting Gaps

Before calling a zero-coverage file a production test gap:

1. Search current production callers.
2. Classify it as production, opt-in diagnostic, simulation, test support, generated, or currently unused.
3. Check whether a different runtime owner now carries the behavior.
4. Recommend tests only for reachable behavior or an explicit architecture contract.

The absence of a production caller can be an applicability finding, not a coverage failure.
