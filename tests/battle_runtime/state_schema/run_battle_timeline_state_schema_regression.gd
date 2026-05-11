extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_valid_to_dict_roundtrip()
	_test_non_dictionary_returns_null()
	_test_missing_field_returns_null()
	_test_extra_field_returns_null()
	_test_wrong_types_return_null()
	_test_string_numbers_return_null()
	_test_empty_ready_id_returns_null()
	_test_non_array_ready_unit_ids_returns_null()
	_test_numeric_boundaries_return_null()

	if _failures.is_empty():
		print("Battle timeline state schema regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle timeline state schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_valid_to_dict_roundtrip() -> void:
	var state := BattleTimelineState.new()
	state.current_tu = 15
	state.tu_per_tick = 5
	state.frozen = true
	state.ready_unit_ids = [&"hero", &"enemy"]

	var restored = BattleTimelineState.from_dict(state.to_dict())
	_assert_true(restored != null, "合法 to_dict payload 应能恢复。")
	if restored == null:
		return
	_assert_eq(restored.current_tu, 15, "roundtrip 应保留 current_tu。")
	_assert_eq(restored.tu_per_tick, 5, "roundtrip 应保留 tu_per_tick。")
	_assert_true(restored.frozen, "roundtrip 应保留 frozen。")
	_assert_eq(restored.ready_unit_ids, [&"hero", &"enemy"], "roundtrip 应保留 ready_unit_ids。")


func _test_non_dictionary_returns_null() -> void:
	_assert_null(BattleTimelineState.from_dict([]), "非 Dictionary 入参应返回 null。")
	_assert_null(BattleTimelineState.from_dict("bad"), "String 入参应返回 null。")


func _test_missing_field_returns_null() -> void:
	var payload := _valid_payload()
	payload.erase("current_tu")
	_assert_null(BattleTimelineState.from_dict(payload), "缺少 current_tu 应返回 null。")


func _test_extra_field_returns_null() -> void:
	var payload := _valid_payload()
	payload["speed"] = 5
	_assert_null(BattleTimelineState.from_dict(payload), "额外旧字段应返回 null。")


func _test_wrong_types_return_null() -> void:
	_assert_null(BattleTimelineState.from_dict(_payload_with("current_tu", 1.0)), "current_tu 必须是 int。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tu_per_tick", 5.0)), "tu_per_tick 必须是 int。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("frozen", 1)), "frozen 必须是 bool。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("ready_unit_ids", [7])), "ready_unit_ids entry 只能是 String/StringName。")


func _test_string_numbers_return_null() -> void:
	_assert_null(BattleTimelineState.from_dict(_payload_with("current_tu", "1")), "current_tu 不接受字符串数字。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tu_per_tick", "5")), "tu_per_tick 不接受字符串数字。")


func _test_empty_ready_id_returns_null() -> void:
	_assert_null(BattleTimelineState.from_dict(_payload_with("ready_unit_ids", [""])), "空 String ready id 应返回 null。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("ready_unit_ids", [&""])), "空 StringName ready id 应返回 null。")


func _test_non_array_ready_unit_ids_returns_null() -> void:
	_assert_null(BattleTimelineState.from_dict(_payload_with("ready_unit_ids", "hero")), "ready_unit_ids 非 Array 应返回 null。")


func _test_numeric_boundaries_return_null() -> void:
	_assert_null(BattleTimelineState.from_dict(_payload_with("current_tu", -1)), "current_tu 不能为负数。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tu_per_tick", 0)), "tu_per_tick 必须为正数。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tu_per_tick", -5)), "tu_per_tick 不能为负数。")


func _valid_payload() -> Dictionary:
	return {
		"current_tu": 10,
		"tu_per_tick": 5,
		"frozen": false,
		"ready_unit_ids": ["hero", &"enemy"],
	}


func _payload_with(field_name: String, value: Variant) -> Dictionary:
	var payload := _valid_payload()
	payload[field_name] = value
	return payload


func _assert_null(value: Variant, message: String) -> void:
	if value != null:
		_test.fail("%s | actual=%s" % [message, str(value)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_test.fail(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_test.fail("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
