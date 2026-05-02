## 文件说明：该脚本属于成就定义相关的定义资源脚本，集中维护成就唯一标识、显示名称、描述等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name AchievementDef
extends RefCounted

const ACHIEVEMENT_DEF_SCRIPT = preload("res://scripts/player/progression/achievement_def.gd")
const ACHIEVEMENT_REWARD_DEF_SCRIPT = preload("res://scripts/player/progression/achievement_reward_def.gd")
const AchievementRewardDef = ACHIEVEMENT_REWARD_DEF_SCRIPT

const REQUIRED_SERIALIZED_FIELDS := [
	"achievement_id",
	"display_name",
	"description",
	"event_type",
	"subject_id",
	"threshold",
	"rewards",
]

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


static func from_dict(data):
	if data is not Dictionary:
		return null
	if not _has_exact_serialized_fields(data):
		return null
	var achievement_id = _parse_string_name_field(data["achievement_id"], false)
	var event_type = _parse_string_name_field(data["event_type"], false)
	var subject_id = _parse_string_name_field(data["subject_id"], true)
	if achievement_id == null or event_type == null or subject_id == null:
		return null
	if data["display_name"] is not String or data["description"] is not String:
		return null
	var threshold_variant: Variant = data["threshold"]
	if threshold_variant is not int or int(threshold_variant) <= 0:
		return null
	var rewards_variant: Variant = data["rewards"]
	if rewards_variant is not Array:
		return null

	var achievement = ACHIEVEMENT_DEF_SCRIPT.new()
	achievement.achievement_id = achievement_id
	achievement.display_name = String(data["display_name"])
	achievement.description = String(data["description"])
	achievement.event_type = event_type
	achievement.subject_id = subject_id
	achievement.threshold = int(threshold_variant)

	for reward_data in rewards_variant:
		var reward = ACHIEVEMENT_REWARD_DEF_SCRIPT.from_dict(reward_data)
		if reward == null:
			return null
		achievement.rewards.append(reward)

	return achievement


static func _has_exact_serialized_fields(payload: Dictionary) -> bool:
	if payload.size() != REQUIRED_SERIALIZED_FIELDS.size():
		return false
	for field_name in REQUIRED_SERIALIZED_FIELDS:
		if not payload.has(field_name):
			return false
	return true


static func _parse_string_name_field(value: Variant, allow_empty: bool):
	var value_type := typeof(value)
	if value_type != TYPE_STRING and value_type != TYPE_STRING_NAME:
		return null
	var parsed_value := ProgressionDataUtils.to_string_name(value)
	if parsed_value == &"" and not allow_empty:
		return null
	return parsed_value
