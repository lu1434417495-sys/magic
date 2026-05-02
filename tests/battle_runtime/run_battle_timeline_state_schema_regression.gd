extends SceneTree

const BattleTimelineState = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")

var _failures: Array[String] = []


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
	state.units_per_second = 10
	state.tick_interval_seconds = 0.5
	state.tu_per_tick = 5
	state.frozen = true
	state.ready_unit_ids = [&"hero", &"enemy"]
	state.delta_remainder = 0.25

	var restored = BattleTimelineState.from_dict(state.to_dict())
	_assert_true(restored != null, "合法 to_dict payload 应能恢复。")
	if restored == null:
		return
	_assert_eq(restored.current_tu, 15, "roundtrip 应保留 current_tu。")
	_assert_eq(restored.units_per_second, 10, "roundtrip 应保留 units_per_second。")
	_assert_eq(restored.tick_interval_seconds, 0.5, "roundtrip 应保留 tick_interval_seconds。")
	_assert_eq(restored.tu_per_tick, 5, "roundtrip 应保留 tu_per_tick。")
	_assert_true(restored.frozen, "roundtrip 应保留 frozen。")
	_assert_eq(restored.ready_unit_ids, [&"hero", &"enemy"], "roundtrip 应保留 ready_unit_ids。")
	_assert_eq(restored.delta_remainder, 0.25, "roundtrip 应保留 delta_remainder。")

	var integer_seconds_payload := state.to_dict()
	integer_seconds_payload["tick_interval_seconds"] = 1
	integer_seconds_payload["delta_remainder"] = 0
	var restored_integer_seconds = BattleTimelineState.from_dict(integer_seconds_payload)
	_assert_true(restored_integer_seconds != null, "int 形式的秒数与余量仍是当前合法 roundtrip 数字输入。")


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
	_assert_null(BattleTimelineState.from_dict(_payload_with("units_per_second", 5.0)), "units_per_second 必须是 int。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tu_per_tick", 5.0)), "tu_per_tick 必须是 int。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tick_interval_seconds", true)), "tick_interval_seconds 不接受 bool。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("delta_remainder", false)), "delta_remainder 不接受 bool。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("frozen", 1)), "frozen 必须是 bool。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("ready_unit_ids", [7])), "ready_unit_ids entry 只能是 String/StringName。")


func _test_string_numbers_return_null() -> void:
	_assert_null(BattleTimelineState.from_dict(_payload_with("current_tu", "1")), "current_tu 不接受字符串数字。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("units_per_second", "5")), "units_per_second 不接受字符串数字。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tu_per_tick", "5")), "tu_per_tick 不接受字符串数字。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tick_interval_seconds", "1.0")), "tick_interval_seconds 不接受字符串数字。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("delta_remainder", "0.0")), "delta_remainder 不接受字符串数字。")


func _test_empty_ready_id_returns_null() -> void:
	_assert_null(BattleTimelineState.from_dict(_payload_with("ready_unit_ids", [""])), "空 String ready id 应返回 null。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("ready_unit_ids", [&""])), "空 StringName ready id 应返回 null。")


func _test_non_array_ready_unit_ids_returns_null() -> void:
	_assert_null(BattleTimelineState.from_dict(_payload_with("ready_unit_ids", "hero")), "ready_unit_ids 非 Array 应返回 null。")


func _test_numeric_boundaries_return_null() -> void:
	_assert_null(BattleTimelineState.from_dict(_payload_with("current_tu", -1)), "current_tu 不能为负数。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("units_per_second", 0)), "units_per_second 必须为正数。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("units_per_second", -5)), "units_per_second 不能为负数。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tu_per_tick", 0)), "tu_per_tick 必须为正数。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tu_per_tick", -5)), "tu_per_tick 不能为负数。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tick_interval_seconds", 0.0)), "tick_interval_seconds 必须大于 0。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("tick_interval_seconds", -0.5)), "tick_interval_seconds 不能为负数。")
	_assert_null(BattleTimelineState.from_dict(_payload_with("delta_remainder", -0.01)), "delta_remainder 不能为负数。")


func _valid_payload() -> Dictionary:
	return {
		"current_tu": 10,
		"units_per_second": 5,
		"tick_interval_seconds": 1.0,
		"tu_per_tick": 5,
		"frozen": false,
		"ready_unit_ids": ["hero", &"enemy"],
		"delta_remainder": 0.0,
	}


func _payload_with(field_name: String, value: Variant) -> Dictionary:
	var payload := _valid_payload()
	payload[field_name] = value
	return payload


func _assert_null(value: Variant, message: String) -> void:
	if value != null:
		_failures.append("%s | actual=%s" % [message, str(value)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
