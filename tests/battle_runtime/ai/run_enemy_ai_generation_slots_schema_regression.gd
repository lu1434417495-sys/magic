extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")
const ENEMY_AI_STATE_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_state_def.gd")
const ENEMY_AI_GENERATION_SLOT_DEF_SCRIPT = preload("res://scripts/enemies/enemy_ai_generation_slot_def.gd")
const USE_UNIT_SKILL_ACTION_SCRIPT = preload("res://scripts/enemies/actions/use_unit_skill_action.gd")
const MOVE_TO_RANGE_ACTION_SCRIPT = preload("res://scripts/enemies/actions/move_to_range_action.gd")

var _test := TestRunner.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_valid_generation_slots_pass_schema()
	_test_duplicate_slot_ids_and_orders_are_rejected()
	_test_invalid_family_and_template_are_rejected()
	_test_selector_distance_contracts_are_rejected()
	_test.finish(self, "Enemy AI generation slots schema regression")


func _test_valid_generation_slots_pass_schema() -> void:
	var state = _build_state()
	state.generation_slots = [
		_slot(&"offense", 10, [&"unit_hostile.damage"], [&"use_unit_skill"], &"template_attack"),
		_slot(&"close", 20, [&"random_chain"], [&"move_to_range"], &"template_move"),
	]
	var errors: Array[String] = state.validate_schema(&"schema_brain", _skill_defs())
	_test.assert_true(errors.is_empty(), "合法 generation slots 不应产生 schema error: %s" % str(errors))


func _test_duplicate_slot_ids_and_orders_are_rejected() -> void:
	var state = _build_state()
	state.generation_slots = [
		_slot(&"dup", 10, [&"unit_hostile.damage"], [&"use_unit_skill"], &"template_attack"),
		_slot(&"dup", 10, [&"ground_control"], [&"use_ground_skill"], &"template_attack"),
	]
	var errors: Array[String] = state.validate_schema(&"schema_brain", _skill_defs())
	_test.assert_true(_contains_error(errors, "duplicate generation slot_id dup"), "重复 slot_id 应被拒绝: %s" % str(errors))
	_test.assert_true(_contains_error(errors, "duplicate generation slot order 10"), "重复 slot order 应被拒绝: %s" % str(errors))


func _test_invalid_family_and_template_are_rejected() -> void:
	var state = _build_state()
	state.generation_slots = [
		_slot(&"bad_family", 10, [&"unit_hostile.damage"], [&"old_use_skill"], &"template_attack"),
		_slot(&"missing_template", 20, [&"unit_hostile.damage"], [&"use_unit_skill"], &"does_not_exist"),
	]
	var errors: Array[String] = state.validate_schema(&"schema_brain", _skill_defs())
	_test.assert_true(_contains_error(errors, "unsupported action_family old_use_skill"), "旧 alias/未知 family 不应被兼容: %s" % str(errors))
	_test.assert_true(_contains_error(errors, "style_template_action_id does_not_exist does not exist"), "缺失 template action 应被拒绝: %s" % str(errors))


func _test_selector_distance_contracts_are_rejected() -> void:
	var state = _build_state()
	var bad_selector = _slot(&"bad_selector", 10, [&"unit_hostile.damage"], [&"use_unit_skill"], &"template_attack")
	bad_selector.target_selector = &"legacy_selector"
	var bad_distance = _slot(&"bad_distance", 20, [&"random_chain"], [&"move_to_range"], &"template_move")
	bad_distance.desired_min_distance = 6
	bad_distance.desired_max_distance = 2
	state.generation_slots = [bad_selector, bad_distance]
	var errors: Array[String] = state.validate_schema(&"schema_brain", _skill_defs())
	_test.assert_true(_contains_error(errors, "unsupported target_selector legacy_selector"), "未知 selector 应被拒绝: %s" % str(errors))
	_test.assert_true(_contains_error(errors, "desired_min_distance cannot exceed desired_max_distance"), "距离契约 min > max 应被拒绝: %s" % str(errors))


func _build_state():
	var state = ENEMY_AI_STATE_DEF_SCRIPT.new()
	state.state_id = &"engage"
	var attack = USE_UNIT_SKILL_ACTION_SCRIPT.new()
	attack.action_id = &"template_attack"
	attack.skill_ids.append(&"dummy_skill")
	attack.desired_min_distance = 1
	attack.desired_max_distance = 4
	attack.distance_reference = USE_UNIT_SKILL_ACTION_SCRIPT.DISTANCE_REF_TARGET_UNIT
	var move = MOVE_TO_RANGE_ACTION_SCRIPT.new()
	move.action_id = &"template_move"
	state.actions = [attack, move]
	return state


func _slot(slot_id: StringName, order: int, affordances: Array, families: Array, template_action_id: StringName):
	var slot = ENEMY_AI_GENERATION_SLOT_DEF_SCRIPT.new()
	slot.slot_id = slot_id
	slot.order = order
	for affordance in affordances:
		slot.allowed_affordances.append(ProgressionDataUtils.to_string_name(affordance))
	for family in families:
		slot.action_families.append(ProgressionDataUtils.to_string_name(family))
	slot.style_template_action_id = template_action_id
	slot.target_selector = &"nearest_enemy"
	slot.score_bucket_id = &"default_offense"
	return slot


func _skill_defs() -> Dictionary:
	return {&"dummy_skill": true}


func _contains_error(errors: Array[String], expected_fragment: String) -> bool:
	for error in errors:
		if error.find(expected_fragment) >= 0:
			return true
	return false
