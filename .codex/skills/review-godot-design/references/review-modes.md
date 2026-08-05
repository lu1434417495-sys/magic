# Review Modes

Select one primary mode and add another only when the artifact genuinely crosses boundaries.

## Current-truth audit

Use for documents under `docs/design/` or claims described as already implemented.

- Verify every material statement against current owners and focused tests.
- Flag stale owner names, paths, call chains, or schema descriptions.
- Treat a code-verified relationship as current truth only when the whole claimed boundary exists.

## Proposal feasibility

Use for `docs/proposals/`, roadmaps, or requested future behavior.

- Separate existing prerequisites from new contracts and unresolved decisions.
- Identify owner changes, dependency direction, state shape, compatibility choices, and validation needs.
- Report contradictions and missing decisions. Do not silently redesign the proposal.

## Specification or contract audit

Use for schemas, APIs, state machines, event order, ownership contracts, or save formats.

- Trace producer, validator, converter, owner, mutator, consumers, teardown, and tests.
- Check closed value sets, failure behavior, ordering, rollback, and serialization authority.
- Require explicit user direction before recommending compatibility code.

## Test-matrix audit

Use when the artifact claims coverage or presents a test plan.

- Map each behavior and failure mode to a concrete test layer and runner.
- Distinguish compile/load validation, regression pass, application E2E, line coverage, and branch coverage.
- Mark tests that only prove existence or loadability as insufficient for behavioral claims.

## Landing-gap audit

Use when the user asks what remains before a proposal can be called complete.

- Apply the parity matrix to each claim.
- Identify the earliest missing owner or consumer, not only the last missing UI or test.
- Produce small dependency-ordered gaps suitable for a later `algorithm-design` pass.

## Handoff Boundaries

- Hand new algorithm and implementation planning to `algorithm-design`.
- Hand diff, commit, branch, or PR regression review to `godot-code-review`.
- Hand approved combat skill resource work to `design-godot-skill`.
- Do not start any handoff that writes files unless the user authorized implementation.
