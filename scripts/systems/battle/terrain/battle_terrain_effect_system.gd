## 文件说明：该脚本属于战斗地形效果相关的业务脚本，集中维护 timed terrain effect 的写入、推进与结算。
## 审查重点：重点核对地形效果写入、跳点推进、目标过滤与击倒收尾是否仍然保持与原主模块一致的语义。
## 备注：后续如果增删字段，需要同步检查调用方、状态同步链路以及历史数据兼容处理。

class_name BattleTerrainEffectSystem
extends RefCounted

const BATTLE_TERRAIN_EFFECT_STATE_SCRIPT = preload("res://scripts/systems/battle/terrain/battle_terrain_effect_state.gd")
const BATTLE_EVENT_BATCH_SCRIPT = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const COMBAT_EFFECT_DEF_SCRIPT = preload("res://scripts/player/progression/combat_effect_def.gd")
const SKILL_DEF_SCRIPT = preload("res://scripts/player/progression/skill_def.gd")
const BATTLE_UNIT_STATE_SCRIPT = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

const TERRAIN_EFFECT_DAMAGE: StringName = &"damage"
const TERRAIN_EFFECT_MOVEMENT_COST: StringName = &"movement_cost"
const TERRAIN_EFFECT_NONE: StringName = &"none"
const LIFETIME_POLICY_TIMED: StringName = &"timed"
const LIFETIME_POLICY_BATTLE: StringName = &"battle"
const STACK_BEHAVIOR_REFRESH: StringName = &"refresh"
const STACK_BEHAVIOR_STACK: StringName = &"stack"
const STACK_BEHAVIOR_IGNORE_EXISTING: StringName = &"ignore_existing"
const PARAM_LIFETIME_POLICY := "lifetime_policy"
const PARAM_MOVE_COST_DELTA := "move_cost_delta"
const PARAM_DOES_NOT_STACK_WITH_STATUS_ID := "does_not_stack_with_status_id"
const PARAM_DOES_NOT_STACK_WITH_STATUS_IDS := "does_not_stack_with_status_ids"
const TU_GRANULARITY := 5

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


func get_move_cost_delta_for_unit_target(
	unit_state: BattleUnitState,
	target_coord: Vector2i
) -> int:
	if not _has_runtime() or unit_state == null:
		return 0

	var state = _runtime.get_state()
	var grid_service = _runtime.get_grid_service()
	if state == null or grid_service == null:
		return 0

	var max_delta := 0
	for occupied_coord in grid_service.get_unit_target_coords(unit_state, target_coord):
		var cell: BattleCellState = grid_service.get_cell(state, occupied_coord) as BattleCellState
		if cell == null or cell.timed_terrain_effects.is_empty():
			continue
		for effect_variant in cell.timed_terrain_effects:
			var effect_state := effect_variant as BattleTerrainEffectState
			var move_cost_delta := _get_timed_terrain_move_cost_delta(effect_state)
			if move_cost_delta <= 0:
				continue
			var source_unit: BattleUnitState = state.units.get(effect_state.source_unit_id) as BattleUnitState if effect_state.source_unit_id != &"" else null
			if not _is_unit_valid_for_effect(source_unit, unit_state, effect_state.target_team_filter):
				continue
			if _is_blocked_by_nonstacking_status(unit_state, effect_state):
				continue
			max_delta = maxi(max_delta, move_cost_delta)
	return max_delta


func upsert_timed_terrain_effect(
	effect_coord: Vector2i,
	source_unit,
	skill_def,
	effect_def,
	field_instance_id: StringName
) -> bool:
	if not _has_runtime():
		return false

	var state = _runtime.get_state()
	var grid_service = _runtime.get_grid_service()
	if state == null or grid_service == null or effect_def == null or effect_def.terrain_effect_id == &"":
		return false

	var cell: BattleCellState = grid_service.get_cell(state, effect_coord) as BattleCellState
	if cell == null:
		return false

	var normalized_behavior := _normalize_stack_behavior(effect_def.stack_behavior)
	var existing_index := -1
	for index in range(cell.timed_terrain_effects.size()):
		var existing_effect := cell.timed_terrain_effects[index] as BattleTerrainEffectState
		if existing_effect != null and existing_effect.effect_id == effect_def.terrain_effect_id:
			existing_index = index
			break

	if existing_index >= 0:
		match normalized_behavior:
			STACK_BEHAVIOR_IGNORE_EXISTING:
				return false
			STACK_BEHAVIOR_REFRESH:
				var refreshed_effect := _build_timed_terrain_effect(source_unit, skill_def, effect_def, field_instance_id)
				if refreshed_effect == null:
					return false
				cell.timed_terrain_effects[existing_index] = refreshed_effect
				return true
			_:
				pass

	var new_effect := _build_timed_terrain_effect(source_unit, skill_def, effect_def, field_instance_id)
	if new_effect == null:
		return false
	cell.timed_terrain_effects.append(new_effect)
	return true


func process_timed_terrain_effects(batch: BattleEventBatch) -> void:
	if not _has_runtime():
		return

	var state = _runtime.get_state()
	var grid_service = _runtime.get_grid_service()
	if state == null or state.timeline == null or grid_service == null:
		return

	var processed_tick_keys: Dictionary = {}
	for coord in _sort_coords(state.cells.keys()):
		var cell: BattleCellState = state.cells.get(coord) as BattleCellState
		if cell == null or cell.timed_terrain_effects.is_empty():
			continue

		var retained_effects: Array[BattleTerrainEffectState] = []
		var cell_changed := false
		for effect_variant in cell.timed_terrain_effects:
			var effect_state: BattleTerrainEffectState = effect_variant as BattleTerrainEffectState
			if effect_state == null:
				cell_changed = true
				continue
			if _is_battle_lifetime_effect(effect_state):
				retained_effects.append(effect_state)
				continue

			while effect_state.remaining_tu > 0 and effect_state.tick_interval_tu > 0 and state.timeline.current_tu >= effect_state.next_tick_at_tu:
				apply_timed_terrain_effect_tick(coord, effect_state, processed_tick_keys, batch)
				effect_state.remaining_tu = maxi(effect_state.remaining_tu - effect_state.tick_interval_tu, 0)
				effect_state.next_tick_at_tu += effect_state.tick_interval_tu
				cell_changed = true

			if effect_state.remaining_tu > 0:
				retained_effects.append(effect_state)
			else:
				cell_changed = true

		if cell_changed:
			cell.timed_terrain_effects = retained_effects
			_runtime.append_changed_coord(batch, coord)


func apply_timed_terrain_effect_tick(
	target_coord: Vector2i,
	effect_state,
	processed_tick_keys: Dictionary,
	batch: BattleEventBatch
) -> void:
	if not _has_runtime():
		return
	if effect_state != null and (effect_state.effect_type == TERRAIN_EFFECT_MOVEMENT_COST or effect_state.effect_type == TERRAIN_EFFECT_NONE):
		return

	var state = _runtime.get_state()
	var grid_service = _runtime.get_grid_service()
	var damage_resolver = _runtime.get_damage_resolver()
	if state == null or effect_state == null or processed_tick_keys == null or grid_service == null or damage_resolver == null:
		return

	var cell: BattleCellState = grid_service.get_cell(state, target_coord) as BattleCellState
	if cell == null or cell.occupant_unit_id == &"":
		return

	var target_unit: BattleUnitState = state.units.get(cell.occupant_unit_id) as BattleUnitState
	if target_unit == null or not target_unit.is_alive:
		return

	var source_unit: BattleUnitState = state.units.get(effect_state.source_unit_id) as BattleUnitState if effect_state.source_unit_id != &"" else null
	if not _is_unit_valid_for_effect(source_unit, target_unit, effect_state.target_team_filter):
		return

	var tick_key := "%s|%s|%d" % [String(effect_state.field_instance_id), String(target_unit.unit_id), int(effect_state.next_tick_at_tu)]
	if processed_tick_keys.has(tick_key):
		return
	processed_tick_keys[tick_key] = true

	var temp_effect := COMBAT_EFFECT_DEF_SCRIPT.new()
	temp_effect.effect_type = effect_state.effect_type
	temp_effect.power = int(effect_state.power)
	temp_effect.damage_tag = effect_state.damage_tag
	temp_effect.status_id = ProgressionDataUtils.to_string_name(effect_state.params.get("status_id", ""))
	temp_effect.params = effect_state.params.duplicate(true)

	var result: Dictionary = damage_resolver.resolve_effects(source_unit, target_unit, [temp_effect])
	if not bool(result.get("applied", false)):
		return

	_runtime.mark_applied_statuses_for_turn_timing(target_unit, result.get("status_effect_ids", []))
	_runtime.append_result_source_status_effects(batch, source_unit, result)
	_runtime.append_changed_unit_id(batch, target_unit.unit_id)
	_runtime.append_changed_unit_coords(batch, target_unit)
	var damage := int(result.get("damage", 0))
	var healing := int(result.get("healing", 0))
	var damage_summary: Dictionary = _runtime.summarize_damage_result(result)
	var kill_count := 0
	if bool(damage_summary.get("has_damage_event", false)):
		if damage > 0:
			var damage_line := "%s 受到 %s 的 %d 点伤害" % [
				target_unit.display_name,
				_get_timed_terrain_effect_display_name(effect_state),
				damage,
			]
			if bool(damage_summary.get("any_double", false)):
				damage_line += "（触发易伤）"
			elif bool(damage_summary.get("any_half", false)):
				damage_line += "（减半后结算）"
			_runtime.append_batch_log(batch, "%s。" % damage_line)
			if int(damage_summary.get("shield_absorbed", 0)) > 0:
				_runtime.append_batch_log(batch, "%s 的护盾吸收了 %d 点伤害。" % [
					target_unit.display_name,
					int(damage_summary.get("shield_absorbed", 0)),
				])
		else:
			if bool(damage_summary.get("any_immune", false)):
				_runtime.append_batch_log(batch, "%s 命中，但 %s 免疫该伤害。" % [
					_get_timed_terrain_effect_display_name(effect_state),
					target_unit.display_name,
				])
			elif int(damage_summary.get("shield_absorbed", 0)) > 0:
				_runtime.append_batch_log(batch, "%s 命中，但被 %s 的护盾吸收了 %d 点伤害。" % [
					_get_timed_terrain_effect_display_name(effect_state),
					target_unit.display_name,
					int(damage_summary.get("shield_absorbed", 0)),
				])
			else:
				_runtime.append_batch_log(batch, "%s 命中，但 %s 的伤害被%s完全吸收。" % [
					_get_timed_terrain_effect_display_name(effect_state),
					target_unit.display_name,
					String(damage_summary.get("absorb_reason_text", "防护")),
				])
		if bool(damage_summary.get("shield_broken", false)):
			_runtime.append_batch_log(batch, "%s 的护盾被击碎。" % target_unit.display_name)
	if healing > 0:
		_runtime.append_batch_log(batch, "%s 受到 %s 影响，恢复 %d 点生命。" % [
			target_unit.display_name,
			_get_timed_terrain_effect_display_name(effect_state),
			healing,
		])
	for status_id in result.get("status_effect_ids", []):
		_runtime.append_batch_log(batch, "%s 获得状态 %s。" % [target_unit.display_name, String(status_id)])

	if not target_unit.is_alive:
		kill_count = 1
		_runtime.clear_defeated_unit(target_unit, batch)
		_runtime.append_batch_log(batch, "%s 被击倒。" % target_unit.display_name)
		_runtime.record_enemy_defeated_achievement(source_unit, target_unit)

	if source_unit != null:
		_runtime.record_skill_effect_result(source_unit, damage, healing, kill_count)


func _get_timed_terrain_move_cost_delta(effect_state) -> int:
	if effect_state == null:
		return 0
	if effect_state.remaining_tu <= 0 and not _is_battle_lifetime_effect(effect_state):
		return 0
	return maxi(int(effect_state.params.get(PARAM_MOVE_COST_DELTA, 0)), 0)


func _is_blocked_by_nonstacking_status(unit_state: BattleUnitState, effect_state) -> bool:
	if unit_state == null or effect_state == null:
		return false
	if _unit_has_status_from_param(unit_state, effect_state.params.get(PARAM_DOES_NOT_STACK_WITH_STATUS_ID, "")):
		return true
	return _unit_has_status_from_param(unit_state, effect_state.params.get(PARAM_DOES_NOT_STACK_WITH_STATUS_IDS, []))


func _unit_has_status_from_param(unit_state: BattleUnitState, value: Variant) -> bool:
	if unit_state == null:
		return false
	if value is String or value is StringName:
		var status_id := ProgressionDataUtils.to_string_name(value)
		return status_id != &"" and unit_state.has_status_effect(status_id)
	if value is Array:
		for status_variant in value:
			var status_id := ProgressionDataUtils.to_string_name(status_variant)
			if status_id != &"" and unit_state.has_status_effect(status_id):
				return true
	return false


func _build_timed_terrain_effect(
	source_unit,
	skill_def,
	effect_def,
	field_instance_id: StringName
) -> BattleTerrainEffectState:
	var lifetime_policy := _resolve_lifetime_policy(effect_def)
	var tick_interval_tu := 0
	var duration_tu := 0
	if lifetime_policy == LIFETIME_POLICY_BATTLE:
		tick_interval_tu = 0
		duration_tu = 0
	else:
		tick_interval_tu = _normalize_positive_tu_value(int(effect_def.tick_interval_tu), "terrain effect tick_interval_tu")
		duration_tu = _normalize_positive_tu_value(int(effect_def.duration_tu), "terrain effect duration_tu")
		if tick_interval_tu <= 0 or duration_tu <= 0:
			return null

	var effect_state := BATTLE_TERRAIN_EFFECT_STATE_SCRIPT.new()
	effect_state.field_instance_id = field_instance_id
	effect_state.effect_id = effect_def.terrain_effect_id
	effect_state.effect_type = effect_def.tick_effect_type if effect_def.tick_effect_type != &"" else (TERRAIN_EFFECT_NONE if lifetime_policy == LIFETIME_POLICY_BATTLE else TERRAIN_EFFECT_DAMAGE)
	effect_state.source_unit_id = source_unit.unit_id if source_unit != null else &""
	effect_state.source_skill_id = skill_def.skill_id if skill_def != null else &""
	effect_state.target_team_filter = _resolve_effect_target_filter(skill_def, effect_def)
	effect_state.power = int(effect_def.power)
	effect_state.damage_tag = effect_def.damage_tag
	effect_state.tick_interval_tu = tick_interval_tu
	effect_state.remaining_tu = 0 if lifetime_policy == LIFETIME_POLICY_BATTLE else maxi(duration_tu, tick_interval_tu)
	effect_state.next_tick_at_tu = 0 if lifetime_policy == LIFETIME_POLICY_BATTLE else (_runtime.get_state().timeline.current_tu + tick_interval_tu if _runtime != null and _runtime.get_state() != null and _runtime.get_state().timeline != null else tick_interval_tu)
	effect_state.stack_behavior = _normalize_stack_behavior(effect_def.stack_behavior)
	effect_state.params = effect_def.params.duplicate(true)
	effect_state.params[PARAM_LIFETIME_POLICY] = String(lifetime_policy)
	if effect_def.status_id != &"":
		effect_state.params["status_id"] = String(effect_def.status_id)
	return effect_state


static func is_terrain_effect_active(effect_state) -> bool:
	if effect_state == null:
		return false
	if _is_battle_lifetime_effect_static(effect_state):
		return true
	return int(effect_state.remaining_tu) > 0


static func _is_battle_lifetime_effect_static(effect_state) -> bool:
	if effect_state == null or effect_state.params == null:
		return false
	var policy_value = effect_state.params.get(PARAM_LIFETIME_POLICY, effect_state.params.get(StringName(PARAM_LIFETIME_POLICY), ""))
	if policy_value is StringName:
		return policy_value == LIFETIME_POLICY_BATTLE
	if policy_value is String:
		return StringName(policy_value) == LIFETIME_POLICY_BATTLE
	return false


func _is_battle_lifetime_effect(effect_state) -> bool:
	return _is_battle_lifetime_effect_static(effect_state)


func _resolve_lifetime_policy(effect_def) -> StringName:
	if effect_def == null or effect_def.params == null:
		return LIFETIME_POLICY_TIMED
	var value = effect_def.params.get(PARAM_LIFETIME_POLICY, effect_def.params.get(StringName(PARAM_LIFETIME_POLICY), LIFETIME_POLICY_TIMED))
	if value is StringName:
		return value if value == LIFETIME_POLICY_BATTLE else LIFETIME_POLICY_TIMED
	if value is String:
		return LIFETIME_POLICY_BATTLE if StringName(value) == LIFETIME_POLICY_BATTLE else LIFETIME_POLICY_TIMED
	return LIFETIME_POLICY_TIMED


func _resolve_effect_target_filter(skill_def, effect_def) -> StringName:
	if effect_def != null and effect_def.effect_target_team_filter != &"":
		return effect_def.effect_target_team_filter
	if skill_def != null and skill_def.combat_profile != null:
		return skill_def.combat_profile.target_team_filter
	return &"any"


func _is_unit_valid_for_effect(
	source_unit,
	target_unit,
	target_team_filter: StringName
) -> bool:
	if target_unit == null or not target_unit.is_alive:
		return false
	match target_team_filter:
		&"", &"any":
			return true
		&"self":
			return source_unit != null and target_unit.unit_id == source_unit.unit_id
		&"ally", &"friendly":
			return source_unit != null and target_unit.faction_id == source_unit.faction_id
		&"enemy", &"hostile":
			return source_unit != null and target_unit.faction_id != source_unit.faction_id
		_:
			return true


func _normalize_stack_behavior(stack_behavior: StringName) -> StringName:
	match stack_behavior:
		STACK_BEHAVIOR_STACK, STACK_BEHAVIOR_IGNORE_EXISTING:
			return stack_behavior
		_:
			return STACK_BEHAVIOR_REFRESH


func _normalize_positive_tu_value(value: int, field_label: String) -> int:
	if value <= 0:
		push_error("%s must be positive and use %d TU steps, got %d; skipping effect." % [field_label, TU_GRANULARITY, value])
		return -1
	if value % TU_GRANULARITY != 0:
		push_error("%s must use %d TU steps, got %d; skipping effect." % [field_label, TU_GRANULARITY, value])
		return -1
	return value


func _build_terrain_effect_instance_id(effect_id: StringName) -> StringName:
	if _has_runtime():
		var nonce: int = _runtime.increment_terrain_effect_nonce()
		return StringName("%s_%d_%d" % [
			String(effect_id),
			int(_runtime.get_state().timeline.current_tu) if _runtime.get_state() != null and _runtime.get_state().timeline != null else 0,
			nonce,
		])
	return StringName("%s_%d_%d" % [String(effect_id), 0, 1])


func _get_timed_terrain_effect_display_name(effect_state: BattleTerrainEffectState) -> String:
	if effect_state != null and effect_state.params.has("display_name"):
		return String(effect_state.params.get("display_name", ""))
	return String(effect_state.effect_id) if effect_state != null else "地格效果"


func _sort_coords(target_coords: Variant) -> Array[Vector2i]:
	var sorted_coords: Array[Vector2i] = []
	if target_coords is Array:
		for coord_variant in target_coords:
			if coord_variant is Vector2i:
				sorted_coords.append(coord_variant)
	sorted_coords.sort_custom(func(a: Vector2i, b: Vector2i) -> bool:
		return a.y < b.y or (a.y == b.y and a.x < b.x)
	)
	return sorted_coords


func _has_runtime() -> bool:
	return _runtime != null
