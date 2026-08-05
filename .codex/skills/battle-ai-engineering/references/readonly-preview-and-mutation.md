# Read-Only Preview and Mutation Safety

Use this reference whenever AI evaluation calls battle preview, clones a unit/state graph, captures mutation diagnostics, or returns data beyond the decision scope.

## Read-Only Contract

Evaluation may read current battle facts and may mutate explicitly owned memoization/diagnostic state. It must not mutate gameplay owners:

- battle, unit, objective, barrier, terrain, timeline, cooldown, status, equipment-ability, or inventory state;
- resources, charges, action points, movement points, durability, usage counters, event batches, or production RNG;
- caller-owned commands, target lists, overlays, or definition graphs.

Preview must return detached values. Commit must revalidate current state and run through the normal execution path.

## Canonical Preview Rule

Call the formal preview when a rule can change:

- whether a command or target is legal;
- the affected unit or ground-cell set;
- path, forced movement, edge, barrier, or footprint validity;
- selected skill entry/variant, cost, cast state, or cooldown;
- hit/save/damage/status applicability;
- special resolver output.

A fast typed evaluator may estimate utility after canonical legality is fixed. It must not maintain a second approximation that admits a command the formal preview rejects or excludes a legal command.

## Raw-Exact Mutation Evidence

Mutation diagnostics must compare the semantics that a mutation could corrupt. Preserve, when relevant:

- owner absent versus owner present;
- `null` versus empty collections or values;
- null entries and nullable identifiers;
- source order and duplicate/order-sensitive entries;
- raw nested source, state, mark, objective, equipment, or barrier facts;
- stable keys already consumed by diagnostics.

Do not build an exact snapshot through a clone, codec, canonical projection, lazy getter, or normalization path unless that path is proven mutation-free and preserves these distinctions. Snapshot construction itself must be covered by a regression when it traverses mutable owners.

Service-owned caches may be omitted only when they are explicitly non-gameplay memoization, cannot alter observable decisions, and have their own key/invalidation contract.

## Lifetime Checks

- A decision result owns its command and trace payload; it does not borrow evaluator context.
- A cached candidate never retains a mutable `BattleUnitState`, `BattleState`, Resource, or transient preview result across its valid scope.
- Rebinding or disposing the runtime disconnects callback consumers before providers/owners.
- Reusing a definition or score profile means borrowing an immutable process-snapshot value, not retaining authored Resources.

## Regression Shape

For a new or changed evaluator, prove:

1. the expected candidate wins in a minimal scenario;
2. the rejected or fallback case is explicit;
3. canonical preview and evaluator agree on legality/targets;
4. the mutation guard detects a representative nested mutation;
5. normal evaluation leaves the exact snapshot unchanged;
6. returned command/trace remains valid after the decision scope is disposed.
