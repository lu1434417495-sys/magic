class_name MeteorSwarmCommitResult
extends RefCounted

const MeteorSwarmTargetPlan = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_target_plan.gd")
const MeteorSwarmTargetOutcome = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_target_outcome.gd")

var schema_version := 1
var plan: MeteorSwarmTargetPlan = null
var target_outcomes: Array[MeteorSwarmTargetOutcome] = []
var terrain_effects: Array[Dictionary] = []
var report_entries: Array[Dictionary] = []
var log_lines: Array[String] = []
var changed_unit_ids: Array[StringName] = []
var changed_coords: Array[Vector2i] = []
var total_damage := 0
var total_healing := 0
var defeated_unit_ids: Array[StringName] = []


func add_changed_unit_id(unit_id: StringName) -> void:
	if unit_id == &"" or changed_unit_ids.has(unit_id):
		return
	changed_unit_ids.append(unit_id)


func add_changed_coord(coord: Vector2i) -> void:
	if changed_coords.has(coord):
		return
	changed_coords.append(coord)


func add_defeated_unit_id(unit_id: StringName) -> void:
	if unit_id == &"" or defeated_unit_ids.has(unit_id):
		return
	defeated_unit_ids.append(unit_id)


func to_common_outcome_payload() -> Dictionary:
	return {
		"commit_schema_id": "meteor_swarm_ground_commit",
		"schema_version": schema_version,
		"boundary_kind": "common_outcome_payload",
		"skill_id": String(plan.skill_id) if plan != null else "",
		"source_unit_id": String(plan.source_unit_id) if plan != null else "",
		"anchor_coord": plan.final_anchor_coord if plan != null else Vector2i(-1, -1),
		"nominal_plan_signature": plan.nominal_plan_signature if plan != null else "",
		"final_plan_signature": plan.final_plan_signature if plan != null else "",
		"target_count": target_outcomes.size(),
		"terrain_effect_count": terrain_effects.size(),
		"total_damage": total_damage,
		"total_healing": total_healing,
		"defeated_unit_ids": defeated_unit_ids.duplicate(),
		"changed_unit_ids": changed_unit_ids.duplicate(),
		"changed_coords": changed_coords.duplicate(),
		"target_summaries": _build_target_summaries(),
		"terrain_effects": terrain_effects.duplicate(true),
		"report_entries": report_entries.duplicate(true),
		"log_lines": log_lines.duplicate(),
	}


func _build_target_summaries() -> Array[Dictionary]:
	var summaries: Array[Dictionary] = []
	for target_outcome in target_outcomes:
		if target_outcome != null:
			summaries.append(target_outcome.to_summary_dict())
	return summaries
