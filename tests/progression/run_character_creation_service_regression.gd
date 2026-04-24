extends SceneTree

const AttributeService = preload("res://scripts/systems/attribute_service.gd")
const CharacterCreationService = preload("res://scripts/systems/character_creation_service.gd")
const UnitBaseAttributes = preload("res://scripts/player/progression/unit_base_attributes.gd")
const UnitProgress = preload("res://scripts/player/progression/unit_progress.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_reroll_mapping_covers_all_band_boundaries()
	_test_overflow_inputs_fall_back_to_minus_six()
	_test_bake_hidden_luck_uses_character_creation_write_path()

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


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
