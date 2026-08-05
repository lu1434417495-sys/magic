# Cross-Path Effect Landing

Complete every applicable row before calling a shared effect or mechanic landed.

| Surface | Questions |
|---|---|
| Authoring | Which typed Resource fields express the mechanic? |
| Definition projection | Is the Resource converted to an immutable typed definition without mutable aliases? |
| Closed kind owner | Which enum or typed rule owns the value set and conversion? |
| Validator | Are invalid values, references, combinations, and level windows rejected? |
| Unit execution allow-list | Can formal unit execution accept the effect? |
| Target validation | Are team, dead/alive, range, footprint, area, and pre-payment gates correct? |
| Standard execution | Does the normal orchestrator execute and report it? |
| Special resolvers | Which profile, repeat, chain, ground, contingency, equipment, or auto/pending path bypasses the normal loop? |
| Preview | Is preview detached, ordered, and semantically equal without consuming state? |
| AI affordance | Can candidate generation recognize legal uses? |
| AI score | Does scoring reuse canonical preview/results instead of copying the rule? |
| Auto/pending execution | Do delayed, automatic, contingency, and pending casts preserve the same semantics? |
| Presentation | Are HUD, logs, report facts, and change flags sufficient? |
| Regression | Are validator, execution, preview parity, AI, and mutation safety covered? |

## Ownership Rules

- List all producers and consumers before choosing the owner.
- Put cross-system semantics in a typed shared rule or DTO, not a `skill_id` branch.
- Do not use a resource validator as proof that runtime behavior exists.
- Reuse the canonical full-attack, damage, save, target, or preview entry point when the mechanic promises those semantics.
- Audit runtime-only context before merging state/view or execution/preview models.

## Preview Safety

A clone is not automatically detached. Inspect:

- nested mutable collections
- lazy getters and canonicalization
- definition aliases
- target, terrain, barrier, blackboard, equipment mark, and allocator state
- mutation guard coverage for the new field

Preview and AI evaluation must not mutate live state, consume one-shot facts, advance time, or commit usage.

## Completion Evidence

Report:

- matrix rows that apply
- current owner and implementation for each
- focused tests run
- intentionally unsupported paths
- remaining product decisions

Do not label a path unsupported merely because it was not inspected.
