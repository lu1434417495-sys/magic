class_name ReputationRequirement
extends Resource

@export var state_id: StringName = &""
@export var min_value := 0
@export var max_value := 0


func matches_value(value: int) -> bool:
	return value >= min_value and value <= max_value
