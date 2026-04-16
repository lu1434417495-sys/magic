## 文件说明：该脚本属于世界地图生成系统相关的系统脚本，集中维护随机数生成器、生成配置、网格系统等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name WorldMapSpawnSystem
extends RefCounted

const SETTLEMENT_CONFIG_SCRIPT = preload("res://scripts/utils/settlement_config.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const WORLD_EVENT_CONFIG_SCRIPT = preload("res://scripts/utils/world_event_config.gd")
const MOUNTED_SUBMAP_CONFIG_SCRIPT = preload("res://scripts/utils/mounted_submap_config.gd")

const SERVICE_ACTION_ID_BY_INTERACTION := {
	"party_warehouse": "service:warehouse",
	"service_rest_basic": "service:rest_basic",
	"service_rest_full": "service:rest_full",
	"service_basic_supply": "service:basic_supply",
	"service_local_trade": "service:local_trade",
	"service_city_market": "service:city_market",
	"service_military_supply": "service:military_supply",
	"service_grand_auction": "service:grand_auction",
	"service_village_rumor": "service:village_rumor",
	"service_intel_network": "service:intel_network",
	"service_stagecoach": "service:stagecoach",
	"service_world_gate_travel": "service:world_gate_travel",
	"service_repair_gear": "service:repair_gear",
	"service_contract_board": "service:contract_board",
	"service_join_guild": "service:join_guild",
	"service_identify_relic": "service:identify_relic",
	"service_bounty_registry": "service:bounty_registry",
	"service_recruit_specialist": "service:recruit_specialist",
	"service_issue_regional_edict": "service:issue_regional_edict",
	"service_research": "service:research",
	"service_unlock_archive": "service:unlock_archive",
	"service_diplomatic_clearance": "service:diplomatic_clearance",
	"service_amnesty_review": "service:amnesty_review",
	"service_elite_recruitment": "service:elite_recruitment",
	"service_master_reforge": "service:master_reforge",
	"service_respecialize_build": "service:respecialize_build",
	"service_manage_reputation": "service:manage_reputation",
	"service_open_trade_route": "service:open_trade_route",
	"service_legend_contracts": "service:legend_contracts",
	"service_hire_expert": "service:hire_expert",
}

## 字段说明：缓存随机数生成器实例，保证生成逻辑集中使用同一套随机来源并保持可复现性。
var _rng := RandomNumberGenerator.new()
## 字段说明：记录生成配置，会参与运行时状态流转、系统协作和存档恢复。
var _generation_config
## 字段说明：记录网格系统，会参与运行时状态流转、系统协作和存档恢复。
var _grid_system
## 字段说明：记录按标识索引的设施资源库，作为查表、序列化和跨系统引用时使用的主键。
var _facility_library_by_id: Dictionary = {}
## 字段说明：记录按标识索引的聚落资源库，作为查表、序列化和跨系统引用时使用的主键。
var _settlement_library_by_id: Dictionary = {}


func build_world(generation_config, grid_system) -> Dictionary:
	_generation_config = generation_config
	_grid_system = grid_system
	_rng.seed = generation_config.seed
	_build_libraries()

	var settlements := _generate_settlements()
	var player_start_settlement := _find_player_start_settlement(settlements)
	var player_start_coord: Vector2i = _resolve_player_start_coord(player_start_settlement)
	var world_npcs := _generate_world_npcs(settlements)
	var encounter_anchors := _generate_encounter_anchors(settlements, player_start_coord)

	return {
		"settlements": settlements,
		"world_npcs": world_npcs,
		"encounter_anchors": encounter_anchors,
		"world_events": _generate_world_events(),
		"mounted_submaps": _generate_mounted_submaps(),
		"active_submap_id": "",
		"submap_return_stack": [],
		"world_step": 0,
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
		SETTLEMENT_CONFIG_SCRIPT.SettlementTier.METROPOLIS,
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
	facilities = _augment_facilities_for_settlement(settlement_config, origin, facilities)
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
		"settlement_state": _build_default_settlement_state(is_player_start),
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


func _augment_facilities_for_settlement(settlement_config, settlement_origin: Vector2i, facilities: Array[Dictionary]) -> Array[Dictionary]:
	var next_facilities: Array[Dictionary] = []
	for facility_variant in facilities:
		if facility_variant is Dictionary:
			next_facilities.append((facility_variant as Dictionary).duplicate(true))
	var tier: int = int(settlement_config.tier)
	var existing_interaction_ids := _collect_interaction_ids(next_facilities)
	if tier == SETTLEMENT_CONFIG_SCRIPT.SettlementTier.VILLAGE:
		if not existing_interaction_ids.has("service_rest_basic") or not existing_interaction_ids.has("service_basic_supply") or not existing_interaction_ids.has("service_village_rumor"):
			next_facilities.append(_build_synthetic_facility(
				"village_hearth",
				"篝烟灶",
				"rest",
				"rest",
				settlement_origin,
				"core",
				[
					{"npc_id": "npc_village_elder", "display_name": "村长", "service_type": "歇脚", "interaction_script_id": "service_rest_basic", "local_slot_id": "hearth_rest"},
					{"npc_id": "npc_village_teller", "display_name": "猎径向导", "service_type": "传闻", "interaction_script_id": "service_village_rumor", "local_slot_id": "hearth_rumor"},
					{"npc_id": "npc_village_vendor", "display_name": "补给商", "service_type": "补给", "interaction_script_id": "service_basic_supply", "local_slot_id": "hearth_supply"},
					{"npc_id": "npc_village_keeper", "display_name": "仓管", "service_type": "仓储", "interaction_script_id": "party_warehouse", "local_slot_id": "hearth_warehouse"},
				]
			))
	if tier == SETTLEMENT_CONFIG_SCRIPT.SettlementTier.TOWN:
		if not existing_interaction_ids.has("service_local_trade"):
			next_facilities.append(_build_synthetic_facility(
				"town_market",
				"镇集摊位",
				"trade",
				"shop",
				settlement_origin + Vector2i.ONE,
				"commerce",
				[
					{"npc_id": "npc_town_merchant", "display_name": "杂货商", "service_type": "交易", "interaction_script_id": "service_local_trade", "local_slot_id": "market_trade"},
				]
			))
		if not existing_interaction_ids.has("service_stagecoach"):
			next_facilities.append(_build_synthetic_facility(
				"coach_station",
				"驿站",
				"transport",
				"travel",
				settlement_origin + Vector2i.ONE,
				"service",
				[
					{"npc_id": "npc_coachman", "display_name": "驿夫", "service_type": "驿站", "interaction_script_id": "service_stagecoach", "local_slot_id": "coach_route"},
				]
			))
		if not existing_interaction_ids.has("service_repair_gear"):
			next_facilities.append(_build_synthetic_facility(
				"repair_workshop",
				"工坊",
				"craft",
				"craft",
				settlement_origin + Vector2i.ONE,
				"support",
				[
					{"npc_id": "npc_blacksmith", "display_name": "铁匠", "service_type": "修整", "interaction_script_id": "service_repair_gear", "local_slot_id": "workshop_repair"},
					{"npc_id": "npc_town_keeper", "display_name": "仓管", "service_type": "仓储", "interaction_script_id": "party_warehouse", "local_slot_id": "workshop_warehouse"},
				]
			))
	next_facilities = _ensure_master_reforge_service(next_facilities)
	return next_facilities


func _collect_interaction_ids(facilities: Array[Dictionary]) -> Dictionary:
	var interaction_ids: Dictionary = {}
	for facility_variant in facilities:
		if facility_variant is not Dictionary:
			continue
		var facility_data: Dictionary = facility_variant
		for npc_variant in facility_data.get("service_npcs", []):
			if npc_variant is not Dictionary:
				continue
			var interaction_script_id := String((npc_variant as Dictionary).get("interaction_script_id", ""))
			if not interaction_script_id.is_empty():
				interaction_ids[interaction_script_id] = true
	return interaction_ids


func _build_synthetic_facility(
	facility_id: String,
	display_name: String,
	category: String,
	interaction_type: String,
	world_coord: Vector2i,
	slot_tag: String,
	npc_entries: Array
) -> Dictionary:
	var service_npcs: Array[Dictionary] = []
	for npc_variant in npc_entries:
		if npc_variant is not Dictionary:
			continue
		var npc_data: Dictionary = (npc_variant as Dictionary).duplicate(true)
		npc_data["facility_id"] = facility_id
		npc_data["facility_name"] = display_name
		service_npcs.append(npc_data)
	return {
		"facility_id": facility_id,
		"display_name": display_name,
		"category": category,
		"interaction_type": interaction_type,
		"slot_id": "generated_%s" % facility_id,
		"slot_tag": slot_tag,
		"local_coord": Vector2i.ZERO,
		"world_coord": world_coord,
		"service_npcs": service_npcs,
	}


func _ensure_master_reforge_service(facilities: Array[Dictionary]) -> Array[Dictionary]:
	var next_facilities: Array[Dictionary] = []
	var has_master_reforge := false
	for facility_variant in facilities:
		if facility_variant is not Dictionary:
			continue
		var facility_copy: Dictionary = (facility_variant as Dictionary).duplicate(true)
		for npc_variant in facility_copy.get("service_npcs", []):
			if npc_variant is not Dictionary:
				continue
			if String((npc_variant as Dictionary).get("interaction_script_id", "")) == "service_master_reforge":
				has_master_reforge = true
				break
		next_facilities.append(facility_copy)
	if has_master_reforge:
		return next_facilities

	for facility_index in range(next_facilities.size()):
		var facility := next_facilities[facility_index]
		if not _facility_supports_master_reforge(facility):
			continue
		var service_npcs: Array = facility.get("service_npcs", []).duplicate(true)
		service_npcs.append({
			"npc_id": "npc_master_smith",
			"display_name": "大师铁匠",
			"service_type": "重铸",
			"interaction_script_id": "service_master_reforge",
			"local_slot_id": "master_reforge_slot",
			"facility_id": facility.get("facility_id", ""),
			"facility_name": facility.get("display_name", ""),
		})
		facility["service_npcs"] = service_npcs
		next_facilities[facility_index] = facility
		break
	return next_facilities


func _facility_supports_master_reforge(facility: Dictionary) -> bool:
	var facility_id := String(facility.get("facility_id", ""))
	if String(facility.get("interaction_type", "")) == "craft":
		return true
	if facility_id.contains("forge") or facility_id.contains("workshop"):
		return true
	for npc_variant in facility.get("service_npcs", []):
		if npc_variant is not Dictionary:
			continue
		if String((npc_variant as Dictionary).get("interaction_script_id", "")) == "service_repair_gear":
			return true
	return false


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
	var has_party_warehouse_service := false

	for facility in facilities:
		for npc in facility.get("service_npcs", []):
			var interaction_script_id := String(npc.get("interaction_script_id", ""))
			if interaction_script_id == "party_warehouse":
				has_party_warehouse_service = true
			services.append({
				"facility_id": facility.get("facility_id", ""),
				"facility_name": facility.get("display_name", ""),
				"npc_id": npc.get("npc_id", ""),
				"npc_name": npc.get("display_name", ""),
				"service_type": npc.get("service_type", ""),
				"action_id": _build_service_action_id(
					String(npc.get("service_type", "")),
					interaction_script_id
				),
				"interaction_script_id": interaction_script_id,
			})

	if not has_party_warehouse_service:
		services.append({
			"facility_id": "settlement_service_desk",
			"facility_name": "据点服务台",
			"npc_id": "npc_quartermaster",
			"npc_name": "军需官",
			"service_type": "仓储",
			"action_id": String(SERVICE_ACTION_ID_BY_INTERACTION.get("party_warehouse", "service:warehouse")),
			"interaction_script_id": "party_warehouse",
		})

	return services


func _collect_service_npcs(facilities: Array[Dictionary]) -> Array[Dictionary]:
	var service_npcs: Array[Dictionary] = []

	for facility in facilities:
		for npc in facility.get("service_npcs", []):
			service_npcs.append(npc)

	return service_npcs


func _build_default_settlement_state(is_player_start: bool) -> Dictionary:
	return {
		"visited": is_player_start,
		"reputation": 0,
		"active_conditions": [],
		"cooldowns": {},
		"shop_inventory_seed": 0,
		"shop_last_refresh_step": 0,
		"shop_states": {},
	}


func _build_service_action_id(service_type: String, interaction_script_id: String) -> String:
	if SERVICE_ACTION_ID_BY_INTERACTION.has(interaction_script_id):
		return String(SERVICE_ACTION_ID_BY_INTERACTION.get(interaction_script_id, ""))
	var normalized_service_type := service_type.strip_edges().to_snake_case()
	if normalized_service_type.is_empty():
		normalized_service_type = "service"
	return "service:%s" % normalized_service_type


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


func _generate_encounter_anchors(settlements: Array[Dictionary], player_start_coord: Vector2i = Vector2i(-1, -1)) -> Array:
	var settlement_cells: Array[Vector2i] = []

	for settlement in settlements:
		var origin: Vector2i = settlement.get("origin", Vector2i.ZERO)
		var footprint_size: Vector2i = settlement.get("footprint_size", Vector2i.ONE)
		for y in range(footprint_size.y):
			for x in range(footprint_size.x):
				settlement_cells.append(origin + Vector2i(x, y))

	var encounter_anchors: Array = []
	if _generation_config.procedural_generation_enabled:
		encounter_anchors = _generate_procedural_encounter_anchors(settlement_cells)
	else:
		var monster_index := 0
		for rule in _generation_config.wild_monster_distribution:
			for chunk_coord in rule.chunk_coords:
				for offset in range(max(rule.density_per_chunk, 0)):
					var spawn_coord := _pick_monster_coord_for_chunk(chunk_coord, rule.min_distance_to_settlement, settlement_cells, offset)
					if spawn_coord == Vector2i(-1, -1):
						continue

					monster_index += 1
					encounter_anchors.append(
						_build_encounter_anchor(
							StringName("wild_%d" % monster_index),
							rule.monster_template_id,
							rule.monster_name,
							spawn_coord,
							rule.vision_range,
							rule.region_tag,
							ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SINGLE,
							rule.encounter_profile_id
						)
					)

	_ensure_starting_wild_encounter(encounter_anchors, settlement_cells, player_start_coord)
	_ensure_default_settlement_encounter(encounter_anchors, settlement_cells)
	return encounter_anchors


func _generate_procedural_encounter_anchors(settlement_cells: Array[Vector2i]) -> Array:
	var encounter_anchors: Array = []
	if _generation_config.wild_monster_distribution.is_empty():
		return encounter_anchors

	var north_rule: WildSpawnRule = _generation_config.wild_monster_distribution[0]
	var south_rule: WildSpawnRule = _generation_config.wild_monster_distribution[min(1, _generation_config.wild_monster_distribution.size() - 1)]
	var world_chunks: Vector2i = _generation_config.world_size_in_chunks
	var midpoint_chunk_y: int = int(world_chunks.y / 2)
	var monster_index := 0

	for chunk_y in range(world_chunks.y):
		for chunk_x in range(world_chunks.x):
			var chunk_coord := Vector2i(chunk_x, chunk_y)
			var rule: WildSpawnRule = north_rule if chunk_y < midpoint_chunk_y else south_rule
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
				encounter_anchors.append(
					_build_encounter_anchor(
						StringName("wild_%d" % monster_index),
						rule.monster_template_id,
						rule.monster_name,
						spawn_coord,
						rule.vision_range,
						rule.region_tag,
						ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SINGLE,
						rule.encounter_profile_id
					)
				)

	return encounter_anchors


func _ensure_starting_wild_encounter(
	encounter_anchors: Array,
	settlement_cells: Array[Vector2i],
	player_start_coord: Vector2i
) -> void:
	if not _generation_config.guarantee_starting_wild_encounter:
		return
	if not _grid_system.is_cell_inside_world(player_start_coord):
		return
	if _generation_config.wild_monster_distribution.is_empty():
		return

	var rule: WildSpawnRule = _generation_config.wild_monster_distribution[0]
	var min_distance: int = max(
		int(_generation_config.starting_wild_spawn_min_distance),
		int(rule.min_distance_to_settlement)
	)
	var max_distance: int = max(
		max(
			int(_generation_config.starting_wild_spawn_min_distance),
			int(_generation_config.starting_wild_spawn_max_distance)
		),
		min_distance
	)
	if _has_starting_encounter_in_range(encounter_anchors, player_start_coord, max_distance):
		return

	var spawn_coord := _find_starting_wild_coord(
		player_start_coord,
		settlement_cells,
		encounter_anchors,
		min_distance,
		max_distance
	)
	if spawn_coord == Vector2i(-1, -1):
		push_warning("Unable to place a guaranteed starting wild encounter near %s." % player_start_coord)
		return

	encounter_anchors.append(
		_build_encounter_anchor(
			StringName("wild_%d" % (encounter_anchors.size() + 1)),
			rule.monster_template_id,
			rule.monster_name,
			spawn_coord,
			rule.vision_range,
			rule.region_tag,
			ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SINGLE,
			rule.encounter_profile_id
		)
	)


func _has_starting_encounter_in_range(encounter_anchors: Array, player_start_coord: Vector2i, max_distance: int) -> bool:
	for encounter_anchor_data in encounter_anchors:
		var encounter_anchor = encounter_anchor_data
		if encounter_anchor == null:
			continue
		var encounter_coord: Vector2i = encounter_anchor.world_coord
		var delta: Vector2i = encounter_coord - player_start_coord
		if absi(delta.x) + absi(delta.y) <= max_distance:
			return true
	return false


func _find_starting_wild_coord(
	player_start_coord: Vector2i,
	settlement_cells: Array[Vector2i],
	encounter_anchors: Array,
	min_distance: int,
	max_distance: int
) -> Vector2i:
	var candidates: Array[Vector2i] = []
	for offset_y in range(-max_distance, max_distance + 1):
		for offset_x in range(-max_distance, max_distance + 1):
			var distance := absi(offset_x) + absi(offset_y)
			if distance < min_distance or distance > max_distance:
				continue

			var candidate := player_start_coord + Vector2i(offset_x, offset_y)
			if not _grid_system.is_cell_inside_world(candidate):
				continue
			if _grid_system.get_occupant_root(candidate) != "":
				continue
			if _is_too_close_to_settlement(candidate, min_distance, settlement_cells):
				continue
			if _has_encounter_anchor_at(encounter_anchors, candidate):
				continue
			candidates.append(candidate)

	if candidates.is_empty():
		return Vector2i(-1, -1)
	return candidates[_rng.randi_range(0, candidates.size() - 1)]


func _has_encounter_anchor_at(encounter_anchors: Array, coord: Vector2i) -> bool:
	for encounter_anchor_data in encounter_anchors:
		var encounter_anchor = encounter_anchor_data
		if encounter_anchor == null:
			continue
		if encounter_anchor.world_coord == coord:
			return true
	return false


func _ensure_default_settlement_encounter(encounter_anchors: Array, settlement_cells: Array[Vector2i]) -> void:
	for encounter_anchor_data in encounter_anchors:
		var existing_anchor = encounter_anchor_data
		if existing_anchor == null:
			continue
		if existing_anchor.encounter_kind == ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT:
			return

	for rule in _generation_config.wild_monster_distribution:
		if rule == null or rule.monster_template_id != &"wolf_pack":
			continue
		for chunk_coord in _build_default_settlement_candidate_chunks(rule):
			var spawn_coord := _pick_monster_coord_for_chunk(
				chunk_coord,
				maxi(int(rule.min_distance_to_settlement), 2),
				settlement_cells,
				int(_generation_config.seed) + chunk_coord.x * 4099 + chunk_coord.y * 8191 + 17
			)
			if spawn_coord == Vector2i(-1, -1):
				continue
			if _has_encounter_anchor_at(encounter_anchors, spawn_coord):
				continue
			encounter_anchors.append(
				_build_encounter_anchor(
					StringName("wild_settlement_%d" % (encounter_anchors.size() + 1)),
					rule.monster_template_id,
					"荒狼巢穴",
					spawn_coord,
					maxi(int(rule.vision_range), 2),
					rule.region_tag,
					ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT,
					&"wolf_den",
					0
				)
			)
			return


func _build_default_settlement_candidate_chunks(rule: WildSpawnRule) -> Array[Vector2i]:
	if rule != null and not rule.chunk_coords.is_empty():
		return rule.chunk_coords.duplicate()
	var candidate_chunks: Array[Vector2i] = []
	var world_chunks: Vector2i = _generation_config.world_size_in_chunks
	var midpoint_chunk_y: int = int(world_chunks.y / 2)
	for chunk_y in range(world_chunks.y):
		for chunk_x in range(world_chunks.x):
			if rule != null and rule.monster_template_id == &"wolf_pack" and chunk_y >= midpoint_chunk_y:
				continue
			candidate_chunks.append(Vector2i(chunk_x, chunk_y))
	return candidate_chunks


func _build_encounter_anchor(
	entity_id: StringName,
	monster_template_id: StringName,
	display_name: String,
	world_coord: Vector2i,
	vision_range: int,
	region_tag: StringName,
	encounter_kind: StringName = ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SINGLE,
	encounter_profile_id: StringName = &"",
	growth_stage: int = 0
):
	var encounter_anchor = ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = entity_id
	encounter_anchor.display_name = display_name
	encounter_anchor.world_coord = world_coord
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.enemy_roster_template_id = monster_template_id
	encounter_anchor.region_tag = region_tag
	encounter_anchor.vision_range = vision_range
	encounter_anchor.is_cleared = false
	encounter_anchor.encounter_kind = encounter_kind
	encounter_anchor.encounter_profile_id = encounter_profile_id
	encounter_anchor.growth_stage = maxi(growth_stage, 0)
	encounter_anchor.suppressed_until_step = 0
	return encounter_anchor


func _generate_world_events() -> Array[Dictionary]:
	var generated_events: Array[Dictionary] = []
	for event_variant in _generation_config.world_events:
		var event_config := event_variant as WORLD_EVENT_CONFIG_SCRIPT
		if event_config == null or event_config.event_id == &"":
			continue
		generated_events.append({
			"event_id": String(event_config.event_id),
			"display_name": event_config.display_name,
			"world_coord": event_config.world_coord,
			"event_type": String(event_config.event_type),
			"target_submap_id": String(event_config.target_submap_id),
			"discovery_condition_id": String(event_config.discovery_condition_id),
			"prompt_title": event_config.prompt_title,
			"prompt_text": event_config.prompt_text,
			"is_discovered": _is_world_event_discovered_by_default(event_config),
		})
	return generated_events


func _generate_mounted_submaps() -> Dictionary:
	var mounted_submaps: Dictionary = {}
	for submap_variant in _generation_config.mounted_submaps:
		var submap_config := submap_variant as MOUNTED_SUBMAP_CONFIG_SCRIPT
		if submap_config == null or submap_config.submap_id == &"":
			continue
		mounted_submaps[String(submap_config.submap_id)] = {
			"submap_id": String(submap_config.submap_id),
			"display_name": submap_config.display_name,
			"generation_config_path": submap_config.generation_config_path,
			"return_hint_text": submap_config.return_hint_text,
			"is_generated": false,
			"player_coord": Vector2i(-1, -1),
			"world_data": {},
		}
	return mounted_submaps


func _is_world_event_discovered_by_default(event_config: WORLD_EVENT_CONFIG_SCRIPT) -> bool:
	if event_config == null:
		return false
	var condition_id := String(event_config.discovery_condition_id).strip_edges()
	return condition_id.is_empty() or condition_id == "always_true"


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
