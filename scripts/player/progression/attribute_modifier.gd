class_name AttributeModifier
extends Resource

const MODE_FLAT: StringName = &"flat"
const MODE_PERCENT: StringName = &"percent"

@export var attribute_id: StringName = &""
@export var mode: StringName = MODE_FLAT
@export var value := 0
@export var value_per_rank := 0
@export var source_type: StringName = &""
@export var source_id: StringName = &""


func get_value_for_rank(rank: int) -> int:
	var normalized_rank := maxi(rank, 1)
	return value + value_per_rank * (normalized_rank - 1)


func is_percent() -> bool:
	return mode == MODE_PERCENT


func is_flat() -> bool:
	return not is_percent()
