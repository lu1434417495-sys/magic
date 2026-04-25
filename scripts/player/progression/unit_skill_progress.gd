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
	var progress := UNIT_SKILL_PROGRESS_SCRIPT.new()
	progress.skill_id = ProgressionDataUtils.to_string_name(data.get("skill_id", ""))
	progress.is_learned = bool(data.get("is_learned", false))
	progress.skill_level = int(data.get("skill_level", 0))
	progress.current_mastery = int(data.get("current_mastery", 0))
	progress.total_mastery_earned = int(data.get("total_mastery_earned", 0))
	progress.is_core = bool(data.get("is_core", false))
	progress.assigned_profession_id = ProgressionDataUtils.to_string_name(data.get("assigned_profession_id", ""))
	progress.merged_from_skill_ids = ProgressionDataUtils.to_string_name_array(data.get("merged_from_skill_ids", []))
	progress.mastery_from_training = int(data.get("mastery_from_training", 0))
	progress.mastery_from_battle = int(data.get("mastery_from_battle", 0))
	progress.profession_granted_by = ProgressionDataUtils.to_string_name(data.get("profession_granted_by", ""))
	progress.core_max_growth_claimed = bool(data.get("core_max_growth_claimed", false))
	return progress
