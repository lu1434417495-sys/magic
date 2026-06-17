---
name: design-godot-skill
description: Design, audit, repair, or refactor combat skill content for this Godot 4.6 C# project. Use when Codex creates or edits SkillDef .tres resources; optimizes invalid, incomplete, stale, weak, or nonconforming existing skills; changes combat_profile/effect_defs/passive_effect_defs/cast_variants/special_resolution_profile_id; adjusts mastery, level rewards, targeting, costs, weapon requirements, or attribute growth; or decides whether skill work needs C# runtime, schema, AI, or regression-test changes.
---

# Design Godot Skill

## Purpose

Use this skill to design combat skills in the progression and battle systems. A normal skill is a `SkillDef` resource in `data/configs/skills/` with a `CombatSkillDef` profile and one or more `CombatEffectDef` entries. Some high-complexity skills use `special_resolution_profile_id` and typed runtime/profile code instead of executable `effect_defs`.

## Operating Modes

- For an existing skill optimization or refactor, preview first unless the user explicitly asks for direct implementation. Inspect the resource and relevant runtime/test context, present the proposed field-level changes, then wait for confirmation before editing `.tres`, scripts, docs, or tests.
- For an existing invalid or nonconforming skill, run repair mode first: identify whether the problem is schema validation, runtime support, stale description, missing test coverage, AI/HUD mismatch, role/balance weakness, or unsupported compatibility assumptions. Propose the smallest coherent fix before redesigning the skill.
- For a new skill or an already-approved change, implement directly once the role, targeting, level rewards, mastery, and validation path are clear. Ask only for decisions that cannot be safely inferred.
- For a new closed mode, effect type, resource kind, damage category, target selector, save tag, or parameter family, identify the enum/typed rule utility or typed DTO owner before editing resources.

## Load Repo Context

1. Read `docs/design/project_context_units.md` first and use it as the architecture loading index.
2. For most combat-skill work, start from CU-13, CU-14, CU-15, and CU-16. Add CU-19 for regressions, CU-20 for enemy/AI coupling, and CU-21 for headless/text command surfaces only when needed.
3. Use actual source as the field authority. `project_context_units.md` is not a schema reference.

Minimum source read set by task:

| Task | Read |
|------|------|
| Skill resource authoring | `SkillDef.cs`, `CombatSkillDef.cs`, `CombatEffectDef.cs`, `CombatCastVariantDef.cs`, `SkillContentRegistry.cs`, this skill's `references/skill-config-schema.md` |
| Invalid/nonconforming skill repair | The target `.tres`, `SkillContentRegistry.cs` validation branches, relevant fixture/tests under `tests/progression/` and `tests/battle_runtime/`, and the runtime owner implied by the failing field/effect |
| Level caps, core selection, and growth | `SkillDef.cs`, `SkillContentRegistry.cs`, `SkillEffectiveMaxLevelRules.cs`, `ProfessionAssignmentService.cs`, `LevelGrowthEvaluationService.cs`, `ProgressionService.cs`, `AttributeGrowthContentRules.cs`, `CharacterManagementModule.cs`, `AttributeGrowthService.cs`, and progression regressions for effective max level, core selection, and attribute growth |
| Profession resource conventions | Existing tagged skills under `data/configs/skills/`, `CombatSkillDef.cs`, `CombatResourceIds.cs`, `CombatResourceKind.cs`, and `BattleRuntimeSkillTurnResolver.cs` |
| Targeting, areas, cast variants | `BattleTypedEnums.cs`, `CombatSkillTargetingContentRules.cs`, `CombatTargetTeamContentRules.cs`, `BattleSkillResolutionRules.cs`, relevant battle selection/runtime tests |
| Bow/weapon range skills | `BattleRangeService.cs`, `CombatSkillDef.cs`, `BattleRuntimeSkillTurnResolver.cs`, equipment/weapon projection rules, and similar bow or weapon-range skills |
| New or unusual effect behavior | `BattleTypedEnums.cs`, `SkillContentRegistry.AppendEffectValidationErrors`, `BattleSkillResolutionRules.cs`, and the runtime resolver that would execute the effect |
| Mastery changes | `BattleSkillMasteryService.cs`, `BattleRuntimeModule.cs`, `SkillContentRegistry.cs`, mastery regressions |
| Weapon-gated skills | `CombatSkillDef.cs`, `BattleRuntimeSkillTurnResolver.cs`, equipment/weapon projection rules, weapon dice regressions |
| Special-profile skills | `BattleSpecialProfileRegistry.cs`, the profile manifest/def, profile-specific runtime resolver, preview/HUD/AI/report tests |

## Existing Skill Audit

When previewing or repairing an existing skill, include:

- Current implementation: `skill_id`, display name, role, target mode, selection mode, area/range source, costs, cooldown, casting/fate/backlash fields, weapon gates, effect chain, cast variants, passive effects, special profile, level rewards, static/dynamic level caps, non-core cap, absolute cap after trigger lock, mastery trigger/amount, core-selection / level-trigger growth, and existing tests.
- Evidence: validation errors, failing tests, source owner rules, similar valid skills, and runtime paths that consume the affected fields. For disputed behavior, show the concrete method chain and final state flag instead of relying on wording in this skill or the resource field name.
- Problems or design gaps: invalid schema, unsupported field/value, stale descriptions, unclear role, profession-resource mismatch, mismatched costs, unsafe runtime ownership, AI/HUD mismatch, missing validation, or unsupported compatibility assumptions.
- Proposed minimal repair: exact fields or runtime owners to change, why each change is necessary, and whether it is a schema fix, behavior fix, description fix, balance fix, or test fix.
- Optional design improvement after repair: role, costs, level rewards, mastery, tags, growth, and whether C# code is needed.
- Validation plan and concrete regression commands.
- Open decisions that require user approval.

## Design Checklist

Decide before writing config:

- Role: output, control, support, mobility, summon/terrain, special profile, or composite.
- Consumption: AP/action economy, stamina/体力, MP/法力, aura/斗气, cooldown TU, casting time, maintenance DC, spell control DC. Use the profession defaults below before tuning numbers.
- Targeting: `target_mode`, `target_team_filter`, `target_selection_mode`, `selection_order_mode`, range source, `range_value`, `area_pattern`, `area_value`, target counts, repeat-target rules, and optional cast variants. For bow and weapon-range skills, treat equipped weapon range as the default source.
- Effect chain: direct `effect_defs`, `passive_effect_defs`, `cast_variants[*].effect_defs`, or a special profile. Do not use `effect_defs` as executable truth for a special-profile skill unless the current manifest/runtime explicitly allows it.
- Level caps and rewards: non-core max, approved static non-core/absolute-max pair, dynamic max fields if any, per-driver-level max table or breakpoints when relevant, mastery curve coverage, cost/range/area/target-count/attack/casting changes in `level_overrides`, effect unlock windows, cast variant unlocks, and description config changes.
- Mastery: trigger mode, amount mode, and whether runtime result facts currently support the desired trigger.
- Core-selection / level-trigger growth: `growth_tier`, `attribute_growth_progress`, total tier budget from `AttributeGrowthContentRules`, base attribute distribution, and per-attribute application/capping rules from `AttributeGrowthService`. For normal static-cap skills, the non-core cap is the core-selection point: a 3 -> 5 skill can enter the core/promotion chain at level 3. The submitted promotion path then locks the trigger, applies `attribute_growth_progress` once, and unlocks the effective max to the absolute `max_level`; do not wait until level 5/7/9/10 to grant this growth.
- AI and enemy coupling: whether enemies need action definitions, brain hints, score profiles, or roster/template changes.

## Authoring Rules

- Use the C# resource classes and `SkillContentRegistry` validation as the source of truth. If this skill's schema reference disagrees with code, trust code and update the reference.
- If the user questions timing, caps, range, resources, or growth behavior, re-open the relevant C# owner and regression before answering or editing. Treat this skill and its reference as navigation only.
- `level_overrides` keys are integer levels in `.tres` dictionaries, for example `3: { "stamina_cost": 20 }`. Do not use string keys there. `level_description_configs` uses string keys such as `"3"`.
- To discover supported fields, read exported properties and typed getters in the resource class, then read the corresponding `SkillContentRegistry` validation branch. Do not copy a full field list from this skill.
- To discover supported `level_overrides` keys, read `CombatSkillDef.GetEffective*` methods and the `combatProfile.level_overrides` validation loop in `SkillContentRegistry`.
- For bow-tagged or bow-required skills, default the base range to the equipped bow's `weapon_attack_range`. Do not treat `combat_profile.range_value` as the visible absolute range of a normal bow skill. Use a configured fixed range only for non-weapon skills or explicit exceptions such as ground relocation. If a special bow skill extends reach, implement and describe it as an additive bonus on top of weapon range after reading `BattleRangeService`.
- For warrior skills, default to AP plus stamina for weapon, mobility, control, and routine martial actions. Use aura instead of stamina for qi/aura, elemental martial, finisher, or high-impact tactical skills. Do not use MP for warrior skills unless the user approves a deliberate hybrid/cross-profession exception.
- For mage skills, use MP as the primary required resource. Use AP, cooldown, casting time, maintenance DC, and spell control DC to express action/time/risk. Use aura only for overcharged, forbidden, cataclysmic, or explicit hybrid magic. Treat mage stamina costs as exceptional and justify or repair them.
- To audit skill levels, read `SkillEffectiveMaxLevelRules` before inferring behavior from examples. Report the non-core cap, core cap, and any dynamic max-level formula or breakpoint table that can raise the effective maximum.
- For normal static-cap skills, use the approved cap ladder: `non_core_max_level = 3` -> `max_level = 5`, `5` -> `7`, `7` -> `9`, and `9` -> `10`. Do not invent `3` -> `3`, `5` -> `5`, or other equal/unsupported pairs unless the runtime source or test explicitly defines a dynamic/nonstandard skill and the preview calls it out.
- Treat `attribute_growth_progress` as the one-time reward for the core-selection / level-trigger promotion chain. Read the code path before describing timing: `ProfessionAssignmentService.PromoteNonCoreToCore` allows a learned non-core skill at its effective cap to become core; `SetActiveTriggerCoreSkillTyped` marks a core skill as the active trigger; `ProgressionService.PromoteProfession` locks the ready trigger; `CharacterManagementModule` then applies growth and marks it claimed. The user-facing flow may feel immediate after choosing the core skill, but the state change that unlocks the absolute `max_level` is `is_level_trigger_locked`.
- Use `CombatSkillDef.required_weapon_families` for positive equipped-weapon-family gates. Do not also set damage `requires_weapon = true` just to enforce the family gate; use damage weapon fields only when the damage effect itself needs weapon dice/tag resolution.
- Put migrated effect data on typed `CombatEffectDef` fields, not in `params`. Read `TypedEffectParamTargets`, `AppendTypedEffectParamValidationErrors`, and effect-specific validation branches before adding or moving params.
- `min_skill_level` and `max_skill_level` gate effects by skill level. Overlap is allowed only when the combined behavior is intentional and covered by tests or an existing pattern.
- `StringName` exported fields are resource boundaries. Runtime logic should decode them through enum-backed converters, typed rules, or DTOs before use.
- Do not add compatibility logic, legacy aliases, fallback migrations, or old payload/schema support without confirming with the user and explaining the concrete breakage that compatibility would avoid.

## Engine Change Decision

| Need | Likely owner |
|------|--------------|
| New effect kind or closed value | `BattleTypedEnums.cs`, `CombatEffectDef.cs`, `SkillContentRegistry.cs`, `BattleSkillResolutionRules.cs`, relevant runtime resolver |
| New targeting/area/cast variant mode | `BattleTypedEnums.cs`, `CombatSkillTargetingContentRules.cs`, `CombatCastVariantDef.cs`, battle selection/range/runtime rules |
| New hit, save, or damage math | `BattleHitResolver.cs`, `BattleDamageResolver*.cs`, `BattleSaveContentRules.cs`, targeted battle rules tests |
| New follow-up cost or repeat-attack behavior | `BattleRepeatAttackResolver.cs`, attack policy/rules tests, skill runtime tests |
| New mastery trigger or amount mode | `BattleTypedEnums.cs`, `BattleSkillMasteryService.cs`, `BattleRuntimeModule.cs`, `SkillContentRegistry.cs` |
| New special-profile behavior | special profile manifest/registry, profile resolver, preview/HUD/AI/report contracts |

## Validation

After authoring or code changes:

1. Run `dotnet build magic.csproj`.
2. Run relevant schema regressions, commonly `godot --headless --script tests/progression/schema/run_skill_requirements_typed_regression.cs`, `run_skill_attribute_growth_typed_regression.cs`, and/or `run_battle_save_skill_schema_regression.cs`.
3. Run relevant battle regressions under `tests/battle_runtime/skills/`, `tests/battle_runtime/rules/`, or `tests/battle_runtime/runtime/`.
4. Run AI/enemy/headless tests only when the change touches those surfaces.
5. Do not include battle simulation or balance runners in a routine validation pass unless the user explicitly asks for simulation or balance analysis.
6. Check static or dynamic max-level fields, the approved non-core/absolute-max cap pair, bow/weapon range source, mastery curve length, effect/cast-variant level gates, and level descriptions against the current effective max-level rules.
7. Check `attribute_growth_progress` sums to the `growth_tier` budget, uses valid base attributes, and matches the current core-selection / level-trigger promotion chain timing in source.
8. Check warrior skills do not consume MP, mage skills consume MP, and any stamina/aura/resource exception is backed by an existing pattern or an explicit design reason.
9. Check descriptions and `level_description_configs` match the real effective values.
10. Check new closed value domains have typed conversion and invalid-value schema coverage, not only runtime string comparisons.
11. If runtime relationships, ownership boundaries, or recommended read sets changed, update `docs/design/project_context_units.md`. Do not add parameter-by-parameter skill notes there.

## References

- `references/skill-config-schema.md` - Schema owner map, source-reading commands, stable pitfalls, and `.tres` examples. It is not a complete field list.
