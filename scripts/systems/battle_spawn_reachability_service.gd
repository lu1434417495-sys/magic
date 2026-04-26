class_name BattleSpawnReachabilityService
extends RefCounted

const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleTargetCollectionService = preload("res://scripts/systems/battle_target_collection_service.gd")
const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attribute_service.gd")

const DEFAULT_MAX_SEARCH_NODES := 2048
const STATUS_ARCHER_RANGE_UP: StringName = &"archer_range_up"

var _target_collection_service := BattleTargetCollectionService.new()


func validate_state(
	state: BattleState,
	grid_service,
	skill_defs: Dictionary,
	options: Dictionary = {}
) -> Dictionary:
	var result := {
		"valid": true,
		"invalid_enemy_unit_ids": [],
		"details": [],
	}
	if state == null or grid_service == null:
		result.valid = false
		result.details.append({
			"reason": "missing_state_or_grid",
		})
		return result

	var player_targets := _collect_living_player_targets(state)
	if player_targets.is_empty():
		result.valid = false
		result.details.append({
			"reason": "no_living_player_targets",
		})
		return result

	for enemy_unit_id_variant in state.enemy_unit_ids:
		var enemy_unit_id := StringName(String(enemy_unit_id_variant))
		var enemy_unit := state.units.get(enemy_unit_id) as BattleUnitState
		if enemy_unit == null or not enemy_unit.is_alive:
			continue
		var enemy_result := _validate_enemy_unit(
			state,
			grid_service,
			skill_defs,
			enemy_unit,
			player_targets,
			options
		)
		if bool(enemy_result.get("valid", false)):
			continue
		result.valid = false
		(result.invalid_enemy_unit_ids as Array).append(enemy_unit.unit_id)
		(result.details as Array).append(enemy_result)
	return result


func _validate_enemy_unit(
	state: BattleState,
	grid_service,
	skill_defs: Dictionary,
	enemy_unit: BattleUnitState,
	player_targets: Array[BattleUnitState],
	options: Dictionary
) -> Dictionary:
	var attack_skill_ids := _collect_attack_skill_ids(enemy_unit, skill_defs, player_targets)
	if attack_skill_ids.is_empty():
		return {
			"valid": false,
			"unit_id": enemy_unit.unit_id,
			"reason": "no_attack_skill",
		}

	var occupant_snapshot := _snapshot_occupants(state)
	_clear_nonblocking_enemy_occupants(state, enemy_unit)
	var reachable_anchors := _collect_reachable_anchors(state, grid_service, enemy_unit, options)
	var attack_anchor := Vector2i(-1, -1)
	var attack_target_id: StringName = &""
	var attack_skill_id: StringName = &""
	for anchor_coord in reachable_anchors:
		var attack_match := _find_attack_match_from_anchor(
			state,
			grid_service,
			skill_defs,
			enemy_unit,
			anchor_coord,
			player_targets,
			attack_skill_ids
		)
		if attack_match.is_empty():
			continue
		attack_anchor = anchor_coord
		attack_target_id = attack_match.get("target_unit_id", &"")
		attack_skill_id = attack_match.get("skill_id", &"")
		break
	_restore_occupants(state, occupant_snapshot)

	if attack_anchor == Vector2i(-1, -1):
		return {
			"valid": false,
			"unit_id": enemy_unit.unit_id,
			"reason": "no_reachable_attack_anchor",
			"reachable_anchor_count": reachable_anchors.size(),
			"attack_skill_ids": _string_name_array_to_strings(attack_skill_ids),
		}
	return {
		"valid": true,
		"unit_id": enemy_unit.unit_id,
		"attack_anchor": attack_anchor,
		"target_unit_id": attack_target_id,
		"skill_id": attack_skill_id,
		"reachable_anchor_count": reachable_anchors.size(),
	}


func _collect_living_player_targets(state: BattleState) -> Array[BattleUnitState]:
	var targets: Array[BattleUnitState] = []
	if state == null:
		return targets
	for unit_id_variant in state.ally_unit_ids:
		var unit_id := StringName(String(unit_id_variant))
		var unit_state := state.units.get(unit_id) as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		targets.append(unit_state)
	return targets


func _collect_attack_skill_ids(
	enemy_unit: BattleUnitState,
	skill_defs: Dictionary,
	player_targets: Array[BattleUnitState]
) -> Array[StringName]:
	var skill_ids: Array[StringName] = []
	if enemy_unit == null:
		return skill_ids
	for skill_id_variant in enemy_unit.known_active_skill_ids:
		var skill_id := StringName(String(skill_id_variant))
		var skill_def = skill_defs.get(skill_id)
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if not _skill_has_attackable_target(enemy_unit, skill_def, player_targets):
			continue
		skill_ids.append(skill_id)
	return skill_ids


func _skill_has_attackable_target(
	enemy_unit: BattleUnitState,
	skill_def,
	player_targets: Array[BattleUnitState]
) -> bool:
	if enemy_unit == null or skill_def == null or skill_def.combat_profile == null:
		return false
	var target_mode := StringName(skill_def.combat_profile.target_mode)
	if target_mode != &"unit" and target_mode != &"ground":
		return false
	for target_unit in player_targets:
		if _target_filter_allows(enemy_unit, target_unit, skill_def.combat_profile.target_team_filter):
			return true
	return false


func _snapshot_occupants(state: BattleState) -> Dictionary:
	var snapshot: Dictionary = {}
	if state == null:
		return snapshot
	for coord_variant in state.cells.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var cell := state.cells.get(coord) as BattleCellState
		if cell == null:
			continue
		snapshot[coord] = cell.occupant_unit_id
	return snapshot


func _clear_nonblocking_enemy_occupants(state: BattleState, subject_unit: BattleUnitState) -> void:
	if state == null or subject_unit == null:
		return
	for enemy_unit_id_variant in state.enemy_unit_ids:
		var enemy_unit_id := StringName(String(enemy_unit_id_variant))
		if enemy_unit_id == subject_unit.unit_id:
			continue
		var enemy_unit := state.units.get(enemy_unit_id) as BattleUnitState
		if enemy_unit == null:
			continue
		enemy_unit.refresh_footprint()
		for occupied_coord in enemy_unit.occupied_coords:
			var cell := state.cells.get(occupied_coord) as BattleCellState
			if cell != null and cell.occupant_unit_id == enemy_unit.unit_id:
				cell.occupant_unit_id = &""


func _restore_occupants(state: BattleState, snapshot: Dictionary) -> void:
	if state == null:
		return
	for coord_variant in snapshot.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		var cell := state.cells.get(coord) as BattleCellState
		if cell != null:
			cell.occupant_unit_id = StringName(String(snapshot.get(coord, "")))


func _collect_reachable_anchors(
	state: BattleState,
	grid_service,
	unit_state: BattleUnitState,
	options: Dictionary
) -> Array[Vector2i]:
	var anchors: Array[Vector2i] = []
	if state == null or grid_service == null or unit_state == null:
		return anchors
	var max_search_nodes := maxi(int(options.get("max_search_nodes", DEFAULT_MAX_SEARCH_NODES)), 1)
	var origin := unit_state.coord
	var frontier: Array[Vector2i] = [origin]
	var seen: Dictionary = {
		origin: true,
	}
	var frontier_index := 0
	while frontier_index < frontier.size() and seen.size() <= max_search_nodes:
		var current: Vector2i = frontier[frontier_index]
		frontier_index += 1
		anchors.append(current)
		for neighbor in grid_service.get_neighbors_4(state, current):
			if seen.has(neighbor):
				continue
			if not grid_service.can_unit_step_between_anchors(state, unit_state, current, neighbor):
				continue
			seen[neighbor] = true
			frontier.append(neighbor)
	return anchors


func _find_attack_match_from_anchor(
	state: BattleState,
	grid_service,
	skill_defs: Dictionary,
	enemy_unit: BattleUnitState,
	anchor_coord: Vector2i,
	player_targets: Array[BattleUnitState],
	attack_skill_ids: Array[StringName]
) -> Dictionary:
	for skill_id in attack_skill_ids:
		var skill_def = skill_defs.get(skill_id)
		if skill_def == null or skill_def.combat_profile == null:
			continue
		for target_unit in player_targets:
			if not _target_filter_allows(enemy_unit, target_unit, skill_def.combat_profile.target_team_filter):
				continue
			if _can_skill_hit_target_from_anchor(state, grid_service, enemy_unit, anchor_coord, target_unit, skill_def):
				return {
					"skill_id": skill_id,
					"target_unit_id": target_unit.unit_id,
				}
	return {}


func _can_skill_hit_target_from_anchor(
	state: BattleState,
	grid_service,
	enemy_unit: BattleUnitState,
	anchor_coord: Vector2i,
	target_unit: BattleUnitState,
	skill_def
) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	match StringName(skill_def.combat_profile.target_mode):
		&"unit":
			return _distance_from_anchor_to_unit(grid_service, enemy_unit, anchor_coord, target_unit) <= _get_effective_skill_range(enemy_unit, skill_def)
		&"ground":
			return _can_ground_skill_hit_target(state, grid_service, enemy_unit, anchor_coord, target_unit, skill_def)
		_:
			return false


func _can_ground_skill_hit_target(
	state: BattleState,
	grid_service,
	enemy_unit: BattleUnitState,
	anchor_coord: Vector2i,
	target_unit: BattleUnitState,
	skill_def
) -> bool:
	if state == null or grid_service == null or enemy_unit == null or target_unit == null or skill_def == null or skill_def.combat_profile == null:
		return false
	var skill_range := _get_effective_skill_range(enemy_unit, skill_def)
	target_unit.refresh_footprint()
	for coord_variant in state.cells.keys():
		if coord_variant is not Vector2i:
			continue
		var target_coord: Vector2i = coord_variant
		if _distance_from_anchor_to_coord(grid_service, enemy_unit, anchor_coord, target_coord) > skill_range:
			continue
		var collected := _target_collection_service.collect_combat_profile_target_coords(
			state,
			grid_service,
			anchor_coord,
			skill_def.combat_profile,
			[target_coord],
			enemy_unit,
			[]
		)
		var effect_coords: Array[Vector2i] = []
		for effect_coord_variant in collected.get("target_coords", []):
			if effect_coord_variant is Vector2i:
				effect_coords.append(effect_coord_variant)
		for occupied_coord in target_unit.occupied_coords:
			if effect_coords.has(occupied_coord):
				return true
	return false


func _distance_from_anchor_to_unit(
	grid_service,
	source_unit: BattleUnitState,
	source_anchor: Vector2i,
	target_unit: BattleUnitState
) -> int:
	if grid_service == null or source_unit == null or target_unit == null:
		return 999999
	target_unit.refresh_footprint()
	var best_distance := 999999
	for source_coord in grid_service.get_unit_target_coords(source_unit, source_anchor):
		for target_coord in target_unit.occupied_coords:
			best_distance = mini(best_distance, grid_service.get_distance(source_coord, target_coord))
	return best_distance


func _distance_from_anchor_to_coord(
	grid_service,
	source_unit: BattleUnitState,
	source_anchor: Vector2i,
	target_coord: Vector2i
) -> int:
	if grid_service == null or source_unit == null:
		return 999999
	var best_distance := 999999
	for source_coord in grid_service.get_unit_target_coords(source_unit, source_anchor):
		best_distance = mini(best_distance, grid_service.get_distance(source_coord, target_coord))
	return best_distance


func _target_filter_allows(source_unit: BattleUnitState, target_unit: BattleUnitState, target_team_filter: StringName) -> bool:
	if source_unit == null or target_unit == null:
		return false
	match target_team_filter:
		&"enemy", &"hostile":
			return source_unit.faction_id != target_unit.faction_id
		&"ally":
			return source_unit.faction_id == target_unit.faction_id
		&"self":
			return source_unit.unit_id == target_unit.unit_id
		&"all", &"any":
			return true
		_:
			return false


func _get_effective_skill_range(unit_state: BattleUnitState, skill_def) -> int:
	if skill_def == null or skill_def.combat_profile == null:
		return 0
	if _is_weapon_range_skill(skill_def):
		var weapon_range := _get_weapon_attack_range(unit_state)
		if weapon_range > 0:
			return weapon_range
		if _requires_melee_weapon(skill_def):
			return 0
		if _skill_has_tag(skill_def, &"melee"):
			return 1
	var skill_range := int(skill_def.combat_profile.range_value)
	if unit_state != null and unit_state.has_status_effect(STATUS_ARCHER_RANGE_UP):
		skill_range += 1
	return skill_range


func _is_weapon_range_skill(skill_def) -> bool:
	return _skill_has_tag(skill_def, &"melee") or _skill_has_tag(skill_def, &"bow") or _skill_has_tag(skill_def, &"weapon")


func _get_weapon_attack_range(unit_state: BattleUnitState) -> int:
	if unit_state == null:
		return 0
	return unit_state.get_weapon_attack_range()


func _requires_melee_weapon(skill_def) -> bool:
	if skill_def == null or skill_def.combat_profile == null or not _skill_has_tag(skill_def, &"melee"):
		return false
	for effect_def in skill_def.combat_profile.effect_defs:
		if effect_def != null and effect_def.params != null and bool(effect_def.params.get("use_weapon_physical_damage_tag", false)):
			return true
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant == null:
			continue
		for effect_def in cast_variant.effect_defs:
			if effect_def != null and effect_def.params != null and bool(effect_def.params.get("use_weapon_physical_damage_tag", false)):
				return true
	return false


func _skill_has_tag(skill_def, expected_tag: StringName) -> bool:
	if skill_def == null or expected_tag == &"":
		return false
	for tag in skill_def.tags:
		if StringName(String(tag)) == expected_tag:
			return true
	return false


func _string_name_array_to_strings(values: Array[StringName]) -> Array[String]:
	var results: Array[String] = []
	for value in values:
		results.append(String(value))
	return results
