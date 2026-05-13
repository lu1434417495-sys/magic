class_name UseGroundRepositionSkillAction
extends "res://scripts/enemies/enemy_ai_action.gd"

@export var skill_ids: Array[StringName] = []
@export var target_selector: StringName = &"nearest_enemy"
@export var minimum_safe_distance := 3
@export var safe_distance_margin := 1
@export var desired_max_distance_bonus := 2
@export var action_base_score := 1500


func decide(context):
	AI_TRACE_RECORDER.enter(&"decide:ground_reposition_skill")
	var result = _decide_impl(context)
	AI_TRACE_RECORDER.exit(&"decide:ground_reposition_skill")
	return result


func _decide_impl(context):
	var action_trace := _begin_action_trace(context, {
		"action_kind": "ground_reposition_skill",
		"target_selector": String(target_selector),
		"minimum_safe_distance": minimum_safe_distance,
		"safe_distance_margin": safe_distance_margin,
		"desired_max_distance_bonus": desired_max_distance_bonus,
		"action_base_score": action_base_score,
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
	if focus_target == null:
		_trace_add_block_reason(action_trace, "no_valid_targets")
		_finalize_action_trace(context, action_trace)
		return null

	var resolved_safe_distance := maxi(minimum_safe_distance + safe_distance_margin, 1)
	var current_distance := _distance_from_anchor_to_unit(
		context,
		context.unit_state,
		context.unit_state.coord,
		focus_target
	)
	var trace_metadata: Dictionary = action_trace.get("metadata", {})
	trace_metadata["focus_target_unit_id"] = String(focus_target.unit_id)
	trace_metadata["current_distance"] = current_distance
	trace_metadata["resolved_safe_distance"] = resolved_safe_distance
	action_trace["metadata"] = trace_metadata
	if current_distance >= resolved_safe_distance:
		_trace_add_block_reason(action_trace, "already_safe")
		_finalize_action_trace(context, action_trace)
		return null

	var best_decision = null
	var best_score_input = null
	for skill_id in _resolve_known_skill_ids(context, skill_ids):
		_trace_count_increment(action_trace, "skill_considered_count", 1)
		var skill_def = _get_skill_def(context, skill_id)
		if skill_def == null or skill_def.combat_profile == null:
			_trace_add_block_reason(action_trace, "missing_skill_def")
			continue
		if skill_def.combat_profile.target_mode != &"ground":
			_trace_add_block_reason(action_trace, "non_ground_skill")
			continue
		var block_reason := _get_skill_cast_block_reason(context, skill_def)
		if not block_reason.is_empty():
			_trace_add_block_reason(action_trace, block_reason)
			continue
		var effective_range := BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_range(context.unit_state, skill_def)
		for cast_variant in _get_ground_variants(context, skill_def):
			if cast_variant == null or _is_charge_variant(cast_variant):
				continue
			if not _has_reposition_effect(cast_variant.effect_defs):
				_trace_add_block_reason(action_trace, "missing_reposition_effect")
				continue
			for target_coords in _enumerate_ground_target_coord_sets(context, cast_variant):
				if target_coords.size() != 1:
					continue
				var landing_coord: Vector2i = target_coords[0]
				var cast_distance: int = context.grid_service.get_distance_from_unit_to_coord(
					context.unit_state,
					landing_coord
				)
				if effective_range >= 0 and cast_distance > effective_range:
					continue
				var landing_distance := _distance_from_anchor_to_unit(
					context,
					context.unit_state,
					landing_coord,
					focus_target
				)
				if landing_distance <= current_distance:
					_trace_add_block_reason(action_trace, "does_not_improve_safety")
					continue
				_trace_count_increment(action_trace, "evaluation_count", 1)
				var command = _build_ground_skill_command(context, skill_id, cast_variant.variant_id, target_coords)
				var preview = context.preview_command(command)
				if preview == null or not bool(preview.allowed):
					_trace_count_increment(action_trace, "preview_reject_count", 1)
					continue
				var score_input = _build_skill_score_input(
					context,
					skill_def,
					command,
					preview,
					cast_variant.effect_defs,
					{
						"action_label": _format_skill_variant_label(skill_def, cast_variant),
						"action_base_score": action_base_score,
						"position_target_unit": focus_target,
						"position_anchor_coord": landing_coord,
						"position_current_distance": current_distance,
						"position_safe_distance": resolved_safe_distance,
						"desired_min_distance": resolved_safe_distance,
						"desired_max_distance": resolved_safe_distance + maxi(desired_max_distance_bonus, 0),
						"position_objective_kind": &"distance_band_progress",
					}
				)
				_trace_offer_candidate(action_trace, _build_candidate_summary(
					"%s_to_%d_%d" % [_format_skill_variant_label(skill_def, cast_variant), landing_coord.x, landing_coord.y],
					command,
					score_input,
					{
						"skill_id": String(skill_id),
						"landing_distance": landing_distance,
						"resolved_safe_distance": resolved_safe_distance,
					}
				))
				if not _is_better_skill_score_input(score_input, best_score_input):
					continue
				best_score_input = score_input
				best_decision = _create_scored_decision(
					command,
					score_input,
					"%s 准备用 %s 拉开到 %d 格（评分 %d）。" % [
						context.unit_state.display_name,
						skill_def.display_name,
						landing_distance,
						int(score_input.total_score),
					]
				)
	_finalize_action_trace(context, action_trace, best_decision)
	return best_decision


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if skill_ids.is_empty():
		errors.append("UseGroundRepositionSkillAction %s must declare at least one skill_id." % String(action_id))
	if target_selector == &"":
		errors.append("UseGroundRepositionSkillAction %s is missing target_selector." % String(action_id))
	if minimum_safe_distance <= 0:
		errors.append("UseGroundRepositionSkillAction %s minimum_safe_distance must be >= 1." % String(action_id))
	if safe_distance_margin < 0:
		errors.append("UseGroundRepositionSkillAction %s safe_distance_margin must be >= 0." % String(action_id))
	if desired_max_distance_bonus < 0:
		errors.append("UseGroundRepositionSkillAction %s desired_max_distance_bonus must be >= 0." % String(action_id))
	return errors


func _has_reposition_effect(effect_defs: Array) -> bool:
	for effect_def in effect_defs:
		if effect_def == null:
			continue
		if effect_def.effect_type != &"forced_move":
			continue
		if effect_def.forced_move_mode == &"blink" or effect_def.forced_move_mode == &"jump":
			return true
	return false
