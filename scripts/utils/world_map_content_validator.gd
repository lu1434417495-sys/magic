class_name WorldMapContentValidator
extends RefCounted

const WORLD_PRESET_REGISTRY_SCRIPT = preload("res://scripts/utils/world_preset_registry.gd")
const WORLD_MAP_GENERATION_CONFIG_SCRIPT = preload("res://scripts/utils/world_map_generation_config.gd")
const SETTLEMENT_CONFIG_SCRIPT = preload("res://scripts/utils/settlement_config.gd")
const FACILITY_CONFIG_SCRIPT = preload("res://scripts/utils/facility_config.gd")
const FACILITY_SLOT_CONFIG_SCRIPT = preload("res://scripts/utils/facility_slot_config.gd")
const FACILITY_NPC_CONFIG_SCRIPT = preload("res://scripts/utils/facility_npc_config.gd")
const SETTLEMENT_DISTRIBUTION_RULE_SCRIPT = preload("res://scripts/utils/settlement_distribution_rule.gd")
const WEIGHTED_FACILITY_ENTRY_SCRIPT = preload("res://scripts/utils/weighted_facility_entry.gd")
const WILD_SPAWN_RULE_SCRIPT = preload("res://scripts/utils/wild_spawn_rule.gd")
const WORLD_MAP_SETTLEMENT_BUNDLE_SCRIPT = preload("res://scripts/utils/world_map_settlement_bundle.gd")
const WORLD_MAP_WILD_SPAWN_BUNDLE_SCRIPT = preload("res://scripts/utils/world_map_wild_spawn_bundle.gd")
const WORLD_MAP_SETTLEMENT_NAME_POOL_SCRIPT = preload("res://scripts/utils/world_map_settlement_name_pool.gd")

const DEFAULT_MAIN_WORLD_SETTLEMENT_BUNDLE_PATH := "res://data/configs/world_map/shared/main_world_default_settlement_bundle.tres"
const DEFAULT_MAIN_WORLD_WILD_SPAWN_BUNDLE_PATH := "res://data/configs/world_map/shared/main_world_default_wild_spawn_bundle.tres"
const DEFAULT_MAIN_WORLD_SETTLEMENT_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_settlement_name_pool.tres"
const DEFAULT_MAIN_WORLD_TOWN_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_town_name_pool.tres"
const DEFAULT_MAIN_WORLD_CITY_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_city_name_pool.tres"
const DEFAULT_MAIN_WORLD_CAPITAL_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_capital_name_pool.tres"
const DEFAULT_MAIN_WORLD_METROPOLIS_NAME_POOL_PATH := "res://data/configs/world_map/shared/main_world_metropolis_name_pool.tres"


func validate_world_presets(enemy_templates: Dictionary = {}, wild_encounter_rosters: Dictionary = {}) -> Array[String]:
	var errors: Array[String] = []
	var seen_preset_ids: Dictionary = {}
	var presets := WORLD_PRESET_REGISTRY_SCRIPT.list_presets()
	if presets.is_empty():
		errors.append("World preset registry is empty.")
		return errors

	for preset_variant in presets:
		if preset_variant is not Dictionary:
			errors.append("World preset entry must be a Dictionary.")
			continue
		var preset := preset_variant as Dictionary
		var preset_id := String(preset.get("preset_id", "")).strip_edges()
		var display_name := String(preset.get("display_name", "")).strip_edges()
		var generation_config_path := String(preset.get("generation_config_path", "")).strip_edges()
		if preset_id.is_empty():
			errors.append("World preset entry is missing preset_id.")
			continue
		if seen_preset_ids.has(preset_id):
			errors.append("Duplicate world preset_id registered: %s." % preset_id)
		seen_preset_ids[preset_id] = true
		if display_name.is_empty():
			errors.append("World preset %s is missing display_name." % preset_id)
		if generation_config_path.is_empty():
			errors.append("World preset %s is missing generation_config_path." % preset_id)
			continue
		var generation_config = load(generation_config_path)
		if generation_config == null:
			errors.append("World preset %s failed to load generation config %s." % [preset_id, generation_config_path])
			continue
		errors.append_array(validate_generation_config(
			generation_config,
			generation_config_path,
			enemy_templates,
			wild_encounter_rosters
		))
	return errors


func validate_generation_config(
	generation_config,
	label: String,
	enemy_templates: Dictionary = {},
	wild_encounter_rosters: Dictionary = {}
) -> Array[String]:
	var errors: Array[String] = []
	if generation_config == null or generation_config.get_script() != WORLD_MAP_GENERATION_CONFIG_SCRIPT:
		errors.append("World generation config %s must use WorldMapGenerationConfig." % label)
		return errors

	var world_size_in_chunks: Vector2i = generation_config.get("world_size_in_chunks")
	var chunk_size: Vector2i = generation_config.get("chunk_size")
	if world_size_in_chunks.x <= 0 or world_size_in_chunks.y <= 0:
		errors.append("World generation config %s has invalid world_size_in_chunks %s." % [label, str(world_size_in_chunks)])
	if chunk_size.x <= 0 or chunk_size.y <= 0:
		errors.append("World generation config %s has invalid chunk_size %s." % [label, str(chunk_size)])
	if int(generation_config.get("starting_wild_spawn_min_distance")) > int(generation_config.get("starting_wild_spawn_max_distance")):
		errors.append("World generation config %s has starting_wild_spawn_min_distance greater than max distance." % label)

	var settlement_resources := _build_effective_settlement_resources(generation_config, label, errors)
	var facility_resources := _build_effective_facility_resources(generation_config, label, errors)
	var wild_spawn_rules := _build_effective_wild_spawn_rules(generation_config, label, errors)

	var facility_ids := _validate_facility_library(facility_resources, label, errors)
	var settlement_ids := _validate_settlement_library(settlement_resources, facility_ids, label, errors)
	_validate_settlement_distribution(generation_config.get("settlement_distribution"), settlement_ids, label, errors)
	_validate_wild_spawn_rules(wild_spawn_rules, enemy_templates, wild_encounter_rosters, label, errors)
	return errors


func _build_effective_settlement_resources(generation_config, label: String, errors: Array[String]) -> Array:
	var resources: Array = []
	if bool(generation_config.get("inject_default_main_world_content")):
		var bundle = _load_resource(DEFAULT_MAIN_WORLD_SETTLEMENT_BUNDLE_PATH, WORLD_MAP_SETTLEMENT_BUNDLE_SCRIPT, label, errors)
		if bundle != null:
			resources.append_array(_as_array(bundle.get("settlement_library")))
		_validate_name_pool(DEFAULT_MAIN_WORLD_SETTLEMENT_NAME_POOL_PATH, label, errors)
		_validate_name_pool(DEFAULT_MAIN_WORLD_TOWN_NAME_POOL_PATH, label, errors)
		_validate_name_pool(DEFAULT_MAIN_WORLD_CITY_NAME_POOL_PATH, label, errors)
		_validate_name_pool(DEFAULT_MAIN_WORLD_CAPITAL_NAME_POOL_PATH, label, errors)
		_validate_name_pool(DEFAULT_MAIN_WORLD_METROPOLIS_NAME_POOL_PATH, label, errors)
	resources.append_array(_as_array(generation_config.get("settlement_library")))
	return resources


func _build_effective_facility_resources(generation_config, label: String, errors: Array[String]) -> Array:
	var resources: Array = []
	if bool(generation_config.get("inject_default_main_world_content")):
		var bundle = _load_resource(DEFAULT_MAIN_WORLD_SETTLEMENT_BUNDLE_PATH, WORLD_MAP_SETTLEMENT_BUNDLE_SCRIPT, label, errors)
		if bundle != null:
			resources.append_array(_as_array(bundle.get("facility_library")))
	resources.append_array(_as_array(generation_config.get("facility_library")))
	return resources


func _build_effective_wild_spawn_rules(generation_config, label: String, errors: Array[String]) -> Array:
	var resources: Array = []
	if bool(generation_config.get("inject_default_main_world_content")):
		var bundle = _load_resource(DEFAULT_MAIN_WORLD_WILD_SPAWN_BUNDLE_PATH, WORLD_MAP_WILD_SPAWN_BUNDLE_SCRIPT, label, errors)
		if bundle != null:
			resources.append_array(_as_array(bundle.get("wild_monster_distribution")))
	resources.append_array(_as_array(generation_config.get("wild_monster_distribution")))
	return resources


func _validate_facility_library(facility_resources: Array, label: String, errors: Array[String]) -> Dictionary:
	var ids: Dictionary = {}
	for facility_variant in facility_resources:
		if facility_variant == null or facility_variant.get_script() != FACILITY_CONFIG_SCRIPT:
			errors.append("World generation config %s has non-FacilityConfig facility entry." % label)
			continue
		var facility_id := String(facility_variant.call("get_template_id")).strip_edges()
		if facility_id.is_empty():
			errors.append("World generation config %s has facility missing facility_id." % label)
			continue
		if ids.has(facility_id):
			errors.append("World generation config %s has duplicate facility_id %s." % [label, facility_id])
		ids[facility_id] = true
		if String(facility_variant.get("display_name")).strip_edges().is_empty():
			errors.append("World facility %s in %s is missing display_name." % [facility_id, label])
		if String(facility_variant.get("interaction_type")).strip_edges().is_empty() and _as_array(facility_variant.get("bound_service_npcs")).is_empty():
			errors.append("World facility %s in %s must declare interaction_type or bound service NPCs." % [facility_id, label])
		_validate_facility_npcs(facility_variant.get("bound_service_npcs"), facility_id, label, errors)
	return ids


func _validate_facility_npcs(npc_resources_variant: Variant, facility_id: String, label: String, errors: Array[String]) -> void:
	var npc_ids: Dictionary = {}
	for npc_variant in _as_array(npc_resources_variant):
		if npc_variant == null or npc_variant.get_script() != FACILITY_NPC_CONFIG_SCRIPT:
			errors.append("World facility %s in %s has non-FacilityNpcConfig service NPC." % [facility_id, label])
			continue
		var npc_id := String(npc_variant.call("get_template_id")).strip_edges()
		if npc_id.is_empty():
			errors.append("World facility %s in %s has NPC missing npc_id." % [facility_id, label])
			continue
		if npc_ids.has(npc_id):
			errors.append("World facility %s in %s has duplicate npc_id %s." % [facility_id, label, npc_id])
		npc_ids[npc_id] = true
		if String(npc_variant.get("service_type")).strip_edges().is_empty():
			errors.append("World facility %s NPC %s in %s is missing service_type." % [facility_id, npc_id, label])
		if String(npc_variant.get("interaction_script_id")).strip_edges().is_empty():
			errors.append("World facility %s NPC %s in %s is missing interaction_script_id." % [facility_id, npc_id, label])


func _validate_settlement_library(
	settlement_resources: Array,
	facility_ids: Dictionary,
	label: String,
	errors: Array[String]
) -> Dictionary:
	var ids: Dictionary = {}
	for settlement_variant in settlement_resources:
		if settlement_variant == null or settlement_variant.get_script() != SETTLEMENT_CONFIG_SCRIPT:
			errors.append("World generation config %s has non-SettlementConfig settlement entry." % label)
			continue
		var settlement_id := String(settlement_variant.call("get_template_id")).strip_edges()
		if settlement_id.is_empty():
			errors.append("World generation config %s has settlement missing settlement_id." % label)
			continue
		if ids.has(settlement_id):
			errors.append("World generation config %s has duplicate settlement_id %s." % [label, settlement_id])
		ids[settlement_id] = true
		if String(settlement_variant.get("display_name")).strip_edges().is_empty():
			errors.append("World settlement %s in %s is missing display_name." % [settlement_id, label])
		_validate_facility_slots(settlement_variant.get("facility_slots"), settlement_id, label, errors)
		for facility_id_variant in settlement_variant.get("guaranteed_facility_ids"):
			var facility_id := String(facility_id_variant).strip_edges()
			if facility_id.is_empty():
				errors.append("World settlement %s in %s has empty guaranteed facility id." % [settlement_id, label])
			elif not facility_ids.has(facility_id):
				errors.append("World settlement %s in %s references missing guaranteed facility %s." % [settlement_id, label, facility_id])
		_validate_optional_facility_pool(settlement_variant.get("optional_facility_pool"), facility_ids, settlement_id, label, errors)
	return ids


func _validate_facility_slots(slots_variant: Variant, settlement_id: String, label: String, errors: Array[String]) -> void:
	var slot_ids: Dictionary = {}
	for slot_variant in _as_array(slots_variant):
		if slot_variant == null or slot_variant.get_script() != FACILITY_SLOT_CONFIG_SCRIPT:
			errors.append("World settlement %s in %s has non-FacilitySlotConfig slot." % [settlement_id, label])
			continue
		var slot_id := String(slot_variant.get("slot_id")).strip_edges()
		if slot_id.is_empty():
			errors.append("World settlement %s in %s has slot missing slot_id." % [settlement_id, label])
			continue
		if slot_ids.has(slot_id):
			errors.append("World settlement %s in %s has duplicate slot_id %s." % [settlement_id, label, slot_id])
		slot_ids[slot_id] = true
		if String(slot_variant.get("slot_tag")).strip_edges().is_empty():
			errors.append("World settlement %s slot %s in %s is missing slot_tag." % [settlement_id, slot_id, label])


func _validate_optional_facility_pool(
	pool_variant: Variant,
	facility_ids: Dictionary,
	settlement_id: String,
	label: String,
	errors: Array[String]
) -> void:
	for entry_variant in _as_array(pool_variant):
		if entry_variant == null or entry_variant.get_script() != WEIGHTED_FACILITY_ENTRY_SCRIPT:
			errors.append("World settlement %s in %s has non-WeightedFacilityEntry optional facility." % [settlement_id, label])
			continue
		var facility_id := String(entry_variant.call("get_facility_template_id")).strip_edges()
		if facility_id.is_empty():
			errors.append("World settlement %s in %s has optional facility missing facility_id." % [settlement_id, label])
		elif not facility_ids.has(facility_id):
			errors.append("World settlement %s in %s references missing optional facility %s." % [settlement_id, label, facility_id])
		var weight_variant: Variant = entry_variant.get("weight")
		if weight_variant is not int:
			errors.append("World settlement %s in %s has optional facility %s with non-integer weight." % [settlement_id, label, facility_id])
		elif int(weight_variant) <= 0:
			errors.append("World settlement %s in %s has optional facility %s with non-positive weight." % [settlement_id, label, facility_id])


func _validate_settlement_distribution(
	distribution_variant: Variant,
	settlement_ids: Dictionary,
	label: String,
	errors: Array[String]
) -> void:
	for rule_variant in _as_array(distribution_variant):
		if rule_variant == null or rule_variant.get_script() != SETTLEMENT_DISTRIBUTION_RULE_SCRIPT:
			errors.append("World generation config %s has non-SettlementDistributionRule entry." % label)
			continue
		var settlement_id := String(rule_variant.call("get_settlement_template_id")).strip_edges()
		if settlement_id.is_empty():
			errors.append("World generation config %s has distribution rule missing settlement_id." % label)
		elif not settlement_ids.has(settlement_id):
			errors.append("World generation config %s settlement distribution references missing settlement %s." % [label, settlement_id])
		if String(rule_variant.get("faction_id")).strip_edges().is_empty():
			errors.append("World generation config %s settlement distribution for %s is missing faction_id." % [label, settlement_id])


func _validate_wild_spawn_rules(
	rule_resources: Array,
	enemy_templates: Dictionary,
	wild_encounter_rosters: Dictionary,
	label: String,
	errors: Array[String]
) -> void:
	for rule_variant in rule_resources:
		if rule_variant == null or rule_variant.get_script() != WILD_SPAWN_RULE_SCRIPT:
			errors.append("World generation config %s has non-WildSpawnRule entry." % label)
			continue
		var region_tag := String(rule_variant.get("region_tag")).strip_edges()
		var enemy_roster_template_id := String(rule_variant.get("enemy_roster_template_id")).strip_edges()
		var encounter_profile_id := String(rule_variant.get("encounter_profile_id")).strip_edges()
		if region_tag.is_empty():
			errors.append("World generation config %s has wild spawn rule missing region_tag." % label)
		if enemy_roster_template_id.is_empty() and encounter_profile_id.is_empty():
			errors.append("World generation config %s wild spawn rule %s must declare enemy_roster_template_id or encounter_profile_id." % [label, region_tag])
		if not enemy_roster_template_id.is_empty() and not enemy_templates.is_empty() and not enemy_templates.has(StringName(enemy_roster_template_id)):
			errors.append("World generation config %s wild spawn rule %s references missing enemy roster template %s." % [label, region_tag, enemy_roster_template_id])
		if not encounter_profile_id.is_empty() and not wild_encounter_rosters.is_empty() and not wild_encounter_rosters.has(StringName(encounter_profile_id)):
			errors.append("World generation config %s wild spawn rule %s references missing encounter profile %s." % [label, region_tag, encounter_profile_id])
		if int(rule_variant.get("density_per_chunk")) <= 0:
			errors.append("World generation config %s wild spawn rule %s has non-positive density_per_chunk." % [label, region_tag])
		if int(rule_variant.get("vision_range")) < 0:
			errors.append("World generation config %s wild spawn rule %s has negative vision_range." % [label, region_tag])


func _validate_name_pool(resource_path: String, label: String, errors: Array[String]) -> void:
	var name_pool = _load_resource(resource_path, WORLD_MAP_SETTLEMENT_NAME_POOL_SCRIPT, label, errors)
	if name_pool == null:
		return
	var names: Array[String] = name_pool.call("build_unique_display_names")
	if names.is_empty():
		errors.append("World generation config %s has empty settlement name pool %s." % [label, resource_path])


func _load_resource(resource_path: String, expected_script: Script, label: String, errors: Array[String]):
	var resource = load(resource_path)
	if resource == null:
		errors.append("World generation config %s failed to load %s." % [label, resource_path])
		return null
	if resource.get_script() != expected_script:
		errors.append("World generation config %s expected %s to use %s." % [label, resource_path, expected_script.resource_path])
		return null
	return resource


func _as_array(value: Variant) -> Array:
	return value if value is Array else []
