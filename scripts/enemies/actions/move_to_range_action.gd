class_name MoveToRangeAction
extends "res://scripts/enemies/enemy_ai_action.gd"

@export var target_selector: StringName = &"nearest_enemy"
@export var desired_min_distance := 1
@export var desired_max_distance := 1


func decide(context):
	var targets = _sort_target_units(context, &"enemy", target_selector)
	if targets.is_empty():
		return null
	var focus_target = targets[0] as BattleUnitState
	var current_distance = _distance_between_units(context, context.unit_state, focus_target)
	var current_score = _distance_band_score(current_distance)
	var best_decision = null
	var best_score = current_score
	for neighbor in context.grid_service.get_neighbors_4(context.state, context.unit_state.coord):
		if not context.grid_service.can_traverse(context.state, context.unit_state.coord, neighbor, context.unit_state):
			continue
		var command = _build_move_command(context, neighbor)
		var preview = context.preview_command(command)
		if preview == null or not bool(preview.allowed):
			continue
		var predicted_distance = _distance_from_anchor_to_unit(context, context.unit_state, neighbor, focus_target)
		var total_score = _distance_band_score(predicted_distance)
		if total_score <= best_score:
			continue
		best_score = total_score
		best_decision = _create_decision(
			command,
			"%s 准备调整到距离 %s %d 格。" % [context.unit_state.display_name, focus_target.display_name, predicted_distance]
		)
	return best_decision


func _distance_band_score(distance_value: int) -> int:
	if distance_value >= desired_min_distance and distance_value <= desired_max_distance:
		return 1000 - distance_value
	if distance_value < desired_min_distance:
		return 500 - (desired_min_distance - distance_value) * 100
	return 500 - (distance_value - desired_max_distance) * 100


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if target_selector == &"":
		errors.append("MoveToRangeAction %s is missing target_selector." % String(action_id))
	if desired_min_distance < 0:
		errors.append("MoveToRangeAction %s desired_min_distance must be >= 0." % String(action_id))
	if desired_max_distance < desired_min_distance:
		errors.append("MoveToRangeAction %s desired_max_distance must be >= desired_min_distance." % String(action_id))
	return errors
