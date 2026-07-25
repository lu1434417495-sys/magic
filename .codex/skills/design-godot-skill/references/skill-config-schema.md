# Skill Schema Navigation

This file is a navigation aid, not the schema authority. Do not copy a complete field list from here. Read the C# owner classes and validators for the exact current fields, closed values, and constraints.

## Contents

- Source Owners
- How To Read The Current Schema
- Existing Skill Repair
- Level Caps, Core Selection, And Growth
- Weapon Range Rules
- Profession Resource Defaults
- Stable Pitfalls
- Params And Typed Fields
- Examples
- Validation

## Source Owners

| Surface | Read |
|---------|------|
| Root skill fields and typed backing state | `scripts/player/progression/SkillDef.cs` |
| Combat profile, costs, targeting, variants, mastery, weapon gates | `scripts/player/progression/CombatSkillDef.cs` |
| Effect fields and effect-local typed helpers | `scripts/player/progression/CombatEffectDef.cs` |
| Cast variant fields | `scripts/player/progression/CombatCastVariantDef.cs` |
| Schema validation and unsupported legacy params | `scripts/player/progression/SkillContentRegistry.cs` |
| Skill level caps, core selection, and effective max behavior | `scripts/systems/progression/SkillEffectiveMaxLevelRules.cs`, `scripts/systems/progression/ProfessionAssignmentService.cs`, `scripts/systems/progression/LevelGrowthEvaluationService.cs`, `scripts/systems/progression/ProgressionService.cs` |
| Attribute growth budget and application | `scripts/player/progression/AttributeGrowthContentRules.cs`, `scripts/systems/progression/CharacterManagementModule.cs`, `scripts/systems/progression/AttributeGrowthService.cs` |
| Bow and weapon range behavior | `scripts/systems/battle/rules/BattleRangeService.cs`, `scripts/player/progression/CombatSkillDef.cs`, `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs` |
| Resource cost fields and payment rules | `scripts/player/progression/CombatSkillDef.cs`, `scripts/player/progression/CombatSkillResourceCosts.cs`, `scripts/player/progression/CombatResourceIds.cs`, `scripts/systems/battle/core/CombatResourceKind.cs`, `scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs` |
| Closed battle names and enum conversion | `scripts/systems/battle/_interop/BattleTypedEnums.cs` |
| Target/team/growth/damage/save rule labels | `CombatSkillTargetingContentRules.cs`, `CombatTargetTeamContentRules.cs`, `AttributeGrowthContentRules.cs`, `DamageTagContentRules.cs`, `BattleSaveContentRules.cs` |
| Effect collection and variant execution policy | `scripts/systems/battle/rules/BattleSkillResolutionRules.cs` |

## How To Read The Current Schema

Use source searches instead of maintaining a duplicated field catalog:

```bash
rg -n "\\[Export\\]|public .*\\{ get; set; \\}" scripts/player/progression/SkillDef.cs scripts/player/progression/CombatSkillDef.cs scripts/player/progression/CombatEffectDef.cs scripts/player/progression/CombatCastVariantDef.cs
rg -n "Append.*ValidationErrors|level_overrides|TypedEffectParamTargets|unsupported" scripts/player/progression/SkillContentRegistry.cs
rg -n "enum BattleEffectKind|ToEffectKind|ToStringName\\(BattleEffectKind|CombatSkillMasteryTriggerMode|BattleForcedMoveMode|BattleTargetSelectionMode" scripts/systems/battle/_interop/BattleTypedEnums.cs
rg -n "GetEffective|level_overrides" scripts/player/progression/CombatSkillDef.cs scripts/player/progression/SkillContentRegistry.cs
rg -n "max_level|non_core_max_level|dynamic_max_level|mastery_curve|GetEffective.*MaxLevel" scripts/player/progression/SkillDef.cs scripts/player/progression/SkillContentRegistry.cs scripts/systems/progression/SkillEffectiveMaxLevelRules.cs
rg -n "PromoteNonCoreToCore|SetActiveTriggerCoreSkillTyped|GetReadyActiveLevelTriggerSkillId|LockReadyActiveLevelTriggerSkill|PromoteProfession\\(" scripts/systems/progression tests/progression
rg -n "attribute_growth_progress|growth_tier|GetTierBudget|core_max_growth_claimed|active_level_trigger_core_skill_id|is_level_trigger_locked|ApplyAttributeProgressTyped" scripts/player/progression scripts/systems/progression tests/progression
rg -n "GetEffectiveSkillRange|ResolveBaseSkillRange|weapon_attack_range|range_value|required_weapon_families|SkillHasTag\\(skillDef, \"bow\"" scripts/systems/battle/rules/BattleRangeService.cs scripts/player/progression/CombatSkillDef.cs scripts/systems/battle/runtime/BattleRuntimeSkillTurnResolver.cs
rg -n "tags =.*&\"(warrior|mage)\"|ap_cost =|stamina_cost =|mp_cost =|aura_cost =" data/configs/skills -g "*.tres"
```

Read in this order:

1. Resource exported fields in `SkillDef`, `CombatSkillDef`, `CombatEffectDef`, or `CombatCastVariantDef`.
2. Typed converters or rule utilities for closed values.
3. `SkillContentRegistry` validation for required fields, rejected legacy aliases, type strictness, and cross-field constraints.
4. Runtime owner that consumes the field, especially when changing behavior rather than only content.
5. Existing valid `.tres` resources and regression fixtures for local formatting and patterns.

## Existing Skill Repair

For an invalid or low-quality existing skill:

1. Inspect the target `.tres` and identify every sub-resource: root `SkillDef`, combat profile, effects, passive effects, cast variants, and special profile id.
2. Find schema errors in `SkillContentRegistry` first. If a regression already reports errors, map each error string back to its validation branch.
3. Classify the issue as schema-invalid, runtime-unsupported, stale description, AI/HUD mismatch, missing regression, or design/balance weakness.
4. Compare against nearby valid skills with the same profession/role/effect family. Use them as format and pattern examples, not as schema authority.
5. Propose the smallest repair that makes the skill valid and coherent before proposing balance or role redesign.
6. If repair requires compatibility with an old payload/schema, stop and ask the user. Explain what breaks without compatibility.

## Level Caps, Core Selection, And Growth

Do not infer level behavior from one existing resource. For every audited, repaired, or newly designed skill, read the current level-cap and growth owners and report:

- Non-core max: the configured non-core cap and whether it limits the skill before level-trigger lock.
- Core max: the absolute max after the chosen core skill is locked as the level-trigger skill. The code flag that raises the effective max is `is_level_trigger_locked`.
- Dynamic max: whether dynamic max fields are present, what stat or profession rank drives them, and how the current rule combines the dynamic value with `max_level`.
- Level coverage: whether `mastery_curve`, effect level gates, cast variant unlocks, `level_overrides`, and `level_description_configs` cover the intended max-level behavior.
- Core-selection / level-trigger growth: `growth_tier`, `attribute_growth_progress`, the total tier budget from `AttributeGrowthContentRules.GetTierBudget`, and the core-selection, level-trigger lock, application, and capping path in `ProfessionAssignmentService`, `LevelGrowthEvaluationService`, `ProgressionService`, `CharacterManagementModule`, and `AttributeGrowthService`.

Approved static cap ladder for normal skills:

| `non_core_max_level` | `max_level` after level-trigger lock |
|----------------------|----------------------------------|
| 3 | 5 |
| 5 | 7 |
| 7 | 9 |
| 9 | 10 |

Use this ladder when auditing or designing normal static-cap skills. Only deviate when `SkillEffectiveMaxLevelRules`, `SkillContentRegistry`, or a regression fixture explicitly proves the skill is dynamic or nonstandard, and call that out in the preview.

For normal static-cap skills, `non_core_max_level` is the core-selection point. A 3 -> 5 skill can enter the core/promotion chain at level 3, not at level 5. Source timing is:

1. `ProfessionAssignmentService.CanPromoteNonCoreToCore` requires the learned non-core skill to be at its current effective max, so an unlocked 3 -> 5 skill is eligible at level 3.
2. `PromoteNonCoreToCore` sets `is_core`, assigns the profession, and adds the profession core skill entry.
3. `SetActiveTriggerCoreSkillTyped` can mark that core skill as the active level trigger.
4. `ProgressionService.PromoteProfession` requires the active trigger to be ready, then `LockReadyActiveLevelTriggerSkill` sets `is_level_trigger_locked`.
5. `CharacterManagementModule.PromoteProfession` applies `attribute_growth_progress` once and sets `core_max_growth_claimed`.
6. `SkillEffectiveMaxLevelRules.GetEffectiveMaxLevel` returns the absolute `max_level` only after `is_level_trigger_locked` is true.

When the UI or workflow submits these steps as one core-selection promotion flow, describe the effect as immediate after choosing the core skill: the skill is locked, attribute growth is granted, and the max level is unlocked. Do not describe the reward as waiting until the absolute core max level 5/7/9/10.

Current growth-tier totals are owned by `AttributeGrowthContentRules`, not by this reference. Read that source before using the numbers. At the time of writing, the owner returns `basic = 60`, `intermediate = 120`, `advanced = 180`, and `ultimate = 240`.

## Weapon Range Rules

Read `BattleRangeService` before auditing weapon or bow skill range. Current runtime behavior:

- Bow, melee, and weapon-tagged skills are weapon-range skills. When the unit has an equipped weapon range, base skill range resolves to `weapon_attack_range`.
- `required_weapon_families` and `required_weapon_type_ids` also make the skill require the current weapon. Use the family gate for broad groups such as bow/hammer; use the type-id gate when a skill must match the exact `WeaponProfileDef.weapon_type_id`, such as `greatsword` without admitting every `sword` family weapon.
- For ordinary bow skills, do not treat `combat_profile.range_value` as the final visible range. Descriptions should say `射程随武器` or equivalent.
- If a special bow skill extends range, model and describe it as an additive bonus on top of weapon range. Verify the exact owner first, such as range-bonus status/passive behavior or a special profile; do not replace weapon range with a fixed configured range.
- Use configured fixed `range_value` for non-weapon skills or explicit exceptions such as ground relocation, where `BattleRangeService` intentionally returns configured range.

## Profession Resource Defaults

Use current tagged skills as examples, but use these conventions as the default resource design rule:

- Warrior: use AP plus stamina for normal weapon attacks, mobility, control, guarding, and tactical martial actions. Use AP plus aura for qi/aura skills, elemental martial skills, finishers, transformations, high-impact area effects, or high-tier command effects. Do not use MP for warrior skills unless the user approves an explicit hybrid/cross-profession exception.
- Mage: use MP as the primary required resource. Use AP, cooldown, casting time, maintenance DC, and spell control DC to express action commitment, cadence, channeling, and risk. Use aura only for overcharged, forbidden, cataclysmic, or deliberately hybrid magic. Treat stamina on mage skills as an exception to justify or repair, not as a default.
- Passive skills usually do not need a combat profile. If a self-only martial reaction has no AP cost, compare against nearby valid mobility/reaction skills and explain why stamina-only is intentional.
- Every non-default resource mix must be reflected in `level_description_template`/`level_description_configs`, AI cost scoring expectations, and the validation summary.

## Stable Pitfalls

- `level_overrides` uses integer keys in `.tres`, for example `3: { "stamina_cost": 20 }`.
- `level_description_configs` uses string keys, for example `"3": { "range": "5" }`.
- AP is action economy. Stamina, MP, and aura are profession/resource identity. Do not swap stamina and MP just to tune cost magnitude.
- MP and aura may be locked until content unlocks them; stamina is default-unlocked. Read `CombatResourceIds` and `BattleRuntimeSkillTurnResolver` before adding a new MP/aura cost to a skill family.
- Non-core and absolute max-level behavior is runtime-owned. Validate it through `SkillEffectiveMaxLevelRules`, not by reading `max_level` alone.
- For normal static-cap skills, `max_level` equal to `non_core_max_level` is usually a design defect because the core-selection / level-trigger lock chain gives no extra cap. Use the approved cap ladder unless a dynamic/nonstandard rule is explicitly documented by source or tests.
- Bow skills normally use equipped bow range. A numeric `range_value` on an old bow resource may be ignored or only act as fallback; do not report it as the skill's actual range without checking `BattleRangeService`.
- `attribute_growth_progress` currently uses strict string keys and strict positive int values; confirm the current owner in `SkillDef.cs` and validator before editing.
- `attribute_growth_progress` must total the `growth_tier` budget and represents the one-time reward when the core-selection / level-trigger promotion chain locks the trigger, not routine per-level growth and not an absolute core-max-level reward.
- `StringName` fields are resource boundaries. Runtime logic should consume typed enum/rule conversion, not raw string comparisons.
- `required_weapon_families` is the broad positive weapon-family gate; `required_weapon_type_ids` is the exact positive weapon-profile-type gate. Damage `requires_weapon` is for effects that need weapon damage resolution, not for weapon-family/type gating by itself.
- Special-profile skills may not use `effect_defs` as executable truth unless the current special-profile manifest/runtime explicitly allows it.
- Passive effects use the normal effect schema, but some executable effects may be invalid there. Check `passive_effect_defs` validation.

## Params And Typed Fields

Prefer typed `CombatEffectDef` fields over `params` when a field exists. Before writing params:

```bash
rg -n "TypedEffectParamTargets|params\\.|DictStringName\\(parameters|DictInt\\(parameters|parameters.ContainsKey" scripts/player/progression/SkillContentRegistry.cs
rg -n "Get.*ParamTyped|@params" scripts/player/progression/CombatEffectDef.cs
```

Only use `params` for effect-specific data that remains dictionary-owned. If a validator says a param is unsupported, move the value to the typed field named in the error instead of adding compatibility aliases.

## Examples

Use real resources as examples:

```bash
rg -n "skill_id = &\"warrior_combo_strike\"|level_overrides =|effect_type = &\"repeat_attack_until_fail\"" data/configs/skills/warrior_combo_strike.tres
rg -n "cast_variants =|passive_effect_defs =|special_resolution_profile_id =" data/configs/skills tests/progression/fixtures tests/battle_runtime
```

Minimal override shape:

```text
level_overrides = {
3: {
"stamina_cost": 20
}
}
```

Minimal description config shape:

```text
level_description_configs = {
"3": {
"stamina": "20"
}
}
```

## Validation

Use validation to discover whether the repair is complete:

```bash
dotnet build magic.csproj
godot --headless --script tests/progression/schema/run_skill_requirements_typed_regression.cs
godot --headless --script tests/progression/schema/run_skill_attribute_growth_typed_regression.cs
godot --headless --script tests/progression/schema/run_battle_save_skill_schema_regression.cs
godot --headless --script tests/progression/core/run_skill_effective_max_level_rules_regression.cs
godot --headless --script tests/progression/core/run_attribute_growth_service_regression.cs
godot --headless --script tests/progression/core/run_character_management_quest_materializer_regression.cs
```

Then run the narrowest battle/runtime regression for the affected behavior. Do not run battle simulation or balance runners unless the user explicitly asks.
