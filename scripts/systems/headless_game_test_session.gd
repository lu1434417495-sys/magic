# Development-only headless bridge for automation and debugging.
# This is not a player-facing startup path or UI layer.
class_name HeadlessGameTestSession
extends RefCounted

const GAME_SESSION_SCRIPT = preload("res://scripts/systems/game_session.gd")
const GAME_RUNTIME_FACADE_SCRIPT = preload("res://scripts/systems/game_runtime_facade.gd")
const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle_command.gd")
const ENCOUNTER_ANCHOR_DATA_SCRIPT = preload("res://scripts/systems/encounter_anchor_data.gd")
const ENCOUNTER_ROSTER_BUILDER_SCRIPT = preload("res://scripts/systems/encounter_roster_builder.gd")
const PROGRESSION_DATA_UTILS_SCRIPT = preload("res://scripts/player/progression/progression_data_utils.gd")
const WORLD_PRESET_REGISTRY_SCRIPT = preload("res://scripts/utils/world_preset_registry.gd")
const GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT = preload("res://scripts/utils/game_text_snapshot_renderer.gd")
const HEADLESS_SETTLEMENT_LOOT_PROFILE_ID: StringName = &"wolf_den"
const HEADLESS_SETTLEMENT_LOOT_ENCOUNTER_ID: StringName = &"headless_settlement_wolf_den"
const HEADLESS_SETTLEMENT_LOOT_DISPLAY_NAME := "荒狼巢穴"

var _game_session = null
var _runtime = null
var _owns_game_session := false
var _active_headless_encounter_anchor = null


func initialize() -> void:
	await _ensure_game_session()


func get_game_session():
	return _game_session


func get_runtime_facade():
	return _runtime


func has_world_loaded() -> bool:
	return _runtime != null


func list_presets() -> Array[Dictionary]:
	return WORLD_PRESET_REGISTRY_SCRIPT.list_presets()


func list_save_slots() -> Array[Dictionary]:
	await _ensure_game_session()
	return _game_session.list_save_slots()


func create_new_game(preset_id: StringName) -> Dictionary:
	await _ensure_game_session()
	var preset: Dictionary = WORLD_PRESET_REGISTRY_SCRIPT.get_preset(preset_id)
	if preset.is_empty():
		return {
			"ok": false,
			"message": "未找到世界预设 %s。" % String(preset_id),
		}
	await _unload_world_scene()
	var create_error := int(_game_session.create_new_save(
		String(preset.get("generation_config_path", "")),
		preset_id,
		String(preset.get("display_name", "世界"))
	))
	if create_error != OK:
		return {
			"ok": false,
			"message": "创建世界失败，错误码 %d。" % create_error,
		}
	return await ensure_world_loaded()


func load_game(save_id: String) -> Dictionary:
	await _ensure_game_session()
	if save_id.is_empty():
		return {
			"ok": false,
			"message": "存档 ID 不能为空。",
		}
	await _unload_world_scene()
	var load_error := int(_game_session.load_save(save_id))
	if load_error != OK:
		return {
			"ok": false,
			"message": "加载存档失败，错误码 %d。" % load_error,
		}
	return await ensure_world_loaded()


func ensure_world_loaded() -> Dictionary:
	await _ensure_game_session()
	if not _game_session.has_active_world():
		return {
			"ok": false,
			"message": "当前没有已加载的世界。",
		}
	if has_world_loaded():
		await settle_frames()
		return {
			"ok": true,
			"message": "世界地图已可用。",
		}
	_runtime = GAME_RUNTIME_FACADE_SCRIPT.new()
	_runtime.setup(_game_session)
	await settle_frames()
	return {
		"ok": true,
		"message": "世界地图已载入。",
	}


func settle_frames(frame_count: int = 2) -> void:
	var scene_tree := _get_scene_tree()
	if scene_tree == null:
		return
	for _index in range(maxi(frame_count, 1)):
		await scene_tree.process_frame


func set_party_storage_capacity(capacity: int) -> Dictionary:
	if not has_world_loaded() or _runtime == null:
		return {
			"ok": false,
			"message": "当前世界地图不可用。",
		}
	var party_state = _runtime.get_party_state()
	if party_state == null:
		return {
			"ok": false,
			"message": "当前不存在队伍数据。",
		}
	var resolved_capacity := maxi(capacity, 0)
	var first_member_assigned := false
	for member_variant in party_state.member_states.values():
		var member_state = member_variant
		if member_state == null or member_state.progression == null:
			continue
		var unit_base_attributes = member_state.progression.unit_base_attributes
		if unit_base_attributes == null:
			continue
		unit_base_attributes.custom_stats[&"storage_space"] = resolved_capacity if not first_member_assigned else 0
		first_member_assigned = true
	await settle_frames(1)
	if not first_member_assigned:
		return {
			"ok": false,
			"message": "当前队伍没有可调整仓库容量的成员。",
		}
	return {
		"ok": true,
		"message": "已将共享仓库总容量调整为 %d。" % resolved_capacity,
	}


func start_battle_by_kind(encounter_kind: StringName) -> Dictionary:
	if not has_world_loaded() or _runtime == null:
		return {
			"ok": false,
			"message": "当前世界地图不可用。",
		}
	if _runtime.is_battle_active():
		return {
			"ok": false,
			"message": "当前已有进行中的战斗。",
		}
	var encounter_anchor: EncounterAnchorData = _find_nearest_encounter_anchor(encounter_kind)
	if encounter_anchor == null:
		encounter_anchor = _build_headless_encounter_anchor(encounter_kind)
	if encounter_anchor == null:
		return {
			"ok": false,
			"message": "未找到 encounter_kind=%s 的遭遇。" % String(encounter_kind),
		}
	_active_headless_encounter_anchor = encounter_anchor
	_game_session.set_battle_save_lock(true)
	_runtime.start_battle(encounter_anchor)
	await settle_frames(1)
	if not _runtime.is_battle_active():
		_active_headless_encounter_anchor = null
		_game_session.set_battle_save_lock(false)
		return {
			"ok": false,
			"message": "遭遇 %s 未能开始战斗。" % String(encounter_anchor.display_name),
		}
	return {
		"ok": true,
		"message": "已进入遭遇 %s 的战斗准备。" % String(encounter_anchor.display_name),
	}


func finish_active_battle(winner_faction_id: StringName) -> Dictionary:
	if not has_world_loaded() or _runtime == null:
		return {
			"ok": false,
			"message": "当前世界地图不可用。",
		}
	if not _runtime.is_battle_active():
		return {
			"ok": false,
			"message": "当前没有进行中的战斗。",
		}
	if winner_faction_id != &"player" and winner_faction_id != &"hostile":
		return {
			"ok": false,
			"message": "胜利方只能是 player 或 hostile。",
		}
	var battle_state = _runtime.get_battle_state()
	if battle_state == null or battle_state.is_empty():
		return {
			"ok": false,
			"message": "当前战斗状态不可用。",
		}
	_prime_headless_battle_loot_if_needed(winner_faction_id)
	battle_state.phase = &"battle_ended"
	battle_state.winner_faction_id = winner_faction_id
	battle_state.active_unit_id = &""
	battle_state.timeline.ready_unit_ids.clear()
	battle_state.timeline.frozen = true
	_runtime.refresh_battle_runtime_state()
	var result: Dictionary = _runtime.command_battle_wait_or_resolve()
	_active_headless_encounter_anchor = null
	await settle_frames(1)
	return result


func change_battle_equipment(
	operation: StringName,
	slot_id: StringName,
	item_id: StringName = &"",
	instance_id: StringName = &"",
	options: Dictionary = {}
) -> Dictionary:
	if not has_world_loaded() or _runtime == null:
		return {
			"ok": false,
			"message": "当前世界地图不可用。",
		}
	if not _runtime.is_battle_active():
		return {
			"ok": false,
			"message": "当前没有进行中的战斗。",
		}
	var battle_state = _runtime.get_battle_state()
	if battle_state == null or battle_state.is_empty():
		return {
			"ok": false,
			"message": "当前战斗状态不可用。",
		}
	if String(battle_state.phase) != "unit_acting" or battle_state.active_unit_id == &"":
		return {
			"ok": false,
			"message": "当前没有可手动操作的行动单位。",
		}
	if String(battle_state.modal_state) != "":
		return {
			"ok": false,
			"message": "当前战斗流程阻止换装。",
		}
	var active_unit = battle_state.units.get(battle_state.active_unit_id)
	if active_unit == null or not bool(active_unit.is_alive):
		return {
			"ok": false,
			"message": "当前行动单位不可用。",
		}
	if String(active_unit.control_mode) != "manual":
		return {
			"ok": false,
			"message": "当前行动单位不是手动单位。",
		}
	var battle_runtime = _runtime.get_battle_runtime()
	if battle_runtime == null:
		return {
			"ok": false,
			"message": "当前战斗运行时不可用。",
		}

	var command = BATTLE_COMMAND_SCRIPT.new()
	command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_CHANGE_EQUIPMENT
	command.unit_id = active_unit.unit_id
	command.target_unit_id = PROGRESSION_DATA_UTILS_SCRIPT.to_string_name(
		options.get("target_unit_id", String(active_unit.unit_id))
	)
	command.equipment_operation = operation
	command.equipment_slot_id = slot_id
	command.equipment_item_id = item_id
	command.equipment_instance_id = instance_id
	if operation == BATTLE_COMMAND_SCRIPT.EQUIPMENT_OPERATION_EQUIP:
		var resolved_instance := _resolve_battle_backpack_equipment_instance(
			battle_state,
			item_id,
			instance_id
		)
		if not bool(resolved_instance.get("ok", false)):
			return resolved_instance
		command.equipment_instance_id = PROGRESSION_DATA_UTILS_SCRIPT.to_string_name(resolved_instance.get("instance_id", ""))
		command.equipment_item_id = PROGRESSION_DATA_UTILS_SCRIPT.to_string_name(resolved_instance.get("item_id", ""))
		command.equipment_instance = {
			"instance_id": String(command.equipment_instance_id),
			"item_id": String(command.equipment_item_id),
		}
	elif operation == BATTLE_COMMAND_SCRIPT.EQUIPMENT_OPERATION_UNEQUIP:
		if command.equipment_instance_id != &"":
			command.equipment_instance = {
				"instance_id": String(command.equipment_instance_id),
				"item_id": String(command.equipment_item_id),
			}
	else:
		return {
			"ok": false,
			"message": "战斗换装操作只能是 equip 或 unequip。",
		}

	var batch = battle_runtime.issue_command(command)
	if _runtime != null:
		if _runtime.has_method("record_command_battle_batch"):
			_runtime.record_command_battle_batch(batch)
		if _runtime.has_method("refresh_battle_runtime_state"):
			_runtime.refresh_battle_runtime_state()
		if batch != null and batch.log_lines is Array and not batch.log_lines.is_empty():
			_runtime.update_status(String(batch.log_lines[-1]))
	await settle_frames(1)

	var report := _find_last_change_equipment_report(batch.report_entries if batch != null else [])
	if report.is_empty():
		return {
			"ok": false,
			"message": "战斗换装命令未产生结果。",
		}
	return {
		"ok": bool(report.get("ok", false)),
		"message": String(report.get("text", "")),
	}


func build_snapshot() -> Dictionary:
	var snapshot := {
		"session": {
			"active_save_id": _game_session.get_active_save_id() if _game_session != null else "",
			"generation_config_path":
				_game_session.get_generation_config_path() if _game_session != null else "",
			"world_loaded": has_world_loaded(),
			"presets": WORLD_PRESET_REGISTRY_SCRIPT.list_presets(),
			"save_slots": _game_session.list_save_slots() if _game_session != null else [],
		},
		"validation": _game_session.get_content_validation_snapshot() if _game_session != null else {},
		"status": {
			"view": "none",
			"text": "",
		},
		"modal": {
			"id": "",
		},
		"logs": _game_session.get_log_snapshot() if _game_session != null else {},
		"world": {},
		"submap": {},
		"party": {},
		"settlement": {},
		"character_info": {},
		"warehouse": {},
		"battle": {},
		"reward": {},
		"promotion": {},
	}
	if has_world_loaded():
		var world_snapshot: Dictionary = _runtime.build_headless_snapshot()
		for key in world_snapshot.keys():
			snapshot[key] = world_snapshot[key]
		_augment_battle_snapshot(snapshot)
	return snapshot


func build_text_snapshot() -> String:
	return GAME_TEXT_SNAPSHOT_RENDERER_SCRIPT.render_full_snapshot(build_snapshot())


func dispose(clear_persisted_game: bool = false) -> void:
	await _unload_world_scene()

	if _game_session != null and is_instance_valid(_game_session):
		if clear_persisted_game:
			_game_session.clear_persisted_game()
		if _owns_game_session:
			_game_session.queue_free()
			await settle_frames(2)
	_game_session = null
	_owns_game_session = false
	_active_headless_encounter_anchor = null


func _ensure_game_session() -> void:
	if _game_session != null and is_instance_valid(_game_session):
		return
	var scene_tree := _get_scene_tree()
	if scene_tree == null:
		return
	_game_session = scene_tree.root.get_node_or_null("GameSession")
	if _game_session != null:
		_owns_game_session = false
		return
	_game_session = GAME_SESSION_SCRIPT.new()
	_game_session.name = "GameSession"
	scene_tree.root.add_child(_game_session)
	_owns_game_session = true
	await settle_frames(1)


func _unload_world_scene() -> void:
	if not has_world_loaded():
		if _game_session != null and is_instance_valid(_game_session):
			_game_session.set_battle_save_lock(false)
		_runtime = null
		_active_headless_encounter_anchor = null
		return
	if _runtime != null:
		_runtime.dispose()
	if _game_session != null and is_instance_valid(_game_session):
		_game_session.set_battle_save_lock(false)
	_runtime = null
	_active_headless_encounter_anchor = null
	await settle_frames()


func _get_scene_tree() -> SceneTree:
	var main_loop := Engine.get_main_loop()
	return main_loop as SceneTree if main_loop is SceneTree else null


func _find_nearest_encounter_anchor(encounter_kind: StringName) -> EncounterAnchorData:
	if _runtime == null:
		return null
	var player_coord: Vector2i = _runtime.get_player_coord()
	var nearest_encounter: EncounterAnchorData = null
	var nearest_distance := 2147483647
	for encounter_variant in _runtime.get_world_data().get("encounter_anchors", []):
		var encounter_anchor: EncounterAnchorData = encounter_variant as ENCOUNTER_ANCHOR_DATA_SCRIPT
		if encounter_anchor == null or encounter_anchor.is_cleared:
			continue
		if encounter_kind != &"" and encounter_anchor.encounter_kind != encounter_kind:
			continue
		if encounter_kind == ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT and not _encounter_has_formal_loot(encounter_anchor):
			continue
		var delta: Vector2i = encounter_anchor.world_coord - player_coord
		var distance := absi(delta.x) + absi(delta.y)
		if distance > nearest_distance:
			continue
		if distance == nearest_distance and nearest_encounter != null and String(encounter_anchor.entity_id) >= String(nearest_encounter.entity_id):
			continue
		nearest_distance = distance
		nearest_encounter = encounter_anchor
	return nearest_encounter


func _encounter_has_formal_loot(encounter_anchor: EncounterAnchorData) -> bool:
	if encounter_anchor == null or _game_session == null:
		return false
	var builder = ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	builder.setup(_game_session.get_wild_encounter_rosters(), _game_session.get_enemy_templates())
	return not builder.build_loot_entries(encounter_anchor, {}).is_empty()


func _build_headless_encounter_anchor(encounter_kind: StringName) -> EncounterAnchorData:
	if encounter_kind != ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT or _runtime == null:
		return null
	if not _game_session.get_wild_encounter_rosters().has(HEADLESS_SETTLEMENT_LOOT_PROFILE_ID):
		return null
	var encounter_anchor := ENCOUNTER_ANCHOR_DATA_SCRIPT.new()
	encounter_anchor.entity_id = HEADLESS_SETTLEMENT_LOOT_ENCOUNTER_ID
	encounter_anchor.display_name = HEADLESS_SETTLEMENT_LOOT_DISPLAY_NAME
	encounter_anchor.world_coord = _runtime.get_player_coord()
	encounter_anchor.faction_id = &"hostile"
	encounter_anchor.encounter_kind = ENCOUNTER_ANCHOR_DATA_SCRIPT.ENCOUNTER_KIND_SETTLEMENT
	encounter_anchor.encounter_profile_id = HEADLESS_SETTLEMENT_LOOT_PROFILE_ID
	if not _encounter_has_formal_loot(encounter_anchor):
		return null
	return encounter_anchor


func _prime_headless_battle_loot_if_needed(winner_faction_id: StringName) -> void:
	if winner_faction_id != &"player" or _runtime == null or _game_session == null:
		return
	if _active_headless_encounter_anchor == null:
		return
	var battle_runtime = _runtime.get_battle_runtime()
	if battle_runtime == null:
		return
	var existing_loot_entries: Array = battle_runtime._active_loot_entries if battle_runtime._active_loot_entries is Array else []
	if not existing_loot_entries.is_empty():
		return
	var roster_builder := ENCOUNTER_ROSTER_BUILDER_SCRIPT.new()
	roster_builder.setup(_game_session.get_wild_encounter_rosters(), _game_session.get_enemy_templates())
	var preview_loot_entries := roster_builder.build_loot_entries(_active_headless_encounter_anchor, {})
	if preview_loot_entries.is_empty():
		return
	battle_runtime._active_loot_entries = preview_loot_entries.duplicate(true)


func _resolve_battle_backpack_equipment_instance(
	battle_state,
	item_id: StringName,
	instance_id: StringName
) -> Dictionary:
	var normalized_item_id := PROGRESSION_DATA_UTILS_SCRIPT.to_string_name(item_id)
	var normalized_instance_id := PROGRESSION_DATA_UTILS_SCRIPT.to_string_name(instance_id)
	if normalized_item_id == &"" and normalized_instance_id == &"":
		return {
			"ok": false,
			"message": "用法: battle equip <slot_id> <item_id> [instance_id=<instance_id>]",
		}
	var backpack_view = battle_state.get_party_backpack_view() if battle_state != null else null
	if backpack_view == null:
		return {
			"ok": false,
			"message": "战斗背包状态不可用。",
		}
	for instance in backpack_view.get_non_empty_instances():
		if instance == null:
			continue
		var candidate_instance_id := PROGRESSION_DATA_UTILS_SCRIPT.to_string_name(instance.instance_id)
		var candidate_item_id := PROGRESSION_DATA_UTILS_SCRIPT.to_string_name(instance.item_id)
		if normalized_instance_id != &"" and candidate_instance_id != normalized_instance_id:
			continue
		if normalized_item_id != &"" and candidate_item_id != normalized_item_id:
			continue
		return {
			"ok": true,
			"instance_id": String(candidate_instance_id),
			"item_id": String(candidate_item_id),
		}
	var label := String(normalized_instance_id) if normalized_instance_id != &"" else String(normalized_item_id)
	return {
		"ok": false,
		"message": "战斗背包中找不到装备 %s。" % label,
	}


func _find_last_change_equipment_report(report_entries: Array) -> Dictionary:
	for index in range(report_entries.size() - 1, -1, -1):
		var report_variant = report_entries[index]
		if report_variant is not Dictionary:
			continue
		var report: Dictionary = report_variant
		if String(report.get("type", report.get("entry_type", ""))) == "change_equipment":
			return report
	return {}


func _augment_battle_snapshot(snapshot: Dictionary) -> void:
	var battle_snapshot_variant = snapshot.get("battle", {})
	if battle_snapshot_variant is not Dictionary:
		return
	var battle_snapshot: Dictionary = battle_snapshot_variant
	if not bool(battle_snapshot.get("active", false)):
		return
	var battle_state = _runtime.get_battle_state() if _runtime != null else null
	if battle_state == null or battle_state.is_empty():
		return
	battle_snapshot["party_backpack"] = _build_battle_backpack_snapshot(battle_state.get_party_backpack_view())
	var units_variant = battle_snapshot.get("units", [])
	if units_variant is Array:
		for unit_snapshot_variant in units_variant:
			if unit_snapshot_variant is not Dictionary:
				continue
			var unit_snapshot: Dictionary = unit_snapshot_variant
			var unit_id := PROGRESSION_DATA_UTILS_SCRIPT.to_string_name(unit_snapshot.get("unit_id", ""))
			var unit_state = battle_state.units.get(unit_id)
			if unit_state == null:
				continue
			var equipment_entries := _build_battle_equipment_entries(unit_state.get_equipment_view())
			unit_snapshot["hp_max"] = _get_battle_unit_hp_max(unit_state)
			unit_snapshot["equipment"] = equipment_entries
			unit_snapshot["equipment_count"] = equipment_entries.size()
	snapshot["battle"] = battle_snapshot


func _build_battle_backpack_snapshot(backpack_view) -> Dictionary:
	var stack_entries: Array[Dictionary] = []
	var equipment_entries: Array[Dictionary] = []
	if backpack_view != null:
		for stack in backpack_view.get_non_empty_stacks():
			if stack == null:
				continue
			stack_entries.append({
				"item_id": String(stack.item_id),
				"quantity": int(stack.quantity),
			})
		for instance in backpack_view.get_non_empty_instances():
			if instance == null:
				continue
			equipment_entries.append({
				"instance_id": String(instance.instance_id),
				"item_id": String(instance.item_id),
			})
	equipment_entries.sort_custom(Callable(self, "_compare_battle_backpack_equipment_entries"))
	return {
		"stack_count": stack_entries.size(),
		"equipment_instance_count": equipment_entries.size(),
		"used_slots": stack_entries.size() + equipment_entries.size(),
		"stacks": stack_entries,
		"equipment_instances": equipment_entries,
	}


func _build_battle_equipment_entries(equipment_view) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	if equipment_view == null or not (equipment_view is Object and equipment_view.has_method("get_entry_slot_ids")):
		return entries
	for entry_slot_id in equipment_view.get_entry_slot_ids():
		var entry = equipment_view.get_entry(entry_slot_id)
		if entry == null:
			continue
		entries.append({
			"slot_id": String(entry_slot_id),
			"item_id": String(entry.item_id),
			"instance_id": String(entry.instance_id),
			"occupied_slot_ids": _string_name_array_to_string_array(entry.occupied_slot_ids),
		})
	return entries


func _get_battle_unit_hp_max(unit_state) -> int:
	if unit_state == null or unit_state.attribute_snapshot == null:
		return 0
	return maxi(int(unit_state.attribute_snapshot.get_value(&"hp_max")), 1)


func _string_name_array_to_string_array(values: Array[StringName]) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	return result


func _compare_battle_backpack_equipment_entries(a: Dictionary, b: Dictionary) -> bool:
	return String(a.get("instance_id", "")) < String(b.get("instance_id", ""))
