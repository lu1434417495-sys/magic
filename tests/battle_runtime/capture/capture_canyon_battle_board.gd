## 文件说明：该脚本属于峡谷战斗棋盘截图相关的回归测试脚本，集中维护网格服务等顶层字段。
## 审查重点：重点核对测试数据、字段用途、断言条件和失败提示是否仍然覆盖目标回归场景。
## 备注：后续如果业务规则变化，需要同步更新测试夹具、预期结果和失败信息。

extends SceneTree

const BattleBoard2D = preload("res://scripts/ui/battle_board_2d.gd")
const BattleBoardScene = preload("res://scenes/ui/battle_board_2d.tscn")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleGridService = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const BattleBoardRenderProfile = preload("res://scripts/ui/battle_board_render_profile.gd")
const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleTerrainGenerator = preload("res://scripts/systems/battle/terrain/battle_terrain_generator.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const VIEWPORT_SIZE := Vector2i(1280, 720)
const TEST_MAP_SIZE := Vector2i(19, 11)
const TEST_WORLD_COORD := Vector2i(7, 11)
const TEST_SEED := 424242
const OUTPUT_PATH := "res://battle_board_canyon_capture.png"
const HEADLESS_SIGNATURE_OUTPUT_PATH := "res://battle_board_canyon_capture.signature.txt"
const MAX_READY_FRAMES := 24

## 字段说明：记录网格服务，用于构造测试场景、记录结果并支撑回归断言。
var _grid_service := BattleGridService.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	var layout: Dictionary = _build_canyon_layout(TEST_SEED)
	var state: BattleState = _build_state(layout)
	root.size = VIEWPORT_SIZE

	var background := ColorRect.new()
	background.color = Color(0.12, 0.08, 0.06, 1.0)
	background.size = Vector2(VIEWPORT_SIZE)
	root.add_child(background)

	var board := BattleBoardScene.instantiate() as BattleBoard2D
	root.add_child(board)
	await process_frame

	var selected_coord: Vector2i = layout.get("player_coord", Vector2i.ZERO)
	board.set_viewport_size(Vector2(VIEWPORT_SIZE))
	board.configure(state, selected_coord, [])
	var ready := await _wait_for_board_render_ready(board)
	if not ready:
		push_error("Battle board capture did not reach render-ready state before screenshot.")
		quit(1)
		return
	if not _validate_unit_placement(state, &"ally_capture", layout.get("player_coord", Vector2i.ZERO), "ally_capture"):
		push_error("Battle board capture could not verify ally placement before screenshot.")
		quit(1)
		return
	if not _validate_unit_placement(state, &"enemy_capture", layout.get("enemy_coord", Vector2i.ZERO), "enemy_capture"):
		push_error("Battle board capture could not verify enemy placement before screenshot.")
		quit(1)
		return

	if DisplayServer.get_name() == "headless":
		var signature_error := _save_headless_board_signature(board)
		if signature_error != OK:
			push_error("Failed to save battle board headless signature.")
			quit(1)
			return
		print("Saved battle board headless signature to %s" % ProjectSettings.globalize_path(HEADLESS_SIGNATURE_OUTPUT_PATH))
		quit(0)
		return

	var image: Image = root.get_texture().get_image()
	var output_path: String = ProjectSettings.globalize_path(OUTPUT_PATH)
	var save_error: int = image.save_png(output_path)
	if save_error != OK:
		push_error("Failed to save battle board capture: %s" % output_path)
		quit(1)
		return

	print("Saved battle board capture to %s" % output_path)
	quit(0)


func _build_canyon_layout(seed: int) -> Dictionary:
	var generator := BattleTerrainGenerator.new()
	return generator.generate({
		"monster": {
			"entity_id": "battle_board_capture",
			"display_name": "测试遭遇",
			"faction_id": "hostile",
			"region_tag": "canyon",
		},
		"world_coord": TEST_WORLD_COORD,
		"world_seed": seed,
		"battle_terrain_profile": "canyon",
		"battle_map_size": TEST_MAP_SIZE,
	})


func _build_state(layout: Dictionary) -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"battle_board_capture"
	state.seed = TEST_SEED
	state.map_size = layout.get("map_size", Vector2i.ZERO)
	state.world_coord = TEST_WORLD_COORD
	state.terrain_profile_id = StringName(String(layout.get("terrain_profile_id", "default")))
	state.cells = _clone_cells(layout.get("cells", {}))
	state.units = {}
	state.ally_unit_ids = []
	state.enemy_unit_ids = []

	var ally := _build_unit(&"ally_capture", "队员", &"player")
	var enemy := _build_unit(&"enemy_capture", "敌人", &"hostile")
	state.units[ally.unit_id] = ally
	state.units[enemy.unit_id] = enemy
	state.ally_unit_ids.append(ally.unit_id)
	state.enemy_unit_ids.append(enemy.unit_id)
	_grid_service.place_unit(state, ally, layout.get("player_coord", Vector2i.ZERO), true)
	_grid_service.place_unit(state, enemy, layout.get("enemy_coord", Vector2i.ZERO), true)
	state.active_unit_id = ally.unit_id
	return state


func _build_unit(unit_id: StringName, display_name: String, faction_id: StringName) -> BattleUnitState:
	var unit := BattleUnitState.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = faction_id
	unit.current_hp = 12
	unit.is_alive = true
	unit.refresh_footprint()
	return unit


func _clone_cells(cells: Dictionary) -> Dictionary:
	var cloned: Dictionary = {}
	for coord_variant in cells.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var cell := cells.get(coord_variant) as BattleCellState
		if cell == null:
			continue
		cloned[coord] = BattleCellState.from_dict(cell.to_dict())
	return cloned


func _wait_for_board_render_ready(board: BattleBoard2D) -> bool:
	if board == null:
		return false
	if board.is_render_content_ready():
		return true
	for _frame in range(MAX_READY_FRAMES):
		await process_frame
		if board.is_render_content_ready():
			return true
	return false


func _validate_unit_placement(state: BattleState, unit_id: StringName, expected_coord: Vector2i, label: String) -> bool:
	if state == null:
		return false
	var unit := state.units.get(unit_id) as BattleUnitState
	if unit == null or unit.coord != expected_coord:
		push_error("%s should be anchored at %s before capture." % [label, str(expected_coord)])
		return false
	var occupant := _grid_service.get_unit_at_coord(state, expected_coord)
	if occupant == null or occupant.unit_id != unit_id:
		push_error("%s should occupy %s before capture." % [label, str(expected_coord)])
		return false
	return true


func _save_headless_board_signature(board: BattleBoard2D) -> int:
	if board == null:
		return ERR_INVALID_PARAMETER
	var file := FileAccess.open(ProjectSettings.globalize_path(HEADLESS_SIGNATURE_OUTPUT_PATH), FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_string("\n".join(_capture_board_signature(board)))
	file.close()
	return OK


func _capture_board_signature(board: BattleBoard2D) -> Array[String]:
	var lines: Array[String] = []
	lines.append("signature_version|battle_board_canyon|3")
	var profile := board.get("_render_profile") as BattleBoardRenderProfile
	if profile != null:
		lines.append("render_profile|%s|%s|height_step=%.2f|asset_dir=%s" % [
			String(profile.terrain_profile_id),
			String(profile.render_profile_id),
			profile.visual_height_step,
			profile.asset_dir,
		])
	lines.append(_capture_v2_summary(board))
	var layer_names: Array[String] = []
	layer_names.append_array(_build_layer_names("TopH", 0, BattleBoard2D.MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("EdgeDropEastH", 1, BattleBoard2D.MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("EdgeDropSouthH", 1, BattleBoard2D.MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("WallEastH", 0, BattleBoard2D.MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("WallSouthH", 0, BattleBoard2D.MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("OverlayH", 0, BattleBoard2D.MAX_RENDER_HEIGHT))
	layer_names.append_array(_build_layer_names("MarkerH", 0, BattleBoard2D.MAX_RENDER_HEIGHT))
	for layer_name in layer_names:
		var layer := board.get_node(layer_name) as TileMapLayer
		if layer == null:
			continue
		var used_cells := layer.get_used_cells()
		used_cells.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
			if a.y == b.y:
				return a.x < b.x
			return a.y < b.y
		)
		for coord in used_cells:
			lines.append("%s|%d,%d|%d" % [layer_name, coord.x, coord.y, layer.get_cell_source_id(coord)])
	var prop_layer := board.get_node("PropLayer")
	for child in prop_layer.get_children():
		lines.append("prop|%s|%s|%d|%s" % [
			String(child.get_meta("prop_id", "")),
			str(child.get_meta("board_coord", Vector2i.ZERO)),
			int(child.z_index),
			str(child.position),
		])
	var unit_layer := board.get_node("UnitLayer")
	for child in unit_layer.get_children():
		lines.append("unit|%s|%d|%s" % [child.name, int(child.z_index), str(child.position)])
	return lines


func _capture_v2_summary(board: BattleBoard2D) -> String:
	var min_top_height := 999999
	var max_top_height := -999999
	var top_cell_count := 0
	var face_cell_count := 0
	for height in range(0, BattleBoard2D.MAX_RENDER_HEIGHT + 1):
		var top_layer := board.get_node_or_null("TopH%d" % height) as TileMapLayer
		if top_layer != null:
			var used_top_cells := top_layer.get_used_cells()
			if not used_top_cells.is_empty():
				min_top_height = mini(min_top_height, height)
				max_top_height = maxi(max_top_height, height)
				top_cell_count += used_top_cells.size()
	for prefix in ["EdgeDropEastH", "EdgeDropSouthH", "WallEastH", "WallSouthH"]:
		for height in range(0, BattleBoard2D.MAX_RENDER_HEIGHT + 1):
			var face_layer := board.get_node_or_null("%s%d" % [prefix, height]) as TileMapLayer
			if face_layer != null:
				face_cell_count += face_layer.get_used_cells().size()
	if min_top_height == 999999:
		min_top_height = -1
		max_top_height = -1
	var prop_layer := board.get_node("PropLayer")
	var unit_layer := board.get_node("UnitLayer")
	return "v2_capture_summary|top_height_range=%d..%d|top_cells=%d|face_cells=%d|props=%d|units=%d" % [
		min_top_height,
		max_top_height,
		top_cell_count,
		face_cell_count,
		prop_layer.get_child_count(),
		unit_layer.get_child_count(),
	]


func _build_layer_names(prefix: String, start_height: int, max_height: int) -> Array[String]:
	var names: Array[String] = []
	for height in range(start_height, max_height + 1):
		names.append("%s%d" % [prefix, height])
	return names
