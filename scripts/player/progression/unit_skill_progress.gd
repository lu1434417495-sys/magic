## 文件说明：该脚本属于单位技能进度相关的业务脚本，集中维护技能唯一标识、是否已学会、技能等级等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name UnitSkillProgress
extends RefCounted

const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")

const GRANTED_SOURCE_PLAYER: StringName = &"player"
const GRANTED_SOURCE_PROFESSION: StringName = &"profession"
const GRANTED_SOURCE_RACE: StringName = &"race"
const GRANTED_SOURCE_SUBRACE: StringName = &"subrace"
const GRANTED_SOURCE_ASCENSION: StringName = &"ascension"
const GRANTED_SOURCE_BLOODLINE: StringName = &"bloodline"
const VALID_GRANTED_SOURCE_TYPES := {
	GRANTED_SOURCE_PLAYER: true,
	GRANTED_SOURCE_PROFESSION: true,
	GRANTED_SOURCE_RACE: true,
	GRANTED_SOURCE_SUBRACE: true,
	GRANTED_SOURCE_ASCENSION: true,
	GRANTED_SOURCE_BLOODLINE: true,
}
const TO_DICT_FIELDS: Array[String] = [
	"skill_id",
	"is_learned",
	"skill_level",
	"current_mastery",
	"total_mastery_earned",
	"is_core",
	"assigned_profession_id",
	"merged_from_skill_ids",
	"mastery_from_training",
	"mastery_from_battle",
	"profession_granted_by",
	"granted_source_type",
	"granted_source_id",
	"core_max_growth_claimed",
	"is_level_trigger_active",
	"is_level_trigger_locked",
	"lock_awaken_tier",
	"bonus_to_hit_from_lock",
]

## 字段说明：记录技能唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var skill_id: StringName = &""
## 字段说明：用于标记当前是否处于已学会状态，避免在不合适的时机重复触发流程，会参与成长规则判定、序列化和界面展示。
var is_learned := false
## 字段说明：记录技能等级，会参与成长规则判定、序列化和界面展示。
var skill_level := 0
## 字段说明：记录当前熟练度，会参与成长规则判定、序列化和界面展示。
var current_mastery := 0
## 字段说明：记录总量熟练度已获得，会参与成长规则判定、序列化和界面展示。
var total_mastery_earned := 0
## 字段说明：用于标记当前是否处于核心状态，避免在不合适的时机重复触发流程，会参与成长规则判定、序列化和界面展示。
var is_core := false
## 字段说明：记录已指派职业唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var assigned_profession_id: StringName = &""
## 字段说明：保存已合并技能标识列表，便于批量遍历、交叉查找和界面展示。
var merged_from_skill_ids: Array[StringName] = []
## 字段说明：记录熟练度训练，会参与成长规则判定、序列化和界面展示。
var mastery_from_training := 0
## 字段说明：记录熟练度战斗，会参与成长规则判定、序列化和界面展示。
var mastery_from_battle := 0
## 字段说明：记录职业授予来源，会参与成长规则判定、序列化和界面展示。
var profession_granted_by: StringName = &""
## 字段说明：记录技能授予来源类型，供种族 / 亚种 / 升华等来源可追踪。
var granted_source_type: StringName = GRANTED_SOURCE_PLAYER
## 字段说明：记录技能授予来源 id；玩家来源允许为空。
var granted_source_id: StringName = &""
## 字段说明：标记该技能达到核心最高等级后的属性进度奖励是否已入队，避免重复发放。
var core_max_growth_claimed := false
## 字段说明：标记当前是否作为唯一激活中的升级触发核心技能。
var is_level_trigger_active := false
## 字段说明：标记是否已完成过一次等级触发并进入锁定态。
var is_level_trigger_locked := false
## 字段说明：记录已完成几次锁定成长阶段。
var lock_awaken_tier := 0
## 字段说明：锁定后提供的命中修正，默认 +1。
var bonus_to_hit_from_lock := 1


func is_max_level(max_level: int) -> bool:
	return skill_level >= max_level


func clear_profession_assignment() -> void:
	assigned_profession_id = &""


func to_dict() -> Dictionary:
	return {
		"skill_id": String(skill_id),
		"is_learned": is_learned,
		"skill_level": skill_level,
		"current_mastery": current_mastery,
		"total_mastery_earned": total_mastery_earned,
		"is_core": is_core,
		"assigned_profession_id": String(assigned_profession_id),
		"merged_from_skill_ids": ProgressionDataUtils.string_name_array_to_string_array(merged_from_skill_ids),
		"mastery_from_training": mastery_from_training,
		"mastery_from_battle": mastery_from_battle,
		"profession_granted_by": String(profession_granted_by),
		"granted_source_type": String(granted_source_type),
		"granted_source_id": String(granted_source_id),
		"core_max_growth_claimed": core_max_growth_claimed,
		"is_level_trigger_active": is_level_trigger_active,
		"is_level_trigger_locked": is_level_trigger_locked,
		"lock_awaken_tier": lock_awaken_tier,
		"bonus_to_hit_from_lock": bonus_to_hit_from_lock,
	}


static func from_dict(data: Dictionary):
	if not _has_exact_fields(data, TO_DICT_FIELDS):
		return null
	var merged_from_skill_ids_variant: Variant = data["merged_from_skill_ids"]
	if merged_from_skill_ids_variant is not Array:
		return null
	var skill_id = _parse_string_name_field(data["skill_id"], false)
	if skill_id == null:
		return null
	for bool_field in ["is_learned", "is_core", "core_max_growth_claimed", "is_level_trigger_active", "is_level_trigger_locked"]:
		if data[bool_field] is not bool:
			return null
	for int_field in ["skill_level", "current_mastery", "total_mastery_earned", "mastery_from_training", "mastery_from_battle", "lock_awaken_tier", "bonus_to_hit_from_lock"]:
		var int_value: Variant = data[int_field]
		if int_value is not int or int(int_value) < 0:
			return null
	var assigned_profession_id = _parse_string_name_field(data["assigned_profession_id"], true)
	if assigned_profession_id == null:
		return null
	var profession_granted_by = _parse_string_name_field(data["profession_granted_by"], true)
	if profession_granted_by == null:
		return null
	var granted_source_type = _parse_string_name_field(data["granted_source_type"], false)
	if granted_source_type == null or not VALID_GRANTED_SOURCE_TYPES.has(granted_source_type):
		return null
	var granted_source_id = _parse_string_name_field(data["granted_source_id"], granted_source_type == GRANTED_SOURCE_PLAYER)
	if granted_source_id == null:
		return null
	var merged_from_skill_ids = _parse_unique_string_name_array(merged_from_skill_ids_variant)
	if merged_from_skill_ids == null:
		return null

	var progress := UNIT_SKILL_PROGRESS_SCRIPT.new()
	progress.skill_id = skill_id
	progress.is_learned = data["is_learned"]
	progress.skill_level = int(data["skill_level"])
	progress.current_mastery = int(data["current_mastery"])
	progress.total_mastery_earned = int(data["total_mastery_earned"])
	progress.is_core = data["is_core"]
	progress.assigned_profession_id = assigned_profession_id
	progress.merged_from_skill_ids = merged_from_skill_ids
	progress.mastery_from_training = int(data["mastery_from_training"])
	progress.mastery_from_battle = int(data["mastery_from_battle"])
	progress.profession_granted_by = profession_granted_by
	progress.granted_source_type = granted_source_type
	progress.granted_source_id = granted_source_id
	progress.core_max_growth_claimed = data["core_max_growth_claimed"]
	progress.is_level_trigger_active = data["is_level_trigger_active"]
	progress.is_level_trigger_locked = data["is_level_trigger_locked"]
	progress.lock_awaken_tier = int(data["lock_awaken_tier"])
	progress.bonus_to_hit_from_lock = int(data["bonus_to_hit_from_lock"])
	return progress


static func _parse_string_name_field(value: Variant, allow_empty: bool):
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return null
	var parsed_value := ProgressionDataUtils.to_string_name(value)
	if parsed_value == &"" and not allow_empty:
		return null
	return parsed_value


static func _parse_unique_string_name_array(values: Array):
	var parsed_values: Array[StringName] = []
	var seen_values: Dictionary = {}
	for raw_value in values:
		var parsed_value = _parse_string_name_field(raw_value, false)
		if parsed_value == null or seen_values.has(parsed_value):
			return null
		seen_values[parsed_value] = true
		parsed_values.append(parsed_value)
	return parsed_values


static func _has_exact_fields(data: Dictionary, expected_fields: Array[String]) -> bool:
	if data.size() != expected_fields.size():
		return false
	var expected_lookup: Dictionary = {}
	var seen_lookup: Dictionary = {}
	for field_name in expected_fields:
		expected_lookup[field_name] = true
	for key in data.keys():
		var key_type := typeof(key)
		if key_type != TYPE_STRING and key_type != TYPE_STRING_NAME:
			return false
		var key_string := String(key)
		if not expected_lookup.has(key_string):
			return false
		if seen_lookup.has(key_string):
			return false
		seen_lookup[key_string] = true
	return seen_lookup.size() == expected_lookup.size()
