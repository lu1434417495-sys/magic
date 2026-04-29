class_name GameRuntimeCommandLogger
extends RefCounted

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

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


func execute_logged_command(event_id: String, domain: String, context: Dictionary, action: Callable) -> Dictionary:
	return _execute_logged_command(event_id, domain, context, action)


func log_active_command_scope_result(result: Dictionary) -> void:
	_log_active_command_scope_result(result)


func build_runtime_log_state() -> Dictionary:
	return _build_runtime_log_state()


func log_runtime_event(level: String, domain: String, event_id: String, message: String, context: Dictionary = {}) -> void:
	_log_runtime_event(level, domain, event_id, message, context)


func log_battle_batch_entries(batch) -> void:
	_log_battle_batch_entries(batch)


func build_battle_log_state() -> Dictionary:
	return _build_battle_log_state()


func build_battle_batch_log_context(batch) -> Dictionary:
	return _build_battle_batch_log_context(batch)


func normalize_log_variant(value):
	return _normalize_log_variant(value)

func _execute_logged_command(event_id: String, domain: String, context: Dictionary, action: Callable) -> Dictionary:
	var previous_scope: Dictionary = _runtime._active_command_log_scope.duplicate(true)
	var command_args: Dictionary = _normalize_log_variant(context)
	var before_state := _build_runtime_log_state()
	if domain == "battle":
		_runtime._pending_command_battle_batches.clear()
	_runtime._active_command_log_scope = {
		"event_id": event_id,
		"domain": domain,
		"context": {
			"command_args": command_args,
			"before": before_state,
		},
		"logged": false,
	}
	var result_variant = action.call()
	var result: Dictionary = result_variant if result_variant is Dictionary else {}
	if not bool(_runtime._active_command_log_scope.get("logged", false)):
		_log_command_result(_runtime._active_command_log_scope, result)
	_runtime._active_command_log_scope = previous_scope
	if domain == "battle":
		_runtime._pending_command_battle_batches.clear()
	return result


func _log_active_command_scope_result(result: Dictionary) -> void:
	if _runtime._active_command_log_scope.is_empty():
		return
	if bool(_runtime._active_command_log_scope.get("logged", false)):
		return
	_log_command_result(_runtime._active_command_log_scope, result)


func _log_command_result(scope: Dictionary, result: Dictionary) -> void:
	if scope.is_empty():
		return
	var resolved_result: Dictionary = result if result != null else {}
	var ok := bool(resolved_result.get("ok", false))
	var message := String(resolved_result.get("message", _runtime._current_status_message))
	var log_context: Dictionary = (scope.get("context", {}) as Dictionary).duplicate(true)
	var after_state := _build_runtime_log_state()
	log_context["runtime"] = after_state
	log_context["ok"] = ok
	if not message.is_empty():
		log_context["result_message"] = message
	var battle_refresh_mode := String(resolved_result.get("battle_refresh_mode", ""))
	if not battle_refresh_mode.is_empty():
		log_context["battle_refresh_mode"] = battle_refresh_mode
	if String(scope.get("domain", "")) == "battle" and not _runtime._pending_command_battle_batches.is_empty():
		log_context["battle_batches"] = _runtime._pending_command_battle_batches.duplicate(true)
		log_context["battle_batch"] = (_runtime._pending_command_battle_batches[-1] as Dictionary).duplicate(true)
		log_context["battle_changed_units"] = _collect_command_battle_changed_units(_runtime._pending_command_battle_batches)
	_log_runtime_event(
		"info" if ok else "warn",
		String(scope.get("domain", "runtime")),
		String(scope.get("event_id", "runtime.command")),
		message if not message.is_empty() else ("命令成功。" if ok else "命令失败。"),
		log_context
	)
	_runtime._active_command_log_scope["logged"] = true


func _build_runtime_log_state() -> Dictionary:
	var context := {
		"save_id": _runtime._game_session.get_active_save_id() if _runtime._game_session != null else "",
		"map_id": _runtime._world_map_data_context.active_map_id,
		"map_display_name": _runtime._world_map_data_context.active_map_display_name,
		"player_coord": _runtime._player_coord,
		"selected_coord": _runtime._selected_coord,
		"active_modal_id": _runtime._active_modal_id,
		"battle_active": _runtime._is_battle_active(),
	}
	if _runtime._is_battle_active():
		context["battle"] = _build_battle_log_state()
	return context


func _log_runtime_event(level: String, domain: String, event_id: String, message: String, context: Dictionary = {}) -> void:
	if _runtime._game_session == null:
		return
	_runtime._game_session.log_event(level, domain, event_id, message, context)


func _log_battle_batch_entries(batch) -> void:
	if batch == null or batch.log_lines.is_empty():
		return
	var base_context := _build_battle_batch_log_context(batch)
	base_context["runtime"] = _build_runtime_log_state()
	for log_line in batch.log_lines:
		_log_runtime_event("info", "battle", "battle.log", String(log_line), base_context)


func _build_battle_log_state() -> Dictionary:
	if not _runtime._is_battle_active() or _runtime._battle_state == null:
		return {}
	var ally_alive_count := 0
	var hostile_alive_count := 0
	for unit_variant in _runtime._battle_state.units.values():
		var unit_state := unit_variant as BattleUnitState
		if unit_state == null or not unit_state.is_alive:
			continue
		if String(unit_state.faction_id) == _runtime._player_faction_id:
			ally_alive_count += 1
		else:
			hostile_alive_count += 1
	return {
		"encounter_id": String(_runtime._active_battle_encounter_id),
		"encounter_name": _runtime._active_battle_encounter_name,
		"battle_id": String(_runtime._battle_state.battle_id),
		"seed": int(_runtime._battle_state.seed),
		"terrain_profile_id": String(_runtime._battle_state.terrain_profile_id),
		"map_size": _runtime._battle_state.map_size,
		"phase": String(_runtime._battle_state.phase),
		"modal_state": String(_runtime._battle_state.modal_state),
		"winner_faction_id": String(_runtime._battle_state.winner_faction_id),
		"active_unit_id": String(_runtime._battle_state.active_unit_id),
		"active_unit_name": _runtime._get_battle_active_unit_name(),
		"selected_coord": _runtime._battle_selected_coord,
		"selected_skill_id": String(_runtime._selected_battle_skill_id),
		"selected_skill_variant_id": String(_runtime._selected_battle_skill_variant_id),
		"selected_target_coord_count": _runtime._queued_battle_skill_target_coords.size(),
		"selected_target_unit_count": _runtime._queued_battle_skill_target_unit_ids.size(),
		"terrain_counts": _runtime._count_battle_terrain_types(),
		"ally_alive_count": ally_alive_count,
		"hostile_alive_count": hostile_alive_count,
		"units": _build_battle_unit_log_entries(),
	}


func _build_battle_batch_log_context(batch) -> Dictionary:
	if batch == null:
		return {}
	return {
		"phase_changed": bool(batch.phase_changed),
		"battle_ended": bool(batch.battle_ended),
		"modal_requested": bool(batch.modal_requested),
		"changed_unit_count": batch.changed_unit_ids.size(),
		"changed_coord_count": batch.changed_coords.size(),
		"changed_coords": _normalize_log_variant(batch.changed_coords),
		"changed_unit_ids": _normalize_log_variant(batch.changed_unit_ids),
		"changed_units": _build_battle_unit_log_entries(batch.changed_unit_ids),
		"report_entry_count": batch.report_entries.size(),
		"report_entries": _normalize_log_variant(batch.report_entries),
	}


func _collect_command_battle_changed_units(batch_contexts: Array[Dictionary]) -> Array[Dictionary]:
	var merged_by_unit_id: Dictionary = {}
	var ordered_unit_ids: Array[String] = []
	for batch_context in batch_contexts:
		if batch_context == null:
			continue
		var changed_units_variant = batch_context.get("changed_units", [])
		if changed_units_variant is not Array:
			continue
		for changed_unit_variant in changed_units_variant:
			if changed_unit_variant is not Dictionary:
				continue
			var changed_unit: Dictionary = changed_unit_variant
			var unit_id := String(changed_unit.get("unit_id", "")).strip_edges()
			if unit_id.is_empty():
				continue
			if not merged_by_unit_id.has(unit_id):
				ordered_unit_ids.append(unit_id)
			merged_by_unit_id[unit_id] = changed_unit.duplicate(true)
	var result: Array[Dictionary] = []
	for unit_id in ordered_unit_ids:
		if merged_by_unit_id.has(unit_id):
			result.append((merged_by_unit_id[unit_id] as Dictionary).duplicate(true))
	return result


func _build_battle_unit_log_entries(unit_ids: Array = []) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if _runtime._battle_state == null:
		return result
	var normalized_ids: Array[StringName] = []
	if unit_ids.is_empty():
		for unit_key in ProgressionDataUtils.sorted_string_keys(_runtime._battle_state.units):
			normalized_ids.append(StringName(unit_key))
	else:
		for unit_id_variant in unit_ids:
			var normalized_unit_id := ProgressionDataUtils.to_string_name(unit_id_variant)
			if normalized_unit_id == &"":
				continue
			if normalized_ids.has(normalized_unit_id):
				continue
			normalized_ids.append(normalized_unit_id)
	for unit_id in normalized_ids:
		var unit_state := _runtime._battle_state.units.get(unit_id) as BattleUnitState
		if unit_state == null:
			continue
		result.append({
			"unit_id": String(unit_state.unit_id),
			"display_name": unit_state.display_name if not unit_state.display_name.is_empty() else String(unit_state.unit_id),
			"faction_id": String(unit_state.faction_id),
			"control_mode": String(unit_state.control_mode),
			"is_alive": bool(unit_state.is_alive),
			"coord": unit_state.coord,
			"current_hp": int(unit_state.current_hp),
			"current_mp": int(unit_state.current_mp),
			"current_stamina": int(unit_state.current_stamina),
			"current_aura": int(unit_state.current_aura),
			"current_ap": int(unit_state.current_ap),
			"current_move_points": int(unit_state.current_move_points),
		})
	return result


func _normalize_log_variant(value):
	match typeof(value):
		TYPE_STRING_NAME:
			return String(value)
		TYPE_VECTOR2I:
			var coord: Vector2i = value
			return {
				"x": coord.x,
				"y": coord.y,
			}
		TYPE_VECTOR2:
			var float_coord: Vector2 = value
			return {
				"x": float_coord.x,
				"y": float_coord.y,
			}
		TYPE_DICTIONARY:
			var normalized_dict: Dictionary = {}
			for key in value.keys():
				normalized_dict[String(key)] = _normalize_log_variant(value.get(key))
			return normalized_dict
		TYPE_ARRAY:
			var normalized_array: Array = []
			for entry in value:
				normalized_array.append(_normalize_log_variant(entry))
			return normalized_array
		TYPE_OBJECT:
			if value == null:
				return null
			if value.has_method("to_dict"):
				return _normalize_log_variant(value.to_dict())
			return str(value)
		_:
			return value


