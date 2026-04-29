class_name SettlementServiceResult
extends RefCounted

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


func from_dictionary(data: Dictionary) -> SettlementServiceResult:
	if data.is_empty():
		return self
	success = bool(data.get("success", false))
	message = String(data.get("message", ""))
	persist_party_state = bool(data.get("persist_party_state", false))
	persist_world_data = bool(data.get("persist_world_data", false))
	persist_player_coord = bool(data.get("persist_player_coord", false))
	inventory_delta = _duplicate_dictionary(data.get("inventory_delta", {}))
	gold_delta = int(data.get("gold_delta", 0))
	pending_character_rewards = _duplicate_dictionary_array(data.get("pending_character_rewards", []))
	quest_progress_events = _duplicate_dictionary_array(data.get("quest_progress_events", []))
	service_side_effects = _duplicate_dictionary(data.get("service_side_effects", {}))
	return self


static func _duplicate_dictionary(value) -> Dictionary:
	return value.duplicate(true) if value is Dictionary else {}


static func _duplicate_dictionary_array(value) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if value is not Array:
		return result
	for entry_variant in value:
		if entry_variant is Dictionary:
			result.append((entry_variant as Dictionary).duplicate(true))
	return result
