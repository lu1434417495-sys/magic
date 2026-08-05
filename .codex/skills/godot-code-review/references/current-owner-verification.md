# Current-Owner Verification

Use this mode for historical review findings, old file or symbol references, unresolved PR feedback, or requests to determine whether a concern is still valid.

## Procedure

1. Freeze the evidence target.
   - Record checkout, HEAD, worktree state, comparison base, and whether the target is local, staged, pushed, or remote.
   - For a PR, inspect the full top-level and inline review timeline, current thread resolution state, requested changes, relevant commits, and current patch. Do not infer thread status from the latest diff alone.

2. Resolve the current owner.
   - Start with `docs/design/project_context_units.md`.
   - Search the old path, symbol, behavior, schema key, and callers with `rg`.
   - Use scoped `git log --follow`, rename history, or blame only when current code cannot explain where ownership moved.
   - Follow callers, projections, validators, state owners, and tests. Do not stop at a forwarding wrapper or deleted legacy file.

3. Re-evaluate the failure.
   - Reproduce the original reasoning against the current owner and production call path.
   - Check preview/execution, save/load, lifecycle, AI, UI/headless, or test consumers when the finding crossed those boundaries.
   - Treat a renamed path or added test as evidence only after verifying behavior.

4. Classify each finding.

| Classification | Meaning |
|---|---|
| `still-valid` | Current production behavior still permits the reported failure. |
| `resolved` | Current code removes the failure across the required path and has proportional verification. |
| `partially-resolved` | The primary case changed but a consumer, edge path, or test gap remains. |
| `covered-existing` | The concern was already prevented by current code at the reviewed baseline. |
| `stale-target` | The named file or symbol moved; report the current owner and continue evaluation. |
| `accepted-debt` | Current evidence explicitly records the risk as intentionally retained debt. |
| `unverified` | Missing environment, history, artifact, or reproducible evidence prevents classification. |

5. Report findings first.
   - Give current `path:line` evidence and the concrete failure mode.
   - Then provide a finding-status table and residual unverified surfaces.
   - Do not implement a repair unless the user explicitly asks to fix the classified findings.

## PR Evidence Rules

- Distinguish unresolved threads from obsolete line anchors.
- Distinguish code pushed to the PR from local unpushed changes.
- Distinguish checks on the current head SHA from checks on an older revision.
- Never call a finding resolved solely because a conversation was marked resolved.
