class_name UseRandomChainSkillAction
extends "res://scripts/enemies/enemy_ai_action.gd"

const DISTANCE_REF_CANDIDATE_POOL: StringName = &"candidate_pool"
const DISTANCE_REF_ENEMY_FRONTLINE: StringName = &"enemy_frontline"

@export var skill_ids: Array[StringName] = []
@export var target_selector: StringName = &"nearest_enemy"
@export var desired_min_distance := -1
@export var desired_max_distance := -1
@export var distance_reference: StringName = DISTANCE_REF_CANDIDATE_POOL


func decide(context):
	if not _has_explicit_distance_contract():
		return null
	var action_trace := _begin_action_trace(context, {
		"action_kind": "random_chain_skill",
		"target_selection_mode": "random_chain",
		"target_selector": String(target_selector),
		"distance_reference": String(distance_reference),
		"desired_min_distance": desired_min_distance,
		"desired_max_distance": desired_max_distance,
		"selection_policy": "random_from_living_pool",
		"pool_refresh_policy": "before_each_attempt",
		"score_estimate_policy": "expected_value",
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
		if not _is_random_chain_skill(skill_def):
			_trace_add_block_reason(action_trace, "non_random_chain_skill")
			continue
		var block_reason := _get_skill_cast_block_reason(context, skill_def)
		if not block_reason.is_empty():
			_trace_add_block_reason(action_trace, block_reason)
			continue
		for cast_variant in _get_random_chain_cast_variants(context, skill_def):
			_trace_count_increment(action_trace, "evaluation_count", 1)
			var command = _build_random_chain_skill_command(context, skill_id, cast_variant)
			var preview = context.preview_command(command)
			if preview == null or not bool(preview.allowed):
				_trace_count_increment(action_trace, "preview_reject_count", 1)
				continue
			var candidate_units := _resolve_candidate_units(context, preview, skill_def)
			if candidate_units.is_empty():
				_trace_add_block_reason(action_trace, "no_random_chain_candidates")
				continue
			var candidate_unit_ids := _candidate_unit_ids(candidate_units)
			var position_metadata := _build_position_metadata(context, candidate_units, skill_def)
			position_metadata["action_kind"] = &"random_chain_skill"
			position_metadata["target_selection_mode"] = &"random_chain"
			position_metadata["action_label"] = _format_skill_variant_label(skill_def, cast_variant)
			position_metadata["candidate_pool_unit_ids"] = candidate_unit_ids.duplicate()
			position_metadata["candidate_pool_count"] = candidate_unit_ids.size()
			position_metadata["random_chain_max_hits_per_target"] = maxi(int(skill_def.combat_profile.max_hits_per_target), 1)
			position_metadata["random_chain_max_attempt_count"] = maxi(candidate_unit_ids.size() * int(position_metadata["random_chain_max_hits_per_target"]), 1)
			position_metadata["random_chain_selection_policy"] = &"random_from_living_pool"
			position_metadata["random_chain_pool_refresh_policy"] = &"before_each_attempt"
			position_metadata["random_chain_score_estimate_policy"] = &"expected_value"
			_update_trace_metadata(action_trace, position_metadata)
			var score_input = _build_skill_score_input(
				context,
				skill_def,
				command,
				preview,
				_collect_random_chain_effect_defs(skill_def, cast_variant),
				position_metadata
			)
			if score_input == null:
				if fallback_decision == null:
					fallback_decision = _create_decision(
						command,
						"%s 准备发动 %s，候选池 %d 个单位。" % [
							context.unit_state.display_name,
							skill_def.display_name,
							candidate_unit_ids.size(),
						]
					)
				_trace_offer_candidate(action_trace, _build_candidate_summary(
					_format_skill_variant_label(skill_def, cast_variant),
					command,
					null,
					{
						"skill_id": String(skill_id),
						"candidate_pool_count": candidate_unit_ids.size(),
						"candidate_pool_unit_ids": _stringify_unit_ids(candidate_unit_ids),
					}
				))
				continue
			_trace_offer_candidate(action_trace, _build_candidate_summary(
				_format_skill_variant_label(skill_def, cast_variant),
				command,
				score_input,
				{
					"skill_id": String(skill_id),
					"candidate_pool_count": candidate_unit_ids.size(),
					"candidate_pool_unit_ids": _stringify_unit_ids(candidate_unit_ids),
				}
			))
			if not _is_better_skill_score_input(score_input, best_score_input):
				continue
			best_score_input = score_input
			best_decision = _create_scored_decision(
				command,
				score_input,
				"%s 准备发动 %s，候选池 %d 个单位（评分 %d）。" % [
					context.unit_state.display_name,
					skill_def.display_name,
					candidate_unit_ids.size(),
					int(score_input.total_score),
				]
			)
	var resolved_decision: BattleAiDecision = best_decision if best_decision != null else fallback_decision
	_finalize_action_trace(context, action_trace, resolved_decision)
	return resolved_decision


func _is_random_chain_skill(skill_def: SkillDef) -> bool:
	return skill_def != null \
		and skill_def.combat_profile != null \
		and skill_def.combat_profile.target_mode == &"unit" \
		and ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_selection_mode) == &"random_chain"


func _get_random_chain_cast_variants(context, skill_def: SkillDef) -> Array:
	if skill_def == null or skill_def.combat_profile == null:
		return []
	if skill_def.combat_profile.cast_variants.is_empty():
		return [null]
	var variants: Array = []
	var skill_level := _get_skill_level(context.unit_state, skill_def.skill_id) if context != null else 0
	for cast_variant in skill_def.combat_profile.get_unlocked_cast_variants(skill_level):
		if cast_variant != null:
			variants.append(cast_variant)
	return variants


func _build_random_chain_skill_command(context, skill_id: StringName, cast_variant):
	if context == null or context.unit_state == null:
		return null
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = context.unit_state.unit_id
	command.skill_id = skill_id
	command.skill_variant_id = cast_variant.variant_id if cast_variant != null else &""
	return command


func _resolve_candidate_units(context, preview, skill_def: SkillDef) -> Array:
	var candidate_ids: Dictionary = {}
	if preview != null:
		for raw_unit_id in preview.random_chain_candidate_unit_ids:
			var unit_id := ProgressionDataUtils.to_string_name(raw_unit_id)
			if unit_id != &"":
				candidate_ids[unit_id] = true
	if candidate_ids.is_empty():
		return []
	var sorted_targets = _sort_target_units(context, skill_def.combat_profile.target_team_filter, target_selector)
	var candidate_units: Array = []
	for target_variant in sorted_targets:
		var target_unit = target_variant as BattleUnitState
		if target_unit != null and candidate_ids.has(target_unit.unit_id):
			candidate_units.append(target_unit)
	return candidate_units


func _candidate_unit_ids(candidate_units: Array) -> Array[StringName]:
	var ids: Array[StringName] = []
	for candidate_variant in candidate_units:
		var candidate = candidate_variant as BattleUnitState
		if candidate != null:
			ids.append(candidate.unit_id)
	return ids


func _stringify_unit_ids(candidate_unit_ids: Array) -> Array[String]:
	var ids: Array[String] = []
	for unit_id in candidate_unit_ids:
		ids.append(String(unit_id))
	return ids


func _collect_random_chain_effect_defs(skill_def: SkillDef, cast_variant) -> Array:
	var effect_defs: Array = []
	if skill_def != null and skill_def.combat_profile != null:
		for effect_def in skill_def.combat_profile.effect_defs:
			if effect_def != null:
				effect_defs.append(effect_def)
	if cast_variant != null:
		for effect_def in cast_variant.effect_defs:
			if effect_def != null:
				effect_defs.append(effect_def)
	return effect_defs


func _build_position_metadata(context, candidate_units: Array, skill_def: SkillDef) -> Dictionary:
	var distance_contract := _resolve_desired_distance_contract(context, skill_def)
	var metadata := distance_contract.duplicate(true)
	match distance_reference:
		DISTANCE_REF_CANDIDATE_POOL:
			var primary_candidate = candidate_units[0] if not candidate_units.is_empty() else null
			if primary_candidate != null:
				metadata["position_target_unit"] = primary_candidate
			else:
				metadata["position_objective_kind"] = &"none"
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


func _update_trace_metadata(action_trace: Dictionary, scoring_metadata: Dictionary) -> void:
	if action_trace.is_empty():
		return
	var metadata: Dictionary = action_trace.get("metadata", {})
	metadata["candidate_pool_count"] = int(scoring_metadata.get("candidate_pool_count", 0))
	metadata["candidate_pool_unit_ids"] = _stringify_unit_ids(scoring_metadata.get("candidate_pool_unit_ids", []))
	metadata["max_hits_per_target"] = int(scoring_metadata.get("random_chain_max_hits_per_target", 0))
	metadata["max_attempt_count"] = int(scoring_metadata.get("random_chain_max_attempt_count", 0))
	action_trace["metadata"] = metadata


func _has_explicit_distance_contract() -> bool:
	return desired_min_distance >= 0 \
		and desired_max_distance >= desired_min_distance \
		and (distance_reference == DISTANCE_REF_CANDIDATE_POOL or distance_reference == DISTANCE_REF_ENEMY_FRONTLINE)


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if skill_ids.is_empty():
		errors.append("UseRandomChainSkillAction %s must declare at least one skill_id." % String(action_id))
	if target_selector == &"":
		errors.append("UseRandomChainSkillAction %s is missing target_selector." % String(action_id))
	if desired_min_distance < 0:
		errors.append("UseRandomChainSkillAction %s desired_min_distance must be >= 0." % String(action_id))
	if desired_max_distance < desired_min_distance:
		errors.append("UseRandomChainSkillAction %s desired_max_distance must be >= desired_min_distance." % String(action_id))
	if distance_reference != DISTANCE_REF_CANDIDATE_POOL and distance_reference != DISTANCE_REF_ENEMY_FRONTLINE:
		errors.append(
			"UseRandomChainSkillAction %s distance_reference must be candidate_pool or enemy_frontline." % String(action_id)
		)
	return errors
