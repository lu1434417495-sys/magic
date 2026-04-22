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
			"position_objective_kind": &"distance_band",
			"move_cost": 0,
		}
	)
	var best_decision = null
	var best_score_input = current_score_input
	for neighbor in context.grid_service.get_neighbors_4(context.state, context.unit_state.coord):
		if not context.grid_service.can_traverse(context.state, context.unit_state.coord, neighbor, context.unit_state):
			continue
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
				"position_objective_kind": &"distance_band",
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
		if not _is_better_skill_score_input(score_input, best_score_input):
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
	_finalize_action_trace(context, action_trace, best_decision)
	return best_decision


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if target_selector == &"":
		errors.append("MoveToRangeAction %s is missing target_selector." % String(action_id))
	if desired_min_distance < 0:
		errors.append("MoveToRangeAction %s desired_min_distance must be >= 0." % String(action_id))
	if desired_max_distance < desired_min_distance:
		errors.append("MoveToRangeAction %s desired_max_distance must be >= desired_min_distance." % String(action_id))
	return errors
