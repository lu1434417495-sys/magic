class_name MoveToRangeAction
extends "res://scripts/enemies/enemy_ai_action.gd"

const SCREENING_NONE: StringName = &"none"
const SCREENING_RANGED_ALLY: StringName = &"ranged_ally"
const SCREENING_PATH_UNREACHABLE_COST := 2147483647

@export var target_selector: StringName = &"nearest_enemy"
@export var desired_min_distance := 1
@export var desired_max_distance := 1
@export var range_skill_ids: Array[StringName] = []
@export var screening_mode: StringName = SCREENING_NONE
@export var screening_min_hp_basis_points := 4000
@export var screening_ally_min_attack_range := 4
@export var screening_enemy_max_contact_range := 2
@export var screening_threat_distance_buffer := 2
@export var screening_path_bonus := 45


func decide(context):
	var distance_contract := _resolve_desired_distance_contract(context, null, range_skill_ids)
	var action_trace := _begin_action_trace(context, {
		"action_kind": "move_to_range",
		"target_selector": String(target_selector),
		"desired_min_distance": int(distance_contract.get("desired_min_distance", desired_min_distance)),
		"desired_max_distance": int(distance_contract.get("desired_max_distance", desired_max_distance)),
		"configured_desired_min_distance": desired_min_distance,
		"configured_desired_max_distance": desired_max_distance,
		"effective_attack_range": int(distance_contract.get("effective_attack_range", -1)),
		"range_skill_ids": range_skill_ids.duplicate(),
		"screening_mode": String(screening_mode),
	})
	var targets = _sort_target_units(context, &"enemy", target_selector)
	if targets.is_empty():
		_trace_add_block_reason(action_trace, "no_valid_targets")
		_finalize_action_trace(context, action_trace)
		return null
	var focus_target = targets[0] as BattleUnitState
	var screening_context := _build_screening_context(context)
	var current_score_input = _build_action_score_input(
		context,
		&"move",
		String(action_id),
		null,
		null,
		{
			"position_target_unit": focus_target,
			"position_anchor_coord": context.unit_state.coord,
			"desired_min_distance": int(distance_contract.get("desired_min_distance", desired_min_distance)),
			"desired_max_distance": int(distance_contract.get("desired_max_distance", desired_max_distance)),
			"position_objective_kind": &"distance_band_progress",
			"move_cost": 0,
		}
	)
	_apply_screening_score(context, current_score_input, context.unit_state.coord, screening_context)
	if not bool(screening_context.get("enabled", false)):
		var path_progress_decision = _build_path_progress_decision(context, focus_target, action_trace, distance_contract)
		if path_progress_decision != null:
			_finalize_action_trace(context, action_trace, path_progress_decision)
			return path_progress_decision
	var best_decision = null
	var best_score_input = current_score_input
	for neighbor in _collect_reachable_move_candidates(context):
		_trace_count_increment(action_trace, "evaluation_count", 1)
		var command = _build_move_command(context, neighbor)
		var preview = context.preview_command(command)
		if preview == null or not bool(preview.allowed):
			_trace_count_increment(action_trace, "preview_reject_count", 1)
			continue
		var score_input = _build_action_score_input(
			context,
			&"move",
			String(action_id),
			command,
			preview,
			{
				"position_target_unit": focus_target,
				"position_anchor_coord": neighbor,
				"desired_min_distance": int(distance_contract.get("desired_min_distance", desired_min_distance)),
				"desired_max_distance": int(distance_contract.get("desired_max_distance", desired_max_distance)),
				"position_objective_kind": &"distance_band_progress",
			}
		)
		var screening_metrics := _apply_screening_score(context, score_input, neighbor, screening_context)
		_trace_offer_candidate(action_trace, _build_candidate_summary(
			"move_to_%d_%d" % [neighbor.x, neighbor.y],
			command,
			score_input,
			{
				"predicted_distance": score_input.distance_to_primary_coord if score_input != null else -1,
				"screening_bonus": int(screening_metrics.get("bonus", 0)),
				"screening_penalty": int(screening_metrics.get("penalty", 0)),
				"screening_threat_unit_id": String(screening_metrics.get("threat_unit_id", "")),
				"screening_protected_unit_id": String(screening_metrics.get("protected_unit_id", "")),
				"screening_path_cost_delta": int(screening_metrics.get("path_cost_delta", 0)),
				"screening_base_path_cost": int(screening_metrics.get("base_path_cost", -1)),
				"screening_blocked_path_cost": int(screening_metrics.get("blocked_path_cost", -1)),
				"screening_current_bonus": int(screening_metrics.get("current_bonus", 0)),
				"screening_candidate_bonus": int(screening_metrics.get("candidate_bonus", 0)),
				"screening_uncapped_bonus": int(screening_metrics.get("uncapped_bonus", 0)),
				"screening_on_shortest_path": bool(screening_metrics.get("on_shortest_path", false)),
				"screening_keeps_contact": bool(screening_metrics.get("keeps_contact", false)),
				"screening_can_counterattack": bool(screening_metrics.get("can_counterattack", false)),
				"screening_hard_block": bool(screening_metrics.get("hard_block", false)),
				"screening_distance_band_capped": bool(screening_metrics.get("distance_band_capped", false)),
			}
		))
		if not _is_better_move_to_range_score_input(score_input, best_score_input):
			continue
		best_score_input = score_input
		best_decision = _create_scored_decision(
			command,
			score_input,
			"%s 准备调整到距离 %s %d 格（评分 %d）。" % [
				context.unit_state.display_name,
				focus_target.display_name,
				int(score_input.distance_to_primary_coord),
				int(score_input.total_score),
			]
		)
	if best_decision == null:
		best_decision = _build_path_progress_decision(context, focus_target, action_trace, distance_contract)
	_finalize_action_trace(context, action_trace, best_decision)
	return best_decision


func _collect_reachable_move_candidates(context) -> Array[Vector2i]:
	var candidates: Array[Vector2i] = []
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return candidates
	var seen: Dictionary = {}
	var origin: Vector2i = context.unit_state.coord
	var max_move_points := maxi(int(context.unit_state.current_move_points), 0)
	var frontier: Array = [{
		"coord": origin,
		"cost": 0,
	}]
	var best_costs := {
		origin: 0,
	}
	while not frontier.is_empty():
		var entry: Dictionary = frontier.pop_front()
		var current_coord: Vector2i = entry.get("coord", origin)
		var current_cost := int(entry.get("cost", 0))
		if current_cost != int(best_costs.get(current_coord, 2147483647)):
			continue
		for neighbor in context.grid_service.get_neighbors_4(context.state, current_coord):
			if not context.grid_service.can_unit_step_between_anchors(context.state, context.unit_state, current_coord, neighbor):
				continue
			var next_cost: int = current_cost + int(context.grid_service.get_unit_move_cost(context.state, context.unit_state, neighbor))
			if next_cost > max_move_points:
				continue
			if next_cost >= int(best_costs.get(neighbor, 2147483647)):
				continue
			best_costs[neighbor] = next_cost
			frontier.append({
				"coord": neighbor,
				"cost": next_cost,
			})
			if not seen.has(neighbor):
				seen[neighbor] = true
				candidates.append(neighbor)
	candidates.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		var left_distance: int = int(context.grid_service.get_distance(origin, left))
		var right_distance: int = int(context.grid_service.get_distance(origin, right))
		if left_distance == right_distance:
			return left.y < right.y or (left.y == right.y and left.x < right.x)
		return left_distance > right_distance
	)
	return candidates


func _build_screening_context(context) -> Dictionary:
	if screening_mode != SCREENING_RANGED_ALLY:
		return {"enabled": false}
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return {"enabled": false}
	if _get_hp_basis_points(context.unit_state) < maxi(int(screening_min_hp_basis_points), 0):
		return {"enabled": false, "reason": "low_hp"}
	var protected_allies := _collect_screening_protected_allies(context)
	if protected_allies.is_empty():
		return {"enabled": false, "reason": "no_protected_allies"}
	var threat_entries := _collect_screening_threat_entries(context, protected_allies)
	if threat_entries.is_empty():
		return {"enabled": false, "reason": "no_contact_threats"}
	return {
		"enabled": true,
		"threat_entries": threat_entries,
		"anchor_metrics_cache": {},
	}


func _collect_screening_protected_allies(context) -> Array:
	var allies: Array = []
	if context == null or context.state == null or context.unit_state == null:
		return allies
	for unit_variant in context.state.units.values():
		var ally_unit = unit_variant as BattleUnitState
		if ally_unit == null or not ally_unit.is_alive:
			continue
		if ally_unit.unit_id == context.unit_state.unit_id:
			continue
		if ally_unit.faction_id != context.unit_state.faction_id:
			continue
		var ally_attack_range := _resolve_unit_effective_threat_range(context, ally_unit)
		if ally_attack_range < maxi(int(screening_ally_min_attack_range), 1):
			continue
		allies.append(ally_unit)
	return allies


func _collect_screening_threat_entries(context, protected_allies: Array) -> Array:
	var threat_entries: Array = []
	for unit_variant in context.state.units.values():
		var threat_unit = unit_variant as BattleUnitState
		if threat_unit == null or not threat_unit.is_alive:
			continue
		if threat_unit.faction_id == context.unit_state.faction_id:
			continue
		var contact_range := _resolve_unit_contact_threat_range(context, threat_unit)
		if contact_range <= 0:
			continue
		for ally_variant in protected_allies:
			var protected_ally = ally_variant as BattleUnitState
			if protected_ally == null:
				continue
			var threat_distance := _distance_between_units(context, threat_unit, protected_ally)
			var threat_reach := contact_range \
				+ maxi(int(threat_unit.current_move_points), 0) \
				+ maxi(int(screening_threat_distance_buffer), 0)
			if threat_distance > threat_reach:
				continue
			var base_path_cost := _resolve_screening_threat_path_cost(
				context,
				threat_unit,
				protected_ally,
				contact_range,
				Vector2i(-999999, -999999),
				false
			)
			if base_path_cost >= SCREENING_PATH_UNREACHABLE_COST:
				continue
			if base_path_cost > threat_reach:
				continue
			threat_entries.append({
				"threat_unit": threat_unit,
				"protected_unit": protected_ally,
				"contact_range": contact_range,
				"threat_distance": threat_distance,
				"threat_reach": threat_reach,
				"base_path_cost": base_path_cost,
			})
	return threat_entries


func _resolve_unit_contact_threat_range(context, threat_unit: BattleUnitState) -> int:
	if context == null or threat_unit == null:
		return -1
	var best_range := -1
	for raw_skill_id in threat_unit.known_active_skill_ids:
		var skill_id := ProgressionDataUtils.to_string_name(raw_skill_id)
		if skill_id == &"":
			continue
		var skill_def = context.skill_defs.get(skill_id) as SkillDef
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if skill_def.combat_profile.target_team_filter == &"ally" or skill_def.combat_profile.target_team_filter == &"self":
			continue
		if not _skill_has_tag(skill_def, &"melee") and not _skill_has_tag(skill_def, &"weapon"):
			continue
		var effective_range := BATTLE_RANGE_SERVICE_SCRIPT.get_effective_skill_range(threat_unit, skill_def)
		if effective_range > maxi(int(screening_enemy_max_contact_range), 1):
			continue
		best_range = maxi(best_range, effective_range)
	var weapon_range := BATTLE_RANGE_SERVICE_SCRIPT.get_weapon_attack_range(threat_unit)
	if weapon_range > 0 and weapon_range <= maxi(int(screening_enemy_max_contact_range), 1):
		best_range = maxi(best_range, weapon_range)
	return best_range


func _apply_screening_score(context, score_input, anchor_coord: Vector2i, screening_context: Dictionary) -> Dictionary:
	var metrics := _build_screening_metrics(context, anchor_coord, screening_context, score_input)
	if score_input != null:
		var bonus := int(metrics.get("bonus", 0))
		score_input.total_score += bonus
		score_input.position_objective_score += bonus
	return metrics


func _build_screening_metrics(context, anchor_coord: Vector2i, screening_context: Dictionary, score_input = null) -> Dictionary:
	if context == null or context.unit_state == null or not bool(screening_context.get("enabled", false)):
		return {"bonus": 0}
	var best_metrics := {"bonus": 0}
	var current_metrics := _build_best_screening_anchor_metrics(context, context.unit_state.coord, screening_context)
	var candidate_metrics := _build_best_screening_anchor_metrics(context, anchor_coord, screening_context)
	_apply_screening_distance_band_cap(candidate_metrics, score_input)
	var candidate_bonus := int(candidate_metrics.get("bonus", 0))
	var current_bonus := int(current_metrics.get("bonus", 0))
	if candidate_bonus <= current_bonus:
		if current_bonus > 0 and candidate_bonus < current_bonus:
			var penalty := mini(current_bonus - candidate_bonus, maxi(int(maxi(int(screening_path_bonus), 0) / 2), 1))
			var penalty_metrics := candidate_metrics.duplicate(true)
			if String(penalty_metrics.get("threat_unit_id", "")).is_empty():
				penalty_metrics["threat_unit_id"] = String(current_metrics.get("threat_unit_id", ""))
			if String(penalty_metrics.get("protected_unit_id", "")).is_empty():
				penalty_metrics["protected_unit_id"] = String(current_metrics.get("protected_unit_id", ""))
			penalty_metrics["bonus"] = -penalty
			penalty_metrics["penalty"] = penalty
			penalty_metrics["current_bonus"] = current_bonus
			penalty_metrics["candidate_bonus"] = candidate_bonus
			penalty_metrics["lost_bonus"] = current_bonus - candidate_bonus
			return penalty_metrics
		return best_metrics
	best_metrics = candidate_metrics
	best_metrics["bonus"] = candidate_bonus - current_bonus
	best_metrics["current_bonus"] = current_bonus
	best_metrics["candidate_bonus"] = candidate_bonus
	return best_metrics


func _build_best_screening_anchor_metrics(context, anchor_coord: Vector2i, screening_context: Dictionary) -> Dictionary:
	var cache: Dictionary = screening_context.get("anchor_metrics_cache", {}) if screening_context.get("anchor_metrics_cache", {}) is Dictionary else {}
	var cache_key := "%d,%d" % [anchor_coord.x, anchor_coord.y]
	if cache.has(cache_key):
		var cached_metrics: Dictionary = cache.get(cache_key, {})
		return cached_metrics.duplicate(true)
	var best_metrics := {"bonus": 0}
	var threat_entries: Array = screening_context.get("threat_entries", [])
	for entry_variant in threat_entries:
		var entry: Dictionary = entry_variant
		var threat_unit = entry.get("threat_unit", null) as BattleUnitState
		var protected_unit = entry.get("protected_unit", null) as BattleUnitState
		if threat_unit == null or protected_unit == null:
			continue
		var threat_distance := int(entry.get("threat_distance", 999999))
		var anchor_to_threat := _distance_from_anchor_to_unit(context, context.unit_state, anchor_coord, threat_unit)
		var anchor_to_protected := _distance_from_anchor_to_unit(context, context.unit_state, anchor_coord, protected_unit)
		var on_shortest_path := anchor_to_threat + anchor_to_protected == threat_distance
		var keeps_contact := anchor_to_threat <= maxi(desired_max_distance, int(entry.get("contact_range", 1)))
		var own_contact_range := _resolve_unit_contact_threat_range(context, context.unit_state)
		if own_contact_range <= 0:
			own_contact_range = maxi(desired_max_distance, 1)
		var can_counterattack := anchor_to_threat <= own_contact_range
		var base_path_cost := int(entry.get("base_path_cost", SCREENING_PATH_UNREACHABLE_COST))
		var blocked_path_cost := _resolve_screening_threat_path_cost(
			context,
			threat_unit,
			protected_unit,
			int(entry.get("contact_range", 1)),
			anchor_coord,
			true
		)
		var path_cost_delta := _calculate_screening_path_cost_delta(
			base_path_cost,
			blocked_path_cost,
			int(entry.get("threat_reach", 0))
		)
		var increases_path_cost := path_cost_delta > 0
		var hard_block := blocked_path_cost >= SCREENING_PATH_UNREACHABLE_COST
		var can_project_pressure := keeps_contact or can_counterattack
		if not increases_path_cost and not can_project_pressure:
			continue
		var bonus := 0
		if increases_path_cost:
			if can_project_pressure:
				bonus += maxi(int(screening_path_bonus), 0)
				if path_cost_delta > 1:
					bonus += mini(path_cost_delta - 1, 2) * int(maxi(int(screening_path_bonus), 0) / 3)
			else:
				if path_cost_delta < 2 and not hard_block:
					continue
				bonus += int(maxi(int(screening_path_bonus), 0) / 2)
				if hard_block:
					bonus += int(maxi(int(screening_path_bonus), 0) / 3)
				elif path_cost_delta > 2:
					bonus += mini(path_cost_delta - 2, 2) * int(maxi(int(screening_path_bonus), 0) / 6)
		if keeps_contact:
			bonus += int(maxi(int(screening_path_bonus), 0) / 3)
		elif can_counterattack:
			bonus += int(maxi(int(screening_path_bonus), 0) / 3)
		if on_shortest_path and can_project_pressure and not increases_path_cost:
			bonus += int(maxi(int(screening_path_bonus), 0) / 3)
		if bonus <= 0:
			continue
		if bonus < int(best_metrics.get("bonus", 0)):
			continue
		if bonus == int(best_metrics.get("bonus", 0)) and path_cost_delta <= int(best_metrics.get("path_cost_delta", 0)):
			continue
		best_metrics = {
			"bonus": bonus,
			"threat_unit_id": String(threat_unit.unit_id),
			"protected_unit_id": String(protected_unit.unit_id),
			"anchor_to_threat": anchor_to_threat,
			"anchor_to_protected": anchor_to_protected,
			"threat_distance": threat_distance,
			"base_path_cost": base_path_cost,
			"blocked_path_cost": blocked_path_cost,
			"path_cost_delta": path_cost_delta,
			"hard_block": hard_block,
			"on_shortest_path": on_shortest_path,
			"keeps_contact": keeps_contact,
			"can_counterattack": can_counterattack,
		}
	cache[cache_key] = best_metrics.duplicate(true)
	screening_context["anchor_metrics_cache"] = cache
	return best_metrics


func _apply_screening_distance_band_cap(metrics: Dictionary, score_input) -> void:
	if metrics == null or score_input == null:
		return
	if bool(metrics.get("hard_block", false)):
		return
	if _get_score_input_distance_gap(score_input) <= 0:
		return
	var bonus := int(metrics.get("bonus", 0))
	var cap := int(maxi(int(screening_path_bonus), 0) / 3)
	if cap <= 0 or bonus <= cap:
		return
	metrics["uncapped_bonus"] = bonus
	metrics["bonus"] = cap
	metrics["distance_band_capped"] = true


func _calculate_screening_path_cost_delta(base_path_cost: int, blocked_path_cost: int, threat_reach: int) -> int:
	if base_path_cost >= SCREENING_PATH_UNREACHABLE_COST:
		return 0
	if blocked_path_cost >= SCREENING_PATH_UNREACHABLE_COST:
		return maxi(threat_reach - base_path_cost + 1, 1)
	return maxi(blocked_path_cost - base_path_cost, 0)


func _resolve_screening_threat_path_cost(
	context,
	threat_unit: BattleUnitState,
	protected_unit: BattleUnitState,
	contact_range: int,
	blocker_anchor: Vector2i,
	use_blocker: bool
) -> int:
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return SCREENING_PATH_UNREACHABLE_COST
	if threat_unit == null or protected_unit == null:
		return SCREENING_PATH_UNREACHABLE_COST
	var blocker_coords: Array[Vector2i] = []
	if use_blocker:
		blocker_coords = context.grid_service.get_unit_target_coords(context.unit_state, blocker_anchor)
	var restore_coords: Array[Vector2i] = []
	context.unit_state.refresh_footprint()
	restore_coords.append_array(context.unit_state.occupied_coords)
	restore_coords.append_array(blocker_coords)
	var occupant_snapshot := _snapshot_screening_occupants(context, restore_coords)
	context.grid_service.set_occupants(context.state, context.unit_state.occupied_coords, &"")
	if use_blocker:
		context.grid_service.set_occupants(context.state, blocker_coords, context.unit_state.unit_id)
	var destinations := _collect_screening_threat_contact_destinations(context, threat_unit, protected_unit, contact_range)
	if destinations.is_empty():
		_restore_screening_occupants(context, occupant_snapshot)
		return SCREENING_PATH_UNREACHABLE_COST
	var best_cost := SCREENING_PATH_UNREACHABLE_COST
	var path_budget := _build_path_search_budget(context)
	for destination in destinations:
		var path_result: Dictionary = context.grid_service.resolve_unit_move_path(
			context.state,
			threat_unit,
			threat_unit.coord,
			destination,
			path_budget
		)
		if not bool(path_result.get("allowed", false)):
			continue
		best_cost = mini(best_cost, int(path_result.get("cost", SCREENING_PATH_UNREACHABLE_COST)))
	_restore_screening_occupants(context, occupant_snapshot)
	return best_cost


func _collect_screening_threat_contact_destinations(
	context,
	threat_unit: BattleUnitState,
	protected_unit: BattleUnitState,
	contact_range: int
) -> Array[Vector2i]:
	var destinations: Array[Vector2i] = []
	if context == null or context.state == null or context.grid_service == null or threat_unit == null or protected_unit == null:
		return destinations
	var resolved_contact_range := maxi(contact_range, 1)
	var seen: Dictionary = {}
	protected_unit.refresh_footprint()
	for occupied_coord in protected_unit.occupied_coords:
		for y in range(occupied_coord.y - resolved_contact_range, occupied_coord.y + resolved_contact_range + 1):
			for x in range(occupied_coord.x - resolved_contact_range, occupied_coord.x + resolved_contact_range + 1):
				var coord := Vector2i(x, y)
				if seen.has(coord):
					continue
				seen[coord] = true
				if not context.grid_service.is_inside(context.state, coord):
					continue
				var distance := _distance_from_anchor_to_unit(context, threat_unit, coord, protected_unit)
				if distance <= 0 or distance > resolved_contact_range:
					continue
				if not context.grid_service.can_place_footprint(context.state, coord, threat_unit.footprint_size, threat_unit.unit_id, threat_unit):
					continue
				destinations.append(coord)
	destinations.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		var left_distance: int = int(context.grid_service.get_distance(threat_unit.coord, left))
		var right_distance: int = int(context.grid_service.get_distance(threat_unit.coord, right))
		if left_distance == right_distance:
			return left.y < right.y or (left.y == right.y and left.x < right.x)
		return left_distance < right_distance
	)
	return destinations


func _snapshot_screening_occupants(context, coords: Array[Vector2i]) -> Dictionary:
	var snapshot := {}
	if context == null or context.state == null or context.grid_service == null:
		return snapshot
	for coord in coords:
		if snapshot.has(coord):
			continue
		var cell = context.grid_service.get_cell(context.state, coord)
		if cell == null:
			continue
		snapshot[coord] = cell.occupant_unit_id
	return snapshot


func _restore_screening_occupants(context, snapshot: Dictionary) -> void:
	if context == null or context.state == null or context.grid_service == null:
		return
	for coord_variant in snapshot.keys():
		var coord: Vector2i = coord_variant
		context.grid_service.set_occupant(context.state, coord, snapshot.get(coord, &""))


func _build_path_progress_decision(
	context,
	focus_target: BattleUnitState,
	action_trace: Dictionary,
	distance_contract: Dictionary
):
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return null
	if focus_target == null or int(context.unit_state.current_move_points) <= 0:
		return null
	var resolved_min_distance := int(distance_contract.get("desired_min_distance", desired_min_distance))
	var resolved_max_distance := int(distance_contract.get("desired_max_distance", desired_max_distance))
	var current_distance := _distance_from_anchor_to_unit(context, context.unit_state, context.unit_state.coord, focus_target)
	if current_distance >= resolved_min_distance and current_distance <= resolved_max_distance:
		return null
	var best_decision = null
	var best_score_input = null
	var best_path_cost := 2147483647
	var best_path_length := 2147483647
	for destination in _collect_distance_band_destinations(context, focus_target, distance_contract):
		var path_result: Dictionary = context.grid_service.resolve_unit_move_path(
			context.state,
			context.unit_state,
			context.unit_state.coord,
			destination,
			_build_path_search_budget(context)
		)
		if not bool(path_result.get("allowed", false)):
			continue
		var path: Array[Vector2i] = _extract_vector2i_path(path_result.get("path", []))
		var move_target := _resolve_current_turn_path_target(context, path)
		if move_target == context.unit_state.coord:
			continue
		var command = _build_move_command(context, move_target)
		var preview = context.preview_command(command)
		if preview == null or not bool(preview.allowed):
			_trace_count_increment(action_trace, "preview_reject_count", 1)
			continue
		var path_cost := int(path_result.get("cost", 0))
		var path_length := path.size()
		var score_input = _build_action_score_input(
			context,
			&"move",
			String(action_id),
			command,
			preview,
			{
				"position_target_unit": focus_target,
				"position_anchor_coord": move_target,
				"desired_min_distance": resolved_min_distance,
				"desired_max_distance": resolved_max_distance,
				"position_objective_kind": &"distance_band_progress",
				"action_base_score": 60,
			}
		)
		_trace_offer_candidate(action_trace, _build_candidate_summary(
			"path_to_%d_%d_via_%d_%d" % [destination.x, destination.y, move_target.x, move_target.y],
			command,
			score_input,
			{
				"path_cost": path_cost,
				"path_length": path_length,
				"path_destination": destination,
			}
		))
		if best_decision != null:
			if path_cost > best_path_cost:
				continue
			if path_cost == best_path_cost and path_length >= best_path_length:
				continue
		best_path_cost = path_cost
		best_path_length = path_length
		best_score_input = score_input
		best_decision = _create_scored_decision(
			command,
			score_input,
			"%s 准备绕路逼近 %s（路径成本 %d，评分 %d）。" % [
				context.unit_state.display_name,
				focus_target.display_name,
				path_cost,
				int(score_input.total_score) if score_input != null else 0,
			]
		)
	return best_decision


func _collect_distance_band_destinations(
	context,
	focus_target: BattleUnitState,
	distance_contract: Dictionary
) -> Array[Vector2i]:
	var destinations: Array[Vector2i] = []
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null or focus_target == null:
		return destinations
	var resolved_min_distance := int(distance_contract.get("desired_min_distance", desired_min_distance))
	var resolved_max_distance := int(distance_contract.get("desired_max_distance", desired_max_distance))
	var max_distance := maxi(resolved_max_distance, resolved_min_distance)
	var seen: Dictionary = {}
	focus_target.refresh_footprint()
	for occupied_coord in focus_target.occupied_coords:
		for y in range(occupied_coord.y - max_distance, occupied_coord.y + max_distance + 1):
			for x in range(occupied_coord.x - max_distance, occupied_coord.x + max_distance + 1):
				var coord := Vector2i(x, y)
				if seen.has(coord):
					continue
				seen[coord] = true
				if not context.grid_service.is_inside(context.state, coord):
					continue
				var distance := _distance_from_anchor_to_unit(context, context.unit_state, coord, focus_target)
				if distance < resolved_min_distance or distance > resolved_max_distance:
					continue
				destinations.append(coord)
	destinations.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		var left_distance: int = int(context.grid_service.get_distance(context.unit_state.coord, left))
		var right_distance: int = int(context.grid_service.get_distance(context.unit_state.coord, right))
		if left_distance == right_distance:
			return left.y < right.y or (left.y == right.y and left.x < right.x)
		return left_distance < right_distance
	)
	return destinations


func _resolve_current_turn_path_target(context, path: Array[Vector2i]) -> Vector2i:
	if context == null or context.state == null or context.unit_state == null or context.grid_service == null:
		return Vector2i(-1, -1)
	if path.size() <= 1:
		return context.unit_state.coord
	var spent_cost := 0
	var max_move_points := maxi(int(context.unit_state.current_move_points), 0)
	var best_coord: Vector2i = context.unit_state.coord
	for path_index in range(1, path.size()):
		var next_coord: Vector2i = path[path_index]
		var step_cost: int = int(context.grid_service.get_unit_move_cost(context.state, context.unit_state, next_coord))
		if spent_cost + step_cost > max_move_points:
			break
		spent_cost += step_cost
		best_coord = next_coord
	return best_coord


func _extract_vector2i_path(path_variant: Variant) -> Array[Vector2i]:
	var path: Array[Vector2i] = []
	if path_variant is not Array:
		return path
	for coord_variant in path_variant:
		if coord_variant is Vector2i:
			path.append(coord_variant)
	return path


func _build_path_search_budget(context) -> int:
	if context == null or context.state == null:
		return 32
	var map_size: Vector2i = context.state.map_size
	return maxi(map_size.x * map_size.y, map_size.x + map_size.y)


func _is_better_move_to_range_score_input(candidate, best_candidate) -> bool:
	if candidate == null:
		return false
	if best_candidate == null:
		return true
	var candidate_gap: int = _get_score_input_distance_gap(candidate)
	var best_gap: int = _get_score_input_distance_gap(best_candidate)
	if candidate_gap != best_gap:
		if candidate_gap < 0:
			return false
		if best_gap < 0:
			return true
		return candidate_gap < best_gap
	return _is_better_skill_score_input(candidate, best_candidate)


func _get_score_input_distance_gap(score_input) -> int:
	if score_input == null:
		return -1
	var distance_value: int = int(score_input.distance_to_primary_coord)
	var min_distance: int = int(score_input.desired_min_distance)
	var max_distance: int = int(score_input.desired_max_distance)
	if distance_value < 0 or min_distance < 0 or max_distance < min_distance:
		return -1
	if distance_value < min_distance:
		return min_distance - distance_value
	if distance_value > max_distance:
		return distance_value - max_distance
	return 0


func validate_schema() -> Array[String]:
	var errors := _collect_base_validation_errors()
	if target_selector == &"":
		errors.append("MoveToRangeAction %s is missing target_selector." % String(action_id))
	if screening_mode != SCREENING_NONE and screening_mode != SCREENING_RANGED_ALLY:
		errors.append("MoveToRangeAction %s screening_mode must be none or ranged_ally." % String(action_id))
	if desired_min_distance < 0:
		errors.append("MoveToRangeAction %s desired_min_distance must be >= 0." % String(action_id))
	if desired_max_distance < desired_min_distance:
		errors.append("MoveToRangeAction %s desired_max_distance must be >= desired_min_distance." % String(action_id))
	if screening_min_hp_basis_points < 0 or screening_min_hp_basis_points > HP_BASIS_POINTS_DENOMINATOR:
		errors.append("MoveToRangeAction %s screening_min_hp_basis_points must be between 0 and 10000." % String(action_id))
	if screening_ally_min_attack_range < 1:
		errors.append("MoveToRangeAction %s screening_ally_min_attack_range must be >= 1." % String(action_id))
	if screening_enemy_max_contact_range < 1:
		errors.append("MoveToRangeAction %s screening_enemy_max_contact_range must be >= 1." % String(action_id))
	if screening_threat_distance_buffer < 0:
		errors.append("MoveToRangeAction %s screening_threat_distance_buffer must be >= 0." % String(action_id))
	if screening_path_bonus < 0:
		errors.append("MoveToRangeAction %s screening_path_bonus must be >= 0." % String(action_id))
	return errors
