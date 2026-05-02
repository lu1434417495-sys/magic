class_name EnemyAiAction
extends Resource

const BATTLE_AI_DECISION_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_decision.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const COMBAT_CAST_VARIANT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const BattleAiDecision = preload("res://scripts/systems/battle/ai/battle_ai_decision.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

@export var action_id: StringName = &""
@export var score_bucket_id: StringName = &""


func decide(_context):
	return null


func validate_schema() -> Array[String]:
	return _collect_base_validation_errors()


func get_declared_skill_ids() -> Array[StringName]:
	var results: Array[StringName] = []
	var seen: Dictionary = {}
	_append_declared_skill_id(results, seen, get("skill_id"))
	var skill_ids_variant = get("skill_ids")
	if skill_ids_variant is Array:
		for raw_skill_id in skill_ids_variant:
			_append_declared_skill_id(results, seen, raw_skill_id)
	return results


func validate_skill_references(skill_defs: Dictionary) -> Array[String]:
	var errors: Array[String] = []
	for skill_id in get_declared_skill_ids():
		if skill_id == &"":
			errors.append("AI action %s references an empty skill_id." % String(action_id))
			continue
		if not skill_defs.has(skill_id):
			errors.append("AI action %s references missing skill %s." % [String(action_id), String(skill_id)])
	return errors


func _collect_base_validation_errors() -> Array[String]:
	var errors: Array[String] = []
	if action_id == &"":
		errors.append("AI action is missing action_id.")
	return errors


func _append_declared_skill_id(results: Array[StringName], seen: Dictionary, raw_skill_id: Variant) -> void:
	if raw_skill_id is not String and raw_skill_id is not StringName:
		return
	var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
	if seen.has(skill_id):
		return
	seen[skill_id] = true
	results.append(skill_id)


func _create_decision(command, reason_text: String = "") -> BattleAiDecision:
	var decision = BATTLE_AI_DECISION_SCRIPT.new()
	decision.command = command
	decision.action_id = action_id
	decision.reason_text = reason_text
	decision.score_bucket_id = score_bucket_id
	return decision


func _create_scored_decision(command, score_input, reason_text: String = "") -> BattleAiDecision:
	var decision = _create_decision(command, reason_text)
	decision.skill_score_input = score_input
	decision.score_input = score_input
	return decision


func _resolve_known_skill_ids(context, preferred_skill_ids: Array[StringName]) -> Array[StringName]:
	var results: Array[StringName] = []
	if context == null or context.unit_state == null:
		return results
	var seen: Dictionary = {}
	var source_ids: Array[StringName] = preferred_skill_ids if not preferred_skill_ids.is_empty() else context.unit_state.known_active_skill_ids
	for raw_skill_id in source_ids:
		var skill_id = StringName(String(raw_skill_id))
		if skill_id == &"" or seen.has(skill_id):
			continue
		seen[skill_id] = true
		if context.unit_state.known_active_skill_ids.has(skill_id):
			results.append(skill_id)
	return results


func _get_skill_def(context, skill_id: StringName) -> SkillDef:
	if context == null or skill_id == &"":
		return null
	return context.skill_defs.get(skill_id) as SkillDef


func _get_skill_cast_block_reason(context, skill_def: SkillDef) -> String:
	if context == null or context.unit_state == null or skill_def == null or skill_def.combat_profile == null:
		return "技能或目标无效。"
	var unit_state: BattleUnitState = context.unit_state
	var combat_profile = skill_def.combat_profile
	var costs := combat_profile.get_effective_resource_costs(_get_skill_level(unit_state, skill_def.skill_id))
	var cooldown := int(unit_state.cooldowns.get(skill_def.skill_id, 0))
	if cooldown > 0:
		return "%s 仍在冷却中（%d）。" % [skill_def.display_name, cooldown]
	if unit_state.current_ap < int(costs.get("ap_cost", combat_profile.ap_cost)):
		return "AP不足，无法施放该技能。"
	if unit_state.current_mp < int(costs.get("mp_cost", combat_profile.mp_cost)):
		return "法力不足，无法施放该技能。"
	if unit_state.current_stamina < int(costs.get("stamina_cost", combat_profile.stamina_cost)):
		return "体力不足，无法施放该技能。"
	if unit_state.current_aura < int(costs.get("aura_cost", combat_profile.aura_cost)):
		return "斗气不足，无法施放该技能。"
	return ""


func _preview_allowed(context, command) -> bool:
	if context == null or command == null:
		return false
	var preview = context.preview_command(command)
	return preview != null and bool(preview.allowed)


func _build_skill_score_input(
	context,
	skill_def: SkillDef,
	command,
	preview,
	effect_defs: Array = [],
	metadata: Dictionary = {}
):
	if context == null:
		return null
	var scoring_metadata := metadata.duplicate(true)
	scoring_metadata["score_bucket_id"] = score_bucket_id
	scoring_metadata["action_kind"] = ProgressionDataUtils.to_string_name(scoring_metadata.get("action_kind", "skill"))
	scoring_metadata["action_label"] = String(scoring_metadata.get("action_label", skill_def.display_name if skill_def != null else String(action_id)))
	return context.build_skill_score_input(skill_def, command, preview, effect_defs, scoring_metadata)


func _build_action_score_input(
	context,
	action_kind: StringName,
	action_label: String,
	command,
	preview,
	metadata: Dictionary = {}
):
	if context == null:
		return null
	return context.build_action_score_input(
		action_kind,
		action_label,
		score_bucket_id,
		command,
		preview,
		metadata
	)


func _is_better_skill_score_input(candidate, best_candidate) -> bool:
	if candidate == null:
		return false
	if best_candidate == null:
		return true
	if int(candidate.score_bucket_priority) != int(best_candidate.score_bucket_priority):
		return int(candidate.score_bucket_priority) > int(best_candidate.score_bucket_priority)
	if int(candidate.total_score) != int(best_candidate.total_score):
		return int(candidate.total_score) > int(best_candidate.total_score)
	if int(candidate.hit_payoff_score) != int(best_candidate.hit_payoff_score):
		return int(candidate.hit_payoff_score) > int(best_candidate.hit_payoff_score)
	if int(candidate.target_count) != int(best_candidate.target_count):
		return int(candidate.target_count) > int(best_candidate.target_count)
	if int(candidate.position_objective_score) != int(best_candidate.position_objective_score):
		return int(candidate.position_objective_score) > int(best_candidate.position_objective_score)
	return int(candidate.resource_cost_score) < int(best_candidate.resource_cost_score)


func _build_wait_command(context):
	if context == null or context.unit_state == null:
		return null
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_WAIT
	command.unit_id = context.unit_state.unit_id
	return command


func _build_move_command(context, target_coord: Vector2i):
	if context == null or context.unit_state == null:
		return null
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_MOVE
	command.unit_id = context.unit_state.unit_id
	command.target_coord = target_coord
	return command


func _build_unit_skill_command(context, skill_id: StringName, target_unit):
	if context == null or context.unit_state == null or target_unit == null:
		return null
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = context.unit_state.unit_id
	command.skill_id = skill_id
	command.target_unit_id = target_unit.unit_id
	command.target_coord = target_unit.coord
	return command


func _build_ground_skill_command(context, skill_id: StringName, skill_variant_id: StringName, target_coords: Array):
	if context == null or context.unit_state == null:
		return null
	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = context.unit_state.unit_id
	command.skill_id = skill_id
	command.skill_variant_id = skill_variant_id
	command.target_coords = _sort_coords(target_coords)
	if not command.target_coords.is_empty():
		command.target_coord = command.target_coords[0]
	return command


func _collect_units_by_filter(context, target_filter: StringName) -> Array:
	var results: Array = []
	if context == null or context.state == null or context.unit_state == null:
		return results
	for unit_id in context.state.units.keys():
		var unit_state = context.state.units.get(unit_id) as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		if not _matches_target_filter(context, unit_state, target_filter):
			continue
		results.append(unit_state)
	return results


func _matches_target_filter(context, unit_state: BattleUnitState, target_filter: StringName) -> bool:
	if context == null or context.unit_state == null or unit_state == null:
		return false
	match target_filter:
		&"enemy":
			return unit_state.faction_id != context.unit_state.faction_id
		&"ally":
			return unit_state.faction_id == context.unit_state.faction_id
		&"self":
			return unit_state.unit_id == context.unit_state.unit_id
		_:
			return true


func _sort_target_units(context, target_filter: StringName, selector: StringName) -> Array:
	var effective_filter = target_filter
	if selector == &"nearest_enemy" or selector == &"lowest_hp_enemy":
		effective_filter = &"enemy"
	elif selector == &"nearest_ally" or selector == &"lowest_hp_ally":
		effective_filter = &"ally"
	elif selector == &"self":
		effective_filter = &"self"
	var units = _collect_units_by_filter(context, effective_filter)
	var forced_target = _resolve_forced_target_unit(context, effective_filter)
	if forced_target != null:
		return [forced_target]
	if selector == &"self":
		return units
	units.sort_custom(func(left: BattleUnitState, right: BattleUnitState) -> bool:
		var left_hp_ratio = _get_hp_ratio(left)
		var right_hp_ratio = _get_hp_ratio(right)
		var left_distance = _distance_between_units(context, context.unit_state, left)
		var right_distance = _distance_between_units(context, context.unit_state, right)
		if selector == &"lowest_hp_enemy" or selector == &"lowest_hp_ally":
			if !is_equal_approx(left_hp_ratio, right_hp_ratio):
				return left_hp_ratio < right_hp_ratio
			return left_distance < right_distance
		if left_distance == right_distance:
			return left_hp_ratio < right_hp_ratio
		return left_distance < right_distance
	)
	return units


func _resolve_forced_target_unit(context, target_filter: StringName):
	if context == null or not context.has_method("resolve_forced_target_unit"):
		return null
	return context.resolve_forced_target_unit(target_filter)


func _get_hp_ratio(unit_state: BattleUnitState) -> float:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 1.0
	var hp_max = maxi(int(unit_state.attribute_snapshot.get_value(&"hp_max")), 1)
	return clampf(float(unit_state.current_hp) / float(hp_max), 0.0, 1.0)


func _distance_between_units(context, first_unit: BattleUnitState, second_unit: BattleUnitState) -> int:
	if context == null or context.grid_service == null:
		return 999999
	return context.grid_service.get_distance_between_units(first_unit, second_unit)


func _distance_from_anchor_to_unit(context, unit_state: BattleUnitState, anchor_coord: Vector2i, target_unit: BattleUnitState) -> int:
	if context == null or context.grid_service == null or unit_state == null or target_unit == null:
		return 999999
	unit_state.refresh_footprint()
	target_unit.refresh_footprint()
	var best_distance = 999999
	for source_coord in context.grid_service.get_footprint_coords(anchor_coord, unit_state.footprint_size):
		for target_coord in target_unit.occupied_coords:
			best_distance = mini(best_distance, context.grid_service.get_distance(source_coord, target_coord))
	return best_distance


func _get_skill_level(unit_state: BattleUnitState, skill_id: StringName) -> int:
	if unit_state == null or skill_id == &"":
		return 0
	if unit_state.known_skill_level_map.has(skill_id):
		return int(unit_state.known_skill_level_map.get(skill_id, 0))
	return 1 if unit_state.known_active_skill_ids.has(skill_id) else 0


func _get_ground_variants(context, skill_def: SkillDef) -> Array:
	var variants: Array = []
	if skill_def == null or skill_def.combat_profile == null or skill_def.combat_profile.target_mode != &"ground":
		return variants
	if skill_def.combat_profile.cast_variants.is_empty():
		variants.append(_build_implicit_ground_variant(skill_def))
		return variants
	var skill_level = _get_skill_level(context.unit_state, skill_def.skill_id)
	for cast_variant in skill_def.combat_profile.get_unlocked_cast_variants(skill_level):
		if cast_variant != null:
			variants.append(cast_variant)
	return variants


func _build_implicit_ground_variant(skill_def: SkillDef) -> CombatCastVariantDef:
	var cast_variant = COMBAT_CAST_VARIANT_DEF_SCRIPT.new()
	cast_variant.variant_id = &""
	cast_variant.display_name = ""
	cast_variant.target_mode = &"ground"
	cast_variant.footprint_pattern = &"single"
	cast_variant.required_coord_count = 1
	cast_variant.effect_defs = skill_def.combat_profile.effect_defs.duplicate()
	return cast_variant


func _is_charge_variant(cast_variant: CombatCastVariantDef) -> bool:
	if cast_variant == null:
		return false
	for effect_def in cast_variant.effect_defs:
		if effect_def != null and effect_def.effect_type == &"charge":
			return true
	return false


func _enumerate_ground_target_coord_sets(context, cast_variant: CombatCastVariantDef) -> Array:
	var results: Array = []
	if context == null or context.state == null or context.grid_service == null or cast_variant == null:
		return results
	var seen: Dictionary = {}
	match cast_variant.footprint_pattern:
		&"line2":
			for y in range(context.state.map_size.y):
				for x in range(context.state.map_size.x):
					var first = Vector2i(x, y)
					for direction in [Vector2i.RIGHT, Vector2i.DOWN]:
						var second = first + direction
						if not context.grid_service.is_inside(context.state, second):
							continue
						var pair = _sort_coords([first, second])
						var key = _coord_set_key(pair)
						if seen.has(key):
							continue
						seen[key] = true
						results.append(pair)
		&"square2":
			for y in range(maxi(context.state.map_size.y - 1, 0)):
				for x in range(maxi(context.state.map_size.x - 1, 0)):
					var coords = _sort_coords([
						Vector2i(x, y),
						Vector2i(x + 1, y),
						Vector2i(x, y + 1),
						Vector2i(x + 1, y + 1),
					])
					var key = _coord_set_key(coords)
					if seen.has(key):
						continue
					seen[key] = true
					results.append(coords)
		_:
			for y in range(context.state.map_size.y):
				for x in range(context.state.map_size.x):
					results.append([Vector2i(x, y)])
	return results


func _sort_coords(coords: Array) -> Array[Vector2i]:
	var sorted_coords: Array[Vector2i] = []
	for coord_variant in coords:
		if coord_variant is Vector2i:
			sorted_coords.append(coord_variant)
	sorted_coords.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		return left.y < right.y or (left.y == right.y and left.x < right.x)
	)
	return sorted_coords


func _coord_set_key(coords: Array[Vector2i]) -> String:
	var parts: Array[String] = []
	for coord in _sort_coords(coords):
		parts.append("%d:%d" % [coord.x, coord.y])
	return "|".join(parts)


func _begin_action_trace(context, metadata: Dictionary = {}) -> Dictionary:
	var trace_id: StringName = context.next_action_trace_id(action_id) if context != null and context.has_method("next_action_trace_id") else action_id
	var action_trace := {
		"trace_id": trace_id,
		"action_id": String(action_id),
		"score_bucket_id": String(score_bucket_id),
		"metadata": metadata.duplicate(true),
		"evaluation_count": 0,
		"blocked_count": 0,
		"preview_reject_count": 0,
		"candidate_count": 0,
		"block_reasons": {},
		"top_candidates": [],
		"chosen": false,
	}
	return action_trace


func _trace_count_increment(action_trace: Dictionary, key: String, amount: int = 1) -> void:
	if action_trace.is_empty() or key.is_empty():
		return
	action_trace[key] = int(action_trace.get(key, 0)) + amount


func _trace_add_block_reason(action_trace: Dictionary, reason_key: String) -> void:
	if action_trace.is_empty() or reason_key.is_empty():
		return
	_trace_count_increment(action_trace, "blocked_count", 1)
	var block_reasons: Dictionary = action_trace.get("block_reasons", {})
	block_reasons[reason_key] = int(block_reasons.get(reason_key, 0)) + 1
	action_trace["block_reasons"] = block_reasons


func _trace_offer_candidate(action_trace: Dictionary, candidate_summary: Dictionary, keep_count: int = 5) -> void:
	if action_trace.is_empty() or candidate_summary.is_empty():
		return
	_trace_count_increment(action_trace, "candidate_count", 1)
	var top_candidates = action_trace.get("top_candidates", [])
	if top_candidates is not Array:
		top_candidates = []
	top_candidates.append(candidate_summary.duplicate(true))
	top_candidates.sort_custom(func(left: Dictionary, right: Dictionary) -> bool:
		return int(left.get("total_score", -999999)) > int(right.get("total_score", -999999))
	)
	while top_candidates.size() > keep_count:
		top_candidates.pop_back()
	action_trace["top_candidates"] = top_candidates


func _finalize_action_trace(context, action_trace: Dictionary, best_decision: BattleAiDecision = null) -> StringName:
	if action_trace.is_empty():
		return &""
	if best_decision != null:
		action_trace["best_reason_text"] = best_decision.reason_text
		action_trace["best_command"] = _build_command_summary(best_decision.command)
		var score_input = best_decision.score_input if best_decision.score_input != null else best_decision.skill_score_input
		action_trace["best_score_input"] = score_input.to_dict() if score_input != null else {}
		best_decision.action_trace_id = ProgressionDataUtils.to_string_name(action_trace.get("trace_id", ""))
	if context != null and context.has_method("record_action_trace"):
		context.record_action_trace(action_trace)
	return ProgressionDataUtils.to_string_name(action_trace.get("trace_id", ""))


func _build_candidate_summary(label: String, command, score_input = null, extra: Dictionary = {}) -> Dictionary:
	var summary := {
		"label": label,
		"command": _build_command_summary(command),
		"total_score": int(score_input.total_score) if score_input != null else int(extra.get("total_score", 0)),
		"score_input": score_input.to_dict() if score_input != null else {},
	}
	for key in extra.keys():
		summary[key] = extra.get(key)
	return summary


func _format_skill_variant_label(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> String:
	if skill_def == null:
		return ""
	if cast_variant == null or cast_variant.display_name.is_empty():
		return skill_def.display_name
	return "%s·%s" % [skill_def.display_name, cast_variant.display_name]


func _build_command_summary(command) -> Dictionary:
	if command == null:
		return {}
	return {
		"command_type": String(command.command_type),
		"unit_id": String(command.unit_id),
		"skill_id": String(command.skill_id),
		"skill_variant_id": String(command.skill_variant_id),
		"target_unit_id": String(command.target_unit_id),
		"target_unit_ids": command.target_unit_ids.duplicate(),
		"target_coord": command.target_coord,
		"target_coords": command.target_coords.duplicate(),
	}
