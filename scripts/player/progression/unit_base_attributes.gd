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
const ACTION_THRESHOLD: StringName = &"action_threshold"
const HIDDEN_LUCK_AT_BIRTH: StringName = &"hidden_luck_at_birth"
const FAITH_LUCK_BONUS: StringName = &"faith_luck_bonus"

const EFFECTIVE_LUCK_MIN := -6
const EFFECTIVE_LUCK_MAX := 7
const DROP_LUCK_MAX := 5
const COMBAT_LUCK_SCORE_MAX := 4

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


static func get_all_base_attribute_ids() -> Array[StringName]:
	return ProgressionDataUtils.to_string_name_array(BASE_ATTRIBUTE_IDS)


func get_hidden_luck_at_birth() -> int:
	return get_attribute_value(HIDDEN_LUCK_AT_BIRTH)


func get_faith_luck_bonus() -> int:
	return get_attribute_value(FAITH_LUCK_BONUS)


func get_effective_luck() -> int:
	return clampi(
		get_hidden_luck_at_birth() + get_faith_luck_bonus(),
		EFFECTIVE_LUCK_MIN,
		EFFECTIVE_LUCK_MAX
	)


func get_combat_luck_score() -> int:
	var positive_hidden_luck := maxi(0, get_hidden_luck_at_birth())
	var positive_faith_luck := maxi(0, get_faith_luck_bonus())
	return mini(COMBAT_LUCK_SCORE_MAX, positive_hidden_luck + int(positive_faith_luck / 2.0))


func get_drop_luck() -> int:
	return clampi(get_effective_luck(), EFFECTIVE_LUCK_MIN, DROP_LUCK_MAX)


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
	for field_name in [
		"strength",
		"agility",
		"constitution",
		"perception",
		"intelligence",
		"willpower",
		"custom_stats",
	]:
		if not data.has(field_name):
			return null
	var custom_stats_variant: Variant = data["custom_stats"]
	if custom_stats_variant is not Dictionary:
		return null
	for attribute_id in BASE_ATTRIBUTE_IDS:
		if data[String(attribute_id)] is not int:
			return null
	var parsed_custom_stats = _parse_int_map(custom_stats_variant)
	if parsed_custom_stats == null:
		return null

	var attributes := UnitBaseAttributes.new()
	attributes.strength = int(data["strength"])
	attributes.agility = int(data["agility"])
	attributes.constitution = int(data["constitution"])
	attributes.perception = int(data["perception"])
	attributes.intelligence = int(data["intelligence"])
	attributes.willpower = int(data["willpower"])
	attributes.custom_stats = parsed_custom_stats
	return attributes


static func _parse_int_map(values: Dictionary):
	var parsed_values: Dictionary = {}
	var seen_keys: Dictionary = {}
	for raw_key in values.keys():
		var key_type := typeof(raw_key)
		if key_type != TYPE_STRING and key_type != TYPE_STRING_NAME:
			return null
		var parsed_key := ProgressionDataUtils.to_string_name(raw_key)
		if parsed_key == &"" or seen_keys.has(parsed_key):
			return null
		if values[raw_key] is not int:
			return null
		seen_keys[parsed_key] = true
		parsed_values[parsed_key] = int(values[raw_key])
	return parsed_values
