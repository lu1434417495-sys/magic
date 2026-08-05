# Path, Cache, and AI Performance Workflow

Use this reference for movement-query caching, path workspaces, scoring hot paths, or AI allocation reductions. If the request is analysis-only, inspect and report without editing.

## Separate Two Lanes

1. **Correctness lane**: canonical legality, reachable cells, path choice, objective behavior, preview parity, and mutation safety.
2. **Performance lane**: repeated work, allocations, cache hit/miss behavior, elapsed time, and bounded/formal benchmark evidence.

Do not trade correctness for speed or use a faster approximate rule when it changes legal commands.

## Baseline

Before editing:

- record checkout, HEAD, dirty state, build configuration, diagnostic defines, command, scenario, run count, iteration budget, and completion status;
- identify the measured owner and hot call chain;
- distinguish a routine regression, bounded diagnostic, formal benchmark, and battle simulation;
- keep unfinished/iteration-budget runs out of completed performance conclusions.

## Cache Contract

Derive the cache key from every input that can change the answer. Audit:

- battle/session epoch;
- topology, edge, barrier, occupancy, or relevant state revision;
- actor identity, position, footprint, movement capability, faction, and status constraints;
- target/goal set and target-selection policy;
- traversal options, budgets, overlays, and special path modes;
- immutable definition/profile revision when it affects the query.

Specify invalidation for new battle, rebind/dispose, topology/occupancy change, relevant actor state change, and any key input that is intentionally not stored in the key.

Never broaden a fast/cache-eligible lane without parity tests for the excluded options. Return detached or defensively copied results when callers could mutate paths or candidate collections.

## Optimization Order

Prefer:

1. remove repeated canonical queries within one decision;
2. reuse immutable definitions and typed scalar views;
3. cache expensive results under an exact epoch/revision key;
4. reduce temporary projections and collection copies;
5. optimize algorithms only after profiling identifies the hot path.

Do not use global mutable caches, raw authored Resources, or stale battle-state borrowers to save allocations.

## Evidence

After editing:

- rerun correctness regressions first;
- rerun the same baseline command and provenance;
- report completed-run counts plus hit/miss/rebuild/workspace diagnostics when available;
- compare like with like and disclose bounded runs;
- do not update tracked baselines unless the user requested baseline publication and the new measurement is formal.
