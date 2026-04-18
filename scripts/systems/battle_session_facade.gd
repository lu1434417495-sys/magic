class_name BattleSessionFacade
extends RefCounted

const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle_command.gd")
const BATTLE_CELL_STATE_SCRIPT = preload("res://scripts/systems/battle_cell_state.gd")
const BattleState = preload("res://scripts/systems/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const RUNTIME_UNAVAILABLE_MESSAGE := "运行时尚未初始化。"

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


func get_selected_battle_skill_name() -> String:
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return ""
	return battle_selection.get_selected_battle_skill_name()


func get_selected_battle_skill_variant_name() -> String:
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return ""
	return battle_selection.get_selected_battle_skill_variant_name()


func get_selected_battle_skill_target_coords() -> Array[Vector2i]:
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return []
	return battle_selection.get_selected_battle_skill_target_coords()


func get_selected_battle_skill_target_unit_ids() -> Array[StringName]:
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return []
	return battle_selection.get_selected_battle_skill_target_unit_ids()


func get_selected_battle_skill_valid_target_coords() -> Array[Vector2i]:
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return []
	return battle_selection.get_selected_battle_skill_valid_target_coords()


func get_selected_battle_skill_required_coord_count() -> int:
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return 0
	return battle_selection.get_selected_battle_skill_required_coord_count()


func get_battle_movement_reachable_coords() -> Array[Vector2i]:
	var battle_runtime = _get_battle_runtime()
	if not _is_battle_ready() or not _is_battle_active() or battle_runtime == null:
		return []
	var active_unit: BattleUnitState = get_manual_active_unit()
	if active_unit == null:
		return []
	return battle_runtime.get_unit_reachable_move_coords(active_unit)


func get_battle_overlay_target_coords() -> Array[Vector2i]:
	if not _is_battle_ready():
		return []
	if _runtime.get_selected_battle_skill_id() != &"":
		return get_selected_battle_skill_valid_target_coords()
	return get_battle_movement_reachable_coords()


func get_battle_active_unit_name() -> String:
	var active_unit := get_battle_active_unit()
	if active_unit == null:
		return "无"
	return active_unit.display_name if not active_unit.display_name.is_empty() else String(active_unit.unit_id)


func get_battle_terrain_counts() -> Dictionary:
	var counts := {
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_LAND: 0,
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_FOREST: 0,
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_SHALLOW_WATER: 0,
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_FLOWING_WATER: 0,
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_DEEP_WATER: 0,
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_MUD: 0,
		BATTLE_CELL_STATE_SCRIPT.TERRAIN_SPIKE: 0,
	}
	var battle_state := _get_battle_state()
	if not _is_battle_ready() or battle_state == null:
		return counts
	for cell_variant in battle_state.cells.values():
		var cell_state := cell_variant as BattleCellState
		if cell_state == null:
			continue
		var terrain_id := String(cell_state.base_terrain)
		counts[terrain_id] = int(counts.get(terrain_id, 0)) + 1
	return counts


func command_battle_tick(total_seconds: float, step_seconds: float = 1.0 / 60.0) -> Dictionary:
	if not _is_battle_ready():
		return _runtime_unavailable_error()
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	if total_seconds <= 0.0:
		return _command_error("推进时间必须大于 0。")
	var battle_runtime = _get_battle_runtime()
	if battle_runtime == null:
		return _runtime_unavailable_error()
	var remaining_seconds := total_seconds
	var delta_seconds := maxf(step_seconds, 1.0 / 60.0)
	while remaining_seconds > 0.0 and _is_battle_active():
		var runtime_state = get_runtime_battle_state()
		if runtime_state != null and String(runtime_state.modal_state) != "":
			break
		var step := minf(remaining_seconds, delta_seconds)
		var batch = battle_runtime.advance(step)
		if _batch_has_updates(batch):
			apply_battle_batch(batch)
		remaining_seconds -= step
	return _command_ok()


func command_battle_select_skill(slot_index: int) -> Dictionary:
	if not _is_battle_ready():
		return _runtime_unavailable_error()
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return _runtime_unavailable_error()
	var select_result: Dictionary = battle_selection.select_battle_skill_slot(slot_index)
	if not bool(select_result.get("ok", false)):
		return _command_error(String(select_result.get("message", "")))
	return _command_ok("", "overlay")


func command_battle_cycle_variant(step: int) -> Dictionary:
	if not _is_battle_ready():
		return _runtime_unavailable_error()
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return _runtime_unavailable_error()
	battle_selection.cycle_selected_battle_skill_variant(step)
	return _command_ok("", "overlay")


func command_battle_clear_skill() -> Dictionary:
	if not _is_battle_ready():
		return _runtime_unavailable_error()
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return _runtime_unavailable_error()
	battle_selection.clear_battle_skill_selection(true)
	return _command_ok("", "overlay")


func command_battle_move_to(target_coord: Vector2i) -> Dictionary:
	if not _is_battle_ready():
		return _runtime_unavailable_error()
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return _runtime_unavailable_error()
	var battle_refresh_mode := StringName(battle_selection.attempt_battle_move_to(target_coord))
	if battle_refresh_mode == &"error":
		return _command_error(_get_runtime_status_text("当前技能无法施放。"))
	return _command_ok("", String(battle_refresh_mode))


func command_battle_move_direction(direction: Vector2i) -> Dictionary:
	if not _is_battle_ready():
		return _runtime_unavailable_error()
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	if direction == Vector2i.ZERO:
		return _command_error("战斗移动方向不能为空。")
	var battle_refresh_mode := attempt_battle_move(direction)
	if battle_refresh_mode == &"error":
		return _command_error(_get_runtime_status_text("当前技能无法施放。"))
	return _command_ok("", String(battle_refresh_mode))


func command_battle_wait_or_resolve() -> Dictionary:
	if not _is_battle_ready():
		return _runtime_unavailable_error()
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	resolve_active_battle()
	return _command_ok()


func command_battle_inspect(coord: Vector2i) -> Dictionary:
	if not _is_battle_ready():
		return _runtime_unavailable_error()
	if not _is_battle_active():
		return _command_error("当前没有进行中的战斗。")
	if _try_open_character_info_at_battle_coord(coord):
		return _command_ok()
	return _command_error("该战斗格没有可查看单位。")


func reset_battle_focus() -> Dictionary:
	if not _is_battle_ready():
		return _runtime_unavailable_error()
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return _runtime_unavailable_error()
	return _command_ok("", String(battle_selection.reset_battle_movement()))


func handle_battle_input(key_event: InputEventKey) -> bool:
	if not _is_battle_ready():
		return false
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return false
	match key_event.keycode:
		KEY_1, KEY_2, KEY_3, KEY_4, KEY_5, KEY_6, KEY_7, KEY_8, KEY_9:
			battle_selection.select_battle_skill_slot(int(key_event.keycode - KEY_1))
		KEY_Q:
			battle_selection.cycle_selected_battle_skill_variant(-1)
		KEY_E:
			battle_selection.cycle_selected_battle_skill_variant(1)
		KEY_ESCAPE:
			battle_selection.clear_battle_skill_selection(true)
		KEY_LEFT, KEY_A:
			attempt_battle_move(Vector2i.LEFT)
		KEY_RIGHT, KEY_D:
			attempt_battle_move(Vector2i.RIGHT)
		KEY_UP, KEY_W:
			attempt_battle_move(Vector2i.UP)
		KEY_DOWN, KEY_S:
			attempt_battle_move(Vector2i.DOWN)
		KEY_R:
			battle_selection.reset_battle_movement()
		KEY_SPACE:
			resolve_active_battle()
		_:
			return false
	return true


func start_battle(encounter_anchor) -> void:
	if not _is_battle_ready() or encounter_anchor == null:
		return
	_runtime.prepare_battle_start(encounter_anchor)

	var battle_runtime = _get_battle_runtime()
	if battle_runtime == null:
		_runtime.handle_battle_start_failure()
		return
	var runtime_state = battle_runtime.start_battle(
		encounter_anchor,
		build_battle_seed(encounter_anchor),
		build_battle_start_context(encounter_anchor)
	)
	if runtime_state == null or runtime_state.is_empty():
		_runtime.handle_battle_start_failure()
		return

	refresh_battle_runtime_state()
	if _runtime != null:
		_runtime.present_battle_start_confirmation()
	_update_status("遭遇 %s，战斗地图已载入，等待确认开始。" % _runtime.get_active_battle_encounter_name())


func resolve_active_battle() -> void:
	if not _is_battle_ready() or not _is_battle_active():
		return

	if not is_battle_finished():
		var wait_command = build_wait_command()
		if wait_command == null:
			_update_status("当前尚未到可操作单位或战斗结果未结算。")
			return
		issue_battle_command(wait_command)
		return

	var battle_runtime = _get_battle_runtime()
	if battle_runtime == null:
		return
	var battle_resolution_result = _consume_battle_resolution_result(battle_runtime)
	if battle_resolution_result == null:
		_update_status("战斗已结束，但缺少正式结算结果。")
		return
	_runtime.finalize_battle_resolution(battle_resolution_result)


func _consume_battle_resolution_result(battle_runtime):
	return battle_runtime.consume_battle_resolution_result()


func attempt_battle_move(direction: Vector2i) -> StringName:
	if not _is_battle_ready() or not _is_battle_active():
		return &"full"
	var active_unit = get_manual_active_unit()
	if active_unit == null:
		_update_status("当前没有可手动操作的单位。")
		return &"overlay"
	var battle_selection = _get_battle_selection()
	if battle_selection == null:
		return &"full"
	return battle_selection.attempt_battle_move_to(active_unit.coord + direction)


func on_battle_cell_clicked(coord: Vector2i) -> void:
	if not _is_battle_ready() or not _is_battle_active():
		return
	if _is_modal_window_open():
		return
	var battle_selection = _get_battle_selection()
	if battle_selection != null:
		battle_selection.attempt_battle_move_to(coord)


func on_battle_cell_right_clicked(coord: Vector2i) -> void:
	if not _is_battle_ready() or not _is_battle_active():
		return
	if _is_modal_window_open():
		return
	if _try_open_character_info_at_battle_coord(coord):
		return
	_update_status("该战斗格没有可查看单位。")


func on_battle_skill_slot_selected(index: int) -> void:
	if not _is_battle_ready() or _is_modal_window_open():
		return
	var battle_selection = _get_battle_selection()
	if battle_selection != null:
		battle_selection.select_battle_skill_slot(index)


func on_battle_skill_variant_cycle_requested(step: int) -> void:
	if not _is_battle_ready() or _is_modal_window_open():
		return
	var battle_selection = _get_battle_selection()
	if battle_selection != null:
		battle_selection.cycle_selected_battle_skill_variant(step)


func on_battle_skill_clear_requested() -> void:
	if not _is_battle_ready() or _is_modal_window_open():
		return
	var battle_selection = _get_battle_selection()
	if battle_selection != null:
		battle_selection.clear_battle_skill_selection(true)


func apply_battle_batch(batch) -> void:
	if batch == null:
		return
	capture_pending_promotion_prompt(batch.progression_deltas)
	refresh_battle_runtime_state()
	if not batch.log_lines.is_empty():
		_update_status(String(batch.log_lines[-1]))
	var battle_state := _get_battle_state()
	if not _get_pending_promotion_prompt().is_empty() and battle_state != null and String(battle_state.modal_state) == "promotion_choice":
		_set_active_modal_id("promotion")
	if is_battle_finished():
		resolve_active_battle()


func refresh_battle_runtime_state() -> void:
	if not _is_battle_ready():
		return
	var battle_selection = _get_battle_selection()
	if battle_selection != null:
		battle_selection.sync_selected_battle_skill_state()
	var battle_state := get_runtime_battle_state()
	if battle_state == null or battle_state.is_empty():
		_set_battle_state(null)
		_set_battle_selected_coord(Vector2i(-1, -1))
		return
	_set_battle_state(battle_state)
	if _runtime.get_battle_selected_coord() == Vector2i(-1, -1) or not battle_state.cells.has(_runtime.get_battle_selected_coord()):
		_set_battle_selected_coord(get_default_battle_selected_coord())


func build_battle_seed(encounter_anchor) -> int:
	if encounter_anchor == null:
		return 0
	var generation_config = _runtime.get_generation_config() if _runtime != null else null
	var player_coord: Vector2i = _runtime.get_player_coord() if _runtime != null else Vector2i.ZERO
	var base_seed := int(generation_config.seed) if generation_config != null else 0
	return base_seed ^ String(encounter_anchor.entity_id).hash() ^ (player_coord.x * 73856093) ^ (player_coord.y * 19349663)


func get_runtime_battle_state() -> BattleState:
	var battle_runtime = _get_battle_runtime()
	return battle_runtime.get_state() if battle_runtime != null else null


func is_battle_finished() -> bool:
	var runtime_state = get_runtime_battle_state()
	return runtime_state != null and String(runtime_state.phase) == "battle_ended"


func get_runtime_active_unit() -> BattleUnitState:
	var runtime_state = get_runtime_battle_state()
	if runtime_state == null or runtime_state.active_unit_id == &"":
		return null
	return runtime_state.units.get(runtime_state.active_unit_id) as BattleUnitState


func get_manual_active_unit() -> BattleUnitState:
	var runtime_state = get_runtime_battle_state()
	var active_unit: BattleUnitState = get_runtime_active_unit()
	if runtime_state == null or active_unit == null:
		return null
	if String(runtime_state.phase) != "unit_acting":
		return null
	if String(runtime_state.modal_state) != "":
		return null
	if String(active_unit.control_mode) != "manual":
		return null
	return active_unit


func get_runtime_unit_at_coord(coord: Vector2i) -> BattleUnitState:
	var runtime_state = get_runtime_battle_state()
	var battle_grid_service = _get_battle_grid_service()
	if runtime_state == null or battle_grid_service == null:
		return null
	return battle_grid_service.get_unit_at_coord(runtime_state, coord)


func build_wait_command():
	var active_unit = get_manual_active_unit()
	if active_unit == null:
		return null
	var wait_command = BATTLE_COMMAND_SCRIPT.new()
	wait_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_WAIT
	wait_command.unit_id = active_unit.unit_id
	return wait_command


func issue_battle_command(command) -> StringName:
	if command == null:
		return &"overlay"
	if command.command_type == BATTLE_COMMAND_SCRIPT.TYPE_SKILL:
		_clear_battle_selection_targets()
	var battle_runtime = _get_battle_runtime()
	if battle_runtime == null:
		return &"overlay"
	var batch = battle_runtime.issue_command(command)
	apply_battle_batch(batch)
	return &"full"


func capture_pending_promotion_prompt(progression_deltas: Array) -> void:
	for delta in progression_deltas:
		if delta == null or not delta.needs_promotion_modal:
			continue
		_set_pending_promotion_prompt(build_promotion_prompt(delta))
		if not _get_pending_promotion_prompt().is_empty():
			return


func build_promotion_prompt(delta, selection_hint: String = "确认后将在战斗中立即生效。") -> Dictionary:
	if delta == null or delta.pending_profession_choices.is_empty():
		return {}

	var party_state = _runtime.get_party_state() if _runtime != null else null
	var game_session = _get_game_session()
	var member_state = party_state.get_member_state(delta.member_id) if party_state != null else null
	var member_name: String = member_state.display_name if member_state != null else str(delta.member_id)
	var profession_defs: Dictionary = game_session.get_profession_defs() if game_session != null else {}
	var choice_entries: Array[Dictionary] = []

	for pending_choice in delta.pending_profession_choices:
		if pending_choice == null:
			continue
		for profession_id in pending_choice.candidate_profession_ids:
			var profession_def = profession_defs.get(profession_id)
			var target_rank := int(pending_choice.target_rank_map.get(profession_id, 1))
			var granted_skill_ids: Array[StringName] = []
			if profession_def != null:
				for granted_skill in profession_def.get_granted_skills_for_rank(target_rank):
					if granted_skill != null and granted_skill.skill_id != &"":
						granted_skill_ids.append(granted_skill.skill_id)

			choice_entries.append({
				"profession_id": String(profession_id),
				"display_name": profession_def.display_name if profession_def != null and not profession_def.display_name.is_empty() else String(profession_id),
				"summary": "Rank %d" % target_rank,
				"description": profession_def.description if profession_def != null else "",
				"granted_skill_ids": granted_skill_ids,
				"selection_hint": selection_hint,
				"selection": {},
			})

	if choice_entries.is_empty():
		return {}
	return {
		"member_id": String(delta.member_id),
		"member_name": member_name,
		"choices": choice_entries,
	}


func get_default_battle_selected_coord() -> Vector2i:
	var active_unit := get_battle_active_unit()
	if active_unit != null:
		return active_unit.coord

	var battle_state := _get_battle_state()
	if battle_state != null:
		for ally_unit_id in battle_state.ally_unit_ids:
			var unit := get_battle_unit_by_id(ally_unit_id)
			if unit != null:
				return unit.coord

	return Vector2i.ZERO


func get_battle_unit_by_id(unit_id: StringName) -> BattleUnitState:
	var battle_state := _get_battle_state()
	if battle_state == null or unit_id == &"":
		return null
	return battle_state.units.get(unit_id) as BattleUnitState


func get_battle_unit_at_coord(coord: Vector2i) -> BattleUnitState:
	var battle_state := _get_battle_state()
	var battle_grid_service = _get_battle_grid_service()
	if battle_state == null or battle_grid_service == null:
		return null
	return battle_grid_service.get_unit_at_coord(battle_state, coord)


func get_battle_active_unit() -> BattleUnitState:
	var battle_state := _get_battle_state()
	if battle_state == null:
		return null
	return get_battle_unit_by_id(battle_state.active_unit_id)


func get_battle_unit_type_label(unit_id: String) -> String:
	var battle_state := _get_battle_state()
	if battle_state == null:
		return "战斗单位"
	for ally_unit_id in battle_state.ally_unit_ids:
		if String(ally_unit_id) == unit_id:
			return "己方单位"
	for enemy_unit_id in battle_state.enemy_unit_ids:
		if String(enemy_unit_id) == unit_id:
			return "敌方单位"
	return "战斗单位"


func build_battle_start_context(encounter_anchor) -> Dictionary:
	var context := {
		"world_coord": encounter_anchor.world_coord if encounter_anchor != null else _runtime.get_player_coord(),
	}
	context["battle_terrain_profile"] = String(resolve_battle_terrain_profile(encounter_anchor))
	return context


func resolve_battle_terrain_profile(encounter_anchor) -> StringName:
	if encounter_anchor == null:
		return &"default"
	match String(encounter_anchor.region_tag).strip_edges().to_lower():
		"canyon":
			return &"canyon"
		_:
			return &"default"


func _get_battle_selection():
	return _runtime.get_battle_selection() if _runtime != null else null


func _is_battle_ready() -> bool:
	return _runtime != null and _get_battle_selection() != null and _get_battle_runtime() != null


func _get_runtime_status_text(fallback_message: String) -> String:
	if _runtime != null:
		var status_text := String(_runtime.get_status_text())
		if not status_text.is_empty():
			return status_text
	return fallback_message


func _command_ok(message: String = "", battle_refresh_mode: String = "") -> Dictionary:
	if _runtime != null:
		return _runtime.build_command_ok(message, battle_refresh_mode)
	return {"ok": true, "message": message, "battle_refresh_mode": battle_refresh_mode}


func _command_error(message: String) -> Dictionary:
	if _runtime != null:
		return _runtime.build_command_error(message)
	return {"ok": false, "message": message}


func _runtime_unavailable_error() -> Dictionary:
	return {"ok": false, "message": RUNTIME_UNAVAILABLE_MESSAGE}


func _get_battle_runtime():
	return _runtime.get_battle_runtime() if _runtime != null else null


func _get_battle_grid_service():
	return _runtime.get_battle_grid_service() if _runtime != null else null


func _get_battle_state() -> BattleState:
	return _runtime.get_battle_state() if _runtime != null else null


func _get_game_session():
	return _runtime.get_game_session() if _runtime != null else null


func _get_pending_promotion_prompt() -> Dictionary:
	return _runtime.get_pending_promotion_prompt() if _runtime != null else {}


func _set_pending_promotion_prompt(prompt: Dictionary) -> void:
	if _runtime != null:
		_runtime.set_pending_promotion_prompt(prompt)


func _set_battle_state(state: BattleState) -> void:
	if _runtime != null:
		_runtime.set_runtime_battle_state(state)


func _set_battle_selected_coord(coord: Vector2i) -> void:
	if _runtime != null:
		_runtime.set_runtime_battle_selected_coord(coord)


func _set_active_modal_id(modal_id: String) -> void:
	if _runtime != null:
		_runtime.set_runtime_active_modal_id(modal_id)


func _clear_battle_selection_targets() -> void:
	if _runtime != null:
		_runtime.clear_battle_selection_targets()


func _is_battle_active() -> bool:
	return _runtime != null and _runtime.is_battle_active()


func _is_modal_window_open() -> bool:
	return _runtime != null and _runtime.is_modal_window_open()


func _batch_has_updates(batch) -> bool:
	return _runtime != null and _runtime.batch_has_updates(batch)


func _update_status(message: String) -> void:
	if _runtime != null:
		_runtime.update_status(message)


func _try_open_character_info_at_battle_coord(coord: Vector2i) -> bool:
	return _runtime != null and _runtime.try_open_character_info_at_battle_coord(coord)
