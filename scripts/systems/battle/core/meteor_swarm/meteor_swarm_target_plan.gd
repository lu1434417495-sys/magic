class_name MeteorSwarmTargetPlan
extends RefCounted

const BattleUnitState = preload("res://scripts/systems/battle/core/battle_unit_state.gd")
const SkillDef = preload("res://scripts/player/progression/skill_def.gd")
const MeteorSwarmProfile = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd")

var skill_id: StringName = &"mage_meteor_swarm"
var source_unit_id: StringName = &""
var source_unit: BattleUnitState = null
var skill_def: SkillDef = null
var profile: MeteorSwarmProfile = null
var final_anchor_coord: Vector2i = Vector2i(-1, -1)
var nominal_anchor_coord: Vector2i = Vector2i(-1, -1)
var coverage_shape_id: StringName = &"square_7x7"
var radius: int = 3
var affected_coords: Array[Vector2i] = []
var ring_by_coord: Dictionary = {}
var target_unit_ids: Array[StringName] = []
var unit_distance_by_id: Dictionary = {}
var unit_primary_coord_by_id: Dictionary = {}
var drift_applied: bool = false
var drift_from_coord: Vector2i = Vector2i(-1, -1)
var nominal_plan_signature: String = ""
var final_plan_signature: String = ""


func get_distance_for_unit(unit_id: StringName) -> int:
	return int(unit_distance_by_id.get(unit_id, 999999))


func get_primary_coord_for_unit(unit_id: StringName) -> Vector2i:
	var value = unit_primary_coord_by_id.get(unit_id, Vector2i(-1, -1))
	return value if value is Vector2i else Vector2i(-1, -1)


func get_ring_for_coord(coord: Vector2i) -> int:
	return int(ring_by_coord.get(coord, 999999))


func to_dict() -> Dictionary:
	var ring_payload: Dictionary = {}
	for coord in affected_coords:
		ring_payload["%d:%d" % [coord.x, coord.y]] = get_ring_for_coord(coord)
	return {
		"skill_id": String(skill_id),
		"source_unit_id": String(source_unit_id),
		"final_anchor_coord": final_anchor_coord,
		"nominal_anchor_coord": nominal_anchor_coord,
		"coverage_shape_id": String(coverage_shape_id),
		"radius": radius,
		"affected_coords": affected_coords.duplicate(),
		"ring_by_coord": ring_payload,
		"target_unit_ids": target_unit_ids.duplicate(),
		"drift_applied": drift_applied,
		"drift_from_coord": drift_from_coord,
		"nominal_plan_signature": nominal_plan_signature,
		"final_plan_signature": final_plan_signature,
	}
