extends SceneTree

const BattleTerrainEffectState = preload("res://scripts/systems/battle/terrain/battle_terrain_effect_state.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_valid_round_trip()
	_test_array_round_trip()
	_test_from_dict_rejects_non_dictionary()
	_test_from_dict_rejects_missing_field()
	_test_from_dict_rejects_extra_field()
	_test_from_dict_rejects_wrong_types()
	_test_from_dict_rejects_string_numbers()
	_test_from_dict_rejects_empty_required_ids()
	_test_from_dict_rejects_negative_tu()
	_test_from_dict_rejects_non_dictionary_params()
	_test_from_dict_array_rejects_non_array()
	_test_from_dict_array_rejects_bad_entry()

	if _failures.is_empty():
		print("Battle terrain effect state schema regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("Battle terrain effect state schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_valid_round_trip() -> void:
	var effect_state := _build_effect_state(&"field_roundtrip")
	var payload := effect_state.to_dict()
	var restored := BattleTerrainEffectState.from_dict(payload) as BattleTerrainEffectState
	_assert_true(restored != null, "正式 to_dict() payload 应能 from_dict()。")
	if restored == null:
		return
	_assert_eq(String(restored.field_instance_id), "field_roundtrip", "field_instance_id 应保持稳定。")
	_assert_eq(String(restored.effect_id), "burning_ground", "effect_id 应保持稳定。")
	_assert_eq(String(restored.effect_type), "damage", "effect_type 应保持稳定。")
	_assert_eq(String(restored.source_unit_id), "caster", "source_unit_id 应保持稳定。")
	_assert_eq(String(restored.source_skill_id), "fire_wall", "source_skill_id 应保持稳定。")
	_assert_eq(String(restored.target_team_filter), "enemy", "target_team_filter 应保持稳定。")
	_assert_eq(restored.power, 7, "power 应保持 int 值。")
	_assert_eq(String(restored.damage_tag), "fire", "damage_tag 应保持稳定。")
	_assert_eq(restored.remaining_tu, 12, "remaining_tu 应保持稳定。")
	_assert_eq(restored.tick_interval_tu, 3, "tick_interval_tu 应保持稳定。")
	_assert_eq(restored.next_tick_at_tu, 6, "next_tick_at_tu 应保持稳定。")
	_assert_eq(String(restored.stack_behavior), "refresh", "stack_behavior 应保持稳定。")
	_assert_eq(restored.params.get("radius", -1), 2, "params 应保持字典内容。")


func _test_array_round_trip() -> void:
	var first := _build_effect_state(&"field_array_a")
	var second := _build_effect_state(&"field_array_b")
	second.power = 11
	var payloads := BattleTerrainEffectState.to_dict_array([first, second])
	var restored_variant = BattleTerrainEffectState.from_dict_array(payloads)
	_assert_true(restored_variant is Array, "正式 to_dict_array() payload 应能 from_dict_array()。")
	if restored_variant is not Array:
		return
	var restored: Array = restored_variant
	_assert_eq(restored.size(), 2, "数组 round trip 应保留元素数量。")
	_assert_true(restored[0] is BattleTerrainEffectState, "数组 round trip 元素应为 BattleTerrainEffectState。")
	_assert_true(restored[1] is BattleTerrainEffectState, "数组 round trip 元素应为 BattleTerrainEffectState。")
	if restored.size() >= 2:
		_assert_eq(String(restored[0].field_instance_id), "field_array_a", "数组第一个元素 id 应保持稳定。")
		_assert_eq(restored[1].power, 11, "数组第二个元素 power 应保持稳定。")

	var duplicated := BattleTerrainEffectState.duplicate_array([first, second])
	_assert_eq(duplicated.size(), 2, "duplicate_array 应继续返回可用的 typed array。")
	_assert_true(duplicated[0] is BattleTerrainEffectState, "duplicate_array 元素应为 BattleTerrainEffectState。")
	_assert_eq(String(duplicated[0].field_instance_id), "field_array_a", "duplicate_array 应保留字段。")


func _test_from_dict_rejects_non_dictionary() -> void:
	_assert_true(BattleTerrainEffectState.from_dict([]) == null, "from_dict() 入参非 Dictionary 时应返回 null。")


func _test_from_dict_rejects_missing_field() -> void:
	var payload := _build_payload()
	payload.erase("effect_type")
	_assert_true(BattleTerrainEffectState.from_dict(payload) == null, "缺少正式字段时应返回 null。")


func _test_from_dict_rejects_extra_field() -> void:
	var payload := _build_payload()
	payload["legacy_effect_kind"] = "damage"
	_assert_true(BattleTerrainEffectState.from_dict(payload) == null, "存在额外旧字段时应返回 null。")


func _test_from_dict_rejects_wrong_types() -> void:
	var wrong_required_string := _build_payload()
	wrong_required_string["effect_id"] = 99
	_assert_true(BattleTerrainEffectState.from_dict(wrong_required_string) == null, "必填 id 非 String/StringName 时应返回 null。")

	var wrong_optional_string := _build_payload()
	wrong_optional_string["source_unit_id"] = 99
	_assert_true(BattleTerrainEffectState.from_dict(wrong_optional_string) == null, "可空 id 非 String/StringName 时应返回 null。")

	var wrong_int := _build_payload()
	wrong_int["power"] = 7.5
	_assert_true(BattleTerrainEffectState.from_dict(wrong_int) == null, "int 字段类型错误时应返回 null。")


func _test_from_dict_rejects_string_numbers() -> void:
	for field_name in ["power", "remaining_tu", "tick_interval_tu", "next_tick_at_tu"]:
		var payload := _build_payload()
		payload[field_name] = "1"
		_assert_true(
			BattleTerrainEffectState.from_dict(payload) == null,
			"数字字符串不应恢复为 int：%s。" % field_name
		)


func _test_from_dict_rejects_empty_required_ids() -> void:
	for field_name in ["field_instance_id", "effect_id", "effect_type", "target_team_filter", "stack_behavior"]:
		var payload := _build_payload()
		payload[field_name] = ""
		_assert_true(
			BattleTerrainEffectState.from_dict(payload) == null,
			"必填 String/StringName 字段为空时应返回 null：%s。" % field_name
		)


func _test_from_dict_rejects_negative_tu() -> void:
	for field_name in ["remaining_tu", "tick_interval_tu", "next_tick_at_tu"]:
		var payload := _build_payload()
		payload[field_name] = -1
		_assert_true(
			BattleTerrainEffectState.from_dict(payload) == null,
			"TU 字段为负时应返回 null：%s。" % field_name
		)


func _test_from_dict_rejects_non_dictionary_params() -> void:
	var payload := _build_payload()
	payload["params"] = []
	_assert_true(BattleTerrainEffectState.from_dict(payload) == null, "params 非 Dictionary 时应返回 null。")


func _test_from_dict_array_rejects_non_array() -> void:
	_assert_true(BattleTerrainEffectState.from_dict_array({}) == null, "from_dict_array() 入参非 Array 时应返回 null。")


func _test_from_dict_array_rejects_bad_entry() -> void:
	var non_dictionary_entry := [_build_payload(), "bad_entry"]
	_assert_true(BattleTerrainEffectState.from_dict_array(non_dictionary_entry) == null, "数组包含非 Dictionary entry 时整体应返回 null。")

	var invalid_payload := _build_payload()
	invalid_payload.erase("effect_id")
	var invalid_dictionary_entry := [_build_payload(), invalid_payload]
	_assert_true(BattleTerrainEffectState.from_dict_array(invalid_dictionary_entry) == null, "数组包含 schema 错误 entry 时整体应返回 null。")


func _build_effect_state(field_instance_id: StringName) -> BattleTerrainEffectState:
	var effect_state := BattleTerrainEffectState.new()
	effect_state.field_instance_id = field_instance_id
	effect_state.effect_id = &"burning_ground"
	effect_state.effect_type = &"damage"
	effect_state.source_unit_id = &"caster"
	effect_state.source_skill_id = &"fire_wall"
	effect_state.target_team_filter = &"enemy"
	effect_state.power = 7
	effect_state.damage_tag = &"fire"
	effect_state.remaining_tu = 12
	effect_state.tick_interval_tu = 3
	effect_state.next_tick_at_tu = 6
	effect_state.stack_behavior = &"refresh"
	effect_state.params = {
		"radius": 2,
		"can_ignite": true,
	}
	return effect_state


func _build_payload() -> Dictionary:
	return _build_effect_state(&"field_schema").to_dict()


func _assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual == expected:
		return
	_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
