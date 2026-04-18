class_name WorldMapDataContext
extends RefCounted

const WORLD_MAP_GRID_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_grid_system.gd")
const WORLD_MAP_SPAWN_SYSTEM_SCRIPT = preload("res://scripts/systems/world_map_spawn_system.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")

var root_world_data: Dictionary = {}
var active_world_data: Dictionary = {}
var active_map_id := ""
var active_map_display_name := ""
var active_generation_config = null
var world_event_by_coord: Dictionary = {}
var submap_generation_configs: Dictionary = {}
var settlement_by_coord: Dictionary = {}
var world_npc_by_coord: Dictionary = {}
var encounter_anchor_by_coord: Dictionary = {}
var settlements_by_id: Dictionary = {}


func bind_root_world_data(world_data: Dictionary) -> void:
	root_world_data = world_data if world_data != null else {}


func reset() -> void:
	root_world_data = {}
	active_world_data = {}
	active_map_id = ""
	active_map_display_name = ""
	active_generation_config = null
	world_event_by_coord = {}
	submap_generation_configs = {}
	settlement_by_coord = {}
	world_npc_by_coord = {}
	encounter_anchor_by_coord = {}
	settlements_by_id = {}


func is_submap_active() -> bool:
	return not active_map_id.is_empty()


func get_world_step() -> int:
	return int(active_world_data.get("world_step", 0))


func get_active_world_data() -> Dictionary:
	return active_world_data


func get_active_generation_config():
	return active_generation_config


func get_active_map_id() -> String:
	return active_map_id


func get_active_map_display_name() -> String:
	return active_map_display_name


func get_submap_return_hint_text() -> String:
	if not is_submap_active():
		return ""
	var submap_entry := get_mounted_submap_entry(active_map_id)
	return String(submap_entry.get("return_hint_text", "点击任意地点返回原位置。"))


func sync_active_world_context(root_generation_config, grid_system, player_coord: Vector2i, selected_coord: Vector2i) -> Dictionary:
	active_map_id = String(root_world_data.get("active_submap_id", ""))
	if not active_map_id.is_empty() and get_mounted_submap_entry(active_map_id).is_empty():
		active_map_id = ""
		root_world_data["active_submap_id"] = ""
	active_world_data = _resolve_active_world_data()
	active_generation_config = _resolve_active_generation_config(root_generation_config)
	active_map_display_name = _resolve_active_map_display_name()
	if active_generation_config != null and grid_system != null:
		grid_system.setup(active_generation_config.world_size_in_chunks, active_generation_config.chunk_size)
	_refresh_world_event_discovery()
	_rebuild_world_coord_lookups()
	_register_settlement_footprints(grid_system)
	var resolved_player_coord := player_coord
	var resolved_selected_coord := selected_coord
	if grid_system != null and not grid_system.is_cell_inside_world(resolved_player_coord):
		resolved_player_coord = _resolve_active_map_player_coord(player_coord)
	if grid_system != null and not grid_system.is_cell_inside_world(resolved_selected_coord):
		resolved_selected_coord = resolved_player_coord
	return {
		"player_coord": resolved_player_coord,
		"selected_coord": resolved_selected_coord,
	}


func get_settlement_at(coord: Vector2i) -> Dictionary:
	return settlement_by_coord.get(coord, {})


func get_world_npc_at(coord: Vector2i) -> Dictionary:
	return world_npc_by_coord.get(coord, {})


func get_encounter_anchor_at(coord: Vector2i) -> ENCOUNTER_ANCHOR_DATA_SCRIPT:
	return encounter_anchor_by_coord.get(coord, null) as ENCOUNTER_ANCHOR_DATA_SCRIPT


func get_encounter_anchor_by_id(entity_id: StringName) -> ENCOUNTER_ANCHOR_DATA_SCRIPT:
	if entity_id == &"":
		return null
	for encounter_variant in active_world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.entity_id == entity_id:
			return encounter_anchor
	return null


func get_world_event_at(coord: Vector2i) -> Dictionary:
	var world_event_variant = world_event_by_coord.get(coord, {})
	return world_event_variant if world_event_variant is Dictionary else {}


func get_settlement_record(settlement_id: String) -> Dictionary:
	return settlements_by_id.get(settlement_id, {}).duplicate(true)


func get_all_settlement_records() -> Array[Dictionary]:
	var settlements: Array[Dictionary] = []
	for settlement_variant in active_world_data.get("settlements", []):
		if settlement_variant is not Dictionary:
			continue
		settlements.append((settlement_variant as Dictionary).duplicate(true))
	return settlements


func get_settlement_state(settlement_id: String) -> Dictionary:
	var settlement: Dictionary = settlements_by_id.get(settlement_id, {})
	if settlement is Dictionary:
		return (settlement as Dictionary).get("settlement_state", {}).duplicate(true)
	return {}


func set_active_settlement_state(settlement_id: String, settlement_state: Dictionary) -> bool:
	var settlements_variant = active_world_data.get("settlements", [])
	if settlements_variant is not Array:
		return false
	for index in range(settlements_variant.size()):
		var settlement_variant = settlements_variant[index]
		if settlement_variant is not Dictionary:
			continue
		var settlement_data: Dictionary = settlement_variant
		if String(settlement_data.get("settlement_id", "")) != settlement_id:
			continue
		settlement_data["settlement_state"] = settlement_state.duplicate(true)
		settlements_variant[index] = settlement_data
		active_world_data["settlements"] = settlements_variant
		_rebuild_world_coord_lookups()
		return true
	return false


func remove_encounter_anchor_by_id(encounter_id: StringName) -> void:
	if encounter_id == &"":
		return
	var remaining_anchors: Array = []
	for encounter_anchor_data in active_world_data.get("encounter_anchors", []):
		var encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT = encounter_anchor_data as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		if encounter_anchor.entity_id == encounter_id:
			continue
		remaining_anchors.append(encounter_anchor)
	active_world_data["encounter_anchors"] = remaining_anchors
	_rebuild_world_coord_lookups()


func refresh_world_event_discovery() -> void:
	_refresh_world_event_discovery()


func get_mounted_submap_entry(submap_id: String) -> Dictionary:
	var mounted_submaps_variant = root_world_data.get("mounted_submaps", {})
	if mounted_submaps_variant is not Dictionary:
		return {}
	var mounted_submaps: Dictionary = mounted_submaps_variant
	var submap_entry_variant = mounted_submaps.get(submap_id, {})
	return submap_entry_variant if submap_entry_variant is Dictionary else {}


func set_mounted_submap_entry(submap_id: String, submap_entry: Dictionary) -> void:
	var mounted_submaps_variant = root_world_data.get("mounted_submaps", {})
	var mounted_submaps: Dictionary = mounted_submaps_variant if mounted_submaps_variant is Dictionary else {}
	mounted_submaps[submap_id] = submap_entry.duplicate(true)
	root_world_data["mounted_submaps"] = mounted_submaps


func ensure_submap_generated(submap_id: String) -> bool:
	var submap_entry := get_mounted_submap_entry(submap_id)
	if submap_entry.is_empty():
		return false
	var current_world_data = submap_entry.get("world_data", {})
	if bool(submap_entry.get("is_generated", false)) and current_world_data is Dictionary and not current_world_data.is_empty():
		return true
	var submap_generation_config = load_submap_generation_config(submap_id)
	if submap_generation_config == null:
		return false
	var generation_grid = WORLD_MAP_GRID_SYSTEM_SCRIPT.new()
	generation_grid.setup(submap_generation_config.world_size_in_chunks, submap_generation_config.chunk_size)
	var spawn_system = WORLD_MAP_SPAWN_SYSTEM_SCRIPT.new()
	var submap_world_data := spawn_system.build_world(submap_generation_config, generation_grid)
	submap_entry["world_data"] = submap_world_data
	submap_entry["player_coord"] = submap_world_data.get("player_start_coord", submap_generation_config.player_start_coord)
	submap_entry["is_generated"] = true
	set_mounted_submap_entry(submap_id, submap_entry)
	return true


func load_submap_generation_config(submap_id: String):
	if submap_generation_configs.has(submap_id):
		return submap_generation_configs.get(submap_id)
	var submap_entry := get_mounted_submap_entry(submap_id)
	var generation_config_path := String(submap_entry.get("generation_config_path", ""))
	if generation_config_path.is_empty():
		return null
	var generation_config = load(generation_config_path)
	if generation_config != null:
		submap_generation_configs[submap_id] = generation_config
	return generation_config


func _register_settlement_footprints(grid_system) -> void:
	if grid_system == null:
		return
	for settlement in active_world_data.get("settlements", []):
		var entity_id: String = settlement.get("entity_id", "")
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		if entity_id.is_empty():
			continue
		if grid_system.can_place_footprint(origin, size):
			grid_system.register_footprint(entity_id, origin, size)


func _rebuild_world_coord_lookups() -> void:
	settlement_by_coord.clear()
	settlements_by_id.clear()
	world_npc_by_coord.clear()
	encounter_anchor_by_coord.clear()
	world_event_by_coord.clear()

	for settlement in active_world_data.get("settlements", []):
		if settlement is not Dictionary:
			continue
		settlements_by_id[String(settlement.get("settlement_id", ""))] = settlement
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		for y in range(size.y):
			for x in range(size.x):
				settlement_by_coord[origin + Vector2i(x, y)] = settlement

	for npc in active_world_data.get("world_npcs", []):
		if npc is not Dictionary:
			continue
		world_npc_by_coord[npc.get("coord", Vector2i.ZERO)] = npc

	for encounter_anchor_data in active_world_data.get("encounter_anchors", []):
		var encounter_anchor: ENCOUNTER_ANCHOR_DATA_SCRIPT = encounter_anchor_data as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null:
			continue
		encounter_anchor_by_coord[encounter_anchor.world_coord] = encounter_anchor

	for world_event_variant in active_world_data.get("world_events", []):
		if world_event_variant is not Dictionary:
			continue
		var world_event: Dictionary = world_event_variant
		if not bool(world_event.get("is_discovered", false)):
			continue
		world_event_by_coord[world_event.get("world_coord", Vector2i.ZERO)] = world_event


func _resolve_active_world_data() -> Dictionary:
	if active_map_id.is_empty():
		return root_world_data
	var submap_entry := get_mounted_submap_entry(active_map_id)
	var submap_world_data = submap_entry.get("world_data", {})
	return submap_world_data if submap_world_data is Dictionary else root_world_data


func _resolve_active_generation_config(root_generation_config):
	if active_map_id.is_empty():
		return root_generation_config
	return load_submap_generation_config(active_map_id)


func _resolve_active_map_display_name() -> String:
	if active_map_id.is_empty():
		return "大地图"
	var submap_entry := get_mounted_submap_entry(active_map_id)
	var display_name := String(submap_entry.get("display_name", ""))
	return display_name if not display_name.is_empty() else active_map_id


func _resolve_active_map_player_coord(fallback_player_coord: Vector2i) -> Vector2i:
	if active_map_id.is_empty():
		return root_world_data.get("player_start_coord", fallback_player_coord)
	var submap_entry := get_mounted_submap_entry(active_map_id)
	var stored_coord: Vector2i = submap_entry.get("player_coord", Vector2i(-1, -1))
	if stored_coord != Vector2i(-1, -1):
		return stored_coord
	return active_world_data.get("player_start_coord", Vector2i.ZERO)


func _refresh_world_event_discovery() -> void:
	var events_variant = active_world_data.get("world_events", [])
	if events_variant is not Array:
		return
	var changed := false
	for index in range(events_variant.size()):
		var event_variant = events_variant[index]
		if event_variant is not Dictionary:
			continue
		var world_event: Dictionary = event_variant
		if bool(world_event.get("is_discovered", false)):
			continue
		if not _is_world_event_discovery_condition_met(world_event):
			continue
		world_event["is_discovered"] = true
		events_variant[index] = world_event
		changed = true
	if changed:
		active_world_data["world_events"] = events_variant
		_rebuild_world_coord_lookups()


func _is_world_event_discovery_condition_met(world_event: Dictionary) -> bool:
	var condition_id := String(world_event.get("discovery_condition_id", "")).strip_edges()
	return condition_id.is_empty() or condition_id == "always_true"
