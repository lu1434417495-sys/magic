## 文件说明：该脚本属于成就定义相关的定义资源脚本，集中维护成就唯一标识、显示名称、描述等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name AchievementDef
extends RefCounted

const ACHIEVEMENT_DEF_SCRIPT = preload("res://scripts/player/progression/achievement_def.gd")
const ACHIEVEMENT_REWARD_DEF_SCRIPT = preload("res://scripts/player/progression/achievement_reward_def.gd")
const AchievementRewardDef = ACHIEVEMENT_REWARD_DEF_SCRIPT

## 字段说明：记录成就唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var achievement_id: StringName = &""
## 字段说明：用于界面展示的名称文本，主要服务于玩家阅读和调试观察，不直接参与数值判定。
var display_name := ""
## 字段说明：用于界面说明的描述文本，帮助玩家或策划理解该对象的用途与限制。
var description := ""
## 字段说明：记录事件类型，用于区分不同规则、资源类别或行为分支。
var event_type: StringName = &""
## 字段说明：记录目标对象唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var subject_id: StringName = &""
## 字段说明：记录阈值，会参与成长规则判定、序列化和界面展示。
var threshold := 0
## 字段说明：保存奖励列表，便于顺序遍历、批量展示、批量运算和整体重建。
var rewards: Array[AchievementRewardDef] = []


func matches_event(target_event_type: StringName, target_subject_id: StringName = &"") -> bool:
	if achievement_id == &"" or event_type == &"" or threshold <= 0:
		return false
	if event_type != target_event_type:
		return false
	return subject_id == &"" or subject_id == target_subject_id


func is_empty() -> bool:
	return achievement_id == &"" or event_type == &"" or threshold <= 0


func to_dict() -> Dictionary:
	var reward_data: Array[Dictionary] = []
	for reward in rewards:
		if reward == null:
			continue
		reward_data.append(reward.to_dict())

	return {
		"achievement_id": String(achievement_id),
		"display_name": display_name,
		"description": description,
		"event_type": String(event_type),
		"subject_id": String(subject_id),
		"threshold": threshold,
		"rewards": reward_data,
	}


static func from_dict(data: Dictionary):
	var achievement = ACHIEVEMENT_DEF_SCRIPT.new()
	achievement.achievement_id = ProgressionDataUtils.to_string_name(data.get("achievement_id", ""))
	achievement.display_name = String(data.get("display_name", ""))
	achievement.description = String(data.get("description", ""))
	achievement.event_type = ProgressionDataUtils.to_string_name(data.get("event_type", ""))
	achievement.subject_id = ProgressionDataUtils.to_string_name(data.get("subject_id", ""))
	achievement.threshold = int(data.get("threshold", 0))

	var rewards_variant: Variant = data.get("rewards", [])
	if rewards_variant is Array:
		for reward_data in rewards_variant:
			if reward_data is not Dictionary:
				continue
			achievement.rewards.append(ACHIEVEMENT_REWARD_DEF_SCRIPT.from_dict(reward_data))

	return achievement
