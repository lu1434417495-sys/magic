class_name MeteorSwarmTargetOutcome
extends RefCounted

const MeteorSwarmImpactComponent = preload("res://scripts/systems/battle/core/meteor_swarm/meteor_swarm_impact_component.gd")

var target_unit_id: StringName = &""
var target_coord: Vector2i = Vector2i(-1, -1)
var target_faction_id: StringName = &""
var distance_from_anchor: int = 0
var components: Array[MeteorSwarmImpactComponent] = []
var damage_events: Array[Dictionary] = []
var status_effect_ids: Array[StringName] = []
var terrain_effect_ids: Array[StringName] = []
var attack_roll_modifier_breakdown: Array[Dictionary] = []
var report_component_breakdown: Array[Dictionary] = []
var total_damage := 0
var total_healing := 0
var defeated := false


func add_component(component: MeteorSwarmImpactComponent) -> void:
	if component != null:
		components.append(component)


func add_status_effect_id(status_id: StringName) -> void:
	if status_id == &"" or status_effect_ids.has(status_id):
		return
	status_effect_ids.append(status_id)


func to_summary_dict() -> Dictionary:
	return {
		"target_unit_id": String(target_unit_id),
		"target_coord": target_coord,
		"target_faction_id": String(target_faction_id),
		"distance_from_anchor": distance_from_anchor,
		"total_damage": total_damage,
		"total_healing": total_healing,
		"defeated": defeated,
		"status_effect_ids": status_effect_ids.duplicate(),
		"component_breakdown": report_component_breakdown.duplicate(true),
	}
