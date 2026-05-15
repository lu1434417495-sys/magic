class_name MoveToAdvantagePositionAction
extends "res://scripts/enemies/enemy_ai_action.gd"

const MODE_ADVANTAGE: StringName = &"advantage"
const MODE_SURVIVAL: StringName = &"survival"
const MODE_HIGH_GROUND: StringName = &"high_ground"

@export var target_selector: StringName = &"nearest_enemy"
@export var desired_min_distance := 3
@export var desired_max_distance := 5
@export var range_skill_ids: Array[StringName] = []
@export var minimum_safe_distance := 3
@export var safe_distance_margin := 1
@export var positioning_mode: StringName = MODE_ADVANTAGE
@export var high_ground_weight := 60
@export var safety_weight := 50
@export var distance_band_weight := 20
@export var candidate_limit := 96


func decide(context):
	AI_TRACE_RECORDER.enter(&"decide:move_to_advantage_position")
	var result = _decide_impl(context)
	AI_TRACE_RECORDER.exit(&"decide:move_to_advantage_position")
	return result


func _decide_impl(context):
	var distance_contract := _resolve_desired_distance_contract(context, null, range_skill_ids)
	var action_trace := _begin_action_trace(context, {
		"action_kind": "move_to_advantage_position",
		"target_selector": String(target_selector),
		"desired_min_distance": int(distance_contract.get("desired_min_distance", desired_min_distance)),
		"desired_max_distance": int(distance_contract.get("desired_max_distance", desired_max_distance)),
		"configured_desired_min_distance": desired_min_distance,
		"configured_desired_max_distance": desired_max_distance,
		"effective_attack_range": int(distance_contract.get("effective_attack_range", -1)),
		"range_skill_ids": range_skill_ids.duplicate(),
		"minimum_safe_distance": minimum_safe_distance,
		"safe_distance_margin": safe_distance_margin,
		"positioning_mode": String(positioning_mode),
		"high_ground_weight": high_ground_weight,
		"safety_weight": safety_weight,
		"distance_band_weight": distance_band_weight,
	})
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		_trace_add_block_reason(action_trace, "missing_context")
		_finalize_action_trace(context, action_trace)
		return null
	var targets = _sort_target_units(context, &"enemy", target_selector)
	if targets.is_empty():
		_trace_add_block_reason(action_trace, "no_valid_targets")
		_finalize_action_trace(context, action_trace)
		return null
	var focus_target = targets[0] as BattleUnitState
	var current_metrics := _build_anchor_metrics(context, context.unit_state.coord, targets, focus_target, distance_contract)
	if positioning_mode == MODE_SURVIVAL and int(current_metrics.get("unsafe_gap", 0)) <= 0:
		_trace_add_block_reason(action_trace, "already_safe")
		_finalize_action_trace(context, action_trace)
		return null

	var best_decision: BattleAiDecision = null
	var best_score_input = null
	var best_metrics: Dictionary = {}
	for destination in _collect_reachable_move_candidates(context):
		_trace_count_increment(action_trace, "evaluation_count", 1)
		var command = _build_move_command(context, destination)
		var preview = context.preview_command(command)
		if preview == null or not bool(preview.allowed):
			_trace_count_increment(action_trace, "preview_reject_count", 1)
			continue
		var candidate_metrics := _build_anchor_metrics(context, destination, targets, focus_target, distance_contract)
		var improvement := _build_improvement_metrics(current_metrics, candidate_metrics)
		var reject_reason := _get_reject_reason(current_metrics, candidate_metrics, improvement)
		if not reject_reason.is_empty():
			_trace_add_block_reason(action_trace, reject_reason)
			continue
		var score_input = _build_action_score_input(
			context,
			&"move",
			String(action_id),
			command,
			preview,
			{
				"position_target_unit": focus_target,
				"position_anchor_coord": destination,
				"position_current_distance": int(current_metrics.get("nearest_distance", -1)),
				"position_safe_distance": int(current_metrics.get("nearest_safe_distance", -1)),
				"desired_min_distance": int(distance_contract.get("desired_min_distance", desired_min_distance)),
				"desired_max_distance": int(distance_contract.get("desired_max_distance", desired_max_distance)),
				"position_objective_kind": &"distance_band_progress",
			}
		)
		if score_input == null:
			continue
		_apply_advantage_score(score_input, improvement)
		_trace_offer_candidate(action_trace, _build_candidate_summary(
			"advantage_to_%d_%d" % [destination.x, destination.y],
			command,
			score_input,
			{
				"current_height": int(current_metrics.get("height", 0)),
				"candidate_height": int(candidate_metrics.get("height", 0)),
				"height_gain": int(improvement.get("height_gain", 0)),
				"current_nearest_distance": int(current_metrics.get("nearest_distance", 999999)),
				"candidate_nearest_distance": int(candidate_metrics.get("nearest_distance", 999999)),
				"safety_gain": int(improvement.get("safety_gain", 0)),
				"band_gain": int(improvement.get("band_gain", 0)),
				"current_unsafe_gap": int(current_metrics.get("unsafe_gap", 0)),
				"candidate_unsafe_gap": int(candidate_metrics.get("unsafe_gap", 0)),
				"candidate_nearest_safe_distance": int(candidate_metrics.get("nearest_safe_distance", minimum_safe_distance)),
				"max_threat_attack_range": int(candidate_metrics.get("max_threat_attack_range", -1)),
			}
		))
		if not _is_better_advantage_candidate(score_input, candidate_metrics, best_score_input, best_metrics):
			continue
		best_score_input = score_input
		best_metrics = candidate_metrics
		best_decision = _create_scored_decision(
			command,
			score_input,
			"%s 准备移动到更安全的高位（高度 %d，最近敌距 %d，评分 %d）。" % [
				context.unit_state.display_name,
				int(candidate_metrics.get("height", 0)),
				int(candidate_metrics.get("nearest_distance", 999999)),
				int(score_input.total_score),
			]
		)
	_finalize_action_trace(context, action_trace, best_decision)
	return best_decision


func _collect_reachable_move_candidates(context) -> Array[Vector2i]:
	var candidates: Array[Vector2i] = []
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return candidates
	var origin: Vector2i = context.unit_state.coord
	var max_move_points := maxi(int(context.unit_state.current_move_points), 0)
	if max_move_points <= 0:
		return candidates
	var seen: Dictionary = {}
	var frontier: Array = [{
		"coord": origin,
		"cost": 0,
	}]
	var best_costs := {
		origin: 0,
	}
	while not frontier.is_empty() and candidates.size() < maxi(candidate_limit, 1):
		var entry: Dictionary = frontier.pop_front()
		var current_coord: Vector2i = entry.get("coord", origin)
		var current_cost := int(entry.get("cost", 0))
		if current_cost != int(best_costs.get(current_coord, 2147483647)):
			continue
		for neighbor in context.grid_service.get_neighbors_4(context.state, current_coord):
			if not context.grid_service.can_unit_step_between_anchors(context.state, context.unit_state, current_coord, neighbor):
				continue
			var next_cost: int = current_cost + int(context.get_move_cost(context.unit_state, neighbor))
			if next_cost > max_move_points:
				continue
			if next_cost >= int(best_costs.get(neighbor, 2147483647)):
				continue
			best_costs[neighbor] = next_cost
			frontier.append({
				"coord": neighbor,
				"cost": next_cost,
			})
			if seen.has(neighbor):
				continue
			seen[neighbor] = true
			candidates.append(neighbor)
	candidates.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		var left_height := _get_anchor_height(context, left)
		var right_height := _get_anchor_height(context, right)
		if left_height != right_height:
			return left_height > right_height
		var left_distance: int = int(context.grid_service.get_distance(origin, left))
		var right_distance: int = int(context.grid_service.get_distance(origin, right))
		if left_distance == right_distance:
			return left.y < right.y or (left.y == right.y and left.x < right.x)
		return left_distance > right_distance
	)
	return candidates


func _build_anchor_metrics(
	context,
	anchor_coord: Vector2i,
	targets: Array,
	focus_target: BattleUnitState,
	distance_contract: Dictionary
) -> Dictionary:
	var nearest_distance := 999999
	var nearest_safe_distance := minimum_safe_distance
	var max_threat_attack_range := -1
	var unsafe_gap := 0
	for target_unit in targets:
		var unit := target_unit as BattleUnitState
		if unit == null:
			continue
		var distance := _distance_from_anchor_to_unit(context, context.unit_state, anchor_coord, unit)
		var safe_distance := _resolve_target_safe_distance(context, unit, minimum_safe_distance, safe_distance_margin)
		var threat_attack_range := _resolve_unit_effective_threat_range(context, unit)
		unsafe_gap = maxi(unsafe_gap, safe_distance - distance)
		max_threat_attack_range = maxi(max_threat_attack_range, threat_attack_range)
		if distance < nearest_distance:
			nearest_distance = distance
			nearest_safe_distance = safe_distance
	var focus_distance := _distance_from_anchor_to_unit(context, context.unit_state, anchor_coord, focus_target)
	return {
		"height": _get_anchor_height(context, anchor_coord),
		"nearest_distance": nearest_distance,
		"nearest_safe_distance": nearest_safe_distance,
		"focus_distance": focus_distance,
		"unsafe_gap": maxi(unsafe_gap, 0),
		"max_threat_attack_range": max_threat_attack_range,
		"band_gap": _build_distance_band_gap(focus_distance, distance_contract),
	}


func _build_improvement_metrics(current_metrics: Dictionary, candidate_metrics: Dictionary) -> Dictionary:
	return {
		"height_gain": int(candidate_metrics.get("height", 0)) - int(current_metrics.get("height", 0)),
		"safety_gain": int(current_metrics.get("unsafe_gap", 0)) - int(candidate_metrics.get("unsafe_gap", 0)),
		"band_gain": int(current_metrics.get("band_gap", 0)) - int(candidate_metrics.get("band_gap", 0)),
	}


func _get_reject_reason(current_metrics: Dictionary, candidate_metrics: Dictionary, improvement: Dictionary) -> String:
	var current_unsafe_gap := int(current_metrics.get("unsafe_gap", 0))
	var candidate_unsafe_gap := int(candidate_metrics.get("unsafe_gap", 0))
	var current_nearest := int(current_metrics.get("nearest_distance", 999999))
	var candidate_nearest := int(candidate_metrics.get("nearest_distance", 999999))
	var height_gain := int(improvement.get("height_gain", 0))
	var safety_gain := int(improvement.get("safety_gain", 0))
	var band_gain := int(improvement.get("band_gain", 0))
	var candidate_band_gap := int(candidate_metrics.get("band_gap", 0))
	match positioning_mode:
		MODE_SURVIVAL:
			if current_unsafe_gap <= 0:
				return "already_safe"
			if candidate_nearest <= current_nearest or safety_gain <= 0:
				return "does_not_improve_safety"
			return ""
		MODE_HIGH_GROUND:
			if height_gain <= 0:
				return "does_not_gain_height"
			if candidate_band_gap > 0:
				return "outside_attack_band"
			if candidate_unsafe_gap > current_unsafe_gap:
				return "worsens_safety"
			return ""
		_:
			if current_unsafe_gap > 0 and candidate_nearest > current_nearest and safety_gain > 0:
				return ""
			if height_gain > 0 and candidate_unsafe_gap <= current_unsafe_gap and candidate_band_gap <= int(current_metrics.get("band_gap", 0)):
				return ""
			if band_gain > 0 and candidate_unsafe_gap <= current_unsafe_gap:
				return ""
			return "does_not_improve_position"


func _apply_advantage_score(score_input, improvement: Dictionary) -> void:
	if score_input == null:
		return
	var height_bonus = maxi(int(improvement.get("height_gain", 0)), 0) * high_ground_weight
	var safety_bonus = maxi(int(improvement.get("safety_gain", 0)), 0) * safety_weight
	var band_bonus = maxi(int(improvement.get("band_gain", 0)), 0) * distance_band_weight
	score_input.total_score += height_bonus + safety_bonus + band_bonus


func _is_better_advantage_candidate(candidate_score, candidate_metrics: Dictionary, best_score, best_metrics: Dictionary) -> bool:
	if candidate_score == null:
		return false
	if best_score == null:
		return true
	if int(candidate_score.score_bucket_priority) != int(best_score.score_bucket_priority):
		return int(candidate_score.score_bucket_priority) > int(best_score.score_bucket_priority)
	if int(candidate_metrics.get("unsafe_gap", 0)) != int(best_metrics.get("unsafe_gap", 0)):
		return int(candidate_metrics.get("unsafe_gap", 0)) < int(best_metrics.get("unsafe_gap", 0))
	if int(candidate_metrics.get("height", 0)) != int(best_metrics.get("height", 0)):
		return int(candidate_metrics.get("height", 0)) > int(best_metrics.get("height", 0))
	if int(candidate_metrics.get("band_gap", 0)) != int(best_metrics.get("band_gap", 0)):
		return int(candidate_metrics.get("band_gap", 0)) < int(best_metrics.get("band_gap", 0))
	if int(candidate_score.total_score) != int(best_score.total_score):
		return int(candidate_score.total_score) > int(best_score.total_score)
	return int(candidate_score.resource_cost_score) < int(best_score.resource_cost_score)


func _get_anchor_height(context, anchor_coord: Vector2i) -> int:
	if context == null or context.grid_service == null or context.state == null or context.unit_state == null:
		return 0
	var height := 2147483647
	var found := false
	for footprint_coord in context.grid_service.get_footprint_coords(anchor_coord, context.unit_state.footprint_size):
		var cell = context.grid_service.get_cell(context.state, footprint_coord)
		if cell == null:
			continue
		height = mini(height, int(cell.current_height))
		found = true
	return height if found else 0


func _build_distance_band_gap(distance_value: int, distance_contract: Dictionary) -> int:
	if distance_value < 0:
		return 999999
	var resolved_min_distance := int(distance_contract.get("desired_min_distance", desired_min_distance))
	var resolved_max_distance := int(distance_contract.get("desired_max_distance", desired_max_distance))
	if distance_value < resolved_min_distance:
		return resolved_min_distance - distance_value
	if distance_value > resolved_max_distance:
		return distance_value - resolved_max_distance
	return 0


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if target_selector == &"":
		errors.append("MoveToAdvantagePositionAction %s is missing target_selector." % String(action_id))
	if desired_min_distance < 0:
		errors.append("MoveToAdvantagePositionAction %s desired_min_distance must be >= 0." % String(action_id))
	if desired_max_distance < desired_min_distance:
		errors.append("MoveToAdvantagePositionAction %s desired_max_distance must be >= desired_min_distance." % String(action_id))
	if minimum_safe_distance < 0:
		errors.append("MoveToAdvantagePositionAction %s minimum_safe_distance must be >= 0." % String(action_id))
	if safe_distance_margin < 0:
		errors.append("MoveToAdvantagePositionAction %s safe_distance_margin must be >= 0." % String(action_id))
	if positioning_mode != MODE_ADVANTAGE and positioning_mode != MODE_SURVIVAL and positioning_mode != MODE_HIGH_GROUND:
		errors.append("MoveToAdvantagePositionAction %s positioning_mode must be advantage, survival, or high_ground." % String(action_id))
	if high_ground_weight < 0:
		errors.append("MoveToAdvantagePositionAction %s high_ground_weight must be >= 0." % String(action_id))
	if safety_weight < 0:
		errors.append("MoveToAdvantagePositionAction %s safety_weight must be >= 0." % String(action_id))
	if distance_band_weight < 0:
		errors.append("MoveToAdvantagePositionAction %s distance_band_weight must be >= 0." % String(action_id))
	if candidate_limit <= 0:
		errors.append("MoveToAdvantagePositionAction %s candidate_limit must be > 0." % String(action_id))
	return errors
