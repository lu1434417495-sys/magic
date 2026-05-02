# Stable text snapshots for regression diffing and agent/debug inspection.
# This renderer is a development aid, not a player-facing presentation layer.
class_name GameTextSnapshotRenderer
extends RefCounted


static func render_full_snapshot(snapshot: Dictionary) -> String:
	var sections: Array[String] = []
	_append_section(sections, "SESSION", _build_session_lines(snapshot.get("session", {})))
	_append_section(sections, "STATUS", _build_status_lines(snapshot.get("status", {}), snapshot.get("modal", {})))
	_append_section(sections, "VALIDATION", _build_validation_lines(snapshot.get("validation", {})))
	_append_section(sections, "LOG", _build_log_lines(snapshot.get("logs", {})))
	_append_section(sections, "WORLD", _build_world_lines(snapshot.get("world", {})))
	_append_section(sections, "SUBMAP", _build_submap_lines(snapshot.get("submap", {})))
	_append_section(sections, "GAME_OVER", _build_game_over_lines(snapshot.get("game_over", {})))
	_append_section(sections, "PARTY", _build_party_lines(snapshot.get("party", {})))
	_append_section(sections, "QUEST", _build_quest_lines(snapshot.get("party", {}).get("quests", {})))
	_append_section(sections, "SETTLEMENT", _build_settlement_lines(snapshot.get("settlement", {})))
	_append_section(sections, "CONTRACT_BOARD", _build_contract_board_lines(snapshot.get("contract_board", {})))
	_append_section(sections, "SHOP", _build_shop_lines(snapshot.get("shop", {})))
	_append_section(sections, "FORGE", _build_forge_lines(snapshot.get("forge", {})))
	_append_section(sections, "STAGECOACH", _build_stagecoach_lines(snapshot.get("stagecoach", {})))
	_append_section(sections, "CHARACTER", _build_character_lines(snapshot.get("character_info", {})))
	_append_section(sections, "WAREHOUSE", _build_warehouse_lines(snapshot.get("warehouse", {})))
	_append_section(sections, "BATTLE", _build_battle_lines(snapshot.get("battle", {})))
	_append_section(sections, "LOOT", _build_loot_lines(snapshot.get("loot", {})))
	_append_section(sections, "REWARD", _build_reward_lines(snapshot.get("reward", {})))
	_append_section(sections, "PROMOTION", _build_promotion_lines(snapshot.get("promotion", {})))
	return "\n\n".join(PackedStringArray(sections))


static func render_world_snapshot(snapshot: Dictionary) -> String:
	return render_full_snapshot(snapshot)


static func _append_section(sections: Array[String], title: String, lines: Array[String]) -> void:
	if lines.is_empty():
		return
	sections.append("[%s]\n%s" % [title, "\n".join(PackedStringArray(lines))])


static func _build_session_lines(session: Dictionary) -> Array[String]:
	if session.is_empty():
		return []
	var lines: Array[String] = [
		"active_save_id=%s" % String(session.get("active_save_id", "")),
		"generation_config=%s" % String(session.get("generation_config_path", "")),
		"world_loaded=%s" % _format_bool(bool(session.get("world_loaded", false))),
	]
	var presets_variant = session.get("presets", [])
	if presets_variant is Array:
		var preset_labels: Array[String] = []
		for preset_variant in presets_variant:
			if preset_variant is not Dictionary:
				continue
			var preset: Dictionary = preset_variant
			preset_labels.append("%s:%s" % [
				String(preset.get("preset_id", "")),
				String(preset.get("display_name", "")),
			])
		lines.append("presets=%s" % " | ".join(PackedStringArray(preset_labels)))
	var save_slots_variant = session.get("save_slots", [])
	if save_slots_variant is Array:
		lines.append("save_slot_count=%d" % save_slots_variant.size())
		for save_slot_variant in save_slots_variant:
			if save_slot_variant is not Dictionary:
				continue
			var save_slot: Dictionary = save_slot_variant
			lines.append("save=%s | %s | %s" % [
				String(save_slot.get("save_id", "")),
				String(save_slot.get("display_name", "")),
				String(save_slot.get("world_preset_name", "")),
			])
	return lines


static func _build_status_lines(status: Dictionary, modal: Dictionary) -> Array[String]:
	return [
		"view=%s" % String(status.get("view", "")),
		"modal=%s" % String(modal.get("id", "")),
		"text=%s" % String(status.get("text", "")),
	]


static func _build_validation_lines(validation_snapshot: Dictionary) -> Array[String]:
	if validation_snapshot.is_empty():
		return []
	var lines: Array[String] = [
		"ok=%s" % _format_bool(bool(validation_snapshot.get("ok", false))),
		"error_count=%d" % int(validation_snapshot.get("error_count", 0)),
	]
	var domains_variant = validation_snapshot.get("domains", {})
	if domains_variant is not Dictionary:
		return lines
	var domains := domains_variant as Dictionary
	var domain_order_variant = validation_snapshot.get("domain_order", [])
	var domain_ids: Array[String] = []
	if domain_order_variant is Array:
		for domain_id_variant in domain_order_variant:
			domain_ids.append(String(domain_id_variant))
	if domain_ids.is_empty():
		for domain_key_variant in domains.keys():
			domain_ids.append(String(domain_key_variant))
		domain_ids.sort()
	for domain_id in domain_ids:
		var domain_snapshot_variant = domains.get(domain_id, {})
		if domain_snapshot_variant is not Dictionary:
			continue
		var domain_snapshot := domain_snapshot_variant as Dictionary
		lines.append("domain=%s | errors=%d" % [
			domain_id,
			int(domain_snapshot.get("error_count", 0)),
		])
		var errors_variant = domain_snapshot.get("errors", [])
		if errors_variant is not Array:
			continue
		for error_variant in errors_variant:
			lines.append("  - %s" % String(error_variant))
	return lines


static func _build_log_lines(log_snapshot: Dictionary) -> Array[String]:
	if log_snapshot.is_empty():
		return []
	var lines: Array[String] = []
	var file_name := _extract_log_file_name(log_snapshot)
	if not file_name.is_empty():
		lines.append("file_name=%s" % file_name)
	lines.append("entry_count=%d" % int(log_snapshot.get("entry_count", 0)))
	var entries_variant = log_snapshot.get("entries", [])
	if entries_variant is Array:
		for entry_variant in entries_variant:
			if entry_variant is not Dictionary:
				continue
			var entry: Dictionary = entry_variant
			lines.append("entry=%d | %s | %s | %s | %s" % [
				int(entry.get("seq", 0)),
				String(entry.get("level", "")),
				String(entry.get("domain", "")),
				String(entry.get("event_id", "")),
				String(entry.get("message", "")),
			])
	return lines


static func _extract_log_file_name(log_snapshot: Dictionary) -> String:
	var virtual_path := String(log_snapshot.get("virtual_path", ""))
	if not virtual_path.is_empty():
		var virtual_file_name := virtual_path.get_file()
		if not virtual_file_name.is_empty():
			return virtual_file_name
	var file_path := String(log_snapshot.get("file_path", ""))
	if file_path.is_empty():
		return ""
	return file_path.get_file()


static func _build_world_lines(world: Dictionary) -> Array[String]:
	if world.is_empty():
		return []
	var lines: Array[String] = [
		"map_id=%s" % String(world.get("map_id", "")),
		"map_display_name=%s" % String(world.get("map_display_name", "")),
		"is_submap=%s" % _format_bool(bool(world.get("is_submap", false))),
		"world_step=%d" % int(world.get("world_step", 0)),
		"player_coord=%s" % _format_coord(world.get("player_coord", {})),
		"player_visible_on_map=%s" % _format_bool(bool(world.get("player_visible_on_map", true))),
		"selected_coord=%s" % _format_coord(world.get("selected_coord", {})),
		"selected_settlement_id=%s" % String(world.get("selected_settlement_id", "")),
		"selected_npc_name=%s" % String(world.get("selected_npc_name", "")),
		"selected_world_event_id=%s" % String(world.get("selected_world_event_id", "")),
		"selected_world_event_name=%s" % String(world.get("selected_world_event_name", "")),
		"selected_encounter_id=%s" % String(world.get("selected_encounter_id", "")),
		"selected_encounter_name=%s" % String(world.get("selected_encounter_name", "")),
	]
	var nearby_events_variant = world.get("nearby_world_events", [])
	if nearby_events_variant is Array:
		for world_event_variant in nearby_events_variant:
			if world_event_variant is not Dictionary:
				continue
			var world_event: Dictionary = world_event_variant
			lines.append("nearby_world_event=%s | %s | distance=%d | coord=%s" % [
				String(world_event.get("event_id", "")),
				String(world_event.get("display_name", "")),
				int(world_event.get("distance", 0)),
				_format_coord(world_event.get("coord", {})),
			])
	var nearby_variant = world.get("nearby_encounters", [])
	if nearby_variant is Array:
		for encounter_variant in nearby_variant:
			if encounter_variant is not Dictionary:
				continue
			var encounter: Dictionary = encounter_variant
			lines.append("nearby_encounter=%s | %s | distance=%d | coord=%s" % [
				String(encounter.get("entity_id", "")),
				String(encounter.get("display_name", "")),
				int(encounter.get("distance", 0)),
				_format_coord(encounter.get("coord", {})),
			])
	return lines


static func _build_submap_lines(submap: Dictionary) -> Array[String]:
	if submap.is_empty():
		return []
	var prompt: Dictionary = submap.get("prompt", {})
	return [
		"active=%s" % _format_bool(bool(submap.get("active", false))),
		"map_id=%s" % String(submap.get("map_id", "")),
		"map_display_name=%s" % String(submap.get("map_display_name", "")),
		"return_hint=%s" % String(submap.get("return_hint_text", "")),
		"confirm_visible=%s" % _format_bool(bool(submap.get("confirm_visible", false))),
		"prompt_title=%s" % String(prompt.get("title", "")),
		"prompt_target=%s" % String(prompt.get("target_display_name", "")),
	]


static func _build_game_over_lines(game_over: Dictionary) -> Array[String]:
	if game_over.is_empty():
		return []
	return [
		"title=%s" % String(game_over.get("title", "")),
		"description=%s" % String(game_over.get("description", "")),
		"confirm_text=%s" % String(game_over.get("confirm_text", "")),
		"main_character_member_id=%s" % String(game_over.get("main_character_member_id", "")),
		"main_character_name=%s" % String(game_over.get("main_character_name", "")),
		"main_character_dead=%s" % _format_bool(bool(game_over.get("main_character_dead", false))),
	]


static func _build_party_lines(party: Dictionary) -> Array[String]:
	if party.is_empty():
		return []
	var lines: Array[String] = [
		"gold=%d" % int(party.get("gold", 0)),
		"leader_member_id=%s" % String(party.get("leader_member_id", "")),
		"active_member_ids=%s" % _format_array(party.get("active_member_ids", [])),
		"reserve_member_ids=%s" % _format_array(party.get("reserve_member_ids", [])),
		"selected_member_id=%s" % String(party.get("selected_member_id", "")),
		"pending_reward_count=%d" % int(party.get("pending_reward_count", 0)),
	]
	var members_variant = party.get("members", [])
	if members_variant is Array:
		for member_variant in members_variant:
			if member_variant is not Dictionary:
				continue
			var member: Dictionary = member_variant
			var achievement_summary: Dictionary = member.get("achievement_summary", {})
			var attributes: Dictionary = member.get("attributes", {})
			lines.append("member=%s | %s | hp=%d mp=%d | leader=%s | unlocked=%d in_progress=%d recent=%s | ac=%d | equip=%s" % [
				String(member.get("member_id", "")),
				String(member.get("roster_role", "")),
				int(member.get("current_hp", 0)),
				int(member.get("current_mp", 0)),
				_format_bool(bool(member.get("is_leader", false))),
				int(achievement_summary.get("unlocked_count", 0)),
				int(achievement_summary.get("in_progress_count", 0)),
				String(achievement_summary.get("recent_unlocked_name", "")),
				int(attributes.get("armor_class", 0)),
				_format_equipment(member.get("equipment", [])),
			])
	return lines


static func _build_quest_lines(quests: Dictionary) -> Array[String]:
	if quests.is_empty():
		return []
	var lines: Array[String] = [
		"active_quest_ids=%s" % _format_array(quests.get("active_quest_ids", [])),
		"claimable_quest_ids=%s" % _format_array(quests.get("claimable_quest_ids", [])),
		"completed_quest_ids=%s" % _format_array(quests.get("completed_quest_ids", [])),
	]
	_append_quest_detail_lines(lines, quests.get("active_quests", []))
	_append_quest_detail_lines(lines, quests.get("claimable_quests", []))
	return lines


static func _append_quest_detail_lines(lines: Array[String], quest_variants) -> void:
	if quest_variants is not Array:
		return
	for quest_variant in quest_variants:
		if quest_variant is not Dictionary:
			continue
		var quest: Dictionary = quest_variant
		if not quest.has("stage_id"):
			continue
		var stage_variant = quest["stage_id"]
		if stage_variant is not String and stage_variant is not StringName:
			continue
		var stage_id := String(stage_variant)
		if stage_id.is_empty():
			continue
		lines.append("quest=%s | stage=%s | status=%s | progress=%s | accepted=%d | completed=%d | rewarded=%d | context=%s" % [
			String(quest.get("quest_id", "")),
			stage_id,
			String(quest.get("status_id", "")),
			_format_quest_progress(quest.get("objective_progress", {})),
			int(quest.get("accepted_at_world_step", -1)),
			int(quest.get("completed_at_world_step", -1)),
			int(quest.get("reward_claimed_at_world_step", -1)),
			_format_key_value_pairs(quest.get("last_progress_context", {})),
		])


static func _build_shop_lines(shop_snapshot: Dictionary) -> Array[String]:
	if shop_snapshot.is_empty():
		return []
	var window_data: Dictionary = shop_snapshot.get("window_data", {})
	var lines: Array[String] = [
		"visible=%s" % _format_bool(bool(shop_snapshot.get("visible", false))),
		"title=%s" % String(window_data.get("title", "")),
		"settlement_id=%s" % String(window_data.get("settlement_id", "")),
	]
	var entries_variant = window_data.get("entries", [])
	if entries_variant is Array:
		for entry_variant in entries_variant:
			if entry_variant is not Dictionary:
				continue
			var entry: Dictionary = entry_variant
			lines.append("entry=%s | state=%s | cost=%s" % [
				String(entry.get("display_name", "")),
				String(entry.get("state_label", "")),
				String(entry.get("cost_label", "")),
			])
	return lines


static func _build_contract_board_lines(contract_board_snapshot: Dictionary) -> Array[String]:
	if contract_board_snapshot.is_empty():
		return []
	var window_data: Dictionary = contract_board_snapshot.get("window_data", {})
	var lines: Array[String] = [
		"visible=%s" % _format_bool(bool(contract_board_snapshot.get("visible", false))),
		"title=%s" % String(window_data.get("title", "")),
		"settlement_id=%s" % String(window_data.get("settlement_id", "")),
		"provider_interaction_id=%s" % String(window_data.get("provider_interaction_id", "")),
	]
	var entries_variant = window_data.get("entries", [])
	if entries_variant is Array:
		for entry_variant in entries_variant:
			if entry_variant is not Dictionary:
				continue
			var entry: Dictionary = entry_variant
			lines.append("entry=%s | state=%s | reward=%s" % [
				String(entry.get("display_name", "")),
				String(entry.get("state_label", "")),
				String(entry.get("cost_label", "")),
			])
	return lines


static func _build_stagecoach_lines(stagecoach_snapshot: Dictionary) -> Array[String]:
	if stagecoach_snapshot.is_empty():
		return []
	var window_data: Dictionary = stagecoach_snapshot.get("window_data", {})
	var lines: Array[String] = [
		"visible=%s" % _format_bool(bool(stagecoach_snapshot.get("visible", false))),
		"title=%s" % String(window_data.get("title", "")),
		"settlement_id=%s" % String(window_data.get("settlement_id", "")),
	]
	var entries_variant = window_data.get("entries", [])
	if entries_variant is Array:
		for entry_variant in entries_variant:
			if entry_variant is not Dictionary:
				continue
			var entry: Dictionary = entry_variant
			lines.append("route=%s | state=%s | cost=%s" % [
				String(entry.get("display_name", "")),
				String(entry.get("state_label", "")),
				String(entry.get("cost_label", "")),
			])
	return lines


static func _build_forge_lines(forge_snapshot: Dictionary) -> Array[String]:
	if forge_snapshot.is_empty():
		return []
	var window_data: Dictionary = forge_snapshot.get("window_data", {})
	var lines: Array[String] = [
		"visible=%s" % _format_bool(bool(forge_snapshot.get("visible", false))),
		"title=%s" % String(window_data.get("title", "")),
		"settlement_id=%s" % String(window_data.get("settlement_id", "")),
	]
	var entries_variant = window_data.get("entries", [])
	if entries_variant is Array:
		for entry_variant in entries_variant:
			if entry_variant is not Dictionary:
				continue
			var entry: Dictionary = entry_variant
			lines.append("entry=%s | state=%s | cost=%s" % [
				String(entry.get("display_name", "")),
				String(entry.get("state_label", "")),
				String(entry.get("cost_label", "")),
			])
	return lines


static func _build_settlement_lines(settlement: Dictionary) -> Array[String]:
	if settlement.is_empty():
		return []
	var lines: Array[String] = [
		"visible=%s" % _format_bool(bool(settlement.get("visible", false))),
		"settlement_id=%s" % String(settlement.get("settlement_id", "")),
		"display_name=%s" % String(settlement.get("display_name", "")),
		"tier_name=%s" % String(settlement.get("tier_name", "")),
		"faction_id=%s" % String(settlement.get("faction_id", "")),
		"feedback=%s" % String(settlement.get("feedback_text", "")),
	]
	var services_variant = settlement.get("services", [])
	if services_variant is Array:
		for service_variant in services_variant:
			if service_variant is not Dictionary:
				continue
			var service: Dictionary = service_variant
			lines.append("service=%s | %s | %s | %s | %s" % [
				String(service.get("action_id", "")),
				String(service.get("facility_name", "")),
				String(service.get("npc_name", "")),
				String(service.get("service_type", "")),
				String(service.get("interaction_script_id", "")),
			])
	return lines


static func _build_character_lines(character_info: Dictionary) -> Array[String]:
	if character_info.is_empty():
		return []
	var lines: Array[String] = [
		"visible=%s" % _format_bool(bool(character_info.get("visible", false))),
		"source=%s" % String(character_info.get("source", "")),
		"display_name=%s" % String(character_info.get("display_name", "")),
		"meta_label=%s" % String(character_info.get("meta_label", "")),
		"status_label=%s" % String(character_info.get("status_label", "")),
	]
	var sections_variant = character_info.get("sections", [])
	if sections_variant is Array:
		for section_variant in sections_variant:
			if section_variant is not Dictionary:
				continue
			var section: Dictionary = section_variant
			lines.append("section=%s | entries=%d" % [
				String(section.get("title", "")),
				_count_character_section_entries(section.get("entries", [])),
			])
	return lines


static func _count_character_section_entries(entries_variant: Variant) -> int:
	if entries_variant is Array:
		return (entries_variant as Array).size()
	if entries_variant is PackedStringArray:
		return (entries_variant as PackedStringArray).size()
	return 0


static func _build_warehouse_lines(warehouse: Dictionary) -> Array[String]:
	if warehouse.is_empty():
		return []
	var window_data: Dictionary = warehouse.get("window_data", {})
	var lines: Array[String] = [
		"visible=%s" % _format_bool(bool(warehouse.get("visible", false))),
		"entry_label=%s" % String(warehouse.get("entry_label", "")),
		"title=%s" % String(window_data.get("title", "")),
		"meta=%s" % String(window_data.get("meta", "")),
		"summary=%s" % String(window_data.get("summary_text", "")),
		"status=%s" % String(window_data.get("status_text", "")),
	]
	var entries_variant = window_data.get("entries", [])
	if entries_variant is Array:
		for entry_variant in entries_variant:
			if entry_variant is not Dictionary:
				continue
			var entry: Dictionary = entry_variant
			lines.append("entry=%s | qty=%d | total=%d | stackable=%s | limit=%d | mode=%s" % [
				String(entry.get("item_id", "")),
				int(entry.get("quantity", 0)),
				int(entry.get("total_quantity", 0)),
				_format_bool(bool(entry.get("is_stackable", false))),
				int(entry.get("stack_limit", 0)),
				String(entry.get("storage_mode", "")),
			])
	return lines


static func _build_battle_lines(battle: Dictionary) -> Array[String]:
	if battle.is_empty():
		return []
	var start_prompt: Dictionary = battle.get("start_prompt", {})
	var lines: Array[String] = [
		"active=%s" % _format_bool(bool(battle.get("active", false))),
		"encounter_id=%s" % String(battle.get("encounter_id", "")),
		"encounter_name=%s" % String(battle.get("encounter_name", "")),
		"phase=%s" % String(battle.get("phase", "")),
		"active_unit_id=%s" % String(battle.get("active_unit_id", "")),
		"active_unit_name=%s" % String(battle.get("active_unit_name", "")),
		"modal_state=%s" % String(battle.get("modal_state", "")),
		"winner_faction_id=%s" % String(battle.get("winner_faction_id", "")),
		"selected_coord=%s" % _format_coord(battle.get("selected_coord", {})),
		"selected_skill_id=%s" % String(battle.get("selected_skill_id", "")),
		"selected_skill_variant_id=%s" % String(battle.get("selected_skill_variant_id", "")),
		"selected_target_coords=%s" % _format_coord_array(battle.get("selected_target_coords", [])),
		"selected_target_unit_ids=%s" % _format_array(battle.get("selected_target_unit_ids", [])),
		"selected_target_unit_count=%d" % int(battle.get("selected_target_unit_count", 0)),
		"start_confirm_visible=%s" % _format_bool(bool(battle.get("start_confirm_visible", false))),
		"start_prompt_title=%s" % String(start_prompt.get("title", "")),
		"start_prompt_description=%s" % String(start_prompt.get("description", "")),
		"start_prompt_confirm_text=%s" % String(start_prompt.get("confirm_text", "")),
	]
	var calamity_snapshot: Dictionary = battle.get("calamity_by_member_id", {})
	if not calamity_snapshot.is_empty():
		lines.append("calamity=%s" % _format_key_value_pairs(calamity_snapshot))
	var hud: Dictionary = battle.get("hud", {})
	if not hud.is_empty():
		lines.append("hud_header=%s" % String(hud.get("header_subtitle", "")))
		lines.append("hud_round=%s" % String(hud.get("round_badge", "")))
		lines.append("hud_command=%s" % String(hud.get("command_text", hud.get("skill_subtitle", ""))))
		lines.append("hud_log=%s" % String(hud.get("log_text", "")))
	lines.append("report_entry_count=%d" % int(battle.get("report_entry_count", 0)))
	var report_entries_variant = battle.get("report_entries", [])
	if report_entries_variant is Array:
		for report_entry_variant in report_entries_variant:
			if report_entry_variant is not Dictionary:
				continue
			var report_entry: Dictionary = report_entry_variant
			var report_type := String(report_entry.get("type", ""))
			if report_type == "change_equipment":
				lines.append(_build_change_equipment_report_line(report_entry))
				continue
			if report_type.is_empty() and String(report_entry.get("entry_type", "")) == "change_equipment":
				continue
			lines.append("report=%s | reason=%s | tags=%s | text=%s" % [
				String(report_entry.get("entry_type", "")),
				String(report_entry.get("reason_id", "")),
				_format_array(report_entry.get("event_tags", [])),
				String(report_entry.get("text", "")),
			])
	var party_backpack: Dictionary = battle.get("party_backpack", {})
	if not party_backpack.is_empty():
		lines.append("backpack_used_slots=%d | stacks=%d | equipment_instances=%d" % [
			int(party_backpack.get("used_slots", 0)),
			int(party_backpack.get("stack_count", 0)),
			int(party_backpack.get("equipment_instance_count", 0)),
		])
		var stack_entries_variant = party_backpack.get("stacks", [])
		if stack_entries_variant is Array:
			for stack_entry_variant in stack_entries_variant:
				if stack_entry_variant is not Dictionary:
					continue
				var stack_entry: Dictionary = stack_entry_variant
				lines.append("backpack_stack=%s | qty=%d" % [
					String(stack_entry.get("item_id", "")),
					int(stack_entry.get("quantity", 0)),
				])
		var equipment_entries_variant = party_backpack.get("equipment_instances", [])
		if equipment_entries_variant is Array:
			for equipment_entry_variant in equipment_entries_variant:
				if equipment_entry_variant is not Dictionary:
					continue
				var equipment_entry: Dictionary = equipment_entry_variant
				lines.append("backpack_equipment=%s | %s" % [
					String(equipment_entry.get("instance_id", "")),
					String(equipment_entry.get("item_id", "")),
				])
	var units_variant = battle.get("units", [])
	if units_variant is Array:
		for unit_variant in units_variant:
			if unit_variant is not Dictionary:
				continue
			var unit: Dictionary = unit_variant
			lines.append("unit=%s | %s | %s | hp=%d/%d mp=%d st=%d/%d au=%d/%d shield=%d/%d dur=%d ap=%d move=%d | alive=%s | coord=%s | equip=%s" % [
				String(unit.get("unit_id", "")),
				String(unit.get("display_name", "")),
				String(unit.get("faction_id", "")),
				int(unit.get("current_hp", 0)),
				int(unit.get("hp_max", 0)),
				int(unit.get("current_mp", 0)),
				int(unit.get("current_stamina", 0)),
				int(unit.get("stamina_max", 0)),
				int(unit.get("current_aura", 0)),
				int(unit.get("aura_max", 0)),
				int(unit.get("current_shield_hp", 0)),
				int(unit.get("shield_max_hp", 0)),
				int(unit.get("shield_duration", -1)),
				int(unit.get("current_ap", 0)),
				int(unit.get("current_move_points", 0)),
				_format_bool(bool(unit.get("is_alive", false))),
				_format_coord(unit.get("coord", {})),
				_format_battle_equipment(unit.get("equipment", [])),
			])
	return lines


static func _build_change_equipment_report_line(report_entry: Dictionary) -> String:
	return "report=change_equipment | ok=%s | error=%s | op=%s | unit=%s | target=%s | slot=%s | item=%s | instance=%s | ap=%d>%d | hp=%d/%d>%d/%d | hp_clamped=%s | text=%s" % [
		_format_bool(bool(report_entry.get("ok", false))),
		String(report_entry.get("error_code", "")),
		String(report_entry.get("operation", "")),
		String(report_entry.get("unit_id", "")),
		String(report_entry.get("target_unit_id", "")),
		String(report_entry.get("slot_id", "")),
		String(report_entry.get("item_id", "")),
		String(report_entry.get("instance_id", "")),
		int(report_entry.get("ap_before", 0)),
		int(report_entry.get("ap_after", 0)),
		int(report_entry.get("hp_before", 0)),
		int(report_entry.get("hp_max_before", 0)),
		int(report_entry.get("hp_after", 0)),
		int(report_entry.get("hp_max_after", 0)),
		_format_bool(bool(report_entry.get("hp_clamped", false))),
		String(report_entry.get("text", "")),
	]


static func _build_loot_lines(loot: Dictionary) -> Array[String]:
	if loot.is_empty():
		return []
	var lines: Array[String] = [
		"battle_name=%s" % String(loot.get("battle_name", "")),
		"winner_faction_id=%s" % String(loot.get("winner_faction_id", "")),
		"loot_entry_count=%d" % int(loot.get("loot_entry_count", 0)),
		"loot_summary=%s" % String(loot.get("loot_summary_text", "")),
		"overflow_entry_count=%d" % int(loot.get("overflow_entry_count", 0)),
		"overflow_summary=%s" % String(loot.get("overflow_summary_text", "")),
	]
	return lines


static func _build_reward_lines(reward_snapshot: Dictionary) -> Array[String]:
	if reward_snapshot.is_empty():
		return []
	var reward: Dictionary = reward_snapshot.get("reward", {})
	var lines: Array[String] = [
		"visible=%s" % _format_bool(bool(reward_snapshot.get("visible", false))),
		"remaining_count=%d" % int(reward_snapshot.get("remaining_count", 0)),
		"reward_id=%s" % String(reward.get("reward_id", "")),
		"member_id=%s" % String(reward.get("member_id", "")),
		"member_name=%s" % String(reward.get("member_name", "")),
		"source_label=%s" % String(reward.get("source_label", "")),
		"summary=%s" % String(reward.get("summary_text", "")),
	]
	var entries_variant = reward.get("entries", [])
	if entries_variant is Array:
		for entry_variant in entries_variant:
			if entry_variant is not Dictionary:
				continue
			var entry: Dictionary = entry_variant
			lines.append("entry=%s | %s | amount=%d | reason=%s" % [
				String(entry.get("entry_type", "")),
				String(entry.get("target_id", "")),
				int(entry.get("amount", 0)),
				String(entry.get("reason_text", "")),
			])
	return lines


static func _build_promotion_lines(promotion: Dictionary) -> Array[String]:
	if promotion.is_empty():
		return []
	var prompt: Dictionary = promotion.get("prompt", {})
	var lines: Array[String] = [
		"visible=%s" % _format_bool(bool(promotion.get("visible", false))),
		"member_id=%s" % String(prompt.get("member_id", "")),
		"member_name=%s" % String(prompt.get("member_name", "")),
	]
	var choices_variant = prompt.get("choices", [])
	if choices_variant is Array:
		for choice_variant in choices_variant:
			if choice_variant is not Dictionary:
				continue
			var choice: Dictionary = choice_variant
			lines.append("choice=%s | %s | %s | skills=%s" % [
				String(choice.get("profession_id", "")),
				String(choice.get("display_name", "")),
				String(choice.get("summary", "")),
				_format_array(choice.get("granted_skill_ids", [])),
			])
	return lines


static func _format_bool(value: bool) -> String:
	return "true" if value else "false"


static func _format_coord(coord_variant: Variant) -> String:
	if coord_variant is Dictionary:
		return "(%d,%d)" % [int(coord_variant.get("x", 0)), int(coord_variant.get("y", 0))]
	return "(0,0)"


static func _format_coord_array(coords_variant: Variant) -> String:
	if coords_variant is not Array:
		return ""
	var parts: Array[String] = []
	for coord_variant in coords_variant:
		parts.append(_format_coord(coord_variant))
	return " ".join(PackedStringArray(parts))


static func _format_array(values_variant: Variant) -> String:
	if values_variant is not Array:
		return ""
	var parts: Array[String] = []
	for value in values_variant:
		parts.append(String(value))
	return " ".join(PackedStringArray(parts))


static func _format_equipment(entries_variant: Variant) -> String:
	if entries_variant is not Array:
		return ""
	var parts: Array[String] = []
	for entry_variant in entries_variant:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		parts.append("%s:%s" % [
			String(entry.get("slot_id", "")),
			String(entry.get("item_id", "")),
		])
	return " ".join(PackedStringArray(parts))


static func _format_battle_equipment(entries_variant: Variant) -> String:
	if entries_variant is not Array:
		return ""
	var parts: Array[String] = []
	for entry_variant in entries_variant:
		if entry_variant is not Dictionary:
			continue
		var entry: Dictionary = entry_variant
		var instance_id := String(entry.get("instance_id", ""))
		if instance_id.is_empty():
			parts.append("%s:%s" % [
				String(entry.get("slot_id", "")),
				String(entry.get("item_id", "")),
			])
		else:
			parts.append("%s:%s#%s" % [
				String(entry.get("slot_id", "")),
				String(entry.get("item_id", "")),
				instance_id,
			])
	return " ".join(PackedStringArray(parts))


static func _format_quest_progress(progress_variant: Variant) -> String:
	if progress_variant is not Dictionary:
		return ""
	var progress: Dictionary = progress_variant
	var keys := ProgressionDataUtils.sorted_string_keys(progress)
	var parts: Array[String] = []
	for key in keys:
		parts.append("%s:%d" % [key, int(progress.get(key, 0))])
	return " ".join(PackedStringArray(parts))


static func _format_key_value_pairs(value_variant: Variant) -> String:
	if value_variant is not Dictionary:
		return ""
	var value: Dictionary = value_variant
	var keys := ProgressionDataUtils.sorted_string_keys(value)
	var parts: Array[String] = []
	for key in keys:
		parts.append("%s=%s" % [key, str(value.get(key, ""))])
	return " ".join(PackedStringArray(parts))
