# Skill Config Schema Reference

## File Location

Create under `data/configs/skills/<skill_id>.tres`.

## Constraint Policy

- Treat `.tres` `StringName` fields as the resource/serialization boundary, not the formal runtime type.
- Closed domains such as effect kinds, target modes, damage tags/categories, mastery modes, forced-move modes, save tags, and resource kinds should be owned by C# enums or typed rule utilities.
- Multi-field constraints should be decoded into typed DTOs/value objects before runtime logic consumes them.
- Do not add new public whitelist sets, repeated string comparisons, or dictionary-shaped runtime contracts when an enum-backed converter or strong type can express the rule.

## SkillDef Fields

```text
skill_id: StringName
display_name: String
description: String
icon_id: StringName
max_level: int
non_core_max_level: int
mastery_curve: PackedInt32Array  # size must equal max_level
growth_tier: StringName  # basic(60) / intermediate(120) / advanced(180) / ultimate(240)
attribute_growth_progress: Dictionary  # { "strength": 40, "constitution": 20 }
attribute_requirements: Dictionary  # { "willpower": 12 }
attribute_modifiers: Array[AttributeModifier]
tags: Array[StringName]
learn_source: StringName  # innate / book / profession
unlock_mode: StringName  # composite_upgrade (optional)
upgrade_source_skill_ids: Array[StringName]  # for composite_upgrade
knowledge_requirements: Array[StringName]
skill_level_requirements: Dictionary  # { "source_skill_id": 5 }
achievement_requirements: Array[StringName]
level_descriptions: Dictionary  # { "1": "desc at lv1" }
level_description_template: String  # template with {vars} and {{?key}}...{{/key}} blocks
level_description_configs: Dictionary  # { "1": { "var": "value" } }
combat_profile: CombatSkillDef
```

## CombatSkillDef Fields

```text
skill_id: StringName
target_mode: StringName  # unit / ground
target_team_filter: StringName  # enemy / ally
target_selection_mode: StringName  # single_unit / self
range_value: int
ap_cost: int
stamina_cost: int
mp_cost: int
aura_cost: int
cooldown_tu: int
attack_roll_bonus: int
level_overrides: Dictionary  # { "2": { "stamina_cost": 20 } }
mastery_trigger_mode: StringName  # skill_damage_dice_max / weapon_attack_quality / damage_dealt / status_applied / effect_applied / incoming_physical_hit
mastery_amount_mode: StringName  # per_target_rank / per_cast_hp_ratio
effect_defs: Array[CombatEffectDef]
required_weapon_families: Array[StringName]
```

## CombatEffectDef Fields

```text
effect_type: StringName  # damage / status / heal / shield / stamina_restore / charge / forced_move / repeat_attack_until_fail / terrain / terrain_effect / terrain_replace / terrain_replace_to / height / height_delta / apply_status
tick_effect_type: StringName
power: int
min_skill_level: int  # 0 = available immediately
max_skill_level: int  # -1 = no upper cap
damage_tag: StringName
dice_count: int
dice_sides: int
add_weapon_dice: bool
requires_weapon: bool
use_weapon_physical_damage_tag: bool
resolve_as_weapon_attack: bool
status_id: StringName
duration_tu: int
params: Dictionary  # effect-specific parameters
forced_move_mode: StringName  # push / pull / swap / teleport
forced_move_distance: int
```

## Common Params by Effect Type

### damage
```text
add_weapon_dice = true
requires_weapon = true
use_weapon_physical_damage_tag = true
resolve_as_weapon_attack = true
dice_count = 1  # optional skill damage dice count
dice_sides = 6  # optional skill damage dice sides
```

### repeat_attack_until_fail
```text
params = {
    "same_target_only": true,
    "cost_resource": "stamina",  # stamina / ap / mp / aura
    "follow_up_fixed_cost": 5,   # fixed additional cost per follow-up stage
    "follow_up_cost_addition": 5,  # linear increment (alternative to fixed)
    "follow_up_cost_multiplier": 1.0,  # exponential cost (default)
    "follow_up_damage_multiplier": 1.0,
    "follow_up_attack_penalty": 1,
    "exponential_penalty": true,  # penalty = 2^stage_index * penalty_value
    "penalty_free_stages_by_level": {  # stage_index < N gets zero penalty
        "1": 1,
        "3": 2,
        "5": 3,
    },
    "base_attack_bonus": 0,
    "stop_on_miss": true,
    "stop_on_target_down": true,
    "stop_on_insufficient_resource": true,
}
```

### status / apply_status
```text
params = {
    "trigger_event": "critical_hit",  # only apply on crit
}
```

### stamina_restore
```text
dice_count = 1  # optional dice count
dice_sides = 6  # optional dice sides
```

### forced_move
```text
# Uses top-level fields, not params:
forced_move_mode = &"push"   # push / pull / swap / teleport
forced_move_distance = 2
```

### charge
```text
params = {
    "skill_id": &"charge",
    "base_distance": 3,
    "distance_by_level": {
        "1": 4,
        "3": 5,
        "5": 6
    },
    "collision_base_damage": 10,
    "collision_size_gap_damage": 10,
    "trap_immunity_level": 7
}
```

## Full Example: warrior_combo_strike.tres

```text
[gd_resource type="Resource" script_class="SkillDef" format=3]

[ext_resource type="Script" path="res://scripts/player/progression/CombatEffectDef.cs" id="1_effect"]
[ext_resource type="Script" path="res://scripts/player/progression/CombatSkillDef.cs" id="2_combat"]
[ext_resource type="Script" path="res://scripts/player/progression/SkillDef.cs" id="3_skill"]

[sub_resource type="Resource" id="damage"]
script = ExtResource("1_effect")
effect_type = &"damage"
power = 0
damage_tag = &"physical_slash"
add_weapon_dice = true
requires_weapon = true
use_weapon_physical_damage_tag = true
resolve_as_weapon_attack = true

[sub_resource type="Resource" id="repeat"]
script = ExtResource("1_effect")
effect_type = &"repeat_attack_until_fail"
params = {
"same_target_only": true,
"cost_resource": "stamina",
"follow_up_fixed_cost": 5,
"follow_up_damage_multiplier": 1,
"follow_up_attack_penalty": 1,
"exponential_penalty": true,
"penalty_free_stages_by_level": {
"1": 1,
"3": 2,
"5": 3
},
"stop_on_miss": true,
"stop_on_target_down": true,
"stop_on_insufficient_resource": true
}

[sub_resource type="Resource" id="combat"]
script = ExtResource("2_combat")
skill_id = &"warrior_combo_strike"
ap_cost = 1
stamina_cost = 30
cooldown_tu = 5
level_overrides = {
"2": {
"stamina_cost": 25
},
"4": {
"stamina_cost": 20
}
}
mastery_trigger_mode = &"weapon_attack_quality"
mastery_amount_mode = &"per_target_rank"
effect_defs = Array[ExtResource("1_effect")]([SubResource("damage"), SubResource("repeat")])

[resource]
script = ExtResource("3_skill")
skill_id = &"warrior_combo_strike"
display_name = "连击"
description = "..."
max_level = 5
non_core_max_level = 3
mastery_curve = PackedInt32Array(200, 500, 1100, 2000, 3200)
growth_tier = &"basic"
attribute_growth_progress = {
"agility": 40,
"strength": 20
}
tags = Array[StringName]([&"warrior", &"melee", &"combo"])
combat_profile = SubResource("combat")
```

## Validation Rules

1. `mastery_curve.size() == max_level`
2. `non_core_max_level <= max_level`
3. `level_overrides` keys must be numeric strings (e.g. `"2"` not `"level2"`)
4. `attribute_growth_progress` values must sum to `get_tier_budget(growth_tier)`
5. `attribute_growth_progress` keys must be in `UnitBaseAttributes.BASE_ATTRIBUTE_IDS`
6. `effect_defs` with overlapping `min/max_skill_level` ranges are allowed but should be reviewed
7. `combat_profile.mastery_trigger_mode` must be in `VALID_MASTERY_TRIGGER_MODES`
8. New closed value domains should have enum/typed conversion and invalid-value schema coverage
