## 文件说明：该脚本属于峡谷战斗棋盘截图相关的回归测试脚本，集中维护网格服务等顶层字段。
## 审查重点：重点核对测试数据、字段用途、断言条件和失败提示是否仍然覆盖目标回归场景。
## 备注：后续如果业务规则变化，需要同步更新测试夹具、预期结果和失败信息。

extends SceneTree

const BattleBoard2D = preload("res://scripts/ui/battle_board_2d.gd")
const BattleBoardScene = preload("res://scenes/ui/battle_board_2d.tscn")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleGridService = preload("res://scripts/systems/battle_grid_service.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleTerrainGenerator = preload("res://scripts/systems/battle_terrain_generator.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")

const VIEWPORT_SIZE := Vector2i(1280, 720)
const TEST_MAP_SIZE := Vector2i(19, 11)
const TEST_WORLD_COORD := Vector2i(7, 11)
const TEST_SEED := 424242
const OUTPUT_PATH := "res://battle_board_canyon_capture.png"

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
	await process_frame
	await process_frame

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
