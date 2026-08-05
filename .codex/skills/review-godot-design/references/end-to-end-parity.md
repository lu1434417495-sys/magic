# End-to-End Parity

Use the applicable rows. A feature is not landed merely because its authoring type or runtime class exists.

| Surface | Verify |
|---|---|
| Intent and owner | One canonical owner for state, mutation, ordering, and failure behavior |
| Authoring/resource | Typed fields, valid defaults, references, and content placement |
| Projection | Resource or boundary payload converts once into detached typed definitions/state |
| Validation | Closed values, cross-table references, invalid combinations, and failure messages |
| Runtime entry | Production call path reaches the owner without test-only or legacy bypasses |
| Resolution | Standard and special paths share required rules, ordering, transactions, and events |
| Preview/query | Read-only path matches execution gates and does not mutate canonical state |
| AI/automation | Candidate generation, legality, preview, scoring, and command submission agree |
| Pending/reaction | Delayed casts, auto-casts, nested reactions, retries, and cancellation preserve semantics |
| Presentation | UI, headless snapshot, log, and report consume detached facts rather than reimplement rules |
| Persistence/writeback | Save version, serialization owner, rollback, battle-to-world writeback, and teardown are explicit |
| Tests | Positive, invalid, edge, lifecycle, and cross-path regressions exercise production entry points |
| Documentation | Current-design truth and context-unit ownership match the landed runtime |

## Cross-Path Checks

- Compare standard, special resolver, ground/area, repeated/chain, equipment-triggered, pending, contingency, and simulation paths when relevant.
- Compare execution, preview, AI, and presentation using the same typed rule owner.
- Verify event order and mutation boundaries, not only final values.
- Reject feature-ID branches for reusable cross-system mechanics; model them as typed data and shared rules.
- Preserve plain C# runtime ownership. Keep Godot collections at resource, API, UI, or serialization projection boundaries.

## Test Sufficiency

- Resource loading confirms parsing and declared validation only.
- A focused runner confirms its asserted behavior only.
- A routine suite pass does not prove application E2E behavior.
- Line and branch coverage require a coverage-producing run tied to the exact checkout and source state.
