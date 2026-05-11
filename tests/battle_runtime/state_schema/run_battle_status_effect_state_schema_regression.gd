extends SceneTree

const TestRunner = preload("res://tests/shared/test_runner.gd")

const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")

var _test := TestRunner.new()
var _failures: Array[String] = _test.failures


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_valid_roundtrip_without_duration()
	_test_valid_roundtrip_with_duration_tick_and_skip()
	_test_non_dictionary_returns_null()
	_test_missing_required_field_returns_null()
	_test_extra_legacy_field_returns_null()
	_test_wrong_types_return_null()
	_test_string_numbers_and_bools_return_null()
	_test_empty_status_id_returns_null()
	_test_negative_duration_returns_null()
	_test_zero_tick_optional_returns_null()
	_test_skip_false_optional_returns_null()
	_test_duplicate_state_still_works()

	if _failures.is_empty():
		print("Battle status effect state schema regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle status effect state schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_valid_roundtrip_without_duration() -> void:
	var effect := BattleStatusEffectState.new()
	effect.status_id = &"guarded"
	effect.source_unit_id = &""
	effect.power = 2
	effect.params = {"damage_tag": "holy"}
	effect.stacks = 0

	var payload := effect.to_dict()
	_assert_true(not payload.has("duration"), "无 duration 状态的 to_dict 不应写 duration。")
	var restored = BattleStatusEffectState.from_dict(payload)
	_assert_true(restored != null, "无 duration 的当前 to_dict 形状应能恢复。")
	if restored == null:
		return
	_assert_eq(restored.status_id, &"guarded", "roundtrip 应保留 status_id。")
	_assert_eq(restored.source_unit_id, &"", "roundtrip 应允许空 source_unit_id。")
	_assert_eq(restored.power, 2, "roundtrip 应保留 power。")
	_assert_eq(restored.params, {"damage_tag": "holy"}, "roundtrip 应深拷贝并保留 params。")
	_assert_eq(restored.stacks, 0, "roundtrip 应允许新建状态写出的 0 stacks。")
	_assert_eq(restored.duration, -1, "缺失 duration 应恢复为 -1。")
	_assert_eq(restored.tick_interval_tu, 0, "缺失 tick_interval_tu 应恢复为 0。")
	_assert_eq(restored.next_tick_at_tu, 0, "缺失 next_tick_at_tu 应恢复为 0。")
	_assert_true(not restored.skip_next_turn_end_decay, "缺失 skip_next_turn_end_decay 应恢复为 false。")


func _test_valid_roundtrip_with_duration_tick_and_skip() -> void:
	var effect := BattleStatusEffectState.new()
	effect.status_id = &"burning"
	effect.source_unit_id = &"caster"
	effect.power = 3
	effect.params = {"damage_tag": "fire", "nested": {"value": 1}}
	effect.stacks = 2
	effect.duration = 20
	effect.tick_interval_tu = 10
	effect.next_tick_at_tu = 15
	effect.skip_next_turn_end_decay = true

	var restored = BattleStatusEffectState.from_dict(effect.to_dict())
	_assert_true(restored != null, "带 duration/tick/skip 的当前 to_dict 形状应能恢复。")
	if restored == null:
		return
	_assert_eq(restored.status_id, &"burning", "roundtrip 应保留 status_id。")
	_assert_eq(restored.source_unit_id, &"caster", "roundtrip 应保留 source_unit_id。")
	_assert_eq(restored.power, 3, "roundtrip 应保留 power。")
	_assert_eq(restored.params, {"damage_tag": "fire", "nested": {"value": 1}}, "roundtrip 应保留 params。")
	_assert_eq(restored.stacks, 2, "roundtrip 应保留 stacks。")
	_assert_eq(restored.duration, 20, "roundtrip 应保留 duration。")
	_assert_eq(restored.tick_interval_tu, 10, "roundtrip 应保留 tick_interval_tu。")
	_assert_eq(restored.next_tick_at_tu, 15, "roundtrip 应保留 next_tick_at_tu。")
	_assert_true(restored.skip_next_turn_end_decay, "roundtrip 应保留 skip_next_turn_end_decay。")


func _test_non_dictionary_returns_null() -> void:
	_assert_null(BattleStatusEffectState.from_dict([]), "非 Dictionary Array 入参应返回 null。")
	_assert_null(BattleStatusEffectState.from_dict("burning"), "非 Dictionary String 入参应返回 null。")
	_assert_null(BattleStatusEffectState.from_dict(null), "null 入参应返回 null。")


func _test_missing_required_field_returns_null() -> void:
	for field in ["status_id", "source_unit_id", "power", "params", "stacks"]:
		var payload := _valid_payload()
		payload.erase(field)
		_assert_null(BattleStatusEffectState.from_dict(payload), "缺少必需字段 %s 应返回 null。" % field)


func _test_extra_legacy_field_returns_null() -> void:
	var payload := _valid_payload()
	payload["remaining_turns"] = 2
	_assert_null(BattleStatusEffectState.from_dict(payload), "额外旧字段应返回 null。")


func _test_wrong_types_return_null() -> void:
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("status_id", 7)), "status_id 必须是 String/StringName。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("source_unit_id", 7)), "source_unit_id 必须是 String/StringName。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("power", 1.5)), "power 必须是 int。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("params", [])), "params 必须是 Dictionary。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("stacks", 1.0)), "stacks 必须是 int。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("duration", 1.0)), "duration 必须是 int。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("tick_interval_tu", 1.0)), "tick_interval_tu 必须是 int。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("next_tick_at_tu", 1.0)), "next_tick_at_tu 必须是 int。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("skip_next_turn_end_decay", 1)), "skip_next_turn_end_decay 必须是 bool true。")

	var string_name_payload := _valid_payload()
	string_name_payload["status_id"] = &"slow"
	string_name_payload["source_unit_id"] = &"caster"
	_assert_true(BattleStatusEffectState.from_dict(string_name_payload) != null, "StringName status_id/source_unit_id 应继续可用。")


func _test_string_numbers_and_bools_return_null() -> void:
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("power", "3")), "power 不接受字符串数字。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("stacks", "1")), "stacks 不接受字符串数字。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("duration", "10")), "duration 不接受字符串数字。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("tick_interval_tu", "10")), "tick_interval_tu 不接受字符串数字。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("next_tick_at_tu", "15")), "next_tick_at_tu 不接受字符串数字。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("skip_next_turn_end_decay", "true")), "skip_next_turn_end_decay 不接受字符串 bool。")


func _test_empty_status_id_returns_null() -> void:
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("status_id", "")), "空 String status_id 应返回 null。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("status_id", &"")), "空 StringName status_id 应返回 null。")


func _test_negative_duration_returns_null() -> void:
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("duration", -1)), "显式负 duration 应返回 null。")


func _test_zero_tick_optional_returns_null() -> void:
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("tick_interval_tu", 0)), "显式 0 tick_interval_tu 应返回 null。")
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("next_tick_at_tu", 0)), "显式 0 next_tick_at_tu 应返回 null。")


func _test_skip_false_optional_returns_null() -> void:
	_assert_null(BattleStatusEffectState.from_dict(_payload_with("skip_next_turn_end_decay", false)), "显式 false skip_next_turn_end_decay 应返回 null。")


func _test_duplicate_state_still_works() -> void:
	var effect := BattleStatusEffectState.new()
	effect.status_id = &"slow"
	effect.source_unit_id = &"caster"
	effect.power = 1
	effect.params = {"move_cost_delta": 1}
	effect.stacks = 1
	effect.duration = 15

	var duplicate := effect.duplicate_state()
	_assert_true(duplicate != null, "duplicate_state 应继续返回有效对象。")
	if duplicate == null:
		return
	_assert_true(duplicate != effect, "duplicate_state 应返回新对象。")
	_assert_eq(duplicate.status_id, &"slow", "duplicate_state 应保留 status_id。")
	_assert_eq(duplicate.source_unit_id, &"caster", "duplicate_state 应保留 source_unit_id。")
	_assert_eq(duplicate.power, 1, "duplicate_state 应保留 power。")
	_assert_eq(duplicate.params, {"move_cost_delta": 1}, "duplicate_state 应保留 params。")
	_assert_eq(duplicate.stacks, 1, "duplicate_state 应保留 stacks。")
	_assert_eq(duplicate.duration, 15, "duplicate_state 应保留 duration。")


func _valid_payload() -> Dictionary:
	return {
		"status_id": "burning",
		"source_unit_id": "caster",
		"power": 3,
		"params": {"damage_tag": "fire"},
		"stacks": 1,
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
