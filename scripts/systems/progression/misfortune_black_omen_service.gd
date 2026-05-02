## Owns the controlled doom_marked write surface for future story/event scripts.
## Notes:
## - doom_marked is a permanent boolean-like custom stat, so this service writes 1 directly.
## - This path intentionally does not route through AttributeService's protected custom-stat gate.
## - The first two hooks are sample black omen events; additional low-luck hooks can extend this file later.

class_name MisfortuneBlackOmenService
extends RefCounted

const PARTY_MEMBER_STATE_SCRIPT = preload("res://scripts/player/progression/party_member_state.gd")
const ITEM_DEF_SCRIPT = preload("res://scripts/player/warehouse/item_def.gd")
const LOW_LUCK_RELIC_RULES_SCRIPT = preload("res://scripts/systems/fate/low_luck_relic_rules.gd")
const PartyMemberState = PARTY_MEMBER_STATE_SCRIPT
const ItemDef = ITEM_DEF_SCRIPT

const DOOM_MARKED_STAT_ID: StringName = &"doom_marked"

const HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY: StringName = &"cursed_relic_elite_or_boss_victory"
const HOOK_BOSS_CURSE_SURVIVAL_VICTORY: StringName = &"boss_curse_survival_victory"
const HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH: StringName = &"dead_road_lantern_black_omen_path"

const CURSED_RELIC_REQUIRED_TAGS: Array[StringName] = [
	&"cursed",
	&"relic",
]

var _character_gateway: Object = null
var _item_defs: Dictionary = {}


func setup(character_gateway: Object = null, item_defs: Dictionary = {}) -> void:
	_character_gateway = character_gateway
	_item_defs = item_defs if item_defs != null else {}


func dispose() -> void:
	_character_gateway = null
	_item_defs = {}


func try_run_hook(hook_id: StringName, payload: Dictionary = {}) -> Dictionary:
	match hook_id:
		HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY:
			return _try_grant_cursed_relic_elite_or_boss_victory(payload)
		HOOK_BOSS_CURSE_SURVIVAL_VICTORY:
			return _try_grant_boss_curse_survival_victory(payload)
		HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH:
			return _try_grant_dead_road_lantern_black_omen_path(payload)
		_:
			var member_id := _resolve_member_id(payload)
			return {
				"ok": false,
				"hook_id": String(hook_id),
				"member_id": String(member_id),
				"conditions_met": false,
				"granted": false,
				"already_marked": false,
				"doom_marked": _get_doom_marked_value(_get_member_state(member_id)),
				"error_code": "unknown_hook_id",
			}


func grant_doom_mark(member_id: StringName, source_id: StringName, _source_context: Dictionary = {}) -> Dictionary:
	var result := _build_result(member_id, source_id)
	if member_id == &"" or source_id == &"":
		result["error_code"] = "invalid_request"
		return result

	var member_state := _get_member_state(member_id)
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		result["error_code"] = "member_not_found"
		return result

	result["ok"] = true
	result["conditions_met"] = true

	var current_value := _get_doom_marked_value(member_state)
	result["doom_marked"] = current_value
	if current_value >= 1:
		result["already_marked"] = true
		return result

	member_state.progression.unit_base_attributes.set_attribute_value(DOOM_MARKED_STAT_ID, 1)
	result["granted"] = true
	result["doom_marked"] = 1
	return result


func _try_grant_cursed_relic_elite_or_boss_victory(payload: Dictionary) -> Dictionary:
	var member_id := _resolve_member_id(payload)
	var result := _build_result(member_id, HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY)
	var member_state := _get_member_state(member_id)
	if member_id == &"":
		result["error_code"] = "invalid_request"
		return result
	if member_state == null:
		result["error_code"] = "member_not_found"
		return result

	var encounter_won := _is_payload_bool_true(payload, "encounter_won")
	var defeated_elite_or_boss := _is_payload_bool_true(payload, "defeated_elite_or_boss")
	var has_cursed_relic := _has_cursed_relic(member_state, payload)
	result["ok"] = true
	result["conditions_met"] = encounter_won and defeated_elite_or_boss and has_cursed_relic
	result["doom_marked"] = _get_doom_marked_value(member_state)
	if not bool(result.get("conditions_met", false)):
		result["error_code"] = "conditions_not_met"
		return result

	return grant_doom_mark(member_id, HOOK_CURSED_RELIC_ELITE_OR_BOSS_VICTORY, payload)


func _try_grant_boss_curse_survival_victory(payload: Dictionary) -> Dictionary:
	var member_id := _resolve_member_id(payload)
	var result := _build_result(member_id, HOOK_BOSS_CURSE_SURVIVAL_VICTORY)
	var member_state := _get_member_state(member_id)
	if member_id == &"":
		result["error_code"] = "invalid_request"
		return result
	if member_state == null:
		result["error_code"] = "member_not_found"
		return result

	var encounter_won := _is_payload_bool_true(payload, "encounter_won")
	var boss_encounter := _is_payload_bool_true(payload, "boss_encounter")
	var member_survived := _is_payload_bool_true(payload, "member_survived")
	var has_boss_curse := _has_boss_curse(payload)
	result["ok"] = true
	result["conditions_met"] = encounter_won and boss_encounter and member_survived and has_boss_curse
	result["doom_marked"] = _get_doom_marked_value(member_state)
	if not bool(result.get("conditions_met", false)):
		result["error_code"] = "conditions_not_met"
		return result

	return grant_doom_mark(member_id, HOOK_BOSS_CURSE_SURVIVAL_VICTORY, payload)


func _try_grant_dead_road_lantern_black_omen_path(payload: Dictionary) -> Dictionary:
	var member_id := _resolve_member_id(payload)
	var result := _build_result(member_id, HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH)
	var member_state := _get_member_state(member_id)
	if member_id == &"":
		result["error_code"] = "invalid_request"
		return result
	if member_state == null:
		result["error_code"] = "member_not_found"
		return result

	var path_tags := LOW_LUCK_RELIC_RULES_SCRIPT.normalize_path_tags(payload.get("path_tags", []))
	var has_lantern := LOW_LUCK_RELIC_RULES_SCRIPT.member_has_item(
		_item_defs,
		member_state,
		LOW_LUCK_RELIC_RULES_SCRIPT.ITEM_DEAD_ROAD_LANTERN
	)
	result["ok"] = true
	result["conditions_met"] = has_lantern and path_tags.has(LOW_LUCK_RELIC_RULES_SCRIPT.PATH_TAG_BLACK_OMEN)
	result["doom_marked"] = _get_doom_marked_value(member_state)
	if not bool(result.get("conditions_met", false)):
		result["error_code"] = "conditions_not_met"
		return result

	return grant_doom_mark(member_id, HOOK_DEAD_ROAD_LANTERN_BLACK_OMEN_PATH, payload)


func _resolve_member_id(payload: Dictionary) -> StringName:
	if not payload.has("member_id"):
		return &""
	var member_id_variant: Variant = payload["member_id"]
	var member_id_type := typeof(member_id_variant)
	if member_id_type != TYPE_STRING and member_id_type != TYPE_STRING_NAME:
		return &""
	return ProgressionDataUtils.to_string_name(member_id_variant)


func _is_payload_bool_true(payload: Dictionary, field_name: String) -> bool:
	return payload.has(field_name) and payload[field_name] is bool and bool(payload[field_name])


func _has_cursed_relic(member_state: PartyMemberState, payload: Dictionary) -> bool:
	if payload.has("has_cursed_relic"):
		return payload["has_cursed_relic"] is bool and bool(payload["has_cursed_relic"])
	if member_state == null or member_state.equipment_state == null or _item_defs.is_empty():
		return false

	for entry_slot_id in member_state.equipment_state.get_entry_slot_ids():
		var entry = member_state.equipment_state.get_entry(entry_slot_id)
		if entry == null or entry.item_id == &"":
			continue
		var item_def: ItemDef = _get_item_def(entry.item_id)
		if item_def == null:
			continue
		var item_tags := item_def.get_tags()
		var matched := true
		for required_tag in CURSED_RELIC_REQUIRED_TAGS:
			if not item_tags.has(required_tag):
				matched = false
				break
		if matched:
			return true
	return false


func _has_boss_curse(payload: Dictionary) -> bool:
	if payload.has("has_boss_curse"):
		return payload["has_boss_curse"] is bool and bool(payload["has_boss_curse"])
	var curse_ids := ProgressionDataUtils.to_string_name_array(payload.get("boss_curse_status_ids", []))
	return not curse_ids.is_empty()


func _get_item_def(item_id: StringName) -> ItemDef:
	var item_def = _get_item_def_by_string_name_key(_item_defs, item_id)
	return item_def as ItemDef if item_def is ItemDef else null


func _get_item_def_by_string_name_key(item_defs: Dictionary, item_id: StringName):
	if item_id == &"":
		return null
	for key in item_defs.keys():
		if typeof(key) != TYPE_STRING_NAME:
			continue
		if key == item_id:
			return item_defs[key]
	return null


func _get_member_state(member_id: StringName) -> PartyMemberState:
	if _character_gateway == null or member_id == &"":
		return null
	if not _character_gateway.has_method("get_member_state"):
		return null
	return _character_gateway.get_member_state(member_id) as PartyMemberState


func _get_doom_marked_value(member_state: PartyMemberState) -> int:
	if member_state == null or member_state.progression == null or member_state.progression.unit_base_attributes == null:
		return 0
	return member_state.progression.unit_base_attributes.get_attribute_value(DOOM_MARKED_STAT_ID)


func _build_result(member_id: StringName, source_id: StringName) -> Dictionary:
	return {
		"ok": false,
		"hook_id": String(source_id),
		"member_id": String(member_id),
		"conditions_met": false,
		"granted": false,
		"already_marked": false,
		"doom_marked": 0,
		"error_code": "",
	}
