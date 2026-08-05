# Equipment Ability Reaction Ordering

Use this reference when adding a trigger, nested action, query modifier, roll gate, kill provenance, status expiry reaction, or ability-state transition.

## Define the Phase

Name the exact authoritative event and whether the reaction occurs:

- before a check or commit;
- after an attack check;
- after a confirmed hit;
- after each resolved damage event;
- after the complete skill result;
- after a final kill result;
- at turn/timeline/status expiry;
- at source refresh/removal;
- at battle completion/writeback.

Verify supported phases in current definitions and runtime code. Do not invent a phase by reusing the nearest callback when its facts or ordering differ.

## Facts and Provenance

For every reaction, define:

- source unit, equipment instance, binding, action, skill/entry, and target identity;
- whether damage facts are attempted, mitigated, shield, HP, fatal, or aggregate values;
- whether a kill belongs to the final actual attack/result or an outer nested ability;
- whether the trigger is once per event, target, skill, turn, battle, or persistent period;
- which facts remain valid during nested reactions.

Use typed provenance carried by the canonical result. Do not reconstruct origin from ids, current equipment, logs, or display text after the event.

## Ordering Rules

- Keep a reaction in the originating synchronous call stack when ordering with the parent result is observable.
- Reuse the caller's event batch/effect-origin scope when nested results belong to one command.
- Run read-only modifiers before the canonical check they modify; run result reactions only after the canonical result exists.
- Base after-hit/after-damage/on-kill behavior on final resolved facts, not pre-mitigation intent.
- Revalidate source and usage gates at commit time.
- Define nested-action recursion and charge/once-scope behavior explicitly. Never rely on incidental call depth.
- Merge nested skill/damage results through the canonical result contract; do not create a shadow damage or death pipeline.
- If a later canonical rule removes a forced result, preserve provenance for what actually happened, not what an earlier modifier requested.

## Port Shape

Prefer three directions:

- **Query**: collect immutable attack/damage/availability modifiers from explicit state context.
- **Reaction sink**: submit confirmed typed events and mutate through the equipment runtime owner.
- **Canonical service**: execute damage, skill, movement, status, durability, timeline, or death rules.

Ports must not expose `GetBattleState()`, `GetRuntimeModule()`, or a broad composition root. Bind them once at an explicit owner setup point. During teardown, disconnect consumers before providers and clear nested resolver links in reverse binding order.

## Regression Cases

Cover:

1. trigger fires once at the intended phase;
2. near-miss phase does not trigger it;
3. fact values reflect final canonical results;
4. provenance survives nested reactions and is removed when the final result no longer supports it;
5. usage/roll gates are deterministic under a controlled test seam;
6. nested action shares or separates event batches exactly as specified;
7. invalid/missing source fails closed;
8. preview collects read-only modifiers without firing reaction sinks.
