class_name RetreatAction
extends "res://scripts/enemies/enemy_ai_action.gd"

var target_selector: StringName = &"nearest_enemy"
var minimum_safe_distance := 3


func decide(context):
	var targets = _sort_target_units(context, &"enemy", target_selector)
	if targets.is_empty():
		return null
	var focus_target = targets[0] as BattleUnitState
	var current_distance = _distance_between_units(context, context.unit_state, focus_target)
	var best_decision = null
	var best_distance = current_distance
	for neighbor in context.grid_service.get_neighbors_4(context.state, context.unit_state.coord):
		if not context.grid_service.can_traverse(context.state, context.unit_state.coord, neighbor, context.unit_state):
			continue
		var command = _build_move_command(context, neighbor)
		var preview = context.preview_command(command)
		if preview == null or not bool(preview.allowed):
			continue
		var predicted_distance = _distance_from_anchor_to_unit(context, context.unit_state, neighbor, focus_target)
		if predicted_distance < minimum_safe_distance and predicted_distance <= best_distance:
			continue
		if predicted_distance <= best_distance:
			continue
		best_distance = predicted_distance
		best_decision = _create_decision(
			command,
			"%s 准备与 %s 拉开到 %d 格。" % [context.unit_state.display_name, focus_target.display_name, predicted_distance]
		)
	return best_decision
