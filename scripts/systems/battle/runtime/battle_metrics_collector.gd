class_name BattleMetricsCollector
extends RefCounted

const BattleCommand = preload("res://scripts/systems/battle/core/battle_command.gd")

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


func _initialize_battle_metrics() -> void:
	_runtime._battle_metrics = {
		"battle_id": String(_runtime._state.battle_id) if _runtime._state != null else "",
		"seed": int(_runtime._state.seed) if _runtime._state != null else 0,
		"units": {},
		"factions": {},
	}
	if _runtime._state == null:
		return
	for unit_variant in _runtime._state.units.values():
		var unit_state = unit_variant as BattleUnitState
		if unit_state == null:
			continue
		var unit_entry = _build_unit_metric_entry(unit_state)
		_runtime._battle_metrics["units"][String(unit_state.unit_id)] = unit_entry
		var faction_entry = _ensure_faction_metric_entry(unit_state.faction_id)
		faction_entry["unit_count"] = int(faction_entry.get("unit_count", 0)) + 1

func _build_unit_metric_entry(unit_state: BattleUnitState) -> Dictionary:
	return {
		"unit_id": String(unit_state.unit_id),
		"display_name": unit_state.display_name,
		"faction_id": String(unit_state.faction_id),
		"control_mode": String(unit_state.control_mode),
		"source_member_id": String(unit_state.source_member_id),
		"turn_count": 0,
		"action_counts": {"move": 0, "skill": 0, "wait": 0},
		"skill_attempt_counts": {},
		"skill_success_counts": {},
		"successful_skill_count": 0,
		"total_damage_done": 0,
		"total_healing_done": 0,
		"total_damage_taken": 0,
		"total_healing_received": 0,
		"kill_count": 0,
		"death_count": 0,
	}

func _ensure_unit_metric_entry(unit_state: BattleUnitState) -> Dictionary:
	if _runtime._battle_metrics.is_empty() or unit_state == null:
		return {}
	var units: Dictionary = _runtime._battle_metrics.get("units", {})
	var unit_key = String(unit_state.unit_id)
	if not units.has(unit_key):
		units[unit_key] = _build_unit_metric_entry(unit_state)
		_runtime._battle_metrics["units"] = units
	return units.get(unit_key, {})

func _ensure_faction_metric_entry(faction_id: StringName) -> Dictionary:
	if _runtime._battle_metrics.is_empty():
		return {}
	var factions: Dictionary = _runtime._battle_metrics.get("factions", {})
	var faction_key = String(faction_id)
	if not factions.has(faction_key):
		factions[faction_key] = {
			"faction_id": faction_key,
			"unit_count": 0,
			"turn_count": 0,
			"action_counts": {"move": 0, "skill": 0, "wait": 0},
			"skill_attempt_counts": {},
			"skill_success_counts": {},
			"successful_skill_count": 0,
			"total_damage_done": 0,
			"total_healing_done": 0,
			"total_damage_taken": 0,
			"total_healing_received": 0,
			"kill_count": 0,
			"death_count": 0,
		}
		_runtime._battle_metrics["factions"] = factions
	return factions.get(faction_key, {})

func _record_turn_started(unit_state: BattleUnitState) -> void:
	var unit_entry = _ensure_unit_metric_entry(unit_state)
	if unit_entry.is_empty():
		return
	unit_entry["turn_count"] = int(unit_entry.get("turn_count", 0)) + 1
	var faction_entry = _ensure_faction_metric_entry(unit_state.faction_id)
	faction_entry["turn_count"] = int(faction_entry.get("turn_count", 0)) + 1

func _record_action_issued(unit_state: BattleUnitState, command_type: StringName, ap_cost: int = 0) -> void:
	if unit_state != null:
		if command_type == BattleCommand.TYPE_MOVE:
			unit_state.has_moved_this_turn = true
		elif command_type != BattleCommand.TYPE_WAIT and ap_cost > 0:
			unit_state.has_taken_action_this_turn = true
			unit_state.is_resting = false
	var command_key = String(command_type)
	if command_key.is_empty():
		return
	var unit_entry = _ensure_unit_metric_entry(unit_state)
	if unit_entry.is_empty():
		return
	_increment_metric_count(unit_entry.get("action_counts", {}), command_key, 1)
	var faction_entry = _ensure_faction_metric_entry(unit_state.faction_id)
	_increment_metric_count(faction_entry.get("action_counts", {}), command_key, 1)

func _record_skill_attempt(unit_state: BattleUnitState, skill_id: StringName) -> void:
	var skill_key = String(skill_id)
	if skill_key.is_empty():
		return
	var unit_entry = _ensure_unit_metric_entry(unit_state)
	if unit_entry.is_empty():
		return
	_increment_metric_count(unit_entry.get("skill_attempt_counts", {}), skill_key, 1)
	var faction_entry = _ensure_faction_metric_entry(unit_state.faction_id)
	_increment_metric_count(faction_entry.get("skill_attempt_counts", {}), skill_key, 1)

func _record_skill_success(unit_state: BattleUnitState, skill_id: StringName) -> void:
	var skill_key = String(skill_id)
	if skill_key.is_empty():
		return
	var unit_entry = _ensure_unit_metric_entry(unit_state)
	if unit_entry.is_empty():
		return
	_increment_metric_count(unit_entry.get("skill_success_counts", {}), skill_key, 1)
	unit_entry["successful_skill_count"] = int(unit_entry.get("successful_skill_count", 0)) + 1
	var faction_entry = _ensure_faction_metric_entry(unit_state.faction_id)
	_increment_metric_count(faction_entry.get("skill_success_counts", {}), skill_key, 1)
	faction_entry["successful_skill_count"] = int(faction_entry.get("successful_skill_count", 0)) + 1

func _record_effect_metrics(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	damage: int,
	healing: int,
	kill_count: int
) -> void:
	if source_unit == null or target_unit == null:
		return
	var source_entry = _ensure_unit_metric_entry(source_unit)
	var target_entry = _ensure_unit_metric_entry(target_unit)
	var source_faction_entry = _ensure_faction_metric_entry(source_unit.faction_id)
	var target_faction_entry = _ensure_faction_metric_entry(target_unit.faction_id)
	if damage > 0:
		source_entry["total_damage_done"] = int(source_entry.get("total_damage_done", 0)) + damage
		target_entry["total_damage_taken"] = int(target_entry.get("total_damage_taken", 0)) + damage
		source_faction_entry["total_damage_done"] = int(source_faction_entry.get("total_damage_done", 0)) + damage
		target_faction_entry["total_damage_taken"] = int(target_faction_entry.get("total_damage_taken", 0)) + damage
	if healing > 0:
		source_entry["total_healing_done"] = int(source_entry.get("total_healing_done", 0)) + healing
		target_entry["total_healing_received"] = int(target_entry.get("total_healing_received", 0)) + healing
		source_faction_entry["total_healing_done"] = int(source_faction_entry.get("total_healing_done", 0)) + healing
		target_faction_entry["total_healing_received"] = int(target_faction_entry.get("total_healing_received", 0)) + healing
	if kill_count > 0:
		source_entry["kill_count"] = int(source_entry.get("kill_count", 0)) + kill_count
		source_faction_entry["kill_count"] = int(source_faction_entry.get("kill_count", 0)) + kill_count

func _record_unit_defeated(unit_state: BattleUnitState) -> void:
	var unit_entry = _ensure_unit_metric_entry(unit_state)
	if unit_entry.is_empty():
		return
	unit_entry["death_count"] = int(unit_entry.get("death_count", 0)) + 1
	var faction_entry = _ensure_faction_metric_entry(unit_state.faction_id)
	faction_entry["death_count"] = int(faction_entry.get("death_count", 0)) + 1

func _increment_metric_count(metric_map: Dictionary, key: String, delta: int) -> void:
	metric_map[key] = int(metric_map.get(key, 0)) + delta
