class_name BattleSimTerrainGenerator
extends RefCounted

const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")


func generate(_encounter_anchor, _seed: int, context: Dictionary = {}) -> Dictionary:
	var cells: Dictionary = context.get("cells", {}) if context.get("cells", {}) is Dictionary else {}
	if cells.is_empty():
		return {}
	var map_size := _resolve_map_size(cells, context)
	return {
		"map_size": map_size,
		"cells": cells.duplicate(true),
		"cell_columns": BATTLE_CELL_STATE_SCRIPT.build_columns_from_surface_cells(cells),
		"terrain_profile_id": ProgressionDataUtils.to_string_name(context.get("battle_terrain_profile", "default")),
		"ally_spawns": _duplicate_vector2i_array(context.get("ally_spawns", [])),
		"enemy_spawns": _duplicate_vector2i_array(context.get("enemy_spawns", [])),
	}


func _resolve_map_size(cells: Dictionary, context: Dictionary) -> Vector2i:
	var explicit_size = context.get("battle_map_size", context.get("map_size", Vector2i.ZERO))
	if explicit_size is Vector2i and explicit_size != Vector2i.ZERO:
		return explicit_size
	var max_x := -1
	var max_y := -1
	for coord_variant in cells.keys():
		if coord_variant is not Vector2i:
			continue
		var coord := coord_variant as Vector2i
		max_x = maxi(max_x, coord.x)
		max_y = maxi(max_y, coord.y)
	return Vector2i(max_x + 1, max_y + 1) if max_x >= 0 and max_y >= 0 else Vector2i.ZERO


func _duplicate_vector2i_array(values: Variant) -> Array[Vector2i]:
	var result: Array[Vector2i] = []
	if values is not Array:
		return result
	for value in values:
		if value is Vector2i:
			result.append(value)
	return result
