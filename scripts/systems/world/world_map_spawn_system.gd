## 文件说明：该脚本属于世界地图生成系统相关的系统脚本，集中维护随机数生成器、生成配置、网格系统等顶层字段。
## 审查重点：重点核对字段默认值、状态流转顺序、跨系统引用关系以及运行时读写时机是否仍然可靠。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name WorldMapSpawnSystem
extends RefCounted

const SETTLEMENT_CONFIG_SCRIPT = preload("res://scripts/utils/settlement_config.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const WORLD_EVENT_CONFIG_SCRIPT = preload("res://scripts/utils/world_event_config.gd")
const MOUNTED_SUBMAP_CONFIG_SCRIPT = preload("res://scripts/utils/mounted_submap_config.gd")
const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")
const DEFAULT_MAIN_WORLD_SETTLEMENT_BUNDLE_PATH := "res://data/configs/world_map/shared/main_world_default_settlement_bundle.tres"
const DEFAULT_MAIN_WORLD_WILD_SPAWN_BUNDLE_PATH := "res://data/configs/world_map/shared/main_world_default_wild_spawn_bundle.tres"
const DEFAULT_MAIN_WORLD_SETTLEMENT_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_settlement_name_pool.tres"
const DEFAULT_MAIN_WORLD_TOWN_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_town_name_pool.tres"
const DEFAULT_MAIN_WORLD_CITY_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_city_name_pool.tres"
const DEFAULT_MAIN_WORLD_CAPITAL_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_capital_name_pool.tres"
const DEFAULT_MAIN_WORLD_METROPOLIS_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_metropolis_name_pool.tres"

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

## 字段说明：缓存随机数生成器实例，保证生成逻辑集中使用同一套随机来源。
var _rng := RandomNumberGenerator.new()
## 字段说明：记录本次建图由真随机接口分配的地图种子，派生地图随机项只在本次生成内消费。
var _map_seed := 0
## 字段说明：记录生成配置，会参与运行时状态流转、系统协作和存档恢复。
var _generation_config
## 字段说明：记录网格系统，会参与运行时状态流转、系统协作和存档恢复。
var _grid_system
## 字段说明：记录按标识索引的设施资源库，作为查表、序列化和跨系统引用时使用的主键。
var _facility_library_by_id: Dictionary = {}
## 字段说明：记录按标识索引的聚落资源库，作为查表、序列化和跨系统引用时使用的主键。
var _settlement_library_by_id: Dictionary = {}
## 字段说明：缓存当前世界最终生效的设施模板集合，便于统一承接共享内容注入与局部覆盖。
var _resolved_facility_library: Array = []
## 字段说明：缓存当前世界最终生效的聚落模板集合，便于统一承接共享内容注入与局部覆盖。
var _resolved_settlement_library: Array = []
## 字段说明：缓存当前世界最终生效的野外生成规则集合，便于统一承接共享内容注入与局部覆盖。
var _resolved_wild_spawn_rules: Array = []
## 字段说明：缓存本次建图解析出的默认主世界据点/设施共享包，避免同一次建图重复加载共享资源。
var _default_main_world_settlement_bundle = null
## 字段说明：缓存本次建图解析出的默认主世界野外生成规则包，避免同一次建图重复加载共享资源。
var _default_main_world_wild_spawn_bundle = null
## 字段说明：缓存本次建图可用的默认主世界据点展示名池，保证实例展示名随机且不重复。
var _remaining_default_main_world_settlement_display_names: Array[String] = []
## 字段说明：缓存 town 模板专用的默认主世界城镇名池，保证城镇实例展示名使用专门语义且不重复。
var _remaining_default_main_world_town_display_names: Array[String] = []
## 字段说明：缓存 city 模板专用的默认主世界城市名池，保证城市实例展示名使用专门语义且不重复。
var _remaining_default_main_world_city_display_names: Array[String] = []
## 字段说明：缓存 capital 模板专用的默认主世界主城名池，保证主城实例展示名使用专门语义且不重复。
var _remaining_default_main_world_capital_display_names: Array[String] = []
## 字段说明：缓存 metropolis 模板专用的默认主世界都会名池，保证都会实例展示名使用专门语义且不重复。
var _remaining_default_main_world_metropolis_display_names: Array[String] = []


func build_world(generation_config, grid_system) -> Dictionary:
	_generation_config = generation_config
	_grid_system = grid_system
	_map_seed = TRUE_RANDOM_SEED_SERVICE_SCRIPT.generate_seed()
	_rng.seed = _map_seed
	_build_libraries()

	var settlements := _generate_settlements()
	var player_start_settlement := _find_player_start_settlement(settlements)
	var player_start_coord: Vector2i = _resolve_player_start_coord(player_start_settlement)
	var world_npcs := _generate_world_npcs(settlements)
	var encounter_anchors := _generate_encounter_anchors(settlements, player_start_coord)

	return {
		"map_seed": _map_seed,
		"settlements": settlements,
		"world_npcs": world_npcs,
		"encounter_anchors": encounter_anchors,
		"world_events": _generate_world_events(),
		"mounted_submaps": _generate_mounted_submaps(),
		"active_submap_id": "",
		"submap_return_stack": [],
		"world_step": 0,
		"next_equipment_instance_serial": 1,
		"player_start_coord": player_start_coord,
		"player_start_settlement_id": player_start_settlement.get("settlement_id", ""),
		"player_start_settlement_name": player_start_settlement.get("display_name", ""),
	}


func _build_libraries() -> void:
	_facility_library_by_id.clear()
	_settlement_library_by_id.clear()
	_default_main_world_settlement_bundle = _load_default_main_world_settlement_bundle()
	_default_main_world_wild_spawn_bundle = _load_default_main_world_wild_spawn_bundle()
	_remaining_default_main_world_settlement_display_names = _build_default_main_world_settlement_display_names()
	_remaining_default_main_world_town_display_names = _build_default_main_world_town_display_names()
	_remaining_default_main_world_city_display_names = _build_default_main_world_city_display_names()
	_remaining_default_main_world_capital_display_names = _build_default_main_world_capital_display_names()
	_remaining_default_main_world_metropolis_display_names = _build_default_main_world_metropolis_display_names()
	_resolved_facility_library = _resolve_effective_facility_library()
	_resolved_settlement_library = _resolve_effective_settlement_library()
	_resolved_wild_spawn_rules = _resolve_effective_wild_spawn_rules()

	for facility_config in _resolved_facility_library:
		var facility_template_id := _get_facility_template_id(facility_config)
		if facility_template_id.is_empty():
			continue
		_facility_library_by_id[facility_template_id] = facility_config

	for settlement_config in _resolved_settlement_library:
		var settlement_template_id := _get_settlement_template_id(settlement_config)
		if settlement_template_id.is_empty():
			continue
		_settlement_library_by_id[settlement_template_id] = settlement_config


func _generate_settlements() -> Array[Dictionary]:
	if _generation_config.procedural_generation_enabled:
		return _generate_procedural_settlements()

	return _generate_fixed_settlements()


func _generate_fixed_settlements() -> Array[Dictionary]:
	var settlements: Array[Dictionary] = []
	var instance_counts: Dictionary = {}

	for distribution_rule in _generation_config.settlement_distribution:
		var settlement_template_id := _get_distribution_rule_template_id(distribution_rule)
		var settlement_config = _settlement_library_by_id.get(settlement_template_id)
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

	for settlement_config in _resolved_settlement_library:
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
		push_error("Invalid settlement placement for %s at %s" % [_get_settlement_template_id(settlement_config), origin])
		return {}

	var template_id := _get_settlement_template_id(settlement_config)
	if template_id.is_empty():
		push_error("Settlement template is missing template_id for placement at %s." % origin)
		return {}
	var instance_index: int = int(instance_counts.get(template_id, 0)) + 1
	instance_counts[template_id] = instance_index

	var settlement_id := _build_settlement_instance_id(template_id, instance_index)
	var display_name := _resolve_settlement_display_name(settlement_config, template_id, instance_index)

	var entity_id := "settlement_%s" % settlement_id
	_grid_system.register_footprint(entity_id, origin, footprint_size)

	var facilities := _generate_facilities_for_settlement(settlement_id, settlement_config, origin)
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
	settlement["available_services"] = _collect_services(settlement_id, facilities)
	settlement["service_npcs"] = _collect_service_npcs(facilities)
	return settlement


func _generate_facilities_for_settlement(settlement_id: String, settlement_config, settlement_origin: Vector2i) -> Array[Dictionary]:
	var generated_facilities: Array[Dictionary] = []
	var used_slot_ids: Dictionary = {}

	for facility_template_id in settlement_config.guaranteed_facility_ids:
		var facility_config = _facility_library_by_id.get(facility_template_id)
		if facility_config == null:
			continue

		var placed_facility := _try_place_facility(
			settlement_id,
			facility_config,
			settlement_config,
			settlement_origin,
			used_slot_ids
		)
		if not placed_facility.is_empty():
			generated_facilities.append(placed_facility)

	var optional_limit: int = min(
		settlement_config.max_optional_facilities,
		max(settlement_config.facility_slots.size() - generated_facilities.size(), 0)
	)
	var optional_pool: Array = settlement_config.optional_facility_pool.duplicate()

	for _optional_index in range(optional_limit):
		var selected_facility_template_id := _pick_weighted_facility(optional_pool)
		if selected_facility_template_id.is_empty():
			break

		var facility_config = _facility_library_by_id.get(selected_facility_template_id)
		if facility_config == null:
			continue

		var placed_facility := _try_place_facility(
			settlement_id,
			facility_config,
			settlement_config,
			settlement_origin,
			used_slot_ids
		)
		if placed_facility.is_empty():
			continue

		generated_facilities.append(placed_facility)
		_remove_weighted_entry(optional_pool, selected_facility_template_id)

	return generated_facilities


func _try_place_facility(
	settlement_id: String,
	facility_config,
	settlement_config,
	settlement_origin: Vector2i,
	used_slot_ids: Dictionary
) -> Dictionary:
	if facility_config.min_settlement_tier > settlement_config.tier:
		return {}
	var facility_template_id := _get_facility_template_id(facility_config)
	if facility_template_id.is_empty():
		return {}

	for slot_config in settlement_config.facility_slots:
		if used_slot_ids.has(slot_config.slot_id):
			continue
		if not facility_config.allowed_slot_tags.is_empty() and not facility_config.allowed_slot_tags.has(slot_config.slot_tag):
			continue

		used_slot_ids[slot_config.slot_id] = true
		var facility_id := _build_facility_instance_id(settlement_id, facility_template_id, slot_config.slot_id)
		var service_npcs: Array[Dictionary] = []
		var npc_index := 0
		for npc_config in facility_config.bound_service_npcs:
			var npc_template_id := _get_npc_template_id(npc_config)
			if npc_template_id.is_empty():
				continue
			service_npcs.append({
				"template_id": npc_template_id,
				"npc_id": _build_npc_instance_id(facility_id, npc_template_id, npc_config.local_slot_id, npc_index),
				"display_name": npc_config.display_name,
				"service_type": npc_config.service_type,
				"interaction_script_id": npc_config.interaction_script_id,
				"local_slot_id": npc_config.local_slot_id,
				"facility_id": facility_id,
				"facility_template_id": facility_template_id,
				"facility_name": facility_config.display_name,
				"settlement_id": settlement_id,
			})
			npc_index += 1

		return {
			"template_id": facility_template_id,
			"facility_id": facility_id,
			"display_name": facility_config.display_name,
			"category": facility_config.category,
			"interaction_type": facility_config.interaction_type,
			"slot_id": slot_config.slot_id,
			"slot_tag": slot_config.slot_tag,
			"local_coord": slot_config.local_coord,
			"world_coord": settlement_origin + slot_config.local_coord,
			"settlement_id": settlement_id,
			"service_npcs": service_npcs,
		}

	return {}


func _collect_services(settlement_id: String, facilities: Array[Dictionary]) -> Array[Dictionary]:
	var services: Array[Dictionary] = []
	var has_party_warehouse_service := false

	for facility in facilities:
		for npc in facility.get("service_npcs", []):
			var interaction_script_id := String(npc.get("interaction_script_id", ""))
			if interaction_script_id == "party_warehouse":
				has_party_warehouse_service = true
			services.append({
				"settlement_id": settlement_id,
				"facility_id": facility.get("facility_id", ""),
				"facility_template_id": facility.get("template_id", ""),
				"facility_name": facility.get("display_name", ""),
				"npc_id": npc.get("npc_id", ""),
				"npc_template_id": npc.get("template_id", ""),
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
			"settlement_id": settlement_id,
			"facility_id": "%s__settlement_service_desk" % settlement_id,
			"facility_template_id": "",
			"facility_name": "据点服务台",
			"npc_id": "%s__settlement_quartermaster" % settlement_id,
			"npc_template_id": "",
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
		"shop_inventory_seed": TRUE_RANDOM_SEED_SERVICE_SCRIPT.generate_seed(),
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


func _get_settlement_template_id(settlement_config) -> String:
	if settlement_config == null:
		return ""
	if settlement_config.has_method("get_template_id"):
		return String(settlement_config.get_template_id())
	return String(settlement_config.settlement_id).strip_edges()


func _get_distribution_rule_template_id(distribution_rule) -> String:
	if distribution_rule == null:
		return ""
	if distribution_rule.has_method("get_settlement_template_id"):
		return String(distribution_rule.get_settlement_template_id())
	return String(distribution_rule.settlement_id).strip_edges()


func _get_facility_template_id(facility_config) -> String:
	if facility_config == null:
		return ""
	if facility_config.has_method("get_template_id"):
		return String(facility_config.get_template_id())
	return String(facility_config.facility_id).strip_edges()


func _get_npc_template_id(npc_config) -> String:
	if npc_config == null:
		return ""
	if npc_config.has_method("get_template_id"):
		return String(npc_config.get_template_id())
	return String(npc_config.npc_id).strip_edges()


func _get_weighted_facility_template_id(weighted_entry) -> String:
	if weighted_entry == null:
		return ""
	if weighted_entry.has_method("get_facility_template_id"):
		return String(weighted_entry.get_facility_template_id())
	return String(weighted_entry.facility_id).strip_edges()


func _build_settlement_instance_id(template_id: String, instance_index: int) -> String:
	if template_id.is_empty():
		return ""
	return "%s_%02d" % [template_id, maxi(instance_index, 1)]


func _build_facility_instance_id(settlement_id: String, template_id: String, slot_id: String) -> String:
	var normalized_template_id := template_id.strip_edges().to_snake_case()
	var normalized_slot_id := slot_id.strip_edges().to_snake_case()
	if normalized_template_id.is_empty():
		normalized_template_id = "facility"
	if normalized_slot_id.is_empty():
		normalized_slot_id = "slot"
	return "%s__%s__%s" % [settlement_id, normalized_template_id, normalized_slot_id]


func _build_npc_instance_id(
	facility_id: String,
	template_id: String,
	local_slot_id: String,
	npc_index: int
) -> String:
	var normalized_template_id := template_id.strip_edges().to_snake_case()
	var normalized_slot_id := local_slot_id.strip_edges().to_snake_case()
	if normalized_template_id.is_empty():
		normalized_template_id = "npc"
	if normalized_slot_id.is_empty():
		normalized_slot_id = "slot_%02d" % maxi(npc_index + 1, 1)
	return "%s__%s__%s" % [facility_id, normalized_template_id, normalized_slot_id]


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
			return _get_weighted_facility_template_id(entry)

	return _get_weighted_facility_template_id(optional_pool[0])


func _remove_weighted_entry(optional_pool: Array, facility_id: String) -> void:
	for index in range(optional_pool.size()):
		var entry = optional_pool[index]
		if _get_weighted_facility_template_id(entry) == facility_id:
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
		for rule in _resolved_wild_spawn_rules:
			for chunk_coord in rule.chunk_coords:
				for offset in range(max(rule.density_per_chunk, 0)):
					var spawn_coord := _pick_monster_coord_for_chunk(chunk_coord, rule.min_distance_to_settlement, settlement_cells, offset)
					if spawn_coord == Vector2i(-1, -1):
						continue

					monster_index += 1
					encounter_anchors.append(
						_build_encounter_anchor(
							StringName("wild_%d" % monster_index),
							rule.enemy_roster_template_id,
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
	if _resolved_wild_spawn_rules.is_empty():
		return encounter_anchors

	var world_chunks: Vector2i = _generation_config.world_size_in_chunks
	var midpoint_chunk_y: int = int(world_chunks.y / 2)
	var monster_index := 0
	var spawn_chunk_chance_denominator := maxi(
		int(_generation_config.procedural_wild_spawn_chunk_chance_denominator),
		1
	)

	for chunk_y in range(world_chunks.y):
		for chunk_x in range(world_chunks.x):
			var chunk_coord := Vector2i(chunk_x, chunk_y)
			var rule: WildSpawnRule = _resolve_procedural_wild_spawn_rule_for_chunk_y(chunk_y)
			if rule == null:
				continue
			var chunk_seed: int = TRUE_RANDOM_SEED_SERVICE_SCRIPT.generate_seed()
			if posmod(chunk_seed, spawn_chunk_chance_denominator) != 0:
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
						rule.enemy_roster_template_id,
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
	if _resolved_wild_spawn_rules.is_empty():
		return

	var rule: WildSpawnRule = _resolved_wild_spawn_rules[0]
	if _generation_config.procedural_generation_enabled:
		var player_chunk_coord: Vector2i = _grid_system.get_chunk_coord(player_start_coord)
		rule = _resolve_procedural_wild_spawn_rule_for_chunk_y(player_chunk_coord.y)
	if rule == null:
		return
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
			rule.enemy_roster_template_id,
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

	for rule in _resolved_wild_spawn_rules:
		if rule == null or rule.enemy_roster_template_id != &"wolf_pack":
			continue
		for chunk_coord in _build_default_settlement_candidate_chunks(rule):
			var spawn_coord := _pick_monster_coord_for_chunk(
				chunk_coord,
				maxi(int(rule.min_distance_to_settlement), 2),
				settlement_cells,
				TRUE_RANDOM_SEED_SERVICE_SCRIPT.generate_seed()
			)
			if spawn_coord == Vector2i(-1, -1):
				continue
			if _has_encounter_anchor_at(encounter_anchors, spawn_coord):
				continue
			encounter_anchors.append(
				_build_encounter_anchor(
					StringName("wild_settlement_%d" % (encounter_anchors.size() + 1)),
					rule.enemy_roster_template_id,
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
			if rule != null and rule.enemy_roster_template_id == &"wolf_pack" and chunk_y >= midpoint_chunk_y:
				continue
			candidate_chunks.append(Vector2i(chunk_x, chunk_y))
	return candidate_chunks


func _resolve_effective_settlement_library() -> Array:
	var resolved: Array = []
	var default_bundle = _default_main_world_settlement_bundle
	if default_bundle != null:
		resolved.append_array(default_bundle.settlement_library)
	for settlement_config in _generation_config.settlement_library:
		resolved.append(settlement_config)
	return resolved


func _resolve_effective_facility_library() -> Array:
	var resolved: Array = []
	var default_bundle = _default_main_world_settlement_bundle
	if default_bundle != null:
		resolved.append_array(default_bundle.facility_library)
	for facility_config in _generation_config.facility_library:
		resolved.append(facility_config)
	return resolved


func _resolve_effective_wild_spawn_rules() -> Array:
	var resolved: Array = []
	var default_bundle = _default_main_world_wild_spawn_bundle
	if default_bundle != null:
		resolved.append_array(default_bundle.wild_monster_distribution)
	for rule in _generation_config.wild_monster_distribution:
		resolved.append(rule)
	return resolved


func _load_default_main_world_settlement_bundle():
	if _generation_config == null or not bool(_generation_config.inject_default_main_world_content):
		return null
	var settlement_bundle = load(DEFAULT_MAIN_WORLD_SETTLEMENT_BUNDLE_PATH)
	if settlement_bundle == null:
		push_warning("Unable to load default main-world settlement bundle from %s." % DEFAULT_MAIN_WORLD_SETTLEMENT_BUNDLE_PATH)
	return settlement_bundle


func _load_default_main_world_wild_spawn_bundle():
	if _generation_config == null or not bool(_generation_config.inject_default_main_world_content):
		return null
	var wild_spawn_bundle = load(DEFAULT_MAIN_WORLD_WILD_SPAWN_BUNDLE_PATH)
	if wild_spawn_bundle == null:
		push_warning("Unable to load default main-world wild spawn bundle from %s." % DEFAULT_MAIN_WORLD_WILD_SPAWN_BUNDLE_PATH)
	return wild_spawn_bundle


func _build_default_main_world_settlement_display_names() -> Array[String]:
	return _build_shuffled_display_names_from_pool(
		DEFAULT_MAIN_WORLD_SETTLEMENT_NAME_POOL_PATH,
		104729,
		"default main-world settlement"
	)


func _build_default_main_world_town_display_names() -> Array[String]:
	return _build_shuffled_display_names_from_pool(
		DEFAULT_MAIN_WORLD_TOWN_NAME_POOL_PATH,
		130363,
		"default main-world town"
	)


func _build_default_main_world_city_display_names() -> Array[String]:
	return _build_shuffled_display_names_from_pool(
		DEFAULT_MAIN_WORLD_CITY_NAME_POOL_PATH,
		155921,
		"default main-world city"
	)


func _build_default_main_world_capital_display_names() -> Array[String]:
	return _build_shuffled_display_names_from_pool(
		DEFAULT_MAIN_WORLD_CAPITAL_NAME_POOL_PATH,
		181081,
		"default main-world capital"
	)


func _build_default_main_world_metropolis_display_names() -> Array[String]:
	return _build_shuffled_display_names_from_pool(
		DEFAULT_MAIN_WORLD_METROPOLIS_NAME_POOL_PATH,
		206369,
		"default main-world metropolis"
	)


func _build_shuffled_display_names_from_pool(resource_path: String, _seed_offset: int, warning_label: String) -> Array[String]:
	var name_pool = _load_default_main_world_settlement_name_pool(resource_path, warning_label)
	if name_pool == null:
		return []
	var unique_names: Array[String] = name_pool.build_unique_display_names()
	if unique_names.is_empty():
		return []

	var name_rng := RandomNumberGenerator.new()
	name_rng.seed = TRUE_RANDOM_SEED_SERVICE_SCRIPT.generate_seed()
	for index in range(unique_names.size() - 1, 0, -1):
		var swap_index := name_rng.randi_range(0, index)
		var temp_name := unique_names[index]
		unique_names[index] = unique_names[swap_index]
		unique_names[swap_index] = temp_name
	return unique_names


func _load_default_main_world_settlement_name_pool(resource_path: String, warning_label: String):
	if _generation_config == null or not bool(_generation_config.inject_default_main_world_content):
		return null
	var name_pool = load(resource_path)
	if name_pool == null:
		push_warning("Unable to load %s name pool from %s." % [warning_label, resource_path])
	return name_pool


func _resolve_procedural_wild_spawn_rule_for_chunk_y(chunk_y: int) -> WildSpawnRule:
	var north_rule: WildSpawnRule = _find_wild_spawn_rule_by_region_tag(&"north_wilds")
	var south_rule: WildSpawnRule = _find_wild_spawn_rule_by_region_tag(&"south_wilds")
	if north_rule == null and not _resolved_wild_spawn_rules.is_empty():
		north_rule = _resolved_wild_spawn_rules[0] as WildSpawnRule
	if south_rule == null:
		if _resolved_wild_spawn_rules.size() > 1:
			south_rule = _resolved_wild_spawn_rules[1] as WildSpawnRule
		else:
			south_rule = north_rule
	if north_rule == null:
		return south_rule
	if south_rule == null:
		return north_rule

	var midpoint_chunk_y: int = int(_generation_config.world_size_in_chunks.y / 2)
	return north_rule if chunk_y < midpoint_chunk_y else south_rule


func _find_wild_spawn_rule_by_region_tag(region_tag: StringName) -> WildSpawnRule:
	for rule_variant in _resolved_wild_spawn_rules:
		var rule := rule_variant as WildSpawnRule
		if rule == null:
			continue
		if StringName(rule.region_tag) == region_tag:
			return rule
	return null


func _resolve_settlement_display_name(settlement_config, template_id: String, instance_index: int) -> String:
	if template_id == "template_town" and not _remaining_default_main_world_town_display_names.is_empty():
		return _remaining_default_main_world_town_display_names.pop_back()
	if template_id == "template_city" and not _remaining_default_main_world_city_display_names.is_empty():
		return _remaining_default_main_world_city_display_names.pop_back()
	if template_id == "template_capital" and not _remaining_default_main_world_capital_display_names.is_empty():
		return _remaining_default_main_world_capital_display_names.pop_back()
	if template_id == "template_metropolis" and not _remaining_default_main_world_metropolis_display_names.is_empty():
		return _remaining_default_main_world_metropolis_display_names.pop_back()
	if template_id == "template_world_stronghold":
		var stronghold_display_name: String = settlement_config.display_name
		if instance_index > 1:
			stronghold_display_name = "%s %02d" % [stronghold_display_name, instance_index]
		return stronghold_display_name
	if template_id.begins_with("template_") and not _remaining_default_main_world_settlement_display_names.is_empty():
		return _remaining_default_main_world_settlement_display_names.pop_back()

	var display_name: String = settlement_config.display_name
	if instance_index > 1:
		display_name = "%s %02d" % [display_name, instance_index]
	return display_name


func _build_encounter_anchor(
	entity_id: StringName,
	enemy_roster_template_id: StringName,
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
	encounter_anchor.enemy_roster_template_id = enemy_roster_template_id
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
