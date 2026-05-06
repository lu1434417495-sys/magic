class_name UseChargePathAoeAction
extends "res://scripts/enemies/enemy_ai_action.gd"

const PATH_STEP_AOE_EFFECT_TYPE: StringName = &"path_step_aoe"

@export var skill_ids: Array[StringName] = []
@export var target_selector: StringName = &"nearest_enemy"
@export var minimum_hit_count := 1
@export var desired_min_distance := 1
@export var desired_max_distance := 1


func decide(context):
	var action_trace := _begin_action_trace(context, {
		"action_kind": "charge_path_aoe",
		"target_selector": String(target_selector),
		"minimum_hit_count": minimum_hit_count,
		"desired_min_distance": desired_min_distance,
		"desired_max_distance": desired_max_distance,
	})
	var targets = _sort_target_units(context, &"enemy", target_selector)
	if targets.is_empty():
		_trace_add_block_reason(action_trace, "no_valid_targets")
		_finalize_action_trace(context, action_trace)
		return null
	var focus_target = targets[0] as BattleUnitState
	var best_decision: BattleAiDecision = null
	var best_score_input = null
	var fallback_decision: BattleAiDecision = null
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
		for cast_variant in _get_ground_variants(context, skill_def):
			if cast_variant == null or not _is_charge_variant(cast_variant):
				continue
			var path_step_effect = _get_path_step_aoe_effect(cast_variant)
			if path_step_effect == null:
				_trace_add_block_reason(action_trace, "missing_path_step_aoe")
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
					var path_metrics := _build_path_step_hit_metrics(
						context,
						skill_def,
						path_step_effect,
						preview.resolved_anchor_coord
					)
					var path_hit_count := int(path_metrics.get("path_step_hit_count", 0))
					if path_hit_count < minimum_hit_count:
						_trace_add_block_reason(action_trace, "minimum_hit_count")
						continue
					var resolved_anchor: Vector2i = path_metrics.get("resolved_anchor_coord", preview.resolved_anchor_coord)
					var resolved_move_distance := int(path_metrics.get("resolved_move_distance", 0))
					var charge_action_base_score := 10 + maxi(resolved_move_distance - 1, 0) * 4
					var position_metadata := {
						"action_kind": &"skill",
						"action_base_score": charge_action_base_score,
						"position_target_unit": focus_target,
						"position_anchor_coord": resolved_anchor,
						"desired_min_distance": desired_min_distance,
						"desired_max_distance": desired_max_distance,
						"action_label": _format_skill_variant_label(skill_def, cast_variant),
						"path_step_aoe_effect": path_step_effect,
					}
					for metric_key in path_metrics.keys():
						position_metadata[metric_key] = path_metrics.get(metric_key)
					var score_input = _build_skill_score_input(
						context,
						skill_def,
						command,
						preview,
						cast_variant.effect_defs,
						position_metadata
					)
					_trace_offer_candidate(action_trace, _build_candidate_summary(
						_format_skill_variant_label(skill_def, cast_variant),
						command,
						score_input,
						{
							"path_step_hit_count": path_hit_count,
							"path_step_unique_target_count": int(path_metrics.get("path_step_unique_target_count", 0)),
							"resolved_anchor_coord": resolved_anchor,
							"resolved_move_distance": resolved_move_distance,
							"skill_id": String(skill_id),
						}
					))
					if score_input == null:
						if fallback_decision == null:
							fallback_decision = _create_decision(
								command,
								"%s 准备用 %s 沿途命中 %d 次。" % [
									context.unit_state.display_name,
									skill_def.display_name,
									path_hit_count,
								]
							)
						continue
					if not _is_better_skill_score_input(score_input, best_score_input):
						continue
					best_score_input = score_input
					best_decision = _create_scored_decision(
						command,
						score_input,
						"%s 准备用 %s 沿途命中 %d 次（评分 %d）。" % [
							context.unit_state.display_name,
							skill_def.display_name,
							path_hit_count,
							int(score_input.total_score),
						]
					)
	var resolved_decision: BattleAiDecision = best_decision if best_decision != null else fallback_decision
	_finalize_action_trace(context, action_trace, resolved_decision)
	return resolved_decision


func _get_path_step_aoe_effect(cast_variant: CombatCastVariantDef):
	if cast_variant == null:
		return null
	for effect_def in cast_variant.effect_defs:
		if effect_def != null and effect_def.effect_type == PATH_STEP_AOE_EFFECT_TYPE:
			return effect_def
	return null


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
			return {"valid": true, "distance": left_distance, "direction": Vector2i.LEFT}
		if target_coord.x > max_x:
			var right_distance = target_coord.x - max_x
			return {"valid": true, "distance": right_distance, "direction": Vector2i.RIGHT}
	if target_coord.x >= min_x and target_coord.x <= max_x:
		if target_coord.y < min_y:
			var up_distance = min_y - target_coord.y
			return {"valid": true, "distance": up_distance, "direction": Vector2i.UP}
		if target_coord.y > max_y:
			var down_distance = target_coord.y - max_y
			return {"valid": true, "distance": down_distance, "direction": Vector2i.DOWN}
	return {"valid": false}


func _build_path_step_hit_metrics(
	context,
	skill_def: SkillDef,
	path_step_effect,
	resolved_anchor_coord: Vector2i
) -> Dictionary:
	var empty_result := {
		"resolved_anchor_coord": resolved_anchor_coord,
		"resolved_move_distance": 0,
		"path_step_hit_count": 0,
		"path_step_unique_target_count": 0,
		"path_step_hit_counts_by_unit_id": {},
	}
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return empty_result
	if path_step_effect == null or resolved_anchor_coord == Vector2i(-1, -1):
		return empty_result
	var path := _build_resolved_anchor_path(context.unit_state.coord, resolved_anchor_coord)
	if path.is_empty():
		return empty_result
	var allow_repeat_hits := bool(path_step_effect.params.get("allow_repeat_hits_across_steps", false))
	var target_filter := _resolve_path_step_target_filter(skill_def, path_step_effect)
	var hit_counts_by_unit_id: Dictionary = {}
	var total_hit_count := 0
	for anchor_coord in path:
		var effect_coords := _build_path_step_effect_coords(context, anchor_coord, path_step_effect)
		if effect_coords.is_empty():
			continue
		var step_unit_ids: Dictionary = {}
		for unit_variant in context.state.units.values():
			var target_unit := unit_variant as BattleUnitState
			if target_unit == null or not target_unit.is_alive:
				continue
			if not _matches_path_step_target_filter(context.unit_state, target_unit, target_filter):
				continue
			if not _unit_intersects_coords(target_unit, effect_coords):
				continue
			if not allow_repeat_hits and hit_counts_by_unit_id.has(target_unit.unit_id):
				continue
			if step_unit_ids.has(target_unit.unit_id):
				continue
			step_unit_ids[target_unit.unit_id] = true
		for unit_id in step_unit_ids.keys():
			hit_counts_by_unit_id[unit_id] = int(hit_counts_by_unit_id.get(unit_id, 0)) + 1
			total_hit_count += 1
	return {
		"resolved_anchor_coord": resolved_anchor_coord,
		"resolved_move_distance": path.size(),
		"path_step_hit_count": total_hit_count,
		"path_step_unique_target_count": hit_counts_by_unit_id.size(),
		"path_step_hit_counts_by_unit_id": hit_counts_by_unit_id,
	}


func _build_resolved_anchor_path(start_coord: Vector2i, resolved_anchor_coord: Vector2i) -> Array[Vector2i]:
	var path: Array[Vector2i] = []
	var delta := resolved_anchor_coord - start_coord
	var direction := Vector2i.ZERO
	var distance := 0
	if delta.y == 0 and delta.x != 0:
		direction = Vector2i.RIGHT if delta.x > 0 else Vector2i.LEFT
		distance = absi(delta.x)
	elif delta.x == 0 and delta.y != 0:
		direction = Vector2i.DOWN if delta.y > 0 else Vector2i.UP
		distance = absi(delta.y)
	if direction == Vector2i.ZERO or distance <= 0:
		return path
	var anchor_coord := start_coord
	for _step in range(distance):
		anchor_coord += direction
		path.append(anchor_coord)
	return path


func _build_path_step_effect_coords(context, anchor_coord: Vector2i, path_step_effect) -> Array[Vector2i]:
	var effect_coords: Array[Vector2i] = []
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null or path_step_effect == null:
		return effect_coords
	var step_shape := ProgressionDataUtils.to_string_name(path_step_effect.params.get("step_shape", "diamond"))
	var step_radius := maxi(int(path_step_effect.params.get("step_radius", 1)), 0)
	var coord_set: Dictionary = {}
	for occupied_coord in context.grid_service.get_unit_target_coords(context.unit_state, anchor_coord):
		for effect_coord in context.grid_service.get_area_coords(context.state, occupied_coord, step_shape, step_radius):
			coord_set[effect_coord] = true
	for coord_variant in coord_set.keys():
		effect_coords.append(coord_variant)
	return _sort_coords(effect_coords)


func _unit_intersects_coords(unit_state: BattleUnitState, coords: Array[Vector2i]) -> bool:
	if unit_state == null or coords.is_empty():
		return false
	var coord_set: Dictionary = {}
	for coord in coords:
		coord_set[coord] = true
	unit_state.refresh_footprint()
	for occupied_coord in unit_state.occupied_coords:
		if coord_set.has(occupied_coord):
			return true
	return false


func _resolve_path_step_target_filter(skill_def: SkillDef, path_step_effect) -> StringName:
	if path_step_effect != null and path_step_effect.effect_target_team_filter != &"":
		return path_step_effect.effect_target_team_filter
	if skill_def != null and skill_def.combat_profile != null:
		return skill_def.combat_profile.target_team_filter
	return &"any"


func _matches_path_step_target_filter(source_unit: BattleUnitState, target_unit: BattleUnitState, target_filter: StringName) -> bool:
	if target_unit == null or not target_unit.is_alive:
		return false
	match target_filter:
		&"", &"any":
			return true
		&"self":
			return source_unit != null and target_unit.unit_id == source_unit.unit_id
		&"ally", &"friendly":
			return source_unit != null and target_unit.faction_id == source_unit.faction_id
		&"enemy", &"hostile":
			return source_unit != null and target_unit.faction_id != source_unit.faction_id
		_:
			return true


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if skill_ids.is_empty():
		errors.append("UseChargePathAoeAction %s must declare at least one skill_id." % String(action_id))
	if target_selector == &"":
		errors.append("UseChargePathAoeAction %s is missing target_selector." % String(action_id))
	if minimum_hit_count <= 0:
		errors.append("UseChargePathAoeAction %s minimum_hit_count must be >= 1." % String(action_id))
	if desired_min_distance < 0:
		errors.append("UseChargePathAoeAction %s desired_min_distance must be >= 0." % String(action_id))
	if desired_max_distance < desired_min_distance:
		errors.append("UseChargePathAoeAction %s desired_max_distance must be >= desired_min_distance." % String(action_id))
	return errors
