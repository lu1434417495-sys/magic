class_name BattleBarrierLayerState
extends RefCounted

var layer_id: StringName = &""
var display_name := ""
var order := 0
var broken := false
var blocked_categories: Array[StringName] = []
var breaker_skill_ids: Array[StringName] = []
var passage_outcomes: Array[Dictionary] = []


func to_runtime_dict() -> Dictionary:
	return {
		"layer_id": String(layer_id),
		"display_name": display_name,
		"order": int(order),
		"broken": bool(broken),
		"blocked_categories": _string_array(blocked_categories),
		"breaker_skill_ids": _string_array(breaker_skill_ids),
		"passage_outcomes": passage_outcomes.duplicate(true),
	}


func _string_array(values: Array) -> Array:
	var result: Array = []
	for value in values:
		var text := String(value)
		if not text.is_empty():
			result.append(text)
	return result
