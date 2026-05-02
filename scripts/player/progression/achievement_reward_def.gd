## 文件说明：该脚本属于成就奖励定义相关的定义资源脚本，集中维护奖励对象类型、目标唯一标识、目标标签等顶层字段。
## 审查重点：重点核对字段命名、默认值、配置含义以及它们与存档结构、规则判定之间的对应关系。
## 备注：后续如果调整字段语义，需要同步检查资源配置、序列化逻辑和所有读取方。

class_name AchievementRewardDef
extends RefCounted

const ACHIEVEMENT_REWARD_DEF_SCRIPT = preload("res://scripts/player/progression/achievement_reward_def.gd")

const TYPE_KNOWLEDGE_UNLOCK: StringName = &"knowledge_unlock"
const TYPE_SKILL_UNLOCK: StringName = &"skill_unlock"
const TYPE_SKILL_MASTERY: StringName = &"skill_mastery"
const TYPE_ATTRIBUTE_DELTA: StringName = &"attribute_delta"

const REQUIRED_SERIALIZED_FIELDS := [
	"reward_type",
	"target_id",
	"target_label",
	"amount",
	"reason_text",
]

## 字段说明：记录奖励对象类型，用于区分不同规则、资源类别或行为分支。
var reward_type: StringName = &""
## 字段说明：记录目标唯一标识，作为查表、序列化和跨系统引用时使用的主键。
var target_id: StringName = &""
## 字段说明：记录目标标签，会参与成长规则判定、序列化和界面展示。
var target_label := ""
## 字段说明：记录数量，会参与成长规则判定、序列化和界面展示。
var amount := 0
## 字段说明：记录原因文本，会参与成长规则判定、序列化和界面展示。
var reason_text := ""


func is_empty() -> bool:
	return reward_type == &"" or target_id == &"" or amount == 0


func to_dict() -> Dictionary:
	return {
		"reward_type": String(reward_type),
		"target_id": String(target_id),
		"target_label": target_label,
		"amount": amount,
		"reason_text": reason_text,
	}


static func from_dict(data):
	if data is not Dictionary:
		return null
	if not _has_exact_serialized_fields(data):
		return null
	var reward_type = _parse_string_name_field(data["reward_type"], false)
	var target_id = _parse_string_name_field(data["target_id"], false)
	if reward_type == null or target_id == null:
		return null
	if data["target_label"] is not String or data["reason_text"] is not String:
		return null
	var amount_variant: Variant = data["amount"]
	if amount_variant is not int or int(amount_variant) == 0:
		return null

	var reward = ACHIEVEMENT_REWARD_DEF_SCRIPT.new()
	reward.reward_type = reward_type
	reward.target_id = target_id
	reward.target_label = String(data["target_label"])
	reward.amount = int(amount_variant)
	reward.reason_text = String(data["reason_text"])
	return reward


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
