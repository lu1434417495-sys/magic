## 文件说明：该脚本属于属性快照相关的业务脚本，集中维护数值表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name AttributeSnapshot
extends RefCounted

## 字段说明：缓存数值表字典，集中保存可按键查询的运行时数据。
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
