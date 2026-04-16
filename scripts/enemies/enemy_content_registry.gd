class_name EnemyContentRegistry
extends RefCounted

const ENEMY_AI_BRAIN_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_state_def.gd")
const ENEMY_TEMPLATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_template_def.gd")
const WILD_ENCOUNTER_ROSTER_DEF_SCRIPT = preload("res://scripts/enemies/wild_encounter_roster_def.gd")
const MOVE_TO_RANGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/move_to_range_action.gd")
const RETREAT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/retreat_action.gd")
const USE_CHARGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_charge_action.gd")
const USE_GROUND_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_ground_skill_action.gd")
const USE_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_unit_skill_action.gd")
const WAIT_ACTION_SCRIPT = preload("res://scripts/enemies/actions/wait_action.gd")
const EnemyAiBrainDef = preload("res://scripts/enemies/enemy_ai_brain_def.gd")
const EnemyAiStateDef = preload("res://scripts/enemies/enemy_ai_state_def.gd")
const EnemyTemplateDef = preload("res://scripts/enemies/enemy_template_def.gd")
const WildEncounterRosterDef = preload("res://scripts/enemies/wild_encounter_roster_def.gd")

var _enemy_templates: Dictionary = {}
var _enemy_ai_brains: Dictionary = {}
var _wild_encounter_rosters: Dictionary = {}


func _init() -> void:
	rebuild()


func rebuild() -> void:
	_enemy_templates.clear()
	_enemy_ai_brains.clear()
	_wild_encounter_rosters.clear()
	_register_brain(_build_melee_aggressor_brain())
	_register_brain(_build_frontline_bulwark_brain())
	_register_brain(_build_ranged_controller_brain())
	_register_brain(_build_ranged_suppressor_brain())
	_register_brain(_build_healer_controller_brain())
	_register_template(_build_wolf_pack_template())
	_register_template(_build_wolf_raider_template())
	_register_template(_build_wolf_alpha_template())
	_register_template(_build_wolf_vanguard_template())
	_register_template(_build_wolf_shaman_template())
	_register_template(_build_mist_beast_template())
	_register_template(_build_mist_harrier_template())
	_register_template(_build_mist_weaver_template())
	_register_wild_encounter_roster(_build_wolf_den_roster())


func get_enemy_templates() -> Dictionary:
	return _enemy_templates


func get_enemy_ai_brains() -> Dictionary:
	return _enemy_ai_brains


func get_wild_encounter_rosters() -> Dictionary:
	return _wild_encounter_rosters


func _register_brain(brain: EnemyAiBrainDef) -> void:
	if brain == null or brain.brain_id == &"":
		return
	_enemy_ai_brains[brain.brain_id] = brain


func _register_template(template: EnemyTemplateDef) -> void:
	if template == null or template.template_id == &"":
		return
	_enemy_templates[template.template_id] = template


func _register_wild_encounter_roster(roster: WildEncounterRosterDef) -> void:
	if roster == null or roster.profile_id == &"":
		return
	_wild_encounter_rosters[roster.profile_id] = roster


func _build_melee_aggressor_brain() -> EnemyAiBrainDef:
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"melee_aggressor"
	brain.default_state_id = &"engage"
	brain.retreat_hp_ratio = 0.35
	brain.support_hp_ratio = 0.4
	brain.pressure_distance = 1
	brain.states = {
		&"engage": _build_state(&"engage", [
			_build_charge_action(&"wolf_charge_open", &"charge"),
			_build_move_to_range_action(&"wolf_close_in", &"nearest_enemy", 1, 1),
			_build_wait_action(&"wolf_wait"),
		]),
		&"pressure": _build_state(&"pressure", [
			_build_unit_skill_action(&"wolf_basic_attack", [&"warrior_heavy_strike"], &"nearest_enemy"),
			_build_move_to_range_action(&"wolf_keep_contact", &"nearest_enemy", 1, 1),
			_build_wait_action(&"wolf_wait"),
		]),
		&"support": _build_state(&"support", [
			_build_unit_skill_action(&"wolf_basic_attack", [&"warrior_heavy_strike"], &"nearest_enemy"),
			_build_wait_action(&"wolf_wait"),
		]),
		&"retreat": _build_state(&"retreat", [
			_build_retreat_action(&"wolf_retreat", 2),
			_build_wait_action(&"wolf_wait"),
		]),
	}
	return brain


func _build_ranged_controller_brain() -> EnemyAiBrainDef:
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"ranged_controller"
	brain.default_state_id = &"pressure"
	brain.retreat_hp_ratio = 0.4
	brain.support_hp_ratio = 0.6
	brain.pressure_distance = 4
	brain.states = {
		&"engage": _build_state(&"engage", [
			_build_ground_skill_action(&"mist_aoe", [&"mage_fireball"], 2),
			_build_unit_skill_action(&"mist_ranged_single", [&"mage_ice_lance"], &"lowest_hp_enemy"),
			_build_move_to_range_action(&"mist_keep_range", &"nearest_enemy", 3, 4),
			_build_wait_action(&"mist_wait"),
		]),
		&"pressure": _build_state(&"pressure", [
			_build_ground_skill_action(&"mist_aoe", [&"mage_fireball"], 2),
			_build_unit_skill_action(&"mist_ranged_single", [&"mage_ice_lance"], &"lowest_hp_enemy"),
			_build_move_to_range_action(&"mist_keep_range", &"nearest_enemy", 3, 4),
			_build_wait_action(&"mist_wait"),
		]),
		&"support": _build_state(&"support", [
			_build_unit_skill_action(&"mist_support", [&"mage_temporal_rewind"], &"lowest_hp_ally"),
			_build_ground_skill_action(&"mist_aoe", [&"mage_fireball"], 1),
			_build_unit_skill_action(&"mist_ranged_single", [&"mage_ice_lance"], &"lowest_hp_enemy"),
			_build_wait_action(&"mist_wait"),
		]),
		&"retreat": _build_state(&"retreat", [
			_build_retreat_action(&"mist_retreat", 4),
			_build_unit_skill_action(&"mist_support", [&"mage_temporal_rewind"], &"lowest_hp_ally"),
			_build_wait_action(&"mist_wait"),
		]),
	}
	return brain


func _build_ranged_suppressor_brain() -> EnemyAiBrainDef:
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"ranged_suppressor"
	brain.default_state_id = &"pressure"
	brain.retreat_hp_ratio = 0.3
	brain.pressure_distance = 4
	brain.states = {
		&"engage": _build_state(&"engage", [
			_build_ground_skill_action(&"harrier_suppress_lane", [&"archer_suppressive_fire"], 2),
			_build_unit_skill_action(&"harrier_pin_target", [&"archer_pinning_shot"], &"lowest_hp_enemy"),
			_build_move_to_range_action(&"harrier_take_cover", &"nearest_enemy", 4, 5),
			_build_wait_action(&"harrier_wait"),
		]),
		&"pressure": _build_state(&"pressure", [
			_build_ground_skill_action(&"harrier_suppress_lane", [&"archer_suppressive_fire"], 2),
			_build_unit_skill_action(&"harrier_pin_target", [&"archer_pinning_shot"], &"lowest_hp_enemy"),
			_build_move_to_range_action(&"harrier_keep_range", &"nearest_enemy", 4, 5),
			_build_wait_action(&"harrier_wait"),
		]),
		&"retreat": _build_state(&"retreat", [
			_build_unit_skill_action(&"harrier_cover_retreat", [&"archer_pinning_shot"], &"nearest_enemy"),
			_build_retreat_action(&"harrier_retreat", 5),
			_build_wait_action(&"harrier_wait"),
		]),
	}
	return brain


func _build_healer_controller_brain() -> EnemyAiBrainDef:
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"healer_controller"
	brain.default_state_id = &"pressure"
	brain.retreat_hp_ratio = 0.35
	brain.support_hp_ratio = 0.7
	brain.pressure_distance = 4
	brain.states = {
		&"engage": _build_state(&"engage", [
			_build_unit_skill_action(&"weaver_lock_target", [&"mage_glacial_prison"], &"lowest_hp_enemy"),
			_build_unit_skill_action(&"weaver_pressure_bolt", [&"mage_ice_lance"], &"lowest_hp_enemy"),
			_build_move_to_range_action(&"weaver_take_position", &"nearest_enemy", 3, 4),
			_build_wait_action(&"weaver_wait"),
		]),
		&"pressure": _build_state(&"pressure", [
			_build_unit_skill_action(&"weaver_lock_target", [&"mage_glacial_prison"], &"lowest_hp_enemy"),
			_build_unit_skill_action(&"weaver_pressure_bolt", [&"mage_ice_lance"], &"lowest_hp_enemy"),
			_build_move_to_range_action(&"weaver_keep_range", &"nearest_enemy", 3, 4),
			_build_wait_action(&"weaver_wait"),
		]),
		&"support": _build_state(&"support", [
			_build_unit_skill_action(&"weaver_rewind_ally", [&"mage_temporal_rewind"], &"lowest_hp_ally"),
			_build_unit_skill_action(&"weaver_cover_bind", [&"mage_glacial_prison"], &"lowest_hp_enemy"),
			_build_wait_action(&"weaver_wait"),
		]),
		&"retreat": _build_state(&"retreat", [
			_build_unit_skill_action(&"weaver_rewind_retreat", [&"mage_temporal_rewind"], &"lowest_hp_ally"),
			_build_retreat_action(&"weaver_retreat", 4),
			_build_wait_action(&"weaver_wait"),
		]),
	}
	return brain


func _build_frontline_bulwark_brain() -> EnemyAiBrainDef:
	var brain = ENEMY_AI_BRAIN_DEF_SCRIPT.new()
	brain.brain_id = &"frontline_bulwark"
	brain.default_state_id = &"engage"
	brain.retreat_hp_ratio = 0.25
	brain.support_hp_ratio = 0.55
	brain.pressure_distance = 1
	brain.states = {
		&"engage": _build_state(&"engage", [
			_build_charge_action(&"vanguard_charge_open", &"charge"),
			_build_unit_skill_action(&"vanguard_taunt_open", [&"warrior_taunt"], &"nearest_enemy"),
			_build_move_to_range_action(&"vanguard_close_in", &"nearest_enemy", 1, 1),
			_build_wait_action(&"vanguard_wait"),
		]),
		&"pressure": _build_state(&"pressure", [
			_build_unit_skill_action(&"vanguard_shield_bash", [&"warrior_shield_bash"], &"nearest_enemy"),
			_build_unit_skill_action(&"vanguard_taunt_pressure", [&"warrior_taunt"], &"nearest_enemy"),
			_build_move_to_range_action(&"vanguard_hold_line", &"nearest_enemy", 1, 1),
			_build_wait_action(&"vanguard_wait"),
		]),
		&"support": _build_state(&"support", [
			_build_unit_skill_action(&"vanguard_guard_self", [&"warrior_guard"], &"self"),
			_build_wait_action(&"vanguard_wait"),
		]),
		&"retreat": _build_state(&"retreat", [
			_build_unit_skill_action(&"vanguard_guard_retreat", [&"warrior_guard"], &"self"),
			_build_retreat_action(&"vanguard_retreat", 2),
			_build_wait_action(&"vanguard_wait"),
		]),
	}
	return brain


func _build_wolf_pack_template() -> EnemyTemplateDef:
	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = &"wolf_pack"
	template.display_name = "荒狼群"
	template.brain_id = &"melee_aggressor"
	template.initial_state_id = &"engage"
	template.enemy_count = 2
	template.skill_ids = ProgressionDataUtils.to_string_name_array(["charge", "warrior_heavy_strike"])
	template.attribute_overrides = {
		"hp_max": 28,
		"mp_max": 0,
		"stamina_max": 0,
		"action_points": 2,
		"physical_attack": 9,
		"physical_defense": 3,
		"magic_attack": 0,
		"magic_defense": 1,
		"speed": 10,
	}
	return template


func _build_wolf_raider_template() -> EnemyTemplateDef:
	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = &"wolf_raider"
	template.display_name = "荒狼"
	template.brain_id = &"melee_aggressor"
	template.initial_state_id = &"engage"
	template.enemy_count = 1
	template.skill_ids = ProgressionDataUtils.to_string_name_array(["charge", "warrior_heavy_strike"])
	template.attribute_overrides = {
		"hp_max": 26,
		"mp_max": 0,
		"stamina_max": 0,
		"action_points": 2,
		"physical_attack": 8,
		"physical_defense": 3,
		"magic_attack": 0,
		"magic_defense": 1,
		"speed": 10,
	}
	return template


func _build_wolf_alpha_template() -> EnemyTemplateDef:
	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = &"wolf_alpha"
	template.display_name = "荒狼头目"
	template.brain_id = &"melee_aggressor"
	template.initial_state_id = &"engage"
	template.enemy_count = 1
	template.skill_ids = ProgressionDataUtils.to_string_name_array(["charge", "warrior_guard_break"])
	template.attribute_overrides = {
		"hp_max": 34,
		"mp_max": 0,
		"stamina_max": 0,
		"action_points": 2,
		"physical_attack": 12,
		"physical_defense": 5,
		"magic_attack": 0,
		"magic_defense": 2,
		"speed": 11,
	}
	return template


func _build_wolf_vanguard_template() -> EnemyTemplateDef:
	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = &"wolf_vanguard"
	template.display_name = "荒狼先锋"
	template.brain_id = &"frontline_bulwark"
	template.initial_state_id = &"engage"
	template.enemy_count = 1
	template.skill_ids = ProgressionDataUtils.to_string_name_array([
		"charge",
		"warrior_shield_bash",
		"warrior_taunt",
		"warrior_guard",
	])
	template.attribute_overrides = {
		"hp_max": 42,
		"mp_max": 0,
		"stamina_max": 0,
		"action_points": 2,
		"physical_attack": 10,
		"physical_defense": 6,
		"magic_attack": 0,
		"magic_defense": 3,
		"speed": 8,
	}
	return template


func _build_wolf_shaman_template() -> EnemyTemplateDef:
	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = &"wolf_shaman"
	template.display_name = "荒狼祭司"
	template.brain_id = &"ranged_controller"
	template.initial_state_id = &"support"
	template.enemy_count = 1
	template.skill_ids = ProgressionDataUtils.to_string_name_array(["mage_ice_lance", "mage_temporal_rewind"])
	template.attribute_overrides = {
		"hp_max": 22,
		"mp_max": 12,
		"stamina_max": 0,
		"action_points": 2,
		"physical_attack": 4,
		"physical_defense": 2,
		"magic_attack": 11,
		"magic_defense": 5,
		"speed": 9,
	}
	return template


func _build_mist_beast_template() -> EnemyTemplateDef:
	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = &"mist_beast"
	template.display_name = "雾沼异兽"
	template.brain_id = &"ranged_controller"
	template.initial_state_id = &"pressure"
	template.enemy_count = 2
	template.skill_ids = ProgressionDataUtils.to_string_name_array(["mage_fireball", "mage_ice_lance", "mage_temporal_rewind"])
	template.attribute_overrides = {
		"hp_max": 24,
		"mp_max": 8,
		"stamina_max": 0,
		"action_points": 2,
		"physical_attack": 4,
		"physical_defense": 2,
		"magic_attack": 12,
		"magic_defense": 5,
		"fire_resistance": 2,
		"speed": 9,
	}
	return template


func _build_mist_harrier_template() -> EnemyTemplateDef:
	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = &"mist_harrier"
	template.display_name = "雾沼猎压者"
	template.brain_id = &"ranged_suppressor"
	template.initial_state_id = &"pressure"
	template.enemy_count = 1
	template.skill_ids = ProgressionDataUtils.to_string_name_array([
		"archer_suppressive_fire",
		"archer_pinning_shot",
	])
	template.attribute_overrides = {
		"hp_max": 26,
		"mp_max": 0,
		"stamina_max": 4,
		"action_points": 2,
		"physical_attack": 9,
		"physical_defense": 3,
		"magic_attack": 0,
		"magic_defense": 2,
		"speed": 11,
	}
	return template


func _build_mist_weaver_template() -> EnemyTemplateDef:
	var template = ENEMY_TEMPLATE_DEF_SCRIPT.new()
	template.template_id = &"mist_weaver"
	template.display_name = "雾沼织咒者"
	template.brain_id = &"healer_controller"
	template.initial_state_id = &"pressure"
	template.enemy_count = 1
	template.skill_ids = ProgressionDataUtils.to_string_name_array([
		"mage_temporal_rewind",
		"mage_glacial_prison",
		"mage_ice_lance",
	])
	template.attribute_overrides = {
		"hp_max": 24,
		"mp_max": 14,
		"stamina_max": 0,
		"action_points": 2,
		"physical_attack": 3,
		"physical_defense": 2,
		"magic_attack": 12,
		"magic_defense": 6,
		"speed": 9,
	}
	return template


func _build_wolf_den_roster() -> WildEncounterRosterDef:
	var roster = WILD_ENCOUNTER_ROSTER_DEF_SCRIPT.new()
	roster.profile_id = &"wolf_den"
	roster.display_name = "荒狼巢穴"
	roster.initial_stage = 0
	roster.growth_step_interval = 2
	roster.suppression_steps_on_victory = 3
	roster.stages.clear()
	roster.stages.append({
		"stage": 0,
		"unit_entries": [
			{"template_id": &"wolf_raider", "count": 2},
		],
	})
	roster.stages.append({
		"stage": 1,
		"unit_entries": [
			{"template_id": &"wolf_raider", "count": 3},
		],
	})
	roster.stages.append({
		"stage": 2,
		"unit_entries": [
			{"template_id": &"wolf_raider", "count": 4},
		],
	})
	roster.stages.append({
		"stage": 3,
		"unit_entries": [
			{"template_id": &"wolf_raider", "count": 4},
			{"template_id": &"wolf_alpha", "count": 1},
		],
	})
	roster.stages.append({
		"stage": 4,
		"unit_entries": [
			{"template_id": &"wolf_raider", "count": 4},
			{"template_id": &"wolf_alpha", "count": 1},
			{"template_id": &"wolf_shaman", "count": 1},
		],
	})
	return roster


func _build_state(state_id: StringName, actions: Array) -> EnemyAiStateDef:
	var state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	state.state_id = state_id
	state.actions = actions
	return state


func _build_unit_skill_action(action_id: StringName, skill_ids: Array[StringName], target_selector: StringName):
	var action = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	action.action_id = action_id
	action.skill_ids = skill_ids.duplicate()
	action.target_selector = target_selector
	return action


func _build_ground_skill_action(action_id: StringName, skill_ids: Array[StringName], minimum_hit_count: int):
	var action = USE_GROUND_SKILL_ACTION_SCRIPT.new()
	action.action_id = action_id
	action.skill_ids = skill_ids.duplicate()
	action.minimum_hit_count = minimum_hit_count
	return action


func _build_charge_action(action_id: StringName, skill_id: StringName):
	var action = USE_CHARGE_ACTION_SCRIPT.new()
	action.action_id = action_id
	action.skill_id = skill_id
	return action


func _build_move_to_range_action(action_id: StringName, target_selector: StringName, desired_min_distance: int, desired_max_distance: int):
	var action = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	action.action_id = action_id
	action.target_selector = target_selector
	action.desired_min_distance = desired_min_distance
	action.desired_max_distance = desired_max_distance
	return action


func _build_retreat_action(action_id: StringName, minimum_safe_distance: int):
	var action = RETREAT_ACTION_SCRIPT.new()
	action.action_id = action_id
	action.minimum_safe_distance = minimum_safe_distance
	return action


func _build_wait_action(action_id: StringName):
	var action = WAIT_ACTION_SCRIPT.new()
	action.action_id = action_id
	return action
