# Coverage Versus Regression

## Evidence Classes

- A focused runner PASS proves the selected assertions passed in that process.
- A domain sweep PASS proves the discovered selected runners passed.
- Application E2E proves the declared production-main-scene journey.
- A Cobertura or equivalent report measures instrumented line/branch execution for its source state.
- A historical or sibling-worktree report is not current dirty-worktree coverage.

Do not turn test counts or runner pass rates into source coverage percentages.

## When Coverage Is Requested

1. Search the current checkout for coverage scripts, runsettings, workflow commands, and reports.
2. Search Git history and sibling worktrees before claiming no coverage path exists.
3. Record checkout, HEAD, dirty state, instrumented source, report hash/path, filters, covered count, and valid count.
4. Re-instrument current dirty source before giving a current number.
5. Check current production callers before classifying a zero-coverage file as a test gap.

Use `battle-performance-evidence` for battle-specific performance or Cobertura provenance analysis.

## Test Selection

Coverage can reveal an unexecuted surface, but the regression should still be placed at the narrowest layer that protects the observable contract. Do not add a broad E2E test merely to raise a percentage.
