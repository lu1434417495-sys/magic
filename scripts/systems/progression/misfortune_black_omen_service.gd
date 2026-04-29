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

	var encounter_won := _did_player_win(payload)
	var defeated_elite_or_boss := bool(payload.get("defeated_elite_or_boss", payload.get("defender_is_elite_or_boss", false)))
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

	var encounter_won := _did_player_win(payload)
	var boss_encounter := bool(payload.get("boss_encounter", payload.get("encounter_is_boss", false)))
	var member_survived := bool(payload.get("member_survived", payload.get("unit_survived", false)))
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
	return ProgressionDataUtils.to_string_name(payload.get("member_id", payload.get("attacker_member_id", "")))


func _did_player_win(payload: Dictionary) -> bool:
	if payload.has("encounter_won"):
		return bool(payload.get("encounter_won", false))
	if payload.has("player_won"):
		return bool(payload.get("player_won", false))
	return String(payload.get("winner_faction_id", "")).strip_edges() == "player"


func _has_cursed_relic(member_state: PartyMemberState, payload: Dictionary) -> bool:
	if payload.has("has_cursed_relic"):
		return bool(payload.get("has_cursed_relic", false))
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
		return bool(payload.get("has_boss_curse", false))
	var curse_ids := ProgressionDataUtils.to_string_name_array(
		payload.get("boss_curse_status_ids", payload.get("member_boss_curse_status_ids", []))
	)
	return not curse_ids.is_empty()


func _get_item_def(item_id: StringName) -> ItemDef:
	var direct_match = _item_defs.get(item_id)
	if direct_match is ItemDef:
		return direct_match as ItemDef
	var string_match = _item_defs.get(String(item_id))
	return string_match as ItemDef if string_match is ItemDef else null


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
