---
name: weapon-ability-landing
description: "Land concrete weapon content and its configured equipment abilities in the E:/game/magic Godot 4.6 C# repository. Use when selecting a weapon from docs/content/weapons/by_family, adding or revising its ItemDef/TraitDef/EquipmentAbilityContentPackDef/SkillDef .tres resources, obtaining weapon-specific approval, or writing the real battle regression for that weapon. When the primary task is evolving the shared trigger/fact/condition/action/state ABI or equipment reaction framework without a concrete weapon landing, use evolve-equipment-ability-runtime instead."
---

# Weapon Ability Landing

## Purpose

Use this skill to turn a weapon design entry into real, configured game content. The fixed flow is: inspect the source weapon, present the exact weapon and test cases for approval, then implement by TDD with configuration-first traits, equipment ability bindings, and SkillDef resources where the weapon grants an active/selectable ability.

This is a project-specific workflow for `E:/game/magic`.

## Required Context

1. Read `docs/design/project_context_units.md` first.
2. Load the weapon source from `docs/content/weapons/by_family/`, and treat it as content intent rather than runtime truth.
3. For equipment abilities read CU-10, CU-13, CU-15, CU-16, and CU-19 in the context map.
4. Treat C# owners as truth over design prose.
5. Use `godot-test-writing` before adding or changing tests, and follow a focused red-green-refactor cycle through that repository test workflow.
6. Use `evolve-equipment-ability-runtime` first when the work is primarily a shared ABI, reaction-ordering, projection/save, port, or lifecycle change rather than a concrete weapon landing.

Core owner files to inspect as needed:

| Area | Owners |
|---|---|
| Item resources | `scripts/player/warehouse/ItemDef.cs`, `data/configs/items/*.tres` |
| Trait resources | `scripts/player/progression/TraitDef.cs`, `TraitContentRegistry.cs`, `TraitContentRules.cs`, `data/configs/traits/*.tres` |
| Skill resources | `scripts/player/progression/SkillDef.cs`, `CombatSkillDef.cs`, `CombatEffectDef.cs`, `SkillContentRegistry.cs`, `data/configs/skills/*.tres` |
| Equipment ability authoring | `scripts/player/progression/equipment_abilities/EquipmentAbilityAuthoringDefs.cs`, `EquipmentAbilityContentRegistry.cs`, `EquipmentAbilityRuntimeDefinitions.cs` |
| Equipment state | `scripts/player/warehouse/EquipmentInstanceState.cs`, `scripts/player/equipment/*` |
| Battle projection | `BattleUnitFactory.cs`, `BattleEquipmentAbilityProjectionService.cs`, `BattleTraitPassiveProjectionService.cs` |
| Battle runtime | `BattleEquipmentAbilityRuntimeService.cs`, `BattleSkillAvailabilityService.cs`, `BattleAttackCheckPolicyService.cs`, `BattleHitResolver.cs`, `BattleStatusSemanticTable.cs` |

## Approval Gate

Before editing weapon content, show the user:

1. Weapon source: file, line, display name, family, base item, base damage/range, price, and all listed traits or specials.
2. Mechanism classification: supported now, needs generic runtime support, should be a granted `SkillDef`, or should be deferred.
3. Use case design: equip/unequip projection, unit state changes, true attack/damage flow, status/usage/state expiry, and cleanup.
4. Exact balance choices that are not forced by existing config, including every duration as an explicit TU value.

Wait for user confirmation unless the user has already approved that exact weapon and behavior in the current conversation.

## Duration Contract

All weapon ability durations must be defined in TU before approval. This includes status durations, mark expiry, summon lifetime, cooldowns, lockouts, terrain or area effect lifetime, delayed effects, and any "round", "turn", "minute", or "hour" text from the source design.

- Approval text must show the source duration and the chosen TU value, for example `1 round => 60TU`.
- Plans, `.tres` resources, tests, and runtime assertions must use typed TU fields such as `duration_tu` or the relevant typed TU owner; natural-language duration is not an implementation value.
- Do not copy design provenance into formal runtime resources. Avoid `source_traces`, `source_kind`, `source_file`, `bullet_text`, or similar design-audit fields in landed `.tres` content; keep source prose in approval notes, tests, or design docs instead.
- If the conversion is unclear, stop and ask the user before writing content or tests.

## Content Model

Use configuration as the weapon-specific source of truth.

| Content | Rule |
|---|---|
| Item | Create or update `data/configs/items/weapon_unique_<weapon_type>_<name>.tres`, where `<weapon_type>` comes from the concrete `base_item_id` such as `greataxe`, `longsword`, or `heavy_crossbow`; do not include source-list numbers in filenames. Put fixed trait ids on the item. |
| Trait | Use one trait per semantic feature where possible. Prefer ids like `weapon.<family>.<name>.<feature>`. `TraitDef` is an identity/source and static-passive boundary, not a trigger/condition/action behavior engine. |
| Equipment ability pack | Put the real weapon behavior wiring here: trigger, condition group, roll gate, outcome table, action results, state schema, granted actions, overlays, and world effects in `data/configs/equipment_abilities/<name>_pack.tres`. Binding ids should be stable, for example `binding.weapon.<family>.<name>.<feature>`. |
| Active ability | If the weapon grants something the player chooses, targets, pays AP/resource for, uses per period, or cools down, make it a `SkillDef`. The equipment trait/binding grants availability; the skill owns action economy and targeting. |
| Passive trait | Use trait typed passive fields for always-on unit facts such as save advantage tags, resistances, immunities, attribute modifiers, or other projection-ready data. |
| Runtime support | Add only generic handlers/facts/actions that can serve more than one weapon. Do not hardcode weapon ids or trait ids in battle logic. |

Godot `.tres` resources may reference schema scripts such as `TraitDef` or `EquipmentAbilityContentPackDef`; that is normal resource typing. Weapon behavior still belongs in generic runtime handlers and typed data fields, not in weapon-specific C# branches.

## Trait and Ability Boundary

For weapon abilities, `TraitDef` names and exposes a feature; `EquipmentAbilityContentPackDef` defines how that feature behaves. Do not claim a weapon behavior is implemented because a trait id, display text, description, or `effect_type = equipment_ability` exists.

Use `TraitDef` fields only for these cases:

- Source identity and matching: `trait_id`, categories, source kinds, stack and charge metadata.
- Static passive projection: attribute modifiers, save advantage tags, damage resistance entries, and other explicitly typed passive fields already consumed by runtime owners.
- Equipment ability source marker: `effect_type = equipment_ability` lets equipment ability bindings match the trait, but it does not define the behavior by itself.

Put configurable behavior in the equipment ability pack:

- Triggers such as on hit, on kill, on turn end, on damage roll, on damage applied, on granted skill used, or on battle end.
- Conditions, fact comparisons, equipment tag checks, status checks, and action-level gates.
- Results such as add damage dice, deal damage, heal, apply status, modify ability state, mark targets, clear status, schedule area effects, damage equipment durability, summon or consume summoned units, immediate weapon attacks, loot multipliers, attack roll modifiers, defense modifiers, damage roll mode overrides, granted skills, weapon profile overlays, and world effects.

When a weapon needs a condition or result that is not currently configurable, add a generic typed authoring payload/definition, registry validation, runtime handler, and focused regression. The weapon data should then reference that generic handler; battle code should not branch on that weapon id or trait id.

## Mechanism Decisions

Use this decision table when translating design text:

| Design phrase | Default implementation |
|---|---|
| On equip, permanent while held | Trait passive projection or equipment ability source projected into `BattleUnitState`. Test removal restores state. |
| On weapon hit | Equipment ability binding with a true weapon-damage hit fact. Trigger only when this weapon contributed weapon damage, not any skill by the holder. |
| On damage dealt | Use resolved damage facts such as `hp_damage`, not pre-mitigation intent. |
| Save or resistance | Use typed save/damage/resistance owners. Do not put new behavior in generic `params` when a typed field is needed. |
| Player chooses an action | Grant a `SkillDef` through equipment availability. Skill uses normal targeting, AP/resource, cooldown/TU, and direct-effect or attack resolution modes. |
| Duration text such as minutes, rounds, or "1 minute" | Convert to an explicit TU value before approval and implementation. Use TU fields such as `duration_tu`; do not leave natural-language duration in the plan, resource, or test. If the conversion is unclear, ask the user. |
| Per battle/day/month/permanent use | Use `EquipmentAbilityUsageRuntime` and typed period/state schema. Confirm the period if unclear. |
| State marker or target mark | Use typed equipment ability state/mark support. Define ownership, expiry, death cleanup, and whether it is unique per source. |
| Creature category | Read from `BattleUnitState.creature_type_tags`, not enemy templates. |
| Body size or attributes | Use generic fact queries such as body size or attribute value. |
| AC component changes | Adjust this attack's defense calculation only. Do not mutate target `armor_class` unless the design explicitly applies a status. |
| Status semantics | Add generic status behavior in `BattleStatusSemanticTable`, movement, casting, or turn resolver owners. Do not special-case the weapon. |
| Unsupported subsystem | Present the mismatch and ask whether to defer, redesign, or add a generic subsystem. Do not silently approximate. |

## Typed Data Rule

Prefer typed C# resources, DTOs, lists, enums, and small value objects. Avoid `Godot.Collections.Dictionary`, stringly typed params, or `Dictionary<StringName, Variant>` as the formal ABI for new behavior.

Accept dictionaries only at catalog indexes, Godot boundary projections, or existing owner caches where the key-value map is the real data structure. If a new weapon needs a new configurable concept, add a typed resource/definition and registry validation for it.

Do not add compatibility aliases, legacy field migrations, or fallback payload support without explicit user confirmation.

## Implementation Flow

1. Read the source weapon and similar implemented weapons.
2. Present weapon info and test design to the user.
3. After approval, use `godot-test-writing` to write or update focused failing tests first.
4. Implement the smallest generic typed mechanism that makes the tests pass.
5. Add item, trait, equipment ability pack, and skill resources.
6. Update validation rules when new authoring fields or enum values are added.
7. Update `docs/design/project_context_units.md` if ownership boundaries or required read sets changed.
8. Run focused verification.
9. Search scripts for the weapon id and trait ids; weapon-specific ids should normally appear only in data and tests.
10. Report what is configured, what is generic runtime, and what remains deferred.

## Test Contract

Every landed weapon needs a regression that proves user-visible behavior through the real owner path. Prefer `tests/battle_runtime/runtime/run_<weapon>_weapon_ability_regression.cs`.

Minimum assertions:

- Content loads through the registries/catalog.
- Equipment can be equipped by the intended unit and rejected when requirements fail.
- Equipping projects traits, skills, status facts, ability sources, or passive effects onto the battle unit.
- Unequipping or using a unit without the weapon does not keep the projected state.
- Active/granted skills appear through `BattleSkillAvailabilityService`, not by manually inserting known skill ids.
- Triggered traits use the real attack/hit/damage path when the behavior is hit or damage based.
- Saves, conditions, immunities, resistance bypass, AC adjustments, target marks, exact TU duration values, and use counters are asserted at their public typed state.
- Cleanup is covered: expiry, death, target invalidation, combat end, or period reset when the weapon uses state.

Do not test only a direct service stub when the claim is that the ability works on a unit in battle. A pure service test is acceptable only as extra coverage for a generic helper.

## Verification Commands

Run the narrowest relevant set:

```powershell
dotnet build magic.csproj
godot --headless --script tests/battle_runtime/runtime/run_<weapon>_weapon_ability_regression.cs
godot --headless --script tests/progression/schema/run_equipment_ability_content_registry_regression.cs
rg -n "<weapon_id>|<trait_id>|<binding_id>" scripts
git diff --check
```

If the skill grants or changes `SkillDef` behavior, also run the nearest skill/progression validation or battle skill runtime regression implied by the touched owner.

## Multiple Weapons

If the user asks to land multiple weapons and explicitly authorizes subagents, split by disjoint write ownership:

- One worker per weapon owns that weapon's item, traits, ability pack, skill, and test.
- Shared runtime/schema changes must be assigned to one owner or done by the main agent before workers start.
- Tell workers they are not alone in the codebase and must not revert unrelated edits.
- Main agent integrates and reruns all verification.

## Common Mistakes

| Mistake | Fix |
|---|---|
| Implementing a weapon by hardcoding its id in battle runtime | Move the id to `.tres` config and add a generic typed handler or fact. |
| Treating `TraitDef` text, `trait_id`, or `effect_type = equipment_ability` as implemented behavior | Put trigger/condition/action wiring in an equipment ability pack, or use an existing typed passive field if the feature is truly always-on. |
| Treating an active weapon ability as a trait-only effect | Make a `SkillDef`; let the equipment ability grant it. |
| Showing only final code, not the chosen weapon/use cases | Stop and present the approval gate first. |
| Testing a helper but not the real battle flow | Add a battle runtime regression that equips the item and resolves the real attack or skill. |
| Leaving equip projection behind after unequip | Assert both equipped and unequipped unit states. |
| Copying design prose like "1 minute" into the landing plan | Convert it to a concrete TU duration first, state that TU value in the approval gate, and assert the public status/skill state uses the same TU value. |
| Reading creature type from enemy templates | Use battle unit creature tags. |
| Adding new config in params | Add typed fields and registry validation. |
| Adding compatibility fallback silently | Ask the user first and explain the concrete breakage it prevents. |
