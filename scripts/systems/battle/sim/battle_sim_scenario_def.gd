class_name BattleSimScenarioDef
extends Resource

const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BATTLE_SIM_UNIT_SPEC_SCRIPT = preload("res://scripts/systems/battle/sim/battle_sim_unit_spec.gd")
const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")
const BattleSimUnitSpec = preload("res://scripts/systems/battle/sim/battle_sim_unit_spec.gd")

@export var scenario_id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""
@export var map_size: Vector2i = Vector2i(7, 5)
@export var terrain_profile_id: StringName = &"default"
@export var use_formal_terrain_generation := false
@export var world_coord: Vector2i = Vector2i.ZERO
@export var ally_units: Array = []
@export var enemy_units: Array = []
@export var cell_overrides: Array[Dictionary] = []
@export var tick_interval_seconds := 1.0
@export var tu_per_tick := 5
@export var max_iterations := 200
@export var manual_policy: StringName = &"wait"
@export var trace_enabled := true
@export var seeds: PackedInt32Array = PackedInt32Array([101])


func resolve_seeds() -> Array[int]:
	var resolved: Array[int] = []
	for seed_value in seeds:
		resolved.append(int(seed_value))
	if resolved.is_empty():
		resolved.append(101)
	return resolved


func build_start_context() -> Dictionary:
	var context := {
		"battle_party": _build_unit_payloads(ally_units, &"player", &"manual"),
		"enemy_units": _build_unit_payloads(enemy_units, &"hostile", &"ai"),
		"tick_interval_seconds": tick_interval_seconds,
		"tu_per_tick": tu_per_tick,
		"battle_terrain_profile": terrain_profile_id,
		"world_coord": world_coord,
	}
	if use_formal_terrain_generation:
		if map_size != Vector2i.ZERO:
			context["battle_map_size"] = map_size
		return context
	context["ally_spawns"] = _build_spawn_coords(ally_units)
	context["enemy_spawns"] = _build_spawn_coords(enemy_units)
	context["map_size"] = map_size
	context["cells"] = _build_cells()
	return context


func to_dict() -> Dictionary:
	return {
		"scenario_id": String(scenario_id),
		"display_name": display_name,
		"description": description,
		"map_size": map_size,
		"terrain_profile_id": String(terrain_profile_id),
		"use_formal_terrain_generation": use_formal_terrain_generation,
		"world_coord": world_coord,
		"tick_interval_seconds": tick_interval_seconds,
		"tu_per_tick": tu_per_tick,
		"max_iterations": max_iterations,
		"manual_policy": String(manual_policy),
		"trace_enabled": trace_enabled,
		"seeds": resolve_seeds(),
		"ally_unit_count": ally_units.size(),
		"enemy_unit_count": enemy_units.size(),
	}


func _build_unit_payloads(unit_specs: Array, default_faction: StringName, default_control_mode: StringName) -> Array:
	var payloads: Array = []
	for unit_spec in unit_specs:
		if unit_spec == null:
			continue
		payloads.append(unit_spec.to_battle_unit_state(default_faction, default_control_mode).to_dict())
	return payloads


func _build_spawn_coords(unit_specs: Array) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for unit_spec in unit_specs:
		if unit_spec == null:
			continue
		coords.append(unit_spec.coord)
	return coords


func _build_cells() -> Dictionary:
	var cells: Dictionary = {}
	for y in range(map_size.y):
		for x in range(map_size.x):
			var cell_state: BattleCellState = BATTLE_CELL_STATE_SCRIPT.new()
			cell_state.coord = Vector2i(x, y)
			cell_state.base_terrain = &"land"
			cell_state.base_height = 4
			cell_state.height_offset = 0
			cell_state.recalculate_runtime_values()
			cells[cell_state.coord] = cell_state
	for override_entry in cell_overrides:
		if override_entry is not Dictionary:
			continue
		var coord := _resolve_override_coord(override_entry)
		if coord == Vector2i(-1, -1):
			continue
		var cell_state := cells.get(coord) as BattleCellState
		if cell_state == null:
			cell_state = BATTLE_CELL_STATE_SCRIPT.new()
			cell_state.coord = coord
		_apply_cell_override(cell_state, override_entry)
		cell_state.recalculate_runtime_values()
		cells[coord] = cell_state
	return cells


func _resolve_override_coord(override_entry: Dictionary) -> Vector2i:
	var coord_variant = override_entry.get("coord", Vector2i(-1, -1))
	if coord_variant is Vector2i:
		return coord_variant
	if coord_variant is Dictionary:
		return Vector2i(int(coord_variant.get("x", -1)), int(coord_variant.get("y", -1)))
	return Vector2i(-1, -1)


func _apply_cell_override(cell_state: BattleCellState, override_entry: Dictionary) -> void:
	if override_entry.has("base_terrain"):
		cell_state.base_terrain = ProgressionDataUtils.to_string_name(override_entry.get("base_terrain", "land"))
	if override_entry.has("base_height"):
		cell_state.base_height = int(override_entry.get("base_height", cell_state.base_height))
	if override_entry.has("height_offset"):
		cell_state.height_offset = int(override_entry.get("height_offset", cell_state.height_offset))
	if override_entry.has("flow_direction") and override_entry.get("flow_direction") is Vector2i:
		cell_state.flow_direction = override_entry.get("flow_direction")
	if override_entry.has("terrain_effect_ids") and override_entry.get("terrain_effect_ids") is Array:
		cell_state.terrain_effect_ids.clear()
		for effect_id in override_entry.get("terrain_effect_ids", []):
			cell_state.terrain_effect_ids.append(ProgressionDataUtils.to_string_name(effect_id))
	if override_entry.has("prop_ids") and override_entry.get("prop_ids") is Array:
		cell_state.prop_ids.clear()
		for prop_id in override_entry.get("prop_ids", []):
			cell_state.prop_ids.append(ProgressionDataUtils.to_string_name(prop_id))
