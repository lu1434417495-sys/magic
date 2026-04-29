class_name BattleTargetCollectionService
extends RefCounted

const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")


func collect_combat_profile_target_coords(
	state: BattleState,
	grid_service,
	source_coord: Vector2i,
	combat_profile: CombatSkillDef,
	target_coords: Array[Vector2i],
	source_unit: BattleUnitState = null,
	target_units: Array = [],
	skill_level: int = -1
) -> Dictionary:
	if combat_profile == null:
		return _build_unhandled_result(target_coords)
	if _is_self_target_collection(combat_profile, skill_level):
		return _build_handled_result(_collect_self_target_coords(state, grid_service, source_coord, source_unit))
	if StringName(combat_profile.target_mode) == &"unit":
		return _build_handled_result(_collect_target_unit_coords(target_units))
	if StringName(combat_profile.target_mode) != &"ground":
		return _build_unhandled_result(target_coords)
	if state == null or grid_service == null:
		return _build_unhandled_result(target_coords)

	var area_pattern := StringName(combat_profile.get_effective_area_pattern(skill_level) if skill_level >= 0 else combat_profile.area_pattern)
	var area_value := maxi(int(combat_profile.get_effective_area_value(skill_level) if skill_level >= 0 else combat_profile.area_value), 0)
	var coord_set: Dictionary = {}
	for target_coord in target_coords:
		if not grid_service.is_inside(state, target_coord):
			continue
		var area_center := target_coord
		if area_pattern == &"self" and source_coord != Vector2i(-1, -1):
			area_center = source_coord
		var collected_any := false
		var area_direction := area_center - source_coord if source_coord != Vector2i(-1, -1) else Vector2i.ZERO
		for effect_coord in grid_service.get_area_coords(state, area_center, area_pattern, area_value, area_direction):
			coord_set[effect_coord] = true
			collected_any = true
		if not collected_any:
			coord_set[area_center] = true
	return {
		"handled": true,
		"target_coords": _sort_coords(_collect_coords(coord_set)),
	}


func _is_self_target_collection(combat_profile: CombatSkillDef, skill_level: int = -1) -> bool:
	if combat_profile == null:
		return false
	var selection_mode := StringName(combat_profile.target_selection_mode)
	if selection_mode == &"self":
		return true
	if StringName(combat_profile.target_team_filter) == &"self":
		return true
	var effective_area_pattern := combat_profile.get_effective_area_pattern(skill_level) if skill_level >= 0 else combat_profile.area_pattern
	return StringName(effective_area_pattern) == &"self"


func _collect_self_target_coords(
	state: BattleState,
	grid_service,
	source_coord: Vector2i,
	source_unit: BattleUnitState
) -> Array[Vector2i]:
	if source_unit != null:
		source_unit.refresh_footprint()
		return _sort_coords(source_unit.occupied_coords)
	if state != null and grid_service != null and grid_service.is_inside(state, source_coord):
		return [source_coord]
	return []


func _collect_target_unit_coords(target_units: Array) -> Array[Vector2i]:
	var coord_set: Dictionary = {}
	for target_unit_variant in target_units:
		var target_unit := target_unit_variant as BattleUnitState
		if target_unit == null:
			continue
		target_unit.refresh_footprint()
		for occupied_coord in target_unit.occupied_coords:
			coord_set[occupied_coord] = true
	return _sort_coords(_collect_coords(coord_set))


func _build_handled_result(target_coords: Array[Vector2i]) -> Dictionary:
	return {
		"handled": true,
		"target_coords": _sort_coords(target_coords),
	}


func _build_unhandled_result(target_coords: Array[Vector2i]) -> Dictionary:
	return {
		"handled": false,
		"target_coords": _sort_coords(target_coords),
	}


func _collect_coords(coord_set: Dictionary) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord_variant in coord_set.keys():
		if coord_variant is Vector2i:
			coords.append(coord_variant)
	return coords


func _sort_coords(target_coords: Array[Vector2i]) -> Array[Vector2i]:
	var coords := target_coords.duplicate()
	coords.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		if a.y == b.y:
			return a.x < b.x
		return a.y < b.y
	)
	return coords
