class_name MoveToRangeAction
extends "res://scripts/enemies/enemy_ai_action.gd"

@export var target_selector: StringName = &"nearest_enemy"
@export var desired_min_distance := 1
@export var desired_max_distance := 1


func decide(context):
	var action_trace := _begin_action_trace(context, {
		"action_kind": "move_to_range",
		"target_selector": String(target_selector),
		"desired_min_distance": desired_min_distance,
		"desired_max_distance": desired_max_distance,
	})
	var targets = _sort_target_units(context, &"enemy", target_selector)
	if targets.is_empty():
		_trace_add_block_reason(action_trace, "no_valid_targets")
		_finalize_action_trace(context, action_trace)
		return null
	var focus_target = targets[0] as BattleUnitState
	var current_score_input = _build_action_score_input(
		context,
		&"move",
		String(action_id),
		null,
		null,
		{
			"position_target_unit": focus_target,
			"position_anchor_coord": context.unit_state.coord,
			"desired_min_distance": desired_min_distance,
			"desired_max_distance": desired_max_distance,
			"position_objective_kind": &"distance_band_progress",
			"move_cost": 0,
		}
	)
	var path_progress_decision = _build_path_progress_decision(context, focus_target, action_trace)
	if path_progress_decision != null:
		_finalize_action_trace(context, action_trace, path_progress_decision)
		return path_progress_decision
	var best_decision = null
	var best_score_input = current_score_input
	for neighbor in _collect_reachable_move_candidates(context):
		_trace_count_increment(action_trace, "evaluation_count", 1)
		var command = _build_move_command(context, neighbor)
		var preview = context.preview_command(command)
		if preview == null or not bool(preview.allowed):
			_trace_count_increment(action_trace, "preview_reject_count", 1)
			continue
		var score_input = _build_action_score_input(
			context,
			&"move",
			String(action_id),
			command,
			preview,
			{
				"position_target_unit": focus_target,
				"position_anchor_coord": neighbor,
				"desired_min_distance": desired_min_distance,
				"desired_max_distance": desired_max_distance,
				"position_objective_kind": &"distance_band_progress",
			}
		)
		_trace_offer_candidate(action_trace, _build_candidate_summary(
			"move_to_%d_%d" % [neighbor.x, neighbor.y],
			command,
			score_input,
			{
				"predicted_distance": score_input.distance_to_primary_coord if score_input != null else -1,
			}
		))
		if not _is_better_move_to_range_score_input(score_input, best_score_input):
			continue
		best_score_input = score_input
		best_decision = _create_scored_decision(
			command,
			score_input,
			"%s 准备调整到距离 %s %d 格（评分 %d）。" % [
				context.unit_state.display_name,
				focus_target.display_name,
				int(score_input.distance_to_primary_coord),
				int(score_input.total_score),
			]
		)
	if best_decision == null:
		best_decision = _build_path_progress_decision(context, focus_target, action_trace)
	_finalize_action_trace(context, action_trace, best_decision)
	return best_decision


func _collect_reachable_move_candidates(context) -> Array[Vector2i]:
	var candidates: Array[Vector2i] = []
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return candidates
	var seen: Dictionary = {}
	var origin: Vector2i = context.unit_state.coord
	var max_move_points := maxi(int(context.unit_state.current_move_points), 0)
	var frontier: Array = [{
		"coord": origin,
		"cost": 0,
	}]
	var best_costs := {
		origin: 0,
	}
	while not frontier.is_empty():
		var entry: Dictionary = frontier.pop_front()
		var current_coord: Vector2i = entry.get("coord", origin)
		var current_cost := int(entry.get("cost", 0))
		if current_cost != int(best_costs.get(current_coord, 2147483647)):
			continue
		for neighbor in context.grid_service.get_neighbors_4(context.state, current_coord):
			if not context.grid_service.can_unit_step_between_anchors(context.state, context.unit_state, current_coord, neighbor):
				continue
			var next_cost: int = current_cost + int(context.grid_service.get_unit_move_cost(context.state, context.unit_state, neighbor))
			if next_cost > max_move_points:
				continue
			if next_cost >= int(best_costs.get(neighbor, 2147483647)):
				continue
			best_costs[neighbor] = next_cost
			frontier.append({
				"coord": neighbor,
				"cost": next_cost,
			})
			if not seen.has(neighbor):
				seen[neighbor] = true
				candidates.append(neighbor)
	candidates.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		var left_distance: int = int(context.grid_service.get_distance(origin, left))
		var right_distance: int = int(context.grid_service.get_distance(origin, right))
		if left_distance == right_distance:
			return left.y < right.y or (left.y == right.y and left.x < right.x)
		return left_distance > right_distance
	)
	return candidates


func _build_path_progress_decision(context, focus_target: BattleUnitState, action_trace: Dictionary):
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return null
	if focus_target == null or int(context.unit_state.current_move_points) <= 0:
		return null
	var current_distance := _distance_from_anchor_to_unit(context, context.unit_state, context.unit_state.coord, focus_target)
	if current_distance >= desired_min_distance and current_distance <= desired_max_distance:
		return null
	var best_decision = null
	var best_score_input = null
	var best_path_cost := 2147483647
	var best_path_length := 2147483647
	for destination in _collect_distance_band_destinations(context, focus_target):
		var path_result: Dictionary = context.grid_service.resolve_unit_move_path(
			context.state,
			context.unit_state,
			context.unit_state.coord,
			destination,
			_build_path_search_budget(context)
		)
		if not bool(path_result.get("allowed", false)):
			continue
		var path: Array[Vector2i] = _extract_vector2i_path(path_result.get("path", []))
		var move_target := _resolve_current_turn_path_target(context, path)
		if move_target == context.unit_state.coord:
			continue
		var command = _build_move_command(context, move_target)
		var preview = context.preview_command(command)
		if preview == null or not bool(preview.allowed):
			_trace_count_increment(action_trace, "preview_reject_count", 1)
			continue
		var path_cost := int(path_result.get("cost", 0))
		var path_length := path.size()
		var score_input = _build_action_score_input(
			context,
			&"move",
			String(action_id),
			command,
			preview,
			{
				"position_target_unit": focus_target,
				"position_anchor_coord": move_target,
				"desired_min_distance": desired_min_distance,
				"desired_max_distance": desired_max_distance,
				"position_objective_kind": &"distance_band_progress",
				"action_base_score": 60,
			}
		)
		_trace_offer_candidate(action_trace, _build_candidate_summary(
			"path_to_%d_%d_via_%d_%d" % [destination.x, destination.y, move_target.x, move_target.y],
			command,
			score_input,
			{
				"path_cost": path_cost,
				"path_length": path_length,
				"path_destination": destination,
			}
		))
		if best_decision != null:
			if path_cost > best_path_cost:
				continue
			if path_cost == best_path_cost and path_length >= best_path_length:
				continue
		best_path_cost = path_cost
		best_path_length = path_length
		best_score_input = score_input
		best_decision = _create_scored_decision(
			command,
			score_input,
			"%s 准备绕路逼近 %s（路径成本 %d，评分 %d）。" % [
				context.unit_state.display_name,
				focus_target.display_name,
				path_cost,
				int(score_input.total_score) if score_input != null else 0,
			]
		)
	return best_decision


func _collect_distance_band_destinations(context, focus_target: BattleUnitState) -> Array[Vector2i]:
	var destinations: Array[Vector2i] = []
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null or focus_target == null:
		return destinations
	var max_distance := maxi(desired_max_distance, desired_min_distance)
	var seen: Dictionary = {}
	focus_target.refresh_footprint()
	for occupied_coord in focus_target.occupied_coords:
		for y in range(occupied_coord.y - max_distance, occupied_coord.y + max_distance + 1):
			for x in range(occupied_coord.x - max_distance, occupied_coord.x + max_distance + 1):
				var coord := Vector2i(x, y)
				if seen.has(coord):
					continue
				seen[coord] = true
				if not context.grid_service.is_inside(context.state, coord):
					continue
				var distance := _distance_from_anchor_to_unit(context, context.unit_state, coord, focus_target)
				if distance < desired_min_distance or distance > desired_max_distance:
					continue
				destinations.append(coord)
	destinations.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		var left_distance: int = int(context.grid_service.get_distance(context.unit_state.coord, left))
		var right_distance: int = int(context.grid_service.get_distance(context.unit_state.coord, right))
		if left_distance == right_distance:
			return left.y < right.y or (left.y == right.y and left.x < right.x)
		return left_distance < right_distance
	)
	return destinations


func _resolve_current_turn_path_target(context, path: Array[Vector2i]) -> Vector2i:
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return Vector2i(-1, -1)
	if path.size() <= 1:
		return context.unit_state.coord
	var spent_cost := 0
	var max_move_points := maxi(int(context.unit_state.current_move_points), 0)
	var best_coord: Vector2i = context.unit_state.coord
	for path_index in range(1, path.size()):
		var next_coord: Vector2i = path[path_index]
		var step_cost: int = int(context.grid_service.get_unit_move_cost(context.state, context.unit_state, next_coord))
		if spent_cost + step_cost > max_move_points:
			break
		spent_cost += step_cost
		best_coord = next_coord
	return best_coord


func _extract_vector2i_path(path_variant: Variant) -> Array[Vector2i]:
	var path: Array[Vector2i] = []
	if path_variant is not Array:
		return path
	for coord_variant in path_variant:
		if coord_variant is Vector2i:
			path.append(coord_variant)
	return path


func _build_path_search_budget(context) -> int:
	if context == null or context.state == null:
		return 32
	var map_size: Vector2i = context.state.map_size
	return maxi(map_size.x * map_size.y, map_size.x + map_size.y)


func _is_better_move_to_range_score_input(candidate, best_candidate) -> bool:
	if candidate == null:
		return false
	if best_candidate == null:
		return true
	var candidate_gap: int = _get_score_input_distance_gap(candidate)
	var best_gap: int = _get_score_input_distance_gap(best_candidate)
	if candidate_gap != best_gap:
		if candidate_gap < 0:
			return false
		if best_gap < 0:
			return true
		return candidate_gap < best_gap
	return _is_better_skill_score_input(candidate, best_candidate)


func _get_score_input_distance_gap(score_input) -> int:
	if score_input == null:
		return -1
	var distance_value: int = int(score_input.distance_to_primary_coord)
	var min_distance: int = int(score_input.desired_min_distance)
	var max_distance: int = int(score_input.desired_max_distance)
	if distance_value < 0 or min_distance < 0 or max_distance < min_distance:
		return -1
	if distance_value < min_distance:
		return min_distance - distance_value
	if distance_value > max_distance:
		return distance_value - max_distance
	return 0


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if target_selector == &"":
		errors.append("MoveToRangeAction %s is missing target_selector." % String(action_id))
	if desired_min_distance < 0:
		errors.append("MoveToRangeAction %s desired_min_distance must be >= 0." % String(action_id))
	if desired_max_distance < desired_min_distance:
		errors.append("MoveToRangeAction %s desired_max_distance must be >= desired_min_distance." % String(action_id))
	return errors
