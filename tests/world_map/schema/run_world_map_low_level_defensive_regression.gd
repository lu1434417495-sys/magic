extends SceneTree

const WORLD_MAP_GRID_SYSTEM_SCRIPT = preload("res://scripts/systems/world/world_map_grid_system.gd")
const WORLD_MAP_OCCUPANT_STATE_SCRIPT = preload("res://scripts/systems/world/world_map_occupant_state.gd")
const WORLD_MAP_FOG_SYSTEM_SCRIPT = preload("res://scripts/systems/world/world_map_fog_system.gd")
const VISION_SOURCE_DATA_SCRIPT = preload("res://scripts/utils/vision_source_data.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_empty_occupant_state_objects_are_erased()
	_test_grid_cell_surface_keeps_minimal_runtime_contract()
	_test_visibility_rebuild_ignores_foreign_faction_sources()
	_test_fog_reveal_export_load_keeps_revealed_cells()

	if _failures.is_empty():
		print("World map low-level defensive regression: PASS")
		quit(0)
		return

	for failure in _failures:
		push_error(failure)
	print("World map low-level defensive regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_empty_occupant_state_objects_are_erased() -> void:
	var grid_system = WORLD_MAP_GRID_SYSTEM_SCRIPT.new()
	grid_system.setup(Vector2i(2, 2), Vector2i(4, 4))

	var coord := Vector2i(1, 1)
	grid_system._occupied_cells[coord] = WORLD_MAP_OCCUPANT_STATE_SCRIPT.create("", "")

	_assert_eq(
		grid_system.get_occupant_root(coord),
		"",
		"空的 WorldMapOccupantState 对象不应继续暴露占位根。"
	)
	_assert_true(
		not grid_system._occupied_cells.has(coord),
		"空的 WorldMapOccupantState 对象应在读取时自清理。"
	)


func _test_grid_cell_surface_keeps_minimal_runtime_contract() -> void:
	var grid_system = WORLD_MAP_GRID_SYSTEM_SCRIPT.new()
	grid_system.setup(Vector2i(2, 2), Vector2i(4, 4))
	grid_system.register_footprint("camp", Vector2i(5, 6), Vector2i.ONE)

	var cell = grid_system.get_cell(Vector2i(5, 6))
	_assert_true(cell != null, "世界地图格子读取面应继续返回有效格子对象。")
	_assert_eq(cell.coord, Vector2i(5, 6), "格子读取面应继续暴露正式坐标。")
	_assert_eq(cell.chunk_coord, Vector2i(1, 1), "格子读取面应继续暴露区块坐标。")
	_assert_eq(cell.occupant_id, "camp", "格子读取面应继续暴露占用者 id。")
	_assert_eq(cell.footprint_root_id, "camp", "格子读取面应继续暴露占位根 id。")
	_assert_true(
		not _property_list_has_name(cell, "terrain_visual_type"),
		"WorldMapCellData 不应继续暴露未消费的 terrain_visual_type 字段。"
	)
	_assert_true(
		not grid_system.has_method("get_cells_in_rect"),
		"WorldMapGridSystem 不应继续保留无调用方的 get_cells_in_rect()。"
	)


func _test_visibility_rebuild_ignores_foreign_faction_sources() -> void:
	var fog_system = WORLD_MAP_FOG_SYSTEM_SCRIPT.new()
	fog_system.setup(Vector2i(8, 8))

	var player_source = VISION_SOURCE_DATA_SCRIPT.new("scout", Vector2i(2, 2), 1, "player")
	var hostile_source = VISION_SOURCE_DATA_SCRIPT.new("raider", Vector2i(5, 5), 1, "hostile")

	fog_system.rebuild_visibility_for_faction("player", [player_source, hostile_source])

	_assert_true(
		fog_system.is_visible(Vector2i(2, 2), "player"),
		"玩家阵营的自有视野源应继续正常生效。"
	)
	_assert_true(
		not fog_system.is_visible(Vector2i(5, 5), "player"),
		"foreign faction 的视野源不应污染当前阵营可见区。"
	)


func _test_fog_reveal_export_load_keeps_revealed_cells() -> void:
	var fog_system = WORLD_MAP_FOG_SYSTEM_SCRIPT.new()
	fog_system.setup(Vector2i(8, 8))

	var revealed_coords := fog_system.reveal_diamond(Vector2i(3, 3), 1, "player")
	_assert_true(revealed_coords.has(Vector2i(3, 3)), "迷雾揭示应返回中心格。")

	var persisted_state := fog_system.export_persistent_state()
	var restored_fog_system = WORLD_MAP_FOG_SYSTEM_SCRIPT.new()
	restored_fog_system.setup(Vector2i(8, 8), persisted_state)

	_assert_true(
		restored_fog_system.is_explored(Vector2i(3, 3), "player"),
		"持久化恢复后 paid reveal 中心格应保持已探索。"
	)
	_assert_true(
		not restored_fog_system.is_visible(Vector2i(3, 3), "player"),
		"持久化恢复不应把 paid reveal 误当作当前可见。"
	)

	var distant_source = VISION_SOURCE_DATA_SCRIPT.new("scout", Vector2i(7, 7), 0, "player")
	restored_fog_system.rebuild_visibility_for_faction("player", [distant_source])
	_assert_true(
		restored_fog_system.is_explored(Vector2i(3, 3), "player"),
		"后续可见性刷新不应清除已持久化的 paid reveal。"
	)


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])


func _property_list_has_name(instance: Object, property_name: String) -> bool:
	if instance == null:
		return false

	for property_info in instance.get_property_list():
		if String(property_info.get("name", "")) == property_name:
			return true
	return false
