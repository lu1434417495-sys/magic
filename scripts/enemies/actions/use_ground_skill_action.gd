class_name UseGroundSkillAction
extends "res://scripts/enemies/enemy_ai_action.gd"

const DISTANCE_REF_TARGET_COORD: StringName = &"target_coord"
const DISTANCE_REF_ENEMY_FRONTLINE: StringName = &"enemy_frontline"

@export var skill_ids: Array[StringName] = []
@export var minimum_hit_count := 1
@export var desired_min_distance := -1
@export var desired_max_distance := -1
@export var distance_reference: StringName = &""


func decide(context):
	if not _has_explicit_distance_contract():
		return null
	var action_trace := _begin_action_trace(context, {
		"action_kind": "ground_skill",
		"minimum_hit_count": minimum_hit_count,
		"distance_reference": String(distance_reference),
		"desired_min_distance": desired_min_distance,
		"desired_max_distance": desired_max_distance,
	})
	var best_decision = null
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
			if cast_variant == null or _is_charge_variant(cast_variant):
				continue
			for target_coords in _enumerate_ground_target_coord_sets(context, cast_variant):
				_trace_count_increment(action_trace, "evaluation_count", 1)
				var command = _build_ground_skill_command(context, skill_id, cast_variant.variant_id, target_coords)
				var preview = context.preview_command(command)
				if preview == null or not bool(preview.allowed):
					_trace_count_increment(action_trace, "preview_reject_count", 1)
					continue
				var hit_count = preview.target_unit_ids.size()
				if hit_count < minimum_hit_count:
					_trace_add_block_reason(action_trace, "minimum_hit_count")
					continue
				var position_metadata := _build_position_metadata(context, command, skill_def)
				position_metadata["action_label"] = _format_skill_variant_label(skill_def, cast_variant)
				var score_input = _build_skill_score_input(
					context,
					skill_def,
					command,
					preview,
					_collect_ground_skill_effect_defs(skill_def, cast_variant),
					position_metadata
				)
				if score_input == null:
					if fallback_decision == null:
						fallback_decision = _create_decision(
							command,
							"%s 准备用 %s 覆盖 %d 个单位。" % [context.unit_state.display_name, skill_def.display_name, hit_count]
						)
					_trace_offer_candidate(action_trace, _build_candidate_summary(
						_format_skill_variant_label(skill_def, cast_variant),
						command,
						null,
						{
							"hit_count": hit_count,
							"skill_id": String(skill_id),
						}
					))
					continue
				_trace_offer_candidate(action_trace, _build_candidate_summary(
					_format_skill_variant_label(skill_def, cast_variant),
					command,
					score_input,
					{
						"hit_count": hit_count,
						"skill_id": String(skill_id),
					}
				))
				if not _is_better_skill_score_input(score_input, best_score_input):
					continue
				best_score_input = score_input
				best_decision = _create_scored_decision(
					command,
					score_input,
					"%s 准备用 %s 覆盖 %d 个单位（评分 %d）。" % [
						context.unit_state.display_name,
						skill_def.display_name,
						hit_count,
						int(score_input.total_score),
					]
				)
	var resolved_decision: BattleAiDecision = best_decision if best_decision != null else fallback_decision
	_finalize_action_trace(context, action_trace, resolved_decision)
	return resolved_decision


func _build_position_metadata(context, command, skill_def: SkillDef) -> Dictionary:
	var distance_contract := _resolve_desired_distance_contract(context, skill_def)
	var metadata := distance_contract.duplicate(true)
	match distance_reference:
		DISTANCE_REF_TARGET_COORD:
			metadata["position_objective_kind"] = &"cast_distance"
			metadata["position_target_coord"] = command.target_coord if command != null else Vector2i(-1, -1)
		DISTANCE_REF_ENEMY_FRONTLINE:
			var frontline_unit = _resolve_enemy_frontline_unit(context)
			if frontline_unit != null:
				metadata["position_target_unit"] = frontline_unit
			else:
				metadata["position_objective_kind"] = &"none"
		_:
			metadata["position_objective_kind"] = &"none"
	return metadata


func _collect_ground_skill_effect_defs(skill_def: SkillDef, cast_variant) -> Array:
	var effect_defs: Array = []
	if skill_def != null and skill_def.combat_profile != null:
		if skill_def.combat_profile.cast_variants.is_empty():
			if cast_variant != null:
				for effect_def in cast_variant.effect_defs:
					if effect_def != null:
						effect_defs.append(effect_def)
			else:
				for effect_def in skill_def.combat_profile.effect_defs:
					if effect_def != null:
						effect_defs.append(effect_def)
			return effect_defs
		for effect_def in skill_def.combat_profile.effect_defs:
			if effect_def != null:
				effect_defs.append(effect_def)
	if cast_variant != null:
		for effect_def in cast_variant.effect_defs:
			if effect_def != null:
				effect_defs.append(effect_def)
	return effect_defs


func _resolve_enemy_frontline_unit(context):
	var targets = _sort_target_units(context, &"enemy", &"nearest_enemy")
	return targets[0] if not targets.is_empty() else null


func _has_explicit_distance_contract() -> bool:
	return desired_min_distance >= 0 \
		and desired_max_distance >= desired_min_distance \
		and (distance_reference == DISTANCE_REF_TARGET_COORD or distance_reference == DISTANCE_REF_ENEMY_FRONTLINE)


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if skill_ids.is_empty():
		errors.append("UseGroundSkillAction %s must declare at least one skill_id." % String(action_id))
	if minimum_hit_count <= 0:
		errors.append("UseGroundSkillAction %s minimum_hit_count must be >= 1." % String(action_id))
	if desired_min_distance < 0:
		errors.append("UseGroundSkillAction %s desired_min_distance must be >= 0." % String(action_id))
	if desired_max_distance < desired_min_distance:
		errors.append("UseGroundSkillAction %s desired_max_distance must be >= desired_min_distance." % String(action_id))
	if distance_reference != DISTANCE_REF_TARGET_COORD and distance_reference != DISTANCE_REF_ENEMY_FRONTLINE:
		errors.append(
			"UseGroundSkillAction %s distance_reference must be target_coord or enemy_frontline." % String(action_id)
		)
	return errors
