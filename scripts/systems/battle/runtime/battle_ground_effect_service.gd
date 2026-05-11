class_name BattleGroundEffectService
extends RefCounted

const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")

const BattleCellState = preload("res://scripts/systems/battle/core/battle_cell_state.gd")

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")

const BattleEdgeFeatureState = preload("res://scripts/systems/battle/core/battle_edge_feature_state.gd")

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const BattleTerrainRules = preload("res://scripts/systems/battle/terrain/battle_terrain_rules.gd")

const CombatCastVariantDef = preload("res://scripts/player/progression/combat_cast_variant_def.gd")

const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")

const SkillDef = preload("res://scripts/player/progression/skill_def.gd")

const WIND_PUSH_MODE: StringName = &"wind_push"

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


func _append_result_report_entry(batch: BattleEventBatch, result: Dictionary) -> void:
	if _runtime == null:
		return
	_runtime._append_result_report_entry(batch, result)

func mark_applied_statuses_for_turn_timing(target_unit: BattleUnitState, status_effect_ids: Variant) -> void:
	if _runtime == null:
		return
	_runtime.mark_applied_statuses_for_turn_timing(target_unit, status_effect_ids)

func append_result_source_status_effects(batch: BattleEventBatch, source_unit: BattleUnitState, result: Dictionary) -> void:
	if _runtime == null:
		return
	_runtime.append_result_source_status_effects(batch, source_unit, result)

func _record_effect_metrics(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	damage: int,
	healing: int,
	kill_count: int
) -> void:
	if _runtime == null:
		return
	_runtime._record_effect_metrics(source_unit, target_unit, damage, healing, kill_count)

func _record_unit_defeated(unit_state: BattleUnitState) -> void:
	if _runtime == null:
		return
	_runtime._record_unit_defeated(unit_state)

func append_damage_result_log_lines(
	batch: BattleEventBatch,
	subject_label: String,
	target_display_name: String,
	result: Dictionary
) -> void:
	if _runtime == null:
		return
	_runtime.append_damage_result_log_lines(batch, subject_label, target_display_name, result)

func _build_skill_log_subject_label(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> String:
	if _runtime == null:
		return ""
	return _runtime._build_skill_log_subject_label(source_unit, skill_def, cast_variant)

func _apply_on_kill_gain_resources_effects(
	source_unit: BattleUnitState,
	defeated_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	batch: BattleEventBatch
) -> void:
	if _runtime == null:
		return
	_runtime._apply_on_kill_gain_resources_effects(source_unit, defeated_unit, skill_def, effect_defs, batch)

func _is_crown_break_target_eligible(active_unit: BattleUnitState, target_unit: BattleUnitState) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_crown_break_target_eligible(active_unit, target_unit)

func _is_crown_break_skill(skill_id: StringName) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_crown_break_skill(skill_id)

func _record_vajra_body_mastery_from_incoming_damage(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	result: Dictionary,
	batch: BattleEventBatch = null
) -> void:
	if _runtime == null:
		return
	_runtime._record_vajra_body_mastery_from_incoming_damage(source_unit, target_unit, skill_def, result, batch)

func _collect_units_in_coords(effect_coords: Array[Vector2i]) -> Array[BattleUnitState]:
	if _runtime == null:
		return []
	return _runtime._collect_units_in_coords(effect_coords)

func _apply_unit_shield_effects(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	shield_roll_context: Dictionary = {}
) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._apply_unit_shield_effects(source_unit, target_unit, skill_def, effect_defs, shield_roll_context)

func _resolve_effect_target_filter(skill_def: SkillDef, effect_def: CombatEffectDef) -> StringName:
	if _runtime == null:
		return &""
	return _runtime._resolve_effect_target_filter(skill_def, effect_def)

func _is_unit_valid_for_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	target_team_filter: StringName
) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_unit_valid_for_effect(source_unit, target_unit, target_team_filter)

func _flush_last_stand_mastery_records(batch: BattleEventBatch) -> void:
	if _runtime == null:
		return
	_runtime._flush_last_stand_mastery_records(batch)

func _append_changed_coord(batch: BattleEventBatch, coord: Vector2i) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_coord(batch, coord)

func _append_changed_coords(batch: BattleEventBatch, coords: Array[Vector2i]) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_coords(batch, coords)

func _append_changed_unit_id(batch: BattleEventBatch, unit_id: StringName) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_unit_id(batch, unit_id)

func _append_changed_unit_coords(batch: BattleEventBatch, unit_state: BattleUnitState) -> void:
	if _runtime == null:
		return
	_runtime._append_changed_unit_coords(batch, unit_state)

func _collect_defeated_unit_loot(unit_state: BattleUnitState, killer_unit: BattleUnitState = null) -> void:
	if _runtime == null:
		return
	_runtime._collect_defeated_unit_loot(unit_state, killer_unit)

func _clear_defeated_unit(unit_state: BattleUnitState, batch: BattleEventBatch = null) -> void:
	if _runtime == null:
		return
	_runtime._clear_defeated_unit(unit_state, batch)

func _sort_coords(target_coords: Variant) -> Array[Vector2i]:
	if _runtime == null:
		return []
	return _runtime._sort_coords(target_coords)

func _get_unit_skill_level(unit_state: BattleUnitState, skill_id: StringName) -> int:
	if _runtime == null:
		return 0
	return _runtime._get_unit_skill_level(unit_state, skill_id)

func _get_skill_cast_block_reason(active_unit: BattleUnitState, skill_def: SkillDef) -> String:
	if _runtime == null:
		return ""
	return _runtime._get_skill_cast_block_reason(active_unit, skill_def)

func _get_effective_skill_costs(active_unit: BattleUnitState, skill_def: SkillDef) -> Dictionary:
	if _runtime == null:
		return {}
	return _runtime._get_effective_skill_costs(active_unit, skill_def)

func _get_effective_skill_range(active_unit: BattleUnitState, skill_def: SkillDef) -> int:
	if _runtime == null:
		return 0
	return _runtime._get_effective_skill_range(active_unit, skill_def)

func _is_movement_blocked(unit_state: BattleUnitState) -> bool:
	if _runtime == null:
		return false
	return _runtime._is_movement_blocked(unit_state)


func _resolve_ground_spell_control_after_cost(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	spent_mp: int,
	batch: BattleEventBatch
) -> Dictionary:
	if _runtime._damage_resolver == null or _runtime._magic_backlash_resolver == null:
		return {}
	if not _runtime._magic_backlash_resolver.should_resolve_spell_control(skill_def):
		return {}
	var skill_level = _get_unit_skill_level(active_unit, skill_def.skill_id if skill_def != null else &"")
	var control_metadata = _runtime._damage_resolver.resolve_spell_control_check(active_unit, {
		"battle_state": _runtime._state,
		"skill_id": skill_def.skill_id if skill_def != null else &"",
	})
	var control_context = _runtime._magic_backlash_resolver.apply_spell_control_after_cost(
		active_unit,
		skill_def,
		skill_level,
		spent_mp,
		control_metadata,
		batch
	)
	_append_changed_unit_id(batch, active_unit.unit_id if active_unit != null else &"")
	return control_context

func _resolve_unit_spell_control_after_cost(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	batch: BattleEventBatch
) -> Dictionary:
	if _runtime._damage_resolver == null or _runtime._magic_backlash_resolver == null:
		return {}
	if not _runtime._magic_backlash_resolver.should_resolve_spell_control(skill_def):
		return {}
	var skill_level = _get_unit_skill_level(active_unit, skill_def.skill_id if skill_def != null else &"")
	var costs = _get_effective_skill_costs(active_unit, skill_def)
	var spent_mp = int(costs.get("mp_cost", skill_def.combat_profile.mp_cost if skill_def != null and skill_def.combat_profile != null else 0))
	var control_metadata = _runtime._damage_resolver.resolve_spell_control_check(active_unit, {
		"battle_state": _runtime._state,
		"skill_id": skill_def.skill_id if skill_def != null else &"",
	})
	var control_context = _runtime._magic_backlash_resolver.apply_spell_control_after_cost(
		active_unit,
		skill_def,
		skill_level,
		spent_mp,
		control_metadata,
		batch
	)
	_append_changed_unit_id(batch, active_unit.unit_id if active_unit != null else &"")
	return control_context

func _apply_ground_precast_special_effects(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	target_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> bool:
	if _get_ground_relocation_effect_def(skill_def, cast_variant) == null:
		return true
	return _apply_ground_relocation(active_unit, skill_def, cast_variant, target_coords, batch)

func _apply_ground_relocation(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	target_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> bool:
	if _runtime._state == null or active_unit == null or target_coords.is_empty():
		return false
	var effect_def := _get_ground_relocation_effect_def(skill_def, cast_variant)
	if effect_def == null:
		return false
	return _apply_ground_relocation_with_mode(
		active_unit,
		target_coords,
		batch,
		_get_effect_forced_move_mode(effect_def)
	)

func _apply_ground_relocation_with_mode(
	active_unit: BattleUnitState,
	target_coords: Array[Vector2i],
	batch: BattleEventBatch,
	move_mode: StringName
) -> bool:
	if _runtime._state == null or active_unit == null or target_coords.is_empty():
		return false
	var landing_coord = target_coords[0]
	if active_unit.coord == landing_coord:
		return true

	var previous_anchor = active_unit.coord
	var previous_coords = active_unit.occupied_coords.duplicate()
	if not _runtime._grid_service.move_unit_force(_runtime._state, active_unit, landing_coord):
		return false

	_append_changed_coords(batch, previous_coords)
	_append_changed_unit_coords(batch, active_unit)
	_append_changed_unit_id(batch, active_unit.unit_id)
	var move_label := "闪现至" if move_mode == &"blink" else "跳至"
	batch.log_lines.append("%s 从 (%d, %d) %s (%d, %d)。" % [
		active_unit.display_name,
		previous_anchor.x,
		previous_anchor.y,
		move_label,
		landing_coord.x,
		landing_coord.y,
	])
	return true

func _apply_ground_jump_relocation(
	active_unit: BattleUnitState,
	target_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> bool:
	return _apply_ground_relocation_with_mode(active_unit, target_coords, batch, &"jump")

func _get_ground_relocation_effect_def(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> CombatEffectDef:
	if cast_variant != null:
		for effect_def in cast_variant.effect_defs:
			if _is_ground_relocation_effect(effect_def):
				return effect_def
	if skill_def != null and skill_def.combat_profile != null:
		for effect_def in skill_def.combat_profile.effect_defs:
			if _is_ground_relocation_effect(effect_def):
				return effect_def
	return null

func _get_ground_jump_effect_def(skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> CombatEffectDef:
	var effect_def := _get_ground_relocation_effect_def(skill_def, cast_variant)
	if _get_effect_forced_move_mode(effect_def) == &"jump":
		return effect_def
	return null

func _is_ground_jump_effect(effect_def: CombatEffectDef) -> bool:
	return effect_def != null \
		and effect_def.effect_type == &"forced_move" \
		and _get_effect_forced_move_mode(effect_def) == &"jump"

func _is_ground_relocation_effect(effect_def: CombatEffectDef) -> bool:
	return effect_def != null \
		and effect_def.effect_type == &"forced_move" \
		and _is_ground_relocation_mode(_get_effect_forced_move_mode(effect_def))

func _is_ground_relocation_mode(mode: StringName) -> bool:
	return mode == &"jump" or mode == &"blink"

func _can_use_ground_relocation(
	active_unit: BattleUnitState,
	landing_coord: Vector2i,
	effect_def: CombatEffectDef
) -> bool:
	if effect_def == null:
		return false
	match _get_effect_forced_move_mode(effect_def):
		&"jump":
			return _runtime._grid_service.can_jump_arc(_runtime._state, active_unit, landing_coord, effect_def)
		&"blink":
			return _runtime._grid_service.can_blink_to_coord(_runtime._state, active_unit, landing_coord, effect_def)
		_:
			return false

func _get_effect_forced_move_mode(effect_def: CombatEffectDef) -> StringName:
	if effect_def == null:
		return &""
	if effect_def.forced_move_mode != &"":
		return effect_def.forced_move_mode
	return &""

func _build_ground_effect_coords(
	skill_def: SkillDef,
	target_coords: Array,
	source_coord: Vector2i = Vector2i(-1, -1),
	active_unit: BattleUnitState = null,
	cast_variant = null
) -> Array[Vector2i]:
	var normalized_target_coords: Array[Vector2i] = []
	for target_coord in target_coords:
		normalized_target_coords.append(target_coord)
	if cast_variant != null and cast_variant.params != null \
			and cast_variant.params.has("square2_corner") and normalized_target_coords.size() == 1:
		var center: Vector2i = normalized_target_coords[0]
		var expanded: Array[Vector2i] = []
		match cast_variant.params["square2_corner"]:
			"top_left":
				expanded = [center, Vector2i(center.x + 1, center.y),
					Vector2i(center.x, center.y + 1), Vector2i(center.x + 1, center.y + 1)]
			"top_right":
				expanded = [Vector2i(center.x - 1, center.y), center,
					Vector2i(center.x - 1, center.y + 1), Vector2i(center.x, center.y + 1)]
			"bottom_left":
				expanded = [Vector2i(center.x, center.y - 1), Vector2i(center.x + 1, center.y - 1),
					center, Vector2i(center.x + 1, center.y)]
			"bottom_right":
				expanded = [Vector2i(center.x - 1, center.y - 1), Vector2i(center.x, center.y - 1),
					Vector2i(center.x - 1, center.y), center]
		var valid: Array[Vector2i] = []
		for c in expanded:
			if _runtime._state != null and _runtime._grid_service.is_inside(_runtime._state, c):
				valid.append(c)
		if not valid.is_empty():
			return _sort_coords(valid)
	if _runtime._state == null or skill_def == null or skill_def.combat_profile == null:
		return _sort_coords(normalized_target_coords)
	var skill_level = _get_unit_skill_level(active_unit, skill_def.skill_id)
	var collected_target_coords = _runtime._target_collection_service.collect_combat_profile_target_coords(
		_runtime._state,
		_runtime._grid_service,
		source_coord,
		skill_def.combat_profile,
		normalized_target_coords,
		null,
		[],
		skill_level
	)
	if bool(collected_target_coords.get("handled", false)):
		return _sort_coords(collected_target_coords.get("target_coords", []))
	return _sort_coords(normalized_target_coords)

func _collect_ground_unit_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	return _runtime._skill_resolution_rules.collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit)

func _collect_ground_terrain_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	return _runtime._skill_resolution_rules.collect_ground_terrain_effect_defs(skill_def, cast_variant, active_unit)

func _collect_ground_effect_defs(
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	active_unit: BattleUnitState = null
) -> Array[CombatEffectDef]:
	return _runtime._skill_resolution_rules.collect_ground_effect_defs(skill_def, cast_variant, active_unit)

func _collect_ground_preview_unit_ids(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i]
) -> Array[StringName]:
	var target_unit_ids: Array[StringName] = []
	for target_unit in _collect_units_in_coords(effect_coords):
		for effect_def in effect_defs:
			if _is_unit_valid_for_effect(source_unit, target_unit, _resolve_effect_target_filter(skill_def, effect_def)):
				target_unit_ids.append(target_unit.unit_id)
				break
	return target_unit_ids

func _build_ground_forced_move_context(source_unit: BattleUnitState, target_coords: Array[Vector2i]) -> Dictionary:
	if source_unit == null or target_coords.is_empty():
		return {}
	var direction := _normalize_axis_direction(target_coords[0] - source_unit.coord)
	if direction == Vector2i.ZERO:
		return {}
	return {"direction": direction}

func _normalize_axis_direction(direction: Vector2i) -> Vector2i:
	if direction == Vector2i.ZERO:
		return Vector2i.ZERO
	var abs_x := absi(direction.x)
	var abs_y := absi(direction.y)
	if abs_x >= abs_y and abs_x > 0:
		return Vector2i(1 if direction.x > 0 else -1, 0)
	if abs_y > 0:
		return Vector2i(0, 1 if direction.y > 0 else -1)
	return Vector2i.ZERO

func _is_wind_push_effect(effect_def: CombatEffectDef) -> bool:
	if effect_def == null:
		return false
	if effect_def.effect_type != &"forced_move":
		return false
	return effect_def.forced_move_mode == WIND_PUSH_MODE

func _collect_wind_push_effects(effect_defs: Array[CombatEffectDef]) -> Array[CombatEffectDef]:
	var wind_push_effects: Array[CombatEffectDef] = []
	var seen: Dictionary = {}
	for effect_def in effect_defs:
		if not _is_wind_push_effect(effect_def):
			continue
		var instance_id := effect_def.get_instance_id()
		if seen.has(instance_id):
			continue
		seen[instance_id] = true
		wind_push_effects.append(effect_def)
	return wind_push_effects

func _build_effect_instance_lookup(effect_defs: Array[CombatEffectDef]) -> Dictionary:
	var lookup: Dictionary = {}
	for effect_def in effect_defs:
		if effect_def != null:
			lookup[effect_def.get_instance_id()] = true
	return lookup

func _dot_coord(coord: Vector2i, direction: Vector2i) -> int:
	return coord.x * direction.x + coord.y * direction.y

func _perpendicular_coord(coord: Vector2i, direction: Vector2i) -> int:
	if direction.x != 0:
		return coord.y
	return coord.x

func _sort_wind_push_units_near_to_far(units: Array, direction: Vector2i) -> Array:
	var sorted: Array = []
	for unit_state in units:
		if unit_state != null and unit_state.is_alive:
			sorted.append(unit_state)
	sorted.sort_custom(func(left, right) -> bool:
		var left_projection: int = _dot_coord(left.coord, direction)
		var right_projection: int = _dot_coord(right.coord, direction)
		if left_projection != right_projection:
			return left_projection < right_projection
		var left_side: int = _perpendicular_coord(left.coord, direction)
		var right_side: int = _perpendicular_coord(right.coord, direction)
		if left_side != right_side:
			return left_side < right_side
		return String(left.unit_id) < String(right.unit_id)
	)
	return sorted

func _append_affected_unit_id(affected_unit_ids: Dictionary, unit_state: BattleUnitState) -> void:
	if unit_state == null:
		return
	affected_unit_ids[unit_state.unit_id] = true

func _collect_wind_push_target_units(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_def: CombatEffectDef,
	effect_coords: Array[Vector2i],
	batch: BattleEventBatch,
	result: Dictionary,
	affected_unit_ids: Dictionary
) -> Array:
	var units: Array = []
	if effect_def == null:
		return units
	var target_filter := _resolve_effect_target_filter(skill_def, effect_def)
	var barrier_effects: Array[CombatEffectDef] = [effect_def]
	for target_unit in _collect_units_in_coords(effect_coords):
		if target_unit == null or not target_unit.is_alive:
			continue
		if not _is_unit_valid_for_effect(source_unit, target_unit, target_filter):
			continue
		var barrier_result: Dictionary = _runtime._layered_barrier_service.resolve_skill_barrier_interaction(
			source_unit,
			target_unit,
			skill_def,
			barrier_effects,
			batch
		) if _runtime._layered_barrier_service != null else {}
		if bool(barrier_result.get("blocked", false)):
			if bool(barrier_result.get("applied", false)):
				result["applied"] = true
				_append_affected_unit_id(affected_unit_ids, target_unit)
			continue
		units.append(target_unit)
	return units

func _try_wind_push_unit_one_step(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_def: CombatEffectDef,
	unit_state: BattleUnitState,
	direction: Vector2i,
	moved_this_step: Dictionary,
	affected_unit_ids: Dictionary,
	recursion_stack: Dictionary,
	batch: BattleEventBatch
) -> bool:
	if _runtime == null or _runtime._state == null:
		return false
	if unit_state == null or not unit_state.is_alive:
		return false
	if direction == Vector2i.ZERO:
		return false
	if moved_this_step.has(unit_state.unit_id):
		return false
	if _runtime._blocks_enemy_forced_move(source_unit, unit_state):
		if batch != null:
			batch.log_lines.append("%s 稳如金刚，未被强制位移。" % unit_state.display_name)
		return false
	if recursion_stack.has(unit_state.unit_id):
		return false
	var next_coord := unit_state.coord + direction
	if not _runtime._grid_service.is_inside(_runtime._state, next_coord):
		return false
	var next_stack: Dictionary = recursion_stack.duplicate()
	next_stack[unit_state.unit_id] = true
	var target_filter := _resolve_effect_target_filter(skill_def, effect_def)
	var blocking_ids: Array[StringName] = _runtime._grid_service.collect_blocking_unit_ids(_runtime._state, unit_state, next_coord)
	for blocking_unit_id in blocking_ids:
		if blocking_unit_id == unit_state.unit_id:
			continue
		var blocking_unit := _runtime._state.units.get(blocking_unit_id) as BattleUnitState
		if blocking_unit == null or not blocking_unit.is_alive:
			return false
		if not _is_unit_valid_for_effect(source_unit, blocking_unit, target_filter):
			return false
		if not _try_wind_push_unit_one_step(source_unit, skill_def, effect_def, blocking_unit, direction, moved_this_step, affected_unit_ids, next_stack, batch):
			return false
	if not _runtime._grid_service.can_traverse(_runtime._state, unit_state.coord, next_coord, unit_state):
		return false
	var barrier_result: Dictionary = _runtime._layered_barrier_service.resolve_unit_boundary_crossing(
		unit_state,
		unit_state.coord,
		next_coord,
		batch
	) if _runtime._layered_barrier_service != null else {}
	if bool(barrier_result.get("blocked", false)) or not unit_state.is_alive:
		_append_affected_unit_id(affected_unit_ids, unit_state)
		return false
	var previous_coords: Array[Vector2i] = unit_state.occupied_coords.duplicate()
	if not _runtime._grid_service.move_unit(_runtime._state, unit_state, next_coord):
		return false
	moved_this_step[unit_state.unit_id] = true
	_append_affected_unit_id(affected_unit_ids, unit_state)
	_append_changed_coords(batch, previous_coords)
	_append_changed_unit_coords(batch, unit_state)
	_append_changed_unit_id(batch, unit_state.unit_id)
	return true

func _apply_ground_wind_push_effects(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	wind_push_effects: Array[CombatEffectDef],
	effect_coords: Array[Vector2i],
	target_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> Dictionary:
	var result: Dictionary = {
		"applied": false,
		"affected_unit_ids": [],
	}
	if wind_push_effects.is_empty() or source_unit == null:
		return result
	var forced_move_context := _build_ground_forced_move_context(source_unit, target_coords)
	var direction: Vector2i = forced_move_context.get("direction", Vector2i.ZERO)
	if direction == Vector2i.ZERO:
		return result
	var affected_unit_ids: Dictionary = {}
	for effect_def in wind_push_effects:
		if effect_def == null:
			continue
		var target_units := _collect_wind_push_target_units(source_unit, skill_def, effect_def, effect_coords, batch, result, affected_unit_ids)
		if target_units.is_empty():
			continue
		var move_distance: int = maxi(int(effect_def.forced_move_distance), 0)
		for _step_index in range(move_distance):
			var moved_this_step: Dictionary = {}
			var moved_any := false
			var ordered_units := _sort_wind_push_units_near_to_far(target_units, direction)
			for target_unit in ordered_units:
				if target_unit == null or not target_unit.is_alive:
					continue
				if moved_this_step.has(target_unit.unit_id):
					continue
				if _try_wind_push_unit_one_step(source_unit, skill_def, effect_def, target_unit, direction, moved_this_step, affected_unit_ids, {}, batch):
					moved_any = true
					result["applied"] = true
			if not moved_any:
				break
	result["affected_unit_ids"] = affected_unit_ids.keys()
	return result

func _apply_ground_unit_effects(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i],
	batch: BattleEventBatch,
	target_coords: Array[Vector2i] = []
) -> Dictionary:
	var applied = false
	var total_damage = 0
	var total_healing = 0
	var total_kill_count = 0
	var affected_unit_ids: Dictionary = {}
	var shield_roll_context = {}
	var forced_move_context := _build_ground_forced_move_context(source_unit, target_coords)
	var wind_push_effects := _collect_wind_push_effects(effect_defs)
	var wind_push_effect_ids := _build_effect_instance_lookup(wind_push_effects)

	for target_unit in _collect_units_in_coords(effect_coords):
		if target_unit == null or not target_unit.is_alive:
			continue
		var applicable_effects: Array[CombatEffectDef] = []
		for effect_def in effect_defs:
			if effect_def == null:
				continue
			if wind_push_effect_ids.has(effect_def.get_instance_id()):
				continue
			if _is_unit_valid_for_effect(source_unit, target_unit, _resolve_effect_target_filter(skill_def, effect_def)):
				applicable_effects.append(effect_def)
		if applicable_effects.is_empty():
			continue

		var barrier_result: Dictionary = _runtime._layered_barrier_service.resolve_skill_barrier_interaction(
			source_unit,
			target_unit,
			skill_def,
			applicable_effects,
			batch
		) if _runtime._layered_barrier_service != null else {}
		if bool(barrier_result.get("blocked", false)):
			applied = applied or bool(barrier_result.get("applied", false))
			if bool(barrier_result.get("applied", false)):
				_append_affected_unit_id(affected_unit_ids, target_unit)
			continue

		var result = _resolve_ground_unit_effect_result(source_unit, target_unit, skill_def, applicable_effects)
		_runtime._skill_mastery_service.record_target_result(source_unit, target_unit, skill_def, result, applicable_effects)
		var shield_result = _apply_unit_shield_effects(
			source_unit,
			target_unit,
			skill_def,
			applicable_effects,
			shield_roll_context
		)
		var special_result = _runtime._apply_unit_skill_special_effects(
			source_unit,
			target_unit,
			skill_def,
			null,
			applicable_effects,
			batch,
			forced_move_context
		)
		_record_vajra_body_mastery_from_incoming_damage(source_unit, target_unit, skill_def, result, batch)
		mark_applied_statuses_for_turn_timing(target_unit, result.get("status_effect_ids", []))
		var attack_resolved = result.has("attack_success")
		var attack_hit = attack_resolved and bool(result.get("attack_success", false))
		var unit_applied = bool(result.get("applied", false)) or bool(shield_result.get("applied", false)) or bool(special_result.get("applied", false)) or attack_hit
		if not unit_applied:
			if attack_resolved:
				_append_result_report_entry(batch, result)
			continue

		applied = true
		_append_affected_unit_id(affected_unit_ids, target_unit)
		_append_changed_unit_id(batch, source_unit.unit_id if source_unit != null else &"")
		_append_changed_unit_id(batch, target_unit.unit_id)
		_append_changed_unit_coords(batch, target_unit)
		append_result_source_status_effects(batch, source_unit, result)

		var damage = int(result.get("damage", 0))
		var healing = int(result.get("healing", 0))
		total_damage += damage
		total_healing += healing
		append_damage_result_log_lines(
			batch,
			_build_skill_log_subject_label(source_unit, skill_def),
			target_unit.display_name,
			result
		)
		if attack_resolved and not bool(result.get("applied", false)):
			_append_result_report_entry(batch, result)
		if healing > 0:
			batch.log_lines.append("%s 为 %s 恢复 %d 点生命。" % [
				_build_skill_log_subject_label(source_unit, skill_def),
				target_unit.display_name,
				healing,
			])
		if bool(shield_result.get("applied", false)):
			batch.log_lines.append("%s 使 %s 的护盾值变为 %d。" % [
				_build_skill_log_subject_label(source_unit, skill_def),
				target_unit.display_name,
				int(shield_result.get("current_shield_hp", 0)),
			])
		for status_id in result.get("status_effect_ids", []):
			batch.log_lines.append("%s 获得状态 %s。" % [target_unit.display_name, String(status_id)])

		if not target_unit.is_alive:
			total_kill_count += 1
			_apply_on_kill_gain_resources_effects(source_unit, target_unit, skill_def, effect_defs, batch)
			_collect_defeated_unit_loot(target_unit, source_unit)
			_clear_defeated_unit(target_unit, batch)
			batch.log_lines.append("%s 被击倒。" % target_unit.display_name)
			_runtime._battle_rating_system.record_enemy_defeated_achievement(source_unit, target_unit)
			_record_unit_defeated(target_unit)
		if source_unit != null and target_unit != null:
			_record_effect_metrics(source_unit, target_unit, damage, healing, 1 if not target_unit.is_alive else 0)

	var wind_push_result := _apply_ground_wind_push_effects(source_unit, skill_def, wind_push_effects, effect_coords, target_coords, batch)
	if bool(wind_push_result.get("applied", false)):
		applied = true
		_append_changed_unit_id(batch, source_unit.unit_id if source_unit != null else &"")
	for affected_unit_id in wind_push_result.get("affected_unit_ids", []):
		affected_unit_ids[affected_unit_id] = true

	_flush_last_stand_mastery_records(batch)
	if applied and source_unit != null:
		_runtime._battle_rating_system.record_skill_effect_result(source_unit, total_damage, total_healing, total_kill_count)
	return {
		"applied": applied,
		"affected_unit_count": affected_unit_ids.size(),
		"damage": total_damage,
		"healing": total_healing,
		"kill_count": total_kill_count,
	}

func _resolve_ground_unit_effect_result(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef]
) -> Dictionary:
	if _should_resolve_ground_effects_as_attack(effect_defs):
		var attack_effect_defs = _dedupe_effect_defs_by_instance(effect_defs)
		var attack_check = _runtime._hit_resolver.build_skill_attack_check(source_unit, target_unit, skill_def)
		return _runtime._damage_resolver.resolve_attack_effects(
			source_unit,
			target_unit,
			attack_effect_defs,
			attack_check,
			{"battle_state": _runtime._state, "skill_id": skill_def.skill_id if skill_def != null else &""}
		)
	return _runtime._damage_resolver.resolve_effects(
		source_unit,
		target_unit,
		effect_defs,
		{"skill_id": skill_def.skill_id if skill_def != null else &""}
	)

func _should_resolve_ground_effects_as_attack(effect_defs: Array[CombatEffectDef]) -> bool:
	for effect_def in effect_defs:
		if effect_def == null or effect_def.params == null:
			continue
		if bool(effect_def.params.get("resolve_as_weapon_attack", false)):
			return true
	return false

func _dedupe_effect_defs_by_instance(effect_defs: Array[CombatEffectDef]) -> Array[CombatEffectDef]:
	var deduped: Array[CombatEffectDef] = []
	var seen: Dictionary = {}
	for effect_def in effect_defs:
		if effect_def == null:
			continue
		var instance_id = effect_def.get_instance_id()
		if seen.has(instance_id):
			continue
		seen[instance_id] = true
		deduped.append(effect_def)
	return deduped

func _apply_ground_terrain_effects(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_defs: Array[CombatEffectDef],
	effect_coords: Array[Vector2i],
	batch: BattleEventBatch
) -> Dictionary:
	var applied = false
	var requires_topology_reconcile = false

	for effect_def in effect_defs:
		if effect_def == null:
			continue
		match effect_def.effect_type:
			&"terrain", &"terrain_replace", &"terrain_replace_to", &"height", &"height_delta":
				requires_topology_reconcile = true
				for effect_coord in effect_coords:
					var barrier_result: Dictionary = _runtime._layered_barrier_service.resolve_ground_barrier_interaction(source_unit, effect_coord, skill_def, effect_defs, batch) if _runtime._layered_barrier_service != null else {}
					if bool(barrier_result.get("blocked", false)):
						applied = applied or bool(barrier_result.get("applied", false))
						continue
					if _apply_ground_cell_effect(source_unit, skill_def, effect_coord, effect_def, batch):
						applied = true
			&"terrain_effect":
				if effect_def.duration_tu > 0 and effect_def.tick_interval_tu > 0:
					var field_instance_id = _build_terrain_effect_instance_id(effect_def.terrain_effect_id)
					var applied_coord_count = 0
					for effect_coord in effect_coords:
						var barrier_result: Dictionary = _runtime._layered_barrier_service.resolve_ground_barrier_interaction(source_unit, effect_coord, skill_def, effect_defs, batch) if _runtime._layered_barrier_service != null else {}
						if bool(barrier_result.get("blocked", false)):
							applied = applied or bool(barrier_result.get("applied", false))
							continue
						if _runtime._terrain_effect_system.upsert_timed_terrain_effect(effect_coord, source_unit, skill_def, effect_def, field_instance_id):
							applied = true
							applied_coord_count += 1
							_append_changed_coord(batch, effect_coord)
					if applied_coord_count > 0:
						batch.log_lines.append("%s 在 %d 个地格留下 %s。" % [
							_build_skill_log_subject_label(source_unit, skill_def),
							applied_coord_count,
							_get_terrain_effect_display_name(effect_def),
						])
				elif effect_def.terrain_effect_id != &"":
					var tagged_coord_count = 0
					for effect_coord in effect_coords:
						var barrier_result: Dictionary = _runtime._layered_barrier_service.resolve_ground_barrier_interaction(source_unit, effect_coord, skill_def, effect_defs, batch) if _runtime._layered_barrier_service != null else {}
						if bool(barrier_result.get("blocked", false)):
							applied = applied or bool(barrier_result.get("applied", false))
							continue
						var cell = _runtime._grid_service.get_cell(_runtime._state, effect_coord)
						if cell == null or cell.terrain_effect_ids.has(effect_def.terrain_effect_id):
							continue
						cell.terrain_effect_ids.append(effect_def.terrain_effect_id)
						_append_changed_coord(batch, effect_coord)
						tagged_coord_count += 1
						applied = true
					if tagged_coord_count > 0:
						batch.log_lines.append("%s 使 %d 个地格附加效果 %s。" % [
							_build_skill_log_subject_label(source_unit, skill_def),
							tagged_coord_count,
							_get_terrain_effect_display_name(effect_def),
						])
			&"edge_clear":
				if _apply_ground_edge_clear_effect(source_unit, skill_def, effect_coords, effect_def, batch):
					applied = true
			_:
				pass

	if requires_topology_reconcile and _reconcile_water_topology(effect_coords, batch):
		applied = true
	return {"applied": applied}

func _apply_ground_edge_clear_effect(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	effect_coords: Array[Vector2i],
	effect_def: CombatEffectDef,
	batch: BattleEventBatch
) -> bool:
	if _runtime == null or _runtime._state == null or effect_coords.size() < 2:
		return false
	var edge_coords := _sort_coords(effect_coords)
	var first: Vector2i = edge_coords[0]
	var second: Vector2i = edge_coords[1]
	if _runtime._grid_service.get_distance(first, second) != 1:
		return false
	var barrier_effect_defs: Array[CombatEffectDef] = [effect_def]
	for barrier_coord in [first, second]:
		var barrier_result: Dictionary = _runtime._layered_barrier_service.resolve_ground_barrier_interaction(source_unit, barrier_coord, skill_def, barrier_effect_defs, batch) if _runtime._layered_barrier_service != null else {}
		if bool(barrier_result.get("blocked", false)):
			return bool(barrier_result.get("applied", false))
	var edge_ref := _get_edge_authoring_reference(first, second)
	if edge_ref.is_empty():
		return false
	var edge_coord: Vector2i = edge_ref.get("coord", Vector2i(-1, -1))
	var edge_direction: Vector2i = edge_ref.get("direction", Vector2i.ZERO)
	var cell: BattleCellState = _runtime._grid_service.get_cell(_runtime._state, edge_coord)
	if cell == null:
		return false
	var feature_state: BattleEdgeFeatureState = cell.get_edge_feature(edge_direction)
	if feature_state == null or feature_state.is_empty():
		return false
	if not _can_edge_clear_remove_feature(effect_def, feature_state):
		return false
	if not (feature_state.blocks_move or feature_state.blocks_occupancy or feature_state.blocks_los):
		return false
	if not _runtime._grid_service.clear_edge_feature(_runtime._state, edge_coord, edge_direction):
		return false
	_append_changed_coord(batch, first)
	_append_changed_coord(batch, second)
	batch.log_lines.append("%s 在 (%d, %d) 与 (%d, %d) 之间开辟通道，移除了%s。" % [
		_build_skill_log_subject_label(source_unit, skill_def),
		first.x,
		first.y,
		second.x,
		second.y,
		_get_edge_feature_display_name(feature_state),
	])
	return true

func _get_edge_authoring_reference(from_coord: Vector2i, to_coord: Vector2i) -> Dictionary:
	var delta := to_coord - from_coord
	match delta:
		Vector2i.RIGHT:
			return {"coord": from_coord, "direction": Vector2i.RIGHT}
		Vector2i.LEFT:
			return {"coord": to_coord, "direction": Vector2i.RIGHT}
		Vector2i.DOWN:
			return {"coord": from_coord, "direction": Vector2i.DOWN}
		Vector2i.UP:
			return {"coord": to_coord, "direction": Vector2i.DOWN}
		_:
			return {}

func _can_edge_clear_remove_feature(effect_def: CombatEffectDef, feature_state: BattleEdgeFeatureState) -> bool:
	var allowed_kinds := _get_edge_clear_feature_kinds(effect_def)
	return allowed_kinds.has(feature_state.feature_kind)

func _get_edge_clear_feature_kinds(effect_def: CombatEffectDef) -> Dictionary:
	var allowed := {}
	var params: Dictionary = effect_def.params if effect_def != null and effect_def.params != null else {}
	var raw_kinds: Variant = params.get("clear_feature_kinds", [])
	if raw_kinds is Array:
		for raw_kind in raw_kinds:
			if raw_kind is StringName or raw_kind is String:
				var kind := StringName(raw_kind)
				if kind != &"":
					allowed[kind] = true
	if allowed.is_empty():
		allowed[BattleEdgeFeatureState.FEATURE_WALL] = true
		allowed[BattleEdgeFeatureState.FEATURE_DOOR] = true
		allowed[BattleEdgeFeatureState.FEATURE_GATE] = true
	return allowed

func _get_edge_feature_display_name(feature_state: BattleEdgeFeatureState) -> String:
	if feature_state == null:
		return "阻挡边界"
	match feature_state.feature_kind:
		BattleEdgeFeatureState.FEATURE_WALL:
			return "墙体"
		BattleEdgeFeatureState.FEATURE_DOOR:
			return "门"
		BattleEdgeFeatureState.FEATURE_GATE:
			return "闸门"
		_:
			return "阻挡边界"

func _apply_ground_cell_effect(
	source_unit: BattleUnitState,
	skill_def: SkillDef,
	target_coord: Vector2i,
	effect_def: CombatEffectDef,
	batch: BattleEventBatch
) -> bool:
	var cell = _runtime._grid_service.get_cell(_runtime._state, target_coord)
	if cell == null:
		return false

	var cell_applied = false
	var before_terrain = cell.base_terrain
	var before_height = int(cell.current_height)
	var occupant_unit = _runtime._state.units.get(cell.occupant_unit_id) as BattleUnitState if cell.occupant_unit_id != &"" else null

	match effect_def.effect_type:
		&"terrain", &"terrain_replace", &"terrain_replace_to":
			if effect_def.terrain_replace_to != &"" and cell.base_terrain != effect_def.terrain_replace_to:
				if _runtime._grid_service.set_base_terrain(_runtime._state, target_coord, effect_def.terrain_replace_to):
					cell_applied = true
		&"height", &"height_delta":
			if effect_def.height_delta != 0:
				var height_result = _runtime._grid_service.apply_height_delta_result(_runtime._state, target_coord, int(effect_def.height_delta))
				if bool(height_result.get("changed", false)):
					cell_applied = true
		_:
			pass

	var after_height = int(cell.current_height)
	if before_terrain != cell.base_terrain or before_height != after_height:
		_append_changed_coord(batch, target_coord)
	if before_terrain != cell.base_terrain:
		batch.log_lines.append("%s 使 (%d, %d) 的地形由 %s 变为 %s。" % [
			_build_skill_log_subject_label(source_unit, skill_def),
			target_coord.x,
			target_coord.y,
			_runtime._grid_service.get_terrain_display_name(String(before_terrain)),
			_runtime._grid_service.get_terrain_display_name(String(cell.base_terrain)),
		])
	if before_height != after_height:
		batch.log_lines.append("%s 使 (%d, %d) 的高度由 %d 变为 %d。" % [
			_build_skill_log_subject_label(source_unit, skill_def),
			target_coord.x,
			target_coord.y,
			before_height,
			after_height,
		])

	if occupant_unit != null and occupant_unit.is_alive and after_height < before_height:
		var fall_layers = before_height - after_height
		var fall_result = _runtime._damage_resolver.resolve_fall_damage(occupant_unit, fall_layers)
		var fall_damage = int(fall_result.get("damage", 0))
		var shield_absorbed = int(fall_result.get("shield_absorbed", 0))
		if fall_damage > 0 or shield_absorbed > 0:
			cell_applied = true
			_append_changed_coord(batch, target_coord)
			_append_changed_unit_id(batch, occupant_unit.unit_id)
			if fall_damage > 0:
				batch.log_lines.append("%s 使 (%d, %d) 的高度下降 %d 层，导致 %s 坠落并受到 %d 点伤害。" % [
					_build_skill_log_subject_label(source_unit, skill_def),
					target_coord.x,
					target_coord.y,
					fall_layers,
					occupant_unit.display_name,
					fall_damage,
				])
				if shield_absorbed > 0:
					batch.log_lines.append("%s 的护盾吸收了 %d 点坠落伤害。" % [
						occupant_unit.display_name,
						shield_absorbed,
					])
			else:
				batch.log_lines.append("%s 使 (%d, %d) 的高度下降 %d 层，导致 %s 坠落，但被护盾吸收了 %d 点坠落伤害。" % [
					_build_skill_log_subject_label(source_unit, skill_def),
					target_coord.x,
					target_coord.y,
					fall_layers,
					occupant_unit.display_name,
					shield_absorbed,
				])
			if bool(fall_result.get("shield_broken", false)):
				batch.log_lines.append("%s 的护盾被击碎。" % occupant_unit.display_name)
			if not occupant_unit.is_alive:
				_collect_defeated_unit_loot(occupant_unit, source_unit)
				_clear_defeated_unit(occupant_unit, batch)
				batch.log_lines.append("%s 被击倒。" % occupant_unit.display_name)

	_flush_last_stand_mastery_records(batch)
	return cell_applied

func _reconcile_water_topology(effect_coords: Array[Vector2i], batch: BattleEventBatch) -> bool:
	if _runtime._state == null or _runtime._state.map_size == Vector2i.ZERO or effect_coords.is_empty():
		return false

	var changes: Array[Dictionary] = _runtime._terrain_topology_service.reclassify_water_terrain_near_coords(
		_runtime._state.cells,
		_runtime._state.map_size,
		effect_coords
	)
	var applied = false
	for change in changes:
		var coord: Vector2i = change.get("coord", Vector2i.ZERO)
		var cell: BattleCellState = _runtime._grid_service.get_cell(_runtime._state, coord)
		if cell == null:
			continue
		var before_terrain: StringName = cell.base_terrain
		var before_flow_direction: Vector2i = cell.flow_direction
		var after_terrain: StringName = change.get("after_terrain", before_terrain)
		var after_flow_direction: Vector2i = change.get("after_flow_direction", before_flow_direction)
		if before_terrain != after_terrain:
			_runtime._grid_service.set_base_terrain(_runtime._state, coord, after_terrain)
			cell = _runtime._grid_service.get_cell(_runtime._state, coord)
			if cell == null:
				continue
		if cell.flow_direction != after_flow_direction:
			cell.flow_direction = after_flow_direction
			_runtime._grid_service.recalculate_cell(cell)
			_runtime._grid_service.sync_column_from_surface_cell(_runtime._state, coord)
		if before_terrain != cell.base_terrain or before_flow_direction != cell.flow_direction:
			applied = true
			_append_changed_coord(batch, coord)
		if before_terrain != cell.base_terrain:
			batch.log_lines.append("相邻水域在 (%d, %d) 重分类为 %s。" % [
				coord.x,
				coord.y,
				_runtime._grid_service.get_terrain_display_name(String(cell.base_terrain)),
			])
	return applied

func _get_ground_special_effect_validation_message(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	target_coords: Array[Vector2i]
) -> String:
	var relocation_effect_def = _get_ground_relocation_effect_def(skill_def, cast_variant)
	if relocation_effect_def == null:
		return ""
	if active_unit == null or _runtime._state == null:
		return "位移落点无效。"
	if _is_movement_blocked(active_unit):
		return "当前状态下无法移动。"
	if target_coords.is_empty():
		return "位移落点无效。"

	var landing_coord = target_coords[0]
	if not _can_use_ground_relocation(active_unit, landing_coord, relocation_effect_def):
		return "目标地格无法作为位移落点。"
	return ""

func _validate_ground_skill_command(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	command: BattleCommand
) -> Dictionary:
	var normalized_coords = _normalize_target_coords(command)
	var result = {
		"allowed": false,
		"message": "地面技能目标无效。",
		"target_coords": normalized_coords,
	}
	if _runtime._state == null or active_unit == null or skill_def == null or skill_def.combat_profile == null or cast_variant == null:
		return result
	if cast_variant.target_mode != &"ground":
		result.message = "该技能形态不是地面施法。"
		return result
	var block_reason = _get_skill_cast_block_reason(active_unit, skill_def)
	if not block_reason.is_empty():
		result.message = block_reason
		return result
	if normalized_coords.size() != int(cast_variant.required_coord_count):
		result.message = "该技能形态需要选择 %d 个地格。" % int(cast_variant.required_coord_count)
		return result
	if _runtime._charge_resolver.is_charge_variant(cast_variant):
		return _runtime._charge_resolver.validate_charge_command(active_unit, skill_def, cast_variant, normalized_coords, result)

	var relocation_effect_def = _get_ground_relocation_effect_def(skill_def, cast_variant)
	var effective_skill_range = _get_effective_skill_range(active_unit, skill_def)
	var seen_coords: Dictionary = {}
	for target_coord in normalized_coords:
		var coord: Vector2i = target_coord
		if seen_coords.has(coord):
			result.message = "同一地格不能重复选择。"
			return result
		seen_coords[coord] = true
		if not _runtime._grid_service.is_inside(_runtime._state, coord):
			result.message = "存在超出战场范围的目标地格。"
			return result
		var target_distance: int = _runtime._grid_service.get_chebyshev_distance(active_unit.coord, coord) \
			if relocation_effect_def != null else _runtime._grid_service.get_distance_from_unit_to_coord(active_unit, coord)
		if target_distance > effective_skill_range:
			result.message = "目标地格超出技能施放距离。"
			return result
		var cell = _runtime._grid_service.get_cell(_runtime._state, coord)
		if cell == null:
			result.message = "目标地格数据不可用。"
			return result
		if not cast_variant.allowed_base_terrains.is_empty():
			var normalized_allowed = false
			var normalized_cell_terrain = BattleTerrainRules.normalize_terrain_id(cell.base_terrain)
			for allowed_terrain in cast_variant.allowed_base_terrains:
				if BattleTerrainRules.normalize_terrain_id(allowed_terrain) == normalized_cell_terrain:
					normalized_allowed = true
					break
			if not normalized_allowed:
				result.message = "目标地格地形不符合该技能形态的要求。"
				return result
		if _is_crown_break_skill(skill_def.skill_id):
			var target_unit = _runtime._grid_service.get_unit_at_coord(_runtime._state, coord)
			if not _is_crown_break_target_eligible(active_unit, target_unit):
				result.message = "折冠只能对已被黑星烙印的 elite / boss 施放。"
				return result

	if not _validate_target_coords_shape(cast_variant.footprint_pattern, normalized_coords):
		result.message = "目标地格排布不符合该技能形态。"
		return result

	var sorted_target_coords = _sort_coords(normalized_coords)
	var special_validation_message = _get_ground_special_effect_validation_message(
		active_unit,
		skill_def,
		cast_variant,
		sorted_target_coords
	)
	if not special_validation_message.is_empty():
		result.message = special_validation_message
		return result

	result.target_coords = sorted_target_coords
	result.allowed = true
	result.message = "可施放。"
	return result

func _validate_target_coords_shape(footprint_pattern: StringName, target_coords: Array[Vector2i]) -> bool:
	match footprint_pattern:
		&"single":
			return target_coords.size() == 1
		&"line2":
			if target_coords.size() != 2:
				return false
			var first = target_coords[0]
			var second = target_coords[1]
			return (first.x == second.x and absi(first.y - second.y) == 1) \
				or (first.y == second.y and absi(first.x - second.x) == 1)
		&"square2":
			if target_coords.size() != 4:
				return false
			var min_x = target_coords[0].x
			var max_x = target_coords[0].x
			var min_y = target_coords[0].y
			var max_y = target_coords[0].y
			var coord_set: Dictionary = {}
			for coord in target_coords:
				min_x = mini(min_x, coord.x)
				max_x = maxi(max_x, coord.x)
				min_y = mini(min_y, coord.y)
				max_y = maxi(max_y, coord.y)
				coord_set[coord] = true
			if max_x - min_x != 1 or max_y - min_y != 1:
				return false
			for x in range(min_x, max_x + 1):
				for y in range(min_y, max_y + 1):
					if not coord_set.has(Vector2i(x, y)):
						return false
			return true
		&"unordered":
			return not target_coords.is_empty()
		_:
			return false

func _normalize_target_coords(command: BattleCommand) -> Array[Vector2i]:
	var coords: Array[Vector2i] = []
	if command == null:
		return coords
	for target_coord in command.target_coords:
		coords.append(target_coord)
	if coords.is_empty() and command.target_coord != Vector2i(-1, -1):
		coords.append(command.target_coord)
	return coords

func _build_terrain_effect_instance_id(effect_id: StringName) -> StringName:
	_runtime._terrain_effect_nonce += 1
	return StringName("%s_%d_%d" % [
		String(effect_id),
		int(_runtime._state.timeline.current_tu) if _runtime._state != null and _runtime._state.timeline != null else 0,
		_runtime._terrain_effect_nonce,
	])

func _get_terrain_effect_display_name(effect_def: CombatEffectDef) -> String:
	if effect_def != null and effect_def.params.has("display_name"):
		return String(effect_def.params.get("display_name", ""))
	return String(effect_def.terrain_effect_id) if effect_def != null else "地格效果"
