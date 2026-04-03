class_name AttributeRequirement
extends Resource

@export var attribute_id: StringName = &""
@export var min_value := 0
@export var max_value := 0


func matches_value(value: int) -> bool:
	return value >= min_value and value <= max_value

