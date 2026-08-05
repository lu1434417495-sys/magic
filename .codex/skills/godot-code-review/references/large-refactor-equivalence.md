# Large-Refactor Equivalence

Use this mode for file splits, owner extraction, service decomposition, typed-state migrations, or repo-wide architecture reviews.

## Establish the Baseline

- Identify the exact before/after commits or comparison base.
- Inventory public and internal entry points, callers, state fields, side effects, event/log order, lifecycle operations, serialization keys, and focused tests.
- Separate intentional behavior changes from claimed physical-only changes.
- Create a coverage ledger from [coverage-ledger-template.md](coverage-ledger-template.md). Do not claim full review without a closed ledger.

## Verify Equivalence

For every moved responsibility, trace:

1. Old entry point to new owner
2. Argument, default, and normalization semantics
3. Validation and failure order
4. State reads and mutation commit point
5. Event, log, callback, and presentation order
6. Preview/query versus execution behavior
7. AI, automation, pending, reaction, and retry paths
8. Save/load, detached projection, rollback, and teardown
9. Caller migration and stale owner access
10. Regression coverage through production entry points

Check owner direction explicitly:

- Extracted services weakly borrow their composition root or depend on narrow ports.
- The composition root owns setup and reverse-order teardown.
- Getters do not perform hidden setup, rebinding, canonicalization, or mutation.
- Moved state has one canonical owner; wrappers do not retain mirrors.
- Godot collections remain boundary projections rather than business-state owners.

## Coverage Discipline

- Partition the review by context unit and owner, not arbitrary line counts alone.
- Record every file and important member in exactly one ledger row.
- Mark generated, excluded, unchanged-context, and unverified paths explicitly.
- Scan all reviewed text files for embedded conflict markers and run `git diff --check`.
- Deduplicate findings by failure mechanism while retaining every affected owner.

## Completion Rule

A refactor is behavior-equivalent only when:

- every ledger row is reviewed or explicitly excluded with a reason;
- all callers resolve to the new owner;
- state, ordering, failure, projection, and lifecycle invariants remain intact;
- intended behavior changes are separately documented and tested;
- residual test gaps and unverified runtime surfaces are reported.
