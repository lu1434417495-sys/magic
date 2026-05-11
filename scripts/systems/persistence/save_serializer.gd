class_name SaveSerializer
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const SAVE_DIRECTORY := "user://saves"
const WORLD_MAP_SEED_KEY := "map_seed"
const WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY := "next_equipment_instance_serial"
const SAVE_PAYLOAD_REQUIRED_KEYS := [
	"version",
	"save_id",
	"generation_config_path",
	"world_state",
	"party_state",
	"meta",
	"save_slot_meta",
]
const WORLD_STATE_REQUIRED_KEYS := [
	"world_data",
	"player_coord",
	"player_faction_id",
]
const SAVE_PAYLOAD_META_REQUIRED_KEYS := [
	"saved_at_unix_time",
	"save_format",
]
const SAVE_META_REQUIRED_KEYS := [
	"save_id",
	"display_name",
	"world_preset_id",
	"world_preset_name",
	"generation_config_path",
	"world_size_cells",
	"created_at_unix_time",
	"updated_at_unix_time",
]
const SAVE_INDEX_REQUIRED_KEYS := [
	"save_id",
	"display_name",
	"world_preset_id",
	"world_preset_name",
	"generation_config_path",
	"world_size_cells",
	"created_at_unix_time",
	"updated_at_unix_time",
]
const MOUNTED_SUBMAP_REQUIRED_KEYS := [
	"submap_id",
	"display_name",
	"generation_config_path",
	"return_hint_text",
	"is_generated",
	"player_coord",
	"world_data",
]
const WORLD_DATA_REQUIRED_KEYS := [
	"map_seed",
	"world_step",
	"next_equipment_instance_serial",
	"active_submap_id",
	"submap_return_stack",
	"settlements",
	"world_events",
	"encounter_anchors",
	"mounted_submaps",
]
const WORLD_DATA_OPTIONAL_KEYS := [
	"world_npcs",
	"player_start_coord",
	"player_start_settlement_id",
	"player_start_settlement_name",
	"fog_states",
]
const WORLDMAP_NPC_REQUIRED_KEYS := [
	"entity_id",
	"display_name",
	"coord",
	"kind",
	"faction_id",
	"vision_range",
]
const WORLD_EVENT_REQUIRED_KEYS := [
	"event_id",
	"display_name",
	"world_coord",
	"event_type",
	"target_submap_id",
	"discovery_condition_id",
	"prompt_title",
	"prompt_text",
	"is_discovered",
]
const SUBMAP_RETURN_STACK_ENTRY_REQUIRED_KEYS := [
	"map_id",
	"coord",
]
const SETTLEMENT_REQUIRED_KEYS := [
	"entity_id",
	"template_id",
	"settlement_id",
	"display_name",
	"tier",
	"tier_name",
	"faction_id",
	"origin",
	"footprint_size",
	"facilities",
	"service_npcs",
	"available_services",
	"is_player_start",
	"settlement_state",
]
const SETTLEMENT_FACILITY_REQUIRED_KEYS := [
	"template_id",
	"facility_id",
	"display_name",
	"category",
	"interaction_type",
	"slot_id",
	"slot_tag",
	"local_coord",
	"world_coord",
	"settlement_id",
	"service_npcs",
]
const SETTLEMENT_SERVICE_NPC_REQUIRED_KEYS := [
	"template_id",
	"npc_id",
	"display_name",
	"service_type",
	"interaction_script_id",
	"local_slot_id",
	"facility_id",
	"facility_template_id",
	"facility_name",
	"settlement_id",
]
const SETTLEMENT_SERVICE_REQUIRED_KEYS := [
	"settlement_id",
	"facility_id",
	"facility_template_id",
	"facility_name",
	"npc_id",
	"npc_template_id",
	"npc_name",
	"service_type",
	"action_id",
	"interaction_script_id",
]
const SETTLEMENT_STATE_REQUIRED_KEYS := [
	"visited",
	"reputation",
	"active_conditions",
	"cooldowns",
	"shop_inventory_seed",
	"shop_last_refresh_step",
	"shop_states",
]
const SETTLEMENT_STATE_OPTIONAL_KEYS := [
	"world_step",
	"shop_feedback_text",
]
const SHOP_STATE_REQUIRED_KEYS := [
	"shop_id",
	"current_inventory",
	"seed",
	"last_refresh_step",
]
const SHOP_INVENTORY_ENTRY_REQUIRED_KEYS := [
	"item_id",
	"quantity",
	"unit_price",
	"sold_out",
]

var _progression_serialization = null
var _world_preset_registry = null
var _party_state_script = null
var _encounter_anchor_script = null
var _save_version := 7
var _save_index_version := 3
var _max_active_member_count := 4


func setup(
	progression_serialization,
	world_preset_registry,
	party_state_script,
	encounter_anchor_script,
	save_version: int = 7,
	save_index_version: int = 3,
	max_active_member_count: int = 4
) -> void:
	_progression_serialization = progression_serialization
	_world_preset_registry = world_preset_registry
	_party_state_script = party_state_script
	_encounter_anchor_script = encounter_anchor_script
	_save_version = save_version
	_save_index_version = save_index_version
	_max_active_member_count = max_active_member_count


func dispose() -> void:
	_progression_serialization = null
	_world_preset_registry = null
	_party_state_script = null
	_encounter_anchor_script = null


func build_save_payload(
	active_save_id: String,
	generation_config_path: String,
	active_save_meta: Dictionary,
	world_data: Dictionary,
	player_coord: Vector2i,
	player_faction_id: String,
	party_state,
	saved_at_unix_time: int
) -> Dictionary:
	var payload := {
		"version": _save_version,
		"save_id": active_save_id,
		"generation_config_path": generation_config_path,
		"world_state": _build_world_state_payload(world_data, player_coord, player_faction_id),
		"party_state": _serialize_party_state(party_state),
		"meta": _build_meta_payload(saved_at_unix_time),
		"save_slot_meta": active_save_meta.duplicate(true),
	}
	return minimize_save_payload_strings(payload)


func _build_world_state_payload(world_data: Dictionary, player_coord: Vector2i, player_faction_id: String) -> Dictionary:
	return {
		"world_data": serialize_world_data(world_data),
		"player_coord": player_coord,
		"player_faction_id": player_faction_id,
	}


func _build_meta_payload(saved_at_unix_time: int) -> Dictionary:
	return {
		"saved_at_unix_time": saved_at_unix_time,
		"save_format": "multi_save_total_save",
	}


func decode_payload(
	payload: Dictionary,
	generation_config_path: String,
	generation_config,
	save_meta: Dictionary
) -> Dictionary:
	var normalized_requested_meta := normalize_save_meta(save_meta)
	if normalized_requested_meta.is_empty():
		return {"error": ERR_INVALID_DATA}
	if String(normalized_requested_meta.get("generation_config_path", "")) != generation_config_path:
		return {"error": ERR_INVALID_DATA}
	var payload_data := restore_minimized_save_payload_strings(payload)
	if not _has_exact_dictionary_keys(payload_data, SAVE_PAYLOAD_REQUIRED_KEYS):
		return {"error": ERR_INVALID_DATA}
	if not payload_data.has("version") or payload_data.get("version") is not int:
		return {"error": ERR_INVALID_DATA}
	var save_version := int(payload_data.get("version"))
	if save_version != _save_version:
		return {"error": ERR_INVALID_DATA}

	var world_state_data = payload_data.get("world_state", {})
	if typeof(world_state_data) != TYPE_DICTIONARY:
		return {"error": ERR_INVALID_DATA}
	var world_state: Dictionary = world_state_data
	if not _has_exact_dictionary_keys(world_state, WORLD_STATE_REQUIRED_KEYS):
		return {"error": ERR_INVALID_DATA}

	var payload_meta_data = payload_data.get("meta", {})
	if typeof(payload_meta_data) != TYPE_DICTIONARY:
		return {"error": ERR_INVALID_DATA}
	var payload_meta: Dictionary = payload_meta_data
	if not _has_exact_dictionary_keys(payload_meta, SAVE_PAYLOAD_META_REQUIRED_KEYS):
		return {"error": ERR_INVALID_DATA}
	if payload_meta.get("saved_at_unix_time") is not int:
		return {"error": ERR_INVALID_DATA}
	if payload_meta.get("save_format") is not String:
		return {"error": ERR_INVALID_DATA}
	if String(payload_meta.get("save_format", "")) != "multi_save_total_save":
		return {"error": ERR_INVALID_DATA}

	var world_data_raw = world_state.get("world_data", {})
	if typeof(world_data_raw) != TYPE_DICTIONARY:
		return {"error": ERR_INVALID_DATA}
	if (world_data_raw as Dictionary).is_empty():
		return {"error": ERR_INVALID_DATA}
	var world_data := normalize_world_data(world_data_raw)
	if world_data.is_empty():
		return {"error": ERR_INVALID_DATA}

	if not payload_data.has("save_id") or payload_data.get("save_id") is not String:
		return {"error": ERR_INVALID_DATA}
	var payload_save_id := String(payload_data.get("save_id", ""))
	if not is_valid_save_id_token(payload_save_id):
		return {"error": ERR_INVALID_DATA}
	if payload_save_id != String(normalized_requested_meta.get("save_id", "")):
		return {"error": ERR_INVALID_DATA}
	if not payload_data.has("generation_config_path") or payload_data.get("generation_config_path") is not String:
		return {"error": ERR_INVALID_DATA}
	var payload_generation_config_path := String(payload_data.get("generation_config_path", "")).strip_edges()
	if payload_generation_config_path.is_empty() or payload_generation_config_path != generation_config_path:
		return {"error": ERR_INVALID_DATA}

	var slot_meta_raw = payload_data.get("save_slot_meta", null)
	if typeof(slot_meta_raw) != TYPE_DICTIONARY:
		return {"error": ERR_INVALID_DATA}
	var normalized_meta := normalize_save_meta(slot_meta_raw)
	if normalized_meta.is_empty():
		return {"error": ERR_INVALID_DATA}
	if String(normalized_meta.get("save_id", "")) != payload_save_id:
		return {"error": ERR_INVALID_DATA}
	if String(normalized_meta.get("save_id", "")) != String(normalized_requested_meta.get("save_id", "")):
		return {"error": ERR_INVALID_DATA}
	if String(normalized_meta.get("generation_config_path", "")) != payload_generation_config_path:
		return {"error": ERR_INVALID_DATA}
	if String(normalized_meta.get("generation_config_path", "")) != String(normalized_requested_meta.get("generation_config_path", "")):
		return {"error": ERR_INVALID_DATA}
	var player_coord_variant: Variant = world_state.get("player_coord", null)
	if not _is_supported_vector2i_value(player_coord_variant):
		return {"error": ERR_INVALID_DATA}
	if not world_state.has("player_faction_id") or not _is_supported_string_name_value(world_state.get("player_faction_id")):
		return {"error": ERR_INVALID_DATA}
	var player_faction_id := String(ProgressionDataUtils.to_string_name(world_state.get("player_faction_id", ""))).strip_edges()
	if player_faction_id.is_empty():
		return {"error": ERR_INVALID_DATA}
	var party_state_payload: Variant = payload_data.get("party_state", null)
	if typeof(party_state_payload) != TYPE_DICTIONARY:
		return {"error": ERR_INVALID_DATA}
	var deserialized_party_state = _deserialize_party_state(party_state_payload)
	if deserialized_party_state == null:
		return {"error": ERR_INVALID_DATA}

	return {
		"error": OK,
		"active_save_id": payload_save_id,
		"active_save_meta": normalized_meta,
		"generation_config_path": generation_config_path,
		"generation_config": generation_config,
		"world_data": world_data,
		"player_coord": read_vector2i(player_coord_variant, Vector2i.ZERO),
		"player_faction_id": player_faction_id,
		"party_state": normalize_party_state(deserialized_party_state),
	}


func build_save_meta(
	save_id: String,
	display_name: String,
	generation_config_path: String,
	preset_id: StringName,
	preset_name: String,
	world_size_cells: Vector2i,
	created_at_unix_time: int,
	updated_at_unix_time: int
) -> Dictionary:
	return normalize_save_meta({
		"save_id": save_id,
		"display_name": display_name if not display_name.is_empty() else save_id,
		"world_preset_id": String(preset_id),
		"world_preset_name": preset_name,
		"generation_config_path": generation_config_path,
		"world_size_cells": world_size_cells,
		"created_at_unix_time": created_at_unix_time,
		"updated_at_unix_time": updated_at_unix_time,
	})


func extract_save_meta_from_payload(payload: Dictionary) -> Dictionary:
	var payload_data := restore_minimized_save_payload_strings(payload)
	if payload_data.is_empty():
		return {}
	if not _has_exact_dictionary_keys(payload_data, SAVE_PAYLOAD_REQUIRED_KEYS):
		return {}

	if not payload_data.has("save_id") or payload_data.get("save_id") is not String:
		return {}
	if not payload_data.has("version") or payload_data.get("version") is not int:
		return {}
	if int(payload_data.get("version", -1)) != _save_version:
		return {}
	if not payload_data.has("generation_config_path") or payload_data.get("generation_config_path") is not String:
		return {}
	var save_id := String(payload_data.get("save_id", "")).strip_edges()
	var generation_config_path := String(payload_data.get("generation_config_path", "")).strip_edges()
	if save_id.is_empty() or generation_config_path.is_empty():
		return {}

	var raw_meta_variant = payload_data.get("save_slot_meta", null)
	if typeof(raw_meta_variant) != TYPE_DICTIONARY:
		return {}
	var normalized_meta := normalize_save_meta(raw_meta_variant)
	if normalized_meta.is_empty():
		return {}
	if String(normalized_meta.get("save_id", "")).strip_edges() != save_id:
		return {}
	if String(normalized_meta.get("generation_config_path", "")).strip_edges() != generation_config_path:
		return {}
	return normalized_meta


func normalize_save_meta(raw_meta: Dictionary) -> Dictionary:
	if not _has_exact_dictionary_keys(raw_meta, SAVE_META_REQUIRED_KEYS):
		return {}
	for string_key in [
		"save_id",
		"display_name",
		"world_preset_id",
		"world_preset_name",
		"generation_config_path",
	]:
		if raw_meta.get(string_key) is not String:
			return {}
	if raw_meta.get("created_at_unix_time") is not int:
		return {}
	if raw_meta.get("updated_at_unix_time") is not int:
		return {}
	var save_id := String(raw_meta.get("save_id", ""))
	if not is_valid_save_id_token(save_id):
		return {}

	var generation_config_path := String(raw_meta.get("generation_config_path", "")).strip_edges()
	if generation_config_path.is_empty():
		return {}
	var display_name := String(raw_meta.get("display_name", "")).strip_edges()
	if display_name.is_empty():
		return {}

	var world_preset_name := String(raw_meta.get("world_preset_name", "")).strip_edges()
	if world_preset_name.is_empty():
		return {}

	var created_at := int(raw_meta.get("created_at_unix_time", 0))
	var updated_at := int(raw_meta.get("updated_at_unix_time", 0))
	if created_at <= 0 or updated_at <= 0:
		return {}
	var world_size_variant: Variant = raw_meta.get("world_size_cells", null)
	if not _is_supported_vector2i_value(world_size_variant):
		return {}
	var world_size_cells := read_vector2i(world_size_variant)
	if world_size_cells.x <= 0 or world_size_cells.y <= 0:
		return {}

	return {
		"save_id": save_id,
		"display_name": display_name,
		"world_preset_id": String(raw_meta.get("world_preset_id", "")),
		"world_preset_name": world_preset_name,
		"generation_config_path": generation_config_path,
		"world_size_cells": world_size_cells,
		"created_at_unix_time": created_at,
		"updated_at_unix_time": updated_at,
	}


func normalize_world_data(world_data: Dictionary) -> Dictionary:
	var validation_error := get_world_data_validation_error(world_data)
	if not validation_error.is_empty():
		push_error(validation_error)
		return {}
	var normalized = world_data.duplicate(true)
	normalized[WORLD_MAP_SEED_KEY] = int(world_data.get(WORLD_MAP_SEED_KEY))
	normalized["world_step"] = int(world_data["world_step"])
	normalized[WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY] = int(world_data.get(WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY))
	normalized["active_submap_id"] = String(world_data.get("active_submap_id", ""))
	normalized["submap_return_stack"] = _normalize_submap_return_stack(world_data.get("submap_return_stack", []))
	normalized["settlements"] = _normalize_settlements(world_data.get("settlements", []))
	normalized["world_events"] = _normalize_world_events(world_data.get("world_events", []))
	var encounter_anchors: Array = []
	for encounter_anchor_data in world_data.get("encounter_anchors", []):
		if encounter_anchor_data is RefCounted and _encounter_anchor_script != null and encounter_anchor_data.get_script() == _encounter_anchor_script:
			encounter_anchors.append(encounter_anchor_data)
		elif encounter_anchor_data is Dictionary:
			var encounter_anchor = _deserialize_encounter_anchor(encounter_anchor_data)
			if encounter_anchor != null:
				encounter_anchors.append(encounter_anchor)
	normalized["encounter_anchors"] = encounter_anchors
	normalized["mounted_submaps"] = _normalize_mounted_submaps(world_data.get("mounted_submaps", {}))
	return normalized


func serialize_world_data(world_data: Dictionary) -> Dictionary:
	var validation_error := get_world_data_validation_error(world_data)
	if not validation_error.is_empty():
		push_error(validation_error)
		return {}
	var serialized_world_data = world_data.duplicate(true)
	serialized_world_data["active_submap_id"] = String(world_data.get("active_submap_id", ""))
	serialized_world_data[WORLD_MAP_SEED_KEY] = int(world_data.get(WORLD_MAP_SEED_KEY))
	serialized_world_data[WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY] = int(world_data.get(WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY))
	serialized_world_data["submap_return_stack"] = _serialize_submap_return_stack(world_data.get("submap_return_stack", []))
	serialized_world_data["world_events"] = _serialize_world_events(world_data.get("world_events", []))
	var encounter_anchor_payloads: Array[Dictionary] = []
	for encounter_anchor_data in world_data.get("encounter_anchors", []):
		var encounter_anchor = encounter_anchor_data
		if encounter_anchor == null:
			continue
		var serialized_encounter_anchor := _serialize_encounter_anchor(encounter_anchor)
		if not serialized_encounter_anchor.is_empty():
			encounter_anchor_payloads.append(serialized_encounter_anchor)
	serialized_world_data["encounter_anchors"] = encounter_anchor_payloads
	serialized_world_data["mounted_submaps"] = _serialize_mounted_submaps(world_data.get("mounted_submaps", {}))
	return serialized_world_data


func get_world_data_validation_error(world_data: Dictionary) -> String:
	var seed_error := get_world_data_seed_validation_error(world_data)
	if not seed_error.is_empty():
		return seed_error
	var world_step_error := get_world_data_step_validation_error(world_data)
	if not world_step_error.is_empty():
		return world_step_error
	var equipment_serial_error := get_equipment_instance_serial_validation_error(world_data)
	if not equipment_serial_error.is_empty():
		return equipment_serial_error
	var schema_error := get_world_data_schema_validation_error(world_data)
	if not schema_error.is_empty():
		return schema_error
	var nested_schema_error := get_world_data_nested_schema_validation_error(world_data)
	if not nested_schema_error.is_empty():
		return nested_schema_error
	return get_mounted_submaps_validation_error(world_data.get("mounted_submaps", {}))


func get_world_data_schema_validation_error(world_data: Dictionary) -> String:
	if not _has_required_and_allowed_dictionary_keys(world_data, WORLD_DATA_REQUIRED_KEYS, WORLD_DATA_OPTIONAL_KEYS):
		return "Corrupt save world_data: fields must match current schema."
	if world_data.get("active_submap_id") is not String and world_data.get("active_submap_id") is not StringName:
		return "Corrupt save world_data: active_submap_id must be a String."
	for array_field in ["submap_return_stack", "settlements", "world_events", "encounter_anchors"]:
		if world_data.get(array_field) is not Array:
			return "Corrupt save world_data: %s must be an Array." % array_field
	if world_data.has("world_npcs") and world_data.get("world_npcs") is not Array:
		return "Corrupt save world_data: world_npcs must be an Array."
	if world_data.get("mounted_submaps") is not Dictionary:
		return "Corrupt save world_data: mounted_submaps must be a Dictionary."
	if world_data.has("player_start_coord") and not _is_supported_vector2i_value(world_data.get("player_start_coord")):
		return "Corrupt save world_data: player_start_coord must be a Vector2i payload."
	for optional_string_field in ["player_start_settlement_id", "player_start_settlement_name"]:
		if world_data.has(optional_string_field) and world_data.get(optional_string_field) is not String and world_data.get(optional_string_field) is not StringName:
			return "Corrupt save world_data: %s must be a String." % optional_string_field
	if world_data.has("fog_states") and world_data.get("fog_states") is not Dictionary:
		return "Corrupt save world_data: fog_states must be a Dictionary."
	return ""


func get_world_data_nested_schema_validation_error(world_data: Dictionary) -> String:
	var return_stack_error := _get_submap_return_stack_validation_error(world_data.get("submap_return_stack", []))
	if not return_stack_error.is_empty():
		return return_stack_error
	var settlement_error := _get_settlements_validation_error(world_data.get("settlements", []))
	if not settlement_error.is_empty():
		return settlement_error
	var event_error := _get_world_events_validation_error(world_data.get("world_events", []))
	if not event_error.is_empty():
		return event_error
	var npc_error := _get_world_npcs_validation_error(world_data.get("world_npcs", []))
	if not npc_error.is_empty():
		return npc_error
	return _get_encounter_anchors_validation_error(world_data.get("encounter_anchors", []))


func get_world_data_seed_validation_error(world_data: Dictionary) -> String:
	if not world_data.has(WORLD_MAP_SEED_KEY):
		return "Corrupt save world_data: missing required field '%s'." % WORLD_MAP_SEED_KEY
	if world_data.get(WORLD_MAP_SEED_KEY) is not int:
		return "Corrupt save world_data: %s must be an int, got %s." % [
			WORLD_MAP_SEED_KEY,
			type_string(typeof(world_data.get(WORLD_MAP_SEED_KEY))),
		]
	var seed := int(world_data.get(WORLD_MAP_SEED_KEY, 0))
	if seed < 1:
		return "Corrupt save world_data: %s must be >= 1, got %s." % [
			WORLD_MAP_SEED_KEY,
			str(world_data.get(WORLD_MAP_SEED_KEY)),
		]
	return ""


func get_world_data_step_validation_error(world_data: Dictionary) -> String:
	if not world_data.has("world_step"):
		return "Corrupt save world_data: missing required field 'world_step'."
	if world_data["world_step"] is not int:
		return "Corrupt save world_data: world_step must be an int, got %s." % typeof(world_data["world_step"])
	var world_step := int(world_data["world_step"])
	if world_step < 0:
		return "Corrupt save world_data: world_step must be >= 0, got %s." % str(world_data["world_step"])
	return ""


func get_equipment_instance_serial_validation_error(world_data: Dictionary) -> String:
	if not world_data.has(WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY):
		return "Corrupt save world_data: missing required field '%s'." % WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY
	if world_data.get(WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY) is not int:
		return "Corrupt save world_data: %s must be an int, got %s." % [
			WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY,
			type_string(typeof(world_data.get(WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY))),
		]
	var serial := int(world_data.get(WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY, 0))
	if serial < 1:
		return "Corrupt save world_data: %s must be >= 1, got %s." % [
			WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY,
			str(world_data.get(WORLD_EQUIPMENT_INSTANCE_SERIAL_KEY)),
		]
	return ""


func get_mounted_submap_world_data_validation_error(
	submap_id: String,
	is_generated: bool,
	world_data_variant: Variant,
	has_world_data: bool = true
) -> String:
	if not has_world_data:
		return _format_mounted_submap_world_data_error(submap_id, "missing required field.")
	if world_data_variant is not Dictionary:
		return _format_mounted_submap_world_data_error(submap_id, "expected Dictionary.")

	var world_data: Dictionary = world_data_variant
	if not is_generated:
		if not world_data.is_empty():
			return _format_mounted_submap_world_data_error(submap_id, "ungenerated submap requires empty world_data.")
		return ""

	if world_data.is_empty():
		return _format_mounted_submap_world_data_error(submap_id, "generated submap requires complete world_data.")

	var validation_error := get_world_data_validation_error(world_data)
	if validation_error.is_empty():
		return ""
	return _format_mounted_submap_world_data_error(submap_id, validation_error)


func get_mounted_submaps_validation_error(submaps_variant: Variant) -> String:
	if submaps_variant is not Dictionary:
		return "Corrupt save world_data: mounted_submaps must be a Dictionary."
	var submaps: Dictionary = submaps_variant
	for submap_key in submaps.keys():
		var entry_variant = submaps.get(submap_key, {})
		if entry_variant is not Dictionary:
			return "Corrupt save mounted_submaps[%s]: expected Dictionary." % String(submap_key)
		var entry: Dictionary = entry_variant
		if not _has_exact_dictionary_keys(entry, MOUNTED_SUBMAP_REQUIRED_KEYS):
			return "Corrupt save mounted_submaps[%s]: fields must exactly match current schema." % String(submap_key)
		var submap_id := String(entry.get("submap_id", String(submap_key)))
		if submap_id.is_empty():
			return "Corrupt save mounted_submaps[%s]: submap_id is required." % String(submap_key)
		if entry.get("display_name") is not String:
			return "Corrupt save mounted_submaps[%s]: display_name must be a String." % String(submap_key)
		if entry.get("generation_config_path") is not String:
			return "Corrupt save mounted_submaps[%s]: generation_config_path must be a String." % String(submap_key)
		if entry.get("return_hint_text") is not String:
			return "Corrupt save mounted_submaps[%s]: return_hint_text must be a String." % String(submap_key)
		if entry.get("is_generated") is not bool:
			return "Corrupt save mounted_submaps[%s]: is_generated must be a bool." % String(submap_key)
		if not _is_supported_vector2i_value(entry.get("player_coord", null)):
			return "Corrupt save mounted_submaps[%s]: player_coord must be a Vector2i payload." % String(submap_key)
		var validation_error := get_mounted_submap_world_data_validation_error(
			submap_id,
			bool(entry.get("is_generated", false)),
			entry.get("world_data", null),
			entry.has("world_data")
		)
		if not validation_error.is_empty():
			return validation_error
	return ""


func _get_submap_return_stack_validation_error(stack_variant: Variant) -> String:
	if stack_variant is not Array:
		return "Corrupt save world_data.submap_return_stack: expected Array."
	var stack: Array = stack_variant
	for index in range(stack.size()):
		var entry_variant = stack[index]
		if entry_variant is not Dictionary:
			return "Corrupt save world_data.submap_return_stack[%d]: expected Dictionary." % index
		var entry: Dictionary = entry_variant
		if not _has_exact_dictionary_keys(entry, SUBMAP_RETURN_STACK_ENTRY_REQUIRED_KEYS):
			return "Corrupt save world_data.submap_return_stack[%d]: fields must exactly match current schema." % index
		if entry.get("map_id") is not String and entry.get("map_id") is not StringName:
			return "Corrupt save world_data.submap_return_stack[%d]: map_id must be a String." % index
		if not _is_supported_vector2i_value(entry.get("coord", null)):
			return "Corrupt save world_data.submap_return_stack[%d]: coord must be a Vector2i payload." % index
	return ""


func _get_world_events_validation_error(world_events_variant: Variant) -> String:
	if world_events_variant is not Array:
		return "Corrupt save world_data.world_events: expected Array."
	var world_events: Array = world_events_variant
	for index in range(world_events.size()):
		var event_variant = world_events[index]
		if event_variant is not Dictionary:
			return "Corrupt save world_data.world_events[%d]: expected Dictionary." % index
		var event_data: Dictionary = event_variant
		if not _has_exact_dictionary_keys(event_data, WORLD_EVENT_REQUIRED_KEYS):
			return "Corrupt save world_data.world_events[%d]: fields must exactly match current schema." % index
		for string_field in [
			"event_id",
			"display_name",
			"event_type",
			"target_submap_id",
			"discovery_condition_id",
			"prompt_title",
			"prompt_text",
		]:
			if event_data.get(string_field) is not String and event_data.get(string_field) is not StringName:
				return "Corrupt save world_data.world_events[%d]: %s must be a String." % [index, string_field]
		if not _is_supported_vector2i_value(event_data.get("world_coord", null)):
			return "Corrupt save world_data.world_events[%d]: world_coord must be a Vector2i payload." % index
		if event_data.get("is_discovered") is not bool:
			return "Corrupt save world_data.world_events[%d]: is_discovered must be a bool." % index
	return ""


func _get_world_npcs_validation_error(world_npcs_variant: Variant) -> String:
	if world_npcs_variant is not Array:
		return "Corrupt save world_data.world_npcs: expected Array."
	var world_npcs: Array = world_npcs_variant
	for index in range(world_npcs.size()):
		var npc_variant = world_npcs[index]
		if npc_variant is not Dictionary:
			return "Corrupt save world_data.world_npcs[%d]: expected Dictionary." % index
		var npc_data: Dictionary = npc_variant
		if not _has_exact_dictionary_keys(npc_data, WORLDMAP_NPC_REQUIRED_KEYS):
			return "Corrupt save world_data.world_npcs[%d]: fields must exactly match current schema." % index
		for string_field in ["entity_id", "display_name", "kind", "faction_id"]:
			if npc_data.get(string_field) is not String and npc_data.get(string_field) is not StringName:
				return "Corrupt save world_data.world_npcs[%d]: %s must be a String." % [index, string_field]
		if not _is_supported_vector2i_value(npc_data.get("coord", null)):
			return "Corrupt save world_data.world_npcs[%d]: coord must be a Vector2i payload." % index
		if npc_data.get("vision_range") is not int or int(npc_data.get("vision_range", 0)) < 0:
			return "Corrupt save world_data.world_npcs[%d]: vision_range must be a non-negative int." % index
	return ""


func _get_encounter_anchors_validation_error(encounter_anchors_variant: Variant) -> String:
	if encounter_anchors_variant is not Array:
		return "Corrupt save world_data.encounter_anchors: expected Array."
	var encounter_anchors: Array = encounter_anchors_variant
	for index in range(encounter_anchors.size()):
		var encounter_variant = encounter_anchors[index]
		if encounter_variant is RefCounted and _encounter_anchor_script != null and encounter_variant.get_script() == _encounter_anchor_script:
			continue
		if encounter_variant is not Dictionary:
			return "Corrupt save world_data.encounter_anchors[%d]: expected Dictionary or EncounterAnchorData." % index
		if _deserialize_encounter_anchor(encounter_variant) == null:
			return "Corrupt save world_data.encounter_anchors[%d]: fields must exactly match current schema." % index
	return ""


func _get_settlements_validation_error(settlements_variant: Variant) -> String:
	if settlements_variant is not Array:
		return "Corrupt save world_data.settlements: expected Array."
	var settlements: Array = settlements_variant
	for index in range(settlements.size()):
		var settlement_variant = settlements[index]
		if settlement_variant is not Dictionary:
			return "Corrupt save world_data.settlements[%d]: expected Dictionary." % index
		var settlement_data: Dictionary = settlement_variant
		if not _has_exact_dictionary_keys(settlement_data, SETTLEMENT_REQUIRED_KEYS):
			return "Corrupt save world_data.settlements[%d]: fields must exactly match current schema." % index
		for string_field in ["entity_id", "template_id", "settlement_id", "display_name", "tier_name", "faction_id"]:
			if settlement_data.get(string_field) is not String and settlement_data.get(string_field) is not StringName:
				return "Corrupt save world_data.settlements[%d]: %s must be a String." % [index, string_field]
		if settlement_data.get("tier") is not int or int(settlement_data.get("tier", 0)) < 0:
			return "Corrupt save world_data.settlements[%d]: tier must be a non-negative int." % index
		for coord_field in ["origin", "footprint_size"]:
			if not _is_supported_vector2i_value(settlement_data.get(coord_field, null)):
				return "Corrupt save world_data.settlements[%d]: %s must be a Vector2i payload." % [index, coord_field]
		if settlement_data.get("facilities") is not Array:
			return "Corrupt save world_data.settlements[%d]: facilities must be an Array." % index
		if settlement_data.get("service_npcs") is not Array:
			return "Corrupt save world_data.settlements[%d]: service_npcs must be an Array." % index
		if settlement_data.get("available_services") is not Array:
			return "Corrupt save world_data.settlements[%d]: available_services must be an Array." % index
		if settlement_data.get("is_player_start") is not bool:
			return "Corrupt save world_data.settlements[%d]: is_player_start must be a bool." % index
		if settlement_data.get("settlement_state") is not Dictionary:
			return "Corrupt save world_data.settlements[%d]: settlement_state must be a Dictionary." % index
		var facility_error := _get_settlement_facilities_validation_error(settlement_data.get("facilities", []), "world_data.settlements[%d].facilities" % index)
		if not facility_error.is_empty():
			return facility_error
		var npc_error := _get_settlement_service_npcs_validation_error(settlement_data.get("service_npcs", []), "world_data.settlements[%d].service_npcs" % index)
		if not npc_error.is_empty():
			return npc_error
		var service_error := _get_settlement_services_validation_error(settlement_data.get("available_services", []), "world_data.settlements[%d].available_services" % index)
		if not service_error.is_empty():
			return service_error
		var state_error := _get_settlement_state_validation_error(settlement_data.get("settlement_state", {}), "world_data.settlements[%d].settlement_state" % index)
		if not state_error.is_empty():
			return state_error
	return ""


func _get_settlement_facilities_validation_error(facilities_variant: Variant, context_path: String) -> String:
	if facilities_variant is not Array:
		return "Corrupt save %s: expected Array." % context_path
	var facilities: Array = facilities_variant
	for index in range(facilities.size()):
		var facility_variant = facilities[index]
		if facility_variant is not Dictionary:
			return "Corrupt save %s[%d]: expected Dictionary." % [context_path, index]
		var facility_data: Dictionary = facility_variant
		if not _has_exact_dictionary_keys(facility_data, SETTLEMENT_FACILITY_REQUIRED_KEYS):
			return "Corrupt save %s[%d]: fields must exactly match current schema." % [context_path, index]
		for string_field in [
			"template_id",
			"facility_id",
			"display_name",
			"category",
			"interaction_type",
			"slot_id",
			"slot_tag",
			"settlement_id",
		]:
			if facility_data.get(string_field) is not String and facility_data.get(string_field) is not StringName:
				return "Corrupt save %s[%d]: %s must be a String." % [context_path, index, string_field]
		for coord_field in ["local_coord", "world_coord"]:
			if not _is_supported_vector2i_value(facility_data.get(coord_field, null)):
				return "Corrupt save %s[%d]: %s must be a Vector2i payload." % [context_path, index, coord_field]
		var npc_error := _get_settlement_service_npcs_validation_error(facility_data.get("service_npcs", []), "%s[%d].service_npcs" % [context_path, index])
		if not npc_error.is_empty():
			return npc_error
	return ""


func _get_settlement_service_npcs_validation_error(service_npcs_variant: Variant, context_path: String) -> String:
	if service_npcs_variant is not Array:
		return "Corrupt save %s: expected Array." % context_path
	var service_npcs: Array = service_npcs_variant
	for index in range(service_npcs.size()):
		var npc_variant = service_npcs[index]
		if npc_variant is not Dictionary:
			return "Corrupt save %s[%d]: expected Dictionary." % [context_path, index]
		var npc_data: Dictionary = npc_variant
		if not _has_exact_dictionary_keys(npc_data, SETTLEMENT_SERVICE_NPC_REQUIRED_KEYS):
			return "Corrupt save %s[%d]: fields must exactly match current schema." % [context_path, index]
		for string_field in SETTLEMENT_SERVICE_NPC_REQUIRED_KEYS:
			if npc_data.get(string_field) is not String and npc_data.get(string_field) is not StringName:
				return "Corrupt save %s[%d]: %s must be a String." % [context_path, index, string_field]
	return ""


func _get_settlement_services_validation_error(services_variant: Variant, context_path: String) -> String:
	if services_variant is not Array:
		return "Corrupt save %s: expected Array." % context_path
	var services: Array = services_variant
	for index in range(services.size()):
		var service_variant = services[index]
		if service_variant is not Dictionary:
			return "Corrupt save %s[%d]: expected Dictionary." % [context_path, index]
		var service_data: Dictionary = service_variant
		if not _has_exact_dictionary_keys(service_data, SETTLEMENT_SERVICE_REQUIRED_KEYS):
			return "Corrupt save %s[%d]: fields must exactly match current schema." % [context_path, index]
		for string_field in SETTLEMENT_SERVICE_REQUIRED_KEYS:
			if service_data.get(string_field) is not String and service_data.get(string_field) is not StringName:
				return "Corrupt save %s[%d]: %s must be a String." % [context_path, index, string_field]
	return ""


func _get_settlement_state_validation_error(state_variant: Variant, context_path: String) -> String:
	if state_variant is not Dictionary:
		return "Corrupt save %s: expected Dictionary." % context_path
	var state_data: Dictionary = state_variant
	if not _has_required_and_allowed_dictionary_keys(state_data, SETTLEMENT_STATE_REQUIRED_KEYS, SETTLEMENT_STATE_OPTIONAL_KEYS):
		return "Corrupt save %s: fields must match current schema." % context_path
	if state_data.get("visited") is not bool:
		return "Corrupt save %s: visited must be a bool." % context_path
	if state_data.get("reputation") is not int:
		return "Corrupt save %s: reputation must be an int." % context_path
	if state_data.get("active_conditions") is not Array:
		return "Corrupt save %s: active_conditions must be an Array." % context_path
	if state_data.get("cooldowns") is not Dictionary:
		return "Corrupt save %s: cooldowns must be a Dictionary." % context_path
	if state_data.get("shop_inventory_seed") is not int or int(state_data.get("shop_inventory_seed", 0)) < 0:
		return "Corrupt save %s: shop_inventory_seed must be a non-negative int." % context_path
	if state_data.get("shop_last_refresh_step") is not int or int(state_data.get("shop_last_refresh_step", 0)) < 0:
		return "Corrupt save %s: shop_last_refresh_step must be a non-negative int." % context_path
	if state_data.get("shop_states") is not Dictionary:
		return "Corrupt save %s: shop_states must be a Dictionary." % context_path
	if state_data.has("world_step") and (state_data.get("world_step") is not int or int(state_data.get("world_step", 0)) < 0):
		return "Corrupt save %s: world_step must be a non-negative int." % context_path
	if state_data.has("shop_feedback_text") and state_data.get("shop_feedback_text") is not String:
		return "Corrupt save %s: shop_feedback_text must be a String." % context_path
	for value in state_data.get("active_conditions", []):
		if value is not String and value is not StringName:
			return "Corrupt save %s: active_conditions values must be Strings." % context_path
	var cooldown_error := _get_int_dictionary_validation_error(state_data.get("cooldowns", {}), "%s.cooldowns" % context_path)
	if not cooldown_error.is_empty():
		return cooldown_error
	return _get_shop_states_validation_error(state_data.get("shop_states", {}), "%s.shop_states" % context_path)


func _get_shop_states_validation_error(shop_states_variant: Variant, context_path: String) -> String:
	if shop_states_variant is not Dictionary:
		return "Corrupt save %s: expected Dictionary." % context_path
	var shop_states: Dictionary = shop_states_variant
	for shop_key in shop_states.keys():
		var shop_id := String(shop_key)
		var state_variant = shop_states.get(shop_key, {})
		if state_variant is not Dictionary:
			return "Corrupt save %s[%s]: expected Dictionary." % [context_path, shop_id]
		var shop_state: Dictionary = state_variant
		if not _has_exact_dictionary_keys(shop_state, SHOP_STATE_REQUIRED_KEYS):
			return "Corrupt save %s[%s]: fields must exactly match current schema." % [context_path, shop_id]
		if shop_state.get("shop_id") is not String and shop_state.get("shop_id") is not StringName:
			return "Corrupt save %s[%s]: shop_id must be a String." % [context_path, shop_id]
		if shop_state.get("current_inventory") is not Array:
			return "Corrupt save %s[%s]: current_inventory must be an Array." % [context_path, shop_id]
		if shop_state.get("seed") is not int or int(shop_state.get("seed", 0)) < 0:
			return "Corrupt save %s[%s]: seed must be a non-negative int." % [context_path, shop_id]
		if shop_state.get("last_refresh_step") is not int or int(shop_state.get("last_refresh_step", 0)) < 0:
			return "Corrupt save %s[%s]: last_refresh_step must be a non-negative int." % [context_path, shop_id]
		var inventory_error := _get_shop_inventory_validation_error(shop_state.get("current_inventory", []), "%s[%s].current_inventory" % [context_path, shop_id])
		if not inventory_error.is_empty():
			return inventory_error
	return ""


func _get_shop_inventory_validation_error(inventory_variant: Variant, context_path: String) -> String:
	if inventory_variant is not Array:
		return "Corrupt save %s: expected Array." % context_path
	var inventory: Array = inventory_variant
	for index in range(inventory.size()):
		var entry_variant = inventory[index]
		if entry_variant is not Dictionary:
			return "Corrupt save %s[%d]: expected Dictionary." % [context_path, index]
		var entry_data: Dictionary = entry_variant
		if not _has_exact_dictionary_keys(entry_data, SHOP_INVENTORY_ENTRY_REQUIRED_KEYS):
			return "Corrupt save %s[%d]: fields must exactly match current schema." % [context_path, index]
		if entry_data.get("item_id") is not String and entry_data.get("item_id") is not StringName:
			return "Corrupt save %s[%d]: item_id must be a String." % [context_path, index]
		if entry_data.get("quantity") is not int or int(entry_data.get("quantity", 0)) < 0:
			return "Corrupt save %s[%d]: quantity must be a non-negative int." % [context_path, index]
		if entry_data.get("unit_price") is not int or int(entry_data.get("unit_price", 0)) < 0:
			return "Corrupt save %s[%d]: unit_price must be a non-negative int." % [context_path, index]
		if entry_data.get("sold_out") is not bool:
			return "Corrupt save %s[%d]: sold_out must be a bool." % [context_path, index]
	return ""


func _get_int_dictionary_validation_error(values_variant: Variant, context_path: String) -> String:
	if values_variant is not Dictionary:
		return "Corrupt save %s: expected Dictionary." % context_path
	var values: Dictionary = values_variant
	for key in values.keys():
		var key_type := typeof(key)
		if key_type != TYPE_STRING and key_type != TYPE_STRING_NAME:
			return "Corrupt save %s: keys must be Strings." % context_path
		if values.get(key) is not int:
			return "Corrupt save %s[%s]: value must be an int." % [context_path, String(key)]
	return ""


func normalize_party_state(party_state):
	if party_state == null:
		return _new_party_state()

	var normalized = _deserialize_party_state(_serialize_party_state(party_state))
	if normalized == null:
		return _new_party_state()
	var living_member_ids: Array[StringName] = []
	for key in ProgressionDataUtils.sorted_string_keys(normalized.member_states):
		var member_id := StringName(key)
		var member_state = normalized.get_member_state(member_id)
		if member_state == null or bool(member_state.is_dead):
			continue
		living_member_ids.append(member_id)

	var seen_ids: Dictionary = {}
	var active_member_ids: Array[StringName] = []
	for member_id in normalized.active_member_ids:
		if member_id == &"" or seen_ids.has(member_id):
			continue
		var member_state = normalized.get_member_state(member_id)
		if member_state == null or bool(member_state.is_dead):
			continue
		if active_member_ids.size() >= _max_active_member_count:
			continue
		seen_ids[member_id] = true
		active_member_ids.append(member_id)

	var reserve_member_ids: Array[StringName] = []
	for member_id in normalized.reserve_member_ids:
		if member_id == &"" or seen_ids.has(member_id):
			continue
		var member_state = normalized.get_member_state(member_id)
		if member_state == null or bool(member_state.is_dead):
			continue
		seen_ids[member_id] = true
		reserve_member_ids.append(member_id)

	for member_id in living_member_ids:
		if seen_ids.has(member_id):
			continue
		if active_member_ids.size() < _max_active_member_count:
			active_member_ids.append(member_id)
		else:
			reserve_member_ids.append(member_id)
		seen_ids[member_id] = true

	var main_character_member_id: StringName = normalized.main_character_member_id
	if main_character_member_id != &"" and normalized.get_member_state(main_character_member_id) != null:
		var main_character_dead: bool = normalized.is_member_dead(main_character_member_id)
		if not main_character_dead:
			reserve_member_ids.erase(main_character_member_id)
			if not active_member_ids.has(main_character_member_id):
				if active_member_ids.size() >= _max_active_member_count and not active_member_ids.is_empty():
					var demoted_member_id: StringName = active_member_ids.pop_back()
					if demoted_member_id != &"" and demoted_member_id != main_character_member_id and not reserve_member_ids.has(demoted_member_id):
						reserve_member_ids.append(demoted_member_id)
				active_member_ids.append(main_character_member_id)
	if active_member_ids.is_empty() and not living_member_ids.is_empty():
		active_member_ids.append(living_member_ids[0])
	if normalized.leader_member_id == &"" or not active_member_ids.has(normalized.leader_member_id):
		normalized.leader_member_id = active_member_ids[0] if not active_member_ids.is_empty() else &""

	normalized.active_member_ids = ProgressionDataUtils.to_string_name_array(active_member_ids)
	normalized.reserve_member_ids = ProgressionDataUtils.to_string_name_array(reserve_member_ids)
	return normalized


func serialize_save_index_entries(entries: Array[Dictionary]) -> Array[Dictionary]:
	var serialized_entries: Array[Dictionary] = []
	for entry in entries:
		var normalized_entry := normalize_save_meta(entry)
		if normalized_entry.is_empty():
			continue
		serialized_entries.append(normalized_entry.duplicate(true))
	return serialized_entries


func deserialize_save_index_entry(raw_entry: Dictionary) -> Dictionary:
	if raw_entry.is_empty():
		return {}
	for required_key in SAVE_INDEX_REQUIRED_KEYS:
		if not raw_entry.has(required_key):
			return {}
	return normalize_save_meta(raw_entry)


func normalize_save_index_entries(raw_entries: Array) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for raw_entry in raw_entries:
		if typeof(raw_entry) != TYPE_DICTIONARY:
			continue
		var entry := normalize_save_meta(deserialize_save_index_entry(raw_entry))
		if entry.is_empty():
			continue
		var save_path := _build_save_file_path(String(entry.get("save_id", "")))
		if save_path.is_empty() or not FileAccess.file_exists(save_path):
			continue
		entries.append(entry)
	entries.sort_custom(sort_save_meta_newest_first)
	return entries


func merge_save_index_entries(primary_entries: Array[Dictionary], fallback_entries: Array[Dictionary]) -> Array[Dictionary]:
	var merged_entries: Array[Dictionary] = primary_entries.duplicate(true)
	for fallback_entry in fallback_entries:
		merged_entries = upsert_save_meta(merged_entries, fallback_entry)
	return merged_entries


func upsert_save_meta(entries: Array[Dictionary], save_meta: Dictionary) -> Array[Dictionary]:
	var normalized_meta := normalize_save_meta(save_meta)
	if normalized_meta.is_empty():
		return entries

	var updated_entries: Array[Dictionary] = []
	var replaced := false
	for entry in entries:
		if String(entry.get("save_id", "")) == String(normalized_meta.get("save_id", "")):
			updated_entries.append(normalized_meta)
			replaced = true
		else:
			var normalized_existing_entry := normalize_save_meta(entry)
			if not normalized_existing_entry.is_empty():
				updated_entries.append(normalized_existing_entry)

	if not replaced:
		updated_entries.append(normalized_meta)

	updated_entries.sort_custom(sort_save_meta_newest_first)
	return updated_entries


func build_save_index_payload(entries: Array[Dictionary]) -> Dictionary:
	return minimize_save_payload_strings({
		"version": _save_index_version,
		"saves": serialize_save_index_entries(entries),
	})


func read_save_index_payload(index_file: FileAccess) -> Variant:
	if index_file == null:
		return null
	var file_length := int(index_file.get_length())
	if file_length <= 0:
		return {}
	var raw_bytes := index_file.get_buffer(file_length)
	if raw_bytes.is_empty():
		return {}
	if raw_bytes.size() < 8:
		return null
	if is_text_save_index_buffer(raw_bytes):
		return null
	index_file.seek(0)
	var raw_payload = index_file.get_var(false)
	if raw_payload is not Dictionary:
		return null
	return restore_minimized_save_payload_strings(raw_payload)


func minimize_save_payload_strings(payload: Dictionary) -> Dictionary:
	var minimized_payload = _minimize_save_payload_value(payload)
	if minimized_payload is not Dictionary:
		return {}
	return minimized_payload


func restore_minimized_save_payload_strings(payload: Dictionary) -> Dictionary:
	var restored_payload = _restore_minimized_save_payload_value(payload)
	if restored_payload is not Dictionary:
		return {}
	return restored_payload


func _minimize_save_payload_value(value: Variant) -> Variant:
	if value is Dictionary:
		return _minimize_save_payload_dictionary(value)
	if value is Array:
		return _minimize_save_payload_array(value)
	if _is_supported_string_name_value(value):
		return ProgressionDataUtils.to_string_name(value)
	return value


func _minimize_save_payload_dictionary(values: Dictionary) -> Dictionary:
	var minimized_values: Dictionary = {}
	for raw_key in values.keys():
		var minimized_key = raw_key
		if _is_supported_string_name_value(raw_key):
			minimized_key = ProgressionDataUtils.to_string_name(raw_key)
		minimized_values[minimized_key] = _minimize_save_payload_value(values[raw_key])
	return minimized_values


func _minimize_save_payload_array(values: Array) -> Array:
	var minimized_values: Array = []
	for item in values:
		minimized_values.append(_minimize_save_payload_value(item))
	return minimized_values


func _restore_minimized_save_payload_value(value: Variant) -> Variant:
	if value is Dictionary:
		return _restore_minimized_save_payload_dictionary(value)
	if value is Array:
		return _restore_minimized_save_payload_array(value)
	if typeof(value) == TYPE_STRING_NAME:
		return String(value)
	return value


func _restore_minimized_save_payload_dictionary(values: Dictionary) -> Dictionary:
	var restored_values: Dictionary = {}
	for raw_key in values.keys():
		var restored_key = raw_key
		if typeof(raw_key) == TYPE_STRING_NAME:
			restored_key = String(raw_key)
		restored_values[restored_key] = _restore_minimized_save_payload_value(values[raw_key])
	return restored_values


func _restore_minimized_save_payload_array(values: Array) -> Array:
	var restored_values: Array = []
	for item in values:
		restored_values.append(_restore_minimized_save_payload_value(item))
	return restored_values


func _is_supported_string_name_value(value: Variant) -> bool:
	var value_type := typeof(value)
	return value_type == TYPE_STRING or value_type == TYPE_STRING_NAME


func is_text_save_index_buffer(raw_bytes: PackedByteArray) -> bool:
	var saw_content := false
	var all_printable_text := true
	for byte_value in raw_bytes:
		var byte_int := int(byte_value)
		if byte_int == 9 or byte_int == 10 or byte_int == 13 or byte_int == 32:
			continue
		if not saw_content:
			if byte_int == 123 or byte_int == 91:
				return true
			saw_content = true
		if byte_int < 32 or byte_int > 126:
			all_printable_text = false
	return saw_content and all_printable_text


func read_vector2i(value: Variant, fallback: Vector2i = Vector2i.ZERO) -> Vector2i:
	if value is Vector2i:
		return value
	if value is Dictionary:
		var vector_dict := value as Dictionary
		return Vector2i(int(vector_dict.get("x", fallback.x)), int(vector_dict.get("y", fallback.y)))
	return fallback


func _is_supported_vector2i_value(value: Variant) -> bool:
	if value is Vector2i:
		return true
	if value is Dictionary:
		var vector_dict := value as Dictionary
		return vector_dict.has("x") \
			and vector_dict.has("y") \
			and vector_dict.get("x") is int \
			and vector_dict.get("y") is int
	return false


func is_save_index_integer_value(value: Variant) -> bool:
	return value is int


func sort_save_meta_newest_first(a: Dictionary, b: Dictionary) -> bool:
	var updated_a := int(a.get("updated_at_unix_time", 0))
	var updated_b := int(b.get("updated_at_unix_time", 0))
	if updated_a == updated_b:
		var created_a := int(a.get("created_at_unix_time", 0))
		var created_b := int(b.get("created_at_unix_time", 0))
		if created_a == created_b:
			return String(a.get("save_id", "")) > String(b.get("save_id", ""))
		return created_a > created_b
	return updated_a > updated_b


func _build_save_file_path(save_id: String) -> String:
	if not is_valid_save_id_token(save_id):
		return ""
	return "%s/%s.dat" % [SAVE_DIRECTORY, save_id]


func is_valid_save_id_token(save_id: String) -> bool:
	if save_id.is_empty():
		return false
	if save_id != save_id.strip_edges():
		return false
	if save_id.contains("/") or save_id.contains("\\"):
		return false
	if save_id == "." or save_id == ".." or save_id.contains(".."):
		return false
	return true


func _has_exact_dictionary_keys(data: Dictionary, required_keys: Array) -> bool:
	if data.size() != required_keys.size():
		return false
	for required_key in required_keys:
		if not data.has(required_key):
			return false
	return true


func _has_required_and_allowed_dictionary_keys(data: Dictionary, required_keys: Array, optional_keys: Array = []) -> bool:
	for required_key in required_keys:
		if not data.has(required_key):
			return false
	var allowed_keys: Dictionary = {}
	for required_key in required_keys:
		allowed_keys[String(required_key)] = true
	for optional_key in optional_keys:
		allowed_keys[String(optional_key)] = true
	for raw_key in data.keys():
		var key_type := typeof(raw_key)
		if key_type != TYPE_STRING and key_type != TYPE_STRING_NAME:
			return false
		if not allowed_keys.has(String(raw_key)):
			return false
	return true


func _get_generation_player_start_coord(generation_config) -> Vector2i:
	if generation_config == null:
		return Vector2i.ZERO
	return generation_config.player_start_coord


func _serialize_party_state(party_state) -> Dictionary:
	if party_state == null:
		return {}
	if _progression_serialization != null and _progression_serialization.has_method("serialize_party_state"):
		return _progression_serialization.serialize_party_state(party_state)
	if party_state is Object and party_state.has_method("to_dict"):
		return party_state.to_dict()
	return {}


func _deserialize_party_state(data: Dictionary):
	if _progression_serialization != null and _progression_serialization.has_method("deserialize_party_state"):
		return _progression_serialization.deserialize_party_state(data)
	if _party_state_script != null:
		return _party_state_script.from_dict(data)
	return null


func _serialize_encounter_anchor(encounter_anchor) -> Dictionary:
	if encounter_anchor == null:
		return {}
	if _progression_serialization != null and _progression_serialization.has_method("serialize_encounter_anchor"):
		return _progression_serialization.serialize_encounter_anchor(encounter_anchor)
	if encounter_anchor is Object and encounter_anchor.has_method("to_dict"):
		return encounter_anchor.to_dict()
	return {}


func _deserialize_encounter_anchor(data: Dictionary):
	if _progression_serialization != null and _progression_serialization.has_method("deserialize_encounter_anchor"):
		return _progression_serialization.deserialize_encounter_anchor(data)
	if _encounter_anchor_script != null:
		return _encounter_anchor_script.from_dict(data)
	return null


func _new_party_state():
	if _party_state_script != null:
		return _party_state_script.new()
	return null


func _normalize_world_events(world_events_variant: Variant) -> Array[Dictionary]:
	var normalized_events: Array[Dictionary] = []
	if world_events_variant is not Array:
		return normalized_events
	for event_variant in world_events_variant:
		if event_variant is not Dictionary:
			continue
		var event_data: Dictionary = event_variant.duplicate(true)
		normalized_events.append({
			"event_id": String(event_data.get("event_id", "")),
			"display_name": String(event_data.get("display_name", "")),
			"world_coord": read_vector2i(event_data.get("world_coord", Vector2i.ZERO)),
			"event_type": String(event_data.get("event_type", "")),
			"target_submap_id": String(event_data.get("target_submap_id", "")),
			"discovery_condition_id": String(event_data.get("discovery_condition_id", "")),
			"prompt_title": String(event_data.get("prompt_title", "")),
			"prompt_text": String(event_data.get("prompt_text", "")),
			"is_discovered": bool(event_data.get("is_discovered", false)),
		})
	return normalized_events


func _serialize_world_events(world_events_variant: Variant) -> Array[Dictionary]:
	var serialized_events: Array[Dictionary] = []
	if world_events_variant is not Array:
		return serialized_events
	for event_variant in world_events_variant:
		if event_variant is not Dictionary:
			continue
		var event_data: Dictionary = event_variant
		serialized_events.append({
			"event_id": String(event_data.get("event_id", "")),
			"display_name": String(event_data.get("display_name", "")),
			"world_coord": read_vector2i(event_data.get("world_coord", Vector2i.ZERO)),
			"event_type": String(event_data.get("event_type", "")),
			"target_submap_id": String(event_data.get("target_submap_id", "")),
			"discovery_condition_id": String(event_data.get("discovery_condition_id", "")),
			"prompt_title": String(event_data.get("prompt_title", "")),
			"prompt_text": String(event_data.get("prompt_text", "")),
			"is_discovered": bool(event_data.get("is_discovered", false)),
		})
	return serialized_events


func _normalize_submap_return_stack(stack_variant: Variant) -> Array[Dictionary]:
	var normalized_stack: Array[Dictionary] = []
	if stack_variant is not Array:
		return normalized_stack
	for entry_variant in stack_variant:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		normalized_stack.append({
			"map_id": String(entry.get("map_id", "")),
			"coord": read_vector2i(entry.get("coord", Vector2i.ZERO)),
		})
	return normalized_stack


func _serialize_submap_return_stack(stack_variant: Variant) -> Array[Dictionary]:
	var serialized_stack: Array[Dictionary] = []
	if stack_variant is not Array:
		return serialized_stack
	for entry_variant in stack_variant:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		serialized_stack.append({
			"map_id": String(entry.get("map_id", "")),
			"coord": read_vector2i(entry.get("coord", Vector2i.ZERO)),
		})
	return serialized_stack


func _format_mounted_submap_world_data_error(submap_id: String, message: String) -> String:
	var detail := message
	var corrupt_world_data_prefix := "Corrupt save world_data: "
	if detail.begins_with(corrupt_world_data_prefix):
		detail = detail.substr(corrupt_world_data_prefix.length())
	return "Corrupt save mounted_submaps[%s].world_data: %s" % [submap_id, detail]


func _normalize_mounted_submaps(submaps_variant: Variant) -> Dictionary:
	var normalized_submaps: Dictionary = {}
	if submaps_variant is not Dictionary:
		return normalized_submaps
	for submap_key in submaps_variant.keys():
		var entry_variant = submaps_variant.get(submap_key, {})
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant.duplicate(true)
		var submap_id := String(entry.get("submap_id", String(submap_key)))
		if submap_id.is_empty():
			continue
		var is_generated := bool(entry.get("is_generated", false))
		var validation_error := get_mounted_submap_world_data_validation_error(
			submap_id,
			is_generated,
			entry.get("world_data", null),
			entry.has("world_data")
		)
		if not validation_error.is_empty():
			push_error(validation_error)
			return {}
		var normalized_world_data: Dictionary = {}
		if is_generated:
			normalized_world_data = normalize_world_data(entry.get("world_data", {}))
		normalized_submaps[submap_id] = {
			"submap_id": submap_id,
			"display_name": String(entry.get("display_name", "")),
			"generation_config_path": String(entry.get("generation_config_path", "")),
			"return_hint_text": String(entry.get("return_hint_text", "")),
			"is_generated": is_generated,
			"player_coord": read_vector2i(entry.get("player_coord", Vector2i(-1, -1)), Vector2i(-1, -1)),
			"world_data": normalized_world_data,
		}
	return normalized_submaps


func _normalize_settlements(settlements_variant: Variant) -> Array[Dictionary]:
	var normalized_settlements: Array[Dictionary] = []
	if settlements_variant is not Array:
		return normalized_settlements
	for settlement_variant in settlements_variant:
		if settlement_variant is not Dictionary:
			continue
		var settlement_data: Dictionary = settlement_variant.duplicate(true)
		normalized_settlements.append({
			"entity_id": String(settlement_data.get("entity_id", "")),
			"template_id": String(settlement_data.get("template_id", "")),
			"settlement_id": String(settlement_data.get("settlement_id", "")),
			"display_name": String(settlement_data.get("display_name", "")),
			"tier": int(settlement_data.get("tier", 0)),
			"tier_name": String(settlement_data.get("tier_name", "")),
			"faction_id": String(settlement_data.get("faction_id", "neutral")),
			"origin": read_vector2i(settlement_data.get("origin", Vector2i.ZERO)),
			"footprint_size": read_vector2i(settlement_data.get("footprint_size", Vector2i.ONE), Vector2i.ONE),
			"facilities": _normalize_settlement_facilities(settlement_data.get("facilities", [])),
			"service_npcs": _normalize_settlement_service_npcs(settlement_data.get("service_npcs", [])),
			"available_services": _normalize_settlement_services(settlement_data.get("available_services", [])),
			"is_player_start": bool(settlement_data.get("is_player_start", false)),
			"settlement_state": _normalize_settlement_state(settlement_data.get("settlement_state", {})),
		})
	return normalized_settlements


func _normalize_settlement_facilities(facilities_variant: Variant) -> Array[Dictionary]:
	var normalized_facilities: Array[Dictionary] = []
	if facilities_variant is not Array:
		return normalized_facilities
	for facility_variant in facilities_variant:
		if facility_variant is not Dictionary:
			continue
		var facility_data: Dictionary = facility_variant.duplicate(true)
		normalized_facilities.append({
			"template_id": String(facility_data.get("template_id", "")),
			"facility_id": String(facility_data.get("facility_id", "")),
			"display_name": String(facility_data.get("display_name", "")),
			"category": String(facility_data.get("category", "")),
			"interaction_type": String(facility_data.get("interaction_type", "")),
			"slot_id": String(facility_data.get("slot_id", "")),
			"slot_tag": String(facility_data.get("slot_tag", "")),
			"local_coord": read_vector2i(facility_data.get("local_coord", Vector2i.ZERO)),
			"world_coord": read_vector2i(facility_data.get("world_coord", Vector2i.ZERO)),
			"settlement_id": String(facility_data.get("settlement_id", "")),
			"service_npcs": _normalize_settlement_service_npcs(facility_data.get("service_npcs", [])),
		})
	return normalized_facilities


func _normalize_settlement_service_npcs(service_npcs_variant: Variant) -> Array[Dictionary]:
	var normalized_service_npcs: Array[Dictionary] = []
	if service_npcs_variant is not Array:
		return normalized_service_npcs
	for npc_variant in service_npcs_variant:
		if npc_variant is not Dictionary:
			continue
		var npc_data: Dictionary = npc_variant.duplicate(true)
		normalized_service_npcs.append({
			"template_id": String(npc_data.get("template_id", "")),
			"npc_id": String(npc_data.get("npc_id", "")),
			"display_name": String(npc_data.get("display_name", "")),
			"service_type": String(npc_data.get("service_type", "")),
			"interaction_script_id": String(npc_data.get("interaction_script_id", "")),
			"local_slot_id": String(npc_data.get("local_slot_id", "")),
			"facility_id": String(npc_data.get("facility_id", "")),
			"facility_template_id": String(npc_data.get("facility_template_id", "")),
			"facility_name": String(npc_data.get("facility_name", "")),
			"settlement_id": String(npc_data.get("settlement_id", "")),
		})
	return normalized_service_npcs


func _normalize_settlement_services(services_variant: Variant) -> Array[Dictionary]:
	var normalized_services: Array[Dictionary] = []
	if services_variant is not Array:
		return normalized_services
	for service_variant in services_variant:
		if service_variant is not Dictionary:
			continue
		var service_data: Dictionary = service_variant.duplicate(true)
		normalized_services.append({
			"settlement_id": String(service_data.get("settlement_id", "")),
			"facility_id": String(service_data.get("facility_id", "")),
			"facility_template_id": String(service_data.get("facility_template_id", "")),
			"facility_name": String(service_data.get("facility_name", "")),
			"npc_id": String(service_data.get("npc_id", "")),
			"npc_template_id": String(service_data.get("npc_template_id", "")),
			"npc_name": String(service_data.get("npc_name", "")),
			"service_type": String(service_data.get("service_type", "")),
			"action_id": String(service_data.get("action_id", "")),
			"interaction_script_id": String(service_data.get("interaction_script_id", "")),
		})
	return normalized_services


func _normalize_settlement_state(state_variant: Variant) -> Dictionary:
	var state_data: Dictionary = state_variant.duplicate(true) if state_variant is Dictionary else {}
	var normalized_state := {
		"visited": bool(state_data.get("visited", false)),
		"reputation": clampi(int(state_data.get("reputation", 0)), -100, 100),
		"active_conditions": _normalize_string_array(state_data.get("active_conditions", [])),
		"cooldowns": _normalize_int_dictionary(state_data.get("cooldowns", {})),
		"shop_inventory_seed": maxi(int(state_data.get("shop_inventory_seed", 0)), 0),
		"shop_last_refresh_step": maxi(int(state_data.get("shop_last_refresh_step", 0)), 0),
		"shop_states": _normalize_shop_states(state_data.get("shop_states", {})),
	}
	if state_data.has("world_step"):
		normalized_state["world_step"] = maxi(int(state_data.get("world_step", 0)), 0)
	if state_data.has("shop_feedback_text"):
		normalized_state["shop_feedback_text"] = String(state_data.get("shop_feedback_text", ""))
	return normalized_state


func _normalize_shop_states(shop_states_variant: Variant) -> Dictionary:
	var normalized_shop_states: Dictionary = {}
	if shop_states_variant is not Dictionary:
		return normalized_shop_states
	for shop_key in shop_states_variant.keys():
		var state_variant = shop_states_variant.get(shop_key, {})
		if state_variant is not Dictionary:
			continue
		var shop_state: Dictionary = state_variant.duplicate(true)
		var shop_id := String(shop_state.get("shop_id", String(shop_key)))
		normalized_shop_states[shop_id] = {
			"shop_id": shop_id,
			"current_inventory": _normalize_shop_inventory(shop_state.get("current_inventory", [])),
			"seed": maxi(int(shop_state.get("seed", 0)), 0),
			"last_refresh_step": maxi(int(shop_state.get("last_refresh_step", 0)), 0),
		}
	return normalized_shop_states


func _normalize_shop_inventory(inventory_variant: Variant) -> Array[Dictionary]:
	var normalized_inventory: Array[Dictionary] = []
	if inventory_variant is not Array:
		return normalized_inventory
	for entry_variant in inventory_variant:
		if entry_variant is not Dictionary:
			continue
		var entry_data: Dictionary = entry_variant.duplicate(true)
		normalized_inventory.append({
			"item_id": String(entry_data.get("item_id", "")),
			"quantity": maxi(int(entry_data.get("quantity", 0)), 0),
			"unit_price": maxi(int(entry_data.get("unit_price", 0)), 0),
			"sold_out": bool(entry_data.get("sold_out", false)),
		})
	return normalized_inventory


func _normalize_string_array(values_variant: Variant) -> Array[String]:
	var normalized_values: Array[String] = []
	if values_variant is not Array:
		return normalized_values
	for value in values_variant:
		normalized_values.append(String(value))
	return normalized_values


func _normalize_int_dictionary(values_variant: Variant) -> Dictionary:
	var normalized_values: Dictionary = {}
	if values_variant is not Dictionary:
		return normalized_values
	for key in values_variant.keys():
		normalized_values[String(key)] = maxi(int(values_variant.get(key, 0)), 0)
	return normalized_values


func _serialize_mounted_submaps(submaps_variant: Variant) -> Dictionary:
	var serialized_submaps: Dictionary = {}
	if submaps_variant is not Dictionary:
		return serialized_submaps
	for submap_key in submaps_variant.keys():
		var entry_variant = submaps_variant.get(submap_key, {})
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		var submap_id := String(entry.get("submap_id", String(submap_key)))
		if submap_id.is_empty():
			continue
		var is_generated := bool(entry.get("is_generated", false))
		var validation_error := get_mounted_submap_world_data_validation_error(
			submap_id,
			is_generated,
			entry.get("world_data", null),
			entry.has("world_data")
		)
		if not validation_error.is_empty():
			push_error(validation_error)
			return {}
		var serialized_world_data: Dictionary = {}
		if is_generated:
			serialized_world_data = serialize_world_data(entry.get("world_data", {}))
		serialized_submaps[submap_id] = {
			"submap_id": submap_id,
			"display_name": String(entry.get("display_name", "")),
			"generation_config_path": String(entry.get("generation_config_path", "")),
			"return_hint_text": String(entry.get("return_hint_text", "")),
			"is_generated": is_generated,
			"player_coord": read_vector2i(entry.get("player_coord", Vector2i(-1, -1)), Vector2i(-1, -1)),
			"world_data": serialized_world_data,
		}
	return serialized_submaps
