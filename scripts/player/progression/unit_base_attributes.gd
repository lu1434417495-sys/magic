## 文件说明：该脚本属于单位基础属性集合相关的业务脚本，集中维护力量、敏捷、体质等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name UnitBaseAttributes
extends RefCounted

const STRENGTH: StringName = &"strength"
const AGILITY: StringName = &"agility"
const CONSTITUTION: StringName = &"constitution"
const PERCEPTION: StringName = &"perception"
const INTELLIGENCE: StringName = &"intelligence"
const WILLPOWER: StringName = &"willpower"

const BASE_ATTRIBUTE_IDS := [
	STRENGTH,
	AGILITY,
	CONSTITUTION,
	PERCEPTION,
	INTELLIGENCE,
	WILLPOWER,
]

## 字段说明：记录力量，会参与成长规则判定、序列化和界面展示。
var strength := 0
## 字段说明：记录敏捷，会参与成长规则判定、序列化和界面展示。
var agility := 0
## 字段说明：记录体质，会参与成长规则判定、序列化和界面展示。
var constitution := 0
## 字段说明：记录感知，会参与成长规则判定、序列化和界面展示。
var perception := 0
## 字段说明：记录智力，会参与成长规则判定、序列化和界面展示。
var intelligence := 0
## 字段说明：记录意志，会参与成长规则判定、序列化和界面展示。
var willpower := 0
## 字段说明：缓存自定义统计字典，集中保存可按键查询的运行时数据。
var custom_stats: Dictionary = {}


func get_attribute_value(attribute_id: StringName) -> int:
	match attribute_id:
		STRENGTH:
			return strength
		AGILITY:
			return agility
		CONSTITUTION:
			return constitution
		PERCEPTION:
			return perception
		INTELLIGENCE:
			return intelligence
		WILLPOWER:
			return willpower
		_:
			return int(custom_stats.get(attribute_id, 0))


func set_attribute_value(attribute_id: StringName, value: int) -> void:
	match attribute_id:
		STRENGTH:
			strength = value
		AGILITY:
			agility = value
		CONSTITUTION:
			constitution = value
		PERCEPTION:
			perception = value
		INTELLIGENCE:
			intelligence = value
		WILLPOWER:
			willpower = value
		_:
			custom_stats[attribute_id] = value


func get_all_base_attribute_ids() -> Array[StringName]:
	return ProgressionDataUtils.to_string_name_array(BASE_ATTRIBUTE_IDS)


func to_dict() -> Dictionary:
	return {
		"strength": strength,
		"agility": agility,
		"constitution": constitution,
		"perception": perception,
		"intelligence": intelligence,
		"willpower": willpower,
		"custom_stats": ProgressionDataUtils.string_name_int_map_to_string_dict(custom_stats),
	}


static func from_dict(data: Dictionary) -> UnitBaseAttributes:
	var attributes := UnitBaseAttributes.new()
	attributes.strength = int(data.get("strength", 0))
	attributes.agility = int(data.get("agility", 0))
	attributes.constitution = int(data.get("constitution", 0))
	attributes.perception = int(data.get("perception", 0))
	attributes.intelligence = int(data.get("intelligence", 0))
	attributes.willpower = int(data.get("willpower", 0))
	attributes.custom_stats = ProgressionDataUtils.to_string_name_int_map(data.get("custom_stats", {}))
	return attributes
