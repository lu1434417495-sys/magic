extends SceneTree

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/persistence/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime/game_runtime_facade.gd")

const TEST_WORLD_CONFIG := "res://data/configs/world_map/test_world_map_config.tres"

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_entering_settlement_hides_player_until_close()

	if _failures.is_empty():
		print("World map settlement entry regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World map settlement entry regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_entering_settlement_hides_player_until_close() -> void:
	var game_session = GAME_SESSION_SCRIPT.new()
	var create_error := int(game_session.create_new_save(TEST_WORLD_CONFIG))
	_assert_true(create_error == OK, "测试世界应能成功创建新存档。")
	if create_error != OK:
		_cleanup(game_session)
		return

	var facade = GAME_RUNTIME_FACADE_SCRIPT.new()
	facade.setup(game_session)

	var probe := _find_adjacent_settlement_probe(facade)
	_assert_true(not probe.is_empty(), "测试世界中应至少存在一组可从外格踏入的据点入口。")
	if probe.is_empty():
		facade.dispose()
		_cleanup(game_session)
		return

	var source_coord: Vector2i = probe.get("source_coord", Vector2i.ZERO)
	var target_coord: Vector2i = probe.get("target_coord", Vector2i.ZERO)
	var direction: Vector2i = target_coord - source_coord
	var settlement_id := String(probe.get("settlement_id", ""))

	facade.set_player_coord(source_coord)
	facade.set_selected_coord(source_coord)
	facade.refresh_world_visibility()
	game_session.set_player_coord(source_coord)

	var move_result: Dictionary = facade.command_world_move(direction, 1)
	_assert_true(bool(move_result.get("ok", false)), "从外格踏入据点占格时应成功打开据点。")
	_assert_eq(facade.get_active_modal_id(), "settlement", "踏入据点占格后应自动进入 settlement modal。")
	_assert_eq(facade.get_active_settlement_id(), settlement_id, "自动打开的 settlement 应指向目标据点。")
	_assert_eq(facade.get_player_coord(), source_coord, "据点窗口打开时玩家逻辑坐标应保留在进入前格子。")
	_assert_eq(facade.get_selected_coord(), target_coord, "据点窗口打开时选中格应保持在目标据点格。")
	_assert_true(not facade.is_player_visible_on_world_map(), "据点窗口打开时世界地图上不应绘制玩家。")

	var open_snapshot: Dictionary = facade.build_headless_snapshot()
	_assert_true(not bool(open_snapshot.get("world", {}).get("player_visible_on_map", true)), "据点窗口打开时 world snapshot 应暴露隐藏玩家状态。")
	_assert_eq(
		open_snapshot.get("world", {}).get("player_coord", {}),
		{"x": source_coord.x, "y": source_coord.y},
		"据点窗口打开时快照中的 player_coord 应保持在进入前格子。"
	)

	var close_result: Dictionary = facade.command_close_active_modal()
	_assert_true(bool(close_result.get("ok", false)), "关闭据点窗口应成功返回世界地图。")
	_assert_eq(facade.get_active_modal_id(), "", "关闭据点窗口后不应残留 modal。")
	_assert_eq(facade.get_player_coord(), source_coord, "关闭据点窗口后玩家应出现在进入前格子。")
	_assert_eq(facade.get_selected_coord(), source_coord, "关闭据点窗口后选中格应回到玩家当前格。")
	_assert_true(facade.is_player_visible_on_world_map(), "关闭据点窗口后世界地图上应重新显示玩家。")

	facade.dispose()
	_cleanup(game_session)


func _find_adjacent_settlement_probe(facade) -> Dictionary:
	var settlements: Array = facade.get_world_data().get("settlements", [])
	var grid_system = facade.get_grid_system()
	for settlement_variant in settlements:
		if settlement_variant is not Dictionary:
			continue
		var settlement: Dictionary = settlement_variant
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		for offset_y in range(size.y):
			var candidate_source := origin + Vector2i(-1, offset_y)
			if _is_valid_entry_probe(settlements, grid_system, candidate_source):
				return {
					"settlement_id": String(settlement.get("settlement_id", "")),
					"source_coord": candidate_source,
					"target_coord": origin + Vector2i(0, offset_y),
				}
			candidate_source = origin + Vector2i(size.x, offset_y)
			if _is_valid_entry_probe(settlements, grid_system, candidate_source):
				return {
					"settlement_id": String(settlement.get("settlement_id", "")),
					"source_coord": candidate_source,
					"target_coord": origin + Vector2i(size.x - 1, offset_y),
				}
		for offset_x in range(size.x):
			var top_source := origin + Vector2i(offset_x, -1)
			if _is_valid_entry_probe(settlements, grid_system, top_source):
				return {
					"settlement_id": String(settlement.get("settlement_id", "")),
					"source_coord": top_source,
					"target_coord": origin + Vector2i(offset_x, 0),
				}
			var bottom_source := origin + Vector2i(offset_x, size.y)
			if _is_valid_entry_probe(settlements, grid_system, bottom_source):
				return {
					"settlement_id": String(settlement.get("settlement_id", "")),
					"source_coord": bottom_source,
					"target_coord": origin + Vector2i(offset_x, size.y - 1),
				}
	return {}


func _is_valid_entry_probe(settlements: Array, grid_system, source_coord: Vector2i) -> bool:
	if grid_system == null or not grid_system.is_cell_inside_world(source_coord):
		return false
	return _find_settlement_covering_coord(settlements, source_coord).is_empty()


func _find_settlement_covering_coord(settlements: Array, coord: Vector2i) -> Dictionary:
	for settlement_variant in settlements:
		if settlement_variant is not Dictionary:
			continue
		var settlement: Dictionary = settlement_variant
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var footprint_size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		if Rect2i(origin, footprint_size).has_point(coord):
			return settlement
	return {}


func _cleanup(game_session) -> void:
	if game_session == null:
		return
	game_session.clear_persisted_game()
	game_session.free()


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
