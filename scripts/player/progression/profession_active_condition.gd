class_name ProfessionActiveCondition
extends Resource

@export var condition_type: StringName = &"attribute_range"
@export var attribute_id: StringName = &""
@export var state_id: StringName = &""
@export var min_value := 0
@export var max_value := 0


func matches_value(value: int) -> bool:
	return value >= min_value and value <= max_value
