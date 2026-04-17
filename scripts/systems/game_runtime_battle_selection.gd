class_name GameRuntimeBattleSelection
extends RefCounted

const BATTLE_COMMAND_SCRIPT = preload("res://scripts/systems/battle_command.gd")
const BATTLE_TARGET_COLLECTION_SERVICE_SCRIPT = preload("res://scripts/systems/battle_target_collection_service.gd")
const BattleTerrainRules = preload("res://scripts/systems/battle_terrain_rules.gd")

var _runtime_ref: WeakRef = null
var _target_collection_service = BATTLE_TARGET_COLLECTION_SERVICE_SCRIPT.new()
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
	var active_unit: BattleUnitState = _get_manual_active_unit()
	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null:
		return ""
	return skill_def.display_name


func get_selected_battle_skill_variant_name() -> String:
	var active_unit: BattleUnitState = _get_manual_active_unit()
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	if cast_variant == null:
		return ""
	return String(cast_variant.display_name)


func get_selected_battle_skill_target_coords() -> Array[Vector2i]:
	return _collect_selected_battle_skill_target_coords()


func get_selected_battle_skill_target_unit_ids() -> Array[StringName]:
	return _get_target_unit_ids_state().duplicate()


func get_selected_battle_skill_valid_target_coords() -> Array[Vector2i]:
	return _collect_selected_battle_skill_valid_target_coords()


func get_selected_battle_skill_required_coord_count() -> int:
	var active_unit: BattleUnitState = _get_manual_active_unit()
	var skill_def = _get_selected_battle_skill_def(active_unit)
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	if skill_def != null and skill_def.combat_profile != null:
		var selection_mode := _get_selected_battle_skill_target_selection_mode(active_unit)
		if selection_mode == &"multi_unit":
			return maxi(int(skill_def.combat_profile.max_target_count), int(skill_def.combat_profile.min_target_count))
		if skill_def.combat_profile.target_mode == &"unit":
			return 1
	if cast_variant == null:
		return 0
	return int(cast_variant.required_coord_count)


func select_battle_skill_slot(index: int) -> Dictionary:
	var active_unit = _get_manual_active_unit()
	if active_unit == null:
		_update_status("当前没有可手动操作的单位。")
		return _selection_error("当前没有可手动操作的单位。")
	if index < 0 or index >= active_unit.known_active_skill_ids.size():
		_update_status("该技能栏当前没有技能。")
		return _selection_error("该技能栏当前没有技能。")

	var skill_id: StringName = active_unit.known_active_skill_ids[index]
	var skill_def = _get_skill_def(skill_id)
	if skill_def == null or skill_def.combat_profile == null:
		_update_status("该技能当前不可用于战斗。")
		return _selection_error("该技能当前不可用于战斗。")

	if _get_selected_skill_id() == skill_id:
		clear_battle_skill_selection(true)
		return _selection_ok()

	var block_reason := _get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		_refresh_battle_selection_state()
		_update_status(block_reason)
		return _selection_error(block_reason)

	_set_selected_skill_id(skill_id)
	_set_selected_skill_variant_id(&"")
	_clear_battle_skill_target_selection()
	var unlocked_variants := _get_unlocked_cast_variants(active_unit, skill_def)
	if not unlocked_variants.is_empty():
		_set_selected_skill_variant_id(unlocked_variants[0].variant_id)
	_refresh_battle_selection_state()
	_update_status(_build_battle_skill_selection_status(skill_def, active_unit))
	return _selection_ok()


func cycle_selected_battle_skill_variant(step: int) -> void:
	var active_unit = _get_manual_active_unit()
	if active_unit == null:
		_update_status("当前没有可手动操作的单位。")
		return
	if _get_selected_skill_id() == &"":
		_update_status("请先用数字键选择一个技能。")
		return

	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null or skill_def.combat_profile == null or skill_def.combat_profile.cast_variants.is_empty():
		_update_status("当前技能没有可切换的施法形态。")
		return

	var unlocked_variants := _get_unlocked_cast_variants(active_unit, skill_def)
	if unlocked_variants.is_empty():
		_update_status("当前技能等级尚未解锁任何施法形态。")
		return

	var current_index := 0
	for variant_index in range(unlocked_variants.size()):
		var cast_variant = unlocked_variants[variant_index]
		if cast_variant != null and cast_variant.variant_id == _get_selected_skill_variant_id():
			current_index = variant_index
			break

	var next_index := posmod(current_index + step, unlocked_variants.size())
	_set_selected_skill_variant_id(unlocked_variants[next_index].variant_id)
	_clear_battle_skill_target_selection()
	_refresh_battle_selection_state()
	_update_status(_build_battle_skill_selection_status(skill_def, active_unit))


func clear_battle_skill_selection(announce: bool = false) -> void:
	_set_selected_skill_id(&"")
	_set_selected_skill_variant_id(&"")
	_clear_battle_skill_target_selection()
	_set_last_manual_unit_id(&"")
	if _is_battle_active():
		_refresh_battle_selection_state()
	if announce:
		_update_status("已清除当前战斗技能选择。")


func sync_selected_battle_skill_state() -> void:
	var active_unit = _get_manual_active_unit()
	var active_unit_id: StringName = active_unit.unit_id if active_unit != null else &""
	if active_unit_id != _get_last_manual_unit_id():
		_set_selected_skill_id(&"")
		_set_selected_skill_variant_id(&"")
		_clear_battle_skill_target_selection()
	_set_last_manual_unit_id(active_unit_id)
	if active_unit == null:
		return
	if _get_selected_skill_id() == &"":
		return
	if not active_unit.known_active_skill_ids.has(_get_selected_skill_id()):
		_set_selected_skill_id(&"")
		_set_selected_skill_variant_id(&"")
		_clear_battle_skill_target_selection()
		return

	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null or skill_def.combat_profile == null:
		_set_selected_skill_id(&"")
		_set_selected_skill_variant_id(&"")
		_clear_battle_skill_target_selection()
		return

	if skill_def.combat_profile.cast_variants.is_empty():
		_set_selected_skill_variant_id(&"")
		return

	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	if cast_variant == null:
		_set_selected_skill_id(&"")
		_set_selected_skill_variant_id(&"")
		_clear_battle_skill_target_selection()
		return
	_set_selected_skill_variant_id(cast_variant.variant_id)


func attempt_battle_move_to(target_coord: Vector2i) -> StringName:
	if not _is_battle_active():
		return &"full"

	_set_battle_selected_coord(target_coord)
	var battle_state = _get_battle_state()
	if battle_state == null or not battle_state.cells.has(target_coord):
		_refresh_battle_selection_state()
		_update_status("该战斗格超出当前战场范围。")
		return &"overlay"

	var active_unit = _get_manual_active_unit()
	if active_unit == null:
		_refresh_battle_selection_state()
		_update_status("等待当前单位进入可操作状态。")
		return &"overlay"

	if _is_selected_ground_skill_ready(active_unit):
		return _handle_selected_ground_skill_click(active_unit, target_coord)

	var target_unit = _get_runtime_unit_at_coord(target_coord)
	if target_unit != null:
		var selected_skill_result := _handle_selected_unit_skill_click(active_unit, target_unit)
		if selected_skill_result != &"":
			return selected_skill_result

		var skill_command = _build_selected_skill_command(active_unit, target_unit)
		if skill_command != null:
			return _issue_battle_command(skill_command)

		skill_command = _build_skill_command(active_unit, target_unit)
		if skill_command != null:
			return _issue_battle_command(skill_command)
	elif _get_selected_battle_skill_target_selection_mode(active_unit) == &"multi_unit":
		var skill_def = _get_selected_battle_skill_def(active_unit)
		if skill_def != null and skill_def.combat_profile != null:
			var min_target_count := maxi(int(skill_def.combat_profile.min_target_count), 1)
			if _get_target_unit_ids_state().size() >= min_target_count:
				return _issue_selected_multi_unit_skill(active_unit, skill_def)

	if active_unit.occupies_coord(target_coord):
		_refresh_battle_selection_state()
		_update_status("已选中当前行动单位。")
		return &"overlay"

	var move_command = BATTLE_COMMAND_SCRIPT.new()
	move_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_MOVE
	move_command.unit_id = active_unit.unit_id
	move_command.target_coord = target_coord
	var preview = _preview_battle_command(move_command)
	if preview != null and preview.allowed:
		return _issue_battle_command(move_command)

	_refresh_battle_selection_state()
	if preview != null and not preview.log_lines.is_empty():
		_update_status(String(preview.log_lines[-1]))
	else:
		_update_status("已选中战斗格 %s。" % _format_coord(target_coord))
	return &"overlay"


func reset_battle_movement() -> StringName:
	if not _is_battle_active():
		return &"full"

	var active_unit = _get_runtime_active_unit()
	if active_unit == null:
		_update_status("当前没有可聚焦的行动单位。")
		return &"overlay"

	_set_battle_selected_coord(active_unit.coord)
	_refresh_battle_selection_state()
	_update_status("已聚焦当前行动单位。")
	return &"overlay"


func _build_skill_command(active_unit, target_unit):
	if active_unit == null or target_unit == null:
		return null

	var active_skill_ids: Array[StringName] = active_unit.known_active_skill_ids
	for skill_id in active_skill_ids:
		var skill_def = _get_skill_def(skill_id)
		if skill_def == null or skill_def.combat_profile == null:
			continue
		if skill_def.combat_profile.target_mode != &"unit":
			continue
		if not _can_skill_target_unit(active_unit, target_unit, skill_def):
			continue

		var skill_command = BATTLE_COMMAND_SCRIPT.new()
		skill_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
		skill_command.unit_id = active_unit.unit_id
		skill_command.skill_id = skill_id
		skill_command.target_unit_id = target_unit.unit_id
		skill_command.target_coord = target_unit.coord
		return skill_command

	return null


func _build_selected_skill_command(active_unit, target_unit):
	if active_unit == null or target_unit == null:
		return null
	if _get_selected_skill_id() == &"":
		return null

	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null or skill_def.combat_profile == null:
		return null
	if skill_def.combat_profile.target_mode != &"unit":
		return null
	if not _can_skill_target_unit(active_unit, target_unit, skill_def):
		return null

	var skill_command = BATTLE_COMMAND_SCRIPT.new()
	skill_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	skill_command.unit_id = active_unit.unit_id
	skill_command.skill_id = _get_selected_skill_id()
	skill_command.target_unit_id = target_unit.unit_id
	skill_command.target_coord = target_unit.coord
	return skill_command


func _is_selected_ground_skill_ready(active_unit) -> bool:
	if _get_selected_battle_skill_target_selection_mode(active_unit) == &"multi_unit":
		return false
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	return cast_variant != null and cast_variant.target_mode == &"ground"


func _handle_selected_ground_skill_click(active_unit, target_coord: Vector2i) -> StringName:
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	var skill_def = _get_selected_battle_skill_def(active_unit)
	if cast_variant == null or skill_def == null:
		_refresh_battle_selection_state()
		_update_status("当前地面技能形态不可用。")
		return &"overlay"
	var block_reason := _get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		_refresh_battle_selection_state()
		_update_status(block_reason)
		return &"error"

	var required_coord_count := maxi(int(cast_variant.required_coord_count), 1)
	var queued_target_coords := _get_target_coords_state()
	var previous_targets: Array[Vector2i] = queued_target_coords.duplicate()
	var existing_index: int = queued_target_coords.find(target_coord)
	if existing_index >= 0:
		queued_target_coords.remove_at(existing_index)
		_set_target_coords_state(queued_target_coords)
		_refresh_battle_selection_state()
		_update_status("已取消目标格 %s。" % _format_coord(target_coord))
		return &"overlay"

	if required_coord_count == 1:
		_set_target_coords_state([target_coord])
	else:
		if queued_target_coords.size() >= required_coord_count:
			_update_status("该技能形态最多选择 %d 个地格；点击已选地格可取消。" % required_coord_count)
			return &"overlay"
		queued_target_coords.append(target_coord)
		_set_target_coords_state(queued_target_coords)

	var resolved_target_coords := _get_target_coords_state()
	if resolved_target_coords.size() < required_coord_count:
		_refresh_battle_selection_state()
		_update_status("%s：已选择 %d / %d 个地格。" % [
			_build_skill_variant_display_name(skill_def, cast_variant),
			resolved_target_coords.size(),
			required_coord_count,
		])
		return &"overlay"

	var skill_command = BATTLE_COMMAND_SCRIPT.new()
	skill_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	skill_command.unit_id = active_unit.unit_id
	skill_command.skill_id = _get_selected_skill_id()
	skill_command.skill_variant_id = cast_variant.variant_id
	skill_command.target_coords = resolved_target_coords.duplicate()
	skill_command.target_coord = target_coord

	var preview = _preview_battle_command(skill_command)
	if preview != null and preview.allowed:
		return _issue_battle_command(skill_command)

	_set_target_coords_state(previous_targets if required_coord_count > 1 else [])
	_refresh_battle_selection_state()
	if preview != null and not preview.log_lines.is_empty():
		_update_status(String(preview.log_lines[-1]))
	else:
		_update_status("当前地面技能目标无效。")
	return &"overlay"


func _handle_selected_unit_skill_click(active_unit, target_unit) -> StringName:
	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null or skill_def.combat_profile == null:
		return &""
	var block_reason := _get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		_refresh_battle_selection_state()
		_update_status(block_reason)
		return &"error"

	var selection_mode := StringName(skill_def.combat_profile.target_selection_mode)
	if selection_mode == &"multi_unit":
		return _toggle_selected_multi_unit_skill_target(active_unit, target_unit, skill_def)
	if skill_def.combat_profile.target_mode != &"unit":
		return &""

	var skill_command = _build_selected_skill_command(active_unit, target_unit)
	if skill_command != null:
		return _issue_battle_command(skill_command)
	return &""


func _get_selected_battle_skill_def(active_unit):
	if active_unit == null or _get_selected_skill_id() == &"":
		return null
	if not active_unit.known_active_skill_ids.has(_get_selected_skill_id()):
		return null
	return _get_skill_def(_get_selected_skill_id())


func _get_selected_battle_skill_variant(active_unit):
	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null or skill_def.combat_profile == null:
		return null
	if skill_def.combat_profile.cast_variants.is_empty():
		return _build_implicit_ground_cast_variant(skill_def) if skill_def.combat_profile.target_mode == &"ground" else null
	var unlocked_variants := _get_unlocked_cast_variants(active_unit, skill_def)
	if unlocked_variants.is_empty():
		return null
	if _get_selected_skill_variant_id() == &"":
		return unlocked_variants[0]
	for cast_variant in unlocked_variants:
		if cast_variant != null and cast_variant.variant_id == _get_selected_skill_variant_id():
			return cast_variant
	return unlocked_variants[0]


func _get_unlocked_cast_variants(active_unit, skill_def) -> Array:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return []
	var skill_level_map: Dictionary = active_unit.known_skill_level_map
	var default_skill_level := 1 if active_unit.known_active_skill_ids.has(skill_def.skill_id) else 0
	var skill_level := int(skill_level_map.get(skill_def.skill_id, default_skill_level))
	return skill_def.combat_profile.get_unlocked_cast_variants(skill_level)


func _build_implicit_ground_cast_variant(skill_def):
	if skill_def == null or skill_def.combat_profile == null or skill_def.combat_profile.target_mode != &"ground":
		return null
	var cast_variant = CombatCastVariantDef.new()
	cast_variant.variant_id = &""
	cast_variant.display_name = ""
	cast_variant.target_mode = &"ground"
	cast_variant.footprint_pattern = &"single"
	cast_variant.required_coord_count = 1
	cast_variant.effect_defs = skill_def.combat_profile.effect_defs.duplicate()
	return cast_variant


func _get_skill_def(skill_id: StringName):
	return _runtime.get_skill_defs().get(skill_id) if _runtime != null else null


func _collect_selected_battle_skill_valid_target_coords() -> Array[Vector2i]:
	if not _is_battle_active():
		return []
	var active_unit: BattleUnitState = _get_manual_active_unit()
	var skill_def = _get_selected_battle_skill_def(active_unit)
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return []
	if not _get_skill_cast_block_reason(active_unit, skill_def).is_empty():
		return []
	if _get_selected_battle_skill_target_selection_mode(active_unit) == &"multi_unit":
		return _collect_valid_unit_skill_target_coords(active_unit, skill_def, _get_target_unit_ids_state())
	if skill_def.combat_profile.target_mode == &"unit":
		return _collect_valid_unit_skill_target_coords(active_unit, skill_def, _get_target_unit_ids_state())
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	if cast_variant == null or cast_variant.target_mode != &"ground":
		return []
	return _collect_valid_ground_skill_target_coords(active_unit, skill_def, cast_variant)


func _collect_valid_unit_skill_target_coords(active_unit: BattleUnitState, skill_def, excluded_unit_ids: Array = []) -> Array[Vector2i]:
	var coord_set: Dictionary = {}
	var battle_state = _get_battle_state()
	if battle_state == null or active_unit == null or skill_def == null:
		return []
	var excluded_unit_id_set: Dictionary = {}
	for excluded_unit_id_variant in excluded_unit_ids:
		excluded_unit_id_set[StringName(excluded_unit_id_variant)] = true
	var use_anchor_coords := _get_selected_battle_skill_target_selection_mode(active_unit) == &"multi_unit"
	for unit_variant in battle_state.units.values():
		var target_unit := unit_variant as BattleUnitState
		if target_unit == null:
			continue
		if excluded_unit_id_set.has(target_unit.unit_id):
			continue
		if not _can_skill_target_unit(active_unit, target_unit, skill_def):
			continue
		if use_anchor_coords:
			coord_set[target_unit.coord] = true
		else:
			target_unit.refresh_footprint()
			for occupied_coord in target_unit.occupied_coords:
				coord_set[occupied_coord] = true
	return _sort_coords(_collect_coord_set(coord_set))


func _collect_valid_ground_skill_target_coords(active_unit: BattleUnitState, skill_def, cast_variant) -> Array[Vector2i]:
	var coord_set: Dictionary = {}
	var battle_state = _get_battle_state()
	if battle_state == null or active_unit == null or skill_def == null or cast_variant == null:
		return []
	if not _get_skill_cast_block_reason(active_unit, skill_def).is_empty():
		return []
	var queued_coords = _get_target_coords_state().duplicate()
	for coord_variant in battle_state.cells.keys():
		if coord_variant is not Vector2i:
			continue
		var target_coord: Vector2i = coord_variant
		if queued_coords.has(target_coord):
			continue
		if not _is_next_ground_target_coord_selectable(active_unit, skill_def, cast_variant, queued_coords, target_coord):
			continue
		coord_set[target_coord] = true
	return _sort_coords(_collect_coord_set(coord_set))


func _is_next_ground_target_coord_selectable(
	active_unit: BattleUnitState,
	skill_def,
	cast_variant,
	queued_coords: Array,
	candidate_coord: Vector2i
) -> bool:
	if not _get_skill_cast_block_reason(active_unit, skill_def).is_empty():
		return false
	var next_coords := queued_coords.duplicate()
	next_coords.append(candidate_coord)
	if not _are_ground_target_coords_individually_valid(active_unit, skill_def, cast_variant, next_coords):
		return false
	var required_coord_count := maxi(int(cast_variant.required_coord_count), 1)
	if next_coords.size() >= required_coord_count:
		return _is_ground_target_combo_allowed(active_unit, skill_def, cast_variant, next_coords)
	if StringName(cast_variant.footprint_pattern) == &"unordered":
		return true
	for full_coords in _build_ground_completion_sets(cast_variant, next_coords):
		if not _are_ground_target_coords_individually_valid(active_unit, skill_def, cast_variant, full_coords):
			continue
		if _is_ground_target_combo_allowed(active_unit, skill_def, cast_variant, full_coords):
			return true
	return false


func _are_ground_target_coords_individually_valid(
	active_unit: BattleUnitState,
	skill_def,
	cast_variant,
	target_coords: Array
) -> bool:
	var battle_state = _get_battle_state()
	var battle_grid_service = _get_battle_grid_service()
	if battle_state == null or battle_grid_service == null or active_unit == null or skill_def == null or skill_def.combat_profile == null or cast_variant == null:
		return false
	var seen_coords: Dictionary = {}
	for coord_variant in target_coords:
		if coord_variant is not Vector2i:
			return false
		var coord: Vector2i = coord_variant
		if seen_coords.has(coord):
			return false
		seen_coords[coord] = true
		if not battle_state.cells.has(coord):
			return false
		if battle_grid_service.get_distance_from_unit_to_coord(active_unit, coord) > _get_effective_skill_range(active_unit, skill_def):
			return false
		var cell = battle_state.cells.get(coord)
		if cell == null:
			return false
		if not cast_variant.allowed_base_terrains.is_empty():
			var normalized_allowed := false
			var normalized_cell_terrain := BattleTerrainRules.normalize_terrain_id(cell.base_terrain)
			for allowed_terrain in cast_variant.allowed_base_terrains:
				if BattleTerrainRules.normalize_terrain_id(allowed_terrain) == normalized_cell_terrain:
					normalized_allowed = true
					break
			if not normalized_allowed:
				return false
	return true


func _is_ground_target_combo_allowed(active_unit: BattleUnitState, skill_def, cast_variant, target_coords: Array) -> bool:
	if active_unit == null or skill_def == null or cast_variant == null:
		return false
	var skill_command = BATTLE_COMMAND_SCRIPT.new()
	skill_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	skill_command.unit_id = active_unit.unit_id
	skill_command.skill_id = skill_def.skill_id
	skill_command.skill_variant_id = cast_variant.variant_id
	skill_command.target_coords = _sort_coords(target_coords)
	if not skill_command.target_coords.is_empty():
		skill_command.target_coord = skill_command.target_coords[-1]
	var preview = _preview_battle_command(skill_command)
	return preview != null and preview.allowed


func _build_ground_completion_sets(cast_variant, partial_coords: Array) -> Array:
	if cast_variant == null:
		return []
	var required_coord_count := maxi(int(cast_variant.required_coord_count), 1)
	if partial_coords.size() > required_coord_count:
		return []
	var footprint_pattern := StringName(cast_variant.footprint_pattern)
	match footprint_pattern:
		&"single":
			return [_sort_coords(partial_coords)] if partial_coords.size() == 1 else []
		&"line2":
			return _build_line2_completion_sets(partial_coords)
		&"square2":
			return _build_square2_completion_sets(partial_coords)
		&"unordered":
			return [_sort_coords(partial_coords)] if partial_coords.size() == required_coord_count else []
		_:
			return []


func _build_line2_completion_sets(partial_coords: Array) -> Array:
	var completion_sets: Array = []
	var seen_signatures: Dictionary = {}
	var directions := [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]
	for coord_variant in partial_coords:
		if coord_variant is not Vector2i:
			continue
		var origin: Vector2i = coord_variant
		for direction in directions:
			var candidate_pair := _sort_coords([origin, origin + direction])
			if not _coord_array_contains_all(candidate_pair, partial_coords):
				continue
			var signature := _build_coord_signature(candidate_pair)
			if seen_signatures.has(signature):
				continue
			seen_signatures[signature] = true
			completion_sets.append(candidate_pair)
	return completion_sets


func _build_square2_completion_sets(partial_coords: Array) -> Array:
	var completion_sets: Array = []
	var seen_signatures: Dictionary = {}
	var candidate_origins: Dictionary = {}
	for coord_variant in partial_coords:
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		for offset in [Vector2i.ZERO, Vector2i.LEFT, Vector2i.UP, Vector2i(-1, -1)]:
			candidate_origins[coord + offset] = true
	for origin_variant in candidate_origins.keys():
		if origin_variant is not Vector2i:
			continue
		var origin: Vector2i = origin_variant
		var block_coords := _sort_coords([
			origin,
			origin + Vector2i.RIGHT,
			origin + Vector2i.DOWN,
			origin + Vector2i.ONE,
		])
		if not _coord_array_contains_all(block_coords, partial_coords):
			continue
		var signature := _build_coord_signature(block_coords)
		if seen_signatures.has(signature):
			continue
		seen_signatures[signature] = true
		completion_sets.append(block_coords)
	return completion_sets


func _coord_array_contains_all(full_coords: Array, partial_coords: Array) -> bool:
	var coord_set: Dictionary = {}
	for coord_variant in full_coords:
		if coord_variant is Vector2i:
			coord_set[coord_variant] = true
	for coord_variant in partial_coords:
		if coord_variant is not Vector2i:
			return false
		if not coord_set.has(coord_variant):
			return false
	return true


func _build_coord_signature(target_coords: Array) -> String:
	var segments: Array[String] = []
	for coord_variant in _sort_coords(target_coords):
		var coord: Vector2i = coord_variant
		segments.append("%d:%d" % [coord.x, coord.y])
	return "|".join(segments)


func _collect_coord_set(coord_set: Dictionary) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord_variant in coord_set.keys():
		if coord_variant is Vector2i:
			coords.append(coord_variant)
	return coords


func _can_skill_target_unit(active_unit: BattleUnitState, target_unit: BattleUnitState, skill_def) -> bool:
	if active_unit == null or target_unit == null or skill_def == null or skill_def.combat_profile == null:
		return false
	if not target_unit.is_alive:
		return false
	if not _get_skill_cast_block_reason(active_unit, skill_def).is_empty():
		return false
	if active_unit.current_ap < int(skill_def.combat_profile.ap_cost):
		return false
	if not _skill_target_filter_matches_unit(active_unit, target_unit, skill_def.combat_profile.target_team_filter):
		return false
	active_unit.refresh_footprint()
	target_unit.refresh_footprint()
	var battle_grid_service = _get_battle_grid_service()
	if battle_grid_service == null:
		return false
	return battle_grid_service.get_distance_between_units(active_unit, target_unit) <= _get_effective_skill_range(active_unit, skill_def)


func _skill_target_filter_matches_unit(active_unit: BattleUnitState, target_unit: BattleUnitState, target_team_filter: StringName) -> bool:
	if active_unit == null or target_unit == null:
		return false
	var is_same_unit := active_unit.unit_id == target_unit.unit_id
	var is_same_faction := String(active_unit.faction_id) == String(target_unit.faction_id)
	match target_team_filter:
		&"enemy":
			return not is_same_faction
		&"ally":
			return is_same_faction
		&"self":
			return is_same_unit
		&"", &"any":
			return true
		_:
			return true


func _get_effective_skill_range(active_unit: BattleUnitState, skill_def) -> int:
	if skill_def == null or skill_def.combat_profile == null:
		return 0
	var skill_range := int(skill_def.combat_profile.range_value)
	if active_unit != null and active_unit.has_status_effect(&"archer_range_up"):
		skill_range += 1
	return skill_range


func _get_skill_cast_block_reason(active_unit: BattleUnitState, skill_def) -> String:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return "技能或目标无效。"
	var combat_profile = skill_def.combat_profile
	var cooldown := int(active_unit.cooldowns.get(skill_def.skill_id, 0))
	if cooldown > 0:
		return "%s 仍在冷却中（%d）。" % [skill_def.display_name, cooldown]
	if active_unit.current_ap < int(combat_profile.ap_cost):
		return "行动点不足，无法施放该技能。"
	if active_unit.current_mp < int(combat_profile.mp_cost):
		return "法力不足，无法施放该技能。"
	if active_unit.current_stamina < int(combat_profile.stamina_cost):
		return "体力不足，无法施放该技能。"
	if active_unit.current_aura < int(combat_profile.aura_cost):
		return "斗气不足，无法施放该技能。"
	return ""


func _build_battle_skill_selection_status(skill_def, active_unit) -> String:
	if skill_def == null:
		return "当前技能不可用。"
	var block_reason := _get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		return "%s按 Esc 清除选择。" % block_reason
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	var selection_mode := _get_selected_battle_skill_target_selection_mode(active_unit)
	if selection_mode == &"multi_unit":
		var min_target_count := maxi(int(skill_def.combat_profile.min_target_count), 1)
		var max_target_count := maxi(int(skill_def.combat_profile.max_target_count), min_target_count)
		return _build_multi_unit_target_status(skill_def, min_target_count, max_target_count)
	if cast_variant == null:
		if skill_def.combat_profile.target_mode == &"unit" and (selection_mode == &"self" or skill_def.combat_profile.target_team_filter == &"self"):
			return "已选择技能 %s。点击自身即可施放，Esc 清除选择。" % skill_def.display_name
		return "已选择技能 %s。左键选择目标单位施放，Esc 清除选择。" % skill_def.display_name
	if skill_def.combat_profile.target_mode == &"unit":
		if selection_mode == &"self" or skill_def.combat_profile.target_team_filter == &"self":
			return "已选择 %s，点击自身即可施放，Esc 清除选择。" % _build_skill_variant_display_name(skill_def, cast_variant)
		return "已选择 %s，左键选择目标单位施放，Esc 清除选择。" % _build_skill_variant_display_name(skill_def, cast_variant)
	return "已选择 %s，需目标 %d 格。左键逐格选点，Q/E 切换形态，Esc 清除选择。" % [
		_build_skill_variant_display_name(skill_def, cast_variant),
		int(cast_variant.required_coord_count),
	]


func _build_skill_variant_display_name(skill_def, cast_variant) -> String:
	if skill_def == null:
		return "技能"
	if cast_variant == null or String(cast_variant.display_name).is_empty():
		return skill_def.display_name
	return "%s·%s" % [skill_def.display_name, String(cast_variant.display_name)]


func _toggle_selected_multi_unit_skill_target(active_unit: BattleUnitState, target_unit: BattleUnitState, skill_def) -> StringName:
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return &"overlay"
	var block_reason := _get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		_refresh_battle_selection_state()
		_update_status(block_reason)
		return &"overlay"

	var min_target_count := maxi(int(skill_def.combat_profile.min_target_count), 1)
	var max_target_count := maxi(int(skill_def.combat_profile.max_target_count), min_target_count)
	var queued_target_unit_ids := _get_target_unit_ids_state()
	if target_unit == null:
		if queued_target_unit_ids.size() >= min_target_count:
			return _issue_selected_multi_unit_skill(active_unit, skill_def)
		_refresh_battle_selection_state()
		_update_status(_build_multi_unit_target_status(skill_def, min_target_count, max_target_count))
		return &"overlay"

	var target_unit_id: StringName = target_unit.unit_id
	var existing_index: int = queued_target_unit_ids.find(target_unit_id)
	if existing_index >= 0:
		queued_target_unit_ids.remove_at(existing_index)
		_set_target_unit_ids_state(queued_target_unit_ids)
		_refresh_selected_unit_target_coords_from_queue()
		_sync_multi_unit_confirm_focus(active_unit, min_target_count, max_target_count)
		_refresh_battle_selection_state()
		_update_status(_build_multi_unit_target_status(skill_def, min_target_count, max_target_count))
		return &"overlay"

	if not _can_skill_target_unit(active_unit, target_unit, skill_def):
		if target_unit.unit_id == active_unit.unit_id and queued_target_unit_ids.size() >= min_target_count:
			return _issue_selected_multi_unit_skill(active_unit, skill_def)
		_refresh_battle_selection_state()
		_update_status("该单位不是当前技能的合法目标。")
		return &"overlay"

	if queued_target_unit_ids.size() >= max_target_count:
		_update_status("该技能最多选择 %d 个单位目标；点击已选目标可取消。" % max_target_count)
		return &"overlay"

	queued_target_unit_ids.append(target_unit_id)
	_set_target_unit_ids_state(queued_target_unit_ids)
	_refresh_selected_unit_target_coords_from_queue()
	if queued_target_unit_ids.size() >= max_target_count:
		return _issue_selected_multi_unit_skill(active_unit, skill_def)
	_sync_multi_unit_confirm_focus(active_unit, min_target_count, max_target_count)
	_refresh_battle_selection_state()
	_update_status(_build_multi_unit_target_status(skill_def, min_target_count, max_target_count))
	return &"overlay"


func _issue_selected_multi_unit_skill(active_unit: BattleUnitState, skill_def) -> StringName:
	if active_unit == null or skill_def == null:
		return &"overlay"
	var skill_command = BATTLE_COMMAND_SCRIPT.new()
	skill_command.command_type = BATTLE_COMMAND_SCRIPT.TYPE_SKILL
	skill_command.unit_id = active_unit.unit_id
	skill_command.skill_id = _get_selected_skill_id()
	skill_command.skill_variant_id = _get_selected_skill_variant_id()
	skill_command.target_unit_ids = _get_target_unit_ids_state().duplicate()
	if not skill_command.target_unit_ids.is_empty():
		skill_command.target_unit_id = skill_command.target_unit_ids[0]
		var first_target: BattleUnitState = _get_battle_unit_by_id(skill_command.target_unit_id)
		if first_target != null:
			skill_command.target_coord = first_target.coord
	var preview = _preview_battle_command(skill_command)
	if preview != null and preview.allowed:
		return _issue_battle_command(skill_command)
	_refresh_battle_selection_state()
	if preview != null and not preview.log_lines.is_empty():
		_update_status(String(preview.log_lines[-1]))
	else:
		_update_status("当前单位技能目标无效。")
	return &"overlay"


func _sync_multi_unit_confirm_focus(active_unit: BattleUnitState, min_target_count: int, max_target_count: int) -> void:
	if active_unit == null:
		return
	var selected_count := _get_target_unit_ids_state().size()
	if selected_count >= min_target_count and selected_count < max_target_count:
		_set_battle_selected_coord(active_unit.coord)


func _build_multi_unit_target_status(skill_def, min_target_count: int, max_target_count: int) -> String:
	var selected_count: int = _get_target_unit_ids_state().size()
	var title: String = skill_def.display_name if skill_def != null else "技能"
	if selected_count <= 0:
		return "已选择技能 %s。左键逐个点选单位目标，Esc 清除选择。" % title
	if selected_count < min_target_count:
		return "已选择 %s，已选择 %d / %d 个单位目标。继续点选，或点击已选目标取消，Esc 清除选择。" % [
			title,
			selected_count,
			min_target_count,
		]
	if selected_count < max_target_count:
		return "已选择 %s，已选择 %d / %d 个单位目标。还可继续添加，点击已选目标可取消，Esc 清除选择。" % [
			title,
			selected_count,
			max_target_count,
		]
	return "已选择 %s，已选择 %d / %d 个单位目标。已达到上限，点击已选目标可取消，Esc 清除选择。" % [
		title,
		selected_count,
		max_target_count,
	]


func _refresh_selected_unit_target_coords_from_queue() -> void:
	var target_coords: Array[Vector2i] = []
	var battle_state = _get_battle_state()
	if battle_state == null:
		_set_target_coords_state(target_coords)
		return
	for target_unit_id in _get_target_unit_ids_state():
		var target_unit: BattleUnitState = _get_battle_unit_by_id(target_unit_id)
		if target_unit == null:
			continue
		target_coords.append(target_unit.coord)
	_set_target_coords_state(_sort_coords(target_coords))


func _collect_selected_battle_skill_target_coords() -> Array[Vector2i]:
	if not _get_target_unit_ids_state().is_empty():
		_refresh_selected_unit_target_coords_from_queue()
	var active_unit: BattleUnitState = _get_manual_active_unit()
	var skill_def = _get_selected_battle_skill_def(active_unit)
	var target_coords := _get_target_coords_state().duplicate()
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return target_coords
	var cast_variant = _get_selected_battle_skill_variant(active_unit)
	if StringName(skill_def.combat_profile.target_mode) == &"ground":
		if cast_variant == null or cast_variant.target_mode != &"ground":
			return target_coords
		if target_coords.size() < maxi(int(cast_variant.required_coord_count), 1):
			return target_coords
	var collected_target_coords := _target_collection_service.collect_combat_profile_target_coords(
		_get_battle_state(),
		_get_battle_grid_service(),
		active_unit.coord,
		skill_def.combat_profile,
		target_coords,
		active_unit,
		_collect_selected_target_units(active_unit, skill_def)
	)
	if bool(collected_target_coords.get("handled", false)):
		return _sort_coords(collected_target_coords.get("target_coords", []))
	return target_coords


func _collect_selected_target_units(active_unit: BattleUnitState, skill_def) -> Array:
	var target_units: Array = []
	if active_unit == null or skill_def == null or skill_def.combat_profile == null:
		return target_units
	for target_unit_id in _get_target_unit_ids_state():
		var target_unit: BattleUnitState = _get_battle_unit_by_id(target_unit_id)
		if target_unit != null:
			target_units.append(target_unit)
	if not target_units.is_empty():
		return target_units
	if _get_selected_battle_skill_target_selection_mode(active_unit) == &"self" \
		or StringName(skill_def.combat_profile.target_team_filter) == &"self" \
		or StringName(skill_def.combat_profile.area_pattern) == &"self":
		target_units.append(active_unit)
	return target_units


func _clear_battle_skill_target_selection() -> void:
	_clear_target_coords_state()
	_clear_target_unit_ids_state()


func _get_selected_battle_skill_target_selection_mode(active_unit) -> StringName:
	var skill_def = _get_selected_battle_skill_def(active_unit)
	if skill_def == null or skill_def.combat_profile == null:
		return &"single_unit"
	var selection_mode := StringName(skill_def.combat_profile.target_selection_mode)
	return selection_mode if selection_mode != &"" else &"single_unit"


func _sort_coords(target_coords: Array) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	for coord_variant in target_coords:
		if coord_variant is Vector2i:
			coords.append(coord_variant)
	coords.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		if a.y == b.y:
			return a.x < b.x
		return a.y < b.y
	)
	return coords


func _get_manual_active_unit() -> BattleUnitState:
	return _runtime.get_manual_battle_unit() if _runtime != null else null


func _get_runtime_active_unit() -> BattleUnitState:
	return _runtime.get_runtime_battle_active_unit() if _runtime != null else null


func _get_runtime_unit_at_coord(coord: Vector2i) -> BattleUnitState:
	return _runtime.get_runtime_battle_unit_at_coord(coord) if _runtime != null else null


func _get_battle_unit_by_id(unit_id: StringName) -> BattleUnitState:
	return _runtime.get_runtime_battle_unit_by_id(unit_id) if _runtime != null else null


func _get_battle_state() -> BattleState:
	return _runtime.get_battle_state() if _runtime != null else null


func _get_battle_grid_service():
	return _runtime.get_battle_grid_service() if _runtime != null else null


func _preview_battle_command(command):
	return _runtime.preview_battle_command(command) if _runtime != null else null


func _issue_battle_command(command) -> StringName:
	return _runtime.issue_battle_command(command) if _runtime != null else &"overlay"


func _refresh_battle_selection_state() -> void:
	if _runtime != null:
		_runtime.refresh_battle_selection_state()


func _update_status(message: String) -> void:
	if _runtime != null:
		_runtime.update_status(message)


func _format_coord(coord: Vector2i) -> String:
	return _runtime.format_coord(coord) if _runtime != null else "(%d,%d)" % [coord.x, coord.y]


func _is_battle_active() -> bool:
	return _runtime != null and _runtime.is_battle_active()


func _get_selected_skill_id() -> StringName:
	return _runtime.get_selected_battle_skill_id() if _runtime != null else &""


func _set_selected_skill_id(skill_id: StringName) -> void:
	if _runtime != null:
		_runtime.set_battle_selection_skill_id(skill_id)


func _get_selected_skill_variant_id() -> StringName:
	return _runtime.get_selected_battle_skill_variant_id() if _runtime != null else &""


func _set_selected_skill_variant_id(variant_id: StringName) -> void:
	if _runtime != null:
		_runtime.set_battle_selection_skill_variant_id(variant_id)


func _get_last_manual_unit_id() -> StringName:
	return _runtime.get_battle_selection_last_manual_unit_id() if _runtime != null else &""


func _set_last_manual_unit_id(unit_id: StringName) -> void:
	if _runtime != null:
		_runtime.set_battle_selection_last_manual_unit_id(unit_id)


func _get_target_coords_state() -> Array[Vector2i]:
	return _runtime.get_battle_selection_target_coords_state() if _runtime != null else []


func _set_target_coords_state(target_coords: Array[Vector2i]) -> void:
	if _runtime != null:
		_runtime.set_battle_selection_target_coords_state(target_coords)


func _clear_target_coords_state() -> void:
	_set_target_coords_state([])


func _get_target_unit_ids_state() -> Array[StringName]:
	return _runtime.get_battle_selection_target_unit_ids_state() if _runtime != null else []


func _set_target_unit_ids_state(target_unit_ids: Array[StringName]) -> void:
	if _runtime != null:
		_runtime.set_battle_selection_target_unit_ids_state(target_unit_ids)


func _clear_target_unit_ids_state() -> void:
	_set_target_unit_ids_state([])


func _set_battle_selected_coord(coord: Vector2i) -> void:
	if _runtime != null:
		_runtime.set_runtime_battle_selected_coord(coord)


func _selection_ok() -> Dictionary:
	return {
		"ok": true,
	}


func _selection_error(message: String) -> Dictionary:
	return {
		"ok": false,
		"message": message,
	}
