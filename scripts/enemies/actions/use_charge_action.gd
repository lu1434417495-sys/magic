class_name UseChargeAction
extends "res://scripts/enemies/enemy_ai_action.gd"

var skill_id: StringName = &"charge"
var target_selector: StringName = &"nearest_enemy"


func decide(context):
	var skill_def = _get_skill_def(context, skill_id)
	if skill_def == null or skill_def.combat_profile == null or skill_def.combat_profile.target_mode != &"ground":
		return null
	if not _get_skill_cast_block_reason(context, skill_def).is_empty():
		return null
	var targets = _sort_target_units(context, &"enemy", target_selector)
	if targets.is_empty():
		return null
	var focus_target = targets[0] as BattleUnitState
	var best_decision = null
	var best_score_input = null
	var best_fallback_score = -999999
	for cast_variant in _get_ground_variants(context, skill_def):
		if cast_variant == null or not _is_charge_variant(cast_variant):
			continue
		for y in range(context.state.map_size.y):
			for x in range(context.state.map_size.x):
				var target_coord = Vector2i(x, y)
				var charge_info = _resolve_charge_target_info(context.unit_state, target_coord)
				if not bool(charge_info.get("valid", false)):
					continue
				var command = _build_ground_skill_command(context, skill_id, cast_variant.variant_id, [target_coord])
				var preview = context.preview_command(command)
				if preview == null or not bool(preview.allowed):
					continue
				var predicted_anchor: Vector2i = charge_info.get("predicted_anchor", context.unit_state.coord)
				var predicted_distance = _distance_from_anchor_to_unit(context, context.unit_state, predicted_anchor, focus_target)
				var score_input = _build_skill_score_input(
					context,
					skill_def,
					command,
					preview,
					cast_variant.effect_defs,
					{
						"position_target_unit": focus_target,
						"position_anchor_coord": predicted_anchor,
						"desired_min_distance": 1,
						"desired_max_distance": 1,
					}
				)
				if score_input != null:
					if not _is_better_skill_score_input(score_input, best_score_input):
						continue
					best_score_input = score_input
					best_decision = _create_scored_decision(
						command,
						score_input,
						"%s 准备用冲锋逼近 %s（评分 %d）。" % [
							context.unit_state.display_name,
							focus_target.display_name,
							int(score_input.total_score),
						]
					)
					continue
				var fallback_score = 1000 - predicted_distance * 100 + int(charge_info.get("distance", 0))
				if fallback_score <= best_fallback_score:
					continue
				best_fallback_score = fallback_score
				best_decision = _create_decision(
					command,
					"%s 准备用冲锋逼近 %s。" % [context.unit_state.display_name, focus_target.display_name]
				)
	return best_decision


func _resolve_charge_target_info(unit_state: BattleUnitState, target_coord: Vector2i) -> Dictionary:
	if unit_state == null:
		return {"valid": false}
	unit_state.refresh_footprint()
	var min_x = unit_state.coord.x
	var max_x = unit_state.coord.x + unit_state.footprint_size.x - 1
	var min_y = unit_state.coord.y
	var max_y = unit_state.coord.y + unit_state.footprint_size.y - 1
	if target_coord.y >= min_y and target_coord.y <= max_y:
		if target_coord.x < min_x:
			var left_distance = min_x - target_coord.x
			return {
				"valid": true,
				"distance": left_distance,
				"predicted_anchor": unit_state.coord + Vector2i.LEFT * left_distance,
			}
		if target_coord.x > max_x:
			var right_distance = target_coord.x - max_x
			return {
				"valid": true,
				"distance": right_distance,
				"predicted_anchor": unit_state.coord + Vector2i.RIGHT * right_distance,
			}
	if target_coord.x >= min_x and target_coord.x <= max_x:
		if target_coord.y < min_y:
			var up_distance = min_y - target_coord.y
			return {
				"valid": true,
				"distance": up_distance,
				"predicted_anchor": unit_state.coord + Vector2i.UP * up_distance,
			}
		if target_coord.y > max_y:
			var down_distance = target_coord.y - max_y
			return {
				"valid": true,
				"distance": down_distance,
				"predicted_anchor": unit_state.coord + Vector2i.DOWN * down_distance,
			}
	return {"valid": false}
