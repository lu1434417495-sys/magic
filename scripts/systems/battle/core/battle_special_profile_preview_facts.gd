class_name BattleSpecialProfilePreviewFacts
extends RefCounted

var profile_id: StringName = &""
var skill_id: StringName = &""
var preview_fact_id: StringName = &""
var nominal_plan_signature: String = ""
var final_plan_signature: String = ""
var resolved_anchor_coord: Vector2i = Vector2i(-1, -1)
var target_unit_ids: Array[StringName] = []
var target_coords: Array[Vector2i] = []
var terrain_summary: Dictionary = {}
var friendly_fire_numeric_summary: Array[Dictionary] = []
var attack_roll_modifier_breakdown: Array[Dictionary] = []


func to_dict() -> Dictionary:
	return {
		"profile_id": String(profile_id),
		"skill_id": String(skill_id),
		"preview_fact_id": String(preview_fact_id),
		"nominal_plan_signature": nominal_plan_signature,
		"final_plan_signature": final_plan_signature,
		"resolved_anchor_coord": resolved_anchor_coord,
		"target_unit_ids": target_unit_ids.duplicate(),
		"target_coords": target_coords.duplicate(),
		"terrain_summary": terrain_summary.duplicate(true),
		"friendly_fire_numeric_summary": friendly_fire_numeric_summary.duplicate(true),
		"attack_roll_modifier_breakdown": attack_roll_modifier_breakdown.duplicate(true),
	}


func get_friendly_fire_numeric_summary() -> Array[Dictionary]:
	return friendly_fire_numeric_summary.duplicate(true)
