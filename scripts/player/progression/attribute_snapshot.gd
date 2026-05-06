## 文件说明：该脚本属于属性快照相关的业务脚本，集中维护数值表等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name AttributeSnapshot
extends RefCounted

const STRENGTH_MODIFIER: StringName = &"strength_modifier"
const AGILITY_MODIFIER: StringName = &"agility_modifier"
const CONSTITUTION_MODIFIER: StringName = &"constitution_modifier"
const PERCEPTION_MODIFIER: StringName = &"perception_modifier"
const INTELLIGENCE_MODIFIER: StringName = &"intelligence_modifier"
const WILLPOWER_MODIFIER: StringName = &"willpower_modifier"

## 字段说明：缓存数值表字典，集中保存可按键查询的运行时数据。
var _values: Dictionary = {}


func set_value(attribute_id: StringName, value: int) -> void:
	_values[attribute_id] = value
	var modifier_id := get_base_attribute_modifier_id(attribute_id)
	if modifier_id != &"":
		_values[modifier_id] = calculate_score_modifier(value)


func get_value(attribute_id: StringName) -> int:
	return int(_values.get(attribute_id, 0))


func has_value(attribute_id: StringName) -> bool:
	return _values.has(attribute_id)


func get_all_values() -> Dictionary:
	return _values.duplicate(true)


func to_dict() -> Dictionary:
	return ProgressionDataUtils.string_name_int_map_to_string_dict(_values)


static func get_base_attribute_modifier_id(attribute_id: StringName) -> StringName:
	match attribute_id:
		UnitBaseAttributes.STRENGTH:
			return STRENGTH_MODIFIER
		UnitBaseAttributes.AGILITY:
			return AGILITY_MODIFIER
		UnitBaseAttributes.CONSTITUTION:
			return CONSTITUTION_MODIFIER
		UnitBaseAttributes.PERCEPTION:
			return PERCEPTION_MODIFIER
		UnitBaseAttributes.INTELLIGENCE:
			return INTELLIGENCE_MODIFIER
		UnitBaseAttributes.WILLPOWER:
			return WILLPOWER_MODIFIER
		_:
			return &""


static func calculate_score_modifier(score: int) -> int:
	return int(floor(float(score - 10) / 2.0))
