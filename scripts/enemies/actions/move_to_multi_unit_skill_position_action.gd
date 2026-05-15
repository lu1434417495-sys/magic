class_name MoveToMultiUnitSkillPositionAction
extends "res://scripts/enemies/actions/use_multi_unit_skill_action.gd"

@export var target_count_weight := 40


func decide(context):
	AI_TRACE_RECORDER.enter(&"decide:move_to_multi_unit_skill_position")
	var result = _decide_impl(context)
	AI_TRACE_RECORDER.exit(&"decide:move_to_multi_unit_skill_position")
	return result


func _decide_impl(context):
	if not _has_explicit_distance_contract():
		return null
	var action_trace := _begin_action_trace(context, {
		"action_kind": "move_to_multi_unit_skill_position",
		"target_selector": String(target_selector),
		"distance_reference": String(distance_reference),
		"desired_min_distance": desired_min_distance,
		"desired_max_distance": desired_max_distance,
		"candidate_pool_limit": candidate_pool_limit,
		"candidate_group_limit": candidate_group_limit,
		"target_count_weight": target_count_weight,
	})
	var best_decision: BattleAiDecision = null
	var best_score_input = null
	for skill_id in _resolve_known_skill_ids(context, skill_ids):
		_trace_count_increment(action_trace, "skill_considered_count", 1)
		var skill_def = _get_skill_def(context, skill_id)
		if skill_def == null or skill_def.combat_profile == null:
			_trace_add_block_reason(action_trace, "missing_skill_def")
			continue
		if not _is_multi_unit_skill(skill_def):
			_trace_add_block_reason(action_trace, "non_multi_unit_skill")
			continue
		var block_reason := _get_skill_cast_block_reason(context, skill_def)
		if not block_reason.is_empty():
			_trace_add_block_reason(action_trace, block_reason)
			continue
		var sorted_targets = _sort_target_units(context, skill_def.combat_profile.target_team_filter, target_selector)
		if sorted_targets.is_empty():
			_trace_add_block_reason(action_trace, "no_valid_targets")
			continue
		for cast_variant in _get_multi_unit_cast_variants(context, skill_def):
			if cast_variant != null and _is_charge_variant(cast_variant):
				continue
			var current_group := _build_anchor_target_group(context, skill_def, cast_variant, sorted_targets, context.unit_state.coord)
			var current_target_count := current_group.size()
			for destination in _collect_reachable_move_candidates(context):
				_trace_count_increment(action_trace, "evaluation_count", 1)
				var target_group := _build_anchor_target_group(context, skill_def, cast_variant, sorted_targets, destination)
				var target_count := target_group.size()
				if target_count <= current_target_count:
					_trace_add_block_reason(action_trace, "does_not_improve_target_count")
					continue
				var command = _build_move_command(context, destination)
				var preview = context.preview_command(command)
				if preview == null or not bool(preview.allowed):
					_trace_count_increment(action_trace, "preview_reject_count", 1)
					continue
				var position_metadata := _build_position_metadata(context, target_group, skill_def)
				position_metadata["position_anchor_coord"] = destination
				var score_input = _build_action_score_input(
					context,
					&"move",
					String(action_id),
					command,
					preview,
					position_metadata
				)
				if score_input == null:
					continue
				_apply_target_group_score(score_input, target_group)
				_trace_offer_candidate(action_trace, _build_candidate_summary(
					"move_to_multi_%d_%d" % [destination.x, destination.y],
					command,
					score_input,
					{
						"skill_id": String(skill_id),
						"current_target_count": current_target_count,
						"target_count": target_count,
					}
				))
				if not _is_better_reposition_score_input(score_input, best_score_input):
					continue
				best_score_input = score_input
				best_decision = _create_scored_decision(
					command,
					score_input,
					"%s 准备移动到更适合 %s 的位置，可覆盖 %d 个目标（评分 %d）。" % [
						context.unit_state.display_name,
						skill_def.display_name,
						target_count,
						int(score_input.total_score),
					]
				)
	_finalize_action_trace(context, action_trace, best_decision)
	return best_decision


func _build_anchor_target_group(context, skill_def: SkillDef, cast_variant, sorted_targets: Array, anchor_coord: Vector2i) -> Array:
	var group: Array = []
	if context == null or context.unit_state == null or skill_def == null or skill_def.combat_profile == null:
		return group
	var skill_level := _get_skill_level(context.unit_state, skill_def.skill_id)
	var min_count := maxi(int(skill_def.combat_profile.min_target_count), 1)
	var max_count := maxi(int(skill_def.combat_profile.get_effective_max_target_count(skill_level)), min_count)
	for target_unit in sorted_targets:
		if group.size() >= max_count or group.size() >= candidate_pool_limit:
			break
		if target_unit == null:
			continue
		if not _can_anchor_target_unit(context, skill_def, cast_variant, anchor_coord, target_unit):
			continue
		group.append(target_unit)
	return group if group.size() >= min_count else []


func _can_anchor_target_unit(context, skill_def: SkillDef, _cast_variant, anchor_coord: Vector2i, target_unit) -> bool:
	if context == null or context.unit_state == null or context.grid_service == null:
		return false
	if target_unit == null or not bool(target_unit.is_alive):
		return false
	if not _matches_target_filter(context, target_unit, skill_def.combat_profile.target_team_filter):
		return false
	var effective_range := BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_range(context.unit_state, skill_def)
	return _distance_from_anchor_to_unit(context, context.unit_state, anchor_coord, target_unit) <= effective_range


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
	while not frontier.is_empty():
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
			if not seen.has(neighbor):
				seen[neighbor] = true
				candidates.append(neighbor)
	candidates.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		var left_distance := _distance_from_anchor_to_nearest_target(context, left)
		var right_distance := _distance_from_anchor_to_nearest_target(context, right)
		if left_distance == right_distance:
			return left.y < right.y or (left.y == right.y and left.x < right.x)
		return left_distance < right_distance
	)
	return candidates


func _distance_from_anchor_to_nearest_target(context, anchor_coord: Vector2i) -> int:
	var targets = _sort_target_units(context, &"enemy", target_selector)
	if targets.is_empty():
		return 999999
	return _distance_from_anchor_to_unit(context, context.unit_state, anchor_coord, targets[0])


func _apply_target_group_score(score_input, target_group: Array) -> void:
	var target_unit_ids: Array[StringName] = []
	var target_coords: Array[Vector2i] = []
	for target_unit in target_group:
		if target_unit == null:
			continue
		target_unit_ids.append(target_unit.unit_id)
		target_coords.append(target_unit.coord)
	score_input.target_unit_ids = target_unit_ids
	score_input.target_coords = target_coords
	score_input.target_count = target_unit_ids.size()
	score_input.total_score += score_input.target_count * target_count_weight


func _is_better_reposition_score_input(candidate, best_candidate) -> bool:
	if candidate == null:
		return false
	if best_candidate == null:
		return true
	if int(candidate.target_count) != int(best_candidate.target_count):
		return int(candidate.target_count) > int(best_candidate.target_count)
	if int(candidate.position_objective_score) != int(best_candidate.position_objective_score):
		return int(candidate.position_objective_score) > int(best_candidate.position_objective_score)
	if int(candidate.total_score) != int(best_candidate.total_score):
		return int(candidate.total_score) > int(best_candidate.total_score)
	return int(candidate.resource_cost_score) < int(best_candidate.resource_cost_score)


func validate_schema() -> Array[String]:
	var errors := super.validate_schema()
	if target_count_weight < 0:
		errors.append("MoveToMultiUnitSkillPositionAction %s target_count_weight must be >= 0." % String(action_id))
	return errors
