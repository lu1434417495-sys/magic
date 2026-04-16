extends SceneTree

const BattleBoard2D = preload("res://scripts/ui/battle_board_2d.gd")
const BattleBoardScene = preload("res://scenes/ui/battle_board_2d.tscn")
const BattleHudAdapter = preload("res://scripts/ui/battle_hud_adapter.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")
const BattleMapPanel = preload("res://scripts/ui/battle_map_panel.gd")
const BattlePanelScene = preload("res://scenes/ui/battle_map_panel.tscn")

const VIEWPORT_SIZE := Vector2(1280.0, 720.0)
const ULTRAWIDE_PANEL_SIZE := Vector2i(3857, 786)

var _failures: Array[String] = []


class MockGameSession:
	extends Node

	var skill_defs: Dictionary = {}

	func get_skill_defs() -> Dictionary:
		return skill_defs


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _test_multi_unit_hud_copy_and_selection_state()
	await _test_multi_unit_board_highlights_confirm_state()
	await _test_multi_unit_board_confirm_halo_follows_active_unit()
	await _test_multi_unit_board_highlights_continue_state()
	await _test_movement_mode_uses_classic_srpg_style_markers()
	await _test_battle_panel_flushes_to_ultrawide_edges()
	await _test_battle_panel_loading_overlay_waits_for_first_presented_frame()
	if _failures.is_empty():
		print("Battle UI regression: PASS")
		quit(0)
		return
	for failure in _failures:
		push_error(failure)
	print("Battle UI regression: FAIL (%d)" % _failures.size())
	quit(1)


func _test_multi_unit_hud_copy_and_selection_state() -> void:
	var game_session := await _install_mock_game_session()
	game_session.skill_defs = {
		&"archer_multishot": _build_multi_unit_skill_def(),
	}
	var adapter := BattleHudAdapter.new()
	var state := _build_state()
	var snapshot := adapter.build_snapshot(
		state,
		Vector2i(0, 0),
		&"archer_multishot",
		"连珠箭",
		"",
		[Vector2i(1, 1), Vector2i(2, 1)],
		3
	)
	_assert_eq(snapshot.get("selected_skill_target_selection_mode", ""), "multi_unit", "multi_unit 技能应暴露目标选择模式。")
	_assert_eq(int(snapshot.get("selected_skill_target_min_count", 0)), 2, "multi_unit 技能应暴露最小目标数量。")
	_assert_eq(int(snapshot.get("selected_skill_target_max_count", 0)), 3, "multi_unit 技能应暴露最大目标数量。")
	_assert_true(String(snapshot.get("skill_subtitle", "")).contains("已满足最小数量"), "multi_unit HUD 副标题应提示确认态。")
	_assert_true(String(snapshot.get("hint_text", "")).contains("点击自己或空地确认"), "multi_unit HUD 提示应说明确认路径。")
	_assert_true(String(snapshot.get("command_text", "")).contains("可点击自己或空地确认"), "multi_unit 命令摘要应说明确认路径。")
	game_session.queue_free()
	await process_frame


func _test_multi_unit_board_highlights_confirm_state() -> void:
	var board := await _instantiate_board()
	var state := _build_state()
	board.configure(
		state,
		Vector2i(0, 0),
		[Vector2i(1, 1), Vector2i(2, 1)],
		[Vector2i(3, 1)],
		&"multi_unit",
		2,
		3
	)
	await process_frame
	var highlight_layer := board.get_node("TargetHighlightLayer")
	var highlight_names := _collect_node_names(highlight_layer)
	_assert_true(highlight_names.has("LockedTarget_1_1"), "锁定目标应有独立高亮节点。")
	_assert_true(highlight_names.has("LockedTarget_2_1"), "第二个锁定目标应有独立高亮节点。")
	_assert_true(highlight_names.has("ValidTarget_3_1"), "可选目标应有独立高亮节点。")
	_assert_true(highlight_names.has("ConfirmReady_0_0"), "满足最小数量时应显示确认态高亮。")
	board.queue_free()
	await process_frame


func _test_multi_unit_board_confirm_halo_follows_active_unit() -> void:
	var board := await _instantiate_board()
	var state := _build_state()
	board.configure(
		state,
		Vector2i(2, 1),
		[Vector2i(1, 1), Vector2i(2, 1)],
		[Vector2i(3, 1)],
		&"multi_unit",
		2,
		3
	)
	await process_frame
	var highlight_layer := board.get_node("TargetHighlightLayer")
	var highlight_names := _collect_node_names(highlight_layer)
	_assert_true(highlight_names.has("ConfirmReady_0_0"), "确认态 halo 应始终指向当前行动单位，而不是最后一个锁定目标。")
	_assert_true(not highlight_names.has("ConfirmReady_2_1"), "确认态 halo 不应继续画在会触发取消选择的锁定目标上。")
	board.queue_free()
	await process_frame


func _test_multi_unit_board_highlights_continue_state() -> void:
	var board := await _instantiate_board()
	var state := _build_state()
	board.configure(
		state,
		Vector2i(0, 0),
		[Vector2i(1, 1)],
		[Vector2i(2, 1)],
		&"multi_unit",
		2,
		3
	)
	await process_frame
	var highlight_layer := board.get_node("TargetHighlightLayer")
	var highlight_names := _collect_node_names(highlight_layer)
	_assert_true(highlight_names.has("LockedTarget_1_1"), "继续选目标时仍应显示已锁定目标。")
	_assert_true(highlight_names.has("ValidTarget_2_1"), "继续选目标时仍应显示可选目标。")
	_assert_true(not highlight_names.has("ConfirmReady_0_0"), "未达到最小数量时不应显示确认态高亮。")
	board.queue_free()
	await process_frame


func _test_movement_mode_uses_classic_srpg_style_markers() -> void:
	var board := await _instantiate_board()
	var state := _build_state()
	board.configure(
		state,
		Vector2i(0, 0),
		[],
		[Vector2i(1, 0), Vector2i(0, 1)],
		&"movement",
		1,
		1
	)
	await process_frame

	var highlight_layer := board.get_node("TargetHighlightLayer")
	_assert_eq(
		_collect_node_names(highlight_layer).size(),
		0,
		"movement 模式不应复用顶层红色目标高亮节点。"
	)
	var marker_layer := board.get_node("MarkerH0") as TileMapLayer
	var reachable_image := _get_layer_cell_image(marker_layer, Vector2i(1, 0))
	_assert_true(reachable_image != null, "movement 模式应在 Marker 层渲染可达地格。")
	if reachable_image != null:
		var center := reachable_image.get_pixel(reachable_image.get_width() / 2, reachable_image.get_height() / 2)
		_assert_true(center.a >= 0.28 and center.a <= 0.52, "可达地格中心像素应保持更清晰的半透明高亮。")
		_assert_true(
			center.b > center.g and center.g > center.r,
			"可达地格应保持偏青蓝的可行走提示色。"
		)

	board.queue_free()
	await process_frame


func _test_battle_panel_flushes_to_ultrawide_edges() -> void:
	root.size = ULTRAWIDE_PANEL_SIZE
	var panel := BattlePanelScene.instantiate() as BattleMapPanel
	root.add_child(panel)
	await process_frame
	panel.size = Vector2(ULTRAWIDE_PANEL_SIZE)
	panel.show_battle(_build_state(), Vector2i(0, 0))
	await process_frame

	var map_frame_rect := panel.map_frame.get_global_rect()
	var top_bar_rect := panel.top_bar.get_global_rect()
	var bottom_panel_rect := panel.bottom_panel.get_global_rect()
	_assert_eq(map_frame_rect.position.x, 0.0, "BattleMapPanel 的 MapFrame 左边界应贴齐父窗口。")
	_assert_eq(map_frame_rect.size.x, float(ULTRAWIDE_PANEL_SIZE.x), "BattleMapPanel 的 MapFrame 宽度应与父窗口一致。")
	_assert_eq(top_bar_rect.position.x, 0.0, "BattleMapPanel 的 TopBar 左边界应贴齐父窗口。")
	_assert_eq(top_bar_rect.size.x, float(ULTRAWIDE_PANEL_SIZE.x), "BattleMapPanel 的 TopBar 宽度应与父窗口一致。")
	_assert_eq(bottom_panel_rect.position.x, 0.0, "BattleMapPanel 的 BottomPanel 左边界应贴齐父窗口。")
	_assert_eq(bottom_panel_rect.size.x, float(ULTRAWIDE_PANEL_SIZE.x), "BattleMapPanel 的 BottomPanel 宽度应与父窗口一致。")

	panel.queue_free()
	await process_frame
	root.size = Vector2i(VIEWPORT_SIZE)


func _test_battle_panel_loading_overlay_waits_for_first_presented_frame() -> void:
	root.size = Vector2i(VIEWPORT_SIZE)
	var panel := BattlePanelScene.instantiate() as BattleMapPanel
	root.add_child(panel)
	await process_frame
	panel.size = VIEWPORT_SIZE
	var state := _build_state()
	state.battle_id = &"battle_ui_loading_overlay"
	panel.show_battle(state, Vector2i(0, 0))
	_assert_true(panel.is_loading_battle(), "loading 遮罩展示期间应保持 battle 输入锁定。")
	_assert_true(panel.get_loading_progress() > 0.0, "新 battle 进入时应推进 loading 进度。")

	await process_frame
	_assert_true(panel.is_battle_render_content_ready(), "loading 期间 battle 棋盘内容应先达到完整渲染态。")

	await process_frame
	_assert_true(panel.is_loading_battle(), "最短 loading 时长内仍应维持 loading 状态。")

	await _wait_seconds(0.5)
	await process_frame
	_assert_true(panel.visible, "首帧渲染完成后才应显示 battle 面板。")
	_assert_true(not panel.is_loading_battle(), "首帧渲染完成后应解除 battle 输入锁定。")

	panel.show_battle(state, Vector2i(0, 0))
	_assert_true(panel.visible, "同一 battle 的后续全量刷新不应重新隐藏 battle 面板。")

	panel.queue_free()
	await process_frame


func _install_mock_game_session() -> MockGameSession:
	for child in root.get_children():
		if child.name == "GameSession":
			child.queue_free()
	await process_frame
	var game_session := MockGameSession.new()
	game_session.name = "GameSession"
	root.add_child(game_session)
	await process_frame
	return game_session


func _build_multi_unit_skill_def() -> SkillDef:
	var skill_def := SkillDef.new()
	skill_def.skill_id = &"archer_multishot"
	skill_def.display_name = "连珠箭"
	skill_def.combat_profile = CombatSkillDef.new()
	skill_def.combat_profile.skill_id = skill_def.skill_id
	skill_def.combat_profile.target_selection_mode = &"multi_unit"
	skill_def.combat_profile.min_target_count = 2
	skill_def.combat_profile.max_target_count = 3
	return skill_def


func _build_state() -> BattleState:
	var state := BattleState.new()
	state.battle_id = &"battle_ui_regression"
	state.map_size = Vector2i(4, 4)
	state.terrain_profile_id = &"default"
	state.cells = {}
	for y in range(4):
		for x in range(4):
			state.cells[Vector2i(x, y)] = _build_cell(Vector2i(x, y))
	state.ally_unit_ids = []
	state.enemy_unit_ids = []
	var unit := BattleUnitState.new()
	unit.unit_id = &"ally_ui"
	unit.display_name = "我方"
	unit.faction_id = &"player"
	unit.is_alive = true
	unit.current_hp = 10
	unit.current_mp = 0
	unit.current_ap = 4
	unit.known_active_skill_ids = [&"archer_multishot"]
	unit.refresh_footprint()
	state.units = {
		unit.unit_id: unit,
	}
	state.ally_unit_ids.append(unit.unit_id)
	state.active_unit_id = unit.unit_id
	return state


func _build_cell(coord: Vector2i) -> BattleCellState:
	var cell := BattleCellState.new()
	cell.coord = coord
	cell.stack_layer = 0
	cell.base_height = 0
	cell.base_terrain = BattleCellState.TERRAIN_LAND
	cell.recalculate_runtime_values()
	return cell


func _instantiate_board() -> BattleBoard2D:
	var board := BattleBoardScene.instantiate() as BattleBoard2D
	root.add_child(board)
	await process_frame
	board.set_viewport_size(VIEWPORT_SIZE)
	return board


func _collect_node_names(node: Node) -> Array[String]:
	var names: Array[String] = []
	if node == null:
		return names
	for child in node.get_children():
		names.append(child.name)
	return names


func _get_layer_cell_image(layer: TileMapLayer, coord: Vector2i) -> Image:
	if layer == null or layer.tile_set == null:
		return null
	var source_id := layer.get_cell_source_id(coord)
	if source_id < 0:
		return null
	var atlas_source := layer.tile_set.get_source(source_id) as TileSetAtlasSource
	if atlas_source == null or atlas_source.texture == null:
		return null
	return atlas_source.texture.get_image()


func _wait_seconds(duration_seconds: float) -> void:
	var target_time_msec := Time.get_ticks_msec() + int(round(duration_seconds * 1000.0))
	while Time.get_ticks_msec() < target_time_msec:
		await process_frame


func _assert_true(condition: bool, message: String) -> void:
	if not condition:
		_failures.append(message)


func _assert_eq(actual, expected, message: String) -> void:
	if actual != expected:
		_failures.append("%s | actual=%s expected=%s" % [message, str(actual), str(expected)])
