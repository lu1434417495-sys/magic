class_name BattleBarrierInstanceState
extends RefCounted

var barrier_instance_id: StringName = &""
var profile_id: StringName = &""
var display_name := ""
var source_unit_id: StringName = &""
var source_skill_id: StringName = &""
var anchor_mode: StringName = &"fixed"
var anchor_coord := Vector2i.ZERO
var radius_cells := 0
var area_pattern: StringName = &"diamond"
var remaining_tu := 0
var created_tu := 0
var save_dc := 0
var catch_all_projected_effects := false
var layers: Array[Dictionary] = []


func to_runtime_dict() -> Dictionary:
	return {
		"barrier_instance_id": String(barrier_instance_id),
		"profile_id": String(profile_id),
		"display_name": display_name,
		"source_unit_id": String(source_unit_id),
		"source_skill_id": String(source_skill_id),
		"anchor_mode": String(anchor_mode),
		"anchor_coord": anchor_coord,
		"radius_cells": int(radius_cells),
		"area_pattern": String(area_pattern),
		"remaining_tu": int(remaining_tu),
		"created_tu": int(created_tu),
		"save_dc": int(save_dc),
		"catch_all_projected_effects": bool(catch_all_projected_effects),
		"layers": layers.duplicate(true),
	}
