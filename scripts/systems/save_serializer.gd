class_name SaveSerializer
extends RefCounted

const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")
const DEFAULT_PLAYER_FACTION_ID := "player"
const SAVE_DIRECTORY := "user://saves"

var _progression_serialization = null
var _world_preset_registry = null
var _party_state_script = null
var _encounter_anchor_script = null
var _save_version := 5
var _save_index_version := 1
var _max_active_member_count := 4


func setup(
	progression_serialization,
	world_preset_registry,
	party_state_script,
	encounter_anchor_script,
	save_version: int = 5,
	save_index_version: int = 1,
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
	return {
		"version": _save_version,
		"save_id": active_save_id,
		"generation_config_path": generation_config_path,
		"world_state": _build_world_state_payload(world_data, player_coord, player_faction_id),
		"party_state": _serialize_party_state(party_state),
		"meta": _build_meta_payload(saved_at_unix_time),
		"save_slot_meta": active_save_meta.duplicate(true),
	}


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


func decode_v5_payload(
	payload: Dictionary,
	generation_config_path: String,
	generation_config,
	save_meta: Dictionary
) -> Dictionary:
	var save_version := int(payload.get("version", -1))
	if save_version != _save_version:
		return {"error": ERR_INVALID_DATA}

	var world_state_data = payload.get("world_state", {})
	if typeof(world_state_data) != TYPE_DICTIONARY:
		return {"error": ERR_INVALID_DATA}
	var world_state: Dictionary = world_state_data

	var world_data_raw = world_state.get("world_data", {})
	if typeof(world_data_raw) != TYPE_DICTIONARY:
		return {"error": ERR_INVALID_DATA}
	if (world_data_raw as Dictionary).is_empty():
		return {"error": ERR_INVALID_DATA}
	var world_data := normalize_world_data(world_data_raw)
	if world_data.is_empty():
		return {"error": ERR_INVALID_DATA}

	var payload_save_id := String(payload.get("save_id", save_meta.get("save_id", "")))
	if payload_save_id.is_empty():
		return {"error": ERR_INVALID_DATA}

	var slot_meta_raw = payload.get("save_slot_meta", {})
	if typeof(slot_meta_raw) != TYPE_DICTIONARY:
		return {"error": ERR_INVALID_DATA}
	var merged_meta: Dictionary = save_meta.duplicate(true)
	for key in slot_meta_raw.keys():
		merged_meta[key] = slot_meta_raw[key]

	merged_meta["save_id"] = payload_save_id
	merged_meta["generation_config_path"] = generation_config_path
	var normalized_meta := normalize_save_meta(merged_meta)
	if normalized_meta.is_empty():
		return {"error": ERR_INVALID_DATA}
	var player_coord_variant: Variant = world_state.get("player_coord", null)
	if not _is_supported_vector2i_value(player_coord_variant):
		return {"error": ERR_INVALID_DATA}
	var party_state_payload: Variant = payload.get("party_state", {})
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
		"player_faction_id": String(world_state.get("player_faction_id", DEFAULT_PLAYER_FACTION_ID)),
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


func extract_save_meta_from_payload(payload: Dictionary, fallback_save_id: String = "") -> Dictionary:
	if payload.is_empty():
		return {}

	var raw_meta_variant = payload.get("save_slot_meta", {})
	var merged_meta: Dictionary = {}
	if typeof(raw_meta_variant) == TYPE_DICTIONARY:
		merged_meta = raw_meta_variant.duplicate(true)
	var save_id := String(payload.get("save_id", merged_meta.get("save_id", fallback_save_id))).strip_edges()
	if save_id.is_empty():
		return {}

	var generation_config_path := String(payload.get(
		"generation_config_path",
		merged_meta.get("generation_config_path", "")
	)).strip_edges()
	if generation_config_path.is_empty():
		return {}

	var recovered_meta: Dictionary = merged_meta.duplicate(true)
	recovered_meta["save_id"] = save_id
	recovered_meta["generation_config_path"] = generation_config_path
	if String(recovered_meta.get("display_name", "")).strip_edges().is_empty():
		recovered_meta["display_name"] = save_id
	if String(recovered_meta.get("world_preset_name", "")).strip_edges().is_empty():
		recovered_meta["world_preset_name"] = _get_fallback_world_preset_name(generation_config_path)

	var payload_meta_variant = payload.get("meta", {})
	if typeof(payload_meta_variant) == TYPE_DICTIONARY:
		var payload_meta: Dictionary = payload_meta_variant
		var saved_at_unix_time := int(payload_meta.get("saved_at_unix_time", 0))
		if int(recovered_meta.get("created_at_unix_time", 0)) <= 0 and saved_at_unix_time > 0:
			recovered_meta["created_at_unix_time"] = saved_at_unix_time
		if int(recovered_meta.get("updated_at_unix_time", 0)) <= 0 and saved_at_unix_time > 0:
			recovered_meta["updated_at_unix_time"] = saved_at_unix_time

	var world_size_cells := read_vector2i(recovered_meta.get("world_size_cells", Vector2i.ZERO))
	if world_size_cells == Vector2i.ZERO:
		var generation_config = load(generation_config_path)
		var recovered_world_size := _get_generation_world_size_cells(generation_config)
		if recovered_world_size != Vector2i.ZERO:
			recovered_meta["world_size_cells"] = recovered_world_size

	return normalize_save_meta(recovered_meta)


func normalize_save_meta(raw_meta: Dictionary) -> Dictionary:
	var save_id := String(raw_meta.get("save_id", "")).strip_edges()
	if save_id.is_empty():
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
	var updated_at := int(raw_meta.get("updated_at_unix_time", created_at))
	if created_at <= 0 or updated_at <= 0:
		return {}
	var world_size_cells := read_vector2i(raw_meta.get("world_size_cells", Vector2i.ZERO))
	if world_size_cells == Vector2i.ZERO:
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
	var normalized = world_data.duplicate(true)
	normalized["map_seed"] = _normalize_runtime_seed(world_data.get("map_seed", 0))
	normalized["world_step"] = maxi(int(world_data.get("world_step", 0)), 0)
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
	var serialized_world_data = world_data.duplicate(true)
	serialized_world_data["active_submap_id"] = String(world_data.get("active_submap_id", ""))
	serialized_world_data["map_seed"] = _normalize_runtime_seed(world_data.get("map_seed", 0))
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
		var world_size_cells := read_vector2i(normalized_entry.get("world_size_cells", Vector2i.ZERO))
		serialized_entries.append({
			"save_id_b64": _encode_save_index_string(String(normalized_entry.get("save_id", ""))),
			"display_name_b64": _encode_save_index_string(String(normalized_entry.get("display_name", ""))),
			"world_preset_id_b64": _encode_save_index_string(String(normalized_entry.get("world_preset_id", ""))),
			"world_preset_name_b64": _encode_save_index_string(String(normalized_entry.get("world_preset_name", ""))),
			"generation_config_path_b64": _encode_save_index_string(String(normalized_entry.get("generation_config_path", ""))),
			"world_size_cells": {
				"x": world_size_cells.x,
				"y": world_size_cells.y,
			},
			"created_at_unix_time": int(normalized_entry.get("created_at_unix_time", 0)),
			"updated_at_unix_time": int(normalized_entry.get("updated_at_unix_time", 0)),
		})
	return serialized_entries


func deserialize_save_index_entry(raw_entry: Dictionary) -> Dictionary:
	if raw_entry.is_empty():
		return {}
	if not raw_entry.has("save_id_b64"):
		return {}
	return {
		"save_id": _decode_save_index_string(String(raw_entry.get("save_id_b64", ""))),
		"display_name": _decode_save_index_string(String(raw_entry.get("display_name_b64", ""))),
		"world_preset_id": _decode_save_index_string(String(raw_entry.get("world_preset_id_b64", ""))),
		"world_preset_name": _decode_save_index_string(String(raw_entry.get("world_preset_name_b64", ""))),
		"generation_config_path": _decode_save_index_string(String(raw_entry.get("generation_config_path_b64", ""))),
		"world_size_cells": raw_entry.get("world_size_cells", {"x": 0, "y": 0}),
		"created_at_unix_time": int(raw_entry.get("created_at_unix_time", 0)),
		"updated_at_unix_time": int(raw_entry.get("updated_at_unix_time", 0)),
	}


func normalize_save_index_entries(raw_entries: Array) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for raw_entry in raw_entries:
		if typeof(raw_entry) != TYPE_DICTIONARY:
			continue
		var entry := normalize_save_meta(deserialize_save_index_entry(raw_entry))
		if entry.is_empty():
			continue
		if not FileAccess.file_exists(_build_save_file_path(String(entry.get("save_id", "")))):
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
			updated_entries.append(normalize_save_meta(entry))

	if not replaced:
		updated_entries.append(normalized_meta)

	updated_entries.sort_custom(sort_save_meta_newest_first)
	return updated_entries


func read_save_index_payload(index_file: FileAccess) -> Variant:
	if index_file == null:
		return null
	var raw_bytes := index_file.get_buffer(index_file.get_length())
	if raw_bytes.is_empty():
		return {}
	if not is_ascii_save_index_buffer(raw_bytes):
		return null
	var raw_text := ascii_buffer_to_string(raw_bytes).strip_edges()
	if raw_text.is_empty():
		return {}
	var json := JSON.new()
	if json.parse(raw_text) != OK:
		return null
	return json.data


func is_ascii_save_index_buffer(raw_bytes: PackedByteArray) -> bool:
	var saw_content := false
	for byte_value in raw_bytes:
		var byte_int := int(byte_value)
		if byte_int == 9 or byte_int == 10 or byte_int == 13 or byte_int == 32:
			continue
		if not saw_content:
			if byte_int != 123 and byte_int != 91:
				return false
			saw_content = true
		if byte_int < 0 or byte_int > 127:
			return false
	return saw_content


func ascii_buffer_to_string(raw_bytes: PackedByteArray) -> String:
	var builder := ""
	for byte_value in raw_bytes:
		builder += char(int(byte_value))
	return builder


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
		return vector_dict.has("x") and vector_dict.has("y")
	return false


func _normalize_runtime_seed(value: Variant) -> int:
	var seed := int(value)
	return seed if seed > 0 else TRUE_RANDOM_SEED_SERVICE_SCRIPT.generate_seed()


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


func _encode_save_index_string(value: String) -> String:
	if value.is_empty():
		return ""
	return Marshalls.raw_to_base64(value.to_utf8_buffer())


func _decode_save_index_string(value: String) -> String:
	if value.is_empty():
		return ""
	return Marshalls.base64_to_raw(value).get_string_from_utf8()


func _build_save_file_path(save_id: String) -> String:
	return "%s/%s.dat" % [SAVE_DIRECTORY, save_id]


func _get_fallback_world_preset_name(generation_config_path: String) -> String:
	if _world_preset_registry != null and _world_preset_registry.has_method("get_fallback_preset_name"):
		return String(_world_preset_registry.get_fallback_preset_name(generation_config_path))
	return ""


func _get_generation_world_size_cells(generation_config) -> Vector2i:
	if generation_config != null and generation_config.has_method("get_world_size_cells"):
		return generation_config.get_world_size_cells()
	return Vector2i.ZERO


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
		normalized_submaps[submap_id] = {
			"submap_id": submap_id,
			"display_name": String(entry.get("display_name", "")),
			"generation_config_path": String(entry.get("generation_config_path", "")),
			"return_hint_text": String(entry.get("return_hint_text", "")),
			"is_generated": bool(entry.get("is_generated", false)),
			"player_coord": read_vector2i(entry.get("player_coord", Vector2i(-1, -1)), Vector2i(-1, -1)),
			"world_data": normalize_world_data(entry.get("world_data", {})) if entry.get("world_data", {}) is Dictionary else {},
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
	return {
		"visited": bool(state_data.get("visited", false)),
		"reputation": clampi(int(state_data.get("reputation", 0)), -100, 100),
		"active_conditions": _normalize_string_array(state_data.get("active_conditions", [])),
		"cooldowns": _normalize_int_dictionary(state_data.get("cooldowns", {})),
		"shop_inventory_seed": maxi(int(state_data.get("shop_inventory_seed", 0)), 0),
		"shop_last_refresh_step": maxi(int(state_data.get("shop_last_refresh_step", 0)), 0),
		"shop_states": _normalize_shop_states(state_data.get("shop_states", {})),
	}


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
		serialized_submaps[submap_id] = {
			"submap_id": submap_id,
			"display_name": String(entry.get("display_name", "")),
			"generation_config_path": String(entry.get("generation_config_path", "")),
			"return_hint_text": String(entry.get("return_hint_text", "")),
			"is_generated": bool(entry.get("is_generated", false)),
			"player_coord": read_vector2i(entry.get("player_coord", Vector2i(-1, -1)), Vector2i(-1, -1)),
			"world_data": serialize_world_data(entry.get("world_data", {})) if entry.get("world_data", {}) is Dictionary else {},
		}
	return serialized_submaps
