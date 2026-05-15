class_name GameRuntimeSnapshotBuilder
extends RefCounted

const BATTLE_HUD_ADAPTER_SCRIPT = preload("res://scripts/systems/battle/presentation/battle_hud_adapter.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/world/encounter_anchor_data.gd")
const GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT = preload("res://scripts/utils/game_text_snapshot_renderer.gd")
const QUEST_ENTRY_REQUIRED_FIELDS := [
	"quest_id",
	"status_id",
	"objective_progress",
	"accepted_at_world_step",
	"completed_at_world_step",
	"reward_claimed_at_world_step",
	"last_progress_context",
]

var _runtime_ref: WeakRef = null
var _runtime = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null


func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func build_headless_snapshot() -> Dictionary:
	if _runtime == null:
		return {}
	return {
		"status": {
			"view": "battle" if _runtime.is_battle_active() else "world",
			"text": _runtime.get_status_text(),
		},
		"modal": {
			"id": _runtime.get_active_modal_id(),
		},
		"logs": _build_log_snapshot(),
		"world": _build_world_snapshot(),
		"submap": _build_submap_snapshot(),
		"game_over": _build_game_over_snapshot(),
		"party": _build_party_snapshot(),
		"settlement": _build_settlement_snapshot(),
		"contract_board": _build_contract_board_snapshot(),
		"shop": _build_shop_snapshot(),
		"forge": _build_forge_snapshot(),
		"stagecoach": _build_stagecoach_snapshot(),
		"character_info": _build_character_info_snapshot(),
		"warehouse": _build_warehouse_snapshot(),
		"battle": _build_battle_snapshot(),
		"loot": _build_loot_snapshot(),
		"reward": _build_reward_snapshot(),
		"promotion": _build_promotion_snapshot(),
	}


func build_text_snapshot() -> String:
	return GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT.render_world_snapshot(build_headless_snapshot())


func _build_world_snapshot() -> Dictionary:
	var selected_settlement: Dictionary = _runtime.get_selected_settlement()
	var selected_npc: Dictionary = _runtime.get_selected_world_npc()
	var selected_encounter: ENCOUNTER_ANCHOR_DATA_SCRIPT = _runtime.get_selected_encounter_anchor()
	var selected_world_event: Dictionary = _runtime.get_selected_world_event()
	return {
		"map_id": _runtime.get_active_map_id(),
		"map_display_name": _runtime.get_active_map_display_name(),
		"is_submap": _runtime.is_submap_active(),
		"world_step": _runtime.get_world_step(),
		"player_coord": _coord_to_dict(_runtime.get_player_coord()),
		"player_visible_on_map": bool(_runtime.is_player_visible_on_world_map()),
		"selected_coord": _coord_to_dict(_runtime.get_selected_coord()),
		"selected_settlement_id": String(selected_settlement.get("settlement_id", "")),
		"selected_npc_name": String(selected_npc.get("display_name", "")),
		"selected_world_event_id": String(selected_world_event.get("event_id", "")),
		"selected_world_event_name": String(selected_world_event.get("display_name", "")),
		"selected_encounter_id": String(selected_encounter.entity_id) if selected_encounter != null else "",
		"selected_encounter_name": String(selected_encounter.display_name) if selected_encounter != null else "",
		"selected_encounter_kind": String(selected_encounter.encounter_kind) if selected_encounter != null else "",
		"selected_encounter_growth_stage": int(selected_encounter.growth_stage) if selected_encounter != null else 0,
		"nearby_world_events": _build_nearby_world_event_entries(),
		"nearby_encounters": _build_nearby_encounter_entries(),
	}


func _build_submap_snapshot() -> Dictionary:
	var prompt: Dictionary = _runtime.get_pending_submap_prompt()
	return {
		"active": _runtime.is_submap_active(),
		"map_id": _runtime.get_active_map_id(),
		"map_display_name": _runtime.get_active_map_display_name(),
		"return_hint_text": _runtime.get_submap_return_hint_text(),
		"confirm_visible": _runtime.get_active_modal_id() == "submap_confirm",
		"prompt": prompt.duplicate(true),
	}


func _build_game_over_snapshot() -> Dictionary:
	var context: Dictionary = _runtime.get_game_over_context()
	if context.is_empty():
		return {}
	return context.duplicate(true)


func _build_party_snapshot() -> Dictionary:
	var members: Array[Dictionary] = []
	var party_state = _runtime.get_party_state()
	if party_state != null:
		for member_id in party_state.active_member_ids:
			members.append(_build_party_member_snapshot(member_id, "active"))
		for member_id in party_state.reserve_member_ids:
			members.append(_build_party_member_snapshot(member_id, "reserve"))
	return {
		"gold": int(party_state.gold) if party_state != null else 0,
		"leader_member_id": String(party_state.leader_member_id) if party_state != null else "",
		"active_member_ids":
			_string_name_array_to_string_array(party_state.active_member_ids if party_state != null else []),
		"reserve_member_ids":
			_string_name_array_to_string_array(party_state.reserve_member_ids if party_state != null else []),
		"selected_member_id": String(_runtime.get_party_selected_member_id()),
		"pending_reward_count": _runtime.get_pending_reward_count(),
		"members": members,
		"quests": _build_quest_snapshot(party_state),
	}


func _build_quest_snapshot(party_state) -> Dictionary:
	if party_state == null:
		return {}
	var active_quests_variant = _get_party_state_quest_value(party_state, "active_quests", "get_active_quests")
	var claimable_quests_variant = _get_party_state_quest_value(party_state, "claimable_quests", "get_claimable_quests")
	var completed_quest_ids_variant = _get_party_state_quest_value(party_state, "completed_quest_ids", "get_completed_quest_ids")
	if active_quests_variant == null and claimable_quests_variant == null and completed_quest_ids_variant == null:
		return {}
	var active_quest_entries := _build_quest_entries(active_quests_variant, "active")
	var claimable_quest_entries := _build_quest_entries(claimable_quests_variant, "claimable")
	var active_quest_ids := _build_quest_ids(active_quest_entries)
	var claimable_quest_ids := _build_quest_ids(claimable_quest_entries)
	var completed_quest_ids: Array[String] = []
	if completed_quest_ids_variant is Array:
		completed_quest_ids = _string_name_array_to_string_array(ProgressionDataUtils.to_string_name_array(completed_quest_ids_variant))
	return {
		"active_quest_ids": active_quest_ids,
		"claimable_quest_ids": claimable_quest_ids,
		"completed_quest_ids": completed_quest_ids,
		"active_quests": active_quest_entries,
		"claimable_quests": claimable_quest_entries,
	}


func _get_party_state_quest_value(party_state, _property_name: String, getter_name: String):
	if party_state == null:
		return null
	if party_state is Object and party_state.has_method(getter_name):
		return party_state.call(getter_name)
	return null


func _build_quest_entries(quest_entries_variant, stage_id: String) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	if quest_entries_variant is Array:
		for quest_variant in quest_entries_variant:
			var quest_entry := _normalize_quest_entry(quest_variant, stage_id)
			if not quest_entry.is_empty():
				entries.append(quest_entry)
		entries.sort_custom(Callable(self, "_compare_quest_entries"))
	return entries


func _build_quest_ids(quest_entries: Array[Dictionary]) -> Array[String]:
	var quest_ids: Array[String] = []
	for quest_entry in quest_entries:
		var quest_id := String(quest_entry.get("quest_id", ""))
		if quest_id.is_empty():
			continue
		quest_ids.append(quest_id)
	return quest_ids


func _normalize_quest_entry(quest_variant, stage_id: String) -> Dictionary:
	var quest_data: Dictionary = {}
	if quest_variant is Dictionary:
		quest_data = (quest_variant as Dictionary).duplicate(true)
	elif quest_variant != null and quest_variant.has_method("to_dict"):
		var quest_data_variant = quest_variant.to_dict()
		if quest_data_variant is Dictionary:
			quest_data = (quest_data_variant as Dictionary).duplicate(true)
	if quest_data.is_empty():
		return {}
	if not _has_exact_quest_entry_fields(quest_data):
		return {}
	var quest_id := _read_quest_string(quest_data["quest_id"])
	var status_id := _read_quest_string(quest_data["status_id"])
	if quest_id.is_empty() or status_id.is_empty():
		return {}
	if (
		quest_data["accepted_at_world_step"] is not int
		or int(quest_data["accepted_at_world_step"]) < -1
		or quest_data["completed_at_world_step"] is not int
		or int(quest_data["completed_at_world_step"]) < -1
		or quest_data["reward_claimed_at_world_step"] is not int
		or int(quest_data["reward_claimed_at_world_step"]) < -1
	):
		return {}
	var objective_progress = _normalize_quest_progress_map(quest_data["objective_progress"])
	if objective_progress == null:
		return {}
	var context_variant = quest_data["last_progress_context"]
	if context_variant is not Dictionary:
		return {}
	quest_data["quest_id"] = quest_id
	quest_data["stage_id"] = stage_id
	quest_data["status_id"] = status_id
	quest_data["objective_progress"] = objective_progress
	quest_data["last_progress_context"] = (context_variant as Dictionary).duplicate(true)
	return quest_data


func _has_exact_quest_entry_fields(quest_data: Dictionary) -> bool:
	if quest_data.size() != QUEST_ENTRY_REQUIRED_FIELDS.size():
		return false
	for field_name in QUEST_ENTRY_REQUIRED_FIELDS:
		if not quest_data.has(field_name):
			return false
	return true


func _read_quest_string(value) -> String:
	if value is not String and value is not StringName:
		return ""
	var text := String(value).strip_edges()
	return text


func _normalize_quest_progress_map(progress_variant):
	if progress_variant is not Dictionary:
		return null
	var result: Dictionary = {}
	for objective_id_variant in (progress_variant as Dictionary).keys():
		var objective_id := _read_quest_string(objective_id_variant)
		if objective_id.is_empty():
			return null
		var progress_value = (progress_variant as Dictionary)[objective_id_variant]
		if progress_value is not int or int(progress_value) < 0:
			return null
		result[objective_id] = int(progress_value)
	return result


func _compare_quest_entries(a: Dictionary, b: Dictionary) -> bool:
	return String(a.get("quest_id", "")) < String(b.get("quest_id", ""))


func _build_party_member_snapshot(member_id: StringName, roster_role: String) -> Dictionary:
	var party_state = _runtime.get_party_state()
	var member_state = party_state.get_member_state(member_id) if party_state != null else null
	var achievement_summary: Dictionary = _runtime.get_member_achievement_summary(member_id)
	var attribute_snapshot = _runtime.get_member_attribute_snapshot(member_id)
	var equipment_entries: Array = _runtime.get_member_equipped_entries(member_id)
	var progression = member_state.progression if member_state != null else null
	return {
		"member_id": String(member_id),
		"display_name": _runtime.get_member_display_name(member_id),
		"roster_role": roster_role,
		"is_leader": party_state != null and party_state.leader_member_id == member_id,
		"current_hp": int(member_state.current_hp) if member_state != null else 0,
		"current_mp": int(member_state.current_mp) if member_state != null else 0,
		"current_aura": int(member_state.current_aura) if member_state != null else 0,
		"unlocked_combat_resource_ids": _build_member_unlocked_combat_resource_ids(member_state),
		"learned_skill_ids": _build_member_learned_skill_ids(member_state),
		"active_core_skill_ids": _build_sorted_string_name_array(progression.active_core_skill_ids if progression != null else []),
		"active_level_trigger_core_skill_id": String(progression.active_level_trigger_core_skill_id) if progression != null else "",
		"locked_level_trigger_skill_ids": _build_sorted_string_name_array(progression.locked_level_trigger_skill_ids if progression != null else []),
		"blocked_relearn_skill_ids": _build_sorted_string_name_array(progression.blocked_relearn_skill_ids if progression != null else []),
		"skill_entries": _build_member_skill_entries(member_state),
		"profession_entries": _build_member_profession_entries(member_state),
		"achievement_summary": achievement_summary.duplicate(true) if achievement_summary is Dictionary else {},
		"attributes": attribute_snapshot.to_dict() if attribute_snapshot is Object and attribute_snapshot.has_method("to_dict") else {},
		"equipment": equipment_entries,
		"equipment_count": equipment_entries.size(),
	}


func _build_member_learned_skill_ids(member_state) -> Array[String]:
	var learned_skill_ids: Array[String] = []
	if member_state == null or member_state.progression == null:
		return learned_skill_ids
	for skill_key in member_state.progression.skills.keys():
		var skill_id := ProgressionDataUtils.to_string_name(skill_key)
		var skill_progress = member_state.progression.get_skill_progress(skill_id)
		if skill_progress == null or not skill_progress.is_learned:
			continue
		learned_skill_ids.append(String(skill_id))
	learned_skill_ids.sort()
	return learned_skill_ids


func _build_member_unlocked_combat_resource_ids(member_state) -> Array[String]:
	var resource_ids: Array[String] = []
	if member_state == null or member_state.progression == null:
		return resource_ids
	for resource_id in member_state.progression.unlocked_combat_resource_ids:
		resource_ids.append(String(resource_id))
	resource_ids.sort()
	return resource_ids


func _build_member_skill_entries(member_state) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	if member_state == null or member_state.progression == null:
		return entries
	for skill_key in ProgressionDataUtils.sorted_string_keys(member_state.progression.skills):
		var skill_id := ProgressionDataUtils.to_string_name(skill_key)
		var skill_progress = member_state.progression.get_skill_progress(skill_id)
		if skill_progress == null or not bool(skill_progress.is_learned):
			continue
		entries.append({
			"skill_id": String(skill_id),
			"level": int(skill_progress.skill_level),
			"is_core": bool(skill_progress.is_core),
			"assigned_profession_id": String(skill_progress.assigned_profession_id),
			"is_level_trigger_active": bool(skill_progress.is_level_trigger_active),
			"is_level_trigger_locked": bool(skill_progress.is_level_trigger_locked),
			"core_max_growth_claimed": bool(skill_progress.core_max_growth_claimed),
			"granted_source_type": String(skill_progress.granted_source_type),
			"granted_source_id": String(skill_progress.granted_source_id),
		})
	return entries


func _build_member_profession_entries(member_state) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	if member_state == null or member_state.progression == null:
		return entries
	for profession_key in ProgressionDataUtils.sorted_string_keys(member_state.progression.professions):
		var profession_id := ProgressionDataUtils.to_string_name(profession_key)
		var profession_progress = member_state.progression.get_profession_progress(profession_id)
		if profession_progress == null:
			continue
		entries.append({
			"profession_id": String(profession_id),
			"rank": int(profession_progress.rank),
			"is_active": bool(profession_progress.is_active),
			"is_hidden": bool(profession_progress.is_hidden),
			"core_skill_ids": _build_sorted_string_name_array(profession_progress.core_skill_ids),
			"granted_skill_ids": _build_sorted_string_name_array(profession_progress.granted_skill_ids),
			"inactive_reason": String(profession_progress.inactive_reason),
		})
	return entries


func _build_sorted_string_name_array(values: Array) -> Array[String]:
	var result := _string_name_array_to_string_array(values)
	result.sort()
	return result


func _build_settlement_snapshot() -> Dictionary:
	var settlement_id: String = _runtime.get_resolved_settlement_id()
	var window_data: Dictionary = _runtime.get_settlement_window_data(settlement_id) if not settlement_id.is_empty() else {}
	var services: Array[Dictionary] = []
	for service_variant in window_data.get("available_services", []):
		if service_variant is not Dictionary:
			continue
		var service_data: Dictionary = service_variant
		services.append({
			"action_id": String(service_data.get("action_id", "")),
			"facility_name": String(service_data.get("facility_name", "")),
			"npc_name": String(service_data.get("npc_name", "")),
			"service_type": String(service_data.get("service_type", "")),
			"interaction_script_id": String(service_data.get("interaction_script_id", "")),
		})
	return {
		"visible": _runtime.get_active_modal_id() == "settlement",
		"settlement_id": settlement_id,
		"display_name": String(window_data.get("display_name", "")),
		"tier_name": String(window_data.get("tier_name", "")),
		"faction_id": String(window_data.get("faction_id", "")),
		"services": services,
		"feedback_text": _runtime.get_settlement_feedback_text(),
	}


func _build_shop_snapshot() -> Dictionary:
	var window_data: Dictionary = _runtime.get_shop_window_data()
	if _window_data_matches_panel_kind(window_data, "forge"):
		window_data.clear()
	window_data.erase("party_state")
	return {
		"visible": _runtime.get_active_modal_id() == "shop",
		"window_data": window_data.duplicate(true),
	}


func _build_contract_board_snapshot() -> Dictionary:
	var window_data := _resolve_contract_board_window_data()
	window_data.erase("party_state")
	return {
		"visible": _runtime.get_active_modal_id() == "contract_board",
		"window_data": window_data.duplicate(true),
	}


func _build_forge_snapshot() -> Dictionary:
	var window_data := _resolve_forge_window_data()
	window_data.erase("party_state")
	return {
		"visible": _runtime.get_active_modal_id() == "forge",
		"window_data": window_data.duplicate(true),
	}


func _build_stagecoach_snapshot() -> Dictionary:
	var window_data: Dictionary = _runtime.get_stagecoach_window_data()
	window_data.erase("party_state")
	return {
		"visible": _runtime.get_active_modal_id() == "stagecoach",
		"window_data": window_data.duplicate(true),
	}


func _build_character_info_snapshot() -> Dictionary:
	var context: Dictionary = _runtime.get_character_info_context()
	context["visible"] = _runtime.get_active_modal_id() == "character_info"
	if context.has("coord"):
		context["coord"] = _coord_to_dict(context.get("coord", Vector2i.ZERO))
	return context


func _build_warehouse_snapshot() -> Dictionary:
	return {
		"visible": _runtime.get_active_modal_id() == "warehouse",
		"entry_label": _runtime.get_active_warehouse_entry_label(),
		"window_data": _runtime.get_warehouse_window_data() if _runtime.get_party_state() != null else {},
	}


func _build_battle_snapshot() -> Dictionary:
	var battle_state = _runtime.get_battle_state()
	if battle_state == null or battle_state.is_empty():
		return {
			"active": false,
		}
	var battle_runtime = _runtime.get_battle_runtime() if _runtime != null and _runtime.has_method("get_battle_runtime") else null
	var calamity_snapshot := {}
	if battle_runtime != null and battle_runtime.has_method("get_calamity_by_member_id"):
		calamity_snapshot = ProgressionDataUtils.string_name_int_map_to_string_dict(
			battle_runtime.get_calamity_by_member_id()
		)
	var adapter = BATTLE_HUD_ADAPTER_SCRIPT.new()
	adapter.set_party_member_state_resolver(Callable(self, "_get_party_member_state_for_snapshot"))
	adapter.set_content_def_providers(
		Callable(_runtime, "get_skill_defs") if _runtime.has_method("get_skill_defs") else Callable(),
		Callable(_runtime, "get_item_defs") if _runtime.has_method("get_item_defs") else Callable()
	)
	var hud_snapshot = adapter.build_snapshot(
		battle_state,
		_runtime.get_battle_selected_coord(),
		_runtime.get_selected_battle_skill_id(),
		_runtime.get_selected_battle_skill_name(),
		_runtime.get_selected_battle_skill_variant_name(),
		_runtime.get_selected_battle_skill_target_coords(),
		_runtime.get_selected_battle_skill_required_coord_count(),
		_runtime.get_selected_battle_skill_target_unit_ids(),
		_runtime.get_selected_battle_skill_variant_id(),
		Callable(_runtime, "preview_battle_command") if _runtime.has_method("preview_battle_command") else Callable(),
		_runtime.get_active_battle_encounter_name()
	)
	var units: Array[Dictionary] = []
	for unit_id_str in ProgressionDataUtils.sorted_string_keys(battle_state.units):
		var unit_id := StringName(unit_id_str)
		var unit_state: BattleUnitState = battle_state.units.get(unit_id) as BattleUnitState
		if unit_state == null:
			continue
		units.append({
			"unit_id": String(unit_state.unit_id),
			"display_name": unit_state.display_name if not unit_state.display_name.is_empty() else String(unit_state.unit_id),
			"coord": _coord_to_dict(unit_state.coord),
			"faction_id": String(unit_state.faction_id),
			"control_mode": String(unit_state.control_mode),
			"is_alive": unit_state.is_alive,
			"current_hp": int(unit_state.current_hp),
			"current_mp": int(unit_state.current_mp),
			"current_stamina": int(unit_state.current_stamina),
			"stamina_max": int(unit_state.attribute_snapshot.get_value(&"stamina_max")) if unit_state.attribute_snapshot != null else 0,
			"current_aura": int(unit_state.current_aura),
			"aura_max": int(unit_state.get_aura_max()),
			"current_shield_hp": int(unit_state.current_shield_hp),
			"shield_max_hp": int(unit_state.shield_max_hp),
			"shield_duration": int(unit_state.shield_duration),
			"shield_family": String(unit_state.shield_family),
			"current_ap": int(unit_state.current_ap),
			"current_move_points": int(unit_state.current_move_points),
		})
	return {
		"active": true,
		"encounter_id": String(_runtime.get_active_battle_encounter_id()),
		"encounter_name": _runtime.get_active_battle_encounter_name(),
		"phase": String(battle_state.phase),
		"active_unit_id": String(battle_state.active_unit_id),
		"active_unit_name": _runtime.get_battle_active_unit_name(),
		"modal_state": String(battle_state.modal_state),
		"winner_faction_id": String(battle_state.winner_faction_id),
		"selected_coord": _coord_to_dict(_runtime.get_battle_selected_coord()),
		"selected_skill_id": String(_runtime.get_selected_battle_skill_id()),
		"selected_skill_variant_id": String(_runtime.get_selected_battle_skill_variant_id()),
		"selected_target_coords": _coord_array_to_dict_array(_runtime.get_selected_battle_skill_target_coords()),
		"selected_target_unit_ids": _string_name_array_to_string_array(_runtime.get_selected_battle_skill_target_unit_ids()),
		"selected_target_unit_count": _runtime.get_selected_battle_skill_target_unit_ids().size(),
		"start_confirm_visible": _runtime.get_active_modal_id() == "battle_start_confirm",
		"start_prompt": _runtime.get_pending_battle_start_prompt(),
		"terrain_counts": _runtime.get_battle_terrain_counts(),
		"calamity_by_member_id": calamity_snapshot,
		"hud": hud_snapshot,
		"report_entry_count": battle_state.report_entries.size(),
		"report_entries": battle_state.report_entries.duplicate(true),
		"units": units,
	}


func _get_party_member_state_for_snapshot(member_id: StringName):
	if _runtime == null or member_id == &"":
		return null
	var party_state = _runtime.get_party_state() if _runtime.has_method("get_party_state") else null
	if party_state == null or not (party_state is Object) or not party_state.has_method("get_member_state"):
		return null
	return party_state.call("get_member_state", member_id)


func _build_reward_snapshot() -> Dictionary:
	var reward = _runtime.get_snapshot_reward()
	return {
		"visible": _runtime.get_active_modal_id() == "reward",
		"remaining_count": _runtime.get_pending_reward_count(),
		"reward": reward.to_dict() if reward != null and reward.has_method("to_dict") else {},
	}


func _build_loot_snapshot() -> Dictionary:
	if _runtime == null:
		return {}
	var loot_snapshot_variant = _runtime.get_last_battle_loot_snapshot()
	if loot_snapshot_variant is not Dictionary:
		return {}
	var loot_snapshot := (loot_snapshot_variant as Dictionary).duplicate(true)
	if loot_snapshot.is_empty():
		return {}
	if int(loot_snapshot.get("loot_entry_count", 0)) <= 0 and int(loot_snapshot.get("overflow_entry_count", 0)) <= 0:
		return {}
	return loot_snapshot


func _build_promotion_snapshot() -> Dictionary:
	var prompt: Dictionary = _runtime.get_current_promotion_prompt()
	return {
		"visible": _runtime.get_active_modal_id() == "promotion",
		"prompt": prompt.duplicate(true),
	}


func _build_log_snapshot(limit: int = 30) -> Dictionary:
	return _runtime.get_log_snapshot(limit) if _runtime != null else {}


func _resolve_contract_board_window_data() -> Dictionary:
	var window_data := _get_window_data_from_runtime("get_contract_board_window_data")
	if not window_data.is_empty():
		return window_data
	return _get_window_data_from_runtime("get_active_contract_board_context")


func _resolve_forge_window_data() -> Dictionary:
	var window_data := _get_window_data_from_runtime("get_forge_window_data")
	if not window_data.is_empty():
		return window_data
	var active_shop_context := _get_window_data_from_runtime("get_active_shop_context")
	if _window_data_matches_panel_kind(active_shop_context, "forge"):
		return active_shop_context
	var shop_window_data := _get_window_data_from_runtime("get_shop_window_data")
	if _window_data_matches_panel_kind(shop_window_data, "forge"):
		return shop_window_data
	return {}


func _get_window_data_from_runtime(method_name: String) -> Dictionary:
	if _runtime == null:
		return {}
	var window_data_variant = _runtime.call(method_name)
	return (window_data_variant as Dictionary).duplicate(true) if window_data_variant is Dictionary else {}


func _window_data_matches_panel_kind(window_data: Dictionary, panel_kind: String) -> bool:
	if window_data.is_empty():
		return false
	return String(window_data.get("panel_kind", "")) == panel_kind


func _coord_to_dict(coord: Vector2i) -> Dictionary:
	return {
		"x": coord.x,
		"y": coord.y,
	}


func _coord_array_to_dict_array(coords: Array[Vector2i]) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for coord in coords:
		result.append(_coord_to_dict(coord))
	return result


func _string_name_array_to_string_array(values: Array) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	return result


func _build_nearby_encounter_entries(limit: int = 8) -> Array[Dictionary]:
	return _runtime.get_nearby_encounter_entries(limit)


func _build_nearby_world_event_entries(limit: int = 8) -> Array[Dictionary]:
	return _runtime.get_nearby_world_event_entries(limit)
