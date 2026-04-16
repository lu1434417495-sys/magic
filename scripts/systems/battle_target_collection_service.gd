class_name BattleTargetCollectionService
extends RefCounted

const BattleState = preload("res://scripts/systems/battle_state.gd")
const CombatSkillDef = preload("res://scripts/player/progression/combat_skill_def.gd")


func collect_combat_profile_target_coords(
	state: BattleState,
	grid_service,
	source_coord: Vector2i,
	combat_profile: CombatSkillDef,
	target_coords: Array[Vector2i]
) -> Dictionary:
	if combat_profile == null:
		return _build_unhandled_result(target_coords)
	var area_pattern := StringName(combat_profile.area_pattern)
	if area_pattern != &"line" and area_pattern != &"cone":
		return _build_unhandled_result(target_coords)
	if state == null or grid_service == null:
		return _build_unhandled_result(target_coords)

	var radius := maxi(int(combat_profile.area_value), 0)
	var coord_set: Dictionary = {}
	for target_coord in target_coords:
		if not grid_service.is_inside(state, target_coord):
			continue
		var collected_any := false
		var area_direction := target_coord - source_coord if source_coord != Vector2i(-1, -1) else Vector2i.ZERO
		for effect_coord in grid_service.get_area_coords(state, target_coord, area_pattern, radius, area_direction):
			coord_set[effect_coord] = true
			collected_any = true
		if not collected_any:
			coord_set[target_coord] = true
	return {
		"handled": true,
		"target_coords": _sort_coords(_collect_coords(coord_set)),
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
