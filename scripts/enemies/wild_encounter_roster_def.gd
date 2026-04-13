class_name WildEncounterRosterDef
extends RefCounted


var profile_id: StringName = &""
var display_name: String = ""
var initial_stage := 0
var growth_step_interval := 1
var suppression_steps_on_victory := 0
var stages: Array[Dictionary] = []


func get_max_stage() -> int:
	var max_stage := initial_stage
	for stage_data in stages:
		if stage_data is not Dictionary:
			continue
		max_stage = maxi(max_stage, int(stage_data.get("stage", initial_stage)))
	return max_stage


func get_stage_unit_entries(stage: int) -> Array[Dictionary]:
	var best_stage := -1
	var best_entries: Array[Dictionary] = []
	for stage_variant in stages:
		if stage_variant is not Dictionary:
			continue
		var stage_data: Dictionary = stage_variant
		var stage_index := int(stage_data.get("stage", initial_stage))
		if stage_index > stage or stage_index < best_stage:
			continue
		var entries: Array[Dictionary] = []
		for entry_variant in stage_data.get("unit_entries", []):
			if entry_variant is not Dictionary:
				continue
			entries.append((entry_variant as Dictionary).duplicate(true))
		best_stage = stage_index
		best_entries = entries
	return best_entries
