extends SceneTree

const BattleEdgeFeatureState = preload("res://scripts/systems/battle/core/battle_edge_feature_state.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_make_wall_roundtrip()
	_test_make_none_roundtrip()
	_test_non_dictionary_is_rejected()
	_test_missing_field_is_rejected()
	_test_extra_field_is_rejected()
	_test_wrong_types_are_rejected()
	_test_string_bool_and_int_are_rejected()
	_test_empty_required_enum_is_rejected()
	_test_negative_render_layers_is_rejected()
	_test_duplicate_feature_still_uses_current_schema()
	if _failures.is_empty():
		print("Battle edge feature state schema regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle edge feature state schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_make_wall_roundtrip() -> void:
	var wall := BattleEdgeFeatureState.make_wall()
	var restored = BattleEdgeFeatureState.from_dict(wall.to_dict())
	_assert_true(restored != null, "make_wall 的当前 to_dict 形状应能恢复。")
	if restored == null:
		return
	_assert_eq(restored.feature_kind, BattleEdgeFeatureState.FEATURE_WALL, "make_wall roundtrip 应保留 feature_kind。")
	_assert_eq(restored.render_kind, BattleEdgeFeatureState.RENDER_WALL, "make_wall roundtrip 应保留 render_kind。")
	_assert_eq(restored.render_layers, 1, "make_wall roundtrip 应保留 render_layers。")
	_assert_true(restored.blocks_move, "make_wall roundtrip 应保留 blocks_move。")
	_assert_true(restored.blocks_occupancy, "make_wall roundtrip 应保留 blocks_occupancy。")
	_assert_true(restored.blocks_los, "make_wall roundtrip 应保留 blocks_los。")
	_assert_eq(restored.interaction_kind, BattleEdgeFeatureState.INTERACT_NONE, "make_wall roundtrip 应保留 interaction_kind。")
	_assert_eq(restored.state_tag, &"", "make_wall roundtrip 应允许空 state_tag。")


func _test_make_none_roundtrip() -> void:
	var none_feature := BattleEdgeFeatureState.make_none()
	var restored = BattleEdgeFeatureState.from_dict(none_feature.to_dict())
	_assert_true(restored != null, "make_none 的当前 to_dict 形状应能恢复。")
	if restored == null:
		return
	_assert_eq(restored.feature_kind, BattleEdgeFeatureState.FEATURE_NONE, "make_none roundtrip 应保留 feature_kind。")
	_assert_eq(restored.render_kind, BattleEdgeFeatureState.RENDER_NONE, "make_none roundtrip 应保留 render_kind。")
	_assert_eq(restored.render_layers, 0, "make_none roundtrip 应保留 render_layers。")
	_assert_true(restored.is_empty(), "make_none roundtrip 后仍应为空 edge feature。")


func _test_non_dictionary_is_rejected() -> void:
	_assert_true(BattleEdgeFeatureState.from_dict("none") == null, "非 Dictionary 入参应返回 null。")
	_assert_true(BattleEdgeFeatureState.from_dict(null) == null, "null 入参应返回 null。")


func _test_missing_field_is_rejected() -> void:
	var payload := _valid_payload()
	payload.erase("state_tag")
	_assert_true(BattleEdgeFeatureState.from_dict(payload) == null, "缺少当前 schema 字段应返回 null。")


func _test_extra_field_is_rejected() -> void:
	var payload := _valid_payload()
	payload["legacy_blocks_projectile"] = true
	_assert_true(BattleEdgeFeatureState.from_dict(payload) == null, "包含额外旧字段应返回 null。")


func _test_wrong_types_are_rejected() -> void:
	var feature_kind_number := _valid_payload()
	feature_kind_number["feature_kind"] = 1
	_assert_true(BattleEdgeFeatureState.from_dict(feature_kind_number) == null, "feature_kind 非 String/StringName 应返回 null。")

	var state_tag_number := _valid_payload()
	state_tag_number["state_tag"] = 1
	_assert_true(BattleEdgeFeatureState.from_dict(state_tag_number) == null, "state_tag 非 String/StringName 应返回 null。")

	var render_layers_float := _valid_payload()
	render_layers_float["render_layers"] = 1.0
	_assert_true(BattleEdgeFeatureState.from_dict(render_layers_float) == null, "render_layers 非 int 应返回 null。")

	var blocks_move_number := _valid_payload()
	blocks_move_number["blocks_move"] = 1
	_assert_true(BattleEdgeFeatureState.from_dict(blocks_move_number) == null, "blocks_move 非 bool 应返回 null。")

	var string_name_payload := _valid_payload()
	string_name_payload["feature_kind"] = &"wall"
	string_name_payload["render_kind"] = &"wall"
	string_name_payload["interaction_kind"] = &"none"
	string_name_payload["state_tag"] = &"closed"
	_assert_true(BattleEdgeFeatureState.from_dict(string_name_payload) != null, "StringName enum 字段应继续可用。")


func _test_string_bool_and_int_are_rejected() -> void:
	var string_int := _valid_payload()
	string_int["render_layers"] = "1"
	_assert_true(BattleEdgeFeatureState.from_dict(string_int) == null, "字符串 render_layers 不应被 int() 恢复。")

	var string_bool := _valid_payload()
	string_bool["blocks_los"] = "true"
	_assert_true(BattleEdgeFeatureState.from_dict(string_bool) == null, "字符串 bool 不应被 bool() 恢复。")


func _test_empty_required_enum_is_rejected() -> void:
	for field in ["feature_kind", "render_kind", "interaction_kind"]:
		var payload := _valid_payload()
		payload[field] = ""
		_assert_true(BattleEdgeFeatureState.from_dict(payload) == null, "空必填 enum 字段 %s 应返回 null。" % field)


func _test_negative_render_layers_is_rejected() -> void:
	var payload := _valid_payload()
	payload["render_layers"] = -1
	_assert_true(BattleEdgeFeatureState.from_dict(payload) == null, "负 render_layers 应返回 null。")


func _test_duplicate_feature_still_uses_current_schema() -> void:
	var duplicate := BattleEdgeFeatureState.make_low_wall().duplicate_feature()
	_assert_true(duplicate != null, "duplicate_feature 应继续返回有效对象。")
	if duplicate == null:
		return
	_assert_eq(duplicate.feature_kind, BattleEdgeFeatureState.FEATURE_LOW_WALL, "duplicate_feature 应保留 feature_kind。")
	_assert_eq(duplicate.render_kind, BattleEdgeFeatureState.RENDER_WALL, "duplicate_feature 应保留 render_kind。")
	_assert_eq(duplicate.render_layers, 1, "duplicate_feature 应保留 render_layers。")


func _valid_payload() -> Dictionary:
	return BattleEdgeFeatureState.make_wall().to_dict()


func _assert_true(value: bool, message: String) -> void:
	if not value:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
