class_name BarrierLayerDef
extends Resource

const BarrierOutcomeDef = preload("res://scripts/player/progression/barrier_outcome_def.gd")

@export var layer_id: StringName = &""
@export var display_name := ""
@export var order := 0
@export var blocked_categories: Array[StringName] = []
@export var breaker_skill_ids: Array[StringName] = []
@export var passage_outcomes: Array[BarrierOutcomeDef] = []


func to_runtime_dict(default_save_dc: int = 0) -> Dictionary:
	var outcomes: Array = []
	for outcome in passage_outcomes:
		if outcome == null:
			continue
		outcomes.append(outcome.to_runtime_dict(default_save_dc))
	return {
		"layer_id": String(layer_id),
		"display_name": display_name,
		"order": int(order),
		"broken": false,
		"blocked_categories": _string_array(blocked_categories),
		"breaker_skill_ids": _string_array(breaker_skill_ids),
		"passage_outcomes": outcomes,
	}


func _string_array(values: Array) -> Array:
	var result: Array = []
	for value in values:
		var text := String(value)
		if not text.is_empty():
			result.append(text)
	return result
