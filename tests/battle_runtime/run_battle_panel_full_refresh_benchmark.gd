extends SceneTree

const BATTLE_PANEL_SCENE = preload("res://scenes/ui/battle_map_panel.tscn")
const BATTLE_MAP_PANEL_SCRIPT = preload("res://scripts/ui/battle_map_panel.gd")
const BATTLE_GRID_SERVICE_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_grid_service.gd")
const BATTLE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_state.gd")
const BATTLE_TIMELINE_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_timeline_state.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")

const VIEWPORT_SIZE := Vector2i(1600, 900)
const MAP_SIZE := Vector2i(20, 14)
const ALLY_COUNT := 6
const ENEMY_COUNT := 40
const ITERATIONS := 24
const MAX_READY_FRAMES := 24

var _failures: Array[String] = []
var _grid_service = BATTLE_GRID_SERVICE_SCRIPT.new()


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await _run_async()


func _run_async() -> void:
	root.size = VIEWPORT_SIZE
	var panel := BATTLE_PANEL_SCENE.instantiate() as BATTLE_MAP_PANEL_SCRIPT
	if panel == null:
		push_error("BattlePanelRefreshBenchmark could not instantiate BattleMapPanel.")
		print("Battle panel full refresh benchmark: FAIL (1)")
		quit(1)
		return

	root.add_child(panel)
	await process_frame
	panel.size = Vector2(VIEWPORT_SIZE)
	panel.visible = true

	var state = _build_flat_state(MAP_SIZE)
	_populate_units(state)
	var selected_cycle := _build_selected_cycle(state)
	var valid_target_coords := _collect_all_coords(state)
	var preview_target_coords: Array[Vector2i] = []
	var target_unit_ids: Array[StringName] = []
	if selected_cycle.is_empty():
		_failures.append("BattlePanelRefreshBenchmark could not build a selected cycle.")

	if _failures.is_empty():
		panel.refresh(
			state,
			selected_cycle[0],
			&"",
			"",
			"",
			preview_target_coords,
			valid_target_coords,
			0,
			target_unit_ids
		)
		var warmup_ready := await _wait_for_panel_render_ready(panel)
		if not warmup_ready:
			_failures.append("BattlePanelRefreshBenchmark did not reach render-ready state before timing.")

	var full_refresh := await _run_panel_pass(
		"full_refresh",
		panel,
		state,
		selected_cycle,
		valid_target_coords,
		true
	)
	var overlay_only := await _run_panel_pass(
		"overlay_only",
		panel,
		state,
		selected_cycle,
		valid_target_coords,
		false
	)

	panel.queue_free()
	await process_frame

	if not _failures.is_empty():
		for failure in _failures:
			push_error(failure)
		print("Battle panel full refresh benchmark: FAIL (%d)" % _failures.size())
		quit(1)
		return

	print(_format_result(full_refresh))
	print(_format_result(overlay_only))
	print(_format_comparison(full_refresh, overlay_only))
	print("Battle panel full refresh benchmark: PASS")
	quit(0)


func _run_panel_pass(
	pass_id: String,
	panel,
	state,
	selected_cycle: Array[Vector2i],
	valid_target_coords: Array[Vector2i],
	redraw_board: bool
) -> Dictionary:
	if not panel.is_battle_render_content_ready():
		var baseline_ready := await _wait_for_panel_render_ready(panel)
		if not baseline_ready:
			_failures.append("BattlePanelRefreshBenchmark pass %s started before render content became ready." % pass_id)
			return {
				"pass_id": pass_id,
				"iterations": 0,
				"redraw_board": redraw_board,
				"call_usec": 0,
				"frame_usec": 0,
				"total_usec": 0,
				"valid_coord_count": valid_target_coords.size(),
				"selected_cycle_count": selected_cycle.size(),
			}
	var call_usec := 0
	var frame_usec := 0
	var preview_target_coords: Array[Vector2i] = []
	var target_unit_ids: Array[StringName] = []
	for iteration in range(ITERATIONS):
		var selected_coord := selected_cycle[iteration % selected_cycle.size()]
		var call_start := Time.get_ticks_usec()
		if redraw_board:
			panel.refresh(
				state,
				selected_coord,
				&"",
				"",
				"",
				preview_target_coords,
				valid_target_coords,
				0,
				target_unit_ids
			)
		else:
			panel.refresh_overlay(
				state,
				selected_coord,
				&"",
				"",
				"",
				preview_target_coords,
				valid_target_coords,
				0,
				target_unit_ids
			)
		call_usec += Time.get_ticks_usec() - call_start

		var frame_start := Time.get_ticks_usec()
		var ready := await _wait_for_panel_render_ready(panel)
		frame_usec += Time.get_ticks_usec() - frame_start
		if not ready:
			_failures.append("BattlePanelRefreshBenchmark pass %s iteration %d did not regain render-ready state." % [pass_id, iteration])
			break

	return {
		"pass_id": pass_id,
		"iterations": ITERATIONS,
		"redraw_board": redraw_board,
		"call_usec": call_usec,
		"frame_usec": frame_usec,
		"total_usec": call_usec + frame_usec,
		"valid_coord_count": valid_target_coords.size(),
		"selected_cycle_count": selected_cycle.size(),
	}


func _wait_for_panel_render_ready(panel) -> bool:
	if panel == null:
		return false
	if panel.is_battle_render_content_ready():
		return true
	for _frame in range(MAX_READY_FRAMES):
		await process_frame
		if panel.is_battle_render_content_ready():
			return true
	return false


func _build_flat_state(map_size: Vector2i):
	var state = BATTLE_STATE_SCRIPT.new()
	state.battle_id = &"battle_panel_full_refresh_benchmark"
	state.phase = &"unit_acting"
	state.terrain_profile_id = &"default"
	state.map_size = map_size
	state.timeline = BATTLE_TIMELINE_STATE_SCRIPT.new()
	state.timeline.current_tu = 120
	state.cells = {}
	state.units = {}
	state.log_entries.append("刷新压测：初始化战场。")
	state.log_entries.append("刷新压测：准备执行 BattleMapPanel.refresh().")
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell = BATTLE_CELL_STATE_SCRIPT.new()
			cell.coord = Vector2i(x, y)
			cell.base_terrain = BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND
			cell.base_height = 4
			cell.height_offset = 0
			cell.recalculate_runtime_values()
			state.cells[cell.coord] = cell
	state.cell_columns = BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(state.cells)
	return state


func _populate_units(state) -> void:
	var ally_positions := [
		Vector2i(1, 4),
		Vector2i(1, 6),
		Vector2i(1, 8),
		Vector2i(2, 5),
		Vector2i(2, 7),
		Vector2i(2, 9),
	]
	for index in range(ALLY_COUNT):
		var ally_unit = _build_manual_unit(
			StringName("panel_ally_%02d" % [index + 1]),
			"面板友军%02d" % [index + 1],
			ally_positions[index]
		)
		_add_unit_to_state(state, ally_unit, false)
		if index == 0:
			state.active_unit_id = ally_unit.unit_id

	var enemy_positions: Array[Vector2i] = []
	for y in range(4, 9):
		for x in range(10, 18):
			enemy_positions.append(Vector2i(x, y))
	enemy_positions.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		return left.y < right.y or (left.y == right.y and left.x < right.x)
	)

	for index in range(ENEMY_COUNT):
		var enemy_unit = _build_ai_unit(
			StringName("panel_enemy_%02d" % [index + 1]),
			"面板敌军%02d" % [index + 1],
			enemy_positions[index]
		)
		_add_unit_to_state(state, enemy_unit, true)


func _build_manual_unit(unit_id: StringName, display_name: String, coord: Vector2i):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = &"player"
	unit.control_mode = &"manual"
	unit.current_hp = 280
	unit.current_mp = 60
	unit.current_stamina = 60
	unit.current_aura = 20
	unit.current_ap = 2
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 280)
	unit.attribute_snapshot.set_value(&"mp_max", 60)
	unit.attribute_snapshot.set_value(&"stamina_max", 60)
	unit.attribute_snapshot.set_value(&"aura_max", 20)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 18)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 10)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 22)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 18)
	unit.known_active_skill_ids.append(&"warrior_heavy_strike")
	unit.known_active_skill_ids.append(&"warrior_guard")
	unit.known_active_skill_ids.append(&"charge")
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	return unit


func _build_ai_unit(unit_id: StringName, display_name: String, coord: Vector2i):
	var unit = BATTLE_UNIT_STATE_SCRIPT.new()
	unit.unit_id = unit_id
	unit.display_name = display_name
	unit.faction_id = &"enemy"
	unit.control_mode = &"ai"
	unit.current_hp = 180
	unit.current_mp = 80
	unit.current_stamina = 80
	unit.current_aura = 20
	unit.current_ap = 2
	unit.is_alive = true
	unit.set_anchor_coord(coord)
	unit.attribute_snapshot.set_value(&"hp_max", 180)
	unit.attribute_snapshot.set_value(&"mp_max", 80)
	unit.attribute_snapshot.set_value(&"stamina_max", 80)
	unit.attribute_snapshot.set_value(&"aura_max", 20)
	unit.attribute_snapshot.set_value(&"action_points", 2)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 16)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ATTACK_BONUS, 14)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 16)
	unit.attribute_snapshot.set_value(ATTRIBUTE_SERVICE_SCRIPT.ARMOR_CLASS, 16)
	unit.known_active_skill_ids.append(&"archer_suppressive_fire")
	unit.known_active_skill_ids.append(&"archer_pinning_shot")
	for skill_id in unit.known_active_skill_ids:
		unit.known_skill_level_map[skill_id] = 1
	return unit


func _add_unit_to_state(state, unit, is_enemy: bool) -> void:
	state.units[unit.unit_id] = unit
	if is_enemy:
		state.enemy_unit_ids.append(unit.unit_id)
	else:
		state.ally_unit_ids.append(unit.unit_id)
	var placed: bool = _grid_service.place_unit(state, unit, unit.coord, true)
	if not placed:
		_failures.append("BattlePanelRefreshBenchmark could not place unit %s." % String(unit.unit_id))


func _build_selected_cycle(state) -> Array[Vector2i]:
	var cycle: Array[Vector2i] = []
	for unit_id in state.ally_unit_ids:
		var ally_unit = state.units.get(unit_id)
		if ally_unit != null:
			cycle.append(ally_unit.coord)
	for index in range(mini(6, state.enemy_unit_ids.size())):
		var enemy_unit = state.units.get(state.enemy_unit_ids[index])
		if enemy_unit != null:
			cycle.append(enemy_unit.coord)
	return cycle


func _collect_all_coords(state) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for y in range(state.map_size.y):
		for x in range(state.map_size.x):
			coords.append(Vector2i(x, y))
	return coords


func _format_result(result: Dictionary) -> String:
	var total_usec := float(result.get("total_usec", 0.0))
	var iterations := maxi(int(result.get("iterations", 1)), 1)
	return "[BattlePanelRefreshBenchmark] pass=%s iterations=%d total_ms=%.3f call_ms=%.3f frame_ms=%.3f ms_per_refresh=%.3f valid_coords=%d selected_cycle=%d" % [
		String(result.get("pass_id", "")),
		iterations,
		_usec_to_msec(total_usec),
		_usec_to_msec(float(result.get("call_usec", 0.0))),
		_usec_to_msec(float(result.get("frame_usec", 0.0))),
		_usec_to_msec(total_usec) / float(iterations),
		int(result.get("valid_coord_count", 0)),
		int(result.get("selected_cycle_count", 0)),
	]


func _format_comparison(full_refresh: Dictionary, overlay_only: Dictionary) -> String:
	var full_total := float(full_refresh.get("total_usec", 0.0))
	var overlay_total := float(overlay_only.get("total_usec", 0.0))
	var iterations := maxi(int(full_refresh.get("iterations", 1)), 1)
	var redraw_delta_usec := maxi(int(full_total - overlay_total), 0)
	return "[BattlePanelRefreshBenchmark] comparison redraw_delta_ms=%.3f redraw_delta_ms_per_refresh=%.3f full_refresh_ms_per_iter=%.3f overlay_ms_per_iter=%.3f" % [
		_usec_to_msec(redraw_delta_usec),
		_usec_to_msec(redraw_delta_usec) / float(iterations),
		_usec_to_msec(full_total) / float(iterations),
		_usec_to_msec(overlay_total) / float(iterations),
	]


func _usec_to_msec(value_usec: float) -> float:
	return value_usec / 1000.0
