class_name EnemyAiBrainDef
extends RefCounted

var brain_id: StringName = &""
var default_state_id: StringName = &"engage"
var retreat_hp_ratio := 0.35
var support_hp_ratio := 0.55
var pressure_distance := 2
var states: Dictionary = {}


func get_state(state_id: StringName):
	return states.get(state_id)


func has_state(state_id: StringName) -> bool:
	return states.has(state_id)
