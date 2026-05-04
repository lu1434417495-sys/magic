---
name: design-godot-skill
description: Guide for designing combat skills in this Godot 4.6 project. Use when creating new skills, refactoring existing skill configs, or adding level-up rewards / mastery growth / attribute growth to skills. Covers .tres resource authoring, CombatSkillDef/CombatEffectDef configuration, engine code extension decisions, and validation.
---

# Design Godot Skill

## Overview

This skill provides the complete workflow for designing a combat skill in the project's progression/battle system. A skill is defined by a `.tres` resource under `data/configs/skills/` and may require engine extensions when introducing new mechanics.

## Workflow

### Step 0: Existing Skill Optimization Preview

When optimizing or refactoring an existing skill config, do not edit `.tres`, scripts, docs, or tests immediately unless the user explicitly asks for direct implementation. First inspect the current resource and relevant runtime/test context, then present detailed information and wait for user confirmation.

The preview must include:
- Current implementation: `skill_id`, display name, role, target mode, range, costs, cooldown, effect chain, level rewards, mastery trigger, attribute growth, and existing tests.
- Problems or design gaps found in the current config.
- Proposed field-level changes: effects, `level_overrides`, costs, cooldowns, mastery, tags, growth, and whether engine code is needed.
- Validation plan and regression commands.
- Open decisions that need user approval.

### Step 1: Skill Positioning

Decide before writing any config:
- **Role**: output / control / support / mobility / composite?
- **Consumption**: AP, stamina, MP, aura, cooldown TU
- **Effect chain**: damage / status / heal / shield / repeat_attack / terrain?
- **Level rewards**: What changes at each level? (cost reduction / new effects / penalty reduction / damage scaling)
- **Mastery trigger**: `skill_damage_dice_max` / `weapon_attack_quality` / `damage_dealt` / `status_applied`?
- **Attribute growth**: Which base attributes on core-max? (`strength` / `agility` / `constitution` / `perception` / `intelligence` / `willpower`)

### Step 2: Create `.tres` Resource

Required fields on `SkillDef`:

| Field | Rule |
|-------|------|
| `skill_id` | Globally unique StringName |
| `max_level` | Mastery curve size must equal this |
| `non_core_max_level` | Usually 3 when max_level=5 |
| `mastery_curve` | PackedInt32Array, size == max_level |
| `tags` | e.g. `[warrior, melee, output]` |
| `growth_tier` | `basic`(60) / `intermediate`(120) / `advanced`(180) / `ultimate`(240) |
| `attribute_growth_progress` | Dict summing to growth_tier budget; keys from BASE_ATTRIBUTE_IDS |
| `combat_profile` | Sub-resource `CombatSkillDef` |

Required fields on `CombatSkillDef`:

| Field | Rule |
|-------|------|
| `ap_cost` / `stamina_cost` / `cooldown_tu` | Base combat costs |
| `attack_roll_bonus` | Hit check modifier |
| `level_overrides` | Dict keyed by min level; values can override ap/stamina/mp/aura/cooldown |
| `mastery_trigger_mode` | `skill_damage_dice_max` / `weapon_attack_quality` / `damage_dealt` / `status_applied` / `effect_applied` |
| `mastery_amount_mode` | `per_target_rank` / `per_cast_hp_ratio` |
| `required_weapon_families` | Optional positive weapon-family gate such as `[&"bow"]`; use this instead of `params.requires_weapon` when the skill must require a specific equipped weapon family |
| `effect_defs` | Array of `CombatEffectDef`; use `min_skill_level`/`max_skill_level` for tiered unlocks |

### Step 3: Design Effect Chain (`CombatEffectDef`)

Supported `effect_type` values:
- `damage`: `power`, `damage_tag`, `params.add_weapon_dice`, `params.requires_weapon`
- `status`: `status_id`, `duration_tu`, `power`, `trigger_event`
- `heal` / `shield`: `power`, `effect_target_team_filter`; heal supports `params.dice_count/dice_sides`
- `stamina_restore`: `power`, `effect_target_team_filter`; supports `params.dice_count/dice_sides`
- `charge`: `params.skill_id`, `params.base_distance`, `params.distance_by_level`
- `forced_move`: `forced_move_mode`, `forced_move_distance`
- `repeat_attack_until_fail`: Controller effect; see references for param schema
- `terrain` / `terrain_effect` / `terrain_replace` / `terrain_replace_to`: Ground mutation
- `apply_status`: Alias for `status`

Level-tier effects: set `min_skill_level` and/or `max_skill_level` on each `CombatEffectDef`.

Use `CombatSkillDef.required_weapon_families` for specific equipped-weapon gates. For example, a bow-only skill should set `required_weapon_families = [&"bow"]` and should not also set damage params `requires_weapon = true`; the family gate already implies an equipped valid weapon.

### Step 4: Decide Whether Engine Code Needs Changing

| New Mechanic | File to Modify | Example |
|-------------|----------------|---------|
| New hit penalty math | `scripts/systems/battle_hit_resolver.gd` | `exponential_penalty`, `penalty_free_stages_by_level` |
| New follow-up cost model | `scripts/systems/battle_repeat_attack_resolver.gd` | `follow_up_fixed_cost`, `follow_up_cost_addition` |
| New mastery bonus logic | `scripts/systems/battle_runtime_module.gd` + resolver | `record_skill_mastery_bonus` |
| New trigger mode | `scripts/systems/battle_runtime_module.gd` | `_is_skill_mastery_qualifying_result` |
| New amount mode | `scripts/systems/battle/runtime/battle_skill_mastery_service.gd` + `skill_content_registry.gd` | `_resolve_skill_mastery_target_amount` |
| New effect type | `scripts/systems/battle/rules/battle_damage_resolver.gd` + `battle_skill_resolution_rules.gd` | match branch in `_apply_effect_to_target` |

**Rule**: Prefer adding optional `params` entries. Keep backward compatibility so existing skills are untouched.

### Step 5: Validation & Regression

After authoring:
1. Run schema regression: `godot --headless --script tests/progression/run_skill_schema_regression.gd`
2. Run battle protocol regression: `godot --headless --script tests/battle_runtime/run_battle_skill_protocol_regression.gd`
3. Check `level_overrides` keys are numeric strings (e.g. `"2"`)
4. Check `attribute_growth_progress` sum equals `growth_tier` budget
5. Check `effect_defs` have non-overlapping `min/max_skill_level` ranges

## References

- `references/skill-config-schema.md` — Complete field schema, params dictionaries, and example `.tres` snippets
