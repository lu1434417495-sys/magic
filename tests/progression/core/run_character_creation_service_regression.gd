extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const AttributeService = preload("res://scripts/systems/attributes/attribute_service.gd")
const CharacterCreationService = preload("res://scripts/systems/progression/character_creation_service.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_reroll_mapping_covers_all_band_boundaries()
	_test_overflow_inputs_fall_back_to_minus_six()
	_test_initial_hp_max_uses_level_zero_formula()
	_test_bake_hidden_luck_uses_character_creation_write_path()
	_test_creation_payload_does_not_bake_reroll_luck_by_default()
	_test_creation_payload_can_opt_into_reroll_luck_for_main_character()

	if _failures.is_empty():
		print("CharacterCreationService regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("CharacterCreationService regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_reroll_mapping_covers_all_band_boundaries() -> void:
	var cases := [
		{"label": "0 次 reroll", "reroll_count": 0, "expected_hidden_luck": 2},
		{"label": "1 次 reroll", "reroll_count": 1, "expected_hidden_luck": 1},
		{"label": "9 次 reroll", "reroll_count": 9, "expected_hidden_luck": 1},
		{"label": "10 次 reroll", "reroll_count": 10, "expected_hidden_luck": 0},
		{"label": "99 次 reroll", "reroll_count": 99, "expected_hidden_luck": 0},
		{"label": "100 次 reroll", "reroll_count": 100, "expected_hidden_luck": -1},
		{"label": "999 次 reroll", "reroll_count": 999, "expected_hidden_luck": -1},
		{"label": "1,000 次 reroll", "reroll_count": 1000, "expected_hidden_luck": -2},
		{"label": "9,999 次 reroll", "reroll_count": 9999, "expected_hidden_luck": -2},
		{"label": "10,000 次 reroll", "reroll_count": 10000, "expected_hidden_luck": -3},
		{"label": "99,999 次 reroll", "reroll_count": 99999, "expected_hidden_luck": -3},
		{"label": "100,000 次 reroll", "reroll_count": 100000, "expected_hidden_luck": -4},
		{"label": "999,999 次 reroll", "reroll_count": 999999, "expected_hidden_luck": -4},
		{"label": "1,000,000 次 reroll", "reroll_count": 1000000, "expected_hidden_luck": -5},
		{"label": "9,999,999 次 reroll", "reroll_count": 9999999, "expected_hidden_luck": -5},
		{"label": "10,000,000 次 reroll", "reroll_count": 10000000, "expected_hidden_luck": -6},
		{"label": "10,000,001 次 reroll", "reroll_count": 10000001, "expected_hidden_luck": -6},
	]

	for case in cases:
		var actual_hidden_luck := CharacterCreationService.map_reroll_count_to_hidden_luck_at_birth(case.get("reroll_count"))
		_assert_eq(
			actual_hidden_luck,
			int(case.get("expected_hidden_luck", 0)),
			"%s 映射结果错误。" % String(case.get("label", "未知 case"))
		)


func _test_overflow_inputs_fall_back_to_minus_six() -> void:
	var cases := [
		{"label": "超大 float", "reroll_count": 1.0e30},
		{"label": "超大 decimal string", "reroll_count": "1000000000000000000000000000000"},
		{"label": "超大 StringName", "reroll_count": &"1000000000000000000000000000000"},
	]

	for case in cases:
		var actual_hidden_luck := CharacterCreationService.map_reroll_count_to_hidden_luck_at_birth(case.get("reroll_count"))
		_assert_eq(actual_hidden_luck, -6, "%s 应回退到 -6。" % String(case.get("label", "未知 case")))


func _test_initial_hp_max_uses_level_zero_formula() -> void:
	_assert_eq(CharacterCreationService.calculate_initial_hp_max(10), 14, "10 体质的 0 级初始生命应为 14。")
	_assert_eq(CharacterCreationService.calculate_initial_hp_max(14), 18, "14 体质的 0 级初始生命应为 14 + 2*2。")
	_assert_eq(CharacterCreationService.calculate_initial_hp_max(8), 12, "8 体质的 0 级初始生命应为 14 - 1*2。")


func _test_bake_hidden_luck_uses_character_creation_write_path() -> void:
	var progression := UnitProgress.new()
	progression.unit_id = &"hero"
	progression.display_name = "Hero"
	progression.unit_base_attributes.set_attribute_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH, 1)

	var attribute_service := AttributeService.new()
	attribute_service.setup(progression)

	var creation_service := CharacterCreationService.new()
	var baked := creation_service.bake_hidden_luck_at_birth(attribute_service, 10000)

	_assert_true(baked, "CharacterCreationService 应能通过 character_creation 来源写入 hidden_luck_at_birth。")
	_assert_eq(
		attribute_service.get_base_value(UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH),
		-3,
		"CharacterCreationService 应把 reroll=10000 烘焙为 -3。"
	)


func _test_creation_payload_does_not_bake_reroll_luck_by_default() -> void:
	var payload := _build_creation_payload(0)
	var member_state = CharacterCreationService.create_member_from_character_creation_payload(&"companion", payload)

	_assert_eq(
		member_state.get_hidden_luck_at_birth(),
		0,
		"非主角通过正式建卡 payload 创建时，即使 payload 带 reroll_count，也应默认 hidden_luck_at_birth=0。"
	)


func _test_creation_payload_can_opt_into_reroll_luck_for_main_character() -> void:
	var payload := _build_creation_payload(0)
	var member_state = CharacterCreationService.create_member_from_character_creation_payload(
		&"hero",
		payload,
		null,
		{CharacterCreationService.CREATION_OPTION_BAKE_REROLL_LUCK: true}
	)

	_assert_eq(
		member_state.get_hidden_luck_at_birth(),
		2,
		"主角建卡 opt-in 后应按 reroll_count=0 烘焙 hidden_luck_at_birth=+2。"
	)


func _build_creation_payload(reroll_count: int) -> Dictionary:
	return {
		"display_name": "Creation Test",
		"race_id": &"human",
		"subrace_id": &"common_human",
		"age_years": 24,
		"birth_at_world_step": 0,
		"age_profile_id": &"human_age_profile",
		"natural_age_stage_id": &"adult",
		"effective_age_stage_id": &"adult",
		"body_size_category": &"medium",
		"strength": 10,
		"agility": 10,
		"constitution": 10,
		"perception": 10,
		"intelligence": 10,
		"willpower": 10,
		"action_threshold": 30,
		"reroll_count": reroll_count,
	}


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
