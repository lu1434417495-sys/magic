class_name SettlementServiceResult
extends RefCounted

const PENDING_CHARACTER_REWARD_SCRIPT = preload("res://scripts/systems/progression/pending_character_reward.gd")

const REQUIRED_SERIALIZED_FIELDS := [
	"success",
	"message",
	"persist_party_state",
	"persist_world_data",
	"persist_player_coord",
	"inventory_delta",
	"gold_delta",
	"pending_character_rewards",
	"quest_progress_events",
	"service_side_effects",
]
const PENDING_CHARACTER_REWARD_FIELDS := [
	"reward_id",
	"member_id",
	"member_name",
	"source_type",
	"source_id",
	"source_label",
	"summary_text",
	"entries",
]
const PENDING_CHARACTER_REWARD_ENTRY_FIELDS := [
	"entry_type",
	"target_id",
	"target_label",
	"amount",
	"reason_text",
]
const QUEST_PROGRESS_EVENT_ALLOWED_FIELDS := [
	"event_type",
	"quest_id",
	"objective_id",
	"objective_type",
	"target_id",
	"target_value",
	"progress_delta",
	"world_step",
	"allow_reaccept",
	"auto_accept",
	"action_id",
	"settlement_id",
	"member_id",
]
const QUEST_PROGRESS_EVENT_ACCEPT_ALLOWED_FIELDS := [
	"event_type",
	"quest_id",
	"world_step",
	"allow_reaccept",
]
const QUEST_PROGRESS_EVENT_COMPLETE_ALLOWED_FIELDS := [
	"event_type",
	"quest_id",
	"world_step",
	"auto_accept",
	"allow_reaccept",
]
const QUEST_PROGRESS_EVENT_PROGRESS_ALLOWED_FIELDS := [
	"event_type",
	"quest_id",
	"objective_id",
	"objective_type",
	"target_id",
	"target_value",
	"progress_delta",
	"world_step",
	"allow_reaccept",
	"auto_accept",
	"action_id",
	"settlement_id",
	"member_id",
]
const QUEST_PROGRESS_EVENT_ACCEPT := &"accept"
const QUEST_PROGRESS_EVENT_COMPLETE := &"complete"
const QUEST_PROGRESS_EVENT_PROGRESS := &"progress"

var success := false
var message := ""
var persist_party_state := false
var persist_world_data := false
var persist_player_coord := false
var inventory_delta: Dictionary = {}
var gold_delta := 0
var pending_character_rewards: Array[Dictionary] = []
var quest_progress_events: Array[Dictionary] = []
var service_side_effects: Dictionary = {}


func set_pending_character_rewards(rewards: Array) -> SettlementServiceResult:
	pending_character_rewards = _duplicate_dictionary_array(rewards)
	return self


func set_service_side_effects(effects: Dictionary) -> SettlementServiceResult:
	service_side_effects = _duplicate_dictionary(effects)
	return self


func to_dictionary() -> Dictionary:
	return {
		"success": success,
		"message": message,
		"persist_party_state": persist_party_state,
		"persist_world_data": persist_world_data,
		"persist_player_coord": persist_player_coord,
		"inventory_delta": _duplicate_dictionary(inventory_delta),
		"gold_delta": gold_delta,
		"pending_character_rewards": _duplicate_dictionary_array(pending_character_rewards),
		"quest_progress_events": _duplicate_dictionary_array(quest_progress_events),
		"service_side_effects": _duplicate_dictionary(service_side_effects),
	}


func from_dictionary(data: Variant) -> SettlementServiceResult:
	if data is not Dictionary:
		return null
	var payload := data as Dictionary
	if not _has_valid_serialized_payload(payload):
		return null

	success = payload["success"]
	message = payload["message"]
	persist_party_state = payload["persist_party_state"]
	persist_world_data = payload["persist_world_data"]
	persist_player_coord = payload["persist_player_coord"]
	inventory_delta = _duplicate_dictionary(payload["inventory_delta"])
	gold_delta = payload["gold_delta"]
	pending_character_rewards = _duplicate_dictionary_array(payload["pending_character_rewards"])
	quest_progress_events = _duplicate_dictionary_array(payload["quest_progress_events"])
	service_side_effects = _duplicate_dictionary(payload["service_side_effects"])
	return self


static func _has_valid_serialized_payload(payload: Dictionary) -> bool:
	if not _has_exact_fields(payload, REQUIRED_SERIALIZED_FIELDS):
		return false
	if payload["success"] is not bool:
		return false
	if payload["message"] is not String:
		return false
	if payload["persist_party_state"] is not bool:
		return false
	if payload["persist_world_data"] is not bool:
		return false
	if payload["persist_player_coord"] is not bool:
		return false
	if payload["inventory_delta"] is not Dictionary:
		return false
	if payload["gold_delta"] is not int:
		return false
	if not _is_pending_character_reward_array(payload["pending_character_rewards"]):
		return false
	if not _is_quest_progress_event_array(payload["quest_progress_events"]):
		return false
	if payload["service_side_effects"] is not Dictionary:
		return false
	return true


static func _has_exact_fields(payload: Dictionary, expected_fields: Array) -> bool:
	if payload.size() != expected_fields.size():
		return false
	var expected_lookup := {}
	var seen_lookup := {}
	for field_name in expected_fields:
		expected_lookup[field_name] = true
	for key_variant in payload.keys():
		if key_variant is not String:
			return false
		if not expected_lookup.has(key_variant):
			return false
		if seen_lookup.has(key_variant):
			return false
		seen_lookup[key_variant] = true
	return seen_lookup.size() == expected_lookup.size()


static func _duplicate_dictionary(value) -> Dictionary:
	return value.duplicate(true) if value is Dictionary else {}


static func _is_pending_character_reward_array(value: Variant) -> bool:
	if value is not Array:
		return false
	for entry_variant in value:
		if entry_variant is not Dictionary:
			return false
		var entry_data := entry_variant as Dictionary
		if not _is_pending_character_reward_payload(entry_data):
			return false
	return true


static func _is_pending_character_reward_payload(entry_data: Dictionary) -> bool:
	if not _has_exact_fields(entry_data, PENDING_CHARACTER_REWARD_FIELDS):
		return false
	if entry_data["entries"] is not Array:
		return false
	for reward_entry_variant in entry_data["entries"]:
		if reward_entry_variant is not Dictionary:
			return false
		var reward_entry_data := reward_entry_variant as Dictionary
		if not _has_exact_fields(reward_entry_data, PENDING_CHARACTER_REWARD_ENTRY_FIELDS):
			return false
	return PENDING_CHARACTER_REWARD_SCRIPT.from_dict(entry_data) != null


static func _is_quest_progress_event_array(value: Variant) -> bool:
	if value is not Array:
		return false
	for entry_variant in value:
		if entry_variant is not Dictionary:
			return false
		var event_data := entry_variant as Dictionary
		if not _is_quest_progress_event_payload(event_data):
			return false
	return true


static func _is_quest_progress_event_payload(event_data: Dictionary) -> bool:
	if not _has_allowed_fields(event_data, QUEST_PROGRESS_EVENT_ALLOWED_FIELDS):
		return false
	if not event_data.has("event_type") or not _is_non_empty_string_name_value(event_data["event_type"]):
		return false
	var event_type := ProgressionDataUtils.to_string_name(event_data["event_type"])
	if event_data.has("world_step") and (event_data["world_step"] is not int or int(event_data["world_step"]) < 0):
		return false
	for bool_field in ["allow_reaccept", "auto_accept"]:
		if event_data.has(bool_field) and event_data[bool_field] is not bool:
			return false
	for optional_id_field in ["action_id", "settlement_id", "member_id"]:
		if event_data.has(optional_id_field) and not _is_string_name_value(event_data[optional_id_field]):
			return false
	match event_type:
		QUEST_PROGRESS_EVENT_ACCEPT:
			if not _has_allowed_fields(event_data, QUEST_PROGRESS_EVENT_ACCEPT_ALLOWED_FIELDS):
				return false
			return _is_required_id_field(event_data, "quest_id")
		QUEST_PROGRESS_EVENT_COMPLETE:
			if not _has_allowed_fields(event_data, QUEST_PROGRESS_EVENT_COMPLETE_ALLOWED_FIELDS):
				return false
			return _is_required_id_field(event_data, "quest_id")
		QUEST_PROGRESS_EVENT_PROGRESS:
			if not _has_allowed_fields(event_data, QUEST_PROGRESS_EVENT_PROGRESS_ALLOWED_FIELDS):
				return false
			return _is_valid_progress_event_payload(event_data)
		_:
			return false


static func _is_valid_progress_event_payload(event_data: Dictionary) -> bool:
	if not event_data.has("progress_delta") or event_data["progress_delta"] is not int or int(event_data["progress_delta"]) <= 0:
		return false
	if event_data.has("target_value") and (event_data["target_value"] is not int or int(event_data["target_value"]) <= 0):
		return false
	if event_data.has("quest_id") or event_data.has("objective_id"):
		return _is_required_id_field(event_data, "quest_id") and _is_required_id_field(event_data, "objective_id")
	return _is_required_id_field(event_data, "objective_type") and _is_required_id_field(event_data, "target_id")


static func _has_allowed_fields(payload: Dictionary, allowed_fields: Array) -> bool:
	var allowed_lookup := {}
	var seen_lookup := {}
	for field_name in allowed_fields:
		allowed_lookup[field_name] = true
	for key_variant in payload.keys():
		if key_variant is not String:
			return false
		if not allowed_lookup.has(key_variant):
			return false
		if seen_lookup.has(key_variant):
			return false
		seen_lookup[key_variant] = true
	return true


static func _is_required_id_field(payload: Dictionary, field_name: String) -> bool:
	return payload.has(field_name) and _is_non_empty_string_name_value(payload[field_name])


static func _is_non_empty_string_name_value(value: Variant) -> bool:
	return _is_string_name_value(value) and not String(value).strip_edges().is_empty()


static func _is_string_name_value(value: Variant) -> bool:
	var value_type := typeof(value)
	return value_type == TYPE_STRING or value_type == TYPE_STRING_NAME


static func _duplicate_dictionary_array(value) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if value is not Array:
		return result
	for entry_variant in value:
		if entry_variant is Dictionary:
			result.append((entry_variant as Dictionary).duplicate(true))
	return result
