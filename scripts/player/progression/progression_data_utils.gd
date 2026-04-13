## 文件说明：该脚本属于成长数据工具相关的业务脚本，主要封装当前领域所需的辅助逻辑。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

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

