class_name UseMultiUnitSkillAction
extends "res://scripts/enemies/enemy_ai_action.gd"

const DISTANCE_REF_TARGET_UNIT: StringName = &"target_unit"
const DISTANCE_REF_ENEMY_FRONTLINE: StringName = &"enemy_frontline"

@export var skill_ids: Array[StringName] = []
@export var target_selector: StringName = &"nearest_enemy"
@export var desired_min_distance := -1
@export var desired_max_distance := -1
@export var distance_reference: StringName = &""
@export var candidate_pool_limit := 6
@export var candidate_group_limit := 12


func decide(context):
	AI_TRACE_RECORDER.enter(&"decide:multi_unit_skill")
	var result = _decide_impl(context)
	AI_TRACE_RECORDER.exit(&"decide:multi_unit_skill")
	return result


func _decide_impl(context):
	if not _has_explicit_distance_contract():
		return null
	var action_trace := _begin_action_trace(context, {
		"action_kind": "multi_unit_skill",
		"target_selector": String(target_selector),
		"distance_reference": String(distance_reference),
		"desired_min_distance": desired_min_distance,
		"desired_max_distance": desired_max_distance,
		"candidate_pool_limit": candidate_pool_limit,
		"candidate_group_limit": candidate_group_limit,
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
		if not _is_multi_unit_skill(skill_def):
			_trace_add_block_reason(action_trace, "non_multi_unit_skill")
			continue
		var block_reason := _get_skill_cast_block_reason(context, skill_def)
		if not block_reason.is_empty():
			_trace_add_block_reason(action_trace, block_reason)
			continue
		var sorted_targets = _sort_target_units(context, skill_def.combat_profile.target_team_filter, target_selector)
		if sorted_targets.is_empty():
			_trace_add_block_reason(action_trace, "no_valid_targets")
			continue
		for cast_variant in _get_multi_unit_cast_variants(context, skill_def):
			if cast_variant != null and _is_charge_variant(cast_variant):
				continue
			var target_groups := _build_target_groups(context, skill_def, cast_variant, sorted_targets)
			if target_groups.is_empty():
				_trace_add_block_reason(action_trace, "no_valid_target_groups")
				continue
			for target_group in target_groups:
				_trace_count_increment(action_trace, "evaluation_count", 1)
				var command = _build_multi_unit_skill_command(context, skill_id, cast_variant, target_group)
				var preview = context.preview_command(command)
				if preview == null or not bool(preview.allowed):
					_trace_count_increment(action_trace, "preview_reject_count", 1)
					continue
				var position_metadata := _build_position_metadata(context, target_group, skill_def)
				position_metadata["action_label"] = _format_skill_variant_label(skill_def, cast_variant)
				var score_input = _build_skill_score_input(
					context,
					skill_def,
					command,
					preview,
					_collect_multi_unit_effect_defs(skill_def, cast_variant),
					position_metadata
				)
				var target_count: int = command.target_unit_ids.size()
				if score_input == null:
					if fallback_decision == null:
						fallback_decision = _create_decision(
							command,
							"%s 准备用 %s 锁定 %d 个单位。" % [
								context.unit_state.display_name,
								skill_def.display_name,
								target_count,
							]
						)
					_trace_offer_candidate(action_trace, _build_candidate_summary(
						_format_skill_variant_label(skill_def, cast_variant),
						command,
						null,
						{
							"skill_id": String(skill_id),
							"target_count": target_count,
						}
					))
					continue
				_trace_offer_candidate(action_trace, _build_candidate_summary(
					_format_skill_variant_label(skill_def, cast_variant),
					command,
					score_input,
					{
						"skill_id": String(skill_id),
						"target_count": target_count,
					}
				))
				if not _is_better_skill_score_input(score_input, best_score_input):
					continue
				best_score_input = score_input
				best_decision = _create_scored_decision(
					command,
					score_input,
					"%s 准备用 %s 锁定 %d 个单位（评分 %d）。" % [
						context.unit_state.display_name,
						skill_def.display_name,
						target_count,
						int(score_input.total_score),
					]
				)
	var resolved_decision: BattleAiDecision = best_decision if best_decision != null else fallback_decision
	_finalize_action_trace(context, action_trace, resolved_decision)
	return resolved_decision


func _is_multi_unit_skill(skill_def: SkillDef) -> bool:
	return skill_def != null \
		and skill_def.combat_profile != null \
		and StringName(skill_def.combat_profile.target_selection_mode) == &"multi_unit"


func _get_multi_unit_cast_variants(context, skill_def: SkillDef) -> Array:
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


func _build_target_groups(context, skill_def: SkillDef, cast_variant, sorted_targets: Array) -> Array:
	var groups: Array = []
	var pool := _build_candidate_pool(context, skill_def, cast_variant, sorted_targets)
	if pool.is_empty():
		return groups
	var skill_level := _get_skill_level(context.unit_state, skill_def.skill_id)
	var min_count := maxi(int(skill_def.combat_profile.min_target_count), 1)
	var max_count := maxi(int(skill_def.combat_profile.get_effective_max_target_count(skill_level)), min_count)
	max_count = mini(max_count, pool.size())
	if pool.size() < min_count:
		return groups
	var seen: Dictionary = {}
	for count in range(max_count, min_count - 1, -1):
		if count == 1:
			for target_unit in pool:
				_append_target_group(groups, seen, [target_unit])
				if groups.size() >= candidate_group_limit:
					return groups
			continue
		for start_index in range(0, pool.size() - count + 1):
			var target_group: Array = []
			for offset in range(count):
				target_group.append(pool[start_index + offset])
			_append_target_group(groups, seen, target_group)
			if groups.size() >= candidate_group_limit:
				return groups
	return groups


func _build_candidate_pool(context, skill_def: SkillDef, cast_variant, sorted_targets: Array) -> Array:
	var pool: Array = []
	var min_count := maxi(int(skill_def.combat_profile.min_target_count), 1)
	for target_unit in sorted_targets:
		if pool.size() >= candidate_pool_limit:
			break
		if min_count <= 1:
			var single_command = _build_multi_unit_skill_command(context, skill_def.skill_id, cast_variant, [target_unit])
			var single_preview = context.preview_command(single_command)
			if single_preview == null or not bool(single_preview.allowed):
				continue
		pool.append(target_unit)
	return pool


func _append_target_group(groups: Array, seen: Dictionary, target_group: Array) -> void:
	if target_group.is_empty():
		return
	var key := _target_group_key(target_group)
	if key.is_empty() or seen.has(key):
		return
	seen[key] = true
	groups.append(target_group)


func _target_group_key(target_group: Array) -> String:
	var parts: Array[String] = []
	for target_unit in target_group:
		if target_unit == null:
			continue
		parts.append(String(target_unit.unit_id))
	return "|".join(parts)


func _build_multi_unit_skill_command(context, skill_id: StringName, cast_variant, target_group: Array):
	if context == null or context.unit_state == null:
		return null
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = context.unit_state.unit_id
	command.skill_id = skill_id
	command.skill_variant_id = cast_variant.variant_id if cast_variant != null else &""
	for target_unit in target_group:
		if target_unit == null:
			continue
		command.target_unit_ids.append(target_unit.unit_id)
		if command.target_coord == Vector2i(-1, -1):
			command.target_coord = target_unit.coord
	return command


func _collect_multi_unit_effect_defs(skill_def: SkillDef, cast_variant) -> Array:
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


func _build_position_metadata(context, target_group: Array, skill_def: SkillDef) -> Dictionary:
	var distance_contract := _resolve_desired_distance_contract(context, skill_def)
	var metadata := distance_contract.duplicate(true)
	match distance_reference:
		DISTANCE_REF_TARGET_UNIT:
			var primary_target = target_group[0] if not target_group.is_empty() else null
			if primary_target != null:
				metadata["position_target_unit"] = primary_target
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


func _has_explicit_distance_contract() -> bool:
	return desired_min_distance >= 0 \
		and desired_max_distance >= desired_min_distance \
		and (distance_reference == DISTANCE_REF_TARGET_UNIT or distance_reference == DISTANCE_REF_ENEMY_FRONTLINE)


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if skill_ids.is_empty():
		errors.append("UseMultiUnitSkillAction %s must declare at least one skill_id." % String(action_id))
	if target_selector == &"":
		errors.append("UseMultiUnitSkillAction %s is missing target_selector." % String(action_id))
	if desired_min_distance < 0:
		errors.append("UseMultiUnitSkillAction %s desired_min_distance must be >= 0." % String(action_id))
	if desired_max_distance < desired_min_distance:
		errors.append("UseMultiUnitSkillAction %s desired_max_distance must be >= desired_min_distance." % String(action_id))
	if distance_reference != DISTANCE_REF_TARGET_UNIT and distance_reference != DISTANCE_REF_ENEMY_FRONTLINE:
		errors.append(
			"UseMultiUnitSkillAction %s distance_reference must be target_unit or enemy_frontline." % String(action_id)
		)
	if candidate_pool_limit <= 0:
		errors.append("UseMultiUnitSkillAction %s candidate_pool_limit must be > 0." % String(action_id))
	if candidate_group_limit <= 0:
		errors.append("UseMultiUnitSkillAction %s candidate_group_limit must be > 0." % String(action_id))
	return errors
