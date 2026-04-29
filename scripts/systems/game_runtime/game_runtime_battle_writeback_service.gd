class_name GameRuntimeBattleWritebackService
extends RefCounted

const BattleState = preload("res://scripts/systems/battle/core/battle_state.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const PARTY_STATE_SCRIPT = preload("res://scripts/player/progression/party_state.gd")
const PARTY_WAREHOUSE_SERVICE_SCRIPT = preload("res://scripts/systems/inventory/party_warehouse_service.gd")

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


func commit_battle_local_views_to_party_state(battle_state: BattleState, party_state) -> Dictionary:
	return _commit_battle_local_views_to_party_state(battle_state, party_state)


func report_invariant_failure(writeback_result: Dictionary, battle_summary: Dictionary, winner_faction_id: String) -> void:
	_report_battle_local_writeback_invariant_failure(writeback_result, battle_summary, winner_faction_id)

func _commit_battle_local_views_to_party_state(battle_state: BattleState, party_state) -> Dictionary:
	if battle_state == null:
		return _build_battle_local_writeback_failure("battle_local_writeback_missing_battle_state")
	if party_state == null or not (party_state is Object and party_state.has_method("to_dict")):
		return _build_battle_local_writeback_failure("battle_local_writeback_missing_party_state")

	var candidate_party = _clone_party_state_for_battle_writeback(party_state)
	if candidate_party == null:
		return _build_battle_local_writeback_failure("battle_local_writeback_invalid_party_state")

	var backpack_view = battle_state.get_party_backpack_view()
	if backpack_view == null or not (backpack_view is Object and backpack_view.has_method("duplicate_state")):
		return _build_battle_local_writeback_failure("battle_local_writeback_invalid_backpack_view")
	candidate_party.warehouse_state = backpack_view.duplicate_state()
	if candidate_party.warehouse_state == null:
		return _build_battle_local_writeback_failure("battle_local_writeback_invalid_backpack_view")

	var committed_member_ids: Dictionary = {}
	for ally_unit_id in battle_state.ally_unit_ids:
		var unit_state := battle_state.units.get(ally_unit_id) as BattleUnitState
		if unit_state == null:
			return _build_battle_local_writeback_failure("battle_local_writeback_missing_ally_unit", {
				"unit_id": String(ally_unit_id),
			})
		var member_id := ProgressionDataUtils.to_string_name(unit_state.source_member_id)
		if member_id == &"":
			continue
		if committed_member_ids.has(member_id):
			return _build_battle_local_writeback_failure("battle_local_writeback_duplicate_member_unit", {
				"member_id": String(member_id),
			})
		var member_state = candidate_party.get_member_state(member_id)
		if member_state == null:
			return _build_battle_local_writeback_failure("battle_local_writeback_member_not_found", {
				"member_id": String(member_id),
				"unit_id": String(unit_state.unit_id),
			})
		if not bool(unit_state.equipment_view_initialized):
			return _build_battle_local_writeback_failure("battle_local_writeback_uninitialized_equipment_view", {
				"member_id": String(member_id),
				"unit_id": String(unit_state.unit_id),
			})
		var equipment_view = unit_state.equipment_view
		if equipment_view == null or not (equipment_view is Object and equipment_view.has_method("duplicate_state")):
			return _build_battle_local_writeback_failure("battle_local_writeback_invalid_equipment_view", {
				"member_id": String(member_id),
				"unit_id": String(unit_state.unit_id),
			})
		var equipment_copy = equipment_view.duplicate_state()
		if equipment_copy == null:
			return _build_battle_local_writeback_failure("battle_local_writeback_invalid_equipment_view", {
				"member_id": String(member_id),
				"unit_id": String(unit_state.unit_id),
			})
		member_state.equipment_state = equipment_copy
		committed_member_ids[member_id] = true

	var validation_result := _validate_battle_local_candidate_party_state(candidate_party)
	if not bool(validation_result.get("ok", false)):
		return validation_result

	_runtime._party_state = candidate_party
	_sync_runtime_party_services_after_battle_local_writeback()
	return {
		"ok": true,
		"error_code": "",
		"committed_member_count": committed_member_ids.size(),
		"used_slots": int(validation_result.get("used_slots", 0)),
		"capacity": int(validation_result.get("capacity", 0)),
	}


func _clone_party_state_for_battle_writeback(party_state):
	if party_state == null or not (party_state is Object and party_state.has_method("to_dict")):
		return null
	var party_payload: Variant = party_state.to_dict()
	if party_payload is not Dictionary:
		return null
	return PARTY_STATE_SCRIPT.from_dict(party_payload)


func _validate_battle_local_candidate_party_state(candidate_party) -> Dictionary:
	if candidate_party == null or candidate_party.warehouse_state == null:
		return _build_battle_local_writeback_failure("battle_local_writeback_invalid_candidate_party")
	var instance_owner_by_id: Dictionary = {}
	for instance in candidate_party.warehouse_state.get_non_empty_instances():
		var instance_id := ProgressionDataUtils.to_string_name(instance.instance_id)
		var item_id := ProgressionDataUtils.to_string_name(instance.item_id)
		var register_result := _register_battle_local_instance_owner(
			instance_owner_by_id,
			instance_id,
			item_id,
			"backpack"
		)
		if not bool(register_result.get("ok", false)):
			return register_result

	for member_id_str in ProgressionDataUtils.sorted_string_keys(candidate_party.member_states):
		var member_id := StringName(member_id_str)
		var member_state = candidate_party.get_member_state(member_id)
		if member_state == null:
			continue
		var equipment_state = member_state.equipment_state
		if equipment_state == null or not (equipment_state is Object and equipment_state.has_method("get_entry_slot_ids")):
			return _build_battle_local_writeback_failure("battle_local_writeback_invalid_equipment_state", {
				"member_id": String(member_id),
			})
		for entry_slot_id in equipment_state.get_entry_slot_ids():
			var item_id := ProgressionDataUtils.to_string_name(equipment_state.get_equipped_item_id(entry_slot_id))
			if item_id == &"":
				return _build_battle_local_writeback_failure("battle_local_writeback_invalid_equipment_entry", {
					"member_id": String(member_id),
					"entry_slot_id": String(entry_slot_id),
				})
			var instance_id := ProgressionDataUtils.to_string_name(equipment_state.get_equipped_instance_id(entry_slot_id))
			if instance_id == &"":
				continue
			var register_result := _register_battle_local_instance_owner(
				instance_owner_by_id,
				instance_id,
				item_id,
				"equipment:%s:%s" % [String(member_id), String(entry_slot_id)]
			)
			if not bool(register_result.get("ok", false)):
				return register_result

	var capacity_service = PARTY_WAREHOUSE_SERVICE_SCRIPT.new()
	var item_defs: Dictionary = _runtime._game_session.get_item_defs() if _runtime._game_session != null else {}
	capacity_service.setup(candidate_party, item_defs)
	var used_slots := int(capacity_service.get_used_slots())
	var capacity := int(capacity_service.get_total_capacity())
	if used_slots > capacity:
		return _build_battle_local_writeback_failure("battle_local_writeback_capacity_mismatch", {
			"used_slots": used_slots,
			"capacity": capacity,
		})
	return {
		"ok": true,
		"error_code": "",
		"used_slots": used_slots,
		"capacity": capacity,
	}


func _register_battle_local_instance_owner(
	instance_owner_by_id: Dictionary,
	instance_id: StringName,
	item_id: StringName,
	owner_label: String
) -> Dictionary:
	if instance_id == &"":
		return {"ok": true, "error_code": ""}
	var instance_key := String(instance_id)
	if instance_owner_by_id.has(instance_key):
		var previous_owner: Dictionary = instance_owner_by_id.get(instance_key, {})
		return _build_battle_local_writeback_failure("battle_local_writeback_instance_conflict", {
			"instance_id": instance_key,
			"item_id": String(item_id),
			"owner": owner_label,
			"previous_owner": String(previous_owner.get("owner", "")),
			"previous_item_id": String(previous_owner.get("item_id", "")),
		})
	instance_owner_by_id[instance_key] = {
		"owner": owner_label,
		"item_id": String(item_id),
	}
	return {"ok": true, "error_code": ""}


func _sync_runtime_party_services_after_battle_local_writeback() -> void:
	var item_defs: Dictionary = _runtime._game_session.get_item_defs() if _runtime._game_session != null else {}
	if _runtime._character_management != null:
		_runtime._character_management.set_party_state(_runtime._party_state)
	if _runtime._party_warehouse_service != null:
		_runtime._setup_party_warehouse_service(_runtime._party_warehouse_service, _runtime._party_state, item_defs)
	if _runtime._party_item_use_service != null:
		_runtime._party_item_use_service.setup(
			_runtime._party_state,
			item_defs,
			_runtime._game_session.get_skill_defs() if _runtime._game_session != null else {},
			_runtime._party_warehouse_service,
			_runtime._character_management
		)
	if _runtime._party_equipment_service != null:
		_runtime._party_equipment_service.setup(_runtime._party_state, item_defs, _runtime._party_warehouse_service, _runtime._get_equipment_instance_id_allocator())


func _build_battle_local_writeback_failure(error_code: String, details: Dictionary = {}) -> Dictionary:
	return {
		"ok": false,
		"error_code": error_code,
		"details": details.duplicate(true),
	}


func _report_battle_local_writeback_invariant_failure(
	writeback_result: Dictionary,
	battle_summary: Dictionary,
	winner_faction_id: String
) -> void:
	var error_code := String(writeback_result.get("error_code", "battle_local_writeback_invariant_failed"))
	var details: Dictionary = writeback_result.get("details", {}).duplicate(true) if writeback_result.get("details", {}) is Dictionary else {}
	var message := "Battle-local party writeback invariant failed: %s %s" % [error_code, JSON.stringify(details)]
	push_error(message)
	_runtime._update_status("战斗结算发生内部不变量错误：battle-local 队伍状态写回不可能失败但失败了（%s）。" % error_code)
	_runtime._log_runtime_event(
		"error",
		"battle",
		"battle.local_writeback_invariant_failed",
		_runtime._current_status_message,
		{
			"battle": battle_summary,
			"winner_faction_id": winner_faction_id,
			"error_code": error_code,
			"details": details,
		}
	)
	assert(false, message)

