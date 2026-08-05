# Equipment Ability End-to-End ABI Matrix

Use this matrix before implementing a new trigger, fact, condition, action, state field, or handler. Re-read `docs/design/battle/equipment_ability_runtime.md` and current code for the authoritative list of supported mechanisms.

## Required Chain

| Layer | Required question and evidence |
|---|---|
| Behavior contract | What event, source, target, fact, result, duration, usage scope, and cleanup does the mechanic mean? |
| Authoring Resource | Is there a typed `*Def`/payload field with explicit optional/default semantics? |
| Closed names | Does a typed enum/rules owner define fixed trigger, handler, selector, comparison, mode, or state names? |
| Open references | Are trait, skill, item, status, binding, and external ids validated against complete authoritative catalogs? |
| Status declaration | If the mechanic creates/references statuses, are declarations collected before references are validated so pack order is irrelevant? |
| Payload validation | Are required fields, ranges, mutually exclusive fields, consumer support, and nested payloads fail-closed? |
| Definition projection | Are authored fields copied into immutable plain C# definitions without raw Resource borrowers? |
| Snapshot/catalog | Does the process snapshot publish the definition graph once, with session/runtime only borrowing immutable values? |
| Source projection | How do player, enemy, refresh, and simulation/runtime handoff paths produce the same battle-local source facts? |
| Runtime dispatch | Does the trigger/condition/action/state dispatcher recognize the typed definition and reject unknown shapes? |
| Canonical services | Which owner performs hit, save, damage, movement, durability, skill, status, death, timeline, or RNG work? |
| Query/commit ports | Are read-only queries separated from write/reaction sinks, with explicit state contexts? |
| Preview and AI | Can preview collect the same read-only modifiers/facts? Does AI see the same availability and canonical legality? |
| Events/presentation | Are stable typed event facts, batch changes, logs, HUD, and text surfaces updated only when externally observable? |
| State/persistence | Is state stateless, battle-only, equipment-instance persistent, character/save persistent, or a derived projection? |
| Cleanup | What happens on unequip, destruction, source loss, death, expiry, battle end, rebind, rollback, and dispose? |
| Diagnostics | Does mutation-exact capture preserve nested state/source/mark/order semantics without canonicalizing them away? |
| Regressions | Is there a schema/validation test plus a real owner-path runtime test and relevant cleanup/parity tests? |

## Typed Contract Rules

- Use typed Resources at authoring boundaries and immutable plain definitions after projection.
- Keep fixed domains in one enum/rules owner. Validators and runtime consumers share that owner instead of copying string allowlists.
- Treat open catalogs as required dependencies. A missing catalog is not the same as an authoritative empty catalog.
- Preserve absence/default distinctions deliberately. Do not derive a missing field from an item, trait, skill, or handler id.
- Keep nested outcome/action payloads typed and recursively validated.
- Ensure the Resource-to-definition dispatcher, validator, runtime dispatcher, and tests all recognize a new subtype.

## Consumer Closure

For each change, explicitly mark these lanes as changed, verified unaffected, or not applicable:

- content build and snapshot;
- player equipment projection and refresh;
- enemy roster projection;
- granted-skill availability;
- attack-check query;
- damage query and combat reaction sink;
- skill execution and usage commit;
- preview;
- AI;
- timeline/status expiry;
- battle end/writeback/save;
- simulation/runtime-only projection transfer;
- HUD/text/report projection;
- lifecycle teardown.

An omitted lane is not evidence that it is unaffected.
