# Quest Save And Transaction Checklist

## Canonical Owners

- The quest journal owns active, claimable, failed, and rewarded state transitions.
- Queries return detached facts; callers must not mutate them as canonical state.
- Progress and rewards apply through the current progression, inventory, equipment, attribute, or runtime gateway.
- Save projection is derived from the canonical party/world owners.

## Claim Transaction

Verify this sequence:

1. Revalidate claimability.
2. Capture every owner that a reward may mutate.
3. Apply all reward entries.
4. Move the journal state exactly once.
5. Stage party/world/coordination owners together.
6. Commit through the canonical runtime transaction.
7. Restore all captured owners on a pre-commit failure.
8. Distinguish durable payload commit from a derived index/cache write failure.

Test multi-entry failure after an earlier entry has already mutated state.

## Idempotency And Duplicates

Confirm:

- repeated claim commands cannot award twice
- a failed transaction leaves the quest claimable when appropriate
- duplicate skill/item handling is explicit
- pending character rewards have a stable target and do not bypass learning gates accidentally
- repeatable quests re-enter only through the supported lifecycle

## Persistence

Round-trip:

- journal stage and objective progress
- failed/restartable state
- rewarded ids
- pending character rewards and their typed payloads
- any newly persisted reward owner

Do not add compatibility fields, fallback defaults, or old-version migration without user approval.
