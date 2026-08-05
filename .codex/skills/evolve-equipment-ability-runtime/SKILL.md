---
name: evolve-equipment-ability-runtime
description: Design, implement, review, or refactor the generic typed equipment-ability framework in this Godot 4.6 C# repository. Use when work adds or changes trigger, fact, condition, action, roll-gate, outcome, state-schema, validation, Resource-to-definition projection, battle source projection, reaction ordering, query/sink ports, lifecycle cleanup, save/writeback, preview/AI consumers, or framework regressions. Use weapon-ability-landing instead when only landing a concrete weapon with already-supported mechanics.
---

# Evolve Equipment Ability Runtime

Evolve equipment abilities as a typed cross-layer ABI. A mechanism is complete only when authoring, validation, immutable definitions, projection, runtime execution, canonical rules, cleanup, and regressions agree.

## Load Context

1. Read `docs/design/project_context_units.md`.
2. Load CU-10, CU-13, CU-15, CU-16, and CU-19.
3. Read `docs/design/battle/equipment_ability_runtime.md` and `docs/design/battle/skill_runtime.md`.
4. Add CU-02/CU-11 for save or persistent equipment state, CU-18 for presentation, CU-20 for enemy/AI/BattleSim consumption, and CU-21 for text/headless surfaces.
5. Read the current owners and nearest regressions. Treat `docs/design/` and current code as truth; do not promote proposal fields as implemented.
6. Run the read-only inventory:

```powershell
python .codex/skills/evolve-equipment-ability-runtime/scripts/inventory_equipment_ability_surface.py --root . --term <mechanic>
```

Omit `--term` for the complete framework. Add `--changed-only` to audit a dirty worktree.
Term filtering is lexical and returns a scope seed, not a dependency graph. Expand it to the direct typed owners, canonical services, state/save projections, cross-path consumers, and focused tests reached by the mechanic.

## Scope Decision

- Use this skill when the repository lacks a generic typed mechanism or when an existing mechanism's cross-layer contract changes.
- Use `weapon-ability-landing` when a concrete weapon can be expressed entirely with existing typed fields and handlers.
- Use `design-godot-skill` when the work is primarily a `SkillDef` content/behavior design.
- Ask before adding compatibility aliases, fallback payloads, legacy migrations, or old save/schema support. Explain the concrete old data or caller that would otherwise break.

## Workflow

1. Translate the requested behavior into typed trigger, fact, condition, action, state, and ownership statements. Separate static content, battle-only state, and persistent equipment state.
2. Inventory equivalent mechanisms and current consumers. Prefer extension of an existing generic handler over a parallel path.
3. Complete every applicable row in [end-to-end-abi-matrix.md](references/end-to-end-abi-matrix.md). Do not stop after adding a payload Resource or runtime branch.
4. Validate closed domains through their canonical typed rules and open-content references through complete catalogs. Reject unknown handlers and unsupported consumer combinations before publishing the content snapshot.
5. Keep runtime rules behind narrow typed query/sink ports. Pass state in explicit contexts; do not let rules locate `BattleRuntimeModule` or another composition root.
6. Define event phase, provenance, recursion, batch, and nested-action semantics with [reaction-ordering.md](references/reaction-ordering.md). Delegate hit, save, damage, death, skill, movement, durability, and status semantics to canonical services.
7. Define projection, persistent state, source removal, teardown, and mutation-exact behavior with [state-save-projection.md](references/state-save-projection.md).
8. Audit manual execution, preview, AI, granted-skill availability, enemy roster, simulation seed, presentation/text, and writeback consumers. Include only applicable consumers, but state why excluded lanes are unaffected.
9. Add focused schema plus real-runtime regressions. Build before running C# headless tests.
10. Update `docs/design/project_context_units.md` only when ownership, dependencies, or recommended read sets changed.

## Non-Negotiable Boundaries

- Do not encode new framework behavior in generic `params`, weak dictionaries, or string handler fallbacks.
- Do not branch on a concrete item, weapon, trait, skill, binding, or status id in battle runtime.
- Do not let authored Resources or registries escape synchronous content construction.
- Do not mutate `BattleUnitState` while computing a projection; build a complete result and install it atomically.
- Do not let query ports expose a runtime owner or hidden state getter. Use explicit typed contexts.
- Do not duplicate canonical hit, save, damage, movement, status, skill, durability, or death rules inside an ability action resolver.
- Do not infer successful implementation from a loadable `.tres`; prove execution, cleanup, preview/AI parity where applicable, and failure behavior.
- Do not assume a canonical codec carries runtime-only equipment projection or diagnostic-exact data. Verify the current transfer owner.

## Validation

Run the narrowest relevant set:

```powershell
dotnet build magic.csproj
godot --headless --script tests/progression/schema/run_equipment_ability_content_registry_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_<focused>_regression.cs
git diff --check
```

Add rules, AI, state-schema, persistence, or lifecycle runners when those contracts changed. Do not include simulation or benchmark entry points unless explicitly requested.

## Deliverable

Report:

- ABI fields and consumers added or changed;
- validation and immutable projection path;
- event ordering, provenance, and canonical services reused;
- state owner, transfer/save behavior, and cleanup;
- ports and lifecycle binding;
- focused tests run and any intentionally deferred consumer.
