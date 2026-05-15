class_name BattleMovementService
extends RefCounted

const BATTLE_STATUS_SEMANTIC_TABLE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_status_semantic_table.gd")
const AI_TRACE_RECORDER = preload("res://scripts/dev_tools/ai_trace_recorder.gd")

const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null

func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func _record_action_issued(unit_state: BattleUnitState, command_type: StringName, ap_cost: int = 0) -> void:
	if _runtime == null:
		return
	_runtime._record_action_issued(unit_state, command_type, ap_cost)

func _append_changed_coords(batch: BattleEventBatch, coords: Array[Vector2i]) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_coords(batch, coords)

func _append_changed_unit_coords(batch: BattleEventBatch, unit_state: BattleUnitState) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_unit_coords(batch, unit_state)

func _sort_coords(target_coords: Variant) -> Array[Vector2i]:
	if _runtime == null:
		return []
	return _runtime._sort_coords(target_coords)

func _is_movement_blocked(unit_state: BattleUnitState) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_movement_blocked(unit_state)

func _has_status(unit_state: BattleUnitState, status_id: StringName) -> bool:
	if _runtime == null:
		return false
	return _runtime._has_status(unit_state, status_id)

func get_unit_reachable_move_coords(unit_state: BattleUnitState) -> Array[Vector2i]:
	if _runtime._state == null or unit_state == null or not unit_state.is_alive:
		return []
	if _is_movement_blocked(unit_state):
		return []

	var origin = unit_state.coord
	var max_move_points = _get_available_move_points(unit_state)
	var best_coord_costs = {
		origin: 0,
	}
	var buckets = _build_reachable_move_buckets(max_move_points)
	buckets[0].append({
		"coord": origin,
		"spent_cost": 0,
	})
	for current_cost in range(max_move_points + 1):
		var bucket_index = 0
		while bucket_index < buckets[current_cost].size():
			var frontier_entry: Dictionary = buckets[current_cost][bucket_index]
			bucket_index += 1
			var current_coord: Vector2i = frontier_entry.get("coord", origin)
			var spent_cost = int(frontier_entry.get("spent_cost", current_cost))
			if spent_cost != current_cost:
				continue
			if spent_cost != int(best_coord_costs.get(current_coord, 2147483647)):
				continue
			for neighbor_coord in _runtime._grid_service.get_neighbors_4(_runtime._state, current_coord):
				if not _runtime._grid_service.can_unit_step_between_anchors(_runtime._state, unit_state, current_coord, neighbor_coord):
					continue
				var move_cost = _get_move_cost_for_unit_target(unit_state, neighbor_coord)
				var next_cost = spent_cost + move_cost
				if next_cost > max_move_points:
					continue
				if next_cost >= int(best_coord_costs.get(neighbor_coord, 2147483647)):
					continue
				best_coord_costs[neighbor_coord] = next_cost
				buckets[next_cost].append({
					"coord": neighbor_coord,
					"spent_cost": next_cost,
				})

	best_coord_costs.erase(origin)
	return _sort_coords(_collect_dict_vector2i_keys(best_coord_costs))

func _get_move_cost_for_unit_target(
	unit_state: BattleUnitState,
	target_coord: Vector2i
) -> int:
	if _runtime._state == null or unit_state == null:
		return 1
	var move_cost = _runtime._grid_service.get_unit_move_cost(_runtime._state, unit_state, target_coord)
	if _runtime._terrain_effect_system != null:
		move_cost += _runtime._terrain_effect_system.get_move_cost_delta_for_unit_target(unit_state, target_coord)
	move_cost += _get_status_move_cost_delta(unit_state)
	return move_cost

func _get_move_path_cost(unit_state: BattleUnitState, anchor_path: Array[Vector2i]) -> int:
	if unit_state == null or anchor_path.size() <= 1:
		return 0
	var total_cost = 0
	for path_index in range(1, anchor_path.size()):
		total_cost += _get_move_cost_for_unit_target(unit_state, anchor_path[path_index])
	return total_cost

func _get_status_move_cost_delta(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 0
	var total_delta = 0
	for status_id_str in ProgressionDataUtils.sorted_string_keys(unit_state.status_effects):
		var status_entry = unit_state.get_status_effect(StringName(status_id_str))
		total_delta += BATTLE_STATUS_SEMANTIC_TABLE_SCRIPT.get_move_cost_delta(status_entry)
	return maxi(total_delta, 0)

func _resolve_move_path_result(active_unit: BattleUnitState, target_coord: Vector2i) -> Dictionary:
	if _runtime._state == null or active_unit == null:
		return {
			"allowed": false,
			"cost": 0,
			"path": [],
			"message": "当前单位数据不可用。",
		}
	var available_move_points = _get_available_move_points(active_unit)
	if available_move_points <= 0:
		return {
			"allowed": false,
			"cost": 0,
			"path": [],
			"message": "已行动，移动力被锁定。" if _is_normal_movement_locked(active_unit) else "移动力不足，无法移动。",
		}
	AI_TRACE_RECORDER.enter(&"move_path:grid_resolve")
	var move_result = _runtime._grid_service.resolve_unit_move_path(
		_runtime._state,
		active_unit,
		active_unit.coord,
		target_coord,
		available_move_points,
		Callable(self, "_get_move_cost_for_unit_target")
	)
	AI_TRACE_RECORDER.exit(&"move_path:grid_resolve")
	AI_TRACE_RECORDER.enter(&"move_path:extract_path")
	var anchor_path: Array[Vector2i] = []
	var path_variant = move_result.get("path", [])
	if path_variant is Array:
		for coord_variant in path_variant:
			if coord_variant is Vector2i:
				anchor_path.append(coord_variant)
	AI_TRACE_RECORDER.exit(&"move_path:extract_path")
	if anchor_path.size() > 1:
		AI_TRACE_RECORDER.enter(&"move_path:semantic_cost")
		var semantic_cost = _get_move_path_cost(active_unit, anchor_path)
		move_result["cost"] = semantic_cost
		if semantic_cost > available_move_points:
			move_result["allowed"] = false
			move_result["message"] = "移动力不足，无法移动。"
		AI_TRACE_RECORDER.exit(&"move_path:semantic_cost")
	return move_result

func _get_available_move_points(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 0
	var normal_move_points = maxi(int(unit_state.current_move_points), 0)
	if normal_move_points <= 0:
		return 0
	if not _is_normal_movement_locked(unit_state):
		return normal_move_points
	return normal_move_points if unit_state.can_use_locked_move_points_this_turn else 0

func _is_normal_movement_locked(unit_state: BattleUnitState) -> bool:
	return unit_state != null and (unit_state.has_taken_action_this_turn or unit_state.has_moved_this_turn)

func _handle_move_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void:
	if _is_movement_blocked(active_unit):
		batch.log_lines.append("%s 当前被限制移动。" % active_unit.display_name)
		return
	var target_coord = command.target_coord
	var move_result = _resolve_move_path_result(active_unit, target_coord)
	if not bool(move_result.get("allowed", false)):
		batch.log_lines.append(String(move_result.get("message", "该移动不可执行。")))
		return
	var target_cell = _runtime._grid_service.get_cell(_runtime._state, target_coord)
	if target_cell == null:
		return
	var move_cost = int(move_result.get("cost", 0))
	var anchor_path: Array[Vector2i] = []
	for coord_variant in move_result.get("path", []):
		if coord_variant is Vector2i:
			anchor_path.append(coord_variant)

	var previous_anchor = active_unit.coord
	var previous_coords = active_unit.occupied_coords.duplicate()
	var execution_result := _move_unit_along_validated_path_result(active_unit, anchor_path, target_coord, batch)
	if bool(execution_result.get("executed", false)):
		var executed_path: Array[Vector2i] = []
		for executed_coord_variant in execution_result.get("executed_path", []):
			if executed_coord_variant is Vector2i:
				executed_path.append(executed_coord_variant)
		move_cost = _get_move_path_cost(active_unit, executed_path)
		active_unit.current_move_points = maxi(active_unit.current_move_points - move_cost, 0)
		_record_action_issued(active_unit, BattleCommand.TYPE_MOVE)
		batch.changed_unit_ids.append(active_unit.unit_id)
		_append_changed_coords(batch, previous_coords)
		_append_changed_unit_coords(batch, active_unit)
		target_cell = _runtime._grid_service.get_cell(_runtime._state, active_unit.coord)
		var terrain_name = _runtime._grid_service.get_terrain_display_name(String(target_cell.base_terrain)) if target_cell != null else "地格"
		batch.log_lines.append("%s 从 (%d, %d) 移动到 (%d, %d)，移动距离消耗 %d 点，剩余移动力 %d 点并锁定。%s。" % [
			active_unit.display_name,
			previous_anchor.x,
			previous_anchor.y,
			active_unit.coord.x,
			active_unit.coord.y,
			move_cost,
			active_unit.current_move_points,
			terrain_name,
		])
		if bool(execution_result.get("stopped_by_barrier", false)):
			batch.log_lines.append("%s 的移动被屏障拦下，停在当前可达位置。" % active_unit.display_name)
	else:
		batch.log_lines.append("%s 的移动落点已失效，无法执行。" % active_unit.display_name)

func _move_unit_along_validated_path(
	active_unit: BattleUnitState,
	anchor_path: Array[Vector2i],
	target_coord: Vector2i,
	batch: BattleEventBatch
) -> bool:
	return bool(_move_unit_along_validated_path_result(active_unit, anchor_path, target_coord, batch).get("reached_target", false))

func _move_unit_along_validated_path_result(
	active_unit: BattleUnitState,
	anchor_path: Array[Vector2i],
	target_coord: Vector2i,
	batch: BattleEventBatch
) -> Dictionary:
	var result := {
		"executed": false,
		"reached_target": false,
		"stopped_by_barrier": false,
		"executed_path": [],
	}
	if active_unit == null:
		return result
	if anchor_path.is_empty():
		return result
	if anchor_path[0] != active_unit.coord or anchor_path[anchor_path.size() - 1] != target_coord:
		return result
	result["executed_path"] = [active_unit.coord]
	if anchor_path.size() == 1:
		result["executed"] = active_unit.coord == target_coord
		result["reached_target"] = active_unit.coord == target_coord
		return result
	for path_index in range(1, anchor_path.size()):
		var next_coord = anchor_path[path_index]
		if not _runtime._grid_service.can_unit_step_between_anchors(_runtime._state, active_unit, active_unit.coord, next_coord):
			if batch != null:
				batch.log_lines.append("%s 的移动路径第 %d 步已不可通行。" % [active_unit.display_name, path_index])
			return result
		var barrier_result: Dictionary = _runtime._layered_barrier_service.resolve_unit_boundary_crossing(active_unit, active_unit.coord, next_coord, batch) if _runtime._layered_barrier_service != null else {}
		if bool(barrier_result.get("blocked", false)) or not active_unit.is_alive or active_unit.coord != anchor_path[path_index - 1]:
			result["executed"] = bool(result["executed"]) or bool(barrier_result.get("applied", false)) or (result["executed_path"] as Array).size() > 1
			result["stopped_by_barrier"] = bool(barrier_result.get("blocked", false))
			return result
		if not _runtime._grid_service.move_unit(_runtime._state, active_unit, next_coord):
			if batch != null:
				batch.log_lines.append("%s 的移动路径第 %d 步执行失败。" % [active_unit.display_name, path_index])
			return result
		result["executed"] = true
		(result["executed_path"] as Array).append(active_unit.coord)
	result["reached_target"] = active_unit.coord == target_coord
	return result

func _collect_dict_vector2i_keys(values: Dictionary) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord_variant in values.keys():
		if coord_variant is Vector2i:
			coords.append(coord_variant)
	return coords

func _build_reachable_move_buckets(max_move_points: int) -> Array:
	var bucket_count = maxi(max_move_points, 0) + 1
	var buckets: Array = []
	buckets.resize(bucket_count)
	for bucket_index in range(bucket_count):
		buckets[bucket_index] = []
	return buckets
