class_name BattleChargeResolver
extends RefCounted

const BATTLE_TERRAIN_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle_terrain_effect_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle_unit_state.gd")
const BattleCellState = preload("res://scripts/systems/battle_cell_state.gd")
const BattleEventBatch = preload("res://scripts/systems/battle_event_batch.gd")
const BattleTerrainRules = preload("res://scripts/systems/battle_terrain_rules.gd")
const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")

const CHARGE_EFFECT_TYPE: StringName = &"charge"
const PATH_STEP_AOE_EFFECT_TYPE: StringName = &"path_step_aoe"
const TRAP_EFFECT_PREFIX = "trap_"

var _runtime_ref: WeakRef = null
var _runtime: Object = null:
	get:
		return _runtime_ref.get_ref() if _runtime_ref != null else null
	set(value):
		_runtime_ref = weakref(value) if value != null else null


func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func handle_charge_skill_command(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	validation: Dictionary,
	batch: BattleEventBatch
) -> bool:
	if not _has_runtime() or active_unit == null or skill_def == null or cast_variant == null or batch == null:
		return false

	var validation_data: Dictionary = validation if validation != null else {}
	var direction: Vector2i = validation_data.get("direction", Vector2i.ZERO)
	var requested_distance = int(validation_data.get("distance", 0))
	if direction == Vector2i.ZERO or requested_distance <= 0:
		return false

	var charge_batch: BattleEventBatch = _runtime._new_batch()
	var moved_steps = 0
	var path_step_trigger_count = 0
	var path_step_hit_count = 0
	var path_step_seen_unit_ids: Dictionary = {}
	var trap_result: Dictionary = {"triggered": false}
	var stop_reason = ""

	while moved_steps < requested_distance:
		var next_anchor = active_unit.coord + direction
		if not _can_charge_enter_anchor(active_unit, next_anchor):
			charge_batch.log_lines.append("%s 前方地形无法通过，冲锋被迫停下。" % active_unit.display_name)
			stop_reason = "terrain"
			break

		var blocker_result: Dictionary = _resolve_charge_step_blockers(active_unit, next_anchor, direction, charge_batch)
		match String(blocker_result.get("result", "continue")):
			"stop":
				stop_reason = String(blocker_result.get("reason", "blocker"))
				break
			_:
				pass

		var previous_coords = active_unit.occupied_coords.duplicate()
		if not _runtime._grid_service.move_unit(_runtime._state, active_unit, next_anchor):
			stop_reason = "blocked"
			break
		moved_steps += 1
		_runtime._append_changed_unit_id(charge_batch, active_unit.unit_id)
		_runtime._append_changed_coords(charge_batch, previous_coords)
		_runtime._append_changed_unit_coords(charge_batch, active_unit)

		var step_aoe_result = _apply_charge_path_step_aoe_effects(
			active_unit,
			skill_def,
			cast_variant,
			charge_batch,
			path_step_seen_unit_ids
		)
		if bool(step_aoe_result.get("triggered", false)):
			path_step_trigger_count += 1
			path_step_hit_count += int(step_aoe_result.get("hit_count", 0))

		trap_result = _trigger_charge_trap(active_unit)
		if bool(trap_result.get("triggered", false)):
			var trap_coord: Vector2i = trap_result.get("coord", active_unit.coord)
			_runtime._append_changed_coord(charge_batch, trap_coord)
			charge_batch.log_lines.append("%s 在 (%d, %d) 触发陷阱，冲锋被中断。" % [
				active_unit.display_name,
				trap_coord.x,
				trap_coord.y,
			])
			stop_reason = "trap"
			break

	_runtime._merge_batch(batch, charge_batch)
	if moved_steps > 0:
		batch.log_lines.append("%s 使用 %s，向%s冲锋 %d 格。" % [
			active_unit.display_name,
			_runtime._format_skill_variant_label(skill_def, cast_variant),
			_format_charge_direction(direction),
			moved_steps,
		])
		if path_step_trigger_count > 0:
			batch.log_lines.append("%s 沿途触发 %d 次旋斩，共命中 %d 个单位。" % [
				active_unit.display_name,
				path_step_trigger_count,
				path_step_hit_count,
			])
		return true
	if not charge_batch.log_lines.is_empty():
		batch.log_lines.append("%s 使用 %s，但在起步时被拦下。" % [
			active_unit.display_name,
			_runtime._format_skill_variant_label(skill_def, cast_variant),
		])
		return true
	return false


func validate_charge_command(
	active_unit: BattleUnitState,
	cast_variant: CombatCastVariantDef,
	normalized_coords: Array[Vector2i],
	base_result: Dictionary
) -> Dictionary:
	var result: Dictionary = base_result.duplicate(true) if base_result != null else {}
	if not _has_runtime() or active_unit == null or cast_variant == null or normalized_coords.is_empty():
		return result

	var target_coord: Vector2i = normalized_coords[0]
	if not _runtime._grid_service.is_inside(_runtime._state, target_coord):
		result.message = "目标地格超出战场范围。"
		return result

	var target_info: Dictionary = _resolve_charge_target(active_unit, target_coord)
	if not bool(target_info.get("valid", false)):
		result.message = "冲锋只能选择当前单位同一行或同一列的目标地格。"
		return result

	var max_distance: int = _get_charge_max_distance(active_unit, cast_variant)
	var charge_distance = int(target_info.get("distance", 0))
	if charge_distance > max_distance:
		result.message = "目标地格超出当前冲锋距离 %d。" % max_distance
		return result

	var charge_direction: Vector2i = target_info.get("direction", Vector2i.ZERO)
	result.allowed = true
	result.message = "可施放；若途中受阻会在当前可达位置停下。"
	result.target_coords = [target_coord]
	result.preview_coords = _build_charge_preview_coords(active_unit, charge_direction, charge_distance)
	result.direction = charge_direction
	result.distance = charge_distance
	return result


func build_charge_step_aoe_preview_coords(
	active_unit: BattleUnitState,
	direction: Vector2i,
	distance: int,
	path_step_aoe_effect: CombatEffectDef
) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if not _has_runtime() or active_unit == null or direction == Vector2i.ZERO or distance <= 0 or path_step_aoe_effect == null:
		return coords
	var coord_set: Dictionary = {}
	for anchor_coord in _build_charge_path_anchor_coords(active_unit, direction, distance):
		for effect_coord in _build_charge_step_effect_coords_for_anchor(active_unit, anchor_coord, path_step_aoe_effect):
			coord_set[effect_coord] = true
	for coord_variant in coord_set.keys():
		coords.append(coord_variant)
	return _runtime._sort_coords(coords)


func get_charge_path_step_aoe_effect_def(cast_variant: CombatCastVariantDef) -> CombatEffectDef:
	if cast_variant == null:
		return null
	for effect_def in cast_variant.effect_defs:
		if effect_def != null and effect_def.effect_type == PATH_STEP_AOE_EFFECT_TYPE:
			return effect_def
	return null


func is_charge_variant(cast_variant: CombatCastVariantDef) -> bool:
	return get_charge_effect_def(cast_variant) != null


func get_charge_effect_def(cast_variant: CombatCastVariantDef) -> CombatEffectDef:
	if cast_variant == null:
		return null
	for effect_def in cast_variant.effect_defs:
		if effect_def != null and effect_def.effect_type == CHARGE_EFFECT_TYPE:
			return effect_def
	return null


func _apply_charge_path_step_aoe_effects(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch,
	seen_unit_ids: Dictionary
) -> Dictionary:
	var path_step_aoe_effect = get_charge_path_step_aoe_effect_def(cast_variant)
	if active_unit == null or skill_def == null or path_step_aoe_effect == null:
		return {"triggered": false, "hit_count": 0}

	var allow_repeat_hits = bool(path_step_aoe_effect.params.get("allow_repeat_hits_across_steps", false))
	var effect_coords = _build_charge_step_effect_coords(active_unit, path_step_aoe_effect)
	var hit_count = 0
	var total_damage = 0
	var total_healing = 0
	var total_kill_count = 0
	var target_filter: StringName = _runtime._resolve_effect_target_filter(skill_def, path_step_aoe_effect)
	var stage_effect = COMBAT_EFFECT_DEF_SCRIPT.new()
	stage_effect.effect_type = &"damage"
	stage_effect.power = int(path_step_aoe_effect.power)
	stage_effect.scaling_attribute_id = path_step_aoe_effect.scaling_attribute_id
	stage_effect.defense_attribute_id = path_step_aoe_effect.defense_attribute_id
	stage_effect.resistance_attribute_id = path_step_aoe_effect.resistance_attribute_id

	for target_unit in _runtime._collect_units_in_coords(effect_coords):
		if not _runtime._is_unit_valid_for_effect(active_unit, target_unit, target_filter):
			continue
		if not allow_repeat_hits and seen_unit_ids.has(target_unit.unit_id):
			continue
		seen_unit_ids[target_unit.unit_id] = true

		var result: Dictionary = _runtime._damage_resolver.resolve_effects(active_unit, target_unit, [stage_effect])
		_runtime._mark_applied_statuses_for_turn_timing(target_unit, result.get("status_effect_ids", []))
		if not bool(result.get("applied", false)):
			continue

		hit_count += 1
		_runtime._append_changed_unit_id(batch, target_unit.unit_id)
		_runtime._append_changed_unit_coords(batch, target_unit)
		var damage = int(result.get("damage", 0))
		var healing = int(result.get("healing", 0))
		total_damage += damage
		total_healing += healing
		if damage > 0:
			batch.log_lines.append("%s 的 %s 沿途旋斩命中 %s，造成 %d 伤害。" % [
				active_unit.display_name,
				skill_def.display_name,
				target_unit.display_name,
				damage,
			])
		if healing > 0:
			batch.log_lines.append("%s 的 %s 沿途旋斩为 %s 恢复 %d 点生命。" % [
				active_unit.display_name,
				skill_def.display_name,
				target_unit.display_name,
				healing,
			])
		if not target_unit.is_alive:
			total_kill_count += 1
			_runtime._clear_defeated_unit(target_unit, batch)
			batch.log_lines.append("%s 被击倒。" % target_unit.display_name)
			_runtime._battle_rating_system.record_enemy_defeated_achievement(active_unit, target_unit)

	if total_damage > 0 or total_healing > 0 or total_kill_count > 0:
		_runtime._battle_rating_system.record_skill_effect_result(active_unit, total_damage, total_healing, total_kill_count)
	return {
		"triggered": true,
		"hit_count": hit_count,
	}


func _build_charge_step_effect_coords(active_unit: BattleUnitState, path_step_aoe_effect: CombatEffectDef) -> Array[Vector2i]:
	if active_unit == null:
		return []
	return _build_charge_step_effect_coords_for_anchor(active_unit, active_unit.coord, path_step_aoe_effect)


func _build_charge_path_anchor_coords(
	active_unit: BattleUnitState,
	direction: Vector2i,
	distance: int
) -> Array[Vector2i]:
	var anchor_coords: Array[Vector2i] = []
	if active_unit == null or direction == Vector2i.ZERO or distance <= 0:
		return anchor_coords
	var preview_anchor = active_unit.coord
	for _step in range(distance):
		preview_anchor += direction
		anchor_coords.append(preview_anchor)
	return anchor_coords


func _build_charge_step_effect_coords_for_anchor(
	active_unit: BattleUnitState,
	anchor_coord: Vector2i,
	path_step_aoe_effect: CombatEffectDef
) -> Array[Vector2i]:
	var effect_coords: Array[Vector2i] = []
	if not _has_runtime() or active_unit == null or path_step_aoe_effect == null:
		return effect_coords

	var step_shape = ProgressionDataUtils.to_string_name(path_step_aoe_effect.params.get("step_shape", "diamond"))
	var step_radius = maxi(int(path_step_aoe_effect.params.get("step_radius", 1)), 0)
	var coord_set: Dictionary = {}
	for occupied_coord in _runtime._grid_service.get_unit_target_coords(active_unit, anchor_coord):
		for effect_coord in _runtime._grid_service.get_area_coords(_runtime._state, occupied_coord, step_shape, step_radius):
			coord_set[effect_coord] = true
	for coord_variant in coord_set.keys():
		effect_coords.append(coord_variant)
	return _runtime._sort_coords(effect_coords)


func _can_charge_enter_anchor(active_unit: BattleUnitState, target_anchor: Vector2i) -> bool:
	if not _has_runtime() or _runtime._state == null or active_unit == null:
		return false
	active_unit.refresh_footprint()
	var delta = target_anchor - active_unit.coord
	if _runtime._grid_service.get_distance(active_unit.coord, target_anchor) != 1:
		return false
	var target_coords: Array[Vector2i] = _runtime._grid_service.get_unit_target_coords(active_unit, target_anchor)
	if not _can_charge_place_footprint_ignoring_occupants(active_unit, target_coords):
		return false
	if not _can_charge_step_across_edges(active_unit, delta):
		return false

	for footprint_coord in target_coords:
		var target_cell: BattleCellState = _runtime._grid_service.get_cell(_runtime._state, footprint_coord) as BattleCellState
		if target_cell == null:
			return false
		var reference_coord: Vector2i = footprint_coord - delta
		var reference_cell: BattleCellState = _runtime._grid_service.get_cell(_runtime._state, reference_coord) as BattleCellState
		if reference_cell == null:
			return false
		if absi(int(reference_cell.current_height) - int(target_cell.current_height)) > 1:
			return false
	return true


func _can_charge_place_footprint_ignoring_occupants(
	active_unit: BattleUnitState,
	target_coords: Array[Vector2i]
) -> bool:
	var target_lookup: Dictionary = {}
	for target_coord in target_coords:
		target_lookup[target_coord] = true
		if not _runtime._grid_service.is_inside(_runtime._state, target_coord):
			return false
		var target_cell: BattleCellState = _runtime._grid_service.get_cell(_runtime._state, target_coord) as BattleCellState
		if target_cell == null:
			return false
		if not BattleTerrainRules.can_unit_enter_terrain(target_cell.base_terrain, active_unit.movement_tags):
			return false
	for target_coord in target_coords:
		for direction in [Vector2i.RIGHT, Vector2i.DOWN]:
			var neighbor_coord: Vector2i = target_coord + direction
			if not target_lookup.has(neighbor_coord):
				continue
			if _is_charge_edge_blocked(target_coord, neighbor_coord, true):
				return false
	return true


func _can_charge_step_across_edges(active_unit: BattleUnitState, delta: Vector2i) -> bool:
	var frontier_from_coords: Array[Vector2i] = []
	match delta:
		Vector2i.RIGHT:
			for y in range(active_unit.footprint_size.y):
				frontier_from_coords.append(active_unit.coord + Vector2i(active_unit.footprint_size.x - 1, y))
		Vector2i.LEFT:
			for y in range(active_unit.footprint_size.y):
				frontier_from_coords.append(active_unit.coord + Vector2i(0, y))
		Vector2i.DOWN:
			for x in range(active_unit.footprint_size.x):
				frontier_from_coords.append(active_unit.coord + Vector2i(x, active_unit.footprint_size.y - 1))
		Vector2i.UP:
			for x in range(active_unit.footprint_size.x):
				frontier_from_coords.append(active_unit.coord + Vector2i(x, 0))
		_:
			return false
	for from_coord in frontier_from_coords:
		if _is_charge_edge_blocked(from_coord, from_coord + delta, false):
			return false
	return true


func _is_charge_edge_blocked(from_coord: Vector2i, to_coord: Vector2i, blocks_occupancy: bool) -> bool:
	var edge_face = _runtime._grid_service.get_edge_face(_runtime._state, from_coord, to_coord)
	if edge_face == null:
		return true
	if blocks_occupancy:
		if edge_face.blocks_occupancy():
			return true
	else:
		if edge_face.blocks_move():
			return true
	return int(edge_face.height_difference) > 1


func _resolve_charge_step_blockers(
	active_unit: BattleUnitState,
	next_anchor: Vector2i,
	direction: Vector2i,
	batch: BattleEventBatch
) -> Dictionary:
	var reserved_coords: Array = _runtime._grid_service.get_unit_target_coords(active_unit, next_anchor)
	var reserved_coord_set: Dictionary = {}
	for reserved_coord in reserved_coords:
		reserved_coord_set[reserved_coord] = true

	var seen_blockers: Dictionary = {}
	for frontier_coord in _get_charge_frontier_coords(active_unit, next_anchor):
		var blocker: BattleUnitState = _runtime._grid_service.get_unit_at_coord(_runtime._state, frontier_coord) as BattleUnitState
		if blocker == null or blocker.unit_id == active_unit.unit_id or not blocker.is_alive:
			continue
		if seen_blockers.has(blocker.unit_id):
			continue
		seen_blockers[blocker.unit_id] = true
		if active_unit.body_size < blocker.body_size:
			batch.log_lines.append("%s 被更大体型的 %s 拦住，无法继续冲锋。" % [active_unit.display_name, blocker.display_name])
			return {"result": "stop", "reason": "smaller_body"}
		if blocker.footprint_size != Vector2i.ONE:
			batch.log_lines.append("%s 被 %s 拦住，无法继续冲锋。" % [active_unit.display_name, blocker.display_name])
			return {"result": "stop", "reason": "large_blocker"}
		var blocker_result: String = _resolve_charge_blocker(active_unit, blocker, direction, reserved_coord_set, batch)
		if blocker_result != "continue":
			return {"result": blocker_result, "reason": blocker_result}
	return {"result": "continue"}


func _get_charge_frontier_coords(active_unit: BattleUnitState, next_anchor: Vector2i) -> Array[Vector2i]:
	var current_coords: Dictionary = {}
	for occupied_coord in active_unit.occupied_coords:
		current_coords[occupied_coord] = true
	var frontier_coords: Array[Vector2i] = []
	for target_coord in _runtime._grid_service.get_unit_target_coords(active_unit, next_anchor):
		if not current_coords.has(target_coord):
			frontier_coords.append(target_coord)
	return _runtime._sort_coords(frontier_coords)


func _resolve_charge_blocker(
	active_unit: BattleUnitState,
	blocker: BattleUnitState,
	direction: Vector2i,
	reserved_coord_set: Dictionary,
	batch: BattleEventBatch
) -> String:
	var side_push = _pick_charge_side_push(blocker, direction, reserved_coord_set)
	if bool(side_push.get("available", false)):
		var previous_coords = blocker.occupied_coords.duplicate()
		var side_coord: Vector2i = side_push.get("coord", blocker.coord)
		if _runtime._grid_service.move_unit_force(_runtime._state, blocker, side_coord):
			_runtime._append_changed_coords(batch, previous_coords)
			_runtime._append_changed_unit_coords(batch, blocker)
			_runtime._append_changed_unit_id(batch, blocker.unit_id)
			batch.log_lines.append("%s 将 %s 顶向侧面。" % [active_unit.display_name, blocker.display_name])
			var fall_layers: int = int(side_push.get("fall_layers", 0))
			if fall_layers > 0:
				var fall_damage: int = int(_runtime._damage_resolver.resolve_fall_damage(blocker, fall_layers))
				if fall_damage > 0:
					batch.log_lines.append("%s 因侧推跌落 %d 层，受到 %d 点坠落伤害。" % [
						blocker.display_name,
						fall_layers,
						fall_damage,
					])
					_runtime._append_changed_unit_id(batch, blocker.unit_id)
					if not blocker.is_alive:
						_runtime._clear_defeated_unit(blocker, batch)
						batch.log_lines.append("%s 被击倒。" % blocker.display_name)
			return "continue"

	var forward_coord = blocker.coord + direction
	if not reserved_coord_set.has(forward_coord):
		var previous_coords = blocker.occupied_coords.duplicate()
		if _runtime._grid_service.move_unit(_runtime._state, blocker, forward_coord):
			_runtime._append_changed_coords(batch, previous_coords)
			_runtime._append_changed_unit_coords(batch, blocker)
			_runtime._append_changed_unit_id(batch, blocker.unit_id)
			batch.log_lines.append("%s 将 %s 向前顶开。" % [active_unit.display_name, blocker.display_name])
			return "continue"

	var collision_damage: int = int(_runtime._damage_resolver.resolve_collision_damage(blocker, active_unit.body_size, blocker.body_size))
	_runtime._append_changed_unit_id(batch, blocker.unit_id)
	batch.log_lines.append("%s 撞上 %s，造成 %d 点碰撞伤害。" % [
		active_unit.display_name,
		blocker.display_name,
		collision_damage,
	])
	if not blocker.is_alive:
		_runtime._clear_defeated_unit(blocker, batch)
		batch.log_lines.append("%s 被击倒。" % blocker.display_name)
		return "continue"

	if not reserved_coord_set.has(forward_coord):
		var previous_coords = blocker.occupied_coords.duplicate()
		if _runtime._grid_service.move_unit_force(_runtime._state, blocker, forward_coord):
			_runtime._append_changed_coords(batch, previous_coords)
			_runtime._append_changed_unit_coords(batch, blocker)
			_runtime._append_changed_unit_id(batch, blocker.unit_id)
			batch.log_lines.append("%s 被强行撞退一格。" % blocker.display_name)
			return "continue"
	return "stop"


func _pick_charge_side_push(
	blocker: BattleUnitState,
	direction: Vector2i,
	reserved_coord_set: Dictionary
) -> Dictionary:
	if blocker == null:
		return {"available": false}
	var blocker_cell: BattleCellState = _runtime._grid_service.get_cell(_runtime._state, blocker.coord) as BattleCellState
	if blocker_cell == null:
		return {"available": false}
	var current_height = int(blocker_cell.current_height)
	var lower_candidates: Array[Dictionary] = []
	var level_candidates: Array[Dictionary] = []
	for side_direction in _get_side_directions_for_charge(direction):
		var side_coord = blocker.coord + side_direction
		if reserved_coord_set.has(side_coord):
			continue
		if not _runtime._grid_service.can_place_footprint(_runtime._state, side_coord, blocker.footprint_size, blocker.unit_id):
			continue
		var side_cell: BattleCellState = _runtime._grid_service.get_cell(_runtime._state, side_coord) as BattleCellState
		if side_cell == null:
			continue
		var side_height = int(side_cell.current_height)
		if side_height > current_height:
			continue
		var candidate = {
			"available": true,
			"coord": side_coord,
			"fall_layers": maxi(current_height - side_height, 0),
		}
		if side_height < current_height:
			lower_candidates.append(candidate)
		else:
			level_candidates.append(candidate)
	if not lower_candidates.is_empty():
		return lower_candidates[0]
	if not level_candidates.is_empty():
		return level_candidates[0]
	return {"available": false}


func _get_side_directions_for_charge(direction: Vector2i) -> Array[Vector2i]:
	if direction.x != 0:
		return [Vector2i.UP, Vector2i.DOWN]
	return [Vector2i.LEFT, Vector2i.RIGHT]


func _trigger_charge_trap(active_unit: BattleUnitState) -> Dictionary:
	if not _has_runtime() or active_unit == null:
		return {"triggered": false}
	for occupied_coord in _runtime._sort_coords(active_unit.occupied_coords):
		var cell: BattleCellState = _runtime._grid_service.get_cell(_runtime._state, occupied_coord) as BattleCellState
		if cell == null or cell.terrain_effect_ids.is_empty():
			continue
		var removed_ids: Array[StringName] = []
		for terrain_effect_id in cell.terrain_effect_ids.duplicate():
			if String(terrain_effect_id).begins_with(TRAP_EFFECT_PREFIX):
				cell.terrain_effect_ids.erase(terrain_effect_id)
				removed_ids.append(terrain_effect_id)
		if not removed_ids.is_empty():
			return {
				"triggered": true,
				"coord": occupied_coord,
				"terrain_effect_ids": removed_ids,
			}
	return {"triggered": false}


func _format_charge_direction(direction: Vector2i) -> String:
	if direction == Vector2i.LEFT:
		return "左"
	if direction == Vector2i.RIGHT:
		return "右"
	if direction == Vector2i.UP:
		return "上"
	if direction == Vector2i.DOWN:
		return "下"
	return "前"


func _resolve_charge_target(active_unit: BattleUnitState, target_coord: Vector2i) -> Dictionary:
	if active_unit == null:
		return {"valid": false}
	active_unit.refresh_footprint()
	var footprint_size: Vector2i = active_unit.footprint_size
	var min_x: int = active_unit.coord.x
	var max_x: int = active_unit.coord.x + footprint_size.x - 1
	var min_y: int = active_unit.coord.y
	var max_y: int = active_unit.coord.y + footprint_size.y - 1

	if target_coord.y >= min_y and target_coord.y <= max_y:
		if target_coord.x < min_x:
			return {"valid": true, "direction": Vector2i.LEFT, "distance": min_x - target_coord.x}
		if target_coord.x > max_x:
			return {"valid": true, "direction": Vector2i.RIGHT, "distance": target_coord.x - max_x}
	if target_coord.x >= min_x and target_coord.x <= max_x:
		if target_coord.y < min_y:
			return {"valid": true, "direction": Vector2i.UP, "distance": min_y - target_coord.y}
		if target_coord.y > max_y:
			return {"valid": true, "direction": Vector2i.DOWN, "distance": target_coord.y - max_y}
	return {"valid": false}


func _build_charge_preview_coords(
	active_unit: BattleUnitState,
	direction: Vector2i,
	distance: int
) -> Array[Vector2i]:
	var preview_coords: Array[Vector2i] = []
	if active_unit == null or direction == Vector2i.ZERO or distance <= 0:
		return preview_coords
	var seen_coords: Dictionary = {}
	var preview_anchor: Vector2i = active_unit.coord
	for _step in range(distance):
		preview_anchor += direction
		for occupied_coord in _runtime._grid_service.get_unit_target_coords(active_unit, preview_anchor):
			if seen_coords.has(occupied_coord):
				continue
			seen_coords[occupied_coord] = true
			preview_coords.append(occupied_coord)
	return _runtime._sort_coords(preview_coords)


func _get_charge_max_distance(active_unit: BattleUnitState, cast_variant: CombatCastVariantDef) -> int:
	var charge_effect: CombatEffectDef = get_charge_effect_def(cast_variant)
	if charge_effect == null or not _has_runtime():
		return 0
	var skill_level: int = int(_runtime._get_unit_skill_level(active_unit, charge_effect.params.get("skill_id", &"charge")))
	var max_distance: int = maxi(int(charge_effect.params.get("base_distance", 3)), 0)
	var distance_by_level: Dictionary = charge_effect.params.get("distance_by_level", {})
	for breakpoint_key in distance_by_level.keys():
		var level_breakpoint: int = int(breakpoint_key)
		if skill_level >= level_breakpoint:
			max_distance = maxi(max_distance, int(distance_by_level.get(breakpoint_key, max_distance)))
	return max_distance


func _has_runtime() -> bool:
	return _runtime != null

