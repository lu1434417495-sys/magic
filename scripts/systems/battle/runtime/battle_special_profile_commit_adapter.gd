class_name BattleSpecialProfileCommitAdapter
extends RefCounted

const BattleCommonSkillOutcome = preload("res://scripts/systems/battle/core/battle_common_skill_outcome.gd")
const BattleEventBatch = preload("res://scripts/systems/battle/core/battle_event_batch.gd")
const BattleSkillOutcomeCommitter = preload("res://scripts/systems/battle/runtime/battle_skill_outcome_committer.gd")
const MeteorSwarmCommitResult = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_commit_result.gd")

var _runtime = null
var _committer: BattleSkillOutcomeCommitter = null


func setup(runtime, committer: BattleSkillOutcomeCommitter = null) -> void:
	_runtime = runtime
	_committer = committer


func dispose() -> void:
	_runtime = null
	_committer = null


func commit_meteor_swarm_result(result: MeteorSwarmCommitResult, batch: BattleEventBatch) -> bool:
	if result == null or batch == null:
		return false
	if _committer == null:
		return false
	var commit_payload := result.to_common_outcome_payload().duplicate(true)
	if String(commit_payload.get("commit_schema_id", "")) != "meteor_swarm_ground_commit":
		return false
	var outcome := _build_common_outcome_from_meteor_result(result, commit_payload)
	return _committer.commit_common_outcome(outcome, batch)


func _build_common_outcome_from_meteor_result(
	result: MeteorSwarmCommitResult,
	commit_payload: Dictionary
) -> BattleCommonSkillOutcome:
	var outcome := BattleCommonSkillOutcome.new()
	if result.plan != null:
		outcome.source_unit_id = result.plan.source_unit_id
		outcome.skill_id = result.plan.skill_id
	outcome.total_damage = int(commit_payload.get("total_damage", result.total_damage))
	outcome.total_healing = int(commit_payload.get("total_healing", result.total_healing))
	for unit_id in result.changed_unit_ids:
		outcome.add_changed_unit_id(unit_id)
	for coord in result.changed_coords:
		outcome.add_changed_coord(coord)
	for defeated_unit_id in result.defeated_unit_ids:
		outcome.add_defeated_unit_id(defeated_unit_id)
	for target_outcome in result.target_outcomes:
		if target_outcome == null:
			continue
		outcome.add_changed_unit_id(target_outcome.target_unit_id)
		outcome.add_status_effect_ids(target_outcome.target_unit_id, target_outcome.status_effect_ids)
		if target_outcome.defeated:
			outcome.add_defeated_unit_id(target_outcome.target_unit_id)
	for message in result.log_lines:
		outcome.log_lines.append(message)
	for report_entry in result.report_entries:
		outcome.report_entries.append(report_entry.duplicate(true))
	return outcome
