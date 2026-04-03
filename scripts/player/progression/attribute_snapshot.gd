class_name AttributeSnapshot
extends RefCounted

var _values: Dictionary = {}


func set_value(attribute_id: StringName, value: int) -> void:
	_values[attribute_id] = value


func get_value(attribute_id: StringName) -> int:
	return int(_values.get(attribute_id, 0))


func has_value(attribute_id: StringName) -> bool:
	return _values.has(attribute_id)


func get_all_values() -> Dictionary:
	return _values.duplicate(true)


func to_dict() -> Dictionary:
	return ProgressionDataUtils.string_name_int_map_to_string_dict(_values)
