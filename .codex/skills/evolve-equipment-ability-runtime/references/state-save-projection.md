# Equipment Ability State, Save, and Projection

Use this reference when a mechanic stores counters, charges, target marks, summoned-unit facts, temporal modifiers, usage state, persistent growth, or any data that crosses an owner boundary.

## Classify the State

| Lifetime | Preferred owner |
|---|---|
| Stateless configuration | Immutable equipment-ability definition in the process content snapshot. |
| Derived equipped source | Battle-local equipment projection, rebuilt from the current equipment/trait view. |
| Command/reaction temporary | Local typed context/result; do not store after the event. |
| Battle-only state | Battle unit/equipment runtime owner with explicit expiry and battle teardown. |
| Equipment-instance persistent | Equipment instance state/writeback owner keyed by stable source identity. |
| Character or party persistent | Existing typed party/progression owner and canonical save snapshot. |
| Presentation-only | Detached HUD/text/report snapshot derived from an authoritative runtime owner. |

Do not add a save field for a value that can be derived from immutable content plus canonical equipment state.

## Projection Contract

- Compute a complete projection result without mutating the unit.
- Deep-copy mutable lists/entries at the owner boundary.
- Atomically replace all coupled projection components; on failure, retain the previous complete projection.
- Use stable source identity that includes every scope needed to distinguish unit, equipment instance, binding, state key, and target.
- Expose immutable scalar/read views to hot rules instead of the mutable backing collection.
- Rebuild or clear derived state after equip, unequip, destruction, source loss, roster construction, rebind, and rollback as applicable.
- Keep runtime-only projection in its typed transfer owner. Do not assume a canonical Godot payload or save codec carries it.

## Persistent State and Save

Before changing persistence:

1. Identify the canonical typed owner and `BuildSaveSnapshotPlain()` source.
2. Define valid ranges, missing/default behavior, and atomic update/rollback.
3. Trace serialization, deserialization, validation, battle projection, battle-end writeback, and save transaction.
4. Decide whether the change is schema-breaking.
5. Ask the user before adding compatibility aliases, migrations, fallbacks, or old-version support; explain which existing save/caller would fail without it.

Godot collections may appear only at explicit synchronous save/API projection boundaries. Runtime and cached state remain plain typed C#.

## Cleanup Matrix

For each stored fact, define behavior on:

- unequip or equipment destruction;
- source trait/binding removal;
- target death, removal, or invalidation;
- status/mark duration expiry;
- summoned-unit death or consumption;
- turn/battle/period boundary;
- transaction rollback;
- content/runtime rebind;
- runtime dispose and repeated dispose.

Cleanup should be source-aware. Removing one source must not remove another source's equivalent mark, status, or counter.

## Mutation-Exact Diagnostics

Diagnostic capture may need to preserve owner absence, null versus empty components, null entries, nullable ids, raw order, nested source/state/mark data, and runtime-only projection. Do not reuse a normal codec or canonical clone if it normalizes those differences.

## Regression Shape

- Projection: player and enemy source paths when both are supported.
- Atomicity: failed projection leaves the previous view unchanged.
- Isolation: unequipped/source-missing unit has no stale ability state.
- Transfer: the intended typed roster/simulation handoff preserves runtime-only state.
- Persistence: round trip plus invalid payload rejection and rollback, when saved.
- Cleanup: source removal, expiry/death/battle-end as applicable.
- Mutation: exact guard detects representative nested changes.
