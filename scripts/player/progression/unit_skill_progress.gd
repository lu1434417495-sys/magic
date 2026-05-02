## 文件说明：该脚本属于单位技能进度相关的业务脚本，集中维护技能唯一标识、是否已学会、技能等级等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name UnitSkillProgress
extends RefCounted

const UNIT_SKILL_PROGRESS_SCRIPT = preload("res://scripts/player/progression/unit_skill_progress.gd")

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
## 字段说明：标记该技能达到核心最高等级后的属性进度奖励是否已入队，避免重复发放。
var core_max_growth_claimed := false


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
		"core_max_growth_claimed": core_max_growth_claimed,
	}


static func from_dict(data: Dictionary):
	for field_name in [
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
		"core_max_growth_claimed",
	]:
		if not data.has(field_name):
			return null
	var merged_from_skill_ids_variant: Variant = data["merged_from_skill_ids"]
	if merged_from_skill_ids_variant is not Array:
		return null
	var skill_id = _parse_string_name_field(data["skill_id"], false)
	if skill_id == null:
		return null
	for bool_field in ["is_learned", "is_core", "core_max_growth_claimed"]:
		if data[bool_field] is not bool:
			return null
	for int_field in ["skill_level", "current_mastery", "total_mastery_earned", "mastery_from_training", "mastery_from_battle"]:
		var int_value: Variant = data[int_field]
		if int_value is not int or int(int_value) < 0:
			return null
	var assigned_profession_id = _parse_string_name_field(data["assigned_profession_id"], true)
	if assigned_profession_id == null:
		return null
	var profession_granted_by = _parse_string_name_field(data["profession_granted_by"], true)
	if profession_granted_by == null:
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
	progress.core_max_growth_claimed = data["core_max_growth_claimed"]
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
