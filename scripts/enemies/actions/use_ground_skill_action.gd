class_name UseGroundSkillAction
extends "res://scripts/enemies/enemy_ai_action.gd"

const DISTANCE_REF_TARGET_COORD: StringName = &"target_coord"
const DISTANCE_REF_ENEMY_FRONTLINE: StringName = &"enemy_frontline"

@export var skill_ids: Array[StringName] = []
@export var minimum_hit_count := 1
@export var allow_empty_ground_control := false
@export var allow_ground_control_supplement_partial_hits := false
@export var minimum_ground_control_score := 1
@export var minimum_ally_threat_hit_count := 0
@export var maximum_friendly_fire_target_count := 0
@export var allow_friendly_lethal := false
@export var threat_minimum_safe_distance := 0
@export var threat_safe_distance_margin := 0
@export var desired_min_distance := -1
@export var desired_max_distance := -1
@export var distance_reference: StringName = &""


func decide(context):
	AI_TRACE_RECORDER.enter(&"decide:ground_skill")
	var result = _decide_impl(context)
	AI_TRACE_RECORDER.exit(&"decide:ground_skill")
	return result


func _decide_impl(context):
	if not _has_explicit_distance_contract():
		return null
	var action_trace := _begin_action_trace(context, {
		"action_kind": "ground_skill",
		"minimum_hit_count": minimum_hit_count,
		"allow_empty_ground_control": allow_empty_ground_control,
		"allow_ground_control_supplement_partial_hits": allow_ground_control_supplement_partial_hits,
		"minimum_ground_control_score": minimum_ground_control_score,
		"minimum_ally_threat_hit_count": minimum_ally_threat_hit_count,
		"maximum_friendly_fire_target_count": maximum_friendly_fire_target_count,
		"allow_friendly_lethal": allow_friendly_lethal,
		"threat_minimum_safe_distance": threat_minimum_safe_distance,
		"threat_safe_distance_margin": threat_safe_distance_margin,
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
				var raw_hit_count = preview.target_unit_ids.size()
				var ally_threat_hit_count := _count_ally_threatening_preview_targets(context, preview)
				if minimum_ally_threat_hit_count > 0 and ally_threat_hit_count < minimum_ally_threat_hit_count:
					_trace_add_block_reason(action_trace, "minimum_ally_threat_hit_count")
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
					if fallback_decision == null and raw_hit_count > 0:
						fallback_decision = _create_decision(
							command,
							"%s 准备用 %s 覆盖 %d 个单位。" % [context.unit_state.display_name, skill_def.display_name, raw_hit_count]
						)
					_trace_offer_candidate(action_trace, _build_candidate_summary(
						_format_skill_variant_label(skill_def, cast_variant),
						command,
						null,
						{
							"raw_hit_count": raw_hit_count,
							"ally_threat_hit_count": ally_threat_hit_count,
							"skill_id": String(skill_id),
						}
					))
					continue
				if not _passes_minimum_effective_target_or_ground_control(score_input):
					_trace_add_block_reason(action_trace, _resolve_minimum_hit_block_reason(score_input))
					continue
				if not _passes_friendly_fire_limits(score_input):
					_trace_add_block_reason(action_trace, "friendly_fire_limit")
					continue
				_trace_offer_candidate(action_trace, _build_candidate_summary(
					_format_skill_variant_label(skill_def, cast_variant),
					command,
					score_input,
					{
						"raw_hit_count": raw_hit_count,
						"effective_hit_count": int(score_input.effective_target_count),
						"ally_threat_hit_count": ally_threat_hit_count,
						"allow_empty_ground_control": allow_empty_ground_control,
						"allow_ground_control_supplement_partial_hits": allow_ground_control_supplement_partial_hits,
						"estimated_ground_control_cell_count": int(score_input.estimated_ground_control_cell_count),
						"ground_control_score": int(score_input.ground_control_score),
						"acceptance_reason": _resolve_candidate_acceptance_reason(score_input),
						"skill_id": String(skill_id),
					}
				))
				if not _is_better_skill_score_input(score_input, best_score_input):
					continue
				best_score_input = score_input
				best_decision = _create_scored_decision(
					command,
					score_input,
					_build_decision_reason(context, skill_def, score_input)
				)
	var resolved_decision: BattleAiDecision = best_decision if best_decision != null else fallback_decision
	_finalize_action_trace(context, action_trace, resolved_decision)
	return resolved_decision


func _passes_minimum_effective_target_or_ground_control(score_input) -> bool:
	if score_input == null:
		return false
	if int(score_input.effective_target_count) >= minimum_hit_count:
		return true
	if _is_empty_ground_control_candidate(score_input):
		return true
	return _is_ground_control_supplement_candidate(score_input)


func _is_empty_ground_control_candidate(score_input) -> bool:
	if score_input == null:
		return false
	if not allow_empty_ground_control:
		return false
	if int(score_input.effective_target_count) != 0:
		return false
	if int(score_input.estimated_ground_control_cell_count) <= 0:
		return false
	return int(score_input.ground_control_score) >= minimum_ground_control_score


func _is_ground_control_supplement_candidate(score_input) -> bool:
	if score_input == null:
		return false
	if not allow_ground_control_supplement_partial_hits:
		return false
	var effective_target_count := int(score_input.effective_target_count)
	if effective_target_count <= 0 or effective_target_count >= minimum_hit_count:
		return false
	if int(score_input.estimated_ground_control_cell_count) <= 0:
		return false
	return int(score_input.ground_control_score) >= minimum_ground_control_score


func _resolve_minimum_hit_block_reason(score_input) -> String:
	if score_input != null:
		var effective_target_count := int(score_input.effective_target_count)
		if effective_target_count == 0 and int(score_input.estimated_ground_control_cell_count) > 0:
			if not allow_empty_ground_control:
				return "empty_ground_control_not_allowed"
			if int(score_input.ground_control_score) < minimum_ground_control_score:
				return "minimum_ground_control_score"
		elif effective_target_count == 0 and allow_empty_ground_control:
			return "no_ground_control_score"
		elif effective_target_count > 0 and effective_target_count < minimum_hit_count \
				and int(score_input.estimated_ground_control_cell_count) > 0:
			if not allow_ground_control_supplement_partial_hits:
				return "ground_control_supplement_not_allowed"
			if int(score_input.ground_control_score) < minimum_ground_control_score:
				return "minimum_ground_control_score"
	return "minimum_effective_hit_count"


func _resolve_candidate_acceptance_reason(score_input) -> String:
	if _is_empty_ground_control_candidate(score_input):
		return "ground_control"
	if _is_ground_control_supplement_candidate(score_input):
		return "ground_control_supplement"
	return "effective_targets"


func _build_decision_reason(context, skill_def: SkillDef, score_input) -> String:
	if _is_empty_ground_control_candidate(score_input):
		return "%s 准备用 %s 控制 %d 个地格（评分 %d）。" % [
			context.unit_state.display_name,
			skill_def.display_name,
			int(score_input.estimated_ground_control_cell_count),
			int(score_input.total_score),
		]
	if _is_ground_control_supplement_candidate(score_input):
		return "%s 准备用 %s 覆盖 %d 个有效目标并控制 %d 个地格（评分 %d）。" % [
			context.unit_state.display_name,
			skill_def.display_name,
			int(score_input.effective_target_count),
			int(score_input.estimated_ground_control_cell_count),
			int(score_input.total_score),
		]
	return "%s 准备用 %s 覆盖 %d 个有效目标（评分 %d）。" % [
		context.unit_state.display_name,
		skill_def.display_name,
		int(score_input.effective_target_count),
		int(score_input.total_score),
	]


func _passes_friendly_fire_limits(score_input) -> bool:
	if score_input == null:
		return false
	if score_input.get("friendly_fire_reject_reason") != null \
			and not String(score_input.get("friendly_fire_reject_reason")).is_empty():
		return false
	if _is_meteor_special_score_input(score_input):
		return true
	if int(score_input.estimated_friendly_fire_target_count) > maximum_friendly_fire_target_count:
		return false
	if not allow_friendly_lethal and int(score_input.estimated_friendly_lethal_target_count) > 0:
		return false
	return true


func _is_meteor_special_score_input(score_input) -> bool:
	if score_input == null:
		return false
	var facts = score_input.get("special_profile_preview_facts")
	if facts is not Dictionary:
		return false
	return String((facts as Dictionary).get("profile_id", "")) == "meteor_swarm"


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


func _count_ally_threatening_preview_targets(context, preview) -> int:
	if minimum_ally_threat_hit_count <= 0:
		return 0
	if context == null or context.state == null or context.unit_state == null or preview == null:
		return 0
	var allies := _collect_units_by_filter(context, &"ally")
	if allies.is_empty():
		return 0
	var count := 0
	for target_unit_id in preview.target_unit_ids:
		var target_unit = context.state.units.get(target_unit_id) as BattleUnitState
		if target_unit == null or target_unit.faction_id == context.unit_state.faction_id:
			continue
		if _is_target_threatening_any_ally(context, target_unit, allies):
			count += 1
	return count


func _is_target_threatening_any_ally(context, target_unit: BattleUnitState, allies: Array) -> bool:
	if context == null or target_unit == null:
		return false
	var safe_distance := _resolve_target_safe_distance(
		context,
		target_unit,
		threat_minimum_safe_distance,
		threat_safe_distance_margin
	)
	for ally_variant in allies:
		var ally_unit = ally_variant as BattleUnitState
		if ally_unit == null or not ally_unit.is_alive:
			continue
		if _distance_between_units(context, target_unit, ally_unit) <= safe_distance:
			return true
	return false


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
	if minimum_ground_control_score <= 0:
		errors.append("UseGroundSkillAction %s minimum_ground_control_score must be >= 1." % String(action_id))
	if minimum_ally_threat_hit_count < 0:
		errors.append("UseGroundSkillAction %s minimum_ally_threat_hit_count must be >= 0." % String(action_id))
	if maximum_friendly_fire_target_count < 0:
		errors.append("UseGroundSkillAction %s maximum_friendly_fire_target_count must be >= 0." % String(action_id))
	if threat_minimum_safe_distance < 0:
		errors.append("UseGroundSkillAction %s threat_minimum_safe_distance must be >= 0." % String(action_id))
	if threat_safe_distance_margin < 0:
		errors.append("UseGroundSkillAction %s threat_safe_distance_margin must be >= 0." % String(action_id))
	if desired_min_distance < 0:
		errors.append("UseGroundSkillAction %s desired_min_distance must be >= 0." % String(action_id))
	if desired_max_distance < desired_min_distance:
		errors.append("UseGroundSkillAction %s desired_max_distance must be >= desired_min_distance." % String(action_id))
	if distance_reference != DISTANCE_REF_TARGET_COORD and distance_reference != DISTANCE_REF_ENEMY_FRONTLINE:
		errors.append(
			"UseGroundSkillAction %s distance_reference must be target_coord or enemy_frontline." % String(action_id)
		)
	return errors
