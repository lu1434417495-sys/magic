class_name BattleBarrierOutcomeResolver
extends RefCounted

const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleStatusEffectState = preload("res://scripts/systems/battle/core/battle_status_effect_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const BattleSaveResolver = preload("res://scripts/systems/battle/rules/battle_save_resolver.gd")
const CombatEffectDef = preload("res://scripts/player/progression/combat_effect_def.gd")
const ProgressionDataUtils = preload("res://scripts/player/progression/progression_data_utils.gd")
const TRUE_RANDOM_SEED_SERVICE_SCRIPT = preload("res://scripts/utils/true_random_seed_service.gd")
const UNIT_BASE_ATTRIBUTES_SCRIPT = preload("res://scripts/player/progression/unit_base_attributes.gd")

const DEFAULT_FATAL_DAMAGE := 99999
const TELEPORT_RANDOM_ATTEMPTS := 64

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


func apply_passage_outcomes(
	unit_state: BattleUnitState,
	barrier: Dictionary,
	layer: Dictionary,
	batch: BattleEventBatch
) -> Dictionary:
	var result := {
		"applied": false,
		"stopped": false,
	}
	if unit_state == null or barrier.is_empty() or layer.is_empty():
		return result
	var outcomes: Array = layer.get("passage_outcomes", [])
	if outcomes.is_empty() and layer.has("passage"):
		outcomes = [layer.get("passage", {})]
	for outcome_variant in outcomes:
		var outcome: Dictionary = outcome_variant if outcome_variant is Dictionary else {}
		if outcome.is_empty():
			continue
		var outcome_result := _apply_outcome(unit_state, barrier, layer, outcome, batch)
		result["applied"] = true
		if bool(outcome_result.get("stopped", false)) or not unit_state.is_alive:
			result["stopped"] = true
			return result
	return result


func _apply_outcome(
	unit_state: BattleUnitState,
	barrier: Dictionary,
	layer: Dictionary,
	outcome: Dictionary,
	batch: BattleEventBatch
) -> Dictionary:
	var outcome_type := ProgressionDataUtils.to_string_name(outcome.get("outcome_type", outcome.get("outcome", "")))
	match outcome_type:
		&"damage":
			return _apply_damage_outcome(unit_state, barrier, layer, outcome, batch)
		&"poison_death":
			return _apply_poison_death_outcome(unit_state, barrier, layer, outcome, batch)
		&"status":
			var status_id := ProgressionDataUtils.to_string_name(outcome.get("status_id", ""))
			return _apply_status_outcome(unit_state, barrier, layer, outcome, status_id, batch)
		&"banish":
			return _apply_banish_outcome(unit_state, barrier, layer, outcome, batch)
		_:
			return {"stopped": false}


func _apply_damage_outcome(
	unit_state: BattleUnitState,
	barrier: Dictionary,
	layer: Dictionary,
	outcome: Dictionary,
	batch: BattleEventBatch
) -> Dictionary:
	var amount := maxi(int(outcome.get("amount", 0)), 0)
	if amount <= 0:
		return {"stopped": false}
	var save_result := _resolve_outcome_save(unit_state, barrier, layer, outcome)
	var final_amount := amount
	if bool(save_result.get("success", false)) and bool(outcome.get("half_on_success", false)):
		final_amount = maxi(int(ceil(float(amount) / 2.0)), 1)
	var damage_tag := ProgressionDataUtils.to_string_name(outcome.get("damage_tag", "force"))
	var damage_result := _apply_direct_damage(unit_state, barrier, final_amount, damage_tag)
	_append_changed_unit(batch, unit_state)
	_append_log(batch, "%s 触碰 %s，受到 %d 点伤害。" % [
		unit_state.display_name,
		_get_layer_label(layer),
		int(damage_result.get("damage", final_amount)),
	])
	if not unit_state.is_alive:
		_handle_defeated_by_barrier(unit_state, barrier, batch)
		return {"stopped": true}
	return {"stopped": false}


func _apply_poison_death_outcome(
	unit_state: BattleUnitState,
	barrier: Dictionary,
	layer: Dictionary,
	outcome: Dictionary,
	batch: BattleEventBatch
) -> Dictionary:
	var save_result := _resolve_outcome_save(unit_state, barrier, layer, outcome)
	if bool(save_result.get("success", false)):
		var success_amount := maxi(int(outcome.get("success_amount", 0)), 0)
		if success_amount <= 0:
			return {"stopped": false}
		var damage_tag := ProgressionDataUtils.to_string_name(outcome.get("success_damage_tag", outcome.get("damage_tag", "poison")))
		var damage_result := _apply_direct_damage(unit_state, barrier, success_amount, damage_tag)
		_append_changed_unit(batch, unit_state)
		_append_log(batch, "%s 通过 %s 的豁免，仍受到 %d 点伤害。" % [
			unit_state.display_name,
			_get_layer_label(layer),
			int(damage_result.get("damage", success_amount)),
		])
		if not unit_state.is_alive:
			_handle_defeated_by_barrier(unit_state, barrier, batch)
			return {"stopped": true}
		return {"stopped": false}
	var fatal_damage := maxi(
		unit_state.current_hp + unit_state.current_shield_hp + int(outcome.get("fatal_damage", DEFAULT_FATAL_DAMAGE)),
		int(outcome.get("fatal_damage", DEFAULT_FATAL_DAMAGE))
	)
	var death_result := _apply_direct_damage(unit_state, barrier, fatal_damage, &"poison")
	_append_changed_unit(batch, unit_state)
	_append_log(batch, "%s 未通过 %s 的豁免，触发即死效果。" % [
		unit_state.display_name,
		_get_layer_label(layer),
	])
	if not unit_state.is_alive:
		_handle_defeated_by_barrier(unit_state, barrier, batch)
		return {"stopped": true}
	if int(death_result.get("damage", 0)) > 0:
		_append_log(batch, "%s 的免死效果抵消了即死。" % unit_state.display_name)
	return {"stopped": false}


func _apply_status_outcome(
	unit_state: BattleUnitState,
	barrier: Dictionary,
	layer: Dictionary,
	outcome: Dictionary,
	status_id: StringName,
	batch: BattleEventBatch
) -> Dictionary:
	if status_id == &"":
		return {"stopped": false}
	var save_result := _resolve_outcome_save(unit_state, barrier, layer, outcome)
	if bool(save_result.get("success", false)):
		_append_log(batch, "%s 通过 %s 的豁免。" % [
			unit_state.display_name,
			_get_layer_label(layer),
		])
		return {"stopped": false}
	_apply_barrier_status(unit_state, barrier, layer, outcome, status_id)
	_append_changed_unit(batch, unit_state)
	_append_log(batch, "%s 未通过 %s 的豁免，获得状态 %s。" % [
		unit_state.display_name,
		_get_layer_label(layer),
		String(status_id),
	])
	return {"stopped": true}


func _apply_banish_outcome(
	unit_state: BattleUnitState,
	barrier: Dictionary,
	layer: Dictionary,
	outcome: Dictionary,
	batch: BattleEventBatch
) -> Dictionary:
	var save_result := _resolve_outcome_save(unit_state, barrier, layer, outcome)
	if bool(save_result.get("success", false)):
		_append_log(batch, "%s 通过 %s 的豁免。" % [
			unit_state.display_name,
			_get_layer_label(layer),
		])
		return {"stopped": false}
	if _is_summoned_unit(unit_state):
		_remove_summoned_unit(unit_state, barrier, layer, batch)
		return {"stopped": true}
	var destination := _find_banish_teleport_coord(unit_state, barrier)
	if destination == Vector2i(-1, -1):
		_append_log(batch, "%s 被 %s 放逐，但没有找到可传送落点。" % [unit_state.display_name, _get_layer_label(layer)])
		return {"stopped": true}
	var previous_coords := unit_state.occupied_coords.duplicate()
	_runtime._grid_service.clear_unit_occupancy(_runtime._state, unit_state)
	unit_state.set_anchor_coord(destination)
	_runtime._grid_service.set_occupants(_runtime._state, unit_state.occupied_coords, unit_state.unit_id)
	_append_changed_coords(batch, previous_coords)
	_append_changed_unit(batch, unit_state)
	_append_log(batch, "%s 被 %s 随机传送到 (%d, %d)。" % [
		unit_state.display_name,
		_get_layer_label(layer),
		destination.x,
		destination.y,
	])
	return {"stopped": true}


func _resolve_outcome_save(
	unit_state: BattleUnitState,
	barrier: Dictionary,
	layer: Dictionary,
	outcome: Dictionary
) -> Dictionary:
	var effect := CombatEffectDef.new()
	effect.effect_type = &"status"
	effect.save_dc = maxi(int(outcome.get("save_dc", barrier.get("save_dc", 0))), 1)
	effect.save_dc_mode = BattleSaveResolver.SAVE_DC_MODE_STATIC
	effect.save_ability = ProgressionDataUtils.to_string_name(outcome.get("save_ability", UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER))
	effect.save_tag = ProgressionDataUtils.to_string_name(outcome.get("save_tag", &"magic"))
	var context := {}
	if layer.has("save_roll_override"):
		context["save_roll_override"] = int(layer.get("save_roll_override", 0))
	return BattleSaveResolver.resolve_save(_get_barrier_source_unit(barrier), unit_state, effect, context)


func _apply_barrier_status(
	unit_state: BattleUnitState,
	barrier: Dictionary,
	layer: Dictionary,
	outcome: Dictionary,
	status_id: StringName
) -> void:
	var status_entry := BattleStatusEffectState.new()
	status_entry.status_id = status_id
	status_entry.source_unit_id = ProgressionDataUtils.to_string_name(barrier.get("source_unit_id", ""))
	status_entry.power = 1
	status_entry.stacks = 1
	status_entry.duration = -1
	status_entry.params = {
		"source": String(barrier.get("profile_id", "")),
		"layer_id": String(layer.get("layer_id", "")),
		"counts_as_debuff": true,
		"self_save_dc": maxi(int(outcome.get("save_dc", barrier.get("save_dc", 0))), 1),
		"self_save_ability": String(outcome.get("save_ability", UNIT_BASE_ATTRIBUTES_SCRIPT.WILLPOWER)),
		"self_save_tag": String(outcome.get("save_tag", &"magic")),
	}
	unit_state.set_status_effect(status_entry)


func _apply_direct_damage(
	unit_state: BattleUnitState,
	barrier: Dictionary,
	damage_amount: int,
	damage_tag: StringName
) -> Dictionary:
	var damage_outcome := {
		"resolved_damage": maxi(int(damage_amount), 0),
		"base_damage": maxi(int(damage_amount), 0),
		"damage_tag": String(damage_tag),
		"damage_kind": String(barrier.get("profile_id", "barrier")),
	}
	var source_unit: BattleUnitState = _get_barrier_source_unit(barrier)
	var damage_result: Dictionary = _runtime._damage_resolver.apply_direct_damage_to_target(unit_state, damage_outcome, source_unit)
	unit_state.is_alive = unit_state.current_hp > 0
	return damage_result


func _handle_defeated_by_barrier(unit_state: BattleUnitState, barrier: Dictionary, batch: BattleEventBatch) -> void:
	var source_unit: BattleUnitState = _get_barrier_source_unit(barrier)
	_runtime.handle_unit_defeated_by_runtime_effect(
		unit_state,
		source_unit,
		batch,
		"%s 被 %s 击倒。" % [unit_state.display_name, _get_barrier_label(barrier)]
	)


func _remove_summoned_unit(
	unit_state: BattleUnitState,
	barrier: Dictionary,
	layer: Dictionary,
	batch: BattleEventBatch
) -> void:
	_runtime.remove_summoned_unit_from_battle(
		unit_state,
		batch,
		"%s 是召唤物，被 %s 直接放逐消失。" % [unit_state.display_name, _get_layer_label(layer)]
	)


func _find_banish_teleport_coord(unit_state: BattleUnitState, barrier: Dictionary) -> Vector2i:
	if _runtime == null or _runtime._state == null or unit_state == null:
		return Vector2i(-1, -1)
	var candidates: Array[Vector2i] = []
	for coord_variant in _runtime._state.cells.keys():
		if coord_variant is not Vector2i:
			continue
		var coord: Vector2i = coord_variant
		if _is_coord_inside_barrier(coord, barrier):
			continue
		if not _runtime._grid_service.can_place_footprint(_runtime._state, coord, unit_state.footprint_size, unit_state.unit_id, unit_state):
			continue
		candidates.append(coord)
	if candidates.is_empty():
		return Vector2i(-1, -1)
	for _attempt in range(TELEPORT_RANDOM_ATTEMPTS):
		var index := int(TRUE_RANDOM_SEED_SERVICE_SCRIPT.randi_range(0, candidates.size() - 1))
		return candidates[index]
	candidates.sort_custom(func(left: Vector2i, right: Vector2i) -> bool:
		var left_distance: int = _runtime._grid_service.get_distance(unit_state.coord, left)
		var right_distance: int = _runtime._grid_service.get_distance(unit_state.coord, right)
		if left_distance != right_distance:
			return left_distance < right_distance
		return left.y < right.y or (left.y == right.y and left.x < right.x)
	)
	return candidates[0]


func _is_coord_inside_barrier(coord: Vector2i, barrier: Dictionary) -> bool:
	var anchor: Vector2i = barrier.get("anchor_coord", Vector2i(-999999, -999999))
	var radius := maxi(int(barrier.get("radius_cells", 0)), 0)
	var pattern := ProgressionDataUtils.to_string_name(barrier.get("area_pattern", "diamond"))
	var dx := absi(coord.x - anchor.x)
	var dy := absi(coord.y - anchor.y)
	match pattern:
		&"square", &"radius":
			return maxi(dx, dy) <= radius
		_:
			return dx + dy <= radius


func _is_summoned_unit(unit_state: BattleUnitState) -> bool:
	if unit_state == null:
		return false
	if unit_state.has_status_effect(&"summoned"):
		return true
	if bool(unit_state.ai_blackboard.get("summoned", false)):
		return true
	if bool(unit_state.ai_blackboard.get("temporary_unit", false)):
		return true
	return not String(unit_state.ai_blackboard.get("summon_source_unit_id", "")).is_empty()


func _get_barrier_source_unit(barrier: Dictionary):
	if _runtime == null or _runtime._state == null:
		return null
	var source_unit_id := ProgressionDataUtils.to_string_name(barrier.get("source_unit_id", ""))
	return _runtime._state.units.get(source_unit_id) as BattleUnitState if source_unit_id != &"" else null


func _get_layer_label(layer: Dictionary) -> String:
	return String(layer.get("display_name", layer.get("layer_id", "屏障层")))


func _get_barrier_label(barrier: Dictionary) -> String:
	return String(barrier.get("display_name", barrier.get("profile_id", "屏障")))


func _append_changed_unit(batch: BattleEventBatch, unit_state: BattleUnitState) -> void:
	if _runtime == null or batch == null or unit_state == null:
		return
	_runtime._append_changed_unit_id(batch, unit_state.unit_id)
	_runtime._append_changed_unit_coords(batch, unit_state)


func _append_changed_coords(batch: BattleEventBatch, coords: Array) -> void:
	if _runtime == null or batch == null:
		return
	_runtime._append_changed_coords(batch, coords)


func _append_log(batch: BattleEventBatch, line: String) -> void:
	if batch == null or line.is_empty():
		return
	batch.log_lines.append(line)
