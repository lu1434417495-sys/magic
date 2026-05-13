class_name EnemyAiAction
extends Resource

const AI_TRACE_RECORDER = preload("res://scripts/dev_tools/ai_trace_recorder.gd")
const BATTLE_AI_DECISION_SCRIPT = preload("res://scripts/systems/battle/ai/battle_ai_decision.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle/core/battle_command.gd")
const BATTLE_RANGE_SERVICE_SCRIPT = preload("res://scripts/systems/battle/rules/battle_range_service.gd")
const COMBAT_CAST_VARIANT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const ENEMY_AI_ACTION_HELPER_SCRIPT = preload("res://scripts/enemies/enemy_ai_action_helper.gd")
const BattleAiDecision = preload("res://scripts/systems/battle/ai/battle_ai_decision.gd")
const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const TARGET_SELECTOR_NEAREST_ROLE_THREAT_ENEMY: StringName = &"nearest_role_threat_enemy"
const HP_BASIS_POINTS_DENOMINATOR := 10000
const ROLE_THREAT_MIN_EFFECTIVE_RANGE := 4
const ROLE_THREAT_DISTANCE_WINDOW := 4
const ROLE_THREAT_MAX_APPROACH_DISTANCE := 7
const ROLE_THREAT_MAX_CONTACT_RANGE := 2

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
	var range_skill_ids_variant = get("range_skill_ids")
	if range_skill_ids_variant is Array:
		for raw_skill_id in range_skill_ids_variant:
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
	return ENEMY_AI_ACTION_HELPER_SCRIPT.create_decision(action_id, score_bucket_id, command, reason_text)


func _create_scored_decision(command, score_input, reason_text: String = "") -> BattleAiDecision:
	return ENEMY_AI_ACTION_HELPER_SCRIPT.create_scored_decision(action_id, score_bucket_id, command, score_input, reason_text)


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
	var costs: Dictionary = combat_profile.get_effective_resource_costs(_get_skill_level(unit_state, skill_def.skill_id))
	var cooldown := int(unit_state.cooldowns.get(skill_def.skill_id, 0))
	if cooldown > 0:
		return "%s 仍在冷却中（%d）。" % [skill_def.display_name, cooldown]
	var locked_resource_block_reason := _get_locked_combat_resource_block_reason(unit_state, costs)
	if not locked_resource_block_reason.is_empty():
		return locked_resource_block_reason
	if unit_state.current_ap < int(costs.get("ap_cost", combat_profile.ap_cost)):
		return "AP不足，无法施放该技能。"
	if unit_state.current_mp < int(costs.get("mp_cost", combat_profile.mp_cost)):
		return "法力不足，无法施放该技能。"
	if unit_state.current_stamina < int(costs.get("stamina_cost", combat_profile.stamina_cost)):
		return "体力不足，无法施放该技能。"
	if unit_state.current_aura < int(costs.get("aura_cost", combat_profile.aura_cost)):
		return "斗气不足，无法施放该技能。"
	return ""


func _get_locked_combat_resource_block_reason(unit_state: BattleUnitState, costs: Dictionary) -> String:
	if unit_state == null:
		return "技能施放者无效。"
	if int(costs.get("mp_cost", 0)) > 0 and not unit_state.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_MP):
		return "法力尚未解锁，无法施放该技能。"
	if int(costs.get("stamina_cost", 0)) > 0 and not unit_state.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_STAMINA):
		return "体力尚未解锁，无法施放该技能。"
	if int(costs.get("aura_cost", 0)) > 0 and not unit_state.has_combat_resource_unlocked(BattleUnitState.COMBAT_RESOURCE_AURA):
		return "斗气尚未解锁，无法施放该技能。"
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
	if int(candidate.estimated_friendly_lethal_target_count) != int(best_candidate.estimated_friendly_lethal_target_count):
		return int(candidate.estimated_friendly_lethal_target_count) < int(best_candidate.estimated_friendly_lethal_target_count)
	if int(candidate.estimated_friendly_fire_target_count) != int(best_candidate.estimated_friendly_fire_target_count):
		return int(candidate.estimated_friendly_fire_target_count) < int(best_candidate.estimated_friendly_fire_target_count)
	if int(candidate.friendly_fire_penalty_score) != int(best_candidate.friendly_fire_penalty_score):
		return int(candidate.friendly_fire_penalty_score) < int(best_candidate.friendly_fire_penalty_score)
	var survival_risk_comparison := _compare_post_action_survival_risk(candidate, best_candidate)
	if survival_risk_comparison != 0:
		return survival_risk_comparison > 0
	if int(candidate.estimated_lethal_threat_target_count) != int(best_candidate.estimated_lethal_threat_target_count):
		return int(candidate.estimated_lethal_threat_target_count) > int(best_candidate.estimated_lethal_threat_target_count)
	if int(candidate.estimated_lethal_target_count) != int(best_candidate.estimated_lethal_target_count):
		return int(candidate.estimated_lethal_target_count) > int(best_candidate.estimated_lethal_target_count)
	var candidate_is_emergency_survival := _is_emergency_survival_score_input(candidate)
	var best_is_emergency_survival := _is_emergency_survival_score_input(best_candidate)
	if candidate_is_emergency_survival != best_is_emergency_survival:
		return candidate_is_emergency_survival
	if int(candidate.estimated_lethal_target_count) > 0 and int(best_candidate.estimated_lethal_target_count) > 0:
		if int(candidate.total_score) != int(best_candidate.total_score):
			return int(candidate.total_score) > int(best_candidate.total_score)
		if int(candidate.hit_payoff_score) != int(best_candidate.hit_payoff_score):
			return int(candidate.hit_payoff_score) > int(best_candidate.hit_payoff_score)
		if int(candidate.effective_target_count) != int(best_candidate.effective_target_count):
			return int(candidate.effective_target_count) > int(best_candidate.effective_target_count)
		if int(candidate.resource_cost_score) != int(best_candidate.resource_cost_score):
			return int(candidate.resource_cost_score) < int(best_candidate.resource_cost_score)
	if int(candidate.score_bucket_priority) != int(best_candidate.score_bucket_priority):
		return int(candidate.score_bucket_priority) > int(best_candidate.score_bucket_priority)
	if int(candidate.total_score) != int(best_candidate.total_score):
		return int(candidate.total_score) > int(best_candidate.total_score)
	if int(candidate.hit_payoff_score) != int(best_candidate.hit_payoff_score):
		return int(candidate.hit_payoff_score) > int(best_candidate.hit_payoff_score)
	if int(candidate.effective_target_count) != int(best_candidate.effective_target_count):
		return int(candidate.effective_target_count) > int(best_candidate.effective_target_count)
	if int(candidate.target_count) != int(best_candidate.target_count):
		return int(candidate.target_count) > int(best_candidate.target_count)
	if int(candidate.position_objective_score) != int(best_candidate.position_objective_score):
		return int(candidate.position_objective_score) > int(best_candidate.position_objective_score)
	return int(candidate.resource_cost_score) < int(best_candidate.resource_cost_score)


func _is_emergency_survival_score_input(score_input) -> bool:
	if score_input == null:
		return false
	if score_input.score_bucket_id != &"archer_survival":
		return false
	if bool(score_input.has_post_action_threat_projection):
		if bool(score_input.pre_action_is_lethal_survival_risk) and not bool(score_input.post_action_is_lethal_survival_risk):
			return true
		if int(score_input.pre_action_threat_expected_damage) > int(score_input.post_action_remaining_threat_expected_damage) \
				and int(score_input.post_action_survival_margin) >= 0:
			return true
	if int(score_input.target_count) > 0 or int(score_input.effective_target_count) > 0:
		return false
	if int(score_input.enemy_target_count) > 0 or int(score_input.ally_target_count) > 0:
		return false
	if int(score_input.estimated_damage) != 0 or int(score_input.estimated_control_count) != 0:
		return false
	if int(score_input.position_current_distance) >= 0 and int(score_input.position_safe_distance) > 0:
		var current_gap := int(score_input.position_safe_distance) - int(score_input.position_current_distance)
		if current_gap < 2:
			return false
		if int(score_input.distance_to_primary_coord) >= 0:
			return int(score_input.distance_to_primary_coord) >= int(score_input.position_safe_distance)
		return int(score_input.position_objective_score) > 0
	return int(score_input.position_objective_score) > 0


func _compare_post_action_survival_risk(candidate, best_candidate) -> int:
	if candidate == null or best_candidate == null:
		return 0
	if not bool(candidate.has_post_action_threat_projection) or not bool(best_candidate.has_post_action_threat_projection):
		return 0
	var candidate_fatal := bool(candidate.post_action_is_lethal_survival_risk)
	var best_fatal := bool(best_candidate.post_action_is_lethal_survival_risk)
	if candidate_fatal != best_fatal:
		return -1 if candidate_fatal else 1
	return 0


func _build_wait_command(context):
	return ENEMY_AI_ACTION_HELPER_SCRIPT.build_wait_command(context)


func _build_move_command(context, target_coord: Vector2i):
	return ENEMY_AI_ACTION_HELPER_SCRIPT.build_move_command(context, target_coord)


func _build_unit_skill_command(context, skill_id: StringName, target_unit):
	return ENEMY_AI_ACTION_HELPER_SCRIPT.build_unit_skill_command(context, skill_id, target_unit)


func _build_ground_skill_command(context, skill_id: StringName, skill_variant_id: StringName, target_coords: Array):
	return ENEMY_AI_ACTION_HELPER_SCRIPT.build_ground_skill_command(context, skill_id, skill_variant_id, target_coords)


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
	if bool(context.unit_state.ai_blackboard.get("madness_target_any_team", false)) \
			and target_filter != &"self":
		return unit_state.unit_id != context.unit_state.unit_id
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
	if context != null \
			and context.unit_state != null \
			and bool(context.unit_state.ai_blackboard.get("madness_target_any_team", false)) \
			and selector != &"self":
		effective_filter = &"any"
	elif selector == &"nearest_enemy" \
			or selector == &"lowest_hp_enemy" \
			or selector == TARGET_SELECTOR_NEAREST_ROLE_THREAT_ENEMY:
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
	var nearest_distance := _resolve_nearest_distance(context, units)
	units.sort_custom(func(left: BattleUnitState, right: BattleUnitState) -> bool:
		var left_hp_basis_points := _get_hp_basis_points(left)
		var right_hp_basis_points := _get_hp_basis_points(right)
		var left_distance = _distance_between_units(context, context.unit_state, left)
		var right_distance = _distance_between_units(context, context.unit_state, right)
		if selector == TARGET_SELECTOR_NEAREST_ROLE_THREAT_ENEMY:
			var left_threat_score := _get_role_threat_selector_score(context, left, nearest_distance, left_distance)
			var right_threat_score := _get_role_threat_selector_score(context, right, nearest_distance, right_distance)
			if left_threat_score != right_threat_score:
				return left_threat_score > right_threat_score
		if selector == &"lowest_hp_enemy" or selector == &"lowest_hp_ally":
			if left_hp_basis_points != right_hp_basis_points:
				return left_hp_basis_points < right_hp_basis_points
			return left_distance < right_distance
		if left_distance == right_distance:
			return left_hp_basis_points < right_hp_basis_points
		return left_distance < right_distance
	)
	return units


func _resolve_nearest_distance(context, units: Array) -> int:
	var nearest_distance := 999999
	for unit_variant in units:
		var unit_state = unit_variant as BattleUnitState
		if unit_state == null:
			continue
		nearest_distance = mini(nearest_distance, _distance_between_units(context, context.unit_state, unit_state))
	return nearest_distance


func _get_role_threat_selector_score(
	context,
	unit_state: BattleUnitState,
	nearest_distance: int,
	distance: int
) -> int:
	if unit_state == null:
		return 0
	var threat_range := _resolve_unit_effective_threat_range(context, unit_state)
	var is_local_role_threat := threat_range >= ROLE_THREAT_MIN_EFFECTIVE_RANGE \
		and distance <= nearest_distance + ROLE_THREAT_DISTANCE_WINDOW \
		and distance <= ROLE_THREAT_MAX_APPROACH_DISTANCE
	if is_local_role_threat:
		return 1000 + threat_range * 10
	if _resolve_unit_contact_threat_range(context, unit_state) > 0:
		return 500
	return 0


func _resolve_unit_contact_threat_range(context, threat_unit: BattleUnitState) -> int:
	if context == null or threat_unit == null:
		return -1
	var best_range := -1
	for raw_skill_id in threat_unit.known_active_skill_ids:
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		if skill_id == &"":
			continue
		var skill_def = context.skill_defs.get(skill_id) as SkillDef
		if not _is_hostile_threat_skill(skill_def):
			continue
		if not _skill_has_tag(skill_def, &"melee") and not _skill_has_tag(skill_def, &"weapon"):
			continue
		var effective_range := BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_range(threat_unit, skill_def)
		if effective_range <= 0 and _skill_has_tag(skill_def, &"melee"):
			effective_range = 1
		if effective_range > ROLE_THREAT_MAX_CONTACT_RANGE:
			continue
		best_range = maxi(best_range, effective_range)
	var weapon_range := BATTLE_RANGE_SERVICE_SCRIPT.get_weapon_attack_range(threat_unit)
	if weapon_range > 0 and weapon_range <= ROLE_THREAT_MAX_CONTACT_RANGE:
		best_range = maxi(best_range, weapon_range)
	return best_range


func _resolve_forced_target_unit(context, target_filter: StringName):
	if context == null or not context.has_method("resolve_forced_target_unit"):
		return null
	return context.resolve_forced_target_unit(target_filter)


func _get_hp_basis_points(unit_state: BattleUnitState) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return HP_BASIS_POINTS_DENOMINATOR
	var hp_max = maxi(int(unit_state.attribute_snapshot.get_value(&"hp_max")), 1)
	var current_hp := clampi(int(unit_state.current_hp), 0, hp_max)
	return clampi(int((current_hp * HP_BASIS_POINTS_DENOMINATOR) / hp_max), 0, HP_BASIS_POINTS_DENOMINATOR)


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


func _resolve_desired_distance_contract(
	context,
	skill_def: SkillDef = null,
	range_skill_ids: Array[StringName] = []
) -> Dictionary:
	var configured_min := int(get("desired_min_distance"))
	var configured_max := int(get("desired_max_distance"))
	var effective_attack_range := _resolve_effective_attack_range(context, skill_def, range_skill_ids)
	var resolved_max := configured_max
	if effective_attack_range >= 0:
		resolved_max = effective_attack_range
	var resolved_min := configured_min
	if resolved_max >= 0 and resolved_min > resolved_max:
		resolved_min = resolved_max
	return {
		"desired_min_distance": resolved_min,
		"desired_max_distance": maxi(resolved_max, resolved_min),
		"configured_desired_min_distance": configured_min,
		"configured_desired_max_distance": configured_max,
		"effective_attack_range": effective_attack_range,
	}


func _resolve_effective_attack_range(context, skill_def: SkillDef = null, range_skill_ids: Array[StringName] = []) -> int:
	if context == null or context.unit_state == null:
		return -1
	if skill_def != null:
		return BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(context.unit_state, skill_def)
	var best_range := -1
	for skill_id in _resolve_known_skill_ids(context, range_skill_ids):
		var candidate_skill_def := _get_skill_def(context, skill_id)
		if candidate_skill_def == null or candidate_skill_def.combat_profile == null:
			continue
		var block_reason := _get_skill_cast_block_reason(context, candidate_skill_def)
		if not block_reason.is_empty():
			continue
		best_range = maxi(best_range, BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(context.unit_state, candidate_skill_def))
	return best_range


func _resolve_target_safe_distance(
	context,
	target_unit: BattleUnitState,
	configured_minimum_safe_distance: int,
	safe_distance_margin: int = 1
) -> int:
	var resolved_minimum := maxi(configured_minimum_safe_distance, 0)
	var threat_range := _resolve_unit_effective_threat_range(context, target_unit)
	if threat_range <= 0:
		return resolved_minimum
	return maxi(resolved_minimum, threat_range + maxi(safe_distance_margin, 0))


func _resolve_unit_effective_threat_range(context, threat_unit: BattleUnitState) -> int:
	if context == null or threat_unit == null:
		return -1
	var best_range := -1
	for raw_skill_id in threat_unit.known_active_skill_ids:
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		if skill_id == &"":
			continue
		var skill_def = context.skill_defs.get(skill_id) as SkillDef
		if not _is_hostile_threat_skill(skill_def):
			continue
		best_range = maxi(best_range, BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_threat_range(threat_unit, skill_def))
	if best_range < 0:
		best_range = BATTLE_RANGE_SERVICE_SCRIPT.get_weapon_attack_range(threat_unit)
	return best_range


func _select_most_unsafe_target(
	context,
	targets: Array,
	anchor_coord: Vector2i,
	configured_minimum_safe_distance: int,
	safe_distance_margin: int = 1
) -> BattleUnitState:
	var best_target: BattleUnitState = null
	var best_gap := -1
	var best_distance := 999999
	for target_variant in targets:
		var target_unit = target_variant as BattleUnitState
		if target_unit == null:
			continue
		var distance := _distance_from_anchor_to_unit(context, context.unit_state, anchor_coord, target_unit)
		var safe_distance := _resolve_target_safe_distance(
			context,
			target_unit,
			configured_minimum_safe_distance,
			safe_distance_margin
		)
		var unsafe_gap := maxi(safe_distance - distance, 0)
		if best_target == null \
				or unsafe_gap > best_gap \
				or (unsafe_gap == best_gap and distance < best_distance):
			best_target = target_unit
			best_gap = unsafe_gap
			best_distance = distance
	return best_target


func _is_hostile_threat_skill(skill_def: SkillDef) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	var target_filter := ProgressionDataUtils.to_string_name(skill_def.combat_profile.target_team_filter)
	if target_filter == &"ally" or target_filter == &"self":
		return false
	if _skill_has_tag(skill_def, &"output") \
			or _skill_has_tag(skill_def, &"melee") \
			or _skill_has_tag(skill_def, &"bow") \
			or _skill_has_tag(skill_def, &"weapon"):
		return true
	if _effect_list_has_hostile_threat(skill_def.combat_profile.effect_defs):
		return true
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant != null and _effect_list_has_hostile_threat(cast_variant.effect_defs):
			return true
	return false


func _skill_has_tag(skill_def: SkillDef, expected_tag: StringName) -> bool:
	if skill_def == null or expected_tag == &"":
		return false
	for tag in skill_def.tags:
		if ProgressionDataUtils.to_string_name(tag) == expected_tag:
			return true
	return false


func _effect_list_has_hostile_threat(effect_defs: Array) -> bool:
	for effect_def in effect_defs:
		if effect_def == null:
			continue
		var effect_type := ProgressionDataUtils.to_string_name(effect_def.effect_type)
		if effect_type == &"damage" \
				or effect_type == &"chain_damage" \
				or effect_type == &"charge" \
				or effect_type == &"forced_move" \
				or effect_type == &"path_step_aoe" \
				or effect_type == &"status":
			return true
	return false


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
	return ENEMY_AI_ACTION_HELPER_SCRIPT.sort_coords(coords)


func _coord_set_key(coords: Array[Vector2i]) -> String:
	return ENEMY_AI_ACTION_HELPER_SCRIPT.coord_set_key(coords)


func _begin_action_trace(context, metadata: Dictionary = {}) -> Dictionary:
	return ENEMY_AI_ACTION_HELPER_SCRIPT.begin_action_trace(action_id, score_bucket_id, context, metadata)


func _trace_count_increment(action_trace: Dictionary, key: String, amount: int = 1) -> void:
	ENEMY_AI_ACTION_HELPER_SCRIPT.trace_count_increment(action_trace, key, amount)


func _trace_add_block_reason(action_trace: Dictionary, reason_key: String) -> void:
	ENEMY_AI_ACTION_HELPER_SCRIPT.trace_add_block_reason(action_trace, reason_key)


func _trace_offer_candidate(action_trace: Dictionary, candidate_summary: Dictionary, keep_count: int = 5) -> void:
	ENEMY_AI_ACTION_HELPER_SCRIPT.trace_offer_candidate(action_trace, candidate_summary, keep_count)


func _finalize_action_trace(context, action_trace: Dictionary, best_decision: BattleAiDecision = null) -> StringName:
	return ENEMY_AI_ACTION_HELPER_SCRIPT.finalize_action_trace(context, action_trace, best_decision)


func _build_candidate_summary(label: String, command, score_input = null, extra: Dictionary = {}) -> Dictionary:
	return ENEMY_AI_ACTION_HELPER_SCRIPT.build_candidate_summary(label, command, score_input, extra)


func _format_skill_variant_label(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> String:
	return ENEMY_AI_ACTION_HELPER_SCRIPT.format_skill_variant_label(skill_def, cast_variant)


func _build_command_summary(command) -> Dictionary:
	return ENEMY_AI_ACTION_HELPER_SCRIPT.build_command_summary(command)
