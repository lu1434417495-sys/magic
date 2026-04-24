class_name UseChargeAction
extends "res://scripts/enemies/enemy_ai_action.gd"

@export var skill_id: StringName = &"charge"
@export var target_selector: StringName = &"nearest_enemy"


func decide(context):
	var action_trace := _begin_action_trace(context, {
		"action_kind": "charge",
		"target_selector": String(target_selector),
	})
	var skill_def = _get_skill_def(context, skill_id)
	if skill_def == null or skill_def.combat_profile == null or skill_def.combat_profile.target_mode != &"ground":
		_trace_add_block_reason(action_trace, "invalid_charge_skill")
		_finalize_action_trace(context, action_trace)
		return null
	var block_reason := _get_skill_cast_block_reason(context, skill_def)
	if not block_reason.is_empty():
		_trace_add_block_reason(action_trace, block_reason)
		_finalize_action_trace(context, action_trace)
		return null
	var targets = _sort_target_units(context, &"enemy", target_selector)
	if targets.is_empty():
		_trace_add_block_reason(action_trace, "no_valid_targets")
		_finalize_action_trace(context, action_trace)
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
				_trace_count_increment(action_trace, "evaluation_count", 1)
				var target_coord = Vector2i(x, y)
				var charge_info = _resolve_charge_target_info(context.unit_state, target_coord)
				if not bool(charge_info.get("valid", false)):
					continue
				var command = _build_ground_skill_command(context, skill_id, cast_variant.variant_id, [target_coord])
				var preview = context.preview_command(command)
				if preview == null or not bool(preview.allowed):
					_trace_count_increment(action_trace, "preview_reject_count", 1)
					continue
				var resolved_anchor: Vector2i = preview.resolved_anchor_coord if preview.resolved_anchor_coord != Vector2i(-1, -1) else context.unit_state.coord
				var resolved_distance = _distance_from_anchor_to_unit(context, context.unit_state, resolved_anchor, focus_target)
				var resolved_move_distance: int = context.grid_service.get_distance(
					context.unit_state.coord,
					resolved_anchor
				) if context.grid_service != null else 0
				var charge_action_base_score: int = 20 + maxi(resolved_move_distance - 1, 0) * 8
				var score_input = _build_skill_score_input(
					context,
					skill_def,
					command,
					preview,
					cast_variant.effect_defs,
					{
						# Charge is a mobility skill. Scale its base value with the
						# distance covered so normal movement does not always dominate
						# after movement stopped consuming AP.
						"action_kind": &"move",
						"action_base_score": charge_action_base_score,
						"position_target_unit": focus_target,
						"position_anchor_coord": resolved_anchor,
						"desired_min_distance": 1,
						"desired_max_distance": 1,
						"action_label": _format_skill_variant_label(skill_def, cast_variant),
					}
				)
				_trace_offer_candidate(action_trace, _build_candidate_summary(
					"%s->%s" % [_format_skill_variant_label(skill_def, cast_variant), focus_target.display_name],
					command,
					score_input,
					{
						"resolved_anchor_coord": resolved_anchor,
						"resolved_distance": resolved_distance,
					}
				))
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
				var moved_distance = context.grid_service.get_distance(context.unit_state.coord, resolved_anchor) if context.grid_service != null else 0
				var fallback_score = 1000 - resolved_distance * 100 + moved_distance
				if fallback_score <= best_fallback_score:
					continue
				best_fallback_score = fallback_score
				best_decision = _create_decision(
					command,
					"%s 准备用冲锋逼近 %s。" % [context.unit_state.display_name, focus_target.display_name]
				)
	_finalize_action_trace(context, action_trace, best_decision)
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


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if skill_id == &"":
		errors.append("UseChargeAction %s is missing skill_id." % String(action_id))
	if target_selector == &"":
		errors.append("UseChargeAction %s is missing target_selector." % String(action_id))
	return errors
