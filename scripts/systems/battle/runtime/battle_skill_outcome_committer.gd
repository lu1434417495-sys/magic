class_name BattleSkillOutcomeCommitter
extends RefCounted

const BattleCommonSkillOutcome = preload("res://scripts/systems/battle/core/battle_common_skill_outcome.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")

var _runtime = null


func setup(runtime) -> void:
	_runtime = runtime


func dispose() -> void:
	_runtime = null


func commit_common_outcome(outcome: BattleCommonSkillOutcome, batch: BattleEventBatch) -> bool:
	if _runtime == null or outcome == null or batch == null:
		return false
	for unit_id in outcome.changed_unit_ids:
		_runtime.append_changed_unit_id(batch, unit_id)
	for coord in outcome.changed_coords:
		_runtime.append_changed_coord(batch, coord)
	for message in outcome.log_lines:
		_runtime.append_batch_log(batch, message)
	for report_entry in outcome.report_entries:
		if report_entry.is_empty():
			continue
		_runtime._append_report_entry_to_batch(batch, report_entry)
	_commit_status_turn_timing(outcome)
	var defeated_count := _commit_defeated_units(outcome, batch)
	var source_unit := _get_unit(outcome.source_unit_id)
	if source_unit != null:
		_runtime.record_skill_effect_result(source_unit, outcome.total_damage, outcome.total_healing, defeated_count)
	return true


func _commit_status_turn_timing(outcome: BattleCommonSkillOutcome) -> void:
	for unit_id_variant in outcome.status_effect_ids_by_unit_id.keys():
		var unit_id := ProgressionDataUtils.to_string_name(unit_id_variant)
		var unit_state := _get_unit(unit_id)
		if unit_state == null:
			continue
		var status_ids: Array[StringName] = []
		var raw_status_ids = outcome.status_effect_ids_by_unit_id.get(unit_id_variant, [])
		if raw_status_ids is Array:
			for status_variant in raw_status_ids:
				var status_id := ProgressionDataUtils.to_string_name(status_variant)
				if status_id != &"" and not status_ids.has(status_id):
					status_ids.append(status_id)
		_runtime.mark_applied_statuses_for_turn_timing(unit_state, status_ids)


func _commit_defeated_units(outcome: BattleCommonSkillOutcome, batch: BattleEventBatch) -> int:
	var source_unit := _get_unit(outcome.source_unit_id)
	var defeated_count := 0
	for defeated_unit_id in outcome.defeated_unit_ids:
		var defeated_unit := _get_unit(defeated_unit_id)
		if defeated_unit == null:
			continue
		defeated_count += 1
		_runtime.handle_unit_defeated_by_runtime_effect(
			defeated_unit,
			source_unit,
			batch,
			"%s 被击倒。" % defeated_unit.display_name,
			{"record_enemy_defeated_achievement": true}
		)
	return defeated_count


func _get_unit(unit_id: StringName) -> BattleUnitState:
	if _runtime == null or unit_id == &"" or _runtime.get_state() == null:
		return null
	return _runtime.get_state().units.get(unit_id) as BattleUnitState
