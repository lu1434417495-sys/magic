class_name ProgressionDataUtils
extends RefCounted


static func to_string_name(value: Variant) -> StringName:
	if value == null:
		return &""

	var text := str(value)
	if text == "<null>":
		return &""

	return StringName(text)


static func string_name_to_string(value: StringName) -> String:
	return String(value)


static func to_string_name_array(values: Variant) -> Array[StringName]:
	var result: Array[StringName] = []
	if values is not Array:
		return result

	for value in values:
		var text := str(value)
		if text.is_empty() or text == "<null>":
			continue
		result.append(StringName(text))

	return result


static func string_name_array_to_string_array(values: Array[StringName]) -> Array[String]:
	var result: Array[String] = []
	for value in values:
		result.append(String(value))
	return result


static func to_string_name_int_map(values: Variant) -> Dictionary:
	var result: Dictionary = {}
	if values is not Dictionary:
		return result

	for key in values.keys():
		result[to_string_name(key)] = int(values[key])

	return result


static func string_name_int_map_to_string_dict(values: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	for key in values.keys():
		result[String(key)] = int(values[key])
	return result


static func to_string_name_array_map(values: Variant) -> Dictionary:
	var result: Dictionary = {}
	if values is not Dictionary:
		return result

	for key in values.keys():
		result[to_string_name(key)] = to_string_name_array(values[key])

	return result


static func string_name_array_map_to_string_dict(values: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	for key in values.keys():
		result[String(key)] = string_name_array_to_string_array(values[key])
	return result


static func sorted_string_keys(values: Dictionary) -> Array[String]:
	var result: Array[String] = []
	for key in values.keys():
		result.append(str(key))
	result.sort()
	return result

