extends SceneTree

const WORLD_MAP_GRID_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_grid_system.gd")
const WORLD_MAP_OCCUPANT_STATE_SCRIPT = preload("res://scripts/systems/world_map_occupant_state.gd")
const WORLD_MAP_FOG_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_fog_system.gd")
const VISION_SOURCE_DATA_SCRIPT = preload("res://scripts/utils/vision_source_data.gd")

var _failures: Array[String] = []


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	_test_empty_occupant_state_objects_are_erased()
	_test_visibility_rebuild_ignores_foreign_faction_sources()

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


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
