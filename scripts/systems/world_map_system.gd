class_name WorldMapSystem
extends Control

const WORLD_MAP_GRID_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_grid_system.gd")
const WORLD_MAP_FOG_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_fog_system.gd")
const BATTLE_MAP_GENERATION_SYSTEM_SCRIPT = preload("res://scripts/systems/battle_map_generation_system.gd")
const VISION_SOURCE_DATA_SCRIPT = preload("res://scripts/utils/vision_source_data.gd")

@export_file("*.tres") var generation_config_path := ""

@onready var title_label: Label = $MarginContainer/Layout/TopBar/TitleLabel
@onready var world_map_view = $MarginContainer/Layout/Content/MapPanel/MapMargin/MapViewport/WorldMapView
@onready var battle_map_panel: BattleMapPanel = $MarginContainer/Layout/Content/MapPanel/MapMargin/MapViewport/BattleMapPanel
@onready var status_label: Label = $MarginContainer/Layout/TopBar/StatusLabel
@onready var structure_summary: RichTextLabel = $RuntimeSidebar/SidebarMargin/SidebarContent/StructureSummary
@onready var selection_info: RichTextLabel = $RuntimeSidebar/SidebarMargin/SidebarContent/SelectionInfo
@onready var runtime_help_text: Label = $RuntimeSidebar/SidebarMargin/SidebarContent/RuntimeHelpText
@onready var settlement_window = $SettlementWindow
@onready var settlement_window_system = $SettlementWindowSystem

var _generation_config
var _grid_system = WORLD_MAP_GRID_SYSTEM_SCRIPT.new()
var _fog_system = WORLD_MAP_FOG_SYSTEM_SCRIPT.new()
var _battle_generation_system = BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.new()
var _player_coord := Vector2i.ZERO
var _selected_coord := Vector2i.ZERO
var _player_faction_id := "player"
var _world_data: Dictionary = {}
var _battle_state: Dictionary = {}
var _battle_selected_coord := Vector2i(-1, -1)
var _active_battle_monster_id := ""
var _world_title_text := ""
var _world_help_text := ""


func _ready() -> void:
	if generation_config_path.is_empty():
		push_error("World map config path is not assigned.")
		return

	var prepare_error := GameSession.ensure_world_ready(generation_config_path)
	if prepare_error != OK:
		push_error("Failed to prepare persistent world state for %s. Error code: %s" % [generation_config_path, prepare_error])
		return

	_generation_config = GameSession.get_generation_config()
	if _generation_config == null:
		push_error("Failed to load world map config from %s." % generation_config_path)
		return

	_world_title_text = title_label.text
	_world_help_text = runtime_help_text.text
	battle_map_panel.hide_battle()

	_grid_system.setup(_generation_config.world_size_in_chunks, _generation_config.chunk_size)
	_fog_system.setup(_generation_config.get_world_size_cells())
	_world_data = GameSession.get_world_data()
	_player_coord = GameSession.get_player_coord()
	_player_faction_id = GameSession.get_player_faction_id()
	_register_settlement_footprints()
	_selected_coord = _player_coord

	settlement_window.action_requested.connect(_on_settlement_action_requested)
	settlement_window.closed.connect(_on_settlement_window_closed)
	world_map_view.cell_clicked.connect(_on_world_map_cell_clicked)
	battle_map_panel.battle_cell_clicked.connect(_on_battle_cell_clicked)
	battle_map_panel.movement_reset_requested.connect(_reset_battle_movement)
	battle_map_panel.resolve_requested.connect(_resolve_active_battle)

	settlement_window_system.setup(_world_data.get("settlements", []))
	_refresh_fog()
	world_map_view.configure(
		_grid_system,
		_fog_system,
		_world_data,
		_player_coord,
		_selected_coord,
		_player_faction_id
	)
	_set_world_view_active()
	_update_structure_summary()
	_update_selection_info()

	var start_settlement_name: String = _world_data.get("player_start_settlement_name", "")
	if start_settlement_name.is_empty():
		_update_status("大地图已载入。方向键/WASD移动，点击可见据点或按 Enter 打开据点窗口。")
	else:
		_update_status("大地图已载入，初始村庄为 %s。方向键/WASD移动，点击可见据点或按 Enter 打开据点窗口。" % start_settlement_name)


func _unhandled_input(event: InputEvent) -> void:
	if _generation_config == null:
		return
	if settlement_window_system.is_window_open():
		return
	if event is not InputEventKey:
		return

	var key_event := event as InputEventKey
	if not key_event.pressed or key_event.echo:
		return

	if _is_battle_active():
		if _handle_battle_input(key_event):
			get_viewport().set_input_as_handled()
		return

	if _handle_world_input(key_event):
		get_viewport().set_input_as_handled()


func _handle_world_input(key_event: InputEventKey) -> bool:
	var movement := Vector2i.ZERO
	match key_event.keycode:
		KEY_LEFT, KEY_A:
			movement = Vector2i.LEFT
		KEY_RIGHT, KEY_D:
			movement = Vector2i.RIGHT
		KEY_UP, KEY_W:
			movement = Vector2i.UP
		KEY_DOWN, KEY_S:
			movement = Vector2i.DOWN
		KEY_ENTER, KEY_KP_ENTER, KEY_SPACE:
			_try_open_settlement_at(_selected_coord)
			return true
		_:
			return false

	_move_player(movement)
	return true


func _handle_battle_input(key_event: InputEventKey) -> bool:
	match key_event.keycode:
		KEY_LEFT, KEY_A:
			_attempt_battle_move(Vector2i.LEFT)
		KEY_RIGHT, KEY_D:
			_attempt_battle_move(Vector2i.RIGHT)
		KEY_UP, KEY_W:
			_attempt_battle_move(Vector2i.UP)
		KEY_DOWN, KEY_S:
			_attempt_battle_move(Vector2i.DOWN)
		KEY_R:
			_reset_battle_movement()
		KEY_ENTER, KEY_KP_ENTER, KEY_SPACE:
			_resolve_active_battle()
		_:
			return false
	return true


func _move_player(direction: Vector2i) -> void:
	var target_coord := _player_coord + direction
	if not _grid_system.is_cell_walkable(target_coord):
		_update_status("已到达大地图边界。")
		return

	_player_coord = target_coord
	var persist_error := GameSession.set_player_coord(_player_coord)
	_selected_coord = _player_coord
	_refresh_fog()
	world_map_view.set_runtime_state(_player_coord, _selected_coord)
	_update_selection_info()

	var encountered_monster := _get_monster_at(_player_coord)
	if not encountered_monster.is_empty():
		_start_battle(encountered_monster)
		return

	if persist_error == OK:
		_update_status("玩家移动到 %s，视野已刷新。" % _format_coord(_player_coord))
	else:
		_update_status("玩家移动到 %s，但大地图持久化失败。" % _format_coord(_player_coord))


func _start_battle(monster: Dictionary) -> void:
	_active_battle_monster_id = monster.get("entity_id", "")
	_battle_state = _battle_generation_system.build_battle({
		"monster": monster,
		"world_coord": _player_coord,
		"world_seed": int(_generation_config.seed),
	})
	if _battle_state.is_empty():
		_active_battle_monster_id = ""
		_update_status("遭遇战生成失败。")
		return

	_battle_selected_coord = _battle_state.get("player_coord", Vector2i.ZERO)
	_battle_state["selected_coord"] = _battle_selected_coord
	_set_battle_view_active()
	_refresh_battle_panel()
	_update_structure_summary()
	_update_selection_info()
	_update_status("遭遇 %s，世界地图停止渲染，已切入战斗地图。" % monster.get("display_name", "野怪"))


func _resolve_active_battle() -> void:
	if not _is_battle_active():
		return

	var battle_monster: Dictionary = _battle_state.get("monster", {})
	var monster_name: String = String(battle_monster.get("display_name", "野怪"))
	_remove_active_battle_monster()
	var persist_error := GameSession.set_world_data(_world_data)

	_battle_state.clear()
	_battle_selected_coord = Vector2i(-1, -1)
	_active_battle_monster_id = ""
	_selected_coord = _player_coord

	_set_world_view_active()
	_update_structure_summary()
	_update_selection_info()

	if persist_error == OK:
		_update_status("%s 已被击退，返回世界地图。" % monster_name)
	else:
		_update_status("%s 已被击退，但世界状态持久化失败。" % monster_name)


func _attempt_battle_move(direction: Vector2i) -> void:
	if not _is_battle_active():
		return
	_attempt_battle_move_to(_battle_state.get("player_coord", Vector2i.ZERO) + direction)


func _attempt_battle_move_to(target_coord: Vector2i) -> void:
	if not _is_battle_active():
		return

	var player_battle_coord: Vector2i = _battle_state.get("player_coord", Vector2i.ZERO)
	_battle_selected_coord = target_coord
	_battle_state["selected_coord"] = _battle_selected_coord

	var move_result: Dictionary = _battle_generation_system.evaluate_move(_battle_state, player_battle_coord, target_coord)
	if not bool(move_result.get("allowed", false)):
		_refresh_battle_panel()
		_update_selection_info()
		_update_status(move_result.get("message", "无法移动。"))
		return

	_battle_state["player_coord"] = target_coord
	_battle_state["remaining_move"] = int(_battle_state.get("remaining_move", 0)) - int(move_result.get("cost", 1))
	_battle_selected_coord = target_coord
	_battle_state["selected_coord"] = target_coord
	_refresh_battle_panel()
	_update_selection_info()

	if target_coord == _battle_state.get("enemy_coord", Vector2i.ZERO):
		_update_status("已接敌并完成本次遭遇。")
		_resolve_active_battle()
		return

	_update_status("战斗中移动到 %s，消耗 %d 点行动点。" % [
		_format_coord(target_coord),
		int(move_result.get("cost", 1)),
	])


func _reset_battle_movement() -> void:
	if not _is_battle_active():
		return

	_battle_state["remaining_move"] = int(_battle_state.get("max_move", 0))
	_refresh_battle_panel()
	_update_selection_info()
	_update_status("当前回合行动点已重置。")


func _refresh_fog() -> void:
	var sources: Array = [
		VISION_SOURCE_DATA_SCRIPT.new("player_main", _player_coord, _generation_config.player_vision_range, _player_faction_id),
	]
	_fog_system.rebuild_visibility_for_faction(_player_faction_id, sources)


func _on_world_map_cell_clicked(coord: Vector2i) -> void:
	if _is_battle_active():
		return

	_selected_coord = coord
	world_map_view.set_runtime_state(_player_coord, _selected_coord)
	_update_selection_info()

	if _fog_system.is_visible(coord, _player_faction_id):
		if _try_open_settlement_at(coord):
			return

	_update_status("已选中格子 %s。" % _format_coord(coord))


func _on_battle_cell_clicked(coord: Vector2i) -> void:
	if not _is_battle_active():
		return

	_battle_selected_coord = coord
	_battle_state["selected_coord"] = coord
	var player_battle_coord: Vector2i = _battle_state.get("player_coord", Vector2i.ZERO)
	if _is_adjacent_4(player_battle_coord, coord):
		_attempt_battle_move_to(coord)
		return

	_refresh_battle_panel()
	_update_selection_info()
	_update_status("已选中战斗格 %s。" % _format_coord(coord))


func _try_open_settlement_at(coord: Vector2i) -> bool:
	if _is_battle_active():
		return false
	if not _fog_system.is_visible(coord, _player_faction_id):
		_update_status("该格当前不在视野中。")
		return false

	var settlement := _get_settlement_at(coord)
	if settlement.is_empty():
		_update_status("当前格没有可交互据点。")
		return false

	settlement_window_system.open_settlement_window(settlement.get("settlement_id", ""))
	_update_status("已打开 %s 的据点窗口。" % settlement.get("display_name", "据点"))
	return true


func _get_settlement_at(coord: Vector2i) -> Dictionary:
	for settlement in _world_data.get("settlements", []):
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		var rect := Rect2i(origin, size)
		if rect.has_point(coord):
			return settlement

	return {}


func _get_world_npc_at(coord: Vector2i) -> Dictionary:
	for npc in _world_data.get("world_npcs", []):
		if npc.get("coord", Vector2i.ZERO) == coord:
			return npc
	return {}


func _get_monster_at(coord: Vector2i) -> Dictionary:
	for monster in _world_data.get("wild_monsters", []):
		if monster.get("coord", Vector2i.ZERO) == coord:
			return monster
	return {}


func _update_structure_summary() -> void:
	if _is_battle_active():
		_update_battle_structure_summary()
		return

	var world_size_cells: Vector2i = _generation_config.get_world_size_cells()
	var summary := PackedStringArray([
		"[b]地图结构[/b]",
		"四方格世界地图：%d x %d" % [world_size_cells.x, world_size_cells.y],
		"Chunk：%d x %d，每个 chunk %d x %d 格" % [
			_generation_config.world_size_in_chunks.x,
			_generation_config.world_size_in_chunks.y,
			_generation_config.chunk_size.x,
			_generation_config.chunk_size.y,
		],
		"",
		"[b]迷雾接口[/b]",
		"可见 / 已探索 / 未探索 三层状态",
		"玩家视野半径：%d" % _generation_config.player_vision_range,
		"",
		"[b]据点占地[/b]",
		"村 1x1，镇 2x2，城市 2x2",
		"主城 3x3，世界据点 4x4",
		"",
		"[b]交互模型[/b]",
		"无城内地图，据点通过窗口交付设施与服务",
		"踩入野怪格会切入随机战斗地图",
		"",
		"[b]本次生成[/b]",
		"据点数量：%d" % _world_data.get("settlements", []).size(),
		"出生据点：%s" % _world_data.get("player_start_settlement_name", "未指定"),
		"剩余野怪：%d" % _world_data.get("wild_monsters", []).size(),
	])
	structure_summary.text = "\n".join(summary)


func _update_battle_structure_summary() -> void:
	var map_size: Vector2i = _battle_state.get("size", Vector2i.ZERO)
	var terrain_counts: Dictionary = _battle_state.get("terrain_counts", {})
	var summary := PackedStringArray([
		"[b]战斗地图[/b]",
		"尺寸：%d x %d" % [map_size.x, map_size.y],
		"随机种子：%d" % int(_battle_state.get("seed", 0)),
		"",
		"[b]地形分布[/b]",
		"陆地 %d / 森林 %d / 水域 %d" % [
			int(terrain_counts.get(BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_LAND, 0)),
			int(terrain_counts.get(BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_FOREST, 0)),
			int(terrain_counts.get(BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_WATER, 0)),
		],
		"泥沼 %d / 地刺 %d" % [
			int(terrain_counts.get(BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_MUD, 0)),
			int(terrain_counts.get(BATTLE_MAP_GENERATION_SYSTEM_SCRIPT.TERRAIN_SPIKE, 0)),
		],
		"",
		"[b]移动规则[/b]",
		"森林可通行，水域不可通行",
		"泥沼与地刺消耗 2 点行动点",
		"相邻高度差 <= 1 可通行，> 1 阻挡移动",
		"人物行动点属性：%d" % int(_battle_state.get("action_points", 0)),
	])
	structure_summary.text = "\n".join(summary)


func _update_selection_info() -> void:
	if _is_battle_active():
		_update_battle_selection_info()
		return

	var chunk_coord: Vector2i = _grid_system.get_chunk_coord(_selected_coord)
	var fog_name: String = _get_fog_state_name(_fog_system.get_fog_state(_selected_coord, _player_faction_id))
	var lines := PackedStringArray([
		"[b]当前格信息[/b]",
		"坐标：%s" % _format_coord(_selected_coord),
		"Chunk：%s" % _format_coord(chunk_coord),
		"迷雾：%s" % fog_name,
	])

	var settlement := _get_settlement_at(_selected_coord)
	if not settlement.is_empty():
		lines.append("据点：%s (%s)" % [settlement.get("display_name", "据点"), settlement.get("tier_name", "未知")])
		lines.append("设施数：%d" % settlement.get("facilities", []).size())
	else:
		var npc := _get_world_npc_at(_selected_coord)
		if not npc.is_empty():
			lines.append("NPC：%s" % npc.get("display_name", "NPC"))
		var monster := _get_monster_at(_selected_coord)
		if not monster.is_empty():
			lines.append("野怪：%s" % monster.get("display_name", "野怪"))
		if npc.is_empty() and monster.is_empty():
			lines.append("占用：空")

	selection_info.text = "\n".join(lines)


func _update_battle_selection_info() -> void:
	var selected_coord := _battle_selected_coord
	if selected_coord == Vector2i(-1, -1):
		selected_coord = _battle_state.get("player_coord", Vector2i.ZERO)

	var selected_cell: Dictionary = _battle_generation_system.get_cell(_battle_state, selected_coord)
	var player_battle_coord: Vector2i = _battle_state.get("player_coord", Vector2i.ZERO)
	var player_cell: Dictionary = _battle_generation_system.get_cell(_battle_state, player_battle_coord)
	var terrain_name := _battle_generation_system.get_terrain_display_name(String(selected_cell.get("terrain", "")))
	var height_diff := absi(int(selected_cell.get("height", 0)) - int(player_cell.get("height", 0)))

	var lines := PackedStringArray([
		"[b]战斗格信息[/b]",
		"坐标：%s" % _format_coord(selected_coord),
		"地形：%s" % terrain_name,
		"高度：%d" % int(selected_cell.get("height", 0)),
		"移动消耗：%d" % int(selected_cell.get("move_cost", 0)),
		"与玩家高度差：%d" % height_diff,
		"剩余行动点：%d / %d" % [
			int(_battle_state.get("remaining_move", 0)),
			int(_battle_state.get("max_move", 0)),
		],
	])

	if not bool(selected_cell.get("passable", false)):
		lines.append("通行：否")
	else:
		lines.append("通行：是")

	if selected_coord == player_battle_coord:
		lines.append("占用：玩家")
	elif selected_coord == _battle_state.get("enemy_coord", Vector2i.ZERO):
		lines.append("占用：野怪")

	selection_info.text = "\n".join(lines)


func _refresh_battle_panel() -> void:
	if not _is_battle_active():
		return
	battle_map_panel.refresh(_battle_state, _battle_selected_coord)


func _set_world_view_active() -> void:
	title_label.text = _world_title_text
	runtime_help_text.text = _world_help_text
	world_map_view.visible = true
	battle_map_panel.hide_battle()
	world_map_view.refresh_world(_world_data)
	world_map_view.set_runtime_state(_player_coord, _selected_coord)


func _set_battle_view_active() -> void:
	title_label.text = "战斗地图"
	runtime_help_text.text = "1. 方向键/WASD 战斗移动\n2. 点击格子查看或尝试移动\n3. R 重置行动点\n4. Enter 或按钮结束遭遇"
	world_map_view.visible = false
	battle_map_panel.show_battle(_battle_state, _battle_selected_coord)


func _remove_active_battle_monster() -> void:
	if _active_battle_monster_id.is_empty():
		return

	var remaining_monsters: Array = _world_data.get("wild_monsters", []).duplicate(true)
	for index in range(remaining_monsters.size()):
		var monster: Dictionary = remaining_monsters[index]
		if monster.get("entity_id", "") != _active_battle_monster_id:
			continue
		remaining_monsters.remove_at(index)
		break

	_world_data["wild_monsters"] = remaining_monsters


func _on_settlement_action_requested(settlement_id: String, action_id: String, payload: Dictionary) -> void:
	var result: Dictionary = settlement_window_system.execute_settlement_action(settlement_id, action_id, payload)
	var message: String = result.get("message", "交互已完成。")
	settlement_window.set_feedback(message)
	_update_status(message)


func _on_settlement_window_closed() -> void:
	_update_status("已关闭据点窗口，返回世界地图。")


func _update_status(message: String) -> void:
	status_label.text = message


func _get_fog_state_name(fog_state: int) -> String:
	match fog_state:
		WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_VISIBLE:
			return "当前可见"
		WORLD_MAP_FOG_SYSTEM_SCRIPT.FOG_EXPLORED:
			return "已探索"
		_:
			return "未探索"


func _is_battle_active() -> bool:
	return not _battle_state.is_empty()


func _is_adjacent_4(from_coord: Vector2i, to_coord: Vector2i) -> bool:
	return absi(from_coord.x - to_coord.x) + absi(from_coord.y - to_coord.y) == 1


func _format_coord(coord: Vector2i) -> String:
	return "(%d, %d)" % [coord.x, coord.y]


func _register_settlement_footprints() -> void:
	for settlement in _world_data.get("settlements", []):
		var entity_id: String = settlement.get("entity_id", "")
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		if entity_id.is_empty():
			continue
		if _grid_system.can_place_footprint(origin, size):
			_grid_system.register_footprint(entity_id, origin, size)
