# Landed Versus Planned

Classify each material claim independently.

`mixed` is a document-level description, not a claim classification. Split a compound statement into atomic claims before assigning a status. Use `partially-landed` only when one atomic behavior spans required surfaces and some of those surfaces are missing.

| Classification | Required evidence |
|---|---|
| `landed` | Current owner and all required consumers implement the claimed semantics; focused verification exists or the unverified portion is stated. |
| `partially-landed` | Some required surfaces exist, but at least one behavior, consumer, failure path, or test boundary is missing. |
| `planned` | The claim exists only in a proposal, task, comment, stub, TODO, unused resource, or uncalled API. |
| `superseded` | A different current owner or contract intentionally replaced the described approach. |
| `contradicted` | Current implementation or current-design truth conflicts with the claim. |
| `unverified` | Available evidence cannot establish the claim without a missing environment, history source, or test result. |

## Evidence Order

Prefer, in order:

1. Current owner code and data resources
2. Focused tests and current runtime entry points
3. `docs/design/` documents verified against code
4. Current branch or PR history when the claim is historical
5. Proposals, discussions, reviews, comments, and task lists

Existence is not semantic evidence. A resource validator proves loadability and declared-shape checks, not complete runtime behavior. A test name proves neither execution nor coverage. An unused class or method is still planned.

## Documentation Consequences

- Keep only fully code-verified current truth under `docs/design/`.
- Keep mixed or partial roadmaps under `docs/proposals/`.
- Keep point-in-time findings under `docs/reviews/`.
- Do not move a whole mixed document merely because some claims landed.
- When a claim lands, update the owning current-design document and `project_context_units.md` only if runtime relationships, ownership boundaries, or recommended read sets changed.

## Claim Table

Use this compact form after findings:

| Claim ID | Status | Current evidence | Missing or next boundary |
|---|---|---|---|
| C001 | `partially-landed` | `path:line` | Preview and focused regression are absent |
