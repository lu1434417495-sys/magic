class_name WorldMapSpawnSystem
extends RefCounted

const SETTLEMENT_CONFIG_SCRIPT = preload("res://scripts/utils/settlement_config.gd")

var _rng := RandomNumberGenerator.new()
var _generation_config
var _grid_system
var _facility_library_by_id: Dictionary = {}
var _settlement_library_by_id: Dictionary = {}


func build_world(generation_config, grid_system) -> Dictionary:
	_generation_config = generation_config
	_grid_system = grid_system
	_rng.seed = generation_config.seed
	_build_libraries()

	var settlements := _generate_settlements()
	var world_npcs := _generate_world_npcs(settlements)
	var wild_monsters := _generate_wild_monsters(settlements)
	var player_start_settlement := _find_player_start_settlement(settlements)
	var player_start_coord: Vector2i = _resolve_player_start_coord(player_start_settlement)

	return {
		"settlements": settlements,
		"world_npcs": world_npcs,
		"wild_monsters": wild_monsters,
		"player_start_coord": player_start_coord,
		"player_start_settlement_id": player_start_settlement.get("settlement_id", ""),
		"player_start_settlement_name": player_start_settlement.get("display_name", ""),
	}


func _build_libraries() -> void:
	_facility_library_by_id.clear()
	_settlement_library_by_id.clear()

	for facility_config in _generation_config.facility_library:
		_facility_library_by_id[facility_config.facility_id] = facility_config

	for settlement_config in _generation_config.settlement_library:
		_settlement_library_by_id[settlement_config.settlement_id] = settlement_config


func _generate_settlements() -> Array[Dictionary]:
	if _generation_config.procedural_generation_enabled:
		return _generate_procedural_settlements()

	return _generate_fixed_settlements()


func _generate_fixed_settlements() -> Array[Dictionary]:
	var settlements: Array[Dictionary] = []
	var instance_counts: Dictionary = {}

	for distribution_rule in _generation_config.settlement_distribution:
		var settlement_config = _settlement_library_by_id.get(distribution_rule.settlement_id)
		if settlement_config == null:
			continue

		var settlement := _create_settlement_instance(
			settlement_config,
			distribution_rule.preferred_origin,
			distribution_rule.faction_id,
			instance_counts,
			false
		)
		if not settlement.is_empty():
			settlements.append(settlement)

	return settlements


func _generate_procedural_settlements() -> Array[Dictionary]:
	var settlements: Array[Dictionary] = []
	var instance_counts: Dictionary = {}
	var templates_by_tier := _build_settlement_templates_by_tier()
	var player_village_template = _pick_settlement_template_for_tier(
		templates_by_tier,
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE,
		0
	)

	if player_village_template != null:
		var player_origin := _get_centered_origin(player_village_template.get_footprint_size())
		var player_settlement := _create_settlement_instance(
			player_village_template,
			player_origin,
			"player",
			instance_counts,
			true
		)
		if not player_settlement.is_empty():
			settlements.append(player_settlement)

	var generation_order := [
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.WORLD_STRONGHOLD,
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CAPITAL,
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.CITY,
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.TOWN,
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE,
	]

	for tier in generation_order:
		var target_count: int = _generation_config.get_target_settlement_count(tier)
		if tier == SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE and not settlements.is_empty():
			target_count = max(target_count - 1, 0)

		for tier_index in range(target_count):
			var settlement_template = _pick_settlement_template_for_tier(templates_by_tier, tier, tier_index)
			if settlement_template == null:
				break

			var origin := _find_procedural_origin(
				settlement_template.get_footprint_size(),
				settlements,
				_generation_config.get_settlement_spacing_cells(tier)
			)
			if origin == Vector2i(-1, -1):
				push_warning("Unable to place settlement for tier %d after repeated attempts." % tier)
				continue

			var settlement := _create_settlement_instance(
				settlement_template,
				origin,
				"neutral",
				instance_counts,
				false
			)
			if not settlement.is_empty():
				settlements.append(settlement)

	return settlements


func _build_settlement_templates_by_tier() -> Dictionary:
	var templates_by_tier: Dictionary = {}

	for settlement_config in _generation_config.settlement_library:
		var tier: int = settlement_config.tier
		if not templates_by_tier.has(tier):
			templates_by_tier[tier] = []
		templates_by_tier[tier].append(settlement_config)

	return templates_by_tier


func _pick_settlement_template_for_tier(templates_by_tier: Dictionary, tier: int, index: int):
	var tier_templates: Array = templates_by_tier.get(tier, [])
	if tier_templates.is_empty():
		return null
	return tier_templates[index % tier_templates.size()]


func _create_settlement_instance(
	settlement_config,
	origin: Vector2i,
	faction_id: String,
	instance_counts: Dictionary,
	is_player_start: bool
) -> Dictionary:
	var footprint_size: Vector2i = settlement_config.get_footprint_size()
	if not _grid_system.can_place_footprint(origin, footprint_size):
		push_error("Invalid settlement placement for %s at %s" % [settlement_config.settlement_id, origin])
		return {}

	var template_id: String = settlement_config.settlement_id
	var instance_index: int = int(instance_counts.get(template_id, 0)) + 1
	instance_counts[template_id] = instance_index

	var settlement_id := template_id
	if instance_index > 1 or _generation_config.procedural_generation_enabled:
		settlement_id = "%s_%02d" % [template_id, instance_index]

	var display_name: String = settlement_config.display_name
	if instance_index > 1:
		display_name = "%s %02d" % [display_name, instance_index]

	var entity_id := "settlement_%s" % settlement_id
	_grid_system.register_footprint(entity_id, origin, footprint_size)

	var facilities := _generate_facilities_for_settlement(settlement_config, origin)
	var settlement := {
		"entity_id": entity_id,
		"template_id": template_id,
		"settlement_id": settlement_id,
		"display_name": display_name,
		"tier": settlement_config.tier,
		"tier_name": settlement_config.get_tier_name(),
		"faction_id": faction_id,
		"origin": origin,
		"footprint_size": footprint_size,
		"facilities": facilities,
		"is_player_start": is_player_start,
	}
	settlement["available_services"] = _collect_services(facilities)
	settlement["service_npcs"] = _collect_service_npcs(facilities)
	return settlement


func _generate_facilities_for_settlement(settlement_config, settlement_origin: Vector2i) -> Array[Dictionary]:
	var generated_facilities: Array[Dictionary] = []
	var used_slot_ids: Dictionary = {}

	for facility_id in settlement_config.guaranteed_facility_ids:
		var facility_config = _facility_library_by_id.get(facility_id)
		if facility_config == null:
			continue

		var placed_facility := _try_place_facility(facility_config, settlement_config, settlement_origin, used_slot_ids)
		if not placed_facility.is_empty():
			generated_facilities.append(placed_facility)

	var optional_limit: int = min(
		settlement_config.max_optional_facilities,
		max(settlement_config.facility_slots.size() - generated_facilities.size(), 0)
	)
	var optional_pool: Array = settlement_config.optional_facility_pool.duplicate()

	for _optional_index in range(optional_limit):
		var selected_facility_id := _pick_weighted_facility(optional_pool)
		if selected_facility_id.is_empty():
			break

		var facility_config = _facility_library_by_id.get(selected_facility_id)
		if facility_config == null:
			continue

		var placed_facility := _try_place_facility(facility_config, settlement_config, settlement_origin, used_slot_ids)
		if placed_facility.is_empty():
			continue

		generated_facilities.append(placed_facility)
		_remove_weighted_entry(optional_pool, selected_facility_id)

	return generated_facilities


func _try_place_facility(facility_config, settlement_config, settlement_origin: Vector2i, used_slot_ids: Dictionary) -> Dictionary:
	if facility_config.min_settlement_tier > settlement_config.tier:
		return {}

	for slot_config in settlement_config.facility_slots:
		if used_slot_ids.has(slot_config.slot_id):
			continue
		if not facility_config.allowed_slot_tags.is_empty() and not facility_config.allowed_slot_tags.has(slot_config.slot_tag):
			continue

		used_slot_ids[slot_config.slot_id] = true
		var service_npcs: Array[Dictionary] = []
		for npc_config in facility_config.bound_service_npcs:
			service_npcs.append({
				"npc_id": npc_config.npc_id,
				"display_name": npc_config.display_name,
				"service_type": npc_config.service_type,
				"interaction_script_id": npc_config.interaction_script_id,
				"local_slot_id": npc_config.local_slot_id,
				"facility_id": facility_config.facility_id,
				"facility_name": facility_config.display_name,
			})

		return {
			"facility_id": facility_config.facility_id,
			"display_name": facility_config.display_name,
			"category": facility_config.category,
			"interaction_type": facility_config.interaction_type,
			"slot_id": slot_config.slot_id,
			"slot_tag": slot_config.slot_tag,
			"local_coord": slot_config.local_coord,
			"world_coord": settlement_origin + slot_config.local_coord,
			"service_npcs": service_npcs,
		}

	return {}


func _collect_services(facilities: Array[Dictionary]) -> Array[Dictionary]:
	var services: Array[Dictionary] = []

	for facility in facilities:
		for npc in facility.get("service_npcs", []):
			services.append({
				"facility_id": facility.get("facility_id", ""),
				"facility_name": facility.get("display_name", ""),
				"npc_id": npc.get("npc_id", ""),
				"npc_name": npc.get("display_name", ""),
				"service_type": npc.get("service_type", ""),
				"action_id": "service:%s" % npc.get("service_type", ""),
			})

	return services


func _collect_service_npcs(facilities: Array[Dictionary]) -> Array[Dictionary]:
	var service_npcs: Array[Dictionary] = []

	for facility in facilities:
		for npc in facility.get("service_npcs", []):
			service_npcs.append(npc)

	return service_npcs


func _pick_weighted_facility(optional_pool: Array) -> String:
	if optional_pool.is_empty():
		return ""

	var total_weight := 0.0
	for entry in optional_pool:
		total_weight += max(entry.weight, 0.0)

	if total_weight <= 0.0:
		return ""

	var roll := _rng.randf_range(0.0, total_weight)
	var cursor := 0.0

	for entry in optional_pool:
		cursor += max(entry.weight, 0.0)
		if roll <= cursor:
			return entry.facility_id

	return optional_pool[0].facility_id


func _remove_weighted_entry(optional_pool: Array, facility_id: String) -> void:
	for index in range(optional_pool.size()):
		var entry = optional_pool[index]
		if entry.facility_id == facility_id:
			optional_pool.remove_at(index)
			return


func _generate_world_npcs(settlements: Array[Dictionary]) -> Array[Dictionary]:
	var world_npcs: Array[Dictionary] = []
	var npc_names := [
		"巡路信使",
		"驿站商人",
		"边地向导",
		"地图学者",
		"补给联络员",
	]
	var name_index := 0

	for settlement in settlements:
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var footprint_size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		var spawn_coord := _find_free_coord_near(origin + footprint_size - Vector2i.ONE)
		if spawn_coord == Vector2i(-1, -1):
			continue

		var npc_name: String = npc_names[name_index % npc_names.size()]
		name_index += 1
		world_npcs.append({
			"entity_id": "world_npc_%d" % name_index,
			"display_name": npc_name,
			"coord": spawn_coord,
			"kind": "service_hint",
			"faction_id": settlement.get("faction_id", "neutral"),
			"vision_range": 1,
		})

	return world_npcs


func _generate_wild_monsters(settlements: Array[Dictionary]) -> Array[Dictionary]:
	var settlement_cells: Array[Vector2i] = []

	for settlement in settlements:
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var footprint_size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		for y in range(footprint_size.y):
			for x in range(footprint_size.x):
				settlement_cells.append(origin + Vector2i(x, y))

	if _generation_config.procedural_generation_enabled:
		return _generate_procedural_wild_monsters(settlement_cells)

	var monsters: Array[Dictionary] = []
	var monster_index := 0
	for rule in _generation_config.wild_monster_distribution:
		for chunk_coord in rule.chunk_coords:
			for offset in range(max(rule.density_per_chunk, 0)):
				var spawn_coord := _pick_monster_coord_for_chunk(chunk_coord, rule.min_distance_to_settlement, settlement_cells, offset)
				if spawn_coord == Vector2i(-1, -1):
					continue

				monster_index += 1
				monsters.append({
					"entity_id": "wild_%d" % monster_index,
					"display_name": rule.monster_name,
					"coord": spawn_coord,
					"vision_range": rule.vision_range,
					"region_tag": rule.region_tag,
				})

	return monsters


func _generate_procedural_wild_monsters(settlement_cells: Array[Vector2i]) -> Array[Dictionary]:
	var monsters: Array[Dictionary] = []
	if _generation_config.wild_monster_distribution.is_empty():
		return monsters

	var north_rule = _generation_config.wild_monster_distribution[0]
	var south_rule = _generation_config.wild_monster_distribution[min(1, _generation_config.wild_monster_distribution.size() - 1)]
	var world_chunks: Vector2i = _generation_config.world_size_in_chunks
	var midpoint_chunk_y: int = int(world_chunks.y / 2)
	var monster_index := 0

	for chunk_y in range(world_chunks.y):
		for chunk_x in range(world_chunks.x):
			var chunk_coord := Vector2i(chunk_x, chunk_y)
			var rule = north_rule if chunk_y < midpoint_chunk_y else south_rule
			var chunk_seed: int = int(_generation_config.seed) + chunk_x * 92821 + chunk_y * 68917
			if posmod(chunk_seed, 6) != 0:
				continue

			for offset in range(max(rule.density_per_chunk, 0)):
				var spawn_coord := _pick_monster_coord_for_chunk(
					chunk_coord,
					rule.min_distance_to_settlement,
					settlement_cells,
					chunk_seed + offset
				)
				if spawn_coord == Vector2i(-1, -1):
					continue

				monster_index += 1
				monsters.append({
					"entity_id": "wild_%d" % monster_index,
					"display_name": rule.monster_name,
					"coord": spawn_coord,
					"vision_range": rule.vision_range,
					"region_tag": rule.region_tag,
				})

	return monsters


func _find_free_coord_near(origin: Vector2i) -> Vector2i:
	var candidate_directions := [
		Vector2i.RIGHT,
		Vector2i.DOWN,
		Vector2i.LEFT,
		Vector2i.UP,
		Vector2i(1, 1),
	]

	for direction in candidate_directions:
		var candidate: Vector2i = origin + direction
		if not _grid_system.is_cell_inside_world(candidate):
			continue
		if _grid_system.get_occupant_root(candidate) != "":
			continue
		return candidate

	return Vector2i(-1, -1)


func _find_player_start_settlement(settlements: Array[Dictionary]) -> Dictionary:
	for settlement in settlements:
		if settlement.get("is_player_start", false):
			return settlement

	for settlement in settlements:
		if settlement.get("tier", -1) == SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE:
			return settlement

	return {}


func _resolve_player_start_coord(player_start_settlement: Dictionary) -> Vector2i:
	if player_start_settlement.is_empty():
		return _generation_config.player_start_coord

	return player_start_settlement.get("origin", _generation_config.player_start_coord)


func _get_centered_origin(footprint_size: Vector2i) -> Vector2i:
	var world_size: Vector2i = _generation_config.get_world_size_cells()
	var max_x: int = max(world_size.x - footprint_size.x, 0)
	var max_y: int = max(world_size.y - footprint_size.y, 0)
	return Vector2i(
		clampi(int(max_x / 2), 0, max_x),
		clampi(int(max_y / 2), 0, max_y)
	)


func _find_procedural_origin(
	footprint_size: Vector2i,
	existing_settlements: Array[Dictionary],
	min_distance_cells: int
) -> Vector2i:
	var world_size: Vector2i = _generation_config.get_world_size_cells()
	var max_x: int = world_size.x - footprint_size.x
	var max_y: int = world_size.y - footprint_size.y
	if max_x < 0 or max_y < 0:
		return Vector2i(-1, -1)

	for _attempt in range(192):
		var origin := Vector2i(
			_rng.randi_range(0, max_x),
			_rng.randi_range(0, max_y)
		)
		if not _grid_system.can_place_footprint(origin, footprint_size):
			continue
		if not _is_origin_far_enough(origin, footprint_size, existing_settlements, min_distance_cells):
			continue
		return origin

	return Vector2i(-1, -1)


func _is_origin_far_enough(
	candidate_origin: Vector2i,
	candidate_size: Vector2i,
	existing_settlements: Array[Dictionary],
	min_distance_cells: int
) -> bool:
	var candidate_center := Vector2(candidate_origin) + Vector2(candidate_size) * 0.5

	for settlement in existing_settlements:
		var other_origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var other_size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		var other_center := Vector2(other_origin) + Vector2(other_size) * 0.5
		var other_tier: int = settlement.get("tier", SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE)
		var required_distance := float(max(min_distance_cells, _generation_config.get_settlement_spacing_cells(other_tier)))
		if candidate_center.distance_to(other_center) < required_distance:
			return false

	return true


func _pick_monster_coord_for_chunk(
	chunk_coord: Vector2i,
	min_distance_to_settlement: int,
	settlement_cells: Array[Vector2i],
	offset_seed: int
) -> Vector2i:
	var base_origin := Vector2i(
		chunk_coord.x * _generation_config.chunk_size.x,
		chunk_coord.y * _generation_config.chunk_size.y
	)
	var candidates: Array[Vector2i] = []

	for y in range(_generation_config.chunk_size.y):
		for x in range(_generation_config.chunk_size.x):
			var candidate := base_origin + Vector2i(x, y)
			if not _grid_system.is_cell_inside_world(candidate):
				continue
			if _grid_system.get_occupant_root(candidate) != "":
				continue
			if _is_too_close_to_settlement(candidate, min_distance_to_settlement, settlement_cells):
				continue
			candidates.append(candidate)

	if candidates.is_empty():
		return Vector2i(-1, -1)

	var index := posmod(offset_seed * 3 + chunk_coord.x + chunk_coord.y, candidates.size())
	return candidates[index]


func _is_too_close_to_settlement(candidate: Vector2i, min_distance_to_settlement: int, settlement_cells: Array[Vector2i]) -> bool:
	for settlement_cell in settlement_cells:
		if candidate.distance_to(settlement_cell) < float(min_distance_to_settlement):
			return true

	return false
