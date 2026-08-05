# Parallel Delivery Campaign

Use this reference only when the user explicitly requests multi-agent or parallel implementation.

## Build the dependency DAG

1. Select the relevant context units from `docs/design/project_context_units.md`.
2. Trace current owners in source, data, scenes, tests, and current design documents.
3. Express each slice as an artifact and dependency, not as a vague subsystem label.
4. Order shared contracts before consumers:
   - schema, enum, value object, or definition;
   - canonical state and domain service;
   - runtime adapter or orchestrator;
   - projection, UI, or presentation;
   - focused tests and current-truth documentation.

Keep tightly coupled edits in one slice when separating them would require temporary compatibility code or duplicate ownership.

## Assign file ownership

- Give each path to one active owner.
- Split work by disjoint file sets, not merely by feature descriptions.
- Mark shared-owner chokepoints such as project files, central registries, common DTOs, save schemas, content validators, context-unit documentation, and test-suite manifests.
- Assign each chokepoint to one integration owner. Other slices submit required contract changes as evidence instead of editing the same path concurrently.
- Re-plan before touching a file that another slice already owns.

## Define the evidence contract

Require every slice to report:

- requested behavior and non-goals;
- exact files inspected and changed;
- assumptions about current owners and dependency versions;
- commands run with pass, fail, or not-run status;
- untracked, generated, or unrelated changes encountered;
- compatibility or serialization questions that still need user authority;
- remaining integration work.

A subagent PASS is evidence for its checkout and filesystem state only. It is not proof that the integrated commit or remote CI passes.

## Integrate in dependency order

1. Verify shared contracts and ownership first.
2. Integrate canonical state and service changes.
3. Integrate runtime consumers and projections.
4. Integrate presentation and content.
5. Reconcile tests against the final owners.
6. Update current design truth only after the implementation lands.

After each boundary, inspect the actual diff and resolve semantic overlap before continuing. Do not rely on isolated slice summaries as substitutes for source review.

## Validate the campaign

- Build after shared contracts and again after final integration.
- Run the narrowest owner-level regressions for each slice.
- Run cross-boundary regressions after integration, especially for save/load, preview/execution parity, AI, scene wiring, and GodotSharp lifecycle.
- Verify the final worktree and staged snapshot separately.
- Maintain a coverage ledger for any repository-wide review or refactor campaign: context unit, artifact, owner, reviewer, evidence, and unresolved risk.
- State whether `docs/design/project_context_units.md` remains accurate. Change it only when ownership boundaries, main runtime chains, context-unit responsibilities, or recommended read sets changed.
