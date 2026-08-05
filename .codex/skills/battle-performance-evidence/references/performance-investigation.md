# Performance Investigation

## Evidence Packet

Record these facts before comparing results:

- checkout path, branch, HEAD, dirty state, and relevant diff
- build configuration and whether the assembly was rebuilt from that source
- command, environment flags, scenario, workload, iteration cap, and timeout
- process exit status and whether the workload completed
- warmup policy, repetitions, sample size, median or percentile, and variance
- total user-visible cost plus the suspected subsystem metric

Do not compare a cold single process with a warmed repeated process or a bounded diagnostic with a formal baseline.

## Diagnosis Order

1. Confirm the cost is on a production caller path.
2. Identify the owning context unit and state owner.
3. Measure total cost and the suspected layer.
4. Form one hypothesis.
5. Change or model one axis.
6. Re-run the same workload.
7. Run correctness and parity regressions.

Use allocation counts, call counts, path expansions, cache hits, and layer timing to explain the total. They do not replace the total.

## Correctness Isolation Versus Optimization

Keep a copy, snapshot, or detached graph when it is required to prevent preview or AI evaluation from mutating live state. Treat removing that isolation as a correctness change, not a micro-optimization.

Before changing copying or caching, inspect:

- lazy getters and canonicalization that may mutate while reading
- mutable nested collections and shared definition aliases
- epoch, owner identity, topology, geometry, and content-revision invalidation
- stable ordering and equal-cost tie-breaking
- cleanup at rebind, battle end, and dispose

## Equivalence Gates

Add or run focused coverage for:

- preview versus execution acceptance and result parity
- mutation guard before and after the optimized read path
- equal-cost path/target ordering
- cache hit, miss, invalidation, and owner-rebind behavior
- teardown returning owner/lease/cache state to baseline

A build and a faster benchmark are insufficient when behavior ordering can change.

## Result Classification

- **Confirmed**: repeated measurement on a fixed workload supports the claim.
- **Directional**: sample size or variance is too weak for a stable magnitude.
- **Inferred**: source structure suggests a cost but it was not measured.
- **Rejected**: measurement did not support the hypothesis.
- **Unverified**: the required source, caller, build, or artifact provenance is missing.
