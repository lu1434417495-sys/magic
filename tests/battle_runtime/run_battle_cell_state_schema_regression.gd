extends SceneTree

const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleEdgeFeatureState = preload("res://scripts/systems/battle/core/battle_edge_feature_state.gd")
const BattleTerrainEffectState = preload("res://scripts/systems/battle/terrain/battle_terrain_effect_state.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_valid_round_trip_with_edge_wall_and_timed_effect()
	_test_rejects_non_dictionary()
	_test_rejects_missing_field()
	_test_rejects_extra_field()
	_test_rejects_wrong_type()
	_test_rejects_string_numeric_values()
	_test_rejects_non_array_ids()
	_test_rejects_empty_id_entry()
	_test_rejects_bad_timed_terrain_effect_entry()
	_test_rejects_bad_edge_feature_entry()
	_test_null_edge_feature_serializes_as_current_none_payload()

	if _failures.is_empty():
		print("Battle cell state schema regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle cell state schema regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_valid_round_trip_with_edge_wall_and_timed_effect() -> void:
	var source := _build_valid_cell()
	var payload := source.to_dict()
	var restored: BattleCellState = BattleCellState.from_dict(payload)
	_assert_true(restored != null, "合法 BattleCellState payload 应能恢复。")
	if restored == null:
		return
	_assert_eq(restored.coord, source.coord, "roundtrip 应保留 coord。")
	_assert_eq(restored.base_terrain, source.base_terrain, "roundtrip 应保留 base_terrain。")
	_assert_eq(restored.base_height, source.base_height, "roundtrip 应保留 base_height。")
	_assert_eq(restored.height_offset, source.height_offset, "roundtrip 应保留 height_offset。")
	_assert_eq(restored.current_height, source.current_height, "roundtrip 应保留 current_height。")
	_assert_eq(restored.stack_layer, source.stack_layer, "roundtrip 应保留 stack_layer。")
	_assert_eq(restored.move_cost, source.move_cost, "roundtrip 应保留 move_cost。")
	_assert_eq(restored.flow_direction, source.flow_direction, "roundtrip 应保留 flow_direction。")
	_assert_eq(restored.prop_ids, source.prop_ids, "roundtrip 应保留 prop_ids。")
	_assert_eq(restored.terrain_effect_ids, source.terrain_effect_ids, "roundtrip 应保留 terrain_effect_ids。")
	_assert_eq(restored.timed_terrain_effects.size(), 1, "roundtrip 应恢复 timed terrain effect。")
	_assert_eq(restored.timed_terrain_effects[0].field_instance_id, &"field_001", "roundtrip 应保留 terrain effect 字段。")
	_assert_eq(restored.edge_feature_east.feature_kind, BattleEdgeFeatureState.FEATURE_WALL, "roundtrip 应恢复 east wall。")
	_assert_eq(restored.edge_feature_south.feature_kind, BattleEdgeFeatureState.FEATURE_NONE, "roundtrip 应恢复 south none edge。")

	var duplicate: BattleCellState = restored.duplicate_cell()
	_assert_true(duplicate != null, "duplicate_cell 应继续可用。")
	_assert_eq(duplicate.edge_feature_east.feature_kind, BattleEdgeFeatureState.FEATURE_WALL, "duplicate_cell 应复制 edge feature。")
	var columns := BattleCellState.build_columns_from_surface_cells({restored.coord: restored})
	_assert_true(columns.has(restored.coord), "build_columns_from_surface_cells 应继续为合法 cell 生成列。")
	_assert_true((columns.get(restored.coord, []) as Array).size() > 0, "build_columns_from_surface_cells 生成的列不应为空。")


func _test_rejects_non_dictionary() -> void:
	_assert_null(BattleCellState.from_dict("not_a_dictionary"), "from_dict 应拒绝非 Dictionary 入参。")


func _test_rejects_missing_field() -> void:
	var payload := _valid_payload()
	payload.erase("move_cost")
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝缺字段 payload。")


func _test_rejects_extra_field() -> void:
	var payload := _valid_payload()
	payload["legacy_height"] = 2
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝额外旧字段 payload。")


func _test_rejects_wrong_type() -> void:
	var payload := _valid_payload()
	payload["coord"] = Vector2(1.0, 2.0)
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝 coord 非 Vector2i。")

	payload = _valid_payload()
	payload["passable"] = "true"
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝 passable 非 bool。")

	payload = _valid_payload()
	payload["base_terrain"] = ""
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝空 base_terrain。")

	payload = _valid_payload()
	payload["occupant_unit_id"] = 12
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝 occupant_unit_id 非 String/StringName。")

	payload = _valid_payload()
	payload["flow_direction"] = Vector2(1.0, 0.0)
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝 flow_direction 非 Vector2i。")

	payload = _valid_payload()
	payload["move_cost"] = 0
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝非正 move_cost。")


func _test_rejects_string_numeric_values() -> void:
	var int_fields := ["stack_layer", "base_height", "height_offset", "current_height", "move_cost"]
	for field_name in int_fields:
		var payload := _valid_payload()
		payload[field_name] = "1"
		_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝字符串数值字段 %s。" % field_name)


func _test_rejects_non_array_ids() -> void:
	var payload := _valid_payload()
	payload["prop_ids"] = "rock"
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝非 Array prop_ids。")

	payload = _valid_payload()
	payload["terrain_effect_ids"] = "mud"
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝非 Array terrain_effect_ids。")


func _test_rejects_empty_id_entry() -> void:
	var payload := _valid_payload()
	payload["prop_ids"] = ["rock", ""]
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝空 prop id entry。")

	payload = _valid_payload()
	payload["terrain_effect_ids"] = [&"mud", 7]
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝非 String/StringName terrain effect id entry。")


func _test_rejects_bad_timed_terrain_effect_entry() -> void:
	var payload := _valid_payload()
	payload["timed_terrain_effects"] = [_build_timed_effect().to_dict(), "bad_effect_entry"]
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝 timed_terrain_effects 坏 entry。")


func _test_rejects_bad_edge_feature_entry() -> void:
	var payload := _valid_payload()
	payload["edge_feature_east"] = "bad_edge_entry"
	_assert_null(BattleCellState.from_dict(payload), "from_dict 应拒绝 edge feature 坏 entry。")


func _test_null_edge_feature_serializes_as_current_none_payload() -> void:
	var cell := _build_valid_cell()
	cell.edge_feature_east = null
	var payload := cell.to_dict()
	var edge_payload: Variant = payload.get("edge_feature_east")
	_assert_true(edge_payload is Dictionary, "null edge feature 的 to_dict 仍应输出当前 none edge Dictionary。")
	if edge_payload is Dictionary:
		_assert_true((edge_payload as Dictionary).has("feature_kind"), "none edge payload 应包含正式字段。")
	var restored := BattleCellState.from_dict(payload)
	_assert_true(restored != null, "null edge feature 的 canonical to_dict payload 应能被 strict from_dict 恢复。")
	if restored != null:
		_assert_eq(restored.edge_feature_east.feature_kind, BattleEdgeFeatureState.FEATURE_NONE, "null edge feature 应恢复为 none。")


func _build_valid_cell() -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = Vector2i(2, 3)
	cell.base_terrain = BattleCellState.TERRAIN_FLOWING_WATER
	cell.base_height = 1
	cell.height_offset = 1
	cell.recalculate_runtime_values()
	cell.occupant_unit_id = &"unit_001"
	cell.prop_ids = [&"stone_pillar", &"torch"]
	cell.terrain_effect_ids = [&"rapid_current"]
	cell.timed_terrain_effects = [_build_timed_effect()]
	cell.flow_direction = Vector2i.RIGHT
	cell.edge_feature_east = BattleEdgeFeatureState.make_wall()
	cell.edge_feature_south = BattleEdgeFeatureState.make_none()
	return cell


func _build_timed_effect() -> BattleTerrainEffectState:
	var effect := BattleTerrainEffectState.new()
	effect.field_instance_id = &"field_001"
	effect.effect_id = &"burning_ground"
	effect.effect_type = &"damage"
	effect.source_unit_id = &"caster_001"
	effect.source_skill_id = &"flame_patch"
	effect.target_team_filter = &"hostile"
	effect.power = 3
	effect.damage_tag = &"fire"
	effect.remaining_tu = 20
	effect.tick_interval_tu = 10
	effect.next_tick_at_tu = 10
	effect.stack_behavior = &"refresh"
	effect.params = {"damage_tag": "fire"}
	return effect


func _valid_payload() -> Dictionary:
	return _build_valid_cell().to_dict().duplicate(true)


func _assert_null(value: Variant, message: String) -> void:
	if value != null:
		_failures.append("%s actual=%s" % [message, str(value)])


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual: Variant, expected: Variant, message: String) -> void:
	if actual != expected:
		_failures.append("%s actual=%s expected=%s" % [message, str(actual), str(expected)])
