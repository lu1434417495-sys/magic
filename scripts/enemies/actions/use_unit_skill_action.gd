class_name UseUnitSkillAction
extends "res://scripts/enemies/enemy_ai_action.gd"

const DISTANCE_REF_TARGET_UNIT: StringName = &"target_unit"
const DISTANCE_REF_ENEMY_FRONTLINE: StringName = &"enemy_frontline"

@export var skill_ids: Array[StringName] = []
@export var target_selector: StringName = &"nearest_enemy"
@export var desired_min_distance := -1
@export var desired_max_distance := -1
@export var distance_reference: StringName = &""


func decide(context):
	if not _has_explicit_distance_contract():
		return null
	var action_trace := _begin_action_trace(context, {
		"action_kind": "unit_skill",
		"target_selector": String(target_selector),
		"distance_reference": String(distance_reference),
		"desired_min_distance": desired_min_distance,
		"desired_max_distance": desired_max_distance,
	})
	var best_decision: BattleAiDecision = null
	var best_score_input = null
	var fallback_decision: BattleAiDecision = null
	for skill_id in _resolve_known_skill_ids(context, skill_ids):
		_trace_count_increment(action_trace, "skill_considered_count", 1)
		var skill_def = _get_skill_def(context, skill_id)
		if skill_def == null or skill_def.combat_profile == null:
			_trace_add_block_reason(action_trace, "missing_skill_def")
			continue
		if skill_def.combat_profile.target_mode != &"unit":
			_trace_add_block_reason(action_trace, "non_unit_skill")
			continue
		var block_reason := _get_skill_cast_block_reason(context, skill_def)
		if not block_reason.is_empty():
			_trace_add_block_reason(action_trace, block_reason)
			continue
		var targets = _sort_target_units(context, skill_def.combat_profile.target_team_filter, target_selector)
		if targets.is_empty():
			_trace_add_block_reason(action_trace, "no_valid_targets")
			continue
		for target_unit in targets:
			_trace_count_increment(action_trace, "evaluation_count", 1)
			var command = _build_unit_skill_command(context, skill_id, target_unit)
			var preview = context.preview_command(command)
			if preview == null or not bool(preview.allowed):
				_trace_count_increment(action_trace, "preview_reject_count", 1)
				continue
			var position_metadata := _build_position_metadata(context, target_unit, skill_def)
			position_metadata["action_label"] = skill_def.display_name
			var score_input = _build_skill_score_input(
				context,
				skill_def,
				command,
				preview,
				skill_def.combat_profile.effect_defs,
				position_metadata
			)
			if score_input == null:
				if fallback_decision == null:
					fallback_decision = _create_decision(
						command,
						"%s 选择对 %s 使用 %s。" % [
							context.unit_state.display_name,
							target_unit.display_name,
							skill_def.display_name,
						]
					)
				_trace_offer_candidate(action_trace, _build_candidate_summary(
					"%s->%s" % [skill_def.display_name, target_unit.display_name],
					command,
					null,
					{
						"skill_id": String(skill_id),
						"target_unit_id": String(target_unit.unit_id),
					}
				))
				continue
			_trace_offer_candidate(action_trace, _build_candidate_summary(
				"%s->%s" % [skill_def.display_name, target_unit.display_name],
				command,
				score_input,
				{
					"skill_id": String(skill_id),
					"target_unit_id": String(target_unit.unit_id),
				}
			))
			if not _is_better_skill_score_input(score_input, best_score_input):
				continue
			best_score_input = score_input
			best_decision = _create_scored_decision(
				command,
				score_input,
				"%s 选择对 %s 使用 %s（评分 %d）。" % [
					context.unit_state.display_name,
					target_unit.display_name,
					skill_def.display_name,
					int(score_input.total_score),
				]
			)
	var resolved_decision: BattleAiDecision = best_decision if best_decision != null else fallback_decision
	_finalize_action_trace(context, action_trace, resolved_decision)
	return resolved_decision


func _build_position_metadata(context, target_unit, skill_def: SkillDef) -> Dictionary:
	var distance_contract := _resolve_desired_distance_contract(context, skill_def)
	var metadata := distance_contract.duplicate(true)
	match distance_reference:
		DISTANCE_REF_TARGET_UNIT:
			metadata["position_target_unit"] = target_unit
		DISTANCE_REF_ENEMY_FRONTLINE:
			var frontline_unit = _resolve_enemy_frontline_unit(context)
			if frontline_unit != null:
				metadata["position_target_unit"] = frontline_unit
			else:
				metadata["position_objective_kind"] = &"none"
		_:
			metadata["position_objective_kind"] = &"none"
	return metadata


func _resolve_enemy_frontline_unit(context):
	var targets = _sort_target_units(context, &"enemy", &"nearest_enemy")
	return targets[0] if not targets.is_empty() else null


func _has_explicit_distance_contract() -> bool:
	return desired_min_distance >= 0 \
		and desired_max_distance >= desired_min_distance \
		and (distance_reference == DISTANCE_REF_TARGET_UNIT or distance_reference == DISTANCE_REF_ENEMY_FRONTLINE)


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if skill_ids.is_empty():
		errors.append("UseUnitSkillAction %s must declare at least one skill_id." % String(action_id))
	if target_selector == &"":
		errors.append("UseUnitSkillAction %s is missing target_selector." % String(action_id))
	if desired_min_distance < 0:
		errors.append("UseUnitSkillAction %s desired_min_distance must be >= 0." % String(action_id))
	if desired_max_distance < desired_min_distance:
		errors.append("UseUnitSkillAction %s desired_max_distance must be >= desired_min_distance." % String(action_id))
	if distance_reference != DISTANCE_REF_TARGET_UNIT and distance_reference != DISTANCE_REF_ENEMY_FRONTLINE:
		errors.append(
			"UseUnitSkillAction %s distance_reference must be target_unit or enemy_frontline." % String(action_id)
		)
	return errors
